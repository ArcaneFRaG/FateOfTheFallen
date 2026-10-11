using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Shared weighted corpse-loot resolver for Varkesh and Gorath.
    /// Each kill makes exactly two independent equipment rolls from the boss's
    /// 100% weighted equipment pool. The same item may win both rolls.
    /// Quest 9/10 progression items remain quest-only additive drops.
    /// </summary>
    internal static class LateWorldBossLootTable
    {
        private sealed class EquipmentEntry
        {
            internal string ItemId;
            internal float Weight;
        }

        private static readonly HashSet<int> PendingBosses =
            new HashSet<int>();

        private static readonly HashSet<int> ResolvedBosses =
            new HashSet<int>();

        private static readonly HashSet<string> ValidatedPools =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly EquipmentEntry[] VarkeshPool =
        {
            // Original final Death/Varkesh distribution.  Each equipment roll
            // totals exactly 100%, with the strongest chase item the rarest.
            new EquipmentEntry { ItemId = VarkeshEquipment.GravetideReaverId, Weight = 12f },
            new EquipmentEntry { ItemId = VarkeshEquipment.HeartOfTheDeathlessId, Weight = 16f },
            new EquipmentEntry { ItemId = VarkeshEquipment.SoulbinderGrimoireId, Weight = 20f },
            new EquipmentEntry { ItemId = VarkeshEquipment.LanternOfDepartedSoulsId, Weight = 24f },
            new EquipmentEntry { ItemId = VarkeshEquipment.BandOfTheGravewindId, Weight = 28f }
        };

        private static readonly EquipmentEntry[] GorathPool =
        {
            // Original final Demon/Gorath distribution.  Stormfire is the
            // chase item and therefore carries the lowest weight.
            new EquipmentEntry { ItemId = GorathEquipment.StormfireId, Weight = 12f },
            new EquipmentEntry { ItemId = GorathEquipment.EmberOfTheDemonKingId, Weight = 16f },
            new EquipmentEntry { ItemId = GorathEquipment.HellforgedTalonId, Weight = 20f },
            new EquipmentEntry { ItemId = GorathEquipment.InfernalBastionId, Weight = 24f },
            new EquipmentEntry { ItemId = GorathEquipment.AshenCovenantId, Weight = 28f }
        };

        internal static void ResolveVarkeshOnDeath(NPC boss)
        {
            ScheduleResolveOnDeath(
                boss,
                VarkeshPool,
                VarkeshSoulFragment.ItemId,
                "VarkeshLoot",
                VarkeshQuestContent.EnsureLootableCorpse);
        }

        internal static void ResolveGorathOnDeath(NPC boss)
        {
            ScheduleResolveOnDeath(
                boss,
                GorathPool,
                GorathInfernalHeart.ItemId,
                "GorathLoot",
                GorathQuestContent.EnsureLootableCorpse);
        }

        private static void ScheduleResolveOnDeath(
            NPC boss,
            EquipmentEntry[] pool,
            string questItemId,
            string logTag,
            Func<NPC, bool> ensureCorpse)
        {
            if (boss == null)
                return;

            ValidatePoolTotal(pool, logTag);

            int instanceId = boss.GetInstanceID();

            if (PendingBosses.Contains(instanceId) ||
                ResolvedBosses.Contains(instanceId))
            {
                return;
            }

            PendingBosses.Add(instanceId);
            CoroutineHost.Start(
                ResolveOnDeathDelayed(
                    boss,
                    instanceId,
                    pool,
                    questItemId,
                    logTag,
                    ensureCorpse));
        }

        private static IEnumerator ResolveOnDeathDelayed(
            NPC boss,
            int instanceId,
            EquipmentEntry[] pool,
            string questItemId,
            string logTag,
            Func<NPC, bool> ensureCorpse)
        {
            // Native death/loot initialization on both Varkesh's cloned
            // Templar runtime and Gorath's custom runtime can continue for a
            // few frames after Character.DoDeath. Wait until that path settles
            // before replacing the corpse table with the FOTF pool.
            for (int i = 0; i < 6; i++)
                yield return null;

            PendingBosses.Remove(instanceId);

            if (boss == null || ResolvedBosses.Contains(instanceId))
                yield break;

            List<Item> rewards = ResolveRewardPair(pool, logTag);
            if (rewards == null || rewards.Count < 2)
                yield break;

            if (!ArmCorpseLoot(boss, rewards, questItemId, logTag))
            {
                Plugin.NativeLog.LogError(
                    "[" + logTag + "] reward pair resolved but corpse loot could not be armed.");
                yield break;
            }

            if (ensureCorpse != null)
                ensureCorpse(boss);

            ResolvedBosses.Add(instanceId);

            Plugin.NativeLog.LogInfo(
                "[" + logTag + "] final FOTF corpse loot armed. Rewards=[" +
                rewards[0].ItemName +
                "] + [" +
                rewards[1].ItemName +
                "]. Duplicate results are allowed.");

            // Reassert the SAME already-rolled pair after late native corpse
            // initialization. Never reroll here. Quest items are preserved by
            // ArmCorpseLoot, so this is safe whether the quest bridge appended
            // its progression item before or after the first pass.
            int[] reassertFrames = { 6, 18, 36 };

            for (int r = 0; r < reassertFrames.Length; r++)
            {
                for (int f = 0; f < reassertFrames[r]; f++)
                    yield return null;

                if (boss == null)
                    yield break;

                ArmCorpseLoot(
                    boss,
                    rewards,
                    questItemId,
                    logTag);

                if (ensureCorpse != null)
                    ensureCorpse(boss);
            }
        }

        private static void ValidatePoolTotal(
            EquipmentEntry[] pool,
            string logTag)
        {
            if (pool == null ||
                string.IsNullOrEmpty(logTag) ||
                !ValidatedPools.Add(logTag))
            {
                return;
            }

            float total = 0f;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] != null)
                    total += Mathf.Max(0f, pool[i].Weight);
            }

            if (Mathf.Abs(total - 100f) > 0.01f)
            {
                Plugin.NativeLog.LogError(
                    "[" + logTag + "] equipment pool weights total [" +
                    total.ToString("0.##") +
                    "%], expected exactly 100%.");
            }
            else
            {
                Plugin.NativeLog.LogInfo(
                    "[" + logTag + "] equipment pool validated at exactly 100% per roll.");
            }
        }


        private static List<Item> ResolveRewardPair(
            EquipmentEntry[] pool,
            string logTag)
        {
            string firstId;
            Item firstItem;

            if (!ResolveGuaranteedEquipment(pool, out firstId, out firstItem))
            {
                Plugin.NativeLog.LogError(
                    "[" + logTag + "] failed resolving first equipment reward.");
                return null;
            }

            string secondId;
            Item secondItem;

            // Independent second roll: intentionally do NOT exclude firstId.
            if (!ResolveGuaranteedEquipment(pool, out secondId, out secondItem))
            {
                Plugin.NativeLog.LogError(
                    "[" + logTag + "] failed resolving second equipment reward.");
                return null;
            }

            return new List<Item>
            {
                firstItem,
                secondItem
            };
        }

        private static bool ResolveGuaranteedEquipment(
            EquipmentEntry[] pool,
            out string awardedId,
            out Item awardedItem)
        {
            awardedId = null;
            awardedItem = null;

            HashSet<string> attempted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < pool.Length; i++)
            {
                string id = RollEquipmentIdExcluding(pool, attempted);
                if (string.IsNullOrEmpty(id))
                    break;

                attempted.Add(id);
                Item item = ResolveEquipmentItem(id);
                if (item == null)
                    continue;

                awardedId = id;
                awardedItem = item;
                return true;
            }

            return false;
        }


        private static string RollEquipmentIdExcluding(
            EquipmentEntry[] pool,
            HashSet<string> excluded)
        {
            float totalWeight = 0f;

            for (int i = 0; i < pool.Length; i++)
            {
                EquipmentEntry entry = pool[i];
                if (entry == null || string.IsNullOrEmpty(entry.ItemId))
                    continue;
                if (excluded != null && excluded.Contains(entry.ItemId))
                    continue;
                totalWeight += Mathf.Max(0f, entry.Weight);
            }

            if (totalWeight <= 0f)
                return null;

            float roll = UnityEngine.Random.Range(0f, totalWeight);

            for (int i = 0; i < pool.Length; i++)
            {
                EquipmentEntry entry = pool[i];
                if (entry == null || string.IsNullOrEmpty(entry.ItemId))
                    continue;
                if (excluded != null && excluded.Contains(entry.ItemId))
                    continue;

                float weight = Mathf.Max(0f, entry.Weight);
                if (roll < weight)
                    return entry.ItemId;
                roll -= weight;
            }

            return null;
        }

        private static Item ResolveEquipmentItem(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            Item item = CustomItemRegistry.GetItemById(id);
            if (item != null)
                return item;

            if (GameData.ItemDB != null)
                return CustomItemRegistry.Resolve(id, GameData.ItemDB);

            return null;
        }

        private static bool ArmCorpseLoot(
            NPC boss,
            List<Item> rewards,
            string questItemId,
            string logTag)
        {
            if (boss == null || rewards == null || rewards.Count < 2)
                return false;

            try
            {
                GameObject corpse = boss.gameObject;
                Character host = boss.GetChar();
                if (host == null && corpse != null)
                    host = corpse.GetComponent<Character>();
                if (corpse == null || host == null)
                    return false;

                LootTable table = corpse.GetComponent<LootTable>();
                if (table == null)
                    table = corpse.GetComponentInChildren<LootTable>(true);
                if (table == null)
                    table = corpse.AddComponent<LootTable>();
                if (table == null)
                    return false;

                table.enabled = false;
                InitializeLootLists(table);

                Item preservedQuestItem = null;
                if (table.ActualDrops != null && !string.IsNullOrEmpty(questItemId))
                {
                    for (int i = 0; i < table.ActualDrops.Count; i++)
                    {
                        Item existing = table.ActualDrops[i];
                        if (existing != null && string.Equals(existing.Id, questItemId, StringComparison.OrdinalIgnoreCase))
                        {
                            preservedQuestItem = existing;
                            break;
                        }
                    }
                }

                table.GuaranteeOneDrop.Clear();
                table.CommonDrop.Clear();
                table.UncommonDrop.Clear();
                table.RareDrop.Clear();
                table.LegendaryDrop.Clear();
                table.UltraRareDrop.Clear();
                table.ActualDrops.Clear();
                table.ActualDropsQual.Clear();

                for (int i = 0; i < rewards.Count; i++)
                {
                    if (rewards[i] == null)
                        continue;
                    table.ActualDrops.Add(rewards[i]);
                    table.ActualDropsQual.Add(1);
                }

                if (preservedQuestItem != null)
                {
                    table.ActualDrops.Add(preservedQuestItem);
                    table.ActualDropsQual.Add(1);
                }

                if (table.ActualDrops.Count < 2)
                    return false;

                table.hostChar = host;
                table.MaxNumberDrops = table.ActualDrops.Count;
                table.MaxNonCommonDrops = table.ActualDrops.Count;
                table.NumberOfGuaranteedDrops = table.ActualDrops.Count;
                table.MinGold = 0;
                table.MaxGold = 0;
                table.MyGold = 0;

                FieldInfo characterLootField = AccessTools.Field(typeof(Character), "MyLootTable");
                if (characterLootField == null)
                    return false;

                characterLootField.SetValue(host, table);
                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError("[" + logTag + "] failed arming corpse loot: " + exception);
                return false;
            }
        }

        private static void InitializeLootLists(LootTable table)
        {
            if (table.GuaranteeOneDrop == null) table.GuaranteeOneDrop = new List<Item>();
            if (table.CommonDrop == null) table.CommonDrop = new List<Item>();
            if (table.UncommonDrop == null) table.UncommonDrop = new List<Item>();
            if (table.RareDrop == null) table.RareDrop = new List<Item>();
            if (table.LegendaryDrop == null) table.LegendaryDrop = new List<Item>();
            if (table.UltraRareDrop == null) table.UltraRareDrop = new List<Item>();
            if (table.ActualDrops == null) table.ActualDrops = new List<Item>();
            if (table.ActualDropsQual == null) table.ActualDropsQual = new List<int>();
        }
    }
}
