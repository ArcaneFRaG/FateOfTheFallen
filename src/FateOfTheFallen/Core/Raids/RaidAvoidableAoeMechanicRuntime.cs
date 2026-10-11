using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Second encounter-mechanics PoC: a boss-facing avoidable cone.
    ///
    /// Blighted Rupture chooses a random 360-degree bearing, stops the boss's
    /// native NPC combat loop, turns the boss to face that bearing and channels
    /// for three seconds. A visible cone telegraphs the danger area. SimPlayers
    /// inside the cone temporarily surrender native raid AI, path to safe points
    /// outside the cone but still inside the authored 50x50 boss room, hold until
    /// impact, then return to their previous target/native raid logic.
    /// </summary>
    internal static class RaidAvoidableAoeMechanicRuntime
    {
        internal const string MechanicName =
            "Blighted Rupture";

        internal const float ConeRange =
            18f;

        internal const float ConeAngleDegrees =
            90f;

        internal const float TelegraphSeconds =
            3f;

        internal const float InitialDelaySeconds =
            18f;

        internal const float RepeatDelaySeconds =
            24f;

        internal const int ImpactDamagePerVictim =
            9000;

        private const string CoordinatorOwner =
            "RaidAvoidableCone";

        private const float BossTurnSpeedDegreesPerSecond =
            1080f;

        private const float SafeRoomMargin =
            1.5f;

        private const float ArrivalTolerance =
            0.9f;

        private const float HoldDriftTolerance =
            0.25f;

        private const float FallbackMoveSpeed =
            10f;

        private const float SafePositionSampleRadius =
            2.5f;

        private const int ConeMeshSegments =
            28;

        private sealed class AvoidAssignment
        {
            internal SimPlayer Sim;
            internal NPC Npc;
            internal Character SavedAggroTarget;
            internal NavMeshAgent Agent;
            internal Vector3 TargetPosition;
            internal int InstanceId;
            internal bool HasArrived;
        }

        private static readonly List<AvoidAssignment> ActiveAssignments =
            new List<AvoidAssignment>();

        private static readonly HashSet<int> SuppressedSimIds =
            new HashSet<int>();

        private static NPC _boss;
        private static GameObject _zoneObject;
        private static Mesh _zoneMesh;
        private static Material _zoneMaterial;
        private static int _generation;
        private static bool _mechanicActive;

        // Deterministic encounter clock for Blighted Rupture. The clock starts
        // from the first real damage received by this boss, not from native
        // aggro state. Native CurrentAggroTarget proved too transient to use as
        // a reliable encounter-start signal.
        private static bool _encounterClockStarted;
        private static float _encounterStartRealtime;
        private static float _nextScheduledCastRealtime;
        private static int _scheduledCastIndex;

        // Boss-channel state is stored separately from _boss so a wipe/stop can
        // restore native NPC control even while the controller is being torn down.
        private static NPC _channelBoss;
        private static Character _savedBossAggroTarget;
        private static NavMeshAgent _channelBossAgent;
        private static bool _savedBossNpcEnabled;
        private static bool _savedBossAgentStopped;
        private static bool _bossChanneling;


        internal static void AttachBoss(
            NPC boss)
        {
            CancelActiveMechanic(
                "boss attach");

            _generation++;
            _boss =
                boss;

            ResetEncounterClock();

            if (_boss == null)
            {
                return;
            }

            int generation =
                _generation;

            CoroutineHost.Start(
                RunBossMechanicLoop(
                    generation));

        }


        internal static void Stop(
            string reason)
        {
            _generation++;

            CancelActiveMechanic(
                reason);

            _boss =
                null;

            ResetEncounterClock();
        }


        internal static void NotifyBossDamaged(
            Character damagedCharacter,
            int incomingDamage)
        {
            if (_encounterClockStarted ||
                incomingDamage <= 0 ||
                _boss == null ||
                damagedCharacter == null)
            {
                return;
            }

            Character bossCharacter =
                _boss.GetChar();

            if (bossCharacter == null ||
                damagedCharacter != bossCharacter ||
                !IsControllerCurrent(
                    _generation))
            {
                return;
            }

            _encounterClockStarted =
                true;

            _encounterStartRealtime =
                Time.realtimeSinceStartup;

            _scheduledCastIndex =
                1;

            _nextScheduledCastRealtime =
                _encounterStartRealtime +
                InitialDelaySeconds;

        }


        private static void ResetEncounterClock()
        {
            _encounterClockStarted =
                false;

            _encounterStartRealtime =
                0f;

            _nextScheduledCastRealtime =
                0f;

            _scheduledCastIndex =
                0;
        }


        internal static void CancelActiveMechanic(
            string reason)
        {
            if (_mechanicActive ||
                ActiveAssignments.Count > 0 ||
                _zoneObject != null ||
                _bossChanneling)
            {
            }

            ReleaseAssignments();
            DestroyZone();
            RestoreBossAfterChannel();

            RaidBossCastBarRuntime.Hide();

            RaidMechanicCoordinator.Release(
                CoordinatorOwner);

            _mechanicActive =
                false;
        }


        internal static bool ShouldSuppressNativeRaidLogic(
            SimPlayer sim)
        {
            if (sim == null ||
                SuppressedSimIds.Count == 0)
            {
                return false;
            }

            return SuppressedSimIds.Contains(
                sim.GetInstanceID());
        }


        internal static bool IsBossChanneling(
            NPC npc)
        {
            return
                _bossChanneling &&
                npc != null &&
                npc == _channelBoss;
        }


        private static IEnumerator RunBossMechanicLoop(
            int generation)
        {
            // Wait for an explicit encounter-start signal. This is armed by
            // the first real DamageMe/MagicDamageMe/BleedDamageMe call against
            // the boss and therefore cannot drift with CurrentAggroTarget.
            while (IsControllerCurrent(
                       generation) &&
                   !_encounterClockStarted)
            {
                yield return null;
            }

            while (IsControllerCurrent(
                       generation) &&
                   _encounterClockStarted)
            {
                float now =
                    Time.realtimeSinceStartup;

                if (now < _nextScheduledCastRealtime)
                {
                    yield return null;
                    continue;
                }

                float scheduledOffset =
                    _nextScheduledCastRealtime -
                    _encounterStartRealtime;


                yield return ExecuteAvoidableCone(
                    generation);

                if (!IsControllerCurrent(
                        generation) ||
                    !_encounterClockStarted)
                {
                    yield break;
                }

                // Keep future cones anchored to the original pull clock. A
                // cast that was briefly queued behind another movement
                // mechanic does not permanently shift the encounter cadence.
                // If a very long block made multiple old slots stale, skip
                // those stale slots rather than burst-casting cones back to
                // back.
                float afterCast =
                    Time.realtimeSinceStartup;

                do
                {
                    _scheduledCastIndex++;

                    _nextScheduledCastRealtime =
                        _encounterStartRealtime +
                        InitialDelaySeconds +
                        ((_scheduledCastIndex - 1) *
                         RepeatDelaySeconds);
                }
                while (_nextScheduledCastRealtime <=
                       afterCast);
            }
        }


        private static IEnumerator ExecuteAvoidableCone(
            int generation)
        {
            if (!IsControllerCurrent(
                    generation) ||
                _mechanicActive)
            {
                yield break;
            }

            bool queueLogged =
                false;

            while (IsControllerCurrent(
                       generation) &&
                   !RaidMechanicCoordinator.TryAcquire(
                       CoordinatorOwner))
            {
                if (!queueLogged)
                {
                    queueLogged =
                        true;

                }

                yield return null;
            }

            if (!IsControllerCurrent(
                    generation))
            {
                yield break;
            }

            _mechanicActive =
                true;

            float randomYaw =
                UnityEngine.Random.Range(
                    0f,
                    360f);

            Quaternion targetRotation =
                Quaternion.Euler(
                    0f,
                    randomYaw,
                    0f);

            BeginBossChannel();

            RaidBossCastBarRuntime.Show(
                MechanicName,
                TelegraphSeconds);


            float impactAt =
                Time.realtimeSinceStartup +
                TelegraphSeconds;

            while (IsControllerCurrent(
                       generation) &&
                   _mechanicActive &&
                   Time.realtimeSinceStartup < impactAt)
            {
                if (_channelBoss != null)
                {
                    // A separate BlightedThrallBehaviour can still perform its
                    // fallback aggro scan even while NPC.Update is disabled.
                    // Clear any reacquired target every frame until channel end.
                    _channelBoss.CurrentAggroTarget =
                        null;

                    _channelBoss.transform.rotation =
                        Quaternion.RotateTowards(
                            _channelBoss.transform.rotation,
                            targetRotation,
                            BossTurnSpeedDegreesPerSecond * Time.unscaledDeltaTime);
                }

                EnsureZoneCreated();
                UpdateZoneTransform();
                RefreshThreatenedAssignments();
                UpdateAssignmentMovement();

                yield return null;
            }

            if (!IsControllerCurrent(
                    generation) ||
                !_mechanicActive)
            {
                CancelActiveMechanic(
                    "controller invalidated before cone impact");

                yield break;
            }

            // Finish exact facing before evaluating the final cone hit.
            if (_channelBoss != null)
            {
                _channelBoss.transform.rotation =
                    targetRotation;
            }

            UpdateZoneTransform();
            ApplyImpact();

            ReleaseAssignments();
            DestroyZone();
            RestoreBossAfterChannel();
            RaidBossCastBarRuntime.Hide();

            RaidMechanicCoordinator.Release(
                CoordinatorOwner);

            _mechanicActive =
                false;

        }


        private static void BeginBossChannel()
        {
            RestoreBossAfterChannel();

            _channelBoss =
                _boss;

            if (_channelBoss == null)
            {
                return;
            }

            _savedBossAggroTarget =
                _channelBoss.CurrentAggroTarget;

            _savedBossNpcEnabled =
                _channelBoss.enabled;

            _channelBossAgent =
                _channelBoss.GetComponent<NavMeshAgent>();

            if (_channelBossAgent != null &&
                _channelBossAgent.enabled &&
                _channelBossAgent.isOnNavMesh)
            {
                try
                {
                    _savedBossAgentStopped =
                        _channelBossAgent.isStopped;

                    _channelBossAgent.isStopped =
                        true;

                    _channelBossAgent.velocity =
                        Vector3.zero;

                    if (_channelBossAgent.hasPath)
                    {
                        _channelBossAgent.ResetPath();
                    }
                }
                catch
                {
                    _channelBossAgent =
                        null;
                }
            }

            _channelBoss.CurrentAggroTarget =
                null;

            // NPC is a MonoBehaviour. Disabling it suspends Erenshor's native
            // hostile movement/swing loop without disabling Character, Stats,
            // targeting, damage reception or our encounter coroutine.
            _channelBoss.enabled =
                false;

            _bossChanneling =
                true;
        }


        private static void RestoreBossAfterChannel()
        {
            NPC boss =
                _channelBoss;

            if (boss != null)
            {
                try
                {
                    boss.enabled =
                        _savedBossNpcEnabled;

                    if (_savedBossAggroTarget != null &&
                        _savedBossAggroTarget.Alive &&
                        _savedBossAggroTarget.MyStats != null &&
                        _savedBossAggroTarget.MyStats.CurrentHP > 0)
                    {
                        boss.CurrentAggroTarget =
                            _savedBossAggroTarget;
                    }
                }
                catch
                {
                    // Scene teardown/corpse cleanup can invalidate the NPC.
                }
            }

            if (_channelBossAgent != null &&
                _channelBossAgent.enabled &&
                _channelBossAgent.isOnNavMesh)
            {
                try
                {
                    _channelBossAgent.isStopped =
                        _savedBossAgentStopped;
                }
                catch
                {
                    // Native AI repairs its NavMeshAgent on the next tick.
                }
            }

            _channelBoss =
                null;

            _channelBossAgent =
                null;

            _savedBossAggroTarget =
                null;

            _savedBossNpcEnabled =
                true;

            _savedBossAgentStopped =
                false;

            _bossChanneling =
                false;
        }


        private static IEnumerator WaitRealtimeWhileCurrent(
            float seconds,
            int generation)
        {
            float end =
                Time.realtimeSinceStartup +
                seconds;

            while (IsControllerCurrent(
                       generation) &&
                   Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }


        private static bool IsControllerCurrent(
            int generation)
        {
            if (generation != _generation ||
                _boss == null ||
                !CustomRaidLoader.IsRaidPocScene(
                    SceneManager.GetActiveScene()))
            {
                return false;
            }

            Character bossCharacter =
                _boss.GetChar();

            Stats bossStats =
                _boss.GetComponent<Stats>();

            return
                bossCharacter != null &&
                bossCharacter.Alive &&
                bossStats != null &&
                bossStats.CurrentHP > 0;
        }


        private static bool IsBossEngaged()
        {
            if (_boss == null)
            {
                return false;
            }

            if (_boss.CurrentAggroTarget != null)
            {
                return true;
            }

            Stats stats =
                _boss.GetComponent<Stats>();

            return
                stats != null &&
                stats.CurrentMaxHP > 0 &&
                stats.CurrentHP < stats.CurrentMaxHP;
        }


        private static void RefreshThreatenedAssignments()
        {
            if (_boss == null)
            {
                return;
            }

            List<SimPlayer> threatened =
                FindLivingRaidSimsInsideCone();

            for (int i = 0;
                 i < threatened.Count;
                 i++)
            {
                SimPlayer sim =
                    threatened[i];

                if (HasAssignment(
                        sim))
                {
                    continue;
                }

                AddAssignment(
                    sim);
            }
        }


        private static bool HasAssignment(
            SimPlayer sim)
        {
            if (sim == null)
            {
                return false;
            }

            for (int i = 0;
                 i < ActiveAssignments.Count;
                 i++)
            {
                if (ActiveAssignments[i].Sim == sim)
                {
                    return true;
                }
            }

            return false;
        }


        private static void AddAssignment(
            SimPlayer sim)
        {
            if (!IsValidLivingRaidSim(
                    sim))
            {
                return;
            }

            NPC npc =
                sim.GetThisNPC();

            Vector3 safePosition;

            if (!TryResolveSafePosition(
                    sim,
                    out safePosition))
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] [" +
                    MechanicName +
                    "] could not resolve a boss-room-contained escape point for Sim [" +
                    GetSimName(sim) +
                    "]. Native raid AI remains suppressed so the failure is visible in testing.");

                safePosition =
                    RaidBossRoomRuntime.ClampPoint(
                        sim.transform.position,
                        SafeRoomMargin);
            }

            Character savedTarget =
                npc != null
                    ? npc.CurrentAggroTarget
                    : null;

            if (npc != null)
            {
                npc.CurrentAggroTarget =
                    null;
            }

            NavMeshAgent agent =
                sim.GetComponent<NavMeshAgent>();

            AvoidAssignment assignment =
                new AvoidAssignment
                {
                    Sim = sim,
                    Npc = npc,
                    SavedAggroTarget = savedTarget,
                    Agent = agent,
                    TargetPosition = safePosition,
                    InstanceId = sim.GetInstanceID(),
                    HasArrived = false
                };

            ActiveAssignments.Add(
                assignment);

            SuppressedSimIds.Add(
                assignment.InstanceId);

        }


        private static List<SimPlayer> FindLivingRaidSimsInsideCone()
        {
            List<SimPlayer> result =
                new List<SimPlayer>();

            RaidManager manager =
                GameData.RaidManager;

            if (manager == null ||
                manager.Raiders == null)
            {
                return result;
            }

            foreach (RaidMemberSlot slot in manager.Raiders)
            {
                SimPlayer sim =
                    slot != null
                        ? slot.AssignedAvatar
                        : null;

                if (!IsValidLivingRaidSim(
                        sim))
                {
                    continue;
                }

                if (IsPointInsideCone(
                        sim.transform.position))
                {
                    AddUniqueSim(
                        result,
                        sim);
                }
            }

            return result;
        }


        private static List<Character> FindLivingRaidCharactersInsideCone()
        {
            List<Character> result =
                new List<Character>();

            PlayerControl player =
                GameData.PlayerControl;

            if (player != null &&
                player.Myself != null)
            {
                AddCharacterIfInsideCone(
                    result,
                    player.Myself);
            }

            RaidManager manager =
                GameData.RaidManager;

            if (manager != null &&
                manager.Raiders != null)
            {
                foreach (RaidMemberSlot slot in manager.Raiders)
                {
                    SimPlayer sim =
                        slot != null
                            ? slot.AssignedAvatar
                            : null;

                    if (!IsValidLivingRaidSim(
                            sim))
                    {
                        continue;
                    }

                    NPC npc =
                        sim.GetThisNPC();

                    Character character =
                        npc != null
                            ? npc.GetChar()
                            : null;

                    AddCharacterIfInsideCone(
                        result,
                        character);
                }
            }

            return result;
        }


        private static void AddCharacterIfInsideCone(
            List<Character> result,
            Character character)
        {
            if (character == null ||
                !character.Alive ||
                character.MyStats == null ||
                character.MyStats.CurrentHP <= 0 ||
                !IsPointInsideCone(
                    character.transform.position))
            {
                return;
            }

            AddUniqueCharacter(
                result,
                character);
        }


        private static bool IsPointInsideCone(
            Vector3 worldPosition)
        {
            if (_boss == null)
            {
                return false;
            }

            Vector3 origin =
                _boss.transform.position;

            Vector3 forward =
                _boss.transform.forward;

            forward.y =
                0f;

            if (forward.sqrMagnitude <=
                0.0001f)
            {
                forward =
                    Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            Vector3 delta =
                worldPosition - origin;

            delta.y =
                0f;

            float distance =
                delta.magnitude;

            if (distance > ConeRange)
            {
                return false;
            }

            if (distance <=
                0.15f)
            {
                return true;
            }

            delta /=
                distance;

            float halfAngle =
                ConeAngleDegrees * 0.5f;

            return
                Vector3.Angle(
                    forward,
                    delta) <= halfAngle;
        }


        private static bool TryResolveSafePosition(
            SimPlayer sim,
            out Vector3 safePosition)
        {
            safePosition =
                Vector3.zero;

            if (sim == null ||
                _boss == null)
            {
                return false;
            }

            Vector3 origin =
                sim.transform.position;

            float[] radii =
            {
                4f,
                6f,
                8f,
                10f,
                12f
            };

            // First preference is a point near the Sim. Sample all around it so
            // the escape can choose whichever cone edge is closest and remains
            // inside the boss room/NavMesh.
            for (int r = 0;
                 r < radii.Length;
                 r++)
            {
                float radius =
                    radii[r];

                for (int step = 0;
                     step < 16;
                     step++)
                {
                    float angle =
                        (360f / 16f) * step;

                    Vector3 direction =
                        Quaternion.Euler(
                            0f,
                            angle,
                            0f) *
                        Vector3.forward;

                    Vector3 desired =
                        origin +
                        (direction * radius);

                    if (!RaidBossRoomRuntime.ContainsPoint(
                            desired,
                            SafeRoomMargin))
                    {
                        continue;
                    }

                    NavMeshHit hit;

                    if (!NavMesh.SamplePosition(
                            desired,
                            out hit,
                            SafePositionSampleRadius,
                            NavMesh.AllAreas))
                    {
                        continue;
                    }

                    if (!RaidBossRoomRuntime.ContainsPoint(
                            hit.position,
                            SafeRoomMargin) ||
                        IsPointInsideCone(
                            hit.position) ||
                        !HasCompleteNavMeshPath(
                            origin,
                            hit.position))
                    {
                        continue;
                    }

                    safePosition =
                        hit.position;

                    return true;
                }
            }

            return false;
        }


        private static bool HasCompleteNavMeshPath(
            Vector3 from,
            Vector3 to)
        {
            NavMeshHit startHit;
            NavMeshHit endHit;

            if (!NavMesh.SamplePosition(
                    from,
                    out startHit,
                    3f,
                    NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(
                    to,
                    out endHit,
                    3f,
                    NavMesh.AllAreas))
            {
                return false;
            }

            NavMeshPath path =
                new NavMeshPath();

            if (!NavMesh.CalculatePath(
                    startHit.position,
                    endHit.position,
                    NavMesh.AllAreas,
                    path))
            {
                return false;
            }

            return path.status ==
                   NavMeshPathStatus.PathComplete;
        }


        private static bool IsValidLivingRaidSim(
            SimPlayer sim)
        {
            if (sim == null ||
                !sim.InRaid ||
                sim.MyStats == null ||
                sim.MyStats.CurrentHP <= 0)
            {
                return false;
            }

            NPC npc =
                sim.GetThisNPC();

            Character character =
                npc != null
                    ? npc.GetChar()
                    : null;

            return
                npc != null &&
                character != null &&
                character.Alive;
        }


        private static void UpdateAssignmentMovement()
        {
            for (int i = 0;
                 i < ActiveAssignments.Count;
                 i++)
            {
                AvoidAssignment assignment =
                    ActiveAssignments[i];

                SimPlayer sim =
                    assignment.Sim;

                if (!IsValidLivingRaidSim(
                        sim))
                {
                    continue;
                }

                NPC npc =
                    assignment.Npc;

                if (npc != null)
                {
                    npc.CurrentAggroTarget =
                        null;
                }

                float distance =
                    Vector3.Distance(
                        sim.transform.position,
                        assignment.TargetPosition);

                if (assignment.HasArrived)
                {
                    HoldAssignmentAtTarget(
                        assignment,
                        distance);

                    continue;
                }

                if (distance <=
                    ArrivalTolerance)
                {
                    assignment.HasArrived =
                        true;

                    HoldAssignmentAtTarget(
                        assignment,
                        distance);


                    continue;
                }

                NavMeshAgent agent =
                    assignment.Agent;

                if (agent != null &&
                    agent.enabled &&
                    agent.isOnNavMesh)
                {
                    try
                    {
                        agent.isStopped =
                            false;

                        agent.SetDestination(
                            assignment.TargetPosition);

                        continue;
                    }
                    catch
                    {
                        // Fall through to transform movement.
                    }
                }

                sim.transform.position =
                    Vector3.MoveTowards(
                        sim.transform.position,
                        assignment.TargetPosition,
                        FallbackMoveSpeed * Time.deltaTime);
            }
        }


        private static void HoldAssignmentAtTarget(
            AvoidAssignment assignment,
            float distance)
        {
            if (assignment == null ||
                assignment.Sim == null)
            {
                return;
            }

            NavMeshAgent agent =
                assignment.Agent;

            if (agent != null &&
                agent.enabled &&
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

                    if (distance > HoldDriftTolerance)
                    {
                        agent.Warp(
                            assignment.TargetPosition);
                    }

                    return;
                }
                catch
                {
                    // Transform correction below handles transient agent issues.
                }
            }

            if (distance > HoldDriftTolerance)
            {
                assignment.Sim.transform.position =
                    assignment.TargetPosition;
            }
        }


        private static void ApplyImpact()
        {
            List<Character> victims =
                FindLivingRaidCharactersInsideCone();

            Character bossCharacter =
                _boss != null
                    ? _boss.GetChar()
                    : null;

            int hits =
                0;

            for (int i = 0;
                 i < victims.Count;
                 i++)
            {
                Character target =
                    victims[i];

                if (target == null ||
                    !target.Alive ||
                    target.MyStats == null ||
                    target.MyStats.CurrentHP <= 0)
                {
                    continue;
                }

                try
                {
                    RaidMechanicDamageAttribution.SetNamedSource(
                        bossCharacter,
                        MechanicName);

                    target.DamageMe(
                        ImpactDamagePerVictim,
                        false,
                        GameData.DamageType.Poison,
                        bossCharacter,
                        false,
                        false,
                        0);

                    hits++;
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "[RaidPoC] [" +
                        MechanicName +
                        "] cone damage failed for [" +
                        target.name +
                        "]: " +
                        exception);
                }
            }

        }


        private static void ReleaseAssignments()
        {
            for (int i = 0;
                 i < ActiveAssignments.Count;
                 i++)
            {
                AvoidAssignment assignment =
                    ActiveAssignments[i];

                SuppressedSimIds.Remove(
                    assignment.InstanceId);

                SimPlayer sim =
                    assignment.Sim;

                if (sim == null)
                {
                    continue;
                }

                NavMeshAgent agent =
                    assignment.Agent;

                if (agent != null &&
                    agent.enabled &&
                    agent.isOnNavMesh)
                {
                    try
                    {
                        agent.isStopped =
                            false;

                        if (agent.hasPath)
                        {
                            agent.ResetPath();
                        }
                    }
                    catch
                    {
                        // Native raid AI repairs movement on its next tick.
                    }
                }

                NPC npc =
                    assignment.Npc;

                if (npc != null &&
                    assignment.SavedAggroTarget != null &&
                    assignment.SavedAggroTarget.Alive)
                {
                    npc.CurrentAggroTarget =
                        assignment.SavedAggroTarget;
                }
            }

            ActiveAssignments.Clear();
            SuppressedSimIds.Clear();
        }


        private static void EnsureZoneCreated()
        {
            if (_zoneObject != null)
            {
                return;
            }

            _zoneObject =
                new GameObject(
                    "FOTF_" +
                    MechanicName.Replace(" ", "_") +
                    "_Cone");

            MeshFilter meshFilter =
                _zoneObject.AddComponent<MeshFilter>();

            MeshRenderer renderer =
                _zoneObject.AddComponent<MeshRenderer>();

            _zoneMesh =
                BuildConeMesh();

            meshFilter.sharedMesh =
                _zoneMesh;

            Shader shader =
                Shader.Find(
                    "Sprites/Default");

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Unlit/Color");
            }

            if (shader != null)
            {
                _zoneMaterial =
                    new Material(
                        shader);

                _zoneMaterial.color =
                    new Color(
                        0.74f,
                        0.08f,
                        0.16f,
                        0.48f);

                _zoneMaterial.renderQueue =
                    3001;

                renderer.material =
                    _zoneMaterial;
            }
        }


        private static Mesh BuildConeMesh()
        {
            Mesh mesh =
                new Mesh();

            mesh.name =
                "FOTF_BlightedRuptureConeMesh";

            Vector3[] vertices =
                new Vector3[ConeMeshSegments + 2];

            int[] triangles =
                new int[ConeMeshSegments * 3];

            vertices[0] =
                Vector3.zero;

            float halfAngle =
                ConeAngleDegrees * 0.5f;

            for (int i = 0;
                 i <= ConeMeshSegments;
                 i++)
            {
                float t =
                    (float)i / ConeMeshSegments;

                float angleDegrees =
                    Mathf.Lerp(
                        -halfAngle,
                        halfAngle,
                        t);

                float radians =
                    angleDegrees * Mathf.Deg2Rad;

                vertices[i + 1] =
                    new Vector3(
                        Mathf.Sin(radians) * ConeRange,
                        0f,
                        Mathf.Cos(radians) * ConeRange);

                if (i < ConeMeshSegments)
                {
                    int triangle =
                        i * 3;

                    triangles[triangle] =
                        0;

                    triangles[triangle + 1] =
                        i + 1;

                    triangles[triangle + 2] =
                        i + 2;
                }
            }

            mesh.vertices =
                vertices;

            mesh.triangles =
                triangles;

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }


        private static void UpdateZoneTransform()
        {
            if (_zoneObject == null ||
                _boss == null)
            {
                return;
            }

            _zoneObject.transform.position =
                _boss.transform.position +
                (Vector3.up * 0.06f);

            _zoneObject.transform.rotation =
                Quaternion.Euler(
                    0f,
                    _boss.transform.eulerAngles.y,
                    0f);
        }


        private static void DestroyZone()
        {
            if (_zoneObject != null)
            {
                UnityEngine.Object.Destroy(
                    _zoneObject);

                _zoneObject =
                    null;
            }

            if (_zoneMesh != null)
            {
                UnityEngine.Object.Destroy(
                    _zoneMesh);

                _zoneMesh =
                    null;
            }

            if (_zoneMaterial != null)
            {
                UnityEngine.Object.Destroy(
                    _zoneMaterial);

                _zoneMaterial =
                    null;
            }
        }


        private static void AddUniqueSim(
            List<SimPlayer> sims,
            SimPlayer sim)
        {
            if (sim == null)
            {
                return;
            }

            for (int i = 0;
                 i < sims.Count;
                 i++)
            {
                if (sims[i] == sim)
                {
                    return;
                }
            }

            sims.Add(
                sim);
        }


        private static void AddUniqueCharacter(
            List<Character> characters,
            Character character)
        {
            if (character == null)
            {
                return;
            }

            for (int i = 0;
                 i < characters.Count;
                 i++)
            {
                if (characters[i] == character)
                {
                    return;
                }
            }

            characters.Add(
                character);
        }


        private static string GetSimName(
            SimPlayer sim)
        {
            if (sim == null)
            {
                return "<null>";
            }

            NPC npc =
                sim.GetThisNPC();

            if (npc != null &&
                !string.IsNullOrEmpty(
                    npc.NPCName))
            {
                return npc.NPCName;
            }

            return sim.name;
        }
    }


    // =====================================================================
    // Deterministic boss-combat clock hooks
    // =====================================================================

    [HarmonyPatch(
        typeof(Character),
        "DamageMe",
        new Type[]
        {
            typeof(int),
            typeof(bool),
            typeof(GameData.DamageType),
            typeof(Character),
            typeof(bool),
            typeof(bool),
            typeof(int)
        })]
    internal static class Patch_RaidConeCombatClockDamage
    {
        private static void Prefix(
            Character __instance,
            int _incdmg)
        {
            RaidAvoidableAoeMechanicRuntime.NotifyBossDamaged(
                __instance,
                _incdmg);
        }
    }


    [HarmonyPatch(
        typeof(Character),
        "MagicDamageMe",
        new Type[]
        {
            typeof(int),
            typeof(bool),
            typeof(GameData.DamageType),
            typeof(Character),
            typeof(float),
            typeof(int)
        })]
    internal static class Patch_RaidConeCombatClockMagicDamage
    {
        private static void Prefix(
            Character __instance,
            int _dmg)
        {
            RaidAvoidableAoeMechanicRuntime.NotifyBossDamaged(
                __instance,
                _dmg);
        }
    }


    [HarmonyPatch(
        typeof(Character),
        "BleedDamageMe",
        new Type[]
        {
            typeof(int),
            typeof(bool),
            typeof(Character)
        })]
    internal static class Patch_RaidConeCombatClockBleedDamage
    {
        private static void Prefix(
            Character __instance,
            int _incdmg)
        {
            RaidAvoidableAoeMechanicRuntime.NotifyBossDamaged(
                __instance,
                _incdmg);
        }
    }

}
