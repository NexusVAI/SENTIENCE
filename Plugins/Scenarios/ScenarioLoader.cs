// =============================================================================
//  Scenario · Loader
// -----------------------------------------------------------------------------
//  Discovery pass at startup:
//      1. Ensure %Documents%/GTA5MOD2026/Scenarios/ exists.
//      2. Read every *.json file with UTF-8.
//      3. Newtonsoft parse → Scenario object.  Errors are logged, never
//         thrown — one bad scenario must not kill the whole run.
//      4. Dedupe by Id, keeping the first one (with a warning).
//
//  Matching pass at runtime:
//      MatchTrigger(scenario, NPC, kind, hotkey, ...) → bool
//  Used by NPCManager just before building the LLM payload to inject the
//  scenario's SystemPromptAppend.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using GTA5MOD2026.SDK;

namespace GTA5MOD2026.Plugins.Scenarios
{
    public sealed class ScenarioLoader
    {
        private readonly List<Scenario> _scenarios = new List<Scenario>();
        private readonly ISentienceLogger _logger;

        public IReadOnlyList<Scenario> Scenarios => _scenarios;

        public ScenarioLoader(ISentienceLogger logger = null)
        {
            _logger = logger;
        }

        public string ScenarioDirectory => Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments),
            "GTA5MOD2026", SentienceSdk.ScenarioFolderName);

        public void Discover()
        {
            _scenarios.Clear();
            string dir = ScenarioDirectory;
            try { Directory.CreateDirectory(dir); }
            catch { return; }

            string[] files;
            try { files = Directory.GetFiles(dir, "*.json"); }
            catch { return; }

            foreach (var file in files)
            {
                try
                {
                    string raw = File.ReadAllText(
                        file, System.Text.Encoding.UTF8);
                    var jo = JObject.Parse(raw);
                    var scenario = ParseScenario(jo, file);
                    if (scenario == null) continue;

                    if (_scenarios.Any(s => string.Equals(
                        s.Id, scenario.Id, StringComparison.Ordinal)))
                    {
                        _logger?.Warn(
                            $"Duplicate scenario id '{scenario.Id}' in " +
                            $"{Path.GetFileName(file)} — kept the first.");
                        continue;
                    }
                    _scenarios.Add(scenario);
                    _logger?.Info(
                        $"Scenario loaded: {scenario.Id} ({scenario.Name})");
                }
                catch (Exception ex)
                {
                    _logger?.Error(
                        $"Scenario {Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }

        private Scenario ParseScenario(JObject jo, string sourceFile)
        {
            if (jo == null) return null;
            var s = new Scenario
            {
                Id = (jo["id"] ?? "").ToString().Trim(),
                Name = (jo["name"] ?? "").ToString().Trim(),
                Author = (jo["author"] ?? "").ToString().Trim(),
                Description = (jo["description"] ?? "").ToString(),
                SourceFile = sourceFile,
                Enabled = jo["enabled"]?.Value<bool?>() ?? true,
                SystemPromptAppend =
                    (jo["systemPromptAppend"] ?? "").ToString()
            };

            if (string.IsNullOrWhiteSpace(s.Id))
            {
                _logger?.Warn(
                    $"Scenario in {Path.GetFileName(sourceFile)} " +
                    "has no 'id' — skipped.");
                return null;
            }

            var trig = jo["trigger"] as JObject;
            if (trig != null)
            {
                s.Trigger = ParseTrigger(trig);
            }

            var lines = jo["lines"] as JArray;
            if (lines != null)
            {
                foreach (var l in lines.OfType<JObject>())
                {
                    s.Lines.Add(new ScenarioLine
                    {
                        From = (l["from"] ?? "").ToString(),
                        Text = (l["text"] ?? "").ToString()
                    });
                }
            }

            var actions = jo["actions"] as JArray;
            if (actions != null)
            {
                foreach (var a in actions.OfType<JObject>())
                {
                    s.Actions.Add(new ScenarioAction
                    {
                        Name = (a["name"] ?? "").ToString(),
                        Trigger = (a["trigger"] ?? "").ToString(),
                        AI = a["ai"]?.Value<bool?>() ?? true
                    });
                }
            }

            return s;
        }

        private ScenarioTrigger ParseTrigger(JObject trig)
        {
            var st = new ScenarioTrigger();
            string type = (trig["type"] ?? "always").ToString().ToLowerInvariant();
            switch (type)
            {
                case "always":         st.Type = TriggerType.Always; break;
                case "ped_archetype":  st.Type = TriggerType.PedArchetype; break;
                case "ped_model_hash": st.Type = TriggerType.PedModelHash; break;
                case "hotkey":         st.Type = TriggerType.Hotkey; break;
                case "zone_name":      st.Type = TriggerType.ZoneName; break;
                case "player_weapon":  st.Type = TriggerType.PlayerWeapon; break;
                default: st.Type = TriggerType.Always; break;
            }
            st.Archetypes  = StringList(trig["archetypes"]);
            st.ModelHashes = StringList(trig["modelHashes"]);
            st.Zones       = StringList(trig["zones"]);
            st.Hotkey      = (trig["hotkey"] ?? "").ToString();
            st.PlayerInVehicle = trig["playerInVehicle"]?.Value<bool?>();
            st.PlayerArmed     = trig["playerArmed"]?.Value<bool?>();
            st.MinDistanceMeters = trig["minDistanceMeters"]?.Value<float?>() ?? 0f;
            st.MaxDistanceMeters = trig["maxDistanceMeters"]?.Value<float?>() ?? 50f;
            return st;
        }

        private static List<string> StringList(JToken token)
        {
            var list = new List<string>();
            if (token is JArray arr)
            {
                foreach (var t in arr)
                {
                    string s = (t ?? "").ToString();
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                }
            }
            return list;
        }

        /// <summary>
        /// Returns the first enabled scenario whose trigger matches.
        /// Caller passes a small bag of facts; null if no scenario fires.
        /// </summary>
        public Scenario MatchFirst(ScenarioMatchFacts facts)
        {
            if (facts == null) return null;
            foreach (var s in _scenarios)
            {
                if (!s.Enabled) continue;
                if (Matches(s.Trigger, facts))
                    return s;
            }
            return null;
        }

        private static bool Matches(
            ScenarioTrigger trigger, ScenarioMatchFacts f)
        {
            if (trigger == null) return false;

            if (f.DistanceMeters < trigger.MinDistanceMeters) return false;
            if (f.DistanceMeters > trigger.MaxDistanceMeters) return false;
            if (trigger.PlayerInVehicle.HasValue
                && trigger.PlayerInVehicle.Value != f.PlayerInVehicle)
                return false;
            if (trigger.PlayerArmed.HasValue
                && trigger.PlayerArmed.Value != f.PlayerArmed)
                return false;

            switch (trigger.Type)
            {
                case TriggerType.Always:
                    return true;
                case TriggerType.PedArchetype:
                    return trigger.Archetypes.Any(a =>
                        string.Equals(a, f.Archetype,
                            StringComparison.OrdinalIgnoreCase));
                case TriggerType.PedModelHash:
                    return trigger.ModelHashes.Any(h =>
                        string.Equals(h, f.ModelHashHex,
                            StringComparison.OrdinalIgnoreCase));
                case TriggerType.Hotkey:
                    return string.Equals(trigger.Hotkey,
                        f.Hotkey, StringComparison.OrdinalIgnoreCase);
                case TriggerType.ZoneName:
                    return trigger.Zones.Any(z =>
                        string.Equals(z, f.ZoneName,
                            StringComparison.OrdinalIgnoreCase));
                case TriggerType.PlayerWeapon:
                    return trigger.PlayerArmed.HasValue
                        ? trigger.PlayerArmed.Value == f.PlayerArmed
                        : f.PlayerArmed;
                default:
                    return false;
            }
        }
    }

    /// <summary>Tiny POCO with the inputs ScenarioLoader needs to match.</summary>
    public sealed class ScenarioMatchFacts
    {
        public string Archetype { get; set; } = "";
        public string ModelHashHex { get; set; } = "";
        public string Hotkey { get; set; } = "";
        public string ZoneName { get; set; } = "";
        public float DistanceMeters { get; set; }
        public bool PlayerInVehicle { get; set; }
        public bool PlayerArmed { get; set; }
    }
}
