using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Minimal raid encounter harness for the first FOTF raid PoC.
    ///
    /// Current responsibilities:
    /// - spawn three proven Blighted Thralls at TrashPack_01/Spawn_01..03
    /// - spawn one placeholder raid boss at Boss_01 using the same native NPC bridge
    /// - preserve per-enemy death state across raid scene unload/re-entry
    /// - reset only surviving enemies on a wipe
    /// - independently respawn dead enemies after the configured raid timer
    /// - leave death/respawn UI entirely under Erenshor's control
    ///
    /// A Harmony patch on RespawnWindow.RespawnPlayer handles the actual
    /// player click. If the click occurs inside the FOTF raid, FOTF captures
    /// enemy lifecycle state, resets surviving mobs, arms raid recovery and
    /// then allows Erenshor's original RespawnPlayer() method to run unchanged.
    /// </summary>
    internal static class RaidPocEncounterRuntime
    {
        // Production trash respawn duration. The 30-second PoC timer has been
        // validated in-game, so raid trash now uses the intended 44 minutes.
        internal const float RaidTrashRespawnDelaySeconds =
            44f * 60f;

        // Retained for lifecycle diagnostics/legacy test logging. Successful
        // boss kills now mark the encounter complete and do NOT use this
        // independent respawn timer.
        internal const float RaidBossRespawnDelaySeconds =
            30f;

        internal const string RaidBossDisplayName =
            "Blighted Colossus [PoC]";

        internal const int RaidBossHP =
            20000000;

        internal const int RaidBossBaseAttackDamage =
            5000;

        // Visual/combat intimidation pass for the boss PoC. Scale is applied
        // once when the boss is configured; attack speed is represented by
        // reducing the native main-hand attack delay by this multiplier.
        internal const float RaidBossScaleMultiplier =
            2.5f;

        internal const float RaidBossAttackSpeedMultiplier =
            2.5f;

        private const float FailedRespawnRetryDelaySeconds =
            2f;

        private static readonly string[] TrashSpawnNames =
        {
            "Spawn_01 Local",
            "Spawn_02 Local",
            "Spawn_03 Local"
        };

        private sealed class RaidTrashState
        {
            internal int Index;
            internal string InstanceName;
            internal string MarkerName;
            internal Transform Marker;
            internal NPC CurrentNpc;
            internal bool IsDead;
            internal float RespawnAtRealtime;
            internal float NextSpawnAttemptRealtime;
            internal bool SpawnInProgress;
            internal int DeathRevision;
        }

        private sealed class RaidBossState
        {
            internal string InstanceName =
                "Boss_01";

            internal string MarkerName =
                CustomRaidLoader.BossMarkerName;

            internal Transform Marker;
            internal NPC CurrentNpc;
            internal bool IsDead;
            internal float RespawnAtRealtime;
            internal float NextSpawnAttemptRealtime;
            internal bool SpawnInProgress;
            internal int DeathRevision;
        }

        private static readonly List<RaidTrashState> TrashStates =
            CreateTrashStates();

        private static readonly RaidBossState BossState =
            new RaidBossState();

        private static int _generation;
        private static bool _running;


        internal static bool TryGetBossRoomCenter(
            out Vector3 center)
        {
            center =
                Vector3.zero;

            if (BossState.Marker != null)
            {
                center =
                    BossState.Marker.position;

                return true;
            }

            if (BossState.CurrentNpc != null)
            {
                center =
                    BossState.CurrentNpc.transform.position;

                return true;
            }

            return false;
        }


        internal static bool IsCurrentRaidBossStats(
            Stats stats)
        {
            if (stats == null ||
                BossState.CurrentNpc == null)
            {
                return false;
            }

            Stats bossStats =
                BossState.CurrentNpc.GetComponent<Stats>();

            return bossStats != null &&
                bossStats == stats;
        }


        internal static void Begin(
            Scene raidScene)
        {
            _generation++;

            _running =
                true;

            for (int i = 0;
                 i < TrashStates.Count;
                 i++)
            {
                RaidTrashState state =
                    TrashStates[i];

                state.Marker =
                    null;

                state.CurrentNpc =
                    null;

                state.SpawnInProgress =
                    false;

                state.NextSpawnAttemptRealtime =
                    0f;
            }

            BossState.Marker =
                null;

            BossState.CurrentNpc =
                null;

            BossState.SpawnInProgress =
                false;

            BossState.NextSpawnAttemptRealtime =
                0f;

            int generation =
                _generation;

            CoroutineHost.Start(
                Run(
                    raidScene,
                    generation));
        }


        internal static void Stop()
        {
            RaidBossPhaseRuntime.Stop(
                "raid runtime stop");

            RaidSoakMechanicRuntime.Stop(
                "raid runtime stop");

            RaidAvoidableAoeMechanicRuntime.Stop(
                "raid runtime stop");

            CaptureCurrentDeathState(
                "runtime stop");

            CaptureCurrentBossDeathState(
                "runtime stop");

            _generation++;

            _running =
                false;

            for (int i = 0;
                 i < TrashStates.Count;
                 i++)
            {
                RaidTrashState state =
                    TrashStates[i];

                state.Marker =
                    null;

                state.CurrentNpc =
                    null;

                state.SpawnInProgress =
                    false;
            }

            BossState.Marker =
                null;

            BossState.CurrentNpc =
                null;

            BossState.SpawnInProgress =
                false;
        }


        internal static void ResetLifecycle()
        {
            Stop();

            RaidEncounterCompletionRuntime.Reset(
                "raid lifecycle reset");

            for (int i = 0;
                 i < TrashStates.Count;
                 i++)
            {
                RaidTrashState state =
                    TrashStates[i];

                state.IsDead =
                    false;

                state.RespawnAtRealtime =
                    0f;

                state.NextSpawnAttemptRealtime =
                    0f;

                state.Marker =
                    null;

                state.CurrentNpc =
                    null;

                state.SpawnInProgress =
                    false;

                // Invalidate any dedicated death timer coroutine that may
                // still be alive on the persistent CoroutineHost.
                state.DeathRevision++;
            }

            BossState.IsDead =
                false;

            BossState.RespawnAtRealtime =
                0f;

            BossState.NextSpawnAttemptRealtime =
                0f;

            BossState.Marker =
                null;

            BossState.CurrentNpc =
                null;

            BossState.SpawnInProgress =
                false;

            BossState.DeathRevision++;
        }


        /// <summary>
        /// Called immediately before the native raid-respawn scene transition.
        /// Dead mobs keep their independent respawn clocks. Living mobs are
        /// restored to full HP and their authored spawn markers, then the scene
        /// is allowed to unload normally.
        /// </summary>
        internal static void HandleWipeBeforeSceneExit()
        {
            if (!CustomRaidLoader.IsRaidPocScene(
                    SceneManager.GetActiveScene()))
            {
                return;
            }

            RaidBossPhaseRuntime.Stop(
                "raid wipe");

            RaidSoakMechanicRuntime.Stop(
                "raid wipe");

            RaidAvoidableAoeMechanicRuntime.Stop(
                "raid wipe");

            CaptureCurrentDeathState(
                "wipe capture");

            int livingReset =
                0;

            int deadPreserved =
                0;

            for (int i = 0;
                 i < TrashStates.Count;
                 i++)
            {
                RaidTrashState state =
                    TrashStates[i];

                if (state.IsDead)
                {
                    deadPreserved++;
                    continue;
                }

                NPC npc =
                    state.CurrentNpc;

                if (npc == null ||
                    state.Marker == null)
                {
                    continue;
                }

                if (IsNpcDead(
                        npc))
                {
                    MarkDead(
                        state,
                        "wipe final check");

                    deadPreserved++;
                    continue;
                }

                BlightedThrallRuntime.ResetRaidTrashForWipe(
                    npc,
                    state.Marker);

                livingReset++;

            }

            if (!BossState.IsDead &&
                BossState.CurrentNpc != null &&
                BossState.Marker != null)
            {
                if (IsNpcDead(
                        BossState.CurrentNpc))
                {
                    MarkBossDead(
                        "wipe final check");
                }
                else
                {
                    BlightedThrallRuntime.ResetRaidTrashForWipe(
                        BossState.CurrentNpc,
                        BossState.Marker);
                }
            }

        }


        /// <summary>
        /// Fast death notification used by BlightedThrallBehaviour. The main
        /// raid loop also polls state, so the lifecycle remains correct even if
        /// native corpse handling destroys the behaviour before this fires.
        /// </summary>
        internal static void NotifyRaidTrashDeath(
            NPC npc)
        {
            if (npc == null)
            {
                return;
            }

            RaidTrashState state =
                FindStateForNpc(
                    npc);

            if (state == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] death notification received for an NPC that is not bound to a raid lifecycle slot. NPC=[" +
                    (npc != null
                        ? npc.name
                        : "<null>") +
                    "].");

                return;
            }

            MarkDead(
                state,
                "behaviour death notification");
        }


        internal static void NotifyRaidTrashDeath(
            int stateIndex,
            string source)
        {
            if (stateIndex < 0 ||
                stateIndex >= TrashStates.Count)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] indexed death notification contained an invalid lifecycle slot [" +
                    stateIndex +
                    "] Source=[" +
                    (source ?? "<unknown>") +
                    "].");

                return;
            }

            MarkDead(
                TrashStates[stateIndex],
                "identity death notification: " +
                (source ?? "<unknown>"));
        }


        internal static void NotifyRaidBossDeath(
            string source)
        {
            MarkBossDead(
                "identity death notification: " +
                (source ?? "<unknown>"));
        }


        internal static void NotifyRaidBossDeath(
            NPC npc,
            string source)
        {
            if (npc == null ||
                (BossState.CurrentNpc != null &&
                 BossState.CurrentNpc != npc))
            {
                return;
            }

            MarkBossDead(
                "behaviour death notification: " +
                (source ?? "<unknown>"));
        }


        private static IEnumerator Run(
            Scene raidScene,
            int generation)
        {
            // Allow raid roster placement and immediate MyRaidSlot repair to
            // finish before introducing combatants.
            yield return null;
            yield return null;

            if (!IsCurrentRun(
                    raidScene,
                    generation))
            {
                yield break;
            }

            GameObject pack =
                ResolveTrashMarkers(
                    raidScene);

            if (pack == null)
            {
                yield break;
            }

            ResolveBossMarker(
                raidScene);

            yield return SpawnAvailableTrash(
                raidScene,
                generation);

            if (!IsCurrentRun(
                    raidScene,
                    generation))
            {
                yield break;
            }

            yield return SpawnBossIfAvailable(
                raidScene,
                generation);

            if (!IsCurrentRun(
                    raidScene,
                    generation))
            {
                yield break;
            }


            while (IsCurrentRun(
                       raidScene,
                       generation))
            {
                CaptureCurrentDeathState(
                    "runtime poll");

                CaptureCurrentBossDeathState(
                    "runtime poll");

                RaidTrashState dueState =
                    FindNextDueSpawn();

                if (dueState != null)
                {
                    bool isRespawn =
                        dueState.IsDead;

                    yield return SpawnSingleState(
                        raidScene,
                        generation,
                        dueState,
                        isRespawn);
                }

                if (IsBossSpawnDue())
                {
                    yield return SpawnBossState(
                        raidScene,
                        generation,
                        BossState.IsDead);
                }

                yield return null;
            }
        }


        private static GameObject ResolveTrashMarkers(
            Scene raidScene)
        {
            GameObject pack =
                FindSceneObject(
                    raidScene,
                    CustomRaidLoader.TrashPackMarkerName);

            if (pack == null)
            {
                Plugin.NativeLog.LogError(
                    "[RaidPoC] trash pack marker [" +
                    CustomRaidLoader.TrashPackMarkerName +
                    "] was not found.");

                return null;
            }

            for (int i = 0;
                 i < TrashStates.Count;
                 i++)
            {
                RaidTrashState state =
                    TrashStates[i];

                state.Marker =
                    FindChildRecursive(
                        pack.transform,
                        state.MarkerName);

                if (state.Marker == null)
                {
                    Plugin.NativeLog.LogError(
                        "[RaidPoC] trash spawn marker [" +
                        CustomRaidLoader.TrashPackMarkerName +
                        "/" +
                        state.MarkerName +
                        "] was not found.");
                }
            }

            return pack;
        }


        private static void ResolveBossMarker(
            Scene raidScene)
        {
            GameObject bossMarker =
                FindSceneObject(
                    raidScene,
                    CustomRaidLoader.BossMarkerName);

            BossState.Marker =
                bossMarker != null
                    ? bossMarker.transform
                    : null;

            if (BossState.Marker == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] boss marker [" +
                    CustomRaidLoader.BossMarkerName +
                    "] was not found. Trash lifecycle will continue, but the boss PoC will be skipped.");
            }
        }


        private static IEnumerator SpawnBossIfAvailable(
            Scene raidScene,
            int generation)
        {
            if (BossState.Marker == null)
            {
                yield break;
            }

            if (RaidEncounterCompletionRuntime.IsComplete)
            {

                yield break;
            }

            if (BossState.IsDead &&
                Time.realtimeSinceStartup <
                    BossState.RespawnAtRealtime)
            {

                yield break;
            }

            yield return SpawnBossState(
                raidScene,
                generation,
                BossState.IsDead);
        }


        private static IEnumerator SpawnBossState(
            Scene raidScene,
            int generation,
            bool respawn)
        {
            if (RaidEncounterCompletionRuntime.IsComplete ||
                BossState.Marker == null ||
                BossState.SpawnInProgress ||
                !IsCurrentRun(
                    raidScene,
                    generation))
            {
                yield break;
            }

            BossState.SpawnInProgress =
                true;

            NPC spawnedNpc =
                null;

            yield return BlightedThrallRuntime.CloneRaidBossAtMarker(
                raidScene,
                BossState.Marker,
                BossState.InstanceName,
                npc =>
                {
                    spawnedNpc =
                        npc;
                });

            BossState.SpawnInProgress =
                false;

            if (!IsCurrentRun(
                    raidScene,
                    generation))
            {
                yield break;
            }

            if (spawnedNpc == null)
            {
                BossState.NextSpawnAttemptRealtime =
                    Time.realtimeSinceStartup +
                    FailedRespawnRetryDelaySeconds;

                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] failed to " +
                    (respawn
                        ? "respawn"
                        : "spawn") +
                    " boss [" +
                    BossState.InstanceName +
                    "]; retrying in [" +
                    FailedRespawnRetryDelaySeconds.ToString("0") +
                    "s].");

                yield break;
            }

            BindBossNpc(
                spawnedNpc);

        }


        private static void BindBossNpc(
            NPC npc)
        {
            if (npc == null)
            {
                return;
            }

            if (RaidEncounterCompletionRuntime.IsComplete)
            {
                UnityEngine.Object.Destroy(
                    npc.gameObject);

                return;
            }

            RaidTrashIdentity inheritedTrashIdentity =
                npc.GetComponent<RaidTrashIdentity>();

            if (inheritedTrashIdentity != null)
            {
                UnityEngine.Object.Destroy(
                    inheritedTrashIdentity);
            }

            RaidBossIdentity identity =
                npc.GetComponent<RaidBossIdentity>();

            if (identity == null)
            {
                identity =
                    npc.gameObject.AddComponent<RaidBossIdentity>();
            }

            identity.InstanceName =
                BossState.InstanceName;

            BossState.CurrentNpc =
                npc;

            BossState.IsDead =
                false;

            BossState.RespawnAtRealtime =
                0f;

            BossState.NextSpawnAttemptRealtime =
                0f;

            BossState.SpawnInProgress =
                false;

            BossState.DeathRevision++;

            RaidSoakMechanicRuntime.AttachBoss(
                npc);

            RaidAvoidableAoeMechanicRuntime.AttachBoss(
                npc);

            RaidBossPhaseRuntime.AttachBoss(
                npc);
        }


        private static IEnumerator SpawnAvailableTrash(
            Scene raidScene,
            int generation)
        {
            NPC seededTrashNpc =
                null;

            int spawnedCount =
                0;

            for (int i = 0;
                 i < TrashStates.Count;
                 i++)
            {
                if (!IsCurrentRun(
                        raidScene,
                        generation))
                {
                    yield break;
                }

                RaidTrashState state =
                    TrashStates[i];

                if (state.Marker == null)
                {
                    continue;
                }

                if (state.IsDead &&
                    Time.realtimeSinceStartup <
                        state.RespawnAtRealtime)
                {

                    continue;
                }

                NPC spawnedNpc =
                    null;

                state.SpawnInProgress =
                    true;

                if (seededTrashNpc == null &&
                    !BlightedThrallRuntime.HasRaidTrashPrototype)
                {
                    // The native SpawnPoint bridge is needed only once per
                    // session: the very first raid-trash spawn. That proven
                    // NPC is immediately captured as a persistent prototype.
                    yield return BlightedThrallRuntime.SpawnRaidTrashAtMarker(
                        raidScene,
                        state.Marker,
                        state.InstanceName,
                        npc =>
                        {
                            spawnedNpc =
                                npc;
                        });
                }
                else
                {
                    // Prefer a live pack member when available. If this is a
                    // later scene entry with no live seed, CloneRaidTrashAtMarker
                    // automatically falls back to the persistent prototype.
                    yield return BlightedThrallRuntime.CloneRaidTrashAtMarker(
                        raidScene,
                        seededTrashNpc,
                        state.Marker,
                        state.InstanceName,
                        npc =>
                        {
                            spawnedNpc =
                                npc;
                        });
                }

                state.SpawnInProgress =
                    false;

                if (spawnedNpc == null)
                {
                    state.NextSpawnAttemptRealtime =
                        Time.realtimeSinceStartup +
                        FailedRespawnRetryDelaySeconds;

                    continue;
                }

                bool wasExpiredDeath =
                    state.IsDead;

                BindSpawnedNpc(
                    state,
                    spawnedNpc);

                spawnedCount++;

                if (wasExpiredDeath)
                {
                }

                if (seededTrashNpc == null)
                {
                    seededTrashNpc =
                        spawnedNpc;
                }
            }

        }


        private static IEnumerator SpawnSingleState(
            Scene raidScene,
            int generation,
            RaidTrashState state,
            bool respawn)
        {
            if (state == null ||
                state.Marker == null ||
                state.SpawnInProgress ||
                !IsCurrentRun(
                    raidScene,
                    generation))
            {
                yield break;
            }

            state.SpawnInProgress =
                true;

            NPC sourceNpc =
                FindAliveSeedNpc(
                    state);

            NPC spawnedNpc =
                null;

            // CloneRaidTrashAtMarker prefers a live pack member, but when
            // every mob is dead it falls back to the persistent clean
            // Blighted Thrall prototype captured from the first proven spawn.
            // This is the key independent-respawn path and deliberately avoids
            // trying to make a native SpawnPoint fire repeatedly.
            yield return BlightedThrallRuntime.CloneRaidTrashAtMarker(
                raidScene,
                sourceNpc,
                state.Marker,
                state.InstanceName,
                npc =>
                {
                    spawnedNpc =
                        npc;
                });

            state.SpawnInProgress =
                false;

            if (!IsCurrentRun(
                    raidScene,
                    generation))
            {
                yield break;
            }

            if (spawnedNpc == null)
            {
                state.NextSpawnAttemptRealtime =
                    Time.realtimeSinceStartup +
                    FailedRespawnRetryDelaySeconds;

                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] failed to " +
                    (respawn
                        ? "respawn"
                        : "spawn") +
                    " [" +
                    state.InstanceName +
                    "]; retrying in [" +
                    FailedRespawnRetryDelaySeconds.ToString("0") +
                    "s].");

                yield break;
            }

            BindSpawnedNpc(
                state,
                spawnedNpc);

        }


        private static void BindSpawnedNpc(
            RaidTrashState state,
            NPC npc)
        {
            if (state == null ||
                npc == null)
            {
                return;
            }

            RaidTrashIdentity identity =
                npc.GetComponent<RaidTrashIdentity>();

            if (identity == null)
            {
                identity =
                    npc.gameObject.AddComponent<RaidTrashIdentity>();
            }

            identity.StateIndex =
                state.Index;

            identity.InstanceName =
                state.InstanceName;

            state.CurrentNpc =
                npc;

            state.IsDead =
                false;

            state.RespawnAtRealtime =
                0f;

            state.NextSpawnAttemptRealtime =
                0f;

            state.SpawnInProgress =
                false;

            // A successful bind cancels any older dedicated death countdown
            // for this lifecycle slot.
            state.DeathRevision++;
        }


        private static void CaptureCurrentDeathState(
            string reason)
        {
            for (int i = 0;
                 i < TrashStates.Count;
                 i++)
            {
                RaidTrashState state =
                    TrashStates[i];

                if (state.IsDead ||
                    state.SpawnInProgress)
                {
                    continue;
                }

                NPC npc =
                    state.CurrentNpc;

                if (ReferenceEquals(
                        npc,
                        null))
                {
                    continue;
                }

                try
                {
                    // Unity keeps a managed reference after native destruction.
                    // Inside an active raid run, a previously bound NPC becoming
                    // Unity-null is treated as its native corpse/death transition.
                    if (npc == null ||
                        IsNpcDead(
                            npc))
                    {
                        MarkDead(
                            state,
                            reason);
                    }
                }
                catch (Exception exception)
                {
                    // Native corpse cleanup can race component access. Never
                    // allow that race to terminate the encounter coroutine.
                    Plugin.NativeLog.LogWarning(
                        "[RaidPoC] lifecycle poll hit native corpse cleanup for [" +
                        state.InstanceName +
                        "]; treating the slot as dead. " +
                        exception.GetType().Name +
                        ": " +
                        exception.Message);

                    MarkDead(
                        state,
                        reason +
                        " (corpse cleanup race)");
                }
            }
        }


        private static void CaptureCurrentBossDeathState(
            string reason)
        {
            if (BossState.IsDead ||
                BossState.SpawnInProgress)
            {
                return;
            }

            NPC npc =
                BossState.CurrentNpc;

            if (ReferenceEquals(
                    npc,
                    null))
            {
                return;
            }

            try
            {
                if (npc == null ||
                    IsNpcDead(
                        npc))
                {
                    MarkBossDead(
                        reason);
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] boss lifecycle poll hit native corpse cleanup; treating [" +
                    BossState.InstanceName +
                    "] as dead. " +
                    exception.GetType().Name +
                    ": " +
                    exception.Message);

                MarkBossDead(
                    reason +
                    " (corpse cleanup race)");
            }
        }


        private static void MarkBossDead(
            string reason)
        {
            if (BossState.IsDead ||
                RaidEncounterCompletionRuntime.IsComplete)
            {
                return;
            }

            // A real boss death is now the successful encounter-completion
            // signal. The earlier 30-second boss respawn was useful for
            // lifecycle testing, but a completed encounter must remain clear.
            RaidEncounterCompletionRuntime.Complete(
                reason);

            BossState.IsDead =
                true;

            BossState.RespawnAtRealtime =
                0f;

            BossState.NextSpawnAttemptRealtime =
                0f;

            BossState.SpawnInProgress =
                false;

            // Invalidate any old dedicated respawn coroutine that may still be
            // alive from a previous lifecycle test/revision.
            BossState.DeathRevision++;

            int deathRevision =
                BossState.DeathRevision;

        }


        private static IEnumerator RespawnBossAfterDeathDelay(
            int deathRevision)
        {

            while (!RaidEncounterCompletionRuntime.IsComplete &&
                   BossState.IsDead &&
                   BossState.DeathRevision == deathRevision)
            {
                float remaining =
                    BossState.RespawnAtRealtime -
                    Time.realtimeSinceStartup;

                if (remaining <= 0f)
                {
                    break;
                }

                yield return new WaitForSecondsRealtime(
                    Mathf.Min(
                        remaining,
                        1f));
            }

            if (RaidEncounterCompletionRuntime.IsComplete ||
                !BossState.IsDead ||
                BossState.DeathRevision != deathRevision)
            {
                yield break;
            }

            Scene activeScene =
                SceneManager.GetActiveScene();


            if (!_running ||
                !CustomRaidLoader.IsRaidPocScene(
                    activeScene))
            {

                yield break;
            }

            while (!RaidEncounterCompletionRuntime.IsComplete &&
                   BossState.IsDead &&
                   BossState.DeathRevision == deathRevision)
            {
                activeScene =
                    SceneManager.GetActiveScene();

                if (!_running ||
                    !CustomRaidLoader.IsRaidPocScene(
                        activeScene))
                {
                    yield break;
                }

                BossState.NextSpawnAttemptRealtime =
                    0f;

                int generation =
                    _generation;

                yield return SpawnBossState(
                    activeScene,
                    generation,
                    true);

                if (!BossState.IsDead ||
                    BossState.DeathRevision != deathRevision)
                {
                    yield break;
                }

                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] dedicated boss respawn attempt did not rebind [" +
                    BossState.InstanceName +
                    "]; retrying in [" +
                    FailedRespawnRetryDelaySeconds.ToString("0") +
                    "s].");

                yield return new WaitForSecondsRealtime(
                    FailedRespawnRetryDelaySeconds);
            }
        }


        private static bool IsBossSpawnDue()
        {
            if (RaidEncounterCompletionRuntime.IsComplete ||
                BossState.Marker == null ||
                BossState.SpawnInProgress)
            {
                return false;
            }

            float now =
                Time.realtimeSinceStartup;

            if (BossState.IsDead)
            {
                return
                    now >= BossState.RespawnAtRealtime &&
                    now >= BossState.NextSpawnAttemptRealtime;
            }

            return
                ReferenceEquals(
                    BossState.CurrentNpc,
                    null) &&
                BossState.NextSpawnAttemptRealtime > 0f &&
                now >= BossState.NextSpawnAttemptRealtime;
        }


        private static float GetBossRespawnRemainingSeconds()
        {
            if (!BossState.IsDead)
            {
                return 0f;
            }

            return Mathf.Max(
                0f,
                BossState.RespawnAtRealtime -
                Time.realtimeSinceStartup);
        }


        private static bool IsNpcDead(
            NPC npc)
        {
            if (npc == null)
            {
                return true;
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

            return
                character == null ||
                !character.Alive ||
                stats == null ||
                stats.CurrentHP <= 0;
        }


        private static void MarkDead(
            RaidTrashState state,
            string reason)
        {
            if (state == null ||
                state.IsDead)
            {
                return;
            }

            state.IsDead =
                true;

            state.RespawnAtRealtime =
                Time.realtimeSinceStartup +
                RaidTrashRespawnDelaySeconds;

            state.NextSpawnAttemptRealtime =
                state.RespawnAtRealtime;

            state.SpawnInProgress =
                false;

            state.DeathRevision++;

            int deathRevision =
                state.DeathRevision;


            // Do not rely solely on the encounter harness poll remaining alive
            // for the entire 30-second test window. Every death owns a small
            // dedicated realtime countdown on the persistent CoroutineHost.
            CoroutineHost.Start(
                RespawnAfterDeathDelay(
                    state,
                    deathRevision));
        }


        private static IEnumerator RespawnAfterDeathDelay(
            RaidTrashState state,
            int deathRevision)
        {
            if (state == null)
            {
                yield break;
            }


            while (state.IsDead &&
                   state.DeathRevision == deathRevision)
            {
                float remaining =
                    state.RespawnAtRealtime -
                    Time.realtimeSinceStartup;

                if (remaining <= 0f)
                {
                    break;
                }

                // Realtime waiting means pause menus/timeScale changes cannot
                // stall an enemy's independent raid respawn clock.
                yield return new WaitForSecondsRealtime(
                    Mathf.Min(
                        remaining,
                        1f));
            }

            if (!state.IsDead ||
                state.DeathRevision != deathRevision)
            {
                yield break;
            }

            Scene activeScene =
                SceneManager.GetActiveScene();


            // If the player is currently in the Reliquary (or any other
            // scene), keep the slot dead. Begin()/SpawnAvailableTrash() will
            // notice that the absolute deadline has elapsed on the next raid
            // entry and restore only this enemy.
            if (!_running ||
                !CustomRaidLoader.IsRaidPocScene(
                    activeScene))
            {

                yield break;
            }

            // Retry independently until this particular slot has been rebound
            // or its death revision is invalidated. This makes a transient
            // clone/bootstrap failure visible and self-healing instead of
            // silently losing the respawn forever.
            while (state.IsDead &&
                   state.DeathRevision == deathRevision)
            {
                activeScene =
                    SceneManager.GetActiveScene();

                if (!_running ||
                    !CustomRaidLoader.IsRaidPocScene(
                        activeScene))
                {
                    yield break;
                }

                state.NextSpawnAttemptRealtime =
                    0f;

                int generation =
                    _generation;

                yield return SpawnSingleState(
                    activeScene,
                    generation,
                    state,
                    true);

                if (!state.IsDead ||
                    state.DeathRevision != deathRevision)
                {
                    yield break;
                }

                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] dedicated respawn attempt did not rebind [" +
                    state.InstanceName +
                    "]; retrying in [" +
                    FailedRespawnRetryDelaySeconds.ToString("0") +
                    "s].");

                yield return new WaitForSecondsRealtime(
                    FailedRespawnRetryDelaySeconds);
            }
        }


        private static RaidTrashState FindNextDueSpawn()
        {
            float now =
                Time.realtimeSinceStartup;

            for (int i = 0;
                 i < TrashStates.Count;
                 i++)
            {
                RaidTrashState state =
                    TrashStates[i];

                if (state.SpawnInProgress ||
                    state.Marker == null)
                {
                    continue;
                }

                if (state.IsDead)
                {
                    if (now >=
                            state.RespawnAtRealtime &&
                        now >=
                            state.NextSpawnAttemptRealtime)
                    {
                        return state;
                    }

                    continue;
                }

                // A live lifecycle slot with no bound NPC means its initial
                // creation failed. Retry it without converting it into a
                // death/respawn event.
                if (ReferenceEquals(
                        state.CurrentNpc,
                        null) &&
                    state.NextSpawnAttemptRealtime > 0f &&
                    now >=
                        state.NextSpawnAttemptRealtime)
                {
                    return state;
                }
            }

            return null;
        }


        private static NPC FindAliveSeedNpc(
            RaidTrashState excluded)
        {
            for (int i = 0;
                 i < TrashStates.Count;
                 i++)
            {
                RaidTrashState state =
                    TrashStates[i];

                if (state == excluded ||
                    state.IsDead)
                {
                    continue;
                }

                NPC npc =
                    state.CurrentNpc;

                if (npc != null &&
                    !IsNpcDead(
                        npc))
                {
                    return npc;
                }
            }

            return null;
        }


        private static RaidTrashState FindStateForNpc(
            NPC npc)
        {
            if (npc == null)
            {
                return null;
            }

            for (int i = 0;
                 i < TrashStates.Count;
                 i++)
            {
                RaidTrashState state =
                    TrashStates[i];

                if (ReferenceEquals(
                        state.CurrentNpc,
                        npc) ||
                    state.CurrentNpc == npc)
                {
                    return state;
                }
            }

            return null;
        }


        private static float GetRespawnRemainingSeconds(
            RaidTrashState state)
        {
            if (state == null ||
                !state.IsDead)
            {
                return 0f;
            }

            return Mathf.Max(
                0f,
                state.RespawnAtRealtime -
                Time.realtimeSinceStartup);
        }


        private static int CountAliveStates()
        {
            int count =
                0;

            for (int i = 0;
                 i < TrashStates.Count;
                 i++)
            {
                if (!TrashStates[i].IsDead)
                {
                    count++;
                }
            }

            return count;
        }


        private static int CountDeadStates()
        {
            int count =
                0;

            for (int i = 0;
                 i < TrashStates.Count;
                 i++)
            {
                if (TrashStates[i].IsDead)
                {
                    count++;
                }
            }

            return count;
        }


        private static List<RaidTrashState> CreateTrashStates()
        {
            List<RaidTrashState> states =
                new List<RaidTrashState>();

            for (int i = 0;
                 i < TrashSpawnNames.Length;
                 i++)
            {
                states.Add(
                    new RaidTrashState
                    {
                        Index = i,
                        InstanceName =
                            "Trash_" +
                            (i + 1).ToString("00"),
                        MarkerName =
                            TrashSpawnNames[i]
                    });
            }

            return states;
        }


        private static bool IsCurrentRun(
            Scene raidScene,
            int generation)
        {
            return
                _running &&
                generation == _generation &&
                raidScene.IsValid() &&
                raidScene.isLoaded &&
                CustomRaidLoader.IsRaidPocScene(
                    SceneManager.GetActiveScene());
        }


        private static GameObject FindSceneObject(
            Scene scene,
            string objectName)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                string.IsNullOrEmpty(
                    objectName))
            {
                return null;
            }

            GameObject[] roots =
                scene.GetRootGameObjects();

            if (roots == null)
            {
                return null;
            }

            for (int i = 0;
                 i < roots.Length;
                 i++)
            {
                GameObject result =
                    FindByNameRecursive(
                        roots[i],
                        objectName);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }


        private static GameObject FindByNameRecursive(
            GameObject root,
            string objectName)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(
                    root.name,
                    objectName,
                    StringComparison.Ordinal))
            {
                return root;
            }

            Transform transform =
                root.transform;

            for (int i = 0;
                 i < transform.childCount;
                 i++)
            {
                Transform child =
                    transform.GetChild(
                        i);

                GameObject result =
                    FindByNameRecursive(
                        child.gameObject,
                        objectName);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }


        private static Transform FindChildRecursive(
            Transform root,
            string childName)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(
                    root.name,
                    childName,
                    StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0;
                 i < root.childCount;
                 i++)
            {
                Transform result =
                    FindChildRecursive(
                        root.GetChild(i),
                        childName);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }


    /// <summary>
    /// Lightweight runtime identity attached to each raid-trash NPC. Native
    /// corpse cleanup can invalidate the NPC component before FOTF's next
    /// frame, so callbacks can address the lifecycle slot directly.
    /// </summary>
    internal sealed class RaidTrashIdentity :
        MonoBehaviour
    {
        internal int StateIndex =
            -1;

        internal string InstanceName;
    }


    /// <summary>
    /// Runtime identity for the single PoC boss lifecycle slot. The boss is
    /// still a native hostile NPC; this tag only lets FOTF distinguish its
    /// death/reset/respawn lifecycle from ordinary raid trash.
    /// </summary>
    internal sealed class RaidBossIdentity :
        MonoBehaviour
    {
        internal string InstanceName;
    }


    /// <summary>
    /// Hooks the exact native action fired by the Respawn button.
    ///
    /// Outside the FOTF raid this patch is a no-op. Inside the FOTF raid it
    /// captures raid-NPC lifecycle state, resets surviving mobs, arms FOTF's
    /// post-scene-load raid reconstruction, then returns and lets the original
    /// Erenshor RespawnPlayer() execute normally.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_RaidPocNativeRespawnClick
    {
        private static Type _respawnWindowType;


        [HarmonyPrepare]
        private static bool Prepare()
        {
            _respawnWindowType =
                AccessTools.TypeByName(
                    "RespawnWindow");

            if (_respawnWindowType == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] RespawnWindow type was not found; FOTF raid respawn interception will be skipped.");

                return false;
            }

            MethodBase method =
                AccessTools.Method(
                    _respawnWindowType,
                    "RespawnPlayer");

            if (method == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] RespawnWindow.RespawnPlayer() was not found; FOTF raid respawn interception will be skipped.");

                return false;
            }

            return true;
        }


        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            return
                AccessTools.Method(
                    _respawnWindowType,
                    "RespawnPlayer");
        }


        [HarmonyPrefix]
        private static void Prefix()
        {
            Scene activeScene =
                SceneManager.GetActiveScene();

            if (!CustomRaidLoader.IsRaidPocScene(
                    activeScene))
            {
                return;
            }


            RaidPocEncounterRuntime.HandleWipeBeforeSceneExit();

            CustomRaidLoader.PrepareForNativeWipeRecovery();
        }
    }
}
