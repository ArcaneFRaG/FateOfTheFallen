using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Central visual identity for Emberweaver UI surfaces.
    ///
    /// The class icon is bespoke FOTF artwork made to match the circular
    /// Blightcaller class-icon treatment. It is separate from the Emberweaver
    /// spell and passive-skill icon sets.
    /// </summary>
    internal static class EmberweaverClassIcon
    {
        internal const string ClassIconFileName =
            "FOTF_Emberweaver_ClassIcon.png";

        internal static Sprite Sprite
        {
            get;
            private set;
        }

        /*
         * The source artwork is already fully coloured, so native Image
         * components should render it without an additional tint.
         */
        internal static readonly Color IconColor =
            Color.white;

        /*
         * Native RaidUI uses a darker plate colour than the external group
         * frames. Keep this readable behind the bright orange class art.
         */
        internal static readonly Color NameplateColor =
            new Color(
                0.68f,
                0.22f,
                0.055f,
                1f);

        /*
         * Canonical Emberweaver class colour for external class-colour UIs.
         * This is intentionally a brighter amber-orange so it remains clearly
         * distinguishable from Group Frames' Duelist and Reaver colours.
         */
        internal static readonly Color ClassColor =
            new Color(
                1.00f,
                0.50f,
                0.08f,
                1f);

        internal static void Initialize()
        {
            Sprite =
                EmbeddedSpriteLoader.LoadByFileName(
                    ClassIconFileName);

            if (Sprite == null)
            {
                Plugin.LogWarning(
                    "EmberweaverClassIcon: class icon sprite could not be loaded.");
            }
        }
    }
}
