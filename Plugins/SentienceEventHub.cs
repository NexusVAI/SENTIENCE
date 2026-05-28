// =============================================================================
//  Internal · Event Hub Implementation
// -----------------------------------------------------------------------------
//  Single instance shared across all plugins.  Each Raise* method:
//    1. Iterates subscribers (snapshot copy, safe under add/remove).
//    2. Catches per-handler exceptions, logs them, marks the owning plugin
//       as faulted via PluginLoader so it can be auto-disabled.
//    3. Always returns the (possibly mutated) args so the caller can read
//       Handled / ExtraSystemPrompt back.
//  Designed to be cheap when nobody subscribes — single null check.
// =============================================================================
using System;
using GTA5MOD2026.SDK;

namespace GTA5MOD2026.Plugins
{
    internal sealed class SentienceEventHub : ISentienceEvents
    {
        private readonly Action<Delegate, Exception> _onHandlerFault;

        public SentienceEventHub(Action<Delegate, Exception> onHandlerFault)
        {
            _onHandlerFault = onHandlerFault
                ?? throw new ArgumentNullException(nameof(onHandlerFault));
        }

        public event EventHandler<NPCSpawnedEventArgs>          NPCSpawned;
        public event EventHandler<NPCDespawnedEventArgs>        NPCDespawned;
        public event EventHandler<NPCRequestingAIEventArgs>     NPCRequestingAI;
        public event EventHandler<NPCDialogueEventArgs>         NPCDialogue;
        public event EventHandler<PlayerInteractionEventArgs>   PlayerInteraction;
        public event EventHandler<PlayerWeaponChangedEventArgs> PlayerWeaponChanged;

        public NPCSpawnedEventArgs RaiseNpcSpawned(
            INPCContext npc, float gameTime)
        {
            var args = new NPCSpawnedEventArgs
            {
                Npc = npc,
                GameTime = gameTime
            };
            SafeInvoke(NPCSpawned, args);
            return args;
        }

        public NPCDespawnedEventArgs RaiseNpcDespawned(
            INPCContext npc, string reason, float gameTime)
        {
            var args = new NPCDespawnedEventArgs
            {
                Npc = npc,
                Reason = reason,
                GameTime = gameTime
            };
            SafeInvoke(NPCDespawned, args);
            return args;
        }

        public NPCRequestingAIEventArgs RaiseNpcRequestingAi(
            INPCContext npc, string playerInput,
            string interactionType, float gameTime)
        {
            var args = new NPCRequestingAIEventArgs
            {
                Npc = npc,
                PlayerInput = playerInput,
                InteractionType = interactionType,
                GameTime = gameTime
            };
            SafeInvoke(NPCRequestingAI, args);
            return args;
        }

        public NPCDialogueEventArgs RaiseNpcDialogue(
            INPCContext npc, string dialogue, string action,
            string emotion, string intent, float gameTime)
        {
            var args = new NPCDialogueEventArgs
            {
                Npc = npc,
                Dialogue = dialogue,
                Action = action,
                Emotion = emotion,
                Intent = intent,
                GameTime = gameTime
            };
            SafeInvoke(NPCDialogue, args);
            return args;
        }

        public PlayerInteractionEventArgs RaisePlayerInteraction(
            INPCContext npc, string kind, string playerText, float gameTime)
        {
            var args = new PlayerInteractionEventArgs
            {
                Npc = npc,
                Kind = kind,
                PlayerText = playerText,
                GameTime = gameTime
            };
            SafeInvoke(PlayerInteraction, args);
            return args;
        }

        public PlayerWeaponChangedEventArgs RaisePlayerWeaponChanged(
            bool isArmed, string weaponHashName, float gameTime)
        {
            var args = new PlayerWeaponChangedEventArgs
            {
                IsArmed = isArmed,
                WeaponHashName = weaponHashName,
                GameTime = gameTime
            };
            SafeInvoke(PlayerWeaponChanged, args);
            return args;
        }

        private void SafeInvoke<TArgs>(
            EventHandler<TArgs> handler, TArgs args)
            where TArgs : SentienceEventArgs
        {
            if (handler == null) return;

            // Snapshot delegate list so concurrent unsubscribes are safe.
            var invocationList = handler.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                var d = invocationList[i];
                try
                {
                    ((EventHandler<TArgs>)d).Invoke(this, args);
                }
                catch (Exception ex)
                {
                    _onHandlerFault(d, ex);
                }
            }
        }

        /// <summary>
        /// Remove every handler that belongs to <paramref name="targetType"/>'s
        /// declaring assembly.  Called when a plugin is being unloaded /
        /// quarantined so its handlers never fire again.
        /// </summary>
        public void DropHandlersForAssembly(System.Reflection.Assembly assembly)
        {
            if (assembly == null) return;
            NPCSpawned          = DropFrom(NPCSpawned, assembly);
            NPCDespawned        = DropFrom(NPCDespawned, assembly);
            NPCRequestingAI     = DropFrom(NPCRequestingAI, assembly);
            NPCDialogue         = DropFrom(NPCDialogue, assembly);
            PlayerInteraction   = DropFrom(PlayerInteraction, assembly);
            PlayerWeaponChanged = DropFrom(PlayerWeaponChanged, assembly);
        }

        private static EventHandler<TArgs> DropFrom<TArgs>(
            EventHandler<TArgs> root,
            System.Reflection.Assembly assembly)
            where TArgs : SentienceEventArgs
        {
            if (root == null) return null;
            EventHandler<TArgs> kept = null;
            foreach (var d in root.GetInvocationList())
            {
                if (d.Method.DeclaringType?.Assembly != assembly)
                    kept += (EventHandler<TArgs>)d;
            }
            return kept;
        }
    }
}
