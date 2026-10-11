using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest/world item 1703.
    /// Recovered from the Flame Rift in Blacksalt Strand during
    /// Quest 5 - Ash Beneath the Earth.
    ///
    /// This intentionally follows the same canonical CustomItemRegistry
    /// pattern as Veiled Residue [1701] and Lingering Soul-Essence [1702].
    /// </summary>
    internal static class CinderedRiftShard
    {
        internal const string ItemId =
            "1703";

        internal const string ItemName =
            "Cindered Rift Shard";

        private const string ItemLore =
            "A blackened fragment taken from the edge of the Flame Rift. " +
            "Heat pulses through it in slow waves, yet its surface never cools. " +
            "The residue clinging to the shard feels deliberate rather than natural.";

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
                CinderedRiftShardIconLoader.Load();

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
                    "FOTF quest items: failed to create Cindered Rift Shard [1703].");

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
                "FOTF quest items: registered [1703] Cindered Rift Shard.");

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
                "FOTF - Cindered Rift Shard";

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
                CinderedRiftShardIconLoader.Load();

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
