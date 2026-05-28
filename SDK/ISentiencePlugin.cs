// =============================================================================
//  Sentience SDK · Public Plugin Contract
// -----------------------------------------------------------------------------
//  Implement this interface in your plugin DLL, drop the compiled file into
//  <GTA V>\scripts\SentiencePlugins\, and Sentience will auto-load it on
//  startup.  Each lifecycle method runs on the game's main thread, so you
//  can call any SHVDN3 / ScriptHookV native safely.  Long-running work must
//  be dispatched to a Task so the game thread is not blocked.
//
//  This contract is part of the public ABI.  Once shipped, member signatures
//  here are considered frozen — additions go to derived interfaces, never
//  breaking changes.
// =============================================================================
using System;

namespace GTA5MOD2026.SDK
{
    /// <summary>
    /// Lifecycle hooks every Sentience plugin must implement.
    /// </summary>
    public interface ISentiencePlugin
    {
        /// <summary>Human-readable name of the plugin (shown in F5 menu).</summary>
        string Name { get; }

        /// <summary>Author handle / studio / username.</summary>
        string Author { get; }

        /// <summary>SemVer string, e.g. "1.0.0".</summary>
        string Version { get; }

        /// <summary>
        /// Minimum Sentience SDK version this plugin targets.
        /// Sentience refuses to load a plugin that requires a higher SDK
        /// version than what it ships.  Use <see cref="SentienceSdk.Version"/>
        /// to compare.
        /// </summary>
        string MinSdkVersion { get; }

        /// <summary>
        /// Called exactly once after the plugin DLL is loaded and resolved.
        /// Subscribe to <see cref="ISentienceEvents"/> events here.  Anything
        /// thrown is logged and the plugin is disabled — the main mod
        /// continues to run.
        /// </summary>
        void OnLoad(IPluginContext context);

        /// <summary>
        /// Called when Sentience is shutting down (game closing or hot-reload).
        /// Release any unmanaged resources here.  Event subscriptions are
        /// automatically cleared by the loader.
        /// </summary>
        void OnUnload();
    }
}
