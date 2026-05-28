// =============================================================================
//  Sentience · Service Bootstrap (singleton)
// -----------------------------------------------------------------------------
//  One-stop entry point that NPCManager reaches into.  Owns:
//      - ArchetypeRegistry   (ped → archetype + voice metadata)
//      - ScenarioLoader      (community-authored JSON triggers)
//      - PluginLoader        (third-party DLL plugins)
//
//  Initialized once on first access.  Initialization MUST not throw — every
//  failure path is logged and the affected subsystem is left empty.  The
//  goal is "Sentience never bricks the game just because someone shipped a
//  bad plugin or scenario file".
// =============================================================================
using System;
using System.Collections.Generic;
using GTA5MOD2026.Plugins.Scenarios;
using GTA5MOD2026.SDK;
using GTA5MOD2026.Voices;

namespace GTA5MOD2026.Plugins
{
    public sealed class SentienceServices
    {
        private static readonly object _lock = new object();
        private static SentienceServices _instance;

        public ArchetypeRegistry Archetypes { get; }
        public ScenarioLoader Scenarios { get; }
        public PluginLoader Plugins { get; }

        public string LoadedArchetypeIniPath { get; private set; }
        public string ScenarioDirectoryPath
            => Scenarios?.ScenarioDirectory ?? "";
        public string PluginDirectoryPath
            => Plugins?.PluginDirectory ?? "";

        public const string HostModVersion = "5.1.0";

        private SentienceServices(ModConfig config)
        {
            // ── Archetypes (load defaults + optional INI overrides) ──
            Archetypes = new ArchetypeRegistry();
            try { LoadedArchetypeIniPath = Archetypes.LoadAll(); }
            catch { LoadedArchetypeIniPath = ""; }

            // ── Plugin loader (must come before scenarios so scenario-
            //    aware plugins can subscribe to events first) ──
            Plugins = new PluginLoader(HostModVersion);
            try
            {
                Plugins.DiscoverAndLoad(BuildConfigSnapshot(config));
            }
            catch { /* must not fail bootstrap */ }

            // ── Scenarios (ISentienceLogger here is shared/null-safe) ──
            var bootstrapLogger = new PluginLogger("Sentience.Core");
            Scenarios = new ScenarioLoader(bootstrapLogger);
            try { Scenarios.Discover(); }
            catch { /* must not fail bootstrap */ }
        }

        public static SentienceServices Instance => _instance;

        public static SentienceServices Initialize(ModConfig config)
        {
            if (_instance != null) return _instance;
            lock (_lock)
            {
                if (_instance != null) return _instance;
                _instance = new SentienceServices(config);
            }
            return _instance;
        }

        public static void Shutdown()
        {
            SentienceServices toUnload;
            lock (_lock)
            {
                toUnload = _instance;
                _instance = null;
            }
            try { toUnload?.Plugins?.UnloadAll(); }
            catch { }
        }

        /// <summary>
        /// Flatten a ModConfig into the read-only snapshot exposed to plugins.
        /// Whitelist approach: only fields safe to expose to third parties.
        /// </summary>
        private static IReadOnlyDictionary<string, string> BuildConfigSnapshot(
            ModConfig cfg)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (cfg == null) return d;
            try
            {
                d["LLM.Provider"]      = cfg.LLM.Provider ?? "";
                d["LLM.LocalModel"]    = cfg.LLM.LocalModel ?? "";
                d["LLM.LightModel"]    = cfg.LLM.LightModel ?? "";
                d["LLM.CloudModel"]    = cfg.LLM.CloudModel ?? "";
                d["TTS.Provider"]      = cfg.TTS?.TTSProvider ?? "";
                d["TTS.VoiceEnabled"]  = (cfg.TTS?.VoiceEnabled ?? false)
                    .ToString();
                d["Awakening.Enabled"] = cfg.Awakening.Enabled.ToString();
            }
            catch { /* ignore — partial snapshot is fine */ }
            return d;
        }

        /// <summary>
        /// Compose the full extra-prompt block for a given NPC interaction.
        /// Order is: archetype prompt → matched scenario → plugin events.
        /// Returns "" if nothing contributes.
        /// </summary>
        public string ComposeExtraSystemPrompt(
            INPCContext npc,
            string playerInput,
            string interactionType,
            ScenarioMatchFacts facts,
            float gameTime)
        {
            var pieces = new List<string>(4);

            // 1) Archetype.
            if (!string.IsNullOrWhiteSpace(npc?.Archetype))
            {
                var arch = Archetypes.Get(npc.Archetype);
                if (!string.IsNullOrWhiteSpace(arch?.PersonalityPrompt))
                    pieces.Add(arch.PersonalityPrompt);
            }

            // 2) Scenario.
            try
            {
                var s = Scenarios?.MatchFirst(facts);
                if (s != null
                    && !string.IsNullOrWhiteSpace(s.SystemPromptAppend))
                {
                    pieces.Add(s.SystemPromptAppend);
                }
            }
            catch { /* one bad scenario must not poison the request */ }

            // 3) Plugin events.
            try
            {
                var args = Plugins?.Hub?.RaiseNpcRequestingAi(
                    npc, playerInput, interactionType, gameTime);
                if (args != null
                    && !string.IsNullOrWhiteSpace(args.ExtraSystemPrompt))
                {
                    pieces.Add(args.ExtraSystemPrompt);
                }
            }
            catch { }

            if (pieces.Count == 0) return "";
            return string.Join("\n\n", pieces.ToArray());
        }
    }
}
