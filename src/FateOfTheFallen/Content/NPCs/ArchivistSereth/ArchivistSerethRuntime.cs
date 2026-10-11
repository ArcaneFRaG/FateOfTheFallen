using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace FateOfTheFallen
{
    /// <summary>
    /// Runtime-owned Archivist Sereth NPC.
    ///
    /// Sereth is NOT cloned from any native Erenshor NPC. His custom prefab
    /// from the shared fotf_npcs AssetBundle is instantiated directly and the
    /// minimum Erenshor gameplay/interaction components are created on it at
    /// runtime.
    /// </summary>
    internal static class ArchivistSerethRuntime
    {
        private static bool _initialized;
        private static GameObject _sereth;
        private static Coroutine _spawnRoutine;
        private static GameObject _cachedPrefab;


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

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (IsSerethScene(
                    activeScene))
            {
                StartSpawn();
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

            DestroySereth();

            _cachedPrefab =
                null;
        }


        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            if (!IsSerethScene(
                    scene))
            {
                _sereth =
                    null;

                return;
            }

            StartSpawn();
        }


        private static bool IsSerethScene(
            Scene scene)
        {
            return
                scene.IsValid() &&
                string.Equals(
                    scene.name,
                    ArchivistSerethDefinition.SceneName,
                    StringComparison.OrdinalIgnoreCase);
        }


        private static void StartSpawn()
        {
            if (_sereth != null ||
                _spawnRoutine != null)
            {
                return;
            }

            GameObject existing =
                FindExistingSereth();

            if (existing != null)
            {
                _sereth =
                    existing;

                return;
            }

            _spawnRoutine =
                CoroutineHost.Start(
                    SpawnWhenReady());
        }


        private static IEnumerator SpawnWhenReady()
        {
            const float timeout =
                20f;

            const float interval =
                0.5f;

            float elapsed =
                0f;

            while (elapsed < timeout)
            {
                if (!IsSerethScene(
                        SceneManager.GetActiveScene()))
                {
                    _spawnRoutine =
                        null;

                    yield break;
                }

                GameObject prefab =
                    LoadPrefab();

                if (prefab != null)
                {
                    Spawn(
                        prefab);

                    _spawnRoutine =
                        null;

                    yield break;
                }

                yield return
                    new WaitForSeconds(
                        interval);

                elapsed +=
                    interval;
            }

            _spawnRoutine =
                null;

            Plugin.NativeLog.LogError(
                "Archivist Sereth: prefab was not found in bundle [" +
                ArchivistSerethDefinition.BundleName +
                "].");
        }


        private static GameObject LoadPrefab()
        {
            if (_cachedPrefab != null)
            {
                return
                    _cachedPrefab;
            }

            AssetBundle bundle =
                AssetBundleRegistry.GetBundle(
                    ArchivistSerethDefinition.BundleName);

            if (bundle == null)
            {
                return null;
            }

            for (int i = 0;
                 i < ArchivistSerethDefinition.PrefabNames.Length;
                 i++)
            {
                string candidate =
                    ArchivistSerethDefinition.PrefabNames[i];

                if (string.IsNullOrEmpty(
                        candidate))
                {
                    continue;
                }

                GameObject direct =
                    bundle.LoadAsset<GameObject>(
                        candidate);

                if (direct != null)
                {
                    _cachedPrefab =
                        direct;

                    return
                        _cachedPrefab;
                }
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
                        assetName) ||
                    assetName.IndexOf(
                        "sereth",
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                GameObject found =
                    bundle.LoadAsset<GameObject>(
                        assetName);

                if (found != null)
                {
                    _cachedPrefab =
                        found;

                    return
                        _cachedPrefab;
                }
            }

            return null;
        }


        private static void Spawn(
            GameObject prefab)
        {
            if (_sereth != null ||
                prefab == null)
            {
                return;
            }

            GameObject stagingRoot =
                null;

            try
            {
                // Keep the custom prefab inactive while native components are
                // added/configured. This mirrors serialized-prefab setup much
                // more closely than adding dependencies to a live object.
                stagingRoot =
                    new GameObject(
                        "FOTF_ArchivistSereth_Staging");

                stagingRoot.SetActive(
                    false);

                GameObject sereth =
                    UnityEngine.Object.Instantiate(
                        prefab,
                        stagingRoot.transform,
                        false);

                if (sereth == null)
                {
                    Plugin.NativeLog.LogError(
                        "Archivist Sereth: custom prefab instantiation returned null.");

                    UnityEngine.Object.Destroy(
                        stagingRoot);

                    return;
                }

                sereth.name =
                    ArchivistSerethDefinition.NpcName;

                sereth.transform.SetParent(
                    null,
                    true);

                UnityEngine.Object.Destroy(
                    stagingRoot);

                stagingRoot =
                    null;

                // The instantiated prefab remains inactive while its
                // interaction/runtime shell is built.
                sereth.SetActive(
                    false);

                sereth.transform.SetPositionAndRotation(
                    ArchivistSerethDefinition.SpawnPosition,
                    ArchivistSerethDefinition.SpawnRotation);

                // AssetBundle materials can retain an authoring-project shader
                // that does not participate correctly in Erenshor's built-in
                // lighting. Repair them before the object becomes visible.
                RepairRuntimeMaterials(
                    sereth);

                BuildRuntimeNpc(
                    sereth);

                _sereth =
                    sereth;

                sereth.SetActive(
                    true);

                CoroutineHost.Start(
                    FinalizeAfterNativeStart(
                        sereth));
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Archivist Sereth: from-scratch spawn failed",
                    exception);

                if (stagingRoot != null)
                {
                    UnityEngine.Object.Destroy(
                        stagingRoot);
                }

                DestroySereth();
            }
        }


        private static void BuildRuntimeNpc(
            GameObject sereth)
        {
            if (sereth == null)
            {
                return;
            }

            DisableAuthoredColliders(
                sereth);

            AudioSource audio =
                GetOrAddComponent<AudioSource>(
                    sereth);

            Inventory inventory =
                GetOrAddComponent<Inventory>(
                    sereth);

            UseSkill skills =
                GetOrAddComponent<UseSkill>(
                    sereth);

            CastSpell spells =
                GetOrAddComponent<CastSpell>(
                    sereth);

            NavMeshAgent agent =
                GetOrAddComponent<NavMeshAgent>(
                    sereth);

            CapsuleCollider capsule =
                GetOrAddComponent<CapsuleCollider>(
                    sereth);

            // Character/Stats are added before NPC so every dependency the
            // native NPC runtime expects already exists when it starts.
            Character character =
                GetOrAddComponent<Character>(
                    sereth);

            Stats stats =
                GetOrAddComponent<Stats>(
                    sereth);

            QuestManager questManager =
                GetOrAddComponent<QuestManager>(
                    sereth);

            NPCDialog dialog =
                GetOrAddComponent<NPCDialog>(
                    sereth);

            NPCDialogManager dialogManager =
                GetOrAddComponent<NPCDialogManager>(
                    sereth);

            NPC npc =
                GetOrAddComponent<NPC>(
                    sereth);

            InitializeNullLists(
                inventory);

            InitializeNullLists(
                skills);

            InitializeNullLists(
                spells);

            InitializeNullLists(
                character);

            InitializeNullLists(
                stats);

            InitializeNullLists(
                questManager);

            InitializeNullLists(
                dialog);

            InitializeNullLists(
                dialogManager);

            InitializeNullLists(
                npc);

            ConfigureCollider(
                capsule);

            ConfigureAgent(
                agent);

            GameObject targetRing =
                ConfigureTargetRing(
                    sereth);

            ConfigureCharacter(
                character,
                stats,
                npc,
                skills,
                spells,
                capsule);

            character.TargetRing =
                targetRing;

            ConfigureStats(
                stats,
                character);

            ConfigureNpc(
                npc);

            ConfigureAnimator(
                sereth,
                character);

            ArchivistSerethQuestBridge.Configure(
                sereth);

            ArchivistSerethStationaryAnchor anchor =
                GetOrAddComponent<ArchivistSerethStationaryAnchor>(
                    sereth);

            anchor.Initialize(
                npc,
                agent,
                ArchivistSerethDefinition.SpawnPosition,
                ArchivistSerethDefinition.SpawnRotation);

            // Explicit references are reasserted at the end because runtime
            // components do not receive Unity's serialized native wiring.
            character.MyStats =
                stats;

            character.MyNPC =
                npc;

            character.MySkills =
                skills;

            character.MySpells =
                spells;

            character.MyCap =
                capsule;

            stats.Myself =
                character;
        }


        private static IEnumerator FinalizeAfterNativeStart(
            GameObject sereth)
        {
            // Allow native Awake/Start methods to run once, then reassert the
            // values FOTF owns in case a native Start assigned defaults.
            yield return null;
            yield return null;

            if (sereth == null ||
                _sereth != sereth)
            {
                yield break;
            }

            Character character =
                sereth.GetComponent<Character>();

            Stats stats =
                sereth.GetComponent<Stats>();

            NPC npc =
                sereth.GetComponent<NPC>();

            NavMeshAgent agent =
                sereth.GetComponent<NavMeshAgent>();

            if (character != null &&
                stats != null &&
                npc != null)
            {
                ConfigureCharacter(
                    character,
                    stats,
                    npc,
                    sereth.GetComponent<UseSkill>(),
                    sereth.GetComponent<CastSpell>(),
                    sereth.GetComponent<CapsuleCollider>());

                character.TargetRing =
                    FindTargetRing(
                        sereth);

                ConfigureStats(
                    stats,
                    character);

                ConfigureNpc(
                    npc);

                try
                {
                    stats.CalcStats();

                    stats.CurrentHP =
                        stats.CurrentMaxHP;
                }
                catch
                {
                }
            }

            ConfigureAgent(
                agent);

            sereth.transform.SetPositionAndRotation(
                ArchivistSerethDefinition.SpawnPosition,
                ArchivistSerethDefinition.SpawnRotation);
        }


        private static void ConfigureCharacter(
            Character character,
            Stats stats,
            NPC npc,
            UseSkill skills,
            CastSpell spells,
            CapsuleCollider capsule)
        {
            if (character == null)
            {
                return;
            }

            character.Alive =
                true;

            character.isNPC =
                true;

            character.isVendor =
                false;

            // Sereth is a story NPC, not a combatant.
            character.Invulnerable =
                true;

            character.Relax =
                true;

            character.NoRun =
                true;

            character.DestroyOnDeath =
                false;

            character.ShrinkColliderOnDeath =
                false;

            character.AggroRange =
                0f;

            character.AttackRange =
                0f;

            character.MyStats =
                stats;

            character.MyNPC =
                npc;

            character.MySkills =
                skills;

            character.MySpells =
                spells;

            character.MyCap =
                capsule;

            if (character.AggressiveTowards == null)
            {
                character.AggressiveTowards =
                    new List<Character.Faction>();
            }
            else
            {
                character.AggressiveTowards.Clear();
            }

            if (character.NearbyFriends == null)
            {
                character.NearbyFriends =
                    new List<Character>();
            }

            if (character.NearbyEnemies == null)
            {
                character.NearbyEnemies =
                    new List<Character>();
            }
            else
            {
                character.NearbyEnemies.Clear();
            }
        }


        private static void ConfigureStats(
            Stats stats,
            Character character)
        {
            if (stats == null)
            {
                return;
            }

            stats.Myself =
                character;

            stats.MyName =
                ArchivistSerethDefinition.NpcName;

            stats.Level =
                1;

            stats.BaseHP =
                1;

            stats.BaseAC =
                0;

            stats.BaseMana =
                0;

            stats.RunSpeed =
                0f;

            stats.OverrideHPforNPC =
                true;
        }


        private static void ConfigureNpc(
            NPC npc)
        {
            if (npc == null)
            {
                return;
            }

            npc.NPCName =
                ArchivistSerethDefinition.NpcName;

            npc.SimPlayer =
                false;

            npc.GroupEncounter =
                false;

            npc.CurrentAggroTarget =
                null;

            npc.retreat =
                false;

            npc.noClick =
                false;

            npc.BaseAtkDmg =
                0;

            npc.DamageRange =
                Vector2.zero;

            npc.DamageMult =
                0f;

            npc.OHAtkDmg =
                0;

            npc.NoDmgCap =
                false;

            // We do not need NPC.Start to manufacture level-based combat
            // resistances for a permanently invulnerable story NPC.
            npc.HandSetResistances =
                true;

            TrySetMember(
                npc,
                "NeverAggro",
                true);

            TrySetMember(
                npc,
                "AggroRegardlessOfLevel",
                false);

            TrySetMember(
                npc,
                "AggroRegardlessofLOS",
                false);

            TrySetMember(
                npc,
                "IgnoreLOSForAggro",
                false);

            TrySetMember(
                npc,
                "safePointProtect",
                99999f);
        }


        private static void ConfigureAnimator(
            GameObject sereth,
            Character character)
        {
            Animator animator =
                sereth.GetComponent<Animator>();

            if (animator == null)
            {
                animator =
                    sereth.GetComponentInChildren<Animator>(
                        true);
            }

            if (animator == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Archivist Sereth: custom prefab contains no Animator.");

                return;
            }

            animator.applyRootMotion =
                false;

            animator.enabled =
                true;

            // If the authored Sereth prefab already has a controller, retain
            // it. Otherwise borrow only a compatible native humanoid Animator
            // controller. No native NPC object is instantiated or cloned.
            if (animator.runtimeAnimatorController == null)
            {
                NativeCreatureAnimationBridge.TryCaptureAndApply(
                    sereth);
            }

            if (character != null)
            {
                try
                {
                    character.AssignAnim(
                        animator);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogWarning(
                        "Archivist Sereth: Character.AssignAnim failed: " +
                        exception.Message);
                }
            }
        }


        private static void ConfigureCollider(
            CapsuleCollider capsule)
        {
            if (capsule == null)
            {
                return;
            }

            capsule.enabled =
                true;

            capsule.isTrigger =
                false;

            capsule.direction =
                1;

            capsule.radius =
                ArchivistSerethDefinition.ColliderRadius;

            capsule.height =
                ArchivistSerethDefinition.ColliderHeight;

            capsule.center =
                ArchivistSerethDefinition.ColliderCenter;
        }


        private static void ConfigureAgent(
            NavMeshAgent agent)
        {
            if (agent == null)
            {
                return;
            }

            agent.speed =
                0f;

            agent.angularSpeed =
                0f;

            agent.acceleration =
                0f;

            agent.autoBraking =
                true;

            agent.updateRotation =
                false;

            if (agent.enabled &&
                agent.isOnNavMesh)
            {
                try
                {
                    agent.isStopped =
                        true;

                    agent.velocity =
                        Vector3.zero;

                    if (agent.hasPath)
                    {
                        agent.ResetPath();
                    }
                }
                catch
                {
                }
            }
        }


        private static void DisableAuthoredColliders(
            GameObject sereth)
        {
            if (sereth == null)
            {
                return;
            }

            Collider[] colliders =
                sereth.GetComponentsInChildren<Collider>(
                    true);

            for (int i = 0;
                 i < colliders.Length;
                 i++)
            {
                Collider collider =
                    colliders[i];

                if (collider != null)
                {
                    collider.enabled =
                        false;
                }
            }
        }


        // ============================================================
        // TARGET RING
        // ============================================================

        private static GameObject ConfigureTargetRing(
            GameObject sereth)
        {
            if (sereth == null)
            {
                return null;
            }

            GameObject existing =
                FindTargetRing(
                    sereth);

            if (existing != null)
            {
                existing.SetActive(
                    false);

                return
                    existing;
            }

            GameObject ring =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cylinder);

            ring.name =
                "FOTF_ArchivistSereth_TargetRing";

            Collider primitiveCollider =
                ring.GetComponent<Collider>();

            if (primitiveCollider != null)
            {
                primitiveCollider.enabled =
                    false;

                UnityEngine.Object.Destroy(
                    primitiveCollider);
            }

            ring.transform.SetParent(
                sereth.transform,
                false);

            ring.transform.localPosition =
                new Vector3(
                    0f,
                    0.035f,
                    0f);

            ring.transform.localRotation =
                Quaternion.identity;

            ring.transform.localScale =
                new Vector3(
                    0.72f,
                    0.02f,
                    0.72f);

            Renderer renderer =
                ring.GetComponent<Renderer>();

            if (renderer != null)
            {
                Shader shader =
                    Shader.Find(
                        "Unlit/Color");

                if (shader == null)
                {
                    shader =
                        Shader.Find(
                            "Sprites/Default");
                }

                if (shader == null)
                {
                    shader =
                        Shader.Find(
                            "Standard");
                }

                if (shader != null)
                {
                    Material material =
                        new Material(
                            shader);

                    material.name =
                        "FOTF_ArchivistSereth_TargetRing_Material";

                    if (material.HasProperty(
                            "_Color"))
                    {
                        material.color =
                            new Color(
                                1f,
                                0.82f,
                                0.08f,
                                0.65f);
                    }

                    renderer.sharedMaterial =
                        material;
                }
            }

            ring.SetActive(
                false);

            return
                ring;
        }


        private static GameObject FindTargetRing(
            GameObject sereth)
        {
            if (sereth == null)
            {
                return null;
            }

            Transform targetRing =
                sereth.transform.Find(
                    "FOTF_ArchivistSereth_TargetRing");

            return
                targetRing != null
                    ? targetRing.gameObject
                    : null;
        }


        // ============================================================
        // MATERIAL / LIGHTING REPAIR
        // ============================================================

        private static void RepairRuntimeMaterials(
            GameObject sereth)
        {
            if (sereth == null)
            {
                return;
            }

            Shader standard =
                Shader.Find(
                    "Standard");

            if (standard == null ||
                !standard.isSupported)
            {
                Plugin.NativeLog.LogWarning(
                    "Archivist Sereth: supported Standard shader was not available; " +
                    "custom materials could not be repaired.");

                return;
            }

            Renderer[] renderers =
                sereth.GetComponentsInChildren<Renderer>(
                    true);

            if (renderers == null)
            {
                return;
            }

            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Renderer renderer =
                    renderers[rendererIndex];

                if (renderer == null)
                {
                    continue;
                }

                renderer.shadowCastingMode =
                    ShadowCastingMode.On;

                renderer.receiveShadows =
                    true;

                Material[] materials =
                    renderer.materials;

                if (materials == null)
                {
                    continue;
                }

                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    Material material =
                        materials[materialIndex];

                    if (material == null)
                    {
                        continue;
                    }

                    RepairMaterial(
                        material,
                        renderer);
                }

                renderer.materials =
                    materials;
            }
        }


        private static void RepairMaterial(
            Material material,
            Renderer owner)
        {
            if (material == null)
            {
                return;
            }

            Texture baseTexture =
                GetTextureProperty(
                    material,
                    "_BaseMap");

            if (baseTexture == null)
            {
                baseTexture =
                    GetTextureProperty(
                        material,
                        "_MainTex");
            }

            Color baseColor =
                GetColorProperty(
                    material,
                    "_BaseColor",
                    GetColorProperty(
                        material,
                        "_Color",
                        Color.white));

            Texture normalMap =
                GetTextureProperty(
                    material,
                    "_BumpMap");

            if (normalMap == null)
            {
                normalMap =
                    GetTextureProperty(
                        material,
                        "_NormalMap");
            }

            Texture emissionMap =
                GetTextureProperty(
                    material,
                    "_EmissionMap");

            Color emissionColor =
                GetColorProperty(
                    material,
                    "_EmissionColor",
                    Color.black);

            float metallic =
                GetFloatProperty(
                    material,
                    "_Metallic",
                    0f);

            float smoothness =
                GetFloatProperty(
                    material,
                    "_Smoothness",
                    GetFloatProperty(
                        material,
                        "_Glossiness",
                        0.35f));

            float cutoff =
                GetFloatProperty(
                    material,
                    "_Cutoff",
                    0.5f);

            string materialName =
                material.name ?? "";

            string rendererName =
                owner != null
                    ? owner.name ?? ""
                    : "";

            string combined =
                (materialName + " " + rendererName)
                    .ToLowerInvariant();

            bool alphaCutout =
                combined.Contains("hair") ||
                combined.Contains("beard") ||
                combined.Contains("brow") ||
                combined.Contains("lash");

            material.shader =
                Shader.Find(
                    "Standard");

            if (material.HasProperty(
                    "_MainTex"))
            {
                material.SetTexture(
                    "_MainTex",
                    baseTexture);
            }

            if (material.HasProperty(
                    "_Color"))
            {
                material.SetColor(
                    "_Color",
                    baseColor);
            }

            if (material.HasProperty(
                    "_Metallic"))
            {
                material.SetFloat(
                    "_Metallic",
                    Mathf.Clamp01(
                        metallic));
            }

            if (material.HasProperty(
                    "_Glossiness"))
            {
                material.SetFloat(
                    "_Glossiness",
                    Mathf.Clamp01(
                        smoothness));
            }

            if (normalMap != null &&
                material.HasProperty(
                    "_BumpMap"))
            {
                material.SetTexture(
                    "_BumpMap",
                    normalMap);

                material.EnableKeyword(
                    "_NORMALMAP");
            }

            if (emissionMap != null &&
                material.HasProperty(
                    "_EmissionMap"))
            {
                material.SetTexture(
                    "_EmissionMap",
                    emissionMap);
            }

            if (emissionColor.maxColorComponent >
                    0.001f &&
                material.HasProperty(
                    "_EmissionColor"))
            {
                material.SetColor(
                    "_EmissionColor",
                    emissionColor);

                material.EnableKeyword(
                    "_EMISSION");
            }

            if (alphaCutout)
            {
                ConfigureStandardCutout(
                    material,
                    cutoff);
            }
            else
            {
                ConfigureStandardOpaque(
                    material);
            }
        }


        private static void ConfigureStandardOpaque(
            Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty(
                    "_Mode"))
            {
                material.SetFloat(
                    "_Mode",
                    0f);
            }

            material.SetOverrideTag(
                "RenderType",
                "");

            if (material.HasProperty(
                    "_SrcBlend"))
            {
                material.SetInt(
                    "_SrcBlend",
                    (int)BlendMode.One);
            }

            if (material.HasProperty(
                    "_DstBlend"))
            {
                material.SetInt(
                    "_DstBlend",
                    (int)BlendMode.Zero);
            }

            if (material.HasProperty(
                    "_ZWrite"))
            {
                material.SetInt(
                    "_ZWrite",
                    1);
            }

            material.DisableKeyword(
                "_ALPHATEST_ON");

            material.DisableKeyword(
                "_ALPHABLEND_ON");

            material.DisableKeyword(
                "_ALPHAPREMULTIPLY_ON");

            material.renderQueue =
                -1;
        }


        private static void ConfigureStandardCutout(
            Material material,
            float cutoff)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty(
                    "_Mode"))
            {
                material.SetFloat(
                    "_Mode",
                    1f);
            }

            material.SetOverrideTag(
                "RenderType",
                "TransparentCutout");

            if (material.HasProperty(
                    "_SrcBlend"))
            {
                material.SetInt(
                    "_SrcBlend",
                    (int)BlendMode.One);
            }

            if (material.HasProperty(
                    "_DstBlend"))
            {
                material.SetInt(
                    "_DstBlend",
                    (int)BlendMode.Zero);
            }

            if (material.HasProperty(
                    "_ZWrite"))
            {
                material.SetInt(
                    "_ZWrite",
                    1);
            }

            if (material.HasProperty(
                    "_Cutoff"))
            {
                material.SetFloat(
                    "_Cutoff",
                    Mathf.Clamp(
                        cutoff,
                        0.05f,
                        0.95f));
            }

            material.EnableKeyword(
                "_ALPHATEST_ON");

            material.DisableKeyword(
                "_ALPHABLEND_ON");

            material.DisableKeyword(
                "_ALPHAPREMULTIPLY_ON");

            material.renderQueue =
                (int)RenderQueue.AlphaTest;
        }


        private static Texture GetTextureProperty(
            Material material,
            string property)
        {
            if (material == null ||
                string.IsNullOrEmpty(
                    property) ||
                !material.HasProperty(
                    property))
            {
                return null;
            }

            try
            {
                return
                    material.GetTexture(
                        property);
            }
            catch
            {
                return null;
            }
        }


        private static Color GetColorProperty(
            Material material,
            string property,
            Color fallback)
        {
            if (material == null ||
                string.IsNullOrEmpty(
                    property) ||
                !material.HasProperty(
                    property))
            {
                return
                    fallback;
            }

            try
            {
                return
                    material.GetColor(
                        property);
            }
            catch
            {
                return
                    fallback;
            }
        }


        private static float GetFloatProperty(
            Material material,
            string property,
            float fallback)
        {
            if (material == null ||
                string.IsNullOrEmpty(
                    property) ||
                !material.HasProperty(
                    property))
            {
                return
                    fallback;
            }

            try
            {
                return
                    material.GetFloat(
                        property);
            }
            catch
            {
                return
                    fallback;
            }
        }


        // ============================================================
        // HAIR / ATTACHMENT RIG REPAIR
        // ============================================================

        private static GameObject FindExistingSereth()
        {
            Scene activeScene =
                SceneManager.GetActiveScene();

            GameObject[] objects =
                UnityEngine.Object.FindObjectsOfType<GameObject>();

            for (int i = 0;
                 i < objects.Length;
                 i++)
            {
                GameObject obj =
                    objects[i];

                if (obj == null ||
                    !obj.scene.IsValid() ||
                    obj.scene != activeScene)
                {
                    continue;
                }

                if (string.Equals(
                        obj.name,
                        ArchivistSerethDefinition.NpcName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return
                        obj;
                }
            }

            return null;
        }


        private static T GetOrAddComponent<T>(
            GameObject owner)
            where T : Component
        {
            if (owner == null)
            {
                return null;
            }

            T component =
                owner.GetComponent<T>();

            if (component == null)
            {
                component =
                    owner.AddComponent<T>();
            }

            return
                component;
        }


        /// <summary>
        /// Runtime-added Erenshor components do not receive serialized List
        /// instances from a prefab. Initialize null List&lt;T&gt; fields before
        /// their Start methods run.
        /// </summary>
        private static void InitializeNullLists(
            object instance)
        {
            if (instance == null)
            {
                return;
            }

            FieldInfo[] fields =
                instance.GetType().GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            for (int i = 0;
                 i < fields.Length;
                 i++)
            {
                FieldInfo field =
                    fields[i];

                if (field == null ||
                    field.IsInitOnly ||
                    !field.FieldType.IsGenericType ||
                    field.FieldType.GetGenericTypeDefinition() !=
                        typeof(List<>))
                {
                    continue;
                }

                try
                {
                    if (field.GetValue(
                            instance) != null)
                    {
                        continue;
                    }

                    object list =
                        Activator.CreateInstance(
                            field.FieldType);

                    field.SetValue(
                        instance,
                        list);
                }
                catch
                {
                }
            }
        }


        private static void TrySetMember(
            object instance,
            string memberName,
            object value)
        {
            if (instance == null ||
                string.IsNullOrEmpty(
                    memberName))
            {
                return;
            }

            Type type =
                instance.GetType();

            try
            {
                FieldInfo field =
                    type.GetField(
                        memberName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                if (field != null)
                {
                    field.SetValue(
                        instance,
                        value);

                    return;
                }

                PropertyInfo property =
                    type.GetProperty(
                        memberName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                if (property != null &&
                    property.CanWrite)
                {
                    property.SetValue(
                        instance,
                        value,
                        null);
                }
            }
            catch
            {
            }
        }


        private static void DestroySereth()
        {
            if (_sereth == null)
            {
                return;
            }

            try
            {
                UnityEngine.Object.Destroy(
                    _sereth);
            }
            catch
            {
            }

            _sereth =
                null;
        }
    }


    /// <summary>
    /// Hard anchor for a non-combat story NPC. Native NPC.Update can still
    /// run for ordinary interaction bookkeeping, but cannot walk Sereth away
    /// from his authored world position.
    /// </summary>
    internal sealed class ArchivistSerethStationaryAnchor :
        MonoBehaviour
    {
        private NPC _npc;
        private NavMeshAgent _agent;
        private Vector3 _position;
        private bool _initialized;


        internal void Initialize(
            NPC npc,
            NavMeshAgent agent,
            Vector3 position,
            Quaternion rotation)
        {
            _npc =
                npc;

            _agent =
                agent;

            _position =
                position;

            _initialized =
                true;
        }


        private void LateUpdate()
        {
            if (!_initialized)
            {
                return;
            }

            if (_npc != null)
            {
                _npc.CurrentAggroTarget =
                    null;

                _npc.retreat =
                    false;
            }

            if (_agent != null &&
                _agent.enabled &&
                _agent.isOnNavMesh)
            {
                try
                {
                    _agent.isStopped =
                        true;

                    _agent.velocity =
                        Vector3.zero;

                    if (_agent.hasPath)
                    {
                        _agent.ResetPath();
                    }
                }
                catch
                {
                }
            }

            // Keep Sereth pinned to his authored position, but DO NOT
            // continuously reapply root rotation here.
            //
            // Erenshor's native NamePlate component billboards the world-space
            // name text toward the camera. Reapplying the NPC root rotation in
            // LateUpdate after the nameplate has billboarded rotates the child
            // nameplate as well, leaving its back face toward the camera.
            //
            // Sereth's northeast facing is established when he spawns and his
            // NavMeshAgent is permanently stopped, so there is no need to
            // overwrite rotation every frame.
            transform.position =
                _position;
        }
    }
}
