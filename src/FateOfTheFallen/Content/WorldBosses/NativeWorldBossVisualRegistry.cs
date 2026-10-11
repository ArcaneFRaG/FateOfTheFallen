using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Resolves native NPCs strictly as VISUAL donors for FOTF world bosses.
    ///
    /// Native gameplay state is deliberately discarded. The resulting template
    /// contains only the transform/model/renderer/Animator hierarchy (plus
    /// passive visual components such as particle systems). Character, Stats,
    /// NPC, targeting, aggro, colliders, NavMeshAgent, UseSkill, CastSpell and
    /// every native MonoBehaviour are stripped before CustomCreatureRuntime
    /// builds a fresh, known-good Erenshor gameplay graph around the visual.
    ///
    /// This is the important boundary: native art is reused, native runtime
    /// ownership is not.
    /// </summary>
    internal static class NativeWorldBossVisualRegistry
    {
        private sealed class VisualTemplate
        {
            internal string Key;
            internal GameObject Root;
        }

        private static readonly Dictionary<string, VisualTemplate>
            Templates =
                new Dictionary<string, VisualTemplate>(
                    StringComparer.OrdinalIgnoreCase);

        // Startup donor probing is targeted-only. r29 deliberately forbids
        // global Resources enumeration from the plugin/menu critical path.
        private static bool _safeStartupPrimeAttempted;

        // r31 donor recovery is shared by BOTH late bosses. The proven r26
        // resource fallback is deliberately never run in plugin initialization,
        // the main menu, sceneLoaded or either permanent-spawn watchdog.
        private static bool _lateDonorRecoveryQueued;
        private static bool _lateDonorRecoveryCompleted;
        private static int _lateDonorRecoveryEpoch;
        private const int RecoveryRootsPerFrame = 24;
        private static readonly string[] LateDonorResourceFolders =
        {
            "NPCs", "NPC", "Enemies", "Monsters", "Creatures",
            "Characters", "Prefabs/NPCs", "Prefabs/NPC"
        };

        private static readonly HashSet<string>
            MissingSourceWarnings =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

        // Expensive-but-targeted bundle/resource-path probes are allowed only
        // once per donor per active scene. Retry loops then become cache checks
        // plus the already-published SpawnPoint list instead of repeatedly
        // enumerating every loaded AssetBundle.
        private static readonly HashSet<string>
            TargetedResidentProbeAttempts =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);


        internal static void Shutdown()
        {
            HashSet<int> destroyed =
                new HashSet<int>();

            foreach (KeyValuePair<string, VisualTemplate> pair
                     in Templates)
            {
                VisualTemplate template =
                    pair.Value;

                if (template == null ||
                    template.Root == null)
                {
                    continue;
                }

                int id =
                    template.Root.GetInstanceID();

                if (!destroyed.Add(
                        id))
                {
                    continue;
                }

                UnityEngine.Object.Destroy(
                    template.Root);
            }

            Templates.Clear();
            _lateDonorRecoveryEpoch++;
            _lateDonorRecoveryQueued = false;
            _lateDonorRecoveryCompleted = false;
            _safeStartupPrimeAttempted =
                false;
            MissingSourceWarnings.Clear();
            TargetedResidentProbeAttempts.Clear();
        }


        /// <summary>
        /// Performs a targeted donor probe without any global Resources scan.
        /// This helper is retained for explicit/diagnostic use, but r29 removes
        /// it from the normal plugin-startup critical path.
        /// </summary>
        internal static bool TryPrimeAtSafeStartup(
            string primaryName,
            string alternateName)
        {
            VisualTemplate template;

            if (TryGetCachedTemplate(
                    primaryName,
                    alternateName,
                    out template))
            {
                return true;
            }

            // r29: startup donor priming is TARGETED ONLY.  The old r26 path
            // called Resources.LoadAll<GameObject>("") here, which synchronously
            // loaded/enumerated the game's Resources tree on the Unity main
            // thread and could leave the menu apparently frozen for a very long
            // time.  Never perform that global operation during plugin startup.
            Scene activeScene =
                SceneManager.GetActiveScene();

            NPC source =
                FindNativeNpcInScene(
                    activeScene,
                    primaryName,
                    alternateName);

            string origin =
                source != null
                    ? "active-scene targeted donor"
                    : null;

            if (source == null)
            {
                TryResolveFromLoadedAssetBundles(
                    primaryName,
                    alternateName,
                    out source,
                    out origin);
            }

            if (source == null)
            {
                TryResolveFromExactResources(
                    primaryName,
                    alternateName,
                    out source,
                    out origin);
            }

            if (source == null)
            {
                if (!_safeStartupPrimeAttempted)
                {
                    Plugin.NativeLog.LogInfo(
                        "[WorldBossVisual] targeted startup donor probe deferred for [" +
                        (primaryName ?? alternateName ?? "<unnamed>") +
                        "]. No Resources.LoadAll was used; scene-local capture will resolve it when native source content becomes resident.");
                }

                _safeStartupPrimeAttempted =
                    true;

                return false;
            }

            bool cached =
                CacheTemplateFromSource(
                    source,
                    primaryName,
                    alternateName,
                    out template);

            _safeStartupPrimeAttempted =
                true;

            if (cached)
            {
                Plugin.NativeLog.LogInfo(
                    "[WorldBossVisual] targeted startup donor cached [" +
                    (primaryName ?? alternateName ?? "<unnamed>") +
                    "] Origin=[" +
                    (origin ?? "<unknown>") +
                    "].");
            }

            return cached;
        }


        /// <summary>
        /// Attempts to capture one native visual donor from a specific loaded
        /// scene. This is deliberately scene-local: it examines only that
        /// scene's roots and published SpawnPoints and never touches the global
        /// Resources object table.
        /// </summary>
        internal static bool TryCaptureFromScene(
            Scene scene,
            string primaryName,
            string alternateName,
            string origin)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded)
            {
                return false;
            }

            VisualTemplate existing;

            if (TryGetCachedTemplate(
                    primaryName,
                    alternateName,
                    out existing))
            {
                return true;
            }

            NPC source =
                FindNativeNpcInScene(
                    scene,
                    primaryName,
                    alternateName);

            if (source == null)
            {
                return false;
            }

            bool cached =
                CacheTemplateFromSource(
                    source,
                    primaryName,
                    alternateName,
                    out existing);

            if (cached)
            {
                Plugin.NativeLog.LogInfo(
                    "[WorldBossVisual] scene-local donor capture succeeded [" +
                    (primaryName ?? alternateName ?? "<unnamed>") +
                    "] Scene=[" +
                    scene.name +
                    "] Origin=[" +
                    (origin ?? "scene-local") +
                    "].");
            }

            return cached;
        }


        /// <summary>
        /// Recover the native late-boss donors when targeted r29 lookups have
        /// failed. This queues work only: NEVER run Resources.LoadAll from the
        /// caller, a sceneLoaded callback, a menu or a permanent watchdog.
        /// The last-resort broad fallback is intentionally on an already-loaded
        /// boss scene, one time per active expansion; it may still cause a
        /// visible hitch on the Unity main thread (LoadAll has no async API).
        /// </summary>
        internal static void QueueLateBossDonorRecovery(string requestedBy)
        {
            if (_lateDonorRecoveryQueued || _lateDonorRecoveryCompleted ||
                !WorldBossPersistencePolicy.CanExistInWorld)
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded ||
                (!string.Equals(scene.name,
                    VarkeshWorldSpawnRuntime.SpawnSceneName,
                    StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(scene.name,
                    GorathWorldSpawnRuntime.SpawnSceneName,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _lateDonorRecoveryQueued = true;
            int epoch = _lateDonorRecoveryEpoch;
            CoroutineHost.Start(RecoverLateBossDonors(epoch, requestedBy));
        }


        private static IEnumerator RecoverLateBossDonors(int epoch, string requestedBy)
        {
            // Allow SpawnPoint publication and the player transition to settle.
            // No game-object enumeration occurs synchronously in Queue.
            yield return new WaitForSecondsRealtime(1.5f);

            if (epoch != _lateDonorRecoveryEpoch ||
                !CanRecoverInCurrentScene())
            {
                _lateDonorRecoveryQueued = false;
                yield break;
            }

            Plugin.NativeLog.LogInfo(
                "[LateBossDonors] shared deferred recovery started. Trigger=[" +
                (requestedBy ?? "unknown") + "].");

            // r93: Before any potentially 20-second Resources.LoadAll,
            // asynchronously load the exact addresses from Erenshor's
            // globalgamemanagers ResourceManager index. This also covers
            // direct cold boot into Willowwatch or Vitheo with no saved
            // donor hints. Keep the r91 hint mechanism as a backup.
            IEnumerator learnedPrewarm =
                WorldBossDonorPrecache.TryLearnedPathsInActiveBossScene();
            while (learnedPrewarm.MoveNext())
            {
                if (epoch != _lateDonorRecoveryEpoch ||
                    !CanRecoverInCurrentScene())
                {
                    _lateDonorRecoveryQueued = false;
                    yield break;
                }
                yield return learnedPrewarm.Current;
            }

            // The shared recovery previously continued searching for the
            // SECOND boss donor even when the current boss already had a
            // working template. Only require both for an unknown caller.
            if (RequestedDonorReady(requestedBy))
            {
                _lateDonorRecoveryQueued = false;
                _lateDonorRecoveryCompleted = LateDonorsReady();
                Plugin.NativeLog.LogInfo(
                    "[LateBossDonors] r93 skipped synchronous fallback; " +
                    "requested donor ready after indexed async prewarm. Trigger=[" +
                    (requestedBy ?? "unknown") + "] VarkeshReady=[" +
                    VarkeshWorldSpawnRuntime.NativeTemplateReady +
                    "] GorathReady=[" + IsGorathDonorReady() + "].");
                yield break;
            }

            // Probe individual likely folders first. These are independent
            // loads with frame yields and can avoid the cold broad fallback.
            for (int folder = 0; folder < LateDonorResourceFolders.Length; folder++)
            {
                if (RequestedDonorReady(requestedBy)) break;
                if (!CanRecoverInCurrentScene())
                {
                    _lateDonorRecoveryQueued = false;
                    yield break;
                }

                GameObject[] roots = null;
                float scopedStarted = Time.realtimeSinceStartup;
                try
                {
                    roots = Resources.LoadAll<GameObject>(
                        LateDonorResourceFolders[folder]);
                }
                catch (Exception ex)
                {
                    Plugin.NativeLog.LogWarning(
                        "[LateBossDonors] scoped Resources folder [" +
                        LateDonorResourceFolders[folder] + "] failed: " + ex.Message);
                }

                float scopedElapsedMs = (Time.realtimeSinceStartup - scopedStarted) * 1000f;
                if (scopedElapsedMs >= 25f)
                    Plugin.NativeLog.LogInfo(
                        "[LateBossDonors] synchronous Resources folder [" +
                        LateDonorResourceFolders[folder] + "] load took [" +
                        scopedElapsedMs.ToString("0.0") + "ms].");

                if (roots != null && roots.Length > 0)
                {
                    IEnumerator scan = ScanLateDonorRoots(
                        roots,
                        "scoped Resources [" + LateDonorResourceFolders[folder] + "]",
                        LateDonorResourceFolders[folder], requestedBy);
                    while (scan.MoveNext())
                    {
                        if (epoch != _lateDonorRecoveryEpoch) yield break;
                        if (!CanRecoverInCurrentScene())
                        {
                            _lateDonorRecoveryQueued = false;
                            yield break;
                        }
                        yield return scan.Current;
                    }
                }

                // Spread folder loads over multiple frames as well.
                yield return null;
            }

            // In Unity, Resources.LoadAll<T> can filter prefab components.
            // Query NPC and SpawnPoint specifically before touching every
            // GameObject/mesh in Resources. These remain synchronous calls,
            // but generally return far less geometry than LoadAll<GameObject>.
            if (!RequestedDonorReady(requestedBy) && epoch == _lateDonorRecoveryEpoch &&
                CanRecoverInCurrentScene())
            {
                NPC[] npcAssets = null;
                float typedNpcStarted = Time.realtimeSinceStartup;
                try { npcAssets = Resources.LoadAll<NPC>(string.Empty); }
                catch (Exception ex)
                {
                    Plugin.NativeLog.LogWarning(
                        "[LateBossDonors] typed NPC resource probe failed: " + ex.Message);
                }
                Plugin.NativeLog.LogInfo(
                    "[LateBossDonors] typed NPC resource candidates=[" +
                    (npcAssets == null ? 0 : npcAssets.Length) + "] loadMs=[" +
                    ((Time.realtimeSinceStartup - typedNpcStarted) * 1000f).ToString("0.0") + "].");
                for (int i = 0; npcAssets != null && i < npcAssets.Length; i++)
                {
                    NPC source = npcAssets[i];
                    if (source != null)
                    {
                        if (!VarkeshWorldSpawnRuntime.NativeTemplateReady)
                            VarkeshWorldSpawnRuntime.PrimeNativeTemplateFromNpc(
                                source, "typed NPC resource probe");
                        if (!IsGorathDonorReady() && NameMatches(
                            source,
                            GorathWorldSpawnRuntime.NativeTemplateNpcName,
                            GorathWorldSpawnRuntime.NativeTemplateNpcAlternateName))
                        {
                            VisualTemplate template;
                            CacheTemplateFromSource(
                                source,
                                GorathWorldSpawnRuntime.NativeTemplateNpcName,
                                GorathWorldSpawnRuntime.NativeTemplateNpcAlternateName,
                                out template);
                        }
                    }
                    if (RequestedDonorReady(requestedBy)) break;
                    if ((i + 1) % RecoveryRootsPerFrame == 0) yield return null;
                }
            }

            if (!RequestedDonorReady(requestedBy) && epoch == _lateDonorRecoveryEpoch &&
                CanRecoverInCurrentScene())
            {
                SpawnPoint[] pointAssets = null;
                float typedPointStarted = Time.realtimeSinceStartup;
                try { pointAssets = Resources.LoadAll<SpawnPoint>(string.Empty); }
                catch (Exception ex)
                {
                    Plugin.NativeLog.LogWarning(
                        "[LateBossDonors] typed SpawnPoint resource probe failed: " + ex.Message);
                }
                Plugin.NativeLog.LogInfo(
                    "[LateBossDonors] typed SpawnPoint resource candidates=[" +
                    (pointAssets == null ? 0 : pointAssets.Length) + "] loadMs=[" +
                    ((Time.realtimeSinceStartup - typedPointStarted) * 1000f).ToString("0.0") + "].");
                for (int i = 0; pointAssets != null && i < pointAssets.Length; i++)
                {
                    SpawnPoint point = pointAssets[i];
                    if (point != null)
                    {
                        if (!VarkeshWorldSpawnRuntime.NativeTemplateReady)
                            VarkeshWorldSpawnRuntime.PrimeNativeTemplateFromSpawnPoint(
                                point, "typed SpawnPoint resource probe");
                        if (!IsGorathDonorReady())
                        {
                            NPC source = FindReferencedNpc(
                                point,
                                GorathWorldSpawnRuntime.NativeTemplateNpcName,
                                GorathWorldSpawnRuntime.NativeTemplateNpcAlternateName);
                            if (source != null)
                            {
                                VisualTemplate template;
                                CacheTemplateFromSource(
                                    source,
                                    GorathWorldSpawnRuntime.NativeTemplateNpcName,
                                    GorathWorldSpawnRuntime.NativeTemplateNpcAlternateName,
                                    out template);
                            }
                        }
                    }
                    if (RequestedDonorReady(requestedBy)) break;
                    if ((i + 1) % RecoveryRootsPerFrame == 0) yield return null;
                }
            }

            if (!RequestedDonorReady(requestedBy) && epoch == _lateDonorRecoveryEpoch &&
                CanRecoverInCurrentScene())
            {
                // The old r26 shared fallback was proven to find these native
                // prefabs, which are not discoverable by the r29 exact paths.
                // Never call it from startup/menu/scene callbacks. Unity 2021
                // has no async Resources.LoadAll API, so document the hitch.
                Plugin.NativeLog.LogWarning(
                    "[LateBossDonors] targeted folders did not yield the requested donor. " +
                    "Starting ONE deferred shared Resources.LoadAll<GameObject>(\"\") " +
                    "on a loaded gameplay scene; Unity may hitch on this frame.");

                float started = Time.realtimeSinceStartup;
                GameObject[] allRoots = null;
                try
                {
                    allRoots = Resources.LoadAll<GameObject>(string.Empty);
                }
                catch (Exception ex)
                {
                    Plugin.NativeLog.LogError(
                        "[LateBossDonors] shared resource recovery failed: " + ex);
                }

                Plugin.NativeLog.LogInfo(
                    "[LateBossDonors] shared resource load returned [" +
                    (allRoots == null ? 0 : allRoots.Length) +
                    "] roots in [" +
                    ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") +
                    "ms]; scanning in bounded frame batches.");

                if (allRoots != null)
                {
                    IEnumerator scan = ScanLateDonorRoots(
                        allRoots, "deferred shared r26 resource fallback", null,
                        requestedBy);
                    while (scan.MoveNext())
                    {
                        if (epoch != _lateDonorRecoveryEpoch) yield break;
                        if (!CanRecoverInCurrentScene())
                        {
                            _lateDonorRecoveryQueued = false;
                            yield break;
                        }
                        yield return scan.Current;
                    }
                }
            }

            if (epoch != _lateDonorRecoveryEpoch) yield break;
            _lateDonorRecoveryQueued = false;
            if (!CanRecoverInCurrentScene()) yield break;
            // Preserve r31's one-time fallback guarantee: if a donor still
            // cannot be resolved, do not trigger the expensive scan again
            // on every boss watchdog tick. The earlier async-success path
            // intentionally leaves recovery available for the other boss.
            _lateDonorRecoveryCompleted = true;
            Plugin.NativeLog.LogInfo(
                "[LateBossDonors] recovery complete. VarkeshReady=[" +
                VarkeshWorldSpawnRuntime.NativeTemplateReady +
                "] GorathReady=[" + IsGorathDonorReady() + "]. " +
                "The normal world-boss watchdogs will retry a missing live spawn.");
        }


        private static bool RequestedDonorReady(string requestedBy)
        {
            if (!string.IsNullOrEmpty(requestedBy))
            {
                if (requestedBy.IndexOf("Varkesh",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                    return VarkeshWorldSpawnRuntime.NativeTemplateReady;
                if (requestedBy.IndexOf("Gorath",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                    return IsGorathDonorReady();
            }
            return LateDonorsReady();
        }

        private static bool CanRecoverInCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            return WorldBossPersistencePolicy.CanExistInWorld &&
                scene.IsValid() && scene.isLoaded &&
                (string.Equals(scene.name,
                    VarkeshWorldSpawnRuntime.SpawnSceneName,
                    StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(scene.name,
                    GorathWorldSpawnRuntime.SpawnSceneName,
                    StringComparison.OrdinalIgnoreCase));
        }


        private static bool IsGorathDonorReady()
        {
            VisualTemplate template;
            return TryGetCachedTemplate(
                GorathWorldSpawnRuntime.NativeTemplateNpcName,
                GorathWorldSpawnRuntime.NativeTemplateNpcAlternateName,
                out template);
        }


        private static bool LateDonorsReady()
        {
            return VarkeshWorldSpawnRuntime.NativeTemplateReady &&
                IsGorathDonorReady();
        }


        private static IEnumerator ScanLateDonorRoots(
            GameObject[] roots, string origin, string resourceFolder = null,
            string requestedBy = null)
        {
            // Each root may contain the NPC prefab directly OR hold a
            // SpawnPoint field referencing it. Run both passes, each with a
            // fixed per-frame work budget; share the same roots for both bosses.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < roots.Length; i++)
                {
                    if (RequestedDonorReady(requestedBy)) yield break;
                    GameObject root = roots[i];
                    if (root != null)
                    {
                        // r91: Remember which Resource root actually contains
                        // each donor. The donor's NPC name is not necessarily
                        // its addressable Resources asset name. Resolve the
                        // parent prefab's exact path while it is resident so
                        // the next cold session can try Resources.LoadAsync.
                        bool hadVarkesh = VarkeshWorldSpawnRuntime.NativeTemplateReady;
                        bool hadGorath = IsGorathDonorReady();
                        if (!VarkeshWorldSpawnRuntime.NativeTemplateReady)
                        {
                            VarkeshWorldSpawnRuntime.PrimeNativeTemplateFromResourceRoot(
                                root, pass == 0, origin);
                        }

                        if (!IsGorathDonorReady())
                        {
                            NPC source = null;
                            if (pass == 0)
                            {
                                source = FindMatchingNpcInRoot(
                                    root,
                                    GorathWorldSpawnRuntime.NativeTemplateNpcName,
                                    GorathWorldSpawnRuntime.NativeTemplateNpcAlternateName);
                            }
                            else
                            {
                                SpawnPoint[] points = null;
                                try { points = root.GetComponentsInChildren<SpawnPoint>(true); }
                                catch { }
                                for (int s = 0; points != null && s < points.Length; s++)
                                {
                                    source = FindReferencedNpc(
                                        points[s],
                                        GorathWorldSpawnRuntime.NativeTemplateNpcName,
                                        GorathWorldSpawnRuntime.NativeTemplateNpcAlternateName);
                                    if (source != null) break;
                                }
                            }

                            if (source != null)
                            {
                                VisualTemplate cached;
                                if (CacheTemplateFromSource(
                                    source,
                                    GorathWorldSpawnRuntime.NativeTemplateNpcName,
                                    GorathWorldSpawnRuntime.NativeTemplateNpcAlternateName,
                                    out cached))
                                {
                                    Plugin.NativeLog.LogInfo(
                                        "[LateBossDonors] Gorath donor recovered from [" +
                                        origin + "].");
                                }
                            }
                        }
                        if (!hadVarkesh && VarkeshWorldSpawnRuntime.NativeTemplateReady)
                            WorldBossDonorPrecache.TryRememberRecoveredRoot(
                                root, resourceFolder, "Varkesh");
                        if (!hadGorath && IsGorathDonorReady())
                            WorldBossDonorPrecache.TryRememberRecoveredRoot(
                                root, resourceFolder, "Gorath");
                    }
                    if ((i + 1) % RecoveryRootsPerFrame == 0)
                        yield return null;
                }
            }
        }


        private static NPC FindMatchingNpcInResourceRoots(
            GameObject[] roots,
            string primaryName,
            string alternateName)
        {
            if (roots == null)
            {
                return null;
            }

            // Direct NPC/prefab pass.
            for (int i = 0;
                 i < roots.Length;
                 i++)
            {
                NPC source =
                    FindMatchingNpcInRoot(
                        roots[i],
                        primaryName,
                        alternateName);

                if (source != null)
                {
                    return source;
                }
            }

            // SpawnPoint-reference pass. Some Erenshor native enemies are
            // referenced by a SpawnPoint asset instead of parented beneath it.
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
                    NPC source =
                        FindReferencedNpc(
                            spawnPoints[s],
                            primaryName,
                            alternateName);

                    if (source != null)
                    {
                        return source;
                    }
                }
            }

            return null;
        }


        private static NPC FindReferencedNpc(
            SpawnPoint spawnPoint,
            string primaryName,
            string alternateName)
        {
            if (spawnPoint == null)
            {
                return null;
            }

            NPC spawned =
                spawnPoint.SpawnedNPC;

            if (spawned != null &&
                NameMatches(
                    spawned,
                    primaryName,
                    alternateName))
            {
                return spawned;
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

                    if (npc != null &&
                        NameMatches(
                            npc,
                            primaryName,
                            alternateName))
                    {
                        return npc;
                    }

                    GameObject gameObject =
                        value as GameObject;

                    NPC fromRoot =
                        FindMatchingNpcInRoot(
                            gameObject,
                            primaryName,
                            alternateName);

                    if (fromRoot != null)
                    {
                        return fromRoot;
                    }

                    Component component =
                        value as Component;

                    if (component != null)
                    {
                        fromRoot =
                            FindMatchingNpcInRoot(
                                component.gameObject,
                                primaryName,
                                alternateName);

                        if (fromRoot != null)
                        {
                            return fromRoot;
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }


        private static bool TryResolveFromLoadedAssetBundles(
            string primaryName,
            string alternateName,
            out NPC source,
            out string origin)
        {
            source =
                null;

            origin =
                null;

            try
            {
                IEnumerable<AssetBundle> loadedBundles =
                    AssetBundle.GetAllLoadedAssetBundles();

                if (loadedBundles == null)
                {
                    return false;
                }

                foreach (AssetBundle bundle in loadedBundles)
                {
                    if (bundle == null)
                    {
                        continue;
                    }

                    string[] assetNames =
                        null;

                    try
                    {
                        assetNames =
                            bundle.GetAllAssetNames();
                    }
                    catch
                    {
                        continue;
                    }

                    for (int i = 0;
                         assetNames != null &&
                         i < assetNames.Length;
                         i++)
                    {
                        string assetName =
                            assetNames[i];

                        if (!LooksLikeNativeVisualAsset(
                                assetName,
                                primaryName,
                                alternateName))
                        {
                            continue;
                        }

                        GameObject root =
                            null;

                        try
                        {
                            root =
                                bundle.LoadAsset<GameObject>(
                                    assetName);
                        }
                        catch
                        {
                            continue;
                        }

                        source =
                            FindMatchingNpcInRoot(
                                root,
                                primaryName,
                                alternateName);

                        if (source == null)
                        {
                            continue;
                        }

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
                    "[WorldBossVisual] targeted loaded-AssetBundle lookup failed: " +
                    exception.Message);
            }

            return false;
        }


        private static bool TryResolveFromExactResources(
            string primaryName,
            string alternateName,
            out NPC source,
            out string origin)
        {
            source =
                null;

            origin =
                null;

            List<string> candidatePaths =
                BuildExactResourceCandidates(
                    primaryName,
                    alternateName);

            for (int i = 0;
                 i < candidatePaths.Count;
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
                        "[WorldBossVisual] exact Resources.Load failed for path [" +
                        path +
                        "]: " +
                        exception.Message);

                    continue;
                }

                if (root == null)
                {
                    continue;
                }

                source =
                    FindMatchingNpcInRoot(
                        root,
                        primaryName,
                        alternateName);

                if (source == null)
                {
                    continue;
                }

                origin =
                    "exact Resources path [" +
                    path +
                    "]";

                return true;
            }

            return false;
        }


        private static List<string> BuildExactResourceCandidates(
            string primaryName,
            string alternateName)
        {
            List<string> names =
                new List<string>();

            AddUnique(
                names,
                primaryName);

            AddUnique(
                names,
                alternateName);

            if (!string.IsNullOrEmpty(primaryName) &&
                primaryName.StartsWith(
                    "An ",
                    StringComparison.OrdinalIgnoreCase))
            {
                AddUnique(
                    names,
                    primaryName.Substring(3));
            }

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

            List<string> paths =
                new List<string>();

            for (int n = 0;
                 n < names.Count;
                 n++)
            {
                string name =
                    names[n];

                string underscored =
                    name.Replace(
                        ' ',
                        '_');

                for (int p = 0;
                     p < prefixes.Length;
                     p++)
                {
                    AddUnique(
                        paths,
                        prefixes[p] +
                        name);

                    AddUnique(
                        paths,
                        prefixes[p] +
                        underscored);
                }
            }

            return paths;
        }


        private static void AddUnique(
            List<string> values,
            string value)
        {
            if (values == null ||
                string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            for (int i = 0;
                 i < values.Count;
                 i++)
            {
                if (string.Equals(
                        values[i],
                        value,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            values.Add(
                value);
        }


        private static bool LooksLikeNativeVisualAsset(
            string assetName,
            string primaryName,
            string alternateName)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                return false;
            }

            string normalized =
                assetName
                    .Replace("_", " ")
                    .Replace("-", " ")
                    .Replace("/", " ")
                    .ToLowerInvariant();

            if (normalized.IndexOf(
                    "sivakaya",
                    StringComparison.Ordinal) < 0 ||
                normalized.IndexOf(
                    "apparition",
                    StringComparison.Ordinal) < 0)
            {
                return false;
            }

            return true;
        }


        private static NPC FindMatchingNpcInRoot(
            GameObject root,
            string primaryName,
            string alternateName)
        {
            if (root == null)
            {
                return null;
            }

            NPC direct =
                root.GetComponent<NPC>();

            if (direct != null &&
                NameMatches(
                    direct,
                    primaryName,
                    alternateName))
            {
                return direct;
            }

            NPC[] candidates =
                root.GetComponentsInChildren<NPC>(
                    true);

            for (int i = 0;
                 candidates != null &&
                 i < candidates.Length;
                 i++)
            {
                NPC candidate =
                    candidates[i];

                if (candidate != null &&
                    NameMatches(
                        candidate,
                        primaryName,
                        alternateName))
                {
                    return candidate;
                }
            }

            return null;
        }


        // A cache-only check for prewarming. Unlike CanResolve, this does not
        // probe scene SpawnPoints, bundles or Resources on every call.
        internal static bool HasCachedTemplate(string primaryName, string alternateName)
        {
            VisualTemplate template;
            return TryGetCachedTemplate(primaryName, alternateName, out template);
        }

        // Called ONLY with a prefab obtained by an asynchronous preload or
        // an already-loaded bundle. Creates the same sanitized template used
        // by the normal Gorath path without changing gameplay ownership.
        internal static bool TryPrimeFromPreloadedRoot(GameObject root, string origin)
        {
            if (root == null) return false;
            if (HasCachedTemplate(GorathWorldSpawnRuntime.NativeTemplateNpcName,
                GorathWorldSpawnRuntime.NativeTemplateNpcAlternateName)) return true;
            NPC donor = FindMatchingNpcInRoot(root,
                GorathWorldSpawnRuntime.NativeTemplateNpcName,
                GorathWorldSpawnRuntime.NativeTemplateNpcAlternateName);
            if (donor == null)
            {
                SpawnPoint[] points = null;
                try { points = root.GetComponentsInChildren<SpawnPoint>(true); }
                catch { }
                for (int i = 0; points != null && i < points.Length; i++)
                {
                    donor = FindReferencedNpc(points[i],
                        GorathWorldSpawnRuntime.NativeTemplateNpcName,
                        GorathWorldSpawnRuntime.NativeTemplateNpcAlternateName);
                    if (donor != null) break;
                }
            }
            VisualTemplate template;
            bool ready = donor != null && CacheTemplateFromSource(donor,
                GorathWorldSpawnRuntime.NativeTemplateNpcName,
                GorathWorldSpawnRuntime.NativeTemplateNpcAlternateName, out template);
            if (ready)
                Plugin.NativeLog.LogInfo("[WorldBossPrecache] Gorath model cached from [" + origin + "].");
            return ready;
        }

        internal static bool CanResolve(
            string primaryName,
            string alternateName)
        {
            VisualTemplate template;

            return
                TryResolveTemplate(
                    primaryName,
                    alternateName,
                    out template);
        }


        internal static GameObject CreateVisualRoot(
            FOTFCreatureDefinition definition,
            Scene scene,
            Vector3 position,
            Quaternion rotation)
        {
            if (definition == null ||
                definition.VisualSource !=
                    FOTFCreatureVisualSource.NativeNpcVisual ||
                !scene.IsValid() ||
                !scene.isLoaded)
            {
                return null;
            }

            VisualTemplate template;

            if (!TryResolveTemplate(
                    definition.NativeVisualNpcName,
                    definition.NativeVisualNpcAlternateName,
                    out template) ||
                template == null ||
                template.Root == null)
            {
                return null;
            }

            GameObject wrapper =
                new GameObject(
                    "FOTF_CustomVisual_" +
                    SanitizeName(
                        definition.DisplayName));

            wrapper.transform.SetPositionAndRotation(
                position,
                rotation);

            wrapper.SetActive(
                false);

            SceneManager.MoveGameObjectToScene(
                wrapper,
                scene);

            GameObject visual =
                UnityEngine.Object.Instantiate(
                    template.Root,
                    wrapper.transform,
                    false);

            if (visual == null)
            {
                UnityEngine.Object.Destroy(
                    wrapper);

                return null;
            }

            visual.name =
                "FOTF_NativeVisual_" +
                SanitizeName(
                    definition.DisplayName);

            visual.transform.localPosition =
                Vector3.zero;

            visual.transform.localRotation =
                Quaternion.identity;

            float visualScale =
                definition.NativeVisualScale >
                    0f
                    ? definition.NativeVisualScale
                    : 1f;

            visual.transform.localScale =
                template.Root.transform.localScale *
                visualScale;

            visual.SetActive(
                true);

            Plugin.NativeLog.LogInfo(
                "[WorldBossVisual] created visual-only native donor [" +
                definition.NativeVisualNpcName +
                "] for [" +
                definition.DisplayName +
                "] Scale=[" +
                visualScale.ToString("0.00") +
                "].");

            return wrapper;
        }


        internal static Transform FindVisualRoot(
            GameObject bossRoot)
        {
            if (bossRoot == null)
            {
                return null;
            }

            Transform root =
                bossRoot.transform;

            for (int i = 0;
                 i < root.childCount;
                 i++)
            {
                Transform child =
                    root.GetChild(
                        i);

                if (child == null)
                {
                    continue;
                }

                string name =
                    child.name ??
                    string.Empty;

                if (name.StartsWith(
                        "FOTF_NativeVisual_",
                        StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }


        private static bool TryResolveTemplate(
            string primaryName,
            string alternateName,
            out VisualTemplate template,
            bool logFailure = true)
        {
            template =
                null;

            string key =
                !string.IsNullOrEmpty(
                    primaryName)
                    ? primaryName
                    : alternateName;

            if (string.IsNullOrEmpty(
                    key))
            {
                return false;
            }

            if (TryGetCachedTemplate(
                    primaryName,
                    alternateName,
                    out template))
            {
                return true;
            }

            NPC source =
                FindNativeNpc(
                    primaryName,
                    alternateName);

            if (source == null)
            {
                Scene active =
                    SceneManager.GetActiveScene();

                string probeKey =
                    (active.IsValid()
                        ? active.handle.ToString()
                        : "no-scene") +
                    "|" +
                    key;

                if (TargetedResidentProbeAttempts.Add(
                        probeKey))
                {
                    string origin;

                    TryResolveFromLoadedAssetBundles(
                        primaryName,
                        alternateName,
                        out source,
                        out origin);

                    if (source == null)
                    {
                        TryResolveFromExactResources(
                            primaryName,
                            alternateName,
                            out source,
                            out origin);
                    }
                }
            }

            if (source == null)
            {
                if (logFailure &&
                    MissingSourceWarnings.Add(
                        key))
                {
                    Plugin.NativeLog.LogWarning(
                        "[WorldBossVisual] native visual source was not found by scene-local/targeted lookup. Primary=[" +
                        (primaryName ?? "<null>") +
                        "] Alternate=[" +
                        (alternateName ?? "<null>") +
                        "]. No global Resources sweep was used.");
                }

                return false;
            }

            return
                CacheTemplateFromSource(
                    source,
                    primaryName,
                    alternateName,
                    out template);
        }


        private static bool TryGetCachedTemplate(
            string primaryName,
            string alternateName,
            out VisualTemplate template)
        {
            template =
                null;

            string key =
                !string.IsNullOrEmpty(primaryName)
                    ? primaryName
                    : alternateName;

            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (Templates.TryGetValue(
                    key,
                    out template) &&
                template != null &&
                template.Root != null)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(alternateName) &&
                Templates.TryGetValue(
                    alternateName,
                    out template) &&
                template != null &&
                template.Root != null)
            {
                return true;
            }

            template =
                null;

            return false;
        }


        private static bool CacheTemplateFromSource(
            NPC source,
            string primaryName,
            string alternateName,
            out VisualTemplate template)
        {
            template =
                null;

            string key =
                !string.IsNullOrEmpty(primaryName)
                    ? primaryName
                    : alternateName;

            if (source == null ||
                string.IsNullOrEmpty(key))
            {
                return false;
            }

            MissingSourceWarnings.Remove(
                key);

            GameObject sanitized =
                CreateSanitizedTemplate(
                    source,
                    key);

            if (sanitized == null)
            {
                return false;
            }

            template =
                new VisualTemplate
                {
                    Key =
                        key,

                    Root =
                        sanitized
                };

            Templates[key] =
                template;

            if (!string.IsNullOrEmpty(alternateName))
            {
                Templates[alternateName] =
                    template;
            }

            Plugin.NativeLog.LogInfo(
                "[WorldBossVisual] cached visual-only native template [" +
                key +
                "] Animator=[" +
                DescribeAnimator(
                    sanitized) +
                "].");

            return true;
        }



        private static NPC FindNativeNpc(
            string primaryName,
            string alternateName)
        {
            // Hot-path resolver used by CanResolve(). Only inspect the already
            // published SpawnPoint list for the active scene. Do not allocate a
            // global Resources.FindObjectsOfTypeAll<NPC>() array every retry.
            Scene active =
                SceneManager.GetActiveScene();

            return
                FindNativeNpcInPublishedSpawnPoints(
                    active,
                    primaryName,
                    alternateName);
        }


        private static NPC FindNativeNpcInPublishedSpawnPoints(
            Scene scene,
            string primaryName,
            string alternateName)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                SpawnPointManager.SpawnPointsInScene == null)
            {
                return null;
            }

            Scene active =
                SceneManager.GetActiveScene();

            // SpawnPointsInScene belongs to the current active scene. Avoid
            // accidentally treating it as a donor list for an additive scene.
            if (!active.IsValid() ||
                active.handle != scene.handle)
            {
                return null;
            }

            try
            {
                foreach (SpawnPoint spawnPoint in
                         SpawnPointManager.SpawnPointsInScene)
                {
                    if (spawnPoint == null)
                    {
                        continue;
                    }

                    NPC spawned =
                        spawnPoint.SpawnedNPC;

                    if (spawned != null &&
                        NameMatches(
                            spawned,
                            primaryName,
                            alternateName))
                    {
                        return spawned;
                    }

                    NPC referenced =
                        FindReferencedNpc(
                            spawnPoint,
                            primaryName,
                            alternateName);

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


        private static NPC FindNativeNpcInScene(
            Scene scene,
            string primaryName,
            string alternateName)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded)
            {
                return null;
            }

            NPC published =
                FindNativeNpcInPublishedSpawnPoints(
                    scene,
                    primaryName,
                    alternateName);

            if (published != null)
            {
                return published;
            }

            GameObject[] roots;

            try
            {
                roots =
                    scene.GetRootGameObjects();
            }
            catch
            {
                return null;
            }

            for (int i = 0;
                 roots != null &&
                 i < roots.Length;
                 i++)
            {
                GameObject root =
                    roots[i];

                if (root == null)
                {
                    continue;
                }

                NPC source =
                    FindMatchingNpcInRoot(
                        root,
                        primaryName,
                        alternateName);

                if (source != null)
                {
                    return source;
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
                    source =
                        FindReferencedNpc(
                            spawnPoints[s],
                            primaryName,
                            alternateName);

                    if (source != null)
                    {
                        return source;
                    }
                }
            }

            return null;
        }


        private static bool NameMatches(
            NPC npc,
            string primaryName,
            string alternateName)
        {
            if (npc == null)
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

            if (string.IsNullOrEmpty(
                    name) &&
                npc.gameObject != null)
            {
                name =
                    npc.gameObject.name;
            }

            return
                (!string.IsNullOrEmpty(
                     primaryName) &&
                 string.Equals(
                     name,
                     primaryName,
                     StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(
                     alternateName) &&
                 string.Equals(
                     name,
                     alternateName,
                     StringComparison.OrdinalIgnoreCase));
        }


        private static GameObject CreateSanitizedTemplate(
            NPC source,
            string key)
        {
            if (source == null ||
                source.gameObject == null)
            {
                return null;
            }

            try
            {
                // Do NOT instantiate the native NPC and then DestroyImmediate
                // its gameplay components. Unity RequireComponent relationships
                // inside native NPC prefabs make destructive stripping unsafe
                // during plugin startup and can crash the player outright.
                //
                // Instead, construct a brand-new visual-only hierarchy from the
                // donor's transforms, Animator, meshes, materials and lights.
                // No Character/NPC/Stats/UseSkill/Collider/NavMesh component is
                // ever copied into this template.
                GameObject visualOnly =
                    CreateVisualOnlyHierarchy(
                        source.gameObject,
                        key);

                if (visualOnly == null)
                {
                    return null;
                }

                visualOnly.name =
                    "FOTF_NativeVisualTemplate_" +
                    SanitizeName(
                        key);

                visualOnly.SetActive(
                    false);

                UnityEngine.Object.DontDestroyOnLoad(
                    visualOnly);

                return visualOnly;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[WorldBossVisual] failed creating visual-only template [" +
                    key +
                    "]: " +
                    exception);

                return null;
            }
        }


        private static GameObject CreateVisualOnlyHierarchy(
            GameObject sourceRoot,
            string key)
        {
            if (sourceRoot == null)
            {
                return null;
            }

            Dictionary<Transform, Transform> map =
                new Dictionary<Transform, Transform>();

            Transform clonedRoot =
                CloneTransformTree(
                    sourceRoot.transform,
                    null,
                    map,
                    true);

            if (clonedRoot == null)
            {
                return null;
            }

            GameObject result =
                clonedRoot.gameObject;

            result.SetActive(
                false);

            CopyVisualComponents(
                map);

            // Restore authored child active states only after all visual
            // components have been copied. The root remains inactive until the
            // custom creature runtime deliberately instantiates it.
            foreach (KeyValuePair<Transform, Transform> pair
                     in map)
            {
                if (pair.Key == null ||
                    pair.Value == null ||
                    pair.Value == clonedRoot)
                {
                    continue;
                }

                pair.Value.gameObject.SetActive(
                    pair.Key.gameObject.activeSelf);
            }

            Plugin.NativeLog.LogInfo(
                "[WorldBossVisual] built non-destructive visual-only hierarchy [" +
                key +
                "] Transforms=[" +
                map.Count +
                "].");

            return result;
        }


        private static Transform CloneTransformTree(
            Transform source,
            Transform parent,
            Dictionary<Transform, Transform> map,
            bool isRoot)
        {
            if (source == null)
            {
                return null;
            }

            if (!isRoot &&
                IsNativeSupportTransformName(
                    source.name))
            {
                return null;
            }

            GameObject cloneObject =
                new GameObject(
                    source.name);

            cloneObject.SetActive(
                false);

            cloneObject.layer =
                source.gameObject.layer;

            Transform clone =
                cloneObject.transform;

            clone.SetParent(
                parent,
                false);

            clone.localPosition =
                source.localPosition;

            clone.localRotation =
                source.localRotation;

            clone.localScale =
                source.localScale;

            map[source] =
                clone;

            for (int i = 0;
                 i < source.childCount;
                 i++)
            {
                CloneTransformTree(
                    source.GetChild(i),
                    clone,
                    map,
                    false);
            }

            return clone;
        }


        private static bool IsNativeSupportTransformName(
            string name)
        {
            if (string.IsNullOrEmpty(
                    name))
            {
                return false;
            }

            return
                name.IndexOf(
                    "NamePlate",
                    StringComparison.OrdinalIgnoreCase) >=
                0 ||
                name.IndexOf(
                    "Nameplate",
                    StringComparison.OrdinalIgnoreCase) >=
                0 ||
                name.IndexOf(
                    "TargetRing",
                    StringComparison.OrdinalIgnoreCase) >=
                0 ||
                name.IndexOf(
                    "AggroArea",
                    StringComparison.OrdinalIgnoreCase) >=
                0;
        }


        private static void CopyVisualComponents(
            Dictionary<Transform, Transform> map)
        {
            if (map == null)
            {
                return;
            }

            foreach (KeyValuePair<Transform, Transform> pair
                     in map)
            {
                Transform sourceTransform =
                    pair.Key;

                Transform targetTransform =
                    pair.Value;

                if (sourceTransform == null ||
                    targetTransform == null)
                {
                    continue;
                }

                CopyAnimators(
                    sourceTransform,
                    targetTransform);

                CopyMeshFilters(
                    sourceTransform,
                    targetTransform);

                CopyMeshRenderers(
                    sourceTransform,
                    targetTransform,
                    map);

                CopySkinnedMeshRenderers(
                    sourceTransform,
                    targetTransform,
                    map);

                CopyLights(
                    sourceTransform,
                    targetTransform);
            }
        }


        private static void CopyAnimators(
            Transform source,
            Transform target)
        {
            Animator[] sourceAnimators =
                source.GetComponents<Animator>();

            for (int i = 0;
                 sourceAnimators != null &&
                 i < sourceAnimators.Length;
                 i++)
            {
                Animator sourceAnimator =
                    sourceAnimators[i];

                if (sourceAnimator == null)
                {
                    continue;
                }

                Animator targetAnimator =
                    target.gameObject.AddComponent<Animator>();

                targetAnimator.runtimeAnimatorController =
                    sourceAnimator.runtimeAnimatorController;

                targetAnimator.avatar =
                    sourceAnimator.avatar;

                targetAnimator.applyRootMotion =
                    false;

                targetAnimator.updateMode =
                    sourceAnimator.updateMode;

                targetAnimator.cullingMode =
                    sourceAnimator.cullingMode;

                targetAnimator.speed =
                    sourceAnimator.speed;

                targetAnimator.enabled =
                    sourceAnimator.enabled;
            }
        }


        private static void CopyMeshFilters(
            Transform source,
            Transform target)
        {
            MeshFilter[] sourceFilters =
                source.GetComponents<MeshFilter>();

            for (int i = 0;
                 sourceFilters != null &&
                 i < sourceFilters.Length;
                 i++)
            {
                MeshFilter sourceFilter =
                    sourceFilters[i];

                if (sourceFilter == null)
                {
                    continue;
                }

                MeshFilter targetFilter =
                    target.gameObject.AddComponent<MeshFilter>();

                targetFilter.sharedMesh =
                    sourceFilter.sharedMesh;
            }
        }


        private static void CopyMeshRenderers(
            Transform source,
            Transform target,
            Dictionary<Transform, Transform> map)
        {
            MeshRenderer[] sourceRenderers =
                source.GetComponents<MeshRenderer>();

            for (int i = 0;
                 sourceRenderers != null &&
                 i < sourceRenderers.Length;
                 i++)
            {
                MeshRenderer sourceRenderer =
                    sourceRenderers[i];

                if (sourceRenderer == null)
                {
                    continue;
                }

                MeshRenderer targetRenderer =
                    target.gameObject.AddComponent<MeshRenderer>();

                CopyRendererSettings(
                    sourceRenderer,
                    targetRenderer,
                    map);
            }
        }


        private static void CopySkinnedMeshRenderers(
            Transform source,
            Transform target,
            Dictionary<Transform, Transform> map)
        {
            SkinnedMeshRenderer[] sourceRenderers =
                source.GetComponents<SkinnedMeshRenderer>();

            for (int i = 0;
                 sourceRenderers != null &&
                 i < sourceRenderers.Length;
                 i++)
            {
                SkinnedMeshRenderer sourceRenderer =
                    sourceRenderers[i];

                if (sourceRenderer == null)
                {
                    continue;
                }

                SkinnedMeshRenderer targetRenderer =
                    target.gameObject.AddComponent<SkinnedMeshRenderer>();

                targetRenderer.sharedMesh =
                    sourceRenderer.sharedMesh;

                targetRenderer.localBounds =
                    sourceRenderer.localBounds;

                targetRenderer.quality =
                    sourceRenderer.quality;

                targetRenderer.updateWhenOffscreen =
                    sourceRenderer.updateWhenOffscreen;

                targetRenderer.skinnedMotionVectors =
                    sourceRenderer.skinnedMotionVectors;

                if (sourceRenderer.rootBone != null)
                {
                    Transform mappedRoot;

                    if (map.TryGetValue(
                            sourceRenderer.rootBone,
                            out mappedRoot))
                    {
                        targetRenderer.rootBone =
                            mappedRoot;
                    }
                }

                Transform[] sourceBones =
                    sourceRenderer.bones;

                if (sourceBones != null)
                {
                    Transform[] targetBones =
                        new Transform[sourceBones.Length];

                    for (int n = 0;
                         n < sourceBones.Length;
                         n++)
                    {
                        Transform sourceBone =
                            sourceBones[n];

                        Transform mappedBone;

                        if (sourceBone != null &&
                            map.TryGetValue(
                                sourceBone,
                                out mappedBone))
                        {
                            targetBones[n] =
                                mappedBone;
                        }
                    }

                    targetRenderer.bones =
                        targetBones;
                }

                CopyRendererSettings(
                    sourceRenderer,
                    targetRenderer,
                    map);

                Mesh sharedMesh =
                    sourceRenderer.sharedMesh;

                if (sharedMesh != null)
                {
                    int blendShapeCount =
                        sharedMesh.blendShapeCount;

                    for (int n = 0;
                         n < blendShapeCount;
                         n++)
                    {
                        targetRenderer.SetBlendShapeWeight(
                            n,
                            sourceRenderer.GetBlendShapeWeight(
                                n));
                    }
                }
            }
        }


        private static void CopyRendererSettings(
            Renderer source,
            Renderer target,
            Dictionary<Transform, Transform> map)
        {
            if (source == null ||
                target == null)
            {
                return;
            }

            target.sharedMaterials =
                source.sharedMaterials;

            target.shadowCastingMode =
                source.shadowCastingMode;

            target.receiveShadows =
                source.receiveShadows;

            target.lightProbeUsage =
                source.lightProbeUsage;

            target.reflectionProbeUsage =
                source.reflectionProbeUsage;

            target.sortingLayerID =
                source.sortingLayerID;

            target.sortingOrder =
                source.sortingOrder;

            if (source.probeAnchor != null)
            {
                Transform mappedAnchor;

                if (map.TryGetValue(
                        source.probeAnchor,
                        out mappedAnchor))
                {
                    target.probeAnchor =
                        mappedAnchor;
                }
            }

            target.enabled =
                source.enabled;
        }


        private static void CopyLights(
            Transform source,
            Transform target)
        {
            Light[] sourceLights =
                source.GetComponents<Light>();

            for (int i = 0;
                 sourceLights != null &&
                 i < sourceLights.Length;
                 i++)
            {
                Light sourceLight =
                    sourceLights[i];

                if (sourceLight == null)
                {
                    continue;
                }

                Light targetLight =
                    target.gameObject.AddComponent<Light>();

                targetLight.type =
                    sourceLight.type;

                targetLight.color =
                    sourceLight.color;

                targetLight.intensity =
                    sourceLight.intensity;

                targetLight.range =
                    sourceLight.range;

                targetLight.spotAngle =
                    sourceLight.spotAngle;

                targetLight.shadows =
                    sourceLight.shadows;

                targetLight.enabled =
                    sourceLight.enabled;
            }
        }


        private static string DescribeAnimator(
            GameObject root)
        {
            if (root == null)
            {
                return "<null>";
            }

            Animator animator =
                root.GetComponentInChildren<Animator>(
                    true);

            if (animator == null)
            {
                return "<none>";
            }

            return
                "Controller=" +
                (animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.name
                    : "<null>") +
                " Avatar=" +
                (animator.avatar != null
                    ? animator.avatar.name
                    : "<null>") +
                " Human=" +
                (animator.avatar != null &&
                 animator.avatar.isHuman);
        }


        private static string SanitizeName(
            string value)
        {
            if (string.IsNullOrEmpty(
                    value))
            {
                return "Unnamed";
            }

            return
                value.Replace(
                    " ",
                    "_")
                    .Replace(
                        "'",
                        string.Empty);
        }
    }
}
