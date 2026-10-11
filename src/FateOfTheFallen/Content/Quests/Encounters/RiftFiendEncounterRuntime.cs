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
    /// <summary>
    /// Quest 5 - Ash Beneath the Earth
    ///
    /// 5-man Flame Rift encounter using Erenshor's native Lava Fiend from
    /// Brax's Plane of Elements as the visual / combat template.
    ///
    /// Encounter:
    ///   Anchor 1: Riftbound Infernal
    ///   Anchor 2: Cinder Wretch
    ///   Anchor 3: Cinder Wretch
    ///
    /// The native Lava Fiend template is acquired from Erenshor's authoritative
    /// PlaneOfBrax scene. Quest 5 can preload that scene temporarily/additively,
    /// cache the exact native NPC, unload the source scene, then populate the
    /// encounter in SaltedStrand without moving the player.
    ///
    /// No native Lava Fiend item loot is retained.
    /// </summary>
    internal static class RiftFiendEncounterRuntime
    {
        internal const string NativeTemplateNpcName =
            "Lava Fiend";

        internal const string NativeSourceSceneName =
            "PlaneOfBrax";

        private const string TemplateObjectName =
            "FOTF_Q5_LavaFiendTemplate";

        private const string EncounterSceneName =
            FlameRiftRuntime.SceneName;

        private const int PollFrames =
            20;

        private const float ResolveRetrySeconds =
            3.0f;

        private static bool _initialized;
        private static bool _loggedMissingTemplate;
        private static bool _directLookupAttempted;
        private static bool _residentProbeAttempted;
        private static bool _residentSpawnPointProbeAttempted;
        private static bool _automaticSourcePreloadAttempted;
        private static bool _automaticSourcePreloadInProgress;
        private static bool _automaticSourcePreloadCancelRequested;
        private static bool _loggedAuxiliaryNavSuppression;
        private static bool _loggedAuxiliaryCharacterStartSuppression;
        private static bool _encounterCleared;
        private static bool _clearAnnouncementSent;

        private static GameObject _lavaFiendTemplate;
        private static Coroutine _monitorRoutine;
        private static Coroutine _sourceCaptureRoutine;
        private static Coroutine _automaticSourcePreloadRoutine;

        private static readonly List<EncounterMob>
            _mobs =
                new List<EncounterMob>();


        internal static bool IsEncounterCleared
        {
            get
            {
                if (AshBeneathEarthContent.PlayerHasShard())
                {
                    return true;
                }

                return
                    _encounterCleared;
            }
        }


        // ============================================================
        // TUNING
        // ============================================================

        private sealed class EncounterDefinition
        {
            internal int Index;
            internal string Name;
            internal int Level;
            internal int HP;
            internal int AC;
            internal int Resistance;
            internal int MeleeDamage;
            internal int MainHandDelay;
            internal float RunSpeed;
            internal float AggroRange;
            internal float Scale;
            internal int MinGold;
            internal int MaxGold;
        }


        private static readonly EncounterDefinition[]
            Definitions =
            {
                new EncounterDefinition
                {
                    Index = 1,
                    Name = "Riftbound Infernal",
                    Level = 40,
                    HP = 800000,
                    AC = 550,
                    Resistance = 45,
                    MeleeDamage = 1800,
                    MainHandDelay = 115,
                    RunSpeed = 4.25f,
                    AggroRange = 20f,
                    Scale = 1.25f,
                    MinGold = 75,
                    MaxGold = 125
                },

                new EncounterDefinition
                {
                    Index = 2,
                    Name = "Cinder Wretch",
                    Level = 38,
                    HP = 350000,
                    AC = 450,
                    Resistance = 35,
                    MeleeDamage = 1200,
                    MainHandDelay = 105,
                    RunSpeed = 4.5f,
                    AggroRange = 20f,
                    Scale = 0.82f,
                    MinGold = 25,
                    MaxGold = 50
                },

                new EncounterDefinition
                {
                    Index = 3,
                    Name = "Cinder Wretch",
                    Level = 38,
                    HP = 350000,
                    AC = 450,
                    Resistance = 35,
                    MeleeDamage = 1200,
                    MainHandDelay = 105,
                    RunSpeed = 4.5f,
                    AggroRange = 20f,
                    Scale = 0.82f,
                    MinGold = 25,
                    MaxGold = 50
                }
            };


        // ============================================================
        // LIFECYCLE
        // ============================================================

        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized =
                true;

            SceneManager.sceneLoaded -=
                OnSceneLoaded;

            SceneManager.sceneLoaded +=
                OnSceneLoaded;

            FOTFExpansionGate.EffectiveUnlockChanged -=
                OnExpansionGateChanged;

            FOTFExpansionGate.EffectiveUnlockChanged +=
                OnExpansionGateChanged;

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] initialized. NativeTemplate=[" +
                NativeTemplateNpcName +
                "] NativeSourceScene=[" +
                NativeSourceSceneName +
                "].");

            // Safe cold-path attempts:
            //   1. exact Resources.Load paths
            //   2. one-time scan of ALREADY-LOADED NPC objects
            //
            // Neither path bulk-loads the Resources database.
            TryResolveDirectNativeResource();

            if (_lavaFiendTemplate == null)
            {
                TryResolveResidentNativeObject();
            }

            if (_lavaFiendTemplate == null)
            {
                TryResolveResidentSpawnPointAsset();
            }

            BeginForScene(
                SceneManager.GetActiveScene());
        }


        internal static void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            _initialized =
                false;

            SceneManager.sceneLoaded -=
                OnSceneLoaded;

            FOTFExpansionGate.EffectiveUnlockChanged -=
                OnExpansionGateChanged;

            StopMonitor();
            StopSourceCaptureProbe();

            // If a temporary additive PlaneOfBrax preload is in flight, let
            // its coroutine finish unloading the auxiliary scene cleanly.
            _automaticSourcePreloadCancelRequested =
                true;

            DestroyEncounter();
            DestroyTemplate();

            _directLookupAttempted =
                false;

            _residentProbeAttempted =
                false;

            _residentSpawnPointProbeAttempted =
                false;

            _automaticSourcePreloadAttempted =
                false;

            _encounterCleared =
                false;

            _clearAnnouncementSent =
                false;

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] shutdown.");
        }


        private static void OnExpansionGateChanged(
            bool unlocked)
        {
            if (!_initialized)
            {
                return;
            }

            if (!unlocked)
            {
                _automaticSourcePreloadCancelRequested =
                    true;

                StopMonitor();
                StopSourceCaptureProbe();
                DestroyEncounter();

                _encounterCleared =
                    false;

                _clearAnnouncementSent =
                    false;

                return;
            }

            BeginForScene(
                SceneManager.GetActiveScene());
        }


        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            // Temporary additive scene loads are implementation details of
            // the Lava Fiend preloader. Keep the active SaltedStrand encounter
            // and its monitor intact.
            if (mode == LoadSceneMode.Additive &&
                !IsEncounterScene(
                    scene))
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest5Encounter] auxiliary additive sceneLoaded=[" +
                    (scene.IsValid()
                        ? scene.name
                        : "<invalid>") +
                    "] activeScene=[" +
                    SceneManager.GetActiveScene().name +
                    "]; encounter state preserved.");

                return;
            }

            // A real scene transition should cancel any source preload. The
            // preload coroutine itself will unload PlaneOfBrax if it was
            // already requested.
            _automaticSourcePreloadCancelRequested =
                true;

            StopMonitor();
            StopSourceCaptureProbe();
            DestroyEncounter();

            // Clearing is intentionally per visit until the shard is taken.
            // If the player already has the shard, IsEncounterCleared remains
            // true through the inventory check.
            _encounterCleared =
                AshBeneathEarthContent.PlayerHasShard();

            _clearAnnouncementSent =
                _encounterCleared;

            BeginForScene(
                scene);
        }


        private static void BeginForScene(
            Scene scene)
        {
            if (!_initialized ||
                !FOTFExpansionGate.IsUnlocked ||
                !scene.IsValid() ||
                !scene.isLoaded)
            {
                return;
            }

            // PlaneOfBrax is the authoritative source for Lava Fiend.
            // Only perform native-source capture work there.
            if (IsNativeSourceScene(
                    scene))
            {
                if (_lavaFiendTemplate == null)
                {
                    StartSourceCaptureProbe(
                        scene);
                }

                return;
            }

            if (!IsEncounterScene(
                    scene))
            {
                return;
            }

            if (_monitorRoutine == null)
            {
                _monitorRoutine =
                    CoroutineHost.Start(
                        MonitorEncounter(
                            scene));
            }
        }


        private static bool IsNativeSourceScene(
            Scene scene)
        {
            return
                scene.IsValid() &&
                scene.isLoaded &&
                string.Equals(
                    scene.name,
                    NativeSourceSceneName,
                    StringComparison.OrdinalIgnoreCase);
        }


        private static void StartSourceCaptureProbe(
            Scene scene)
        {
            if (_lavaFiendTemplate != null ||
                _sourceCaptureRoutine != null ||
                !IsNativeSourceScene(
                    scene))
            {
                return;
            }

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] entered native source scene [" +
                NativeSourceSceneName +
                "]; waiting for exact [" +
                NativeTemplateNpcName +
                "] SpawnPoint.");

            _sourceCaptureRoutine =
                CoroutineHost.Start(
                    CaptureSourceWhenReady(
                        scene));
        }


        private static void StopSourceCaptureProbe()
        {
            if (_sourceCaptureRoutine == null)
            {
                return;
            }

            CoroutineHost.Stop(
                _sourceCaptureRoutine);

            _sourceCaptureRoutine =
                null;
        }


        private static IEnumerator CaptureSourceWhenReady(
            Scene scene)
        {
            const int MaximumFrames =
                1800;

            for (int frame = 0;
                 frame < MaximumFrames;
                 frame++)
            {
                if (!_initialized ||
                    !FOTFExpansionGate.IsUnlocked ||
                    !scene.IsValid() ||
                    !scene.isLoaded ||
                    !IsNativeSourceScene(
                        SceneManager.GetActiveScene()))
                {
                    _sourceCaptureRoutine =
                        null;

                    yield break;
                }

                if (_lavaFiendTemplate != null)
                {
                    _sourceCaptureRoutine =
                        null;

                    yield break;
                }

                if (frame == 0 ||
                    frame % 30 == 0)
                {
                    if (TryResolveLoadedNativeSource(
                            false))
                    {
                        Plugin.NativeLog.LogInfo(
                            "[Quest5Encounter] successfully cached native [" +
                            NativeTemplateNpcName +
                            "] from authoritative source scene [" +
                            NativeSourceSceneName +
                            "]. Returning to SaltedStrand can now populate the Quest 5 encounter.");

                        _sourceCaptureRoutine =
                            null;

                        yield break;
                    }
                }

                yield return null;
            }

            Plugin.NativeLog.LogWarning(
                "[Quest5Encounter] remained in [" +
                NativeSourceSceneName +
                "] but no exact [" +
                NativeTemplateNpcName +
                "] SpawnPoint became available.");

            _sourceCaptureRoutine =
                null;
        }


        private static bool IsEncounterScene(
            Scene scene)
        {
            return
                scene.IsValid() &&
                scene.isLoaded &&
                string.Equals(
                    scene.name,
                    EncounterSceneName,
                    StringComparison.OrdinalIgnoreCase);
        }


        private static void StopMonitor()
        {
            if (_monitorRoutine == null)
            {
                return;
            }

            CoroutineHost.Stop(
                _monitorRoutine);

            _monitorRoutine =
                null;
        }


        // ============================================================
        // MONITOR
        // ============================================================

        private static IEnumerator MonitorEncounter(
            Scene scene)
        {
            // Wait for normal Erenshor scene setup plus the Flame Rift runtime.
            for (int frame = 0;
                 frame < 180;
                 frame++)
            {
                if (!CanContinue(
                        scene))
                {
                    _monitorRoutine =
                        null;

                    yield break;
                }

                if (GameData.PlayerControl != null &&
                    FlameRiftRuntime.CurrentRift != null)
                {
                    break;
                }

                yield return null;
            }

            while (CanContinue(
                       scene))
            {
                if (!AshBeneathEarthQuest.IsActive() ||
                    AshBeneathEarthQuest.IsComplete() ||
                    AshBeneathEarthContent.PlayerHasShard())
                {
                    DestroyEncounter();

                    if (AshBeneathEarthContent.PlayerHasShard())
                    {
                        _encounterCleared =
                            true;
                    }

                    yield return WaitFrames(
                        PollFrames);

                    continue;
                }

                if (FlameRiftRuntime.CurrentRift == null)
                {
                    yield return WaitFrames(
                        PollFrames);

                    continue;
                }

                if (_encounterCleared)
                {
                    DestroyRemainingLivingEncounterMobs();

                    yield return WaitFrames(
                        PollFrames);

                    continue;
                }

                if (_mobs.Count == 0)
                {
                    if (!EnsureNativeTemplate())
                    {
                        yield return WaitSecondsByFrames(
                            ResolveRetrySeconds);

                        continue;
                    }

                    yield return SpawnEncounter(
                        scene);

                    yield return WaitFrames(
                        PollFrames);

                    continue;
                }

                CheckEncounterCompletion();

                yield return WaitFrames(
                    PollFrames);
            }

            _monitorRoutine =
                null;
        }


        private static bool CanContinue(
            Scene scene)
        {
            return
                _initialized &&
                FOTFExpansionGate.IsUnlocked &&
                scene.IsValid() &&
                scene.isLoaded &&
                IsEncounterScene(
                    SceneManager.GetActiveScene());
        }


        private static IEnumerator WaitFrames(
            int frames)
        {
            for (int i = 0;
                 i < frames;
                 i++)
            {
                yield return null;
            }
        }


        private static IEnumerator WaitSecondsByFrames(
            float seconds)
        {
            float until =
                Time.realtimeSinceStartup +
                Mathf.Max(
                    0.1f,
                    seconds);

            while (Time.realtimeSinceStartup <
                   until)
            {
                yield return null;
            }
        }


        // ============================================================
        // NATIVE LAVA FIEND RESOLUTION
        // ============================================================

        private static bool EnsureNativeTemplate()
        {
            if (_lavaFiendTemplate != null)
            {
                return true;
            }

            if (TryResolveDirectNativeResource())
            {
                return true;
            }

            if (TryResolveResidentNativeObject())
            {
                return true;
            }

            if (TryResolveResidentSpawnPointAsset())
            {
                return true;
            }

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (IsNativeSourceScene(
                    activeScene))
            {
                if (TryResolveLoadedNativeSource(
                        false))
                {
                    return true;
                }

                StartSourceCaptureProbe(
                    activeScene);

                return false;
            }

            // We now know Lava Fiend is a PlaneOfBrax-only dependency.
            // When Quest 5 needs it from SaltedStrand, preload that scene
            // additively, clone the native NPC, and unload the source scene.
            if (IsEncounterScene(
                    activeScene))
            {
                StartAutomaticSourcePreload(
                    activeScene);

                if (_automaticSourcePreloadInProgress)
                {
                    return false;
                }
            }

            if (!_loggedMissingTemplate)
            {
                _loggedMissingTemplate =
                    true;

                Plugin.NativeLog.LogWarning(
                    "[Quest5Encounter] native [" +
                    NativeTemplateNpcName +
                    "] template is still unavailable after the automatic [" +
                    NativeSourceSceneName +
                    "] additive preload attempt.");
            }

            return false;
        }


        private static void StartAutomaticSourcePreload(
            Scene encounterScene)
        {
            if (_lavaFiendTemplate != null ||
                _automaticSourcePreloadInProgress ||
                _automaticSourcePreloadAttempted ||
                !IsEncounterScene(
                    encounterScene))
            {
                return;
            }

            _automaticSourcePreloadAttempted =
                true;

            _automaticSourcePreloadInProgress =
                true;

            _automaticSourcePreloadCancelRequested =
                false;

            _loggedAuxiliaryNavSuppression =
                false;

            _loggedAuxiliaryCharacterStartSuppression =
                false;

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] starting temporary additive preload of [" +
                NativeSourceSceneName +
                "] to acquire native [" +
                NativeTemplateNpcName +
                "] without moving the player.");

            _automaticSourcePreloadRoutine =
                CoroutineHost.Start(
                    AutomaticSourcePreload(
                        encounterScene));
        }


        private static IEnumerator AutomaticSourcePreload(
            Scene encounterScene)
        {
            bool loadedByThisRuntime =
                false;

            Scene sourceScene =
                SceneManager.GetSceneByName(
                    NativeSourceSceneName);

            // --------------------------------------------------------
            // LOAD SOURCE SCENE ADDITIVELY
            // --------------------------------------------------------
            if (!sourceScene.IsValid() ||
                !sourceScene.isLoaded)
            {
                AsyncOperation loadOperation =
                    null;

                try
                {
                    loadOperation =
                        SceneManager.LoadSceneAsync(
                            NativeSourceSceneName,
                            LoadSceneMode.Additive);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "[Quest5Encounter] automatic additive load of [" +
                        NativeSourceSceneName +
                        "] failed to start: " +
                        exception);

                    FinishAutomaticSourcePreload();

                    yield break;
                }

                if (loadOperation == null)
                {
                    Plugin.NativeLog.LogError(
                        "[Quest5Encounter] SceneManager.LoadSceneAsync returned null for [" +
                        NativeSourceSceneName +
                        "].");

                    FinishAutomaticSourcePreload();

                    yield break;
                }

                loadedByThisRuntime =
                    true;

                while (!loadOperation.isDone)
                {
                    yield return null;
                }

                sourceScene =
                    SceneManager.GetSceneByName(
                        NativeSourceSceneName);
            }

            // Keep gameplay rooted in SaltedStrand. Additive loads normally
            // preserve the active scene, but enforce that explicitly.
            if (encounterScene.IsValid() &&
                encounterScene.isLoaded &&
                SceneManager.GetActiveScene() !=
                encounterScene)
            {
                SceneManager.SetActiveScene(
                    encounterScene);
            }

            if (!sourceScene.IsValid() ||
                !sourceScene.isLoaded)
            {
                Plugin.NativeLog.LogError(
                    "[Quest5Encounter] additive source scene [" +
                    NativeSourceSceneName +
                    "] did not become available after loading.");

                FinishAutomaticSourcePreload();

                yield break;
            }

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] additive source scene [" +
                NativeSourceSceneName +
                "] loaded. Searching only that scene for [" +
                NativeTemplateNpcName +
                "].");

            // --------------------------------------------------------
            // CAPTURE LAVA FIEND
            // --------------------------------------------------------
            bool captured =
                false;

            const int MaximumCaptureFrames =
                600;

            for (int frame = 0;
                 frame < MaximumCaptureFrames;
                 frame++)
            {
                if (_automaticSourcePreloadCancelRequested ||
                    !_initialized ||
                    !FOTFExpansionGate.IsUnlocked ||
                    !encounterScene.IsValid() ||
                    !encounterScene.isLoaded ||
                    !IsEncounterScene(
                        SceneManager.GetActiveScene()))
                {
                    break;
                }

                if (_lavaFiendTemplate != null)
                {
                    captured =
                        true;

                    break;
                }

                if (frame == 0 ||
                    frame % 10 == 0)
                {
                    if (TryResolveFromLoadedSourceScene(
                            sourceScene))
                    {
                        captured =
                            true;

                        break;
                    }
                }

                yield return null;
            }

            if (captured)
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest5Encounter] automatic source preload successfully cached [" +
                    NativeTemplateNpcName +
                    "].");
            }
            else if (!_automaticSourcePreloadCancelRequested)
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest5Encounter] automatic additive preload of [" +
                    NativeSourceSceneName +
                    "] completed, but [" +
                    NativeTemplateNpcName +
                    "] was not resolved within the capture window.");
            }

            // --------------------------------------------------------
            // UNLOAD AUXILIARY SOURCE SCENE
            // --------------------------------------------------------
            if (loadedByThisRuntime &&
                sourceScene.IsValid() &&
                sourceScene.isLoaded)
            {
                // Never allow Unity to consider the auxiliary source scene
                // active when it is unloaded.
                if (encounterScene.IsValid() &&
                    encounterScene.isLoaded)
                {
                    SceneManager.SetActiveScene(
                        encounterScene);
                }

                AsyncOperation unloadOperation =
                    null;

                try
                {
                    unloadOperation =
                        SceneManager.UnloadSceneAsync(
                            sourceScene);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogWarning(
                        "[Quest5Encounter] failed to begin unloading temporary [" +
                        NativeSourceSceneName +
                        "] scene: " +
                        exception.Message);
                }

                if (unloadOperation != null)
                {
                    while (!unloadOperation.isDone)
                    {
                        yield return null;
                    }

                    Plugin.NativeLog.LogInfo(
                        "[Quest5Encounter] temporary additive source scene [" +
                        NativeSourceSceneName +
                        "] unloaded.");
                }
            }

            FinishAutomaticSourcePreload();
        }


        private static void FinishAutomaticSourcePreload()
        {
            _automaticSourcePreloadInProgress =
                false;

            _automaticSourcePreloadCancelRequested =
                false;

            _loggedAuxiliaryNavSuppression =
                false;

            _loggedAuxiliaryCharacterStartSuppression =
                false;

            _automaticSourcePreloadRoutine =
                null;
        }


        /// <summary>
        /// PlaneOfBrax is being loaded only as a temporary source container
        /// while SaltedStrand remains the active gameplay scene.
        ///
        /// Native Character.Start() assumes the scene it belongs to is the
        /// active gameplay scene and can dereference scene/gameplay services
        /// that are intentionally not initialized for this auxiliary load.
        ///
        /// Skip Character.Start only for Character components physically
        /// owned by the temporary PlaneOfBrax scene. The cached template is
        /// inactive, and the actual Quest 5 clones are moved to SaltedStrand
        /// before activation, so their normal Character.Start() still runs.
        /// </summary>
        internal static bool ShouldSuppressAuxiliaryPlaneCharacterStart(
            Character character)
        {
            if (!_automaticSourcePreloadInProgress ||
                character == null ||
                character.gameObject == null)
            {
                return false;
            }

            Scene characterScene =
                character.gameObject.scene;

            if (!characterScene.IsValid() ||
                !characterScene.isLoaded ||
                !string.Equals(
                    characterScene.name,
                    NativeSourceSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (!IsEncounterScene(
                    activeScene))
            {
                return false;
            }

            if (!_loggedAuxiliaryCharacterStartSuppression)
            {
                _loggedAuxiliaryCharacterStartSuppression =
                    true;

                Plugin.NativeLog.LogInfo(
                    "[Quest5Encounter] suppressing Character.Start for temporary auxiliary [" +
                    NativeSourceSceneName +
                    "] characters while [" +
                    activeScene.name +
                    "] remains the active gameplay scene.");
            }

            return true;
        }


        /// <summary>
        /// During the temporary additive PlaneOfBrax preload, native NPCs in
        /// that auxiliary scene are not part of the active gameplay zone.
        ///
        /// Their normal NPC.NavUpdate coroutine can run before the raid scene
        /// has a complete active-zone navigation context, which causes the
        /// observed NPC.UpdateNav NullReferenceException.
        ///
        /// Return true only for NPCs physically owned by the temporary
        /// PlaneOfBrax scene while SaltedStrand remains the active scene.
        /// Quest 5 clones are moved into SaltedStrand, so their normal native
        /// navigation is never suppressed by this guard.
        /// </summary>
        internal static bool ShouldSuppressAuxiliaryPlaneNpcNavigation(
            NPC npc)
        {
            if (!_automaticSourcePreloadInProgress ||
                npc == null ||
                npc.gameObject == null)
            {
                return false;
            }

            Scene npcScene =
                npc.gameObject.scene;

            if (!npcScene.IsValid() ||
                !npcScene.isLoaded ||
                !string.Equals(
                    npcScene.name,
                    NativeSourceSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (!IsEncounterScene(
                    activeScene))
            {
                return false;
            }

            if (!_loggedAuxiliaryNavSuppression)
            {
                _loggedAuxiliaryNavSuppression =
                    true;

                Plugin.NativeLog.LogInfo(
                    "[Quest5Encounter] suppressing NPC.UpdateNav for temporary auxiliary [" +
                    NativeSourceSceneName +
                    "] NPCs while [" +
                    activeScene.name +
                    "] remains the active gameplay scene.");
            }

            return true;
        }


        private static bool TryResolveFromLoadedSourceScene(
            Scene sourceScene)
        {
            if (_lavaFiendTemplate != null)
            {
                return true;
            }

            if (!sourceScene.IsValid() ||
                !sourceScene.isLoaded)
            {
                return false;
            }

            GameObject[] roots;

            try
            {
                roots =
                    sourceScene.GetRootGameObjects();
            }
            catch
            {
                return false;
            }

            if (roots == null ||
                roots.Length == 0)
            {
                return false;
            }

            // Prefer SpawnPoints because that is the exact path confirmed by
            // the live PlaneOfBrax test.
            for (int i = 0;
                 i < roots.Length;
                 i++)
            {
                GameObject root =
                    roots[i];

                if (root == null)
                {
                    continue;
                }

                SpawnPoint[] spawnPoints =
                    root.GetComponentsInChildren<SpawnPoint>(
                        true);

                if (spawnPoints == null)
                {
                    continue;
                }

                for (int j = 0;
                     j < spawnPoints.Length;
                     j++)
                {
                    SpawnPoint spawnPoint =
                        spawnPoints[j];

                    if (spawnPoint == null)
                    {
                        continue;
                    }

                    NPC npc =
                        spawnPoint.SpawnedNPC;

                    if (!IsExactLavaFiend(
                            npc))
                    {
                        npc =
                            FindReferencedLavaFiendNpc(
                                spawnPoint);
                    }

                    if (!IsExactLavaFiend(
                            npc))
                    {
                        continue;
                    }

                    if (CacheNpcTemplate(
                            npc,
                            "automatic additive PlaneOfBrax SpawnPoint"))
                    {
                        return true;
                    }
                }
            }

            // Defensive fallback: scan NPCs belonging only to PlaneOfBrax.
            for (int i = 0;
                 i < roots.Length;
                 i++)
            {
                GameObject root =
                    roots[i];

                if (root == null)
                {
                    continue;
                }

                NPC[] npcs =
                    root.GetComponentsInChildren<NPC>(
                        true);

                if (npcs == null)
                {
                    continue;
                }

                for (int j = 0;
                     j < npcs.Length;
                     j++)
                {
                    NPC npc =
                        npcs[j];

                    if (!IsExactLavaFiend(
                            npc))
                    {
                        continue;
                    }

                    if (CacheNpcTemplate(
                            npc,
                            "automatic additive PlaneOfBrax NPC"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        private static bool TryResolveDirectNativeResource()
        {
            if (_lavaFiendTemplate != null)
            {
                return true;
            }

            if (_directLookupAttempted)
            {
                return false;
            }

            _directLookupAttempted =
                true;

            string[] candidatePaths =
            {
                "Lava Fiend",
                "LavaFiend",
                "NPC/Lava Fiend",
                "NPC/LavaFiend",
                "NPCs/Lava Fiend",
                "NPCs/LavaFiend",
                "Enemies/Lava Fiend",
                "Enemies/LavaFiend",
                "Monsters/Lava Fiend",
                "Monsters/LavaFiend"
            };

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] attempting SAFE exact Resources.Load lookup for [" +
                NativeTemplateNpcName +
                "]. CandidateCount=[" +
                candidatePaths.Length +
                "].");

            for (int i = 0;
                 i < candidatePaths.Length;
                 i++)
            {
                string path =
                    candidatePaths[i];

                GameObject root =
                    null;

                try
                {
                    root =
                        Resources.Load<GameObject>(
                            path);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogWarning(
                        "[Quest5Encounter] exact Resources.Load failed for path [" +
                        path +
                        "]: " +
                        exception.Message);

                    continue;
                }

                if (root == null)
                {
                    continue;
                }

                NPC npc =
                    root.GetComponent<NPC>();

                if (npc == null)
                {
                    npc =
                        root.GetComponentInChildren<NPC>(
                            true);
                }

                if (!IsExactLavaFiend(
                        npc))
                {
                    Plugin.NativeLog.LogInfo(
                        "[Quest5Encounter] Resources path [" +
                        path +
                        "] returned an object, but it was not exact NPC [" +
                        NativeTemplateNpcName +
                        "].");

                    continue;
                }

                if (CacheNpcTemplate(
                        npc,
                        "exact Resources path [" +
                        path +
                        "]"))
                {
                    Plugin.NativeLog.LogInfo(
                        "[Quest5Encounter] SAFE cold lookup resolved [" +
                        NativeTemplateNpcName +
                        "] from exact Resources path [" +
                        path +
                        "]. PlaneOfBrax visit is not required.");

                    return true;
                }
            }

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] SAFE exact Resources.Load lookup did not resolve [" +
                NativeTemplateNpcName +
                "].");

            return false;
        }


        private static bool TryResolveResidentNativeObject()
        {
            if (_lavaFiendTemplate != null)
            {
                return true;
            }

            if (_residentProbeAttempted)
            {
                return false;
            }

            _residentProbeAttempted =
                true;

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] probing already-loaded native NPC objects for exact [" +
                NativeTemplateNpcName +
                "]. No assets will be loaded by this probe.");

            NPC[] all =
                null;

            try
            {
                all =
                    Resources.FindObjectsOfTypeAll<NPC>();
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest5Encounter] resident NPC probe failed: " +
                    exception.Message);

                return false;
            }

            if (all == null ||
                all.Length == 0)
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest5Encounter] resident NPC probe found no NPC objects.");

                return false;
            }

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] resident NPC probe inspecting [" +
                all.Length +
                "] already-loaded NPC object(s).");

            // Prefer non-scene prefab/assets first. If an exact live Lava Fiend
            // is already present for some reason, accept that as a fallback.
            for (int pass = 0;
                 pass < 2;
                 pass++)
            {
                for (int i = 0;
                     i < all.Length;
                     i++)
                {
                    NPC candidate =
                        all[i];

                    if (!IsExactLavaFiend(
                            candidate))
                    {
                        continue;
                    }

                    if (candidate.gameObject.name.StartsWith(
                            "FOTF_",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    bool sceneObject =
                        candidate.gameObject.scene.IsValid();

                    if (pass == 0 &&
                        sceneObject)
                    {
                        continue;
                    }

                    if (pass == 1 &&
                        !sceneObject)
                    {
                        continue;
                    }

                    string origin =
                        sceneObject
                            ? "resident scene NPC [" +
                              candidate.gameObject.scene.name +
                              "]"
                            : "resident native prefab/asset";

                    Plugin.NativeLog.LogInfo(
                        "[Quest5Encounter] resident probe found exact [" +
                        NativeTemplateNpcName +
                        "] object=[" +
                        candidate.gameObject.name +
                        "] scene=[" +
                        (sceneObject
                            ? candidate.gameObject.scene.name
                            : "<asset>") +
                        "].");

                    if (CacheNpcTemplate(
                            candidate,
                            origin))
                    {
                        Plugin.NativeLog.LogInfo(
                            "[Quest5Encounter] resident object probe resolved [" +
                            NativeTemplateNpcName +
                            "]. PlaneOfBrax visit is not required.");

                        return true;
                    }
                }
            }

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] resident NPC probe did not find an exact [" +
                NativeTemplateNpcName +
                "] among already-loaded objects.");

            return false;
        }


        private static bool TryResolveResidentSpawnPointAsset()
        {
            if (_lavaFiendTemplate != null)
            {
                return true;
            }

            if (_residentSpawnPointProbeAttempted)
            {
                return false;
            }

            _residentSpawnPointProbeAttempted =
                true;

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] probing already-loaded SpawnPoint objects for serialized [" +
                NativeTemplateNpcName +
                "] references. No assets will be loaded by this probe.");

            SpawnPoint[] all =
                null;

            try
            {
                all =
                    Resources.FindObjectsOfTypeAll<SpawnPoint>();
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest5Encounter] resident SpawnPoint probe failed: " +
                    exception.Message);

                return false;
            }

            if (all == null ||
                all.Length == 0)
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest5Encounter] resident SpawnPoint probe found no SpawnPoint objects.");

                return false;
            }

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] resident SpawnPoint probe inspecting [" +
                all.Length +
                "] already-loaded SpawnPoint object(s).");

            // Prefer prefab/assets outside live scenes.
            for (int pass = 0;
                 pass < 2;
                 pass++)
            {
                for (int i = 0;
                     i < all.Length;
                     i++)
                {
                    SpawnPoint spawnPoint =
                        all[i];

                    if (spawnPoint == null)
                    {
                        continue;
                    }

                    bool sceneObject =
                        spawnPoint.gameObject.scene.IsValid();

                    if (pass == 0 &&
                        sceneObject)
                    {
                        continue;
                    }

                    if (pass == 1 &&
                        !sceneObject)
                    {
                        continue;
                    }

                    NPC referencedNpc =
                        FindReferencedLavaFiendNpc(
                            spawnPoint);

                    if (referencedNpc == null)
                    {
                        continue;
                    }

                    string origin =
                        sceneObject
                            ? "resident SpawnPoint scene object [" +
                              spawnPoint.gameObject.scene.name +
                              "]"
                            : "resident SpawnPoint prefab/asset";

                    Plugin.NativeLog.LogInfo(
                        "[Quest5Encounter] resident SpawnPoint probe found serialized [" +
                        NativeTemplateNpcName +
                        "] reference. SpawnPointObject=[" +
                        spawnPoint.gameObject.name +
                        "] scene=[" +
                        (sceneObject
                            ? spawnPoint.gameObject.scene.name
                            : "<asset>") +
                        "].");

                    if (CacheNpcTemplate(
                            referencedNpc,
                            origin))
                    {
                        Plugin.NativeLog.LogInfo(
                            "[Quest5Encounter] resident SpawnPoint asset probe resolved [" +
                            NativeTemplateNpcName +
                            "]. PlaneOfBrax visit is not required.");

                        return true;
                    }
                }
            }

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] resident SpawnPoint probe did not find a serialized [" +
                NativeTemplateNpcName +
                "] reference.");

            return false;
        }


        private static NPC FindReferencedLavaFiendNpc(
            SpawnPoint spawnPoint)
        {
            if (spawnPoint == null)
            {
                return null;
            }

            if (IsExactLavaFiend(
                    spawnPoint.SpawnedNPC))
            {
                return
                    spawnPoint.SpawnedNPC;
            }

            try
            {
                FieldInfo[] fields =
                    typeof(SpawnPoint).GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                if (fields == null)
                {
                    return null;
                }

                for (int i = 0;
                     i < fields.Length;
                     i++)
                {
                    FieldInfo field =
                        fields[i];

                    if (field == null)
                    {
                        continue;
                    }

                    object value;

                    try
                    {
                        value =
                            field.GetValue(
                                spawnPoint);
                    }
                    catch
                    {
                        continue;
                    }

                    if (value == null)
                    {
                        continue;
                    }

                    NPC npc =
                        value as NPC;

                    if (IsExactLavaFiend(
                            npc))
                    {
                        return npc;
                    }

                    GameObject gameObject =
                        value as GameObject;

                    if (gameObject != null)
                    {
                        NPC candidate =
                            gameObject
                                .GetComponentInChildren<NPC>(
                                    true);

                        if (IsExactLavaFiend(
                                candidate))
                        {
                            return candidate;
                        }
                    }

                    Component component =
                        value as Component;

                    if (component != null)
                    {
                        NPC candidate =
                            component
                                .GetComponentInChildren<NPC>(
                                    true);

                        if (IsExactLavaFiend(
                                candidate))
                        {
                            return candidate;
                        }
                    }

                    string text =
                        value as string;

                    if (!string.IsNullOrEmpty(
                            text) &&
                        text.IndexOf(
                            NativeTemplateNpcName,
                            StringComparison.OrdinalIgnoreCase) >=
                        0)
                    {
                        // A string match proves this SpawnPoint references the
                        // Lava Fiend by name, but we still need an actual NPC
                        // object reference to clone. Keep scanning remaining
                        // serialized fields for that concrete reference.
                        continue;
                    }
                }
            }
            catch
            {
            }

            return null;
        }


        private static bool TryResolveLoadedNativeSource(
            bool logFailure)
        {
            if (_lavaFiendTemplate != null)
            {
                return true;
            }

            // ========================================================
            // 1. AUTHORITATIVE ACTIVE-SCENE SPAWNPOINT COLLECTION
            // ========================================================
            //
            // This is deliberately cheap. Do NOT use
            // Resources.FindObjectsOfTypeAll here.
            //
            if (SpawnPointManager.SpawnPointsInScene != null)
            {
                foreach (SpawnPoint spawnPoint in
                         SpawnPointManager.SpawnPointsInScene)
                {
                    if (spawnPoint == null ||
                        spawnPoint.SpawnedNPC == null)
                    {
                        continue;
                    }

                    NPC npc =
                        spawnPoint.SpawnedNPC;

                    if (!IsExactLavaFiend(
                            npc))
                    {
                        continue;
                    }

                    if (CacheNpcTemplate(
                            npc,
                            "active-scene SpawnPoint"))
                    {
                        return true;
                    }
                }
            }

            // ========================================================
            // 2. ACTIVE-SCENE NPCS ONLY
            // ========================================================
            //
            // FindObjectsOfType only inspects live scene objects. This is
            // dramatically cheaper than FindObjectsOfTypeAll / LoadAll.
            //
            try
            {
                NPC[] activeNpcs =
                    UnityEngine.Object.FindObjectsOfType<NPC>();

                if (activeNpcs != null)
                {
                    for (int i = 0;
                         i < activeNpcs.Length;
                         i++)
                    {
                        NPC npc =
                            activeNpcs[i];

                        if (!IsExactLavaFiend(
                                npc))
                        {
                            continue;
                        }

                        if (CacheNpcTemplate(
                                npc,
                                "active-scene NPC"))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }

            if (logFailure)
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest5Encounter] no active native [" +
                    NativeTemplateNpcName +
                    "] source is currently loaded.");
            }

            return false;
        }


        private static bool IsExactLavaFiend(
            NPC npc)
        {
            return
                npc != null &&
                string.Equals(
                    npc.NPCName,
                    NativeTemplateNpcName,
                    StringComparison.OrdinalIgnoreCase);
        }


        private static bool CacheNpcTemplate(
            NPC source,
            string origin)
        {
            if (!IsExactLavaFiend(
                    source))
            {
                return false;
            }

            bool sourceWasActive =
                source.gameObject.activeSelf;

            try
            {
                if (source.gameObject.scene.IsValid() &&
                    sourceWasActive)
                {
                    source.gameObject.SetActive(
                        false);
                }

                GameObject clone =
                    UnityEngine.Object.Instantiate(
                        source.gameObject);

                if (clone == null)
                {
                    return false;
                }

                clone.name =
                    TemplateObjectName;

                clone.transform.SetParent(
                    null,
                    true);

                clone.SetActive(
                    false);

                UnityEngine.Object.DontDestroyOnLoad(
                    clone);

                _lavaFiendTemplate =
                    clone;

                _loggedMissingTemplate =
                    false;

                LogTemplateFingerprint(
                    source,
                    origin);

                Plugin.NativeLog.LogInfo(
                    "[Quest5Encounter] resolved native [" +
                    NativeTemplateNpcName +
                    "] template from [" +
                    origin +
                    "].");

                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest5Encounter] failed caching Lava Fiend template from [" +
                    origin +
                    "]: " +
                    exception.Message);

                return false;
            }
            finally
            {
                if (source != null &&
                    source.gameObject.scene.IsValid() &&
                    sourceWasActive)
                {
                    source.gameObject.SetActive(
                        true);
                }
            }
        }


        private static void LogTemplateFingerprint(
            NPC npc,
            string origin)
        {
            if (npc == null)
            {
                return;
            }

            Animator animator =
                npc.GetComponentInChildren<Animator>(
                    true);

            Renderer[] renderers =
                npc.GetComponentsInChildren<Renderer>(
                    true);

            Plugin.NativeLog.LogInfo(
                "[Quest5EncounterAssets] SOURCE origin=[" +
                origin +
                "] scene=[" +
                (npc.gameObject.scene.IsValid()
                    ? npc.gameObject.scene.name
                    : "<asset>") +
                "] object=[" +
                npc.gameObject.name +
                "] npcName=[" +
                npc.NPCName +
                "] animatorController=[" +
                (animator != null &&
                 animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.name
                    : "<none>") +
                "] rendererCount=[" +
                (renderers != null
                    ? renderers.Length
                    : 0) +
                "].");
        }


        // ============================================================
        // SPAWN
        // ============================================================

        private static IEnumerator SpawnEncounter(
            Scene scene)
        {
            DestroyEncounter();

            if (_lavaFiendTemplate == null)
            {
                yield break;
            }

            int spawned =
                0;

            for (int i = 0;
                 i < Definitions.Length;
                 i++)
            {
                EncounterDefinition definition =
                    Definitions[i];

                Transform anchor;

                if (!FlameRiftRuntime.TryGetEncounterAnchor(
                        definition.Index,
                        out anchor) ||
                    anchor == null)
                {
                    Plugin.NativeLog.LogError(
                        "[Quest5Encounter] FlameRift is missing EncounterAnchor_" +
                        definition.Index +
                        ".");

                    continue;
                }

                Vector3 spawnPosition =
                    ResolveSafeNavMeshPosition(
                        anchor.position);

                Quaternion spawnRotation =
                    anchor.rotation;

                EncounterMob state =
                    null;

                yield return SpawnOne(
                    scene,
                    definition,
                    spawnPosition,
                    spawnRotation,
                    result =>
                    {
                        state =
                            result;
                    });

                if (state != null)
                {
                    _mobs.Add(
                        state);

                    spawned++;
                }
            }

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] population complete. Spawned=[" +
                spawned +
                "/" +
                Definitions.Length +
                "].");
        }


        private static IEnumerator SpawnOne(
            Scene scene,
            EncounterDefinition definition,
            Vector3 position,
            Quaternion rotation,
            Action<EncounterMob> onSpawned)
        {
            if (_lavaFiendTemplate == null ||
                definition == null)
            {
                yield break;
            }

            GameObject npcObject =
                null;

            try
            {
                npcObject =
                    UnityEngine.Object.Instantiate(
                        _lavaFiendTemplate);

                if (npcObject == null)
                {
                    yield break;
                }

                npcObject.name =
                    "FOTF_Q5_" +
                    definition.Name.Replace(
                        " ",
                        "_") +
                    "_" +
                    definition.Index;

                npcObject.transform.SetParent(
                    null,
                    true);

                npcObject.transform.SetPositionAndRotation(
                    position,
                    rotation);

                SceneManager.MoveGameObjectToScene(
                    npcObject,
                    scene);

                npcObject.SetActive(
                    true);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Quest5Encounter] direct Lava Fiend clone failed for [" +
                    definition.Name +
                    "]: " +
                    exception);

                if (npcObject != null)
                {
                    UnityEngine.Object.Destroy(
                        npcObject);
                }

                yield break;
            }

            // Give native Awake / OnEnable / Start time to settle.
            yield return null;
            yield return null;
            yield return null;

            NPC npc =
                npcObject.GetComponent<NPC>();

            if (npc == null)
            {
                npc =
                    npcObject.GetComponentInChildren<NPC>(
                        true);
            }

            if (npc == null)
            {
                Plugin.NativeLog.LogError(
                    "[Quest5Encounter] Lava Fiend clone produced no NPC component for [" +
                    definition.Name +
                    "].");

                UnityEngine.Object.Destroy(
                    npcObject);

                yield break;
            }

            ConfigureEncounterNpc(
                npc,
                definition,
                position);

            EncounterMob state =
                new EncounterMob
                {
                    Definition =
                        definition,

                    Root =
                        npcObject,

                    Npc =
                        npc,

                    Dead =
                        false
                };

            onSpawned?.Invoke(
                state);

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] spawned [" +
                definition.Name +
                "] at [" +
                npc.transform.position +
                "] from native Lava Fiend template.");
        }


        private static void ConfigureEncounterNpc(
            NPC npc,
            EncounterDefinition definition,
            Vector3 requestedPosition)
        {
            if (npc == null ||
                definition == null)
            {
                return;
            }

            string originalNpcName =
                npc.NPCName;

            GameObject monster =
                npc.gameObject;

            monster.name =
                definition.Name;

            npc.NPCName =
                definition.Name;

            npc.SimPlayer =
                false;

            npc.GroupEncounter =
                false;

            npc.noClick =
                false;

            npc.retreat =
                false;

            npc.CurrentAggroTarget =
                null;

            npc.BaseAtkDmg =
                definition.MeleeDamage;

            npc.DamageRange =
                new Vector2(
                    definition.MeleeDamage,
                    definition.MeleeDamage);

            npc.DamageMult =
                1f;

            npc.OHAtkDmg =
                0;

            npc.NoDmgCap =
                true;

            Character character =
                npc.GetChar();

            if (character == null)
            {
                character =
                    monster.GetComponent<Character>();
            }

            Stats stats =
                monster.GetComponent<Stats>();

            if (stats == null)
            {
                stats =
                    monster.GetComponentInChildren<Stats>(
                        true);
            }

            if (stats != null)
            {
                stats.MyName =
                    definition.Name;

                stats.Level =
                    definition.Level;

                stats.BaseHP =
                    definition.HP;

                stats.BaseAC =
                    definition.AC;

                stats.BasePR =
                    definition.Resistance;

                stats.BaseMR =
                    definition.Resistance;

                stats.BaseER =
                    definition.Resistance;

                stats.BaseVR =
                    definition.Resistance;

                stats.RunSpeed =
                    definition.RunSpeed;

                stats.BaseMHAtkDelay =
                    definition.MainHandDelay;

                stats.OverrideHPforNPC =
                    true;

                stats.CalcStats();

                stats.CurrentHP =
                    stats.CurrentMaxHP;
            }

            if (character != null)
            {
                character.Alive =
                    true;

                character.AggroRange =
                    definition.AggroRange;
            }

            // Preserve the authored Lava Fiend proportions while making the
            // central Infernal read larger and the Wretches smaller.
            monster.transform.localScale =
                monster.transform.localScale *
                definition.Scale;

            NavMeshAgent agent =
                monster.GetComponent<NavMeshAgent>();

            if (agent == null)
            {
                agent =
                    monster.GetComponentInChildren<NavMeshAgent>(
                        true);
            }

            if (agent != null &&
                agent.enabled)
            {
                agent.speed =
                    definition.RunSpeed;

                Vector3 navPosition =
                    ResolveSafeNavMeshPosition(
                        requestedPosition);

                if (agent.isOnNavMesh)
                {
                    agent.Warp(
                        navPosition);
                }
                else
                {
                    monster.transform.position =
                        navPosition;
                }
            }
            else
            {
                monster.transform.position =
                    ResolveSafeNavMeshPosition(
                        requestedPosition);
            }

            ClearInheritedLoot(
                npc,
                definition);

            RefreshIdentity(
                npc,
                originalNpcName,
                definition.Name);

            RiftFiendIdentityKeeper keeper =
                monster.GetComponent<RiftFiendIdentityKeeper>();

            if (keeper == null)
            {
                keeper =
                    monster.AddComponent<RiftFiendIdentityKeeper>();
            }

            keeper.Initialize(
                npc,
                originalNpcName,
                definition.Name);

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] configured [" +
                definition.Name +
                "] Level=[" +
                (stats != null
                    ? stats.Level.ToString()
                    : "?") +
                "] HP=[" +
                (stats != null
                    ? stats.CurrentMaxHP.ToString()
                    : "?") +
                "] AC=[" +
                (stats != null
                    ? stats.BaseAC.ToString()
                    : "?") +
                "] Melee=[" +
                definition.MeleeDamage +
                "] Scale=[" +
                definition.Scale.ToString("0.00") +
                "].");
        }


        private static Vector3 ResolveSafeNavMeshPosition(
            Vector3 requested)
        {
            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                    requested,
                    out hit,
                    8f,
                    NavMesh.AllAreas))
            {
                return
                    hit.position;
            }

            return
                requested;
        }


        // ============================================================
        // LOOT
        // ============================================================

        private static void ClearInheritedLoot(
            NPC npc,
            EncounterDefinition definition)
        {
            LootTable loot =
                npc.GetComponent<LootTable>();

            if (loot == null)
            {
                loot =
                    npc.GetComponentInChildren<LootTable>(
                        true);
            }

            if (loot == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest5Encounter] [" +
                    definition.Name +
                    "] has no LootTable; inherited raid loot could not be explicitly cleared.");

                return;
            }

            ClearLootList(
                loot.GuaranteeOneDrop);

            ClearLootList(
                loot.CommonDrop);

            ClearLootList(
                loot.UncommonDrop);

            ClearLootList(
                loot.RareDrop);

            ClearLootList(
                loot.LegendaryDrop);

            ClearLootList(
                loot.UltraRareDrop);

            if (loot.ActualDrops == null)
            {
                loot.ActualDrops =
                    new List<Item>();
            }
            else
            {
                loot.ActualDrops.Clear();
            }

            if (loot.ActualDropsQual == null)
            {
                loot.ActualDropsQual =
                    new List<int>();
            }
            else
            {
                loot.ActualDropsQual.Clear();
            }

            Character character =
                npc.GetChar();

            if (character == null)
            {
                character =
                    npc.GetComponent<Character>();
            }

            loot.hostChar =
                character;

            loot.MinGold =
                definition.MinGold;

            loot.MaxGold =
                definition.MaxGold;

            loot.MyGold =
                UnityEngine.Random.Range(
                    definition.MinGold,
                    definition.MaxGold + 1);

            loot.NumberOfGuaranteedDrops =
                0;

            loot.MaxNumberDrops =
                0;

            loot.MaxNonCommonDrops =
                0;

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] stripped inherited Lava Fiend loot from [" +
                definition.Name +
                "]. Gold=[" +
                loot.MyGold +
                "].");
        }


        private static void ClearLootList(
            List<Item> list)
        {
            if (list != null)
            {
                list.Clear();
            }
        }


        // ============================================================
        // COMPLETION
        // ============================================================

        private static void CheckEncounterCompletion()
        {
            if (_encounterCleared ||
                _mobs.Count == 0)
            {
                return;
            }

            int dead =
                0;

            for (int i = 0;
                 i < _mobs.Count;
                 i++)
            {
                EncounterMob state =
                    _mobs[i];

                if (state == null)
                {
                    dead++;
                    continue;
                }

                if (!state.Dead &&
                    IsNpcDead(
                        state.Npc))
                {
                    state.Dead =
                        true;

                    Plugin.NativeLog.LogInfo(
                        "[Quest5Encounter] defeated [" +
                        (state.Definition != null
                            ? state.Definition.Name
                            : "unknown") +
                        "].");
                }

                if (state.Dead)
                {
                    dead++;
                }
            }

            if (dead <
                Definitions.Length)
            {
                return;
            }

            _encounterCleared =
                true;

            if (!_clearAnnouncementSent)
            {
                _clearAnnouncementSent =
                    true;

                AddSystemMessage(
                    "The demonic binding around the Flame Rift collapses.",
                    "green");

                AddLocalMessage(
                    "With the fiends destroyed, the Cindered Rift Shard can now be safely recovered.",
                    "white");
            }

            Plugin.NativeLog.LogInfo(
                "[Quest5Encounter] CLEARED. Cindered Rift Shard interaction is now unlocked.");
        }


        private static bool IsNpcDead(
            NPC npc)
        {
            if (npc == null)
            {
                return true;
            }

            Character character =
                npc.GetChar();

            if (character == null)
            {
                character =
                    npc.GetComponent<Character>();
            }

            Stats stats =
                npc.GetComponent<Stats>();

            if (stats == null)
            {
                stats =
                    npc.GetComponentInChildren<Stats>(
                        true);
            }

            if (character != null &&
                !character.Alive)
            {
                return true;
            }

            return
                stats != null &&
                stats.CurrentHP <= 0;
        }


        // ============================================================
        // IDENTITY
        // ============================================================

        private static void RefreshIdentity(
            NPC npc,
            string nativeName,
            string desiredName)
        {
            if (npc == null ||
                string.IsNullOrEmpty(
                    desiredName))
            {
                return;
            }

            npc.NPCName =
                desiredName;

            npc.gameObject.name =
                desiredName;

            Stats stats =
                npc.GetComponent<Stats>();

            if (stats == null)
            {
                stats =
                    npc.GetComponentInChildren<Stats>(
                        true);
            }

            if (stats != null)
            {
                stats.MyName =
                    desiredName;
            }

            if (npc.NamePlate == null)
            {
                return;
            }

            TMP_Text[] texts =
                npc.NamePlate.GetComponentsInChildren<TMP_Text>(
                    true);

            if (texts == null)
            {
                return;
            }

            for (int i = 0;
                 i < texts.Length;
                 i++)
            {
                TMP_Text text =
                    texts[i];

                if (text == null ||
                    string.IsNullOrEmpty(
                        text.text))
                {
                    continue;
                }

                string value =
                    text.text.Trim();

                if (string.Equals(
                        value,
                        NativeTemplateNpcName,
                        StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(nativeName) &&
                     string.Equals(
                         value,
                         nativeName,
                         StringComparison.OrdinalIgnoreCase)))
                {
                    text.text =
                        desiredName;
                }
            }
        }


        internal sealed class RiftFiendIdentityKeeper :
            MonoBehaviour
        {
            private NPC _npc;
            private string _nativeName;
            private string _desiredName;
            private float _nextRefreshAt;


            internal void Initialize(
                NPC npc,
                string nativeName,
                string desiredName)
            {
                _npc =
                    npc;

                _nativeName =
                    nativeName;

                _desiredName =
                    desiredName;

                _nextRefreshAt =
                    0f;

                RefreshIdentity(
                    _npc,
                    _nativeName,
                    _desiredName);
            }


            private void Update()
            {
                if (_npc == null ||
                    Time.time <
                    _nextRefreshAt)
                {
                    return;
                }

                _nextRefreshAt =
                    Time.time +
                    0.5f;

                RefreshIdentity(
                    _npc,
                    _nativeName,
                    _desiredName);
            }
        }


        // ============================================================
        // CLEANUP
        // ============================================================

        private static void DestroyRemainingLivingEncounterMobs()
        {
            for (int i = 0;
                 i < _mobs.Count;
                 i++)
            {
                EncounterMob state =
                    _mobs[i];

                if (state == null ||
                    state.Root == null)
                {
                    continue;
                }

                if (!state.Dead)
                {
                    UnityEngine.Object.Destroy(
                        state.Root);
                }
            }

            _mobs.RemoveAll(
                state =>
                    state == null ||
                    state.Root == null ||
                    !state.Dead);
        }


        private static void DestroyEncounter()
        {
            for (int i = 0;
                 i < _mobs.Count;
                 i++)
            {
                EncounterMob state =
                    _mobs[i];

                if (state != null &&
                    state.Root != null)
                {
                    UnityEngine.Object.Destroy(
                        state.Root);
                }
            }

            _mobs.Clear();
        }


        private static void DestroyTemplate()
        {
            if (_lavaFiendTemplate == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(
                _lavaFiendTemplate);

            _lavaFiendTemplate =
                null;
        }


        private sealed class EncounterMob
        {
            internal EncounterDefinition Definition;
            internal GameObject Root;
            internal NPC Npc;
            internal bool Dead;
        }


        // ============================================================
        // CHAT
        // ============================================================

        private static void AddSystemMessage(
            string text,
            string color)
        {
            try
            {
                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        text,
                        ChatLogLine.LogType.SystemMessages,
                        color ?? ""));
            }
            catch
            {
            }
        }


        private static void AddLocalMessage(
            string text,
            string color)
        {
            try
            {
                UpdateSocialLog.LocalLogAdd(
                    text,
                    color ?? "white");
            }
            catch
            {
            }
        }
    }
}
