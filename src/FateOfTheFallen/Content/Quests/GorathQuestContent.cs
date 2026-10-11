using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class GorathQuestContent
    {
        internal static Item RegisterItem(ItemDatabase database)
        {
            Item heart = GorathInfernalHeart.RegisterItem(database);
            LinkQuestRequirement();
            return heart;
        }

        internal static Item GetItem()
        {
            Item heart = GorathInfernalHeart.GetItem();

            if (heart == null && GameData.ItemDB != null)
                heart = RegisterItem(GameData.ItemDB);

            return heart;
        }

        internal static void LinkQuestRequirement()
        {
            Quest quest =
                GorathQuest.Quest;

            Item heart =
                GorathInfernalHeart.GetItem();

            if (heart == null &&
                GameData.ItemDB != null)
            {
                heart =
                    GorathInfernalHeart.RegisterItem(
                        GameData.ItemDB);
            }

            if (quest == null ||
                heart == null)
            {
                return;
            }

            if (quest.RequiredItems == null)
            {
                quest.RequiredItems =
                    new List<Item>();
            }

            // Quest 10 requires exactly one canonical Gorath's Infernal Heart
            // [1708]. Corpse loot, inventory checks, Quest.RequiredItems and
            // Sereth's native QuestManager all share this same Item instance.
            quest.RequiredItems.Clear();
            quest.RequiredItems.Add(
                heart);
        }

        internal static bool PlayerHasInfernalHeart()
        {
            Item heart = GetItem();

            if (heart == null || GameData.PlayerInv == null)
                return false;

            try
            {
                return GameData.PlayerInv.HasItem(heart, false);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryConsumeInfernalHeart()
        {
            Item heart = GetItem();

            if (heart == null || GameData.PlayerInv == null)
                return false;

            try
            {
                return GameData.PlayerInv.HasItem(heart, true);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Quest10] failed consuming Gorath's Infernal Heart [1708]: " +
                    exception);
                return false;
            }
        }

        /// <summary>
        /// Appends the Quest 10 heart to Gorath's corpse loot without
        /// replacing any normal boss rewards already present.
        /// </summary>
        internal static bool TryArmInfernalHeartOnCorpse(NPC boss)
        {
            if (boss == null || !GorathQuest.IsActive() || GorathQuest.IsComplete())
                return false;

            if (PlayerHasInfernalHeart())
                return true;

            Item heart = GetItem();
            if (heart == null)
                return false;

            try
            {
                GameObject corpse = boss.gameObject;
                if (corpse == null)
                    return false;

                Character host = boss.GetChar();
                if (host == null)
                    host = corpse.GetComponent<Character>();
                if (host == null)
                    return false;

                LootTable table = corpse.GetComponent<LootTable>();
                if (table == null)
                    table = corpse.GetComponentInChildren<LootTable>(true);

                if (table == null)
                {
                    table = corpse.AddComponent<LootTable>();
                    if (table == null)
                        return false;
                }

                table.enabled = false;

                if (table.GuaranteeOneDrop == null) table.GuaranteeOneDrop = new List<Item>();
                if (table.CommonDrop == null) table.CommonDrop = new List<Item>();
                if (table.UncommonDrop == null) table.UncommonDrop = new List<Item>();
                if (table.RareDrop == null) table.RareDrop = new List<Item>();
                if (table.LegendaryDrop == null) table.LegendaryDrop = new List<Item>();
                if (table.UltraRareDrop == null) table.UltraRareDrop = new List<Item>();
                if (table.ActualDrops == null) table.ActualDrops = new List<Item>();
                if (table.ActualDropsQual == null) table.ActualDropsQual = new List<int>();

                bool alreadyPresent = false;
                for (int i = 0; i < table.ActualDrops.Count; i++)
                {
                    Item item = table.ActualDrops[i];
                    if (item != null && string.Equals(item.Id, heart.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyPresent = true;
                        break;
                    }
                }

                if (!alreadyPresent)
                {
                    table.ActualDrops.Add(heart);
                    table.ActualDropsQual.Add(1);
                }

                table.hostChar = host;
                table.MaxNumberDrops = Mathf.Max(table.MaxNumberDrops, table.ActualDrops.Count);
                table.MaxNonCommonDrops = Mathf.Max(table.MaxNonCommonDrops, table.ActualDrops.Count);
                table.NumberOfGuaranteedDrops = Mathf.Max(table.NumberOfGuaranteedDrops, table.ActualDrops.Count);

                FieldInfo characterLootField = AccessTools.Field(typeof(Character), "MyLootTable");
                if (characterLootField != null)
                    characterLootField.SetValue(host, table);

                EnsureLootableCorpse(
                    boss);

                Plugin.NativeLog.LogInfo(
                    "[Quest10] guaranteed Gorath's Infernal Heart [1708] appended to corpse loot.");

                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Quest10] failed arming Gorath's Infernal Heart corpse drop: " + exception);
                return false;
            }
        }


        internal static bool EnsureLootableCorpse(
            NPC boss)
        {
            if (boss == null)
                return false;

            try
            {
                GameObject corpse = boss.gameObject;

                if (corpse == null)
                    return false;

                Character host = boss.GetChar();

                if (host == null)
                    host = corpse.GetComponent<Character>();

                if (host == null)
                    return false;

                boss.noClick = false;
                corpse.layer = 9;

                Collider[] colliders =
                    corpse.GetComponentsInChildren<Collider>(true);

                int enabled = 0;

                for (int i = 0;
                     colliders != null && i < colliders.Length;
                     i++)
                {
                    Collider collider = colliders[i];

                    if (collider == null || collider.isTrigger)
                        continue;

                    collider.enabled = true;
                    collider.gameObject.layer = 9;
                    enabled++;
                }

                if (enabled == 0)
                {
                    CapsuleCollider fallback =
                        corpse.GetComponent<CapsuleCollider>();

                    if (fallback == null)
                        fallback = corpse.AddComponent<CapsuleCollider>();

                    fallback.isTrigger = false;
                    fallback.enabled = true;
                    fallback.center = new Vector3(0f, 1f, 0f);
                    fallback.height = 2f;
                    fallback.radius = 0.65f;
                    fallback.gameObject.layer = 9;
                    enabled = 1;
                }

                LootTable table = corpse.GetComponent<LootTable>();

                if (table == null)
                    table = corpse.GetComponentInChildren<LootTable>(true);

                if (table == null)
                    table = corpse.AddComponent<LootTable>();

                if (table == null)
                    return false;

                table.enabled = false;

                if (table.GuaranteeOneDrop == null) table.GuaranteeOneDrop = new List<Item>();
                if (table.CommonDrop == null) table.CommonDrop = new List<Item>();
                if (table.UncommonDrop == null) table.UncommonDrop = new List<Item>();
                if (table.RareDrop == null) table.RareDrop = new List<Item>();
                if (table.LegendaryDrop == null) table.LegendaryDrop = new List<Item>();
                if (table.UltraRareDrop == null) table.UltraRareDrop = new List<Item>();
                if (table.ActualDrops == null) table.ActualDrops = new List<Item>();
                if (table.ActualDropsQual == null) table.ActualDropsQual = new List<int>();

                while (table.ActualDropsQual.Count < table.ActualDrops.Count)
                    table.ActualDropsQual.Add(1);

                while (table.ActualDropsQual.Count > table.ActualDrops.Count)
                    table.ActualDropsQual.RemoveAt(table.ActualDropsQual.Count - 1);

                table.hostChar = host;
                table.MaxNumberDrops =
                    Mathf.Max(table.MaxNumberDrops, table.ActualDrops.Count);
                table.MaxNonCommonDrops =
                    Mathf.Max(table.MaxNonCommonDrops, table.ActualDrops.Count);

                FieldInfo characterLootField =
                    AccessTools.Field(
                        typeof(Character),
                        "MyLootTable");

                if (characterLootField == null)
                    return false;

                characterLootField.SetValue(host, table);

                Plugin.NativeLog.LogInfo(
                    "[GorathLoot] corpse interaction ready. Colliders=[" +
                    enabled +
                    "] ActualDrops=[" +
                    table.ActualDrops.Count +
                    "].");

                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[GorathLoot] corpse interaction repair failed: " +
                    exception);

                return false;
            }
        }
    }
}
