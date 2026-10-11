using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest/world item block starts at 1700.
    /// Blighted Spores is the first real FOTF quest item.
    /// </summary>
    internal static class BlightedSpores
    {
        internal const string ItemId =
            "1700";

        internal const string ItemName =
            "Blighted Spores";

        private static Item _item;

        internal static Item RegisterItem(
            ItemDatabase database)
        {
            if (_item != null)
            {
                if (database != null)
                {
                    CustomItemRegistry.RegisterWithNativeDatabase(
                        database,
                        _item);
                }

                return _item;
            }

            Item existing =
                CustomItemRegistry.GetItemById(
                    ItemId);

            if (existing != null)
            {
                _item = existing;

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

            Sprite icon =
                BlightedSporesIconLoader.Load();

            _item =
                CustomItemRegistry.CreateItem(
                    id:
                        ItemId,

                    itemName:
                        ItemName,

                    lore:
                        "A sample of swollen spores gathered from a plant twisted by the spreading blight. " +
                        "Even sealed away, the spores pulse faintly with unnatural vitality.",

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
                    "FOTF quest items: failed to create Blighted Spores [1700].");

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
                "FOTF quest items: registered [1700] Blighted Spores.");

            return _item;
        }

        internal static Item GetItem()
        {
            if (_item != null)
            {
                return _item;
            }

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
                "FOTF - Blighted Spores";

            item.Id =
                ItemId;

            item.ItemName =
                ItemName;

            item.ItemLevel =
                1;

            item.ItemValue =
                0;

            item.Lore =
                "A sample of swollen spores gathered from a plant twisted by the spreading blight. " +
                "Even sealed away, the spores pulse faintly with unnatural vitality.";

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
                BlightedSporesIconLoader.Load();

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
