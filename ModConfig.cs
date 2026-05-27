using System;
using System.Globalization;
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
        public UIConfig UI { get; set; }
            = new UIConfig();
        public BehaviorConfig Behavior { get; set; }
            = new BehaviorConfig();

        public class LLMConfig
        {
            public string Provider { get; set; } = "local";
            public string LocalEndpoint { get; set; }
                = "http://127.0.0.1:5001/v1/chat/completions";
            public string LocalModel { get; set; }
                = "gta5_2b_q4km";
            public string LightEndpoint { get; set; }
                = "http://127.0.0.1:5001/v1/chat/completions";
            public string LightModel { get; set; }
                = "gta5_2b_q4km";
            public string CloudEndpoint { get; set; }
                = "https://api.deepseek.com/v1/chat/completions";
            public string CloudModel { get; set; }
                = "deepseek-v4-flash";
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

        // ─────────────────────────────────────────────────────────
        //  HUD / drawing customization (exposed through the F5 menu)
        // ─────────────────────────────────────────────────────────
        public class UIConfig
        {
            // NPC overhead label
            public bool OverheadEnabled { get; set; } = true;
            // 0.5 = small, 1.0 = default, 2.0 = huge
            public float OverheadScale { get; set; } = 1.0f;
            // "default" | "minimal" | "bold" | "cinematic"
            public string OverheadStyle { get; set; } = "default";
            // When false, every NPC label uses OverheadColor instead of
            // the personality color.
            public bool UsePersonalityColor { get; set; } = true;
            // ARGB hex without alpha, e.g. "FFFFFF".
            public string OverheadColor { get; set; } = "FFFFFF";
            // Show the per-NPC dialogue bubble above their head.
            public bool ShowFloatingDialogue { get; set; } = true;

            // Bottom-of-screen subtitle (the "剧情字幕" mode).
            // "notification" (top-left, default) | "subtitle" (bottom)
            // | "both"
            public string ResponseDisplayMode { get; set; } = "notification";
            // Subtitle seconds.
            public float SubtitleDuration { get; set; } = 6f;

            // Top-left interaction HUD (replaces GTA's HELP queue).
            public bool HudEnabled { get; set; } = true;
            public float HudScale { get; set; } = 1.0f;
            public string HudColor { get; set; } = "FFFFFF";
            public string HudBgColor { get; set; } = "000000";
            public int HudBgAlpha { get; set; } = 140;
            // "top_left" | "top_right" | "bottom_left" | "bottom_right"
            public string HudPosition { get; set; } = "top_left";
            // Beep when help text appears (GTA default behavior).
            public bool HudBeep { get; set; } = false;
        }

        // ─────────────────────────────────────────────────────────
        //  Runtime gameplay behavior knobs
        // ─────────────────────────────────────────────────────────
        public class BehaviorConfig
        {
            // Meters – how close the player must be for the menu to show.
            public float ResponseRadius { get; set; } = 8f;
            // 0 (only manual) → 100 (very chatty).  50 keeps prior pacing.
            public int ActivityLevel { get; set; } = 50;
            // Master switch for executing LLM-suggested actions.
            // When false the NPC will only speak / face the player.
            public bool ActionsEnabled { get; set; } = true;
            // Disable specific high-impact actions even when ActionsEnabled.
            public bool AllowAttack { get; set; } = true;
            public bool AllowAim { get; set; } = true;
            public bool AllowCallCops { get; set; } = true;
            // Allow NPC to initiate conversation without player input.
            public bool AutonomousTalk { get; set; } = true;
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
                                config.Performance.MaxTokens =
                                    ParseInt(value, 80);
                            if (key == "MaxTokensThinking")
                                config.Performance.MaxTokensThinking =
                                    ParseInt(value, 120);
                            if (key == "Temperature")
                                config.Performance.Temperature =
                                    ParseDouble(value, 0.7);
                            if (key == "MaxDialogueLength")
                                config.Performance
                                    .MaxDialogueLength =
                                    ParseInt(value, 45);
                            if (key == "RequestCooldown")
                                config.Performance
                                    .RequestCooldown =
                                    ParseFloat(value, 2.0f);
                            if (key == "StrictMode")
                                config.Performance.StrictMode
                                    = ParseBool(value,
                                        config.Performance.StrictMode);
                            break;
                        case "TTS":
                            if (key == "TTSProvider")
                                config.TTS.TTSProvider = value;
                            else if (key == "TTSServer")
                                config.TTS.TTSServer = value;
                            else if (key == "VoiceEnabled")
                                config.TTS.VoiceEnabled =
                                    ParseBool(value,
                                        config.TTS.VoiceEnabled);
                            break;
                        case "AWAKENING":
                            if (key == "Enabled")
                                config.Awakening.Enabled =
                                    ParseBool(value,
                                        config.Awakening.Enabled);
                            else if (key == "Speed")
                                config.Awakening.Speed =
                                    ParseInt(value, 2);
                            break;
                        case "STT":
                            if (key == "WhisperModelPath")
                                config.STT.WhisperModelPath = value;
                            break;
                        case "UI":
                            if (key == "OverheadEnabled")
                                config.UI.OverheadEnabled = ParseBool(value,
                                    config.UI.OverheadEnabled);
                            else if (key == "OverheadScale")
                                config.UI.OverheadScale = ParseFloat(value,
                                    config.UI.OverheadScale);
                            else if (key == "OverheadStyle")
                                config.UI.OverheadStyle = value;
                            else if (key == "UsePersonalityColor")
                                config.UI.UsePersonalityColor = ParseBool(value,
                                    config.UI.UsePersonalityColor);
                            else if (key == "OverheadColor")
                                config.UI.OverheadColor = value;
                            else if (key == "ShowFloatingDialogue")
                                config.UI.ShowFloatingDialogue = ParseBool(value,
                                    config.UI.ShowFloatingDialogue);
                            else if (key == "ResponseDisplayMode")
                                config.UI.ResponseDisplayMode = value;
                            else if (key == "SubtitleDuration")
                                config.UI.SubtitleDuration = ParseFloat(value,
                                    config.UI.SubtitleDuration);
                            else if (key == "HudEnabled")
                                config.UI.HudEnabled = ParseBool(value,
                                    config.UI.HudEnabled);
                            else if (key == "HudScale")
                                config.UI.HudScale = ParseFloat(value,
                                    config.UI.HudScale);
                            else if (key == "HudColor")
                                config.UI.HudColor = value;
                            else if (key == "HudBgColor")
                                config.UI.HudBgColor = value;
                            else if (key == "HudBgAlpha")
                                config.UI.HudBgAlpha = ParseInt(value,
                                    config.UI.HudBgAlpha);
                            else if (key == "HudPosition")
                                config.UI.HudPosition = value;
                            else if (key == "HudBeep")
                                config.UI.HudBeep = ParseBool(value,
                                    config.UI.HudBeep);
                            break;
                        case "BEHAVIOR":
                            if (key == "ResponseRadius")
                                config.Behavior.ResponseRadius = ParseFloat(value,
                                    config.Behavior.ResponseRadius);
                            else if (key == "ActivityLevel")
                                config.Behavior.ActivityLevel = ParseInt(value,
                                    config.Behavior.ActivityLevel);
                            else if (key == "ActionsEnabled")
                                config.Behavior.ActionsEnabled = ParseBool(value,
                                    config.Behavior.ActionsEnabled);
                            else if (key == "AllowAttack")
                                config.Behavior.AllowAttack = ParseBool(value,
                                    config.Behavior.AllowAttack);
                            else if (key == "AllowAim")
                                config.Behavior.AllowAim = ParseBool(value,
                                    config.Behavior.AllowAim);
                            else if (key == "AllowCallCops")
                                config.Behavior.AllowCallCops = ParseBool(value,
                                    config.Behavior.AllowCallCops);
                            else if (key == "AutonomousTalk")
                                config.Behavior.AutonomousTalk = ParseBool(value,
                                    config.Behavior.AutonomousTalk);
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
                sb.AppendLine("# Option A: Local llama.cpp CPU model");
                sb.AppendLine("#   Provider=local");
                sb.AppendLine("#   LocalEndpoint=http://127.0.0.1:5001/v1/chat/completions");
                sb.AppendLine("#");
                sb.AppendLine("# Option B: Cloud API (fast, costs money)");
                sb.AppendLine("#   Provider=cloud");
                sb.AppendLine("#   Get API key from DeepSeek/OpenAI/Aliyun/etc.");
                sb.AppendLine("#");
                sb.AppendLine("# Option C: Any OpenAI-compatible API");
                sb.AppendLine("#   Provider=cloud");
                sb.AppendLine("#   CloudEndpoint=your_api_url");
                sb.AppendLine("#   CloudModel=model_name");
                sb.AppendLine("#   CloudAPIKey=your_key");
                sb.AppendLine("#   CloudAPIKey=Bearer your_key");
                sb.AppendLine("#   CloudAPIKey=Authorization: Bearer your_key");
                sb.AppendLine("#   CloudAPIKey=x-api-key: your_key");
                sb.AppendLine("# ==========================================");
                sb.AppendLine();

                sb.AppendLine("[LLM]");
                sb.AppendLine("# Provider: local or cloud");
                sb.AppendLine($"Provider={config.LLM.Provider}");
                sb.AppendLine();
                sb.AppendLine("# --- Local (llama.cpp / OpenAI-compatible) ---");
                sb.AppendLine("# llama.cpp: http://127.0.0.1:5001/v1/chat/completions");
                sb.AppendLine("# LM Studio: http://127.0.0.1:1234/v1/chat/completions");
                sb.AppendLine($"LocalEndpoint={config.LLM.LocalEndpoint}");
                sb.AppendLine("# Model should match llama-server --alias");
                sb.AppendLine($"LocalModel={config.LLM.LocalModel}");
                sb.AppendLine();
                sb.AppendLine("# Light model endpoint");
                sb.AppendLine("# Single local model: set same as LocalEndpoint");
                sb.AppendLine($"LightEndpoint={config.LLM.LightEndpoint}");
                sb.AppendLine($"LightModel={config.LLM.LightModel}");
                sb.AppendLine();
                sb.AppendLine("# --- Cloud API ---");
                sb.AppendLine("# DeepSeek: https://api.deepseek.com/v1/chat/completions");
                sb.AppendLine("# OpenAI:   https://api.openai.com/v1/chat/completions");
                sb.AppendLine("# Aliyun:   https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions");
                sb.AppendLine("# SiliconFlow: https://api.siliconflow.cn/v1/chat/completions");
                sb.AppendLine($"CloudEndpoint={config.LLM.CloudEndpoint}");
                sb.AppendLine("# DeepSeek: deepseek-v4-flash");
                sb.AppendLine("# DeepSeek: deepseek-v4-pro");
                sb.AppendLine("# DeepSeek (deprecated 2026/07/24): deepseek-chat");
                sb.AppendLine("# DeepSeek (deprecated 2026/07/24): deepseek-reasoner");
                sb.AppendLine("# OpenAI:   gpt-4o-mini");
                sb.AppendLine("# Aliyun:   qwen-plus");
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
                sb.AppendLine();

                sb.AppendLine("[UI]");
                sb.AppendLine("# Overhead floating label");
                sb.AppendLine($"OverheadEnabled={config.UI.OverheadEnabled}");
                sb.AppendLine("# 0.5=small 1.0=default 2.0=huge");
                sb.AppendLine($"OverheadScale={config.UI.OverheadScale}");
                sb.AppendLine("# default | minimal | bold | cinematic");
                sb.AppendLine($"OverheadStyle={config.UI.OverheadStyle}");
                sb.AppendLine($"UsePersonalityColor={config.UI.UsePersonalityColor}");
                sb.AppendLine("# Hex RGB without # (e.g. FFFFFF)");
                sb.AppendLine($"OverheadColor={config.UI.OverheadColor}");
                sb.AppendLine($"ShowFloatingDialogue={config.UI.ShowFloatingDialogue}");
                sb.AppendLine();
                sb.AppendLine("# Response display");
                sb.AppendLine("# notification | subtitle | both");
                sb.AppendLine($"ResponseDisplayMode={config.UI.ResponseDisplayMode}");
                sb.AppendLine($"SubtitleDuration={config.UI.SubtitleDuration}");
                sb.AppendLine();
                sb.AppendLine("# Top-left interaction HUD");
                sb.AppendLine($"HudEnabled={config.UI.HudEnabled}");
                sb.AppendLine($"HudScale={config.UI.HudScale}");
                sb.AppendLine($"HudColor={config.UI.HudColor}");
                sb.AppendLine($"HudBgColor={config.UI.HudBgColor}");
                sb.AppendLine("# Background opacity 0..255");
                sb.AppendLine($"HudBgAlpha={config.UI.HudBgAlpha}");
                sb.AppendLine("# top_left | top_right | bottom_left | bottom_right");
                sb.AppendLine($"HudPosition={config.UI.HudPosition}");
                sb.AppendLine($"HudBeep={config.UI.HudBeep}");
                sb.AppendLine();

                sb.AppendLine("[Behavior]");
                sb.AppendLine("# How close the player must stand to trigger menu (meters)");
                sb.AppendLine($"ResponseRadius={config.Behavior.ResponseRadius}");
                sb.AppendLine("# 0=manual only, 100=very chatty");
                sb.AppendLine($"ActivityLevel={config.Behavior.ActivityLevel}");
                sb.AppendLine("# Master switch for executing LLM actions");
                sb.AppendLine($"ActionsEnabled={config.Behavior.ActionsEnabled}");
                sb.AppendLine($"AllowAttack={config.Behavior.AllowAttack}");
                sb.AppendLine($"AllowAim={config.Behavior.AllowAim}");
                sb.AppendLine($"AllowCallCops={config.Behavior.AllowCallCops}");
                sb.AppendLine("# NPCs may initiate dialogue on their own");
                sb.AppendLine($"AutonomousTalk={config.Behavior.AutonomousTalk}");

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

        private static int ParseInt(string value, int fallback)
        {
            int parsed;
            if (int.TryParse(value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture, out parsed))
                return parsed;
            return fallback;
        }

        private static float ParseFloat(string value, float fallback)
        {
            float parsed;
            if (float.TryParse(value,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out parsed))
                return parsed;

            if (float.TryParse(
                value?.Replace(',', '.'),
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out parsed))
                return parsed;

            return fallback;
        }

        private static double ParseDouble(string value, double fallback)
        {
            double parsed;
            if (double.TryParse(value,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out parsed)
                && parsed > 0d)
                return parsed;

            if (double.TryParse(
                value?.Replace(',', '.'),
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out parsed)
                && parsed > 0d)
                return parsed;

            return fallback;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            string normalized = value.Trim().ToLowerInvariant();
            if (normalized == "true"
                || normalized == "1"
                || normalized == "yes"
                || normalized == "on")
                return true;

            if (normalized == "false"
                || normalized == "0"
                || normalized == "no"
                || normalized == "off")
                return false;

            bool parsed;
            if (bool.TryParse(value, out parsed))
                return parsed;

            return fallback;
        }
    }
}
