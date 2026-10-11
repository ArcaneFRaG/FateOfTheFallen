using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Phase 2 interrupt proof-of-concept.
    ///
    /// The Blightbound Herald periodically begins Blighted Cataclysm. One
    /// living SimPlayer is assigned to interrupt it. Native raid AI is
    /// temporarily suppressed only for that Sim, it closes to interrupt range,
    /// the cast is cancelled, then normal combat control is restored.
    ///
    /// If the assigned Sim cannot complete the interrupt before the cast ends,
    /// Blighted Cataclysm resolves as heavy named raid-wide damage.
    /// </summary>
    internal static class RaidPhaseTwoInterruptRuntime
    {
        internal const string MechanicName =
            "Blighted Cataclysm";

        internal const float CastDurationSeconds =
            3f;

        internal const float FirstCastDelaySeconds =
            8f;

        internal const float RepeatDelaySeconds =
            14f;

        internal const int FailedInterruptDamagePerVictim =
            12000;

        private const float InterruptRange =
            6f;

        private const float InterruptReactionDelaySeconds =
            1.25f;

        private const float FallbackMoveSpeed =
            9f;

        private const string CoordinatorOwner =
            "Phase2_Herald_Interrupt";

        private static NPC _add;
        private static int _generation;
        private static bool _running;
        private static bool _casting;
        private static SimPlayer _assignedSim;
        private static NPC _assignedSimNpc;
        private static Character _savedSimTarget;
        private static NavMeshAgent _assignedAgent;
        private static int _suppressedSimId;

        private static bool _savedAddEnabled;
        private static Character _savedAddTarget;
        private static NavMeshAgent _addAgent;
        private static bool _savedAddAgentStopped;

        internal static bool IsCasting
        {
            get { return _casting; }
        }

        internal static void Begin(
            NPC add)
        {
            Stop(
                "interrupt runtime restart");

            if (add == null)
            {
                return;
            }

            _generation++;
            _running = true;
            _add = add;

            int generation =
                _generation;

            CoroutineHost.Start(
                RunSchedule(
                    generation));

        }

        internal static void Stop(
            string reason)
        {
            _generation++;
            _running = false;

            CancelCurrentCast(
                reason ?? "stop");

            _add = null;
        }

        internal static bool ShouldSuppressNativeRaidLogic(
            SimPlayer sim)
        {
            return sim != null &&
                _suppressedSimId != 0 &&
                sim.GetInstanceID() == _suppressedSimId;
        }

        private static IEnumerator RunSchedule(
            int generation)
        {
            float encounterStart =
                Time.realtimeSinceStartup;

            int castNumber =
                1;

            while (IsCurrent(generation))
            {
                float dueAt =
                    encounterStart +
                    FirstCastDelaySeconds +
                    ((castNumber - 1) * RepeatDelaySeconds);

                while (IsCurrent(generation) &&
                       Time.realtimeSinceStartup < dueAt)
                {
                    yield return null;
                }

                if (!IsCurrent(generation))
                {
                    yield break;
                }

                while (IsCurrent(generation) &&
                       !RaidMechanicCoordinator.TryAcquire(CoordinatorOwner))
                {
                    yield return null;
                }

                if (!IsCurrent(generation))
                {
                    RaidMechanicCoordinator.Release(
                        CoordinatorOwner);
                    yield break;
                }

                yield return ExecuteCast(
                    generation,
                    castNumber);

                castNumber++;
            }
        }

        private static IEnumerator ExecuteCast(
            int generation,
            int castNumber)
        {
            if (!IsCurrent(generation))
            {
                yield break;
            }

            _casting = true;

            BeginAddChannel();
            AssignInterrupter();

            RaidBossCastBarRuntime.Show(
                MechanicName,
                CastDurationSeconds);


            float castStarted =
                Time.realtimeSinceStartup;

            float castEnds =
                castStarted +
                CastDurationSeconds;

            bool interrupted =
                false;

            while (IsCurrent(generation) &&
                   _casting &&
                   Time.realtimeSinceStartup < castEnds)
            {
                HoldAddChannel();

                bool inRange =
                    UpdateInterrupterMovement();

                if (inRange &&
                    Time.realtimeSinceStartup >=
                        castStarted + InterruptReactionDelaySeconds)
                {
                    interrupted =
                        true;


                    break;
                }

                yield return null;
            }

            if (!IsCurrent(generation))
            {
                CancelCurrentCast(
                    "controller invalidated");
                yield break;
            }

            if (!interrupted)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] [" +
                    MechanicName +
                    "] INTERRUPT FAILED. Cast completed; applying raid-wide damage.");

                ApplyFailedInterruptDamage();
            }

            EndCurrentCast();
        }

        private static void BeginAddChannel()
        {
            NPC add =
                _add;

            if (add == null)
            {
                return;
            }

            _savedAddTarget =
                add.CurrentAggroTarget;

            _savedAddEnabled =
                add.enabled;

            _addAgent =
                add.GetComponent<NavMeshAgent>();

            if (_addAgent != null &&
                _addAgent.enabled &&
                _addAgent.isOnNavMesh)
            {
                try
                {
                    _savedAddAgentStopped =
                        _addAgent.isStopped;

                    _addAgent.isStopped =
                        true;

                    _addAgent.velocity =
                        Vector3.zero;

                    if (_addAgent.hasPath)
                    {
                        _addAgent.ResetPath();
                    }
                }
                catch
                {
                    _addAgent =
                        null;
                }
            }

            add.CurrentAggroTarget =
                null;

            add.enabled =
                false;
        }

        private static void HoldAddChannel()
        {
            NPC add =
                _add;

            if (add == null)
            {
                return;
            }

            add.CurrentAggroTarget =
                null;

            if (_addAgent != null &&
                _addAgent.enabled &&
                _addAgent.isOnNavMesh)
            {
                try
                {
                    _addAgent.isStopped =
                        true;

                    _addAgent.velocity =
                        Vector3.zero;

                    if (_addAgent.hasPath)
                    {
                        _addAgent.ResetPath();
                    }
                }
                catch
                {
                    // Native movement state will be restored after the cast.
                }
            }
        }

        private static void AssignInterrupter()
        {
            ReleaseInterrupter();

            NPC add =
                _add;

            if (add == null)
            {
                return;
            }

            RaidManager manager =
                GameData.RaidManager;

            if (manager == null ||
                manager.Raiders == null)
            {
                return;
            }

            SimPlayer best =
                null;

            float bestDistance =
                float.MaxValue;

            foreach (RaidMemberSlot slot in manager.Raiders)
            {
                SimPlayer sim =
                    slot != null
                        ? slot.AssignedAvatar
                        : null;

                if (!IsEligibleSim(sim))
                {
                    continue;
                }

                float distance =
                    Vector3.Distance(
                        sim.transform.position,
                        add.transform.position);

                if (distance < bestDistance)
                {
                    best =
                        sim;

                    bestDistance =
                        distance;
                }
            }

            if (best == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] [" +
                    MechanicName +
                    "] no eligible living SimPlayer was available to interrupt the cast.");

                return;
            }

            _assignedSim =
                best;

            _suppressedSimId =
                best.GetInstanceID();

            _assignedSimNpc =
                best.GetThisNPC();

            if (_assignedSimNpc != null)
            {
                _savedSimTarget =
                    _assignedSimNpc.CurrentAggroTarget;

                _assignedSimNpc.CurrentAggroTarget =
                    null;
            }

            _assignedAgent =
                best.GetComponent<NavMeshAgent>();

        }

        private static bool UpdateInterrupterMovement()
        {
            SimPlayer sim =
                _assignedSim;

            NPC add =
                _add;

            if (!IsEligibleSim(sim) ||
                add == null)
            {
                return false;
            }

            if (_assignedSimNpc != null)
            {
                _assignedSimNpc.CurrentAggroTarget =
                    null;
            }

            float distance =
                Vector3.Distance(
                    sim.transform.position,
                    add.transform.position);

            if (distance <= InterruptRange)
            {
                StopAssignedAgent();
                return true;
            }

            Vector3 away =
                sim.transform.position -
                add.transform.position;

            away.y =
                0f;

            if (away.sqrMagnitude < 0.001f)
            {
                away =
                    Vector3.forward;
            }

            Vector3 desired =
                add.transform.position +
                (away.normalized * (InterruptRange * 0.65f));

            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                    desired,
                    out hit,
                    4f,
                    NavMesh.AllAreas))
            {
                desired =
                    hit.position;
            }

            if (_assignedAgent != null &&
                _assignedAgent.enabled &&
                _assignedAgent.isOnNavMesh)
            {
                try
                {
                    _assignedAgent.isStopped =
                        false;

                    _assignedAgent.SetDestination(
                        desired);

                    return false;
                }
                catch
                {
                    // Fall back to direct movement below.
                }
            }

            sim.transform.position =
                Vector3.MoveTowards(
                    sim.transform.position,
                    desired,
                    FallbackMoveSpeed * Time.deltaTime);

            return false;
        }

        private static void StopAssignedAgent()
        {
            if (_assignedAgent == null ||
                !_assignedAgent.enabled ||
                !_assignedAgent.isOnNavMesh)
            {
                return;
            }

            try
            {
                _assignedAgent.isStopped =
                    true;

                _assignedAgent.velocity =
                    Vector3.zero;

                if (_assignedAgent.hasPath)
                {
                    _assignedAgent.ResetPath();
                }
            }
            catch
            {
                // Native raid AI will repair movement state on release.
            }
        }

        private static void ApplyFailedInterruptDamage()
        {
            NPC add =
                _add;

            Character source =
                add != null
                    ? add.GetChar()
                    : null;

            List<Character> victims =
                FindLivingRaidCharacters();

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
                        source,
                        MechanicName);

                    target.DamageMe(
                        FailedInterruptDamagePerVictim,
                        false,
                        GameData.DamageType.Poison,
                        source,
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
                        "] failed-cast damage failed for [" +
                        target.name +
                        "]: " +
                        exception);
                }
            }

        }

        private static void EndCurrentCast()
        {
            RaidBossCastBarRuntime.Hide();

            RestoreAddAfterChannel();
            ReleaseInterrupter();

            _casting =
                false;

            RaidMechanicCoordinator.Release(
                CoordinatorOwner);
        }

        private static void CancelCurrentCast(
            string reason)
        {
            if (_casting)
            {
            }

            RaidBossCastBarRuntime.Hide();

            RestoreAddAfterChannel();
            ReleaseInterrupter();

            _casting =
                false;

            RaidMechanicCoordinator.Release(
                CoordinatorOwner);
        }

        private static void RestoreAddAfterChannel()
        {
            NPC add =
                _add;

            if (add != null)
            {
                try
                {
                    add.enabled =
                        _savedAddEnabled;

                    if (_savedAddTarget != null &&
                        _savedAddTarget.Alive &&
                        _savedAddTarget.MyStats != null &&
                        _savedAddTarget.MyStats.CurrentHP > 0)
                    {
                        add.CurrentAggroTarget =
                            _savedAddTarget;
                    }
                }
                catch
                {
                    // Scene teardown/corpse cleanup can invalidate the NPC.
                }
            }

            if (_addAgent != null &&
                _addAgent.enabled &&
                _addAgent.isOnNavMesh)
            {
                try
                {
                    _addAgent.isStopped =
                        _savedAddAgentStopped;
                }
                catch
                {
                    // Native AI will recover next frame.
                }
            }

            _savedAddTarget =
                null;

            _addAgent =
                null;

            _savedAddEnabled =
                true;

            _savedAddAgentStopped =
                false;
        }

        private static void ReleaseInterrupter()
        {
            SimPlayer sim =
                _assignedSim;

            NPC npc =
                _assignedSimNpc;

            if (_assignedAgent != null &&
                _assignedAgent.enabled &&
                _assignedAgent.isOnNavMesh)
            {
                try
                {
                    _assignedAgent.isStopped =
                        false;
                }
                catch
                {
                    // Native raid AI restores movement on its next tick.
                }
            }

            if (npc != null)
            {
                Character target =
                    _savedSimTarget;

                if (target != null &&
                    target.Alive &&
                    target.MyStats != null &&
                    target.MyStats.CurrentHP > 0)
                {
                    // Restore exactly what the Sim was attacking before the
                    // temporary interrupt assignment.
                    npc.CurrentAggroTarget =
                        target;
                }
                else
                {
                    // Do not force the Herald (or boss) as a fallback target.
                    // Native raid/group assignment logic decides what this Sim
                    // should attack on its next raid-AI tick.
                    npc.CurrentAggroTarget =
                        null;
                }
            }

            _assignedSim =
                null;

            _assignedSimNpc =
                null;

            _savedSimTarget =
                null;

            _assignedAgent =
                null;

            _suppressedSimId =
                0;
        }

        private static bool IsEligibleSim(
            SimPlayer sim)
        {
            if (sim == null ||
                RaidSoakMechanicRuntime.ShouldSuppressNativeRaidLogic(sim) ||
                RaidAvoidableAoeMechanicRuntime.ShouldSuppressNativeRaidLogic(sim))
            {
                return false;
            }

            NPC npc =
                sim.GetThisNPC();

            Character character =
                npc != null
                    ? npc.GetChar()
                    : null;

            return npc != null &&
                character != null &&
                character.Alive &&
                character.MyStats != null &&
                character.MyStats.CurrentHP > 0;
        }

        private static List<Character> FindLivingRaidCharacters()
        {
            List<Character> result =
                new List<Character>();

            PlayerControl player =
                GameData.PlayerControl;

            if (player != null &&
                player.Myself != null)
            {
                AddUniqueLivingCharacter(
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

                    if (sim == null)
                    {
                        continue;
                    }

                    NPC npc =
                        sim.GetThisNPC();

                    Character character =
                        npc != null
                            ? npc.GetChar()
                            : null;

                    AddUniqueLivingCharacter(
                        result,
                        character);
                }
            }

            return result;
        }

        private static void AddUniqueLivingCharacter(
            List<Character> result,
            Character character)
        {
            if (character == null ||
                !character.Alive ||
                character.MyStats == null ||
                character.MyStats.CurrentHP <= 0)
            {
                return;
            }

            for (int i = 0;
                 i < result.Count;
                 i++)
            {
                if (result[i] == character)
                {
                    return;
                }
            }

            result.Add(
                character);
        }

        private static bool IsCurrent(
            int generation)
        {
            if (generation != _generation ||
                !_running ||
                _add == null ||
                !CustomRaidLoader.IsRaidPocScene(
                    SceneManager.GetActiveScene()))
            {
                return false;
            }

            Character character =
                _add.GetChar();

            Stats stats =
                _add.GetComponent<Stats>();

            return character != null &&
                character.Alive &&
                stats != null &&
                stats.CurrentHP > 0;
        }

        private static string GetAssignedSimName()
        {
            if (_assignedSimNpc != null &&
                !string.IsNullOrEmpty(_assignedSimNpc.NPCName))
            {
                return _assignedSimNpc.NPCName;
            }

            return _assignedSim != null
                ? _assignedSim.name
                : "<none>";
        }
    }
}
