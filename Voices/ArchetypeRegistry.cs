// =============================================================================
//  Voices · Archetype Registry
// -----------------------------------------------------------------------------
//  Central store of all known archetypes.  Population order:
//      1. BuiltinArchetypes.Defaults  →  hardcoded baseline
//      2. archetype_voices.ini        →  user/community overrides
//
//  archetype_voices.ini format:
//
//      [biker]
//      DisplayName       = 机车党
//      PersonalityPrompt = 你是机车党的成员，对警察怀有敌意，说话粗鲁。
//      MaleVoice         = zh-CN-YunjianNeural
//      FemaleVoice       = zh-CN-XiaomengNeural
//      SpeakingRate      = default
//      Walkstyles        = move_m@brave, move_m@swagger
//
//      [PedHashOverrides]
//      0x12345678 = biker
//      0xABCDEF01 = hipster
//
//  All sections are case-insensitive.  Unknown keys are ignored.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GTA5MOD2026.Voices
{
    public sealed class ArchetypeRegistry
    {
        private readonly Dictionary<string, Archetype> _byId
            = new Dictionary<string, Archetype>(
                StringComparer.OrdinalIgnoreCase);

        public IEnumerable<Archetype> All => _byId.Values;

        /// <summary>
        /// Get archetype by id, falling back to "civilian" if unknown.
        /// </summary>
        public Archetype Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) id = PedArchetypeMap.Civilian;
            if (_byId.TryGetValue(id, out var a)) return a;
            if (_byId.TryGetValue(
                PedArchetypeMap.Civilian, out var civ)) return civ;
            return new Archetype { Id = id, DisplayName = id };
        }

        public bool Contains(string id)
            => !string.IsNullOrWhiteSpace(id) && _byId.ContainsKey(id);

        /// <summary>
        /// Resolve archetype for a given ped model hash.
        /// </summary>
        public Archetype Resolve(uint modelHash)
            => Get(PedArchetypeMap.Resolve(modelHash));

        /// <summary>
        /// Initialize with built-in archetypes, then merge with user INI.
        /// Returns the path that was loaded (or empty string if none).
        /// </summary>
        public string LoadAll()
        {
            _byId.Clear();
            foreach (var a in BuiltinArchetypes.Defaults())
            {
                _byId[a.Id] = a;
            }

            string iniPath = ResolveIniPath();
            if (!string.IsNullOrEmpty(iniPath) && File.Exists(iniPath))
            {
                try { LoadIni(iniPath); }
                catch { /* malformed INI must not crash startup */ }
                return iniPath;
            }
            return "";
        }

        /// <summary>
        /// Lookup order:
        /// 1. %Documents%/GTA5MOD2026/archetype_voices.ini  (user-editable)
        /// 2. Same folder as the mod DLL                    (shipped default)
        /// </summary>
        private static string ResolveIniPath()
        {
            string docsPath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                "GTA5MOD2026", "archetype_voices.ini");
            if (File.Exists(docsPath)) return docsPath;

            string sidecar = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory ?? "",
                "archetype_voices.ini");
            if (File.Exists(sidecar)) return sidecar;

            return "";
        }

        private void LoadIni(string path)
        {
            string currentSection = null;
            var currentEntries = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            var sections = new Dictionary<string,
                Dictionary<string, string>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (string raw in File.ReadAllLines(path,
                System.Text.Encoding.UTF8))
            {
                string line = (raw ?? "").Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith(";") || line.StartsWith("#")) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    if (currentSection != null)
                        sections[currentSection] = currentEntries;
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    currentEntries = new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase);
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0 || currentSection == null) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                currentEntries[key] = val;
            }
            if (currentSection != null)
                sections[currentSection] = currentEntries;

            foreach (var kv in sections)
            {
                if (string.Equals(kv.Key, "PedHashOverrides",
                    StringComparison.OrdinalIgnoreCase))
                {
                    ApplyHashOverrides(kv.Value);
                    continue;
                }
                ApplyArchetypeSection(kv.Key, kv.Value);
            }
        }

        private void ApplyArchetypeSection(string id,
            IReadOnlyDictionary<string, string> entries)
        {
            id = id.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(id)) return;
            if (!_byId.TryGetValue(id, out var a))
            {
                a = new Archetype { Id = id };
                _byId[id] = a;
            }
            if (entries.TryGetValue("DisplayName", out var dn) && dn.Length > 0)
                a.DisplayName = dn;
            if (entries.TryGetValue("PersonalityPrompt", out var pp))
                a.PersonalityPrompt = pp;
            if (entries.TryGetValue("MaleVoice", out var mv) && mv.Length > 0)
                a.MaleVoice = mv;
            if (entries.TryGetValue("FemaleVoice", out var fv) && fv.Length > 0)
                a.FemaleVoice = fv;
            if (entries.TryGetValue("SpeakingRate", out var sr) && sr.Length > 0)
                a.SpeakingRate = sr;
            if (entries.TryGetValue("Walkstyles", out var ws) && ws.Length > 0)
            {
                a.Walkstyles = ws.Split(',')
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .ToList();
            }
            if (string.IsNullOrEmpty(a.DisplayName)) a.DisplayName = id;
        }

        private static void ApplyHashOverrides(
            IReadOnlyDictionary<string, string> entries)
        {
            foreach (var kv in entries)
            {
                if (TryParseHexUint(kv.Key, out uint hash))
                {
                    PedArchetypeMap.Override(hash, kv.Value);
                }
            }
        }

        private static bool TryParseHexUint(string s, out uint value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            string t = s.Trim();
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                t = t.Substring(2);
            return uint.TryParse(t, NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out value);
        }
    }
}
