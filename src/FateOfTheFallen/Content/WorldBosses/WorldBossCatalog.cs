namespace FateOfTheFallen
{
    /// <summary>
    /// v0.2 world-boss catalog. World-boss tuning shared by the permanent world spawn runtimes and encounter logic.
    /// </summary>
    internal static class WorldBossCatalog
    {
        internal static readonly WorldBossDefinition BlightTouchedColossus =
            new WorldBossDefinition
            {
                Id = "FOTF_WORLD_BOSS_BLIGHT_TOUCHED_COLOSSUS",
                DisplayName = "Moldermaw",
                Level = 45,
                MaxHP = 30000000,
                BaseAttackDamage = 6000,
                ScaleMultiplier = 2.75f,
                AttackSpeedMultiplier = 1.75f,
                RespawnDelaySeconds = 44f * 60f,
                LeashRadius = 65f,
                ResetNoTargetSeconds = 8f,
                RewardItemId = MoldermawBlightedCore.ItemId,
                RewardItemName = MoldermawBlightedCore.ItemName,
                CompletionCreditId = "FOTF_WORLD_BOSS_BLIGHT_TOUCHED_COLOSSUS_DEFEATED"
            };

        // Nhal'Sera uses the same custom-creature construction path as Moldermaw.
        // Permanent placement is owned by NhalSeraWorldSpawnRuntime.
        internal static readonly WorldBossDefinition NhalSera =
            new WorldBossDefinition
            {
                Id = "FOTF_WORLD_BOSS_NHALSERA",
                DisplayName = "Nhal'Sera",
                Level = 45,
                MaxHP = 30000000,
                BaseAttackDamage = 6000,
                ScaleMultiplier = 1.0f,
                AttackSpeedMultiplier = 1.75f,
                RespawnDelaySeconds = 44f * 60f,
                LeashRadius = 65f,
                ResetNoTargetSeconds = 8f,
                RewardItemId = null,
                RewardItemName = null,
                CompletionCreditId = "FOTF_WORLD_BOSS_NHALSERA_DEFEATED"
            };

        // Varkesh remains on the proven native Sivakayan Templar gameplay
        // runtime. VarkeshWorldSpawnRuntime owns native-template resolution,
        // permanent Willowwatch placement and the boss lifecycle.
        //
        // Do not route Varkesh through NativeWorldBossVisualRegistry or
        // CustomCreatureRuntime until a separate migration has been validated.
        internal static readonly WorldBossDefinition Varkesh =
            new WorldBossDefinition
            {
                Id = "FOTF_WORLD_BOSS_VARKESH",
                DisplayName = "Varkesh the Unburied",
                Level = 45,
                MaxHP = 30000000,
                BaseAttackDamage = 6000,
                ScaleMultiplier = 2.75f,
                AttackSpeedMultiplier = 1.35f,
                RespawnDelaySeconds = 44f * 60f,
                // Varkesh has a much larger hard reset boundary than his
                // normal tanking area. Sim tanks are softly pulled back toward
                // the encounter centre long before this value is reached.
                LeashRadius = 90f,
                ResetNoTargetSeconds = 8f,
                RewardItemId = null,
                RewardItemName = null,
                CompletionCreditId = "FOTF_WORLD_BOSS_VARKESH_DEFEATED"
            };


        // Gorath uses "An Apparition of Sivakaya" only as a sanitized native
        // visual donor. CustomCreatureRuntime owns his complete gameplay
        // component graph. Permanent Vitheo's Watch placement is owned by
        // GorathWorldSpawnRuntime.
        internal static readonly WorldBossDefinition Gorath =
            new WorldBossDefinition
            {
                Id = "FOTF_WORLD_BOSS_GORATH",
                DisplayName = "Gorath",
                Level = 45,
                MaxHP = 30000000,
                BaseAttackDamage = 6000,
                ScaleMultiplier = 2.0f,
                AttackSpeedMultiplier = 1.45f,
                RespawnDelaySeconds = 44f * 60f,
                LeashRadius = 75f,
                ResetNoTargetSeconds = 8f,
                RewardItemId = GorathInfernalHeart.ItemId,
                RewardItemName = GorathInfernalHeart.ItemName,
                CompletionCreditId = "FOTF_WORLD_BOSS_GORATH_DEFEATED"
            };

    }
}
