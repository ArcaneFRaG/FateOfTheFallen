using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    internal enum FOTFCreatureVisualSource
    {
        AssetBundlePrefab = 0,
        NativeNpcVisual = 1
    }


    /// <summary>
    /// Reusable definition for every FOTF world-boss creature.
    ///
    /// Gameplay is always built by CustomCreatureRuntime. The visual can come
    /// either from a FOTF AssetBundle prefab or from a sanitized visual-only
    /// clone of a native NPC. Native visual donors never contribute Character,
    /// Stats, NPC, aggro, targeting or combat components.
    /// </summary>
    internal sealed class FOTFCreatureDefinition
    {
        internal string Id;
        internal string DisplayName;

        internal FOTFCreatureVisualSource VisualSource;

        internal string BundleName;
        internal string PrefabName;

        internal string NativeVisualNpcName;
        internal string NativeVisualNpcAlternateName;
        internal float NativeVisualScale;
        internal float MinimumVisualHeight;
        internal bool PreserveNativeAnimatorController;

        // When true, native NPC.Update is disabled after its Start lifecycle
        // settles and CustomCreatureCombatProbe owns pursuit/melee damage.
        // This is used by native-visual-donor bosses so native gameplay state
        // cannot leak back in through the donor model.
        internal bool UseCustomMeleeController;

        internal int Level;
        internal int MaxHP;
        internal int BaseAttackDamage;
        internal int BaseArmor;

        internal float AggroRange;
        internal float AttackRange;
        internal float RunSpeed;
        internal float MainHandAttackDelay;

        internal float ColliderRadius;
        internal float ColliderHeight;
        internal Vector3 ColliderCenter;

        internal float NavRadius;
        internal float NavHeight;
        internal float NavSpeed;
        internal float NavAcceleration;
        internal float NavAngularSpeed;
        internal float NavStoppingDistance;

        internal float NameplateHeight;
        internal float TargetRingScale;
    }


    /// <summary>
    /// Shared runtime controller for FOTF bosses that are built from a fresh
    /// runtime component graph.
    ///
    /// Moldermaw and Nhal'Sera use FOTF AssetBundle visuals.
    /// Gorath uses a sanitized visual-only native donor.
    ///
    /// Varkesh deliberately remains on his last proven native Templar runtime
    /// path until a separate migration can be validated without destabilizing
    /// the live encounter.
    ///
    /// In every case the runtime constructs the same fresh Erenshor gameplay
    /// graph while the object is inactive, lets Character/Stats/NPC Start
    /// settle in a controlled order, then attaches the boss-specific encounter
    /// runtime. Native visual donors never own gameplay state.
    ///
    /// Development controls remain routed through WorldBossDevInput.
    /// </summary>
    internal static class CustomCreatureRuntime
    {
        private const string ControllerObjectName =
            "FOTF_CustomCreatureRuntimeController";

        private static CustomCreatureRuntimeController
            _controller;

        internal static readonly FOTFCreatureDefinition
            BlightTouchedColossusTest =
                new FOTFCreatureDefinition
                {
                    Id =
                        "FOTF_CREATURE_BLIGHT_TOUCHED_COLOSSUS_TEST",

                    DisplayName =
                        "Moldermaw",

                    VisualSource =
                        FOTFCreatureVisualSource.AssetBundlePrefab,

                    BundleName =
                        "fotf_worldbosses",

                    PrefabName =
                        "BlightTouchedColossus",

                    Level =
                        WorldBossCatalog.BlightTouchedColossus.Level,

                    MaxHP =
                        WorldBossCatalog.BlightTouchedColossus.MaxHP,

                    BaseAttackDamage =
                        WorldBossCatalog.BlightTouchedColossus.BaseAttackDamage,

                    BaseArmor =
                        300,

                    AggroRange =
                        22f,

                    AttackRange =
                        4.25f,

                    RunSpeed =
                        5.25f,

                    // Native values are "60ths" style in this game.
                    // 120f was the proof baseline; divide by the authoritative
                    // 1.75x world-boss attack-speed multiplier.
                    MainHandAttackDelay =
                        120f /
                        WorldBossCatalog.BlightTouchedColossus.AttackSpeedMultiplier,

                    ColliderRadius =
                        2.0f,

                    ColliderHeight =
                        9.4f,

                    ColliderCenter =
                        new Vector3(
                            0f,
                            4.70f,
                            0f),

                    NavRadius =
                        1.65f,

                    NavHeight =
                        9.0f,

                    NavSpeed =
                        5.25f,

                    NavAcceleration =
                        24f,

                    NavAngularSpeed =
                        180f,

                    NavStoppingDistance =
                        3.5f,

                    NameplateHeight =
                        10.3f,

                    TargetRingScale =
                        2.7f
                };


        internal static readonly FOTFCreatureDefinition
            NhalSeraTest =
                new FOTFCreatureDefinition
                {
                    Id =
                        "FOTF_CREATURE_NHALSERA_TEST",

                    DisplayName =
                        "Nhal'Sera",

                    VisualSource =
                        FOTFCreatureVisualSource.AssetBundlePrefab,

                    BundleName =
                        "fotf_worldbosses",

                    PrefabName =
                        "NhalSera",

                    Level =
                        WorldBossCatalog.NhalSera.Level,

                    MaxHP =
                        WorldBossCatalog.NhalSera.MaxHP,

                    BaseAttackDamage =
                        WorldBossCatalog.NhalSera.BaseAttackDamage,

                    // Temporary validation baseline. Tune these only after
                    // the prefab, Humanoid avatar and native animations have
                    // been verified in-game.
                    BaseArmor =
                        300,

                    AggroRange =
                        22f,

                    AttackRange =
                        4.25f,

                    RunSpeed =
                        5.25f,

                    MainHandAttackDelay =
                        120f /
                        WorldBossCatalog.NhalSera.AttackSpeedMultiplier,

                    ColliderRadius =
                        2.0f,

                    ColliderHeight =
                        9.4f,

                    ColliderCenter =
                        new Vector3(
                            0f,
                            4.70f,
                            0f),

                    NavRadius =
                        1.65f,

                    NavHeight =
                        9.0f,

                    NavSpeed =
                        5.25f,

                    NavAcceleration =
                        24f,

                    NavAngularSpeed =
                        180f,

                    NavStoppingDistance =
                        3.5f,

                    NameplateHeight =
                        10.3f,

                    TargetRingScale =
                        2.7f
                };



        internal static readonly FOTFCreatureDefinition
            GorathDefinition =
                new FOTFCreatureDefinition
                {
                    Id =
                        "FOTF_CREATURE_GORATH",

                    DisplayName =
                        WorldBossCatalog.Gorath.DisplayName,

                    VisualSource =
                        FOTFCreatureVisualSource.NativeNpcVisual,

                    NativeVisualNpcName =
                        GorathWorldSpawnRuntime.NativeTemplateNpcName,

                    NativeVisualNpcAlternateName =
                        "Apparition of Sivakaya",

                    NativeVisualScale =
                        WorldBossCatalog.Gorath.ScaleMultiplier,

                    MinimumVisualHeight =
                        6.0f,

                    PreserveNativeAnimatorController =
                        true,

                    UseCustomMeleeController =
                        true,

                    Level =
                        WorldBossCatalog.Gorath.Level,

                    MaxHP =
                        WorldBossCatalog.Gorath.MaxHP,

                    BaseAttackDamage =
                        WorldBossCatalog.Gorath.BaseAttackDamage,

                    BaseArmor =
                        300,

                    AggroRange =
                        GorathEncounterRuntime.NativeAggroDetectionRange,

                    AttackRange =
                        4.25f,

                    RunSpeed =
                        5.25f,

                    MainHandAttackDelay =
                        120f /
                        WorldBossCatalog.Gorath.AttackSpeedMultiplier,

                    ColliderRadius =
                        1.25f,

                    ColliderHeight =
                        6.2f,

                    ColliderCenter =
                        new Vector3(
                            0f,
                            3.1f,
                            0f),

                    NavRadius =
                        1.0f,

                    NavHeight =
                        6.0f,

                    NavSpeed =
                        5.25f,

                    NavAcceleration =
                        24f,

                    NavAngularSpeed =
                        180f,

                    NavStoppingDistance =
                        3.3f,

                    NameplateHeight =
                        6.9f,

                    TargetRingScale =
                        2.25f
                };


        internal static NPC CurrentNpc
        {
            get
            {
                return
                    _controller != null
                        ? _controller.CurrentNpc
                        : null;
            }
        }


        internal static GameObject CurrentObject
        {
            get
            {
                return
                    _controller != null
                        ? _controller.CurrentObject
                        : null;
            }
        }


        internal static void Initialize()
        {
            if (_controller != null)
            {
                return;
            }

            GameObject controllerObject =
                new GameObject(
                    ControllerObjectName);

            UnityEngine.Object.DontDestroyOnLoad(
                controllerObject);

            _controller =
                controllerObject.AddComponent<
                    CustomCreatureRuntimeController>();

            Plugin.NativeLog.LogInfo(
                "[CustomCreature] shared world-boss runtime installed. " +
                "Moldermaw, Nhal'Sera and Gorath use the runtime-built gameplay-component path; Varkesh remains on the proven native Templar runtime.");
        }


        internal static void Shutdown()
        {
            if (_controller == null)
            {
                NativeWorldBossVisualRegistry.Shutdown();
                return;
            }

            _controller.Shutdown();

            if (_controller.gameObject != null)
            {
                UnityEngine.Object.Destroy(
                    _controller.gameObject);
            }

            _controller =
                null;

            NativeWorldBossVisualRegistry.Shutdown();
        }


        internal static void SpawnDevelopmentColossus()
        {
            if (_controller == null)
            {
                Initialize();
            }

            if (_controller != null)
            {
                _controller.SpawnNearPlayer(
                    BlightTouchedColossusTest);
            }
        }


        internal static void SpawnDevelopmentNhalSera()
        {
            if (_controller == null)
            {
                Initialize();
            }

            if (_controller != null)
            {
                _controller.SpawnNearPlayer(
                    NhalSeraTest);
            }
        }


        internal static void SpawnMoldermawAt(
            Scene scene,
            Vector3 position,
            Quaternion rotation)
        {
            if (_controller == null)
            {
                Initialize();
            }

            if (_controller != null)
            {
                _controller.SpawnAtWorldPosition(
                    BlightTouchedColossusTest,
                    scene,
                    position,
                    rotation);
            }
        }


        internal static void SpawnNhalSeraAt(
            Scene scene,
            Vector3 position,
            Quaternion rotation)
        {
            if (_controller == null)
            {
                Initialize();
            }

            if (_controller != null)
            {
                _controller.SpawnAtWorldPosition(
                    NhalSeraTest,
                    scene,
                    position,
                    rotation);
            }
        }


        internal static void SpawnGorathAt(
            Scene scene,
            Vector3 position,
            Quaternion rotation)
        {
            if (_controller == null)
            {
                Initialize();
            }

            if (_controller != null)
            {
                _controller.SpawnAtWorldPosition(
                    GorathDefinition,
                    scene,
                    position,
                    rotation);
            }
        }


        internal static void DestroyCurrent()
        {
            if (_controller != null)
            {
                _controller.DestroyCurrent();
            }
        }
    }


    internal sealed class CustomCreatureRuntimeController :
        MonoBehaviour
    {
        private const float DevelopmentSpawnDistance =
            18f;

        private const float NavMeshSampleRadius =
            10f;

        private GameObject
            _currentObject;

        private NPC
            _currentNpc;

        private int
            _spawnGeneration;


        internal GameObject CurrentObject
        {
            get
            {
                return
                    _currentObject;
            }
        }


        internal NPC CurrentNpc
        {
            get
            {
                return
                    _currentNpc;
            }
        }


        private void Update()
        {
            // F10/F11 are owned by BlightTouchedColossusVisualTestController
            // (WorldBossDevInput). Keep only the local F8 destroy shortcut
            // here so there is a single authoritative spawn input path.
            if (Input.GetKeyDown(
                    KeyCode.F8))
            {
                DestroyCurrent();
            }
        }


        internal void SpawnNearPlayer(
            FOTFCreatureDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            Plugin.NativeLog.LogInfo(
                "[CustomCreature] SpawnNearPlayer request accepted.");

            PlayerControl playerControl =
                GameData.PlayerControl;

            Character player =
                playerControl != null
                    ? playerControl.Myself
                    : null;

            if (player == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[CustomCreature] player is not available yet.");

                return;
            }

            Scene scene =
                SceneManager.GetActiveScene();

            if (!scene.IsValid() ||
                !scene.isLoaded ||
                CustomRaidLoader.IsRaidPocScene(
                    scene))
            {
                Plugin.NativeLog.LogWarning(
                    "[CustomCreature] development creature must be spawned in a normal loaded Erenshor world scene.");

                return;
            }

            Vector3 requested =
                player.transform.position +
                (player.transform.forward *
                 DevelopmentSpawnDistance);

            Vector3 spawnPosition =
                ResolveSafeNavMeshPosition(
                    requested);

            Vector3 facing =
                player.transform.position -
                spawnPosition;

            facing.y =
                0f;

            Quaternion rotation =
                facing.sqrMagnitude >
                0.001f
                    ? Quaternion.LookRotation(
                        facing.normalized,
                        Vector3.up)
                    : Quaternion.identity;

            // Destroy the previous development creature before assigning the
            // generation token for the new spawn. DestroyCurrent() intentionally
            // invalidates any older in-flight spawn coroutine.
            DestroyCurrent();

            _spawnGeneration++;

            int generation =
                _spawnGeneration;

            Plugin.NativeLog.LogInfo(
                "[CustomCreature] starting spawn coroutine. Generation=[" +
                generation +
                "] Position=[" +
                spawnPosition +
                "].");

            CoroutineHost.Start(
                SpawnRoutine(
                    definition,
                    scene,
                    spawnPosition,
                    rotation,
                    generation));
        }


        internal void SpawnAtWorldPosition(
            FOTFCreatureDefinition definition,
            Scene scene,
            Vector3 requestedPosition,
            Quaternion rotation)
        {
            if (definition == null ||
                !scene.IsValid() ||
                !scene.isLoaded ||
                CustomRaidLoader.IsRaidPocScene(
                    scene))
            {
                return;
            }

            // Identical positioning rule to the proven F10 test spawn.
            Vector3 spawnPosition =
                ResolveSafeNavMeshPosition(
                    requestedPosition);

            // Identical generation ordering to the proven F10 test spawn.
            DestroyCurrent();

            _spawnGeneration++;

            int generation =
                _spawnGeneration;

            Plugin.NativeLog.LogInfo(
                "[CustomCreature] starting fixed-position [" +
                definition.DisplayName +
                "] spawn. Generation=[" +
                generation +
                "] Requested=[" +
                requestedPosition +
                "] Resolved=[" +
                spawnPosition +
                "].");

            CoroutineHost.Start(
                SpawnRoutine(
                    definition,
                    scene,
                    spawnPosition,
                    rotation,
                    generation));
        }


        private static GameObject CreateInactiveCreatureVisual(
            FOTFCreatureDefinition definition,
            Scene scene,
            Vector3 position,
            Quaternion rotation)
        {
            if (definition == null)
            {
                return null;
            }

            if (definition.VisualSource ==
                FOTFCreatureVisualSource.NativeNpcVisual)
            {
                GameObject nativeVisual =
                    NativeWorldBossVisualRegistry.CreateVisualRoot(
                        definition,
                        scene,
                        position,
                        rotation);

                if (nativeVisual == null)
                {
                    Plugin.NativeLog.LogError(
                        "[CustomCreature] native visual source could not be created for [" +
                        definition.DisplayName +
                        "] NativeNpc=[" +
                        (definition.NativeVisualNpcName ?? "<null>") +
                        "].");

                    return null;
                }

                // Erenshor target-frame code can fall back to
                // GameObject.name, so the gameplay root must always use the
                // actual NPC display name rather than an internal FOTF label.
                nativeVisual.name =
                    definition.DisplayName;

                nativeVisual.SetActive(
                    false);

                return nativeVisual;
            }

            GameObject prefab =
                AssetBundleRegistry.GetPrefab(
                    definition.BundleName,
                    definition.PrefabName);

            if (prefab == null)
            {
                Plugin.NativeLog.LogError(
                    "[CustomCreature] prefab [" +
                    definition.PrefabName +
                    "] was not found in bundle [" +
                    definition.BundleName +
                    "]. Expected bundle path=[" +
                    AssetBundleRegistry.GetBundlePath(
                        definition.BundleName) +
                    "].");

                return null;
            }

            GameObject creature =
                null;

            try
            {
                creature =
                    UnityEngine.Object.Instantiate(
                        prefab,
                        position,
                        rotation);

                if (creature == null)
                {
                    Plugin.NativeLog.LogError(
                        "[CustomCreature] Unity returned null while instantiating [" +
                        definition.PrefabName +
                        "].");

                    return null;
                }

                // Keep GameObject.name aligned with Stats.MyName/NPCName.
                // Native target UI uses this as a fallback identity.
                creature.name =
                    definition.DisplayName;

                SceneManager.MoveGameObjectToScene(
                    creature,
                    scene);

                creature.SetActive(
                    false);

                return creature;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[CustomCreature] failed instantiating visual root for [" +
                    definition.DisplayName +
                    "]: " +
                    exception);

                if (creature != null)
                {
                    UnityEngine.Object.Destroy(
                        creature);
                }

                return null;
            }
        }


        private IEnumerator SpawnRoutine(
            FOTFCreatureDefinition definition,
            Scene scene,
            Vector3 position,
            Quaternion rotation,
            int generation)
        {
            Plugin.NativeLog.LogInfo(
                "[CustomCreature] SpawnRoutine ENTER. Generation=[" +
                generation +
                "] CurrentGeneration=[" +
                _spawnGeneration +
                "].");

            GameObject creature =
                CreateInactiveCreatureVisual(
                    definition,
                    scene,
                    position,
                    rotation);

            if (creature == null)
            {
                yield break;
            }

            if (generation !=
                    _spawnGeneration ||
                !scene.IsValid() ||
                !scene.isLoaded)
            {
                Plugin.NativeLog.LogWarning(
                    "[CustomCreature] spawn cancelled after instantiate. RequestedGeneration=[" +
                    generation +
                    "] CurrentGeneration=[" +
                    _spawnGeneration +
                    "] SceneValid=[" +
                    scene.IsValid() +
                    "] SceneLoaded=[" +
                    scene.isLoaded +
                    "].");

                UnityEngine.Object.Destroy(
                    creature);

                yield break;
            }

            CustomCreatureBuildResult build;

            try
            {
                build =
                    BuildNativeCreatureComponents(
                        creature,
                        definition);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[CustomCreature] component construction failed: " +
                    exception);

                UnityEngine.Object.Destroy(
                    creature);

                yield break;
            }

            if (!build.IsValid)
            {
                Plugin.NativeLog.LogError(
                    "[CustomCreature] required component construction did not complete.");

                UnityEngine.Object.Destroy(
                    creature);

                yield break;
            }

            creature.transform.SetPositionAndRotation(
                position,
                rotation);

            // Gameplay root always stays at scale 1. AssetBundle prefabs are
            // authored at world scale; native donor visuals are scaled only on
            // their visual child by NativeWorldBossVisualRegistry.
            creature.transform.localScale =
                Vector3.one;

            // Put root click detection on Erenshor's normal character layer.
            creature.layer =
                9;

            _currentObject =
                creature;

            _currentNpc =
                build.Npc;

            Plugin.NativeLog.LogInfo(
                "[CustomCreature] component graph prepared while inactive. " +
                DescribeBuild(
                    build));

            // Activate Character/Stats/etc. NPC remains disabled for two frames
            // so Erenshor's basic Character and Stats Start passes can settle
            // before the combat-AI Update loop begins.
            build.Npc.enabled =
                false;

            creature.SetActive(
                true);

            // AssetBundle creatures need the captured native humanoid
            // controller. Native visual donors keep their own authored
            // controller when one exists; only fall back to the bridge if the
            // visual donor did not expose a usable controller.
            bool nativeControllerReady =
                definition.PreserveNativeAnimatorController &&
                build.Animator != null &&
                build.Animator.runtimeAnimatorController != null;

            if (nativeControllerReady)
            {
                try
                {
                    build.Animator.enabled =
                        true;

                    build.Animator.applyRootMotion =
                        false;

                    build.Character.AssignAnim(
                        build.Animator);

                    build.Animator.Rebind();
                    build.Animator.Update(
                        0f);

                    Plugin.NativeLog.LogInfo(
                        "[CustomCreature] preserving authored native Animator controller for [" +
                        definition.DisplayName +
                        "] Controller=[" +
                        build.Animator.runtimeAnimatorController.name +
                        "].");
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogWarning(
                        "[CustomCreature] preserving native Animator controller failed for [" +
                        definition.DisplayName +
                        "]: " +
                        exception.Message);

                    nativeControllerReady =
                        false;
                }
            }

            if (!nativeControllerReady &&
                !NativeCreatureAnimationBridge.TryCaptureAndApply(
                    creature))
            {
                NativeCreatureAnimationProbe animationProbe =
                    creature.GetComponent<
                        NativeCreatureAnimationProbe>();

                if (animationProbe == null)
                {
                    creature.AddComponent<
                        NativeCreatureAnimationProbe>();
                }
            }

            yield return null;
            yield return null;

            if (generation !=
                    _spawnGeneration ||
                creature == null)
            {
                yield break;
            }

            // Reassert values after Character/Stats Start/CalcStats.
            ApplyDefinitionAfterNativeStart(
                build,
                definition,
                position);

            if (definition.UseCustomMeleeController)
            {
                // Gorath's unified controller owns all pursuit and melee state.
                // His runtime-built NPC exists only as Erenshor targeting/threat
                // data, so running native NPC.Start/Update on a blank gameplay
                // root adds no value and touches serialized-only fields that do
                // not exist. Keep it disabled for its entire lifetime.
                build.Npc.enabled =
                    false;

                // Still give the Animator/skinned renderer two settling frames
                // before fitting bounds and attaching encounter mechanics.
                yield return null;
                yield return null;

                if (generation !=
                        _spawnGeneration ||
                    creature == null)
                {
                    yield break;
                }

                ApplyDefinitionAfterNativeStart(
                    build,
                    definition,
                    position);

                Plugin.NativeLog.LogInfo(
                    "[CustomCreature] unified combat controller owns pursuit/melee for [" +
                    definition.DisplayName +
                    "]. Native NPC.Start/Update bypassed; NPC component remains disabled as targeting/threat state only.");
            }
            else
            {
                build.Npc.enabled =
                    true;

                // Native-AI bosses still use their normal NPC.Start path.
                yield return null;
                yield return null;

                if (generation !=
                        _spawnGeneration ||
                    creature == null)
                {
                    yield break;
                }

                ApplyDefinitionAfterNativeStart(
                    build,
                    definition,
                    position);
            }

            // Native donor renderer bounds are now settled. Perform the visual
            // world-height correction once after the gameplay root and renderer
            // have completed their controlled settle window.
            ApplyMinimumNativeVisualHeight(
                build,
                definition);

            FitNativeVisualClickCapsule(
                build,
                definition);

            NormalizeNativeDonorAudioHierarchy(
                build.Root,
                definition);

            CustomCreatureCombatProbe probe =
                creature.GetComponent<
                    CustomCreatureCombatProbe>();

            if (probe == null)
            {
                probe =
                    creature.AddComponent<
                        CustomCreatureCombatProbe>();
            }

            probe.Initialize(
                definition,
                build.Npc,
                build.Character,
                build.Stats,
                build.Agent);

            bool isMoldermaw =
                string.Equals(
                    definition.Id,
                    CustomCreatureRuntime.BlightTouchedColossusTest.Id,
                    StringComparison.Ordinal);

            bool isNhalSera =
                string.Equals(
                    definition.Id,
                    CustomCreatureRuntime.NhalSeraTest.Id,
                    StringComparison.Ordinal);


            bool isGorath =
                string.Equals(
                    definition.Id,
                    CustomCreatureRuntime.GorathDefinition.Id,
                    StringComparison.Ordinal);

            if (isMoldermaw)
            {
                WorldBossRuntime.RegisterCustomDevelopmentBoss(
                    build.Npc,
                    position);

                WorldBossEncounterRuntime.AttachBoss(
                    build.Npc,
                    position);

                Plugin.NativeLog.LogInfo(
                    "[CustomCreature] Moldermaw encounter attached. Roar=[" +
                    WorldBossEncounterRuntime.RepeatCastSeconds.ToString("0") +
                    "s] Phase2=[" +
                    (WorldBossEncounterRuntime.PhaseTwoThreshold * 100f).ToString("0") +
                    "%] Phase3=[" +
                    (WorldBossEncounterRuntime.PhaseThreeThreshold * 100f).ToString("0") +
                    "%].");
            }
            else if (isNhalSera)
            {
                NhalSeraEncounterRuntime.AttachBoss(
                    build.Npc,
                    position);

                Plugin.NativeLog.LogInfo(
                    "[CustomCreature] Nhal'Sera encounter attached. Phase2=[75%] Phase3=[60%] Phase4=[30%] Mechanics=[6].");
            }
            else if (isGorath)
            {
                GorathEncounterRuntime.AttachBoss(
                    build.Npc,
                    position);

                Plugin.NativeLog.LogInfo(
                    "[CustomCreature] Gorath encounter attached through unified runtime.");
            }
            else
            {
                Plugin.NativeLog.LogInfo(
                    "[CustomCreature] [" +
                    definition.DisplayName +
                    "] spawned without a dedicated encounter runtime.");
            }

            Plugin.NativeLog.LogInfo(
                "[CustomCreature] REAL CUSTOM CREATURE READY [" +
                definition.DisplayName +
                "] Level=[" +
                build.Stats.Level +
                "] HP=[" +
                build.Stats.CurrentHP +
                "/" +
                build.Stats.CurrentMaxHP +
                "] BaseAttack=[" +
                build.Npc.BaseAtkDmg +
                "] BaseAC=[" +
                build.Stats.BaseAC +
                "] MHDelay=[" +
                build.Stats.BaseMHAtkDelay.ToString("0.0") +
                "] DamageRange=[" +
                build.Npc.DamageRange +
                "] AggroRange=[" +
                build.Character.AggroRange.ToString("0.0") +
                "] AttackRange=[" +
                build.Character.AttackRange.ToString("0.0") +
                "] Faction=[" +
                build.Character.MyFaction +
                "] OnNavMesh=[" +
                (build.Agent != null
                    ? build.Agent.isOnNavMesh.ToString()
                    : "<no agent>") +
                "] Avatar=[" +
                (build.Animator != null &&
                 build.Animator.avatar != null
                    ? build.Animator.avatar.name
                    : "<none>") +
                "].");
        }


        internal void DestroyCurrent()
        {
            _spawnGeneration++;

            NPC previousNpc =
                _currentNpc;

            if (previousNpc != null)
            {
                WorldBossEncounterRuntime.Stop(
                    "custom development boss destroyed");

                NhalSeraEncounterRuntime.Stop(
                    "custom development boss destroyed");

                GorathEncounterRuntime.Stop(
                    "custom development boss destroyed");

                WorldBossRuntime.UnregisterCustomDevelopmentBoss(
                    previousNpc);
            }

            _currentNpc =
                null;

            if (_currentObject != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(
                        _currentObject);
                }
                catch
                {
                }

                _currentObject =
                    null;

                Plugin.NativeLog.LogInfo(
                    "[CustomCreature] current development creature destroyed.");
            }
        }


        internal void Shutdown()
        {
            DestroyCurrent();
        }


        // ============================================================
        // ACTUAL CUSTOM CREATURE CONSTRUCTION
        // ============================================================

        private static CustomCreatureBuildResult
            BuildNativeCreatureComponents(
                GameObject creature,
                FOTFCreatureDefinition definition)
        {
            CustomCreatureBuildResult result =
                new CustomCreatureBuildResult();

            result.Root =
                creature;

            result.Animator =
                creature.GetComponent<Animator>();

            if (result.Animator == null)
            {
                result.Animator =
                    creature.GetComponentInChildren<Animator>(
                        true);
            }

            if (result.Animator == null ||
                result.Animator.avatar == null ||
                !result.Animator.avatar.isValid ||
                !result.Animator.avatar.isHuman)
            {
                Plugin.NativeLog.LogError(
                    "[CustomCreature] custom prefab does not contain a valid Humanoid Animator/Avatar.");

                return result;
            }

            // Native support components first.
            result.Audio =
                GetOrAddComponent<AudioSource>(
                    creature);

            ConfigureCreatureAudio(
                result.Audio,
                definition);

            result.Inventory =
                GetOrAddComponent<Inventory>(
                    creature);

            result.UseSkill =
                GetOrAddComponent<UseSkill>(
                    creature);

            result.CastSpell =
                GetOrAddComponent<CastSpell>(
                    creature);

            result.Agent =
                GetOrAddComponent<NavMeshAgent>(
                    creature);

            result.Capsule =
                GetOrAddComponent<CapsuleCollider>(
                    creature);

            // Core creature components.
            result.Character =
                GetOrAddComponent<Character>(
                    creature);

            result.Stats =
                GetOrAddComponent<Stats>(
                    creature);

            // NPC is intentionally added last. Unity may auto-add native
            // RequireComponent dependencies here, including LootTable.
            result.Npc =
                GetOrAddComponent<NPC>(
                    creature);

            result.LootTable =
                creature.GetComponent<LootTable>();

            // Defensive collection initialization before activation. Native
            // prefabs would normally receive these through serialization /
            // component initialization; a runtime-built creature does not.
            InitializeNullLists(
                result.Inventory);

            InitializeNullLists(
                result.UseSkill);

            InitializeNullLists(
                result.CastSpell);

            InitializeNullLists(
                result.Character);

            InitializeNullLists(
                result.Stats);

            InitializeNullLists(
                result.Npc);

            InitializeNullLists(
                result.LootTable);

            ConfigureCollider(
                result.Capsule,
                definition);

            ConfigureNavAgent(
                result.Agent,
                definition);

            ConfigureTargetRing(
                result,
                definition);

            ConfigureAggroObject(
                result,
                definition);

            ConfigureNameplate(
                result,
                definition);

            ConfigureCharacter(
                result,
                definition);

            ConfigureStatsBeforeStart(
                result,
                definition);

            ConfigureNpcBeforeStart(
                result,
                definition);

            ConfigureRuntimeLootTableBeforeStart(
                result,
                definition);

            result.IsValid =
                result.Root != null &&
                result.Animator != null &&
                result.Character != null &&
                result.Stats != null &&
                result.Npc != null &&
                result.Agent != null &&
                result.Capsule != null;

            return
                result;
        }


        private static void ConfigureCreatureAudio(
            AudioSource source,
            FOTFCreatureDefinition definition)
        {
            if (source == null ||
                definition == null)
            {
                return;
            }

            // Runtime-added AudioSource defaults are effectively 2D/full
            // volume. That is inappropriate for a large world NPC and made
            // Gorath's animation/combat event sounds overwhelm the entire mix.
            //
            // Native visual donors now use a conservative fully-spatial source.
            // AssetBundle bosses retain their existing proven audio behavior.
            if (definition.VisualSource !=
                FOTFCreatureVisualSource.NativeNpcVisual)
            {
                return;
            }

            source.playOnAwake =
                false;

            source.loop =
                false;

            source.volume =
                0.30f;

            source.spatialBlend =
                1.0f;

            source.dopplerLevel =
                0f;

            source.minDistance =
                2.5f;

            source.maxDistance =
                32f;

            source.rolloffMode =
                AudioRolloffMode.Logarithmic;

            Plugin.NativeLog.LogInfo(
                "[CustomCreature] spatial combat audio normalized for [" +
                definition.DisplayName +
                "] Volume=[" +
                source.volume.ToString("0.00") +
                "] MinDistance=[" +
                source.minDistance.ToString("0.0") +
                "] MaxDistance=[" +
                source.maxDistance.ToString("0.0") +
                "].");
        }


        private static void NormalizeNativeDonorAudioHierarchy(
            GameObject root,
            FOTFCreatureDefinition definition)
        {
            if (root == null ||
                definition == null ||
                definition.VisualSource !=
                    FOTFCreatureVisualSource.NativeNpcVisual)
            {
                return;
            }

            AudioSource[] sources =
                root.GetComponentsInChildren<AudioSource>(
                    true);

            if (sources == null)
            {
                return;
            }

            for (int i = 0;
                 i < sources.Length;
                 i++)
            {
                ConfigureCreatureAudio(
                    sources[i],
                    definition);
            }

            Plugin.NativeLog.LogInfo(
                "[CustomCreature] normalized native-donor AudioSource hierarchy for [" +
                definition.DisplayName +
                "] Sources=[" +
                sources.Length +
                "].");
        }


        private static void ConfigureCharacter(
            CustomCreatureBuildResult build,
            FOTFCreatureDefinition definition)
        {
            Character character =
                build.Character;

            character.Alive =
                true;

            character.isNPC =
                true;

            character.isVendor =
                false;

            character.Invulnerable =
                false;

            character.Relax =
                false;

            character.NoRun =
                false;

            character.DestroyOnDeath =
                false;

            character.ShrinkColliderOnDeath =
                true;

            character.AggroRange =
                definition.AggroRange;

            character.AttackRange =
                definition.AttackRange;

            character.MyStats =
                build.Stats;

            character.MyNPC =
                build.Npc;

            character.MySkills =
                build.UseSkill;

            character.MySpells =
                build.CastSpell;

            character.MyCap =
                build.Capsule;

            // A runtime-added Character has no serialized hostile faction
            // identity. Copy the faction tuple from an existing native
            // hostile NPC so players and SimPlayers recognize this as a
            // legitimate enemy target.
            CopyNativeHostileFaction(
                character);

            if (character.AggressiveTowards == null)
            {
                character.AggressiveTowards =
                    new List<Character.Faction>();
            }

            AddFactionIfMissing(
                character.AggressiveTowards,
                Character.Faction.Player);

            AddFactionIfMissing(
                character.AggressiveTowards,
                Character.Faction.PC);

            if (character.Allies == null)
            {
                character.Allies =
                    new List<Character.Faction>();
            }

            character.Allies.Remove(
                Character.Faction.Player);

            character.Allies.Remove(
                Character.Faction.PC);

            if (character.NearbyFriends == null)
            {
                character.NearbyFriends =
                    new List<Character>();
            }

            if (character.NearbyEnemies == null)
            {
                character.NearbyEnemies =
                    new List<Character>();
            }

            try
            {
                character.AssignAnim(
                    build.Animator);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[CustomCreature] Character.AssignAnim failed: " +
                    exception.Message);
            }
        }


        private static void CopyNativeHostileFaction(
            Character target)
        {
            if (target == null)
            {
                return;
            }

            Character[] characters =
                UnityEngine.Object.FindObjectsOfType<Character>();

            if (characters == null)
            {
                return;
            }

            for (int i = 0;
                 i < characters.Length;
                 i++)
            {
                Character source =
                    characters[i];

                if (source == null ||
                    source == target ||
                    !source.isNPC ||
                    source.MyNPC == null ||
                    source.AggressiveTowards == null)
                {
                    continue;
                }

                bool hostileToPlayer =
                    source.AggressiveTowards.Contains(
                        Character.Faction.Player) ||
                    source.AggressiveTowards.Contains(
                        Character.Faction.PC);

                if (!hostileToPlayer)
                {
                    continue;
                }

                target.MyFaction =
                    source.MyFaction;

                target.BaseFaction =
                    source.BaseFaction;

                target.TempFaction =
                    source.TempFaction;

                Plugin.NativeLog.LogInfo(
                    "[CustomCreature] copied native hostile faction from [" +
                    source.MyNPC.NPCName +
                    "] Faction=[" +
                    target.MyFaction +
                    "] Base=[" +
                    target.BaseFaction +
                    "] Temp=[" +
                    target.TempFaction +
                    "].");

                return;
            }

            Plugin.NativeLog.LogWarning(
                "[CustomCreature] no native hostile Character was available to seed the Colossus faction identity.");
        }


        private static void ConfigureStatsBeforeStart(
            CustomCreatureBuildResult build,
            FOTFCreatureDefinition definition)
        {
            Stats stats =
                build.Stats;

            stats.Myself =
                build.Character;

            stats.MyName =
                definition.DisplayName;

            stats.Level =
                definition.Level;

            stats.BaseHP =
                definition.MaxHP;

            stats.BaseAC =
                definition.BaseArmor;

            stats.BaseMHAtkDelay =
                definition.MainHandAttackDelay;

            stats.RunSpeed =
                definition.RunSpeed;

            stats.OverrideHPforNPC =
                true;
        }


        private static void ConfigureNpcBeforeStart(
            CustomCreatureBuildResult build,
            FOTFCreatureDefinition definition)
        {
            NPC npc =
                build.Npc;

            npc.NPCName =
                definition.DisplayName;

            npc.SimPlayer =
                false;

            npc.GroupEncounter =
                false;

            npc.CurrentAggroTarget =
                null;

            npc.retreat =
                false;

            npc.noClick =
                false;

            npc.BaseAtkDmg =
                definition.BaseAttackDamage;

            npc.DamageRange =
                new Vector2(
                    definition.BaseAttackDamage,
                    definition.BaseAttackDamage);

            npc.DamageMult =
                definition.UseCustomMeleeController
                    ? 0f
                    : 1f;

            npc.OHAtkDmg =
                0;

            // The Colossus is a raid-scale creature. Disable the native
            // level-based NPC damage cap so its configured melee baseline can
            // reach Erenshor's normal downstream mitigation instead of being
            // clamped to ordinary-NPC values.
            npc.NoDmgCap =
                true;

            npc.HandSetResistances =
                false;

            npc.NamePlate =
                build.Nameplate != null
                    ? build.Nameplate.transform
                    : null;

            npc.AggroArea =
                build.AggroObject != null
                    ? build.AggroObject.transform
                    : null;

            TrySetMember(
                npc,
                "NeverAggro",
                false);

            TrySetMember(
                npc,
                "AggroRegardlessOfLevel",
                true);

            TrySetMember(
                npc,
                "AggroRegardlessofLOS",
                false);

            TrySetMember(
                npc,
                "IgnoreLOSForAggro",
                false);

            TrySetMember(
                npc,
                "safePointProtect",
                0f);
        }


        private static void ConfigureRuntimeLootTableBeforeStart(
            CustomCreatureBuildResult build,
            FOTFCreatureDefinition definition)
        {
            if (build == null ||
                build.LootTable == null)
            {
                return;
            }

            LootTable table =
                build.LootTable;

            InitializeNullLists(
                table);

            table.hostChar =
                build.Character;

            // Gorath uses a blank runtime gameplay root. NPC's native
            // RequireComponent chain auto-adds LootTable, but that fresh
            // component has none of the serialized data expected by
            // LootTable.Start(). Gorath's corpse rewards are populated
            // explicitly by GorathQuestContent after death, so native Start is
            // unnecessary and unsafe here.
            if (definition != null &&
                definition.VisualSource ==
                    FOTFCreatureVisualSource.NativeNpcVisual &&
                definition.UseCustomMeleeController)
            {
                table.enabled =
                    false;
            }

            try
            {
                FieldInfo characterLootField =
                    HarmonyLib.AccessTools.Field(
                        typeof(Character),
                        "MyLootTable");

                if (characterLootField != null &&
                    build.Character != null)
                {
                    characterLootField.SetValue(
                        build.Character,
                        table);
                }
            }
            catch
            {
            }
        }


        private static void ApplyDefinitionAfterNativeStart(
            CustomCreatureBuildResult build,
            FOTFCreatureDefinition definition,
            Vector3 spawnPosition)
        {
            if (build == null)
            {
                return;
            }

            build.Root.name =
                definition.DisplayName;

            ConfigureCharacter(
                build,
                definition);

            ConfigureNpcBeforeStart(
                build,
                definition);

            ConfigureStatsBeforeStart(
                build,
                definition);

            bool calcSucceeded =
                false;

            try
            {
                build.Stats.CalcStats();

                calcSucceeded =
                    true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[CustomCreature] Stats.CalcStats reassertion threw for [" +
                    definition.DisplayName +
                    "]: " +
                    exception.Message);
            }

            // The unified controller owns a fixed world-boss HP pool. Never
            // allow a failed/incomplete native CalcStats pass to leave a boss
            // with stale native-template max HP.
            if (!calcSucceeded ||
                build.Stats.CurrentMaxHP <=
                    0)
            {
                build.Stats.CurrentMaxHP =
                    definition.MaxHP;
            }

            build.Stats.CurrentHP =
                build.Stats.CurrentMaxHP;

            build.Character.Alive =
                true;

            if (build.Nameplate != null)
            {
                TextMeshPro text =
                    build.Nameplate.GetComponent<
                        TextMeshPro>();

                if (text != null)
                {
                    text.text =
                        definition.DisplayName;
                }
            }

            RemoveDuplicateNativeNameplates(
                build,
                definition);

            if (build.Agent != null)
            {
                ConfigureNavAgent(
                    build.Agent,
                    definition);

                if (build.Agent.enabled &&
                    !build.Agent.isOnNavMesh)
                {
                    NavMeshHit hit;

                    if (NavMesh.SamplePosition(
                            spawnPosition,
                            out hit,
                            NavMeshSampleRadius,
                            NavMesh.AllAreas))
                    {
                        try
                        {
                            build.Agent.Warp(
                                hit.position);
                        }
                        catch
                        {
                            build.Root.transform.position =
                                hit.position;
                        }
                    }
                }

                if (build.Agent.enabled &&
                    build.Agent.isOnNavMesh)
                {
                    build.Agent.isStopped =
                        false;
                }
            }
        }


        private static void ApplyMinimumNativeVisualHeight(
            CustomCreatureBuildResult build,
            FOTFCreatureDefinition definition)
        {
            if (build == null ||
                build.Root == null ||
                definition == null ||
                definition.VisualSource !=
                    FOTFCreatureVisualSource.NativeNpcVisual ||
                definition.MinimumVisualHeight <=
                    0f)
            {
                return;
            }

            Transform visualRoot =
                NativeWorldBossVisualRegistry.FindVisualRoot(
                    build.Root);

            if (visualRoot == null)
            {
                return;
            }

            Renderer[] renderers =
                visualRoot.GetComponentsInChildren<Renderer>(
                    true);

            bool found =
                false;

            float minY =
                float.PositiveInfinity;

            float maxY =
                float.NegativeInfinity;

            for (int i = 0;
                 renderers != null &&
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null ||
                    (!(renderer is SkinnedMeshRenderer) &&
                     !(renderer is MeshRenderer)))
                {
                    continue;
                }

                Bounds bounds =
                    renderer.bounds;

                if (bounds.size.y <=
                    0.01f)
                {
                    continue;
                }

                found =
                    true;

                minY =
                    Mathf.Min(
                        minY,
                        bounds.min.y);

                maxY =
                    Mathf.Max(
                        maxY,
                        bounds.max.y);
            }

            if (!found)
            {
                return;
            }

            float height =
                maxY -
                minY;

            if (height <=
                    0.01f ||
                height >=
                    definition.MinimumVisualHeight)
            {
                return;
            }

            float correction =
                Mathf.Clamp(
                    definition.MinimumVisualHeight /
                    height,
                    1f,
                    4f);

            visualRoot.localScale *=
                correction;

            Plugin.NativeLog.LogInfo(
                "[CustomCreature] native visual height corrected for [" +
                definition.DisplayName +
                "] PreviousHeight=[" +
                height.ToString("0.00") +
                "] Target=[" +
                definition.MinimumVisualHeight.ToString("0.00") +
                "] ScaleFactor=[" +
                correction.ToString("0.00") +
                "].");
        }


        private static void FitNativeVisualClickCapsule(
            CustomCreatureBuildResult build,
            FOTFCreatureDefinition definition)
        {
            if (build == null ||
                build.Root == null ||
                build.Capsule == null ||
                definition == null ||
                definition.VisualSource !=
                    FOTFCreatureVisualSource.NativeNpcVisual)
            {
                return;
            }

            Transform visualRoot =
                NativeWorldBossVisualRegistry.FindVisualRoot(
                    build.Root);

            if (visualRoot == null)
            {
                return;
            }

            Renderer[] renderers =
                visualRoot.GetComponentsInChildren<Renderer>(
                    true);

            if (renderers == null ||
                renderers.Length == 0)
            {
                return;
            }

            bool found =
                false;

            Bounds localBounds =
                new Bounds();

            Transform rootTransform =
                build.Root.transform;

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null ||
                    (!renderer.enabled &&
                     renderer.gameObject.activeInHierarchy) ||
                    (!(renderer is SkinnedMeshRenderer) &&
                     !(renderer is MeshRenderer)))
                {
                    continue;
                }

                Bounds worldBounds =
                    renderer.bounds;

                if (worldBounds.size.sqrMagnitude <=
                    0.0001f)
                {
                    continue;
                }

                Vector3 center =
                    worldBounds.center;

                Vector3 extents =
                    worldBounds.extents;

                for (int x = -1;
                     x <= 1;
                     x += 2)
                {
                    for (int y = -1;
                         y <= 1;
                         y += 2)
                    {
                        for (int z = -1;
                             z <= 1;
                             z += 2)
                        {
                            Vector3 worldCorner =
                                center +
                                new Vector3(
                                    extents.x * x,
                                    extents.y * y,
                                    extents.z * z);

                            Vector3 localCorner =
                                rootTransform.InverseTransformPoint(
                                    worldCorner);

                            if (!found)
                            {
                                localBounds =
                                    new Bounds(
                                        localCorner,
                                        Vector3.zero);

                                found =
                                    true;
                            }
                            else
                            {
                                localBounds.Encapsulate(
                                    localCorner);
                            }
                        }
                    }
                }
            }

            if (!found)
            {
                return;
            }

            // The previous fixed collider was still sized around the gameplay
            // root while Gorath's native donor visual had been enlarged
            // independently. That left the upper torso/head outside the
            // clickable volume, which is why only clicks below the waist
            // selected him.
            float visibleHeight =
                Mathf.Max(
                    0.1f,
                    localBounds.size.y);

            float fittedHeight =
                Mathf.Max(
                    definition.ColliderHeight,
                    visibleHeight * 1.08f);

            // Keep the original tuned radius as the floor. Expand only enough
            // to comfortably cover the torso; do not use the full arm-span
            // because that would make empty space beside a boss clickable.
            float horizontalBodySize =
                Mathf.Min(
                    Mathf.Max(
                        localBounds.size.x,
                        localBounds.size.z),
                    visibleHeight * 0.55f);

            float fittedRadius =
                Mathf.Max(
                    definition.ColliderRadius,
                    horizontalBodySize * 0.38f);

            fittedRadius =
                Mathf.Min(
                    fittedRadius,
                    fittedHeight * 0.32f);

            Vector3 fittedCenter =
                build.Capsule.center;

            fittedCenter.x =
                localBounds.center.x;

            fittedCenter.y =
                localBounds.center.y;

            fittedCenter.z =
                localBounds.center.z;

            build.Capsule.enabled =
                true;

            build.Capsule.isTrigger =
                false;

            build.Capsule.direction =
                1;

            build.Capsule.center =
                fittedCenter;

            build.Capsule.height =
                Mathf.Max(
                    fittedHeight,
                    fittedRadius * 2f);

            build.Capsule.radius =
                fittedRadius;

            // Character.TargetMe / PlayerControl.LeftClick ultimately rely on
            // the root Character collider being on the native character layer.
            build.Root.layer =
                9;

            build.Character.MyCap =
                build.Capsule;

            Plugin.NativeLog.LogInfo(
                "[CustomCreature] fitted native-donor click capsule for [" +
                definition.DisplayName +
                "] VisualBoundsCenter=[" +
                localBounds.center +
                "] VisualBoundsSize=[" +
                localBounds.size +
                "] CapsuleCenter=[" +
                build.Capsule.center +
                "] CapsuleHeight=[" +
                build.Capsule.height.ToString("0.00") +
                "] CapsuleRadius=[" +
                build.Capsule.radius.ToString("0.00") +
                "].");
        }


        // ============================================================
        // GENERATED SUPPORT OBJECTS
        // ============================================================

        private static void ConfigureCollider(
            CapsuleCollider capsule,
            FOTFCreatureDefinition definition)
        {
            capsule.enabled =
                true;

            capsule.isTrigger =
                false;

            capsule.radius =
                definition.ColliderRadius;

            capsule.height =
                definition.ColliderHeight;

            capsule.center =
                definition.ColliderCenter;

            capsule.direction =
                1;
        }


        private static void ConfigureNavAgent(
            NavMeshAgent agent,
            FOTFCreatureDefinition definition)
        {
            agent.enabled =
                true;

            agent.radius =
                definition.NavRadius;

            agent.height =
                definition.NavHeight;

            agent.speed =
                definition.NavSpeed;

            agent.acceleration =
                definition.NavAcceleration;

            agent.angularSpeed =
                definition.NavAngularSpeed;

            agent.stoppingDistance =
                definition.NavStoppingDistance;

            agent.autoBraking =
                true;

            agent.autoTraverseOffMeshLink =
                true;
        }


        private static void ConfigureAggroObject(
            CustomCreatureBuildResult build,
            FOTFCreatureDefinition definition)
        {
            Transform existing =
                FindDirectChild(
                    build.Root.transform,
                    "FOTF_AggroArea");

            GameObject aggroObject;

            if (existing != null)
            {
                aggroObject =
                    existing.gameObject;
            }
            else
            {
                aggroObject =
                    new GameObject(
                        "FOTF_AggroArea");

                aggroObject.transform.SetParent(
                    build.Root.transform,
                    false);
            }

            aggroObject.transform.localPosition =
                new Vector3(
                    0f,
                    definition.ColliderCenter.y,
                    0f);

            aggroObject.transform.localRotation =
                Quaternion.identity;

            aggroObject.transform.localScale =
                Vector3.one;

            SphereCollider trigger =
                aggroObject.GetComponent<
                    SphereCollider>();

            if (trigger == null)
            {
                trigger =
                    aggroObject.AddComponent<
                        SphereCollider>();
            }

            trigger.isTrigger =
                true;

            trigger.enabled =
                true;

            trigger.radius =
                definition.AggroRange;

            build.AggroObject =
                aggroObject;

            build.Character.MyAggro =
                aggroObject.transform;
        }


        private static void ConfigureNameplate(
            CustomCreatureBuildResult build,
            FOTFCreatureDefinition definition)
        {
            Transform existing =
                FindDirectChild(
                    build.Root.transform,
                    "FOTF_NamePlate");

            GameObject nameplate;

            if (existing != null)
            {
                nameplate =
                    existing.gameObject;
            }
            else
            {
                nameplate =
                    new GameObject(
                        "FOTF_NamePlate");

                nameplate.transform.SetParent(
                    build.Root.transform,
                    false);
            }

            nameplate.transform.localPosition =
                new Vector3(
                    0f,
                    definition.NameplateHeight,
                    0f);

            nameplate.transform.localRotation =
                Quaternion.identity;

            nameplate.transform.localScale =
                Vector3.one;

            TextMeshPro text =
                nameplate.GetComponent<
                    TextMeshPro>();

            if (text == null)
            {
                text =
                    nameplate.AddComponent<
                        TextMeshPro>();
            }

            text.text =
                definition.DisplayName;

            text.fontSize =
                2.2f;

            text.alignment =
                TextAlignmentOptions.Center;

            text.enableWordWrapping =
                false;

            text.color =
                Color.white;

            if (nameplate.GetComponent<
                    CustomCreatureNameplateBillboard>() == null)
            {
                nameplate.AddComponent<
                    CustomCreatureNameplateBillboard>();
            }

            build.Nameplate =
                nameplate;
        }


        private static void ConfigureTargetRing(
            CustomCreatureBuildResult build,
            FOTFCreatureDefinition definition)
        {
            Transform existing =
                FindDirectChild(
                    build.Root.transform,
                    "FOTF_TargetRing");

            GameObject ring;

            if (existing != null)
            {
                ring =
                    existing.gameObject;
            }
            else
            {
                ring =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cylinder);

                ring.name =
                    "FOTF_TargetRing";

                ring.transform.SetParent(
                    build.Root.transform,
                    false);
            }

            Collider ringCollider =
                ring.GetComponent<Collider>();

            if (ringCollider != null)
            {
                UnityEngine.Object.Destroy(
                    ringCollider);
            }

            ring.transform.localPosition =
                new Vector3(
                    0f,
                    0.035f,
                    0f);

            ring.transform.localRotation =
                Quaternion.identity;

            ring.transform.localScale =
                new Vector3(
                    definition.TargetRingScale,
                    0.02f,
                    definition.TargetRingScale);

            Renderer renderer =
                ring.GetComponent<Renderer>();

            if (renderer != null)
            {
                Material material =
                    CreateTargetRingMaterial();

                if (material != null)
                {
                    renderer.sharedMaterial =
                        material;
                }
            }

            ring.SetActive(
                false);

            build.TargetRing =
                ring;

            build.Character.TargetRing =
                ring;
        }


        private static Material CreateTargetRingMaterial()
        {
            Shader shader =
                Shader.Find(
                    "Unlit/Color");

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Sprites/Default");
            }

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Standard");
            }

            if (shader == null)
            {
                return null;
            }

            Material material =
                new Material(
                    shader);

            material.name =
                "FOTF_CustomCreature_TargetRing_Material";

            if (material.HasProperty(
                    "_Color"))
            {
                material.color =
                    new Color(
                        1f,
                        0.82f,
                        0.08f,
                        0.65f);
            }

            return
                material;
        }


        // ============================================================
        // HELPERS
        // ============================================================

        private static T GetOrAddComponent<T>(
            GameObject gameObject)
            where T : Component
        {
            T component =
                gameObject.GetComponent<T>();

            if (component == null)
            {
                component =
                    gameObject.AddComponent<T>();
            }

            return
                component;
        }


        private static Transform FindDirectChild(
            Transform parent,
            string name)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0;
                 i < parent.childCount;
                 i++)
            {
                Transform child =
                    parent.GetChild(
                        i);

                if (child != null &&
                    string.Equals(
                        child.name,
                        name,
                        StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }


        private static void AddFactionIfMissing(
            List<Character.Faction> factions,
            Character.Faction faction)
        {
            if (factions == null ||
                factions.Contains(
                    faction))
            {
                return;
            }

            factions.Add(
                faction);
        }


        /// <summary>
        /// Runtime-created components do not receive serialized List instances
        /// from an Erenshor prefab. Initialize null List<T> fields defensively.
        /// We intentionally do not manufacture arbitrary UnityEngine.Object
        /// references or arrays.
        /// </summary>
        private static void InitializeNullLists(
            object instance)
        {
            if (instance == null)
            {
                return;
            }

            FieldInfo[] fields =
                instance.GetType().GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            for (int i = 0;
                 i < fields.Length;
                 i++)
            {
                FieldInfo field =
                    fields[i];

                if (field == null ||
                    field.IsInitOnly ||
                    !field.FieldType.IsGenericType ||
                    field.FieldType.GetGenericTypeDefinition() !=
                        typeof(List<>))
                {
                    continue;
                }

                try
                {
                    if (field.GetValue(
                            instance) != null)
                    {
                        continue;
                    }

                    object list =
                        Activator.CreateInstance(
                            field.FieldType);

                    field.SetValue(
                        instance,
                        list);
                }
                catch
                {
                }
            }
        }


        private static void TrySetMember(
            object instance,
            string memberName,
            object value)
        {
            if (instance == null ||
                string.IsNullOrEmpty(
                    memberName))
            {
                return;
            }

            Type type =
                instance.GetType();

            try
            {
                FieldInfo field =
                    type.GetField(
                        memberName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                if (field != null)
                {
                    field.SetValue(
                        instance,
                        value);

                    return;
                }

                PropertyInfo property =
                    type.GetProperty(
                        memberName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                if (property != null &&
                    property.CanWrite)
                {
                    property.SetValue(
                        instance,
                        value,
                        null);
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[CustomCreature] could not set optional native member [" +
                    memberName +
                    "]: " +
                    exception.Message);
            }
        }


        private static Vector3 ResolveSafeNavMeshPosition(
            Vector3 requestedPosition)
        {
            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                    requestedPosition,
                    out hit,
                    NavMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                return
                    hit.position;
            }

            Plugin.NativeLog.LogWarning(
                "[CustomCreature] no NavMesh position found near requested spawn point; using requested position.");

            return
                requestedPosition;
        }


        private static void RemoveDuplicateNativeNameplates(
            CustomCreatureBuildResult build,
            FOTFCreatureDefinition definition)
        {
            if (build == null ||
                build.Root == null)
            {
                return;
            }

            TextMeshPro[] labels =
                build.Root.GetComponentsInChildren<
                    TextMeshPro>(
                        true);

            if (labels == null)
            {
                return;
            }

            for (int i = 0;
                 i < labels.Length;
                 i++)
            {
                TextMeshPro label =
                    labels[i];

                if (label == null)
                {
                    continue;
                }

                // Keep exactly our authored FOTF nameplate and anything that
                // belongs beneath it. Every other TMP/nameplate created under
                // the custom creature is native startup residue.
                if (build.Nameplate != null &&
                    (label.gameObject == build.Nameplate ||
                     label.transform == build.Nameplate.transform ||
                     label.transform.IsChildOf(
                         build.Nameplate.transform)))
                {
                    continue;
                }

                GameObject labelObject =
                    label.gameObject;

                string objectName =
                    labelObject != null
                        ? labelObject.name ?? string.Empty
                        : string.Empty;

                string value =
                    label.text ?? string.Empty;

                bool looksLikeNativeNameplate =
                    objectName.IndexOf(
                        "NamePlate",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    objectName.IndexOf(
                        "Nameplate",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf(
                        "corpse",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (!string.IsNullOrEmpty(
                         definition.DisplayName) &&
                     string.Equals(
                         value.Trim(),
                         definition.DisplayName.Trim(),
                         StringComparison.OrdinalIgnoreCase));

                if (!looksLikeNativeNameplate)
                {
                    continue;
                }

                Plugin.NativeLog.LogInfo(
                    "[CustomCreature] removing duplicate native nameplate object=[" +
                    objectName +
                    "] text=[" +
                    value +
                    "].");

                try
                {
                    UnityEngine.Object.Destroy(
                        labelObject);
                }
                catch
                {
                    label.enabled =
                        false;
                }
            }

            // Reassert the NPC reference so any native startup-created
            // NamePlate(Clone) cannot remain authoritative after cleanup.
            if (build.Npc != null)
            {
                build.Npc.NamePlate =
                    build.Nameplate != null
                        ? build.Nameplate.transform
                        : null;
            }
        }


        private static string DescribeBuild(
            CustomCreatureBuildResult build)
        {
            return
                "Animator=" +
                (build.Animator != null) +
                ", Character=" +
                (build.Character != null) +
                ", Stats=" +
                (build.Stats != null) +
                ", NPC=" +
                (build.Npc != null) +
                ", NavMeshAgent=" +
                (build.Agent != null) +
                ", CapsuleCollider=" +
                (build.Capsule != null) +
                ", Inventory=" +
                (build.Inventory != null) +
                ", UseSkill=" +
                (build.UseSkill != null) +
                ", CastSpell=" +
                (build.CastSpell != null) +
                ", NamePlate=" +
                (build.Nameplate != null) +
                ", TargetRing=" +
                (build.TargetRing != null) +
                ", AggroArea=" +
                (build.AggroObject != null) +
                ".";
        }
    }


    internal sealed class CustomCreatureBuildResult
    {
        internal bool IsValid;

        internal GameObject Root;
        internal Animator Animator;
        internal AudioSource Audio;
        internal Inventory Inventory;
        internal UseSkill UseSkill;
        internal CastSpell CastSpell;
        internal NavMeshAgent Agent;
        internal CapsuleCollider Capsule;
        internal Character Character;
        internal Stats Stats;
        internal NPC Npc;
        internal LootTable LootTable;

        internal GameObject Nameplate;
        internal GameObject TargetRing;
        internal GameObject AggroObject;
    }


    /// <summary>
    /// Development combat bridge used only to prove that the runtime-built
    /// custom creature can enter Erenshor's native NPC combat path.
    ///
    /// It does NOT implement pursuit or melee itself. Once the player is in
    /// range it calls NPC.AggroOn(), then native NPC AI owns pursuit, swing
    /// timing, attack animation and hit resolution.
    /// </summary>
    internal sealed class CustomCreatureCombatProbe :
        MonoBehaviour
    {
        private const float ScanInterval =
            0.35f;

        private const float TargetCandidateCacheInterval =
            0.75f;

        private static Character[] _targetCandidateCache =
            new Character[0];

        private static float _nextTargetCandidateCacheRefresh;

        private FOTFCreatureDefinition
            _definition;

        private NPC
            _npc;

        private Character
            _character;

        private Stats
            _stats;

        private NavMeshAgent
            _agent;

        private float
            _nextScanTime;

        private Character
            _lastLoggedTarget;

        private bool
            _loggedDeath;

        private float
            _nextCustomMeleeTime;

        private Animator
            _animator;

        private Character
            _lastCustomMeleeTarget;

        private bool
            _loggedFirstCustomMelee;


        internal void Initialize(
            FOTFCreatureDefinition definition,
            NPC npc,
            Character character,
            Stats stats,
            NavMeshAgent agent)
        {
            _definition =
                definition;

            _npc =
                npc;

            _character =
                character;

            _stats =
                stats;

            _agent =
                agent;

            _nextScanTime =
                Time.time;

            _lastLoggedTarget =
                null;

            _loggedDeath =
                false;

            _nextCustomMeleeTime =
                Time.time;

            _animator =
                character != null
                    ? character.GetMyAnim()
                    : null;

            if (_animator == null)
            {
                _animator =
                    GetComponentInChildren<Animator>(
                        true);
            }

            _lastCustomMeleeTarget =
                null;

            _loggedFirstCustomMelee =
                false;
        }


        private void Awake()
        {
            if (_npc == null)
            {
                _npc =
                    GetComponent<NPC>();
            }

            if (_character == null)
            {
                _character =
                    GetComponent<Character>();
            }

            if (_stats == null)
            {
                _stats =
                    GetComponent<Stats>();
            }

            if (_agent == null)
            {
                _agent =
                    GetComponent<NavMeshAgent>();
            }
        }


        private void Update()
        {
            if (_npc == null ||
                _character == null ||
                _stats == null)
            {
                return;
            }

            bool alive =
                _character.Alive &&
                _stats.CurrentHP >
                    0;

            if (!alive)
            {
                if (!_loggedDeath)
                {
                    _loggedDeath =
                        true;

                    Plugin.NativeLog.LogInfo(
                        "[CustomCreature] custom creature death observed. " +
                        "Alive=[" +
                        _character.Alive +
                        "] HP=[" +
                        _stats.CurrentHP +
                        "].");

                    if (_definition != null &&
                        string.Equals(
                            _definition.Id,
                            CustomCreatureRuntime
                                .BlightTouchedColossusTest
                                .Id,
                            StringComparison.Ordinal))
                    {
                        // Do not alter combat/damage construction. These are
                        // post-death integrations only.
                        WorldBossEncounterRuntime.Stop(
                            "Moldermaw defeated");

                        WorldBossRuntime.UnregisterCustomDevelopmentBoss(
                            _npc);

                        WorldBossLoot.AwardCompletionCredit();

                        // Resolve Moldermaw's custom loot without attaching a
                        // native LootTable component to the runtime-built boss.
                        MoldermawLootTable.ResolveOnDeath(
                            _npc);

                        MoldermawQuest.NotifyMoldermawDefeated(
                            "Moldermaw custom creature death");

                        MoldermawWorldSpawnRuntime.NotifyMoldermawDeath();
                    }
                    else if (_definition != null &&
                             string.Equals(
                                 _definition.Id,
                                 CustomCreatureRuntime
                                     .NhalSeraTest
                                     .Id,
                                 StringComparison.Ordinal))
                    {
                        NhalSeraEncounterRuntime.Stop(
                            "Nhal'Sera defeated");

                        // Arm native corpse loot first so the guaranteed Quest 8
                        // shard and normal equipment rewards are available on
                        // the corpse immediately.
                        NhalSeraLootTable.ResolveOnDeath(
                            _npc);

                        NhalSeraQuest.NotifyNhalSeraDefeated(
                            "Nhal'Sera custom creature death");

                        NhalSeraWorldSpawnRuntime.NotifyNhalSeraDeath();

                        Plugin.NativeLog.LogInfo(
                            "[NhalSera] boss defeated. Quest 8 credit, corpse loot and world respawn resolved.");
                    }
                    else if (_definition != null &&
                             string.Equals(
                                 _definition.Id,
                                 CustomCreatureRuntime
                                     .GorathDefinition
                                     .Id,
                                 StringComparison.Ordinal))
                    {
                        GorathEncounterRuntime.Stop(
                            "Gorath defeated");

                        GorathWorldSpawnRuntime.NotifyGorathDeath(
                            "unified custom runtime death");

                        Plugin.NativeLog.LogInfo(
                            "[Gorath] boss defeated through unified custom runtime.");
                    }
                }

                return;
            }

            if (Time.time <
                _nextScanTime)
            {
                return;
            }

            _nextScanTime =
                Time.time +
                ScanInterval;

            Character target =
                _npc.CurrentAggroTarget;

            if (!IsValidPlayerTarget(
                    target))
            {
                target =
                    ResolvePlayerTarget();

                if (!IsValidPlayerTarget(
                        target))
                {
                    return;
                }

                float distance =
                    Vector3.Distance(
                        transform.position,
                        target.transform.position);

                float range =
                    _definition != null
                        ? _definition.AggroRange
                        : 22f;

                if (distance >
                    range)
                {
                    return;
                }

                bool customOwnedCombat =
                    _definition != null &&
                    _definition.UseCustomMeleeController;

                if (customOwnedCombat)
                {
                    // Do not call native NPC.AggroOn for Gorath. NPC.Start is
                    // intentionally bypassed on this runtime-built blank root,
                    // and the unified controller already owns target selection,
                    // pursuit and melee. Keep only the NPC fields needed by
                    // Erenshor targeting/threat consumers.
                    _npc.CurrentAggroTarget =
                        target;

                    _npc.retreat =
                        false;

                    _character.Relax =
                        false;
                }
                else
                {
                    try
                    {
                        _npc.AggroOn(
                            target);
                    }
                    catch (Exception exception)
                    {
                        Plugin.NativeLog.LogError(
                            "[CustomCreature] native NPC.AggroOn failed: " +
                            exception);

                        return;
                    }
                }
            }

            target =
                _npc.CurrentAggroTarget;

            if (target != null &&
                target !=
                    _lastLoggedTarget)
            {
                _lastLoggedTarget =
                    target;

                bool customOwnedCombat =
                    _definition != null &&
                    _definition.UseCustomMeleeController;

                Plugin.NativeLog.LogInfo(
                    "[CustomCreature] " +
                    (customOwnedCombat
                        ? "custom combat target acquired ["
                        : "native aggro acquired [") +
                    GetCharacterName(
                        target) +
                    "] Distance=[" +
                    Vector3.Distance(
                        transform.position,
                        target.transform.position).ToString("0.0") +
                    "] OnNavMesh=[" +
                    (_agent != null
                        ? _agent.isOnNavMesh.ToString()
                        : "<no agent>") +
                    "]. " +
                    (customOwnedCombat
                        ? "Unified controller owns pursuit/melee."
                        : "Native NPC AI now owns pursuit/melee."));
            }

            if (_definition != null &&
                _definition.UseCustomMeleeController)
            {
                UpdateCustomBossCombat(
                    target);

                return;
            }

            // Safety net for bosses still using native NPC.Update: keep the
            // NavMesh destination synchronized to CurrentAggroTarget.
            if (target != null &&
                _agent != null &&
                _agent.enabled &&
                _agent.isOnNavMesh &&
                !_agent.isStopped)
            {
                try
                {
                    _agent.SetDestination(
                        target.transform.position);
                }
                catch
                {
                }
            }
        }


        private void UpdateCustomBossCombat(
            Character target)
        {
            if (!IsValidPlayerTarget(
                    target) ||
                _definition == null ||
                !_definition.UseCustomMeleeController)
            {
                return;
            }

            _npc.CurrentAggroTarget =
                target;

            _npc.retreat =
                false;

            _character.Relax =
                false;

            Vector3 delta =
                target.transform.position -
                transform.position;

            delta.y =
                0f;

            float distance =
                delta.magnitude;

            float attackRange =
                Mathf.Max(
                    1f,
                    _character.AttackRange > 0f
                        ? _character.AttackRange
                        : _definition.AttackRange);

            if (distance >
                    attackRange &&
                _agent != null &&
                _agent.enabled &&
                _agent.isOnNavMesh &&
                !_agent.isStopped)
            {
                try
                {
                    _agent.SetDestination(
                        target.transform.position);
                }
                catch
                {
                }

                return;
            }

            if (distance >
                    attackRange +
                    0.35f ||
                Time.time <
                    _nextCustomMeleeTime)
            {
                return;
            }

            float delayUnits =
                _stats != null &&
                _stats.BaseMHAtkDelay >
                    0f
                    ? _stats.BaseMHAtkDelay
                    : _definition.MainHandAttackDelay;

            float delaySeconds =
                Mathf.Clamp(
                    delayUnits /
                    60f,
                    0.40f,
                    8f);

            _nextCustomMeleeTime =
                Time.time +
                delaySeconds;

            if (delta.sqrMagnitude >
                0.01f)
            {
                try
                {
                    transform.rotation =
                        Quaternion.LookRotation(
                            delta.normalized,
                            Vector3.up);
                }
                catch
                {
                }
            }

            TriggerCustomMeleeAnimation();

            int rawDamage =
                _npc != null &&
                _npc.BaseAtkDmg >
                    0
                    ? _npc.BaseAtkDmg
                    : _definition.BaseAttackDamage;

            if (rawDamage <=
                0)
            {
                return;
            }

            try
            {
                int resolvedDamage =
                    target.DamageMe(
                        rawDamage,
                        false,
                        GameData.DamageType.Physical,
                        _character,
                        true,
                        false,
                        0);

                if (!_loggedFirstCustomMelee ||
                    _lastCustomMeleeTarget !=
                        target)
                {
                    _loggedFirstCustomMelee =
                        true;

                    _lastCustomMeleeTarget =
                        target;

                    Plugin.NativeLog.LogInfo(
                        "[CustomCreature] controller melee active for [" +
                        _definition.DisplayName +
                        "] Target=[" +
                        GetCharacterName(
                            target) +
                        "] Raw=[" +
                        rawDamage +
                        "] Resolved=[" +
                        resolvedDamage +
                        "] Delay=[" +
                        delaySeconds.ToString("0.00") +
                        "s] Range=[" +
                        attackRange.ToString("0.0") +
                        "].");
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[CustomCreature] controller melee failed for [" +
                    _definition.DisplayName +
                    "]: " +
                    exception);
            }
        }


        private void TriggerCustomMeleeAnimation()
        {
            if (_animator == null)
            {
                _animator =
                    _character != null
                        ? _character.GetMyAnim()
                        : null;

                if (_animator == null)
                {
                    _animator =
                        GetComponentInChildren<Animator>(
                            true);
                }
            }

            if (_animator == null ||
                !_animator.enabled ||
                _animator.runtimeAnimatorController ==
                    null)
            {
                return;
            }

            try
            {
                if (HasAnimatorParameter(
                        "AttackIndex",
                        AnimatorControllerParameterType.Int))
                {
                    _animator.SetInteger(
                        "AttackIndex",
                        0);
                }

                if (HasAnimatorParameter(
                        "MeleeSwing",
                        AnimatorControllerParameterType.Trigger))
                {
                    _animator.ResetTrigger(
                        "MeleeSwing");

                    _animator.SetTrigger(
                        "MeleeSwing");
                }
            }
            catch
            {
            }
        }


        private bool HasAnimatorParameter(
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            if (_animator == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters =
                _animator.parameters;

            for (int i = 0;
                 parameters != null &&
                 i < parameters.Length;
                 i++)
            {
                AnimatorControllerParameter parameter =
                    parameters[i];

                if (parameter != null &&
                    parameter.type ==
                        parameterType &&
                    string.Equals(
                        parameter.name,
                        parameterName,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }


        private Character ResolvePlayerTarget()
        {
            if (_targetCandidateCache == null ||
                Time.time >= _nextTargetCandidateCacheRefresh)
            {
                _nextTargetCandidateCacheRefresh =
                    Time.time + TargetCandidateCacheInterval;

                _targetCandidateCache =
                    UnityEngine.Object.FindObjectsOfType<Character>();

                if (_targetCandidateCache == null)
                {
                    _targetCandidateCache =
                        new Character[0];
                }
            }

            Character[] characters =
                _targetCandidateCache;

            if (characters.Length == 0)
            {
                return null;
            }

            Character nearest =
                null;

            float nearestDistance =
                float.PositiveInfinity;

            float maxRange =
                _definition != null
                    ? _definition.AggroRange
                    : 22f;

            float maxRangeSquared =
                maxRange * maxRange;

            for (int i = 0;
                 i < characters.Length;
                 i++)
            {
                Character candidate =
                    characters[i];

                if (!IsValidPlayerTarget(
                        candidate) ||
                    candidate == _character)
                {
                    continue;
                }

                float distance =
                    (transform.position - candidate.transform.position).sqrMagnitude;

                if (distance > maxRangeSquared ||
                    distance >= nearestDistance)
                {
                    continue;
                }

                bool hasLineOfSight =
                    true;

                try
                {
                    hasLineOfSight =
                        _npc == null ||
                        _npc.CheckLOS(
                            candidate);
                }
                catch
                {
                    // Keep fallback acquisition functional even if this
                    // runtime-built NPC lacks part of native LOS wiring.
                }

                if (!hasLineOfSight)
                {
                    continue;
                }

                nearest =
                    candidate;

                nearestDistance =
                    distance;
            }

            return
                nearest;
        }


        private static bool IsValidPlayerTarget(
            Character target)
        {
            if (target == null ||
                !target.Alive ||
                target.MyStats == null ||
                target.MyStats.CurrentHP <=
                    0)
            {
                return false;
            }

            return
                target.MyFaction ==
                    Character.Faction.Player ||
                target.MyFaction ==
                    Character.Faction.PC;
        }


        private static string GetCharacterName(
            Character character)
        {
            if (character == null)
            {
                return
                    "<null>";
            }

            if (character.MyStats != null &&
                !string.IsNullOrEmpty(
                    character.MyStats.MyName))
            {
                return
                    character.MyStats.MyName;
            }

            if (character.MyNPC != null &&
                !string.IsNullOrEmpty(
                    character.MyNPC.NPCName))
            {
                return
                    character.MyNPC.NPCName;
            }

            return
                character.name;
        }
    }


    internal sealed class CustomCreatureNameplateBillboard :
        MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera camera =
                Camera.main;

            if (camera == null)
            {
                return;
            }

            // Face the text toward the camera. The previous subtraction was
            // reversed, so the TextMeshPro face pointed away from the camera
            // and appeared mirrored/backwards.
            Vector3 direction =
                camera.transform.position -
                transform.position;

            if (direction.sqrMagnitude <
                0.001f)
            {
                return;
            }

            // TextMeshPro's rendered face is opposite the transform-forward
            // direction used by Quaternion.LookRotation here. Apply the explicit
            // 180-degree yaw correction so the visible glyph face points at the
            // camera rather than presenting mirrored/backwards text.
            transform.rotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up) *
                Quaternion.Euler(
                    0f,
                    180f,
                    0f);
        }
    }
}
