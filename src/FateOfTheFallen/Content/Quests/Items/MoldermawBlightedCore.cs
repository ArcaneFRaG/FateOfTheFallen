using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest item 1705.
    /// Recovered from Moldermaw, the Blight-corrupted rock golem.
    ///
    /// IMPORTANT:
    /// This item is created and resolved exclusively through
    /// CustomItemRegistry so save/load, native ItemDatabase registration,
    /// quest requirements and future loot-table entries all share one
    /// canonical Item instance.
    /// </summary>
    internal static class MoldermawBlightedCore
    {
        internal const string ItemId =
            "1705";

        internal const string ItemName =
            "Moldermaw's Blighted Core";

        private const string ItemLore =
            "A fractured piece of Moldermaw's stone core. " +
            "Sickly green Blight burns through deep cracks in the rock, " +
            "still pulsing with the force that drove the golem.";

        private static Item _item;


        internal static Item RegisterItem(
            ItemDatabase database)
        {
            if (_item != null)
            {
                ApplyDefinition(
                    _item);

                CustomItemRegistry.RegisterItem(
                    _item);

                if (database != null)
                {
                    CustomItemRegistry.RegisterWithNativeDatabase(
                        database,
                        _item);
                }

                return _item;
            }

            // Reuse the canonical registry instance if another system
            // resolved this item first.
            Item existing =
                CustomItemRegistry.GetItemById(
                    ItemId);

            if (existing != null)
            {
                _item =
                    existing;

                ApplyDefinition(
                    _item);

                CustomItemRegistry.RegisterItem(
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
                MoldermawBlightedCoreIconLoader.Load();

            // The only creation path for this item.
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
                        45,

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
                    "FOTF quest items: failed to create Moldermaw's Blighted Core [1705].");

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
                "FOTF quest items: registered [1705] Moldermaw's Blighted Core.");

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
                "FOTF - Moldermaw's Blighted Core";

            item.Id =
                ItemId;

            item.ItemName =
                ItemName;

            item.ItemLevel =
                45;

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

            item.NoTradeNoDestroy =
                true;

            item.SimPlayersCantGet =
                true;

            Sprite icon =
                MoldermawBlightedCoreIconLoader.Load();

            if (icon != null)
            {
                item.ItemIcon =
                    icon;
            }

            item.hideFlags =
                HideFlags.HideAndDontSave;

            // Reassert identity after every definition refresh.
            CustomItemRegistry.RegisterItem(
                item);
        }
    }
}
