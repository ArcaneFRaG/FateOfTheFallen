#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class FOTFEquipmentMaterialRemapper
{
    private const string MaterialRoot = "Assets/FOTF_Generated/Materials/Equipment";

    private sealed class MaterialDef
    {
        public string Name;
        public Color BaseColor;
        public bool Emissive;
        public Color EmissionColor;

        public MaterialDef(string name, Color baseColor)
        {
            Name = name;
            BaseColor = baseColor;
            Emissive = false;
            EmissionColor = Color.black;
        }

        public MaterialDef(string name, Color baseColor, Color emissionColor)
        {
            Name = name;
            BaseColor = baseColor;
            Emissive = true;
            EmissionColor = emissionColor;
        }
    }

    private static readonly MaterialDef[] Definitions =
    {
        new MaterialDef(
            "FOTF_Mat_Moldermaw_Stone_Black",
            new Color(0.028f, 0.032f, 0.028f, 1f)),

        new MaterialDef(
            "FOTF_Mat_Moldermaw_Stone_Dark",
            new Color(0.070f, 0.077f, 0.063f, 1f)),

        new MaterialDef(
            "FOTF_Mat_Moldermaw_Stone_Mid",
            new Color(0.155f, 0.155f, 0.120f, 1f)),

        new MaterialDef(
            "FOTF_Mat_Moldermaw_Stone_Edge",
            new Color(0.310f, 0.300f, 0.220f, 1f)),

        new MaterialDef(
            "FOTF_Mat_Bulwark_Binding",
            new Color(0.070f, 0.065f, 0.055f, 1f)),

        new MaterialDef(
            "FOTF_Mat_Blight_Green",
            new Color(0.080f, 0.500f, 0.025f, 1f),
            new Color(0.42f, 3.00f, 0.12f, 1f)),

        new MaterialDef(
            "FOTF_Mat_Blight_Hot",
            new Color(0.200f, 0.800f, 0.070f, 1f),
            new Color(1.20f, 4.00f, 0.35f, 1f)),

        new MaterialDef(
            "FOTF_Mat_Fungus_Stem",
            new Color(0.240f, 0.190f, 0.110f, 1f)),

        new MaterialDef(
            "FOTF_Mat_Fungus_Cap",
            new Color(0.340f, 0.180f, 0.100f, 1f)),

        // Shared materials already used by the other Moldermaw equipment.
        new MaterialDef(
            "FOTF_Mat_Rotcleaver_Grip_Dark",
            new Color(0.095f, 0.045f, 0.025f, 1f)),

        new MaterialDef(
            "FOTF_Mat_Rotcleaver_Grip_Light",
            new Color(0.160f, 0.075f, 0.040f, 1f)),

        new MaterialDef(
            "FOTF_Mat_Rotcleaver_Binding",
            new Color(0.075f, 0.070f, 0.058f, 1f)),

        new MaterialDef(
            "FOTF_Mat_Sporepiercer_Grip_Dark",
            new Color(0.100f, 0.052f, 0.030f, 1f)),

        new MaterialDef(
            "FOTF_Mat_Sporepiercer_Grip_Light",
            new Color(0.170f, 0.085f, 0.045f, 1f)),

        new MaterialDef(
            "FOTF_Mat_Sporepiercer_String",
            new Color(0.015f, 0.017f, 0.014f, 1f)),
    };

    [MenuItem("Tools/Fate of the Fallen/Equipment/Remap Selected FBX Materials")]
    private static void RemapSelectedFbxMaterials()
    {
        UnityEngine.Object selected = Selection.activeObject;
        if (selected == null)
        {
            Debug.LogError("[FOTF Equipment] Select the imported equipment FBX in the Project window first.");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(assetPath) ||
            !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[FOTF Equipment] The selected asset is not an FBX.");
            return;
        }

        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("[FOTF Equipment] Could not get ModelImporter for: " + assetPath);
            return;
        }

        EnsureFolder(MaterialRoot);

        Dictionary<string, Material> materials =
            new Dictionary<string, Material>(StringComparer.Ordinal);

        foreach (MaterialDef definition in Definitions)
        {
            Material material = CreateOrUpdateMaterial(definition);
            materials[definition.Name] = material;
        }

        // Remap by the exact Blender material names exported in the FBX.
        foreach (KeyValuePair<string, Material> pair in materials)
        {
            AssetImporter.SourceAssetIdentifier source =
                new AssetImporter.SourceAssetIdentifier(
                    typeof(Material),
                    pair.Key);

            importer.AddRemap(source, pair.Value);
        }

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        Debug.Log(
            "[FOTF Equipment] Remapped equipment materials for [" +
            Path.GetFileName(assetPath) +
            "]. If any renderer is still white, expand the FBX and inspect " +
            "the renderer's source material name; it is then using a material " +
            "name not present in the FOTF remap table.");
    }

    [MenuItem("Tools/Fate of the Fallen/Equipment/Remap Selected FBX Materials", true)]
    private static bool ValidateRemapSelectedFbxMaterials()
    {
        if (Selection.activeObject == null)
            return false;

        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return !string.IsNullOrEmpty(path) &&
               path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
    }

    private static Material CreateOrUpdateMaterial(MaterialDef definition)
    {
        string path = MaterialRoot + "/" + definition.Name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        Shader shader = Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("Unity Standard shader was not found.");

        if (material == null)
        {
            material = new Material(shader);
            material.name = definition.Name;
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        material.SetColor("_Color", definition.BaseColor);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", 0.18f);

        if (definition.Name.Contains("Binding"))
        {
            material.SetFloat("_Metallic", 0.38f);
            material.SetFloat("_Glossiness", 0.26f);
        }

        if (definition.Emissive)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", definition.EmissionColor);
            material.globalIlluminationFlags =
                MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string fullPath)
    {
        string[] pieces = fullPath.Split('/');
        string current = pieces[0];

        for (int i = 1; i < pieces.Length; i++)
        {
            string next = current + "/" + pieces[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, pieces[i]);

            current = next;
        }
    }
}
#endif
