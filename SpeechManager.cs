using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace GTA5MOD2026
{
    public class SpeechManager
    {
        private readonly ConcurrentQueue<Action> _mainQueue
            = new ConcurrentQueue<Action>();

        private volatile bool _isRecording = false;
        public bool IsRecording => _isRecording;

        private static readonly string TempDir = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments),
            "GTA5MOD2026", "temp");
        private readonly string _modelPath;

        public SpeechManager()
        {
            var config = ModConfig.Load();
            _modelPath = string.IsNullOrWhiteSpace(
                config?.STT?.WhisperModelPath)
                ? @"C:\whisper-tiny"
                : config.STT.WhisperModelPath;

            if (!Directory.Exists(TempDir))
                Directory.CreateDirectory(TempDir);

            WriteSttScript();
        }

        private void WriteSttScript()
        {
            string scriptPath = Path.Combine(TempDir, "stt.py");
            string resultFile = Path.Combine(TempDir, "stt_result.txt")
                .Replace("\\", "\\\\");

            string pyScript = $@"
import sys
import os
import tempfile
import wave
import sounddevice as sd
import numpy as np

DURATION = 5
SAMPLE_RATE = 16000
MODEL_PATH = r'{_modelPath.Replace("\\", "\\\\")}'
RESULT_FILE = r'{resultFile.Replace("\\\\", "\\")}'

print('REC_START', flush=True)
audio = sd.rec(int(DURATION * SAMPLE_RATE),
               samplerate=SAMPLE_RATE,
               channels=1, dtype='int16')
sd.wait()
print('REC_DONE', flush=True)

vol = np.abs(audio).mean()
if vol < 10:
    with open(RESULT_FILE, 'w', encoding='utf-8') as f:
        f.write('ERROR:too_quiet')
    sys.exit(0)

tmp = os.path.join(tempfile.gettempdir(), 'gta_stt.wav')
with wave.open(tmp, 'wb') as wf:
    wf.setnchannels(1)
    wf.setsampwidth(2)
    wf.setframerate(SAMPLE_RATE)
    wf.writeframes(audio.tobytes())

print('TRANSCRIBING', flush=True)
try:
    os.environ['HF_HUB_OFFLINE'] = '1'
    from faster_whisper import WhisperModel
    model = WhisperModel(MODEL_PATH, device='cpu',
                         compute_type='int8')
    segments, info = model.transcribe(
        tmp, language='zh', beam_size=3, best_of=3)
    text = ''.join([seg.text for seg in segments]).strip()

    with open(RESULT_FILE, 'w', encoding='utf-8') as f:
        if text and len(text) > 0:
            f.write('RESULT:' + text)
        else:
            f.write('ERROR:no_speech')
except Exception as e:
    with open(RESULT_FILE, 'w', encoding='utf-8') as f:
        f.write('ERROR:' + str(e))

try:
    os.remove(tmp)
except:
    pass

print('DONE', flush=True)
";

            try
            {
                File.WriteAllText(scriptPath, pyScript,
                    System.Text.Encoding.UTF8);
            }
            catch { }
        }

        public void RecordAndTranscribe(
            Action<string> onResult,
            Action<Exception> onError = null)
        {
            if (_isRecording) return;

            Task.Run(() =>
            {
                _isRecording = true;
                try
                {
                    string scriptPath = Path.Combine(
                        TempDir, "stt.py");
                    string resultFile = Path.Combine(
                        TempDir, "stt_result.txt");
                    if (!File.Exists(scriptPath))
                        WriteSttScript();
                    if (File.Exists(resultFile))
                        File.Delete(resultFile);

                    var psi = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = $"\"{scriptPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8
                    };

                    using (var proc = Process.Start(psi))
                    {
                        if (proc == null)
                        {
                            _mainQueue.Enqueue(()
                                => onError?.Invoke(
                                    new Exception("Python not found")));
                            return;
                        }

                        bool exited = proc.WaitForExit(30000);

                        if (!exited)
                        {
                            try { proc.Kill(); } catch { }
                            _mainQueue.Enqueue(() =>
                                onError?.Invoke(
                                    new Exception("Timeout")));
                            return;
                        }

                        if (File.Exists(resultFile))
                        {
                            string content = File.ReadAllText(
                                resultFile,
                                System.Text.Encoding.UTF8).Trim();

                            try { File.Delete(resultFile); }
                            catch { }

                            if (content.StartsWith("RESULT:"))
                            {
                                string text = content
                                    .Substring(7).Trim();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    _mainQueue.Enqueue(() =>
                                        onResult?.Invoke(text));
                                    return;
                                }
                            }

                            string errMsg = content
                                .StartsWith("ERROR:")
                                ? content.Substring(6)
                                : "Unknown error";

                            _mainQueue.Enqueue(() =>
                                onError?.Invoke(
                                    new Exception(errMsg)));
                        }
                        else
                        {
                            _mainQueue.Enqueue(() =>
                                onError?.Invoke(
                                    new Exception("No result file")));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _mainQueue.Enqueue(()
                        => onError?.Invoke(ex));
                }
                finally
                {
                    _isRecording = false;
                }
            });
        }

        public void ProcessMainQueue()
        {
            int count = 0;
            while (_mainQueue.TryDequeue(out var action)
                && count < 3)
            {
                count++;
                try { action?.Invoke(); }
                catch { }
            }
        }
    }
}
