using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest/corpse bridge for the Gorath world-boss runtime.
    /// Any NPC whose canonical name contains "Gorath" will award Quest 10
    /// kill credit and receive the guaranteed Infernal Heart corpse drop.
    /// Delaying the corpse append by two frames lets the boss's normal loot
    /// resolver finish first, so the quest item is additive rather than a
    /// replacement for normal boss rewards.
    /// </summary>
    internal static class GorathQuestDeathBridge
    {
        private static readonly HashSet<int> Pending =
            new HashSet<int>();

        private static readonly HashSet<int> Processed =
            new HashSet<int>();

        internal static void TrySchedule(Character character)
        {
            if (character == null ||
                !character.isNPC ||
                character.MyStats == null ||
                (character.Alive && character.MyStats.CurrentHP > 0) ||
                !IsGorath(character))
            {
                return;
            }

            int id = character.GetInstanceID();

            if (Processed.Contains(id) || Pending.Contains(id))
                return;

            Pending.Add(id);
            CoroutineHost.Start(ResolveAfterDeath(character, id));
        }

        private static IEnumerator ResolveAfterDeath(
            Character character,
            int instanceId)
        {
            yield return null;
            yield return null;

            Pending.Remove(instanceId);

            if (character == null ||
                Processed.Contains(instanceId) ||
                character.MyStats == null ||
                (character.Alive && character.MyStats.CurrentHP > 0) ||
                !IsGorath(character))
            {
                yield break;
            }

            Processed.Add(instanceId);

            NPC npc = character.MyNPC;
            if (npc == null)
                npc = character.GetComponent<NPC>();

            if (npc != null)
            {
                LateWorldBossLootTable.ResolveGorathOnDeath(
                    npc);

                GorathQuestContent.EnsureLootableCorpse(
                    npc);
            }

            if (GorathQuest.IsActive() &&
                !GorathQuest.IsComplete())
            {
                GorathQuest.NotifyGorathDefeated(
                    "Gorath quest death bridge");

                if (npc != null)
                {
                    GorathQuestContent.TryArmInfernalHeartOnCorpse(
                        npc);
                }
            }
        }

        private static bool IsGorath(Character character)
        {
            if (character == null)
                return false;

            string name = null;

            if (character.MyStats != null &&
                !string.IsNullOrEmpty(character.MyStats.MyName))
            {
                name = character.MyStats.MyName;
            }
            else if (character.MyNPC != null &&
                     !string.IsNullOrEmpty(character.MyNPC.NPCName))
            {
                name = character.MyNPC.NPCName;
            }
            else if (character.gameObject != null)
            {
                name = character.gameObject.name;
            }

            return
                !string.IsNullOrEmpty(name) &&
                name.IndexOf(
                    "Gorath",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    [HarmonyPatch(
        typeof(Character),
        "DoDeath")]
    internal static class Patch_GorathQuest_DoDeath
    {
        [HarmonyPostfix]
        private static void Postfix(
            Character __instance)
        {
            GorathQuestDeathBridge.TrySchedule(
                __instance);
        }
    }


    [HarmonyPatch(
        typeof(Character),
        "DamageMe",
        new Type[]
        {
            typeof(int),
            typeof(bool),
            typeof(GameData.DamageType),
            typeof(Character),
            typeof(bool),
            typeof(bool),
            typeof(int)
        })]
    internal static class Patch_GorathQuest_DamageMe
    {
        [HarmonyPostfix]
        private static void Postfix(Character __instance)
        {
            GorathQuestDeathBridge.TrySchedule(__instance);
        }
    }

    [HarmonyPatch(
        typeof(Character),
        "MagicDamageMe",
        new Type[]
        {
            typeof(int),
            typeof(bool),
            typeof(GameData.DamageType),
            typeof(Character),
            typeof(float),
            typeof(int)
        })]
    internal static class Patch_GorathQuest_MagicDamageMe
    {
        [HarmonyPostfix]
        private static void Postfix(Character __instance)
        {
            GorathQuestDeathBridge.TrySchedule(__instance);
        }
    }

    [HarmonyPatch(
        typeof(Character),
        "BleedDamageMe",
        new Type[]
        {
            typeof(int),
            typeof(bool),
            typeof(Character)
        })]
    internal static class Patch_GorathQuest_BleedDamageMe
    {
        [HarmonyPostfix]
        private static void Postfix(Character __instance)
        {
            GorathQuestDeathBridge.TrySchedule(__instance);
        }
    }
}
