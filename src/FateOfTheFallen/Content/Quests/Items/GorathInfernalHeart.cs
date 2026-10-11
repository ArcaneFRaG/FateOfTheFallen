using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 10 turn-in item. The heart is intentionally destroyable so a
    /// later Gorath kill can replace one the player discarded.
    /// </summary>
    internal static class GorathInfernalHeart
    {
        internal const string ItemId = "1708";
        internal const string ItemName = "Gorath's Infernal Heart";

        private const string ItemLore =
            "A blackened heart of infernal matter torn from Gorath. Heat moves through it in slow pulses, " +
            "but the surface never cools and the ember-light within seems to burn without consuming anything.";

        private static Item _item;

        internal static Item RegisterItem(ItemDatabase database)
        {
            if (_item == null)
                _item = CustomItemRegistry.GetItemById(ItemId);

            if (_item == null)
            {
                Sprite icon =
                    GorathInfernalHeartIconLoader.Load();

                // Keep the established infernal Quest 5 art only as a defensive
                // fallback if the dedicated embedded PNG fails to load.
                if (icon == null)
                {
                    Item cinderedShard =
                        CinderedRiftShard.GetItem();

                    if (cinderedShard != null)
                    {
                        icon =
                            cinderedShard.ItemIcon;
                    }
                }

                _item = CustomItemRegistry.CreateItem(
                    id: ItemId,
                    itemName: ItemName,
                    lore: ItemLore,
                    slot: Item.SlotType.General,
                    icon: icon,
                    itemLevel: 45,
                    itemValue: 0,
                    stackable: false,
                    disposable: true,
                    unique: true,
                    playerCannotSell: true,
                    noTradeNoDestroy: false,
                    simPlayersCantGet: true);
            }

            if (_item == null)
            {
                Plugin.NativeLog.LogError(
                    "FOTF quest items: failed to create Gorath's Infernal Heart [1708].");
                return null;
            }

            ApplyDefinition(_item);
            CustomItemRegistry.RegisterItem(_item);

            if (database != null)
                CustomItemRegistry.RegisterWithNativeDatabase(database, _item);

            return _item;
        }

        internal static Item GetItem()
        {
            if (_item != null)
                return _item;

            _item = CustomItemRegistry.GetItemById(ItemId);

            if (_item == null && GameData.ItemDB != null)
                _item = RegisterItem(GameData.ItemDB);

            return _item;
        }

        private static void ApplyDefinition(Item item)
        {
            if (item == null)
                return;

            item.name = "FOTF - " + ItemName;
            item.Id = ItemId;
            item.ItemName = ItemName;
            item.ItemLevel = 45;
            item.ItemValue = 0;
            item.Lore = ItemLore;
            item.RequiredSlot = Item.SlotType.General;
            item.Stackable = false;
            item.Disposable = true;
            item.Unique = true;
            item.PlayerCannotSell = true;
            item.NoTradeNoDestroy = false;
            item.SimPlayersCantGet = true;
            item.hideFlags = HideFlags.HideAndDontSave;

            Sprite icon =
                GorathInfernalHeartIconLoader.Load();

            if (icon != null)
            {
                item.ItemIcon =
                    icon;
            }
            else
            {
                Item cinderedShard =
                    CinderedRiftShard.GetItem();

                if (cinderedShard != null &&
                    cinderedShard.ItemIcon != null)
                {
                    item.ItemIcon =
                        cinderedShard.ItemIcon;
                }
            }
        }
    }
}
