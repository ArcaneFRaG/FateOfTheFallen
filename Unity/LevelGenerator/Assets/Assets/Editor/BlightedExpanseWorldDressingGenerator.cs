using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen.EditorTools
{
    public class BlightedExpanseWorldDressingGenerator : EditorWindow
    {
        private const string RootFolder = "Assets/FateOfTheFallen";
        private const string GeneratedFolder = RootFolder + "/Generated/BlightedExpanse";
        private const string SceneFolder = RootFolder + "/Scenes";

        private const float WorldScale = 2f;
        private const float DesignSize = 1000f;
        private const float WorldSize = DesignSize * WorldScale;

        private int seed = 2407;
        private int meshResolution = 129;
        private float terrainHeight = 420f;
        private bool createNewScene = true;
        private bool saveScene = true;
        private bool addMeshCollider = true;
        private bool quantizeHeights = true;
        private float heightStep = 2.0f;
        private bool showZoneMarkers = true;
        private bool generateSubzoneBlockouts = true;
        private bool generateTransitionLandmarks = true;
        private bool generateWorldDressing = true;

        private readonly List<GameObject> rockPrefabs = new List<GameObject>();
        private readonly List<GameObject> deadTreePrefabs = new List<GameObject>();
        private readonly List<GameObject> ruinPrefabs = new List<GameObject>();
        private readonly List<GameObject> blightPrefabs = new List<GameObject>();

        private int rockCount = 190;
        private int deadTreeCount = 155;
        private int blightCount = 135;
        private int ruinCount = 28;
        private float minPrefabScale = 0.85f;
        private float maxPrefabScale = 1.35f;
        private bool alignToGroundNormal = true;

        private struct LocationDef
        {
            public string Name;
            public Vector2 P;

            public LocationDef(string name, float x, float z)
            {
                Name = name;
                P = new Vector2(x, z);
            }
        }

        private static readonly LocationDef[] Locations =
        {
            new LocationDef("Realm_Entry",            500f,  85f),
            new LocationDef("Withered_March",         500f, 235f),
            new LocationDef("Blightscar_Basin",       500f, 430f),
            new LocationDef("Rotwood",                270f, 505f),
            new LocationDef("Ruined_Sanctum",         735f, 500f),
            new LocationDef("Plaguefields",           515f, 650f),
            new LocationDef("Lord_of_Blight_Gateway", 500f, 885f),
        };

        private static readonly Vector2[] MainRoute =
        {
            new Vector2(500f,  85f),
            new Vector2(500f, 155f),
            new Vector2(500f, 235f),
            new Vector2(495f, 330f),
            new Vector2(500f, 430f),
            new Vector2(510f, 535f),
            new Vector2(515f, 650f),
            new Vector2(510f, 760f),
            new Vector2(500f, 885f),
        };

        private static readonly Vector2[] RotwoodRoute =
        {
            new Vector2(500f, 430f),
            new Vector2(420f, 455f),
            new Vector2(350f, 480f),
            new Vector2(270f, 505f),
        };

        private static readonly Vector2[] SanctumRoute =
        {
            new Vector2(500f, 430f),
            new Vector2(585f, 455f),
            new Vector2(660f, 480f),
            new Vector2(735f, 500f),
        };

        [MenuItem("Tools/Fate of the Fallen/Generate Blighted Expanse - World Dressing")]
        public static void OpenWindow()
        {
            GetWindow<BlightedExpanseWorldDressingGenerator>("Blighted Expanse Dressing");
        }

        private void OnGUI()
        {
            GUILayout.Label("The Blighted Expanse - Low Poly Terrain", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            seed = EditorGUILayout.IntField("Seed", seed);

            meshResolution = EditorGUILayout.IntPopup(
                "Mesh Resolution",
                meshResolution,
                new[] { "65 (Very Chunky)", "97 (Chunky)", "129 (Recommended)", "161 (Finer)" },
                new[] { 65, 97, 129, 161 });

            terrainHeight = EditorGUILayout.Slider("Terrain Height", terrainHeight, 250f, 650f);

            quantizeHeights = EditorGUILayout.Toggle("Quantize Heights", quantizeHeights);
            using (new EditorGUI.DisabledScope(!quantizeHeights))
            {
                heightStep = EditorGUILayout.Slider("Height Step", heightStep, 0.5f, 8f);
            }

            addMeshCollider = EditorGUILayout.Toggle("Add Mesh Collider", addMeshCollider);
            showZoneMarkers = EditorGUILayout.Toggle("Show Zone Markers", showZoneMarkers);
            generateSubzoneBlockouts = EditorGUILayout.Toggle("Generate Subzone Blockouts", generateSubzoneBlockouts);
            generateTransitionLandmarks = EditorGUILayout.Toggle("Generate Transition Landmarks", generateTransitionLandmarks);
            generateWorldDressing = EditorGUILayout.Toggle("Generate World Dressing", generateWorldDressing);

            EditorGUILayout.Space();
            GUILayout.Label("World Dressing Prefabs", EditorStyles.boldLabel);

            DrawPrefabList("Rock Prefabs", rockPrefabs);
            DrawPrefabList("Dead Tree Prefabs", deadTreePrefabs);
            DrawPrefabList("Ruin Prefabs", ruinPrefabs);
            DrawPrefabList("Blight Prefabs", blightPrefabs);

            EditorGUILayout.Space();
            rockCount = EditorGUILayout.IntSlider("Rock Count", rockCount, 0, 600);
            deadTreeCount = EditorGUILayout.IntSlider("Dead Tree Count", deadTreeCount, 0, 500);
            blightCount = EditorGUILayout.IntSlider("Blight Count", blightCount, 0, 500);
            ruinCount = EditorGUILayout.IntSlider("Ruin Count", ruinCount, 0, 100);
            minPrefabScale = EditorGUILayout.Slider("Minimum Scale", minPrefabScale, 0.25f, 2.0f);
            maxPrefabScale = EditorGUILayout.Slider("Maximum Scale", maxPrefabScale, 0.25f, 3.0f);
            alignToGroundNormal = EditorGUILayout.Toggle("Align To Ground", alignToGroundNormal);
            createNewScene = EditorGUILayout.Toggle("Create New Scene", createNewScene);
            saveScene = EditorGUILayout.Toggle("Save Scene", saveScene);

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Creates a true low-poly 2000x2000 terrain mesh with flat-shaded triangle faces. " +
                "129 resolution gives roughly one sample every 15.6 world units. " +
                "The mesh uses four simple material regions for readability. The terrain follows a central south-to-north progression spine, distinct subzones, transition gates, and optional prefab-driven world dressing. Rock, dead-tree, ruin, and blight prefabs are scattered with region-specific rules while keeping the main route and landmark clearings readable.",
                MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate Low-Poly Level", GUILayout.Height(44f)))
                Generate();
        }


        private void DrawPrefabList(string label, List<GameObject> list)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            int removeIndex = -1;

            for (int i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                list[i] = (GameObject)EditorGUILayout.ObjectField(
                    list[i],
                    typeof(GameObject),
                    false);

                if (GUILayout.Button("-", GUILayout.Width(24f)))
                    removeIndex = i;

                EditorGUILayout.EndHorizontal();
            }

            if (removeIndex >= 0)
                list.RemoveAt(removeIndex);

            if (GUILayout.Button("+ Add Prefab"))
                list.Add(null);

            EditorGUILayout.EndVertical();
        }

        private void Generate()
        {
            try
            {
                if (createNewScene)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        return;

                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }

                EnsureFolder(RootFolder);
                EnsureFolder(GeneratedFolder);
                EnsureFolder(SceneFolder);

                GameObject oldRoot = GameObject.Find("The_Blighted_Expanse");
                if (oldRoot != null)
                {
                    if (!EditorUtility.DisplayDialog(
                            "Replace Existing Generated Level?",
                            "A The_Blighted_Expanse root already exists in this scene.",
                            "Replace",
                            "Cancel"))
                        return;

                    DestroyImmediate(oldRoot);
                }

                GameObject root = new GameObject("The_Blighted_Expanse");
                Undo.RegisterCreatedObjectUndo(root, "Generate Blighted Expanse Low Poly");

                Transform environment = CreateGroup(root.transform, "Environment");
                Transform locations = CreateGroup(root.transform, "Locations");
                Transform gameplay = CreateGroup(root.transform, "Gameplay");
                Transform navigation = CreateGroup(root.transform, "Navigation");
                Transform lighting = CreateGroup(root.transform, "Lighting");

                CreateLowPolyTerrain(environment);
                CreateLocationTransforms(locations);
                if (generateSubzoneBlockouts)
                    CreateSubzoneBlockouts(environment);
                if (generateTransitionLandmarks)
                    CreateTransitionLandmarks(environment);
                if (generateWorldDressing)
                    CreateWorldDressing(environment);
                CreateGameplayTransforms(gameplay);
                CreateNavigationTransforms(navigation);
                CreateLighting(lighting);

                Selection.activeGameObject = root;
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

                if (saveScene)
                {
                    string scenePath = SceneFolder + "/The_Blighted_Expanse_LowPoly.unity";
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog(
                    "Low-Poly Terrain Complete",
                    "Generated the 2000x2000 low-poly terrain mesh for The Blighted Expanse.",
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Generation Failed", ex.Message, "OK");
            }
        }

        private void CreateLowPolyTerrain(Transform parent)
        {
            string meshPath = GeneratedFolder + "/BlightedExpanse_LowPoly.asset";
            Mesh oldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (oldMesh != null)
                AssetDatabase.DeleteAsset(meshPath);

            Mesh mesh = BuildFlatMesh();
            mesh.name = "BlightedExpanse_LowPoly";
            AssetDatabase.CreateAsset(mesh, meshPath);

            Material[] materials = CreateTerrainMaterials();

            GameObject terrainGO = new GameObject("LowPoly_Terrain");
            terrainGO.transform.SetParent(parent, false);

            MeshFilter filter = terrainGO.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = terrainGO.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            if (addMeshCollider)
            {
                MeshCollider collider = terrainGO.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }
        }

        private Mesh BuildFlatMesh()
        {
            int cells = meshResolution - 1;

            // Each grid cell = 2 triangles = 6 unique vertices.
            int vertexCount = cells * cells * 6;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];

            List<int>[] submeshTriangles =
            {
                new List<int>(), // Dead Soil
                new List<int>(), // Blight
                new List<int>(), // Stone
                new List<int>(), // Cracked Earth
            };

            int v = 0;

            for (int z = 0; z < cells; z++)
            {
                float z0d = z / (float)cells * DesignSize;
                float z1d = (z + 1) / (float)cells * DesignSize;

                for (int x = 0; x < cells; x++)
                {
                    float x0d = x / (float)cells * DesignSize;
                    float x1d = (x + 1) / (float)cells * DesignSize;

                    Vector3 p00 = MakeWorldVertex(x0d, z0d);
                    Vector3 p10 = MakeWorldVertex(x1d, z0d);
                    Vector3 p01 = MakeWorldVertex(x0d, z1d);
                    Vector3 p11 = MakeWorldVertex(x1d, z1d);

                    // Alternate diagonal direction to avoid a strong repeating visual bias.
                    bool flip = ((x + z) & 1) == 1;

                    if (!flip)
                    {
                        AddTriangle(vertices, normals, uvs, submeshTriangles, ref v, p00, p01, p11);
                        AddTriangle(vertices, normals, uvs, submeshTriangles, ref v, p00, p11, p10);
                    }
                    else
                    {
                        AddTriangle(vertices, normals, uvs, submeshTriangles, ref v, p00, p01, p10);
                        AddTriangle(vertices, normals, uvs, submeshTriangles, ref v, p10, p01, p11);
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.subMeshCount = submeshTriangles.Length;

            for (int i = 0; i < submeshTriangles.Length; i++)
                mesh.SetTriangles(submeshTriangles[i], i, false);

            mesh.RecalculateBounds();
            return mesh;
        }

        private void AddTriangle(
            Vector3[] vertices,
            Vector3[] normals,
            Vector2[] uvs,
            List<int>[] submeshTriangles,
            ref int v,
            Vector3 a,
            Vector3 b,
            Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

            // Ensure normals face upward.
            if (normal.y < 0f)
            {
                Vector3 tmp = b;
                b = c;
                c = tmp;
                normal = Vector3.Cross(b - a, c - a).normalized;
            }

            vertices[v] = a;
            vertices[v + 1] = b;
            vertices[v + 2] = c;

            normals[v] = normal;
            normals[v + 1] = normal;
            normals[v + 2] = normal;

            uvs[v] = new Vector2(a.x / WorldSize, a.z / WorldSize);
            uvs[v + 1] = new Vector2(b.x / WorldSize, b.z / WorldSize);
            uvs[v + 2] = new Vector2(c.x / WorldSize, c.z / WorldSize);

            Vector3 centroid = (a + b + c) / 3f;
            int materialIndex = ClassifyMaterial(centroid, normal);

            submeshTriangles[materialIndex].Add(v);
            submeshTriangles[materialIndex].Add(v + 1);
            submeshTriangles[materialIndex].Add(v + 2);

            v += 3;
        }

        private Vector3 MakeWorldVertex(float designX, float designZ)
        {
            float h01 = Height01(designX, designZ);
            float worldY = h01 * terrainHeight;

            if (quantizeHeights && heightStep > 0.001f)
                worldY = Mathf.Round(worldY / heightStep) * heightStep;

            return new Vector3(
                designX * WorldScale,
                worldY,
                designZ * WorldScale);
        }

        private int ClassifyMaterial(Vector3 worldPos, Vector3 normal)
        {
            float dx = worldPos.x / WorldScale;
            float dz = worldPos.z / WorldScale;
            float slope = 1f - Mathf.Clamp01(normal.y);

            if (slope > 0.34f || worldPos.y > terrainHeight * 0.31f)
                return 2; // Ash / stone

            float blight =
                Radial(dx, dz, 500f, 430f, 215f, 1f) +
                Radial(dx, dz, 515f, 650f, 185f, 0.75f) +
                Radial(dx, dz, 270f, 505f, 155f, 0.48f);

            float cracked =
                Radial(dx, dz, 735f, 500f, 145f, 0.90f) +
                Radial(dx, dz, 500f, 885f, 145f, 0.80f) +
                RouteMask(dx, dz, MainRoute, 34f) * 0.65f;

            if (cracked > 0.62f)
                return 3;

            if (blight > 0.58f)
                return 1;

            return 0;
        }

        private Material[] CreateTerrainMaterials()
        {
            return new[]
            {
                CreateOrReplaceMaterial("LP_DeadSoil",     new Color(0.29f, 0.27f, 0.20f), 0.04f),
                CreateOrReplaceMaterial("LP_BlightGrowth", new Color(0.25f, 0.36f, 0.16f), 0.03f),
                CreateOrReplaceMaterial("LP_AshStone",     new Color(0.34f, 0.35f, 0.32f), 0.02f),
                CreateOrReplaceMaterial("LP_CrackedEarth", new Color(0.22f, 0.17f, 0.13f), 0.02f),
            };
        }

        private Material CreateOrReplaceMaterial(string name, Color color, float smoothness)
        {
            string path = GeneratedFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                existing.color = color;
                if (existing.HasProperty("_Glossiness"))
                    existing.SetFloat("_Glossiness", smoothness);

                EditorUtility.SetDirty(existing);
                return existing;
            }

            Shader shader = Shader.Find("Standard");
            if (shader == null)
                throw new Exception("Could not find Unity Standard shader.");

            Material mat = new Material(shader);
            mat.name = name;
            mat.color = color;

            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", smoothness);

            mat.SetFloat("_Metallic", 0f);

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private float Height01(float dx, float dz)
        {
            float nx = dx / DesignSize;
            float nz = dz / DesignSize;

            float ox = seed * 0.137f;
            float oz = seed * 0.271f;

            float h = 0.105f;

            h += (Mathf.PerlinNoise(ox + nx * 2.1f, oz + nz * 2.1f) - 0.5f) * 0.095f;
            h += (Mathf.PerlinNoise(ox + 31f + nx * 5.2f, oz + 47f + nz * 5.2f) - 0.5f) * 0.048f;
            h += (Mathf.PerlinNoise(ox + 83f + nx * 12.5f, oz + 19f + nz * 12.5f) - 0.5f) * 0.016f;

            float edgeDistance = Mathf.Min(
                Mathf.Min(dx, DesignSize - dx),
                Mathf.Min(dz, DesignSize - dz));

            float edgeMask = 1f - Smooth01(35f, 155f, edgeDistance);

            float edgeNoise = Mathf.Lerp(
                0.75f,
                1.20f,
                Mathf.PerlinNoise(ox + nx * 4.0f, oz + nz * 4.0f));

            h += edgeMask * edgeMask * 0.38f * edgeNoise;

            h += Ridge(dx, dz, new Vector2(70f, 545f), new Vector2(180f, 890f), 75f, 0.22f);
            h += Ridge(dx, dz, new Vector2(160f, 865f), new Vector2(450f, 950f), 95f, 0.20f);

            h += Ridge(dx, dz, new Vector2(760f, 120f), new Vector2(930f, 390f), 80f, 0.25f);
            h += Ridge(dx, dz, new Vector2(875f, 360f), new Vector2(960f, 650f), 90f, 0.20f);

            // Flanking ridge systems frame the central north-south progression path.
            h += Ridge(dx, dz, new Vector2(320f, 220f), new Vector2(360f, 430f), 62f, 0.145f);
            h += Ridge(dx, dz, new Vector2(680f, 220f), new Vector2(640f, 430f), 62f, 0.145f);

            h += Ridge(dx, dz, new Vector2(350f, 585f), new Vector2(390f, 835f), 70f, 0.155f);
            h += Ridge(dx, dz, new Vector2(650f, 585f), new Vector2(610f, 835f), 70f, 0.155f);

            h -= Radial(dx, dz, 500f, 85f, 145f, 0.110f);
            h -= Radial(dx, dz, 500f, 235f, 150f, 0.070f);

            h -= Radial(dx, dz, 500f, 430f, 180f, 0.180f);
            h -= Radial(dx, dz, 500f, 430f, 82f, 0.060f);

            h += Radial(dx, dz, 270f, 505f, 165f, 0.070f);

            float sanctumPlateau = Radial(dx, dz, 735f, 500f, 125f, 1f);
            h = Mathf.Lerp(h, 0.265f, sanctumPlateau * 0.68f);
            h += Ridge(dx, dz, new Vector2(650f, 445f), new Vector2(790f, 540f), 58f, 0.075f);

            h -= Radial(dx, dz, 515f, 650f, 170f, 0.070f);

            float gatewayPlateau = Radial(dx, dz, 500f, 885f, 130f, 1f);
            h = Mathf.Lerp(h, 0.345f, gatewayPlateau * 0.80f);

            h += Ridge(dx, dz, new Vector2(475f, 700f), new Vector2(500f, 870f), 95f, 0.075f);

            h -= Radial(dx, dz, 500f, 335f, 62f, 0.110f);
            h -= Radial(dx, dz, 620f, 460f, 52f, 0.095f);
            h -= Radial(dx, dz, 500f, 760f, 62f, 0.100f);

            h = CarveRoute(h, dx, dz, MainRoute, 28f, 0.105f, 0.52f);
            h = CarveRoute(h, dx, dz, RotwoodRoute, 24f, 0.115f, 0.44f);
            h = CarveRoute(h, dx, dz, SanctumRoute, 24f, 0.120f, 0.46f);

            h = FlattenLocal(h, dx, dz, 500f, 85f, 54f, 0.095f, 0.78f);
            h = FlattenLocal(h, dx, dz, 500f, 235f, 70f, 0.110f, 0.58f);
            h = FlattenLocal(h, dx, dz, 500f, 430f, 78f, 0.060f, 0.50f);
            h = FlattenLocal(h, dx, dz, 735f, 500f, 66f, 0.255f, 0.76f);
            h = FlattenLocal(h, dx, dz, 515f, 650f, 78f, 0.110f, 0.54f);
            h = FlattenLocal(h, dx, dz, 500f, 885f, 62f, 0.340f, 0.82f);

            // --- Subzone identity shaping ---

            // Rotwood: enclosed, lumpy shelf with shallow pockets.
            h += Radial(dx, dz, 270f, 505f, 150f, 0.025f);
            h += (Mathf.PerlinNoise(ox + dx * 0.018f, oz + dz * 0.018f) - 0.5f)
                 * Radial(dx, dz, 270f, 505f, 150f, 0.045f);
            h -= Radial(dx, dz, 230f, 540f, 42f, 0.025f);
            h -= Radial(dx, dz, 310f, 470f, 36f, 0.020f);

            // Blightscar Basin: crater lip + deeper corrupted core.
            float basinRingOuter = Radial(dx, dz, 500f, 430f, 190f, 1f);
            float basinRingInner = Radial(dx, dz, 500f, 430f, 125f, 1f);
            h += Mathf.Max(0f, basinRingOuter - basinRingInner) * 0.055f;
            h -= Radial(dx, dz, 500f, 430f, 58f, 0.045f);

            // Ruined Sanctum: stepped / terraced plateau.
            float sanctumDist = Vector2.Distance(new Vector2(dx, dz), new Vector2(735f, 500f));
            if (sanctumDist < 120f)
            {
                float terraced = Mathf.Round(h * terrainHeight / 7f) * 7f / terrainHeight;
                float terracedBlend = Mathf.Clamp01((120f - sanctumDist) / 85f) * 0.48f;
                h = Mathf.Lerp(h, terraced, terracedBlend);
            }

            // Plaguefields: broad open flats with shallow drainage scars.
            h = FlattenLocal(h, dx, dz, 515f, 650f, 125f, 0.115f, 0.20f);
            h -= Ridge(dx, dz, new Vector2(445f, 610f), new Vector2(570f, 700f), 18f, 0.018f);
            h -= Ridge(dx, dz, new Vector2(470f, 700f), new Vector2(555f, 620f), 15f, 0.014f);

            // Final approach: increasingly harsh lateral ridges framing the ascent.
            h += Ridge(dx, dz, new Vector2(420f, 720f), new Vector2(445f, 870f), 42f, 0.080f);
            h += Ridge(dx, dz, new Vector2(580f, 720f), new Vector2(555f, 870f), 42f, 0.080f);

            // --- Transition shaping between subzones ---

            // Entry -> Withered March: broad shallow corridor.
            h -= Ridge(dx, dz, new Vector2(500f, 105f), new Vector2(500f, 215f), 34f, 0.030f);

            // Withered March -> Basin: terrain tightens to form a recognizable pass.
            h += Ridge(dx, dz, new Vector2(430f, 285f), new Vector2(445f, 390f), 36f, 0.060f);
            h += Ridge(dx, dz, new Vector2(570f, 285f), new Vector2(555f, 390f), 36f, 0.060f);
            h -= Ridge(dx, dz, new Vector2(500f, 285f), new Vector2(500f, 400f), 26f, 0.025f);

            // Basin -> Plaguefields: basin exit opens gradually instead of snapping to flat ground.
            h -= Ridge(dx, dz, new Vector2(500f, 500f), new Vector2(515f, 610f), 40f, 0.025f);

            // Plaguefields -> Final approach: narrow ritual ascent.
            h += Ridge(dx, dz, new Vector2(430f, 690f), new Vector2(455f, 805f), 38f, 0.050f);
            h += Ridge(dx, dz, new Vector2(570f, 690f), new Vector2(545f, 805f), 38f, 0.050f);
            h -= Ridge(dx, dz, new Vector2(500f, 690f), new Vector2(500f, 820f), 24f, 0.022f);

            // Small irregularity on otherwise traversable central path so it feels natural.
            float spineMask = RouteMask(dx, dz, MainRoute, 42f);
            float spineNoise = (Mathf.PerlinNoise(ox + dx * 0.012f, oz + dz * 0.012f) - 0.5f) * 0.018f;
            h += spineNoise * spineMask * 0.45f;

            return Mathf.Clamp01(h);
        }

        private void CreateLocationTransforms(Transform parent)
        {
            Material markerMaterial = CreateOrReplaceMaterial(
                "LP_ZoneMarker",
                new Color(0.55f, 0.24f, 0.68f),
                0.02f);

            foreach (LocationDef def in Locations)
            {
                GameObject location = new GameObject(def.Name);
                location.transform.SetParent(parent, false);

                Vector3 pos = DesignToWorld(def.P);
                location.transform.position = pos;

                GameObject center = new GameObject("Center");
                center.transform.SetParent(location.transform, false);

                if (!showZoneMarkers)
                    continue;

                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "_ZONE_MARKER";
                marker.transform.SetParent(location.transform, false);
                marker.transform.localPosition = new Vector3(0f, 12f, 0f);
                marker.transform.localScale = new Vector3(10f, 12f, 10f);

                Renderer renderer = marker.GetComponent<Renderer>();
                renderer.sharedMaterial = markerMaterial;

                Collider col = marker.GetComponent<Collider>();
                if (col != null)
                    DestroyImmediate(col);

                GameObject labelGO = new GameObject("_ZONE_LABEL");
                labelGO.transform.SetParent(location.transform, false);
                labelGO.transform.localPosition = new Vector3(0f, 32f, 0f);

                TextMesh label = labelGO.AddComponent<TextMesh>();
                label.text = def.Name.Replace("_", " ");
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.fontSize = 72;
                label.characterSize = 0.45f;
                label.color = Color.white;

                // Make labels readable from the default editor perspective.
                labelGO.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
            }
        }


        private void CreateSubzoneBlockouts(Transform parent)
        {
            Transform root = CreateGroup(parent, "Subzone_Blockouts");

            Material stone = CreateOrReplaceMaterial(
                "BLK_Stone",
                new Color(0.30f, 0.31f, 0.28f),
                0.02f);

            Material wood = CreateOrReplaceMaterial(
                "BLK_DeadWood",
                new Color(0.20f, 0.15f, 0.10f),
                0.01f);

            Material blight = CreateOrReplaceMaterial(
                "BLK_Blight",
                new Color(0.28f, 0.43f, 0.16f),
                0.02f);

            Material ruin = CreateOrReplaceMaterial(
                "BLK_Ruin",
                new Color(0.38f, 0.36f, 0.32f),
                0.02f);

            Material gateway = CreateOrReplaceMaterial(
                "BLK_Gateway",
                new Color(0.23f, 0.17f, 0.25f),
                0.02f);

            CreateRealmEntryBlockout(root, stone);
            CreateWitheredMarchBlockout(root, stone, blight);
            CreateRotwoodBlockout(root, wood, blight);
            CreateBasinBlockout(root, stone, blight);
            CreateSanctumBlockout(root, ruin);
            CreatePlaguefieldsBlockout(root, stone, blight);
            CreateGatewayBlockout(root, gateway, stone);
        }

        private void CreateRealmEntryBlockout(Transform parent, Material stone)
        {
            Transform root = CreateGroup(parent, "Realm_Entry_Blockout");

            CreateBlockCube(root, "Entry_Pillar_L", new Vector2(470f, 105f), new Vector3(14f, 42f, 14f), stone, 0f);
            CreateBlockCube(root, "Entry_Pillar_R", new Vector2(530f, 105f), new Vector3(14f, 42f, 14f), stone, 0f);
            CreateBlockCube(root, "Entry_Lintel", new Vector2(500f, 105f), new Vector3(72f, 10f, 14f), stone, 0f, 38f);
        }

        private void CreateWitheredMarchBlockout(Transform parent, Material stone, Material blight)
        {
            Transform root = CreateGroup(parent, "Withered_March_Blockout");

            CreateBlockCube(root, "Broken_Wall_A", new Vector2(455f, 235f), new Vector3(52f, 18f, 10f), stone, -18f);
            CreateBlockCube(root, "Broken_Wall_B", new Vector2(550f, 265f), new Vector3(44f, 16f, 10f), stone, 24f);

            for (int i = 0; i < 8; i++)
            {
                float a = i * 47f;
                float r = 35f + (i % 3) * 13f;
                Vector2 p = new Vector2(500f + Mathf.Cos(a * Mathf.Deg2Rad) * r,
                                        235f + Mathf.Sin(a * Mathf.Deg2Rad) * r);
                CreateBlightSpire(root, "Blight_Spire_" + i, p, 7f + (i % 4) * 2f, blight);
            }
        }

        private void CreateRotwoodBlockout(Transform parent, Material wood, Material blight)
        {
            Transform root = CreateGroup(parent, "Rotwood_Blockout");

            for (int i = 0; i < 48; i++)
            {
                float angle = i * 137.5f;
                float radius = 30f + (i % 9) * 10f;
                Vector2 p = new Vector2(
                    270f + Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                    505f + Mathf.Sin(angle * Mathf.Deg2Rad) * radius);

                float height = 18f + (i % 6) * 4f;
                CreateDeadTree(root, "DeadTree_" + i, p, height, wood);
            }

            for (int i = 0; i < 14; i++)
            {
                float angle = i * 36f;
                Vector2 p = new Vector2(
                    270f + Mathf.Cos(angle * Mathf.Deg2Rad) * 78f,
                    505f + Mathf.Sin(angle * Mathf.Deg2Rad) * 78f);
                CreateBlightSpire(root, "Rotwood_Blight_" + i, p, 5f + (i % 3) * 2f, blight);
            }
        }

        private void CreateBasinBlockout(Transform parent, Material stone, Material blight)
        {
            Transform root = CreateGroup(parent, "Blightscar_Basin_Blockout");

            for (int i = 0; i < 18; i++)
            {
                float angle = i * 20f;
                float radius = 72f + (i % 3) * 10f;
                Vector2 p = new Vector2(
                    500f + Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                    430f + Mathf.Sin(angle * Mathf.Deg2Rad) * radius);

                CreateBlockCube(
                    root,
                    "CraterRock_" + i,
                    p,
                    new Vector3(11f + (i % 4) * 3f, 18f + (i % 5) * 4f, 12f + (i % 3) * 4f),
                    stone,
                    angle + 25f);
            }

            for (int i = 0; i < 14; i++)
            {
                float angle = i * 31f;
                float radius = 18f + (i % 5) * 9f;
                Vector2 p = new Vector2(
                    500f + Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                    430f + Mathf.Sin(angle * Mathf.Deg2Rad) * radius);

                CreateBlightSpire(root, "Core_Blight_" + i, p, 8f + (i % 4) * 3f, blight);
            }

            // Central blight wound / monolith stand-in.
            CreateBlockCube(
                root,
                "Blightscar_Core_Monolith",
                new Vector2(500f, 430f),
                new Vector3(14f, 46f, 14f),
                blight,
                18f);
        }

        private void CreateSanctumBlockout(Transform parent, Material ruin)
        {
            Transform root = CreateGroup(parent, "Ruined_Sanctum_Blockout");

            // Central ruined hall footprint.
            CreateBlockCube(root, "Sanctum_Floor", new Vector2(735f, 500f), new Vector3(88f, 3f, 64f), ruin, 0f);

            CreateBlockCube(root, "Wall_North", new Vector2(735f, 530f), new Vector3(88f, 24f, 8f), ruin, 0f);
            CreateBlockCube(root, "Wall_West", new Vector2(695f, 500f), new Vector3(8f, 26f, 64f), ruin, 0f);
            CreateBlockCube(root, "Wall_East_Broken", new Vector2(775f, 510f), new Vector3(8f, 18f, 42f), ruin, 0f);

            CreateBrokenArch(root, "Sanctum_Arch", new Vector2(735f, 470f), 30f, 34f, ruin);
        }

        private void CreatePlaguefieldsBlockout(Transform parent, Material stone, Material blight)
        {
            Transform root = CreateGroup(parent, "Plaguefields_Blockout");

            for (int i = 0; i < 28; i++)
            {
                float angle = i * 47f;
                float radius = 28f + (i % 10) * 11f;
                Vector2 p = new Vector2(
                    515f + Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                    650f + Mathf.Sin(angle * Mathf.Deg2Rad) * radius);

                if ((i % 3) == 0)
                    CreateBlightSpire(root, "Field_Blight_" + i, p, 4f + (i % 5) * 2f, blight);
                else
                    CreateBlockCube(root, "Field_Rock_" + i, p, new Vector3(8f, 7f + (i % 4) * 3f, 8f), stone, angle);
            }

            // Two ruined field markers to give the area readable anchors.
            CreateBrokenArch(root, "Field_Arch_West", new Vector2(455f, 665f), 22f, 24f, stone);
            CreateBrokenArch(root, "Field_Arch_East", new Vector2(575f, 625f), 20f, 22f, stone);
        }

        private void CreateGatewayBlockout(Transform parent, Material gateway, Material stone)
        {
            Transform root = CreateGroup(parent, "Lord_of_Blight_Gateway_Blockout");

            CreateBlockCube(root, "Gateway_Pylon_L", new Vector2(455f, 885f), new Vector3(26f, 95f, 30f), gateway, -5f);
            CreateBlockCube(root, "Gateway_Pylon_R", new Vector2(545f, 885f), new Vector3(26f, 95f, 30f), gateway, 5f);
            CreateBlockCube(root, "Gateway_Lintel", new Vector2(500f, 885f), new Vector3(120f, 24f, 30f), gateway, 0f, 86f);

            CreateBlockCube(root, "Approach_Platform", new Vector2(500f, 835f), new Vector3(130f, 8f, 90f), stone, 0f);
        }

        private void CreateDeadTree(Transform parent, string name, Vector2 designPos, float height, Material material)
        {
            Vector3 pos = DesignToWorld(designPos);

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = name;
            trunk.transform.SetParent(parent, false);
            trunk.transform.position = pos + new Vector3(0f, height * 0.5f, 0f);
            trunk.transform.localScale = new Vector3(2.2f, height * 0.5f, 2.2f);
            trunk.transform.rotation = Quaternion.Euler((designPos.x + designPos.y) % 9f - 4f, designPos.x % 180f, 0f);
            trunk.GetComponent<Renderer>().sharedMaterial = material;

            Collider c = trunk.GetComponent<Collider>();
            if (c != null)
                DestroyImmediate(c);

            // Two crude broken branches.
            for (int i = 0; i < 2; i++)
            {
                GameObject branch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                branch.name = "Branch";
                branch.transform.SetParent(trunk.transform, false);
                branch.transform.localPosition = new Vector3(0f, 0.45f - i * 0.28f, 0f);
                branch.transform.localScale = new Vector3(0.38f, 0.65f, 0.38f);
                branch.transform.localRotation = Quaternion.Euler(65f + i * 15f, i * 110f, 0f);
                branch.GetComponent<Renderer>().sharedMaterial = material;

                Collider bc = branch.GetComponent<Collider>();
                if (bc != null)
                    DestroyImmediate(bc);
            }
        }

        private void CreateBlightSpire(Transform parent, string name, Vector2 designPos, float height, Material material)
        {
            Vector3 pos = DesignToWorld(designPos);

            GameObject spire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spire.name = name;
            spire.transform.SetParent(parent, false);
            spire.transform.position = pos + new Vector3(0f, height * 0.5f, 0f);
            spire.transform.localScale = new Vector3(3.5f, height * 0.5f, 3.5f);
            spire.transform.rotation = Quaternion.Euler(-8f, designPos.x % 180f, 6f);
            spire.GetComponent<Renderer>().sharedMaterial = material;

            Collider c = spire.GetComponent<Collider>();
            if (c != null)
                DestroyImmediate(c);
        }

        private void CreateBrokenArch(
            Transform parent,
            string name,
            Vector2 designPos,
            float width,
            float height,
            Material material)
        {
            Transform root = CreateGroup(parent, name);

            float half = width * 0.5f;
            CreateBlockCube(root, "Pillar_L", new Vector2(designPos.x - half, designPos.y), new Vector3(8f, height, 8f), material, 0f);
            CreateBlockCube(root, "Pillar_R", new Vector2(designPos.x + half, designPos.y), new Vector3(8f, height * 0.72f, 8f), material, 4f);
            CreateBlockCube(root, "Lintel_Broken", new Vector2(designPos.x - 3f, designPos.y), new Vector3(width * 0.72f, 8f, 8f), material, -8f, height - 2f);
        }

        private void CreateBlockCube(
            Transform parent,
            string name,
            Vector2 designPos,
            Vector3 scale,
            Material material,
            float yaw,
            float yOffset = -1f)
        {
            Vector3 pos = DesignToWorld(designPos);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);

            float y = yOffset >= 0f ? yOffset : scale.y * 0.5f;
            go.transform.position = pos + new Vector3(0f, y, 0f);
            go.transform.localScale = scale;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.GetComponent<Renderer>().sharedMaterial = material;

            Collider c = go.GetComponent<Collider>();
            if (c != null)
                DestroyImmediate(c);
        }


        private void CreateTransitionLandmarks(Transform parent)
        {
            Transform root = CreateGroup(parent, "Transition_Landmarks");

            Material stone = CreateOrReplaceMaterial(
                "BLK_TransitionStone",
                new Color(0.27f, 0.28f, 0.25f),
                0.02f);

            Material dark = CreateOrReplaceMaterial(
                "BLK_TransitionDark",
                new Color(0.18f, 0.15f, 0.17f),
                0.02f);

            // Entry -> Withered March
            CreateBrokenArch(root, "Transition_Entry_March", new Vector2(500f, 165f), 26f, 28f, stone);

            // Withered March -> Basin
            CreateTransitionGate(root, "Transition_March_Basin", new Vector2(500f, 335f), 34f, 30f, stone);

            // Basin -> Plaguefields
            CreateTransitionGate(root, "Transition_Basin_Plaguefields", new Vector2(508f, 555f), 40f, 22f, stone);

            // Plaguefields -> Final approach
            CreateTransitionGate(root, "Transition_Plaguefields_Ascent", new Vector2(505f, 755f), 32f, 42f, dark);

            // Side-branch thresholds.
            CreateBrokenArch(root, "Transition_Basin_Rotwood", new Vector2(385f, 470f), 24f, 25f, stone);
            CreateBrokenArch(root, "Transition_Basin_Sanctum", new Vector2(620f, 470f), 24f, 25f, stone);
        }

        private void CreateTransitionGate(
            Transform parent,
            string name,
            Vector2 designPos,
            float width,
            float height,
            Material material)
        {
            Transform root = CreateGroup(parent, name);

            float half = width * 0.5f;

            CreateBlockCube(
                root,
                "Gate_Pillar_L",
                new Vector2(designPos.x - half, designPos.y),
                new Vector3(8f, height, 10f),
                material,
                -3f);

            CreateBlockCube(
                root,
                "Gate_Pillar_R",
                new Vector2(designPos.x + half, designPos.y),
                new Vector3(8f, height, 10f),
                material,
                3f);

            CreateBlockCube(
                root,
                "Gate_Lintel",
                designPos,
                new Vector3(width + 10f, 8f, 10f),
                material,
                0f,
                height - 2f);
        }


        private enum DressingRegion
        {
            General,
            Rotwood,
            Basin,
            Sanctum,
            Plaguefields,
            FinalApproach
        }

        private void CreateWorldDressing(Transform parent)
        {
            Transform root = CreateGroup(parent, "World_Dressing");

            System.Random rng = new System.Random(seed + 99173);

            ScatterCategory(
                root,
                "Rocks",
                rockPrefabs,
                rockCount,
                rng,
                DressingKind.Rock);

            ScatterCategory(
                root,
                "Dead_Trees",
                deadTreePrefabs,
                deadTreeCount,
                rng,
                DressingKind.DeadTree);

            ScatterCategory(
                root,
                "Blight_Growth",
                blightPrefabs,
                blightCount,
                rng,
                DressingKind.Blight);

            ScatterCategory(
                root,
                "Ruins",
                ruinPrefabs,
                ruinCount,
                rng,
                DressingKind.Ruin);
        }

        private enum DressingKind
        {
            Rock,
            DeadTree,
            Blight,
            Ruin
        }

        private void ScatterCategory(
            Transform root,
            string groupName,
            List<GameObject> prefabs,
            int targetCount,
            System.Random rng,
            DressingKind kind)
        {
            Transform group = CreateGroup(root, groupName);

            List<GameObject> validPrefabs = new List<GameObject>();
            for (int i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i] != null)
                    validPrefabs.Add(prefabs[i]);
            }

            // No prefab means skip cleanly rather than generating substitute primitives.
            if (validPrefabs.Count == 0 || targetCount <= 0)
                return;

            int placed = 0;
            int attempts = 0;
            int maxAttempts = Mathf.Max(500, targetCount * 30);

            while (placed < targetCount && attempts < maxAttempts)
            {
                attempts++;

                float dx = Mathf.Lerp(45f, 955f, (float)rng.NextDouble());
                float dz = Mathf.Lerp(45f, 955f, (float)rng.NextDouble());

                Vector2 p = new Vector2(dx, dz);

                DressingRegion region = GetDressingRegion(p);
                float acceptance = GetAcceptance(kind, region, p);

                if ((float)rng.NextDouble() > acceptance)
                    continue;

                // Preserve the readable central route.
                float mainRouteDistance = DistanceToRoute(p, MainRoute);
                float branchDistance = Mathf.Min(
                    DistanceToRoute(p, RotwoodRoute),
                    DistanceToRoute(p, SanctumRoute));

                float routeClearance = kind == DressingKind.Ruin ? 20f : 14f;
                if (mainRouteDistance < routeClearance || branchDistance < routeClearance * 0.80f)
                    continue;

                // Preserve important landmark centers.
                if (InsideProtectedLandmark(p))
                    continue;

                float slope = EstimateSlopeDegrees(dx, dz);

                if (!SlopeAllowed(kind, slope))
                    continue;

                GameObject prefab = validPrefabs[rng.Next(validPrefabs.Count)];
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

                if (instance == null)
                    continue;

                instance.name = prefab.name + "_Generated_" + placed.ToString("000");
                instance.transform.SetParent(group, true);

                Vector3 worldPos = new Vector3(
                    dx * WorldScale,
                    SampleWorldHeight(dx, dz),
                    dz * WorldScale);

                instance.transform.position = worldPos;

                Vector3 normal = EstimateGroundNormal(dx, dz);
                float yaw = Mathf.Lerp(0f, 360f, (float)rng.NextDouble());

                if (alignToGroundNormal)
                {
                    Quaternion groundRotation = Quaternion.FromToRotation(Vector3.up, normal);
                    instance.transform.rotation = groundRotation * Quaternion.Euler(0f, yaw, 0f);
                }
                else
                {
                    instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                }

                float scale = Mathf.Lerp(
                    Mathf.Min(minPrefabScale, maxPrefabScale),
                    Mathf.Max(minPrefabScale, maxPrefabScale),
                    (float)rng.NextDouble());

                // Kind-specific scale variation.
                if (kind == DressingKind.Rock)
                    scale *= Mathf.Lerp(0.75f, 1.45f, (float)rng.NextDouble());
                else if (kind == DressingKind.Blight)
                    scale *= Mathf.Lerp(0.70f, 1.25f, (float)rng.NextDouble());
                else if (kind == DressingKind.Ruin)
                    scale *= Mathf.Lerp(0.85f, 1.35f, (float)rng.NextDouble());

                instance.transform.localScale =
                    Vector3.Scale(instance.transform.localScale, Vector3.one * scale);

                placed++;
            }

            if (placed < targetCount)
            {
                Debug.LogWarning(
                    "[BlightedExpanse] Dressing group '" + groupName +
                    "' placed " + placed + " / " + targetCount +
                    ". Terrain/path restrictions rejected the remaining attempts.");
            }
        }

        private DressingRegion GetDressingRegion(Vector2 p)
        {
            if (Vector2.Distance(p, new Vector2(270f, 505f)) <= 165f)
                return DressingRegion.Rotwood;

            if (Vector2.Distance(p, new Vector2(500f, 430f)) <= 170f)
                return DressingRegion.Basin;

            if (Vector2.Distance(p, new Vector2(735f, 500f)) <= 145f)
                return DressingRegion.Sanctum;

            if (Vector2.Distance(p, new Vector2(515f, 650f)) <= 165f)
                return DressingRegion.Plaguefields;

            if (p.y >= 725f && Mathf.Abs(p.x - 500f) <= 170f)
                return DressingRegion.FinalApproach;

            return DressingRegion.General;
        }

        private float GetAcceptance(
            DressingKind kind,
            DressingRegion region,
            Vector2 p)
        {
            switch (kind)
            {
                case DressingKind.DeadTree:
                    if (region == DressingRegion.Rotwood) return 0.92f;
                    if (region == DressingRegion.Plaguefields) return 0.20f;
                    if (region == DressingRegion.Basin) return 0.10f;
                    if (region == DressingRegion.FinalApproach) return 0.05f;
                    return 0.16f;

                case DressingKind.Blight:
                    if (region == DressingRegion.Basin) return 0.88f;
                    if (region == DressingRegion.Plaguefields) return 0.72f;
                    if (region == DressingRegion.Rotwood) return 0.50f;
                    if (region == DressingRegion.FinalApproach) return 0.36f;
                    return 0.22f;

                case DressingKind.Ruin:
                    if (region == DressingRegion.Sanctum) return 0.88f;
                    if (region == DressingRegion.Plaguefields) return 0.28f;
                    if (region == DressingRegion.General && p.y > 180f && p.y < 720f) return 0.12f;
                    return 0.035f;

                case DressingKind.Rock:
                default:
                    if (region == DressingRegion.FinalApproach) return 0.90f;
                    if (region == DressingRegion.Basin) return 0.70f;
                    if (region == DressingRegion.Sanctum) return 0.55f;
                    if (region == DressingRegion.Rotwood) return 0.42f;
                    if (region == DressingRegion.Plaguefields) return 0.32f;
                    return 0.48f;
            }
        }

        private bool InsideProtectedLandmark(Vector2 p)
        {
            // Keep breathing room around intended gameplay/landmark centers.
            if (Vector2.Distance(p, new Vector2(500f, 85f)) < 65f) return true;
            if (Vector2.Distance(p, new Vector2(500f, 235f)) < 58f) return true;
            if (Vector2.Distance(p, new Vector2(500f, 430f)) < 52f) return true;
            if (Vector2.Distance(p, new Vector2(270f, 505f)) < 35f) return true;
            if (Vector2.Distance(p, new Vector2(735f, 500f)) < 58f) return true;
            if (Vector2.Distance(p, new Vector2(515f, 650f)) < 48f) return true;
            if (Vector2.Distance(p, new Vector2(500f, 885f)) < 82f) return true;

            return false;
        }

        private bool SlopeAllowed(DressingKind kind, float slope)
        {
            switch (kind)
            {
                case DressingKind.Ruin:
                    return slope <= 16f;

                case DressingKind.DeadTree:
                    return slope <= 28f;

                case DressingKind.Blight:
                    return slope <= 34f;

                case DressingKind.Rock:
                default:
                    return slope <= 48f;
            }
        }

        private float DistanceToRoute(Vector2 p, Vector2[] route)
        {
            float best = float.MaxValue;

            for (int i = 0; i < route.Length - 1; i++)
            {
                float d = DistanceToSegment(p, route[i], route[i + 1]);
                if (d < best)
                    best = d;
            }

            return best;
        }

        private float EstimateSlopeDegrees(float dx, float dz)
        {
            Vector3 normal = EstimateGroundNormal(dx, dz);
            return Vector3.Angle(normal, Vector3.up);
        }

        private Vector3 EstimateGroundNormal(float dx, float dz)
        {
            const float sample = 2.5f;

            float hL = SampleWorldHeight(Mathf.Max(0f, dx - sample), dz);
            float hR = SampleWorldHeight(Mathf.Min(DesignSize, dx + sample), dz);
            float hD = SampleWorldHeight(dx, Mathf.Max(0f, dz - sample));
            float hU = SampleWorldHeight(dx, Mathf.Min(DesignSize, dz + sample));

            Vector3 tangentX = new Vector3(sample * 2f * WorldScale, hR - hL, 0f);
            Vector3 tangentZ = new Vector3(0f, hU - hD, sample * 2f * WorldScale);

            Vector3 normal = Vector3.Cross(tangentZ, tangentX).normalized;

            if (normal.y < 0f)
                normal = -normal;

            return normal;
        }

        private void CreateGameplayTransforms(Transform parent)
        {
            Transform playerSpawns = CreateGroup(parent, "PlayerSpawns");
            Transform enemySpawns = CreateGroup(parent, "EnemySpawns");
            Transform questMarkers = CreateGroup(parent, "QuestMarkers");
            Transform worldEvents = CreateGroup(parent, "WorldEvents");

            CreateEmptyAt(playerSpawns, "PlayerSpawn_RealmEntry", new Vector2(500f, 70f));

            Vector2[] enemies =
            {
                new Vector2(455f, 180f),
                new Vector2(550f, 265f),
                new Vector2(230f, 470f),
                new Vector2(300f, 540f),
                new Vector2(450f, 405f),
                new Vector2(550f, 455f),
                new Vector2(700f, 475f),
                new Vector2(470f, 625f),
                new Vector2(555f, 700f),
                new Vector2(520f, 825f),
            };

            for (int i = 0; i < enemies.Length; i++)
                CreateEmptyAt(enemySpawns, "EnemySpawn_" + (i + 1).ToString("00"), enemies[i]);

            CreateEmptyAt(questMarkers, "Quest_RuinedSanctum", new Vector2(735f, 500f));
            CreateEmptyAt(worldEvents, "Event_BlightscarBasin", new Vector2(500f, 430f));
        }

        private void CreateNavigationTransforms(Transform parent)
        {
            Transform routes = CreateGroup(parent, "Routes");
            CreateRouteObjects(routes, "MainRoute", MainRoute);
            CreateRouteObjects(routes, "RotwoodBranch", RotwoodRoute);
            CreateRouteObjects(routes, "SanctumBranch", SanctumRoute);

            CreateGroup(parent, "PatrolRoutes");
            CreateGroup(parent, "NavMesh");
        }

        private void CreateRouteObjects(Transform parent, string name, Vector2[] points)
        {
            Transform route = CreateGroup(parent, name);

            for (int i = 0; i < points.Length; i++)
                CreateEmptyAt(route, "Point_" + (i + 1).ToString("00"), points[i]);
        }

        private void CreateLighting(Transform parent)
        {
            GameObject sun = new GameObject("Blighted_Sun");
            sun.transform.SetParent(parent, false);
            sun.transform.rotation = Quaternion.Euler(34f, -31f, 0f);

            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.82f;
            light.color = new Color(0.72f, 0.78f, 0.63f);
            light.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.18f, 0.21f, 0.16f);
            RenderSettings.ambientEquatorColor = new Color(0.11f, 0.13f, 0.10f);
            RenderSettings.ambientGroundColor = new Color(0.055f, 0.065f, 0.05f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0032f;
            RenderSettings.fogColor = new Color(0.16f, 0.19f, 0.14f);
        }

        private float SampleWorldHeight(float designX, float designZ)
        {
            float y = Height01(designX, designZ) * terrainHeight;

            if (quantizeHeights && heightStep > 0.001f)
                y = Mathf.Round(y / heightStep) * heightStep;

            return y;
        }

        private static float CarveRoute(
            float currentHeight,
            float x,
            float z,
            Vector2[] route,
            float halfWidth,
            float targetHeight,
            float strength)
        {
            float mask = RouteMask(x, z, route, halfWidth);

            if (mask <= 0f)
                return currentHeight;

            float target = Mathf.Min(currentHeight, targetHeight + 0.02f);
            return Mathf.Lerp(currentHeight, target, mask * strength);
        }

        private static float FlattenLocal(
            float currentHeight,
            float x,
            float z,
            float cx,
            float cz,
            float radius,
            float target,
            float strength)
        {
            float d = Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz));

            if (d >= radius)
                return currentHeight;

            float t = 1f - d / radius;
            t = t * t * (3f - 2f * t);

            return Mathf.Lerp(currentHeight, target, t * strength);
        }

        private static float RouteMask(float x, float z, Vector2[] route, float halfWidth)
        {
            float minDistance = float.MaxValue;
            Vector2 p = new Vector2(x, z);

            for (int i = 0; i < route.Length - 1; i++)
            {
                float d = DistanceToSegment(p, route[i], route[i + 1]);
                if (d < minDistance)
                    minDistance = d;
            }

            if (minDistance >= halfWidth)
                return 0f;

            float t = 1f - minDistance / halfWidth;
            return t * t * (3f - 2f * t);
        }

        private static float Ridge(
            float x,
            float z,
            Vector2 a,
            Vector2 b,
            float width,
            float height)
        {
            float d = DistanceToSegment(new Vector2(x, z), a, b);

            if (d >= width)
                return 0f;

            float t = 1f - d / width;
            t = t * t * (3f - 2f * t);

            return t * height;
        }

        private static float Radial(
            float x,
            float z,
            float cx,
            float cz,
            float radius,
            float amount)
        {
            float d = Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz));

            if (d >= radius)
                return 0f;

            float t = 1f - d / radius;
            t = t * t * (3f - 2f * t);

            return t * amount;
        }

        private static float Smooth01(float a, float b, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, value));
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float sqr = ab.sqrMagnitude;

            if (sqr <= 0.0001f)
                return Vector2.Distance(p, a);

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / sqr);
            return Vector2.Distance(p, a + ab * t);
        }

        private Vector3 DesignToWorld(Vector2 p)
        {
            return new Vector3(
                p.x * WorldScale,
                SampleWorldHeight(p.x, p.y),
                p.y * WorldScale);
        }

        private void CreateEmptyAt(Transform parent, string name, Vector2 designPos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = DesignToWorld(designPos);
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');

            if (parts.Length == 0 || parts[0] != "Assets")
                throw new ArgumentException("Asset path must begin with Assets.");

            string current = "Assets";

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
