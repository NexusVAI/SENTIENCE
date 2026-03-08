using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GTA5MOD2026
{
    public class VoiceManager : IDisposable
    {
        private static readonly HttpClient _http = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        private readonly ConcurrentQueue<Action> _mainQueue
            = new ConcurrentQueue<Action>();

        private static readonly string AudioDir = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments),
            "GTA5MOD2026", "audio");

        // More voice variety
        private static readonly string[] MALE_VOICES = new[]
        {
            "zh-CN-YunxiNeural",
            "zh-CN-YunjianNeural",
            "zh-CN-YunyangNeural",
        };

        private static readonly string[] FEMALE_VOICES = new[]
        {
            "zh-CN-XiaoxiaoNeural",
            "zh-CN-XiaoyiNeural",
            "zh-CN-XiaohanNeural",
        };

        private readonly Random _rand = new Random();
        private readonly ConcurrentDictionary<string, string>
            _npcVoices
                = new ConcurrentDictionary<string, string>();
        private volatile bool _isPlaying = false;

        private string _ttsServer;

        public VoiceManager()
        {
            var config = ModConfig.Load();
            _ttsServer = config.TTS.TTSServer;

            if (!Directory.Exists(AudioDir))
                Directory.CreateDirectory(AudioDir);
            CleanOldAudio();
            PreWarmEdgeTTS();
        }

        /// <summary>
        /// Pre-warm Python/edge-tts to eliminate cold start
        /// </summary>
        private void PreWarmEdgeTTS()
        {
            Task.Run(() =>
            {
                try
                {
                    string testFile = Path.Combine(
                        AudioDir, "warmup.mp3");
                    var psi = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments =
                            "-m edge_tts " +
                            "--voice \"zh-CN-YunxiNeural\" " +
                            "--text \"准备就绪\" " +
                            $"--write-media \"{testFile}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (var proc = Process.Start(psi))
                    {
                        proc?.WaitForExit(10000);
                    }
                    try { File.Delete(testFile); } catch { }
                }
                catch { }
            });
        }

        public string GetVoiceForNpc(string stableId,
            bool isMale = true)
        {
            string key = string.IsNullOrWhiteSpace(stableId)
                ? "UNKNOWN"
                : stableId;
            return _npcVoices.GetOrAdd(key, _ =>
            {
                var voices = isMale
                    ? MALE_VOICES : FEMALE_VOICES;
                return voices[_rand.Next(voices.Length)];
            });
        }

        public void SpeakAsync(int npcHandle, string text,
            string voice, string emotion = "neutral",
            Action onComplete = null,
            Action<Exception> onError = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (_isPlaying) return;

            Task.Run(async () =>
            {
                _isPlaying = true;
                try
                {
                    // Try TTS server first
                    bool serverSuccess = await TryServerTTS(
                        text, voice, emotion);

                    if (!serverSuccess)
                    {
                        // Fallback to edge-tts CLI
                        await EdgeTTSFallback(
                            npcHandle, text, voice, emotion);
                    }

                    _mainQueue.Enqueue(()
                        => onComplete?.Invoke());
                }
                catch (Exception ex)
                {
                    _mainQueue.Enqueue(()
                        => onError?.Invoke(ex));
                }
                finally
                {
                    _isPlaying = false;
                }
            });
        }

        private async Task<bool> TryServerTTS(string text,
            string voice, string emotion)
        {
            try
            {
                var requestBody = new
                {
                    text = text,
                    voice = voice,
                    emotion = emotion
                };

                string json = Newtonsoft.Json.JsonConvert
                    .SerializeObject(requestBody);

                using (var content = new StringContent(
                    json, Encoding.UTF8, "application/json"))
                {
                    var resp = await _http.PostAsync(
                        _ttsServer, content)
                        .ConfigureAwait(false);

                    if (resp.IsSuccessStatusCode)
                    {
                        var body = await resp.Content
                            .ReadAsStringAsync()
                            .ConfigureAwait(false);
                        var result = JObject.Parse(body);
                        string filepath = result["file"]
                            ?.ToString();

                        if (!string.IsNullOrEmpty(filepath)
                            && File.Exists(filepath))
                        {
                            PlayAudioNAudio(filepath);
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private async Task EdgeTTSFallback(int npcHandle,
            string text, string voice, string emotion)
        {
            await Task.Run(() =>
            {
                // ===== FIXED: Much more natural SSML parameters =====
                string rate, pitch;
                switch (emotion)
                {
                    case "angry":
                        rate = "+10%";   // was +30% (too fast)
                        pitch = "-3Hz";  // was -10Hz (too deep)
                        break;
                    case "scared":
                        rate = "+15%";   // was +50% (way too fast)
                        pitch = "+5Hz";  // was +15Hz (squeaky)
                        break;
                    case "happy":
                        rate = "+5%";    // was +15%
                        pitch = "+2Hz";  // was +5Hz
                        break;
                    case "sad":
                        rate = "-10%";
                        pitch = "-2Hz";
                        break;
                    case "cold":
                        rate = "-5%";    // was -20% (too slow)
                        pitch = "-1Hz";  // was -5Hz
                        break;
                    default:
                        rate = "+0%";
                        pitch = "+0Hz";
                        break;
                }

                string filename =
                    $"npc_{npcHandle}_{DateTime.Now.Ticks}.mp3";
                string filepath = Path.Combine(AudioDir, filename);

                string args =
                    $"-m edge_tts " +
                    $"--voice \"{voice}\" " +
                    $"--rate=\"{rate}\" " +
                    $"--pitch=\"{pitch}\" " +
                    $"--text \"{EscapeText(text)}\" " +
                    $"--write-media \"{filepath}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        proc.WaitForExit(8000);
                        if (!proc.HasExited)
                            try { proc.Kill(); } catch { }
                    }
                }

                if (File.Exists(filepath))
                {
                    PlayAudioNAudio(filepath);
                }
            });
        }

        /// <summary>
        /// NAudio playback — replaces PowerShell
        /// Saves 1-2 seconds per playback
        /// </summary>
        private void PlayAudioNAudio(string filepath)
        {
            try
            {
                using (var reader
                    = new NAudio.Wave.Mp3FileReader(filepath))
                using (var waveOut
                    = new NAudio.Wave.WaveOutEvent())
                {
                    waveOut.Init(reader);
                    waveOut.Play();

                    while (waveOut.PlaybackState
                        == NAudio.Wave.PlaybackState.Playing)
                    {
                        System.Threading.Thread.Sleep(50);
                    }
                }
                try { File.Delete(filepath); } catch { }
            }
            catch { }
        }

        private string EscapeText(string text)
        {
            return text
                .Replace("\"", "'")
                .Replace("\n", " ")
                .Replace("\r", "")
                .Replace("&", "and")
                .Replace("|", " ");
        }

        private void CleanOldAudio()
        {
            try
            {
                foreach (var file in Directory.GetFiles(
                    AudioDir, "*.mp3"))
                {
                    var age = DateTime.Now
                        - File.GetCreationTime(file);
                    if (age.TotalMinutes > 10)
                        try { File.Delete(file); } catch { }
                }
            }
            catch { }
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

        public void Dispose()
        {
            CleanOldAudio();
        }
    }
}
