using HarmonyLib;

namespace FateOfTheFallen
{
    /// <summary>
    /// The native SpellbookSlot tooltip already displays the currently hovered
    /// spell's SpellDesc (including the descriptive text supplied to our wards).
    /// Do not inject additional ward text into the game's shared tooltip label:
    /// that label is reused for other spells, leaking shield text into offensive
    /// tooltips and overflowing the tooltip's fixed-height panel.
    /// </summary>
    internal static class LightbringerSpellbookTooltip
    {
        internal static void Install(Harmony harmony)
        {
            // Compatibility no-op: Plugin startup still calls Install.
            // SpellDesc is assigned individually in LightbringerSpellCatalog.
            Plugin.LogInfo("[LBSpellbook] Native spell-specific descriptions enabled; " +
                "shared ward tooltip injection disabled.");
        }
    }
}
