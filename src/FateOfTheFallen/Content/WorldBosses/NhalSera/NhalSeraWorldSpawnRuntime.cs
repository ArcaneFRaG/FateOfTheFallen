using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Permanent world placement for Nhal'Sera in Soluna's Landing.
    /// Construction and encounter logic remain owned by CustomCreatureRuntime
    /// and NhalSeraEncounterRuntime; this class only owns scene placement and
    /// the normal 44-minute world-boss respawn timer.
    /// </summary>
    internal static class NhalSeraWorldSpawnRuntime
    {
        internal const string SpawnSceneName =
            "Soluna";

        internal static readonly Vector3 SpawnPosition =
            new Vector3(
                566.20f,
                82.40f,
                361.41f);

        private static readonly Quaternion SpawnRotation =
            Quaternion.identity;

        private const float RespawnDelaySeconds =
            44f * 60f;

        private const int InitialSettleFrames =
            180;

        private const int AnimationControllerRetryFrames =
            600;

        // Global Animator/Character discovery is expensive. Preserve the same
        // ~10 second readiness window, but only perform discovery every 15
        // frames instead of every frame.
        private const int PrerequisiteRetryStrideFrames =
            15;

        private static bool _initialized;
        private static bool _spawnRequested;
        private static bool _dead;

        private static DateTime _respawnAtUtc =
            DateTime.MinValue;

        private static int _generation;

        internal static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            SceneManager.sceneLoaded +=
                OnSceneLoaded;

            SceneManager.activeSceneChanged +=
                OnActiveSceneChanged;

            TryStartForScene(
                SceneManager.GetActiveScene(),
                "runtime initialize");
        }

        internal static void Shutdown()
        {
            if (!_initialized)
                return;

            _initialized = false;

            SceneManager.sceneLoaded -=
                OnSceneLoaded;

            SceneManager.activeSceneChanged -=
                OnActiveSceneChanged;

            _generation++;
            _spawnRequested = false;
            _dead = false;
            _respawnAtUtc = DateTime.MinValue;

            if (IsSpawnScene(SceneManager.GetActiveScene()))
                CustomCreatureRuntime.DestroyCurrent();
        }

        internal static void NotifyNhalSeraDeath()
        {
            if (!_initialized || _dead)
                return;

            _dead = true;
            _spawnRequested = false;
            _respawnAtUtc =
                DateTime.UtcNow.AddSeconds(
                    RespawnDelaySeconds);

            Scene scene =
                SceneManager.GetActiveScene();

            if (IsSpawnScene(scene))
            {
                int generation =
                    ++_generation;

                CoroutineHost.Start(
                    WaitForRespawn(
                        scene,
                        generation));
            }

            Plugin.NativeLog.LogInfo(
                "[NhalSera] defeated. Permanent Soluna respawn armed for 44 minutes.");
        }

        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            Scene activeScene =
                SceneManager.GetActiveScene();

            if (IsSpawnScene(activeScene))
            {
                TryStartForScene(
                    activeScene,
                    "sceneLoaded");
            }
        }

        private static void OnActiveSceneChanged(
            Scene previous,
            Scene next)
        {
            _generation++;
            _spawnRequested = false;

            if (!IsSpawnScene(next))
            {
                // Only destroy here when the current object is actually the
                // Soluna boss. Moldermaw's runtime owns Loomingwood.
                NPC current = CustomCreatureRuntime.CurrentNpc;
                if (IsNhalSera(current))
                    CustomCreatureRuntime.DestroyCurrent();

                return;
            }

            TryStartForScene(
                next,
                "Soluna became active");
        }

        private static void TryStartForScene(
            Scene scene,
            string reason)
        {
            if (!_initialized ||
                !WorldBossPersistencePolicy.CanExistInWorld ||
                !IsSpawnScene(scene))
            {
                return;
            }

            if (CustomCreatureRuntime.CurrentObject != null)
            {
                if (IsNhalSera(CustomCreatureRuntime.CurrentNpc))
                    return;

                CustomCreatureRuntime.DestroyCurrent();
            }

            if (_dead &&
                DateTime.UtcNow < _respawnAtUtc)
            {
                int generation =
                    ++_generation;

                CoroutineHost.Start(
                    WaitForRespawn(
                        scene,
                        generation));

                return;
            }

            _dead = false;
            _respawnAtUtc = DateTime.MinValue;

            if (_spawnRequested)
                return;

            _spawnRequested = true;

            int spawnGeneration =
                ++_generation;

            CoroutineHost.Start(
                SpawnWhenWorldReady(
                    scene,
                    spawnGeneration,
                    reason));
        }

        private static IEnumerator SpawnWhenWorldReady(
            Scene scene,
            int generation,
            string reason)
        {
            for (int i = 0; i < InitialSettleFrames; i++)
            {
                if (!CanContinue(scene, generation))
                    yield break;

                yield return null;
            }

            bool controllerReady =
                NativeCreatureAnimationBridge.HasCapturedController;

            bool hostileSeedReady =
                HasNativeHostileFactionSeed();

            for (int i = 0;
                 (!controllerReady || !hostileSeedReady) &&
                 i < AnimationControllerRetryFrames;
                 i++)
            {
                if (!CanContinue(scene, generation))
                    yield break;

                if (i % PrerequisiteRetryStrideFrames == 0)
                {
                    if (GameData.PlayerControl != null &&
                        !controllerReady)
                    {
                        controllerReady =
                            NativeCreatureAnimationBridge.TryCaptureBestNativeController();
                    }

                    if (!hostileSeedReady)
                    {
                        hostileSeedReady = HasNativeHostileFactionSeed();
                    }
                }

                if (!controllerReady || !hostileSeedReady)
                    yield return null;
            }

            if (!CanContinue(scene, generation))
                yield break;

            if (!controllerReady || !hostileSeedReady)
            {
                _spawnRequested = false;

                Plugin.NativeLog.LogWarning(
                    "[NhalSera] Soluna prerequisites were not ready. ControllerReady=[" +
                    controllerReady + "] HostileFactionSeedReady=[" +
                    hostileSeedReady + "]. Automatic spawn was deferred.");

                yield break;
            }

            CustomCreatureRuntime.SpawnNhalSeraAt(
                scene,
                SpawnPosition,
                SpawnRotation);

            for (int i = 0; i < 180; i++)
            {
                if (!CanContinue(scene, generation))
                    yield break;

                if (CustomCreatureRuntime.CurrentObject != null &&
                    IsNhalSera(CustomCreatureRuntime.CurrentNpc))
                {
                    Plugin.NativeLog.LogInfo(
                        "[NhalSera] permanent spawn active in Soluna at [" +
                        SpawnPosition + "] reason=[" +
                        (reason ?? "<unknown>") + "].");

                    yield break;
                }

                yield return null;
            }

            _spawnRequested = false;

            Plugin.NativeLog.LogWarning(
                "[NhalSera] automatic fixed-position Soluna spawn did not settle successfully.");
        }

        private static IEnumerator WaitForRespawn(
            Scene scene,
            int generation)
        {
            while (CanContinue(scene, generation) &&
                   DateTime.UtcNow < _respawnAtUtc)
            {
                yield return null;
            }

            if (!CanContinue(scene, generation))
                yield break;

            _dead = false;
            _respawnAtUtc = DateTime.MinValue;

            NPC current = CustomCreatureRuntime.CurrentNpc;
            if (IsNhalSera(current))
                CustomCreatureRuntime.DestroyCurrent();

            _spawnRequested = false;

            TryStartForScene(
                scene,
                "44-minute respawn timer elapsed");
        }

        private static bool CanContinue(
            Scene scene,
            int generation)
        {
            return
                _initialized &&
                generation == _generation &&
                WorldBossPersistencePolicy.CanExistInWorld &&
                scene.IsValid() &&
                scene.isLoaded &&
                IsSpawnScene(SceneManager.GetActiveScene());
        }

        private static bool HasNativeHostileFactionSeed()
        {
            Character[] characters =
                UnityEngine.Object.FindObjectsOfType<Character>();

            if (characters == null)
                return false;

            for (int i = 0; i < characters.Length; i++)
            {
                Character character =
                    characters[i];

                if (character == null ||
                    !character.isNPC ||
                    character.MyNPC == null ||
                    character.AggressiveTowards == null)
                {
                    continue;
                }

                if (character.AggressiveTowards.Contains(Character.Faction.Player) ||
                    character.AggressiveTowards.Contains(Character.Faction.PC))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSpawnScene(Scene scene)
        {
            return
                scene.IsValid() &&
                scene.isLoaded &&
                string.Equals(
                    scene.name,
                    SpawnSceneName,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNhalSera(NPC npc)
        {
            if (npc == null)
                return false;

            string name = npc.NPCName;

            if (string.IsNullOrEmpty(name) &&
                npc.GetChar() != null &&
                npc.GetChar().MyStats != null)
            {
                name = npc.GetChar().MyStats.MyName;
            }

            return
                !string.IsNullOrEmpty(name) &&
                name.IndexOf(
                    "Nhal'Sera",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
