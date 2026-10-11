using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildFOTFWorldBossBundle
{
    private const string PrefabPath =
        "Assets/FateOfTheFallen/WorldBosses/BlightTouchedColossus/Prefabs/BlightTouchedColossus.prefab";

    private const string BundleName =
        "fotf_worldbosses";

    private const string OutputFolder =
        "AssetBundles/StandaloneWindows64";

    [MenuItem("FOTF/Build World Boss Bundle")]
    public static void Build()
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);

        if (prefab == null)
        {
            EditorUtility.DisplayDialog(
                "FOTF World Boss Bundle",
                "Could not find the prefab at:\n\n" +
                PrefabPath +
                "\n\nCreate/move the prefab to that exact path first.",
                "OK");

            Debug.LogError(
                "[FOTF] World boss prefab not found: " +
                PrefabPath);

            return;
        }

        AssetImporter importer =
            AssetImporter.GetAtPath(
                PrefabPath);

        if (importer == null)
        {
            Debug.LogError(
                "[FOTF] Could not get AssetImporter for: " +
                PrefabPath);

            return;
        }

        importer.assetBundleName =
            BundleName;

        importer.assetBundleVariant =
            string.Empty;

        importer.SaveAndReimport();

        Directory.CreateDirectory(
            OutputFolder);

        BuildPipeline.BuildAssetBundles(
            OutputFolder,
            BuildAssetBundleOptions.ChunkBasedCompression,
            BuildTarget.StandaloneWindows64);

        AssetDatabase.Refresh();

        string bundlePath =
            Path.Combine(
                OutputFolder,
                BundleName);

        if (!File.Exists(
                bundlePath))
        {
            Debug.LogError(
                "[FOTF] AssetBundle build finished, but bundle was not found at: " +
                bundlePath);

            EditorUtility.DisplayDialog(
                "FOTF World Boss Bundle",
                "Build finished, but the expected bundle was not found:\n\n" +
                bundlePath,
                "OK");

            return;
        }

        FileInfo info =
            new FileInfo(
                bundlePath);

        Debug.Log(
            "[FOTF] World boss bundle built successfully.\n" +
            "Bundle: " + bundlePath + "\n" +
            "Bytes: " + info.Length + "\n" +
            "Prefab: " + PrefabPath);

        EditorUtility.RevealInFinder(
            bundlePath);

        EditorUtility.DisplayDialog(
            "FOTF World Boss Bundle",
            "Success!\n\n" +
            "Bundle:\n" +
            bundlePath +
            "\n\n" +
            "Next we will copy this bundle into the FOTF mod's runtime asset-bundle folder and spawn the prefab in Erenshor.",
            "OK");
    }
}
