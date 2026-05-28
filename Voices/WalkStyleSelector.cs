// =============================================================================
//  Voices · WalkStyle Selector
// -----------------------------------------------------------------------------
//  Assigns a movement clipset to a freshly-spawned NPC based on its
//  archetype.  Pure visual polish — makes the open world feel less
//  homogenous (a biker swaggers, a businessman strides).  This is the
//  LSPDFR-parity feature we promised to match TTP3.1's walkstyle library.
//
//  Usage:
//      WalkStyleSelector.Apply(ped, archetype.Walkstyles, rng);
//  Safe to call on null / unloaded peds: returns silently.
//
//  GTA V requires the walk clipset to be loaded before SET_PED_MOVEMENT_-
//  CLIPSET applies cleanly.  We REQUEST_ANIM_SET first; if it isn't loaded
//  this tick the call still succeeds, just won't visibly change anything
//  on the very first frame.
// =============================================================================
using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;

namespace GTA5MOD2026.Voices
{
    public static class WalkStyleSelector
    {
        // Tracks which clipsets we've already requested in this session so
        // we don't spam REQUEST_ANIM_SET each spawn.
        private static readonly HashSet<string> _requested
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Pick a random walkstyle from the supplied list and apply it to
        /// the ped.  No-op if list is empty / null.
        /// </summary>
        public static void Apply(Ped ped,
            IReadOnlyList<string> walkstyles, Random rng)
        {
            if (ped == null || !ped.Exists()) return;
            if (walkstyles == null || walkstyles.Count == 0) return;
            if (rng == null) rng = new Random();

            string clip = walkstyles[rng.Next(walkstyles.Count)];
            if (string.IsNullOrWhiteSpace(clip)) return;

            try
            {
                if (!_requested.Contains(clip))
                {
                    Function.Call(Hash.REQUEST_ANIM_SET, clip);
                    _requested.Add(clip);
                }
                Function.Call(Hash.SET_PED_MOVEMENT_CLIPSET,
                    ped.Handle, clip, 1.0f);
            }
            catch { /* visual-only — never crash */ }
        }
    }
}
