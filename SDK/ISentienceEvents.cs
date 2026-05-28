// =============================================================================
//  Sentience SDK · Event Hub
// -----------------------------------------------------------------------------
//  All cross-plugin and host→plugin signalling goes through this single hub.
//  Handlers run on the SHVDN main thread.  An unhandled exception in a
//  handler is caught by the host, logged, and the offending plugin is
//  disabled — but the other plugins keep receiving events.
// =============================================================================
using System;

namespace GTA5MOD2026.SDK
{
    /// <summary>
    /// Args base.  Every concrete event derives from this so handlers can
    /// rely on a common timestamp / cancellation surface.
    /// </summary>
    public abstract class SentienceEventArgs : EventArgs
    {
        /// <summary>Game time (seconds since session start) when the event fired.</summary>
        public float GameTime { get; internal set; }

        /// <summary>
        /// Set to true inside a handler to ask Sentience to skip its own
        /// default reaction (used by, e.g., a PoliceRP plugin that wants to
        /// hijack the standard "G = compliment" branch).
        /// </summary>
        public bool Handled { get; set; }
    }

    /// <summary>Fired when a new NPC enters Sentience's awareness radius.</summary>
    public sealed class NPCSpawnedEventArgs : SentienceEventArgs
    {
        public INPCContext Npc { get; internal set; }
    }

    /// <summary>Fired right before Sentience disposes an NPC's state.</summary>
    public sealed class NPCDespawnedEventArgs : SentienceEventArgs
    {
        public INPCContext Npc { get; internal set; }
        public string Reason { get; internal set; }
    }

    /// <summary>
    /// Fired before Sentience sends a chat completion request.  Plugins may
    /// append <see cref="ExtraSystemPrompt"/>; the host concatenates them in
    /// load order, separated by "\n\n".
    /// </summary>
    public sealed class NPCRequestingAIEventArgs : SentienceEventArgs
    {
        public INPCContext Npc { get; internal set; }
        public string PlayerInput { get; internal set; }
        public string InteractionType { get; internal set; }

        /// <summary>
        /// Free-form text appended to the system prompt for this single
        /// request.  Setting this to non-null lets a plugin steer behavior
        /// (e.g. "Speak like a 1950s detective").
        /// </summary>
        public string ExtraSystemPrompt { get; set; }
    }

    /// <summary>Fired once the LLM has returned and Sentience has parsed it.</summary>
    public sealed class NPCDialogueEventArgs : SentienceEventArgs
    {
        public INPCContext Npc { get; internal set; }
        public string Dialogue { get; internal set; }
        public string Action { get; internal set; }
        public string Emotion { get; internal set; }
        public string Intent { get; internal set; }
    }

    /// <summary>Fired when the player presses one of the interaction hotkeys (G/H/T/J).</summary>
    public sealed class PlayerInteractionEventArgs : SentienceEventArgs
    {
        public INPCContext Npc { get; internal set; }
        public string Kind { get; internal set; }   // "compliment" | "insult" | "type" | "voice"
        public string PlayerText { get; internal set; }
    }

    /// <summary>
    /// Fired when player draws / holsters a weapon.  Useful for PoliceRP-style
    /// plugins that want NPCs to dial 911.
    /// </summary>
    public sealed class PlayerWeaponChangedEventArgs : SentienceEventArgs
    {
        public bool IsArmed { get; internal set; }
        public string WeaponHashName { get; internal set; }
    }

    /// <summary>
    /// Public event hub.  Plugins subscribe in OnLoad and (optionally)
    /// unsubscribe in OnUnload; the loader force-clears any leftover
    /// handlers when a plugin is unloaded, so unsubscribing is best-effort.
    /// </summary>
    public interface ISentienceEvents
    {
        event EventHandler<NPCSpawnedEventArgs>        NPCSpawned;
        event EventHandler<NPCDespawnedEventArgs>      NPCDespawned;
        event EventHandler<NPCRequestingAIEventArgs>   NPCRequestingAI;
        event EventHandler<NPCDialogueEventArgs>       NPCDialogue;
        event EventHandler<PlayerInteractionEventArgs> PlayerInteraction;
        event EventHandler<PlayerWeaponChangedEventArgs> PlayerWeaponChanged;
    }
}
