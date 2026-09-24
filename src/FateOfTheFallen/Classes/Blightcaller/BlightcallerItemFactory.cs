using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class BlightcallerItemFactory
    {
        // ============================================================
        // EMBEDDED RESOURCES
        // ============================================================

        private const string CustomScrollIconResource =
            "FateOfTheFallen.Assets.Blightcaller_Scroll.png";

        private const string CustomAuraIconResource =
            "FateOfTheFallen.Assets.Blightcaller_Aura.png";


        // ============================================================
        // ITEM REGISTRY
        // ============================================================

        private static readonly Dictionary<string, Item> ItemsById =
            new Dictionary<string, Item>(
                StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Item> ItemsByName =
            new Dictionary<string, Item>(
                StringComparer.OrdinalIgnoreCase);


        // ============================================================
        // ICON CACHE
        // ============================================================

        private static readonly Dictionary<string, Sprite> IconsByResource =
            new Dictionary<string, Sprite>(
                StringComparer.OrdinalIgnoreCase);


        // ============================================================
        // GENERIC ITEM CREATION
        // ============================================================
        //
        // This is the common creation path for custom Fate of the
        // Fallen items.
        //
        // Individual systems should use this rather than directly
        // calling ScriptableObject.CreateInstance<Item>().
        //
        // The specialised system can then configure additional fields
        // after creation, for example:
        //
        //     BookTitle
        //     AssignQuestOnRead
        //     CompleteOnRead
        //     TeachSpell
        //     TeachSkill
        //     Aura
        //     WornEffect
        //
        // CreateItem also automatically registers the resulting item
        // with the custom item registry.
        // ============================================================

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
            // ========================================================
            // VALIDATION
            // ========================================================

            if (string.IsNullOrEmpty(id))
            {
                Plugin.NativeLog.LogError(
                    "BlightcallerItemFactory: cannot create an item without an ID.");

                return null;
            }

            if (string.IsNullOrEmpty(itemName))
            {
                Plugin.NativeLog.LogError(
                    "BlightcallerItemFactory: cannot create item " +
                    id +
                    " without an ItemName.");

                return null;
            }


            // ========================================================
            // EXISTING ITEM
            // ========================================================
            //
            // Registration methods can be called more than once as
            // Erenshor databases initialise/reinitialise.
            //
            // Never create a second ScriptableObject for the same ID.
            // ========================================================

            Item existing =
                GetItemById(
                    id);

            if (existing != null)
            {
                return existing;
            }


            // ========================================================
            // CREATE ITEM
            // ========================================================

            Item item =
                ScriptableObject.CreateInstance<Item>();

            if (item == null)
            {
                Plugin.NativeLog.LogError(
                    "BlightcallerItemFactory: ScriptableObject.CreateInstance<Item>() " +
                    "returned null for " +
                    id +
                    ".");

                return null;
            }


            // ========================================================
            // IDENTITY
            // ========================================================

            item.name =
                itemName;

            item.Id =
                id;

            item.ItemName =
                itemName;


            // ========================================================
            // BASIC DATA
            // ========================================================

            item.ItemLevel =
                itemLevel;

            item.ItemValue =
                itemValue;

            item.RequiredSlot =
                slot;

            item.Lore =
                lore ?? string.Empty;


            // ========================================================
            // ICON
            // ========================================================

            item.ItemIcon =
                icon;


            // ========================================================
            // CLASS ACCESS
            // ========================================================
            //
            // Start with an empty list.
            //
            // The specialised item creator can add class restrictions
            // afterwards if required.
            //
            // Quest items, notes, general world objects, etc. normally
            // remain unrestricted.
            // ========================================================

            item.Classes =
                new List<Class>();


            // ========================================================
            // INVENTORY BEHAVIOUR
            // ========================================================

            item.Stackable =
                stackable;

            item.Disposable =
                disposable;

            item.Unique =
                unique;

            item.PlayerCannotSell =
                playerCannotSell;

            item.NoTradeNoDestroy =
                noTradeNoDestroy;

            item.SimPlayersCantGet =
                simPlayersCantGet;


            // ========================================================
            // UNITY LIFETIME
            // ========================================================

            item.hideFlags =
                HideFlags.HideAndDontSave;


            // ========================================================
            // REGISTER
            // ========================================================

            RegisterItem(
                item);



            return item;
        }


        // ============================================================
        // REGISTER ITEM
        // ============================================================

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
                    "BlightcallerItemFactory: refusing to register item with no ID: " +
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


        // ============================================================
        // LOOKUP BY ID
        // ============================================================

        internal static Item GetItemById(
            string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            Item item;

            if (ItemsById.TryGetValue(
                    id,
                    out item))
            {
                return item;
            }

            return null;
        }


        // ============================================================
        // LOOKUP BY NAME
        // ============================================================

        internal static Item GetItem(
            string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                return null;
            }

            Item item;

            if (ItemsByName.TryGetValue(
                    itemName,
                    out item))
            {
                return item;
            }

            return null;
        }


        // ============================================================
        // REGISTERED CHECK
        // ============================================================

        internal static bool IsRegistered(
            string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            return ItemsById.ContainsKey(
                id);
        }


        // ============================================================
        // BLIGHTCALLER SCROLL ICON
        // ============================================================

        internal static Sprite GetCustomScrollIcon()
        {
            return LoadEmbeddedIcon(
                CustomScrollIconResource,
                "Blightcaller Scroll Icon");
        }


        // ============================================================
        // BLIGHTCALLER AURA ICON
        // ============================================================

        internal static Sprite GetCustomAuraIcon()
        {
            return LoadEmbeddedIcon(
                CustomAuraIconResource,
                "Blightcaller Aura Icon");
        }


        // ============================================================
        // GENERIC EMBEDDED SPRITE LOAD
        // ============================================================

        internal static Sprite Load(
            string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                return null;
            }

            return LoadEmbeddedIcon(
                resourceName,
                "Blightcaller Spell Icon");
        }


        // ============================================================
        // LOAD EMBEDDED ICON
        // ============================================================

        internal static Sprite LoadEmbeddedIcon(
            string resourceName,
            string iconName)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                return null;
            }


            // ========================================================
            // CACHE
            // ========================================================

            Sprite cached;

            if (IconsByResource.TryGetValue(
                    resourceName,
                    out cached))
            {
                return cached;
            }


            // ========================================================
            // RESOURCE LOOKUP
            // ========================================================

            Assembly assembly =
                typeof(BlightcallerItemFactory).Assembly;

            using (Stream stream =
                assembly.GetManifestResourceStream(
                    resourceName))
            {
                if (stream == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "Blightcaller: embedded icon resource not found: " +
                        resourceName);

                    string[] resources =
                        assembly.GetManifestResourceNames();

                    if (resources == null ||
                        resources.Length == 0)
                    {
                        Plugin.NativeLog.LogWarning(
                            "Blightcaller: assembly contains no embedded resources.");
                    }
                    else
                    {
                        Plugin.NativeLog.LogWarning(
                            "Blightcaller: available embedded resources:");

                        foreach (
                            string resource
                            in resources)
                        {
                            Plugin.NativeLog.LogWarning(
                                "  " +
                                resource);
                        }
                    }

                    return null;
                }


                // ====================================================
                // READ IMAGE
                // ====================================================

                byte[] imageData =
                    new byte[stream.Length];

                int totalRead =
                    0;

                while (totalRead < imageData.Length)
                {
                    int bytesRead =
                        stream.Read(
                            imageData,
                            totalRead,
                            imageData.Length - totalRead);

                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    totalRead +=
                        bytesRead;
                }

                if (totalRead != imageData.Length)
                {
                    Plugin.NativeLog.LogWarning(
                        "Blightcaller: failed to completely read embedded icon: " +
                        resourceName);

                    return null;
                }


                // ====================================================
                // CREATE TEXTURE
                // ====================================================

                Texture2D texture =
                    new Texture2D(
                        2,
                        2,
                        TextureFormat.RGBA32,
                        false);

                if (!texture.LoadImage(
                        imageData,
                        false))
                {
                    Plugin.NativeLog.LogWarning(
                        "Blightcaller: failed to decode embedded icon: " +
                        resourceName);

                    UnityEngine.Object.Destroy(
                        texture);

                    return null;
                }

                texture.name =
                    iconName;

                texture.wrapMode =
                    TextureWrapMode.Clamp;

                texture.filterMode =
                    FilterMode.Bilinear;


                // ====================================================
                // CREATE SPRITE
                // ====================================================

                Sprite sprite =
                    Sprite.Create(
                        texture,
                        new Rect(
                            0f,
                            0f,
                            texture.width,
                            texture.height),
                        new Vector2(
                            0.5f,
                            0.5f),
                        100f);

                sprite.name =
                    iconName;


                // ====================================================
                // CACHE
                // ====================================================

                IconsByResource[
                    resourceName] =
                    sprite;

                return sprite;
            }
        }


        // ============================================================
        // CLEAR
        // ============================================================

        internal static void Clear()
        {
            ItemsById.Clear();

            ItemsByName.Clear();


            // ========================================================
            // DESTROY CACHED SPRITES / TEXTURES
            // ========================================================

            foreach (
                Sprite sprite
                in IconsByResource.Values)
            {
                if (sprite == null)
                {
                    continue;
                }

                if (sprite.texture != null)
                {
                    UnityEngine.Object.Destroy(
                        sprite.texture);
                }

                UnityEngine.Object.Destroy(
                    sprite);
            }

            IconsByResource.Clear();
        }
    }
}