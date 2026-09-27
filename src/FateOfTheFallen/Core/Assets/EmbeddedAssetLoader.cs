using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Shared embedded-resource loader for Fate of the Fallen.
    /// Content modules provide resource names; this class owns the
    /// common stream/texture/sprite loading and caching behaviour.
    /// </summary>
    internal static class EmbeddedAssetLoader
    {
        private static readonly Dictionary<string, Sprite> Sprites =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        internal static Sprite LoadSprite(
            string resourceName,
            string assetName)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                return null;
            }

            Sprite cached;

            if (Sprites.TryGetValue(resourceName, out cached))
            {
                return cached;
            }

            Assembly assembly =
                typeof(EmbeddedAssetLoader).Assembly;

            try
            {
                using (Stream stream =
                    assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Plugin.NativeLog.LogWarning(
                            "Fate of the Fallen: embedded resource not found: " +
                            resourceName);

                        return null;
                    }

                    byte[] imageData =
                        new byte[stream.Length];

                    int totalRead = 0;

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

                        totalRead += bytesRead;
                    }

                    if (totalRead != imageData.Length)
                    {
                        Plugin.NativeLog.LogWarning(
                            "Fate of the Fallen: failed to completely read embedded resource: " +
                            resourceName);

                        return null;
                    }

                    Texture2D texture =
                        new Texture2D(
                            2,
                            2,
                            TextureFormat.RGBA32,
                            false);

                    if (!texture.LoadImage(imageData, false))
                    {
                        Plugin.NativeLog.LogWarning(
                            "Fate of the Fallen: failed to decode embedded resource: " +
                            resourceName);

                        UnityEngine.Object.Destroy(texture);

                        return null;
                    }

                    texture.name =
                        string.IsNullOrEmpty(assetName)
                            ? resourceName
                            : assetName + " Texture";

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

                    if (sprite == null)
                    {
                        UnityEngine.Object.Destroy(texture);

                        Plugin.NativeLog.LogError(
                            "Fate of the Fallen: Sprite.Create failed for embedded resource: " +
                            resourceName);

                        return null;
                    }

                    sprite.name =
                        string.IsNullOrEmpty(assetName)
                            ? resourceName
                            : assetName;

                    Sprites[resourceName] =
                        sprite;

                    return sprite;
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: failed to load embedded resource [" +
                    resourceName +
                    "]: " +
                    exception);

                return null;
            }
        }

        internal static void Clear()
        {
            foreach (Sprite sprite in Sprites.Values)
            {
                if (sprite == null)
                {
                    continue;
                }

                if (sprite.texture != null)
                {
                    UnityEngine.Object.Destroy(sprite.texture);
                }

                UnityEngine.Object.Destroy(sprite);
            }

            Sprites.Clear();
        }
    }
}
