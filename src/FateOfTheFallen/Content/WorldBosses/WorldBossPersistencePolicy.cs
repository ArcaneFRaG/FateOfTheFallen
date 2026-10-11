namespace FateOfTheFallen
{
    /// <summary>
    /// Permanent open-world bosses are gated only by the server's FOTF
    /// expansion unlock. Quest state is intentionally NOT part of this policy:
    /// completing Quest 7/8/9/10 must never despawn or permanently disable the
    /// corresponding boss, because the encounters remain farmable for gear.
    /// Varkesh and Gorath additionally run permanent spawn watchdogs so a
    /// completed quest, corpse cleanup, native refresh or scene-side cleanup
    /// cannot silently turn either encounter into one-time content.
    /// </summary>
    internal static class WorldBossPersistencePolicy
    {
        internal static bool CanExistInWorld
        {
            get { return FOTFExpansionGate.IsUnlocked; }
        }
    }
}
