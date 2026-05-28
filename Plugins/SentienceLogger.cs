// =============================================================================
//  Internal · Plugin Logger
// -----------------------------------------------------------------------------
//  One physical log file shared by every plugin, with rotation at ~2 MB so
//  it never grows without bound.  Per-plugin scopes prepend the plugin
//  name so logs read like:
//      [12:31:05] [INFO ] [PoliceRP] Player drew weapon — broadcasting...
// =============================================================================
using System;
using System.IO;
using GTA5MOD2026.SDK;

namespace GTA5MOD2026.Plugins
{
    internal sealed class PluginLogger : ISentienceLogger
    {
        private static readonly object _lock = new object();
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments),
            "GTA5MOD2026", "logs");
        private static readonly string LogPath
            = Path.Combine(LogDir, "plugins.log");
        private const long MaxBytes = 2L * 1024 * 1024;

        private readonly string _pluginName;
        private readonly LogLevel _minLevel;

        public PluginLogger(string pluginName,
            LogLevel minLevel = LogLevel.Info)
        {
            _pluginName = string.IsNullOrWhiteSpace(pluginName)
                ? "?" : pluginName;
            _minLevel = minLevel;
            try { Directory.CreateDirectory(LogDir); }
            catch { }
        }

        public void Log(LogLevel level, string message)
        {
            if (level < _minLevel) return;
            string line = string.Format(
                "[{0:HH:mm:ss}] [{1,-5}] [{2}] {3}",
                DateTime.Now, LevelTag(level), _pluginName,
                message ?? "");
            WriteLine(line);
        }

        public void Debug(string m) => Log(LogLevel.Debug, m);
        public void Info(string m)  => Log(LogLevel.Info, m);
        public void Warn(string m)  => Log(LogLevel.Warn, m);
        public void Error(string m) => Log(LogLevel.Error, m);

        private static string LevelTag(LogLevel l)
        {
            switch (l)
            {
                case LogLevel.Debug: return "DEBUG";
                case LogLevel.Info:  return "INFO";
                case LogLevel.Warn:  return "WARN";
                case LogLevel.Error: return "ERROR";
                default: return "?";
            }
        }

        private static void WriteLine(string line)
        {
            try
            {
                lock (_lock)
                {
                    RotateIfNeeded();
                    File.AppendAllText(LogPath,
                        line + Environment.NewLine,
                        System.Text.Encoding.UTF8);
                }
            }
            catch { /* logging must never throw */ }
        }

        private static void RotateIfNeeded()
        {
            try
            {
                var fi = new FileInfo(LogPath);
                if (!fi.Exists || fi.Length < MaxBytes) return;
                string rotated = LogPath + ".1";
                if (File.Exists(rotated)) File.Delete(rotated);
                File.Move(LogPath, rotated);
            }
            catch { }
        }
    }
}
