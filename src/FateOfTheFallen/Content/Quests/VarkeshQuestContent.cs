using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class VarkeshQuestContent
    {
        internal static Item RegisterItem(ItemDatabase database)
        {
            Item fragment = VarkeshSoulFragment.RegisterItem(database);
            LinkQuestRequirement();
            return fragment;
        }

        internal static Item GetItem()
        {
            Item fragment = VarkeshSoulFragment.GetItem();

            if (fragment == null && GameData.ItemDB != null)
                fragment = RegisterItem(GameData.ItemDB);

            return fragment;
        }

        internal static void LinkQuestRequirement()
        {
            Quest quest =
                VarkeshQuest.Quest;

            Item fragment =
                VarkeshSoulFragment.GetItem();

            if (fragment == null &&
                GameData.ItemDB != null)
            {
                fragment =
                    VarkeshSoulFragment.RegisterItem(
                        GameData.ItemDB);
            }

            if (quest == null ||
                fragment == null)
            {
                return;
            }

            if (quest.RequiredItems == null)
            {
                quest.RequiredItems =
                    new List<Item>();
            }

            // Quest 9 requires exactly one canonical Varkesh's Soul Fragment
            // [1707]. Corpse loot, inventory checks, Quest.RequiredItems and
            // Sereth's native QuestManager all share this same Item instance.
            quest.RequiredItems.Clear();
            quest.RequiredItems.Add(
                fragment);
        }

        internal static bool PlayerHasSoulFragment()
        {
            Item fragment = GetItem();

            if (fragment == null || GameData.PlayerInv == null)
                return false;

            try
            {
                return GameData.PlayerInv.HasItem(fragment, false);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryConsumeSoulFragment()
        {
            Item fragment = GetItem();

            if (fragment == null || GameData.PlayerInv == null)
                return false;

            try
            {
                return GameData.PlayerInv.HasItem(fragment, true);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Quest9] failed consuming Varkesh's Soul Fragment [1707]: " +
                    exception);
                return false;
            }
        }

        /// <summary>
        /// Appends the Quest 9 fragment to Varkesh's native corpse loot without
        /// replacing any normal boss rewards already present.
        /// </summary>
        internal static bool TryArmSoulFragmentOnCorpse(NPC boss)
        {
            if (boss == null || !VarkeshQuest.IsActive() || VarkeshQuest.IsComplete())
                return false;

            if (PlayerHasSoulFragment())
                return true;

            Item fragment = GetItem();
            if (fragment == null)
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

                // Whether inherited from Sivakayan Templar or created here,
                // Character.TargetMe/loot interaction can call LoadLootTable
                // directly. Do not let a late Start()/InitLootTable pass rebuild
                // or replace our already-resolved corpse drops.
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
                    if (item != null && string.Equals(item.Id, fragment.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyPresent = true;
                        break;
                    }
                }

                if (!alreadyPresent)
                {
                    table.ActualDrops.Add(fragment);
                    table.ActualDropsQual.Add(1);
                }

                table.hostChar = host;
                table.MaxNumberDrops = Mathf.Max(table.MaxNumberDrops, table.ActualDrops.Count);
                table.MaxNonCommonDrops = Mathf.Max(table.MaxNonCommonDrops, table.ActualDrops.Count);
                table.NumberOfGuaranteedDrops = Mathf.Max(table.NumberOfGuaranteedDrops, table.ActualDrops.Count);

                FieldInfo characterLootField = AccessTools.Field(typeof(Character), "MyLootTable");
                if (characterLootField != null)
                    characterLootField.SetValue(host, table);

                // Native DoDeath may change clickability/collider state after
                // the cloned Templar dies. Reassert corpse interaction after
                // the loot table is linked.
                EnsureLootableCorpse(
                    boss);

                Plugin.NativeLog.LogInfo(
                    "[Quest9] guaranteed Varkesh's Soul Fragment [1707] appended to corpse loot.");

                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Quest9] failed arming Varkesh's Soul Fragment corpse drop: " + exception);
                return false;
            }
        }


        /// <summary>
        /// Reasserts the native corpse interaction state after Varkesh dies.
        /// The boss is a scaled clone of a native humanoid, and native death
        /// processing can disable the collider/click path after our encounter
        /// runtime has already configured it.
        ///
        /// This method never clears or rerolls loot. It only makes the existing
        /// corpse LootTable reachable through Character.MyLootTable and restores
        /// a clickable non-trigger collider on the normal NPC target layer.
        /// </summary>
        internal static bool EnsureLootableCorpse(
            NPC boss)
        {
            if (boss == null)
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
                    Plugin.NativeLog.LogWarning(
                        "[VarkeshLoot] corpse repair found no Character component.");

                    return false;
                }

                // Varkesh must remain click/target eligible after death.
                boss.noClick =
                    false;

                corpse.layer =
                    9;

                Collider[] colliders =
                    corpse.GetComponentsInChildren<Collider>(
                        true);

                int enabledCorpseColliders =
                    0;

                for (int i = 0;
                     colliders != null &&
                     i < colliders.Length;
                     i++)
                {
                    Collider collider =
                        colliders[i];

                    if (collider == null ||
                        collider.isTrigger)
                    {
                        continue;
                    }

                    collider.enabled =
                        true;

                    collider.gameObject.layer =
                        9;

                    enabledCorpseColliders++;
                }

                // Alive Varkesh is targetable, so normally at least one native
                // body collider already exists. Keep a defensive fallback in
                // case the Templar death state destroys/disables all of them.
                if (enabledCorpseColliders == 0)
                {
                    CapsuleCollider fallback =
                        corpse.GetComponent<CapsuleCollider>();

                    if (fallback == null)
                    {
                        fallback =
                            corpse.AddComponent<CapsuleCollider>();
                    }

                    if (fallback != null)
                    {
                        fallback.isTrigger =
                            false;

                        fallback.enabled =
                            true;

                        // Values are local-space and therefore naturally scale
                        // with Varkesh's enlarged root transform.
                        fallback.center =
                            new Vector3(
                                0f,
                                1f,
                                0f);

                        fallback.height =
                            2f;

                        fallback.radius =
                            0.55f;

                        fallback.gameObject.layer =
                            9;

                        enabledCorpseColliders =
                            1;
                    }
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
                    Plugin.NativeLog.LogWarning(
                        "[VarkeshLoot] corpse repair could not create/resolve a LootTable.");

                    return false;
                }

                table.enabled =
                    false;

                if (table.GuaranteeOneDrop == null)
                    table.GuaranteeOneDrop = new List<Item>();

                if (table.CommonDrop == null)
                    table.CommonDrop = new List<Item>();

                if (table.UncommonDrop == null)
                    table.UncommonDrop = new List<Item>();

                if (table.RareDrop == null)
                    table.RareDrop = new List<Item>();

                if (table.LegendaryDrop == null)
                    table.LegendaryDrop = new List<Item>();

                if (table.UltraRareDrop == null)
                    table.UltraRareDrop = new List<Item>();

                if (table.ActualDrops == null)
                    table.ActualDrops = new List<Item>();

                if (table.ActualDropsQual == null)
                    table.ActualDropsQual = new List<int>();

                // Keep the quality list aligned with ActualDrops. A malformed
                // pair can prevent the native loot UI from building correctly.
                while (table.ActualDropsQual.Count <
                       table.ActualDrops.Count)
                {
                    table.ActualDropsQual.Add(
                        1);
                }

                while (table.ActualDropsQual.Count >
                       table.ActualDrops.Count)
                {
                    table.ActualDropsQual.RemoveAt(
                        table.ActualDropsQual.Count -
                        1);
                }

                table.hostChar =
                    host;

                table.MaxNumberDrops =
                    Mathf.Max(
                        table.MaxNumberDrops,
                        table.ActualDrops.Count);

                table.MaxNonCommonDrops =
                    Mathf.Max(
                        table.MaxNonCommonDrops,
                        table.ActualDrops.Count);

                FieldInfo characterLootField =
                    AccessTools.Field(
                        typeof(Character),
                        "MyLootTable");

                if (characterLootField == null)
                {
                    Plugin.NativeLog.LogError(
                        "[VarkeshLoot] Character.MyLootTable field was not found; corpse cannot be linked to native looting.");

                    return false;
                }

                characterLootField.SetValue(
                    host,
                    table);

                Plugin.NativeLog.LogInfo(
                    "[VarkeshLoot] corpse interaction repaired. noClick=[" +
                    boss.noClick +
                    "] Colliders=[" +
                    enabledCorpseColliders +
                    "] ActualDrops=[" +
                    table.ActualDrops.Count +
                    "] Layer=[" +
                    corpse.layer +
                    "].");

                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[VarkeshLoot] failed repairing corpse interaction: " +
                    exception);

                return false;
            }
        }
    }
}
