using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Focused custom-zone diagnostic.
    ///
    /// The original broad scene scan proved the useful part: native Erenshor
    /// zones own ZoneAnnounce / PointOfInterest state and four GameData zone
    /// references differed in Fallen Reach. This version deliberately avoids
    /// dumping thousands of NPC nameplates, equipment bones and ordinary scene
    /// props. It reports only the zone contract and root-canvas state.
    /// </summary>
    internal static class ZoneSceneDiagnostic
    {
        private const string Prefix =
            "[ZoneDiag] ";

        private const string BaselineFileName =
            "FOTF_ZoneDiagnostic_Baseline_v2.txt";

        private static bool _initialized;
        private static ZoneSnapshot _nativeBaseline;


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

            LoadPersistedBaseline();

            SceneManager.sceneLoaded -=
                OnSceneLoaded;

            SceneManager.sceneLoaded +=
                OnSceneLoaded;

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (IsFallenReach(
                    activeScene))
            {
                CoroutineHost.Start(
                    DumpAfterInitialization(
                        activeScene,
                        "FALLEN REACH LOGIN"));
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
        }


        // ============================================================
        // BASELINE
        // ============================================================

        internal static void CaptureNativeBaseline(
            Scene scene,
            string reason)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                IsFallenReach(
                    scene))
            {
                return;
            }

            try
            {
                _nativeBaseline =
                    CaptureSnapshot(
                        scene);

                SavePersistedBaseline(
                    _nativeBaseline);

            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    Prefix +
                    "Native baseline capture failed: " +
                    exception);
            }
        }


        // ============================================================
        // DUMP
        // ============================================================

        internal static void DumpCurrentScene()
        {
            try
            {
                ZoneSnapshot snapshot =
                    CaptureSnapshot(
                        SceneManager.GetActiveScene());

                LogSnapshot(
                    snapshot,
                    "CURRENT ZONE CONTRACT");

                if (_nativeBaseline != null &&
                    IsFallenReach(
                        SceneManager.GetActiveScene()))
                {
                    LogTargetedComparison(
                        _nativeBaseline,
                        snapshot);
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    Prefix +
                    "Current zone dump failed: " +
                    exception);
            }
        }


        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            if (!IsFallenReach(
                    scene))
            {
                return;
            }

            CoroutineHost.Start(
                DumpAfterInitialization(
                    scene,
                    "FALLEN REACH AUTO CHECK"));
        }


        private static IEnumerator DumpAfterInitialization(
            Scene scene,
            string title)
        {
            // Bootstrap creates native components on the first few frames.
            // Wait long enough to inspect the resulting state rather than the
            // transient sceneLoaded frame.
            for (int i = 0;
                 i < 30;
                 i++)
            {
                yield return null;
            }

            if (!scene.IsValid() ||
                !scene.isLoaded)
            {
                yield break;
            }

            ZoneSnapshot snapshot =
                CaptureSnapshot(
                    scene);

            LogSnapshot(
                snapshot,
                title);

            if (_nativeBaseline != null)
            {
                LogTargetedComparison(
                    _nativeBaseline,
                    snapshot);
            }
        }


        // ============================================================
        // SNAPSHOT
        // ============================================================

        private static ZoneSnapshot CaptureSnapshot(
            Scene scene)
        {
            ZoneSnapshot snapshot =
                new ZoneSnapshot();

            snapshot.SceneName =
                scene.IsValid()
                    ? scene.name
                    : "<invalid>";

            snapshot.ScenePath =
                scene.IsValid()
                    ? scene.path
                    : "";

            snapshot.BuildIndex =
                scene.IsValid()
                    ? scene.buildIndex
                    : -999;

            CaptureNativeZoneComponents(
                snapshot);

            CaptureGameDataState(
                snapshot);

            CaptureRootCanvasState(
                snapshot);

            return snapshot;
        }


        private static void CaptureNativeZoneComponents(
            ZoneSnapshot snapshot)
        {
            ZoneAnnounce[] announces =
                Resources.FindObjectsOfTypeAll<ZoneAnnounce>();

            if (announces != null)
            {
                for (int i = 0;
                     i < announces.Length;
                     i++)
                {
                    ZoneAnnounce announce =
                        announces[i];

                    if (!IsLiveSceneComponent(
                            announce))
                    {
                        continue;
                    }

                    snapshot.ZoneAnnounceObjects.Add(
                        DescribeComponent(
                            announce));
                }
            }

            PointOfInterest[] points =
                Resources.FindObjectsOfTypeAll<PointOfInterest>();

            if (points != null)
            {
                for (int i = 0;
                     i < points.Length;
                     i++)
                {
                    PointOfInterest point =
                        points[i];

                    if (!IsLiveSceneComponent(
                            point))
                    {
                        continue;
                    }

                    snapshot.PointsOfInterest.Add(
                        DescribeComponent(
                            point) +
                        " Use=" +
                        point.Use);
                }
            }

            Component[] allComponents =
                Resources.FindObjectsOfTypeAll<Component>();

            if (allComponents == null)
            {
                return;
            }

            for (int i = 0;
                 i < allComponents.Length;
                 i++)
            {
                Component component =
                    allComponents[i];

                if (!IsLiveSceneComponent(
                        component))
                {
                    continue;
                }

                string typeName =
                    component.GetType().Name;

                if (string.Equals(
                        typeName,
                        "Zoneline",
                        StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.ZonelineObjects.Add(
                        DescribeComponent(
                            component));
                }
                else if (string.Equals(
                             typeName,
                             "SpawnPoint",
                             StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.SpawnPointObjects.Add(
                        DescribeComponent(
                            component));
                }
            }
        }


        // ============================================================
        // GAMEDATA
        // ============================================================

        private static void CaptureGameDataState(
            ZoneSnapshot snapshot)
        {
            string[] names =
            {
                "ZoneAnnounce",
                "ZoneAnnounceData",
                "CurrentZoneAnnounce",
                "NearestZoneIn",
                "EgressLocations"
            };

            for (int i = 0;
                 i < names.Length;
                 i++)
            {
                string name =
                    names[i];

                snapshot.GameDataStates[name] =
                    DescribeStaticMember(
                        typeof(GameData),
                        name);
            }

            snapshot.GameDataStates["POI.POIs"] =
                DescribeStaticMember(
                    typeof(POI),
                    "POIs");
        }


        // ============================================================
        // ROOT CANVASES
        // ============================================================

        private static void CaptureRootCanvasState(
            ZoneSnapshot snapshot)
        {
            GameObject[] allObjects =
                Resources.FindObjectsOfTypeAll<GameObject>();

            if (allObjects == null)
            {
                return;
            }

            for (int i = 0;
                 i < allObjects.Length;
                 i++)
            {
                GameObject gameObject =
                    allObjects[i];

                if (gameObject == null ||
                    !gameObject.scene.IsValid() ||
                    gameObject.transform.parent != null)
                {
                    continue;
                }

                Component[] components;

                try
                {
                    components =
                        gameObject.GetComponents<Component>();
                }
                catch
                {
                    continue;
                }

                bool hasCanvas =
                    false;

                List<string> componentNames =
                    new List<string>();

                for (int componentIndex = 0;
                     componentIndex < components.Length;
                     componentIndex++)
                {
                    Component component =
                        components[componentIndex];

                    if (component == null)
                    {
                        continue;
                    }

                    string typeName =
                        component.GetType().Name;

                    componentNames.Add(
                        typeName);

                    if (string.Equals(
                            typeName,
                            "Canvas",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        hasCanvas =
                            true;
                    }
                }

                if (!hasCanvas)
                {
                    continue;
                }

                string identity =
                    gameObject.name;

                snapshot.RootCanvasStates[identity] =
                    "scene=" +
                    gameObject.scene.name +
                    " self=" +
                    gameObject.activeSelf +
                    " hierarchy=" +
                    gameObject.activeInHierarchy +
                    " components=" +
                    string.Join(
                        ",",
                        componentNames.ToArray());
            }
        }


        // ============================================================
        // LOGGING
        // ============================================================

        private static void LogSnapshot(
            ZoneSnapshot snapshot,
            string title)
        {
            if (snapshot == null)
            {
                return;
            }




            LogList(
                "ZoneAnnounce objects",
                snapshot.ZoneAnnounceObjects,
                20);

            LogList(
                "PointOfInterest objects",
                snapshot.PointsOfInterest,
                30);

            LogList(
                "Zoneline objects",
                snapshot.ZonelineObjects,
                20);

            LogList(
                "SpawnPoint objects",
                snapshot.SpawnPointObjects,
                20);

            LogDictionary(
                "GameData/POI state",
                snapshot.GameDataStates,
                20);

            LogDictionary(
                "Root Canvas state",
                snapshot.RootCanvasStates,
                40);

        }


        private static void LogTargetedComparison(
            ZoneSnapshot baseline,
            ZoneSnapshot current)
        {

            foreach (KeyValuePair<string, string> pair
                     in baseline.GameDataStates)
            {
                string currentValue;

                if (!current.GameDataStates.TryGetValue(
                        pair.Key,
                        out currentValue))
                {
                    currentValue =
                        "<not captured>";
                }

                if (!string.Equals(
                        pair.Value,
                        currentValue,
                        StringComparison.Ordinal))
                {
                }
            }

            foreach (KeyValuePair<string, string> pair
                     in baseline.RootCanvasStates)
            {
                string currentValue;

                if (!current.RootCanvasStates.TryGetValue(
                        pair.Key,
                        out currentValue))
                {

                    continue;
                }

                if (!string.Equals(
                        pair.Value,
                        currentValue,
                        StringComparison.Ordinal))
                {
                }
            }
        }


        private static void LogList(
            string title,
            IList<string> values,
            int maximum)
        {

            int count =
                Math.Min(
                    values.Count,
                    maximum);

            for (int i = 0;
                 i < count;
                 i++)
            {
            }

            if (values.Count > maximum)
            {
            }
        }


        private static void LogDictionary(
            string title,
            Dictionary<string, string> values,
            int maximum)
        {
            List<string> lines =
                new List<string>();

            foreach (KeyValuePair<string, string> pair
                     in values)
            {
                lines.Add(
                    pair.Key +
                    "=[" +
                    pair.Value +
                    "]");
            }

            LogList(
                title,
                lines,
                maximum);
        }


        // ============================================================
        // PERSISTENCE
        // ============================================================

        private static string BaselinePath
        {
            get
            {
                return Path.Combine(
                    AssetBundleRegistry.ModDirectory,
                    "Diagnostics",
                    BaselineFileName);
            }
        }


        private static void SavePersistedBaseline(
            ZoneSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            try
            {
                string directory =
                    Path.GetDirectoryName(
                        BaselinePath);

                if (!Directory.Exists(
                        directory))
                {
                    Directory.CreateDirectory(
                        directory);
                }

                List<string> lines =
                    new List<string>();

                lines.Add(
                    "VERSION|2");

                lines.Add(
                    "SCENE|" +
                    Encode(snapshot.SceneName) +
                    "|" +
                    Encode(snapshot.ScenePath) +
                    "|" +
                    snapshot.BuildIndex);

                foreach (KeyValuePair<string, string> pair
                         in snapshot.GameDataStates)
                {
                    lines.Add(
                        "GD|" +
                        Encode(pair.Key) +
                        "|" +
                        Encode(pair.Value));
                }

                foreach (KeyValuePair<string, string> pair
                         in snapshot.RootCanvasStates)
                {
                    lines.Add(
                        "CANVAS|" +
                        Encode(pair.Key) +
                        "|" +
                        Encode(pair.Value));
                }

                File.WriteAllLines(
                    BaselinePath,
                    lines.ToArray());
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    Prefix +
                    "Could not persist native baseline: " +
                    exception.Message);
            }
        }


        private static void LoadPersistedBaseline()
        {
            try
            {
                if (!File.Exists(
                        BaselinePath))
                {
                    return;
                }

                string[] lines =
                    File.ReadAllLines(
                        BaselinePath);

                ZoneSnapshot snapshot =
                    new ZoneSnapshot();

                for (int i = 0;
                     i < lines.Length;
                     i++)
                {
                    string[] parts =
                        lines[i].Split('|');

                    if (parts.Length == 0)
                    {
                        continue;
                    }

                    if (parts[0] == "SCENE" &&
                        parts.Length >= 4)
                    {
                        snapshot.SceneName =
                            Decode(parts[1]);

                        snapshot.ScenePath =
                            Decode(parts[2]);

                        int.TryParse(
                            parts[3],
                            out snapshot.BuildIndex);
                    }
                    else if (parts[0] == "GD" &&
                             parts.Length >= 3)
                    {
                        snapshot.GameDataStates[
                            Decode(parts[1])] =
                            Decode(parts[2]);
                    }
                    else if (parts[0] == "CANVAS" &&
                             parts.Length >= 3)
                    {
                        snapshot.RootCanvasStates[
                            Decode(parts[1])] =
                            Decode(parts[2]);
                    }
                }

                if (!string.IsNullOrEmpty(
                        snapshot.SceneName))
                {
                    _nativeBaseline =
                        snapshot;
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    Prefix +
                    "Could not load persisted native baseline: " +
                    exception.Message);
            }
        }


        private static string Encode(
            string value)
        {
            return Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    value ?? ""));
        }


        private static string Decode(
            string value)
        {
            if (string.IsNullOrEmpty(
                    value))
            {
                return "";
            }

            return Encoding.UTF8.GetString(
                Convert.FromBase64String(
                    value));
        }


        // ============================================================
        // DESCRIPTION HELPERS
        // ============================================================

        private static bool IsLiveSceneComponent(
            Component component)
        {
            return component != null &&
                   component.gameObject != null &&
                   component.gameObject.scene.IsValid() &&
                   component.gameObject.scene.isLoaded;
        }


        private static string DescribeComponent(
            Component component)
        {
            if (component == null)
            {
                return "<null>";
            }

            GameObject gameObject =
                component.gameObject;

            return component.GetType().Name +
                   " name=[" +
                   gameObject.name +
                   "] scene=[" +
                   gameObject.scene.name +
                   "] self=" +
                   gameObject.activeSelf +
                   " hierarchy=" +
                   gameObject.activeInHierarchy +
                   " components=" +
                   DescribeComponents(
                       gameObject);
        }


        private static string DescribeComponents(
            GameObject gameObject)
        {
            if (gameObject == null)
            {
                return "<null>";
            }

            Component[] components;

            try
            {
                components =
                    gameObject.GetComponents<Component>();
            }
            catch
            {
                return "<read-failed>";
            }

            List<string> names =
                new List<string>();

            for (int i = 0;
                 i < components.Length;
                 i++)
            {
                Component component =
                    components[i];

                names.Add(
                    component != null
                        ? component.GetType().Name
                        : "<MissingComponent>");
            }

            return string.Join(
                ",",
                names.ToArray());
        }


        private static string DescribeStaticMember(
            Type type,
            string memberName)
        {
            object value =
                GetStaticMember(
                    type,
                    memberName);

            return DescribeValue(
                value);
        }


        private static object GetStaticMember(
            Type type,
            string memberName)
        {
            if (type == null)
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
                try
                {
                    return field.GetValue(
                        null);
                }
                catch
                {
                    return null;
                }
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
                try
                {
                    return property.GetValue(
                        null,
                        null);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }


        private static string DescribeValue(
            object value)
        {
            if (value == null)
            {
                return "<null>";
            }

            IList list =
                value as IList;

            if (list != null)
            {
                List<string> items =
                    new List<string>();

                int maximum =
                    Math.Min(
                        list.Count,
                        8);

                for (int i = 0;
                     i < maximum;
                     i++)
                {
                    items.Add(
                        DescribeSingleValue(
                            list[i]));
                }

                return value.GetType().Name +
                       " count=" +
                       list.Count +
                       " [" +
                       string.Join(
                           "; ",
                           items.ToArray()) +
                       (list.Count > maximum
                           ? "; ..."
                           : "") +
                       "]";
            }

            return DescribeSingleValue(
                value);
        }


        private static string DescribeSingleValue(
            object value)
        {
            if (value == null)
            {
                return "<null>";
            }

            UnityEngine.Object unityObject =
                value as UnityEngine.Object;

            if (unityObject != null)
            {
                GameObject gameObject =
                    unityObject as GameObject;

                if (gameObject == null)
                {
                    Component component =
                        unityObject as Component;

                    if (component != null)
                    {
                        gameObject =
                            component.gameObject;
                    }
                }

                if (gameObject != null)
                {
                    return value.GetType().Name +
                           " name=[" +
                           gameObject.name +
                           "] scene=[" +
                           gameObject.scene.name +
                           "] self=" +
                           gameObject.activeSelf +
                           " hierarchy=" +
                           gameObject.activeInHierarchy;
                }

                return value.GetType().Name +
                       " name=[" +
                       unityObject.name +
                       "]";
            }

            return value.ToString();
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


        // ============================================================
        // DATA
        // ============================================================

        private sealed class ZoneSnapshot
        {
            internal string SceneName =
                "";

            internal string ScenePath =
                "";

            internal int BuildIndex =
                -999;

            internal readonly List<string> ZoneAnnounceObjects =
                new List<string>();

            internal readonly List<string> PointsOfInterest =
                new List<string>();

            internal readonly List<string> ZonelineObjects =
                new List<string>();

            internal readonly List<string> SpawnPointObjects =
                new List<string>();

            internal readonly Dictionary<string, string> GameDataStates =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            internal readonly Dictionary<string, string> RootCanvasStates =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);
        }
    }
}
