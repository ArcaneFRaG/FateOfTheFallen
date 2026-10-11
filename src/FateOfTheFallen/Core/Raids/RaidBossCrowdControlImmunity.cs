using System;
using HarmonyLib;

namespace FateOfTheFallen
{
    /// <summary>
    /// Prevents hard crowd-control spells from being installed on the raid boss
    /// while leaving ordinary damage, DoTs and non-CC debuffs untouched.
    /// </summary>
    internal static class RaidBossCrowdControlImmunity
    {
        internal static bool ShouldBlock(
            Stats stats,
            Spell spell)
        {
            if (stats == null ||
                spell == null)
            {
                return false;
            }

            bool protectedBoss =
                RaidPocEncounterRuntime.IsCurrentRaidBossStats(
                    stats) ||
                WorldBossRuntime.IsCurrentWorldBossStats(
                    stats) ||
                NhalSeraEncounterRuntime.IsCurrentBossStats(
                    stats) ||
                VarkeshEncounterRuntime.IsCurrentBossStats(
                    stats) ||
                GorathEncounterRuntime.IsCurrentBossStats(
                    stats);

            if (!protectedBoss)
            {
                return false;
            }

            return spell.CrowdControlSpell ||
                spell.FearTarget ||
                spell.RootTarget ||
                spell.StunTarget;
        }

        internal static void LogBlocked(
            Spell spell)
        {
        }
    }

    [HarmonyPatch(
        typeof(Stats),
        "AddStatusEffect",
        new Type[]
        {
            typeof(Spell),
            typeof(bool),
            typeof(int)
        })]
    internal static class Patch_RaidBossCcImmunity_AddStatusEffect3
    {
        private static bool Prefix(
            Stats __instance,
            Spell spell,
            ref int __result)
        {
            if (!RaidBossCrowdControlImmunity.ShouldBlock(
                    __instance,
                    spell))
            {
                return true;
            }

            __result =
                -1;

            RaidBossCrowdControlImmunity.LogBlocked(
                spell);

            return false;
        }
    }

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
    internal static class Patch_RaidBossCcImmunity_AddStatusEffect4
    {
        private static bool Prefix(
            Stats __instance,
            Spell spell,
            ref int __result)
        {
            if (!RaidBossCrowdControlImmunity.ShouldBlock(
                    __instance,
                    spell))
            {
                return true;
            }

            __result =
                -1;

            RaidBossCrowdControlImmunity.LogBlocked(
                spell);

            return false;
        }
    }

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
    internal static class Patch_RaidBossCcImmunity_AddStatusEffect5
    {
        private static bool Prefix(
            Stats __instance,
            Spell spell,
            ref int __result)
        {
            if (!RaidBossCrowdControlImmunity.ShouldBlock(
                    __instance,
                    spell))
            {
                return true;
            }

            __result =
                -1;

            RaidBossCrowdControlImmunity.LogBlocked(
                spell);

            return false;
        }
    }
}
