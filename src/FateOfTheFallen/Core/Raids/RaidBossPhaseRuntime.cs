using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace FateOfTheFallen
{
    /// <summary>
    /// Minimal reusable phase controller for the raid PoC boss.
    ///
    /// Phase 1 runs the normal boss kit. At 60% HP the controller performs a
    /// short deterministic transition, pauses the boss/native mechanics, then
    /// enters Phase 2 and spawns the single add.
    /// </summary>
    internal static class RaidBossPhaseRuntime
    {
        internal const float PhaseTwoHealthFraction =
            0.60f;

        internal const float PhaseTwoTransitionSeconds =
            3f;

        internal const string PhaseTwoTransitionName =
            "The Blight Deepens";

        private const string CoordinatorOwner =
            "RaidBossPhaseTransition";

        private static NPC _boss;
        private static int _generation;
        private static bool _phaseTwo;
        private static bool _transitionActive;

        private static NPC _pausedBoss;
        private static Character _savedAggroTarget;
        private static NavMeshAgent _pausedAgent;
        private static bool _savedNpcEnabled =
            true;
        private static bool _savedAgentStopped;


        internal static bool IsPhaseTwo
        {
            get
            {
                return _phaseTwo;
            }
        }


        internal static void AttachBoss(
            NPC boss)
        {
            Stop(
                "boss attach");

            _generation++;
            _boss =
                boss;

            _phaseTwo =
                false;

            if (_boss == null)
            {
                return;
            }

            int generation =
                _generation;

            CoroutineHost.Start(
                Run(
                    generation));

        }


        internal static void Stop(
            string reason)
        {
            _generation++;

            RaidPhaseTwoAddRuntime.Stop(
                reason);

            RestoreBossAfterTransition();

            if (_transitionActive)
            {
                RaidBossCastBarRuntime.Hide();
            }

            RaidMechanicCoordinator.Release(
                CoordinatorOwner);

            _transitionActive =
                false;

            _phaseTwo =
                false;

            _boss =
                null;
        }


        private static IEnumerator Run(
            int generation)
        {
            while (IsCurrent(
                       generation))
            {
                if (!_phaseTwo &&
                    IsBossAtPhaseTwoThreshold())
                {
                    yield return EnterPhaseTwo(
                        generation);
                }

                yield return null;
            }
        }


        private static IEnumerator EnterPhaseTwo(
            int generation)
        {
            if (!IsCurrent(
                    generation) ||
                _phaseTwo ||
                _transitionActive)
            {
                yield break;
            }

            _transitionActive =
                true;

            // Phase transitions take priority over ordinary movement mechanics.
            // Cancel the current mechanic cleanly and restart the boss kit after
            // the transition completes.
            RaidSoakMechanicRuntime.Stop(
                "phase 2 transition");

            RaidAvoidableAoeMechanicRuntime.Stop(
                "phase 2 transition");

            while (IsCurrent(
                       generation) &&
                   !RaidMechanicCoordinator.TryAcquire(
                       CoordinatorOwner))
            {
                yield return null;
            }

            if (!IsCurrent(
                    generation))
            {
                _transitionActive =
                    false;

                yield break;
            }

            PauseBossForTransition();

            RaidBossCastBarRuntime.Show(
                PhaseTwoTransitionName,
                PhaseTwoTransitionSeconds);


            float end =
                Time.realtimeSinceStartup +
                PhaseTwoTransitionSeconds;

            while (IsCurrent(
                       generation) &&
                   Time.realtimeSinceStartup < end)
            {
                HoldPausedBoss();
                yield return null;
            }

            if (!IsCurrent(
                    generation))
            {
                RestoreBossAfterTransition();
                RaidBossCastBarRuntime.Hide();
                RaidMechanicCoordinator.Release(
                    CoordinatorOwner);

                _transitionActive =
                    false;

                yield break;
            }

            RaidBossCastBarRuntime.Hide();

            _phaseTwo =
                true;

            NPC boss =
                _boss;

            RaidPhaseTwoAddRuntime.StartPhaseTwo(
                boss);

            RestoreBossAfterTransition();

            RaidMechanicCoordinator.Release(
                CoordinatorOwner);

            _transitionActive =
                false;

            // Reattach the proven mechanics with fresh Phase 2 clocks. The
            // cone's deterministic clock will arm on the next real hit received
            // by the boss after this transition.
            if (boss != null)
            {
                RaidSoakMechanicRuntime.AttachBoss(
                    boss);

                RaidAvoidableAoeMechanicRuntime.AttachBoss(
                    boss);
            }

        }


        private static bool IsBossAtPhaseTwoThreshold()
        {
            NPC boss =
                _boss;

            if (boss == null)
            {
                return false;
            }

            Character character =
                boss.GetChar();

            if (character == null ||
                !character.Alive)
            {
                return false;
            }

            Stats stats =
                boss.GetComponent<Stats>();

            if (stats == null ||
                stats.CurrentMaxHP <= 0 ||
                stats.CurrentHP <= 0)
            {
                return false;
            }

            return
                ((float)stats.CurrentHP /
                 stats.CurrentMaxHP) <=
                PhaseTwoHealthFraction;
        }


        private static void PauseBossForTransition()
        {
            RestoreBossAfterTransition();

            _pausedBoss =
                _boss;

            if (_pausedBoss == null)
            {
                return;
            }

            _savedAggroTarget =
                _pausedBoss.CurrentAggroTarget;

            _savedNpcEnabled =
                _pausedBoss.enabled;

            _pausedAgent =
                _pausedBoss.GetComponent<NavMeshAgent>();

            if (_pausedAgent != null &&
                _pausedAgent.enabled &&
                _pausedAgent.isOnNavMesh)
            {
                try
                {
                    _savedAgentStopped =
                        _pausedAgent.isStopped;

                    _pausedAgent.isStopped =
                        true;

                    _pausedAgent.velocity =
                        Vector3.zero;

                    if (_pausedAgent.hasPath)
                    {
                        _pausedAgent.ResetPath();
                    }
                }
                catch
                {
                    _pausedAgent =
                        null;
                }
            }

            _pausedBoss.CurrentAggroTarget =
                null;

            _pausedBoss.enabled =
                false;
        }


        private static void HoldPausedBoss()
        {
            if (_pausedBoss == null)
            {
                return;
            }

            if (_pausedAgent != null &&
                _pausedAgent.enabled &&
                _pausedAgent.isOnNavMesh)
            {
                try
                {
                    _pausedAgent.isStopped =
                        true;

                    _pausedAgent.velocity =
                        Vector3.zero;

                    if (_pausedAgent.hasPath)
                    {
                        _pausedAgent.ResetPath();
                    }
                }
                catch
                {
                    // Restore path below/native AI will recover after transition.
                }
            }
        }


        private static void RestoreBossAfterTransition()
        {
            NPC boss =
                _pausedBoss;

            if (boss != null)
            {
                try
                {
                    boss.enabled =
                        _savedNpcEnabled;

                    if (_savedAggroTarget != null &&
                        _savedAggroTarget.Alive &&
                        _savedAggroTarget.MyStats != null &&
                        _savedAggroTarget.MyStats.CurrentHP > 0)
                    {
                        boss.CurrentAggroTarget =
                            _savedAggroTarget;
                    }
                }
                catch
                {
                    // Scene teardown/corpse cleanup can invalidate the NPC.
                }
            }

            if (_pausedAgent != null &&
                _pausedAgent.enabled &&
                _pausedAgent.isOnNavMesh)
            {
                try
                {
                    _pausedAgent.isStopped =
                        _savedAgentStopped;
                }
                catch
                {
                    // Native AI repairs movement on its next tick.
                }
            }

            _pausedBoss =
                null;

            _pausedAgent =
                null;

            _savedAggroTarget =
                null;

            _savedNpcEnabled =
                true;

            _savedAgentStopped =
                false;
        }


        private static bool IsCurrent(
            int generation)
        {
            if (generation != _generation ||
                _boss == null)
            {
                return false;
            }

            Character character =
                _boss.GetChar();

            Stats stats =
                _boss.GetComponent<Stats>();

            return
                character != null &&
                character.Alive &&
                stats != null &&
                stats.CurrentHP > 0;
        }
    }
}
