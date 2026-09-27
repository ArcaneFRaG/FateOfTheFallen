using UnityEngine;

namespace FateOfTheFallen
{
    internal static class BlightcallerClassIcon
    {
        internal static Sprite Sprite { get; private set; }

        internal static readonly Color IconColor =
            new Color(0.55f, 0.20f, 0.75f, 1f);

        internal static readonly Color NameplateColor =
            new Color(0.22f, 0.30f, 0.24f, 1f);

        internal static void Initialize()
        {
            Sprite =
                EmbeddedAssetLoader.LoadSprite(
                    BlightcallerAssets.ClassIconResource,
                    "BlightcallerClassIcon");

            if (Sprite == null)
            {
                Plugin.NativeLog.LogWarning(
                    "BlightcallerClassIcon: class icon sprite could not be loaded.");
            }
        }
    }
}
