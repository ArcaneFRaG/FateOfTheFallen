using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Supplies the minimum native Erenshor zone contract that an externally
    /// authored Fate of the Fallen scene does not contain by default.
    ///
    /// The first custom-zone diagnostic showed that externally authored scenes
    /// have no live ZoneAnnounce or PointOfInterest objects and that GameData can
    /// at stale native-zone references. Native SimPlayer code explicitly uses
    /// GameData.NearestZoneIn, GameData.ZoneAnnounce and POI.POIs, so these are
    /// treated as required zone infrastructure rather than cosmetic metadata.
    /// </summary>
    internal static class CustomZoneBootstrap
    {
        private const string RuntimeRootName =
            "FOTF_RuntimeZoneBootstrap";

        private const string FallenReachZoneAnnounceObjectName =
            "FOTF_FallenReach_ZoneAnnounce";

        private const string FallenReachZoneDisplayName =
            "Fallen Reach";

        private static readonly string[] FallenReachZoneComments =
        {
            "The air here feels wrong.",
            "This place does not belong to the mortal world.",
            "Something ancient lingers beyond the veil."
        };

        private const string RaidPocZoneAnnounceObjectName =
            "FOTF_RaidPoC_ZoneAnnounce";

        private const string RaidPocZoneDisplayName =
            "FOTF Raid PoC";

        private static readonly string[] RaidPocZoneComments =
        {
            "A sealed proving ground lies ahead.",
            "The raid gathers beyond the threshold.",
            "This place exists to test the rules of battle."
        };

        private sealed class SceneContract
        {
            internal readonly string DisplayName;
            internal readonly string PlayerSpawnName;
            internal readonly string ZoneExitName;
            internal readonly string ZoneAnnounceObjectName;
            internal readonly string[] ZoneComments;
            internal readonly bool RaidCapable;
            internal readonly string UseZoneAsTempBind;

            internal SceneContract(
                string displayName,
                string playerSpawnName,
                string zoneExitName,
                string zoneAnnounceObjectName,
                string[] zoneComments,
                bool raidCapable,
                string useZoneAsTempBind)
            {
                DisplayName =
                    displayName;

                PlayerSpawnName =
                    playerSpawnName;

                ZoneExitName =
                    zoneExitName;

                ZoneAnnounceObjectName =
                    zoneAnnounceObjectName;

                ZoneComments =
                    zoneComments;

                RaidCapable =
                    raidCapable;

                UseZoneAsTempBind =
                    useZoneAsTempBind ?? "";
            }
        }

        private static bool _initialized;
        private static int _bootstrappedSceneHandle =
            -1;

        private static GameObject _runtimeRoot;
        private static ZoneAnnounce _zoneAnnounce;
        private static PointOfInterest _zoneIn;
        private static PointOfInterest _zoneOut;


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

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (GetSceneContract(
                    activeScene) != null)
            {
                CoroutineHost.Start(
                    BootstrapAfterSceneLoad(
                        activeScene));
            }
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

            ClearRuntimeReferences();
        }


        // ============================================================
        // SCENE LOAD
        // ============================================================

        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            if (GetSceneContract(
                    scene) == null)
            {
                return;
            }

            CoroutineHost.Start(
                BootstrapAfterSceneLoad(
                    scene));
        }


        private static IEnumerator BootstrapAfterSceneLoad(
            Scene scene)
        {
            SceneContract contract =
                GetSceneContract(
                    scene);

            if (contract == null)
            {
                yield break;
            }

            // Let Unity finish activating the AssetBundle scene and give
            // Erenshor one frame to settle its persistent runtime objects.
            yield return null;

            const int maximumFrames =
                300;

            GameObject playerSpawn =
                null;

            GameObject zoneExit =
                null;

            for (int frame = 0;
                 frame < maximumFrames;
                 frame++)
            {
                if (!scene.IsValid() ||
                    !scene.isLoaded)
                {
                    yield break;
                }

                playerSpawn =
                    FindSceneObject(
                        scene,
                        contract.PlayerSpawnName);

                zoneExit =
                    FindSceneObject(
                        scene,
                        contract.ZoneExitName);

                if (playerSpawn != null &&
                    zoneExit != null)
                {
                    break;
                }

                yield return null;
            }

            if (playerSpawn == null)
            {
                Plugin.NativeLog.LogError(
                    "CustomZoneBootstrap: [" +
                    contract.DisplayName +
                    "] is missing marker [" +
                    contract.PlayerSpawnName +
                    "]. Native zone bootstrap aborted.");

                yield break;
            }

            if (zoneExit == null)
            {
                Plugin.NativeLog.LogError(
                    "CustomZoneBootstrap: [" +
                    contract.DisplayName +
                    "] is missing marker [" +
                    contract.ZoneExitName +
                    "]. Native zone bootstrap aborted.");

                yield break;
            }

            try
            {
                EnsureBootstrapObjects(
                    scene,
                    playerSpawn,
                    zoneExit,
                    contract);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "CustomZoneBootstrap: initial [" +
                    contract.DisplayName +
                    "] bootstrap failed: " +
                    exception);

                yield break;
            }

            // Native scene/login initialization continues for several frames
            // after sceneLoaded. During a cold login into Fallen Reach those
            // callbacks can run after our component Start() methods and replace
            // GameData references again. Reassert the custom-zone contract until
            // the core Erenshor runtime has been present and stable for a short
            // window, then verify the final bindings.
            const int stabilizationFrames =
                300;

            const int requiredStableFrames =
                30;

            int stableFrames =
                0;

            for (int frame = 0;
                 frame < stabilizationFrames;
                 frame++)
            {
                if (!scene.IsValid() ||
                    !scene.isLoaded)
                {
                    yield break;
                }

                try
                {
                    RegisterNativeZoneState();
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "CustomZoneBootstrap: [" +
                        contract.DisplayName +
                        "] stabilization failed: " +
                        exception);

                    yield break;
                }

                if (IsCoreRuntimeReady() &&
                    IsNativeZoneStateBound())
                {
                    stableFrames++;

                    if (stableFrames >=
                        requiredStableFrames)
                    {
                        break;
                    }
                }
                else
                {
                    stableFrames =
                        0;
                }

                yield return null;
            }

            try
            {
                RegisterNativeZoneState();

                _bootstrappedSceneHandle =
                    scene.handle;

                bool bound =
                    IsNativeZoneStateBound();

                if (bound)
                {
                }
                else
                {
                    Plugin.NativeLog.LogWarning(
                        "CustomZoneBootstrap: [" +
                        contract.DisplayName +
                        "] objects were created, but one or more native zone bindings did not remain attached. Run /fotfzonedump for the focused state.");
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "CustomZoneBootstrap: final [" +
                    contract.DisplayName +
                    "] registration failed: " +
                    exception);
            }
        }


        // ============================================================
        // BOOTSTRAP OBJECTS
        // ============================================================

        private static void EnsureBootstrapObjects(
            Scene scene,
            GameObject playerSpawn,
            GameObject zoneExit,
            SceneContract contract)
        {
            if (_bootstrappedSceneHandle != scene.handle)
            {
                ClearRuntimeReferences();
            }

            _runtimeRoot =
                FindSceneObject(
                    scene,
                    RuntimeRootName);

            if (_runtimeRoot == null)
            {
                _runtimeRoot =
                    new GameObject(
                        RuntimeRootName);

                SceneManager.MoveGameObjectToScene(
                    _runtimeRoot,
                    scene);
            }

            _runtimeRoot.transform.position =
                playerSpawn.transform.position;

            _runtimeRoot.transform.rotation =
                playerSpawn.transform.rotation;

            EnsureZoneAnnounce(
                scene,
                playerSpawn,
                contract);

            _zoneIn =
                EnsurePointOfInterest(
                    playerSpawn,
                    PointOfInterest.POIType.zoneIn);

            _zoneOut =
                EnsurePointOfInterest(
                    zoneExit,
                    PointOfInterest.POIType.zoneline);

            RegisterNativeZoneState();
        }


        private static void EnsureZoneAnnounce(
            Scene scene,
            GameObject playerSpawn,
            SceneContract contract)
        {
            GameObject announceObject =
                FindSceneObject(
                    scene,
                    contract.ZoneAnnounceObjectName);

            if (announceObject == null)
            {
                announceObject =
                    new GameObject(
                        contract.ZoneAnnounceObjectName);

                // Keep native Awake/OnEnable/Start from seeing completely
                // unconfigured metadata if ZoneAnnounce uses those callbacks.
                announceObject.SetActive(
                    false);

                SceneManager.MoveGameObjectToScene(
                    announceObject,
                    scene);

                announceObject.transform.SetParent(
                    _runtimeRoot.transform,
                    false);

                announceObject.transform.position =
                    playerSpawn.transform.position;

                announceObject.transform.rotation =
                    playerSpawn.transform.rotation;

                _zoneAnnounce =
                    announceObject.AddComponent<ZoneAnnounce>();

                ConfigureZoneAnnounce(
                    _zoneAnnounce,
                    contract);

                announceObject.SetActive(
                    true);
            }
            else
            {
                _zoneAnnounce =
                    announceObject.GetComponent<ZoneAnnounce>();

                if (_zoneAnnounce == null)
                {
                    announceObject.SetActive(
                        false);

                    _zoneAnnounce =
                        announceObject.AddComponent<ZoneAnnounce>();

                    ConfigureZoneAnnounce(
                        _zoneAnnounce,
                        contract);

                    announceObject.SetActive(
                        true);
                }
                else
                {
                    ConfigureZoneAnnounce(
                        _zoneAnnounce,
                        contract);
                }
            }
        }


        private static void ConfigureZoneAnnounce(
            ZoneAnnounce zoneAnnounce,
            SceneContract contract)
        {
            if (zoneAnnounce == null)
            {
                return;
            }

            TrySetInstanceMember(
                zoneAnnounce,
                "ZoneName",
                contract.DisplayName);

            TrySetInstanceMember(
                zoneAnnounce,
                "MobsKilledByPlayerParty",
                0);

            // Native Erenshor raid-zone contract.
            //
            // ZoneAnnounce.Start() copies UseZoneAsTempBind into
            // GameData.UseZoneAsTempBind and checks RaidCapable before
            // deciding whether an active raid is allowed to remain in the
            // scene. Respawn.RespawnPlayer() later compares
            // GameData.UseZoneAsTempBind with the active scene name; when
            // they match it overrides the normal wishing-well bind and
            // respawns the player in Reliquary.
            TrySetInstanceMember(
                zoneAnnounce,
                "RaidCapable",
                contract.RaidCapable);

            TrySetInstanceMember(
                zoneAnnounce,
                "UseZoneAsTempBind",
                contract.UseZoneAsTempBind);

            SetStringListMember(
                zoneAnnounce,
                "ZoneComments",
                contract.ZoneComments);
        }


        private static PointOfInterest EnsurePointOfInterest(
            GameObject gameObject,
            PointOfInterest.POIType use)
        {
            if (gameObject == null)
            {
                return null;
            }

            PointOfInterest point =
                gameObject.GetComponent<PointOfInterest>();

            if (point == null)
            {
                point =
                    gameObject.AddComponent<PointOfInterest>();
            }

            point.Use =
                use;

            return point;
        }


        // ============================================================
        // NATIVE REGISTRATION
        // ============================================================

        private static void RegisterNativeZoneState()
        {
            if (_zoneAnnounce == null ||
                _zoneIn == null ||
                _zoneOut == null)
            {
                return;
            }

            // These four references were the exact GameData differences found
            // by the native Azure -> Fallen Reach comparison.
            TrySetStaticMember(
                typeof(GameData),
                "ZoneAnnounce",
                _zoneAnnounce.transform);

            TrySetStaticMember(
                typeof(GameData),
                "ZoneAnnounceData",
                _zoneAnnounce);

            TrySetStaticMember(
                typeof(GameData),
                "CurrentZoneAnnounce",
                _zoneAnnounce);

            TrySetStaticMember(
                typeof(GameData),
                "NearestZoneIn",
                _zoneIn);

            // Mirror ZoneAnnounce.Start(). Custom AssetBundle scene startup
            // races against surviving native runtime objects, so reassert the
            // temp-bind value while the custom zone contract stabilizes.
            object tempBind =
                GetInstanceMember(
                    _zoneAnnounce,
                    "UseZoneAsTempBind");

            TrySetStaticMember(
                typeof(GameData),
                "UseZoneAsTempBind",
                tempBind ?? "");

            EnsureObjectInStaticList(
                typeof(POI),
                "POIs",
                _zoneIn);

            EnsureObjectInStaticList(
                typeof(POI),
                "POIs",
                _zoneOut);

            EnsureObjectInStaticList(
                typeof(GameData),
                "EgressLocations",
                _zoneOut);
        }


        private static bool IsCoreRuntimeReady()
        {
            return GameData.PlayerControl != null &&
                   GameData.PlayerStats != null &&
                   GameData.GM != null;
        }


        private static bool IsNativeZoneStateBound()
        {
            if (_zoneAnnounce == null ||
                _zoneIn == null ||
                _zoneOut == null)
            {
                return false;
            }

            if (!ReferenceEquals(
                    GetStaticMember(
                        typeof(GameData),
                        "ZoneAnnounce"),
                    _zoneAnnounce.transform))
            {
                return false;
            }

            if (!ReferenceEquals(
                    GetStaticMember(
                        typeof(GameData),
                        "ZoneAnnounceData"),
                    _zoneAnnounce))
            {
                return false;
            }

            if (!ReferenceEquals(
                    GetStaticMember(
                        typeof(GameData),
                        "CurrentZoneAnnounce"),
                    _zoneAnnounce))
            {
                return false;
            }

            if (!ReferenceEquals(
                    GetStaticMember(
                        typeof(GameData),
                        "NearestZoneIn"),
                    _zoneIn))
            {
                return false;
            }

            string announceTempBind =
                GetInstanceMember(
                    _zoneAnnounce,
                    "UseZoneAsTempBind")
                as string ?? "";

            string gameDataTempBind =
                GetStaticMember(
                    typeof(GameData),
                    "UseZoneAsTempBind")
                as string ?? "";

            if (!string.Equals(
                    announceTempBind,
                    gameDataTempBind,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return StaticListContains(
                       typeof(POI),
                       "POIs",
                       _zoneIn) &&
                   StaticListContains(
                       typeof(POI),
                       "POIs",
                       _zoneOut) &&
                   StaticListContains(
                       typeof(GameData),
                       "EgressLocations",
                       _zoneOut);
        }


        private static bool StaticListContains(
            Type ownerType,
            string memberName,
            UnityEngine.Object requiredObject)
        {
            if (requiredObject == null)
            {
                return false;
            }

            IList list =
                GetStaticMember(
                    ownerType,
                    memberName)
                as IList;

            if (list == null)
            {
                return false;
            }

            for (int index = 0;
                 index < list.Count;
                 index++)
            {
                if (ReferenceEquals(
                        list[index],
                        requiredObject))
                {
                    return true;
                }
            }

            return false;
        }


        // ============================================================
        // MANUAL REAPPLY
        // ============================================================

        internal static void ReapplyForCurrentScene()
        {
            Scene scene =
                SceneManager.GetActiveScene();

            SceneContract contract =
                GetSceneContract(
                    scene);

            if (contract == null)
            {
                Plugin.NativeLog.LogWarning(
                    "CustomZoneBootstrap: manual reapply ignored because the active scene is not a supported FOTF custom scene.");

                return;
            }


            CoroutineHost.Start(
                BootstrapAfterSceneLoad(
                    scene));
        }


        // ============================================================
        // REFLECTION HELPERS
        // ============================================================

        private static bool TrySetInstanceMember(
            object target,
            string memberName,
            object value)
        {
            if (target == null ||
                string.IsNullOrEmpty(
                    memberName))
            {
                return false;
            }

            Type type =
                target.GetType();

            FieldInfo field =
                type.GetField(
                    memberName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            if (field != null &&
                CanAssign(
                    field.FieldType,
                    value))
            {
                field.SetValue(
                    target,
                    value);

                return true;
            }

            PropertyInfo property =
                type.GetProperty(
                    memberName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            if (property != null &&
                property.CanWrite &&
                CanAssign(
                    property.PropertyType,
                    value))
            {
                property.SetValue(
                    target,
                    value,
                    null);

                return true;
            }

            return false;
        }


        private static bool TrySetStaticMember(
            Type type,
            string memberName,
            object value)
        {
            if (type == null ||
                string.IsNullOrEmpty(
                    memberName))
            {
                return false;
            }

            FieldInfo field =
                type.GetField(
                    memberName,
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            if (field != null &&
                CanAssign(
                    field.FieldType,
                    value))
            {
                field.SetValue(
                    null,
                    value);

                return true;
            }

            PropertyInfo property =
                type.GetProperty(
                    memberName,
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            if (property != null &&
                property.CanWrite &&
                CanAssign(
                    property.PropertyType,
                    value))
            {
                property.SetValue(
                    null,
                    value,
                    null);

                return true;
            }

            return false;
        }


        private static object GetStaticMember(
            Type type,
            string memberName)
        {
            if (type == null ||
                string.IsNullOrEmpty(
                    memberName))
            {
                return null;
            }

            FieldInfo field =
                type.GetField(
                    memberName,
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            if (field != null)
            {
                return field.GetValue(
                    null);
            }

            PropertyInfo property =
                type.GetProperty(
                    memberName,
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            if (property != null &&
                property.CanRead)
            {
                return property.GetValue(
                    null,
                    null);
            }

            return null;
        }


        private static void EnsureObjectInStaticList(
            Type ownerType,
            string memberName,
            UnityEngine.Object requiredObject)
        {
            if (requiredObject == null)
            {
                return;
            }

            IList list =
                GetStaticMember(
                    ownerType,
                    memberName)
                as IList;

            if (list == null)
            {
                return;
            }

            for (int index = list.Count - 1;
                 index >= 0;
                 index--)
            {
                object value =
                    list[index];

                UnityEngine.Object unityValue =
                    value as UnityEngine.Object;

                if (value == null ||
                    (!ReferenceEquals(
                         unityValue,
                         null) &&
                     unityValue == null))
                {
                    list.RemoveAt(
                        index);
                }
            }

            for (int index = 0;
                 index < list.Count;
                 index++)
            {
                object value =
                    list[index];

                if (ReferenceEquals(
                        value,
                        requiredObject))
                {
                    return;
                }
            }

            list.Add(
                requiredObject);
        }


        private static void SetStringListMember(
            object target,
            string memberName,
            IEnumerable<string> values)
        {
            if (target == null)
            {
                return;
            }

            object member =
                GetInstanceMember(
                    target,
                    memberName);

            IList list =
                member as IList;

            if (list != null)
            {
                list.Clear();

                foreach (string value
                         in values)
                {
                    list.Add(
                        value);
                }

                return;
            }

            List<string> replacement =
                new List<string>(
                    values);

            TrySetInstanceMember(
                target,
                memberName,
                replacement);
        }


        private static object GetInstanceMember(
            object target,
            string memberName)
        {
            if (target == null)
            {
                return null;
            }

            Type type =
                target.GetType();

            FieldInfo field =
                type.GetField(
                    memberName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            if (field != null)
            {
                return field.GetValue(
                    target);
            }

            PropertyInfo property =
                type.GetProperty(
                    memberName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            if (property != null &&
                property.CanRead)
            {
                return property.GetValue(
                    target,
                    null);
            }

            return null;
        }


        private static bool CanAssign(
            Type destinationType,
            object value)
        {
            if (destinationType == null)
            {
                return false;
            }

            if (value == null)
            {
                return !destinationType.IsValueType ||
                       Nullable.GetUnderlyingType(
                           destinationType) != null;
            }

            return destinationType.IsAssignableFrom(
                value.GetType());
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

                for (int transformIndex = 0;
                     transformIndex < transforms.Length;
                     transformIndex++)
                {
                    Transform transform =
                        transforms[transformIndex];

                    if (transform != null &&
                        string.Equals(
                            transform.name,
                            objectName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return transform.gameObject;
                    }
                }
            }

            return null;
        }


        private static SceneContract GetSceneContract(
            Scene scene)
        {
            if (IsFallenReach(
                    scene))
            {
                return new SceneContract(
                    FallenReachZoneDisplayName,
                    CustomZoneLoader.FallenReachPlayerSpawnName,
                    "FOTF_ZoneExit",
                    FallenReachZoneAnnounceObjectName,
                    FallenReachZoneComments,
                    false,
                    "");
            }

            if (CustomRaidLoader.IsRaidPoc(
                    scene))
            {
                return new SceneContract(
                    RaidPocZoneDisplayName,
                    CustomRaidLoader.RaidPlayerSpawnName,
                    CustomRaidLoader.RaidExitMarkerName,
                    RaidPocZoneAnnounceObjectName,
                    RaidPocZoneComments,
                    true,
                    CustomRaidLoader.RaidPocSceneName);
            }

            return null;
        }


        private static bool IsFallenReach(
            Scene scene)
        {
            return scene.IsValid() &&
                   scene.isLoaded &&
                   string.Equals(
                       scene.name,
                       CustomZoneLoader.FallenReachSceneName,
                       StringComparison.OrdinalIgnoreCase);
        }


        private static void ClearRuntimeReferences()
        {
            _runtimeRoot =
                null;

            _zoneAnnounce =
                null;

            _zoneIn =
                null;

            _zoneOut =
                null;

            _bootstrappedSceneHandle =
                -1;
        }
    }
}
