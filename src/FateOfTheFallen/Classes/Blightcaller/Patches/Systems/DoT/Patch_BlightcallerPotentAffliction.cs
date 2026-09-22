using HarmonyLib;
using System;
using UnityEngine;
using static GameData;

namespace FateOfTheFallen
{
    // ================================================================
    // POTENT AFFLICTION DAMAGE HANDLER
    // ================================================================
    //
    // Potent Affliction is applied only to the transient damage
    // argument entering Character damage methods.
    //
    // It never modifies:
    //
    //     Spell.TargetDamage
    //     StatusEffect.bonusDmg
    //     Stats.TickEffects locals
    //     scheduler state
    //
    // This prevents boosted damage from being fed back into later
    // hits and causing the old escalating damage ramp.
    //
    // Some native damage methods may call other patched damage methods
    // internally. The thread-local depth guard ensures the multiplier
    // is applied at most once per nested damage chain.
    // ================================================================

    internal static class BlightcallerPotentAfflictionDamage
    {
        [ThreadStatic]
        private static int DamageDepth;


        // ============================================================
        // BEGIN DAMAGE EVENT
        // ============================================================

        internal static bool Begin(
            Character attacker,
            ref int damage)
        {
            if (attacker == null)
            {
                return false;
            }

            if (attacker.MySkills == null)
            {
                return false;
            }

            float multiplier =
                BlightcallerAscensions
                    .GetPotentAfflictionMultiplier(
                        attacker.MySkills);

            if (multiplier <= 1f)
            {
                return false;
            }

            bool shouldApply =
                DamageDepth == 0;

            DamageDepth++;

            if (shouldApply &&
                damage > 0)
            {
                damage =
                    Mathf.RoundToInt(
                        damage *
                        multiplier);
            }

            return true;
        }


        // ============================================================
        // END DAMAGE EVENT
        // ============================================================

        internal static void End(
            bool entered)
        {
            if (!entered)
            {
                return;
            }

            if (DamageDepth > 0)
            {
                DamageDepth--;
            }
            else
            {
                DamageDepth = 0;
            }
        }
    }


    // ================================================================
    // STANDARD DAMAGE
    // ================================================================

    [HarmonyPatch(
        typeof(Character),
        "DamageMe",
        new Type[]
        {
            typeof(int),
            typeof(bool),
            typeof(DamageType),
            typeof(Character),
            typeof(bool),
            typeof(bool),
            typeof(int)
        })]
    internal static class Patch_BlightcallerPotentAfflictionDamage
    {
        private static void Prefix(
            ref int _incdmg,
            Character _attacker,
            out bool __state)
        {
            __state =
                BlightcallerPotentAfflictionDamage.Begin(
                    _attacker,
                    ref _incdmg);
        }

        private static void Postfix(
            bool __state)
        {
            BlightcallerPotentAfflictionDamage.End(
                __state);
        }
    }


    // ================================================================
    // MAGIC DAMAGE
    // ================================================================

    [HarmonyPatch(
        typeof(Character),
        "MagicDamageMe",
        new Type[]
        {
            typeof(int),
            typeof(bool),
            typeof(DamageType),
            typeof(Character),
            typeof(float),
            typeof(int)
        })]
    internal static class Patch_BlightcallerPotentAfflictionMagicDamage
    {
        private static void Prefix(
            ref int _dmg,
            Character _attacker,
            out bool __state)
        {
            __state =
                BlightcallerPotentAfflictionDamage.Begin(
                    _attacker,
                    ref _dmg);
        }

        private static void Postfix(
            bool __state)
        {
            BlightcallerPotentAfflictionDamage.End(
                __state);
        }
    }


    // ================================================================
    // BLEED DAMAGE
    // ================================================================

    [HarmonyPatch(
        typeof(Character),
        "BleedDamageMe",
        new Type[]
        {
            typeof(int),
            typeof(bool),
            typeof(Character)
        })]
    internal static class Patch_BlightcallerPotentAfflictionBleedDamage
    {
        private static void Prefix(
            ref int _incdmg,
            Character _attacker,
            out bool __state)
        {
            __state =
                BlightcallerPotentAfflictionDamage.Begin(
                    _attacker,
                    ref _incdmg);
        }

        private static void Postfix(
            bool __state)
        {
            BlightcallerPotentAfflictionDamage.End(
                __state);
        }
    }
}