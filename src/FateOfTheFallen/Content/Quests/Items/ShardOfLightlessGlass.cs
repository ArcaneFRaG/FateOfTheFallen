using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest item 1706. Guaranteed from Nhal'Sera while Quest 8 is active.
    /// Unlike the old development quest items, this is intentionally destroyable
    /// so the player can discard it and recover another one from a later kill.
    /// </summary>
    internal static class ShardOfLightlessGlass
    {
        internal const string ItemId = "1706";
        internal const string ItemName = "Shard of Lightless Glass";
        private const string IconFile = "FOTF_Shard_of_Lightless_Glass.png";

        private const string ItemLore =
            "A razor-thin shard recovered from Nhal'Sera. Its black-violet surface " +
            "reflects almost no light, and its edge seems slightly displaced from " +
            "the space around it.";

        private static Item _item;

        internal static Item RegisterItem(ItemDatabase database)
        {
            if (_item == null)
                _item = CustomItemRegistry.GetItemById(ItemId);

            if (_item == null)
            {
                Sprite icon =
                    NhalSeraEquipmentIconLoader.Load(IconFile);

                if (icon == null)
                {
                    Item residue = VeiledResidue.GetItem();
                    if (residue != null)
                        icon = residue.ItemIcon;
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
                    "FOTF quest items: failed to create Shard of Lightless Glass [1706].");
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

            // Deliberately destroyable. A later Nhal'Sera kill can replace it.
            item.Disposable = true;
            item.Unique = true;
            item.PlayerCannotSell = true;
            item.NoTradeNoDestroy = false;
            item.SimPlayersCantGet = true;
            item.hideFlags = HideFlags.HideAndDontSave;

            Sprite icon =
                NhalSeraEquipmentIconLoader.Load(IconFile);

            if (icon != null)
            {
                item.ItemIcon = icon;
                return;
            }

            Item residue = VeiledResidue.GetItem();
            if (residue != null && residue.ItemIcon != null)
                item.ItemIcon = residue.ItemIcon;
        }
    }
}
