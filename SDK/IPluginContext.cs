// =============================================================================
//  Sentience SDK · Plugin Context
// -----------------------------------------------------------------------------
//  Passed to ISentiencePlugin.OnLoad.  Bundles the only services a plugin is
//  ever allowed to touch:  events, logger, mod config snapshot, and SDK
//  metadata.  Plugins MUST NOT reflect into internal GTA5MOD2026 types — the
//  contract here is the entire supported surface.
// =============================================================================
using System.Collections.Generic;

namespace GTA5MOD2026.SDK
{
    /// <summary>
    /// Service locator handed to a plugin during <see cref="ISentiencePlugin.OnLoad"/>.
    /// </summary>
    public interface IPluginContext
    {
        /// <summary>SDK runtime version (semver) of the host Sentience build.</summary>
        string HostSdkVersion { get; }

        /// <summary>
        /// Sentience mod version (e.g. "5.1.0"), distinct from SDK version.
        /// </summary>
        string HostModVersion { get; }

        /// <summary>Subscribe / unsubscribe to game-side events here.</summary>
        ISentienceEvents Events { get; }

        /// <summary>Diagnostic logger (writes to %Documents%/GTA5MOD2026/logs/plugins.log).</summary>
        ISentienceLogger Logger { get; }

        /// <summary>
        /// Read-only snapshot of the host's INI configuration.  Plugins
        /// observe but do not mutate — config edits go through Sentience's
        /// F5 menu / launcher to keep persistence consistent.
        /// </summary>
        IReadOnlyDictionary<string, string> ConfigSnapshot { get; }

        /// <summary>
        /// Absolute path to the plugin's own asset folder (created on demand
        /// under %Documents%/GTA5MOD2026/PluginData/&lt;plugin-name&gt;/).
        /// Plugins use this for INI / JSON / cache files they own.
        /// </summary>
        string PluginDataDirectory { get; }
    }
}
