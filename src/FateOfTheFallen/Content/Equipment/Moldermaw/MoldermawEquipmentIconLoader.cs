using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Embedded icon loader for the Moldermaw equipment set.
    ///
    /// Icons are optional during the model/code implementation phase. If an
    /// icon has not been added yet, the native template icon is intentionally
    /// preserved instead of replacing it with a blank sprite.
    /// </summary>
    internal static class MoldermawEquipmentIconLoader
    {
        private const string ResourcePrefix =
            "FateOfTheFallen.Resources.Items.";

        private static readonly Dictionary<string, Sprite> Cache =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        internal static Sprite Load(
            string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            Sprite cached;

            if (Cache.TryGetValue(
                    fileName,
                    out cached))
            {
                return cached;
            }

            string resourceName =
                ResourcePrefix +
                fileName;

            try
            {
                Assembly assembly =
                    Assembly.GetExecutingAssembly();

                Stream stream =
                    assembly.GetManifestResourceStream(
                        resourceName);

                if (stream == null)
                {
                    string[] names =
                        assembly.GetManifestResourceNames();

                    for (int i = 0;
                         names != null && i < names.Length;
                         i++)
                    {
                        if (names[i].EndsWith(
                                "." + fileName,
                                StringComparison.OrdinalIgnoreCase) ||
                            names[i].EndsWith(
                                fileName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            stream =
                                assembly.GetManifestResourceStream(
                                    names[i]);

                            if (stream != null)
                            {
                                resourceName =
                                    names[i];

                                break;
                            }
                        }
                    }
                }

                using (stream)
                {
                    if (stream == null)
                    {
                        Plugin.NativeLog.LogError(
                            "[MoldermawEquipment] embedded icon resource missing [" +
                            fileName +
                            "]. Expected=[" +
                            ResourcePrefix +
                            fileName +
                            "].");

                        Cache[fileName] = null;
                        return null;
                    }

                    byte[] bytes =
                        new byte[stream.Length];

                    int offset = 0;

                    while (offset < bytes.Length)
                    {
                        int read =
                            stream.Read(
                                bytes,
                                offset,
                                bytes.Length - offset);

                        if (read <= 0)
                        {
                            break;
                        }

                        offset += read;
                    }

                    Texture2D texture =
                        new Texture2D(
                            2,
                            2,
                            TextureFormat.RGBA32,
                            false);

                    texture.name =
                        Path.GetFileNameWithoutExtension(
                            fileName) +
                        "_Texture";

                    if (!ImageConversion.LoadImage(
                            texture,
                            bytes,
                            false))
                    {
                        UnityEngine.Object.Destroy(
                            texture);

                        Cache[fileName] = null;
                        return null;
                    }

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
                        Path.GetFileNameWithoutExtension(
                            fileName) +
                        "_Icon";

                    Cache[fileName] =
                        sprite;

                    Plugin.NativeLog.LogInfo(
                        "[MoldermawEquipment] icon loaded [" +
                        fileName +
                        "] Resource=[" +
                        resourceName +
                        "] Size=[" +
                        texture.width +
                        "x" +
                        texture.height +
                        "].");

                    return sprite;
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[MoldermawEquipment] icon load failed for [" +
                    fileName +
                    "]: " +
                    exception.Message);

                Cache[fileName] = null;
                return null;
            }
        }
    }
}
