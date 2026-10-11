using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Central visual identity for Lightbringer UI surfaces.
    ///
    /// The class icon is bespoke FOTF artwork made to match the circular
    /// Blightcaller class-icon treatment. It is separate from the Lightbringer
    /// spell and passive-skill icon sets.
    /// </summary>
    internal static class LightbringerClassIcon
    {
        internal const string ClassIconFileName =
            "FOTF_Lightbringer_ClassIcon.png";

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
         * Native RaidUI uses a darker neutral plate than the external group
         * frames to keep the gold class artwork readable with white labels.
         */
        internal static readonly Color NameplateColor =
            new Color(0.24f, 0.27f, 0.31f, 1f);

        /*
         * Canonical Lightbringer class colour for external class-colour UIs.
         * White keeps the class distinct from other FOTF and native classes.
         * The golden class icon artwork is not tinted by this value.
         */
        internal static readonly Color ClassColor =
            new Color(1f, 1f, 1f, 1f);

        internal static void Initialize()
        {
            Sprite =
                EmbeddedSpriteLoader.LoadByFileName(
                    ClassIconFileName);

            if (Sprite == null)
            {
                Plugin.LogWarning(
                    "LightbringerClassIcon: class icon sprite could not be loaded.");
            }
        }
    }
}
