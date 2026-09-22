using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class BlightcallerClassIcon
    {
        internal static Sprite Sprite { get; private set; }

        internal static readonly Color IconColor =
            new Color(0.55f, 0.20f, 0.75f, 1f);

        internal static readonly Color NameplateColor =
            new Color(0.70f, 0.35f, 0.85f, 1f);

        private const string IconResourceName =
            "FateOfTheFallen.Assets.BlightcallerClassIcon.png";

        internal static void Initialize()
        {
            

            try
            {
                Assembly assembly =
                    Assembly.GetExecutingAssembly();

                using (Stream stream =
                    assembly.GetManifestResourceStream(IconResourceName))
                {
                    if (stream == null)
                    {
                        Plugin.NativeLog.LogWarning(
                            "BlightcallerClassIcon: embedded resource not found: " +
                            IconResourceName);

                        
                            string.Join(
                                ", ",
                                assembly.GetManifestResourceNames());

                        Sprite = null;
                        return;
                    }

                    byte[] data;

                    using (MemoryStream memoryStream =
                        new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        data = memoryStream.ToArray();
                    }

                    if (data.Length == 0)
                    {
                        Plugin.NativeLog.LogWarning(
                            "BlightcallerClassIcon: embedded PNG is empty.");

                        Sprite = null;
                        return;
                    }

                    Texture2D texture =
                        new Texture2D(
                            2,
                            2,
                            TextureFormat.RGBA32,
                            false);

                    if (!texture.LoadImage(data))
                    {
                        UnityEngine.Object.Destroy(texture);

                        Plugin.NativeLog.LogError(
                            "BlightcallerClassIcon: Unity failed to load embedded PNG.");

                        Sprite = null;
                        return;
                    }

                    texture.name =
                        "BlightcallerClassIcon_Texture";

                    Sprite =
                        UnityEngine.Sprite.Create(
                            texture,
                            new Rect(
                                0f,
                                0f,
                                texture.width,
                                texture.height),
                            new Vector2(0.5f, 0.5f),
                            100f);

                    if (Sprite == null)
                    {
                        UnityEngine.Object.Destroy(texture);

                        Plugin.NativeLog.LogError(
                            "BlightcallerClassIcon: Sprite.Create returned null.");

                        return;
                    }

                    Sprite.name =
                        "BlightcallerClassIcon";

                    
                }
            }
            catch (Exception exception)
            {
                Sprite = null;

                Plugin.NativeLog.LogError(
                    "BlightcallerClassIcon: failed to initialize: " +
                    exception);
            }
        }
    }
}