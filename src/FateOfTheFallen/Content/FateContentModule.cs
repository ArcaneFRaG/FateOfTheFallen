using FateOfTheFallen.Content.Quests.World;
using System;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class FateContentModule
    {
        private static bool _initialized;
        private static bool _expansionActive;


        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized =
                true;

            FOTFExpansionGate
                .EffectiveUnlockChanged +=
                    OnExpansionGateChanged;

            FOTFExpansionGate.Initialize();

            if (FOTFExpansionGate.IsUnlocked)
            {
                ActivateExpansionContent();
            }
        }


        internal static void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            FOTFExpansionGate
                .EffectiveUnlockChanged -=
                    OnExpansionGateChanged;

            DeactivateExpansionContent();

            FOTFExpansionGate.Shutdown();

            _initialized =
                false;
        }


        private static void OnExpansionGateChanged(
            bool unlocked)
        {
            if (unlocked)
            {
                ActivateExpansionContent();
            }
            else
            {
                DeactivateExpansionContent();
            }
        }


        private static void ActivateExpansionContent()
        {
            if (_expansionActive)
            {
                RegisterDatabasesIfReady();
                return;
            }

            _expansionActive =
                true;

            float activationStarted =
                Time.realtimeSinceStartup;

            float activationCheckpoint =
                activationStarted;

            if (FOTFContentOptions.Active.Questline)
            {
                FateOfTheFallenQuests.Register();

                // Quest 5 is additive to the existing Quest 1-4 registry.
                AshBeneathEarthQuest.Register();
                AshBeneathEarthQuest.EnsureChainState();

                // Quest 6 continues directly from Quest 5 and repairs characters
                // that completed Q5 before this implementation was installed.
                ThePatternBeneathQuest.Register();
                ThePatternBeneathQuest.EnsureChainState();

                // Quest 7 now goes directly into the first world boss.
                MoldermawQuest.Register();
                MoldermawQuest.EnsureChainState();

                // Quest 8 follows Moldermaw directly and requires Nhal'Sera's
                // Shard of Lightless Glass for turn-in.
                NhalSeraQuest.Register();
                NhalSeraQuest.EnsureChainState();

                // Quest 9 follows Nhal'Sera directly and turns in Varkesh's
                // Soul Fragment to Archivist Sereth.
                VarkeshQuest.Register();
                VarkeshQuest.EnsureChainState();

                GorathQuest.Register();
                GorathQuest.EnsureChainState();

                // Quest 11 is the post-world-boss synthesis beat. It has no item
                // hand-in; the player returns to Sereth and asks about the four
                // wounds revealed by the four recovered relics.
                FourWoundsQuest.Register();
                FourWoundsQuest.EnsureChainState();

                // Quest 12 sends the player back to the Ancient Warding Shrine
                // to inspect hidden inscriptions on all four keeper statues, then
                // returns to Sereth for the story turn-in.
                NamesThatWereBuriedQuest.Register();
                NamesThatWereBuriedQuest.EnsureChainState();

                // Quest 13 is Sereth's deduction that the Blight prison is failing
                // fastest because Moldermaw manifested before the other champions.
                TheWeakestWallQuest.Register();
                TheWeakestWallQuest.EnsureChainState();

                // Quest 14 is the v0.2 finale. Sereth isolates the Blight
                // signature in the old warding fragment and grants the persistent
                // planar-attunement state without opening the Blighted Expanse yet.
                TheFracturingQuest.Register();
                TheFracturingQuest.EnsureChainState();

            }

            CustomItemRegistry.RegisterLazyResolver(
                BlightedSpores.ItemId,
                ResolveBlightedSpores);

            CustomItemRegistry.RegisterLazyResolver(
                VeiledResidue.ItemId,
                ResolveVeiledResidue);

            CustomItemRegistry.RegisterLazyResolver(
                LingeringSoulEssence.ItemId,
                ResolveLingeringSoulEssence);

            CustomItemRegistry.RegisterLazyResolver(
                CinderedRiftShard.ItemId,
                ResolveCinderedRiftShard);

            CustomItemRegistry.RegisterLazyResolver(
                FracturedWardingFragment.ItemId,
                ResolveFracturedWardingFragment);

            CustomItemRegistry.RegisterLazyResolver(
                MoldermawBlightedCore.ItemId,
                ResolveMoldermawBlightedCore);

            CustomItemRegistry.RegisterLazyResolver(
                ShardOfLightlessGlass.ItemId,
                ResolveShardOfLightlessGlass);

            CustomItemRegistry.RegisterLazyResolver(
                VarkeshSoulFragment.ItemId,
                ResolveVarkeshSoulFragment);

            CustomItemRegistry.RegisterLazyResolver(
                GorathInfernalHeart.ItemId,
                ResolveGorathInfernalHeart);

            CustomItemRegistry.RegisterLazyResolver(
                MoldermawEquipment.SporeboundScepterId,
                ResolveMoldermawSporeboundScepter);

            CustomItemRegistry.RegisterLazyResolver(
                MoldermawEquipment.RotcleaverId,
                ResolveMoldermawRotcleaver);

            CustomItemRegistry.RegisterLazyResolver(
                MoldermawEquipment.BulwarkId,
                ResolveMoldermawBulwark);

            CustomItemRegistry.RegisterLazyResolver(
                MoldermawEquipment.BloomId,
                ResolveMoldermawBloom);

            CustomItemRegistry.RegisterLazyResolver(
                MoldermawEquipment.SporepiercerId,
                ResolveMoldermawSporepiercer);

            CustomItemRegistry.RegisterLazyResolver(
                NhalSeraEquipment.LightlessScepterId,
                ResolveNhalSeraLightlessScepter);

            CustomItemRegistry.RegisterLazyResolver(
                NhalSeraEquipment.CarapaceId,
                ResolveNhalSeraCarapace);

            CustomItemRegistry.RegisterLazyResolver(
                NhalSeraEquipment.CrownId,
                ResolveNhalSeraCrown);

            CustomItemRegistry.RegisterLazyResolver(
                NhalSeraEquipment.GraspId,
                ResolveNhalSeraGrasp);

            CustomItemRegistry.RegisterLazyResolver(
                NhalSeraEquipment.GirdleId,
                ResolveNhalSeraGirdle);

            CustomItemRegistry.RegisterLazyResolver(
                VarkeshEquipment.SoulbinderGrimoireId,
                ResolveVarkeshSoulbinderGrimoire);

            CustomItemRegistry.RegisterLazyResolver(
                VarkeshEquipment.GravetideReaverId,
                ResolveVarkeshGravetideReaver);

            CustomItemRegistry.RegisterLazyResolver(
                VarkeshEquipment.HeartOfTheDeathlessId,
                ResolveVarkeshHeartOfTheDeathless);

            CustomItemRegistry.RegisterLazyResolver(
                VarkeshEquipment.LanternOfDepartedSoulsId,
                ResolveVarkeshLanternOfDepartedSouls);

            CustomItemRegistry.RegisterLazyResolver(
                VarkeshEquipment.BandOfTheGravewindId,
                ResolveVarkeshBandOfTheGravewind);

            CustomItemRegistry.RegisterLazyResolver(
                GorathEquipment.EmberOfTheDemonKingId,
                ResolveGorathEmberOfTheDemonKing);

            CustomItemRegistry.RegisterLazyResolver(
                GorathEquipment.HellforgedTalonId,
                ResolveGorathHellforgedTalon);

            CustomItemRegistry.RegisterLazyResolver(
                GorathEquipment.InfernalBastionId,
                ResolveGorathInfernalBastion);

            CustomItemRegistry.RegisterLazyResolver(
                GorathEquipment.AshenCovenantId,
                ResolveGorathAshenCovenant);

            CustomItemRegistry.RegisterLazyResolver(
                GorathEquipment.StormfireId,
                ResolveGorathStormfire);

            CustomItemRegistry.RegisterLazyResolver(
                FallenScepter.ItemId,
                ResolveFallenScepter);

            CustomItemRegistry.RegisterLazyResolver(
                BlightedThrallLoot.TissueItemId,
                ResolveBlightedTissue);

            if (FOTFContentOptions.Active.CustomZones) PreloadZoneBundle();
            if (FOTFContentOptions.Active.CustomRaids) PreloadRaidBundle();

            LogActivationCheckpoint(
                "quests/items/bundle preload",
                activationStarted,
                ref activationCheckpoint);

            MoldermawEquipmentEffects.Initialize();
            FateEquipmentVisuals.Initialize();
            NhalSeraEquipmentEffects.Initialize();
            LateWorldBossEquipmentEffects.Initialize();

            LogActivationCheckpoint(
                "equipment systems",
                activationStarted,
                ref activationCheckpoint);

            if (FOTFContentOptions.Active.CustomZones)
            {
                CustomZoneBootstrap.Initialize();
                ZoneSceneDiagnostic.Initialize();
                CustomZoneLoader.Initialize();
            }
            if (FOTFContentOptions.Active.CustomRaids)
                CustomRaidLoader.Initialize();

            LogActivationCheckpoint(
                "zone/raid loaders",
                activationStarted,
                ref activationCheckpoint);

            if (FOTFContentOptions.Active.Questline)
            {
                ArchivistSerethRuntime.Initialize();
                BlightedSporePlantRuntime.Initialize();
                VeiledDisturbanceRuntime.Initialize();
                DisturbedGraveRuntime.Initialize();

                // Quest 5 now uses the exact same direct module ownership pattern
                // as Quest 4's grave. No Harmony bootstrap required.
                FlameRiftRuntime.Initialize();

                RestlessDeadRuntime.Initialize();

                BlightedThrallRuntime.Initialize();

            }

            LogActivationCheckpoint(
                "quest/NPC/monster runtimes",
                activationStarted,
                ref activationCheckpoint);

            // Keep the proven custom world-boss test runtime intact.
            if (FOTFContentOptions.AnyBoss)
            {
                BlightTouchedColossusVisualTestRuntime.Initialize();
                CustomCreatureRuntime.Initialize();
            }

            // This checkpoint intentionally follows the log emitted by
            // CustomCreatureRuntime.Initialize(). If startup ever stalls after
            // that log again, the next missing [FOTFStartup] checkpoint tells
            // us exactly which group is responsible.
            LogActivationCheckpoint(
                "custom world-boss runtime core",
                activationStarted,
                ref activationCheckpoint);

            // Permanent placement is separate from creature construction.
            if (FOTFContentOptions.SpawnMoldermaw) MoldermawWorldSpawnRuntime.Initialize();
            if (FOTFContentOptions.SpawnNhalSera) NhalSeraWorldSpawnRuntime.Initialize();

            // Do NOT perform a global Resources.LoadAll<GameObject>("") here.
            // That synchronous enumeration was the main-menu freeze observed
            // immediately after CustomCreatureRuntime installed. Late-boss
            // native donors are now resolved only through targeted lookups and
            // scene-local capture when their source content is resident.
            if (FOTFContentOptions.SpawnVarkesh) VarkeshWorldSpawnRuntime.Initialize();
            if (FOTFContentOptions.SpawnGorath) GorathWorldSpawnRuntime.Initialize();

            // r91: learned native donor paths / asynchronous prewarm in stable gameplay
            // scene. Never load all Resources or spawn world bosses at startup.
            if (FOTFContentOptions.SpawnVarkesh || FOTFContentOptions.SpawnGorath)
                WorldBossDonorPrecache.Initialize();

            LogActivationCheckpoint(
                "permanent world-boss runtimes",
                activationStarted,
                ref activationCheckpoint);

            if (FOTFContentOptions.Active.Questline)
            {
                Quest6NativeStatueRuntime.Initialize();
                AncientWardingShrineRuntime.Initialize();
                Quest6ShrineInteractionRuntime.Initialize();

                // Quest 12 reuses the permanent Ancient Warding Shrine. Hidden
                // inscriptions are attached to the outward/rear faces of the four
                // keeper statues once the shrine population is ready.
                Quest12KeeperStatueInscriptionRuntime.Initialize();

            }

            LogActivationCheckpoint(
                "shrine/story runtimes",
                activationStarted,
                ref activationCheckpoint);

            RegisterDatabasesIfReady();

            LogActivationCheckpoint(
                "database registration / activation complete",
                activationStarted,
                ref activationCheckpoint);
        }


        private static void LogActivationCheckpoint(
            string label,
            float activationStarted,
            ref float previousCheckpoint)
        {
            float now =
                Time.realtimeSinceStartup;

            float stepMs =
                (now - previousCheckpoint) * 1000f;

            float totalMs =
                (now - activationStarted) * 1000f;

            previousCheckpoint =
                now;

            Plugin.NativeLog.LogInfo(
                "[FOTFStartup] " +
                (label ?? "<unnamed>") +
                " Step=[" +
                stepMs.ToString("0.0") +
                "ms] Total=[" +
                totalMs.ToString("0.0") +
                "ms].");
        }


        private static void DeactivateExpansionContent()
        {
            if (!_expansionActive)
            {
                return;
            }

            _expansionActive =
                false;

            // Remove active quest encounters/props before shared content.
            if (FOTFContentOptions.Active.Questline)
            {
                RestlessDeadRuntime.Shutdown();
                FlameRiftRuntime.Shutdown();
                DisturbedGraveRuntime.Shutdown();
            }

            if (FOTFContentOptions.Active.Questline)
            {
                VeiledDisturbanceRuntime.Shutdown();
                BlightedSporePlantRuntime.Shutdown();
                ArchivistSerethRuntime.Shutdown();
                BlightedThrallRuntime.Shutdown();
            }

            if (FOTFContentOptions.Active.CustomRaids) CustomRaidLoader.Shutdown();
            if (FOTFContentOptions.Active.CustomZones)
            {
                CustomZoneLoader.Shutdown();
                ZoneSceneDiagnostic.Shutdown();
                CustomZoneBootstrap.Shutdown();
            }

            LateWorldBossEquipmentEffects.Shutdown();
            NhalSeraEquipmentEffects.Shutdown();
            FateEquipmentVisuals.Shutdown();
            MoldermawEquipmentEffects.Shutdown();

            if (FOTFContentOptions.SpawnVarkesh || FOTFContentOptions.SpawnGorath)
                WorldBossDonorPrecache.Shutdown();
            if (FOTFContentOptions.SpawnGorath) GorathWorldSpawnRuntime.Shutdown();
            if (FOTFContentOptions.SpawnVarkesh) VarkeshWorldSpawnRuntime.Shutdown();
            if (FOTFContentOptions.SpawnNhalSera) NhalSeraWorldSpawnRuntime.Shutdown();
            if (FOTFContentOptions.SpawnMoldermaw) MoldermawWorldSpawnRuntime.Shutdown();
            if (FOTFContentOptions.AnyBoss)
            {
                BlightTouchedColossusVisualTestRuntime.Shutdown();
                CustomCreatureRuntime.Shutdown();
            }

            if (FOTFContentOptions.Active.Questline)
            {
                Quest12KeeperStatueInscriptionRuntime.Shutdown();
                Quest6ShrineInteractionRuntime.Shutdown();
                AncientWardingShrineRuntime.Shutdown();
                Quest6NativeStatueRuntime.Shutdown();
            }
        }


        internal static void RegisterItemDatabaseContent(
            ItemDatabase database)
        {
            if (!_expansionActive ||
                !FOTFExpansionGate.IsUnlocked ||
                database == null)
            {
                return;
            }

            if (FOTFContentOptions.Active.Questline)
            {
                FateOfTheFallenQuests.Register();

                AshBeneathEarthQuest.Register();
                AshBeneathEarthQuest.EnsureChainState();

                ThePatternBeneathQuest.Register();
                ThePatternBeneathQuest.EnsureChainState();

                MoldermawQuest.Register();
                MoldermawQuest.EnsureChainState();

                NhalSeraQuest.Register();
                NhalSeraQuest.EnsureChainState();

                VarkeshQuest.Register();
                VarkeshQuest.EnsureChainState();

                GorathQuest.Register();
                GorathQuest.EnsureChainState();

                FourWoundsQuest.Register();
                FourWoundsQuest.EnsureChainState();

                NamesThatWereBuriedQuest.Register();
                NamesThatWereBuriedQuest.EnsureChainState();

                // Quest 13 is Sereth's deduction that the Blight prison is failing
                // fastest because Moldermaw manifested before the other champions.
                TheWeakestWallQuest.Register();
                TheWeakestWallQuest.EnsureChainState();

                // Quest 14 is the v0.2 finale. Sereth isolates the Blight
                // signature in the old warding fragment and grants the persistent
                // planar-attunement state without opening the Blighted Expanse yet.
                TheFracturingQuest.Register();
                TheFracturingQuest.EnsureChainState();

            }

            BlightedSpores.RegisterItem(
                database);

            if (FOTFContentOptions.Active.Questline) SignsOfDecayContent.LinkQuestRequirement();

            VeiledResidue.RegisterItem(
                database);

            if (FOTFContentOptions.Active.Questline) WhereLightFailsContent.LinkQuestRequirement();

            LingeringSoulEssence.RegisterItem(
                database);

            if (FOTFContentOptions.Active.Questline) RestlessDeadContent.LinkQuestRequirement();

            CinderedRiftShard.RegisterItem(
                database);

            if (FOTFContentOptions.Active.Questline) AshBeneathEarthContent.LinkQuestRequirement();

            FracturedWardingFragment.RegisterItem(
                database);

            if (FOTFContentOptions.Active.Questline) ThePatternBeneathContent.LinkQuestRequirement();

            MoldermawQuestContent.RegisterItem(
                database);

            NhalSeraQuestContent.RegisterItem(
                database);

            VarkeshQuestContent.RegisterItem(
                database);

            GorathQuestContent.RegisterItem(
                database);

            MoldermawEquipment.RegisterAll(
                database);

            NhalSeraEquipment.RegisterAll(
                database);

            VarkeshEquipment.RegisterAll(
                database);

            GorathEquipment.RegisterAll(
                database);

            // Emberweaver's initial ItemDB compatibility pass can occur before
            // FOTF world-boss equipment is registered. Run the compatibility
            // pass again now that all FOTF items exist so the actual database
            // objects—not only tooltip-time copies—carry Emberweaver support.
            ArcanistDerivedCompatibility
                .RegisterEquipmentCompatibility(
                    database);

            FallenScepter.RegisterItem(
                database);

            BlightedThrallLoot.RegisterItem(
                database);

            RaidPocBossLoot.RegisterItem(
                database);
        }


        internal static void RegisterSpellDatabaseContent(
            SpellDB database)
        {
            if (!_expansionActive ||
                !FOTFExpansionGate.IsUnlocked ||
                database == null ||
                database.SpellDatabase == null)
            {
                return;
            }

            FallenScepter.RegisterProcSpell(
                database);

            MoldermawEquipmentEffects.RegisterProcSpells(
                database);

            NhalSeraEquipmentEffects.RegisterProcSpells(
                database);
        }


        private static void RegisterDatabasesIfReady()
        {
            try
            {
                if (GameData.ItemDB != null)
                {
                    RegisterItemDatabaseContent(
                        GameData.ItemDB);
                }

                if (GameData.SpellDatabase != null)
                {
                    RegisterSpellDatabaseContent(
                        GameData.SpellDatabase);
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "FateContentModule: failed registering expansion content after gate unlock: " +
                    exception);
            }
        }


        private static void PreloadZoneBundle()
        {
            try
            {
                AssetBundle bundle =
                    AssetBundleRegistry.GetBundle(
                        CustomZoneLoader.FallenReachBundleName);

                if (bundle == null)
                {
                    Plugin.NativeLog.LogError(
                        "FateContentModule: failed to preload zone bundle [" +
                        CustomZoneLoader.FallenReachBundleName +
                        "]. Cold login into Fallen Reach will not work.");

                    return;
                }

                string[] scenePaths =
                    bundle.GetAllScenePaths();

                if (scenePaths == null ||
                    scenePaths.Length == 0)
                {
                    Plugin.NativeLog.LogError(
                        "FateContentModule: zone bundle [" +
                        CustomZoneLoader.FallenReachBundleName +
                        "] loaded successfully but contains no scenes.");
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "FateContentModule: exception while preloading FOTF zone bundle: " +
                    exception);
            }
        }


        private static void PreloadRaidBundle()
        {
            try
            {
                AssetBundle bundle =
                    AssetBundleRegistry.GetBundle(
                        CustomRaidLoader.RaidPocBundleName);

                if (bundle == null)
                {
                    Plugin.NativeLog.LogError(
                        "FateContentModule: failed to preload raid bundle [" +
                        CustomRaidLoader.RaidPocBundleName +
                        "]. /fotfraid will not be available until the bundle is present.");

                    return;
                }

                string[] scenePaths =
                    bundle.GetAllScenePaths();

                if (scenePaths == null ||
                    scenePaths.Length == 0)
                {
                    Plugin.NativeLog.LogError(
                        "FateContentModule: raid bundle [" +
                        CustomRaidLoader.RaidPocBundleName +
                        "] loaded successfully but contains no scenes.");
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "FateContentModule: exception while preloading FOTF raid bundle: " +
                    exception);
            }
        }


        private static Item ResolveBlightedSpores(
            ItemDatabase database)
        {
            Item item =
                BlightedSpores.RegisterItem(
                    database);

            if (FOTFContentOptions.Active.Questline) SignsOfDecayContent.LinkQuestRequirement();

            return item;
        }


        private static Item ResolveVeiledResidue(
            ItemDatabase database)
        {
            Item item =
                VeiledResidue.RegisterItem(
                    database);

            if (FOTFContentOptions.Active.Questline) WhereLightFailsContent.LinkQuestRequirement();

            return item;
        }


        private static Item ResolveLingeringSoulEssence(
            ItemDatabase database)
        {
            Item item =
                LingeringSoulEssence.RegisterItem(
                    database);

            if (FOTFContentOptions.Active.Questline) RestlessDeadContent.LinkQuestRequirement();

            return item;
        }


        private static Item ResolveCinderedRiftShard(
            ItemDatabase database)
        {
            Item item =
                CinderedRiftShard.RegisterItem(
                    database);

            if (FOTFContentOptions.Active.Questline) AshBeneathEarthContent.LinkQuestRequirement();

            return item;
        }


        private static Item ResolveFracturedWardingFragment(
            ItemDatabase database)
        {
            Item item =
                FracturedWardingFragment.RegisterItem(
                    database);

            if (FOTFContentOptions.Active.Questline) ThePatternBeneathContent.LinkQuestRequirement();

            return item;
        }


        private static Item ResolveMoldermawBlightedCore(
            ItemDatabase database)
        {
            return
                MoldermawBlightedCore.RegisterItem(
                    database);
        }


        private static Item ResolveMoldermawSporeboundScepter(
            ItemDatabase database)
        {
            return
                MoldermawEquipment.RegisterSporeboundScepter(
                    database);
        }


        private static Item ResolveMoldermawRotcleaver(
            ItemDatabase database)
        {
            return
                MoldermawEquipment.RegisterRotcleaver(
                    database);
        }


        private static Item ResolveMoldermawBulwark(
            ItemDatabase database)
        {
            return
                MoldermawEquipment.RegisterBulwark(
                    database);
        }


        private static Item ResolveMoldermawBloom(
            ItemDatabase database)
        {
            return
                MoldermawEquipment.RegisterBloom(
                    database);
        }


        private static Item ResolveMoldermawSporepiercer(
            ItemDatabase database)
        {
            return
                MoldermawEquipment.RegisterSporepiercer(
                    database);
        }


        private static Item ResolveShardOfLightlessGlass(
            ItemDatabase database)
        {
            Item item =
                ShardOfLightlessGlass.RegisterItem(
                    database);

            NhalSeraQuestContent.LinkQuestRequirement();

            return item;
        }


        private static Item ResolveVarkeshSoulFragment(
            ItemDatabase database)
        {
            Item item =
                VarkeshSoulFragment.RegisterItem(
                    database);

            VarkeshQuestContent.LinkQuestRequirement();

            return item;
        }


        private static Item ResolveGorathInfernalHeart(
            ItemDatabase database)
        {
            Item item =
                GorathInfernalHeart.RegisterItem(
                    database);

            GorathQuestContent.LinkQuestRequirement();

            return item;
        }


        private static Item ResolveNhalSeraLightlessScepter(
            ItemDatabase database)
        {
            return
                NhalSeraEquipment.RegisterLightlessScepter(
                    database);
        }


        private static Item ResolveNhalSeraCarapace(
            ItemDatabase database)
        {
            return
                NhalSeraEquipment.RegisterCarapace(
                    database);
        }


        private static Item ResolveNhalSeraCrown(
            ItemDatabase database)
        {
            return
                NhalSeraEquipment.RegisterCrown(
                    database);
        }


        private static Item ResolveNhalSeraGrasp(
            ItemDatabase database)
        {
            return
                NhalSeraEquipment.RegisterGrasp(
                    database);
        }


        private static Item ResolveNhalSeraGirdle(
            ItemDatabase database)
        {
            return
                NhalSeraEquipment.RegisterGirdle(
                    database);
        }


        private static Item ResolveVarkeshSoulbinderGrimoire(ItemDatabase database)
        {
            return VarkeshEquipment.RegisterSoulbinderGrimoire(database);
        }

        private static Item ResolveVarkeshGravetideReaver(ItemDatabase database)
        {
            return VarkeshEquipment.RegisterGravetideReaver(database);
        }

        private static Item ResolveVarkeshHeartOfTheDeathless(ItemDatabase database)
        {
            return VarkeshEquipment.RegisterHeartOfTheDeathless(database);
        }

        private static Item ResolveVarkeshLanternOfDepartedSouls(ItemDatabase database)
        {
            return VarkeshEquipment.RegisterLanternOfDepartedSouls(database);
        }

        private static Item ResolveVarkeshBandOfTheGravewind(ItemDatabase database)
        {
            return VarkeshEquipment.RegisterBandOfTheGravewind(database);
        }

        private static Item ResolveGorathEmberOfTheDemonKing(ItemDatabase database)
        {
            return GorathEquipment.RegisterEmberOfTheDemonKing(database);
        }

        private static Item ResolveGorathHellforgedTalon(ItemDatabase database)
        {
            return GorathEquipment.RegisterHellforgedTalon(database);
        }

        private static Item ResolveGorathInfernalBastion(ItemDatabase database)
        {
            return GorathEquipment.RegisterInfernalBastion(database);
        }

        private static Item ResolveGorathAshenCovenant(ItemDatabase database)
        {
            return GorathEquipment.RegisterAshenCovenant(database);
        }

        private static Item ResolveGorathStormfire(ItemDatabase database)
        {
            return GorathEquipment.RegisterStormfire(database);
        }


        private static Item ResolveFallenScepter(
            ItemDatabase database)
        {
            return
                FallenScepter.RegisterItem(
                    database);
        }


        private static Item ResolveBlightedTissue(
            ItemDatabase database)
        {
            return
                BlightedThrallLoot.RegisterItem(
                    database);
        }
    }
}
