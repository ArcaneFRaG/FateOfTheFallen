using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// First custom-zone proof of concept.
    ///
    /// Loads FallenReach.unity from the external fotf_zones AssetBundle,
    /// places the persistent Erenshor player at FOTF_PlayerSpawn, and
    /// supports returning to the exact native scene/position used to enter.
    ///
    /// This deliberately does NOT create the physical entrance/exit yet.
    /// The first test is driven by chat commands so scene loading and player
    /// persistence can be validated independently from trigger logic.
    /// </summary>
    internal static class CustomZoneLoader
    {
        internal const string FallenReachBundleName =
            "fotf_zones";

        internal const string FallenReachSceneName =
            "FallenReach";

        internal const string FallenReachPlayerSpawnName =
            "FOTF_PlayerSpawn";

        private static bool _initialized;
        private static bool _transitionInProgress;

        private static string _returnSceneName;
        private static Vector3 _returnPosition;
        private static Quaternion _returnRotation;
        private static bool _hasReturnPoint;

        private static bool _pendingFallenReachPlacement;
        private static bool _pendingReturnPlacement;


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

        }


        internal static void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            SceneManager.sceneLoaded -=
                OnSceneLoaded;

            _initialized =
                false;

            _transitionInProgress =
                false;

            _pendingFallenReachPlacement =
                false;

            _pendingReturnPlacement =
                false;
        }


        // ============================================================
        // ENTER FALLEN REACH
        // ============================================================

        internal static bool EnterFallenReach()
        {
            if (_transitionInProgress)
            {
                LogSystemMessage(
                    "A zone transition is already in progress.",
                    "yellow");

                return false;
            }

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (IsFallenReach(
                    activeScene))
            {
                LogSystemMessage(
                    "You are already in Fallen Reach.",
                    "yellow");

                return false;
            }

            string scenePath =
                ResolveFallenReachScenePath();

            if (string.IsNullOrEmpty(
                    scenePath))
            {
                LogSystemMessage(
                    "Fallen Reach could not be loaded. Check the FOTF log.",
                    "yellow");

                return false;
            }

            ZoneSceneDiagnostic.CaptureNativeBaseline(
                activeScene,
                "before /fotfzone transition");

            BlightedThrallRuntime.CaptureNativeTemplateBeforeZoneChange();

            CaptureReturnPoint(
                activeScene);

            _transitionInProgress =
                true;

            _pendingFallenReachPlacement =
                true;

            _pendingReturnPlacement =
                false;


            try
            {
                AsyncOperation operation =
                    SceneManager.LoadSceneAsync(
                        scenePath,
                        LoadSceneMode.Single);

                if (operation == null)
                {
                    _transitionInProgress =
                        false;

                    _pendingFallenReachPlacement =
                        false;

                    Plugin.NativeLog.LogError(
                        "CustomZoneLoader: SceneManager.LoadSceneAsync returned null for [" +
                        scenePath +
                        "].");

                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                _transitionInProgress =
                    false;

                _pendingFallenReachPlacement =
                    false;

                Plugin.NativeLog.LogError(
                    "CustomZoneLoader: failed to begin Fallen Reach load: " +
                    exception);

                return false;
            }
        }


        // ============================================================
        // RETURN TO NATIVE ZONE
        // ============================================================

        internal static bool ReturnToPreviousZone()
        {
            if (_transitionInProgress)
            {
                LogSystemMessage(
                    "A zone transition is already in progress.",
                    "yellow");

                return false;
            }

            if (!_hasReturnPoint ||
                string.IsNullOrEmpty(
                    _returnSceneName))
            {
                LogSystemMessage(
                    "No Fate of the Fallen return point has been recorded.",
                    "yellow");

                return false;
            }

            if (!IsFallenReach(
                    SceneManager.GetActiveScene()))
            {
                LogSystemMessage(
                    "The FOTF return command is only needed while inside Fallen Reach.",
                    "yellow");

                return false;
            }

            _transitionInProgress =
                true;

            _pendingReturnPlacement =
                true;

            _pendingFallenReachPlacement =
                false;


            try
            {
                AsyncOperation operation =
                    SceneManager.LoadSceneAsync(
                        _returnSceneName,
                        LoadSceneMode.Single);

                if (operation == null)
                {
                    _transitionInProgress =
                        false;

                    _pendingReturnPlacement =
                        false;

                    Plugin.NativeLog.LogError(
                        "CustomZoneLoader: SceneManager.LoadSceneAsync returned null while returning to [" +
                        _returnSceneName +
                        "].");

                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                _transitionInProgress =
                    false;

                _pendingReturnPlacement =
                    false;

                Plugin.NativeLog.LogError(
                    "CustomZoneLoader: failed to begin return load: " +
                    exception);

                return false;
            }
        }


        // ============================================================
        // SCENE RESOLUTION
        // ============================================================

        private static string ResolveFallenReachScenePath()
        {
            AssetBundle bundle =
                AssetBundleRegistry.GetBundle(
                    FallenReachBundleName);

            if (bundle == null)
            {
                return null;
            }

            string[] scenePaths;

            try
            {
                scenePaths =
                    bundle.GetAllScenePaths();
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "CustomZoneLoader: failed to enumerate scenes in bundle [" +
                    FallenReachBundleName +
                    "]: " +
                    exception);

                return null;
            }

            if (scenePaths == null ||
                scenePaths.Length == 0)
            {
                Plugin.NativeLog.LogError(
                    "CustomZoneLoader: bundle [" +
                    FallenReachBundleName +
                    "] contains no scenes.");

                return null;
            }

            for (int i = 0;
                 i < scenePaths.Length;
                 i++)
            {
                string path =
                    scenePaths[i];

                if (string.IsNullOrEmpty(
                        path))
                {
                    continue;
                }

                string fileName =
                    Path.GetFileNameWithoutExtension(
                        path);

                if (string.Equals(
                        fileName,
                        FallenReachSceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }

            Plugin.NativeLog.LogError(
                "CustomZoneLoader: scene [" +
                FallenReachSceneName +
                "] was not found in bundle [" +
                FallenReachBundleName +
                "]. Available scenes: " +
                string.Join(
                    ", ",
                    scenePaths));

            return null;
        }


        // ============================================================
        // RETURN POINT
        // ============================================================

        private static void CaptureReturnPoint(
            Scene activeScene)
        {
            _returnSceneName =
                activeScene.name;

            PlayerControl player =
                GameData.PlayerControl;

            if (player != null)
            {
                _returnPosition =
                    player.transform.position;

                _returnRotation =
                    player.transform.rotation;

                _hasReturnPoint =
                    true;

                return;
            }

            _returnPosition =
                Vector3.zero;

            _returnRotation =
                Quaternion.identity;

            _hasReturnPoint =
                !string.IsNullOrEmpty(
                    _returnSceneName);

            Plugin.NativeLog.LogWarning(
                "CustomZoneLoader: PlayerControl was unavailable while capturing the return point. " +
                "The native scene name was retained, but the return position could not be captured.");
        }


        // ============================================================
        // SCENE LOADED
        // ============================================================

        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            try
            {
                if (IsFallenReach(
                        scene))
                {
                    _transitionInProgress =
                        false;

                    if (_pendingFallenReachPlacement)
                    {
                        _pendingFallenReachPlacement =
                            false;

                        CoroutineHost.Start(
                            PlacePlayerAtFallenReachSpawn());
                    }

                    return;
                }

                if (_pendingReturnPlacement &&
                    string.Equals(
                        scene.name,
                        _returnSceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _transitionInProgress =
                        false;

                    _pendingReturnPlacement =
                        false;

                    CoroutineHost.Start(
                        RestorePlayerToReturnPoint());
                }
            }
            catch (Exception exception)
            {
                _transitionInProgress =
                    false;

                Plugin.NativeLog.LogError(
                    "CustomZoneLoader: scene-loaded handling failed: " +
                    exception);
            }
        }


        // ============================================================
        // FALLEN REACH SPAWN
        // ============================================================

        private static IEnumerator PlacePlayerAtFallenReachSpawn()
        {
            const int maximumFrames =
                600;

            const int stabilizationFrames =
                45;

            Scene fallenReachScene =
                SceneManager.GetActiveScene();

            for (int frame = 0;
                 frame < maximumFrames;
                 frame++)
            {
                if (!IsFallenReach(
                        fallenReachScene))
                {
                    fallenReachScene =
                        SceneManager.GetActiveScene();
                }

                PlayerControl player =
                    GameData.PlayerControl;

                // Do not use GameObject.Find here. It only sees active
                // objects and is not scoped to Fallen Reach. Our marker
                // hierarchy is authoring data and may be inactive while
                // still being perfectly valid. Search the loaded scene
                // directly and include inactive children.
                GameObject marker =
                    FindSceneObject(
                        fallenReachScene,
                        FallenReachPlayerSpawnName);

                if (player != null &&
                    marker != null)
                {
                    Vector3 spawnPosition =
                        marker.transform.position;

                    Quaternion spawnRotation =
                        marker.transform.rotation;

                    Vector3 originalPlayerPosition =
                        player.transform.position;


                    // The Erenshor player survives the scene transition and
                    // several native systems can still update its transform
                    // during the first frames of a newly loaded scene. Keep
                    // the player pinned to the authored entry marker briefly
                    // instead of asserting the transform only twice.
                    for (int settleFrame = 0;
                         settleFrame < stabilizationFrames;
                         settleFrame++)
                    {
                        PlayerControl currentPlayer =
                            GameData.PlayerControl;

                        if (currentPlayer == null ||
                            !IsFallenReach(
                                SceneManager.GetActiveScene()))
                        {
                            yield break;
                        }

                        SetPlayerTransform(
                            currentPlayer,
                            spawnPosition,
                            spawnRotation);

                        yield return null;
                    }

                    PlayerControl finalPlayer =
                        GameData.PlayerControl;

                    if (finalPlayer != null)
                    {
                        SetPlayerTransform(
                            finalPlayer,
                            spawnPosition,
                            spawnRotation);

                    }

                    LogSystemMessage(
                        "Entered Fallen Reach. Use /fotfreturn to return.",
                        "");

                    yield break;
                }

                yield return null;
            }

            Plugin.NativeLog.LogError(
                "CustomZoneLoader: timed out waiting for PlayerControl and scene marker [" +
                FallenReachPlayerSpawnName +
                "] after Fallen Reach loaded.");

            LogSystemMessage(
                "Fallen Reach loaded, but the player spawn could not be resolved.",
                "yellow");
        }


        // ============================================================
        // RETURN PLACEMENT
        // ============================================================

        private static IEnumerator RestorePlayerToReturnPoint()
        {
            const int maximumFrames =
                600;

            for (int frame = 0;
                 frame < maximumFrames;
                 frame++)
            {
                PlayerControl player =
                    GameData.PlayerControl;

                if (player != null)
                {
                    // Native zone setup can reposition the player during its
                    // first few frames, so give it a short settling period.
                    yield return null;
                    yield return null;
                    yield return null;

                    SetPlayerTransform(
                        player,
                        _returnPosition,
                        _returnRotation);

                    yield return null;

                    SetPlayerTransform(
                        player,
                        _returnPosition,
                        _returnRotation);


                    LogSystemMessage(
                        "Returned from Fallen Reach.",
                        "");

                    yield break;
                }

                yield return null;
            }

            Plugin.NativeLog.LogError(
                "CustomZoneLoader: timed out waiting for PlayerControl while restoring the native return point.");
        }


        // ============================================================
        // PLAYER TRANSFORM
        // ============================================================

        private static void SetPlayerTransform(
            PlayerControl player,
            Vector3 position,
            Quaternion rotation)
        {
            if (player == null)
            {
                return;
            }

            Transform transform =
                player.transform;

            transform.position =
                position;

            transform.rotation =
                rotation;

        }


        // ============================================================
        // HELPERS
        // ============================================================

        private static GameObject FindSceneObject(
            Scene scene,
            string objectName)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                string.IsNullOrEmpty(
                    objectName))
            {
                return null;
            }

            GameObject[] roots =
                scene.GetRootGameObjects();

            if (roots == null)
            {
                return null;
            }

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

                Transform[] transforms =
                    root.GetComponentsInChildren<Transform>(
                        true);

                if (transforms == null)
                {
                    continue;
                }

                for (int transformIndex = 0;
                     transformIndex < transforms.Length;
                     transformIndex++)
                {
                    Transform candidate =
                        transforms[transformIndex];

                    if (candidate != null &&
                        string.Equals(
                            candidate.name,
                            objectName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate.gameObject;
                    }
                }
            }

            return null;
        }


        private static string GetTransformPath(
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


        private static bool IsFallenReach(
            Scene scene)
        {
            if (!scene.IsValid())
            {
                return false;
            }

            if (string.Equals(
                    scene.name,
                    FallenReachSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(
                    scene.path))
            {
                string fileName =
                    Path.GetFileNameWithoutExtension(
                        scene.path);

                if (string.Equals(
                        fileName,
                        FallenReachSceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }


        private static void LogSystemMessage(
            string message,
            string color)
        {
            try
            {
                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        message,
                        ChatLogLine.LogType.SystemMessages,
                        color ?? ""));
            }
            catch
            {
                // Logging to the native console still provides diagnostics
                // if the chat UI is unavailable during a scene transition.
            }
        }
    }
}
