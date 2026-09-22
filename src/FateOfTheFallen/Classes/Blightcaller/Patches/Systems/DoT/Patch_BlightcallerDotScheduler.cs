using HarmonyLib;
using System;
using UnityEngine;

namespace FateOfTheFallen
{
    // ================================================================
    // BLIGHTCALLER DOT SCHEDULER - GLOBAL FRAME DRIVER
    // ================================================================

    [HarmonyPatch(
        typeof(Stats),
        "Update")]
    internal static class Patch_BlightcallerDotSchedulerUpdate
    {
        private static int LastUpdatedFrame =
            -1;

        private static void Postfix(
            Stats __instance)
        {
            if (__instance == null)
            {
                return;
            }

            Stats playerStats =
                GameData.PlayerStats;

            if (playerStats == null)
            {
                return;
            }

            /*
             * Stats.Update runs on many Characters.
             *
             * Only the player's Stats instance is allowed to drive
             * the global Blightcaller DoT scheduler.
             */
            if (__instance != playerStats)
            {
                return;
            }

            int currentFrame =
                Time.frameCount;

            /*
             * Absolute safety guard:
             * UpdateAll() may run at most once per Unity frame.
             */
            if (LastUpdatedFrame == currentFrame)
            {
                return;
            }

            LastUpdatedFrame =
                currentFrame;

            BlightcallerDotScheduler.UpdateAll();
        }
    }


    // ================================================================
    // IMMEDIATE DOT REGISTRATION
    // ================================================================
    //
    // Native spell resolution applies status effects through:
    //
    // Stats.AddStatusEffect(
    //     Spell,
    //     bool,
    //     int,
    //     Character)
    //
    // Register the DoT immediately after native application so
    // Accelerated Decay begins counting from application time rather
    // than waiting for the next native TickEffects cycle.
    // ================================================================

    [HarmonyPatch(
        typeof(Stats),
        "AddStatusEffect",
        new Type[]
        {
            typeof(Spell),
            typeof(bool),
            typeof(int),
            typeof(Character)
        })]
    internal static class Patch_BlightcallerDotSchedulerStatusEffectApplied
    {
        private static void Postfix(
            Stats __instance,
            Spell __0)
        {
            if (__instance == null)
            {
                return;
            }

            Spell effect =
                __0;

            if (effect == null)
            {
                return;
            }

            /*
             * Do absolutely nothing for native/non-Blightcaller
             * status effects.
             *
             * This keeps the hook extremely cheap for the rest
             * of the game.
             */
            if (!BlightcallerAscensions.IsBlightcallerDoT(
                    effect))
            {
                return;
            }

            if (__instance.StatusEffects == null)
            {
                return;
            }

            /*
             * Native AddStatusEffect has completed.
             *
             * TrackStatusEffects will find the newly installed
             * Blightcaller DoT and Upsert it into ActiveDots.
             *
             * If native application failed/rejected the effect,
             * there will simply be nothing new to register.
             */
            BlightcallerDotScheduler.TrackStatusEffects(
                __instance);
        }
    }


    // ================================================================
    // NATIVE DOT TICK NOTIFICATION
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

            if (__instance.StatusEffects == null)
            {
                return;
            }

            /*
             * Keep this as a low-frequency fallback.
             *
             * Normally AddStatusEffect registers our DoTs immediately.
             * This scan protects against unusual restoration/hot-reload
             * paths where a Blightcaller DoT already exists without
             * having passed through our AddStatusEffect hook.
             */
            BlightcallerDotScheduler.TrackStatusEffects(
                __instance);

            /*
             * IMPORTANT:
             *
             * OnNativeTick is cleanup-only.
             * It must NOT reset Accelerated Decay's independent timer.
             */
            BlightcallerDotScheduler.OnNativeTick(
                __instance);
        }
    }
}