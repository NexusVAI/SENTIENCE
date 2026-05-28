// =============================================================================
//  Internal · NPCState → SDK Snapshot Adapter
// -----------------------------------------------------------------------------
//  Plugins are never handed a live NPCState reference (it's mutable, fat,
//  and full of internal SHVDN types).  Instead, NPCManager wraps it in a
//  cheap snapshot at the moment of an event.  Snapshot creation is meant
//  to be O(1) and allocation-light.
// =============================================================================
using GTA5MOD2026.SDK;

namespace GTA5MOD2026.Plugins
{
    internal sealed class NPCContextSnapshot : INPCContext
    {
        public string UniqueId { get; set; } = "";
        public string StableId { get; set; } = "";
        public int    PedHandle { get; set; }
        public string Name { get; set; } = "";
        public string Personality { get; set; } = "";
        public string Archetype { get; set; } = "";
        public bool   IsMale { get; set; }
        public int    AwakenStage { get; set; }
        public int    ThreatLevel { get; set; }
        public float  PlayerReputation { get; set; }
        public float  PositionX { get; set; }
        public float  PositionY { get; set; }
        public float  PositionZ { get; set; }
        public string ZoneName { get; set; } = "";
        public bool   IsWaitingForAI { get; set; }

        public static NPCContextSnapshot From(NPCState s,
            string archetype = "", string zoneName = "")
        {
            if (s == null) return new NPCContextSnapshot();
            var pos = s.Ped != null && s.Ped.Exists()
                ? s.Ped.Position
                : new GTA.Math.Vector3();

            return new NPCContextSnapshot
            {
                UniqueId = s.UniqueId ?? "",
                StableId = s.StableId ?? "",
                PedHandle = s.Handle,
                Name = s.NpcName ?? "",
                Personality = s.Personality ?? "",
                Archetype = archetype ?? "",
                IsMale = s.IsMale,
                AwakenStage = (int)s.Stage,
                ThreatLevel = s.ThreatLevel,
                PlayerReputation = s.PlayerReputation,
                PositionX = pos.X,
                PositionY = pos.Y,
                PositionZ = pos.Z,
                ZoneName = zoneName ?? "",
                IsWaitingForAI = s.WaitingForAI
            };
        }
    }
}
