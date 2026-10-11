using System;
using System.Collections.Generic;
using System.Linq;

namespace FateOfTheFallen
{
    /// <summary>
    /// Emberweaver starting inventory, called by the integrated FOTF
    /// character-creation/save path.
    ///
    /// Gear deliberately mirrors the current Blightcaller creation path,
    /// while Emberweaver receives its own two starting teaching scrolls.
    /// </summary>
    internal static class EmberweaverStartingLoadout
    {
        private static readonly string[] StartingSpellIds =
        {
            EmberweaverSpellCatalog.EmberBoltId,
            EmberweaverSpellCatalog.PyroclasmId
        };

        internal static void GrantStartingLoadout()
        {
            if (!EmberweaverCatalog.IsCurrentPlayerEmberweaver() ||
                GameData.PlayerInv == null)
            {
                return;
            }

            GrantStartingScrolls();

            AddOnce(
                FindStarterPants());

            AddOnce(
                FindStarterDagger());
        }

        internal static void GrantStartingScrolls()
        {
            if (!EmberweaverCatalog.IsCurrentPlayerEmberweaver() ||
                GameData.PlayerInv == null)
            {
                return;
            }

            if (GameData.ItemDB != null)
            {
                EmberweaverModule.RegisterItemDatabase(
                    GameData.ItemDB);
            }

            for (int i = 0;
                 i < StartingSpellIds.Length;
                 i++)
            {
                Item scroll =
                    EmberweaverTeachingItems.GetScrollForSpell(
                        StartingSpellIds[i]);

                AddOnce(
                    scroll);
            }
        }

        private static Item FindStarterPants()
        {
            if (GameData.ItemDB == null ||
                GameData.ItemDB.ItemDB == null)
            {
                return null;
            }

            return GameData.ItemDB.ItemDB
                .Where(
                    item =>
                        item != null &&
                        !string.IsNullOrEmpty(item.ItemName) &&
                        item.ItemName.IndexOf(
                            "Pants",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(
                    item =>
                        item.ItemLevel)
                .FirstOrDefault();
        }

        private static Item FindStarterDagger()
        {
            if (GameData.ItemDB == null ||
                GameData.ItemDB.ItemDB == null)
            {
                return null;
            }

            return GameData.ItemDB.ItemDB
                .FirstOrDefault(
                    item =>
                        item != null &&
                        !string.IsNullOrEmpty(item.ItemName) &&
                        string.Equals(
                            item.ItemName.Trim(),
                            "Rusty Dagger",
                            StringComparison.OrdinalIgnoreCase));
        }

        private static void AddOnce(
            Item item)
        {
            if (item == null ||
                GameData.PlayerInv == null ||
                HasItem(item))
            {
                return;
            }

            try
            {
                if (GameData.PlayerInv.AddItemToInv(
                        item))
                {
                    return;
                }
            }
            catch
            {
            }

            try
            {
                GameData.PlayerInv.ForceItemToInv(
                    item);
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Emberweaver: failed to add starting item " +
                    (item.ItemName ?? item.Id),
                    exception);
            }
        }

        private static bool HasItem(
            Item item)
        {
            if (item == null ||
                GameData.PlayerInv == null)
            {
                return false;
            }

            try
            {
                if (GameData.PlayerInv.HasItem(
                        item,
                        false))
                {
                    return true;
                }
            }
            catch
            {
            }

            if (ContainsById(
                    GameData.PlayerInv.StoredItems,
                    item.Id))
            {
                return true;
            }

            return ContainsById(
                GameData.PlayerInv.EquippedItems,
                item.Id);
        }

        private static bool ContainsById(
            IList<Item> items,
            string id)
        {
            if (items == null ||
                string.IsNullOrEmpty(id))
            {
                return false;
            }

            for (int i = 0;
                 i < items.Count;
                 i++)
            {
                Item current =
                    items[i];

                if (current != null &&
                    string.Equals(
                        current.Id,
                        id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
