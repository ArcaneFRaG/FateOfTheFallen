using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest/world item 1701.
    /// Recovered from the floating void orb at the heart of the
    /// Veiled Disturbance in Soluna's Landing.
    /// </summary>
    internal static class VeiledResidue
    {
        internal const string ItemId =
            "1701";

        internal const string ItemName =
            "Veiled Residue";

        private const string ItemLore =
            "A cold, weightless concentration of void residue gathered from a place where light seemed to vanish before reaching the earth. " +
            "Its surface appears almost black until viewed from the edge, where faint violet patterns move beneath it.";

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

            Sprite icon =
                VeiledResidueIconLoader.Load();

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
                    "FOTF quest items: failed to create Veiled Residue [1701].");

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
                "FOTF quest items: registered [1701] Veiled Residue.");

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
                "FOTF - Veiled Residue";

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
                VeiledResidueIconLoader.Load();

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
