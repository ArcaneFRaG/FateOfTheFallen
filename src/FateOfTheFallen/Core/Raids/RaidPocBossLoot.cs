using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// PoC boss reward + persistent completion-credit bridge.
    ///
    /// The reward intentionally uses the native LootTable/corpse path so this
    /// milestone proves real raid-boss loot rather than direct inventory
    /// injection. Completion credit is stored in FOTF's per-character
    /// milestone store, never in GameData.CompletedQuests.
    /// </summary>
    internal static class RaidPocBossLoot
    {
        internal const string RewardItemId =
            "FOTF_RAID_POC_BLIGHTED_COLOSSUS_CORE";

        internal const string RewardItemName =
            "Blighted Colossus Core [PoC]";

        internal const string CompletionCreditId =
            "FOTF_RAID_POC_BLIGHTED_COLOSSUS_DEFEATED";

        private static Item _rewardItem;

        internal static Item RegisterItem(
            ItemDatabase database)
        {
            if (_rewardItem != null)
            {
                if (database != null)
                {
                    CustomItemRegistry.RegisterWithNativeDatabase(
                        database,
                        _rewardItem);
                }

                return _rewardItem;
            }

            Sprite icon =
                ResolvePlaceholderIcon(
                    database);

            _rewardItem =
                CustomItemRegistry.CreateItem(
                    id:
                        RewardItemId,

                    itemName:
                        RewardItemName,

                    lore:
                        "A dense, diseased core torn from the Blighted Colossus. " +
                        "It still pulses with concentrated corruption. This is a " +
                        "proof-of-concept raid reward and will be replaced by the " +
                        "final encounter loot table.",

                    slot:
                        Item.SlotType.General,

                    icon:
                        icon,

                    itemLevel:
                        35,

                    itemValue:
                        500,

                    stackable:
                        true,

                    disposable:
                        false,

                    unique:
                        false,

                    playerCannotSell:
                        false,

                    noTradeNoDestroy:
                        false,

                    simPlayersCantGet:
                        true);

            if (_rewardItem == null)
            {
                Plugin.NativeLog.LogError(
                    "[RaidPoC] failed to create PoC boss reward item [" +
                    RewardItemId +
                    "].");

                return null;
            }

            if (database != null)
            {
                CustomItemRegistry.RegisterWithNativeDatabase(
                    database,
                    _rewardItem);
            }


            return _rewardItem;
        }


        internal static void ConfigureBossLoot(
            NPC boss)
        {
            if (boss == null)
            {
                return;
            }

            LootTable lootTable =
                boss.GetComponent<LootTable>();

            if (lootTable == null)
            {
                lootTable =
                    boss.GetComponentInChildren<LootTable>(
                        true);
            }

            if (lootTable == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] boss has no LootTable component; encounter completion credit can still be awarded, but corpse loot cannot be tested.");

                return;
            }

            Item reward =
                _rewardItem ??
                CustomItemRegistry.GetItemById(
                    RewardItemId);

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
                    "[RaidPoC] boss reward item is unavailable; leaving the boss corpse without PoC reward loot.");

                return;
            }

            if (lootTable.ActualDrops == null)
            {
                lootTable.ActualDrops =
                    new List<Item>();
            }
            else
            {
                // The native shell was cloned from Blighted Thrall and may
                // still contain its guaranteed tissue reward. Boss loot owns
                // this table completely for the PoC.
                lootTable.ActualDrops.Clear();
            }

            if (lootTable.ActualDropsQual == null)
            {
                lootTable.ActualDropsQual =
                    new List<int>();
            }
            else
            {
                lootTable.ActualDropsQual.Clear();
            }

            lootTable.ActualDrops.Add(
                reward);

            lootTable.ActualDropsQual.Add(
                1);

        }


        internal static bool HasCompletionCredit()
        {
            return
                FOTFCharacterMilestoneStore
                    .HasCredit(
                        CompletionCreditId);
        }

        internal static bool AwardCompletionCredit()
        {
            bool awarded =
                FOTFCharacterMilestoneStore
                    .AwardCredit(
                        CompletionCreditId);

            if (!awarded)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] persistent boss completion credit could not be written to the FOTF milestone store.");
            }

            return awarded;
        }

        private static Sprite ResolvePlaceholderIcon(
            ItemDatabase database)
        {
            Item tissue =
                BlightedThrallLoot.GetItem();

            if (tissue != null &&
                tissue.ItemIcon != null)
            {
                return tissue.ItemIcon;
            }

            if (database == null)
            {
                return null;
            }

            if (database.ItemDBList != null)
            {
                for (int i = 0;
                     i < database.ItemDBList.Count;
                     i++)
                {
                    Item candidate =
                        database.ItemDBList[i];

                    if (IsSuitableIconSource(
                            candidate))
                    {
                        return candidate.ItemIcon;
                    }
                }
            }

            if (database.ItemDB != null)
            {
                for (int i = 0;
                     i < database.ItemDB.Length;
                     i++)
                {
                    Item candidate =
                        database.ItemDB[i];

                    if (IsSuitableIconSource(
                            candidate))
                    {
                        return candidate.ItemIcon;
                    }
                }
            }

            return null;
        }


        private static bool IsSuitableIconSource(
            Item item)
        {
            return item != null &&
                item.ItemIcon != null &&
                item.RequiredSlot == Item.SlotType.General;
        }
    }
}
