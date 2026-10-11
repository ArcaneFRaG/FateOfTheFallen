using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Loot owned by the Blighted Thrall rather than by whatever native
    /// SpawnPoint was used as its temporary combat/animation template.
    ///
    /// The first drop is deliberately guaranteed while this creature is
    /// still serving as the custom-monster proof of concept. Once the full
    /// Blight Zone loot tables exist, the quantity/chance can be tuned there.
    /// </summary>
    internal static class BlightedThrallLoot
    {
        internal const string TissueItemId =
            "FOTF_BLIGHTED_TISSUE";

        internal const string TissueItemName =
            "Blighted Tissue";

        private static Item _blightedTissue;

        internal static Item RegisterItem(
            ItemDatabase database)
        {
            if (_blightedTissue != null)
            {
                if (database != null)
                {
                    CustomItemRegistry.RegisterWithNativeDatabase(
                        database,
                        _blightedTissue);
                }

                return _blightedTissue;
            }

            Sprite icon =
                ResolvePlaceholderIcon(
                    database);

            _blightedTissue =
                CustomItemRegistry.CreateItem(
                    id:
                        TissueItemId,

                    itemName:
                        TissueItemName,

                    lore:
                        "A warm knot of diseased flesh and fungal fibre. " +
                        "Even severed from its host, the tissue continues to twitch, " +
                        "divide, and reach for anything living nearby.",

                    slot:
                        Item.SlotType.General,

                    icon:
                        icon,

                    itemLevel:
                        35,

                    itemValue:
                        25,

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
                        false);

            if (_blightedTissue == null)
            {
                Plugin.NativeLog.LogError(
                    "Blighted Thrall: failed to create Blighted Tissue loot item.");

                return null;
            }

            if (database != null)
            {
                CustomItemRegistry.RegisterWithNativeDatabase(
                    database,
                    _blightedTissue);
            }


            return _blightedTissue;
        }

        internal static Item GetItem()
        {
            if (_blightedTissue != null)
            {
                return _blightedTissue;
            }

            _blightedTissue =
                CustomItemRegistry.GetItemById(
                    TissueItemId);

            return _blightedTissue;
        }

        internal static void ConfigureLoot(
            LootTable lootTable)
        {
            if (lootTable == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Blighted Thrall: spawned monster had no LootTable component; native death can still be tested but custom loot cannot.");

                return;
            }

            Item tissue =
                GetItem();

            if (tissue == null &&
                GameData.ItemDB != null)
            {
                tissue =
                    RegisterItem(
                        GameData.ItemDB);
            }

            if (tissue == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Blighted Thrall: Blighted Tissue was unavailable while configuring loot.");

                return;
            }

            if (lootTable.ActualDrops == null)
            {
                lootTable.ActualDrops =
                    new List<Item>();
            }
            else
            {
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

            // Guaranteed during PoC testing so a single kill proves that the
            // corpse/loot path is using FOTF data rather than template loot.
            lootTable.ActualDrops.Add(
                tissue);

            lootTable.ActualDropsQual.Add(
                1);

        }

        private static Sprite ResolvePlaceholderIcon(
            ItemDatabase database)
        {
            if (database == null)
            {
                return null;
            }

            // Use a harmless native General-slot icon only as a temporary UI
            // placeholder. The item itself is fully custom and can receive a
            // proper Blight Zone icon later without changing its ID/save data.
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
            if (item == null ||
                item.ItemIcon == null)
            {
                return false;
            }

            return item.RequiredSlot ==
                   Item.SlotType.General;
        }
    }
}
