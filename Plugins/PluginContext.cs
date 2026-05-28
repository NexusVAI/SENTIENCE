// =============================================================================
//  Internal · IPluginContext Implementation
// -----------------------------------------------------------------------------
//  One instance per loaded plugin.  Hands the plugin its own logger and a
//  read-only view onto the host configuration.  ConfigSnapshot is built by
//  flattening the active ModConfig into "Section.Key" → "Value" strings so
//  plugins don't have to depend on ModConfig's full type surface.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using GTA5MOD2026.SDK;

namespace GTA5MOD2026.Plugins
{
    internal sealed class PluginContext : IPluginContext
    {
        public string HostSdkVersion => SentienceSdk.Version;
        public string HostModVersion { get; }
        public ISentienceEvents Events { get; }
        public ISentienceLogger Logger { get; }
        public IReadOnlyDictionary<string, string> ConfigSnapshot { get; }
        public string PluginDataDirectory { get; }

        public PluginContext(
            string hostModVersion,
            ISentienceEvents events,
            ISentienceLogger logger,
            IReadOnlyDictionary<string, string> configSnapshot,
            string pluginDataDirectory)
        {
            HostModVersion = hostModVersion ?? "";
            Events = events ?? throw new ArgumentNullException(nameof(events));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ConfigSnapshot = configSnapshot
                ?? new Dictionary<string, string>();
            PluginDataDirectory = pluginDataDirectory ?? "";

            try
            {
                if (!string.IsNullOrWhiteSpace(PluginDataDirectory))
                    Directory.CreateDirectory(PluginDataDirectory);
            }
            catch { }
        }
    }
}
