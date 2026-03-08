using System;
using System.IO;
using System.Text;

namespace GTA5MOD2026
{
    public class ModConfig
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments),
            "GTA5MOD2026", "config.ini");

        public LLMConfig LLM { get; set; }
            = new LLMConfig();
        public PerformanceConfig Performance { get; set; }
            = new PerformanceConfig();
        public TTSConfig TTS { get; set; }
            = new TTSConfig();
        public STTConfig STT { get; set; }
            = new STTConfig();
        public AwakeningConfig Awakening { get; set; }
            = new AwakeningConfig();

        public class LLMConfig
        {
            public string Provider { get; set; } = "local";
            public string LocalEndpoint { get; set; }
                = "http://127.0.0.1:1234/v1/chat/completions";
            // ===== CHANGED: default model =====
            public string LocalModel { get; set; }
                = "qwen2.5-3b-instruct";
            public string LightEndpoint { get; set; }
                = "http://127.0.0.1:1234/v1/chat/completions";
            public string LightModel { get; set; }
                = "qwen3.5-0.8b";
            public string CloudEndpoint { get; set; }
                = "https://api.deepseek.com/v1/chat/completions";
            public string CloudModel { get; set; }
                = "deepseek-chat";
            public string CloudAPIKey { get; set; } = "";
        }

        public class PerformanceConfig
        {
            public int MaxTokens { get; set; } = 80;
            public int MaxTokensThinking { get; set; } = 120;
            public double Temperature { get; set; } = 0.7;
            public int MaxDialogueLength { get; set; } = 45;
            public float RequestCooldown { get; set; } = 2.0f;
            public bool StrictMode { get; set; } = true;
        }

        public class TTSConfig
        {
            public string TTSProvider { get; set; } = "edge";
            public string TTSServer { get; set; }
                = "http://127.0.0.1:5111";
            public bool VoiceEnabled { get; set; } = true;
        }

        public class STTConfig
        {
            public string WhisperModelPath { get; set; }
                = @"C:\whisper-tiny";
        }

        public class AwakeningConfig
        {
            public bool Enabled { get; set; } = true;
            public int Speed { get; set; } = 2;
        }

        public static ModConfig Load()
        {
            var config = new ModConfig();

            if (!File.Exists(ConfigPath))
            {
                Save(config);
                return config;
            }

            try
            {
                string section = "";
                foreach (var line in File.ReadAllLines(
                    ConfigPath))
                {
                    string trim = line.Trim();
                    if (string.IsNullOrEmpty(trim)
                        || trim.StartsWith("#")
                        || trim.StartsWith(";"))
                        continue;

                    if (trim.StartsWith("[")
                        && trim.EndsWith("]"))
                    {
                        section = trim.Substring(
                            1, trim.Length - 2).ToUpper();
                        continue;
                    }

                    var parts = trim.Split(new[] { '=' }, 2);
                    if (parts.Length != 2) continue;

                    string key = parts[0].Trim();
                    string value = parts[1].Trim();

                    switch (section)
                    {
                        case "LLM":
                            if (key == "Provider")
                                config.LLM.Provider = value;
                            else if (key == "LocalEndpoint")
                                config.LLM.LocalEndpoint = value;
                            else if (key == "LocalModel")
                                config.LLM.LocalModel = value;
                            else if (key == "LightEndpoint")
                                config.LLM.LightEndpoint = value;
                            else if (key == "LightModel")
                                config.LLM.LightModel = value;
                            else if (key == "CloudEndpoint")
                                config.LLM.CloudEndpoint = value;
                            else if (key == "CloudModel")
                                config.LLM.CloudModel = value;
                            else if (key == "CloudAPIKey")
                                config.LLM.CloudAPIKey = value;
                            break;
                        case "PERFORMANCE":
                            if (key == "MaxTokens")
                            {
                                int.TryParse(value, out var mt);
                                config.Performance.MaxTokens =
                                    mt > 0 ? mt : 80;
                            }
                            if (key == "MaxTokensThinking")
                            {
                                int.TryParse(value, out var mtt);
                                config.Performance.MaxTokensThinking =
                                    mtt > 0 ? mtt : 120;
                            }
                            if (key == "Temperature")
                            {
                                double.TryParse(value,
                                    out var t);
                                config.Performance.Temperature =
                                    t > 0 ? t : 0.7;
                            }
                            if (key == "MaxDialogueLength")
                            {
                                int.TryParse(value,
                                    out var mdl);
                                config.Performance
                                    .MaxDialogueLength =
                                    mdl > 0 ? mdl : 45;
                            }
                            if (key == "RequestCooldown")
                            {
                                float.TryParse(value,
                                    out var rc);
                                config.Performance
                                    .RequestCooldown =
                                    rc > 0 ? rc : 2.0f;
                            }
                            if (key == "StrictMode")
                            {
                                bool.TryParse(value,
                                    out var sm);
                                config.Performance.StrictMode
                                    = sm;
                            }
                            break;
                        case "TTS":
                            if (key == "TTSProvider")
                                config.TTS.TTSProvider = value;
                            else if (key == "TTSServer")
                                config.TTS.TTSServer = value;
                            else if (key == "VoiceEnabled")
                            {
                                bool.TryParse(value,
                                    out var ve);
                                config.TTS.VoiceEnabled = ve;
                            }
                            break;
                        case "AWAKENING":
                            if (key == "Enabled")
                            {
                                bool.TryParse(value,
                                    out var ae);
                                config.Awakening.Enabled = ae;
                            }
                            else if (key == "Speed")
                            {
                                int.TryParse(value,
                                    out var s);
                                config.Awakening.Speed =
                                    s > 0 ? s : 2;
                            }
                            break;
                        case "STT":
                            if (key == "WhisperModelPath")
                                config.STT.WhisperModelPath = value;
                            break;
                    }
                }
            }
            catch { }

            return config;
        }

        public static void Save(ModConfig config)
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                sb.AppendLine("# ==========================================");
                sb.AppendLine("# Nexus V: Sentience - Configuration");
                sb.AppendLine("# ==========================================");
                sb.AppendLine("#");
                sb.AppendLine("# QUICK SETUP:");
                sb.AppendLine("# Option A: Local model (free, needs LM Studio)");
                sb.AppendLine("#   Provider=local");
                sb.AppendLine("#   Download LM Studio + any model");
                sb.AppendLine("#");
                sb.AppendLine("# Option B: Cloud API (fast, costs money)");
                sb.AppendLine("#   Provider=cloud");
                sb.AppendLine("#   Get API key from DeepSeek/OpenAI");
                sb.AppendLine("#");
                sb.AppendLine("# Option C: Any OpenAI-compatible API");
                sb.AppendLine("#   Provider=cloud");
                sb.AppendLine("#   CloudEndpoint=your_api_url");
                sb.AppendLine("#   CloudModel=model_name");
                sb.AppendLine("#   CloudAPIKey=your_key");
                sb.AppendLine("# ==========================================");
                sb.AppendLine();

                sb.AppendLine("[LLM]");
                sb.AppendLine("# Provider: local or cloud");
                sb.AppendLine($"Provider={config.LLM.Provider}");
                sb.AppendLine();
                sb.AppendLine("# --- Local (LM Studio) ---");
                sb.AppendLine($"LocalEndpoint={config.LLM.LocalEndpoint}");
                sb.AppendLine("# Recommended models:");
                sb.AppendLine("#   Weak GPU/CPU: qwen2.5-1.5b-instruct");
                sb.AppendLine("#   Mid GPU/CPU:  qwen3.5-4b");
                sb.AppendLine("#   Strong GPU:   qwen2.5-7b-instruct");
                sb.AppendLine($"LocalModel={config.LLM.LocalModel}");
                sb.AppendLine();
                sb.AppendLine("# Light model for NPC-NPC chat");
                sb.AppendLine($"LightEndpoint={config.LLM.LightEndpoint}");
                sb.AppendLine($"LightModel={config.LLM.LightModel}");
                sb.AppendLine();
                sb.AppendLine("# --- Cloud API ---");
                sb.AppendLine("# DeepSeek: https://api.deepseek.com/v1/chat/completions");
                sb.AppendLine("# OpenAI:   https://api.openai.com/v1/chat/completions");
                sb.AppendLine("# SiliconFlow: https://api.siliconflow.cn/v1/chat/completions");
                sb.AppendLine($"CloudEndpoint={config.LLM.CloudEndpoint}");
                sb.AppendLine("# DeepSeek: deepseek-chat");
                sb.AppendLine("# OpenAI:   gpt-4o-mini");
                sb.AppendLine($"CloudModel={config.LLM.CloudModel}");
                sb.AppendLine("# Get your API key from the provider website");
                sb.AppendLine($"CloudAPIKey={config.LLM.CloudAPIKey}");
                sb.AppendLine();

                sb.AppendLine("[Performance]");
                sb.AppendLine("# Lower MaxTokens = faster response");
                sb.AppendLine($"MaxTokens={config.Performance.MaxTokens}");
                sb.AppendLine("# Extra tokens for models with thinking mode (qwen3.x)");
                sb.AppendLine($"MaxTokensThinking={config.Performance.MaxTokensThinking}");
                sb.AppendLine("# 0.1-1.0, higher = more creative");
                sb.AppendLine($"Temperature={config.Performance.Temperature}");
                sb.AppendLine("# Max Chinese characters in dialogue");
                sb.AppendLine($"MaxDialogueLength={config.Performance.MaxDialogueLength}");
                sb.AppendLine("# Seconds between requests");
                sb.AppendLine($"RequestCooldown={config.Performance.RequestCooldown}");
                sb.AppendLine("# StrictMode: true=instant responses for simple talk");
                sb.AppendLine($"StrictMode={config.Performance.StrictMode}");
                sb.AppendLine();

                sb.AppendLine("[TTS]");
                sb.AppendLine("# TTSProvider: edge (free, needs python+edge-tts)");
                sb.AppendLine("# or server (custom TTS server)");
                sb.AppendLine("# or none (disable voice)");
                sb.AppendLine($"TTSProvider={config.TTS.TTSProvider}");
                sb.AppendLine($"TTSServer={config.TTS.TTSServer}");
                sb.AppendLine($"VoiceEnabled={config.TTS.VoiceEnabled}");
                sb.AppendLine();

                sb.AppendLine("[STT]");
                sb.AppendLine("# Local faster-whisper model path");
                sb.AppendLine($"WhisperModelPath={config.STT.WhisperModelPath}");
                sb.AppendLine();

                sb.AppendLine("[Awakening]");
                sb.AppendLine("# Enable NPC sentience system");
                sb.AppendLine($"Enabled={config.Awakening.Enabled}");
                sb.AppendLine("# 1=slow 2=normal 5=fast 10=very fast");
                sb.AppendLine($"Speed={config.Awakening.Speed}");

                File.WriteAllText(ConfigPath, sb.ToString(),
                    Encoding.UTF8);
            }
            catch { }
        }

        public string TTSServer => TTS.TTSServer;
        public int MaxDialogueLength
            => Performance.MaxDialogueLength;
        public int MaxTokens => Performance.MaxTokens;
        public double Temperature => Performance.Temperature;
    }
}
