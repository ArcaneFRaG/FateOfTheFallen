using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest/world item 1702.
    /// Recovered from one of the unnaturally risen dead during
    /// Quest 4 - The Restless Dead.
    /// </summary>
    internal static class LingeringSoulEssence
    {
        internal const string ItemId =
            "1702";

        internal const string ItemName =
            "Lingering Soul-Essence";

        private const string ItemLore =
            "A dense knot of soul-energy drawn from one of the restless dead. " +
            "No necromantic binding clings to it; the essence seems to have been " +
            "prevented from departing rather than summoned back.";

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
                LingeringSoulEssenceIconLoader.Load();

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
                    "FOTF quest items: failed to create Lingering Soul-Essence [1702].");

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
                "FOTF quest items: registered [1702] Lingering Soul-Essence.");

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
                "FOTF - Lingering Soul-Essence";

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
                LingeringSoulEssenceIconLoader.Load();

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
