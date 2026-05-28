// =============================================================================
//  Sentience SDK · NPC Read-Only Snapshot
// -----------------------------------------------------------------------------
//  Plugins receive these snapshots inside event handlers.  They are
//  intentionally read-only and contain only the fields safe to expose to
//  third parties.  Mutating state happens through SDK methods (e.g.
//  InjectSystemPrompt) or through SHVDN3 if the plugin really wants to be
//  intrusive.
// =============================================================================
namespace GTA5MOD2026.SDK
{
    /// <summary>
    /// Immutable snapshot of an NPC at the moment an event fires.
    /// </summary>
    public interface INPCContext
    {
        /// <summary>Per-spawn unique id (8 hex chars, stable across one life).</summary>
        string UniqueId { get; }

        /// <summary>Long-term identity hash (model + appearance + region).</summary>
        string StableId { get; }

        /// <summary>SHVDN ped handle.  Caller is responsible for null-checking via SHVDN.</summary>
        int PedHandle { get; }

        /// <summary>Display name (e.g. "Tony", "Lucy").</summary>
        string Name { get; }

        /// <summary>One of: 友善 / 冷漠 / 暴躁 / 胆小 / 搞笑 or "" if unassigned.</summary>
        string Personality { get; }

        /// <summary>Detected archetype id (e.g. "biker", "hipster"), empty if unknown.</summary>
        string Archetype { get; }

        /// <summary>True = male, false = female.</summary>
        bool IsMale { get; }

        /// <summary>0..4 awakening stage (Sleeping..Awakened).</summary>
        int AwakenStage { get; }

        /// <summary>0..N threat level computed by perception.</summary>
        int ThreatLevel { get; }

        /// <summary>Player reputation as scored by this NPC, -100..+100.</summary>
        float PlayerReputation { get; }

        /// <summary>World coordinates: x, y, z (read-only snapshot).</summary>
        float PositionX { get; }
        float PositionY { get; }
        float PositionZ { get; }

        /// <summary>Zone name (LSPDFR-style street label, e.g. "Vinewood").</summary>
        string ZoneName { get; }

        /// <summary>True if the NPC currently has an open AI request.</summary>
        bool IsWaitingForAI { get; }
    }
}
