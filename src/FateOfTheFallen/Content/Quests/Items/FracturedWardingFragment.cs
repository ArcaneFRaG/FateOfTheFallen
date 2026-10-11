using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest/world item 1704.
    /// Recovered from the Ancient Warding Shrine in Port Azure during
    /// Quest 6 - The Pattern Beneath.
    ///
    /// This intentionally follows the same canonical CustomItemRegistry
    /// pattern as the previous FOTF quest items [1700-1703].
    /// </summary>
    internal static class FracturedWardingFragment
    {
        internal const string ItemId =
            "1704";

        internal const string ItemName =
            "Fractured Warding Fragment";

        private const string ItemLore =
            "A splinter broken from the central seal of an ancient warding shrine. " +
            "Four incompatible energies pulse faintly beneath its surface, " +
            "each pressing against the same fractured boundary.";

        private static Item _item;


        internal static Item RegisterItem(
            ItemDatabase database)
        {
            // Reuse the canonical item already created this session.
            if (_item != null)
            {
                ApplyDefinition(
                    _item);

                if (database != null)
                {
                    CustomItemRegistry.RegisterWithNativeDatabase(
                        database,
                        _item);
                }

                return _item;
            }

            // First ask CustomItemRegistry. This is the important save/load
            // identity path used by the previous FOTF quest items.
            Item existing =
                CustomItemRegistry.GetItemById(
                    ItemId);

            if (existing != null)
            {
                _item =
                    existing;

                ApplyDefinition(
                    _item);

                if (database != null)
                {
                    CustomItemRegistry.RegisterWithNativeDatabase(
                        database,
                        _item);
                }

                return _item;
            }

            // No canonical instance exists yet, so create it THROUGH
            // CustomItemRegistry rather than constructing/cloning Item here.
            Sprite icon =
                FracturedWardingFragmentIconLoader.Load();

            _item =
                CustomItemRegistry.CreateItem(
                    id:
                        ItemId,

                    itemName:
                        ItemName,

                    lore:
                        ItemLore,

                    slot:
                        Item.SlotType.General,

                    icon:
                        icon,

                    itemLevel:
                        1,

                    itemValue:
                        0,

                    stackable:
                        false,

                    disposable:
                        true,

                    unique:
                        true,

                    playerCannotSell:
                        true,

                    noTradeNoDestroy:
                        false,

                    simPlayersCantGet:
                        true);

            if (_item == null)
            {
                Plugin.NativeLog.LogError(
                    "FOTF quest items: failed to create Fractured Warding Fragment [1704].");

                return null;
            }

            ApplyDefinition(
                _item);

            if (database != null)
            {
                CustomItemRegistry.RegisterWithNativeDatabase(
                    database,
                    _item);
            }

            Plugin.NativeLog.LogInfo(
                "FOTF quest items: registered [1704] Fractured Warding Fragment.");

            return _item;
        }


        internal static Item GetItem()
        {
            if (_item != null)
            {
                return _item;
            }

            // Always resolve through CustomItemRegistry first so inventory
            // restore, quest requirements and world rewards share one Item.
            _item =
                CustomItemRegistry.GetItemById(
                    ItemId);

            if (_item == null &&
                GameData.ItemDB != null)
            {
                _item =
                    RegisterItem(
                        GameData.ItemDB);
            }

            return _item;
        }


        private static void ApplyDefinition(
            Item item)
        {
            if (item == null)
            {
                return;
            }

            item.name =
                "FOTF - Fractured Warding Fragment";

            item.Id =
                ItemId;

            item.ItemName =
                ItemName;

            item.ItemLevel =
                1;

            item.ItemValue =
                0;

            item.Lore =
                ItemLore;

            item.RequiredSlot =
                Item.SlotType.General;

            item.Stackable =
                false;

            item.Disposable =
                false;

            item.Unique =
                true;

            item.PlayerCannotSell =
                true;

            item.SimPlayersCantGet =
                true;

            Sprite icon =
                FracturedWardingFragmentIconLoader.Load();

            if (icon != null)
            {
                item.ItemIcon =
                    icon;
            }

            item.hideFlags =
                HideFlags.HideAndDontSave;
        }
    }
}
