using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 3 harvestable world prop.
    ///
    /// Soluna's Landing (native scene: Soluna) loads -> VeiledDisturbance
    /// prefab spawns -> player clicks the floating void orb -> canonical
    /// Veiled Residue [1701] is added to inventory.
    /// </summary>
    internal static class VeiledDisturbanceRuntime
    {
        internal const string SceneName =
            "Soluna";

        internal const string BundleName =
            "fotf_questprops";

        internal const string PrefabName =
            "VeiledDisturbance";

        private const string RuntimeObjectName =
            "FOTF_Quest03_VeiledDisturbance";

        private static readonly Vector3 RequestedPosition =
            new Vector3(
                567.49f,
                82.40f,
                372.59f);

        private static bool _initialized;
        private static GameObject _disturbance;
        private static Coroutine _spawnRoutine;

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

            if (IsSolunasLanding(
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

            DestroyDisturbance();
        }

        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            DestroyDisturbance();

            if (!IsSolunasLanding(
                    scene))
            {
                return;
            }

            StartSpawn(
                scene);
        }

        private static bool IsSolunasLanding(
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
                _disturbance != null)
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
            for (int frame = 0;
                 frame < 120;
                 frame++)
            {
                if (!IsSolunasLanding(
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

            if (!FateOfTheFallenQuests.IsWhereLightFailsActive() ||
                FateOfTheFallenQuests.IsWhereLightFailsComplete() ||
                WhereLightFailsContent.PlayerHasResidue())
            {
                _spawnRoutine =
                    null;

                yield break;
            }

            Item residue =
                VeiledResidue.RegisterItem(
                    GameData.ItemDB);

            if (residue == null)
            {
                Plugin.NativeLog.LogError(
                    "Quest 3 disturbance: Veiled Residue [1701] was unavailable; spawn aborted.");

                _spawnRoutine =
                    null;

                yield break;
            }

            WhereLightFailsContent.LinkQuestRequirement();

            GameObject prefab =
                AssetBundleRegistry.GetPrefab(
                    BundleName,
                    PrefabName);

            if (prefab == null)
            {
                Plugin.NativeLog.LogError(
                    "Quest 3 disturbance: prefab [" +
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

            GameObject disturbance =
                null;

            try
            {
                // Preserve the prefab's authored Blender/FBX orientation.
                disturbance =
                    UnityEngine.Object.Instantiate(
                        prefab,
                        RequestedPosition,
                        prefab.transform.rotation);

                if (disturbance == null)
                {
                    Plugin.NativeLog.LogError(
                        "Quest 3 disturbance: Unity returned null while instantiating the prefab.");

                    _spawnRoutine =
                        null;

                    yield break;
                }

                disturbance.name =
                    RuntimeObjectName;

                SceneManager.MoveGameObjectToScene(
                    disturbance,
                    scene);

                ConfigureOrbClickTrigger(
                    disturbance);

                VeiledDisturbanceInteractable interactable =
                    disturbance.GetComponent<VeiledDisturbanceInteractable>();

                if (interactable == null)
                {
                    interactable =
                        disturbance.AddComponent<VeiledDisturbanceInteractable>();
                }

                interactable.Configure(
                    residue);

                _disturbance =
                    disturbance;

                Plugin.NativeLog.LogInfo(
                    "Quest 3 disturbance: spawned [" +
                    RuntimeObjectName +
                    "] in Soluna's Landing at [" +
                    RequestedPosition +
                    "].");
            }
            catch (Exception exception)
            {
                if (disturbance != null)
                {
                    UnityEngine.Object.Destroy(
                        disturbance);
                }

                Plugin.NativeLog.LogError(
                    "Quest 3 disturbance: spawn failed: " +
                    exception);
            }

            _spawnRoutine =
                null;
        }

        private static void ConfigureOrbClickTrigger(
            GameObject disturbance)
        {
            if (disturbance == null)
            {
                return;
            }

            Collider[] authoredColliders =
                disturbance.GetComponentsInChildren<Collider>(
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

            Transform harvestRoot =
                FindChildRecursive(
                    disturbance.transform,
                    "HarvestableResidue");

            Transform boundsRoot =
                harvestRoot ??
                disturbance.transform;

            Renderer[] renderers =
                boundsRoot.GetComponentsInChildren<Renderer>(
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
                    !renderer.enabled)
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
                        disturbance.transform.InverseTransformPoint(
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
                disturbance.transform.lossyScale;

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
                disturbance.GetComponent<BoxCollider>();

            if (collider == null)
            {
                collider =
                    disturbance.AddComponent<BoxCollider>();
            }

            if (hasBounds)
            {
                collider.center =
                    (localMin + localMax) *
                    0.5f;

                Vector3 localPadding =
                    new Vector3(
                        0.20f / scaleX,
                        0.20f / scaleY,
                        0.20f / scaleZ);

                collider.size =
                    (localMax - localMin) +
                    localPadding;
            }
            else
            {
                // Defensive fallback centered on the authored orb height.
                collider.center =
                    new Vector3(
                        0f,
                        1.1f / scaleY,
                        0f);

                collider.size =
                    new Vector3(
                        1.5f / scaleX,
                        1.5f / scaleY,
                        1.5f / scaleZ);
            }

            collider.isTrigger =
                true;

            collider.enabled =
                true;
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

        private static void DestroyDisturbance()
        {
            if (_disturbance == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(
                _disturbance);

            _disturbance =
                null;
        }
    }


    internal sealed class VeiledDisturbanceInteractable :
        MonoBehaviour
    {
        private const float InteractionRange =
            6.0f;

        private Item _residue;
        private bool _harvested;

        internal void Configure(
            Item residue)
        {
            _residue =
                residue;
        }

        private void OnMouseDown()
        {
            TryHarvest();
        }

        private void TryHarvest()
        {
            if (_harvested)
            {
                AddSystemMessage(
                    "The heart of the disturbance is empty.",
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
                    "You are too far away to reach the floating orb.",
                    "yellow");

                return;
            }

            if (!QuestThreeAllowsHarvest())
            {
                AddSystemMessage(
                    "The darkness recoils from your touch. You have no reason to disturb it yet.",
                    "yellow");

                return;
            }

            Item residue =
                _residue ??
                VeiledResidue.GetItem();

            if (residue == null)
            {
                Plugin.NativeLog.LogError(
                    "Quest 3 disturbance: harvest failed because Veiled Residue [1701] could not be resolved.");

                return;
            }

            if (!WhereLightFailsContent.TryGiveResidue(
                    false))
            {
                AddSystemMessage(
                    "You cannot carry the Veiled Residue.",
                    "yellow");

                return;
            }

            _harvested =
                true;

            BoxCollider collider =
                GetComponent<BoxCollider>();

            if (collider != null)
            {
                collider.enabled =
                    false;
            }

            Transform harvestVisual =
                FindChildRecursive(
                    transform,
                    "HarvestableResidue");

            if (harvestVisual != null)
            {
                harvestVisual.gameObject.SetActive(
                    false);
            }

            AddSystemMessage(
                "You draw the floating void orb free and recover the Veiled Residue.",
                "green");

            Plugin.NativeLog.LogInfo(
                "Quest 3 disturbance: player harvested Veiled Residue [1701].");
        }

        private static bool QuestThreeAllowsHarvest()
        {
            return
                FateOfTheFallenQuests.IsWhereLightFailsActive() &&
                !FateOfTheFallenQuests.IsWhereLightFailsComplete();
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
            }
        }
    }
}
