using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Permanent Quest 12 landmark in the native Reliquary.
    ///
    /// Preferred art path:
    ///     fotf_questprops / WardenArchiveStele
    ///
    /// If that prefab is not present yet, a low-poly procedural version is
    /// built at runtime so the quest remains fully playable while the authored
    /// Blender/Unity asset is iterated. Placement is resolved from the four
    /// native keeper statues in Rooms/Room L1, putting the stele at their
    /// visible centre and snapping it to the floor.
    /// </summary>
    internal static class WardenArchiveSteleRuntime
    {
        internal const string SceneName =
            "Reliquary";

        internal const string BundleName =
            "fotf_questprops";

        internal const string PrefabName =
            "WardenArchiveStele";

        private const string RuntimeObjectName =
            "FOTF_WardenArchiveStele";

        private const string LogPrefix =
            "[WardenArchiveStele] ";

        private const int SpawnDelayFrames =
            5;

        private static readonly string[] KeeperStatuePaths =
        {
            "Rooms/Room L1/Stone Soluna",
            "Rooms/Room L1/Stone Brax",
            "Rooms/Room L1/StoneVitheo",
            "Rooms/Room L1/Stone Fernalla"
        };

        private static bool _initialized;
        private static Coroutine _spawnRoutine;
        private static GameObject _stele;

        private static Material _stoneMaterial;
        private static Material _darkStoneMaterial;
        private static Material _engravingMaterial;
        private static Material _blightMaterial;
        private static Material _voidMaterial;
        private static Material _deathMaterial;
        private static Material _infernalMaterial;
        private static Material _wardCrackMaterial;


        internal static GameObject CurrentStele =>
            _stele;


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

            Scene active =
                SceneManager.GetActiveScene();

            if (FOTFExpansionGate.IsUnlocked &&
                IsReliquary(
                    active))
            {
                StartSpawn(
                    active);
            }

            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "initialized. ActiveScene=[" +
                (active.IsValid()
                    ? active.name
                    : "<invalid>") +
                "].");
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
            DestroyStele();
            DestroyMaterials();

            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "shutdown.");
        }


        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            // Quest 6 temporarily loads Reliquary additively while Azure is
            // still active. Never spawn Quest 12 content into that auxiliary
            // asset-source scene.
            if (mode == LoadSceneMode.Additive &&
                SceneManager.GetActiveScene() != scene)
            {
                return;
            }

            StopSpawnRoutine();
            DestroyStele();

            if (!_initialized ||
                !FOTFExpansionGate.IsUnlocked ||
                !IsReliquary(
                    scene))
            {
                return;
            }

            StartSpawn(
                scene);
        }


        private static bool IsReliquary(
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
            if (_spawnRoutine != null)
            {
                return;
            }

            _spawnRoutine =
                CoroutineHost.Start(
                    SpawnRoutine(
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


        private static IEnumerator SpawnRoutine(
            Scene scene)
        {
            for (int frame = 0;
                 frame < SpawnDelayFrames;
                 frame++)
            {
                if (!_initialized ||
                    !IsReliquary(
                        scene) ||
                    SceneManager.GetActiveScene() != scene)
                {
                    _spawnRoutine =
                        null;

                    yield break;
                }

                yield return null;
            }

            NamesThatWereBuriedQuest.Register();
            NamesThatWereBuriedQuest.EnsureChainState();

            Vector3 position;
            Quaternion rotation;
            string placementSource;

            if (!TryResolvePlacement(
                    scene,
                    out position,
                    out rotation,
                    out placementSource))
            {
                Plugin.NativeLog.LogWarning(
                    LogPrefix +
                    "could not resolve the four keeper-statue placement anchors. Using conservative Reliquary fallback position at world origin; this should be tuned after an in-game placement pass.");

                position =
                    Vector3.zero;

                rotation =
                    Quaternion.identity;

                placementSource =
                    "fallback world origin";
            }

            GameObject prefab =
                AssetBundleRegistry.GetPrefab(
                    BundleName,
                    PrefabName);

            GameObject stele =
                null;

            if (prefab != null)
            {
                try
                {
                    stele =
                        UnityEngine.Object.Instantiate(
                            prefab,
                            position,
                            rotation * prefab.transform.rotation);

                    if (stele != null)
                    {
                        Plugin.NativeLog.LogInfo(
                            LogPrefix +
                            "using authored prefab [" +
                            PrefabName +
                            "] from bundle [" +
                            BundleName +
                            "].");
                    }
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogWarning(
                        LogPrefix +
                        "authored prefab instantiate failed; using procedural fallback. " +
                        exception.Message);

                    stele =
                        null;
                }
            }

            if (stele == null)
            {
                stele =
                    BuildProceduralStele(
                        position,
                        rotation);

                Plugin.NativeLog.LogInfo(
                    LogPrefix +
                    "authored prefab is not bundled yet; procedural low-poly stele is active.");
            }

            if (stele == null)
            {
                Plugin.NativeLog.LogError(
                    LogPrefix +
                    "failed to create Warden Archive Stele.");

                _spawnRoutine =
                    null;

                yield break;
            }

            stele.name =
                RuntimeObjectName;

            SceneManager.MoveGameObjectToScene(
                stele,
                scene);

            ConfigureInteraction(
                stele);

            stele.SetActive(
                true);

            _stele =
                stele;

            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "spawned in Reliquary at [" +
                position +
                "] RotationY=[" +
                rotation.eulerAngles.y.ToString("0.0") +
                "] Placement=[" +
                placementSource +
                "].");

            _spawnRoutine =
                null;
        }


        private static bool TryResolvePlacement(
            Scene scene,
            out Vector3 position,
            out Quaternion rotation,
            out string source)
        {
            position =
                Vector3.zero;

            rotation =
                Quaternion.identity;

            source =
                "<none>";

            List<Bounds> keeperBounds =
                new List<Bounds>();

            Transform room =
                FindSceneTransformByPath(
                    scene,
                    "Rooms/Room L1");

            for (int i = 0;
                 i < KeeperStatuePaths.Length;
                 i++)
            {
                Transform statue =
                    FindSceneTransformByPath(
                        scene,
                        KeeperStatuePaths[i]);

                Bounds visible;

                if (statue != null &&
                    TryGetVisibleBounds(
                        statue.gameObject,
                        out visible))
                {
                    keeperBounds.Add(
                        visible);
                }
            }

            Vector3 center =
                Vector3.zero;

            float highest =
                float.MinValue;

            float lowest =
                float.MaxValue;

            if (keeperBounds.Count >= 2)
            {
                for (int i = 0;
                     i < keeperBounds.Count;
                     i++)
                {
                    center +=
                        keeperBounds[i].center;

                    highest =
                        Mathf.Max(
                            highest,
                            keeperBounds[i].max.y);

                    lowest =
                        Mathf.Min(
                            lowest,
                            keeperBounds[i].min.y);
                }

                center /=
                    keeperBounds.Count;

                source =
                    "visible centre of " +
                    keeperBounds.Count +
                    "/4 keeper statues";
            }
            else
            {
                Bounds roomBounds;

                if (room == null ||
                    !TryGetVisibleBounds(
                        room.gameObject,
                        out roomBounds))
                {
                    return false;
                }

                center =
                    roomBounds.center;

                highest =
                    roomBounds.max.y;

                lowest =
                    roomBounds.min.y;

                source =
                    "Room L1 visible-bounds fallback";
            }

            float floorY =
                lowest;

            RaycastHit hit;

            // Start below the room ceiling rather than above the complete
            // room bounds. A top-down cast from outside an interior room can
            // otherwise snap the prop onto the ceiling instead of the floor.
            Vector3 rayStart =
                new Vector3(
                    center.x,
                    center.y + 2.5f,
                    center.z);

            if (Physics.Raycast(
                    rayStart,
                    Vector3.down,
                    out hit,
                    20f,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                floorY =
                    hit.point.y;
            }

            position =
                new Vector3(
                    center.x,
                    floorY + 0.03f,
                    center.z);

            if (room != null)
            {
                Vector3 forward =
                    room.forward;

                forward.y =
                    0f;

                if (forward.sqrMagnitude >
                    0.001f)
                {
                    rotation =
                        Quaternion.LookRotation(
                            -forward.normalized,
                            Vector3.up);
                }
            }

            return true;
        }


        private static Transform FindSceneTransformByPath(
            Scene scene,
            string path)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                string.IsNullOrEmpty(
                    path))
            {
                return null;
            }

            string[] parts =
                path.Split(
                    new char[]
                    {
                        '/'
                    },
                    StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
            {
                return null;
            }

            GameObject[] roots =
                scene.GetRootGameObjects();

            if (roots == null)
            {
                return null;
            }

            for (int i = 0;
                 i < roots.Length;
                 i++)
            {
                GameObject root =
                    roots[i];

                if (root == null ||
                    !string.Equals(
                        root.name,
                        parts[0],
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Transform current =
                    root.transform;

                for (int part = 1;
                     part < parts.Length;
                     part++)
                {
                    current =
                        FindDirectChild(
                            current,
                            parts[part]);

                    if (current == null)
                    {
                        break;
                    }
                }

                if (current != null)
                {
                    return current;
                }
            }

            return null;
        }


        private static Transform FindDirectChild(
            Transform parent,
            string childName)
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
                        childName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
        }


        private static bool TryGetVisibleBounds(
            GameObject root,
            out Bounds bounds)
        {
            bounds =
                new Bounds();

            if (root == null)
            {
                return false;
            }

            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(
                    true);

            bool initialized =
                false;

            if (renderers == null)
            {
                return false;
            }

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null ||
                    !renderer.enabled)
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds =
                        renderer.bounds;

                    initialized =
                        true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }

            return initialized;
        }


        private static GameObject BuildProceduralStele(
            Vector3 position,
            Quaternion rotation)
        {
            EnsureMaterials();

            GameObject root =
                new GameObject(
                    RuntimeObjectName);

            root.transform.position =
                position;

            root.transform.rotation =
                rotation;

            Transform visual =
                new GameObject(
                    "Visual").transform;

            visual.SetParent(
                root.transform,
                false);

            // Three-stage plinth.
            CreatePiece(
                visual,
                PrimitiveType.Cube,
                "Plinth_Lower",
                new Vector3(0f, 0.10f, 0f),
                new Vector3(2.20f, 0.20f, 1.05f),
                Vector3.zero,
                _darkStoneMaterial);

            CreatePiece(
                visual,
                PrimitiveType.Cube,
                "Plinth_Middle",
                new Vector3(0f, 0.26f, 0f),
                new Vector3(1.92f, 0.16f, 0.86f),
                Vector3.zero,
                _stoneMaterial);

            CreatePiece(
                visual,
                PrimitiveType.Cube,
                "Plinth_Upper",
                new Vector3(0f, 0.40f, 0f),
                new Vector3(1.72f, 0.12f, 0.70f),
                Vector3.zero,
                _darkStoneMaterial);

            // Main archive slab and framing stones.
            CreatePiece(
                visual,
                PrimitiveType.Cube,
                "ArchiveSlab",
                new Vector3(0f, 1.78f, 0f),
                new Vector3(1.56f, 2.64f, 0.34f),
                Vector3.zero,
                _stoneMaterial);

            CreatePiece(
                visual,
                PrimitiveType.Cube,
                "Frame_Left",
                new Vector3(-0.86f, 1.79f, 0f),
                new Vector3(0.16f, 2.72f, 0.46f),
                new Vector3(0f, 0f, -1.5f),
                _darkStoneMaterial);

            CreatePiece(
                visual,
                PrimitiveType.Cube,
                "Frame_Right",
                new Vector3(0.86f, 1.79f, 0f),
                new Vector3(0.16f, 2.72f, 0.46f),
                new Vector3(0f, 0f, 1.5f),
                _darkStoneMaterial);

            CreatePiece(
                visual,
                PrimitiveType.Cube,
                "Frame_Top",
                new Vector3(0f, 3.10f, 0f),
                new Vector3(1.90f, 0.22f, 0.48f),
                new Vector3(0f, 0f, -1.0f),
                _darkStoneMaterial);

            // Broken crown stones give the silhouette an intentionally ancient,
            // damaged profile rather than a clean rectangular monument.
            CreatePiece(
                visual,
                PrimitiveType.Cube,
                "Crown_Left",
                new Vector3(-0.43f, 3.25f, 0f),
                new Vector3(0.75f, 0.18f, 0.42f),
                new Vector3(0f, 0f, -6f),
                _stoneMaterial);

            CreatePiece(
                visual,
                PrimitiveType.Cube,
                "Crown_Right",
                new Vector3(0.46f, 3.22f, 0f),
                new Vector3(0.70f, 0.16f, 0.40f),
                new Vector3(0f, 0f, 8f),
                _stoneMaterial);

            // Four inset prison/keeper seals. The colours are deliberately
            // restrained accents against the stone rather than neon panels.
            CreateSigil(
                visual,
                "Seal_Blight",
                new Vector3(-0.43f, 2.28f, 0.22f),
                _blightMaterial);

            CreateSigil(
                visual,
                "Seal_Void",
                new Vector3(0.43f, 2.28f, 0.22f),
                _voidMaterial);

            CreateSigil(
                visual,
                "Seal_Death",
                new Vector3(-0.43f, 1.43f, 0.22f),
                _deathMaterial);

            CreateSigil(
                visual,
                "Seal_Infernal",
                new Vector3(0.43f, 1.43f, 0.22f),
                _infernalMaterial);

            // Deliberately chiselled inscription bands.
            CreatePiece(
                visual,
                PrimitiveType.Cube,
                "InscriptionBand_Upper",
                new Vector3(0f, 2.77f, 0.205f),
                new Vector3(1.22f, 0.09f, 0.05f),
                Vector3.zero,
                _engravingMaterial);

            CreatePiece(
                visual,
                PrimitiveType.Cube,
                "InscriptionBand_Lower",
                new Vector3(0f, 0.90f, 0.205f),
                new Vector3(1.18f, 0.09f, 0.05f),
                Vector3.zero,
                _engravingMaterial);

            // A fractured ward line splits the four seals, visually tying the
            // prop back to the four-wounds discovery from Quest 11.
            CreatePiece(
                visual,
                PrimitiveType.Cube,
                "WardCrack_Upper",
                new Vector3(-0.05f, 2.52f, 0.245f),
                new Vector3(0.045f, 0.78f, 0.045f),
                new Vector3(0f, 0f, -8f),
                _wardCrackMaterial);

            CreatePiece(
                visual,
                PrimitiveType.Cube,
                "WardCrack_Middle",
                new Vector3(0.04f, 1.82f, 0.245f),
                new Vector3(0.045f, 0.68f, 0.045f),
                new Vector3(0f, 0f, 9f),
                _wardCrackMaterial);

            CreatePiece(
                visual,
                PrimitiveType.Cube,
                "WardCrack_Lower",
                new Vector3(-0.02f, 1.16f, 0.245f),
                new Vector3(0.045f, 0.62f, 0.045f),
                new Vector3(0f, 0f, -5f),
                _wardCrackMaterial);

            return root;
        }


        private static void CreateSigil(
            Transform parent,
            string name,
            Vector3 localPosition,
            Material accentMaterial)
        {
            CreatePiece(
                parent,
                PrimitiveType.Cylinder,
                name + "_Backing",
                localPosition,
                new Vector3(0.34f, 0.055f, 0.34f),
                new Vector3(90f, 0f, 0f),
                _engravingMaterial);

            CreatePiece(
                parent,
                PrimitiveType.Cylinder,
                name + "_Glyph",
                localPosition +
                    new Vector3(0f, 0f, 0.035f),
                new Vector3(0.235f, 0.035f, 0.235f),
                new Vector3(90f, 0f, 0f),
                accentMaterial);

            // Simple crossed inset strokes keep the glyphs readable at a
            // distance without depending on textures or fonts.
            CreatePiece(
                parent,
                PrimitiveType.Cube,
                name + "_StrokeA",
                localPosition +
                    new Vector3(0f, 0f, 0.075f),
                new Vector3(0.30f, 0.045f, 0.035f),
                new Vector3(0f, 0f, 45f),
                _darkStoneMaterial);

            CreatePiece(
                parent,
                PrimitiveType.Cube,
                name + "_StrokeB",
                localPosition +
                    new Vector3(0f, 0f, 0.077f),
                new Vector3(0.30f, 0.045f, 0.035f),
                new Vector3(0f, 0f, -45f),
                _darkStoneMaterial);
        }


        private static GameObject CreatePiece(
            Transform parent,
            PrimitiveType primitive,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material)
        {
            GameObject piece =
                GameObject.CreatePrimitive(
                    primitive);

            if (piece == null)
            {
                return null;
            }

            piece.name =
                name;

            piece.transform.SetParent(
                parent,
                false);

            piece.transform.localPosition =
                localPosition;

            piece.transform.localScale =
                localScale;

            piece.transform.localRotation =
                Quaternion.Euler(
                    localEuler);

            Collider collider =
                piece.GetComponent<Collider>();

            if (collider != null)
            {
                UnityEngine.Object.Destroy(
                    collider);
            }

            Renderer renderer =
                piece.GetComponent<Renderer>();

            if (renderer != null &&
                material != null)
            {
                renderer.sharedMaterial =
                    material;
            }

            return piece;
        }


        private static void ConfigureInteraction(
            GameObject stele)
        {
            if (stele == null)
            {
                return;
            }

            BoxCollider collider =
                stele.GetComponent<BoxCollider>();

            if (collider == null)
            {
                collider =
                    stele.AddComponent<BoxCollider>();
            }

            Bounds visible;

            if (TryGetVisibleBounds(
                    stele,
                    out visible))
            {
                Vector3 localCenter =
                    stele.transform.InverseTransformPoint(
                        visible.center);

                Vector3 worldSize =
                    visible.size;

                Vector3 lossy =
                    stele.transform.lossyScale;

                collider.center =
                    localCenter;

                collider.size =
                    new Vector3(
                        SafeLocalSize(
                            worldSize.x,
                            lossy.x) + 0.30f,
                        SafeLocalSize(
                            worldSize.y,
                            lossy.y) + 0.20f,
                        Mathf.Max(
                            0.90f,
                            SafeLocalSize(
                                worldSize.z,
                                lossy.z) + 0.45f));
            }
            else
            {
                collider.center =
                    new Vector3(0f, 1.7f, 0f);

                collider.size =
                    new Vector3(2.2f, 3.5f, 1.0f);
            }

            collider.isTrigger =
                true;

            WardenArchiveSteleInteractable interactable =
                stele.GetComponent<WardenArchiveSteleInteractable>();

            if (interactable == null)
            {
                interactable =
                    stele.AddComponent<WardenArchiveSteleInteractable>();
            }

            interactable.Configure(
                collider);
        }


        private static float SafeLocalSize(
            float worldSize,
            float scale)
        {
            scale =
                Mathf.Abs(
                    scale);

            if (scale <
                0.0001f)
            {
                return worldSize;
            }

            return
                worldSize /
                scale;
        }


        private static void EnsureMaterials()
        {
            Shader shader =
                Shader.Find(
                    "Standard");

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Diffuse");
            }

            if (shader == null)
            {
                return;
            }

            if (_stoneMaterial == null)
            {
                _stoneMaterial =
                    CreateMaterial(
                        shader,
                        "FOTF_WardenArchive_Stone",
                        new Color(0.30f, 0.29f, 0.27f, 1f),
                        null);
            }

            if (_darkStoneMaterial == null)
            {
                _darkStoneMaterial =
                    CreateMaterial(
                        shader,
                        "FOTF_WardenArchive_DarkStone",
                        new Color(0.16f, 0.15f, 0.14f, 1f),
                        null);
            }

            if (_engravingMaterial == null)
            {
                _engravingMaterial =
                    CreateMaterial(
                        shader,
                        "FOTF_WardenArchive_Engraving",
                        new Color(0.075f, 0.07f, 0.065f, 1f),
                        null);
            }

            if (_blightMaterial == null)
            {
                _blightMaterial =
                    CreateMaterial(
                        shader,
                        "FOTF_WardenArchive_Blight",
                        new Color(0.36f, 0.58f, 0.13f, 1f),
                        new Color(0.06f, 0.12f, 0.015f, 1f));
            }

            if (_voidMaterial == null)
            {
                _voidMaterial =
                    CreateMaterial(
                        shader,
                        "FOTF_WardenArchive_Void",
                        new Color(0.34f, 0.13f, 0.55f, 1f),
                        new Color(0.06f, 0.015f, 0.12f, 1f));
            }

            if (_deathMaterial == null)
            {
                _deathMaterial =
                    CreateMaterial(
                        shader,
                        "FOTF_WardenArchive_Death",
                        new Color(0.34f, 0.55f, 0.58f, 1f),
                        new Color(0.02f, 0.08f, 0.09f, 1f));
            }

            if (_infernalMaterial == null)
            {
                _infernalMaterial =
                    CreateMaterial(
                        shader,
                        "FOTF_WardenArchive_Infernal",
                        new Color(0.70f, 0.20f, 0.06f, 1f),
                        new Color(0.16f, 0.025f, 0.005f, 1f));
            }

            if (_wardCrackMaterial == null)
            {
                _wardCrackMaterial =
                    CreateMaterial(
                        shader,
                        "FOTF_WardenArchive_WardCrack",
                        new Color(0.72f, 0.69f, 0.52f, 1f),
                        new Color(0.12f, 0.10f, 0.045f, 1f));
            }
        }


        private static Material CreateMaterial(
            Shader shader,
            string name,
            Color color,
            Color? emission)
        {
            Material material =
                new Material(
                    shader);

            material.name =
                name;

            material.color =
                color;

            if (emission.HasValue &&
                material.HasProperty(
                    "_EmissionColor"))
            {
                material.EnableKeyword(
                    "_EMISSION");

                material.SetColor(
                    "_EmissionColor",
                    emission.Value);
            }

            return material;
        }


        private static void DestroyStele()
        {
            if (_stele == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(
                _stele);

            _stele =
                null;
        }


        private static void DestroyMaterials()
        {
            Material[] materials =
            {
                _stoneMaterial,
                _darkStoneMaterial,
                _engravingMaterial,
                _blightMaterial,
                _voidMaterial,
                _deathMaterial,
                _infernalMaterial,
                _wardCrackMaterial
            };

            for (int i = 0;
                 i < materials.Length;
                 i++)
            {
                if (materials[i] != null)
                {
                    UnityEngine.Object.Destroy(
                        materials[i]);
                }
            }

            _stoneMaterial = null;
            _darkStoneMaterial = null;
            _engravingMaterial = null;
            _blightMaterial = null;
            _voidMaterial = null;
            _deathMaterial = null;
            _infernalMaterial = null;
            _wardCrackMaterial = null;
        }
    }


    internal sealed class WardenArchiveSteleInteractable :
        MonoBehaviour
    {
        private const float InteractionRange =
            7.0f;

        private Collider _collider;


        internal void Configure(
            Collider interactionCollider)
        {
            _collider =
                interactionCollider;

            if (_collider != null)
            {
                _collider.enabled =
                    true;
            }
        }


        private void OnMouseDown()
        {
            TryExamine();
        }


        private void TryExamine()
        {
            if (!FOTFExpansionGate.IsUnlocked)
            {
                return;
            }

            NamesThatWereBuriedQuest.Register();
            NamesThatWereBuriedQuest.EnsureChainState();

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
                    "You are too far away to read the Warden Archive Stele.",
                    "yellow");

                return;
            }

            if (NamesThatWereBuriedQuest.IsComplete())
            {
                AddSystemMessage(
                    "The surviving inscription names four prisoners: Lord of Blight, Shrouded Mother, Deathless One, and Demon King.",
                    "");

                return;
            }

            if (!NamesThatWereBuriedQuest.IsActive())
            {
                AddSystemMessage(
                    "Most of the ancient inscription has been deliberately chiselled away. Four sealed emblems remain around a fractured ward mark.",
                    "yellow");

                return;
            }

            if (NamesThatWereBuriedQuest.HasReadArchive())
            {
                AddSystemMessage(
                    "You have already copied the surviving names. Return to Archivist Sereth in Port Azure.",
                    "yellow");

                return;
            }

            if (!NamesThatWereBuriedQuest.TryMarkArchiveRead())
            {
                AddSystemMessage(
                    "The inscription is visible, but you cannot make a reliable record of it yet.",
                    "red");

                return;
            }

            AddSystemMessage(
                "You clear centuries of dust from the Warden Archive Stele. Four surviving titles emerge from the damaged inscription:",
                "yellow");

            AddSystemMessage(
                "Lord of Blight. Shrouded Mother. Deathless One. Demon King.",
                "yellow");

            AddSystemMessage(
                "Below them, one final line survives: FOUR KEEPERS. FOUR PRISONS. THE MARKS WERE BROKEN, NOT DESTROYED.",
                "yellow");

            AddSystemMessage(
                "Return to Archivist Sereth in Port Azure.",
                "yellow");
        }


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
    }
}
