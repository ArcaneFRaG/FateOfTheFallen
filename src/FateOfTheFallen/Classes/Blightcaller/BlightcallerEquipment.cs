using System;
using System.Collections.Generic;

namespace FateOfTheFallen
{
    internal static class BlightcallerEquipment
    {
        // ============================================================
        // NATIVE CLASS NAMES
        // ============================================================

        private static readonly string[] NativeClassNames =
        {
            "Paladin",
            "Arcanist",
            "Duelist",
            "Druid",
            "Stormcaller",
            "Reaver"
        };


        // ============================================================
        // REGISTER ARCANIST / UNIVERSAL EQUIPMENT
        // ============================================================

        internal static void RegisterArcanistEquipment()
        {
            try
            {
                Class blightcaller =
                    BlightcallerCatalog.BlightcallerClass;

                if (blightcaller == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "Blightcaller equipment registration skipped: " +
                        "Blightcaller class is null.");

                    return;
                }

                if (GameData.ItemDB == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "Blightcaller equipment registration skipped: " +
                        "GameData.ItemDB is null.");

                    return;
                }

                if (GameData.ItemDB.ItemDB == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "Blightcaller equipment registration skipped: " +
                        "ItemDB.ItemDB is null.");

                    return;
                }

                int arcanistEquipmentAdded = 0;
                int universalGeneralAdded = 0;

                Item[] items =
                    GameData.ItemDB.ItemDB;

                for (int i = 0; i < items.Length; i++)
                {
                    Item item =
                        items[i];

                    if (item == null)
                    {
                        continue;
                    }

                    if (item.Classes == null)
                    {
                        continue;
                    }

                    if (item.Classes.Contains(
                            blightcaller))
                    {
                        continue;
                    }

                    // ====================================================
                    // GENERAL ITEMS
                    // ====================================================
                    //
                    // Do NOT automatically copy Arcanist compatibility
                    // for General items.
                    //
                    // Scrolls, books and other class-specific consumables
                    // can also use General as their slot type.
                    //
                    // Only add Blightcaller if the item is already usable
                    // by every one of the six native classes.
                    // ====================================================

                    if (item.RequiredSlot ==
                        Item.SlotType.General)
                    {
                        if (!IsUniversalNativeGeneralItem(
                                item))
                        {
                            continue;
                        }

                        item.Classes.Add(
                            blightcaller);

                        universalGeneralAdded++;

                        

                        continue;
                    }

                    // ====================================================
                    // NORMAL EQUIPMENT
                    // ====================================================
                    //
                    // Blightcaller inherits Arcanist compatibility for
                    // actual equipment.
                    // ====================================================

                    if (!HasClass(
                            item,
                            "Arcanist"))
                    {
                        continue;
                    }

                    item.Classes.Add(
                        blightcaller);

                    arcanistEquipmentAdded++;
                }

               
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: equipment registration failed: " +
                    exception);
            }
        }


        // ============================================================
        // UNIVERSAL GENERAL ITEM CHECK
        // ============================================================

        private static bool IsUniversalNativeGeneralItem(
            Item item)
        {
            if (item == null)
            {
                return false;
            }

            if (item.RequiredSlot !=
                Item.SlotType.General)
            {
                return false;
            }

            if (item.Classes == null ||
                item.Classes.Count == 0)
            {
                return false;
            }

            // The item must explicitly contain every native class.
            //
            // Example:
            //
            // Paladin
            // Arcanist
            // Duelist
            // Druid
            // Stormcaller
            // Reaver
            //
            // If even one is absent, this is not considered a
            // universally usable native item.

            for (int i = 0;
                 i < NativeClassNames.Length;
                 i++)
            {
                if (!HasClass(
                        item,
                        NativeClassNames[i]))
                {
                    return false;
                }
            }

            return true;
        }


        // ============================================================
        // CLASS LOOKUP
        // ============================================================

        private static bool HasClass(
            Item item,
            string className)
        {
            if (item == null ||
                item.Classes == null ||
                string.IsNullOrEmpty(
                    className))
            {
                return false;
            }

            for (int i = 0;
                 i < item.Classes.Count;
                 i++)
            {
                Class itemClass =
                    item.Classes[i];

                if (itemClass == null)
                {
                    continue;
                }

                if (string.Equals(
                        itemClass.DisplayName,
                        className,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(
                        itemClass.name,
                        className,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}