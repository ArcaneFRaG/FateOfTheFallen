using System.IO;
using UnityEditor;
using UnityEngine;

public static class FOTFAssetBundleBuilder
{
    [MenuItem("FOTF/Build AssetBundles")]
    public static void BuildAssetBundles()
    {
        string outputPath =
            Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    "..",
                    "..",
                    "Assets",
                    "Bundles"));

        Directory.CreateDirectory(
            outputPath);

        string[] zoneAssets =
    AssetDatabase.GetAssetPathsFromAssetBundle(
        "fotf_zones");

        Debug.Log(
            "[FOTF] fotf_zones contains:");

        foreach (string assetPath in zoneAssets)
        {
            Debug.Log(
                "[FOTF]   " +
                assetPath);
        }


        BuildPipeline.BuildAssetBundles(
            outputPath,
            BuildAssetBundleOptions.ChunkBasedCompression |
            BuildAssetBundleOptions.ForceRebuildAssetBundle,
            BuildTarget.StandaloneWindows64);

        Debug.Log(
            "FOTF AssetBundles built to: " +
            outputPath);
    }
}