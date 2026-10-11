using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 5 Flame Rift world prop.
    ///
    /// This deliberately mirrors the proven Quest 4 DisturbedGraveRuntime:
    /// sceneLoaded -> monitor -> AssetBundleRegistry.GetPrefab -> Instantiate ->
    /// MoveGameObjectToScene -> SetActive -> renderer bounds -> normalize scale ->
    /// force exact world position -> replace imported materials -> clear terrain.
    ///
    /// Blacksalt Strand
    /// Native scene: SaltedStrand
    /// </summary>
    internal static class FlameRiftRuntime
    {
        internal const string SceneName =
            "SaltedStrand";

        internal const string BundleName =
            "fotf_questprops";

        internal const string PrefabName =
            "FlameRift";

        private const string RuntimeObjectName =
            "FOTF_Quest05_FlameRift";

        private static readonly Vector3 RequestedPosition =
            new Vector3(
                425.81f,
                15.03f,
                94.08f);

        // V2 chasm authored footprint is roughly 6.7m x 10.3m.
        private const float ExpectedHorizontalSize =
            10.3f;

        private const float MinimumAcceptableHorizontalSize =
            4.0f;

        private const float MaximumAutomaticScaleMultiplier =
            100.0f;

        private const float TerrainClearRadius =
            6.25f;

        private const int PollFrames =
            30;

        private static bool _initialized;
        private static bool _loggedWaitingForQuest;
        private static GameObject _rift;
        private static Coroutine _monitorRoutine;

        private static readonly List<TerrainDetailPatch>
            _terrainDetailPatches =
                new List<TerrainDetailPatch>();


        internal static GameObject CurrentRift =>
            _rift;


        // ============================================================
        // LIFECYCLE - SAME PATTERN AS QUEST 4 GRAVE
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
                "[Quest5Rift] initialized. ActiveScene=[" +
                (activeScene.IsValid()
                    ? activeScene.name
                    : "<invalid>") +
                "] expansionUnlocked=[" +
                FOTFExpansionGate.IsUnlocked +
                "].");

            if (FOTFExpansionGate.IsUnlocked &&
                IsTargetScene(
                    activeScene))
            {
                StartMonitor(
                    activeScene);
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

            FOTFExpansionGate.EffectiveUnlockChanged -=
                OnExpansionGateChanged;

            StopMonitor();
            DestroyRift();
        }


        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            // Quest 5 may temporarily preload PlaneOfBrax additively to
            // acquire the native Lava Fiend template. That auxiliary load
            // must not tear down the already-spawned Flame Rift.
            if (mode == LoadSceneMode.Additive &&
                !IsTargetScene(
                    scene))
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest5Rift] ignoring additive auxiliary sceneLoaded=[" +
                    (scene.IsValid()
                        ? scene.name
                        : "<invalid>") +
                    "] while activeScene=[" +
                    SceneManager.GetActiveScene().name +
                    "].");

                return;
            }

            StopMonitor();
            DestroyRift();

            Plugin.NativeLog.LogInfo(
                "[Quest5Rift] sceneLoaded=[" +
                (scene.IsValid()
                    ? scene.name
                    : "<invalid>") +
                "] expansionUnlocked=[" +
                FOTFExpansionGate.IsUnlocked +
                "].");

            if (!FOTFExpansionGate.IsUnlocked)
            {
                return;
            }

            if (IsTargetScene(
                    scene))
            {
                StartMonitor(
                    scene);
            }
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
                StopMonitor();
                DestroyRift();

                Plugin.NativeLog.LogInfo(
                    "[Quest5Rift] expansion lock engaged; removed Flame Rift.");

                return;
            }

            Scene scene =
                SceneManager.GetActiveScene();

            if (IsTargetScene(
                    scene))
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest5Rift] expansion unlocked in SaltedStrand; restarting Quest 5 rift monitor.");

                StartMonitor(
                    scene);
            }
        }


        private static bool IsTargetScene(
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


        private static void StartMonitor(
            Scene scene)
        {
            if (_monitorRoutine != null)
            {
                return;
            }

            _loggedWaitingForQuest =
                false;

            _monitorRoutine =
                CoroutineHost.Start(
                    MonitorScene(
                        scene));
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


        private static IEnumerator MonitorScene(
            Scene scene)
        {
            for (int frame = 0;
                 frame < 120;
                 frame++)
            {
                if (!_initialized ||
                    !scene.IsValid() ||
                    !scene.isLoaded)
                {
                    _monitorRoutine =
                        null;

                    yield break;
                }

                if (GameData.PlayerControl != null)
                {
                    break;
                }

                yield return null;
            }

            // Repair saves where Q4 was completed before Q5 existed.
            AshBeneathEarthQuest.Register();
            AshBeneathEarthQuest.EnsureChainState();

            while (_initialized &&
                   scene.IsValid() &&
                   scene.isLoaded)
            {
                if (!FOTFExpansionGate.IsUnlocked)
                {
                    DestroyRift();

                    _monitorRoutine =
                        null;

                    yield break;
                }

                if (!IsTargetScene(
                        SceneManager.GetActiveScene()))
                {
                    break;
                }

                AshBeneathEarthQuest.EnsureChainState();

                bool active =
                    AshBeneathEarthQuest.IsActive();

                bool complete =
                    AshBeneathEarthQuest.IsComplete();

                if (complete)
                {
                    DestroyRift();
                    break;
                }

                if (active)
                {
                    _loggedWaitingForQuest =
                        false;

                    if (_rift == null)
                    {
                        if (!TrySpawnRift(
                                scene))
                        {
                            for (int delay = 0;
                                 delay < 180;
                                 delay++)
                            {
                                if (!_initialized ||
                                    !scene.IsValid() ||
                                    !scene.isLoaded)
                                {
                                    _monitorRoutine =
                                        null;

                                    yield break;
                                }

                                yield return null;
                            }
                        }
                    }
                    else
                    {
                        RefreshShardState();
                    }
                }
                else if (!_loggedWaitingForQuest)
                {
                    _loggedWaitingForQuest =
                        true;

                    Plugin.NativeLog.LogInfo(
                        "[Quest5Rift] target scene correct; waiting for Quest 5. " +
                        "Active=[" +
                        active +
                        "] Complete=[" +
                        complete +
                        "] Quest4Complete=[" +
                        FateOfTheFallenQuests.IsRestlessDeadComplete() +
                        "].");
                }

                for (int frame = 0;
                     frame < PollFrames;
                     frame++)
                {
                    yield return null;
                }
            }

            _monitorRoutine =
                null;
        }


        // ============================================================
        // SPAWN - SAME METHOD AS QUEST 4 GRAVE
        // ============================================================

        private static bool TrySpawnRift(
            Scene scene)
        {
            if (!FOTFExpansionGate.IsUnlocked)
            {
                return false;
            }

            GameObject prefab =
                AssetBundleRegistry.GetPrefab(
                    BundleName,
                    PrefabName);

            if (prefab == null)
            {
                Plugin.NativeLog.LogError(
                    "[Quest5Rift] prefab [" +
                    PrefabName +
                    "] was not found in bundle [" +
                    BundleName +
                    "]. Path=[" +
                    AssetBundleRegistry.GetBundlePath(
                        BundleName) +
                    "].");

                return false;
            }

            GameObject rift =
                null;

            try
            {
                rift =
                    UnityEngine.Object.Instantiate(
                        prefab,
                        RequestedPosition,
                        prefab.transform.rotation);

                if (rift == null)
                {
                    Plugin.NativeLog.LogError(
                        "[Quest5Rift] Instantiate returned null.");

                    return false;
                }

                rift.name =
                    RuntimeObjectName;

                SceneManager.MoveGameObjectToScene(
                    rift,
                    scene);

                rift.SetActive(
                    true);

                DisableAuthoredColliders(
                    rift);

                Bounds bounds;
                int rendererCount;

                if (!TryGetRendererBounds(
                        rift,
                        out bounds,
                        out rendererCount))
                {
                    Plugin.NativeLog.LogError(
                        "[Quest5Rift] FlameRift contains no enabled Renderer components.");

                    UnityEngine.Object.Destroy(
                        rift);

                    return false;
                }

                Plugin.NativeLog.LogInfo(
                    "[Quest5Rift] IMPORT DIAGNOSTIC rootScale=[" +
                    rift.transform.localScale +
                    "] rotation=[" +
                    rift.transform.eulerAngles +
                    "] rendererCount=[" +
                    rendererCount +
                    "] bounds=[" +
                    FormatBounds(
                        bounds) +
                    "].");

                NormalizeVisualScale(
                    rift,
                    bounds);

                // Exactly like the grounded grave fix: the supplied world Y
                // is authoritative. Do not lift the prefab using bounds.min.y.
                rift.transform.position =
                    RequestedPosition;

                ReplaceImportedMaterials(
                    rift);

                ClearTerrainDetailsAround(
                    RequestedPosition,
                    TerrainClearRadius);

                ConfigureShardInteraction(
                    rift);

                _rift =
                    rift;

                RefreshShardState();

                TryGetRendererBounds(
                    rift,
                    out bounds,
                    out rendererCount);

                Plugin.NativeLog.LogInfo(
                    "[Quest5Rift] SPAWNED grounded Flame Rift. rootPosition=[" +
                    rift.transform.position +
                    "] finalBounds=[" +
                    FormatBounds(
                        bounds) +
                    "] InteractionAnchor=[" +
                    FormatAnchor(
                        rift,
                        "InteractionAnchor") +
                    "] SpawnAnchor=[" +
                    FormatAnchor(
                        rift,
                        "SpawnAnchor") +
                    "].");

                return true;
            }
            catch (Exception exception)
            {
                if (rift != null)
                {
                    UnityEngine.Object.Destroy(
                        rift);
                }

                RestoreTerrainDetails();

                Plugin.NativeLog.LogError(
                    "[Quest5Rift] spawn failed: " +
                    exception);

                return false;
            }
        }


        // ============================================================
        // MATERIAL REPLACEMENT - SAME PRINCIPLE AS QUEST 4 GRAVE
        // ============================================================

        private static void ReplaceImportedMaterials(
            GameObject rift)
        {
            Shader standard =
                Shader.Find(
                    "Standard");

            if (standard == null)
            {
                Plugin.NativeLog.LogError(
                    "[Quest5Rift] Unity Standard shader was not found.");

                return;
            }

            Renderer[] renderers =
                rift.GetComponentsInChildren<Renderer>(
                    true);

            int changed =
                0;

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                string objectName =
                    renderer.gameObject.name ??
                    "";

                Material[] source =
                    renderer.sharedMaterials;

                int slots =
                    source != null &&
                    source.Length > 0
                        ? source.Length
                        : 1;

                Material[] replacements =
                    new Material[slots];

                for (int slot = 0;
                     slot < slots;
                     slot++)
                {
                    string sourceName =
                        source != null &&
                        slot < source.Length &&
                        source[slot] != null
                            ? source[slot].name
                            : "";

                    string key =
                        (objectName +
                         " " +
                         sourceName)
                            .ToLowerInvariant();

                    RiftMaterialKind kind =
                        DetermineMaterialKind(
                            key);

                    Material material =
                        new Material(
                            standard);

                    material.name =
                        "FOTF_Q5_" +
                        kind +
                        "_" +
                        i +
                        "_" +
                        slot;

                    material.hideFlags =
                        HideFlags.DontSave;

                    Color color =
                        GetBaseColor(
                            kind);

                    if (material.HasProperty(
                            "_Color"))
                    {
                        material.SetColor(
                            "_Color",
                            color);
                    }

                    if (material.HasProperty(
                            "_MainTex"))
                    {
                        material.SetTexture(
                            "_MainTex",
                            Texture2D.whiteTexture);
                    }

                    if (material.HasProperty(
                            "_Metallic"))
                    {
                        material.SetFloat(
                            "_Metallic",
                            kind == RiftMaterialKind.Shard
                                ? 0.10f
                                : 0.0f);
                    }

                    if (material.HasProperty(
                            "_Glossiness"))
                    {
                        material.SetFloat(
                            "_Glossiness",
                            kind == RiftMaterialKind.Shard
                                ? 0.22f
                                : 0.06f);
                    }

                    if (material.HasProperty(
                            "_EmissionColor"))
                    {
                        Color emission =
                            GetEmissionColor(
                                kind);

                        if (emission.maxColorComponent >
                            0.001f)
                        {
                            material.SetColor(
                                "_EmissionColor",
                                emission);

                            material.EnableKeyword(
                                "_EMISSION");
                        }
                        else
                        {
                            material.SetColor(
                                "_EmissionColor",
                                Color.black);

                            material.DisableKeyword(
                                "_EMISSION");
                        }
                    }

                    replacements[slot] =
                        material;
                }

                renderer.sharedMaterials =
                    replacements;

                renderer.enabled =
                    true;

                renderer.receiveShadows =
                    true;

                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.On;

                changed++;
            }

            Plugin.NativeLog.LogInfo(
                "[Quest5Rift] assigned corrected Standard material palette to [" +
                changed +
                "] renderer(s).");
        }


        private static RiftMaterialKind DetermineMaterialKind(
            string key)
        {
            if (string.IsNullOrEmpty(
                    key))
            {
                return
                    RiftMaterialKind.ScorchedEarth;
            }

            if (key.Contains("cinderedriftshard") ||
                key.Contains("cinderedshard") ||
                key.Contains("riftshard"))
            {
                return
                    RiftMaterialKind.Shard;
            }

            if (key.Contains("flame") ||
                key.Contains("riftglow"))
            {
                return
                    RiftMaterialKind.Flame;
            }

            if (key.Contains("ember") ||
                key.Contains("hotstone") ||
                key.Contains("hot_stone") ||
                key.Contains("riftpocket"))
            {
                return
                    RiftMaterialKind.Ember;
            }

            if (key.Contains("rock") ||
                key.Contains("stone") ||
                key.Contains("riftedge") ||
                key.Contains("endcap"))
            {
                return
                    RiftMaterialKind.CharredStone;
            }

            if (key.Contains("ash"))
            {
                return
                    RiftMaterialKind.Ash;
            }

            return
                RiftMaterialKind.ScorchedEarth;
        }


        private static Color GetBaseColor(
            RiftMaterialKind kind)
        {
            switch (kind)
            {
                case RiftMaterialKind.CharredStone:
                    return
                        new Color(
                            0.095f,
                            0.080f,
                            0.075f,
                            1.0f);

                case RiftMaterialKind.Ash:
                    return
                        new Color(
                            0.17f,
                            0.145f,
                            0.13f,
                            1.0f);

                case RiftMaterialKind.Ember:
                    return
                        new Color(
                            0.50f,
                            0.075f,
                            0.015f,
                            1.0f);

                case RiftMaterialKind.Flame:
                    return
                        new Color(
                            0.88f,
                            0.10f,
                            0.015f,
                            1.0f);

                case RiftMaterialKind.Shard:
                    return
                        new Color(
                            0.23f,
                            0.045f,
                            0.020f,
                            1.0f);

                default:
                    return
                        new Color(
                            0.055f,
                            0.040f,
                            0.035f,
                            1.0f);
            }
        }


        private static Color GetEmissionColor(
            RiftMaterialKind kind)
        {
            switch (kind)
            {
                case RiftMaterialKind.Flame:
                    return
                        new Color(
                            1.0f,
                            0.13f,
                            0.01f,
                            1.0f) *
                        2.0f;

                case RiftMaterialKind.Ember:
                    return
                        new Color(
                            0.95f,
                            0.07f,
                            0.005f,
                            1.0f) *
                        1.25f;

                case RiftMaterialKind.Shard:
                    return
                        new Color(
                            0.80f,
                            0.035f,
                            0.005f,
                            1.0f) *
                        0.75f;

                default:
                    return
                        Color.black;
            }
        }


        private enum RiftMaterialKind
        {
            ScorchedEarth,
            Ash,
            CharredStone,
            Ember,
            Flame,
            Shard
        }


        // ============================================================
        // SCALE / BOUNDS
        // ============================================================

        private static void NormalizeVisualScale(
            GameObject rift,
            Bounds bounds)
        {
            float horizontalSize =
                Mathf.Max(
                    bounds.size.x,
                    bounds.size.z);

            if (horizontalSize >=
                MinimumAcceptableHorizontalSize)
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest5Rift] imported horizontal size [" +
                    horizontalSize.ToString("0.###") +
                    "m] is already usable; no scale correction required.");

                return;
            }

            if (horizontalSize <=
                0.0001f)
            {
                return;
            }

            float multiplier =
                Mathf.Clamp(
                    ExpectedHorizontalSize /
                    horizontalSize,
                    1.0f,
                    MaximumAutomaticScaleMultiplier);

            rift.transform.localScale =
                rift.transform.localScale *
                multiplier;

            Plugin.NativeLog.LogInfo(
                "[Quest5Rift] corrected imported visual scale by [" +
                multiplier.ToString("0.###") +
                "x].");
        }


        private static bool TryGetRendererBounds(
            GameObject root,
            out Bounds bounds,
            out int rendererCount)
        {
            bounds =
                new Bounds();

            rendererCount =
                0;

            if (root == null)
            {
                return false;
            }

            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(
                    true);

            bool initialized =
                false;

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Bounds current =
                    renderer.bounds;

                if (current.size.sqrMagnitude <=
                    0.0000001f)
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds =
                        current;

                    initialized =
                        true;
                }
                else
                {
                    bounds.Encapsulate(
                        current);
                }

                rendererCount++;
            }

            return
                initialized;
        }


        private static string FormatBounds(
            Bounds bounds)
        {
            return
                "center=" +
                bounds.center +
                " size=" +
                bounds.size +
                " min=" +
                bounds.min +
                " max=" +
                bounds.max;
        }


        // ============================================================
        // SHARD INTERACTION
        // ============================================================

        private static void ConfigureShardInteraction(
            GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Transform shard =
                FindChildRecursive(
                    root.transform,
                    "CinderedRiftShard");

            if (shard == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest5Rift] CinderedRiftShard child is missing; the rift remains visible but cannot currently be harvested.");

                return;
            }

            Transform interactionAnchor =
                FindChildRecursive(
                    root.transform,
                    "InteractionAnchor");

            // ========================================================
            // SAME CLICK-COLLIDER STRATEGY AS QUEST 3
            // ========================================================
            //
            // Do NOT put the click collider on the imported shard child.
            // FBX child transforms can have tiny/non-uniform local scale,
            // making an apparently large local collider nearly impossible to
            // click in world space.
            //
            // Instead:
            //   1. Disable all authored colliders.
            //   2. Measure the visible shard in WORLD space.
            //   3. Convert those bounds into the RIFT ROOT's local space.
            //   4. Put one reliable BoxCollider on the root.
            //
            Collider[] authoredColliders =
                root.GetComponentsInChildren<Collider>(
                    true);

            for (int i = 0;
                 i < authoredColliders.Length;
                 i++)
            {
                if (authoredColliders[i] != null)
                {
                    authoredColliders[i].enabled =
                        false;
                }
            }

            Renderer[] renderers =
                shard.GetComponentsInChildren<Renderer>(
                    true);

            bool hasBounds =
                false;

            Vector3 localMin =
                Vector3.zero;

            Vector3 localMax =
                Vector3.zero;

            if (renderers != null)
            {
                for (int i = 0;
                     i < renderers.Length;
                     i++)
                {
                    Renderer renderer =
                        renderers[i];

                    if (renderer == null ||
                        !renderer.enabled ||
                        !renderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    Bounds worldBounds =
                        renderer.bounds;

                    Vector3 worldMin =
                        worldBounds.min;

                    Vector3 worldMax =
                        worldBounds.max;

                    for (int corner = 0;
                         corner < 8;
                         corner++)
                    {
                        Vector3 worldCorner =
                            new Vector3(
                                (corner & 1) == 0
                                    ? worldMin.x
                                    : worldMax.x,
                                (corner & 2) == 0
                                    ? worldMin.y
                                    : worldMax.y,
                                (corner & 4) == 0
                                    ? worldMin.z
                                    : worldMax.z);

                        Vector3 localCorner =
                            root.transform
                                .InverseTransformPoint(
                                    worldCorner);

                        if (!hasBounds)
                        {
                            localMin =
                                localCorner;

                            localMax =
                                localCorner;

                            hasBounds =
                                true;
                        }
                        else
                        {
                            localMin =
                                Vector3.Min(
                                    localMin,
                                    localCorner);

                            localMax =
                                Vector3.Max(
                                    localMax,
                                    localCorner);
                        }
                    }
                }
            }

            Vector3 rootScale =
                root.transform.lossyScale;

            float scaleX =
                Mathf.Max(
                    Mathf.Abs(rootScale.x),
                    0.0001f);

            float scaleY =
                Mathf.Max(
                    Mathf.Abs(rootScale.y),
                    0.0001f);

            float scaleZ =
                Mathf.Max(
                    Mathf.Abs(rootScale.z),
                    0.0001f);

            BoxCollider collider =
                root.GetComponent<BoxCollider>();

            if (collider == null)
            {
                collider =
                    root.AddComponent<BoxCollider>();
            }

            if (hasBounds)
            {
                collider.center =
                    (localMin + localMax) *
                    0.5f;

                // Make the clickable region intentionally a little larger
                // than the visible shard. Padding values are in world metres
                // and converted back into root-local coordinates.
                Vector3 worldPadding =
                    new Vector3(
                        0.75f / scaleX,
                        0.75f / scaleY,
                        0.75f / scaleZ);

                collider.size =
                    (localMax - localMin) +
                    worldPadding;
            }
            else
            {
                // Defensive fallback around the authored InteractionAnchor.
                Vector3 centerWorld =
                    interactionAnchor != null
                        ? interactionAnchor.position
                        : shard.position;

                collider.center =
                    root.transform
                        .InverseTransformPoint(
                            centerWorld);

                collider.size =
                    new Vector3(
                        1.75f / scaleX,
                        1.75f / scaleY,
                        1.75f / scaleZ);
            }

            collider.isTrigger =
                true;

            collider.enabled =
                true;

            // The interactable must live on the SAME GameObject as the
            // Collider so Unity's OnMouseDown callback reliably reaches it.
            FlameRiftShardInteractable interactable =
                root.GetComponent<FlameRiftShardInteractable>();

            if (interactable == null)
            {
                interactable =
                    root.AddComponent<FlameRiftShardInteractable>();
            }

            interactable.Configure(
                shard,
                interactionAnchor);

            Plugin.NativeLog.LogInfo(
                "[Quest5Rift] shard interaction collider armed on rift root. " +
                "Center=[" +
                collider.center +
                "] Size=[" +
                collider.size +
                "] WorldBoundsResolved=[" +
                hasBounds +
                "] InteractionAnchor=[" +
                (interactionAnchor != null
                    ? interactionAnchor.position.ToString()
                    : "<missing>") +
                "].");
        }


        private static void RefreshShardState()
        {
            if (_rift == null)
            {
                return;
            }

            Transform shard =
                FindChildRecursive(
                    _rift.transform,
                    "CinderedRiftShard");

            if (shard == null)
            {
                return;
            }

            bool shouldShow =
                AshBeneathEarthQuest.IsActive() &&
                !AshBeneathEarthQuest.IsComplete() &&
                !AshBeneathEarthContent.PlayerHasShard();

            if (shard.gameObject.activeSelf !=
                shouldShow)
            {
                shard.gameObject.SetActive(
                    shouldShow);
            }
        }


        // ============================================================
        // TERRAIN DETAIL CLEAR / RESTORE - SAME AS QUEST 4 GRAVE
        // ============================================================

        private static void ClearTerrainDetailsAround(
            Vector3 worldCenter,
            float radiusMetres)
        {
            RestoreTerrainDetails();

            Terrain[] terrains =
                Terrain.activeTerrains;

            if (terrains == null ||
                terrains.Length == 0)
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest5Rift] no active Terrain found; terrain detail could not be cleared.");

                return;
            }

            int modifiedLayers =
                0;

            for (int terrainIndex = 0;
                 terrainIndex < terrains.Length;
                 terrainIndex++)
            {
                Terrain terrain =
                    terrains[terrainIndex];

                if (terrain == null ||
                    terrain.terrainData == null)
                {
                    continue;
                }

                TerrainData data =
                    terrain.terrainData;

                Vector3 origin =
                    terrain.transform.position;

                Vector3 local =
                    worldCenter -
                    origin;

                if (local.x < 0f ||
                    local.z < 0f ||
                    local.x > data.size.x ||
                    local.z > data.size.z)
                {
                    continue;
                }

                if (data.detailWidth <= 0 ||
                    data.detailHeight <= 0 ||
                    data.detailPrototypes == null ||
                    data.detailPrototypes.Length == 0)
                {
                    continue;
                }

                int centerX =
                    Mathf.RoundToInt(
                        (local.x / data.size.x) *
                        data.detailWidth);

                int centerY =
                    Mathf.RoundToInt(
                        (local.z / data.size.z) *
                        data.detailHeight);

                int radiusX =
                    Mathf.Max(
                        1,
                        Mathf.CeilToInt(
                            (radiusMetres / data.size.x) *
                            data.detailWidth));

                int radiusY =
                    Mathf.Max(
                        1,
                        Mathf.CeilToInt(
                            (radiusMetres / data.size.z) *
                            data.detailHeight));

                int xBase =
                    Mathf.Clamp(
                        centerX - radiusX,
                        0,
                        data.detailWidth - 1);

                int yBase =
                    Mathf.Clamp(
                        centerY - radiusY,
                        0,
                        data.detailHeight - 1);

                int width =
                    Mathf.Clamp(
                        radiusX * 2 + 1,
                        1,
                        data.detailWidth - xBase);

                int height =
                    Mathf.Clamp(
                        radiusY * 2 + 1,
                        1,
                        data.detailHeight - yBase);

                for (int layer = 0;
                     layer < data.detailPrototypes.Length;
                     layer++)
                {
                    int[,] original =
                        data.GetDetailLayer(
                            xBase,
                            yBase,
                            width,
                            height,
                            layer);

                    if (original == null)
                    {
                        continue;
                    }

                    int[,] cleared =
                        (int[,])original.Clone();

                    bool changed =
                        false;

                    for (int y = 0;
                         y < height;
                         y++)
                    {
                        for (int x = 0;
                             x < width;
                             x++)
                        {
                            int detailX =
                                xBase + x;

                            int detailY =
                                yBase + y;

                            float worldX =
                                origin.x +
                                ((detailX + 0.5f) /
                                 data.detailWidth) *
                                data.size.x;

                            float worldZ =
                                origin.z +
                                ((detailY + 0.5f) /
                                 data.detailHeight) *
                                data.size.z;

                            float dx =
                                worldX -
                                worldCenter.x;

                            float dz =
                                worldZ -
                                worldCenter.z;

                            if ((dx * dx) +
                                (dz * dz) <=
                                radiusMetres *
                                radiusMetres)
                            {
                                if (cleared[y, x] != 0)
                                {
                                    cleared[y, x] =
                                        0;

                                    changed =
                                        true;
                                }
                            }
                        }
                    }

                    if (!changed)
                    {
                        continue;
                    }

                    _terrainDetailPatches.Add(
                        new TerrainDetailPatch(
                            terrain,
                            layer,
                            xBase,
                            yBase,
                            original));

                    data.SetDetailLayer(
                        xBase,
                        yBase,
                        layer,
                        cleared);

                    modifiedLayers++;
                }
            }

            Plugin.NativeLog.LogInfo(
                "[Quest5Rift] cleared native terrain detail around rift. Radius=[" +
                radiusMetres.ToString("0.00") +
                "m] modifiedLayers=[" +
                modifiedLayers +
                "].");
        }


        private static void RestoreTerrainDetails()
        {
            for (int i =
                     _terrainDetailPatches.Count - 1;
                 i >= 0;
                 i--)
            {
                TerrainDetailPatch patch =
                    _terrainDetailPatches[i];

                if (patch == null ||
                    patch.Terrain == null ||
                    patch.Terrain.terrainData == null ||
                    patch.Original == null)
                {
                    continue;
                }

                try
                {
                    patch.Terrain.terrainData.SetDetailLayer(
                        patch.XBase,
                        patch.YBase,
                        patch.Layer,
                        patch.Original);
                }
                catch
                {
                }
            }

            _terrainDetailPatches.Clear();
        }


        private sealed class TerrainDetailPatch
        {
            internal readonly Terrain Terrain;
            internal readonly int Layer;
            internal readonly int XBase;
            internal readonly int YBase;
            internal readonly int[,] Original;

            internal TerrainDetailPatch(
                Terrain terrain,
                int layer,
                int xBase,
                int yBase,
                int[,] original)
            {
                Terrain = terrain;
                Layer = layer;
                XBase = xBase;
                YBase = yBase;
                Original = original;
            }
        }


        // ============================================================
        // ANCHORS
        // ============================================================

        internal static bool TryGetSpawnAnchor(
            out Transform spawnAnchor)
        {
            spawnAnchor =
                null;

            if (_rift == null)
            {
                return false;
            }

            spawnAnchor =
                FindChildRecursive(
                    _rift.transform,
                    "SpawnAnchor");

            return
                spawnAnchor != null;
        }


        internal static bool TryGetEncounterAnchor(
            int index,
            out Transform encounterAnchor)
        {
            encounterAnchor =
                null;

            if (_rift == null ||
                index < 1 ||
                index > 3)
            {
                return false;
            }

            encounterAnchor =
                FindChildRecursive(
                    _rift.transform,
                    "EncounterAnchor_" +
                    index);

            return
                encounterAnchor != null;
        }


        // ============================================================
        // HELPERS / CLEANUP
        // ============================================================

        private static void DisableAuthoredColliders(
            GameObject root)
        {
            Collider[] colliders =
                root.GetComponentsInChildren<Collider>(
                    true);

            for (int i = 0;
                 i < colliders.Length;
                 i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled =
                        false;
                }
            }
        }


        private static string FormatAnchor(
            GameObject root,
            string anchorName)
        {
            Transform anchor =
                FindChildRecursive(
                    root != null
                        ? root.transform
                        : null,
                    anchorName);

            return
                anchor != null
                    ? anchor.position.ToString()
                    : "<missing>";
        }


        internal static Transform FindChildRecursive(
            Transform root,
            string name)
        {
            if (root == null ||
                string.IsNullOrEmpty(
                    name))
            {
                return null;
            }

            if (string.Equals(
                    root.name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int i = 0;
                 i < root.childCount;
                 i++)
            {
                Transform found =
                    FindChildRecursive(
                        root.GetChild(i),
                        name);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }


        private static void DestroyRift()
        {
            RestoreTerrainDetails();

            if (_rift == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(
                _rift);

            _rift =
                null;
        }
    }


    internal sealed class FlameRiftShardInteractable :
        MonoBehaviour
    {
        private const float InteractionRange =
            6.0f;

        private Transform _shardVisual;
        private Transform _interactionAnchor;
        private bool _harvested;


        internal void Configure(
            Transform shardVisual,
            Transform interactionAnchor)
        {
            _shardVisual =
                shardVisual;

            _interactionAnchor =
                interactionAnchor;

            _harvested =
                AshBeneathEarthContent.PlayerHasShard();
        }


        private void OnMouseDown()
        {
            Plugin.NativeLog.LogInfo(
                "[Quest5Rift] shard click received.");

            TryHarvest();
        }


        private void TryHarvest()
        {
            if (_harvested ||
                AshBeneathEarthContent.PlayerHasShard())
            {
                return;
            }

            if (!FOTFExpansionGate.IsUnlocked ||
                !AshBeneathEarthQuest.IsActive() ||
                AshBeneathEarthQuest.IsComplete())
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest5Rift] shard click rejected. ExpansionUnlocked=[" +
                    FOTFExpansionGate.IsUnlocked +
                    "] QuestActive=[" +
                    AshBeneathEarthQuest.IsActive() +
                    "] QuestComplete=[" +
                    AshBeneathEarthQuest.IsComplete() +
                    "].");

                return;
            }

            PlayerControl player =
                GameData.PlayerControl;

            if (player == null)
            {
                return;
            }

            Vector3 point =
                _interactionAnchor != null
                    ? _interactionAnchor.position
                    : transform.position;

            float distance =
                Vector3.Distance(
                    player.transform.position,
                    point);

            if (distance >
                InteractionRange)
            {
                try
                {
                    UpdateSocialLog.LogAdd(
                        new ChatLogLine(
                            "You are too far away to reach the Cindered Rift Shard.",
                            ChatLogLine.LogType.SystemMessages,
                            "yellow"));
                }
                catch
                {
                }

                Plugin.NativeLog.LogInfo(
                    "[Quest5Rift] shard click rejected by range. Distance=[" +
                    distance.ToString("0.00") +
                    "] Limit=[" +
                    InteractionRange.ToString("0.00") +
                    "].");

                return;
            }

            if (!AshBeneathEarthContent.TryGiveShard(
                    true))
            {
                return;
            }

            _harvested =
                true;

            Collider collider =
                GetComponent<Collider>();

            if (collider != null)
            {
                collider.enabled =
                    false;
            }

            if (_shardVisual != null)
            {
                _shardVisual.gameObject.SetActive(
                    false);
            }

            Plugin.NativeLog.LogInfo(
                "[Quest5Rift] player recovered Cindered Rift Shard [1703].");
        }
    }
}
