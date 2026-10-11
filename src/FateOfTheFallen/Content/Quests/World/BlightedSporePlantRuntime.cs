using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen.Content.Quests.World
{
    /// <summary>
    /// Quest 2 harvestable world prop.
    ///
    /// Flow:
    /// Loomingwood scene loads -> a six-plant blighted patch spawns -> player clicks
    /// any plant within interaction range -> canonical Blighted Spores [1700]
    /// is added to inventory -> the whole patch becomes spent for the scene.
    /// </summary>
    internal static class BlightedSporePlantRuntime
    {
        internal const string SceneName =
            "Loomingwood";

        internal const string BundleName =
            "fotf_questprops";

        internal const string PrefabName =
            "BlightedSporePlant";

        private const string RuntimeObjectName =
            "FOTF_Quest02_BlightedSporePlant";

        // Centre of the Blighted Spore patch in Loomingwood Forest.
        // The first plant uses this exact world-space position.
        private static readonly Vector3 RequestedPosition =
            new Vector3(
                659.02f,
                33.64f,
                560.76f);

        // Six deliberately irregular offsets create a natural patch rather
        // than a perfect circle. X/Z are relative to RequestedPosition.
        private static readonly Vector3[] PatchOffsets =
        {
            new Vector3( 0.00f, 0f,  0.00f),
            new Vector3( 3.20f, 0f,  1.10f),
            new Vector3(-2.40f, 0f,  2.80f),
            new Vector3( 1.40f, 0f, -3.20f),
            new Vector3(-3.50f, 0f, -1.30f),
            new Vector3( 4.10f, 0f, -2.40f)
        };

        // Give the repeated prefab enough variation that the patch does not
        // look stamped out.
        private static readonly float[] PatchYawOffsets =
        {
            0f,
            47f,
            -63f,
            118f,
            -132f,
            176f
        };

        private static readonly float[] PatchScaleMultipliers =
        {
            1.00f,
            0.92f,
            1.07f,
            0.96f,
            1.11f,
            0.89f
        };

        // Preserve the authored Blender/FBX orientation and apply only a
        // world-space yaw on top of it.
        private const float BasePatchYaw =
            210f;

        // Keep the visibility increase from the proven single-plant version.
        private const float PlantVisualScaleMultiplier =
            1.75f;

        // Patch members beyond the centre are vertically settled against
        // nearby terrain without allowing their X/Z offsets to drift.
        private const float TerrainRayHeight =
            12f;

        private const float TerrainRayDistance =
            30f;

        private const float MaximumAcceptedHeightCorrection =
            4f;

        private static bool _initialized;
        private static bool _patchHarvested;
        private static Coroutine _spawnRoutine;

        private static readonly List<GameObject>
            _plants =
                new List<GameObject>();

        internal static bool IsClusterHarvested =>
            _patchHarvested;


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

            Scene scene =
                SceneManager.GetActiveScene();

            if (IsLoomingwood(
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

            if (_spawnRoutine != null)
            {
                CoroutineHost.Stop(
                    _spawnRoutine);

                _spawnRoutine =
                    null;
            }

            DestroyPatch();
        }

        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            DestroyPatch();

            if (!IsLoomingwood(
                    scene))
            {
                return;
            }

            StartSpawn(
                scene);
        }

        private static bool IsLoomingwood(
            Scene scene)
        {
            return scene.IsValid() &&
                scene.isLoaded &&
                string.Equals(
                    scene.name,
                    SceneName,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void StartSpawn(
            Scene scene)
        {
            if (_spawnRoutine != null ||
                _plants.Count > 0)
            {
                return;
            }

            _spawnRoutine =
                CoroutineHost.Start(
                    SpawnWhenReady(
                        scene));
        }


        private static IEnumerator SpawnWhenReady(
            Scene scene)
        {
            // Give native Loomingwood geometry a moment to settle.
            for (int frame = 0;
                 frame < 120;
                 frame++)
            {
                if (!IsLoomingwood(
                        SceneManager.GetActiveScene()))
                {
                    _spawnRoutine =
                        null;

                    yield break;
                }

                if (GameData.PlayerControl != null &&
                    GameData.ItemDB != null)
                {
                    break;
                }

                yield return null;
            }

            if (!FateOfTheFallenQuests.IsSignsOfDecayActive() ||
                FateOfTheFallenQuests.IsSignsOfDecayComplete() ||
                SignsOfDecayContent.PlayerHasSample())
            {
                _spawnRoutine =
                    null;

                yield break;
            }

            Item spores =
                BlightedSpores.RegisterItem(
                    GameData.ItemDB);

            if (spores == null)
            {
                Plugin.NativeLog.LogError(
                    "Quest 2 plant patch: Blighted Spores [1700] was unavailable; patch spawn aborted.");

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
                    "Quest 2 plant patch: prefab [" +
                    PrefabName +
                    "] was not found in bundle [" +
                    BundleName +
                    "]. Expected bundle path=[" +
                    AssetBundleRegistry.GetBundlePath(
                        BundleName) +
                    "].");

                _spawnRoutine =
                    null;

                yield break;
            }

            DestroyPatch();

            _patchHarvested =
                false;

            int spawned =
                0;

            for (int i = 0;
                 i < PatchOffsets.Length;
                 i++)
            {
                Vector3 requested =
                    RequestedPosition +
                    PatchOffsets[i];

                // The centre plant honours the supplied coordinate exactly.
                // Surrounding plants keep their X/Z offsets but settle their
                // Y to nearby terrain where a sensible hit is available.
                Vector3 position =
                    i == 0
                        ? RequestedPosition
                        : ResolvePatchPlantPosition(
                            requested);

                float yawOffset =
                    i < PatchYawOffsets.Length
                        ? PatchYawOffsets[i]
                        : 0f;

                float scaleVariation =
                    i < PatchScaleMultipliers.Length
                        ? PatchScaleMultipliers[i]
                        : 1f;

                GameObject plant =
                    null;

                try
                {
                    Quaternion patchYaw =
                        Quaternion.Euler(
                            0f,
                            BasePatchYaw +
                            yawOffset,
                            0f);

                    Quaternion finalRotation =
                        patchYaw *
                        prefab.transform.rotation;

                    plant =
                        UnityEngine.Object.Instantiate(
                            prefab,
                            position,
                            finalRotation);

                    if (plant == null)
                    {
                        Plugin.NativeLog.LogWarning(
                            "Quest 2 plant patch: Unity returned null for patch member [" +
                            (i + 1) +
                            "].");

                        continue;
                    }

                    plant.name =
                        RuntimeObjectName +
                        "_" +
                        (i + 1);

                    plant.transform.localScale =
                        plant.transform.localScale *
                        PlantVisualScaleMultiplier *
                        scaleVariation;

                    SceneManager.MoveGameObjectToScene(
                        plant,
                        scene);

                    EnsureCollider(
                        plant);

                    BlightedSporePlantInteractable interactable =
                        plant.GetComponent<BlightedSporePlantInteractable>();

                    if (interactable == null)
                    {
                        interactable =
                            plant.AddComponent<BlightedSporePlantInteractable>();
                    }

                    interactable.Configure(
                        spores);

                    _plants.Add(
                        plant);

                    spawned++;
                }
                catch (Exception exception)
                {
                    if (plant != null)
                    {
                        UnityEngine.Object.Destroy(
                            plant);
                    }

                    Plugin.NativeLog.LogWarning(
                        "Quest 2 plant patch: failed spawning member [" +
                        (i + 1) +
                        "]: " +
                        exception.Message);
                }
            }

            Plugin.NativeLog.LogInfo(
                "Quest 2 plant patch: spawned [" +
                spawned +
                "/" +
                PatchOffsets.Length +
                "] Blighted Spore Plants around centre [" +
                RequestedPosition +
                "] with approximate radius [4.8m].");

            _spawnRoutine =
                null;
        }


        private static Vector3 ResolvePatchPlantPosition(
            Vector3 requested)
        {
            RaycastHit hit;

            Vector3 rayStart =
                new Vector3(
                    requested.x,
                    requested.y +
                    TerrainRayHeight,
                    requested.z);

            if (Physics.Raycast(
                    rayStart,
                    Vector3.down,
                    out hit,
                    TerrainRayDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                float correction =
                    Mathf.Abs(
                        hit.point.y -
                        requested.y);

                if (correction <=
                    MaximumAcceptedHeightCorrection)
                {
                    // Preserve exact patch X/Z; use only the discovered Y.
                    return
                        new Vector3(
                            requested.x,
                            hit.point.y,
                            requested.z);
                }
            }

            return requested;
        }


        internal static void NotifyClusterHarvested()
        {
            if (_patchHarvested)
            {
                return;
            }

            _patchHarvested =
                true;

            for (int i = 0;
                 i < _plants.Count;
                 i++)
            {
                SetPlantHarvestedVisual(
                    _plants[i]);
            }

            Plugin.NativeLog.LogInfo(
                "Quest 2 plant patch: one sample was harvested; the entire visible patch is now marked spent.");
        }


        private static void SetPlantHarvestedVisual(
            GameObject plant)
        {
            if (plant == null)
            {
                return;
            }

            Collider[] colliders =
                plant.GetComponentsInChildren<Collider>(
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

            Transform harvestVisual =
                FindChildRecursive(
                    plant.transform,
                    "HarvestableSpores");

            if (harvestVisual != null)
            {
                harvestVisual.gameObject.SetActive(
                    false);
            }
        }


        private static Transform FindChildRecursive(
            Transform root,
            string objectName)
        {
            if (root == null ||
                string.IsNullOrEmpty(
                    objectName))
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


        private static void EnsureCollider(
            GameObject plant)
        {
            if (plant == null)
            {
                return;
            }

            // --------------------------------------------------------
            // WHOLE-PLANT CLICK VOLUME
            // --------------------------------------------------------
            // Disable every authored/imported collider, then build one
            // trigger-only BoxCollider from the ACTUAL rendered bounds of
            // the complete plant after its final rotation and 1.75x scale.
            //
            // This means every visible stem, leaf and spore pod is inside
            // the mouse-hit volume. Because the box is a trigger it cannot
            // become an invisible movement wall.
            // --------------------------------------------------------

            Collider[] authoredColliders =
                plant.GetComponentsInChildren<Collider>(
                    true);

            for (int i = 0;
                 i < authoredColliders.Length;
                 i++)
            {
                Collider authoredCollider =
                    authoredColliders[i];

                if (authoredCollider != null)
                {
                    authoredCollider.enabled =
                        false;
                }
            }

            Renderer[] renderers =
                plant.GetComponentsInChildren<Renderer>(
                    true);

            bool hasBounds =
                false;

            Vector3 localMin =
                Vector3.zero;

            Vector3 localMax =
                Vector3.zero;

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

                // Renderer.bounds is a world-space AABB. Transform all eight
                // corners into plant-local space so the final root collider
                // safely contains the complete visible model even when the
                // prefab is rotated.
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
                        plant.transform.InverseTransformPoint(
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

            Vector3 rootScale =
                plant.transform.lossyScale;

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
                plant.GetComponent<BoxCollider>();

            if (collider == null)
            {
                collider =
                    plant.AddComponent<BoxCollider>();
            }

            if (hasBounds)
            {
                Vector3 center =
                    (localMin + localMax) *
                    0.5f;

                Vector3 size =
                    localMax - localMin;

                // Small WORLD-space padding makes thin leaf/spore edges easy
                // to click without creating a large interaction halo.
                Vector3 localPadding =
                    new Vector3(
                        0.18f / scaleX,
                        0.18f / scaleY,
                        0.18f / scaleZ);

                collider.center =
                    center;

                collider.size =
                    size +
                    localPadding;
            }
            else
            {
                // Defensive fallback if Unity reports no renderers. Keep the
                // trigger modest and scale-compensated.
                collider.center =
                    new Vector3(
                        0f,
                        1.25f / scaleY,
                        0f);

                collider.size =
                    new Vector3(
                        2.50f / scaleX,
                        2.75f / scaleY,
                        2.50f / scaleZ);
            }

            collider.isTrigger =
                true;

            collider.enabled =
                true;

            Plugin.NativeLog.LogInfo(
                "Quest 2 plant: configured whole-plant harvest trigger from renderer bounds. " +
                "Center=[" +
                collider.center +
                "] Size=[" +
                collider.size +
                "] RootLossyScale=[" +
                rootScale +
                "].");
        }

        private static void DestroyPatch()
        {
            for (int i = 0;
                 i < _plants.Count;
                 i++)
            {
                GameObject plant =
                    _plants[i];

                if (plant != null)
                {
                    UnityEngine.Object.Destroy(
                        plant);
                }
            }

            _plants.Clear();

            _patchHarvested =
                false;
        }
    }


    internal sealed class BlightedSporePlantInteractable :
        MonoBehaviour
    {
        private const float InteractionRange =
            4.5f;

        private Item _spores;
        private bool _harvested;

        internal void Configure(
            Item spores)
        {
            _spores =
                spores;
        }

        private void OnMouseDown()
        {
            TryHarvest();
        }

        private void TryHarvest()
        {
            if (_harvested ||
                BlightedSporePlantRuntime.IsClusterHarvested)
            {
                AddSystemMessage(
                    "The blighted patch has already been stripped of its usable spores.",
                    "yellow");

                return;
            }

            PlayerControl player =
                GameData.PlayerControl;

            if (player == null)
            {
                return;
            }

            float distance =
                Vector3.Distance(
                    player.transform.position,
                    transform.position);

            if (distance >
                InteractionRange)
            {
                AddSystemMessage(
                    "You are too far away to gather the spores.",
                    "yellow");

                return;
            }

            if (!QuestTwoAllowsHarvest())
            {
                AddSystemMessage(
                    "The growth is diseased, but you have no reason to disturb it yet.",
                    "yellow");

                return;
            }

            Item spores =
                _spores ??
                BlightedSpores.GetItem();

            if (spores == null)
            {
                Plugin.NativeLog.LogError(
                    "Quest 2 plant: harvest failed because Blighted Spores [1700] could not be resolved.");

                return;
            }

            if (GameData.PlayerInv == null)
            {
                return;
            }

            if (!SignsOfDecayContent.TryGiveSample(
                    false))
            {
                // Do not consume the node if the inventory rejected the item.
                AddSystemMessage(
                    "You cannot carry another sample of Blighted Spores.",
                    "yellow");

                return;
            }

            _harvested =
                true;

            BlightedSporePlantRuntime
                .NotifyClusterHarvested();

            AddSystemMessage(
                "You carefully gather a sample of Blighted Spores from the blighted patch.",
                "green");

            Plugin.NativeLog.LogInfo(
                "Quest 2 plant: player harvested Blighted Spores [1700].");
        }

        /// <summary>
        /// Single integration seam for the existing Quest 2 controller.
        ///
        /// Keep this method here rather than making the plant know about the
        /// whole quest implementation. Once the current Signs of Decay quest
        /// class is in the project, replace the return value with its existing
        /// active-state check, e.g.:
        ///
        ///     return Quest02SignsOfDecay.IsActive;
        ///
        /// The world object, item awarding and persistence path do not change.
        /// </summary>
        private static bool QuestTwoAllowsHarvest()
        {
            return
                FateOfTheFallenQuests.IsSignsOfDecayActive() &&
                !FateOfTheFallenQuests.IsSignsOfDecayComplete();
        }

        private static Transform FindChildRecursive(
            Transform root,
            string objectName)
        {
            if (root == null ||
                string.IsNullOrEmpty(
                    objectName))
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

        private static void AddSystemMessage(
            string message,
            string color)
        {
            try
            {
                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        message,
                        ChatLogLine.LogType.SystemMessages,
                        color ?? string.Empty));
            }
            catch
            {
                // Interaction should never fail solely because chat UI is not ready.
            }
        }
    }
}
