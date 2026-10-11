using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Loads Emberweaver's embedded PNGs without coupling the class module to
    /// the standalone host. This can move unchanged into the main FOTF DLL.
    /// </summary>
    internal static class EmbeddedSpriteLoader
    {
        private static readonly Dictionary<string, Sprite> Cache =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        internal static Sprite LoadByFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            Sprite cached;
            if (Cache.TryGetValue(fileName, out cached))
                return cached;

            try
            {
                Assembly assembly = typeof(EmbeddedSpriteLoader).Assembly;
                string[] resources = assembly.GetManifestResourceNames();
                string wantedSuffix = "." + fileName;
                string resourceName = null;

                for (int i = 0; i < resources.Length; i++)
                {
                    if (resources[i].EndsWith(wantedSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        resourceName = resources[i];
                        break;
                    }
                }

                if (resourceName == null)
                    return null;

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                        return null;

                    byte[] bytes = new byte[stream.Length];
                    int read = 0;
                    while (read < bytes.Length)
                    {
                        int n = stream.Read(bytes, read, bytes.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }

                    Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    texture.name = "FOTF_Emberweaver_" + Path.GetFileNameWithoutExtension(fileName);

                    if (!ImageConversion.LoadImage(texture, bytes, false))
                    {
                        UnityEngine.Object.Destroy(texture);
                        return null;
                    }

                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.filterMode = FilterMode.Bilinear;

                    Sprite sprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);

                    sprite.name = texture.name + "_Sprite";
                    Cache[fileName] = sprite;
                    return sprite;
                }
            }
            catch (Exception exception)
            {
                Plugin.LogWarning("Emberweaver icon load failed for [" + fileName + "]: " + exception.Message);
                return null;
            }
        }

        internal static void TryAssignSpriteMember(object instance, Sprite sprite)
        {
            if (instance == null || sprite == null)
                return;

            string[] names =
            {
                "SpellIcon",
                "SkillIcon",
                "Icon",
                "MyIcon",
                "Sprite",
                "SpellSprite",
                "SkillSprite",
                "IconSprite"
            };

            Type type = instance.GetType();
            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            bool assigned = false;

            /*
             * Some Erenshor data objects expose more than one icon alias.
             * Populate every known Sprite alias instead of stopping after the
             * first match so the native spell/skill-book UI always sees the
             * custom Emberweaver artwork.
             */
            for (int i = 0; i < names.Length; i++)
            {
                FieldInfo field =
                    type.GetField(
                        names[i],
                        flags);

                if (field != null &&
                    typeof(Sprite).IsAssignableFrom(
                        field.FieldType))
                {
                    try
                    {
                        field.SetValue(
                            instance,
                            sprite);

                        assigned = true;
                    }
                    catch
                    {
                    }
                }

                PropertyInfo property =
                    type.GetProperty(
                        names[i],
                        flags);

                if (property != null &&
                    property.CanWrite &&
                    typeof(Sprite).IsAssignableFrom(
                        property.PropertyType))
                {
                    try
                    {
                        property.SetValue(
                            instance,
                            sprite,
                            null);

                        assigned = true;
                    }
                    catch
                    {
                    }
                }
            }

            if (assigned)
            {
                return;
            }

            /*
             * Fallback for a future Erenshor build that renames the icon
             * member: only touch Sprite members whose names clearly identify
             * them as an icon/sprite field.
             */
            foreach (FieldInfo field in type.GetFields(flags))
            {
                if (!typeof(Sprite).IsAssignableFrom(field.FieldType) ||
                    (field.Name.IndexOf(
                         "icon",
                         StringComparison.OrdinalIgnoreCase) < 0 &&
                     field.Name.IndexOf(
                         "sprite",
                         StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }

                try
                {
                    field.SetValue(
                        instance,
                        sprite);
                }
                catch
                {
                }
            }

            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                if (!property.CanWrite ||
                    !typeof(Sprite).IsAssignableFrom(property.PropertyType) ||
                    (property.Name.IndexOf(
                         "icon",
                         StringComparison.OrdinalIgnoreCase) < 0 &&
                     property.Name.IndexOf(
                         "sprite",
                         StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }

                try
                {
                    property.SetValue(
                        instance,
                        sprite,
                        null);
                }
                catch
                {
                }
            }
        }
    }
}
