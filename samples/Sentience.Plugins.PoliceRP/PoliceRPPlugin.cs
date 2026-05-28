// =============================================================================
//  Sentience Plugin · PoliceRP (sample)
// -----------------------------------------------------------------------------
//  Showcases the Sentience SDK with a tiny "police roleplay" plugin:
//
//      • When the player draws a weapon, mark NPCs nearby as "spooked".
//      • For every spooked NPC the plugin biases the system prompt toward
//        calling the cops or fleeing.
//      • The plugin owns no game state — it only reads SDK events and
//        appends text to NPCRequestingAIEventArgs.ExtraSystemPrompt.
//
//  Build and drop the DLL into <GTA V>\scripts\SentiencePlugins\.
//  Sentience auto-discovers it on next launch.
// =============================================================================
using System;
using System.Collections.Generic;
using GTA5MOD2026.SDK;

namespace Sentience.Plugins.PoliceRP
{
    public sealed class PoliceRPPlugin : ISentiencePlugin
    {
        public string Name          => "Sentience.PoliceRP (Sample)";
        public string Author        => "NexusV";
        public string Version       => "1.0.0";
        public string MinSdkVersion => "1.0.0";

        // Set of NPC unique ids that are "spooked" until the player holsters.
        private readonly HashSet<string> _spooked
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private IPluginContext _ctx;
        private bool _playerArmed;

        public void OnLoad(IPluginContext context)
        {
            _ctx = context;
            _ctx.Logger.Info("PoliceRP sample plugin loaded.");

            _ctx.Events.PlayerWeaponChanged += OnPlayerWeaponChanged;
            _ctx.Events.NPCRequestingAI += OnNpcRequestingAi;
            _ctx.Events.NPCDespawned += OnNpcDespawned;
        }

        public void OnUnload()
        {
            if (_ctx == null) return;
            _ctx.Events.PlayerWeaponChanged -= OnPlayerWeaponChanged;
            _ctx.Events.NPCRequestingAI -= OnNpcRequestingAi;
            _ctx.Events.NPCDespawned -= OnNpcDespawned;
            _ctx.Logger.Info("PoliceRP sample plugin unloaded.");
        }

        // ──────────────────────────────────────────────────────────────
        //  Event handlers
        // ──────────────────────────────────────────────────────────────

        private void OnPlayerWeaponChanged(object sender,
            PlayerWeaponChangedEventArgs args)
        {
            _playerArmed = args.IsArmed;
            if (!_playerArmed)
            {
                // Holstered — clear all flagged NPCs so behavior returns to normal.
                _spooked.Clear();
                _ctx.Logger.Debug("Player holstered — clearing spooked list.");
            }
            else
            {
                _ctx.Logger.Info($"Player drew {args.WeaponHashName}.");
            }
        }

        private void OnNpcRequestingAi(object sender,
            NPCRequestingAIEventArgs args)
        {
            if (!_playerArmed) return;
            if (args.Npc == null) return;

            // Mark this NPC as spooked the moment the host first asks for
            // its line — we don't need an explicit "scan radius".
            _spooked.Add(args.Npc.UniqueId);

            // Police archetype shouldn't panic — they engage instead.
            if (string.Equals(args.Npc.Archetype, "police",
                StringComparison.OrdinalIgnoreCase))
            {
                args.ExtraSystemPrompt = string.Join("\n", new[]
                {
                    "【PoliceRP 插件】玩家拔出了武器。",
                    "你是警察，立刻喝令对方放下武器；如果对方拒绝，使用 aim 或 attack 动作。",
                    "不要逃跑，不要 call_cops（你就是警察）。"
                });
                return;
            }

            // Civilian → encourage fear / call_cops / flee.
            args.ExtraSystemPrompt = string.Join("\n", new[]
            {
                "【PoliceRP 插件】对方刚刚拔出武器，你看见了。",
                "你应该感到害怕；优先输出 call_cops 或 flee 动作。",
                "情绪倾向 fear。不要嘲讽对方，不要硬碰硬。"
            });
        }

        private void OnNpcDespawned(object sender,
            NPCDespawnedEventArgs args)
        {
            if (args.Npc != null) _spooked.Remove(args.Npc.UniqueId);
        }
    }
}
