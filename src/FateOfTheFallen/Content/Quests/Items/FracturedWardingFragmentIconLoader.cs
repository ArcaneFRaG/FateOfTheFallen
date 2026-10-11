using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class FracturedWardingFragmentIconLoader
    {
        private const string FileName =
            "FOTF_Warding_Fragment.png";

        private static Sprite _cachedSprite;


        internal static Sprite Load()
        {
            if (_cachedSprite != null)
            {
                return _cachedSprite;
            }

            try
            {
                Assembly assembly =
                    typeof(Plugin).Assembly;

                string resourceName =
                    ResolveResourceName(
                        assembly);

                if (string.IsNullOrEmpty(
                        resourceName))
                {
                    Plugin.NativeLog.LogError(
                        "Fractured Warding Fragment icon: embedded resource ending in [" +
                        FileName +
                        "] was not found. Expected the PNG under Resources/Items as an EmbeddedResource.");

                    return null;
                }

                using (Stream stream =
                       assembly.GetManifestResourceStream(
                           resourceName))
                {
                    if (stream == null)
                    {
                        Plugin.NativeLog.LogError(
                            "Fractured Warding Fragment icon: resource stream was null for [" +
                            resourceName +
                            "].");

                        return null;
                    }

                    byte[] pngBytes;

                    using (MemoryStream memory =
                           new MemoryStream())
                    {
                        stream.CopyTo(
                            memory);

                        pngBytes =
                            memory.ToArray();
                    }

                    Texture2D texture =
                        new Texture2D(
                            2,
                            2,
                            TextureFormat.RGBA32,
                            false);

                    texture.name =
                        "FOTF_Warding_Fragment_Texture";

                    texture.filterMode =
                        FilterMode.Bilinear;

                    texture.wrapMode =
                        TextureWrapMode.Clamp;

                    if (!ImageConversion.LoadImage(
                            texture,
                            pngBytes,
                            false))
                    {
                        UnityEngine.Object.Destroy(
                            texture);

                        Plugin.NativeLog.LogError(
                            "Fractured Warding Fragment icon: Unity failed to decode [" +
                            resourceName +
                            "].");

                        return null;
                    }

                    _cachedSprite =
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
                            Mathf.Max(
                                texture.width,
                                texture.height));

                    _cachedSprite.name =
                        "FOTF_Warding_Fragment_Icon";

                    Plugin.NativeLog.LogInfo(
                        "Fractured Warding Fragment icon: loaded embedded resource [" +
                        resourceName +
                        "] at [" +
                        texture.width +
                        "x" +
                        texture.height +
                        "].");

                    return _cachedSprite;
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fractured Warding Fragment icon: failed to load embedded icon: " +
                    exception);

                return null;
            }
        }


        private static string ResolveResourceName(
            Assembly assembly)
        {
            if (assembly == null)
            {
                return null;
            }

            string[] names =
                assembly.GetManifestResourceNames();

            if (names == null)
            {
                return null;
            }

            for (int i = 0;
                 i < names.Length;
                 i++)
            {
                string name =
                    names[i];

                if (string.IsNullOrEmpty(
                        name))
                {
                    continue;
                }

                if (name.EndsWith(
                        FileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }

            return null;
        }
    }
}
