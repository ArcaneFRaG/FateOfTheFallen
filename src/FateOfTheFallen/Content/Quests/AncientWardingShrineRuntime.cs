using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Permanent Ancient Warding Shrine landmark in Port Azure.
    ///
    /// Lifecycle intentionally matches the existing FOTF asset-spawn runtimes:
    ///
    /// FateContentModule.ActivateExpansionContent()
    ///     -> AncientWardingShrineRuntime.Initialize()
    ///     -> subscribe SceneManager.sceneLoaded
    ///     -> spawn immediately if Azure is already active
    ///
    /// Scene load
    ///     -> wait for native scene/player bootstrap
    ///     -> resolve prefab through AssetBundleRegistry
    ///     -> instantiate at fixed world coordinates
    ///     -> populate native Reliquary god statues
    ///     -> disable the three surrounding Spark Beetle SpawnPoints
    ///
    /// FateContentModule.DeactivateExpansionContent()
    ///     -> AncientWardingShrineRuntime.Shutdown()
    ///     -> remove shrine / restore native spawn controllers
    ///
    /// This landmark is NOT tied to Quest 6 state. Once FOTFLock is unlocked,
    /// it exists permanently whenever Port Azure is loaded.
    /// </summary>
    internal static class AncientWardingShrineRuntime
    {
        internal const string SceneName =
            "Azure";

        internal const string BundleName =
            "fotf_questprops";

        internal const string PrefabName =
            "AncientWardingShrine";

        private const string RuntimeObjectName =
            "FOTF_AncientWardingShrine";

        private const string LogPrefix =
            "[AncientWardingShrine] ";

        private const string SparkBeetleName =
            "Spark Beetle";

        private const int SparkBeetleSpawnCount =
            3;

        private const float SparkBeetleSearchRadius =
            45f;


        private static readonly Vector3 SpawnPosition =
            new Vector3(
                153.08f,
                14.11f,
                442.38f);

        // Initial test rotation. We can tune the yaw after seeing it in-world.
        private static readonly Quaternion SpawnRotation =
            Quaternion.Euler(
                0f,
                0f,
                0f);


        private static bool _initialized;

        private static GameObject _shrine;
        private static Coroutine _spawnRoutine;
        private static Coroutine _statueRetryRoutine;

        private static readonly List<DisabledSpawnState>
            _disabledSparkBeetleSpawns =
                new List<DisabledSpawnState>();


        private sealed class DisabledSpawnState
        {
            internal GameObject Object;
            internal bool WasActiveSelf;
            internal string Path;
            internal Vector3 Position;
        }


        private sealed class SparkSpawnCandidate
        {
            internal SpawnPoint SpawnPoint;
            internal GameObject ObjectToDisable;
            internal string Path;
            internal float Distance;
        }


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

            Scene scene =
                SceneManager.GetActiveScene();

            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "initialized. ActiveScene=[" +
                (scene.IsValid()
                    ? scene.name
                    : "<invalid>") +
                "] expansionUnlocked=[" +
                FOTFExpansionGate.IsUnlocked +
                "].");

            // FateContentModule should only initialize this runtime after
            // FOTFLock is unlocked, but retain the defensive check here.
            if (FOTFExpansionGate.IsUnlocked &&
                IsAzure(
                    scene))
            {
                StartSpawn(
                    scene);
            }
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

            StopSpawnRoutine();
            StopStatueRetry();

            DestroyShrine();
            RestoreSparkBeetleSpawns();

            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "shutdown.");
        }


        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            // Quest 6 can temporarily load Reliquary additively to acquire
            // native statue assets. That is NOT a gameplay scene transition.
            // Ignore any additive source-scene load unless Unity actually made
            // that scene active.
            if (mode == LoadSceneMode.Additive &&
                SceneManager.GetActiveScene() != scene)
            {
                Plugin.NativeLog.LogInfo(
                    LogPrefix +
                    "ignoring auxiliary additive sceneLoaded=[" +
                    (scene.IsValid()
                        ? scene.name
                        : "<invalid>") +
                    "] while active gameplay scene remains [" +
                    SceneManager.GetActiveScene().name +
                    "].");

                return;
            }

            StopSpawnRoutine();
            StopStatueRetry();

            DestroyShrine();

            // When Azure itself is unloading, these objects disappear with
            // the native scene. Do not try to restore destroyed references
            // from the previous scene.
            _disabledSparkBeetleSpawns.Clear();

            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "sceneLoaded=[" +
                (scene.IsValid()
                    ? scene.name
                    : "<invalid>") +
                "] expansionUnlocked=[" +
                FOTFExpansionGate.IsUnlocked +
                "].");

            if (!_initialized ||
                !FOTFExpansionGate.IsUnlocked ||
                !IsAzure(
                    scene))
            {
                return;
            }

            StartSpawn(
                scene);
        }


        // ============================================================
        // SCENE / SPAWN
        // ============================================================

        private static bool IsAzure(
            Scene scene)
        {
            return
                scene.IsValid() &&
                scene.isLoaded &&
                string.Equals(
                    scene.name,
                    SceneName,
                    StringComparison.OrdinalIgnoreCase);
        }


        private static void StartSpawn(
            Scene scene)
        {
            if (!_initialized ||
                _spawnRoutine != null ||
                _shrine != null ||
                !FOTFExpansionGate.IsUnlocked ||
                !IsAzure(
                    scene))
            {
                return;
            }

            _spawnRoutine =
                CoroutineHost.Start(
                    SpawnWhenReady(
                        scene));
        }


        private static void StopSpawnRoutine()
        {
            if (_spawnRoutine == null)
            {
                return;
            }

            CoroutineHost.Stop(
                _spawnRoutine);

            _spawnRoutine =
                null;
        }


        private static IEnumerator SpawnWhenReady(
            Scene scene)
        {
            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "waiting for Azure world bootstrap before spawning landmark.");

            // Same pattern as the other FOTF prop runtimes: let native scene
            // initialization finish before touching scene objects.
            for (int frame = 0;
                 frame < 180;
                 frame++)
            {
                if (!_initialized ||
                    !FOTFExpansionGate.IsUnlocked ||
                    !IsAzure(
                        SceneManager.GetActiveScene()) ||
                    SceneManager.GetActiveScene() !=
                        scene)
                {
                    _spawnRoutine =
                        null;

                    yield break;
                }

                if (GameData.PlayerControl !=
                    null)
                {
                    break;
                }

                yield return null;
            }

            if (!_initialized ||
                !FOTFExpansionGate.IsUnlocked ||
                !IsAzure(
                    SceneManager.GetActiveScene()) ||
                SceneManager.GetActiveScene() !=
                    scene)
            {
                _spawnRoutine =
                    null;

                yield break;
            }

            GameObject prefab =
                AssetBundleRegistry.GetPrefab(
                    BundleName,
                    PrefabName);

            if (prefab == null)
            {
                Plugin.NativeLog.LogError(
                    LogPrefix +
                    "prefab [" +
                    PrefabName +
                    "] was not found in bundle [" +
                    BundleName +
                    "]. Expected bundle path=[" +
                    AssetBundleRegistry.GetBundlePath(
                        BundleName) +
                    "].");

                LogQuestPropsBundleAssets();

                _spawnRoutine =
                    null;

                yield break;
            }

            GameObject shrine =
                null;

            try
            {
                // Preserve the prefab's authored FBX compensation rotation.
                // Replacing it with Quaternion.identity lays this imported
                // shrine on its side.
                Quaternion finalRotation =
                    SpawnRotation *
                    prefab.transform.rotation;

                shrine =
                    UnityEngine.Object.Instantiate(
                        prefab,
                        SpawnPosition,
                        finalRotation);

                if (shrine == null)
                {
                    Plugin.NativeLog.LogError(
                        LogPrefix +
                        "Instantiate returned null.");

                    _spawnRoutine =
                        null;

                    yield break;
                }

                shrine.name =
                    RuntimeObjectName;

                SceneManager.MoveGameObjectToScene(
                    shrine,
                    scene);

                shrine.SetActive(
                    true);

                _shrine =
                    shrine;

                int anchorCount =
                    ValidateAnchors(
                        shrine.transform);

                Plugin.NativeLog.LogInfo(
                    LogPrefix +
                    "prefab anchor validation [" +
                    anchorCount +
                    "/7].");

                bool statuesPopulated =
                    Quest6NativeStatueRuntime.PopulateShrine(
                        shrine.transform);

                if (!statuesPopulated)
                {
                    StartStatueRetry(
                        scene);
                }

                DisableNearbySparkBeetleSpawns(
                    scene);

                Plugin.NativeLog.LogInfo(
                    LogPrefix +
                    "SPAWNED permanent landmark at [" +
                    shrine.transform.position +
                    "] rotation=[" +
                    shrine.transform.rotation.eulerAngles +
                    "] statuesPopulated=[" +
                    statuesPopulated +
                    "] SparkBeetleSpawnsDisabled=[" +
                    _disabledSparkBeetleSpawns.Count +
                    "/" +
                    SparkBeetleSpawnCount +
                    "].");
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    LogPrefix +
                    "failed to spawn landmark: " +
                    exception);

                if (shrine != null)
                {
                    UnityEngine.Object.Destroy(
                        shrine);
                }

                _shrine =
                    null;

                RestoreSparkBeetleSpawns();
            }

            _spawnRoutine =
                null;
        }


        // ============================================================
        // STATUES
        // ============================================================

        private static void StartStatueRetry(
            Scene scene)
        {
            if (_statueRetryRoutine != null ||
                _shrine == null)
            {
                return;
            }

            _statueRetryRoutine =
                CoroutineHost.Start(
                    RetryStatues(
                        scene));
        }


        private static IEnumerator RetryStatues(
            Scene scene)
        {
            WaitForSeconds wait =
                new WaitForSeconds(
                    2f);

            while (_initialized &&
                   FOTFExpansionGate.IsUnlocked &&
                   IsAzure(
                       SceneManager.GetActiveScene()) &&
                   SceneManager.GetActiveScene() ==
                       scene &&
                   _shrine != null)
            {
                if (Quest6NativeStatueRuntime.HasAllTemplates)
                {
                    bool populated =
                        Quest6NativeStatueRuntime.PopulateShrine(
                            _shrine.transform);

                    if (populated)
                    {
                        Plugin.NativeLog.LogInfo(
                            LogPrefix +
                            "native god statues populated after delayed template availability.");

                        _statueRetryRoutine =
                            null;

                        yield break;
                    }
                }

                yield return wait;
            }

            _statueRetryRoutine =
                null;
        }


        private static void StopStatueRetry()
        {
            if (_statueRetryRoutine == null)
            {
                return;
            }

            CoroutineHost.Stop(
                _statueRetryRoutine);

            _statueRetryRoutine =
                null;
        }


        // ============================================================
        // SPARK BEETLE SUPPRESSION
        // ============================================================

        private static void DisableNearbySparkBeetleSpawns(
            Scene scene)
        {
            RestoreSparkBeetleSpawns();

            List<SparkSpawnCandidate> candidates =
                FindSparkBeetleSpawnCandidates(
                    scene);

            candidates.Sort(
                delegate(
                    SparkSpawnCandidate a,
                    SparkSpawnCandidate b)
                {
                    if (a == null &&
                        b == null)
                    {
                        return 0;
                    }

                    if (a == null)
                    {
                        return 1;
                    }

                    if (b == null)
                    {
                        return -1;
                    }

                    return
                        a.Distance.CompareTo(
                            b.Distance);
                });

            int disableCount =
                Mathf.Min(
                    SparkBeetleSpawnCount,
                    candidates.Count);

            for (int i = 0;
                 i < disableCount;
                 i++)
            {
                SparkSpawnCandidate candidate =
                    candidates[i];

                if (candidate == null ||
                    candidate.ObjectToDisable == null)
                {
                    continue;
                }

                if (candidate.SpawnPoint != null &&
                    candidate.SpawnPoint.SpawnedNPC != null &&
                    IsSparkBeetle(
                        candidate.SpawnPoint.SpawnedNPC))
                {
                    UnityEngine.Object.Destroy(
                        candidate.SpawnPoint.SpawnedNPC.gameObject);
                }

                DisableSpawnObject(
                    candidate.ObjectToDisable,
                    candidate.Path,
                    candidate.Distance,
                    i + 1);
            }

            if (_disabledSparkBeetleSpawns.Count <
                SparkBeetleSpawnCount)
            {
                Plugin.NativeLog.LogWarning(
                    LogPrefix +
                    "found only [" +
                    _disabledSparkBeetleSpawns.Count +
                    "] positively identified nearby Spark Beetle SpawnPoint(s); expected [3]. No unrelated SpawnPoints were disabled.");
            }
        }


        private static List<SparkSpawnCandidate>
            FindSparkBeetleSpawnCandidates(
                Scene scene)
        {
            List<SparkSpawnCandidate> result =
                new List<SparkSpawnCandidate>();

            HashSet<int> seen =
                new HashSet<int>();

            GameObject[] roots =
                scene.GetRootGameObjects();

            for (int rootIndex = 0;
                 rootIndex < roots.Length;
                 rootIndex++)
            {
                GameObject root =
                    roots[rootIndex];

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

                for (int i = 0;
                     i < spawnPoints.Length;
                     i++)
                {
                    SpawnPoint spawnPoint =
                        spawnPoints[i];

                    if (spawnPoint == null ||
                        spawnPoint.gameObject == null)
                    {
                        continue;
                    }

                    float distance =
                        HorizontalDistance(
                            spawnPoint.transform.position,
                            SpawnPosition);

                    if (distance >
                        SparkBeetleSearchRadius)
                    {
                        continue;
                    }

                    string fullPath =
                        GetFullPath(
                            spawnPoint.transform);

                    bool pathMatch =
                        ContainsIgnoreCase(
                            fullPath,
                            SparkBeetleName);

                    bool liveNpcMatch =
                        spawnPoint.SpawnedNPC != null &&
                        IsSparkBeetle(
                            spawnPoint.SpawnedNPC);

                    if (!pathMatch &&
                        !liveNpcMatch)
                    {
                        continue;
                    }

                    GameObject objectToDisable =
                        spawnPoint.gameObject;

                    Transform namedParent =
                        FindNamedSparkBeetleAncestor(
                            spawnPoint.transform);

                    // If the native group contains exactly this one spawn
                    // controller, disabling the named parent is cleaner.
                    // If it contains several SpawnPoints, keep each SpawnPoint
                    // individually addressable so we suppress exactly 3.
                    if (namedParent != null)
                    {
                        SpawnPoint[] grouped =
                            namedParent.GetComponentsInChildren<SpawnPoint>(
                                true);

                        if (grouped != null &&
                            grouped.Length == 1)
                        {
                            objectToDisable =
                                namedParent.gameObject;
                        }
                    }

                    int id =
                        objectToDisable.GetInstanceID();

                    if (!seen.Add(
                            id))
                    {
                        continue;
                    }

                    SparkSpawnCandidate candidate =
                        new SparkSpawnCandidate();

                    candidate.SpawnPoint =
                        spawnPoint;

                    candidate.ObjectToDisable =
                        objectToDisable;

                    candidate.Path =
                        GetFullPath(
                            objectToDisable.transform);

                    candidate.Distance =
                        distance;

                    result.Add(
                        candidate);

                    Plugin.NativeLog.LogInfo(
                        LogPrefix +
                        "Spark Beetle spawn candidate Path=[" +
                        candidate.Path +
                        "] Position=[" +
                        spawnPoint.transform.position +
                        "] Distance=[" +
                        candidate.Distance.ToString(
                            "0.00") +
                        "m] liveNpc=[" +
                        (spawnPoint.SpawnedNPC != null
                            ? spawnPoint.SpawnedNPC.NPCName
                            : "<none>") +
                        "].");
                }
            }

            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "identified [" +
                result.Count +
                "] nearby Spark Beetle SpawnPoint candidate(s) within [" +
                SparkBeetleSearchRadius +
                "m].");

            return result;
        }


        private static Transform FindNamedSparkBeetleAncestor(
            Transform start)
        {
            Transform current =
                start;

            while (current != null)
            {
                if (ContainsIgnoreCase(
                        current.name,
                        "SPAWN Spark Beetle"))
                {
                    return current;
                }

                current =
                    current.parent;
            }

            return null;
        }


        private static void DisableSpawnObject(
            GameObject objectToDisable,
            string path,
            float distance,
            int index)
        {
            if (objectToDisable == null)
            {
                return;
            }

            DisabledSpawnState state =
                new DisabledSpawnState();

            state.Object =
                objectToDisable;

            state.WasActiveSelf =
                objectToDisable.activeSelf;

            state.Path =
                path;

            state.Position =
                objectToDisable.transform.position;

            _disabledSparkBeetleSpawns.Add(
                state);

            objectToDisable.SetActive(
                false);

            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "disabled Spark Beetle spawn [" +
                index +
                "/" +
                SparkBeetleSpawnCount +
                "] Path=[" +
                path +
                "] Position=[" +
                state.Position +
                "] Distance=[" +
                distance.ToString(
                    "0.00") +
                "m].");
        }


        private static void RestoreSparkBeetleSpawns()
        {
            for (int i = 0;
                 i < _disabledSparkBeetleSpawns.Count;
                 i++)
            {
                DisabledSpawnState state =
                    _disabledSparkBeetleSpawns[i];

                if (state == null ||
                    state.Object == null)
                {
                    continue;
                }

                state.Object.SetActive(
                    state.WasActiveSelf);
            }

            _disabledSparkBeetleSpawns.Clear();
        }


        private static bool IsSparkBeetle(
            NPC npc)
        {
            if (npc == null)
            {
                return false;
            }

            return
                ContainsIgnoreCase(
                    npc.NPCName,
                    SparkBeetleName) ||
                ContainsIgnoreCase(
                    npc.gameObject != null
                        ? npc.gameObject.name
                        : null,
                    SparkBeetleName);
        }


        // ============================================================
        // PREFAB VALIDATION
        // ============================================================

        private static int ValidateAnchors(
            Transform root)
        {
            string[] required =
            {
                "InteractionAnchor",
                "FXAnchor",
                "FragmentAnchor",
                "StatueSlot_Soluna",
                "StatueSlot_Brax",
                "StatueSlot_Vithean",
                "StatueSlot_Fernalla"
            };

            int found =
                0;

            for (int i = 0;
                 i < required.Length;
                 i++)
            {
                if (FindChildRecursive(
                        root,
                        required[i]) !=
                    null)
                {
                    found++;
                }
                else
                {
                    Plugin.NativeLog.LogError(
                        LogPrefix +
                        "missing prefab anchor [" +
                        required[i] +
                        "].");
                }
            }

            return found;
        }


        private static void LogQuestPropsBundleAssets()
        {
            try
            {
                AssetBundle bundle =
                    AssetBundleRegistry.GetBundle(
                        BundleName);

                if (bundle == null)
                {
                    Plugin.NativeLog.LogError(
                        LogPrefix +
                        "bundle [" +
                        BundleName +
                        "] could not be loaded.");

                    return;
                }

                string[] names =
                    bundle.GetAllAssetNames();

                Plugin.NativeLog.LogInfo(
                    LogPrefix +
                    "bundle contains [" +
                    (names != null
                        ? names.Length
                        : 0) +
                    "] asset(s).");

                if (names == null)
                {
                    return;
                }

                for (int i = 0;
                     i < names.Length;
                     i++)
                {
                    Plugin.NativeLog.LogInfo(
                        LogPrefix +
                        "bundle asset=[" +
                        names[i] +
                        "].");
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    LogPrefix +
                    "failed enumerating quest-props bundle: " +
                    exception);
            }
        }


        // ============================================================
        // CLEANUP / HELPERS
        // ============================================================

        private static void DestroyShrine()
        {
            if (_shrine == null)
            {
                return;
            }

            Quest6NativeStatueRuntime.ClearShrine(
                _shrine.transform);

            UnityEngine.Object.Destroy(
                _shrine);

            _shrine =
                null;
        }


        private static Transform FindChildRecursive(
            Transform root,
            string childName)
        {
            if (root == null ||
                string.IsNullOrEmpty(
                    childName))
            {
                return null;
            }

            if (string.Equals(
                    root.name,
                    childName,
                    StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0;
                 i < root.childCount;
                 i++)
            {
                Transform result =
                    FindChildRecursive(
                        root.GetChild(
                            i),
                        childName);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }


        private static string GetFullPath(
            Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            string path =
                transform.name;

            Transform current =
                transform.parent;

            while (current != null)
            {
                path =
                    current.name +
                    "/" +
                    path;

                current =
                    current.parent;
            }

            return path;
        }


        private static bool ContainsIgnoreCase(
            string source,
            string value)
        {
            if (string.IsNullOrEmpty(
                    source) ||
                string.IsNullOrEmpty(
                    value))
            {
                return false;
            }

            return
                source.IndexOf(
                    value,
                    StringComparison.OrdinalIgnoreCase) >=
                0;
        }


        private static float HorizontalDistance(
            Vector3 a,
            Vector3 b)
        {
            float x =
                a.x -
                b.x;

            float z =
                a.z -
                b.z;

            return
                Mathf.Sqrt(
                    (x * x) +
                    (z * z));
        }


        internal static GameObject CurrentShrine
        {
            get
            {
                return _shrine;
            }
        }
    }
}
