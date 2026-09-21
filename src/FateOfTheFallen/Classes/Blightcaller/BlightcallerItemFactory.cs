using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class BlightcallerItemFactory
    {
        private const string CustomScrollIconResource =
            "FateOfTheFallen.Assets.Blightcaller_Scroll.png";

        private const string CustomAuraIconResource =
            "FateOfTheFallen.Assets.Blightcaller_Aura.png";

        private static readonly Dictionary<string, Item> ItemsById =
            new Dictionary<string, Item>(
                StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Item> ItemsByName =
            new Dictionary<string, Item>(
                StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Sprite> IconsByResource =
            new Dictionary<string, Sprite>(
                StringComparer.OrdinalIgnoreCase);

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

        internal static bool IsRegistered(
            string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            return ItemsById.ContainsKey(id);
        }

        internal static Sprite GetCustomScrollIcon()
        {
            return LoadEmbeddedIcon(
                CustomScrollIconResource,
                "Blightcaller Scroll Icon");
        }

        internal static Sprite GetCustomAuraIcon()
        {
            return LoadEmbeddedIcon(
                CustomAuraIconResource,
                "Blightcaller Aura Icon");
        }

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

        internal static Sprite LoadEmbeddedIcon(
            string resourceName,
            string iconName)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                return null;
            }

            Sprite cached;

            if (IconsByResource.TryGetValue(
                    resourceName,
                    out cached))
            {
                return cached;
            }

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

                        foreach (string resource in resources)
                        {
                            Plugin.NativeLog.LogWarning(
                                "  " +
                                resource);
                        }
                    }

                    return null;
                }

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

                IconsByResource[
                    resourceName] =
                    sprite;

                return sprite;
            }
        }

        internal static void Clear()
        {
            ItemsById.Clear();
            ItemsByName.Clear();

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