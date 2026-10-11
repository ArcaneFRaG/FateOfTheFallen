using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 2 base-world investigation.
    ///
    /// While Signs of Decay is active, a small patch of blighted vegetation
    /// appears in Loomingwood. Approaching it collects the Unnatural Spores
    /// evidence item.
    /// </summary>
    internal static class SignsOfDecayRuntime
    {
        internal const string SceneName =
            "Loomingwood";

        // Initial authored point for the first disturbance. We still sample the
        // live NavMesh before placing the cluster, so small terrain differences
        // do not strand it below/above the walkable ground.
        private static readonly Vector3 DesiredSitePosition =
            new Vector3(
                360f,
                29.5f,
                430f);

        private const float NavMeshSampleRadius =
            18f;

        private const float CollectRadius =
            3.25f;

        private const float CheckInterval =
            0.35f;

        private static GameObject _host;
        private static SignsOfDecayController _controller;


        internal static void Initialize()
        {
            if (_host != null)
            {
                return;
            }

            _host =
                new GameObject(
                    "FOTF_SignsOfDecayRuntime");

            UnityEngine.Object
                .DontDestroyOnLoad(
                    _host);

            _controller =
                _host.AddComponent<
                    SignsOfDecayController>();

            _controller.Initialize(
                DesiredSitePosition,
                NavMeshSampleRadius,
                CollectRadius,
                CheckInterval);
        }


        internal static void Shutdown()
        {
            if (_host != null)
            {
                UnityEngine.Object.Destroy(
                    _host);
            }

            _host =
                null;

            _controller =
                null;
        }
    }


    internal sealed class SignsOfDecayController :
        MonoBehaviour
    {
        private Vector3 _desiredPosition;
        private float _navMeshSampleRadius;
        private float _collectRadius;
        private float _checkInterval;

        private float _nextCheck;
        private GameObject _site;

        private Material _blightMaterial;
        private Material _deadGrowthMaterial;

        private bool _announcedSite;


        internal void Initialize(
            Vector3 desiredPosition,
            float navMeshSampleRadius,
            float collectRadius,
            float checkInterval)
        {
            _desiredPosition =
                desiredPosition;

            _navMeshSampleRadius =
                navMeshSampleRadius;

            _collectRadius =
                collectRadius;

            _checkInterval =
                checkInterval;
        }


        private void OnDestroy()
        {
            DestroySite();

            if (_blightMaterial != null)
            {
                UnityEngine.Object.Destroy(
                    _blightMaterial);

                _blightMaterial =
                    null;
            }

            if (_deadGrowthMaterial != null)
            {
                UnityEngine.Object.Destroy(
                    _deadGrowthMaterial);

                _deadGrowthMaterial =
                    null;
            }
        }


        private void Update()
        {
            if (Time.time <
                _nextCheck)
            {
                return;
            }

            _nextCheck =
                Time.time +
                _checkInterval;

            if (!FOTFExpansionGate.IsUnlocked)
            {
                DestroySite();
                return;
            }

            Scene scene =
                SceneManager.GetActiveScene();

            if (!scene.IsValid() ||
                !scene.isLoaded ||
                !string.Equals(
                    scene.name,
                    SignsOfDecayRuntime.SceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                DestroySite();
                _announcedSite =
                    false;

                return;
            }

            if (!FateOfTheFallenQuests
                    .IsSignsOfDecayActive() ||
                FateOfTheFallenQuests
                    .IsSignsOfDecayComplete())
            {
                DestroySite();
                return;
            }

            if (SignsOfDecayContent
                .PlayerHasSample())
            {
                DestroySite();
                return;
            }

            if (_site == null)
            {
                SpawnSite(
                    scene);
            }

            TryCollect();
        }


        private void SpawnSite(
            Scene scene)
        {
            Vector3 sitePosition =
                ResolveSitePosition();

            _site =
                new GameObject(
                    "FOTF_Q2_BlightedVegetation");

            _site.transform.position =
                sitePosition;

            SceneManager.MoveGameObjectToScene(
                _site,
                scene);

            BuildVisual(
                _site.transform);

            if (!_announcedSite)
            {
                _announcedSite =
                    true;

                AddSystemMessage(
                    "A patch of vegetation in Loomingwood has collapsed into an unnatural grey-green rot.",
                    "yellow");
            }

            Plugin.NativeLog.LogInfo(
                "Signs of Decay: investigation site spawned in Loomingwood at " +
                sitePosition +
                ".");
        }


        private Vector3 ResolveSitePosition()
        {
            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                    _desiredPosition,
                    out hit,
                    _navMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                return
                    hit.position;
            }

            Character player =
                GameData.PlayerControl != null
                    ? GameData.PlayerControl.Myself
                    : null;

            if (player != null)
            {
                Vector3 fallback =
                    player.transform.position +
                    (player.transform.forward *
                     12f);

                if (NavMesh.SamplePosition(
                        fallback,
                        out hit,
                        12f,
                        NavMesh.AllAreas))
                {
                    Plugin.NativeLog.LogWarning(
                        "Signs of Decay: authored Loomingwood site was not on the NavMesh; using a nearby player-relative fallback.");

                    return
                        hit.position;
                }

                return
                    player.transform.position;
            }

            return
                _desiredPosition;
        }


        private void TryCollect()
        {
            if (_site == null)
            {
                return;
            }

            Character player =
                GameData.PlayerControl != null
                    ? GameData.PlayerControl.Myself
                    : null;

            if (player == null)
            {
                return;
            }

            float distance =
                Vector3.Distance(
                    player.transform.position,
                    _site.transform.position);

            if (distance >
                _collectRadius)
            {
                return;
            }

            if (!SignsOfDecayContent
                .TryGiveSample(
                    true))
            {
                return;
            }

            Plugin.NativeLog.LogInfo(
                "Signs of Decay: player collected Unnatural Spores.");

            DestroySite();
        }


        private void BuildVisual(
            Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            EnsureMaterials();

            // Flattened dead-growth chunks.
            CreatePiece(
                parent,
                PrimitiveType.Cube,
                "DeadGrowth_A",
                new Vector3(-0.8f, 0.10f, 0.2f),
                new Vector3(1.2f, 0.12f, 0.55f),
                new Vector3(0f, 17f, 7f),
                _deadGrowthMaterial);

            CreatePiece(
                parent,
                PrimitiveType.Cube,
                "DeadGrowth_B",
                new Vector3(0.55f, 0.12f, -0.35f),
                new Vector3(0.95f, 0.10f, 0.48f),
                new Vector3(0f, -31f, -5f),
                _deadGrowthMaterial);

            CreatePiece(
                parent,
                PrimitiveType.Cube,
                "DeadGrowth_C",
                new Vector3(0.2f, 0.08f, 0.65f),
                new Vector3(0.72f, 0.09f, 0.38f),
                new Vector3(0f, 53f, 3f),
                _deadGrowthMaterial);

            // Small irregular spore-bearing growths.
            CreatePiece(
                parent,
                PrimitiveType.Sphere,
                "SporeGrowth_A",
                new Vector3(-0.35f, 0.34f, 0.10f),
                new Vector3(0.34f, 0.48f, 0.34f),
                new Vector3(8f, 0f, 12f),
                _blightMaterial);

            CreatePiece(
                parent,
                PrimitiveType.Sphere,
                "SporeGrowth_B",
                new Vector3(0.32f, 0.27f, -0.18f),
                new Vector3(0.26f, 0.37f, 0.26f),
                new Vector3(-11f, 0f, 5f),
                _blightMaterial);

            CreatePiece(
                parent,
                PrimitiveType.Sphere,
                "SporeGrowth_C",
                new Vector3(0.08f, 0.22f, 0.42f),
                new Vector3(0.22f, 0.31f, 0.22f),
                new Vector3(5f, 0f, -9f),
                _blightMaterial);
        }


        private void EnsureMaterials()
        {
            if (_blightMaterial != null &&
                _deadGrowthMaterial != null)
            {
                return;
            }

            Shader shader =
                Shader.Find(
                    "Standard");

            if (shader == null)
            {
                return;
            }

            if (_blightMaterial == null)
            {
                _blightMaterial =
                    new Material(
                        shader);

                _blightMaterial.name =
                    "FOTF_Q2_BlightGrowth";

                _blightMaterial.color =
                    new Color(
                        0.24f,
                        0.34f,
                        0.12f,
                        1f);
            }

            if (_deadGrowthMaterial == null)
            {
                _deadGrowthMaterial =
                    new Material(
                        shader);

                _deadGrowthMaterial.name =
                    "FOTF_Q2_DeadGrowth";

                _deadGrowthMaterial.color =
                    new Color(
                        0.20f,
                        0.18f,
                        0.14f,
                        1f);
            }
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

            return
                piece;
        }


        private void DestroySite()
        {
            if (_site == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(
                _site);

            _site =
                null;
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
