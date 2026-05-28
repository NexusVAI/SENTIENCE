// =============================================================================
//  Voices · Archetype Model
// -----------------------------------------------------------------------------
//  An archetype is a "kind of NPC" — biker, hipster, business, etc.  Each
//  archetype carries:
//      - A short personality prompt (Chinese) appended to the LLM system msg.
//      - Suggested edge-tts voice names per gender (zh-CN-* short names).
//      - A pool of walkstyles for movement variety (LSPDFR-parity feature).
//
//  Archetypes are detected from the ped model hash (see PedArchetypeMap).
//  All fields can be overridden by archetype_voices.ini at runtime.
// =============================================================================
using System.Collections.Generic;

namespace GTA5MOD2026.Voices
{
    public sealed class Archetype
    {
        /// <summary>Lowercase slug (e.g. "biker", "hipster"). Stable id.</summary>
        public string Id { get; set; } = "";

        /// <summary>Localized display name (e.g. "机车党"). Shown in F5 menu.</summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// Free-form Chinese sentence appended to the system prompt for any
        /// NPC matching this archetype.  Sample: "你是机车党的成员，说话粗
        /// 鲁，敌视警察。"
        /// </summary>
        public string PersonalityPrompt { get; set; } = "";

        /// <summary>edge-tts voice short name for male NPCs of this archetype.</summary>
        public string MaleVoice { get; set; } = "";

        /// <summary>edge-tts voice short name for female NPCs.</summary>
        public string FemaleVoice { get; set; } = "";

        /// <summary>One of: "default" | "fast" | "slow" — TTS speaking rate.</summary>
        public string SpeakingRate { get; set; } = "default";

        /// <summary>List of GTA walkstyle names; one is randomly assigned.</summary>
        public List<string> Walkstyles { get; set; } = new List<string>();
    }
}
