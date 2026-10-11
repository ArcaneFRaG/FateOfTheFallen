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
    /// Quest 4 risen-dead encounter.
    ///
    /// Native source:
    ///   A Skeleton Soldier
    ///
    /// Resolution order:
    ///  1. Exact loaded SpawnPoint instance/prefab.
    ///  2. Exact loaded NPC instance/prefab.
    ///  3. Force-load Resources GameObjects, then search again.
    ///  4. Bone Pits live capture fallback.
    ///
    /// If an NPC prefab is globally available but its SpawnPoint is not,
    /// the encounter can instantiate the native NPC directly.
    /// </summary>
    internal static class RestlessDeadRuntime
    {
        internal const string NativeTemplateNpcName =
            "A Skeleton Soldier";

        private const string NativeSourceSceneName =
            "Bonepits";

        internal const string QuestMobName =
            "Restless Dead";

        private const string SpawnPointTemplateObjectName =
            "FOTF_Quest04_SkeletonSoldier_SpawnPointTemplate";

        private const string NpcTemplateObjectName =
            "FOTF_Quest04_SkeletonSoldier_NpcTemplate";

        private const string RuntimeSpawnPointPrefix =
            "FOTF_Quest04_RestlessDead_SpawnPoint_";

        private const string RuntimeNpcPrefix =
            "FOTF_Quest04_RestlessDead_";

        private const int EncounterCount =
            3;


        // ============================================================
        // 5-MAN GROUP TUNING
        // ============================================================
        //
        // These are deliberately much lower than FOTF raid trash:
        //
        //   Blighted Thrall raid trash:
        //     Level 42
        //     2,730,000 HP
        //     756 AC
        //     4,000 melee
        //
        // Restless Dead are intended as a three-mob, five-player encounter.
        //
        private const int EncounterLevel =
            38;

        private const int EncounterHP =
            450000;

        private const int EncounterAC =
            500;

        private const int EncounterResistance =
            40;

        // 750 raw was landing at roughly 300-600 after normal player
        // mitigation. 1500 keeps armor relevant while putting these mobs into
        // meaningful five-player elite territory.
        private const int EncounterMeleeDamage =
            1500;

        private const float RespawnDelaySeconds =
            180f;

        private const int MinimumGold =
            60;

        private const int MaximumGold =
            120;

        private const int MinimumBones =
            1;

        private const int MaximumBones =
            3;

        private const float QuestItemDropChance =
            0.20f;

        private const float EncounterRunSpeed =
            4.2f;

        private const float EncounterAggroRange =
            18f;

        private const float EncounterMainHandDelay =
            120f;

        private const int CaptureProbeFrames =
            600;

        private const int CaptureProbeInterval =
            15;

        private const int EncounterPollFrames =
            30;

        private const int SpawnWaitFrames =
            300;

        private static readonly Vector3[] EncounterOffsets =
        {
            new Vector3(-3.2f, 0f, 0.35f),
            new Vector3( 3.2f, 0f, 0.35f),
            new Vector3( 0.0f, 0f, 3.25f)
        };

        private static bool _initialized;
        private static bool _loggedMissingTemplate;
        private static bool _encounterSpawned;
        private static bool _encounterFinished;
        private static bool _resourcesProbeAttempted;
        private static bool _fingerprintLogged;

        private static GameObject _skeletonSpawnPointTemplate;
        private static GameObject _skeletonNpcTemplate;

        private static Coroutine _captureRoutine;
        private static Coroutine _encounterRoutine;

        private static readonly List<EncounterSpawn>
            _encounterSpawns =
                new List<EncounterSpawn>();


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

            Scene activeScene =
                SceneManager.GetActiveScene();

            Plugin.NativeLog.LogInfo(
                "[Quest4Skeletons] initialized. ActiveScene=[" +
                (activeScene.IsValid()
                    ? activeScene.name
                    : "<invalid>") +
                "] expansionUnlocked=[" +
                FOTFExpansionGate.IsUnlocked +
                "].");

            if (!FOTFExpansionGate.IsUnlocked)
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest4Skeletons] expansion is locked; native resolution and Quest 4 encounter population are disabled.");

                return;
            }

            BeginUnlockedRuntime(
                activeScene);
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

            StopCaptureProbe();
            StopEncounterMonitor();

            DestroyEncounter();
            DestroyTemplates();

            _resourcesProbeAttempted =
                false;

            _fingerprintLogged =
                false;

            Plugin.NativeLog.LogInfo(
                "[Quest4Skeletons] shutdown.");
        }


        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            StopCaptureProbe();
            StopEncounterMonitor();

            if (!FOTFExpansionGate.IsUnlocked)
            {
                DestroyEncounter();

                Plugin.NativeLog.LogInfo(
                    "[Quest4Skeletons] scene loaded while expansion is locked; encounter remains disabled.");

                return;
            }

            if (!IsQuestEncounterScene(
                    scene))
            {
                DestroyEncounter();
            }

            BeginUnlockedRuntime(
                scene);
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
                StopCaptureProbe();
                StopEncounterMonitor();
                DestroyEncounter();

                _encounterFinished =
                    false;

                Plugin.NativeLog.LogInfo(
                    "[Quest4Skeletons] expansion lock engaged; removed Quest 4 skeleton encounter.");

                return;
            }

            Plugin.NativeLog.LogInfo(
                "[Quest4Skeletons] expansion unlocked; Quest 4 skeleton runtime re-enabled.");

            BeginUnlockedRuntime(
                SceneManager.GetActiveScene());
        }


        private static void BeginUnlockedRuntime(
            Scene scene)
        {
            if (!_initialized ||
                !FOTFExpansionGate.IsUnlocked ||
                !scene.IsValid() ||
                !scene.isLoaded)
            {
                return;
            }

            // Native skeleton discovery is only relevant in the actual Quest 4
            // encounter scene or in Bonepits, where the donor naturally lives.
            // Do not run global resident-object probes on unrelated transitions
            // such as Willowwatch. Those scans add avoidable scene-load stalls.
            bool questScene =
                IsQuestEncounterScene(scene);

            bool nativeSourceScene =
                string.Equals(
                    scene.name,
                    NativeSourceSceneName,
                    StringComparison.OrdinalIgnoreCase);

            if (questScene || nativeSourceScene)
            {
                TryResolveLoadedNativeSource(false);

                if (!HasNativeTemplate)
                {
                    StartCaptureProbe(scene);
                }
            }

            if (questScene)
            {
                StartEncounterMonitor(scene);
            }
        }


        private static bool HasNativeTemplate
        {
            get
            {
                return
                    _skeletonSpawnPointTemplate != null ||
                    _skeletonNpcTemplate != null;
            }
        }


        // ============================================================
        // COLD NATIVE-ASSET RESOLUTION
        // ============================================================

        private static bool TryResolveLoadedNativeSource(
            bool logFailure)
        {
            if (HasNativeTemplate)
            {
                return true;
            }

            SpawnPoint spawnPoint =
                FindExactSkeletonSpawnPointAnywhere();

            if (spawnPoint != null)
            {
                if (CacheSpawnPointTemplate(
                        spawnPoint,
                        "loaded native SpawnPoint"))
                {
                    return true;
                }
            }

            NPC npc =
                FindExactSkeletonNpcAnywhere();

            if (npc != null)
            {
                if (CacheNpcTemplate(
                        npc,
                        "loaded native NPC/prefab"))
                {
                    return true;
                }
            }

            if (logFailure)
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest4Skeletons] no exact Skeleton Soldier source is currently loaded.");
            }

            return false;
        }


        private static bool TryResolveFromResources()
        {
            if (HasNativeTemplate)
            {
                return true;
            }

            if (_resourcesProbeAttempted)
            {
                return false;
            }

            _resourcesProbeAttempted =
                true;

            Plugin.NativeLog.LogInfo(
                "[Quest4Skeletons] attempting cold native Resources resolution for [" +
                NativeTemplateNpcName +
                "].");

            GameObject[] resourceObjects =
                null;

            try
            {
                // This only loads assets authored under Unity Resources.
                // It does not load or activate a game scene.
                resourceObjects =
                    Resources.LoadAll<GameObject>(
                        "");
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest4Skeletons] Resources.LoadAll<GameObject>(\"\") failed: " +
                    exception.Message);
            }

            if (resourceObjects != null)
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest4Skeletons] native Resources probe loaded [" +
                    resourceObjects.Length +
                    "] GameObject asset(s).");

                // Prefer a SpawnPoint asset because it reproduces the exact
                // native construction path.
                for (int i = 0;
                     i < resourceObjects.Length;
                     i++)
                {
                    GameObject root =
                        resourceObjects[i];

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
                        if (!SpawnPointReferencesSkeletonSoldier(
                                spawnPoints[j]))
                        {
                            continue;
                        }

                        if (CacheSpawnPointTemplate(
                                spawnPoints[j],
                                "Resources SpawnPoint asset"))
                        {
                            return true;
                        }
                    }
                }

                // If the SpawnPoint is scene-authored but the NPC prefab is a
                // Resources asset, use the prefab directly.
                for (int i = 0;
                     i < resourceObjects.Length;
                     i++)
                {
                    GameObject root =
                        resourceObjects[i];

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
                        NPC candidate =
                            npcs[j];

                        if (!IsExactSkeletonNpc(
                                candidate))
                        {
                            continue;
                        }

                        if (CacheNpcTemplate(
                                candidate,
                                "Resources NPC asset"))
                        {
                            return true;
                        }
                    }
                }
            }

            // Resources.LoadAll may cause previously-unloaded prefab assets to
            // appear in FindObjectsOfTypeAll, so perform one final global pass.
            return
                TryResolveLoadedNativeSource(
                    true);
        }


        private static SpawnPoint FindExactSkeletonSpawnPointAnywhere()
        {
            if (SpawnPointManager.SpawnPointsInScene != null)
            {
                foreach (SpawnPoint spawnPoint in
                         SpawnPointManager.SpawnPointsInScene)
                {
                    if (SpawnPointReferencesSkeletonSoldier(
                            spawnPoint))
                    {
                        return spawnPoint;
                    }
                }
            }

            try
            {
                SpawnPoint[] all =
                    Resources.FindObjectsOfTypeAll<SpawnPoint>();

                if (all != null)
                {
                    // Prefer non-scene prefab/assets first.
                    for (int pass = 0;
                         pass < 2;
                         pass++)
                    {
                        for (int i = 0;
                             i < all.Length;
                             i++)
                        {
                            SpawnPoint candidate =
                                all[i];

                            if (candidate == null)
                            {
                                continue;
                            }

                            bool sceneValid =
                                candidate.gameObject.scene.IsValid();

                            if (pass == 0 &&
                                sceneValid)
                            {
                                continue;
                            }

                            if (pass == 1 &&
                                !sceneValid)
                            {
                                continue;
                            }

                            if (SpawnPointReferencesSkeletonSoldier(
                                    candidate))
                            {
                                return candidate;
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


        private static NPC FindExactSkeletonNpcAnywhere()
        {
            try
            {
                NPC[] all =
                    Resources.FindObjectsOfTypeAll<NPC>();

                if (all == null)
                {
                    return null;
                }

                // Prefer prefab/assets outside scenes.
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

                        if (!IsExactSkeletonNpc(
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

                        bool sceneValid =
                            candidate.gameObject.scene.IsValid();

                        if (pass == 0 &&
                            sceneValid)
                        {
                            continue;
                        }

                        if (pass == 1 &&
                            !sceneValid)
                        {
                            continue;
                        }

                        return candidate;
                    }
                }
            }
            catch
            {
            }

            return null;
        }


        private static bool IsExactSkeletonNpc(
            NPC npc)
        {
            return
                npc != null &&
                string.Equals(
                    npc.NPCName,
                    NativeTemplateNpcName,
                    StringComparison.OrdinalIgnoreCase);
        }


        private static bool SpawnPointReferencesSkeletonSoldier(
            SpawnPoint spawnPoint)
        {
            if (spawnPoint == null)
            {
                return false;
            }

            if (IsExactSkeletonNpc(
                    spawnPoint.SpawnedNPC))
            {
                return true;
            }

            // Asset/prefab SpawnPoints often have no runtime SpawnedNPC yet.
            // Inspect their serialized object-reference fields for the source
            // NPC prefab instead.
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

                    if (IsExactSkeletonNpc(
                            npc))
                    {
                        return true;
                    }

                    GameObject gameObject =
                        value as GameObject;

                    if (gameObject != null)
                    {
                        NPC referencedNpc =
                            gameObject.GetComponentInChildren<NPC>(
                                true);

                        if (IsExactSkeletonNpc(
                                referencedNpc))
                        {
                            return true;
                        }
                    }

                    Component component =
                        value as Component;

                    if (component != null)
                    {
                        NPC referencedNpc =
                            component.GetComponentInChildren<NPC>(
                                true);

                        if (IsExactSkeletonNpc(
                                referencedNpc))
                        {
                            return true;
                        }
                    }

                    string text =
                        value as string;

                    if (!string.IsNullOrEmpty(
                            text) &&
                        text.IndexOf(
                            "Skeleton Soldier",
                            StringComparison.OrdinalIgnoreCase) >=
                        0)
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


        private static bool CacheSpawnPointTemplate(
            SpawnPoint source,
            string origin)
        {
            if (source == null)
            {
                return false;
            }

            bool sourceWasActive =
                source.gameObject.activeSelf;

            try
            {
                // For live scene objects, suppress activation of the temporary
                // clone. Prefab assets are already non-scene objects.
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
                    SpawnPointTemplateObjectName;

                clone.transform.SetParent(
                    null,
                    true);

                clone.SetActive(
                    false);

                UnityEngine.Object.DontDestroyOnLoad(
                    clone);

                _skeletonSpawnPointTemplate =
                    clone;

                NPC sourceNpc =
                    source.SpawnedNPC;

                if (sourceNpc != null)
                {
                    DumpVisualFingerprint(
                        sourceNpc,
                        origin);
                }
                else
                {
                    NPC templateNpc =
                        FindReferencedSkeletonNpc(
                            source);

                    if (templateNpc != null)
                    {
                        DumpVisualFingerprint(
                            templateNpc,
                            origin);
                    }
                }

                _loggedMissingTemplate =
                    false;

                Plugin.NativeLog.LogInfo(
                    "[Quest4Skeletons] resolved native Skeleton Soldier SpawnPoint from [" +
                    origin +
                    "]. Bone Pits visit is not required for this session.");

                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest4Skeletons] failed to cache SpawnPoint template from [" +
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


        private static NPC FindReferencedSkeletonNpc(
            SpawnPoint spawnPoint)
        {
            if (spawnPoint == null)
            {
                return null;
            }

            if (IsExactSkeletonNpc(
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

                    if (IsExactSkeletonNpc(
                            npc))
                    {
                        return npc;
                    }

                    GameObject go =
                        value as GameObject;

                    if (go != null)
                    {
                        NPC candidate =
                            go.GetComponentInChildren<NPC>(
                                true);

                        if (IsExactSkeletonNpc(
                                candidate))
                        {
                            return candidate;
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }


        private static bool CacheNpcTemplate(
            NPC source,
            string origin)
        {
            if (!IsExactSkeletonNpc(
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
                    NpcTemplateObjectName;

                clone.transform.SetParent(
                    null,
                    true);

                clone.SetActive(
                    false);

                UnityEngine.Object.DontDestroyOnLoad(
                    clone);

                _skeletonNpcTemplate =
                    clone;

                DumpVisualFingerprint(
                    source,
                    origin);

                _loggedMissingTemplate =
                    false;

                Plugin.NativeLog.LogInfo(
                    "[Quest4Skeletons] resolved native Skeleton Soldier NPC prefab from [" +
                    origin +
                    "]. Encounter will use direct native NPC cloning.");

                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest4Skeletons] failed to cache NPC template from [" +
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


        // ============================================================
        // ASSET FINGERPRINT
        // ============================================================

        private static void DumpVisualFingerprint(
            NPC npc,
            string origin)
        {
            if (_fingerprintLogged ||
                npc == null)
            {
                return;
            }

            _fingerprintLogged =
                true;

            Plugin.NativeLog.LogInfo(
                "[Quest4SkeletonAssets] SOURCE origin=[" +
                origin +
                "] object=[" +
                npc.gameObject.name +
                "] npcName=[" +
                npc.NPCName +
                "] scene=[" +
                (npc.gameObject.scene.IsValid()
                    ? npc.gameObject.scene.name
                    : "<asset>") +
                "].");

            Animator[] animators =
                npc.GetComponentsInChildren<Animator>(
                    true);

            if (animators != null)
            {
                for (int i = 0;
                     i < animators.Length;
                     i++)
                {
                    Animator animator =
                        animators[i];

                    if (animator == null)
                    {
                        continue;
                    }

                    Plugin.NativeLog.LogInfo(
                        "[Quest4SkeletonAssets] Animator path=[" +
                        GetRelativePath(
                            npc.transform,
                            animator.transform) +
                        "] controller=[" +
                        (animator.runtimeAnimatorController != null
                            ? animator.runtimeAnimatorController.name
                            : "<none>") +
                        "] avatar=[" +
                        (animator.avatar != null
                            ? animator.avatar.name
                            : "<none>") +
                        "].");
                }
            }

            SkinnedMeshRenderer[] skinned =
                npc.GetComponentsInChildren<SkinnedMeshRenderer>(
                    true);

            if (skinned != null)
            {
                for (int i = 0;
                     i < skinned.Length;
                     i++)
                {
                    SkinnedMeshRenderer renderer =
                        skinned[i];

                    if (renderer == null)
                    {
                        continue;
                    }

                    Plugin.NativeLog.LogInfo(
                        "[Quest4SkeletonAssets] SkinnedMesh path=[" +
                        GetRelativePath(
                            npc.transform,
                            renderer.transform) +
                        "] mesh=[" +
                        (renderer.sharedMesh != null
                            ? renderer.sharedMesh.name
                            : "<none>") +
                        "] rootBone=[" +
                        (renderer.rootBone != null
                            ? renderer.rootBone.name
                            : "<none>") +
                        "] bones=[" +
                        (renderer.bones != null
                            ? renderer.bones.Length.ToString()
                            : "0") +
                        "] materials=[" +
                        FormatMaterials(
                            renderer.sharedMaterials) +
                        "].");
                }
            }

            MeshFilter[] meshFilters =
                npc.GetComponentsInChildren<MeshFilter>(
                    true);

            if (meshFilters != null)
            {
                for (int i = 0;
                     i < meshFilters.Length;
                     i++)
                {
                    MeshFilter filter =
                        meshFilters[i];

                    if (filter == null ||
                        filter.sharedMesh == null)
                    {
                        continue;
                    }

                    Renderer renderer =
                        filter.GetComponent<Renderer>();

                    Plugin.NativeLog.LogInfo(
                        "[Quest4SkeletonAssets] MeshFilter path=[" +
                        GetRelativePath(
                            npc.transform,
                            filter.transform) +
                        "] mesh=[" +
                        filter.sharedMesh.name +
                        "] materials=[" +
                        (renderer != null
                            ? FormatMaterials(
                                renderer.sharedMaterials)
                            : "<no renderer>") +
                        "].");
                }
            }
        }


        private static string FormatMaterials(
            Material[] materials)
        {
            if (materials == null ||
                materials.Length == 0)
            {
                return
                    "<none>";
            }

            string result =
                "";

            for (int i = 0;
                 i < materials.Length;
                 i++)
            {
                if (i > 0)
                {
                    result +=
                        " | ";
                }

                Material material =
                    materials[i];

                if (material == null)
                {
                    result +=
                        "<null>";

                    continue;
                }

                result +=
                    material.name +
                    "{" +
                    (material.shader != null
                        ? material.shader.name
                        : "<no shader>") +
                    "}";
            }

            return result;
        }


        private static string GetRelativePath(
            Transform root,
            Transform target)
        {
            if (target == null)
            {
                return
                    "<null>";
            }

            if (root == null ||
                target == root)
            {
                return
                    target.name;
            }

            string path =
                target.name;

            Transform current =
                target.parent;

            while (current != null &&
                   current != root)
            {
                path =
                    current.name +
                    "/" +
                    path;

                current =
                    current.parent;
            }

            if (current == root)
            {
                path =
                    root.name +
                    "/" +
                    path;
            }

            return path;
        }


        // ============================================================
        // BONE PITS FALLBACK CAPTURE
        // ============================================================

        private static void StartCaptureProbe(
            Scene scene)
        {
            if (_captureRoutine != null ||
                HasNativeTemplate)
            {
                return;
            }

            _captureRoutine =
                CoroutineHost.Start(
                    CaptureWhenAvailable(
                        scene));
        }


        private static void StopCaptureProbe()
        {
            if (_captureRoutine == null)
            {
                return;
            }

            CoroutineHost.Stop(
                _captureRoutine);

            _captureRoutine =
                null;
        }


        private static IEnumerator CaptureWhenAvailable(
            Scene scene)
        {
            for (int frame = 0;
                 frame < CaptureProbeFrames;
                 frame++)
            {
                if (!_initialized ||
                    HasNativeTemplate)
                {
                    _captureRoutine =
                        null;

                    yield break;
                }

                if (!scene.IsValid() ||
                    !scene.isLoaded ||
                    SceneManager.GetActiveScene() != scene)
                {
                    _captureRoutine =
                        null;

                    yield break;
                }

                if (frame %
                        CaptureProbeInterval ==
                    0 &&
                    TryResolveLoadedNativeSource(
                        false))
                {
                    _captureRoutine =
                        null;

                    yield break;
                }

                yield return null;
            }

            _captureRoutine =
                null;
        }


        // ============================================================
        // QUEST ENCOUNTER
        // ============================================================

        private static bool IsQuestEncounterScene(
            Scene scene)
        {
            return
                scene.IsValid() &&
                scene.isLoaded &&
                string.Equals(
                    scene.name,
                    DisturbedGraveRuntime.SceneName,
                    StringComparison.OrdinalIgnoreCase);
        }


        private static void StartEncounterMonitor(
            Scene scene)
        {
            if (_encounterRoutine != null)
            {
                return;
            }

            _encounterRoutine =
                CoroutineHost.Start(
                    EncounterMonitor(
                        scene));
        }


        private static void StopEncounterMonitor()
        {
            if (_encounterRoutine == null)
            {
                return;
            }

            CoroutineHost.Stop(
                _encounterRoutine);

            _encounterRoutine =
                null;
        }


        private static IEnumerator EncounterMonitor(
            Scene scene)
        {
            _encounterFinished =
                false;

            while (_initialized &&
                   scene.IsValid() &&
                   scene.isLoaded &&
                   IsQuestEncounterScene(
                       SceneManager.GetActiveScene()))
            {
                if (!FOTFExpansionGate.IsUnlocked)
                {
                    DestroyEncounter();

                    Plugin.NativeLog.LogInfo(
                        "[Quest4Skeletons] expansion became locked during encounter monitoring; encounter removed.");

                    _encounterRoutine =
                        null;

                    yield break;
                }

                bool questActive =
                    FateOfTheFallenQuests
                        .IsRestlessDeadActive();

                bool questComplete =
                    FateOfTheFallenQuests
                        .IsRestlessDeadComplete();

                // The encounter is gated by Quest 4 itself, not by whether
                // the player already happens to carry Lingering Soul-Essence.
                //
                // This matters now that the essence is a normal 20% corpse
                // drop: a player may obtain it while the quest remains active,
                // and the grave encounter should continue to exist until the
                // quest is actually completed/turned in.
                if (questComplete)
                {
                    DestroyEncounter();

                    _encounterFinished =
                        true;

                    _encounterRoutine =
                        null;

                    yield break;
                }

                if (!questActive ||
                    DisturbedGraveRuntime.CurrentGrave == null)
                {
                    yield return WaitPollFrames();
                    continue;
                }

                if (!HasNativeTemplate)
                {
                    // Cheap global scan first.
                    TryResolveLoadedNativeSource(
                        false);

                    // One heavy native Resources probe per session.
                    if (!HasNativeTemplate)
                    {
                        TryResolveFromResources();
                    }

                    if (!HasNativeTemplate)
                    {
                        if (!_loggedMissingTemplate)
                        {
                            _loggedMissingTemplate =
                                true;

                            Plugin.NativeLog.LogWarning(
                                "[Quest4Skeletons] cold native resolution did not find [" +
                                NativeTemplateNpcName +
                                "]. Bone Pits live capture remains the fallback. " +
                                "The asset fingerprint logger will identify the exact native visual assets once captured.");
                        }

                        yield return WaitPollFrames();
                        continue;
                    }
                }

                if (!_encounterSpawned)
                {
                    Plugin.NativeLog.LogInfo(
                        "[Quest4Skeletons] Quest 4 active/incomplete; populating grave encounter. PlayerHasEssence=[" +
                        RestlessDeadContent.PlayerHasEssence() +
                        "].");

                    yield return SpawnEncounter(
                        scene);
                }

                if (_encounterSpawned &&
                    !_encounterFinished)
                {
                    CheckEncounterDeaths(
                        scene);
                }

                yield return WaitPollFrames();
            }

            _encounterRoutine =
                null;
        }


        private static IEnumerator WaitPollFrames()
        {
            for (int i = 0;
                 i < EncounterPollFrames;
                 i++)
            {
                yield return null;
            }
        }


        private static IEnumerator SpawnEncounter(
            Scene scene)
        {
            if (!FOTFExpansionGate.IsUnlocked ||
                _encounterSpawned ||
                !HasNativeTemplate ||
                DisturbedGraveRuntime.CurrentGrave == null)
            {
                yield break;
            }

            DestroyEncounter();

            Vector3 graveCenter =
                DisturbedGraveRuntime
                    .CurrentGrave
                    .transform
                    .position;

            int spawnedCount =
                0;

            for (int i = 0;
                 i < EncounterCount;
                 i++)
            {
                if (!FOTFExpansionGate.IsUnlocked)
                {
                    DestroyEncounter();

                    Plugin.NativeLog.LogInfo(
                        "[Quest4Skeletons] expansion lock engaged during spawn; partial encounter removed.");

                    yield break;
                }

                Vector3 requested =
                    graveCenter +
                    EncounterOffsets[i];

                Vector3 position =
                    ResolveSafeNavMeshPosition(
                        requested);

                Vector3 lookDirection =
                    graveCenter -
                    position;

                lookDirection.y =
                    0f;

                Quaternion rotation =
                    lookDirection.sqrMagnitude >
                    0.001f
                        ? Quaternion.LookRotation(
                            lookDirection.normalized,
                            Vector3.up)
                        : Quaternion.identity;

                EncounterSpawn instance =
                    null;

                if (_skeletonSpawnPointTemplate != null)
                {
                    yield return SpawnOneFromSpawnPoint(
                        scene,
                        i + 1,
                        position,
                        rotation,
                        spawned =>
                        {
                            instance =
                                spawned;
                        });
                }
                else
                {
                    yield return SpawnOneFromNpcTemplate(
                        scene,
                        i + 1,
                        position,
                        rotation,
                        spawned =>
                        {
                            instance =
                                spawned;
                        });
                }

                if (instance != null)
                {
                    _encounterSpawns.Add(
                        instance);

                    spawnedCount++;
                }
            }

            _encounterSpawned =
                spawnedCount ==
                EncounterCount;

            Plugin.NativeLog.LogInfo(
                "[Quest4Skeletons] grave encounter population complete. Spawned=[" +
                spawnedCount +
                "/" +
                EncounterCount +
                "] sourceMode=[" +
                (_skeletonSpawnPointTemplate != null
                    ? "SpawnPoint"
                    : "DirectNPC") +
                "].");
        }


        private static IEnumerator SpawnOneFromSpawnPoint(
            Scene scene,
            int index,
            Vector3 position,
            Quaternion rotation,
            Action<EncounterSpawn> onSpawned)
        {
            GameObject runtimeObject =
                null;

            SpawnPoint runtimeSpawnPoint =
                null;

            try
            {
                runtimeObject =
                    UnityEngine.Object.Instantiate(
                        _skeletonSpawnPointTemplate);

                if (runtimeObject == null)
                {
                    yield break;
                }

                runtimeObject.name =
                    RuntimeSpawnPointPrefix +
                    index;

                runtimeObject.transform
                    .SetPositionAndRotation(
                        position,
                        rotation);

                SceneManager.MoveGameObjectToScene(
                    runtimeObject,
                    scene);

                runtimeSpawnPoint =
                    runtimeObject
                        .GetComponent<SpawnPoint>();

                if (runtimeSpawnPoint == null)
                {
                    UnityEngine.Object.Destroy(
                        runtimeObject);

                    yield break;
                }

                runtimeObject.SetActive(
                    true);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Quest4Skeletons] failed creating SpawnPoint skeleton [" +
                    index +
                    "]: " +
                    exception);

                if (runtimeObject != null)
                {
                    UnityEngine.Object.Destroy(
                        runtimeObject);
                }

                yield break;
            }

            NPC npc =
                null;

            for (int frame = 0;
                 frame < SpawnWaitFrames;
                 frame++)
            {
                if (!_initialized ||
                    !scene.IsValid() ||
                    !scene.isLoaded)
                {
                    yield break;
                }

                npc =
                    runtimeSpawnPoint != null
                        ? runtimeSpawnPoint.SpawnedNPC
                        : null;

                if (npc != null)
                {
                    break;
                }

                yield return null;
            }

            if (npc == null)
            {
                Plugin.NativeLog.LogError(
                    "[Quest4Skeletons] timed out waiting for native Skeleton Soldier SpawnPoint [" +
                    index +
                    "].");

                if (runtimeObject != null)
                {
                    UnityEngine.Object.Destroy(
                        runtimeObject);
                }

                yield break;
            }

            yield return null;
            yield return null;

            ConfigureQuestSkeleton(
                npc,
                index,
                position);

            runtimeSpawnPoint.enabled =
                false;

            onSpawned?.Invoke(
                new EncounterSpawn
                {
                    Index =
                        index,

                    SpawnPointObject =
                        runtimeObject,

                    SpawnPoint =
                        runtimeSpawnPoint,

                    Npc =
                        npc,

                    SpawnPosition =
                        position,

                    SpawnRotation =
                        rotation
                });

            Plugin.NativeLog.LogInfo(
                "[Quest4Skeletons] spawned [" +
                QuestMobName +
                " " +
                index +
                "] via native SpawnPoint at [" +
                npc.transform.position +
                "].");
        }


        private static IEnumerator SpawnOneFromNpcTemplate(
            Scene scene,
            int index,
            Vector3 position,
            Quaternion rotation,
            Action<EncounterSpawn> onSpawned)
        {
            if (_skeletonNpcTemplate == null)
            {
                yield break;
            }

            GameObject npcObject =
                null;

            try
            {
                npcObject =
                    UnityEngine.Object.Instantiate(
                        _skeletonNpcTemplate);

                if (npcObject == null)
                {
                    yield break;
                }

                npcObject.name =
                    RuntimeNpcPrefix +
                    index;

                npcObject.transform
                    .SetPositionAndRotation(
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
                    "[Quest4Skeletons] direct NPC clone failed [" +
                    index +
                    "]: " +
                    exception);

                if (npcObject != null)
                {
                    UnityEngine.Object.Destroy(
                        npcObject);
                }

                yield break;
            }

            // Let Awake/OnEnable/Start settle.
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
                    "[Quest4Skeletons] direct native NPC template did not produce an NPC component.");

                UnityEngine.Object.Destroy(
                    npcObject);

                yield break;
            }

            ConfigureQuestSkeleton(
                npc,
                index,
                position);

            onSpawned?.Invoke(
                new EncounterSpawn
                {
                    Index =
                        index,

                    DirectNpcObject =
                        npcObject,

                    Npc =
                        npc,

                    SpawnPosition =
                        position,

                    SpawnRotation =
                        rotation
                });

            Plugin.NativeLog.LogInfo(
                "[Quest4Skeletons] spawned [" +
                QuestMobName +
                " " +
                index +
                "] by direct native NPC clone at [" +
                npc.transform.position +
                "].");
        }


        private static void ConfigureQuestSkeleton(
            NPC npc,
            int index,
            Vector3 requestedPosition)
        {
            if (npc == null)
            {
                return;
            }

            try
            {
                string originalNpcName =
                    npc.NPCName;

                GameObject monster =
                    npc.gameObject;

                // Some native UI paths fall back to GameObject.name, so do
                // not leave FOTF_RestlessDead_1 or A Skeleton Soldier here.
                monster.name =
                    QuestMobName;

                npc.NPCName =
                    QuestMobName;

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

                // Use an explicit native damage range. If DamageRange remains
                // at the Skeleton Soldier sentinel values, Erenshor replaces it
                // with level-derived damage during PerformMeleeHit().
                npc.BaseAtkDmg =
                    EncounterMeleeDamage;

                npc.DamageRange =
                    new Vector2(
                        EncounterMeleeDamage,
                        EncounterMeleeDamage);

                npc.DamageMult =
                    1f;

                npc.OHAtkDmg =
                    0;

                // Level 38's normal NPC damage cap would reduce the explicit
                // 1500-point group-content baseline. FOTF owns this encounter
                // tuning, so bypass that generic NPC cap.
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

                if (stats != null)
                {
                    stats.MyName =
                        QuestMobName;

                    stats.Level =
                        EncounterLevel;

                    stats.BaseHP =
                        EncounterHP;

                    stats.BaseAC =
                        EncounterAC;

                    stats.BasePR =
                        EncounterResistance;

                    stats.BaseMR =
                        EncounterResistance;

                    stats.BaseER =
                        EncounterResistance;

                    stats.BaseVR =
                        EncounterResistance;

                    stats.RunSpeed =
                        EncounterRunSpeed;

                    stats.BaseMHAtkDelay =
                        EncounterMainHandDelay;

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
                        EncounterAggroRange;
                }

                NavMeshAgent agent =
                    monster.GetComponent<NavMeshAgent>();

                if (agent != null)
                {
                    agent.speed =
                        EncounterRunSpeed;

                    if (!agent.isOnNavMesh)
                    {
                        agent.Warp(
                            ResolveSafeNavMeshPosition(
                                requestedPosition));
                    }
                }

                ConfigureRestlessDeadLoot(
                    npc,
                    index);

                RefreshIdentity(
                    npc,
                    originalNpcName);

                RestlessDeadIdentityKeeper keeper =
                    monster.GetComponent<RestlessDeadIdentityKeeper>();

                if (keeper == null)
                {
                    keeper =
                        monster.AddComponent<RestlessDeadIdentityKeeper>();
                }

                keeper.Initialize(
                    npc);

                Plugin.NativeLog.LogInfo(
                    "[Quest4Skeletons] configured [" +
                    QuestMobName +
                    " " +
                    index +
                    "] [Level=" +
                    (stats != null
                        ? stats.Level.ToString()
                        : "?") +
                    ", HP=" +
                    (stats != null
                        ? stats.CurrentMaxHP.ToString()
                        : "?") +
                    ", AC=" +
                    (stats != null
                        ? stats.BaseAC.ToString()
                        : "?") +
                    ", Melee=" +
                    EncounterMeleeDamage +
                    ", Aggro=" +
                    EncounterAggroRange.ToString("0.0") +
                    "m].");
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Quest4Skeletons] failed configuring 5-man Restless Dead [" +
                    index +
                    "]: " +
                    exception);
            }
        }


        private static void RefreshIdentity(
            NPC npc,
            string previousName = null)
        {
            if (npc == null)
            {
                return;
            }

            npc.NPCName =
                QuestMobName;

            Stats stats =
                npc.GetComponent<Stats>();

            if (stats != null)
            {
                stats.MyName =
                    QuestMobName;
            }

            GameObject monster =
                npc.gameObject;

            if (monster != null)
            {
                monster.name =
                    QuestMobName;
            }

            // NamePlate itself may contain either a TextMeshPro component or
            // several child TMP_Text components (front text, outline/shadow,
            // etc.). Update every text element that actually represents the
            // native Skeleton Soldier name while leaving level/HP text alone.
            if (npc.NamePlate != null)
            {
                TMP_Text[] texts =
                    npc.NamePlate.GetComponentsInChildren<TMP_Text>(
                        true);

                if (texts != null)
                {
                    for (int i = 0;
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

                        string objectName =
                            text.gameObject.name ??
                            "";

                        bool looksLikeNameField =
                            objectName.IndexOf(
                                "name",
                                StringComparison.OrdinalIgnoreCase) >=
                            0;

                        bool containsNativeName =
                            current.IndexOf(
                                NativeTemplateNpcName,
                                StringComparison.OrdinalIgnoreCase) >=
                            0 ||
                            current.IndexOf(
                                "Skeleton Soldier",
                                StringComparison.OrdinalIgnoreCase) >=
                            0;

                        bool containsPreviousName =
                            !string.IsNullOrEmpty(
                                previousName) &&
                            current.IndexOf(
                                previousName,
                                StringComparison.OrdinalIgnoreCase) >=
                            0;

                        if (containsNativeName)
                        {
                            current =
                                ReplaceIgnoreCase(
                                    current,
                                    NativeTemplateNpcName,
                                    QuestMobName);

                            current =
                                ReplaceIgnoreCase(
                                    current,
                                    "Skeleton Soldier",
                                    QuestMobName);

                            text.text =
                                current;

                            continue;
                        }

                        if (containsPreviousName)
                        {
                            text.text =
                                ReplaceIgnoreCase(
                                    current,
                                    previousName,
                                    QuestMobName);

                            continue;
                        }

                        if (looksLikeNameField &&
                            (string.IsNullOrWhiteSpace(
                                 current) ||
                             current.IndexOf(
                                 "Level",
                                 StringComparison.OrdinalIgnoreCase) < 0))
                        {
                            text.text =
                                QuestMobName;
                        }
                    }
                }

                TextMeshPro directText =
                    npc.NamePlate.GetComponent<TextMeshPro>();

                if (directText != null)
                {
                    string current =
                        directText.text ??
                        "";

                    if (string.IsNullOrWhiteSpace(
                            current) ||
                        current.IndexOf(
                            NativeTemplateNpcName,
                            StringComparison.OrdinalIgnoreCase) >=
                        0 ||
                        current.IndexOf(
                            "Skeleton Soldier",
                            StringComparison.OrdinalIgnoreCase) >=
                        0)
                    {
                        directText.text =
                            QuestMobName;
                    }
                }
            }
        }


        private static string ReplaceIgnoreCase(
            string source,
            string oldValue,
            string newValue)
        {
            if (string.IsNullOrEmpty(source) ||
                string.IsNullOrEmpty(oldValue))
            {
                return source;
            }

            int index =
                source.IndexOf(
                    oldValue,
                    StringComparison.OrdinalIgnoreCase);

            while (index >= 0)
            {
                source =
                    source.Substring(
                        0,
                        index) +
                    newValue +
                    source.Substring(
                        index +
                        oldValue.Length);

                index =
                    source.IndexOf(
                        oldValue,
                        index +
                        newValue.Length,
                        StringComparison.OrdinalIgnoreCase);
            }

            return source;
        }


        private sealed class RestlessDeadIdentityKeeper :
            MonoBehaviour
        {
            private NPC _npc;
            private float _nextRefreshAt;


            internal void Initialize(
                NPC npc)
            {
                _npc =
                    npc;

                _nextRefreshAt =
                    0f;

                RefreshIdentity(
                    _npc,
                    NativeTemplateNpcName);
            }


            private void Update()
            {
                if (_npc == null)
                {
                    return;
                }

                if (Time.time <
                    _nextRefreshAt)
                {
                    return;
                }

                _nextRefreshAt =
                    Time.time +
                    0.5f;

                RefreshIdentity(
                    _npc,
                    NativeTemplateNpcName);
            }
        }


        private static Vector3 ResolveSafeNavMeshPosition(
            Vector3 requested)
        {
            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                    requested,
                    out hit,
                    6f,
                    NavMesh.AllAreas))
            {
                return hit.position;
            }

            return requested;
        }


        // ============================================================
        // CUSTOM LOOT
        // ============================================================

        private static void ConfigureRestlessDeadLoot(
            NPC npc,
            int index)
        {
            if (npc == null)
            {
                return;
            }

            LootTable lootTable =
                npc.GetComponent<LootTable>();

            if (lootTable == null)
            {
                lootTable =
                    npc.GetComponentInChildren<LootTable>(
                        true);
            }

            if (lootTable == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest4Skeletons] Restless Dead [" +
                    index +
                    "] has no LootTable component; custom corpse loot could not be installed.");

                return;
            }

            // Own the table completely. Do not retain anything inherited from
            // A Skeleton Soldier.
            ClearLootList(
                lootTable.GuaranteeOneDrop);

            ClearLootList(
                lootTable.CommonDrop);

            ClearLootList(
                lootTable.UncommonDrop);

            ClearLootList(
                lootTable.RareDrop);

            ClearLootList(
                lootTable.LegendaryDrop);

            ClearLootList(
                lootTable.UltraRareDrop);

            if (lootTable.ActualDrops == null)
            {
                lootTable.ActualDrops =
                    new List<Item>();
            }
            else
            {
                lootTable.ActualDrops.Clear();
            }

            if (lootTable.ActualDropsQual == null)
            {
                lootTable.ActualDropsQual =
                    new List<int>();
            }
            else
            {
                lootTable.ActualDropsQual.Clear();
            }

            Character host =
                npc.GetChar();

            if (host == null)
            {
                host =
                    npc.GetComponent<Character>();
            }

            lootTable.hostChar =
                host;

            // Gold is always present on the corpse.
            lootTable.MinGold =
                MinimumGold;

            lootTable.MaxGold =
                MaximumGold;

            lootTable.MyGold =
                UnityEngine.Random.Range(
                    MinimumGold,
                    MaximumGold + 1);

            Item bones =
                ResolveNativeBonesItem();

            int boneCount =
                UnityEngine.Random.Range(
                    MinimumBones,
                    MaximumBones + 1);

            int installedBones =
                0;

            if (bones != null)
            {
                for (int i = 0;
                     i < boneCount;
                     i++)
                {
                    AddActualDrop(
                        lootTable,
                        bones);

                    installedBones++;
                }
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest4Skeletons] native Bones item could not be resolved; gold and quest-item loot remain active.");
            }

            bool questItemRolled =
                false;

            if (FateOfTheFallenQuests.IsRestlessDeadActive() &&
                !FateOfTheFallenQuests.IsRestlessDeadComplete() &&
                !RestlessDeadContent.PlayerHasEssence() &&
                UnityEngine.Random.value <
                    QuestItemDropChance)
            {
                Item essence =
                    RestlessDeadContent.GetItem();

                if (essence != null)
                {
                    AddActualDrop(
                        lootTable,
                        essence);

                    questItemRolled =
                        true;
                }
            }

            lootTable.NumberOfGuaranteedDrops =
                installedBones;

            lootTable.MaxNumberDrops =
                Mathf.Max(
                    1,
                    lootTable.ActualDrops.Count);

            lootTable.MaxNonCommonDrops =
                questItemRolled
                    ? 1
                    : 0;

            Plugin.NativeLog.LogInfo(
                "[Quest4Skeletons] custom loot armed for Restless Dead [" +
                index +
                "]: Gold=[" +
                lootTable.MyGold +
                "] Bones=[" +
                installedBones +
                "] LingeringSoulEssence20PercentRoll=[" +
                questItemRolled +
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


        private static void AddActualDrop(
            LootTable lootTable,
            Item item)
        {
            if (lootTable == null ||
                item == null)
            {
                return;
            }

            lootTable.ActualDrops.Add(
                item);

            lootTable.ActualDropsQual.Add(
                1);
        }


        private static Item ResolveNativeBonesItem()
        {
            ItemDatabase database =
                GameData.ItemDB;

            if (database == null)
            {
                return null;
            }

            string[] exactNames =
            {
                "Bones",
                "Bone",
                "Pile of Bones"
            };

            for (int nameIndex = 0;
                 nameIndex < exactNames.Length;
                 nameIndex++)
            {
                Item found =
                    FindNativeItemByExactName(
                        database,
                        exactNames[nameIndex]);

                if (found != null)
                {
                    return found;
                }
            }

            // Last-resort fallback: a General-slot native item whose display
            // name contains "bone". This avoids weapon/armor entries where
            // possible while still surviving minor native naming changes.
            Item fallback =
                FindNativeGeneralItemContaining(
                    database,
                    "bone");

            if (fallback != null)
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest4Skeletons] native bone loot resolved by fallback as [" +
                    fallback.ItemName +
                    "] Id=[" +
                    fallback.Id +
                    "].");
            }

            return fallback;
        }


        private static Item FindNativeItemByExactName(
            ItemDatabase database,
            string itemName)
        {
            if (database == null ||
                string.IsNullOrEmpty(
                    itemName))
            {
                return null;
            }

            if (database.ItemDBList != null)
            {
                for (int i = 0;
                     i < database.ItemDBList.Count;
                     i++)
                {
                    Item candidate =
                        database.ItemDBList[i];

                    if (candidate != null &&
                        string.Equals(
                            candidate.ItemName,
                            itemName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            if (database.ItemDB != null)
            {
                for (int i = 0;
                     i < database.ItemDB.Length;
                     i++)
                {
                    Item candidate =
                        database.ItemDB[i];

                    if (candidate != null &&
                        string.Equals(
                            candidate.ItemName,
                            itemName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }


        private static Item FindNativeGeneralItemContaining(
            ItemDatabase database,
            string fragment)
        {
            if (database == null ||
                string.IsNullOrEmpty(
                    fragment))
            {
                return null;
            }

            if (database.ItemDBList != null)
            {
                for (int i = 0;
                     i < database.ItemDBList.Count;
                     i++)
                {
                    Item candidate =
                        database.ItemDBList[i];

                    if (IsGeneralItemContaining(
                            candidate,
                            fragment))
                    {
                        return candidate;
                    }
                }
            }

            if (database.ItemDB != null)
            {
                for (int i = 0;
                     i < database.ItemDB.Length;
                     i++)
                {
                    Item candidate =
                        database.ItemDB[i];

                    if (IsGeneralItemContaining(
                            candidate,
                            fragment))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }


        private static bool IsGeneralItemContaining(
            Item candidate,
            string fragment)
        {
            return
                candidate != null &&
                candidate.RequiredSlot ==
                    Item.SlotType.General &&
                !string.IsNullOrEmpty(
                    candidate.ItemName) &&
                candidate.ItemName.IndexOf(
                    fragment,
                    StringComparison.OrdinalIgnoreCase) >=
                0;
        }


        // ============================================================
        // DEATH / RESPAWN
        // ============================================================

        private static void CheckEncounterDeaths(
            Scene scene)
        {
            if (!FOTFExpansionGate.IsUnlocked)
            {
                DestroyEncounter();
                return;
            }

            if (_encounterSpawns.Count <
                EncounterCount)
            {
                return;
            }

            for (int i = 0;
                 i < _encounterSpawns.Count;
                 i++)
            {
                EncounterSpawn spawn =
                    _encounterSpawns[i];

                if (spawn == null)
                {
                    continue;
                }

                bool dead =
                    IsNpcDead(
                        spawn.Npc);

                if (!dead)
                {
                    continue;
                }

                if (!spawn.DeathLogged)
                {
                    spawn.DeathLogged =
                        true;

                    spawn.RespawnPending =
                        true;

                    spawn.RespawnAt =
                        Time.time +
                        RespawnDelaySeconds;

                    Plugin.NativeLog.LogInfo(
                        "[Quest4Skeletons] Restless Dead [" +
                        spawn.Index +
                        "] defeated. Respawn scheduled in [" +
                        RespawnDelaySeconds.ToString("0") +
                        "] seconds.");
                }

                if (spawn.RespawnPending &&
                    !spawn.RespawnInProgress &&
                    Time.time >=
                        spawn.RespawnAt)
                {
                    spawn.RespawnInProgress =
                        true;

                    CoroutineHost.Start(
                        RespawnEncounterSlot(
                            scene,
                            spawn));
                }
            }
        }


        private static IEnumerator RespawnEncounterSlot(
            Scene scene,
            EncounterSpawn state)
        {
            if (state == null)
            {
                yield break;
            }

            if (!_initialized ||
                !FOTFExpansionGate.IsUnlocked ||
                !scene.IsValid() ||
                !scene.isLoaded ||
                !IsQuestEncounterScene(
                    SceneManager.GetActiveScene()) ||
                !FateOfTheFallenQuests.IsRestlessDeadActive() ||
                FateOfTheFallenQuests.IsRestlessDeadComplete())
            {
                state.RespawnInProgress =
                    false;

                yield break;
            }

            DestroySpawnObjects(
                state);

            EncounterSpawn replacement =
                null;

            if (_skeletonSpawnPointTemplate != null)
            {
                yield return SpawnOneFromSpawnPoint(
                    scene,
                    state.Index,
                    state.SpawnPosition,
                    state.SpawnRotation,
                    spawned =>
                    {
                        replacement =
                            spawned;
                    });
            }
            else
            {
                yield return SpawnOneFromNpcTemplate(
                    scene,
                    state.Index,
                    state.SpawnPosition,
                    state.SpawnRotation,
                    spawned =>
                    {
                        replacement =
                            spawned;
                    });
            }

            if (!_encounterSpawns.Contains(
                    state))
            {
                if (replacement != null)
                {
                    DestroySpawnObjects(
                        replacement);
                }

                yield break;
            }

            if (replacement == null)
            {
                state.RespawnInProgress =
                    false;

                state.RespawnPending =
                    true;

                // Short retry if native instantiation happened to fail.
                state.RespawnAt =
                    Time.time +
                    5f;

                Plugin.NativeLog.LogWarning(
                    "[Quest4Skeletons] Restless Dead [" +
                    state.Index +
                    "] respawn failed; retrying in 5 seconds.");

                yield break;
            }

            state.SpawnPointObject =
                replacement.SpawnPointObject;

            state.SpawnPoint =
                replacement.SpawnPoint;

            state.DirectNpcObject =
                replacement.DirectNpcObject;

            state.Npc =
                replacement.Npc;

            state.DeathLogged =
                false;

            state.RespawnPending =
                false;

            state.RespawnInProgress =
                false;

            Plugin.NativeLog.LogInfo(
                "[Quest4Skeletons] Restless Dead [" +
                state.Index +
                "] respawned after 3 minutes.");
        }


        private static void DestroySpawnObjects(
            EncounterSpawn spawn)
        {
            if (spawn == null)
            {
                return;
            }

            if (spawn.SpawnPointObject != null)
            {
                UnityEngine.Object.Destroy(
                    spawn.SpawnPointObject);
            }

            if (spawn.DirectNpcObject != null)
            {
                UnityEngine.Object.Destroy(
                    spawn.DirectNpcObject);
            }

            spawn.SpawnPointObject =
                null;

            spawn.SpawnPoint =
                null;

            spawn.DirectNpcObject =
                null;

            spawn.Npc =
                null;
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

            return
                character == null ||
                stats == null ||
                !character.Alive ||
                stats.CurrentHP <= 0;
        }


        // ============================================================
        // STATUS / CLEANUP
        // ============================================================

        internal static void WriteStatusToLog()
        {
            Plugin.NativeLog.LogInfo(
                "[Quest4Skeletons] STATUS SpawnPointTemplate=[" +
                (_skeletonSpawnPointTemplate != null) +
                "] NpcTemplate=[" +
                (_skeletonNpcTemplate != null) +
                "] ResourcesProbe=[" +
                _resourcesProbeAttempted +
                "] expansionUnlocked=[" +
                FOTFExpansionGate.IsUnlocked +
                "] playerHasEssence=[" +
                RestlessDeadContent.PlayerHasEssence() +
                "] encounterSpawned=[" +
                _encounterSpawned +
                "] instances=[" +
                _encounterSpawns.Count +
                "].");
        }


        private static void DestroyEncounter()
        {
            for (int i = 0;
                 i < _encounterSpawns.Count;
                 i++)
            {
                EncounterSpawn spawn =
                    _encounterSpawns[i];

                if (spawn == null)
                {
                    continue;
                }

                DestroySpawnObjects(
                    spawn);
            }

            _encounterSpawns.Clear();

            _encounterSpawned =
                false;
        }


        private static void DestroyTemplates()
        {
            if (_skeletonSpawnPointTemplate != null)
            {
                UnityEngine.Object.Destroy(
                    _skeletonSpawnPointTemplate);

                _skeletonSpawnPointTemplate =
                    null;
            }

            if (_skeletonNpcTemplate != null)
            {
                UnityEngine.Object.Destroy(
                    _skeletonNpcTemplate);

                _skeletonNpcTemplate =
                    null;
            }
        }


        private sealed class EncounterSpawn
        {
            internal int Index;
            internal GameObject SpawnPointObject;
            internal SpawnPoint SpawnPoint;
            internal GameObject DirectNpcObject;
            internal NPC Npc;
            internal Vector3 SpawnPosition;
            internal Quaternion SpawnRotation;
            internal bool DeathLogged;
            internal bool RespawnPending;
            internal bool RespawnInProgress;
            internal float RespawnAt;
        }
    }
}
