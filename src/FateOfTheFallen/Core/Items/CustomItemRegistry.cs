using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Shared registry and creation path for every custom Fate of the
    /// Fallen item. Class/content modules should register through this
    /// type rather than owning separate item registries.
    /// </summary>
    internal static class CustomItemRegistry
    {
        private static readonly Dictionary<string, Item> ItemsById =
            new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Item> ItemsByName =
            new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Func<ItemDatabase, Item>> LazyResolvers =
            new Dictionary<string, Func<ItemDatabase, Item>>(StringComparer.OrdinalIgnoreCase);

        internal static Item CreateItem(
            string id,
            string itemName,
            string lore,
            Item.SlotType slot,
            Sprite icon,
            int itemLevel = 1,
            int itemValue = 0,
            bool stackable = false,
            bool disposable = false,
            bool unique = false,
            bool playerCannotSell = false,
            bool noTradeNoDestroy = false,
            bool simPlayersCantGet = false)
        {
            if (string.IsNullOrEmpty(id))
            {
                Plugin.NativeLog.LogError(
                    "CustomItemRegistry: cannot create an item without an ID.");

                return null;
            }

            if (string.IsNullOrEmpty(itemName))
            {
                Plugin.NativeLog.LogError(
                    "CustomItemRegistry: cannot create item " +
                    id +
                    " without an ItemName.");

                return null;
            }

            Item existing =
                GetItemById(id);

            if (existing != null)
            {
                return existing;
            }

            Item item =
                ScriptableObject.CreateInstance<Item>();

            if (item == null)
            {
                Plugin.NativeLog.LogError(
                    "CustomItemRegistry: ScriptableObject.CreateInstance<Item>() returned null for " +
                    id +
                    ".");

                return null;
            }

            item.name = itemName;
            item.Id = id;
            item.ItemName = itemName;
            item.ItemLevel = itemLevel;
            item.ItemValue = itemValue;
            item.RequiredSlot = slot;
            item.Lore = lore ?? string.Empty;
            item.ItemIcon = icon;
            item.Classes = new List<Class>();
            item.Stackable = stackable;
            item.Disposable = disposable;
            item.Unique = unique;
            item.PlayerCannotSell = playerCannotSell;
            item.NoTradeNoDestroy = noTradeNoDestroy;
            item.SimPlayersCantGet = simPlayersCantGet;
            item.hideFlags = HideFlags.HideAndDontSave;

            RegisterItem(item);

            return item;
        }

        internal static void RegisterItem(
            Item item)
        {
            if (item == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(item.Id))
            {
                Plugin.NativeLog.LogWarning(
                    "CustomItemRegistry: refusing to register item with no ID: " +
                    item.ItemName);

                return;
            }

            ItemsById[item.Id] =
                item;

            if (!string.IsNullOrEmpty(item.ItemName))
            {
                ItemsByName[item.ItemName] =
                    item;
            }
        }

        internal static Item GetItemById(
            string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            Item item;

            return ItemsById.TryGetValue(id, out item)
                ? item
                : null;
        }

        internal static Item GetItem(
            string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                return null;
            }

            Item item;

            return ItemsByName.TryGetValue(itemName, out item)
                ? item
                : null;
        }

        internal static bool IsRegistered(
            string id)
        {
            return
                !string.IsNullOrEmpty(id) &&
                ItemsById.ContainsKey(id);
        }

        internal static void RegisterLazyResolver(
            string id,
            Func<ItemDatabase, Item> resolver)
        {
            if (string.IsNullOrEmpty(id) ||
                resolver == null)
            {
                return;
            }

            LazyResolvers[id] =
                resolver;
        }

        internal static Item Resolve(
            string id,
            ItemDatabase database)
        {
            Item item =
                GetItemById(id);

            if (item != null)
            {
                return item;
            }

            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            Func<ItemDatabase, Item> resolver;

            if (!LazyResolvers.TryGetValue(
                    id,
                    out resolver))
            {
                return null;
            }

            try
            {
                item =
                    resolver(database);

                if (item != null)
                {
                    RegisterItem(item);
                }

                return item;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: lazy item resolver failed for ID [" +
                    id +
                    "]: " +
                    exception);

                return null;
            }
        }

        internal static void RegisterWithNativeDatabase(
            ItemDatabase database,
            Item item)
        {
            if (database == null ||
                item == null ||
                string.IsNullOrEmpty(item.Id))
            {
                return;
            }

            if (database.ItemDB == null)
            {
                database.ItemDB =
                    new Item[]
                    {
                        item
                    };
            }
            else
            {
                bool found = false;

                for (int i = 0;
                     i < database.ItemDB.Length;
                     i++)
                {
                    Item existing =
                        database.ItemDB[i];

                    if (existing == item)
                    {
                        found = true;
                        break;
                    }

                    if (existing == null ||
                        !string.Equals(
                            existing.Id,
                            item.Id,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    database.ItemDB[i] = item;
                    found = true;
                    break;
                }

                if (!found)
                {
                    int oldLength =
                        database.ItemDB.Length;

                    Item[] expanded =
                        new Item[oldLength + 1];

                    Array.Copy(
                        database.ItemDB,
                        expanded,
                        oldLength);

                    expanded[oldLength] =
                        item;

                    database.ItemDB =
                        expanded;
                }
            }

            if (database.ItemDBList == null)
            {
                database.ItemDBList =
                    new List<Item>();
            }

            bool foundInList =
                false;

            for (int i = 0;
                 i < database.ItemDBList.Count;
                 i++)
            {
                Item existing =
                    database.ItemDBList[i];

                if (existing == item)
                {
                    foundInList = true;
                    break;
                }

                if (existing == null ||
                    !string.Equals(
                        existing.Id,
                        item.Id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                database.ItemDBList[i] =
                    item;

                foundInList = true;
                break;
            }

            if (!foundInList)
            {
                database.ItemDBList.Add(item);
            }

            FieldInfo itemDictField =
                AccessTools.Field(
                    typeof(ItemDatabase),
                    "itemDict");

            if (itemDictField == null)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: ItemDatabase.itemDict field was not found.");

                return;
            }

            Dictionary<string, Item> itemDict =
                itemDictField.GetValue(database)
                as Dictionary<string, Item>;

            if (itemDict == null)
            {
                return;
            }

            itemDict[item.Id] =
                item;
        }

        internal static void Clear()
        {
            ItemsById.Clear();
            ItemsByName.Clear();
            LazyResolvers.Clear();
        }
    }
}
