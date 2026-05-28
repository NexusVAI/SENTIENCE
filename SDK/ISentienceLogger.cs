// =============================================================================
//  Sentience SDK · Logger
// -----------------------------------------------------------------------------
//  Lightweight logger handed to plugins.  All writes go to a single shared
//  rotating log at %Documents%/GTA5MOD2026/logs/plugins.log.  Plugin name is
//  prepended to every line so the user can tell who said what.
// =============================================================================
namespace GTA5MOD2026.SDK
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }

    public interface ISentienceLogger
    {
        void Log(LogLevel level, string message);
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }
}
