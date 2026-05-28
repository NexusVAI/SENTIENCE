// =============================================================================
//  Voices · Ped Model → Archetype Mapping
// -----------------------------------------------------------------------------
//  Hardcoded baseline mapping so the archetype system works out of the box,
//  even before the user ships archetype_voices.ini.  Coverage targets the
//  most common GTA V ambient ped models that show up in the open world.
//
//  Hashes are uint values (read off PedHash enum in SHVDN3).  Mapping is
//  deliberately conservative — when in doubt, returns "civilian".
//
//  The community / user can extend by adding [PedHashOverrides] in
//  archetype_voices.ini → key = "0x12345678", value = "biker"
// =============================================================================
using System.Collections.Generic;

namespace GTA5MOD2026.Voices
{
    public static class PedArchetypeMap
    {
        // Default archetype id when no rule matches.
        public const string Civilian = "civilian";

        // Built-in mapping of common GTA V ped models to archetypes.  Keys
        // are uint hashes from GTA.PedHash; values are archetype ids defined
        // in BuiltinArchetypes.
        //
        // The numbers here come from SHVDN3's PedHash enum.  Cast each
        // PedHash to uint at design time and put the literal here.  Anything
        // we don't list falls back to Civilian.
        private static readonly Dictionary<uint, string> _map
            = new Dictionary<uint, string>
            {
                // ── 商务 / 富人 ──
                { 0x9CD0FF42, "business"  }, // Business01AMM
                { 0x6C1F0411, "business"  }, // Business02AMM
                { 0x44E1C0F2, "wealthy"   }, // RichKids
                { 0x6357D4D2, "wealthy"   }, // Bevhills01AMY

                // ── 街头帮派 ──
                { 0x65B1B9B7, "biker"     }, // Lost01GMY
                { 0x9C19E8B9, "biker"     }, // Lost02GMY
                { 0xE48F0F90, "biker"     }, // Lost03GMY
                { 0xC4196005, "gang_hood" }, // Famca01GMY
                { 0x55E80D43, "gang_hood" }, // Famdnf01GMY
                { 0xCBC25195, "gang_latino"}, // Vagos01GFY
                { 0x9090A1F1, "gang_latino"}, // Vagos02GMY

                // ── 流浪 / 边缘 ──
                { 0xBB572883, "homeless"  }, // Tramp01AMY
                { 0xD024D239, "meth"      }, // Hippy01AMM (loose mapping)
                { 0xCA56FA60, "meth"      }, // Tweaker

                // ── 嬉皮 / 亚文化 ──
                { 0x4ABC909E, "hipster"   }, // Hipster01AMY
                { 0x70492FF6, "hipster"   }, // Hipster02AMY
                { 0x35F1782C, "hippie"    }, // Hippy02AMY

                // ── 游客 / 海滩 ──
                { 0xCCBA2537, "tourist"   }, // Tourist01AMM
                { 0x16FF6BB7, "beach"     }, // Beach01AMM
                { 0xCC4A1E04, "beach"     }, // Beach02AMM
                { 0xC0DB0C6D, "beach_muscle"},// MuscleBeach01AMY
                { 0xE0AD33B0, "lifeguard" }, // Lifeguard01AMY

                // ── 蓝领 / 工人 ──
                { 0xB8927ECC, "construction"}, // Drug01AMY (catch-all)
                { 0xE5673AC9, "construction"}, // Construction
                { 0x09141B14, "trucker"   }, // Trucker
                { 0xA1B99B7E, "farmer"    }, // Hick01AMY
                { 0xC52DD00E, "hillbilly" }, // Hick02AMM

                // ── 应急 ──
                { 0x5E3DA4A4, "firefighter"},// Fireman01SMY
                { 0xC7D2EC30, "medical"   }, // Paramedic01SMM
                { 0xCE5A1213, "medical"   }, // ScrubsAMM
                { 0x4DE0B649, "police"    }, // Cop01SMY
                { 0xEE4147B2, "police"    }, // Sheriff01SMY
                { 0x3D427B7F, "police"    }, // Highway01SMY

                // ── 老人 / 长者 ──
                { 0x6347AB14, "elderly"   }, // OldMan01AMY
                { 0xCE2CB751, "elderly"   }, // OldWoman01AMY

                // ── 健身 / 阳光 ──
                { 0xB94D8E60, "fitness"   }, // Salton01AMY (placeholder)
            };

        /// <summary>
        /// Resolve a ped model hash → archetype id.  Falls back to "civilian".
        /// </summary>
        public static string Resolve(uint modelHash)
        {
            if (_map.TryGetValue(modelHash, out var id))
                return id;
            return Civilian;
        }

        /// <summary>
        /// Allow the INI loader to inject per-hash overrides at runtime.
        /// Adds or replaces the entry.
        /// </summary>
        public static void Override(uint modelHash, string archetypeId)
        {
            if (string.IsNullOrWhiteSpace(archetypeId)) return;
            _map[modelHash] = archetypeId.Trim().ToLowerInvariant();
        }
    }
}
