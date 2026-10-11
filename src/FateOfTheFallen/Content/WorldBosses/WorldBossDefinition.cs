namespace FateOfTheFallen
{
    /// <summary>
    /// Data-only description for a Fate of the Fallen open-world raid boss.
    /// Final authored scene/position can be filled in later without changing
    /// the runtime lifecycle or encounter code.
    /// </summary>
    internal sealed class WorldBossDefinition
    {
        internal string Id;
        internal string DisplayName;
        internal int Level;
        internal int MaxHP;
        internal int BaseAttackDamage;
        internal float ScaleMultiplier;
        internal float AttackSpeedMultiplier;
        internal float RespawnDelaySeconds;
        internal float LeashRadius;
        internal float ResetNoTargetSeconds;
        internal string RewardItemId;
        internal string RewardItemName;
        internal string CompletionCreditId;
    }
}
