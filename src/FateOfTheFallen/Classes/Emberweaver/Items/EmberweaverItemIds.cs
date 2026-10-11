namespace FateOfTheFallen
{
    /// <summary>
    /// Stable numeric Item.Id allocation for Emberweaver teaching items.
    ///
    /// FOTF currently reserves 1600-1999 for custom item IDs. Emberweaver
    /// owns 1630-1649 for its primary teaching block. 1643-1646 hold the
    /// higher Scorch / Surge ranks and 1647-1649 hold the Kindle line.
    /// Combust uses 1655, immediately after the existing Blightcaller aura
    /// block (1650-1654), because the original Emberweaver block is now full.
    /// Emberweaver aura items occupy 1656-1660.
    /// </summary>
    internal static class EmberweaverItemIds
    {
        internal const string EmberBoltScroll = "1630";
        internal const string PyroclasmScroll = "1631";
        internal const string ScorchScroll = "1632";
        internal const string FlameSurgeScroll = "1633";
        internal const string SearingBoltScroll = "1634";
        internal const string GreaterPyroclasmScroll = "1635";
        internal const string InfernoBoltScroll = "1636";
        internal const string DevastatingPyroclasmScroll = "1637";
        internal const string CataclysmicBoltScroll = "1638";
        internal const string ApocalypticPyroclasmScroll = "1639";

        internal const string HeatSkillbook = "1640";
        internal const string FlashfireSkillbook = "1641";
        internal const string WildfireSkillbook = "1642";

        // Additional normal-progression Emberweaver spell scrolls.
        internal const string SearingScorchScroll = "1643";
        internal const string BlazingSurgeScroll = "1644";
        internal const string InfernalScorchScroll = "1645";
        internal const string CataclysmicSurgeScroll = "1646";

        internal const string KindleScroll = "1647";
        internal const string StokeScroll = "1648";
        internal const string ConflagrateScroll = "1649";

        internal const string CombustScroll = "1655";

        internal const string AuraOfKindling = "1656";
        internal const string AuraOfEmbers = "1657";
        internal const string AuraOfTheFurnace = "1658";
        internal const string AuraOfIncandescence = "1659";
        internal const string AuraOfEternalFlame = "1660";

        internal const int ReservedStart = 1630;
        internal const int ReservedEnd = 1649;
        internal const int CombustReservedId = 1655;
    }
}
