using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using static FateOfTheFallen.Plugin;

namespace FateOfTheFallen
{
    // ================================================================
    // BLIGHTCALLER DOT SCHEDULER
    // ================================================================

    [HarmonyPatch(
        typeof(Stats),
        "Update")]
    internal static class Patch_BlightcallerDotSchedulerUpdate
    {
        private static void Postfix(
            Stats __instance)
        {
            if (__instance == null)
            {
                return;
            }

            if (__instance.StatusEffects == null)
            {
                return;
            }

            BlightcallerDotScheduler.TrackStatusEffects(
                __instance);

            BlightcallerDotScheduler.Update(
                __instance);
        }
    }


    // ================================================================
    // NATIVE DOT TICK NOTIFICATION
    // ================================================================
    //
    // We no longer modify the native DoT damage value here.
    //
    // Potent Affliction is now handled centrally by:
    //
    //     Character.MagicDamageMe()
    //     Character.DamageMe()
    //
    // This keeps Potent Affliction from being applied twice and allows
    // it to affect ALL Blightcaller damage rather than only DoTs.
    //
    // Accelerated Decay remains handled by the Blightcaller DoT
    // scheduler and is completely independent of Potent Affliction.
    // ================================================================

    [HarmonyPatch(
        typeof(Stats),
        "TickEffects")]
    internal static class Patch_BlightcallerDotSchedulerNativeTick
    {
        private static void Postfix(
            Stats __instance)
        {
            if (__instance == null)
            {
                return;
            }

            BlightcallerDotScheduler.OnNativeTick(
                __instance);
        }
    }
}