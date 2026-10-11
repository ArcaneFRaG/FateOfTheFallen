using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 4 Disturbed Grave world prop.
    ///
    /// Fernalla's Revival Plains
    /// Native scene: fernallafield
    ///
    /// Revision:
    /// - keep root at the exact authored world Y
    /// - do NOT lift the grave based on renderer min bounds
    /// - replace imported materials with neutral/cool Standard materials
    /// - only soul effects use emission
    /// - keep local terrain-detail clearing around the grave
    /// </summary>
    internal static class DisturbedGraveRuntime
    {
        internal const string SceneName =
            "fernallafield";

        internal const string BundleName =
            "fotf_questprops";

        internal const string PrefabName =
            "DisturbedGrave";

        private const string RuntimeObjectName =
            "FOTF_Quest04_DisturbedGrave";

        private static readonly Vector3 RequestedPosition =
            new Vector3(
                1068.71f,
                -1.54f,
                631.74f);

        private const float ExpectedHorizontalSize =
            3.6f;

        private const float MinimumAcceptableHorizontalSize =
            1.25f;

        private const float MaximumAutomaticScaleMultiplier =
            100.0f;

        private const float GrassClearRadius =
            4.25f;

        private const int PollFrames =
            30;

        private static bool _initialized;
        private static bool _loggedWaitingForQuest;
        private static GameObject _grave;
        private static Coroutine _monitorRoutine;

        private static readonly List<TerrainDetailPatch>
            _terrainDetailPatches =
                new List<TerrainDetailPatch>();


        internal static GameObject CurrentGrave =>
            _grave;


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
                "[Quest4Grave] initialized. ActiveScene=[" +
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
            DestroyGrave();
        }


        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            StopMonitor();
            DestroyGrave();

            Plugin.NativeLog.LogInfo(
                "[Quest4Grave] sceneLoaded=[" +
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
                DestroyGrave();

                Plugin.NativeLog.LogInfo(
                    "[Quest4Grave] expansion lock engaged; removed Disturbed Grave.");

                return;
            }

            Scene scene =
                SceneManager.GetActiveScene();

            if (IsTargetScene(
                    scene))
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest4Grave] expansion unlocked in fernallafield; restarting Quest 4 grave monitor.");

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

            while (_initialized &&
                   scene.IsValid() &&
                   scene.isLoaded)
            {
                if (!FOTFExpansionGate.IsUnlocked)
                {
                    DestroyGrave();

                    _monitorRoutine =
                        null;

                    yield break;
                }

                if (!IsTargetScene(
                        SceneManager.GetActiveScene()))
                {
                    break;
                }

                bool active =
                    FateOfTheFallenQuests
                        .IsRestlessDeadActive();

                bool complete =
                    FateOfTheFallenQuests
                        .IsRestlessDeadComplete();

                if (complete)
                {
                    DestroyGrave();
                    break;
                }

                if (active)
                {
                    _loggedWaitingForQuest =
                        false;

                    if (_grave == null)
                    {
                        if (!TrySpawnGrave(
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
                }
                else if (!_loggedWaitingForQuest)
                {
                    _loggedWaitingForQuest =
                        true;

                    Plugin.NativeLog.LogInfo(
                        "[Quest4Grave] target scene correct; waiting for Quest 4. " +
                        "Active=[" +
                        active +
                        "] Complete=[" +
                        complete +
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
        // SPAWN
        // ============================================================

        private static bool TrySpawnGrave(
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
                    "[Quest4Grave] prefab [" +
                    PrefabName +
                    "] was not found in bundle [" +
                    BundleName +
                    "]. Path=[" +
                    AssetBundleRegistry.GetBundlePath(
                        BundleName) +
                    "].");

                return false;
            }

            GameObject grave =
                null;

            try
            {
                grave =
                    UnityEngine.Object.Instantiate(
                        prefab,
                        RequestedPosition,
                        prefab.transform.rotation);

                if (grave == null)
                {
                    Plugin.NativeLog.LogError(
                        "[Quest4Grave] Instantiate returned null.");

                    return false;
                }

                grave.name =
                    RuntimeObjectName;

                SceneManager.MoveGameObjectToScene(
                    grave,
                    scene);

                grave.SetActive(
                    true);

                DisableAuthoredColliders(
                    grave);

                Bounds bounds;
                int rendererCount;

                if (!TryGetRendererBounds(
                        grave,
                        out bounds,
                        out rendererCount))
                {
                    Plugin.NativeLog.LogError(
                        "[Quest4Grave] DisturbedGrave contains no enabled Renderer components.");

                    UnityEngine.Object.Destroy(
                        grave);

                    return false;
                }

                Plugin.NativeLog.LogInfo(
                    "[Quest4Grave] IMPORT DIAGNOSTIC rootScale=[" +
                    grave.transform.localScale +
                    "] rotation=[" +
                    grave.transform.eulerAngles +
                    "] rendererCount=[" +
                    rendererCount +
                    "] bounds=[" +
                    FormatBounds(bounds) +
                    "].");

                NormalizeVisualScale(
                    grave,
                    bounds);

                // IMPORTANT:
                // Do NOT lift the grave using bounds.min.y.
                //
                // The prefab intentionally contains the burial trench below
                // its root origin. The supplied Y coordinate is the correct
                // terrain/root placement. Raising to renderer min caused the
                // entire grave to float above Fernalla's terrain.
                grave.transform.position =
                    RequestedPosition;

                ReplaceImportedMaterials(
                    grave);

                ClearTerrainDetailsAround(
                    RequestedPosition,
                    GrassClearRadius);

                _grave =
                    grave;

                TryGetRendererBounds(
                    grave,
                    out bounds,
                    out rendererCount);

                Transform spawnAnchor =
                    FindChildRecursive(
                        grave.transform,
                        "SpawnAnchor");

                Plugin.NativeLog.LogInfo(
                    "[Quest4Grave] SPAWNED grounded grave. rootPosition=[" +
                    grave.transform.position +
                    "] finalBounds=[" +
                    FormatBounds(bounds) +
                    "] SpawnAnchor=[" +
                    (spawnAnchor != null
                        ? spawnAnchor.position.ToString()
                        : "<missing>") +
                    "].");

                return true;
            }
            catch (Exception exception)
            {
                if (grave != null)
                {
                    UnityEngine.Object.Destroy(
                        grave);
                }

                RestoreTerrainDetails();

                Plugin.NativeLog.LogError(
                    "[Quest4Grave] spawn failed: " +
                    exception);

                return false;
            }
        }


        // ============================================================
        // MATERIAL REPLACEMENT
        // ============================================================

        private static void ReplaceImportedMaterials(
            GameObject grave)
        {
            Shader standard =
                Shader.Find(
                    "Standard");

            if (standard == null)
            {
                Plugin.NativeLog.LogError(
                    "[Quest4Grave] Unity Standard shader was not found.");

                return;
            }

            Renderer[] renderers =
                grave.GetComponentsInChildren<Renderer>(
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

                    GraveMaterialKind kind =
                        DetermineMaterialKind(
                            key);

                    Material material =
                        new Material(
                            standard);

                    material.name =
                        "FOTF_Q4_" +
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
                            0.0f);
                    }

                    if (material.HasProperty(
                            "_Glossiness"))
                    {
                        material.SetFloat(
                            "_Glossiness",
                            kind == GraveMaterialKind.Stone
                                ? 0.08f
                                : 0.03f);
                    }

                    // Only the supernatural soul elements glow.
                    if (material.HasProperty(
                            "_EmissionColor"))
                    {
                        if (kind ==
                            GraveMaterialKind.Soul)
                        {
                            material.SetColor(
                                "_EmissionColor",
                                new Color(
                                    0.26f,
                                    0.035f,
                                    0.52f,
                                    1.0f));

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
                "[Quest4Grave] assigned corrected neutral/cool material palette to [" +
                changed +
                "] renderer(s).");
        }


        private static GraveMaterialKind DetermineMaterialKind(
            string key)
        {
            if (key.Contains("soul") ||
                key.Contains("mist"))
            {
                return
                    GraveMaterialKind.Soul;
            }

            if (key.Contains("bone") ||
                key.Contains("finger") ||
                key.Contains("palm") ||
                key.Contains("forearm"))
            {
                return
                    GraveMaterialKind.Bone;
            }

            if (key.Contains("stone") ||
                key.Contains("headstone"))
            {
                return
                    GraveMaterialKind.Stone;
            }

            if (key.Contains("wood") ||
                key.Contains("coffin") ||
                key.Contains("plank"))
            {
                return
                    GraveMaterialKind.Wood;
            }

            if (key.Contains("grass"))
            {
                return
                    GraveMaterialKind.Grass;
            }

            return
                GraveMaterialKind.Dirt;
        }


        private static Color GetBaseColor(
            GraveMaterialKind kind)
        {
            // Fernalla's Revival Plains has very warm/red lighting.
            // These are deliberately cooler and less saturated than the
            // Blender source values so they read naturally in-game.
            switch (kind)
            {
                case GraveMaterialKind.Stone:
                    return
                        new Color(
                            0.34f,
                            0.39f,
                            0.42f,
                            1.0f);

                case GraveMaterialKind.Wood:
                    return
                        new Color(
                            0.18f,
                            0.115f,
                            0.075f,
                            1.0f);

                case GraveMaterialKind.Bone:
                    return
                        new Color(
                            0.62f,
                            0.64f,
                            0.57f,
                            1.0f);

                case GraveMaterialKind.Grass:
                    return
                        new Color(
                            0.24f,
                            0.27f,
                            0.14f,
                            1.0f);

                case GraveMaterialKind.Soul:
                    return
                        new Color(
                            0.18f,
                            0.035f,
                            0.30f,
                            1.0f);

                default:
                    // Cool dark earth. Avoid strong red because Fernalla's
                    // scene lighting already pushes everything warm.
                    return
                        new Color(
                            0.16f,
                            0.115f,
                            0.090f,
                            1.0f);
            }
        }


        private enum GraveMaterialKind
        {
            Dirt,
            Stone,
            Wood,
            Bone,
            Grass,
            Soul
        }


        // ============================================================
        // TERRAIN DETAIL CLEAR / RESTORE
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
                    "[Quest4Grave] no active Terrain found; terrain detail could not be cleared.");

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
                "[Quest4Grave] cleared native terrain detail around grave. Radius=[" +
                radiusMetres.ToString("0.00") +
                "m] modifiedLayers=[" +
                modifiedLayers +
                "].");
        }


        private static void RestoreTerrainDetails()
        {
            if (_terrainDetailPatches.Count == 0)
            {
                return;
            }

            for (int i = 0;
                 i < _terrainDetailPatches.Count;
                 i++)
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
        // SCALE / BOUNDS
        // ============================================================

        private static void NormalizeVisualScale(
            GameObject grave,
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
                    "[Quest4Grave] imported horizontal size [" +
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

            grave.transform.localScale =
                grave.transform.localScale *
                multiplier;
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
        // SPAWN ANCHOR
        // ============================================================

        internal static bool TryGetSpawnAnchor(
            out Transform spawnAnchor)
        {
            spawnAnchor =
                null;

            if (_grave == null)
            {
                return false;
            }

            spawnAnchor =
                FindChildRecursive(
                    _grave.transform,
                    "SpawnAnchor");

            return
                spawnAnchor != null;
        }


        internal static bool TryGetSpawnPose(
            out Vector3 position,
            out Quaternion rotation)
        {
            position =
                RequestedPosition;

            rotation =
                Quaternion.identity;

            Transform anchor;

            if (!TryGetSpawnAnchor(
                    out anchor))
            {
                return false;
            }

            position =
                anchor.position;

            rotation =
                anchor.rotation;

            return true;
        }


        // ============================================================
        // HELPERS / CLEANUP
        // ============================================================

        private static void DisableAuthoredColliders(
            GameObject grave)
        {
            Collider[] colliders =
                grave != null
                    ? grave.GetComponentsInChildren<Collider>(
                        true)
                    : null;

            if (colliders == null)
            {
                return;
            }

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


        private static Transform FindChildRecursive(
            Transform root,
            string objectName)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(
                    root.name,
                    objectName,
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
                        objectName);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }


        private static void DestroyGrave()
        {
            RestoreTerrainDetails();

            if (_grave != null)
            {
                UnityEngine.Object.Destroy(
                    _grave);

                _grave =
                    null;
            }
        }
    }
}
