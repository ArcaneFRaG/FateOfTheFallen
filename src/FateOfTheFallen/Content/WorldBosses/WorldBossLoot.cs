using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Canonical reward-item registration and once-per-character kill credit
    /// for the first v0.2 world boss.
    ///
    /// The runtime-built Moldermaw uses MoldermawLootTable for actual drop
    /// resolution rather than receiving a native LootTable component.
    /// </summary>
    internal static class WorldBossLoot
    {
        private static Item _rewardItem;

        internal static Item RegisterItem(
            ItemDatabase database)
        {
            // Moldermaw's quest item has one canonical lifecycle:
            // MoldermawBlightedCore -> CustomItemRegistry -> native ItemDatabase.
            _rewardItem =
                MoldermawBlightedCore.RegisterItem(
                    database);

            return _rewardItem;
        }


        internal static void ConfigureBossLoot(
            NPC boss)
        {
            if (boss == null)
            {
                return;
            }

            LootTable table =
                boss.GetComponent<LootTable>();

            if (table == null)
            {
                table =
                    boss.GetComponentInChildren<LootTable>(true);
            }

            if (table == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[WorldBoss] boss has no LootTable component; kill credit remains available but corpse loot cannot be armed.");

                return;
            }

            Item reward =
                _rewardItem ??
                CustomItemRegistry.GetItemById(
                    WorldBossCatalog.BlightTouchedColossus.RewardItemId);

            if (reward == null &&
                GameData.ItemDB != null)
            {
                reward =
                    RegisterItem(
                        GameData.ItemDB);
            }

            if (reward == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[WorldBoss] reward item is unavailable; corpse loot was not armed.");

                return;
            }

            if (table.ActualDrops == null)
            {
                table.ActualDrops =
                    new List<Item>();
            }
            else
            {
                table.ActualDrops.Clear();
            }

            if (table.ActualDropsQual == null)
            {
                table.ActualDropsQual =
                    new List<int>();
            }
            else
            {
                table.ActualDropsQual.Clear();
            }

            table.ActualDrops.Add(reward);
            table.ActualDropsQual.Add(1);

        }

        internal static bool HasCompletionCredit()
        {
            return
                FOTFCharacterMilestoneStore
                    .HasCredit(
                        WorldBossCatalog
                            .BlightTouchedColossus
                            .CompletionCreditId);
        }

        internal static bool AwardCompletionCredit()
        {
            bool awarded =
                FOTFCharacterMilestoneStore
                    .AwardCredit(
                        WorldBossCatalog
                            .BlightTouchedColossus
                            .CompletionCreditId);

            if (!awarded)
            {
                Plugin.NativeLog.LogWarning(
                    "[WorldBoss] persistent kill credit could not be written to the FOTF milestone store.");
            }

            return awarded;
        }


    }
}