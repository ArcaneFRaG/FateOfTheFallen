using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Optional embedded icon loader for Nhal'Sera equipment.
    /// Missing icons deliberately return null so the native template icon remains
    /// until bespoke art is added.
    /// </summary>
    internal static class NhalSeraEquipmentIconLoader
    {
        private const string ResourcePrefix = "FateOfTheFallen.Resources.Items.";

        private static readonly Dictionary<string, Sprite> Cache =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        internal static Sprite Load(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            Sprite cached;
            if (Cache.TryGetValue(fileName, out cached))
                return cached;

            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                Stream stream = assembly.GetManifestResourceStream(ResourcePrefix + fileName);

                if (stream == null)
                {
                    string[] names = assembly.GetManifestResourceNames();
                    for (int i = 0; names != null && i < names.Length; i++)
                    {
                        if (names[i].EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase) ||
                            names[i].EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            stream = assembly.GetManifestResourceStream(names[i]);
                            if (stream != null) break;
                        }
                    }
                }

                if (stream == null)
                {
                    Cache[fileName] = null;
                    return null;
                }

                using (stream)
                {
                    byte[] bytes = new byte[stream.Length];
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read <= 0) break;
                        offset += read;
                    }

                    Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(texture, bytes, false))
                    {
                        UnityEngine.Object.Destroy(texture);
                        Cache[fileName] = null;
                        return null;
                    }

                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.filterMode = FilterMode.Bilinear;

                    Sprite sprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);

                    sprite.name = Path.GetFileNameWithoutExtension(fileName) + "_Icon";
                    Cache[fileName] = sprite;
                    return sprite;
                }
            }
            catch
            {
                Cache[fileName] = null;
                return null;
            }
        }
    }
}
