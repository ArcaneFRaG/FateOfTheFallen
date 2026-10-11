using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Permanent world placement/lifecycle for Gorath.
    ///
    /// IMPORTANT:
    /// This class no longer constructs an NPC and never clones native gameplay
    /// state. It owns only scene placement, respawn timing and suppression of
    /// the displaced native spider. CustomCreatureRuntime owns Gorath's entire
    /// Character/NPC/Stats/targeting/aggro/combat component graph.
    ///
    /// Visual donor:
    ///     "An Apparition of Sivakaya" / "Apparition of Sivakaya"
    ///     visual hierarchy only, sanitized by NativeWorldBossVisualRegistry.
    /// </summary>
    internal static class GorathWorldSpawnRuntime
    {
        internal const string SpawnSceneName =
            "vitheo";

        internal const string NativeSourceSceneName =
            "Bonepits";

        internal const string NativeTemplateNpcName =
            "An Apparition of Sivakaya";

        internal const string NativeTemplateNpcAlternateName =
            "Apparition of Sivakaya";

        internal const string DisplacedNativeNpcName =
            "A Mature Grass Spider";

        internal const string BossName =
            "Gorath";

        internal static readonly Vector3 SpawnPosition =
            new Vector3(
                195.11f,
                38.70f,
                390.06f);

        internal static readonly Vector3 DisplacedNativePosition =
            new Vector3(
                176.32f,
                42.03f,
                410.38f);

        private static readonly Quaternion SpawnRotation =
            Quaternion.identity;

        private const float NativeSuppressionRadius =
            30f;

        private const int InitialSettleFrames =
            180;

        private const int VisualResolveRetryFrames =
            600;

        private const int VisualResolveStrideFrames =
            30;

        private static bool _initialized;
        private static bool _spawnRequested;
        private static bool _dead;
        private static int _generation;

        // Permanent world-boss watchdog. This is deliberately independent of
        // Quest 10 state so Gorath remains farmable after the story turn-in.
        // It repairs missing live spawns but never skips the normal 44-minute
        // respawn timer after a legitimate death.
        private static Coroutine _permanentSpawnWatchdog;

        private static DateTime _respawnAtUtc =
            DateTime.MinValue;

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
                NPC current =
                    CustomCreatureRuntime.CurrentNpc;

                return
                    IsGorath(
                        current)
                        ? current
                        : null;
            }
        }


        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized =
                true;

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

            // Do not perform donor discovery during plugin/menu startup. The
            // donor is captured scene-locally when Bonepits is naturally loaded,
            // or resolved by the spawn coroutine through already-resident data.
            // This keeps world-boss visual discovery off the critical load path.
            Scene activeScene =
                SceneManager.GetActiveScene();

            if (activeScene.IsValid() &&
                activeScene.isLoaded &&
                string.Equals(
                    activeScene.name,
                    NativeSourceSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                CoroutineHost.Start(
                    CaptureNativeVisualAfterSceneSettle(
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

            GorathEncounterRuntime.Stop(
                "Gorath world-spawn shutdown");

            RestoreSuppressedNativeSpawnPoints();

            DestroyCurrentBoss();
        }


        internal static void NotifyGorathDeath(
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

            GorathEncounterRuntime.Stop(
                "Gorath defeated");

            _respawnAtUtc =
                DateTime.UtcNow.AddSeconds(
                    WorldBossCatalog
                        .Gorath
                        .RespawnDelaySeconds);

            NPC currentBoss =
                CurrentNpc;

            Character currentCharacter =
                currentBoss != null
                    ? currentBoss.GetChar()
                    : null;

            if (currentCharacter != null)
            {
                GorathQuestDeathBridge.TrySchedule(
                    currentCharacter);
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
                "[Gorath] defeated. Vitheo's Watch respawn armed for 44 minutes. Source=[" +
                (source ?? "<unknown>") +
                "].");
        }


        /// <summary>
        /// Reasserts Gorath's permanent open-world availability without ever
        /// consulting Quest 10 state. If Gorath is legitimately dead, the
        /// normal 44-minute respawn remains authoritative. This also repairs
        /// unexpected CustomCreature/native cleanup after the story quest has
        /// already been completed.
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

            NPC current =
                CurrentNpc;

            if (IsGorath(
                    current))
            {
                Character character =
                    current.GetChar();

                if (character == null)
                {
                    return;
                }

                bool alive =
                    character.Alive &&
                    (character.MyStats == null ||
                     character.MyStats.CurrentHP > 0);

                if (alive)
                {
                    return;
                }

                // Do not let a dead CustomCreature instance silently block
                // Gorath forever if native death processing ran before the
                // normal world-spawn monitor observed it.
                NotifyGorathDeath(
                    "permanent watchdog observed dead Gorath");

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
                    "permanent Gorath watchdog");

                yield return delay;
            }

            _permanentSpawnWatchdog =
                null;
        }


        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            if (scene.IsValid() &&
                scene.isLoaded &&
                string.Equals(
                    scene.name,
                    NativeSourceSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                CoroutineHost.Start(
                    CaptureNativeVisualAfterSceneSettle(
                        scene));
            }

            Scene active =
                SceneManager.GetActiveScene();

            if (IsSpawnScene(
                    active))
            {
                TryStartForScene(
                    active,
                    "sceneLoaded");
            }
        }


        private static IEnumerator CaptureNativeVisualAfterSceneSettle(
            Scene scene)
        {
            // Bonepits publishes its native NPC/SpawnPoint graph during the
            // first few frames after sceneLoaded. Wait briefly, then inspect
            // only this scene's roots. No global Unity object enumeration.
            for (int frame = 0;
                 frame < 6;
                 frame++)
            {
                yield return null;
            }

            if (!_initialized ||
                !scene.IsValid() ||
                !scene.isLoaded)
            {
                yield break;
            }

            NativeWorldBossVisualRegistry.TryCaptureFromScene(
                scene,
                NativeTemplateNpcName,
                NativeTemplateNpcAlternateName,
                "Bonepits scene-local capture");
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
                GorathEncounterRuntime.Stop(
                    "left Vitheo's Watch");

                RestoreSuppressedNativeSpawnPoints();

                DestroyCurrentBoss();

                return;
            }

            TryStartForScene(
                next,
                "Vitheo's Watch became active");
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

            SuppressNativeSpiderAtReservedLocation();

            NPC current =
                CustomCreatureRuntime.CurrentNpc;

            if (IsGorath(
                    current))
            {
                return;
            }

            if (CustomCreatureRuntime.CurrentObject !=
                null)
            {
                CustomCreatureRuntime.DestroyCurrent();
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

                if (frame == 30 ||
                    frame == 90 ||
                    frame == 150)
                {
                    SuppressNativeSpiderAtReservedLocation();
                }

                yield return null;
            }

            SuppressNativeSpiderAtReservedLocation();

            bool visualReady =
                NativeWorldBossVisualRegistry.CanResolve(
                    NativeTemplateNpcName,
                    NativeTemplateNpcAlternateName);

            if (!visualReady)
            {
                NativeWorldBossVisualRegistry.QueueLateBossDonorRecovery(
                    "Gorath missing Apparition of Sivakaya in Vitheo's Watch");
            }

            for (int frame = 0;
                 !visualReady &&
                 frame < VisualResolveRetryFrames;
                 frame++)
            {
                if (!CanContinue(
                        scene,
                        generation))
                {
                    yield break;
                }

                if (frame %
                    VisualResolveStrideFrames ==
                    0)
                {
                    visualReady =
                        NativeWorldBossVisualRegistry.CanResolve(
                            NativeTemplateNpcName,
                            NativeTemplateNpcAlternateName);
                }

                yield return null;
            }

            if (!CanContinue(
                    scene,
                    generation))
            {
                yield break;
            }

            if (!visualReady)
            {
                _spawnRequested =
                    false;

                Plugin.NativeLog.LogWarning(
                    "[Gorath] visual-only native source [" +
                    NativeTemplateNpcName +
                    "] could not be resolved. Spawn deferred.");

                yield break;
            }

            CustomCreatureRuntime.SpawnGorathAt(
                scene,
                SpawnPosition,
                SpawnRotation);

            for (int frame = 0;
                 frame < 240;
                 frame++)
            {
                if (!CanContinue(
                        scene,
                        generation))
                {
                    yield break;
                }

                if (CurrentNpc != null &&
                    CustomCreatureRuntime.CurrentObject !=
                        null)
                {
                    _spawnRequested =
                        false;

                    Plugin.NativeLog.LogInfo(
                        "[Gorath] unified custom-runtime spawn active in Vitheo's Watch at [" +
                        SpawnPosition +
                        "] VisualSource=[" +
                        NativeTemplateNpcName +
                        "] Reason=[" +
                        (reason ?? "<unknown>") +
                        "].");

                    CoroutineHost.Start(
                        MonitorBoss(
                            scene,
                            generation));

                    yield break;
                }

                yield return null;
            }

            _spawnRequested =
                false;

            Plugin.NativeLog.LogWarning(
                "[Gorath] unified custom-runtime spawn did not settle successfully.");
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
                NPC boss =
                    CurrentNpc;

                Character character =
                    boss != null
                        ? boss.GetChar()
                        : null;

                if (boss == null ||
                    character == null)
                {
                    yield break;
                }

                bool dead =
                    !character.Alive ||
                    (character.MyStats != null &&
                     character.MyStats.CurrentHP <=
                     0);

                if (dead)
                {
                    NotifyGorathDeath(
                        "Gorath unified world-spawn monitor");

                    yield break;
                }

                suppressionRefresh++;

                if (suppressionRefresh >=
                    120)
                {
                    suppressionRefresh =
                        0;

                    SuppressNativeSpiderAtReservedLocation();
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
                SuppressNativeSpiderAtReservedLocation();

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
                generation ==
                    _generation &&
                WorldBossPersistencePolicy.CanExistInWorld &&
                scene.IsValid() &&
                scene.isLoaded &&
                IsSpawnScene(
                    SceneManager.GetActiveScene());
        }


        private static bool IsGorath(
            NPC npc)
        {
            return
                npc != null &&
                string.Equals(
                    npc.NPCName,
                    BossName,
                    StringComparison.OrdinalIgnoreCase);
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


        private static void DestroyCurrentBoss()
        {
            NPC current =
                CustomCreatureRuntime.CurrentNpc;

            if (IsGorath(
                    current))
            {
                CustomCreatureRuntime.DestroyCurrent();
            }
        }


        // ============================================================
        // DISPLACED NATIVE SPIDER
        // ============================================================

        private static void SuppressNativeSpiderAtReservedLocation()
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
                if (SpawnPointManager.SpawnPointsInScene !=
                    null)
                {
                    foreach (SpawnPoint spawnPoint in
                             SpawnPointManager.SpawnPointsInScene)
                    {
                        TrySuppressSpiderSpawnPoint(
                            spawnPoint,
                            radiusSquared);
                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Gorath] SpawnPointManager scan for [" +
                    DisplacedNativeNpcName +
                    "] failed: " +
                    exception.Message);
            }

            // Scene-local fallback only. The previous implementation used
            // Resources.FindObjectsOfTypeAll<SpawnPoint>() here, and this
            // method is refreshed repeatedly while Gorath is alive. Avoid a
            // global Unity object scan in a hot/open-world path.
            try
            {
                Scene activeScene =
                    SceneManager.GetActiveScene();

                GameObject[] roots =
                    activeScene.IsValid() &&
                    activeScene.isLoaded
                        ? activeScene.GetRootGameObjects()
                        : null;

                for (int r = 0;
                     roots != null &&
                     r < roots.Length;
                     r++)
                {
                    GameObject root =
                        roots[r];

                    if (root == null)
                    {
                        continue;
                    }

                    SpawnPoint[] spawnPoints =
                        root.GetComponentsInChildren<SpawnPoint>(
                            true);

                    for (int i = 0;
                         spawnPoints != null &&
                         i < spawnPoints.Length;
                         i++)
                    {
                        TrySuppressSpiderSpawnPoint(
                            spawnPoints[i],
                            radiusSquared);
                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Gorath] scene-local SpawnPoint scan for [" +
                    DisplacedNativeNpcName +
                    "] failed: " +
                    exception.Message);
            }

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
                         DisplacedNativePosition).sqrMagnitude >
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


        private static void TrySuppressSpiderSpawnPoint(
            SpawnPoint spawnPoint,
            float radiusSquared)
        {
            if (spawnPoint == null ||
                !SpawnPointReferencesNpc(
                    spawnPoint,
                    DisplacedNativeNpcName))
            {
                return;
            }

            NPC spawned =
                spawnPoint.SpawnedNPC;

            bool spawnPointNearAnchor =
                (spawnPoint.transform.position -
                 DisplacedNativePosition).sqrMagnitude <=
                radiusSquared;

            bool spawnedNpcNearAnchor =
                spawned != null &&
                (spawned.transform.position -
                 DisplacedNativePosition).sqrMagnitude <=
                radiusSquared;

            if (!spawnPointNearAnchor &&
                !spawnedNpcNearAnchor)
            {
                return;
            }

            TrackAndDisableSpawnPoint(
                spawnPoint);

            if (IsExactNpc(
                    spawned,
                    DisplacedNativeNpcName))
            {
                RemoveDisplacedNpc(
                    spawned);
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
                SuppressedSpawnPoint existing =
                    SuppressedNativeSpawnPoints[i];

                if (existing != null &&
                    existing.SpawnPoint ==
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
                "[Gorath] disabled local [" +
                DisplacedNativeNpcName +
                "] SpawnPoint at [" +
                spawnPoint.transform.position +
                "].");
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
                npc.gameObject == null ||
                !npc.gameObject.activeSelf)
            {
                return;
            }

            Plugin.NativeLog.LogInfo(
                "[Gorath] removing local native [" +
                DisplacedNativeNpcName +
                "] at [" +
                npc.transform.position +
                "] from Gorath's reserved area.");

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

            return
                string.Equals(
                    ResolveNpcName(
                        npc),
                    expectedName,
                    StringComparison.OrdinalIgnoreCase);
        }


        private static string ResolveNpcName(
            NPC npc)
        {
            if (npc == null)
            {
                return null;
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

            if (string.IsNullOrEmpty(
                    name) &&
                npc.gameObject != null)
            {
                name =
                    npc.gameObject.name;
            }

            return name;
        }
    }
}
