using System;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Hard stop at native StartSpell. The native Sim AI can independently
    /// select spells from SimPlayer.Spellbook even when our custom healer AI
    /// has correctly decided not to precast wards. Only our 12 authored
    /// Lightbringer Sims are affected; players/other classes are untouched.
    /// </summary>
    [HarmonyPatch(typeof(CastSpell), "StartSpell", new Type[]
    {
        typeof(Spell), typeof(Stats)
    })]
    [HarmonyPriority(Priority.First)]
    internal static class Patch_LightbringerSimOutOfCombatGuard
    {
        private static float _nextWarning;

        private static bool Prefix(CastSpell __instance, Spell _spell,
            ref bool __result)
        {
            if (__instance == null || _spell == null) return true;
            SimPlayer sim = __instance.GetComponent<SimPlayer>();
            if (sim == null) sim = __instance.GetComponentInParent<SimPlayer>();
            if (sim == null || !LightbringerSimPlayers.IsAuthored(sim)) return true;

            // One authoritative restriction shared by BOTH cast selectors.
            // Do not allow native off-combat buffing, wards, hybrid heals, or
            // offensive spells to bypass the class healer controller.
            if (LightbringerSimCombatController.HasVerifiedCombatThreat(sim))
                return true;

            __result = false;
            if (Time.unscaledTime >= _nextWarning)
            {
                _nextWarning = Time.unscaledTime + 8f;
                Plugin.LogInfo("[LBSimCombatGuard] Blocked idle spell " +
                    _spell.SpellName + " from " + sim.MyStats.MyName);
            }
            return false;
        }
    }
}
