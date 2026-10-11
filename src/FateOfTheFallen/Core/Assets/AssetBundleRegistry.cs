using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Shared runtime loader for external Fate of the Fallen AssetBundles.
    ///
    /// Bundles are resolved beside the compiled mod DLL under Assets/Bundles.
    /// The loader supports both the original flat layout and categorized
    /// subfolders such as Zones, Raids, Equipment and Shared.
    /// </summary>
    internal static class AssetBundleRegistry
    {
        private static readonly Dictionary<string, AssetBundle>
            LoadedBundles =
                new Dictionary<string, AssetBundle>(
                    StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, UnityEngine.Object>
            LoadedAssets =
                new Dictionary<string, UnityEngine.Object>(
                    StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string>
            FailedBundles =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

        private static string _modDirectory;

        internal static string ModDirectory
        {
            get
            {
                if (!string.IsNullOrEmpty(
                        _modDirectory))
                {
                    return _modDirectory;
                }

                try
                {
                    string assemblyPath =
                        Assembly.GetExecutingAssembly()
                            .Location;

                    if (!string.IsNullOrEmpty(
                            assemblyPath))
                    {
                        _modDirectory =
                            Path.GetDirectoryName(
                                assemblyPath);
                    }
                }
                catch (Exception exception)
                {
                    Plugin.ModLog.Error(
                        "AssetBundleRegistry: failed to resolve the Fate of the Fallen assembly directory",
                        exception);
                }

                if (string.IsNullOrEmpty(
                        _modDirectory))
                {
                    _modDirectory =
                        AppDomain.CurrentDomain.BaseDirectory;
                }

                return _modDirectory;
            }
        }

        internal static string GetBundlePath(
            string bundleName)
        {
            if (string.IsNullOrWhiteSpace(
                    bundleName))
            {
                return null;
            }

            string bundleRoot =
                Path.Combine(
                    ModDirectory,
                    "Assets",
                    "Bundles");

            // Preserve compatibility with the original flat bundle layout.
            string directPath =
                Path.Combine(
                    bundleRoot,
                    bundleName);

            if (File.Exists(
                    directPath))
            {
                return directPath;
            }

            // Preferred categorized layout for expansion content.
            string[] categories =
            {
                "Zones",
                "Raids",
                "Equipment",
                "NPCs",
                "Shared"
            };

            for (int i = 0;
                 i < categories.Length;
                 i++)
            {
                string categorizedPath =
                    Path.Combine(
                        bundleRoot,
                        categories[i],
                        bundleName);

                if (File.Exists(
                        categorizedPath))
                {
                    return categorizedPath;
                }
            }

            // Allow future content categories without requiring another code
            // change. Search only for an exact filename; .manifest files will
            // therefore never be mistaken for the AssetBundle itself.
            try
            {
                if (Directory.Exists(
                        bundleRoot))
                {
                    string[] candidates =
                        Directory.GetFiles(
                            bundleRoot,
                            bundleName,
                            SearchOption.AllDirectories);

                    if (candidates != null &&
                        candidates.Length > 0)
                    {
                        Array.Sort(
                            candidates,
                            StringComparer.OrdinalIgnoreCase);

                        return candidates[0];
                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "AssetBundleRegistry: failed while searching categorized bundle folders for [" +
                    bundleName +
                    "]",
                    exception);
            }

            // Return the legacy path even when missing so error logging still
            // reports a useful expected location.
            return directPath;
        }

        internal static AssetBundle GetBundle(
            string bundleName)
        {
            if (string.IsNullOrWhiteSpace(
                    bundleName))
            {
                return null;
            }

            AssetBundle loaded;

            if (LoadedBundles.TryGetValue(
                    bundleName,
                    out loaded))
            {
                return loaded;
            }

            string bundlePath =
                GetBundlePath(
                    bundleName);

            // A bundle may be copied into Assets/Bundles while Erenshor is
            // already running. If an earlier lookup failed only because the
            // file did not exist yet, allow a later call to retry instead of
            // requiring a full game restart.
            if (FailedBundles.Contains(
                    bundleName))
            {
                if (string.IsNullOrEmpty(
                        bundlePath) ||
                    !File.Exists(
                        bundlePath))
                {
                    return null;
                }

                FailedBundles.Remove(
                    bundleName);
            }

            if (string.IsNullOrEmpty(
                    bundlePath) ||
                !File.Exists(
                    bundlePath))
            {
                FailedBundles.Add(
                    bundleName);

                Plugin.ModLog.Error(
                    "AssetBundleRegistry: bundle [" +
                    bundleName +
                    "] was not found at [" +
                    (bundlePath ?? "<null>") +
                    "].");

                return null;
            }

            try
            {
                AssetBundle bundle =
                    AssetBundle.LoadFromFile(
                        bundlePath);

                if (bundle == null)
                {
                    FailedBundles.Add(
                        bundleName);

                    Plugin.ModLog.Error(
                        "AssetBundleRegistry: Unity failed to load bundle [" +
                        bundleName +
                        "] from [" +
                        bundlePath +
                        "].");

                    return null;
                }

                LoadedBundles[bundleName] =
                    bundle;

                return bundle;
            }
            catch (Exception exception)
            {
                FailedBundles.Add(
                    bundleName);

                Plugin.ModLog.Error(
                    "AssetBundleRegistry: exception while loading bundle [" +
                    bundleName +
                    "]",
                    exception);

                return null;
            }
        }

        internal static GameObject GetPrefab(
            string bundleName,
            string prefabName)
        {
            if (string.IsNullOrWhiteSpace(
                    bundleName) ||
                string.IsNullOrWhiteSpace(
                    prefabName))
            {
                return null;
            }

            string cacheKey =
                bundleName +
                "::" +
                prefabName;

            UnityEngine.Object cached;

            if (LoadedAssets.TryGetValue(
                    cacheKey,
                    out cached))
            {
                return cached as GameObject;
            }

            AssetBundle bundle =
                GetBundle(
                    bundleName);

            if (bundle == null)
            {
                return null;
            }

            try
            {
                GameObject prefab =
                    bundle.LoadAsset<GameObject>(
                        prefabName);

                if (prefab == null)
                {
                    string matchedAssetName =
                        FindAssetName(
                            bundle,
                            prefabName);

                    if (!string.IsNullOrEmpty(
                            matchedAssetName))
                    {
                        prefab =
                            bundle.LoadAsset<GameObject>(
                                matchedAssetName);
                    }
                }

                if (prefab == null)
                {
                    Plugin.ModLog.Error(
                        "AssetBundleRegistry: prefab [" +
                        prefabName +
                        "] was not found in bundle [" +
                        bundleName +
                        "]. Bundle assets: " +
                        DescribeAssets(
                            bundle));

                    return null;
                }

                LoadedAssets[cacheKey] =
                    prefab;

                return prefab;
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "AssetBundleRegistry: exception while resolving prefab [" +
                    prefabName +
                    "] from bundle [" +
                    bundleName +
                    "]",
                    exception);

                return null;
            }
        }

        internal static void Clear()
        {
            LoadedAssets.Clear();

            foreach (KeyValuePair<string, AssetBundle> pair
                     in LoadedBundles)
            {
                AssetBundle bundle =
                    pair.Value;

                if (bundle == null)
                {
                    continue;
                }

                try
                {
                    // Instantiated GameObjects should already have been
                    // destroyed by their owning feature before this point.
                    bundle.Unload(
                        false);
                }
                catch (Exception exception)
                {
                    Plugin.ModLog.Error(
                        "AssetBundleRegistry: failed to unload bundle [" +
                        pair.Key +
                        "]",
                        exception);
                }
            }

            LoadedBundles.Clear();
            FailedBundles.Clear();
            _modDirectory = null;
        }

        private static string FindAssetName(
            AssetBundle bundle,
            string requestedName)
        {
            if (bundle == null ||
                string.IsNullOrWhiteSpace(
                    requestedName))
            {
                return null;
            }

            string[] assetNames =
                bundle.GetAllAssetNames();

            if (assetNames == null)
            {
                return null;
            }

            for (int i = 0;
                 i < assetNames.Length;
                 i++)
            {
                string assetName =
                    assetNames[i];

                if (string.IsNullOrEmpty(
                        assetName))
                {
                    continue;
                }

                string fileName =
                    Path.GetFileNameWithoutExtension(
                        assetName);

                if (string.Equals(
                        fileName,
                        requestedName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return assetName;
                }
            }

            return null;
        }

        private static string DescribeAssets(
            AssetBundle bundle)
        {
            if (bundle == null)
            {
                return "<bundle null>";
            }

            try
            {
                string[] assetNames =
                    bundle.GetAllAssetNames();

                if (assetNames == null ||
                    assetNames.Length == 0)
                {
                    return "<none>";
                }

                return string.Join(
                    ", ",
                    assetNames);
            }
            catch
            {
                return "<failed to enumerate>";
            }
        }
    }
}
