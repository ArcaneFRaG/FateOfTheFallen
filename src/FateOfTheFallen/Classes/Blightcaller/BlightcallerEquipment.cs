using System;

namespace FateOfTheFallen
{
    internal static class BlightcallerEquipment
    {
        internal static void RegisterArcanistEquipment()
        {
            BlightcallerCatalog.EnsureClass();

            if (GameData.ClassDB == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller equipment: ClassDB is unavailable.");

                return;
            }

            if (GameData.ClassDB.Arcanist == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller equipment: Arcanist class is unavailable.");

                return;
            }

            if (BlightcallerCatalog.BlightcallerClass == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller equipment: Blightcaller class is unavailable.");

                return;
            }

            if (GameData.ItemDB == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller equipment: ItemDB is unavailable.");

                return;
            }

            if (GameData.ItemDB.ItemDB == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller equipment: ItemDB array is unavailable.");

                return;
            }

            int arcanistItems = 0;
            int skippedGeneralItems = 0;
            int addedBlightcaller = 0;

            foreach (Item item in GameData.ItemDB.ItemDB)
            {
                if (item == null)
                {
                    continue;
                }

                if (item.Classes == null ||
                    item.Classes.Count == 0)
                {
                    continue;
                }

                if (!item.Classes.Contains(
                        GameData.ClassDB.Arcanist))
                {
                    continue;
                }

                arcanistItems++;

                // General-slot items include spell scrolls,
                // skill books, consumables, and other non-equipment.
                //
                // Do NOT add Blightcaller to these items.
                if (item.RequiredSlot == Item.SlotType.General)
                {
                    skippedGeneralItems++;
                    continue;
                }

                if (item.Classes.Contains(
                        BlightcallerCatalog.BlightcallerClass))
                {
                    continue;
                }

                item.Classes.Add(
                    BlightcallerCatalog.BlightcallerClass);

                addedBlightcaller++;
            }

            
        }
    }
}