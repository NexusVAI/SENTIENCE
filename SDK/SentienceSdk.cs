// =============================================================================
//  Sentience SDK · Constants
// -----------------------------------------------------------------------------
//  Single source of truth for SDK version + plugin folder name.  Bump
//  Version whenever a backwards-incompatible change lands in the public
//  contract.  Bump MinPluginSdk if the host stops supporting older plugins.
// =============================================================================
namespace GTA5MOD2026.SDK
{
    public static class SentienceSdk
    {
        /// <summary>Semantic version of this SDK build (frozen at release).</summary>
        public const string Version = "1.0.0";

        /// <summary>Oldest plugin SDK version this host still accepts.</summary>
        public const string MinSupportedPluginSdk = "1.0.0";

        /// <summary>
        /// Folder (relative to GTA V root\scripts\) where plugin DLLs live.
        /// </summary>
        public const string PluginFolderName = "SentiencePlugins";

        /// <summary>
        /// Folder name under %Documents%/GTA5MOD2026/ for scenario JSON files.
        /// </summary>
        public const string ScenarioFolderName = "Scenarios";
    }
}
