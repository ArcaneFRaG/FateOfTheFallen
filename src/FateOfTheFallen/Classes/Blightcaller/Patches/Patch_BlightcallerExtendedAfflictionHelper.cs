using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    internal static class ExtendedAfflictionHelper
    {
        // ============================================================
        // APPLY EXTENDED AFFLICTION
        // ============================================================

        internal static void Apply(
            Stats stats,
            int statusEffectIndex,
            Character casterFallback = null)
        {
            if (stats == null ||
                stats.StatusEffects == null)
            {
                return;
            }

            if (statusEffectIndex < 0 ||
                statusEffectIndex >= stats.StatusEffects.Length)
            {
                return;
            }

            StatusEffect statusEffect =
                stats.StatusEffects[statusEffectIndex];

            if (statusEffect == null ||
                statusEffect.Effect == null)
            {
                return;
            }

            Spell spell =
                statusEffect.Effect;


            // ========================================================
            // MUST BE A PERSISTENT STATUS EFFECT
            // ========================================================

            if (spell.Type != Spell.SpellType.StatusEffect)
            {
                return;
            }


            // ========================================================
            // MUST BE A BLIGHTCALLER SPELL
            // ========================================================

            if (string.IsNullOrEmpty(spell.Id) ||
                !spell.Id.StartsWith(
                    "ARCBLC_",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }


            // ========================================================
            // MUST BE A HOSTILE DOT / DEBUFF
            // ========================================================

            if (!IsExtendedAfflictionLine(spell.Line))
            {
                return;
            }


            // ========================================================
            // IDENTIFY CASTER
            // ========================================================

            Character caster =
                statusEffect.CreditDPS;

            if (caster == null)
            {
                caster =
                    statusEffect.Owner;
            }

            if (caster == null)
            {
                caster =
                    casterFallback;
            }

            if (caster == null ||
                caster.MySkills == null)
            {
                return;
            }


            // ========================================================
            // GET ASCENSION MULTIPLIER
            // ========================================================

            float multiplier =
                BlightcallerAscensions.GetExtendedAfflictionMultiplier(
                    caster.MySkills);

            if (multiplier <= 1f)
            {
                return;
            }


            // ========================================================
            // EXTEND RUNTIME DURATION
            // ========================================================

            if (statusEffect.Duration <= 0f)
            {
                return;
            }

            statusEffect.Duration *= multiplier;
        }


        // ============================================================
        // SPELL LINE CLASSIFICATION
        // ============================================================

        private static bool IsExtendedAfflictionLine(
            Spell.SpellLine line)
        {
            switch (line)
            {
                // ----------------------------------------------------
                // DAMAGE OVER TIME
                // ----------------------------------------------------

                case Spell.SpellLine.Global_Poison_DOT:
                case Spell.SpellLine.Global_Void_DOT:
                case Spell.SpellLine.Global_Magic_DOT:

                // ----------------------------------------------------
                // DEBUFFS
                // ----------------------------------------------------

                case Spell.SpellLine.Global_Resists:
                case Spell.SpellLine.Global_Other_Debuff:

                    return true;

                default:
                    return false;
            }
        }
    }


    // =================================================================
    // 3-ARGUMENT AddStatusEffect
    // =================================================================

    [HarmonyPatch(
        typeof(Stats),
        "AddStatusEffect",
        new Type[]
        {
            typeof(Spell),
            typeof(bool),
            typeof(int)
        })]
    internal static class Patch_ExtendedAffliction_AddStatusEffect3
    {
        private static void Postfix(
            Stats __instance,
            Spell spell,
            bool _fromPlayer,
            int _dmgBonus,
            int __result)
        {
            if (__instance == null ||
                spell == null)
            {
                return;
            }

            if (!_fromPlayer)
            {
                return;
            }

            if (!BlightcallerAscensions.IsBlightcallerPlayer())
            {
                return;
            }

            Character caster = null;

            if (GameData.PlayerControl != null &&
                GameData.PlayerControl.Myself != null)
            {
                caster =
                    GameData.PlayerControl.Myself;
            }

            ExtendedAfflictionHelper.Apply(
                __instance,
                __result,
                caster);
        }
    }


    // =================================================================
    // 4-ARGUMENT AddStatusEffect
    // =================================================================

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
    internal static class Patch_ExtendedAffliction_AddStatusEffect4
    {
        private static void Postfix(
            Stats __instance,
            Spell spell,
            bool _fromPlayer,
            int _dmgBonus,
            Character _specificCaster,
            int __result)
        {
            if (__instance == null ||
                spell == null)
            {
                return;
            }

            ExtendedAfflictionHelper.Apply(
                __instance,
                __result,
                _specificCaster);
        }
    }


    // =================================================================
    // 5-ARGUMENT AddStatusEffect
    // =================================================================

    [HarmonyPatch(
        typeof(Stats),
        "AddStatusEffect",
        new Type[]
        {
            typeof(Spell),
            typeof(bool),
            typeof(int),
            typeof(Character),
            typeof(float)
        })]
    internal static class Patch_ExtendedAffliction_AddStatusEffect5
    {
        private static void Postfix(
            Stats __instance,
            Spell spell,
            bool _fromPlayer,
            int _dmgBonus,
            Character _specificCaster,
            float _duration,
            int __result)
        {
            if (__instance == null ||
                spell == null)
            {
                return;
            }

            ExtendedAfflictionHelper.Apply(
                __instance,
                __result,
                _specificCaster);
        }
    }
}