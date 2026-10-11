using System;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Ensures custom FOTF scene bundles are loaded before Erenshor's
    /// native SceneChange system attempts to restore a custom scene.
    ///
    /// This covers both Fallen Reach and the raid PoC so a character saved
    /// inside either external AssetBundle scene can be restored on login.
    /// </summary>
    [HarmonyPatch(
        typeof(SceneChange),
        "ChangeScene",
        new Type[]
        {
            typeof(string),
            typeof(Vector3),
            typeof(bool),
            typeof(float)
        })]
    internal static class Patch_CustomZoneSceneChange
    {
        [HarmonyPrefix]
        private static void Prefix(
            string __0)
        {
            string bundleName =
                null;

            if (string.Equals(
                    __0,
                    CustomZoneLoader.FallenReachSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                bundleName =
                    CustomZoneLoader.FallenReachBundleName;
            }
            else if (string.Equals(
                         __0,
                         CustomRaidLoader.RaidPocSceneName,
                         StringComparison.OrdinalIgnoreCase))
            {
                bundleName =
                    CustomRaidLoader.RaidPocBundleName;
            }

            if (string.IsNullOrEmpty(
                    bundleName))
            {
                return;
            }

            try
            {
                AssetBundle bundle =
                    AssetBundleRegistry.GetBundle(
                        bundleName);

                if (bundle == null)
                {
                    Plugin.NativeLog.LogError(
                        "Patch_CustomZoneSceneChange: native Erenshor requested [" +
                        __0 +
                        "] but bundle [" +
                        bundleName +
                        "] could not be loaded.");

                    return;
                }

            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Patch_CustomZoneSceneChange: failed while preparing custom scene bundle: " +
                    exception);
            }
        }
    }
}