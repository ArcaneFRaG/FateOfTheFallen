using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Nhal'Sera's native corpse-loot resolver.
    ///
    /// The live Nhal'Sera runtime deliberately does NOT carry a LootTable
    /// while alive because LootTable.InitLootTable() previously interfered
    /// with the custom runtime-built boss. Instead, this class creates/arms a
    /// disabled LootTable only after death, fills ActualDrops directly, and
    /// assigns that table to Character.MyLootTable so normal corpse clicking
    /// opens Erenshor's native loot window.
    /// </summary>
    internal static class NhalSeraLootTable
    {
        internal const float QuestItemDropChance =
            1.0f;


        private sealed class EquipmentEntry
        {
            internal string ItemId;
            internal float Weight;
        }


        private static readonly EquipmentEntry[] EquipmentPool =
        {
            // Final Nhal'Sera equipment distribution.  These weights total
            // exactly 100% for every equipment roll.
            new EquipmentEntry
            {
                ItemId = NhalSeraEquipment.CrownId,
                Weight = 14f
            },
            new EquipmentEntry
            {
                ItemId = NhalSeraEquipment.LightlessScepterId,
                Weight = 16f
            },
            new EquipmentEntry
            {
                ItemId = NhalSeraEquipment.GirdleId,
                Weight = 18f
            },
            new EquipmentEntry
            {
                ItemId = NhalSeraEquipment.CarapaceId,
                Weight = 24f
            },
            new EquipmentEntry
            {
                ItemId = NhalSeraEquipment.GraspId,
                Weight = 28f
            }
        };


        /// <summary>
        /// Resolve the rewards for this kill and place them on the dead
        /// Nhal'Sera corpse. Nothing is injected directly into PlayerInv.
        /// </summary>
        internal static void ResolveOnDeath(
            NPC boss)
        {
            if (boss == null)
            {
                Plugin.NativeLog.LogError(
                    "[NhalSeraLoot] death resolver received a null NPC; corpse loot could not be armed.");

                return;
            }

            List<Item> rewards =
                new List<Item>();

            // Quest 8's Shard is an extra quest drop and does not consume one
            // of Nhal'Sera's two normal world-boss reward slots.
            TryAddQuestItem(
                rewards);

            string firstEquipmentId;
            Item firstEquipment;

            if (!ResolveGuaranteedEquipment(
                    out firstEquipmentId,
                    out firstEquipment))
            {
                Plugin.NativeLog.LogError(
                    "[NhalSeraLoot] failed resolving Nhal'Sera's guaranteed first equipment reward. Corpse loot was not armed.");

                return;
            }

            rewards.Add(
                firstEquipment);

            string secondEquipmentId;
            Item secondReward;

            // Independent second equipment roll. Do not exclude the first
            // result: duplicate item drops are intentionally allowed.
            if (!ResolveGuaranteedEquipment(
                    out secondEquipmentId,
                    out secondReward))
            {
                Plugin.NativeLog.LogError(
                    "[NhalSeraLoot] failed resolving the second equipment reward. Corpse loot was not armed.");

                return;
            }

            rewards.Add(
                secondReward);

            if (!ArmCorpseLoot(
                    boss,
                    rewards))
            {
                Plugin.NativeLog.LogError(
                    "[NhalSeraLoot] reward roll completed but the native corpse LootTable could not be armed. Rewards were NOT injected into inventory.");

                return;
            }

            Plugin.NativeLog.LogInfo(
                "[NhalSeraLoot] native corpse loot armed. NormalRewards=[" +
                firstEquipment.ItemName +
                "] + [" +
                secondReward.ItemName +
                "] TotalCorpseSlots=[" +
                rewards.Count +
                "].");
        }


        /// <summary>
        /// Add Shard of Lightless Glass to the pending corpse drops when Q8
        /// requires it and the player does not already own one.
        /// </summary>
        private static bool TryAddQuestItem(
            List<Item> rewards)
        {
            if (rewards == null ||
                !NhalSeraQuest.IsActive() ||
                NhalSeraQuest.IsComplete())
            {
                return false;
            }

            Item core =
                ResolveQuestItem();

            if (core == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[NhalSeraLoot] quest item [1706] could not be resolved; guaranteed Shard was skipped.");

                return false;
            }

            if (PlayerAlreadyHasShard(
                    core))
            {
                Plugin.NativeLog.LogInfo(
                    "[NhalSeraLoot] player already owns Shard of Lightless Glass [1706]; duplicate corpse drop suppressed.");

                return false;
            }

            rewards.Add(
                core);

            Plugin.NativeLog.LogInfo(
                "[NhalSeraLoot] added guaranteed quest Shard [1706] to corpse loot.");

            return true;
        }


        internal static bool PlayerHasQuestItem()
        {
            Item core =
                ResolveQuestItem();

            return
                core != null &&
                PlayerAlreadyHasShard(
                    core);
        }


        private static Item ResolveQuestItem()
        {
            Item core =
                CustomItemRegistry.GetItemById(
                    ShardOfLightlessGlass.ItemId);

            if (core != null)
            {
                return core;
            }

            if (GameData.ItemDB != null)
            {
                core =
                    CustomItemRegistry.Resolve(
                        ShardOfLightlessGlass.ItemId,
                        GameData.ItemDB);

                if (core != null)
                {
                    return core;
                }
            }

            return
                ShardOfLightlessGlass.GetItem();
        }


        private static bool PlayerAlreadyHasShard(
            Item canonicalShard)
        {
            if (canonicalShard == null ||
                GameData.PlayerInv == null)
            {
                return false;
            }

            try
            {
                if (GameData.PlayerInv.HasItem(
                        canonicalShard,
                        false))
                {
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                List<ItemIcon> slots =
                    GameData.PlayerInv.ALLSLOTS;

                if (slots != null)
                {
                    for (int i = 0;
                         i < slots.Count;
                         i++)
                    {
                        Item item =
                            slots[i] != null
                                ? slots[i].MyItem
                                : null;

                        if (item != null &&
                            string.Equals(
                                item.Id,
                                ShardOfLightlessGlass.ItemId,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }

            return false;
        }


        // ============================================================
        // NORMAL WORLD-BOSS REWARD ROLL
        // ============================================================

        private static bool ResolveGuaranteedEquipment(
            out string awardedId,
            out Item awardedItem)
        {
            awardedId =
                null;

            awardedItem =
                null;

            HashSet<string> attempted =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            for (int attempt = 0;
                 attempt < EquipmentPool.Length;
                 attempt++)
            {
                string candidateId =
                    RollEquipmentIdExcluding(
                        attempted);

                if (string.IsNullOrEmpty(
                        candidateId))
                {
                    break;
                }

                attempted.Add(
                    candidateId);

                Item candidate =
                    ResolveEquipmentItem(
                        candidateId);

                if (candidate == null)
                {
                    continue;
                }

                awardedId =
                    candidateId;

                awardedItem =
                    candidate;

                return true;
            }

            return false;
        }


        private static string RollEquipmentIdExcluding(
            HashSet<string> excludedIds)
        {
            float totalWeight =
                0f;

            for (int i = 0;
                 i < EquipmentPool.Length;
                 i++)
            {
                EquipmentEntry entry =
                    EquipmentPool[i];

                if (entry == null ||
                    entry.Weight <= 0f ||
                    (excludedIds != null &&
                     excludedIds.Contains(
                         entry.ItemId)))
                {
                    continue;
                }

                totalWeight +=
                    entry.Weight;
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float roll =
                UnityEngine.Random.Range(
                    0f,
                    totalWeight);

            float cursor =
                0f;

            for (int i = 0;
                 i < EquipmentPool.Length;
                 i++)
            {
                EquipmentEntry entry =
                    EquipmentPool[i];

                if (entry == null ||
                    entry.Weight <= 0f ||
                    (excludedIds != null &&
                     excludedIds.Contains(
                         entry.ItemId)))
                {
                    continue;
                }

                cursor +=
                    entry.Weight;

                if (roll <
                    cursor)
                {
                    return
                        entry.ItemId;
                }
            }

            return null;
        }


        private static Item ResolveEquipmentItem(
            string itemId)
        {
            if (string.IsNullOrEmpty(
                    itemId))
            {
                return null;
            }

            Item item =
                CustomItemRegistry.GetItemById(
                    itemId);

            if (item != null)
            {
                return item;
            }

            if (GameData.ItemDB != null)
            {
                item =
                    CustomItemRegistry.Resolve(
                        itemId,
                        GameData.ItemDB);
            }

            return item;
        }


        // ============================================================
        // NATIVE CORPSE LOOT BRIDGE
        // ============================================================

        private static bool ArmCorpseLoot(
            NPC boss,
            List<Item> rewards)
        {
            if (boss == null ||
                rewards == null ||
                rewards.Count < 2)
            {
                return false;
            }

            try
            {
                GameObject corpse =
                    boss.gameObject;

                if (corpse == null)
                {
                    return false;
                }

                Character host =
                    boss.GetChar();

                if (host == null)
                {
                    host =
                        corpse.GetComponent<Character>();
                }

                if (host == null)
                {
                    Plugin.NativeLog.LogError(
                        "[NhalSeraLoot] dead Nhal'Sera had no Character component.");

                    return false;
                }

                LootTable table =
                    corpse.GetComponent<LootTable>();

                if (table == null)
                {
                    table =
                        corpse.GetComponentInChildren<LootTable>(
                            true);
                }

                if (table == null)
                {
                    table =
                        corpse.AddComponent<LootTable>();
                }

                if (table == null)
                {
                    return false;
                }

                // Critical: adding LootTable while Nhal'Sera is alive caused
                // InitLootTable failures. This component is created after
                // death and disabled immediately so Start/InitLootTable never
                // owns this runtime-built creature. Character invokes
                // LoadLootTable directly when the corpse is clicked.
                table.enabled =
                    false;

                InitializeLootLists(
                    table);

                ClearItemList(
                    table.GuaranteeOneDrop);

                ClearItemList(
                    table.CommonDrop);

                ClearItemList(
                    table.UncommonDrop);

                ClearItemList(
                    table.RareDrop);

                ClearItemList(
                    table.LegendaryDrop);

                ClearItemList(
                    table.UltraRareDrop);

                table.ActualDrops.Clear();
                table.ActualDropsQual.Clear();

                for (int i = 0;
                     i < rewards.Count;
                     i++)
                {
                    Item reward =
                        rewards[i];

                    if (reward == null)
                    {
                        continue;
                    }

                    table.ActualDrops.Add(
                        reward);

                    table.ActualDropsQual.Add(
                        1);
                }

                if (table.ActualDrops.Count < 2)
                {
                    return false;
                }

                table.hostChar =
                    host;

                table.MaxNumberDrops =
                    table.ActualDrops.Count;

                table.MaxNonCommonDrops =
                    table.ActualDrops.Count;

                table.NumberOfGuaranteedDrops =
                    table.ActualDrops.Count;

                table.MinGold =
                    0;

                table.MaxGold =
                    0;

                table.MyGold =
                    0;

                FieldInfo characterLootField =
                    AccessTools.Field(
                        typeof(Character),
                        "MyLootTable");

                if (characterLootField == null)
                {
                    Plugin.NativeLog.LogError(
                        "[NhalSeraLoot] Character.MyLootTable field was not found; native corpse clicking cannot be linked.");

                    return false;
                }

                characterLootField.SetValue(
                    host,
                    table);

                Plugin.NativeLog.LogInfo(
                    "[NhalSeraLoot] Character.MyLootTable linked to post-death Nhal'Sera LootTable. ActualDrops=[" +
                    DescribeRewards(
                        table.ActualDrops) +
                    "].");

                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[NhalSeraLoot] failed arming native corpse loot: " +
                    exception);

                return false;
            }
        }


        private static void InitializeLootLists(
            LootTable table)
        {
            if (table.GuaranteeOneDrop == null)
            {
                table.GuaranteeOneDrop =
                    new List<Item>();
            }

            if (table.CommonDrop == null)
            {
                table.CommonDrop =
                    new List<Item>();
            }

            if (table.UncommonDrop == null)
            {
                table.UncommonDrop =
                    new List<Item>();
            }

            if (table.RareDrop == null)
            {
                table.RareDrop =
                    new List<Item>();
            }

            if (table.LegendaryDrop == null)
            {
                table.LegendaryDrop =
                    new List<Item>();
            }

            if (table.UltraRareDrop == null)
            {
                table.UltraRareDrop =
                    new List<Item>();
            }

            if (table.ActualDrops == null)
            {
                table.ActualDrops =
                    new List<Item>();
            }

            if (table.ActualDropsQual == null)
            {
                table.ActualDropsQual =
                    new List<int>();
            }

            if (table.VisiblePieces == null)
            {
                table.VisiblePieces =
                    new List<Transform>();
            }

            if (table.qualUps == null)
            {
                table.qualUps =
                    new List<int>();
            }
        }


        private static void ClearItemList(
            List<Item> items)
        {
            if (items != null)
            {
                items.Clear();
            }
        }


        private static string DescribeRewards(
            List<Item> rewards)
        {
            if (rewards == null ||
                rewards.Count == 0)
            {
                return "<none>";
            }

            List<string> names =
                new List<string>();

            for (int i = 0;
                 i < rewards.Count;
                 i++)
            {
                Item item =
                    rewards[i];

                if (item != null)
                {
                    names.Add(
                        item.ItemName +
                        " [" +
                        item.Id +
                        "]");
                }
            }

            return
                names.Count > 0
                    ? string.Join(
                        ", ",
                        names.ToArray())
                    : "<none>";
        }
    }
}
