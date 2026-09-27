using UnityEngine;

namespace FateOfTheFallen
{
    internal static class BlightcallerAssets
    {
        private const string ScrollIconResource =
            "FateOfTheFallen.Assets.Blightcaller_Scroll.png";

        private const string AuraIconResource =
            "FateOfTheFallen.Assets.Blightcaller_Aura.png";

        internal const string ClassIconResource =
            "FateOfTheFallen.Assets.BlightcallerClassIcon.png";

        internal static Sprite GetScrollIcon()
        {
            return EmbeddedAssetLoader.LoadSprite(
                ScrollIconResource,
                "Blightcaller Scroll Icon");
        }

        internal static Sprite GetAuraIcon()
        {
            return EmbeddedAssetLoader.LoadSprite(
                AuraIconResource,
                "Blightcaller Aura Icon");
        }

        internal static Sprite LoadSpellIcon(
            string resourceName)
        {
            return EmbeddedAssetLoader.LoadSprite(
                resourceName,
                "Blightcaller Spell Icon");
        }
    }
}
