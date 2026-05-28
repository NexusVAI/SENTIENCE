using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        // V5.1.1 · Resolved Whisper model path (auto-discovered if config blank).
        //   May be empty string if no install was found — STT then short-circuits
        //   to a friendly error rather than launching Python in vain.
        private readonly string _modelPath;

        // V5.1.1 · Resolved python launcher command ("python" / "py" / ...).
        //   Empty if Python is not on PATH.
        private readonly string _pythonCmd;

        public SpeechManager()
        {
            var config = ModConfig.Load();
            _modelPath = ResolveWhisperModelPath(
                config?.STT?.WhisperModelPath);
            _pythonCmd = ResolvePythonCommand();

            if (!Directory.Exists(TempDir))
                Directory.CreateDirectory(TempDir);

            WriteSttScript();
        }

        // V5.1.1 · Search a priority chain for a usable whisper-tiny directory.
        //   1. Explicit config path (if set and non-empty)
        //   2. %Documents%\GTA5MOD2026\whisper-tiny\        ← preferred user data
        //   3. <GTA>\scripts\whisper-tiny\                  ← drop-in next to DLL
        //   4. <GTA>\whisper-tiny\                          ← legacy
        //   5. C:\whisper-tiny\                             ← legacy V5
        //  Returns the first directory containing a `model.bin` file, else "".
        private static string ResolveWhisperModelPath(string configPath)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(configPath))
                candidates.Add(configPath);

            string docs = Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments);
            candidates.Add(Path.Combine(docs,
                "GTA5MOD2026", "whisper-tiny"));

            string scriptsDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(scriptsDir))
            {
                candidates.Add(Path.Combine(scriptsDir, "whisper-tiny"));
                var parent = Directory.GetParent(scriptsDir);
                if (parent != null)
                    candidates.Add(Path.Combine(
                        parent.FullName, "whisper-tiny"));
            }

            candidates.Add(@"C:\whisper-tiny");

            foreach (var p in candidates)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(p)) continue;
                    if (!Directory.Exists(p)) continue;
                    if (File.Exists(Path.Combine(p, "model.bin")))
                        return p;
                }
                catch { }
            }
            return "";
        }

        // V5.1.1 · Find a Python launcher. Prefers `python` (most common),
        //  falls back to `py -3` (Windows launcher) — both signaled via the
        //  return string which is split at first space.
        private static string ResolvePythonCommand()
        {
            foreach (var cmd in new[] { "python", "py -3" })
            {
                try
                {
                    string fileName = cmd.Split(' ')[0];
                    string args = cmd.Contains(" ")
                        ? cmd.Substring(cmd.IndexOf(' ') + 1) + " --version"
                        : "--version";
                    var psi = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using (var proc = Process.Start(psi))
                    {
                        if (proc == null) continue;
                        if (proc.WaitForExit(2000) && proc.ExitCode == 0)
                            return cmd;
                        try { if (!proc.HasExited) proc.Kill(); } catch { }
                    }
                }
                catch { }
            }
            return "";
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

            // V5.1.1 · Short-circuit with actionable errors BEFORE spawning
            // a process — saves the user 5 seconds of silent waiting.
            if (string.IsNullOrEmpty(_modelPath))
            {
                onError?.Invoke(new Exception(
                    "找不到 whisper-tiny 模型。请把 whisper-tiny 文件夹放到 " +
                    "%USERPROFILE%\\Documents\\GTA5MOD2026\\ 下，或在 config.ini " +
                    "的 [STT] 段设置 WhisperModelPath=完整路径"));
                return;
            }
            if (string.IsNullOrEmpty(_pythonCmd))
            {
                onError?.Invoke(new Exception(
                    "找不到 Python。请安装 Python 3.10+ 并把它加入 PATH，" +
                    "然后执行: pip install sounddevice numpy faster-whisper"));
                return;
            }

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

                    // V5.1.1 · Use resolved python command (supports `py -3`).
                    string pyExe = _pythonCmd.Split(' ')[0];
                    string pyPrefix = _pythonCmd.Contains(" ")
                        ? _pythonCmd.Substring(_pythonCmd.IndexOf(' ') + 1) + " "
                        : "";
                    var psi = new ProcessStartInfo
                    {
                        FileName = pyExe,
                        Arguments = pyPrefix + $"\"{scriptPath}\"",
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
                                    new Exception("Python 启动失败 (Process.Start 返回 null)")));
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
