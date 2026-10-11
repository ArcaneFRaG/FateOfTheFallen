using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// v0.2 open-world boss lifecycle.
    ///
    /// Native Erenshor raid creation/assignment stays untouched. FOTF owns only
    /// the boss spawn, leash/reset, death timer, loot/credit and scripted boss
    /// mechanics.
    /// </summary>
    internal static class WorldBossRuntime
    {
        private const float DevelopmentSpawnDistance =
            18f;

        private const float NavMeshSampleRadius =
            10f;

        private static bool _initialized;
        private static int _generation;
        private static bool _spawnInProgress;

        private static NPC _currentBoss;
        private static string _homeSceneName;
        private static Vector3 _homePosition;
        private static Quaternion _homeRotation;
        private static bool _hasHome;

        private static bool _dead;
        private static DateTime _respawnAtUtc;

        internal static NPC CurrentBoss
        {
            get
            {
                return _currentBoss;
            }
        }

        internal static bool IsDead
        {
            get
            {
                return _dead;
            }
        }

        internal static void RegisterCustomDevelopmentBoss(
            NPC boss,
            Vector3 homePosition)
        {
            if (boss == null)
            {
                return;
            }

            Scene scene =
                SceneManager.GetActiveScene();

            _generation++;

            _currentBoss =
                boss;

            _homePosition =
                homePosition;

            _homeRotation =
                boss.transform.rotation;

            _homeSceneName =
                scene.IsValid()
                    ? scene.name
                    : string.Empty;

            _hasHome =
                scene.IsValid() &&
                scene.isLoaded;

            _dead =
                false;

            _respawnAtUtc =
                DateTime.MinValue;

            _spawnInProgress =
                false;

            Plugin.NativeLog.LogInfo(
                "[WorldBoss] custom development boss registered. Scene=[" +
                _homeSceneName +
                "] Home=[" +
                _homePosition +
                "].");
        }


        internal static void UnregisterCustomDevelopmentBoss(
            NPC boss)
        {
            if (boss == null ||
                _currentBoss != boss)
            {
                return;
            }

            _generation++;

            _currentBoss =
                null;

            _hasHome =
                false;

            _homeSceneName =
                null;

            _dead =
                false;

            _respawnAtUtc =
                DateTime.MinValue;

            _spawnInProgress =
                false;
        }


        internal static bool TryGetHome(
            out Vector3 position)
        {
            position =
                _homePosition;

            return _hasHome;
        }

        internal static bool IsCurrentWorldBossStats(
            Stats stats)
        {
            if (stats == null ||
                _currentBoss == null)
            {
                return false;
            }

            Stats bossStats =
                _currentBoss.GetComponent<Stats>();

            return bossStats != null &&
                bossStats == stats;
        }

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

            _generation++;
            _spawnInProgress =
                false;

            WorldBossEncounterRuntime.Stop(
                "world-boss module shutdown");

            _currentBoss =
                null;
        }

        internal static bool SpawnDevelopmentBossNearPlayer()
        {
            if (_spawnInProgress)
            {
                LogSystemMessage(
                    "A Fate of the Fallen world boss spawn is already in progress.",
                    "yellow");

                return false;
            }

            PlayerControl playerControl =
                GameData.PlayerControl;

            Character player =
                playerControl != null
                    ? playerControl.Myself
                    : null;

            if (player == null)
            {
                LogSystemMessage(
                    "The player character is not available yet.",
                    "yellow");

                return false;
            }

            Scene scene =
                SceneManager.GetActiveScene();

            if (!scene.IsValid() ||
                !scene.isLoaded ||
                CustomRaidLoader.IsRaidPocScene(scene))
            {
                LogSystemMessage(
                    "The v0.2 world-boss development spawn must be used in a normal Erenshor world zone.",
                    "yellow");

                return false;
            }

            if (_currentBoss != null &&
                IsNpcAlive(_currentBoss))
            {
                LogSystemMessage(
                    "The v0.2 development world boss is already alive.",
                    "yellow");

                return false;
            }

            if (!BlightedThrallRuntime.HasCapturedNativeTemplate)
            {
                BlightedThrallRuntime.CaptureNativeTemplateBeforeZoneChange();
            }

            if (!BlightedThrallRuntime.HasCapturedNativeTemplate &&
                !BlightedThrallRuntime.HasRaidTrashPrototype)
            {
                LogSystemMessage(
                    "No hostile NPC template is available in this session. Move into a normal combat zone with hostile NPCs and try again.",
                    "yellow");

                return false;
            }

            Vector3 desired =
                player.transform.position +
                (player.transform.forward * DevelopmentSpawnDistance);

            Vector3 spawnPosition =
                ResolveSafeNavMeshPosition(
                    desired);

            _homeSceneName =
                scene.name;

            _homePosition =
                spawnPosition;

            _homeRotation =
                Quaternion.LookRotation(
                    -player.transform.forward,
                    Vector3.up);

            _hasHome =
                true;

            _dead =
                false;

            _respawnAtUtc =
                DateTime.MinValue;

            StartSpawn(
                scene,
                "development command");

            return true;
        }

        internal static bool ResetDevelopmentBoss()
        {
            if (!_hasHome)
            {
                LogSystemMessage(
                    "No v0.2 development world-boss home point has been recorded yet.",
                    "yellow");

                return false;
            }

            _generation++;
            _spawnInProgress =
                false;

            WorldBossEncounterRuntime.Stop(
                "development reset");

            if (_currentBoss != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(
                        _currentBoss.gameObject);
                }
                catch
                {
                }
            }

            _currentBoss =
                null;

            _dead =
                false;

            _respawnAtUtc =
                DateTime.MinValue;

            Scene scene =
                SceneManager.GetActiveScene();

            if (scene.IsValid() &&
                scene.isLoaded &&
                string.Equals(
                    scene.name,
                    _homeSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                StartSpawn(
                    scene,
                    "development reset");
            }

            return true;
        }

        internal static void WriteStatusToLog()
        {
            WorldBossDefinition definition =
                WorldBossCatalog.BlightTouchedColossus;

            float respawnSeconds =
                _dead
                    ? Mathf.Max(
                        0f,
                        (float)(_respawnAtUtc - DateTime.UtcNow).TotalSeconds)
                    : 0f;

            Plugin.NativeLog.LogInfo(
                "[WorldBoss] Status Id=[" +
                definition.Id +
                "] Name=[" +
                definition.DisplayName +
                "] HomeScene=[" +
                (_homeSceneName ?? "<none>") +
                "] HasHome=[" +
                _hasHome +
                "] SpawnInProgress=[" +
                _spawnInProgress +
                "] Alive=[" +
                IsNpcAlive(_currentBoss) +
                "] Dead=[" +
                _dead +
                "] RespawnRemaining=[" +
                respawnSeconds.ToString("0") +
                "s] NativeRaidSlots=[" +
                CountNativeRaidMembers() +
                "].");
        }

        internal static void NotifyBossDeath(
            NPC boss,
            string source)
        {
            if (_dead ||
                boss == null ||
                _currentBoss == null ||
                boss != _currentBoss)
            {
                return;
            }

            if (IsNpcAlive(boss))
            {
                return;
            }

            WorldBossDefinition definition =
                WorldBossCatalog.BlightTouchedColossus;

            _dead =
                true;

            _respawnAtUtc =
                DateTime.UtcNow.AddSeconds(
                    definition.RespawnDelaySeconds);

            WorldBossEncounterRuntime.Stop(
                "world boss death");

            WorldBossLoot.AwardCompletionCredit();

            Plugin.NativeLog.LogInfo(
                "[WorldBoss] boss death recorded from [" +
                source +
                "]. RespawnIn=[" +
                definition.RespawnDelaySeconds.ToString("0") +
                "s]. Native corpse/loot remains in the world.");

            LogSystemMessage(
                definition.DisplayName +
                " has been defeated.",
                "yellow");

            Scene scene =
                SceneManager.GetActiveScene();

            if (scene.IsValid() &&
                scene.isLoaded &&
                string.Equals(
                    scene.name,
                    _homeSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                int generation =
                    ++_generation;

                CoroutineHost.Start(
                    WaitForRespawnInScene(
                        scene,
                        generation));
            }
        }

        internal static void ResetCurrentBossToHome(
            string reason)
        {
            NPC boss =
                _currentBoss;

            if (boss == null ||
                _dead ||
                !_hasHome)
            {
                return;
            }

            WorldBossEncounterRuntime.Stop(
                "boss reset: " +
                reason);

            try
            {
                boss.enabled =
                    true;

                boss.CurrentAggroTarget =
                    null;

                boss.retreat =
                    false;

                Character character =
                    boss.GetChar();

                if (character == null)
                {
                    character =
                        boss.GetComponent<Character>();
                }

                Stats stats =
                    boss.GetComponent<Stats>();

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

                NavMeshAgent agent =
                    boss.GetComponent<NavMeshAgent>();

                Vector3 home =
                    ResolveSafeNavMeshPosition(
                        _homePosition);

                if (agent != null &&
                    agent.enabled &&
                    agent.isOnNavMesh)
                {
                    agent.isStopped =
                        true;

                    agent.velocity =
                        Vector3.zero;

                    if (agent.hasPath)
                    {
                        agent.ResetPath();
                    }

                    agent.Warp(
                        home);

                    agent.isStopped =
                        false;
                }
                else
                {
                    boss.transform.position =
                        home;
                }

                boss.transform.rotation =
                    _homeRotation;

                Plugin.NativeLog.LogInfo(
                    "[WorldBoss] encounter reset to home. Reason=[" +
                    reason +
                    "] Position=[" +
                    home +
                    "].");
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[WorldBoss] failed to reset boss to home: " +
                    exception);
            }

            WorldBossEncounterRuntime.AttachBoss(
                boss,
                _homePosition);
        }

        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            WorldBossEncounterRuntime.Stop(
                "scene loaded");

            if (!_hasHome ||
                !scene.IsValid() ||
                !scene.isLoaded ||
                !string.Equals(
                    scene.name,
                    _homeSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                _currentBoss =
                    null;

                return;
            }

            _currentBoss =
                null;

            if (_dead &&
                DateTime.UtcNow < _respawnAtUtc)
            {
                float remaining =
                    (float)(_respawnAtUtc - DateTime.UtcNow).TotalSeconds;

                Plugin.NativeLog.LogInfo(
                    "[WorldBoss] home scene entered while boss is dead. RespawnRemaining=[" +
                    remaining.ToString("0") +
                    "s].");

                int generation =
                    ++_generation;

                CoroutineHost.Start(
                    WaitForRespawnInScene(
                        scene,
                        generation));

                return;
            }

            StartSpawn(
                scene,
                _dead
                    ? "respawn timer elapsed while away"
                    : "home scene re-entry");
        }

        private static IEnumerator WaitForRespawnInScene(
            Scene scene,
            int generation)
        {
            while (_initialized &&
                   generation == _generation &&
                   scene.IsValid() &&
                   scene.isLoaded &&
                   DateTime.UtcNow < _respawnAtUtc)
            {
                yield return null;
            }

            if (!_initialized ||
                generation != _generation ||
                !scene.IsValid() ||
                !scene.isLoaded)
            {
                yield break;
            }

            StartSpawn(
                scene,
                "respawn timer elapsed");
        }

        private static void StartSpawn(
            Scene scene,
            string reason)
        {
            if (_spawnInProgress ||
                !_hasHome ||
                !scene.IsValid() ||
                !scene.isLoaded ||
                !string.Equals(
                    scene.name,
                    _homeSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _generation++;

            int generation =
                _generation;

            _spawnInProgress =
                true;

            CoroutineHost.Start(
                SpawnBoss(
                    scene,
                    generation,
                    reason));
        }

        private static IEnumerator SpawnBoss(
            Scene scene,
            int generation,
            string reason)
        {
            if (_currentBoss != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(
                        _currentBoss.gameObject);
                }
                catch
                {
                }

                _currentBoss =
                    null;
            }

            GameObject markerObject =
                new GameObject(
                    "FOTF_WorldBoss_DevSpawnMarker");

            markerObject.transform.SetPositionAndRotation(
                ResolveSafeNavMeshPosition(
                    _homePosition),
                _homeRotation);

            SceneManager.MoveGameObjectToScene(
                markerObject,
                scene);

            NPC spawned =
                null;

            IEnumerator spawnRoutine =
                BlightedThrallRuntime.HasRaidTrashPrototype
                    ? BlightedThrallRuntime.CloneRaidTrashAtMarker(
                        scene,
                        null,
                        markerObject.transform,
                        "WorldBoss_01",
                        npc => spawned = npc)
                    : BlightedThrallRuntime.SpawnRaidTrashAtMarker(
                        scene,
                        markerObject.transform,
                        "WorldBoss_01",
                        npc => spawned = npc);

            yield return spawnRoutine;

            if (markerObject != null)
            {
                UnityEngine.Object.Destroy(
                    markerObject);
            }

            if (!_initialized ||
                generation != _generation ||
                !scene.IsValid() ||
                !scene.isLoaded)
            {
                if (spawned != null)
                {
                    UnityEngine.Object.Destroy(
                        spawned.gameObject);
                }

                yield break;
            }

            if (spawned == null)
            {
                _spawnInProgress =
                    false;

                Plugin.NativeLog.LogError(
                    "[WorldBoss] failed to create the v0.2 development boss.");

                yield break;
            }

            ConfigureWorldBoss(
                spawned);

            _currentBoss =
                spawned;

            _dead =
                false;

            _respawnAtUtc =
                DateTime.MinValue;

            _spawnInProgress =
                false;

            WorldBossEncounterRuntime.AttachBoss(
                spawned,
                _homePosition);

            WorldBossDefinition definition =
                WorldBossCatalog.BlightTouchedColossus;

            Plugin.NativeLog.LogInfo(
                "[WorldBoss] READY Name=[" +
                definition.DisplayName +
                "] HP=[" +
                definition.MaxHP +
                "] Scene=[" +
                scene.name +
                "] Position=[" +
                spawned.transform.position +
                "] Reason=[" +
                reason +
                "] NativeRaidSlots=[" +
                CountNativeRaidMembers() +
                "].");

            LogSystemMessage(
                definition.DisplayName +
                " has manifested nearby.",
                "yellow");
        }

        private static void ConfigureWorldBoss(
            NPC npc)
        {
            WorldBossDefinition definition =
                WorldBossCatalog.BlightTouchedColossus;

            GameObject bossObject =
                npc.gameObject;

            bossObject.name =
                definition.DisplayName;

            npc.NPCName =
                definition.DisplayName;

            npc.BaseAtkDmg =
                definition.BaseAttackDamage;

            npc.DamageRange =
                new Vector2(
                    definition.BaseAttackDamage,
                    definition.BaseAttackDamage);

            npc.NoDmgCap =
                true;

            npc.noClick =
                false;

            npc.CurrentAggroTarget =
                null;

            npc.retreat =
                false;

            bossObject.transform.localScale =
                bossObject.transform.localScale *
                definition.ScaleMultiplier;

            Stats stats =
                bossObject.GetComponent<Stats>();

            if (stats != null)
            {
                stats.MyName =
                    definition.DisplayName;

                stats.Level =
                    definition.Level;

                stats.BaseHP =
                    definition.MaxHP;

                stats.BaseMHAtkDelay =
                    Mathf.Max(
                        1f,
                        stats.BaseMHAtkDelay /
                        Mathf.Max(
                            0.01f,
                            definition.AttackSpeedMultiplier));

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

            WorldBossIdentity identity =
                bossObject.GetComponent<WorldBossIdentity>();

            if (identity == null)
            {
                identity =
                    bossObject.AddComponent<WorldBossIdentity>();
            }

            identity.Initialize(
                npc,
                definition.Id);

            WorldBossLoot.ConfigureBossLoot(
                npc);

            BlightedThrallRuntime.RefreshRaidNpcNamePlate(
                npc);
        }

        private static Vector3 ResolveSafeNavMeshPosition(
            Vector3 desired)
        {
            NavMeshHit hit;

            return NavMesh.SamplePosition(
                    desired,
                    out hit,
                    NavMeshSampleRadius,
                    NavMesh.AllAreas)
                ? hit.position
                : desired;
        }

        private static bool IsNpcAlive(
            NPC npc)
        {
            if (npc == null)
            {
                return false;
            }

            Character character =
                npc.GetChar();

            if (character == null)
            {
                character =
                    npc.GetComponent<Character>();
            }

            Stats stats =
                npc.GetComponent<Stats>();

            return character != null &&
                character.Alive &&
                stats != null &&
                stats.CurrentHP > 0;
        }

        private static int CountNativeRaidMembers()
        {
            RaidManager manager =
                GameData.RaidManager;

            if (manager == null ||
                manager.Raiders == null)
            {
                return 0;
            }

            int count =
                0;

            foreach (RaidMemberSlot slot in manager.Raiders)
            {
                if (slot != null &&
                    slot.AssignedAvatar != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static void LogSystemMessage(
            string message,
            string color)
        {
            try
            {
                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        message,
                        ChatLogLine.LogType.SystemMessages,
                        color ?? string.Empty));
            }
            catch
            {
            }
        }
    }

    internal sealed class WorldBossIdentity :
        MonoBehaviour
    {
        internal string BossId
        {
            get;
            private set;
        }

        private NPC _npc;
        private Character _character;
        private Stats _stats;
        private bool _deathReported;

        internal void Initialize(
            NPC npc,
            string bossId)
        {
            _npc =
                npc;

            BossId =
                bossId;

            _character =
                npc != null
                    ? npc.GetChar()
                    : null;

            if (_character == null &&
                npc != null)
            {
                _character =
                    npc.GetComponent<Character>();
            }

            _stats =
                npc != null
                    ? npc.GetComponent<Stats>()
                    : null;

            _deathReported =
                false;
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

        private void Update()
        {
            TryReportDeath(
                "Update");
        }

        private void OnDisable()
        {
            TryReportDeath(
                "OnDisable");
        }

        private void OnDestroy()
        {
            TryReportDeath(
                "OnDestroy");
        }

        private void TryReportDeath(
            string source)
        {
            if (_deathReported ||
                _npc == null)
            {
                return;
            }

            bool dead =
                (_character != null &&
                 !_character.Alive) ||
                (_stats != null &&
                 _stats.CurrentHP <= 0);

            if (!dead)
            {
                return;
            }

            _deathReported =
                true;

            WorldBossRuntime.NotifyBossDeath(
                _npc,
                source);
        }
    }
}
