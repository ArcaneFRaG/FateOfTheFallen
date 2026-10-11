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
    /// Permanent world placement for Varkesh in Willowwatch Ridge.
    ///
    /// Visual/AI source:
    ///     native "Sivakayan Templar"
    ///
    /// Placement:
    ///     scene Willowwatch
    ///     X 728.29, Y 49.87, Z 560.93
    ///
    /// This location is normally occupied by a native "Sylvan Giant Runt".
    /// The local SpawnPoint is disabled while FOTF content is active so the
    /// native NPC cannot overlap Varkesh.
    ///
    /// Encounter mechanics will live in a separate Varkesh encounter runtime;
    /// this class owns native-template resolution, permanent placement,
    /// displaced-native suppression, identity/baseline stats and the standard
    /// 44-minute world-boss respawn.
    /// </summary>
    internal static class VarkeshWorldSpawnRuntime
    {
        internal const string SpawnSceneName =
            "Willowwatch";

        internal const string NativeTemplateNpcName =
            "Sivakayan Templar";

        internal const string GraveMarchTemplateNpcName =
            "Waking Nightmare";

        internal const string DisplacedNativeNpcName =
            "Sylvan Giant Runt";

        internal const string BossName =
            "Varkesh the Unburied";

        internal static readonly Vector3 SpawnPosition =
            new Vector3(
                728.29f,
                49.87f,
                560.93f);

        private static readonly Quaternion SpawnRotation =
            Quaternion.identity;

        // Only suppress the native occupant/spawn point local to Varkesh's
        // chosen position. Other Sylvan Giant Runts in Willowwatch are left
        // completely untouched.
        private const float NativeSuppressionRadius =
            20f;

        private const int InitialSettleFrames =
            180;

        private const int TemplateResolveRetryFrames =
            600;

        private const int TemplateResolveStrideFrames =
            30;

        // The Sivakayan Templar prefab carries its own authored scale, so a
        // simple localScale multiplier is not a reliable statement of world
        // size. Enforce the boss against its actual renderer bounds instead.
        private const float MinimumBossVisualHeight =
            7.0f;

        private const int BossScaleStabilizeFrames =
            45;

        private static bool _initialized;
        private static bool _spawnRequested;
        private static bool _dead;
        private static bool _loadAllAttempted;
        private static bool _graveMarchLoadAllAttempted;
        private static int _generation;
        // One delayed component-only donor probe per scene instance, not once
        // per watchdog spawn attempt. Prevents repeated broad resident scans.
        private static int _residentTemplarProbeSceneHandle = int.MinValue;

        // Permanent world-boss watchdog. This is intentionally independent
        // of Quest 9 state: Varkesh remains a farmable open-world boss after
        // the story quest is turned in. The watchdog only repairs a missing
        // live spawn; it never bypasses the normal 44-minute death timer.
        private static Coroutine _permanentSpawnWatchdog;

        private static DateTime _respawnAtUtc =
            DateTime.MinValue;

        private static GameObject _nativeTemplate;
        private static GameObject _graveMarchTemplate;
        private static GameObject _graveMarchSpawnPointTemplate;
        private static GameObject _bossObject;
        private static NPC _bossNpc;
        private static Character _bossCharacter;

        private sealed class SuppressedSpawnPoint
        {
            internal SpawnPoint SpawnPoint;
            internal bool WasEnabled;
        }

        private static readonly List<SuppressedSpawnPoint>
            SuppressedNativeSpawnPoints =
                new List<SuppressedSpawnPoint>();


        internal static NPC CurrentNpc
        {
            get
            {
                return _bossNpc;
            }
        }


        internal static bool NativeTemplateReady
        {
            get
            {
                return _nativeTemplate != null;
            }
        }


        /// <summary>
        /// Consumes the one shared safe-startup Resources.LoadAll result.
        /// This method never performs its own global Resources sweep. It first
        /// prefers a SpawnPoint that references the Sivakayan Templar, then
        /// falls back to a direct NPC prefab contained in the returned roots.
        /// </summary>
        internal static bool PrimeNativeTemplateFromResourceRoots(
            GameObject[] resourceRoots,
            string origin)
        {
            if (_nativeTemplate != null)
            {
                return true;
            }

            if (resourceRoots == null ||
                resourceRoots.Length == 0)
            {
                return false;
            }

            // Prefer a SpawnPoint-authored reference when available. This
            // preserves Erenshor's original Templar prefab relationship.
            for (int i = 0;
                 i < resourceRoots.Length;
                 i++)
            {
                GameObject root =
                    resourceRoots[i];

                if (root == null)
                {
                    continue;
                }

                SpawnPoint[] spawnPoints =
                    null;

                try
                {
                    spawnPoints =
                        root.GetComponentsInChildren<SpawnPoint>(
                            true);
                }
                catch
                {
                }

                for (int s = 0;
                     spawnPoints != null &&
                     s < spawnPoints.Length;
                     s++)
                {
                    NPC referenced =
                        FindReferencedNpcByName(
                            spawnPoints[s],
                            NativeTemplateNpcName);

                    if (referenced != null &&
                        CacheNativeTemplate(
                            referenced,
                            (origin ?? "safe startup resource prime") +
                            " SpawnPoint reference"))
                    {
                        return true;
                    }
                }
            }

            // Direct NPC prefab/resource fallback.
            for (int i = 0;
                 i < resourceRoots.Length;
                 i++)
            {
                GameObject root =
                    resourceRoots[i];

                if (root == null)
                {
                    continue;
                }

                NPC[] candidates =
                    null;

                try
                {
                    candidates =
                        root.GetComponentsInChildren<NPC>(
                            true);
                }
                catch
                {
                }

                if (TryCacheFromCandidates(
                        candidates,
                        origin ??
                        "safe startup resource prime"))
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>Per-root version of the proven r26 native prefab search.
        /// This allows the shared r31 loader to yield between resource roots.</summary>
        internal static bool PrimeNativeTemplateFromNpc(NPC npc, string origin)
        {
            return _nativeTemplate != null ||
                (npc != null && CacheNativeTemplate(npc, origin));
        }

        internal static bool PrimeNativeTemplateFromSpawnPoint(
            SpawnPoint point, string origin)
        {
            if (_nativeTemplate != null) return true;
            NPC npc = FindReferencedNpcByName(point, NativeTemplateNpcName);
            return npc != null && CacheNativeTemplate(npc, origin);
        }

        internal static bool PrimeNativeTemplateFromResourceRoot(
            GameObject root, bool checkSpawnPoints, string origin)
        {
            if (_nativeTemplate != null) return true;
            if (root == null) return false;

            if (!checkSpawnPoints)
            {
                NPC direct = FindNamedNpcInRoot(root, NativeTemplateNpcName);
                return direct != null && CacheNativeTemplate(direct, origin);
            }

            SpawnPoint[] points = null;
            try { points = root.GetComponentsInChildren<SpawnPoint>(true); }
            catch { }
            for (int i = 0; points != null && i < points.Length; i++)
            {
                NPC donor = FindReferencedNpcByName(points[i], NativeTemplateNpcName);
                if (donor != null && CacheNativeTemplate(donor, origin)) return true;
            }
            return false;
        }


        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized =
                true;

            Plugin.NativeLog.LogInfo(
                "[Varkesh] runtime mode=[PRE-UNIFIED NATIVE TEMPLAR] SourceBuild=[" +
                Plugin.SourceBuildId +
                "]. CustomCreatureRuntime is NOT used for Varkesh.");

            SceneManager.sceneLoaded +=
                OnSceneLoaded;

            SceneManager.activeSceneChanged +=
                OnActiveSceneChanged;

            if (_permanentSpawnWatchdog == null)
            {
                _permanentSpawnWatchdog =
                    CoroutineHost.Start(
                        PermanentSpawnWatchdog());
            }

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (_nativeTemplate == null &&
                activeScene.IsValid() &&
                activeScene.isLoaded)
            {
                CoroutineHost.Start(
                    CaptureTemplarSourceAfterSceneSettle(
                        activeScene));
            }

            TryStartForScene(
                activeScene,
                "runtime initialize");
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

            SceneManager.activeSceneChanged -=
                OnActiveSceneChanged;

            if (_permanentSpawnWatchdog != null)
            {
                CoroutineHost.Stop(
                    _permanentSpawnWatchdog);

                _permanentSpawnWatchdog =
                    null;
            }

            _generation++;

            _spawnRequested =
                false;

            _dead =
                false;

            _respawnAtUtc =
                DateTime.MinValue;

            VarkeshEncounterRuntime.Stop(
                "Varkesh world-spawn shutdown");

            RestoreSuppressedNativeSpawnPoints();
            DestroyCurrentBoss();
            DestroyTemplate();
            DestroyGraveMarchTemplate();
        }


        internal static void NotifyVarkeshDeath(
            string source)
        {
            if (!_initialized ||
                _dead)
            {
                return;
            }

            _dead =
                true;

            _spawnRequested =
                false;

            VarkeshEncounterRuntime.Stop(
                "Varkesh defeated");

            _respawnAtUtc =
                DateTime.UtcNow.AddSeconds(
                    WorldBossCatalog
                        .Varkesh
                        .RespawnDelaySeconds);

            if (_bossCharacter != null)
            {
                VarkeshQuestDeathBridge.TrySchedule(
                    _bossCharacter);
            }

            Scene scene =
                SceneManager.GetActiveScene();

            if (IsSpawnScene(
                    scene))
            {
                int generation =
                    ++_generation;

                CoroutineHost.Start(
                    WaitForRespawn(
                        scene,
                        generation));
            }

            Plugin.NativeLog.LogInfo(
                "[Varkesh] defeated. Willowwatch respawn armed for 44 minutes. Source=[" +
                (source ?? "<unknown>") +
                "].");
        }


        /// <summary>
        /// Reasserts Varkesh's permanent open-world availability without ever
        /// consulting Quest 9 state. If Varkesh is legitimately dead, the
        /// normal 44-minute respawn remains authoritative. This also recovers
        /// from unexpected native/scene cleanup that destroys the boss object
        /// after the quest has already been completed.
        /// </summary>
        internal static void EnsurePermanentAvailability(
            string source)
        {
            if (!_initialized ||
                !WorldBossPersistencePolicy.CanExistInWorld)
            {
                return;
            }

            Scene scene =
                SceneManager.GetActiveScene();

            if (!IsSpawnScene(
                    scene))
            {
                return;
            }

            if (_dead &&
                DateTime.UtcNow <
                    _respawnAtUtc)
            {
                return;
            }

            if (_dead)
            {
                _dead =
                    false;

                _respawnAtUtc =
                    DateTime.MinValue;
            }

            if (_bossObject != null &&
                _bossNpc != null &&
                _bossCharacter != null)
            {
                bool alive =
                    _bossCharacter.Alive &&
                    (_bossCharacter.MyStats == null ||
                     _bossCharacter.MyStats.CurrentHP > 0);

                if (alive)
                {
                    return;
                }

                // If native death/corpse processing beat the normal monitor
                // callback, arm the permanent respawn timer here rather than
                // leaving a dead quest boss object blocking future spawns.
                NotifyVarkeshDeath(
                    "permanent watchdog observed dead Varkesh");

                return;
            }

            if (_spawnRequested)
            {
                return;
            }

            TryStartForScene(
                scene,
                source ??
                "permanent availability check");
        }


        private static IEnumerator PermanentSpawnWatchdog()
        {
            WaitForSecondsRealtime delay =
                new WaitForSecondsRealtime(
                    2f);

            while (_initialized)
            {
                EnsurePermanentAvailability(
                    "permanent Varkesh watchdog");

                yield return delay;
            }

            _permanentSpawnWatchdog =
                null;
        }


        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            // Never enumerate scene hierarchies/resources inside Unity's
            // sceneLoaded callback. This callback runs on the main thread while
            // a zone is still settling and was the source of the visible
            // transition stalls.
            if (_graveMarchSpawnPointTemplate == null &&
                _graveMarchTemplate == null)
            {
                CoroutineHost.Start(
                    CaptureGraveMarchSourceAfterSceneSettle(
                        scene));
            }

            if (_nativeTemplate == null)
            {
                CoroutineHost.Start(
                    CaptureTemplarSourceAfterSceneSettle(
                        scene));
            }

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (IsSpawnScene(
                    activeScene))
            {
                TryStartForScene(
                    activeScene,
                    "sceneLoaded");
            }
        }


        private static IEnumerator CaptureTemplarSourceAfterSceneSettle(
            Scene scene)
        {
            // Wait for native SpawnPoint publication, then inspect only the
            // newly loaded scene.  This replaces the old need to globally load
            // every Resources GameObject just to discover Sivakayan Templar.
            for (int frame = 0;
                 frame < 6;
                 frame++)
            {
                yield return null;
            }

            if (!_initialized ||
                _nativeTemplate != null ||
                !scene.IsValid() ||
                !scene.isLoaded)
            {
                yield break;
            }

            Scene active =
                SceneManager.GetActiveScene();

            if (active.IsValid() &&
                active.handle == scene.handle)
            {
                NPC published =
                    TryFindReferencedNpcInCurrentSpawnPoints(
                        NativeTemplateNpcName);

                if (published != null &&
                    CacheNativeTemplate(
                        published,
                        "scene-local published SpawnPoint"))
                {
                    yield break;
                }
            }

            GameObject[] roots =
                null;

            try
            {
                roots =
                    scene.GetRootGameObjects();
            }
            catch
            {
            }

            for (int i = 0;
                 roots != null &&
                 i < roots.Length;
                 i++)
            {
                NPC source =
                    FindNamedNpcInRoot(
                        roots[i],
                        NativeTemplateNpcName);

                if (source != null &&
                    CacheNativeTemplate(
                        source,
                        "scene-local root capture [" +
                        scene.name +
                        "]"))
                {
                    yield break;
                }
            }

            // A newly-loaded scene may also have brought a native AssetBundle
            // resident. Allow exactly one new targeted bundle/Resources-path
            // attempt for this scene. TryResolveNativeTemplate never performs
            // Resources.LoadAll.
            _loadAllAttempted =
                false;

            TryResolveNativeTemplate();
        }


        private static IEnumerator CaptureGraveMarchSourceAfterSceneSettle(
            Scene scene)
        {
            // Let native SpawnPointManager finish publishing the new scene's
            // spawn points before the cheap scoped scan.
            for (int frame = 0;
                 frame < 4;
                 frame++)
            {
                yield return null;
            }

            if (!_initialized ||
                !scene.IsValid() ||
                !scene.isLoaded ||
                _graveMarchSpawnPointTemplate != null ||
                _graveMarchTemplate != null)
            {
                yield break;
            }

            float started =
                Time.realtimeSinceStartup;

            TryCaptureGraveMarchSourceFromScene(
                scene);

            float elapsedMs =
                (Time.realtimeSinceStartup -
                 started) *
                1000f;

            if (elapsedMs >=
                8f)
            {
                Plugin.NativeLog.LogInfo(
                    "[Varkesh] deferred scene-local Grave March scan [" +
                    scene.name +
                    "] took [" +
                    elapsedMs.ToString("0.0") +
                    "ms].");
            }
        }


        private static void OnActiveSceneChanged(
            Scene previous,
            Scene next)
        {
            _generation++;

            _spawnRequested =
                false;

            if (!IsSpawnScene(
                    next))
            {
                VarkeshEncounterRuntime.Stop(
                    "left Willowwatch");

                RestoreSuppressedNativeSpawnPoints();
                DestroyCurrentBoss();
                return;
            }

            TryStartForScene(
                next,
                "Willowwatch became active");
        }


        private static void TryStartForScene(
            Scene scene,
            string reason)
        {
            if (!_initialized ||
                !WorldBossPersistencePolicy.CanExistInWorld ||
                !IsSpawnScene(
                    scene))
            {
                return;
            }

            SuppressNativeRuntAtVarkeshLocation();

            if (_bossObject != null &&
                _bossNpc != null)
            {
                return;
            }

            if (_dead &&
                DateTime.UtcNow <
                    _respawnAtUtc)
            {
                int generation =
                    ++_generation;

                CoroutineHost.Start(
                    WaitForRespawn(
                        scene,
                        generation));

                return;
            }

            _dead =
                false;

            _respawnAtUtc =
                DateTime.MinValue;

            if (_spawnRequested)
            {
                return;
            }

            _spawnRequested =
                true;

            int spawnGeneration =
                ++_generation;

            CoroutineHost.Start(
                SpawnWhenWorldReady(
                    scene,
                    spawnGeneration,
                    reason));
        }


        private static IEnumerator SpawnWhenWorldReady(
            Scene scene,
            int generation,
            string reason)
        {
            for (int frame = 0;
                 frame < InitialSettleFrames;
                 frame++)
            {
                if (!CanContinue(
                        scene,
                        generation))
                {
                    yield break;
                }

                // The native occupant can be created part-way through normal
                // Willowwatch initialization. Re-run the local suppression a
                // few times during settle rather than racing the SpawnPoint.
                if (frame == 30 ||
                    frame == 90 ||
                    frame == 150)
                {
                    SuppressNativeRuntAtVarkeshLocation();
                }

                yield return null;
            }

            SuppressNativeRuntAtVarkeshLocation();

            Plugin.NativeLog.LogInfo(
                "[Varkesh] using native Templar template resolver for Willowwatch spawn.");

            bool templateReady =
                TryResolveNativeTemplate();

            if (!templateReady)
            {
                NativeWorldBossVisualRegistry.QueueLateBossDonorRecovery(
                    "Varkesh missing Sivakayan Templar in Willowwatch");
            }

            for (int frame = 0;
                 !templateReady &&
                 frame < TemplateResolveRetryFrames;
                 frame++)
            {
                if (!CanContinue(
                        scene,
                        generation))
                {
                    yield break;
                }

                if (frame %
                    TemplateResolveStrideFrames ==
                    0)
                {
                    templateReady =
                        TryResolveNativeTemplate();
                }

                // A cold session may have the native Templar as an inactive
                // resident NPC component rather than a published SpawnPoint.
                // Do ONE delayed component-only probe after the scene has
                // settled. Never call Resources.LoadAll<GameObject>("") here;
                // that global asset load was the r29 startup freeze.
                if (!templateReady && frame == InitialSettleFrames)
                {
                    templateReady =
                        TryResolveTemplarFromResidentComponents();
                }

                yield return null;
            }

            if (!CanContinue(
                    scene,
                    generation))
            {
                yield break;
            }

            if (!templateReady)
            {
                _spawnRequested =
                    false;

                Plugin.NativeLog.LogWarning(
                    "[Varkesh] could not resolve native [" +
                    NativeTemplateNpcName +
                    "] template. Willowwatch spawn deferred.");

                yield break;
            }

            SpawnVarkesh(
                scene);

            for (int frame = 0;
                 frame < 180;
                 frame++)
            {
                if (!CanContinue(
                        scene,
                        generation))
                {
                    yield break;
                }

                if (_bossObject != null &&
                    _bossNpc != null &&
                    _bossCharacter != null)
                {
                    _spawnRequested =
                        false;

                    Plugin.NativeLog.LogInfo(
                        "[Varkesh] permanent spawn active in Willowwatch at [" +
                        SpawnPosition +
                        "] using native [" +
                        NativeTemplateNpcName +
                        "] model. Reason=[" +
                        (reason ?? "<unknown>") +
                        "].");

                    CoroutineHost.Start(
                        MonitorBoss(
                            scene,
                            generation));

                    CoroutineHost.Start(
                        PrimeGraveMarchSourceDeferred(
                            scene,
                            generation));

                    yield break;
                }

                yield return null;
            }

            _spawnRequested =
                false;

            Plugin.NativeLog.LogWarning(
                "[Varkesh] native Templar clone did not settle into a valid boss instance.");
        }


        private static IEnumerator PrimeGraveMarchSourceDeferred(
            Scene scene,
            int generation)
        {
            if (_graveMarchSpawnPointTemplate != null ||
                _graveMarchTemplate != null)
            {
                yield break;
            }

            // Keep all discovery work well away from the sceneLoaded callback.
            // The boss itself is already settled at this point.
            for (int frame = 0;
                 frame < 120;
                 frame++)
            {
                if (!CanContinue(
                        scene,
                        generation))
                {
                    yield break;
                }

                yield return null;
            }

            float started =
                Time.realtimeSinceStartup;

            TryResolveGraveMarchTemplate();

            float elapsedMs =
                (Time.realtimeSinceStartup -
                 started) *
                1000f;

            Plugin.NativeLog.LogInfo(
                "[Varkesh] deferred Grave March source prime complete. Ready=[" +
                (_graveMarchSpawnPointTemplate != null ||
                 _graveMarchTemplate != null) +
                "] Elapsed=[" +
                elapsedMs.ToString("0.0") +
                "ms].");
        }


        private static IEnumerator MonitorBoss(
            Scene scene,
            int generation)
        {
            int suppressionRefresh =
                0;

            while (CanContinue(
                       scene,
                       generation))
            {
                if (_bossCharacter == null ||
                    _bossObject == null)
                {
                    yield break;
                }

                bool dead =
                    !_bossCharacter.Alive ||
                    (_bossCharacter.MyStats != null &&
                     _bossCharacter.MyStats.CurrentHP <= 0);

                if (dead)
                {
                    NotifyVarkeshDeath(
                        "Varkesh world-spawn monitor");

                    yield break;
                }

                // Cheap defensive check every ~2 seconds. This catches a late
                // native SpawnPoint respawn without doing per-frame discovery.
                suppressionRefresh++;

                if (suppressionRefresh >= 120)
                {
                    suppressionRefresh =
                        0;

                    SuppressNativeRuntAtVarkeshLocation();
                }

                yield return null;
            }
        }


        private static IEnumerator WaitForRespawn(
            Scene scene,
            int generation)
        {
            while (CanContinue(
                       scene,
                       generation) &&
                   DateTime.UtcNow <
                       _respawnAtUtc)
            {
                yield return null;
            }

            if (!CanContinue(
                    scene,
                    generation))
            {
                yield break;
            }

            _dead =
                false;

            _respawnAtUtc =
                DateTime.MinValue;

            DestroyCurrentBoss();

            _spawnRequested =
                false;

            TryStartForScene(
                scene,
                "44-minute respawn timer elapsed");
        }


        private static bool CanContinue(
            Scene scene,
            int generation)
        {
            return
                _initialized &&
                generation == _generation &&
                WorldBossPersistencePolicy.CanExistInWorld &&
                scene.IsValid() &&
                scene.isLoaded &&
                IsSpawnScene(
                    SceneManager.GetActiveScene());
        }


        private static bool IsSpawnScene(
            Scene scene)
        {
            return
                scene.IsValid() &&
                scene.isLoaded &&
                string.Equals(
                    scene.name,
                    SpawnSceneName,
                    StringComparison.OrdinalIgnoreCase);
        }


        // ============================================================
        // NATIVE TEMPLAR TEMPLATE
        // ============================================================

        private static bool TryResolveNativeTemplate()
        {
            if (_nativeTemplate != null)
            {
                return true;
            }

            // Willowwatch is a hot transition path. Never perform a global
            // Resources.FindObjectsOfTypeAll/LoadAll sweep here. First use the
            // already-published scene spawn points, then do one targeted asset
            // lookup by the exact donor name. If it is not resident yet, defer
            // the boss rather than freezing the game.
            NPC sceneSource =
                TryFindReferencedNpcInCurrentSpawnPoints(
                    NativeTemplateNpcName);

            if (sceneSource != null &&
                CacheNativeTemplate(
                    sceneSource,
                    "SpawnPointManager exact donor"))
            {
                return true;
            }

            if (!_loadAllAttempted)
            {
                _loadAllAttempted = true;

                NPC targetedSource;
                string origin;

                if (TryResolveNamedNpcFromLoadedAssetBundles(
                        NativeTemplateNpcName,
                        out targetedSource,
                        out origin) &&
                    CacheNativeTemplate(
                        targetedSource,
                        origin))
                {
                    return true;
                }

                if (TryResolveNamedNpcFromExactResources(
                        NativeTemplateNpcName,
                        out targetedSource,
                        out origin) &&
                    CacheNativeTemplate(
                        targetedSource,
                        origin))
                {
                    return true;
                }

                Plugin.NativeLog.LogWarning(
                    "[Varkesh] targeted native donor lookup did not find [" +
                    NativeTemplateNpcName +
                    "]. Willowwatch spawn will defer; no global Resources sweep was used.");
            }

            return false;
        }


        private static bool TryResolveTemplarFromResidentComponents()
        {
            if (_nativeTemplate != null)
                return true;

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || !IsSpawnScene(scene))
                return false;
            if (_residentTemplarProbeSceneHandle == scene.handle)
                return false;
            _residentTemplarProbeSceneHandle = scene.handle;

            try
            {
                NPC[] residents = Resources.FindObjectsOfTypeAll<NPC>();
                bool found = TryCacheFromCandidates(
                    residents, "delayed already-resident NPC probe");
                Plugin.NativeLog.LogInfo(
                    "[Varkesh] delayed resident Templar lookup. Cached=[" +
                    found + "] NPCComponents=[" +
                    (residents != null ? residents.Length : 0) + "].");
                return found;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Varkesh] deferred resident Templar lookup failed: " +
                    exception.Message);
                return false;
            }
        }


        private static NPC TryFindReferencedNpcInCurrentSpawnPoints(
            string npcName)
        {
            if (string.IsNullOrEmpty(npcName) ||
                SpawnPointManager.SpawnPointsInScene == null)
            {
                return null;
            }

            try
            {
                foreach (SpawnPoint spawnPoint in
                         SpawnPointManager.SpawnPointsInScene)
                {
                    NPC referenced =
                        FindReferencedNpcByName(
                            spawnPoint,
                            npcName);

                    if (referenced != null)
                    {
                        return referenced;
                    }
                }
            }
            catch
            {
            }

            return null;
        }


        private static bool TryResolveNamedNpcFromLoadedAssetBundles(
            string npcName,
            out NPC source,
            out string origin)
        {
            source = null;
            origin = null;

            try
            {
                IEnumerable<AssetBundle> bundles =
                    AssetBundle.GetAllLoadedAssetBundles();

                if (bundles == null)
                    return false;

                foreach (AssetBundle bundle in bundles)
                {
                    if (bundle == null)
                        continue;

                    string[] assetNames;

                    try
                    {
                        assetNames = bundle.GetAllAssetNames();
                    }
                    catch
                    {
                        continue;
                    }

                    for (int i = 0;
                         assetNames != null && i < assetNames.Length;
                         i++)
                    {
                        string assetName = assetNames[i];

                        if (!AssetPathMatchesNpcName(
                                assetName,
                                npcName))
                        {
                            continue;
                        }

                        GameObject root = null;

                        try
                        {
                            root = bundle.LoadAsset<GameObject>(assetName);
                        }
                        catch
                        {
                            continue;
                        }

                        source =
                            FindNamedNpcInRoot(
                                root,
                                npcName);

                        if (source == null)
                            continue;

                        origin =
                            "loaded AssetBundle [" +
                            bundle.name +
                            "] asset [" +
                            assetName +
                            "]";

                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Varkesh] targeted loaded-AssetBundle donor lookup failed: " +
                    exception.Message);
            }

            return false;
        }


        private static bool TryResolveNamedNpcFromExactResources(
            string npcName,
            out NPC source,
            out string origin)
        {
            source = null;
            origin = null;

            string[] prefixes =
            {
                string.Empty,
                "NPC/",
                "NPCs/",
                "Enemies/",
                "Monsters/",
                "Creatures/",
                "Characters/",
                "Prefabs/",
                "Prefabs/NPC/",
                "Prefabs/NPCs/",
                "Prefabs/Enemies/",
                "Prefabs/Monsters/"
            };

            string underscored =
                npcName.Replace(' ', '_');

            for (int p = 0; p < prefixes.Length; p++)
            {
                string[] names =
                {
                    prefixes[p] + npcName,
                    prefixes[p] + underscored
                };

                for (int n = 0; n < names.Length; n++)
                {
                    GameObject root = null;

                    try
                    {
                        root = Resources.Load<GameObject>(names[n]);
                    }
                    catch
                    {
                        continue;
                    }

                    source =
                        FindNamedNpcInRoot(
                            root,
                            npcName);

                    if (source == null)
                        continue;

                    origin =
                        "exact Resources path [" +
                        names[n] +
                        "]";

                    return true;
                }
            }

            return false;
        }


        private static bool AssetPathMatchesNpcName(
            string assetPath,
            string npcName)
        {
            if (string.IsNullOrEmpty(assetPath) ||
                string.IsNullOrEmpty(npcName))
            {
                return false;
            }

            string normalizedPath =
                assetPath
                    .Replace('_', ' ')
                    .Replace('-', ' ')
                    .Replace('/', ' ')
                    .ToLowerInvariant();

            string[] words =
                npcName
                    .ToLowerInvariant()
                    .Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length < 3)
                    continue;

                if (normalizedPath.IndexOf(
                        words[i],
                        StringComparison.Ordinal) < 0)
                {
                    return false;
                }
            }

            return true;
        }


        private static NPC FindNamedNpcInRoot(
            GameObject root,
            string npcName)
        {
            if (root == null)
                return null;

            NPC direct = root.GetComponent<NPC>();
            if (IsExactNpc(direct, npcName))
                return direct;

            NPC[] candidates =
                root.GetComponentsInChildren<NPC>(true);

            for (int i = 0;
                 candidates != null && i < candidates.Length;
                 i++)
            {
                if (IsExactNpc(candidates[i], npcName))
                    return candidates[i];
            }

            return null;
        }


        private static bool TryCacheFromCandidates(
            NPC[] candidates,
            string origin)
        {
            if (candidates == null)
            {
                return false;
            }

            // Prefer prefab/resource objects over live scene instances.
            for (int pass = 0;
                 pass < 2;
                 pass++)
            {
                for (int i = 0;
                     i < candidates.Length;
                     i++)
                {
                    NPC candidate =
                        candidates[i];

                    if (!IsExactNpc(
                            candidate,
                            NativeTemplateNpcName))
                    {
                        continue;
                    }

                    bool isSceneObject =
                        candidate.gameObject.scene.IsValid();

                    if (pass == 0 &&
                        isSceneObject)
                    {
                        continue;
                    }

                    if (pass == 1 &&
                        !isSceneObject)
                    {
                        continue;
                    }

                    if (CacheNativeTemplate(
                            candidate,
                            origin))
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        private static bool CacheNativeTemplate(
            NPC source,
            string origin)
        {
            if (!IsExactNpc(
                    source,
                    NativeTemplateNpcName))
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
                    "FOTF_Varkesh_NativeTemplate";

                clone.transform.SetParent(
                    null,
                    true);

                clone.SetActive(
                    false);

                UnityEngine.Object.DontDestroyOnLoad(
                    clone);

                _nativeTemplate =
                    clone;

                Plugin.NativeLog.LogInfo(
                    "[Varkesh] cached native [" +
                    NativeTemplateNpcName +
                    "] template from [" +
                    (origin ?? "<unknown>") +
                    "].");

                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Varkesh] failed caching native [" +
                    NativeTemplateNpcName +
                    "] template: " +
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


        /// <summary>
        /// Returns an inactive clone of the already-cached native Sivakayan
        /// Templar template for encounter-owned adds. The caller owns the clone.
        /// </summary>
        internal static GameObject CreateTemplarCloneForEncounter()
        {
            if (_nativeTemplate == null &&
                !TryResolveNativeTemplate())
            {
                return null;
            }

            if (_nativeTemplate == null)
            {
                return null;
            }

            try
            {
                GameObject clone =
                    UnityEngine.Object.Instantiate(
                        _nativeTemplate);

                if (clone != null)
                {
                    clone.SetActive(
                        false);
                }

                return clone;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Varkesh] failed cloning native Templar template for encounter add: " +
                    exception.Message);

                return null;
            }
        }


        private static bool TryResolveGraveMarchTemplate()
        {
            if (_graveMarchSpawnPointTemplate != null ||
                _graveMarchTemplate != null)
            {
                return true;
            }

            TryCaptureGraveMarchFromLoadedObjects(
                "loaded objects");

            if (_graveMarchSpawnPointTemplate != null ||
                _graveMarchTemplate != null)
            {
                return true;
            }

            if (!_graveMarchLoadAllAttempted)
            {
                // Legacy name retained for save/source compatibility; this no
                // longer performs Resources.LoadAll. It is now only a one-time
                // search of objects Unity has already loaded.
                _graveMarchLoadAllAttempted =
                    true;

                float started =
                    Time.realtimeSinceStartup;

                TryCaptureGraveMarchFromAlreadyLoadedResources(
                    "one-time Grave March discovery");

                float elapsedMs =
                    (Time.realtimeSinceStartup -
                     started) *
                    1000f;

                if (elapsedMs >=
                    8f)
                {
                    Plugin.NativeLog.LogInfo(
                        "[Varkesh] one-time already-loaded Grave March discovery took [" +
                        elapsedMs.ToString("0.0") +
                        "ms].");
                }
            }

            if (_graveMarchSpawnPointTemplate != null)
            {
                Plugin.NativeLog.LogInfo(
                    "[Varkesh] Grave March source ready via native Waking Nightmare SpawnPoint.");

                return true;
            }

            if (_graveMarchTemplate != null)
            {
                Plugin.NativeLog.LogInfo(
                    "[Varkesh] Grave March source ready via direct native Waking Nightmare NPC fallback.");

                return true;
            }

            Plugin.NativeLog.LogWarning(
                "[Varkesh] could not resolve already-loaded native [" +
                GraveMarchTemplateNpcName +
                "] or its SpawnPoint for Grave March. No synchronous Resources.LoadAll fallback was used.");

            return false;
        }


        private static void TryCaptureGraveMarchSourceFromScene(
            Scene scene)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                _graveMarchSpawnPointTemplate != null ||
                _graveMarchTemplate != null)
            {
                return;
            }

            // Scene-transition discovery is intentionally limited to the
            // already-published SpawnPointManager list. No hierarchy walk, no
            // Object.FindObjectsOfType and no Resources scan is allowed here.
            // Waking Nightmare is expected to be authored through a SpawnPoint;
            // the direct NPC/resource fallback remains available later through
            // deferred/on-demand resolution.
            try
            {
                if (SpawnPointManager.SpawnPointsInScene ==
                        null ||
                    SceneManager.GetActiveScene() !=
                        scene)
                {
                    return;
                }

                foreach (SpawnPoint candidate in
                         SpawnPointManager.SpawnPointsInScene)
                {
                    if (candidate == null ||
                        !SpawnPointReferencesNpc(
                            candidate,
                            GraveMarchTemplateNpcName))
                    {
                        continue;
                    }

                    CacheGraveMarchSpawnPointTemplate(
                        candidate,
                        "deferred scene SpawnPointManager [" +
                        scene.name +
                        "]");

                    NPC referenced =
                        FindReferencedNpcByName(
                            candidate,
                            GraveMarchTemplateNpcName);

                    if (referenced != null)
                    {
                        CacheNamedNpcTemplate(
                            referenced,
                            ref _graveMarchTemplate,
                            "deferred scene SpawnPoint NPC [" +
                            scene.name +
                            "]");
                    }

                    return;
                }
            }
            catch
            {
            }
        }


        private static void TryCaptureGraveMarchFromLoadedObjects(
            string origin)
        {
            if (_graveMarchSpawnPointTemplate != null ||
                _graveMarchTemplate != null)
            {
                return;
            }

            // SpawnPointManager is already scoped to currently active scene
            // content and is far cheaper than Resources.FindObjectsOfTypeAll.
            try
            {
                if (SpawnPointManager.SpawnPointsInScene !=
                    null)
                {
                    foreach (SpawnPoint candidate in
                             SpawnPointManager.SpawnPointsInScene)
                    {
                        if (!SpawnPointReferencesNpc(
                                candidate,
                                GraveMarchTemplateNpcName))
                        {
                            continue;
                        }

                        if (CacheGraveMarchSpawnPointTemplate(
                                candidate,
                                origin +
                                " SpawnPointManager"))
                        {
                            NPC referenced =
                                FindReferencedNpcByName(
                                    candidate,
                                    GraveMarchTemplateNpcName);

                            if (referenced != null)
                            {
                                CacheNamedNpcTemplate(
                                    referenced,
                                    ref _graveMarchTemplate,
                                    origin +
                                    " SpawnPointManager NPC");
                            }

                            return;
                        }
                    }
                }
            }
            catch
            {
            }

            TryCaptureGraveMarchSourceFromScene(
                SceneManager.GetActiveScene());
        }


        private static void TryCaptureGraveMarchFromAlreadyLoadedResources(
            string origin)
        {
            if (_graveMarchSpawnPointTemplate != null ||
                _graveMarchTemplate != null)
            {
                return;
            }

            // Keep this fallback targeted. Global Resources.FindObjectsOfTypeAll
            // scans were large enough to hitch Willowwatch even when deferred.
            // We only inspect exact matching assets in already-loaded bundles
            // or exact Resources paths.
            NPC source;
            string sourceOrigin;

            if (TryResolveNamedNpcFromLoadedAssetBundles(
                    GraveMarchTemplateNpcName,
                    out source,
                    out sourceOrigin) ||
                TryResolveNamedNpcFromExactResources(
                    GraveMarchTemplateNpcName,
                    out source,
                    out sourceOrigin))
            {
                CacheNamedNpcTemplate(
                    source,
                    ref _graveMarchTemplate,
                    origin +
                    " targeted donor [" +
                    sourceOrigin +
                    "]");
            }
        }


        private static void TryCaptureGraveMarchFromResourceRoots(
            GameObject[] resources,
            string origin)
        {
            if (resources == null ||
                _graveMarchSpawnPointTemplate != null ||
                _graveMarchTemplate != null)
            {
                return;
            }

            // Piggyback on Varkesh's existing one-time native Templar resource
            // sweep. Do NOT perform a second Resources.LoadAll for Grave March.
            for (int i = 0;
                 i < resources.Length &&
                 _graveMarchSpawnPointTemplate == null;
                 i++)
            {
                GameObject root =
                    resources[i];

                if (root == null)
                {
                    continue;
                }

                SpawnPoint[] spawnPoints =
                    root.GetComponentsInChildren<SpawnPoint>(
                        true);

                for (int s = 0;
                     spawnPoints != null &&
                     s < spawnPoints.Length;
                     s++)
                {
                    SpawnPoint candidate =
                        spawnPoints[s];

                    if (!SpawnPointReferencesNpc(
                            candidate,
                            GraveMarchTemplateNpcName))
                    {
                        continue;
                    }

                    if (CacheGraveMarchSpawnPointTemplate(
                            candidate,
                            origin +
                            " SpawnPoint"))
                    {
                        break;
                    }
                }
            }

            if (_graveMarchSpawnPointTemplate != null)
            {
                return;
            }

            for (int i = 0;
                 i < resources.Length &&
                 _graveMarchTemplate == null;
                 i++)
            {
                GameObject root =
                    resources[i];

                if (root == null)
                {
                    continue;
                }

                NPC[] candidates =
                    root.GetComponentsInChildren<NPC>(
                        true);

                if (TryCacheNamedTemplateFromCandidates(
                        candidates,
                        GraveMarchTemplateNpcName,
                        ref _graveMarchTemplate,
                        origin +
                        " NPC"))
                {
                    break;
                }
            }
        }


        private static bool CacheGraveMarchSpawnPointTemplate(
            SpawnPoint source,
            string origin)
        {
            if (_graveMarchSpawnPointTemplate != null)
            {
                return true;
            }

            if (source == null ||
                !SpawnPointReferencesNpc(
                    source,
                    GraveMarchTemplateNpcName))
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
                    "FOTF_Varkesh_GraveMarch_WakingNightmare_SpawnPointTemplate";

                clone.transform.SetParent(
                    null,
                    true);

                clone.SetActive(
                    false);

                UnityEngine.Object.DontDestroyOnLoad(
                    clone);

                _graveMarchSpawnPointTemplate =
                    clone;

                Plugin.NativeLog.LogInfo(
                    "[Varkesh] cached native [" +
                    GraveMarchTemplateNpcName +
                    "] SpawnPoint for Grave March from [" +
                    (origin ?? "<unknown>") +
                    "].");

                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Varkesh] failed caching native [" +
                    GraveMarchTemplateNpcName +
                    "] SpawnPoint: " +
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


        private static bool TryCacheNamedTemplateFromCandidates(
            NPC[] candidates,
            string expectedName,
            ref GameObject cache,
            string origin)
        {
            if (cache != null)
            {
                return true;
            }

            if (candidates == null)
            {
                return false;
            }

            for (int pass = 0;
                 pass < 2;
                 pass++)
            {
                for (int i = 0;
                     i < candidates.Length;
                     i++)
                {
                    NPC candidate =
                        candidates[i];

                    if (!IsExactNpc(
                            candidate,
                            expectedName))
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

                    if (CacheNamedNpcTemplate(
                            candidate,
                            ref cache,
                            origin))
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        private static bool CacheNamedNpcTemplate(
            NPC source,
            ref GameObject cache,
            string origin)
        {
            if (source == null ||
                cache != null)
            {
                return
                    cache != null;
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
                    "FOTF_Varkesh_GraveMarch_" +
                    GraveMarchTemplateNpcName +
                    "_NpcTemplate";

                clone.transform.SetParent(
                    null,
                    true);

                clone.SetActive(
                    false);

                UnityEngine.Object.DontDestroyOnLoad(
                    clone);

                cache =
                    clone;

                Plugin.NativeLog.LogInfo(
                    "[Varkesh] cached direct native [" +
                    GraveMarchTemplateNpcName +
                    "] NPC fallback from [" +
                    (origin ?? "<unknown>") +
                    "].");

                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Varkesh] failed caching direct native [" +
                    GraveMarchTemplateNpcName +
                    "] NPC fallback: " +
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


        private static NPC FindReferencedNpcByName(
            SpawnPoint spawnPoint,
            string expectedName)
        {
            if (spawnPoint == null ||
                string.IsNullOrEmpty(
                    expectedName))
            {
                return null;
            }

            if (IsExactNpc(
                    spawnPoint.SpawnedNPC,
                    expectedName))
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

                for (int i = 0;
                     i < fields.Length;
                     i++)
                {
                    object value;

                    try
                    {
                        value =
                            fields[i].GetValue(
                                spawnPoint);
                    }
                    catch
                    {
                        continue;
                    }

                    NPC npc =
                        value as NPC;

                    if (IsExactNpc(
                            npc,
                            expectedName))
                    {
                        return npc;
                    }

                    GameObject gameObject =
                        value as GameObject;

                    if (gameObject != null)
                    {
                        NPC[] npcs =
                            gameObject.GetComponentsInChildren<NPC>(
                                true);

                        for (int n = 0;
                             npcs != null &&
                             n < npcs.Length;
                             n++)
                        {
                            if (IsExactNpc(
                                    npcs[n],
                                    expectedName))
                            {
                                return npcs[n];
                            }
                        }
                    }

                    Component component =
                        value as Component;

                    if (component != null)
                    {
                        NPC[] npcs =
                            component.GetComponentsInChildren<NPC>(
                                true);

                        for (int n = 0;
                             npcs != null &&
                             n < npcs.Length;
                             n++)
                        {
                            if (IsExactNpc(
                                    npcs[n],
                                    expectedName))
                            {
                                return npcs[n];
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }


        /// <summary>
        /// Preferred Grave March construction path. Returns an inactive clone
        /// of Waking Nightmare's native SpawnPoint so Erenshor itself creates
        /// the NPC when the object is activated.
        /// </summary>
        internal static GameObject CreateGraveMarchSpawnPointClone()
        {
            if (_graveMarchSpawnPointTemplate == null &&
                !TryResolveGraveMarchTemplate())
            {
                return null;
            }

            if (_graveMarchSpawnPointTemplate == null)
            {
                return null;
            }

            try
            {
                GameObject clone =
                    UnityEngine.Object.Instantiate(
                        _graveMarchSpawnPointTemplate);

                if (clone != null)
                {
                    clone.SetActive(
                        false);
                }

                return clone;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Varkesh] failed cloning native [" +
                    GraveMarchTemplateNpcName +
                    "] SpawnPoint for Grave March: " +
                    exception.Message);

                return null;
            }
        }


        /// <summary>
        /// Direct-NPC fallback used only when the native SpawnPoint cannot be
        /// resolved. SpawnPoint cloning is intentionally preferred.
        /// </summary>
        internal static GameObject CreateGraveMarchAddClone()
        {
            if (_graveMarchTemplate == null &&
                !TryResolveGraveMarchTemplate())
            {
                return null;
            }

            if (_graveMarchTemplate == null)
            {
                return null;
            }

            try
            {
                GameObject clone =
                    UnityEngine.Object.Instantiate(
                        _graveMarchTemplate);

                if (clone != null)
                {
                    clone.SetActive(
                        false);
                }

                return clone;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Varkesh] failed cloning direct native [" +
                    GraveMarchTemplateNpcName +
                    "] NPC fallback for Grave March: " +
                    exception.Message);

                return null;
            }
        }


        private static void DestroyGraveMarchTemplate()
        {
            if (_graveMarchSpawnPointTemplate != null)
            {
                UnityEngine.Object.Destroy(
                    _graveMarchSpawnPointTemplate);
            }

            if (_graveMarchTemplate != null)
            {
                UnityEngine.Object.Destroy(
                    _graveMarchTemplate);
            }

            _graveMarchSpawnPointTemplate =
                null;

            _graveMarchTemplate =
                null;

            _graveMarchLoadAllAttempted =
                false;
        }


        private static void DestroyTemplate()
        {
            if (_nativeTemplate != null)
            {
                UnityEngine.Object.Destroy(
                    _nativeTemplate);
            }

            _nativeTemplate =
                null;

            _loadAllAttempted =
                false;
        }


        // ============================================================
        // VARKESH INSTANCE
        // ============================================================

        private static void SpawnVarkesh(
            Scene scene)
        {
            if (_nativeTemplate == null ||
                _bossObject != null)
            {
                return;
            }

            GameObject clone =
                null;

            try
            {
                clone =
                    UnityEngine.Object.Instantiate(
                        _nativeTemplate);

                if (clone == null)
                {
                    return;
                }

                clone.name =
                    BossName;

                clone.transform.SetParent(
                    null,
                    true);

                clone.transform.SetPositionAndRotation(
                    ResolveSafeNavMeshPosition(
                        SpawnPosition),
                    SpawnRotation);

                clone.transform.localScale *=
                    WorldBossCatalog
                        .Varkesh
                        .ScaleMultiplier;

                SceneManager.MoveGameObjectToScene(
                    clone,
                    scene);

                clone.SetActive(
                    true);

                NormalizeBossVisualHeight(
                    clone,
                    "immediate spawn");

                NPC npc =
                    clone.GetComponent<NPC>();

                if (npc == null)
                {
                    npc =
                        clone.GetComponentInChildren<NPC>(
                            true);
                }

                if (npc == null)
                {
                    Plugin.NativeLog.LogError(
                        "[Varkesh] native Templar clone has no NPC component.");

                    UnityEngine.Object.Destroy(
                        clone);

                    return;
                }

                ConfigureBoss(
                    npc);

                _bossObject =
                    clone;

                _bossNpc =
                    npc;

                _bossCharacter =
                    npc.GetChar();

                if (_bossCharacter == null)
                {
                    _bossCharacter =
                        clone.GetComponent<Character>();
                }

                if (_bossNpc != null &&
                    _bossCharacter != null)
                {
                    VarkeshEncounterRuntime.AttachBoss(
                        _bossNpc,
                        SpawnPosition);

                    CoroutineHost.Start(
                        StabilizeBossVisualHeight(
                            clone));
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Varkesh] failed spawning native Templar boss: " +
                    exception);

                if (clone != null)
                {
                    UnityEngine.Object.Destroy(
                        clone);
                }

                _bossObject =
                    null;

                _bossNpc =
                    null;

                _bossCharacter =
                    null;
            }
        }


        private static IEnumerator StabilizeBossVisualHeight(
            GameObject bossRoot)
        {
            if (bossRoot == null)
            {
                yield break;
            }

            for (int frame = 0;
                 frame < BossScaleStabilizeFrames;
                 frame++)
            {
                if (bossRoot == null ||
                    bossRoot != _bossObject &&
                    _bossObject != null)
                {
                    yield break;
                }

                // Native modular characters can rebuild their visible parts on
                // activation. Re-check a handful of frames instead of assuming
                // the prefab's serialized root scale survives that setup.
                if (frame == 0 ||
                    frame == 1 ||
                    frame == 5 ||
                    frame == 15 ||
                    frame == 30 ||
                    frame == BossScaleStabilizeFrames - 1)
                {
                    NormalizeBossVisualHeight(
                        bossRoot,
                        "settle frame " +
                        frame);
                }

                yield return null;
            }
        }


        private static void NormalizeBossVisualHeight(
            GameObject bossRoot,
            string reason)
        {
            if (bossRoot == null)
            {
                return;
            }

            float currentHeight;

            if (!TryGetVisualHeight(
                    bossRoot,
                    out currentHeight) ||
                currentHeight <=
                0.05f)
            {
                return;
            }

            if (currentHeight >=
                MinimumBossVisualHeight)
            {
                return;
            }

            float correction =
                MinimumBossVisualHeight /
                currentHeight;

            // Avoid pathological single-frame explosions if a native renderer
            // reports incomplete bounds while the character is still being
            // assembled. Repeated settle passes will finish the correction.
            correction =
                Mathf.Clamp(
                    correction,
                    1f,
                    4f);

            bossRoot.transform.localScale *=
                correction;

            // Keep the feet/pivot back on the intended NavMesh position after
            // enlarging the native model.
            Vector3 safe =
                ResolveSafeNavMeshPosition(
                    SpawnPosition);

            NavMeshAgent nav =
                bossRoot.GetComponent<NavMeshAgent>();

            if (nav == null)
            {
                nav =
                    bossRoot.GetComponentInChildren<NavMeshAgent>(
                        true);
            }

            if (nav != null &&
                nav.enabled &&
                nav.isOnNavMesh)
            {
                try
                {
                    nav.Warp(
                        safe);
                }
                catch
                {
                    bossRoot.transform.position =
                        safe;
                }
            }
            else
            {
                bossRoot.transform.position =
                    safe;
            }

            float newHeight;

            TryGetVisualHeight(
                bossRoot,
                out newHeight);

            Plugin.NativeLog.LogInfo(
                "[Varkesh] visual scale correction [" +
                (reason ?? "<unknown>") +
                "] Height=[" +
                currentHeight.ToString("0.00") +
                " -> " +
                newHeight.ToString("0.00") +
                "] TargetMin=[" +
                MinimumBossVisualHeight.ToString("0.00") +
                "] RootScale=[" +
                bossRoot.transform.localScale +
                "].");
        }


        private static bool TryGetVisualHeight(
            GameObject root,
            out float height)
        {
            height =
                0f;

            if (root == null)
            {
                return false;
            }

            // Prefer skinned character geometry. This avoids letting weapon
            // trails, nameplates or miscellaneous FX inflate the boss height.
            SkinnedMeshRenderer[] skinned =
                root.GetComponentsInChildren<SkinnedMeshRenderer>(
                    true);

            Bounds bounds =
                new Bounds();

            bool hasBounds =
                false;

            for (int i = 0;
                 skinned != null &&
                 i < skinned.Length;
                 i++)
            {
                SkinnedMeshRenderer renderer =
                    skinned[i];

                if (renderer == null ||
                    !renderer.enabled ||
                    renderer.sharedMesh == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds =
                        renderer.bounds;

                    hasBounds =
                        true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                MeshRenderer[] meshes =
                    root.GetComponentsInChildren<MeshRenderer>(
                        true);

                for (int i = 0;
                     meshes != null &&
                     i < meshes.Length;
                     i++)
                {
                    MeshRenderer renderer =
                        meshes[i];

                    if (renderer == null ||
                        !renderer.enabled)
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        bounds =
                            renderer.bounds;

                        hasBounds =
                            true;
                    }
                    else
                    {
                        bounds.Encapsulate(
                            renderer.bounds);
                    }
                }
            }

            if (!hasBounds)
            {
                return false;
            }

            height =
                Mathf.Max(
                    0f,
                    bounds.size.y);

            return
                height >
                0f;
        }


        private static void ConfigureBoss(
            NPC npc)
        {
            if (npc == null)
            {
                return;
            }

            GameObject root =
                npc.gameObject;

            WorldBossDefinition definition =
                WorldBossCatalog.Varkesh;

            npc.NPCName =
                BossName;

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
                definition.BaseAttackDamage;

            npc.DamageRange =
                new Vector2(
                    definition.BaseAttackDamage,
                    definition.BaseAttackDamage);

            npc.DamageMult =
                1f;

            npc.NoDmgCap =
                true;

            Stats stats =
                root.GetComponent<Stats>();

            if (stats == null)
            {
                stats =
                    root.GetComponentInChildren<Stats>(
                        true);
            }

            if (stats != null)
            {
                stats.MyName =
                    BossName;

                stats.Level =
                    definition.Level;

                stats.BaseHP =
                    definition.MaxHP;

                stats.OverrideHPforNPC =
                    true;

                if (stats.BaseMHAtkDelay > 0f &&
                    definition.AttackSpeedMultiplier > 0f)
                {
                    stats.BaseMHAtkDelay =
                        stats.BaseMHAtkDelay /
                        definition.AttackSpeedMultiplier;
                }

                stats.CalcStats();

                stats.CurrentHP =
                    stats.CurrentMaxHP;
            }

            Character character =
                npc.GetChar();

            if (character == null)
            {
                character =
                    root.GetComponent<Character>();
            }

            if (character != null)
            {
                character.Alive =
                    true;

                // Keep native proximity detection local. Combat retention is
                // handled separately by VarkeshEncounterRuntime after the fight
                // has actually started.
                character.AggroRange =
                    VarkeshEncounterRuntime
                        .NativeAggroDetectionRange;

                Animator animator =
                    character.GetMyAnim();

                if (animator == null)
                {
                    animator =
                        root.GetComponentInChildren<Animator>(
                            true);
                }

                if (animator != null)
                {
                    // Native Hurt root motion is exaggerated by Varkesh's
                    // world-boss scale. NPC/NavMesh movement owns the root.
                    animator.applyRootMotion =
                        false;

                    try
                    {
                        animator.ResetTrigger(
                            "Hurt");
                    }
                    catch
                    {
                    }

                    try
                    {
                        animator.SetBool(
                            "Stunned",
                            false);
                    }
                    catch
                    {
                    }
                }
            }

            NavMeshAgent nav =
                root.GetComponent<NavMeshAgent>();

            if (nav == null)
            {
                nav =
                    root.GetComponentInChildren<NavMeshAgent>(
                        true);
            }

            if (nav != null &&
                nav.enabled &&
                !nav.isOnNavMesh)
            {
                nav.Warp(
                    ResolveSafeNavMeshPosition(
                        SpawnPosition));
            }

            RefreshIdentity(
                npc);
        }


        private static void RefreshIdentity(
            NPC npc)
        {
            if (npc == null)
            {
                return;
            }

            npc.NPCName =
                BossName;

            GameObject root =
                npc.gameObject;

            root.name =
                BossName;

            Stats stats =
                root.GetComponent<Stats>();

            if (stats == null)
            {
                stats =
                    root.GetComponentInChildren<Stats>(
                        true);
            }

            if (stats != null)
            {
                stats.MyName =
                    BossName;
            }

            if (npc.NamePlate == null)
            {
                return;
            }

            TMP_Text[] texts =
                npc.NamePlate.GetComponentsInChildren<TMP_Text>(
                    true);

            for (int i = 0;
                 texts != null &&
                 i < texts.Length;
                 i++)
            {
                TMP_Text text =
                    texts[i];

                if (text == null)
                {
                    continue;
                }

                string current =
                    text.text ??
                    "";

                if (current.IndexOf(
                        NativeTemplateNpcName,
                        StringComparison.OrdinalIgnoreCase) <
                    0)
                {
                    continue;
                }

                text.text =
                    ReplaceIgnoreCase(
                        current,
                        NativeTemplateNpcName,
                        BossName);
            }
        }


        private static void DestroyCurrentBoss()
        {
            if (_bossNpc != null)
            {
                VarkeshEncounterRuntime.Stop(
                    "Varkesh instance destroyed");
            }

            if (_bossObject != null)
            {
                UnityEngine.Object.Destroy(
                    _bossObject);
            }

            _bossObject =
                null;

            _bossNpc =
                null;

            _bossCharacter =
                null;
        }


        // ============================================================
        // DISPLACED NATIVE SPAWN
        // ============================================================

        private static void SuppressNativeRuntAtVarkeshLocation()
        {
            if (!IsSpawnScene(
                    SceneManager.GetActiveScene()))
            {
                return;
            }

            float radiusSquared =
                NativeSuppressionRadius *
                NativeSuppressionRadius;

            try
            {
                if (SpawnPointManager.SpawnPointsInScene != null)
                {
                    foreach (SpawnPoint spawnPoint in
                             SpawnPointManager.SpawnPointsInScene)
                    {
                        if (spawnPoint == null)
                        {
                            continue;
                        }

                        if ((spawnPoint.transform.position -
                             SpawnPosition).sqrMagnitude >
                            radiusSquared)
                        {
                            continue;
                        }

                        if (!SpawnPointReferencesNpc(
                                spawnPoint,
                                DisplacedNativeNpcName))
                        {
                            continue;
                        }

                        TrackAndDisableSpawnPoint(
                            spawnPoint);

                        NPC spawned =
                            spawnPoint.SpawnedNPC;

                        if (IsExactNpc(
                                spawned,
                                DisplacedNativeNpcName))
                        {
                            RemoveDisplacedNpc(
                                spawned);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Varkesh] failed scanning Willowwatch SpawnPoints for [" +
                    DisplacedNativeNpcName +
                    "]: " +
                    exception.Message);
            }

            // Defensive live-NPC pass in case the native SpawnPoint produced
            // the Runt before its source reference could be identified.
            try
            {
                NPC[] live =
                    UnityEngine.Object.FindObjectsOfType<NPC>();

                for (int i = 0;
                     live != null &&
                     i < live.Length;
                     i++)
                {
                    NPC npc =
                        live[i];

                    if (!IsExactNpc(
                            npc,
                            DisplacedNativeNpcName))
                    {
                        continue;
                    }

                    if ((npc.transform.position -
                         SpawnPosition).sqrMagnitude >
                        radiusSquared)
                    {
                        continue;
                    }

                    RemoveDisplacedNpc(
                        npc);
                }
            }
            catch
            {
            }
        }


        private static void TrackAndDisableSpawnPoint(
            SpawnPoint spawnPoint)
        {
            if (spawnPoint == null)
            {
                return;
            }

            for (int i = 0;
                 i < SuppressedNativeSpawnPoints.Count;
                 i++)
            {
                if (SuppressedNativeSpawnPoints[i] != null &&
                    SuppressedNativeSpawnPoints[i].SpawnPoint ==
                    spawnPoint)
                {
                    spawnPoint.enabled =
                        false;

                    return;
                }
            }

            SuppressedNativeSpawnPoints.Add(
                new SuppressedSpawnPoint
                {
                    SpawnPoint =
                        spawnPoint,

                    WasEnabled =
                        spawnPoint.enabled
                });

            spawnPoint.enabled =
                false;

            Plugin.NativeLog.LogInfo(
                "[Varkesh] disabled local [" +
                DisplacedNativeNpcName +
                "] SpawnPoint at [" +
                spawnPoint.transform.position +
                "] to reserve Varkesh's Willowwatch location.");
        }


        private static void RestoreSuppressedNativeSpawnPoints()
        {
            for (int i = 0;
                 i < SuppressedNativeSpawnPoints.Count;
                 i++)
            {
                SuppressedSpawnPoint entry =
                    SuppressedNativeSpawnPoints[i];

                if (entry == null ||
                    entry.SpawnPoint == null)
                {
                    continue;
                }

                entry.SpawnPoint.enabled =
                    entry.WasEnabled;
            }

            SuppressedNativeSpawnPoints.Clear();
        }


        private static void RemoveDisplacedNpc(
            NPC npc)
        {
            if (npc == null ||
                npc.gameObject == null)
            {
                return;
            }

            Plugin.NativeLog.LogInfo(
                "[Varkesh] removing local native [" +
                DisplacedNativeNpcName +
                "] at [" +
                npc.transform.position +
                "] from Varkesh's reserved spawn area.");

            npc.gameObject.SetActive(
                false);

            UnityEngine.Object.Destroy(
                npc.gameObject);
        }


        private static bool SpawnPointReferencesNpc(
            SpawnPoint spawnPoint,
            string npcName)
        {
            if (spawnPoint == null ||
                string.IsNullOrEmpty(
                    npcName))
            {
                return false;
            }

            if (IsExactNpc(
                    spawnPoint.SpawnedNPC,
                    npcName))
            {
                return true;
            }

            try
            {
                FieldInfo[] fields =
                    typeof(SpawnPoint).GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                for (int i = 0;
                     i < fields.Length;
                     i++)
                {
                    object value;

                    try
                    {
                        value =
                            fields[i].GetValue(
                                spawnPoint);
                    }
                    catch
                    {
                        continue;
                    }

                    NPC npc =
                        value as NPC;

                    if (IsExactNpc(
                            npc,
                            npcName))
                    {
                        return true;
                    }

                    GameObject gameObject =
                        value as GameObject;

                    if (gameObject != null)
                    {
                        NPC referenced =
                            gameObject.GetComponentInChildren<NPC>(
                                true);

                        if (IsExactNpc(
                                referenced,
                                npcName))
                        {
                            return true;
                        }
                    }

                    Component component =
                        value as Component;

                    if (component != null)
                    {
                        NPC referenced =
                            component.GetComponentInChildren<NPC>(
                                true);

                        if (IsExactNpc(
                                referenced,
                                npcName))
                        {
                            return true;
                        }
                    }

                    string text =
                        value as string;

                    if (!string.IsNullOrEmpty(
                            text) &&
                        string.Equals(
                            text.Trim(),
                            npcName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }


        private static bool IsExactNpc(
            NPC npc,
            string expectedName)
        {
            if (npc == null ||
                string.IsNullOrEmpty(
                    expectedName))
            {
                return false;
            }

            string name =
                npc.NPCName;

            if (string.IsNullOrEmpty(
                    name))
            {
                Character character =
                    npc.GetChar();

                if (character != null &&
                    character.MyStats != null)
                {
                    name =
                        character.MyStats.MyName;
                }
            }

            return
                string.Equals(
                    name,
                    expectedName,
                    StringComparison.OrdinalIgnoreCase);
        }


        // ============================================================
        // HELPERS
        // ============================================================

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
                return hit.position;
            }

            return requested;
        }


        private static string ReplaceIgnoreCase(
            string source,
            string search,
            string replacement)
        {
            if (string.IsNullOrEmpty(
                    source) ||
                string.IsNullOrEmpty(
                    search))
            {
                return source;
            }

            int index =
                source.IndexOf(
                    search,
                    StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                return source;
            }

            return
                source.Substring(
                    0,
                    index) +
                replacement +
                source.Substring(
                    index + search.Length);
        }
    }
}
