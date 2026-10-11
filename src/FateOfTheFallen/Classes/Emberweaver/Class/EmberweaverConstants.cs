namespace FateOfTheFallen
{
    internal static class EmberweaverConstants
    {
        internal const string InternalId = "FOTF_Emberweaver";
        internal const string DisplayName = "Emberweaver";
        internal const string NativeTemplateClass = "Arcanist";

        internal const string PageGroupId = "FOTF";
        internal const string PageGroupTitle = "FATE OF THE FALLEN";

        internal const string Description =
            "A volatile spellcaster who builds Heat through sustained single-target fire magic, " +
            "turning critical strikes into sudden bursts of instant destructive power.";

        // Native character-creation grid in visual row-major order:
        // Arcanist / Windblade
        // Druid    / Paladin
        // Stormcaller / Reaver
        internal static readonly string[] NativeClassNames =
        {
            "Arcanist",
            "Windblade",
            "Druid",
            "Paladin",
            "Stormcaller",
            "Reaver"
        };
    }
}
