using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 12 interaction layer for the four permanent keeper statues around
    /// the Ancient Warding Shrine in Port Azure.
    ///
    /// Each statue receives a deliberately unobtrusive inscription plate low on
    /// its outward/rear face. From the altar the plates are hidden behind the
    /// statues; the player must walk around the shrine and inspect all four.
    /// The inscription itself is represented as worn stone rather than glowing
    /// quest geometry so it reads as something the Wardens intentionally hid.
    /// </summary>
    internal static class Quest12KeeperStatueInscriptionRuntime
    {
        private const string LogPrefix =
            "[Quest12Statues] ";

        private const string InscriptionPrefix =
            "FOTF_Q12_HiddenInscription_";

        private const int PollFrames =
            20;

        private const float PlaqueWorldWidth =
            0.72f;

        private const float PlaqueWorldHeight =
            0.28f;

        private const float PlaqueWorldDepth =
            0.045f;

        // The plaque is intentionally sunk very slightly into the stone so it
        // reads as fixed to the statue rather than hovering behind it.
        private const float PlaqueSurfaceGap =
            0.012f;

        private static bool _initialized;
        private static Coroutine _monitorRoutine;
        private static GameObject _configuredShrine;

        private static readonly List<GameObject>
            _inscriptionObjects =
                new List<GameObject>();

        private static Material _inscriptionMaterial;


        private sealed class InscriptionDefinition
        {
            internal readonly string KeeperName;
            internal readonly string StatueObjectName;
            internal readonly string FallenTitle;

            internal InscriptionDefinition(
                string keeperName,
                string statueObjectName,
                string fallenTitle)
            {
                KeeperName = keeperName;
                StatueObjectName = statueObjectName;
                FallenTitle = fallenTitle;
            }
        }


        private static readonly InscriptionDefinition[]
            Definitions =
            {
                new InscriptionDefinition(
                    "Soluna",
                    "FOTF_Q6_Statue_Soluna",
                    "Shrouded Mother"),

                new InscriptionDefinition(
                    "Brax",
                    "FOTF_Q6_Statue_Brax",
                    "Demon King"),

                new InscriptionDefinition(
                    "Vithean",
                    "FOTF_Q6_Statue_Vithean",
                    "Deathless One"),

                new InscriptionDefinition(
                    "Fernalla",
                    "FOTF_Q6_Statue_Fernalla",
                    "Lord of Blight")
            };


        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            Scene active = SceneManager.GetActiveScene();

            if (FOTFExpansionGate.IsUnlocked &&
                IsAzure(active))
            {
                StartMonitor(active);
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

            _initialized = false;

            SceneManager.sceneLoaded -= OnSceneLoaded;

            StopMonitor();
            ClearInscriptions();
            DestroyMaterial();

            _configuredShrine = null;

            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "shutdown.");
        }


        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            // Quest 6 may load Reliquary additively to acquire the native
            // statue templates. That auxiliary scene is not a gameplay scene.
            if (mode == LoadSceneMode.Additive &&
                SceneManager.GetActiveScene() != scene)
            {
                return;
            }

            StopMonitor();
            ClearInscriptions();
            _configuredShrine = null;

            if (!_initialized ||
                !FOTFExpansionGate.IsUnlocked ||
                !IsAzure(scene))
            {
                return;
            }

            StartMonitor(scene);
        }


        private static bool IsAzure(
            Scene scene)
        {
            return
                scene.IsValid() &&
                scene.isLoaded &&
                string.Equals(
                    scene.name,
                    AncientWardingShrineRuntime.SceneName,
                    StringComparison.OrdinalIgnoreCase);
        }


        private static void StartMonitor(
            Scene scene)
        {
            if (_monitorRoutine != null)
            {
                return;
            }

            _monitorRoutine =
                CoroutineHost.Start(
                    Monitor(scene));
        }


        private static void StopMonitor()
        {
            if (_monitorRoutine == null)
            {
                return;
            }

            CoroutineHost.Stop(_monitorRoutine);
            _monitorRoutine = null;
        }


        private static IEnumerator Monitor(
            Scene scene)
        {
            while (_initialized &&
                   scene.IsValid() &&
                   scene.isLoaded &&
                   IsAzure(SceneManager.GetActiveScene()))
            {
                NamesThatWereBuriedQuest.Register();
                NamesThatWereBuriedQuest.EnsureChainState();

                GameObject shrine =
                    AncientWardingShrineRuntime.CurrentShrine;

                if (shrine != null &&
                    shrine != _configuredShrine)
                {
                    ClearInscriptions();
                    _configuredShrine = shrine;
                    TryConfigureShrine(shrine);
                }
                else if (shrine != null &&
                         _configuredShrine == shrine &&
                         CountLiveInscriptions() < Definitions.Length)
                {
                    // The Quest 6 statue population can complete a few frames
                    // after the shrine root itself appears. Retry cheaply until
                    // all four inscription targets exist.
                    TryConfigureShrine(shrine);
                }

                for (int frame = 0;
                     frame < PollFrames;
                     frame++)
                {
                    if (!_initialized ||
                        !scene.IsValid() ||
                        !scene.isLoaded)
                    {
                        _monitorRoutine = null;
                        yield break;
                    }

                    yield return null;
                }
            }

            _monitorRoutine = null;
        }


        private static bool TryConfigureShrine(
            GameObject shrine)
        {
            if (shrine == null)
            {
                return false;
            }

            int configured = 0;

            for (int i = 0;
                 i < Definitions.Length;
                 i++)
            {
                InscriptionDefinition definition =
                    Definitions[i];

                Transform statue =
                    FindChildRecursive(
                        shrine.transform,
                        definition.StatueObjectName);

                if (statue == null)
                {
                    continue;
                }

                Transform existing =
                    FindChildRecursive(
                        statue,
                        InscriptionPrefix +
                        definition.KeeperName);

                if (existing != null)
                {
                    TrackInscription(existing.gameObject);
                    configured++;
                    continue;
                }

                if (CreateHiddenInscription(
                        shrine.transform,
                        statue,
                        definition) != null)
                {
                    configured++;
                }
            }

            if (configured > 0)
            {
                Plugin.NativeLog.LogInfo(
                    LogPrefix +
                    "hidden inscription targets ready=[" +
                    configured +
                    "/" +
                    Definitions.Length +
                    "] QuestProgress=[" +
                    NamesThatWereBuriedQuest.GetRecoveredNameCount() +
                    "/4].");
            }

            return configured == Definitions.Length;
        }


        private static GameObject CreateHiddenInscription(
            Transform shrineRoot,
            Transform statue,
            InscriptionDefinition definition)
        {
            Bounds bounds;

            if (!TryGetVisibleBounds(
                    statue.gameObject,
                    out bounds))
            {
                Plugin.NativeLog.LogWarning(
                    LogPrefix +
                    "could not resolve visible bounds for [" +
                    definition.KeeperName +
                    "]; hidden inscription not created yet.");

                return null;
            }

            // Quest 6 explicitly rotates the statue root so its visible face
            // looks toward the altar. Because the native Reliquary statue root
            // carries a 180-degree visual compensation, statue.forward is the
            // best exact axis for the physical rear of the placed statue. Use
            // the shrine radial vector only to choose the outward sign.
            Vector3 radialOutward =
                statue.position -
                shrineRoot.position;

            radialOutward.y = 0f;

            Vector3 backDirection =
                statue.forward;

            backDirection.y = 0f;

            if (backDirection.sqrMagnitude < 0.0001f)
            {
                backDirection = radialOutward;
            }

            if (backDirection.sqrMagnitude < 0.0001f)
            {
                backDirection = Vector3.back;
            }

            backDirection.Normalize();

            if (radialOutward.sqrMagnitude > 0.0001f)
            {
                radialOutward.Normalize();

                if (Vector3.Dot(
                        backDirection,
                        radialOutward) < 0f)
                {
                    backDirection =
                        -backDirection;
                }
            }

            float lowHeight =
                Mathf.Clamp(
                    bounds.size.y * 0.16f,
                    0.30f,
                    0.55f);

            float targetWorldY =
                bounds.min.y +
                lowHeight;

            Vector3 centeredPlanePoint =
                new Vector3(
                    bounds.center.x,
                    targetWorldY,
                    bounds.center.z);

            float surfaceProjection;
            string surfaceSource;

            if (!TryResolveBackSurfaceProjection(
                    statue.gameObject,
                    bounds,
                    backDirection,
                    targetWorldY,
                    out surfaceProjection,
                    out surfaceSource))
            {
                surfaceProjection =
                    GetBoundsExtremeProjection(
                        bounds,
                        backDirection);

                surfaceSource =
                    "visible-bounds fallback";
            }

            float centeredProjection =
                Vector3.Dot(
                    centeredPlanePoint,
                    backDirection);

            Vector3 position =
                centeredPlanePoint +
                backDirection *
                (surfaceProjection -
                 centeredProjection +
                 PlaqueSurfaceGap);

            Quaternion rotation =
                Quaternion.LookRotation(
                    backDirection,
                    Vector3.up);

            GameObject plaque =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            if (plaque == null)
            {
                return null;
            }

            plaque.name =
                InscriptionPrefix +
                definition.KeeperName;

            plaque.transform.position = position;
            plaque.transform.rotation = rotation;
            plaque.transform.localScale =
                new Vector3(
                    PlaqueWorldWidth,
                    PlaqueWorldHeight,
                    PlaqueWorldDepth);

            // Preserve the corrected world-space surface fit while making the
            // plate a true child of the statue. If Quest 6 rebuilds a statue,
            // its Quest 12 plate naturally disappears with it.
            plaque.transform.SetParent(
                statue,
                true);

            Renderer renderer =
                plaque.GetComponent<Renderer>();

            if (renderer != null)
            {
                Material material =
                    GetInscriptionMaterial();

                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }
            }

            BoxCollider collider =
                plaque.GetComponent<BoxCollider>();

            if (collider != null)
            {
                // Keep the visible inscription subtle while making the click
                // target forgiving once the player has walked behind the
                // statue and found it.
                collider.size =
                    new Vector3(
                        1.5f,
                        2.0f,
                        4.0f);

                collider.enabled = true;
                collider.isTrigger = false;
            }

            Quest12KeeperStatueInscriptionInteractable interactable =
                plaque.GetComponent<Quest12KeeperStatueInscriptionInteractable>();

            if (interactable == null)
            {
                interactable =
                    plaque.AddComponent<Quest12KeeperStatueInscriptionInteractable>();
            }

            interactable.Configure(
                definition.KeeperName,
                definition.FallenTitle,
                collider);

            // Unity TextMesh's readable/front side faces local -Z at its
            // default rotation. The plate's +Z points away from the statue,
            // so rotate the text 180 degrees around Y and move it just beyond
            // the outer plate face. This prevents mirrored/backwards lettering.
            CreateInscriptionText(
                plaque.transform,
                definition,
                position +
                backDirection *
                (PlaqueWorldDepth * 0.5f + 0.004f),
                rotation *
                Quaternion.Euler(
                    0f,
                    180f,
                    0f));

            TrackInscription(plaque);

            Plugin.NativeLog.LogInfo(
                LogPrefix +
                "placed hidden inscription. Keeper=[" +
                definition.KeeperName +
                "] Fallen=[" +
                definition.FallenTitle +
                "] Position=[" +
                position +
                "] BackDirection=[" +
                backDirection +
                "] Surface=[" +
                surfaceSource +
                "] Side=[true statue rear-low].");

            return plaque;
        }


        private static bool TryResolveBackSurfaceProjection(
            GameObject statueRoot,
            Bounds visibleBounds,
            Vector3 backDirection,
            float targetWorldY,
            out float surfaceProjection,
            out string sourceDescription)
        {
            surfaceProjection =
                float.NegativeInfinity;

            sourceDescription =
                string.Empty;

            if (statueRoot == null ||
                backDirection.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            backDirection.Normalize();

            Vector3 rightDirection =
                Vector3.Cross(
                    Vector3.up,
                    backDirection);

            if (rightDirection.sqrMagnitude < 0.0001f)
            {
                rightDirection =
                    statueRoot.transform.right;
            }

            rightDirection.Normalize();

            Vector3 lateralCenter =
                new Vector3(
                    visibleBounds.center.x,
                    targetWorldY,
                    visibleBounds.center.z);

            float halfWidth =
                Mathf.Max(
                    PlaqueWorldWidth * 0.58f,
                    0.42f);

            float halfHeight =
                Mathf.Max(
                    PlaqueWorldHeight * 0.80f,
                    0.22f);

            bool found = false;

            MeshFilter[] meshFilters =
                statueRoot.GetComponentsInChildren<MeshFilter>(
                    true);

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

                if (renderer == null ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                // Native Unity meshes may be imported without Read/Write.
                // Accessing mesh.vertices on those meshes throws and breaks the
                // keeper plaque placement. Bounds fallback below still works.
                if (!filter.sharedMesh.isReadable)
                    continue;

                Vector3[] vertices;
                try
                {
                    vertices = filter.sharedMesh.vertices;
                }
                catch (UnityException)
                {
                    continue;
                }

                for (int vertexIndex = 0;
                     vertexIndex < vertices.Length;
                     vertexIndex++)
                {
                    Vector3 worldVertex =
                        filter.transform.TransformPoint(
                            vertices[vertexIndex]);

                    if (Mathf.Abs(
                            worldVertex.y -
                            targetWorldY) > halfHeight)
                    {
                        continue;
                    }

                    if (Mathf.Abs(
                            Vector3.Dot(
                                worldVertex -
                                lateralCenter,
                                rightDirection)) > halfWidth)
                    {
                        continue;
                    }

                    float projection =
                        Vector3.Dot(
                            worldVertex,
                            backDirection);

                    if (!found ||
                        projection > surfaceProjection)
                    {
                        surfaceProjection =
                            projection;

                        found = true;
                    }
                }
            }

            if (found)
            {
                sourceDescription =
                    "mesh-vertex rear surface";

                return true;
            }

            // Static Reliquary statues currently resolve through MeshFilters,
            // but retain a renderer-bounds fallback for future source models.
            // The caller places the plate slightly into this plane so even the
            // fallback still reads as attached rather than floating.
            surfaceProjection =
                GetBoundsExtremeProjection(
                    visibleBounds,
                    backDirection);

            sourceDescription =
                "visible-bounds rear surface";

            return true;
        }


        private static float GetBoundsExtremeProjection(
            Bounds bounds,
            Vector3 direction)
        {
            direction.Normalize();

            return
                Vector3.Dot(
                    bounds.center,
                    direction) +
                Mathf.Abs(direction.x) *
                    bounds.extents.x +
                Mathf.Abs(direction.y) *
                    bounds.extents.y +
                Mathf.Abs(direction.z) *
                    bounds.extents.z;
        }

        private static void CreateInscriptionText(
            Transform inscriptionRoot,
            InscriptionDefinition definition,
            Vector3 worldPosition,
            Quaternion worldRotation)
        {
            if (inscriptionRoot == null ||
                definition == null)
            {
                return;
            }

            try
            {
                GameObject textObject =
                    new GameObject(
                        InscriptionPrefix +
                        definition.KeeperName +
                        "_Text");

                textObject.transform.position = worldPosition;
                textObject.transform.rotation = worldRotation;
                textObject.transform.localScale = Vector3.one;
                textObject.transform.SetParent(
                    inscriptionRoot,
                    true);

                TextMesh text =
                    textObject.AddComponent<TextMesh>();

                Font font =
                    Resources.GetBuiltinResource<Font>(
                        "Arial.ttf");

                if (font != null)
                {
                    text.font = font;
                }

                text.text =
                    definition.FallenTitle.ToUpperInvariant();

                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 32;
                text.characterSize = 0.018f;
                text.color =
                    new Color(
                        0.30f,
                        0.28f,
                        0.24f,
                        1f);

                MeshRenderer renderer =
                    textObject.GetComponent<MeshRenderer>();

                if (renderer != null &&
                    font != null &&
                    font.material != null)
                {
                    renderer.sharedMaterial =
                        font.material;
                }

            }
            catch (Exception exception)
            {
                // Text is decorative only. The interaction collider and quest
                // message remain authoritative if the built-in font is not
                // available on a particular client.
                Plugin.NativeLog.LogWarning(
                    LogPrefix +
                    "failed creating decorative inscription text for [" +
                    definition.KeeperName +
                    "]: " +
                    exception.Message);
            }
        }


        private static Material GetInscriptionMaterial()
        {
            if (_inscriptionMaterial != null)
            {
                return _inscriptionMaterial;
            }

            Shader shader =
                Shader.Find("Standard");

            if (shader == null)
            {
                return null;
            }

            _inscriptionMaterial =
                new Material(shader);

            _inscriptionMaterial.name =
                "FOTF_Q12_HiddenInscription_Material";

            // Deliberately low contrast: this is supposed to look like an old
            // inset/carved stone face, not a glowing quest marker.
            _inscriptionMaterial.color =
                new Color(
                    0.17f,
                    0.16f,
                    0.14f,
                    1f);

            return _inscriptionMaterial;
        }


        private static void DestroyMaterial()
        {
            if (_inscriptionMaterial == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(
                _inscriptionMaterial);

            _inscriptionMaterial = null;
        }


        private static void TrackInscription(
            GameObject inscription)
        {
            if (inscription == null ||
                _inscriptionObjects.Contains(inscription))
            {
                return;
            }

            _inscriptionObjects.Add(inscription);
        }


        private static int CountLiveInscriptions()
        {
            PruneInscriptionList();
            return _inscriptionObjects.Count;
        }


        private static void ClearInscriptions()
        {
            for (int i =
                    _inscriptionObjects.Count - 1;
                 i >= 0;
                 i--)
            {
                GameObject inscription =
                    _inscriptionObjects[i];

                if (inscription != null)
                {
                    UnityEngine.Object.Destroy(
                        inscription);
                }
            }

            _inscriptionObjects.Clear();
        }


        private static void PruneInscriptionList()
        {
            for (int i =
                    _inscriptionObjects.Count - 1;
                 i >= 0;
                 i--)
            {
                if (_inscriptionObjects[i] == null)
                {
                    _inscriptionObjects.RemoveAt(i);
                }
            }
        }


        private static bool TryGetVisibleBounds(
            GameObject root,
            out Bounds bounds)
        {
            bounds = new Bounds();

            if (root == null)
            {
                return false;
            }

            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(
                    true);

            bool found = false;

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

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }

            return found;
        }


        private static Transform FindChildRecursive(
            Transform root,
            string targetName)
        {
            if (root == null ||
                string.IsNullOrEmpty(targetName))
            {
                return null;
            }

            if (string.Equals(
                    root.name,
                    targetName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int i = 0;
                 i < root.childCount;
                 i++)
            {
                Transform result =
                    FindChildRecursive(
                        root.GetChild(i),
                        targetName);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }


    internal sealed class Quest12KeeperStatueInscriptionInteractable :
        MonoBehaviour
    {
        private const float InteractionRange =
            5.5f;

        private string _keeperName;
        private string _fallenTitle;
        private Collider _collider;


        internal void Configure(
            string keeperName,
            string fallenTitle,
            Collider interactionCollider)
        {
            _keeperName = keeperName ?? string.Empty;
            _fallenTitle = fallenTitle ?? string.Empty;
            _collider = interactionCollider;

            if (_collider != null)
            {
                _collider.enabled = true;
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

            if (distance > InteractionRange)
            {
                AddSystemMessage(
                    "You are too far away to examine the hidden inscription.",
                    "yellow");

                return;
            }

            NamesThatWereBuriedQuest.Register();
            NamesThatWereBuriedQuest.EnsureChainState();

            if (NamesThatWereBuriedQuest.IsComplete())
            {
                AddInscriptionMessage();
                return;
            }

            if (!NamesThatWereBuriedQuest.IsActive())
            {
                AddSystemMessage(
                    "A shallow inscription has been cut into the rear lower face of " +
                    _keeperName +
                    "'s statue. From the altar it would be almost impossible to notice.",
                    "yellow");

                return;
            }

            bool alreadyRead =
                NamesThatWereBuriedQuest.HasInspectedStatue(
                    _keeperName);

            if (!alreadyRead &&
                !NamesThatWereBuriedQuest.TryMarkStatueInscription(
                    _keeperName))
            {
                AddSystemMessage(
                    "You can make out the carving, but cannot preserve the inscription in your notes.",
                    "red");

                return;
            }

            AddInscriptionMessage();

            int recovered =
                NamesThatWereBuriedQuest.GetRecoveredNameCount();

            if (recovered >= 4)
            {
                AddSystemMessage(
                    "All four hidden names have been copied. Return to Archivist Sereth in Port Azure.",
                    "yellow");
            }
            else if (!alreadyRead)
            {
                AddSystemMessage(
                    "Hidden inscriptions recovered: " +
                    recovered +
                    "/4.",
                    "yellow");
            }
        }


        private void AddInscriptionMessage()
        {
            AddSystemMessage(
                "Behind " +
                _keeperName +
                "'s statue, low against the outward face where it cannot be seen from the altar, a worn inscription reads:",
                "yellow");

            AddSystemMessage(
                _fallenTitle.ToUpperInvariant(),
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
                        color ?? string.Empty));
            }
            catch
            {
            }
        }
    }
}
