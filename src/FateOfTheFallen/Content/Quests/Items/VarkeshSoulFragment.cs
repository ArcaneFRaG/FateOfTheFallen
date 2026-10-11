using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 9 turn-in item. The fragment is intentionally destroyable so a
    /// later Varkesh kill can replace one the player discarded.
    /// </summary>
    internal static class VarkeshSoulFragment
    {
        internal const string ItemId = "1707";
        internal const string ItemName = "Varkesh's Soul Fragment";

        private const string ItemLore =
            "A splinter of Varkesh's soul condensed into a cold translucent fragment. " +
            "It trembles faintly, as though trying to continue a journey it was never allowed to finish.";

        private static Item _item;

        internal static Item RegisterItem(ItemDatabase database)
        {
            if (_item == null)
                _item = CustomItemRegistry.GetItemById(ItemId);

            if (_item == null)
            {
                Sprite icon = null;

                // The existing soul-essence art is a safe thematic fallback
                // until Varkesh receives dedicated quest-item artwork.
                Item soulEssence = LingeringSoulEssence.GetItem();
                if (soulEssence != null)
                    icon = soulEssence.ItemIcon;

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
                    "FOTF quest items: failed to create Varkesh's Soul Fragment [1707].");
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

            Item soulEssence = LingeringSoulEssence.GetItem();
            if (soulEssence != null && soulEssence.ItemIcon != null)
                item.ItemIcon = soulEssence.ItemIcon;
        }
    }
}
