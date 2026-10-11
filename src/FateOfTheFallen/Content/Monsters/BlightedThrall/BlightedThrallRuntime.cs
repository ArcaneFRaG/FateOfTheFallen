using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// First creature of the Lord of Blight's realm.
    ///
    /// The Blighted Thrall is not undead. It is a living mortal host whose
    /// biology has been pushed past every natural limit: flesh regrows when it
    /// should fail, fungal tissue competes with muscle, and swollen growths
    /// rupture under the strain of combat. It establishes the Lord of
    /// Blight's core theme immediately -- life without balance.
    ///
    /// Rather than constructing an Erenshor NPC from individual components,
    /// this system captures one normal native hostile SpawnPoint before the
    /// player enters Fallen Reach. The captured spawn point retains Erenshor's
    /// native NPC prefab, combat AI, faction, loot plumbing, death handling,
    /// and navigation. FOTF owns the final hostility and respawn rules.
    ///
    /// A runtime copy of that native SpawnPoint is then placed at
    /// FOTF_MonsterSpawn_01. Every NPC produced by it is converted into the
    /// FOTF monster "Blighted Thrall".
    ///
    /// This is intentionally a bridge PoC. Once the native NPC path is proven,
    /// the visual/model can be replaced without replacing the combat/runtime
    /// architecture.
    /// </summary>
    internal static class BlightedThrallRuntime
    {
        internal const string MonsterName =
            "Blighted Thrall";

        internal const string SpawnMarkerName =
            "FOTF_MonsterSpawn_01";

        private const string RuntimeSpawnPointName =
            "FOTF_BlightedThrall_SpawnPoint";

        private const string TemplateSpawnPointName =
            "FOTF_BlightedThrall_SpawnPointTemplate";


        // ============================================================
        // RAID-TRASH BASELINE TUNING
        // ============================================================

        // Baseline: Fernallan Planar Guard (Plane of the Willow)
        //   Level: 42
        //   HP:    2,275,000
        //   AC:    630
        //   MR/PR/ER/VR: 50
        //
        // Fate of the Fallen trash is intentionally tuned 20% above that
        // native raid-trash baseline. Level itself is not multiplied.
        private const float RaidTrashStatScale =
            1.20f;

        private const int MonsterLevel =
            42;

        private const int MonsterHP =
            2730000;

        private const int MonsterBaseAC =
            756;

        // FOTF raid-trash offensive tuning.
        // Intentionally much higher than the original PoC placeholder.
        internal const int MonsterBaseAttackDamage =
            4000;

        private const int MonsterPoisonResistance =
            60;

        private const int MonsterMagicResistance =
            60;

        private const int MonsterElementalResistance =
            60;

        private const int MonsterVoidResistance =
            60;

        private const float MonsterRunSpeed =
            4.5f;

        internal const float MonsterAggroRange =
            22f;

        // Explicit FOTF-owned respawn delay for Fallen Reach testing.
        // This deliberately does not inherit the captured native SpawnPoint's
        // respawn timing, so the PoC is deterministic regardless of template.
        internal const float RespawnDelaySeconds =
            60f;


        // ============================================================
        // STATE
        // ============================================================

        private static bool _initialized;

        private static GameObject _spawnPointTemplate;
        private static string _capturedNativeMonsterName;

        private static GameObject _runtimeSpawnPointObject;
        private static SpawnPoint _runtimeSpawnPoint;

        // Clean, configured raid-trash prototype captured from the first
        // proven Blighted Thrall spawn. This survives scene changes and is
        // used for all later raid respawns, so repeat spawning never depends
        // on a native SpawnPoint firing a second time.
        private static GameObject _raidTrashPrototypeObject;
        private static NPC _raidTrashPrototypeNpc;

        private static NPC _lastConfiguredNpc;
        private static int _spawnGeneration;

        private static bool _respawnPending;
        private static float _respawnAtTime;


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

            if (activeScene.IsValid() &&
                !IsFallenReach(
                    activeScene) &&
                !CustomRaidLoader.IsRaidPocScene(
                    activeScene))
            {
                CoroutineHost.Start(
                    CaptureTemplateWhenAvailable(
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

            DestroyRuntimeSpawnPoint();
            DestroyRaidTrashPrototype();
            DestroyTemplate();
        }


        // ============================================================
        // NATIVE TEMPLATE CAPTURE
        // ============================================================

        internal static bool HasCapturedNativeTemplate
        {
            get
            {
                return
                    _spawnPointTemplate != null;
            }
        }

        internal static bool HasRaidTrashPrototype
        {
            get
            {
                return
                    _raidTrashPrototypeNpc != null &&
                    _raidTrashPrototypeObject != null;
            }
        }


        internal static bool CaptureNativeTemplateBeforeZoneChange()
        {
            return TryCaptureNativeTemplate(
                true);
        }


        private static bool TryCaptureNativeTemplate(
            bool logFailure)
        {
            if (_spawnPointTemplate != null)
            {
                return true;
            }

            if (SpawnPointManager.SpawnPointsInScene == null ||
                SpawnPointManager.SpawnPointsInScene.Count == 0)
            {
                if (logFailure)
                {
                    Plugin.NativeLog.LogWarning(
                        "Blighted Thrall: no native hostile SpawnPoints are available in the current scene to seed the FOTF enemy template cache.");
                }

                return false;
            }

            SpawnPoint source =
                FindSuitableNativeSpawnPoint();

            if (source == null)
            {
                if (logFailure)
                {
                    Plugin.NativeLog.LogWarning(
                        "Blighted Thrall: no suitable living hostile native SpawnPoint was found in the current scene to seed the FOTF enemy template cache.");
                }

                return false;
            }

            NPC sourceNpc =
                source.SpawnedNPC;

            _capturedNativeMonsterName =
                sourceNpc != null &&
                !string.IsNullOrEmpty(
                    sourceNpc.NPCName)
                    ? sourceNpc.NPCName
                    : source.name;

            bool sourceWasActive =
                source.gameObject.activeSelf;

            try
            {
                if (sourceWasActive)
                {
                    source.gameObject.SetActive(
                        false);
                }

                _spawnPointTemplate =
                    UnityEngine.Object.Instantiate(
                        source.gameObject);

                if (_spawnPointTemplate == null)
                {
                    return false;
                }

                _spawnPointTemplate.name =
                    TemplateSpawnPointName;

                _spawnPointTemplate.SetActive(
                    false);

                UnityEngine.Object.DontDestroyOnLoad(
                    _spawnPointTemplate);


                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blighted Thrall: failed to cache native hostile SpawnPoint template: " +
                    exception);

                DestroyTemplate();

                return false;
            }
            finally
            {
                if (sourceWasActive &&
                    source != null)
                {
                    source.gameObject.SetActive(
                        true);
                }
            }
        }


        private static IEnumerator CaptureTemplateWhenAvailable(
            Scene sourceScene)
        {
            if (_spawnPointTemplate != null ||
                !sourceScene.IsValid() ||
                IsFallenReach(
                    sourceScene) ||
                CustomRaidLoader.IsRaidPocScene(
                    sourceScene))
            {
                yield break;
            }

            // Native SpawnPoint registration/spawned NPC construction can lag
            // sceneLoaded by several frames. Probe quietly for up to ten
            // seconds, but only attempt the actual capture every 15 frames.
            const int maximumFrames =
                600;

            const int probeIntervalFrames =
                15;

            for (int frame = 0;
                 frame < maximumFrames;
                 frame++)
            {
                if (_spawnPointTemplate != null)
                {
                    yield break;
                }

                if (!sourceScene.IsValid() ||
                    !sourceScene.isLoaded ||
                    SceneManager.GetActiveScene() != sourceScene)
                {
                    yield break;
                }

                if (frame % probeIntervalFrames == 0 &&
                    TryCaptureNativeTemplate(
                        false))
                {
                    yield break;
                }

                yield return null;
            }
        }



        private static SpawnPoint FindSuitableNativeSpawnPoint()
        {
            SpawnPoint best =
                null;

            int bestScore =
                int.MinValue;

            foreach (SpawnPoint spawnPoint in
                     SpawnPointManager.SpawnPointsInScene)
            {
                if (spawnPoint == null ||
                    spawnPoint.SpawnedNPC == null)
                {
                    continue;
                }

                NPC npc =
                    spawnPoint.SpawnedNPC;

                Character character =
                    npc.GetChar();

                if (character == null ||
                    character.MyStats == null)
                {
                    continue;
                }

                if (!character.Alive ||
                    character.MyStats.CurrentHP <= 0)
                {
                    continue;
                }

                if (npc.SimPlayer ||
                    npc.GroupEncounter ||
                    character.isVendor)
                {
                    continue;
                }

                if (character.MyFaction ==
                        Character.Faction.PC ||
                    character.MyFaction ==
                        Character.Faction.Player)
                {
                    continue;
                }

                if (character.MyStats.Charmed ||
                    character.MyStats.RunSpeed <= 0f)
                {
                    continue;
                }

                int score =
                    ScoreNativeSpawnPoint(
                        spawnPoint);

                if (best == null ||
                    score > bestScore)
                {
                    best =
                        spawnPoint;

                    bestScore =
                        score;
                }
            }

            if (best != null)
            {
                string sourceName =
                    best.SpawnedNPC != null
                        ? best.SpawnedNPC.NPCName
                        : best.name;

            }

            return best;
        }


        private static int ScoreNativeSpawnPoint(
            SpawnPoint spawnPoint)
        {
            if (spawnPoint == null ||
                spawnPoint.SpawnedNPC == null)
            {
                return int.MinValue;
            }

            NPC npc =
                spawnPoint.SpawnedNPC;

            int score =
                0;

            if (HasHumanoidRig(
                    npc.transform))
            {
                score +=
                    250;
            }

            string name =
                npc.NPCName ??
                npc.name ??
                string.Empty;

            if (ContainsIgnoreCase(name, "bandit") ||
                ContainsIgnoreCase(name, "cultist") ||
                ContainsIgnoreCase(name, "raider") ||
                ContainsIgnoreCase(name, "outlaw") ||
                ContainsIgnoreCase(name, "soldier") ||
                ContainsIgnoreCase(name, "guard"))
            {
                score +=
                    100;
            }

            if (ContainsIgnoreCase(name, "skeleton") ||
                ContainsIgnoreCase(name, "undead"))
            {
                score +=
                    35;
            }

            if (ContainsIgnoreCase(name, "spider") ||
                ContainsIgnoreCase(name, "wolf") ||
                ContainsIgnoreCase(name, "rat") ||
                ContainsIgnoreCase(name, "boar") ||
                ContainsIgnoreCase(name, "bear"))
            {
                score -=
                    100;
            }

            return score;
        }


        private static bool HasHumanoidRig(
            Transform root)
        {
            if (root == null)
            {
                return false;
            }

            bool hips =
                false;

            bool spine =
                false;

            bool leftHand =
                false;

            bool rightHand =
                false;

            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(
                    true);

            if (transforms == null)
            {
                return false;
            }

            for (int i = 0;
                 i < transforms.Length;
                 i++)
            {
                Transform candidate =
                    transforms[i];

                if (candidate == null)
                {
                    continue;
                }

                string candidateName =
                    candidate.name;

                if (string.Equals(
                        candidateName,
                        "Hips",
                        StringComparison.OrdinalIgnoreCase))
                {
                    hips = true;
                }
                else if (string.Equals(
                             candidateName,
                             "Spine_01",
                             StringComparison.OrdinalIgnoreCase))
                {
                    spine = true;
                }
                else if (string.Equals(
                             candidateName,
                             "Hand_L",
                             StringComparison.OrdinalIgnoreCase))
                {
                    leftHand = true;
                }
                else if (string.Equals(
                             candidateName,
                             "Hand_R",
                             StringComparison.OrdinalIgnoreCase))
                {
                    rightHand = true;
                }
            }

            return hips &&
                   spine &&
                   leftHand &&
                   rightHand;
        }



        // ============================================================
        // RAID TRASH SPAWN BRIDGE
        // ============================================================

        /// <summary>
        /// Creates one Blighted Thrall at an authored marker inside a custom
        /// raid scene using the same captured native SpawnPoint template and
        /// the same monster configuration proven by the Fallen Reach PoC.
        ///
        /// This path deliberately does not participate in Fallen Reach's
        /// singleton 60-second respawn controller. Raid encounter/reset logic
        /// owns raid-trash lifetime instead.
        /// </summary>
        internal static IEnumerator SpawnRaidTrashAtMarker(
            Scene scene,
            Transform marker,
            string instanceName,
            Action<NPC> onSpawned)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                marker == null)
            {
                yield break;
            }

            if (_spawnPointTemplate == null)
            {
                Plugin.NativeLog.LogError(
                    "[RaidPoC] cannot spawn raid trash: no native hostile SpawnPoint template has been captured.");

                yield break;
            }

            GameObject runtimeObject =
                null;

            SpawnPoint runtimeSpawnPoint =
                null;

            try
            {
                runtimeObject =
                    UnityEngine.Object.Instantiate(
                        _spawnPointTemplate);

                if (runtimeObject == null)
                {
                    Plugin.NativeLog.LogError(
                        "[RaidPoC] failed to clone Blighted Thrall SpawnPoint for [" +
                        instanceName +
                        "].");

                    yield break;
                }

                runtimeObject.name =
                    "FOTF_Raid_" +
                    instanceName +
                    "_SpawnPoint";

                Vector3 spawnPosition =
                    ResolveSafeNavMeshPosition(
                        marker.position);

                runtimeObject.transform.SetPositionAndRotation(
                    spawnPosition,
                    marker.rotation);

                SceneManager.MoveGameObjectToScene(
                    runtimeObject,
                    scene);

                runtimeSpawnPoint =
                    runtimeObject.GetComponent<SpawnPoint>();

                if (runtimeSpawnPoint == null)
                {
                    Plugin.NativeLog.LogError(
                        "[RaidPoC] cloned raid-trash object [" +
                        instanceName +
                        "] does not contain SpawnPoint.");

                    UnityEngine.Object.Destroy(
                        runtimeObject);

                    yield break;
                }

                runtimeObject.SetActive(
                    true);

            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[RaidPoC] failed to create Blighted Thrall raid-trash SpawnPoint [" +
                    instanceName +
                    "]: " +
                    exception);

                if (runtimeObject != null)
                {
                    UnityEngine.Object.Destroy(
                        runtimeObject);
                }

                yield break;
            }

            const int maximumSpawnFrames =
                300;

            NPC spawned =
                null;

            for (int frame = 0;
                 frame < maximumSpawnFrames;
                 frame++)
            {
                if (!scene.IsValid() ||
                    !scene.isLoaded ||
                    SceneManager.GetActiveScene() != scene)
                {
                    yield break;
                }

                if (runtimeSpawnPoint != null)
                {
                    spawned =
                        runtimeSpawnPoint.SpawnedNPC;
                }

                if (spawned != null)
                {
                    break;
                }

                yield return null;
            }

            if (spawned == null)
            {
                Plugin.NativeLog.LogError(
                    "[RaidPoC] timed out waiting for Blighted Thrall raid-trash NPC [" +
                    instanceName +
                    "].");

                if (runtimeObject != null)
                {
                    UnityEngine.Object.Destroy(
                        runtimeObject);
                }

                yield break;
            }

            // Match the stable Fallen Reach path: give the native SpawnPoint
            // two frames to finish constructing its NPC before replacing its
            // identity/stats/loot/behaviour.
            yield return null;
            yield return null;

            ConfigureSpawnedMonster(
                spawned);

            ResetRaidTrashRuntimeState(
                spawned,
                ResolveSafeNavMeshPosition(
                    marker.position),
                marker.rotation);

            EnsureRaidTrashPrototype(
                spawned);

            // Raid encounter logic owns reset/repopulation. Prevent an
            // arbitrary native SpawnPoint timer from creating a second copy.
            if (runtimeSpawnPoint != null)
            {
                runtimeSpawnPoint.enabled =
                    false;
            }

            if (onSpawned != null)
            {
                onSpawned(
                    spawned);
            }

        }




        /// <summary>
        /// Raid-pack follow-up spawn path.
        ///
        /// Native SpawnPoint is used once to bootstrap one completely valid
        /// hostile NPC. Additional members of the same trash pack are cloned
        /// from that live, configured NPC rather than trying to activate
        /// multiple copies of the same captured SpawnPoint. The latter is not
        /// reliable in this bridge PoC and can leave later SpawnPoint clones
        /// permanently without SpawnedNPC.
        /// </summary>
        internal static IEnumerator CloneRaidTrashAtMarker(
            Scene scene,
            NPC sourceNpc,
            Transform marker,
            string instanceName,
            Action<NPC> onSpawned)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                marker == null)
            {
                yield break;
            }

            NPC cloneSource =
                sourceNpc;

            if (cloneSource == null)
            {
                cloneSource =
                    _raidTrashPrototypeNpc;
            }

            if (cloneSource == null)
            {
                Plugin.NativeLog.LogError(
                    "[RaidPoC] cannot clone raid trash [" +
                    instanceName +
                    "]: no living seed NPC and no persistent Blighted Thrall prototype are available.");

                yield break;
            }

            GameObject cloneObject =
                null;

            NPC cloneNpc =
                null;

            Vector3 spawnPosition =
                ResolveSafeNavMeshPosition(
                    marker.position);

            try
            {
                cloneObject =
                    UnityEngine.Object.Instantiate(
                        cloneSource.gameObject,
                        spawnPosition,
                        marker.rotation);

                if (cloneObject == null)
                {
                    Plugin.NativeLog.LogError(
                        "[RaidPoC] failed to clone seeded Blighted Thrall NPC for [" +
                        instanceName +
                        "].");

                    yield break;
                }

                cloneObject.name =
                    MonsterName;

                SceneManager.MoveGameObjectToScene(
                    cloneObject,
                    scene);

                // A clone produced from the persistent prototype inherits its
                // inactive state. Activate the scene instance so native Start
                // and AI initialization can run normally.
                if (!cloneObject.activeSelf)
                {
                    cloneObject.SetActive(
                        true);
                }

                cloneNpc =
                    cloneObject.GetComponent<NPC>();

                if (cloneNpc == null)
                {
                    Plugin.NativeLog.LogError(
                        "[RaidPoC] cloned raid-trash NPC [" +
                        instanceName +
                        "] does not contain NPC.");

                    UnityEngine.Object.Destroy(
                        cloneObject);

                    yield break;
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[RaidPoC] failed to clone seeded Blighted Thrall NPC [" +
                    instanceName +
                    "]: " +
                    exception);

                if (cloneObject != null)
                {
                    UnityEngine.Object.Destroy(
                        cloneObject);
                }

                yield break;
            }

            // Give cloned native components their normal Start() pass before
            // FOTF resets the copied runtime state.
            yield return null;
            yield return null;

            if (!scene.IsValid() ||
                !scene.isLoaded ||
                cloneNpc == null)
            {
                yield break;
            }

            ConfigureSpawnedMonster(
                cloneNpc);

            ResetRaidTrashRuntimeState(
                cloneNpc,
                spawnPosition,
                marker.rotation);

            EnsureRaidTrashPrototype(
                cloneNpc);

            if (onSpawned != null)
            {
                onSpawned(
                    cloneNpc);
            }

        }


        /// <summary>
        /// Placeholder boss bridge for the raid PoC. The boss deliberately
        /// reuses the proven Blighted Thrall native NPC shell so this pass tests
        /// boss lifecycle behavior rather than introducing a second prefab path.
        /// </summary>
        internal static IEnumerator CloneRaidBossAtMarker(
            Scene scene,
            Transform marker,
            string instanceName,
            Action<NPC> onSpawned)
        {
            NPC bossNpc =
                null;

            yield return CloneRaidTrashAtMarker(
                scene,
                null,
                marker,
                instanceName,
                npc =>
                {
                    bossNpc =
                        npc;
                });

            if (bossNpc == null)
            {
                yield break;
            }

            ConfigureRaidBossPlaceholder(
                bossNpc);

            if (onSpawned != null)
            {
                onSpawned(
                    bossNpc);
            }

        }


        private static void ConfigureRaidBossPlaceholder(
            NPC npc)
        {
            if (npc == null)
            {
                return;
            }

            GameObject bossObject =
                npc.gameObject;

            bossObject.name =
                RaidPocEncounterRuntime.RaidBossDisplayName;

            npc.NPCName =
                RaidPocEncounterRuntime.RaidBossDisplayName;

            npc.BaseAtkDmg =
                RaidPocEncounterRuntime.RaidBossBaseAttackDamage;

            // Boss intimidation pass: make the native hostile shell visibly
            // larger while leaving the ordinary Blighted Thrall prototype
            // untouched. ConfigureRaidBossPlaceholder() runs once per spawn.
            bossObject.transform.localScale =
                bossObject.transform.localScale *
                RaidPocEncounterRuntime.RaidBossScaleMultiplier;

            npc.NoDmgCap =
                true;

            npc.noClick =
                false;

            npc.CurrentAggroTarget =
                null;

            npc.retreat =
                false;

            Stats stats =
                bossObject.GetComponent<Stats>();

            if (stats != null)
            {
                stats.MyName =
                    RaidPocEncounterRuntime.RaidBossDisplayName;

                stats.BaseHP =
                    RaidPocEncounterRuntime.RaidBossHP;

                // Ordinary Blighted Thralls use BaseMHAtkDelay=120. A 2.5x
                // attack-speed multiplier therefore uses a 48-point delay.
                // Clamp defensively in case the seed NPC changes later.
                stats.BaseMHAtkDelay =
                    Mathf.Max(
                        1f,
                        stats.BaseMHAtkDelay /
                        RaidPocEncounterRuntime.RaidBossAttackSpeedMultiplier);

                stats.OverrideHPforNPC =
                    true;

                stats.CalcStats();

                stats.ArmorPenPercentage =
                    200f;

                stats.CurrentHP =
                    stats.CurrentMaxHP;
            }

            Character character =
                npc.GetChar();

            if (character == null)
            {
                character =
                    bossObject.GetComponent<Character>();
            }

            if (character != null)
            {
                character.Alive =
                    true;
            }

            BlightedThrallBehaviour behaviour =
                bossObject.GetComponent<BlightedThrallBehaviour>();

            if (behaviour != null)
            {
                behaviour.SetRaidBossMode();
            }

            // CloneRaidBossAtMarker begins from the proven Blighted Thrall
            // shell, whose loot table was already armed with Blighted Tissue.
            // Replace that inherited table with the dedicated boss reward so
            // the PoC validates the real native corpse-loot path.
            RaidPocBossLoot.ConfigureBossLoot(
                npc);

            RefreshNamePlate(
                npc);
        }


        internal static void RefreshRaidNpcNamePlate(
            NPC npc)
        {
            RefreshNamePlate(
                npc);
        }


        internal static void ResetRaidTrashForWipe(
            NPC npc,
            Transform marker)
        {
            if (npc == null ||
                marker == null)
            {
                return;
            }

            ResetRaidTrashRuntimeState(
                npc,
                ResolveSafeNavMeshPosition(
                    marker.position),
                marker.rotation);
        }


        private static void ResetRaidTrashRuntimeState(
            NPC npc,
            Vector3 position,
            Quaternion rotation)
        {
            if (npc == null)
            {
                return;
            }

            npc.noClick =
                false;

            npc.CurrentAggroTarget =
                null;

            npc.retreat =
                false;

            Character character =
                npc.GetChar();

            if (character == null)
            {
                character =
                    npc.GetComponent<Character>();
            }

            Stats stats =
                npc.GetComponent<Stats>();

            if (character != null)
            {
                character.Alive =
                    true;
            }

            if (stats != null)
            {
                stats.CurrentHP =
                    stats.CurrentMaxHP;
            }

            // Native humanoids use this capsule placement. Reassert it for the
            // bridge clone so the clickable/targetable volume is centered on
            // the visible NPC rather than inheriting stale copied state.
            CapsuleCollider capsule =
                npc.GetComponent<CapsuleCollider>();

            if (capsule != null)
            {
                capsule.enabled =
                    true;

                capsule.center =
                    new Vector3(
                        0f,
                        1f,
                        0f);

                capsule.height =
                    2f;
            }

            npc.gameObject.layer =
                9;

            NavMeshAgent agent =
                npc.GetComponent<NavMeshAgent>();

            if (agent != null &&
                agent.enabled &&
                agent.isOnNavMesh)
            {
                agent.Warp(
                    position);

                agent.isStopped =
                    false;
            }
            else
            {
                npc.transform.position =
                    position;
            }

            npc.transform.rotation =
                rotation;
        }


        // ============================================================
        // SCENE HANDLING
        // ============================================================

        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            if (IsFallenReach(
                    scene))
            {
                CoroutineHost.Start(
                    SpawnInFallenReach(
                        scene));

                return;
            }

            DestroyRuntimeSpawnPoint();

            // The Reliquary itself contains no hostile NPCs to template.
            // Therefore capture opportunistically in every ordinary native
            // combat scene and carry the cached object forward via
            // DontDestroyOnLoad.
            if (!CustomRaidLoader.IsRaidPocScene(
                    scene) &&
                _spawnPointTemplate == null)
            {
                CoroutineHost.Start(
                    CaptureTemplateWhenAvailable(
                        scene));
            }
        }


        private static IEnumerator SpawnInFallenReach(
            Scene scene)
        {
            yield return null;
            yield return null;

            DestroyRuntimeSpawnPoint();

            GameObject marker =
                null;

            for (int frame = 0;
                 frame < 300;
                 frame++)
            {
                marker =
                    GameObject.Find(
                        SpawnMarkerName);

                if (marker != null)
                {
                    break;
                }

                yield return null;
            }

            if (marker == null)
            {
                Plugin.NativeLog.LogError(
                    "Blighted Thrall: required marker [" +
                    SpawnMarkerName +
                    "] was not found in Fallen Reach.");

                yield break;
            }

            if (_spawnPointTemplate == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Blighted Thrall: Fallen Reach loaded without a captured native SpawnPoint template. " +
                    "Enter the zone once from a normal Erenshor zone with /fotfzone to seed the current PoC template.");

                yield break;
            }

            try
            {
                _runtimeSpawnPointObject =
                    UnityEngine.Object.Instantiate(
                        _spawnPointTemplate);

                if (_runtimeSpawnPointObject == null)
                {
                    Plugin.NativeLog.LogError(
                        "Blighted Thrall: failed to instantiate the runtime SpawnPoint.");

                    yield break;
                }

                _runtimeSpawnPointObject.name =
                    RuntimeSpawnPointName;

                Vector3 spawnPosition =
                    ResolveSafeNavMeshPosition(
                        marker.transform.position);

                _runtimeSpawnPointObject.transform.SetPositionAndRotation(
                    spawnPosition,
                    marker.transform.rotation);

                SceneManager.MoveGameObjectToScene(
                    _runtimeSpawnPointObject,
                    scene);

                _runtimeSpawnPoint =
                    _runtimeSpawnPointObject.GetComponent<SpawnPoint>();

                if (_runtimeSpawnPoint == null)
                {
                    Plugin.NativeLog.LogError(
                        "Blighted Thrall: cloned runtime object did not contain SpawnPoint.");

                    DestroyRuntimeSpawnPoint();

                    yield break;
                }

                _lastConfiguredNpc =
                    null;

                _respawnPending =
                    false;

                _respawnAtTime =
                    0f;

                _runtimeSpawnPointObject.SetActive(
                    true);

            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blighted Thrall: failed to create runtime SpawnPoint: " +
                    exception);

                DestroyRuntimeSpawnPoint();

                yield break;
            }

            while (IsFallenReach(
                       SceneManager.GetActiveScene()) &&
                   _runtimeSpawnPoint != null)
            {
                // Respawn timing must not depend on the dead NPC object still
                // existing. Erenshor may destroy/invalidate the NPC component
                // as part of corpse creation, so advance the FOTF timer from
                // the spawn controller itself.
                if (_respawnPending &&
                    Time.time >=
                        _respawnAtTime)
                {

                    CoroutineHost.Start(
                        SpawnInFallenReach(
                            scene));

                    yield break;
                }

                // Unity's destroyed-object null semantics mean the managed
                // reference can still exist while == null. If the configured
                // NPC disappears during the native death/corpse transition,
                // treat that as death rather than silently losing the timer.
                if (!_respawnPending &&
                    !ReferenceEquals(
                        _lastConfiguredNpc,
                        null) &&
                    _lastConfiguredNpc == null)
                {
                    ScheduleRespawn();
                }

                NPC spawned =
                    _runtimeSpawnPoint.SpawnedNPC;

                if (spawned != null &&
                    spawned != _lastConfiguredNpc)
                {
                    _lastConfiguredNpc =
                        spawned;

                    yield return null;
                    yield return null;

                    ConfigureSpawnedMonster(
                        spawned);

                    // We only need the captured SpawnPoint to create the NPC.
                    // Disable its own lifecycle after the first spawn so an
                    // arbitrary native respawn timer cannot race FOTF's timer.
                    if (_runtimeSpawnPoint != null)
                    {
                        _runtimeSpawnPoint.enabled =
                            false;
                    }

                    _respawnPending =
                        false;

                    _respawnAtTime =
                        0f;
                }

                if (_lastConfiguredNpc != null)
                {
                    Character spawnedCharacter =
                        _lastConfiguredNpc.GetChar();

                    if (spawnedCharacter == null)
                    {
                        spawnedCharacter =
                            _lastConfiguredNpc.GetComponent<Character>();
                    }

                    Stats spawnedStats =
                        _lastConfiguredNpc.GetComponent<Stats>();

                    bool dead =
                        spawnedCharacter == null ||
                        !spawnedCharacter.Alive ||
                        spawnedStats == null ||
                        spawnedStats.CurrentHP <= 0;

                    if (dead)
                    {
                        ScheduleRespawn();
                    }
                }

                yield return null;
            }
        }


        internal static void NotifyMonsterDeath()
        {
            if (!IsFallenReach(
                    SceneManager.GetActiveScene()))
            {
                return;
            }

            ScheduleRespawn();
        }


        private static void ScheduleRespawn()
        {
            if (_respawnPending)
            {
                return;
            }

            _respawnPending =
                true;

            _respawnAtTime =
                Time.time +
                RespawnDelaySeconds;

        }


        // ============================================================
        // MONSTER CONFIGURATION
        // ============================================================

        private static void ConfigureSpawnedMonster(
            NPC npc)
        {
            if (npc == null)
            {
                return;
            }

            try
            {
                GameObject monster =
                    npc.gameObject;

                monster.name =
                    MonsterName;

                npc.NPCName =
                    MonsterName;

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
                    MonsterBaseAttackDamage;

                // Character.DamageMe() hard-caps ordinary NPC physical
                // damage to roughly:
                //
                //     NPC Level * 15 * ServerDMGMod +/- NPC Level
                //
                // unless NPC.NoDmgCap is true. At level 42 that cap is
                // approximately 588-671 damage, which is exactly the range
                // observed in testing. Custom FOTF raid mobs own their
                // offensive tuning, so explicitly opt this NPC out.
                npc.NoDmgCap =
                    true;

                npc.OHAtkDmg =
                    0;

                Character character =
                    npc.GetChar();

                if (character == null)
                {
                    character =
                        monster.GetComponent<Character>();
                }

                ConfigureNaturalHostility(
                    npc,
                    character);

                Stats stats =
                    monster.GetComponent<Stats>();

                if (stats != null)
                {
                    stats.MyName =
                        MonsterName;

                    stats.Level =
                        MonsterLevel;

                    stats.BaseHP =
                        MonsterHP;

                    stats.BaseAC =
                        MonsterBaseAC;

                    stats.BasePR =
                        MonsterPoisonResistance;

                    stats.BaseMR =
                        MonsterMagicResistance;

                    stats.BaseER =
                        MonsterElementalResistance;

                    stats.BaseVR =
                        MonsterVoidResistance;

                    // Keep the runtime NPC combat state identical to the
                    // known-good 4000-damage build. The actual melee
                    // mitigation patch below temporarily substitutes 0%
                    // ArmorPenPercentage only while Erenshor executes
                    // Stats.MitigatePhysical(), then restores this value.
                    stats.BaseArmorPenPercentage =
                        200f;

                    stats.RunSpeed =
                        MonsterRunSpeed;

                    stats.BaseMHAtkDelay =
                        120f;

                    stats.OverrideHPforNPC =
                        true;

                    stats.CalcStats();

                    // Preserve the known-good runtime combat state. This is
                    // temporarily changed to 0 only inside MitigatePhysical()
                    // for Blighted Thrall melee hits, then restored.
                    stats.ArmorPenPercentage =
                        200f;

                    stats.CurrentHP =
                        stats.CurrentMaxHP;
                }

                if (character != null)
                {
                    character.Alive =
                        true;
                }

                NavMeshAgent agent =
                    monster.GetComponent<NavMeshAgent>();

                if (agent != null &&
                    !agent.isOnNavMesh)
                {
                    NavMeshHit hit;

                    if (NavMesh.SamplePosition(
                            monster.transform.position,
                            out hit,
                            8f,
                            NavMesh.AllAreas))
                    {
                        agent.Warp(
                            hit.position);
                    }
                }

                ApplyBlightVisualTreatment(
                    npc);

                LootTable lootTable =
                    monster.GetComponent<LootTable>();

                if (lootTable == null)
                {
                    lootTable =
                        monster.GetComponentInChildren<LootTable>(
                            true);
                }

                BlightedThrallLoot.ConfigureLoot(
                    lootTable);

                BlightedThrallBehaviour behaviour =
                    monster.GetComponent<BlightedThrallBehaviour>();

                if (behaviour == null)
                {
                    behaviour =
                        monster.AddComponent<BlightedThrallBehaviour>();
                }

                behaviour.Initialize(
                    npc,
                    character,
                    stats);

                // Preserve the captured native hostile faction; only the
                // identity, stats, visuals and FOTF behaviour are replaced.
                RefreshNamePlate(
                    npc);

                _spawnGeneration++;

            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blighted Thrall: configuration failed: " +
                    exception);
            }
        }



        private static void ConfigureNaturalHostility(
            NPC npc,
            Character character)
        {
            if (npc == null ||
                character == null)
            {
                return;
            }

            character.isNPC =
                true;

            character.isVendor =
                false;

            character.Relax =
                false;

            // A captured template can carry behavioural flags that make it
            // unsuitable as an always-hostile monster. Clear those explicitly
            // instead of assuming the source SpawnPoint was configured like
            // raid trash. Reflection keeps this resilient if Erenshor changes
            // field visibility between builds.
            TrySetNpcBool(
                npc,
                "NeverAggro",
                false);

            TrySetNpcBool(
                npc,
                "AggroRegardlessOfLevel",
                true);

            TrySetNpcBool(
                npc,
                "AggroRegardlessofLOS",
                false);

            TrySetNpcBool(
                npc,
                "IgnoreLOSForAggro",
                false);

            TrySetNpcFloat(
                npc,
                "safePointProtect",
                0f);

            // Do not allow a captured stationary/no-run template flag to
            // prevent the Thrall from pursuing its target.
            character.NoRun =
                false;

            character.AggroRange =
                MonsterAggroRange;

            if (character.AggressiveTowards == null)
            {
                character.AggressiveTowards =
                    new List<Character.Faction>();
            }

            AddFactionIfMissing(
                character.AggressiveTowards,
                Character.Faction.Player);

            AddFactionIfMissing(
                character.AggressiveTowards,
                Character.Faction.PC);

            // Some captured templates may have friendly player factions in
            // their ally list. Hostility must win for a Blighted Thrall.
            if (character.Allies != null)
            {
                character.Allies.Remove(
                    Character.Faction.Player);

                character.Allies.Remove(
                    Character.Faction.PC);
            }

            ActivateAggroTrigger(
                character.MyAggro);

            if (npc.AggroArea != null)
            {
                Transform npcAggro =
                    npc.AggroArea.transform;

                ActivateAggroTrigger(
                    npcAggro);
            }

        }


        private static void TrySetNpcBool(
            NPC npc,
            string fieldName,
            bool value)
        {
            TrySetNpcField(
                npc,
                fieldName,
                value);
        }


        private static void TrySetNpcFloat(
            NPC npc,
            string fieldName,
            float value)
        {
            TrySetNpcField(
                npc,
                fieldName,
                value);
        }


        private static void TrySetNpcField(
            NPC npc,
            string fieldName,
            object value)
        {
            if (npc == null ||
                string.IsNullOrEmpty(
                    fieldName))
            {
                return;
            }

            try
            {
                FieldInfo field =
                    typeof(NPC).GetField(
                        fieldName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                if (field == null)
                {
                    return;
                }

                field.SetValue(
                    npc,
                    value);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "Blighted Thrall: could not set native NPC field [" +
                    fieldName +
                    "]: " +
                    exception.Message);
            }
        }


        private static void AddFactionIfMissing(
            List<Character.Faction> factions,
            Character.Faction faction)
        {
            if (factions == null ||
                factions.Contains(
                    faction))
            {
                return;
            }

            factions.Add(
                faction);
        }


        private static void ActivateAggroTrigger(
            Transform aggroRoot)
        {
            if (aggroRoot == null)
            {
                return;
            }

            aggroRoot.gameObject.SetActive(
                true);

            aggroRoot.localScale =
                Vector3.one *
                MonsterAggroRange;

            Collider[] colliders =
                aggroRoot.GetComponentsInChildren<Collider>(
                    true);

            if (colliders == null)
            {
                return;
            }

            for (int i = 0;
                 i < colliders.Length;
                 i++)
            {
                Collider collider =
                    colliders[i];

                if (collider == null)
                {
                    continue;
                }

                collider.enabled =
                    true;

                collider.isTrigger =
                    true;
            }
        }


        private static void RefreshNamePlate(
            NPC npc)
        {
            if (npc == null ||
                npc.NamePlate == null)
            {
                return;
            }

            TextMeshPro text =
                npc.NamePlate.GetComponent<TextMeshPro>();

            if (text != null)
            {
                text.text =
                    MonsterName;
            }
        }


        private static Vector3 ResolveSafeNavMeshPosition(
            Vector3 requestedPosition)
        {
            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                    requestedPosition,
                    out hit,
                    8f,
                    NavMesh.AllAreas))
            {
                return hit.position;
            }

            Plugin.NativeLog.LogWarning(
                "Blighted Thrall: no NavMesh position was found near the spawn marker; using marker position directly.");

            return requestedPosition;
        }


        private static void ApplyBlightVisualTreatment(
            NPC npc)
        {
            if (npc == null)
            {
                return;
            }

            Renderer[] renderers =
                npc.GetComponentsInChildren<Renderer>(
                    true);

            if (renderers == null)
            {
                return;
            }

            Color blightTint =
                new Color(
                    0.34f,
                    0.46f,
                    0.15f,
                    1f);

            Color blightEmission =
                new Color(
                    0.16f,
                    0.025f,
                    0.22f,
                    1f);

            int tintedMaterials =
                0;

            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Renderer renderer =
                    renderers[rendererIndex];

                if (renderer == null ||
                    (!(renderer is MeshRenderer) &&
                     !(renderer is SkinnedMeshRenderer)))
                {
                    continue;
                }

                if (npc.NamePlate != null &&
                    (renderer.transform == npc.NamePlate.transform ||
                     renderer.transform.IsChildOf(
                         npc.NamePlate.transform)))
                {
                    continue;
                }

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

                    if (material.HasProperty(
                            "_Color"))
                    {
                        Color original =
                            material.GetColor(
                                "_Color");

                        material.SetColor(
                            "_Color",
                            Color.Lerp(
                                original,
                                blightTint,
                                0.32f));
                    }

                    if (material.HasProperty(
                            "_EmissionColor"))
                    {
                        material.SetColor(
                            "_EmissionColor",
                            blightEmission *
                            0.55f);

                        material.EnableKeyword(
                            "_EMISSION");
                    }

                    tintedMaterials++;
                }

                renderer.materials =
                    materials;
            }

        }


        private static bool ContainsIgnoreCase(
            string source,
            string value)
        {
            if (string.IsNullOrEmpty(source) ||
                string.IsNullOrEmpty(value))
            {
                return false;
            }

            return source.IndexOf(
                       value,
                       StringComparison.OrdinalIgnoreCase) >=
                   0;
        }


        // ============================================================
        // MANUAL TEST / STATUS
        // ============================================================

        internal static void RebuildCurrentSpawn()
        {
            if (!IsFallenReach(
                    SceneManager.GetActiveScene()))
            {
                AddSystemMessage(
                    "The FOTF monster test is only available inside Fallen Reach.",
                    "yellow");

                return;
            }

            if (_spawnPointTemplate == null)
            {
                AddSystemMessage(
                    "No native hostile SpawnPoint template is cached. Return to a normal Erenshor zone and re-enter with /fotfzone.",
                    "yellow");

                return;
            }

            CoroutineHost.Start(
                SpawnInFallenReach(
                    SceneManager.GetActiveScene()));

            AddSystemMessage(
                "Rebuilding the Blighted Thrall SpawnPoint.",
                "");
        }


        internal static void WriteStatusToLog()
        {
            string templateState =
                _spawnPointTemplate != null
                    ? "yes"
                    : "no";

            string runtimeState =
                _runtimeSpawnPoint != null
                    ? "yes"
                    : "no";

            string npcState =
                _runtimeSpawnPoint != null &&
                _runtimeSpawnPoint.SpawnedNPC != null
                    ? _runtimeSpawnPoint.SpawnedNPC.NPCName
                    : "<none>";

            Character currentCharacter =
                _runtimeSpawnPoint != null &&
                _runtimeSpawnPoint.SpawnedNPC != null
                    ? _runtimeSpawnPoint.SpawnedNPC.GetChar()
                    : null;

            NavMeshAgent currentAgent =
                _runtimeSpawnPoint != null &&
                _runtimeSpawnPoint.SpawnedNPC != null
                    ? _runtimeSpawnPoint.SpawnedNPC.GetComponent<NavMeshAgent>()
                    : null;


            AddSystemMessage(
                "FOTF monster status written to the log.",
                "");
        }


        // ============================================================
        // CLEANUP
        // ============================================================

        private static void DestroyRuntimeSpawnPoint()
        {
            _lastConfiguredNpc =
                null;

            _respawnPending =
                false;

            _respawnAtTime =
                0f;

            _runtimeSpawnPoint =
                null;

            if (_runtimeSpawnPointObject != null)
            {
                UnityEngine.Object.Destroy(
                    _runtimeSpawnPointObject);

                _runtimeSpawnPointObject =
                    null;
            }
        }


        private static void EnsureRaidTrashPrototype(
            NPC sourceNpc)
        {
            if (sourceNpc == null ||
                HasRaidTrashPrototype)
            {
                return;
            }

            GameObject prototype =
                null;

            try
            {
                prototype =
                    UnityEngine.Object.Instantiate(
                        sourceNpc.gameObject);

                if (prototype == null)
                {
                    return;
                }

                prototype.name =
                    "FOTF_BlightedThrall_RaidPrototype";

                // DontDestroyOnLoad is only guaranteed for root objects.
                // Detach the template from any copied SpawnPoint hierarchy
                // before making it session-persistent.
                prototype.transform.SetParent(
                    null,
                    true);

                // The source is captured immediately after FOTF has restored
                // full HP, cleared aggro and configured all native components,
                // so this becomes a clean repeatable raid-trash source.
                prototype.SetActive(
                    false);

                UnityEngine.Object.DontDestroyOnLoad(
                    prototype);

                NPC prototypeNpc =
                    prototype.GetComponent<NPC>();

                if (prototypeNpc == null)
                {
                    UnityEngine.Object.Destroy(
                        prototype);

                    return;
                }

                _raidTrashPrototypeObject =
                    prototype;

                _raidTrashPrototypeNpc =
                    prototypeNpc;

            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[RaidPoC] failed to capture persistent Blighted Thrall respawn prototype: " +
                    exception);

                if (prototype != null)
                {
                    UnityEngine.Object.Destroy(
                        prototype);
                }
            }
        }


        private static void DestroyRaidTrashPrototype()
        {
            _raidTrashPrototypeNpc =
                null;

            if (_raidTrashPrototypeObject != null)
            {
                UnityEngine.Object.Destroy(
                    _raidTrashPrototypeObject);

                _raidTrashPrototypeObject =
                    null;
            }
        }


        private static void DestroyTemplate()
        {
            if (_spawnPointTemplate != null)
            {
                UnityEngine.Object.Destroy(
                    _spawnPointTemplate);

                _spawnPointTemplate =
                    null;
            }

            _capturedNativeMonsterName =
                null;
        }


        // ============================================================
        // HELPERS
        // ============================================================

        private static bool IsFallenReach(
            Scene scene)
        {
            return
                scene.IsValid() &&
                string.Equals(
                    scene.name,
                    CustomZoneLoader.FallenReachSceneName,
                    StringComparison.OrdinalIgnoreCase);
        }


        private static void AddSystemMessage(
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
            }
        }
    }


    /// <summary>
    /// Small FOTF behaviour layer placed on top of Erenshor's native hostile
    /// NPC runtime. Navigation, attacks and death remain native. Hostility and
    /// respawn timing are explicitly owned by FOTF for deterministic testing.
    /// This component also adds the Blighted Thrall's signature biology.
    /// </summary>
    internal sealed class BlightedThrallBehaviour :
        MonoBehaviour
    {
        private const float RupturingGrowthCooldown =
            9f;

        private const float RupturingGrowthRange =
            4.75f;

        private const int RupturingGrowthDamage =
            300;

        private const float RegrowthThreshold =
            0.30f;

        private const float RegrowthHealFraction =
            0.12f;

        private const float AggroFallbackScanInterval =
            0.50f;

        private static Character[] _sharedAggroCharacters =
            new Character[0];

        private static float _sharedAggroCharacterScanTime;


        private NPC _npc;
        private Character _character;
        private Stats _stats;

        private float _nextRupturingGrowthTime;
        private float _nextAggroFallbackScanTime;
        private bool _regrowthUsed;
        private bool _loggedDeath;
        private bool _suppressSignatureAbilities;
        private Character _engagedTarget;

        internal void Initialize(
            NPC npc,
            Character character,
            Stats stats)
        {
            _npc =
                npc;

            _character =
                character;

            _stats =
                stats;

            _nextRupturingGrowthTime =
                Time.time +
                UnityEngine.Random.Range(
                    4f,
                    7f);

            _nextAggroFallbackScanTime =
                Time.time;

            _regrowthUsed =
                false;

            _loggedDeath =
                false;

            _suppressSignatureAbilities =
                false;

            _engagedTarget =
                null;
        }


        internal void SetRaidBossMode()
        {
            _suppressSignatureAbilities =
                true;

            _regrowthUsed =
                true;

            _nextRupturingGrowthTime =
                float.PositiveInfinity;
        }


        private void Awake()
        {
            if (_npc == null)
            {
                _npc =
                    GetComponent<NPC>();
            }

            if (_character == null)
            {
                _character =
                    GetComponent<Character>();
            }

            if (_stats == null)
            {
                _stats =
                    GetComponent<Stats>();
            }

        }

        private void OnDisable()
        {
            TryNotifyRaidDeathFromLifecycleEvent(
                "OnDisable");
        }


        private void OnDestroy()
        {
            TryNotifyRaidDeathFromLifecycleEvent(
                "OnDestroy");
        }


        private void TryNotifyRaidDeathFromLifecycleEvent(
            string source)
        {
            if (_loggedDeath ||
                !CustomRaidLoader.IsRaidPocScene(
                    SceneManager.GetActiveScene()))
            {
                return;
            }

            bool dead =
                (_character != null &&
                 !_character.Alive) ||
                (_stats != null &&
                 _stats.CurrentHP <= 0);

            // Scene unload disables/destroys perfectly healthy NPCs too. Only
            // convert the lifecycle callback into a death when native combat
            // state actually says the Thrall died.
            if (!dead)
            {
                return;
            }

            RaidPhaseAddIdentity phaseAddIdentity =
                GetComponent<RaidPhaseAddIdentity>();

            RaidBossIdentity bossIdentity =
                GetComponent<RaidBossIdentity>();

            RaidTrashIdentity identity =
                GetComponent<RaidTrashIdentity>();

            if (phaseAddIdentity != null)
            {
                // Phase 2 adds are encounter-scoped priority targets, not
                // persistent 44-minute raid-trash lifecycle slots.
                RaidPhaseTwoAddRuntime.NotifyAddDeath(
                    source);

                _loggedDeath =
                    true;
            }
            else if (bossIdentity != null)
            {
                // The identity is sufficient here; OnDisable/OnDestroy may run
                // after Unity has already invalidated the NPC component.
                RaidPocEncounterRuntime.NotifyRaidBossDeath(
                    source);

                _loggedDeath =
                    true;
            }
            else if (identity != null &&
                     identity.StateIndex >= 0)
            {
                RaidPocEncounterRuntime.NotifyRaidTrashDeath(
                    identity.StateIndex,
                    source);

                _loggedDeath =
                    true;
            }
            else if (_npc != null)
            {
                RaidPocEncounterRuntime.NotifyRaidTrashDeath(
                    _npc);

                _loggedDeath =
                    true;
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] Blighted Thrall death reached [" +
                    source +
                    "] without a raid identity or live NPC reference; the encounter poll will perform the fallback capture.");
            }

        }


        private void Update()
        {
            if (_npc == null ||
                _character == null ||
                _stats == null)
            {
                return;
            }

            bool alive =
                _character.Alive &&
                _stats.CurrentHP > 0;

            if (!alive)
            {
                if (!_loggedDeath)
                {
                    _loggedDeath =
                        true;

                    // Raid trash owns a separate persistent per-enemy
                    // lifecycle. Fallen Reach keeps its original singleton
                    // spawn-controller timer.
                    if (CustomRaidLoader.IsRaidPocScene(
                            SceneManager.GetActiveScene()))
                    {
                        RaidPhaseAddIdentity phaseAddIdentity =
                            GetComponent<RaidPhaseAddIdentity>();

                        RaidBossIdentity bossIdentity =
                            GetComponent<RaidBossIdentity>();

                        if (phaseAddIdentity != null)
                        {
                            RaidPhaseTwoAddRuntime.NotifyAddDeath(
                                _npc,
                                "Update");

                        }
                        else if (bossIdentity != null)
                        {
                            RaidPocEncounterRuntime.NotifyRaidBossDeath(
                                _npc,
                                "Update");

                        }
                        else
                        {
                            RaidPocEncounterRuntime.NotifyRaidTrashDeath(
                                _npc);

                        }
                    }
                    else
                    {
                        // Hand Fallen Reach death off to the existing static
                        // spawn controller before native corpse handling can
                        // destroy this component.
                        BlightedThrallRuntime.NotifyMonsterDeath();

                    }
                }

                return;
            }

            // NPC.AggroOn is Erenshor's real hostile-engagement entry point.
            // Once it has accepted a target, native NPC AI owns threat,
            // movement, attack range, swing timing and target changes.
            Character target =
                _npc.CurrentAggroTarget;

            if (!IsValidPlayerTarget(
                    target))
            {
                _engagedTarget =
                    null;

                TryAcquireNaturalAggro();

                target =
                    _npc.CurrentAggroTarget;
            }

            if (!IsValidPlayerTarget(
                    target))
            {
                return;
            }

            if (_engagedTarget !=
                target)
            {
                _engagedTarget =
                    target;

                NavMeshAgent agent =
                    GetComponent<NavMeshAgent>();

            }

            if (_suppressSignatureAbilities)
            {
                return;
            }

            TryUncontrolledRegrowth();
            TryRupturingGrowth(
                target);
        }


        private void TryAcquireNaturalAggro()
        {
            if (_engagedTarget != null ||
                Time.time <
                    _nextAggroFallbackScanTime ||
                _npc == null ||
                _character == null ||
                !_character.Alive)
            {
                return;
            }

            _nextAggroFallbackScanTime =
                Time.time +
                AggroFallbackScanInterval;

            Character[] characters;

            if (_sharedAggroCharacters == null ||
                Time.time >= _sharedAggroCharacterScanTime)
            {
                _sharedAggroCharacterScanTime =
                    Time.time + AggroFallbackScanInterval;

                _sharedAggroCharacters =
                    UnityEngine.Object.FindObjectsOfType<Character>();

                if (_sharedAggroCharacters == null)
                {
                    _sharedAggroCharacters =
                        new Character[0];
                }
            }

            characters = _sharedAggroCharacters;

            if (characters == null ||
                characters.Length == 0)
            {
                return;
            }

            Character nearest =
                null;

            float nearestDistance =
                float.PositiveInfinity;

            for (int i = 0;
                 i < characters.Length;
                 i++)
            {
                Character candidate =
                    characters[i];

                if (!IsValidPlayerTarget(
                        candidate) ||
                    candidate == _character)
                {
                    continue;
                }

                float distance =
                    (transform.position - candidate.transform.position).sqrMagnitude;

                float aggroRangeSquared =
                    BlightedThrallRuntime.MonsterAggroRange *
                    BlightedThrallRuntime.MonsterAggroRange;

                if (distance > aggroRangeSquared ||
                    distance >= nearestDistance)
                {
                    continue;
                }

                bool hasLineOfSight =
                    true;

                try
                {
                    hasLineOfSight =
                        _npc.CheckLOS(
                            candidate);
                }
                catch
                {
                    // Keep the PoC functional if a captured template does not
                    // expose normal LOS wiring in the custom scene.
                }

                if (!hasLineOfSight)
                {
                    continue;
                }

                nearest =
                    candidate;

                nearestDistance =
                    distance;
            }

            if (nearest == null)
            {
                return;
            }

            EngageTarget(
                nearest,
                true);
        }


        private void EngageTarget(
            Character target,
            bool seedNativeAggro)
        {
            if (!IsValidPlayerTarget(
                    target))
            {
                return;
            }

            bool changed =
                _engagedTarget !=
                target;

            _engagedTarget =
                target;

            if (seedNativeAggro)
            {
                try
                {
                    // IMPORTANT: ForceAggroOn is used by SimPlayer command
                    // logic and is not sufficient to put a hostile NPC into
                    // its full combat state. AggroOn is the native hostile
                    // entry point and initializes the state used by pursuit
                    // and melee attacks.
                    _npc.AggroOn(
                        target);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "Blighted Thrall: native AggroOn failed: " +
                        exception);

                    return;
                }
            }

            if (!changed)
            {
                return;
            }

            float distance =
                Vector3.Distance(
                    transform.position,
                    target.transform.position);

        }


        private bool IsValidPlayerTarget(
            Character target)
        {
            if (target == null ||
                !target.Alive ||
                target.MyStats == null ||
                target.MyStats.CurrentHP <= 0)
            {
                return false;
            }

            return
                target.MyFaction ==
                    Character.Faction.Player ||
                target.MyFaction ==
                    Character.Faction.PC;
        }


        private void TryRupturingGrowth(
            Character target)
        {
            if (target == null ||
                target.MyStats == null ||
                Time.time <
                    _nextRupturingGrowthTime)
            {
                return;
            }

            float distance =
                Vector3.Distance(
                    transform.position,
                    target.transform.position);

            if (distance >
                RupturingGrowthRange)
            {
                return;
            }

            _nextRupturingGrowthTime =
                Time.time +
                RupturingGrowthCooldown;

            try
            {
                int resolvedDamage =
                    target.MagicDamageMe(
                        RupturingGrowthDamage,
                        false,
                        GameData.DamageType.Poison,
                        _character,
                        0f,
                        RupturingGrowthDamage);

            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blighted Thrall: Rupturing Growth failed: " +
                    exception);
            }
        }

        private void TryUncontrolledRegrowth()
        {
            if (_regrowthUsed ||
                _stats.CurrentMaxHP <= 0)
            {
                return;
            }

            float hpPercent =
                (float)_stats.CurrentHP /
                (float)_stats.CurrentMaxHP;

            if (hpPercent >
                RegrowthThreshold)
            {
                return;
            }

            _regrowthUsed =
                true;

            int healing =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        _stats.CurrentMaxHP *
                        RegrowthHealFraction));

            int before =
                _stats.CurrentHP;

            _stats.CurrentHP =
                Mathf.Min(
                    _stats.CurrentMaxHP,
                    _stats.CurrentHP +
                    healing);

            int actualHealing =
                _stats.CurrentHP -
                before;

            EmitRegrowthMessage();

        }

        private void EmitRegrowthMessage()
        {
            try
            {
                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        BlightedThrallRuntime.MonsterName +
                        "'s flesh convulses and knits itself back together!",
                        ChatLogLine.LogType.NPCEmotes,
                        "yellow"));
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Source-backed melee baseline override.
    ///
    /// NPC.PerformMeleeHit() applies its own range, server and level scaling
    /// before entering Character.DamageMe(). For the Blighted Thrall we want
    /// the configured value to mean "4000 raw physical damage entering
    /// Erenshor's native physical-mitigation calculation".
    ///
    /// The original DamageMe method is NOT skipped. Erenshor still performs
    /// its normal hit consequences, mitigation routine, combat logging,
    /// damage shields, HP reduction, death handling and aggro behavior.
    ///
    /// NPC.NoDmgCap is enabled on the Thrall because DamageMe contains a
    /// separate post-mitigation Level*15 NPC cap that would otherwise turn
    /// this 4000 value back into roughly 600-670 damage at level 42.
    /// </summary>
    [HarmonyPatch(
        typeof(Character),
        "DamageMe")]
    internal static class Patch_BlightedThrallMeleeBaseline
    {
        private static bool _loggedFirstHit;

        [HarmonyPrefix]
        private static void Prefix(
            Character __instance,
            ref int _incdmg,
            bool _fromPlayer,
            GameData.DamageType _dmgType,
            Character _attacker,
            bool _animEffect,
            bool _criticalHit,
            int _bonusDmg)
        {
            if (__instance == null ||
                _attacker == null ||
                _dmgType != GameData.DamageType.Physical ||
                !_animEffect)
            {
                return;
            }

            NPC attackerNpc =
                _attacker.MyNPC;

            if (attackerNpc == null)
            {
                attackerNpc =
                    _attacker.GetComponent<NPC>();
            }

            if (attackerNpc == null)
            {
                return;
            }

            bool isRaidBoss =
                attackerNpc.GetComponent<RaidBossIdentity>() != null ||
                string.Equals(
                    attackerNpc.NPCName,
                    RaidPocEncounterRuntime.RaidBossDisplayName,
                    StringComparison.Ordinal);

            bool isPhaseAdd =
                attackerNpc.GetComponent<RaidPhaseAddIdentity>() != null ||
                string.Equals(
                    attackerNpc.NPCName,
                    RaidPhaseTwoAddRuntime.AddDisplayName,
                    StringComparison.Ordinal);

            bool isBlightedThrall =
                string.Equals(
                    attackerNpc.NPCName,
                    BlightedThrallRuntime.MonsterName,
                    StringComparison.Ordinal);

            if (!isRaidBoss &&
                !isPhaseAdd &&
                !isBlightedThrall)
            {
                return;
            }

            int nativeIncoming =
                _incdmg;

            _incdmg =
                isRaidBoss
                    ? RaidPocEncounterRuntime.RaidBossBaseAttackDamage
                    : (isPhaseAdd
                        ? RaidPhaseTwoAddRuntime.AddBaseAttackDamage
                        : BlightedThrallRuntime.MonsterBaseAttackDamage);

            if (!_loggedFirstHit)
            {
                _loggedFirstHit =
                    true;

            }
        }
    }


    /// <summary>
    /// The known-good Thrall combat state uses ArmorPenPercentage=200 at
    /// runtime. Removing that value caused the native NPC melee path to stop
    /// functioning during testing.
    ///
    /// We still want the target's real AC to mitigate the Thrall's 4000 raw
    /// melee hit. Therefore, only for a Blighted Thrall physical mitigation
    /// call, temporarily expose 0% armor penetration to Erenshor's original
    /// Stats.MitigatePhysical() implementation, then restore the exact prior
    /// value immediately afterward.
    ///
    /// The original mitigation method is never skipped or replaced.
    /// </summary>
    [HarmonyPatch(
        typeof(Stats),
        nameof(Stats.MitigatePhysical))]
    internal static class Patch_BlightedThrallNativeArmorMitigation
    {
        private struct ArmorPenState
        {
            internal bool Applied;
            internal float PreviousArmorPen;
        }

        [HarmonyPrefix]
        private static void Prefix(
            Stats __instance,
            Stats _attacker,
            out ArmorPenState __state)
        {
            __state =
                default(ArmorPenState);

            if (__instance == null ||
                _attacker == null ||
                _attacker.Myself == null)
            {
                return;
            }

            NPC attackerNpc =
                _attacker.Myself.MyNPC;

            if (attackerNpc == null)
            {
                attackerNpc =
                    _attacker.GetComponent<NPC>();
            }

            if (attackerNpc == null)
            {
                return;
            }

            bool isRaidBoss =
                attackerNpc.GetComponent<RaidBossIdentity>() != null ||
                string.Equals(
                    attackerNpc.NPCName,
                    RaidPocEncounterRuntime.RaidBossDisplayName,
                    StringComparison.Ordinal);

            bool isPhaseAdd =
                attackerNpc.GetComponent<RaidPhaseAddIdentity>() != null ||
                string.Equals(
                    attackerNpc.NPCName,
                    RaidPhaseTwoAddRuntime.AddDisplayName,
                    StringComparison.Ordinal);

            bool isBlightedThrall =
                string.Equals(
                    attackerNpc.NPCName,
                    BlightedThrallRuntime.MonsterName,
                    StringComparison.Ordinal);

            if (!isRaidBoss &&
                !isPhaseAdd &&
                !isBlightedThrall)
            {
                return;
            }

            __state.Applied =
                true;

            __state.PreviousArmorPen =
                _attacker.ArmorPenPercentage;

            // Allow the target's actual AC/class mitigation to participate.
            _attacker.ArmorPenPercentage =
                0f;
        }

        [HarmonyPostfix]
        private static void Postfix(
            Stats _attacker,
            ArmorPenState __state)
        {
            if (!__state.Applied ||
                _attacker == null)
            {
                return;
            }

            _attacker.ArmorPenPercentage =
                __state.PreviousArmorPen;
        }
    }

}
