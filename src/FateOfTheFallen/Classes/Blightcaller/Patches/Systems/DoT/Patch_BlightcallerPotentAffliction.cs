using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    // ================================================================
    // POTENT AFFLICTION
    // ================================================================
    //
    // Applies the Blightcaller Potent Affliction Ascension to all
    // outgoing damage originating from a Blightcaller.
    //
    // Rank 0 = 100%
    // Rank 1 = 110%
    // Rank 2 = 120%
    // Rank 3 = 130%
    //
    // We patch the native damage receivers rather than individual
    // spells. This means direct spells, DoTs, wand bolts, AoE damage,
    // and other damage paths using these methods are covered.
    //
    // Accelerated Decay is intentionally NOT handled here.
    // ================================================================


    // ================================================================
    // MAGIC DAMAGE
    // ================================================================

    [HarmonyPatch(
        typeof(Character),
        "MagicDamageMe")]
    internal static class Patch_PotentAfflictionMagicDamage
    {
        private static void Prefix(
            ref int _dmg,
            Character _attacker)
        {
            // Nothing to modify.
            if (_dmg <= 0)
            {
                return;
            }

            // No attacker means there is no Blightcaller source
            // that we can identify.
            if (_attacker == null)
            {
                return;
            }

            // The attacker must have a UseSkill component.
            if (_attacker.MySkills == null)
            {
                return;
            }

            float multiplier =
                BlightcallerAscensions.GetPotentAfflictionMultiplier(
                    _attacker.MySkills);

            // Rank 0, non-Blightcaller, or otherwise inactive.
            if (multiplier <= 1f)
            {
                return;
            }

            _dmg =
                Mathf.RoundToInt(
                    (float)_dmg * multiplier);
        }
    }


    // ================================================================
    // GENERAL / PHYSICAL DAMAGE
    // ================================================================

    [HarmonyPatch(
        typeof(Character),
        "DamageMe")]
    internal static class Patch_PotentAfflictionDamage
    {
        private static void Prefix(
            ref int _incdmg,
            Character _attacker)
        {
            // Nothing to modify.
            if (_incdmg <= 0)
            {
                return;
            }

            // No attacker means there is no Blightcaller source
            // that we can identify.
            if (_attacker == null)
            {
                return;
            }

            // The attacker must have a UseSkill component.
            if (_attacker.MySkills == null)
            {
                return;
            }

            float multiplier =
                BlightcallerAscensions.GetPotentAfflictionMultiplier(
                    _attacker.MySkills);

            // Rank 0, non-Blightcaller, or otherwise inactive.
            if (multiplier <= 1f)
            {
                return;
            }

            _incdmg =
                Mathf.RoundToInt(
                    (float)_incdmg * multiplier);
        }
    }
}