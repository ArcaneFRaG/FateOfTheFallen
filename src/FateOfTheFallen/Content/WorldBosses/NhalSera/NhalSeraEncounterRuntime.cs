using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace FateOfTheFallen
{
    /// <summary>
    /// Nhal'Sera's dedicated six-mechanic open-world encounter.
    ///
    /// Phase 1 (100-75%):
    ///   Event Horizon  - escalating pull + lethal inner collapse.
    ///   Reality Rend   - intersecting line fractures.
    ///
    /// Phase 2 (75-60%):
    ///   Fractured Reality - paired players must separate.
    ///
    /// Phase 3 (60-30%):
    ///   Shattered Reflection - identify/burn the true reflection before Unmaking.
    ///   Into the Void       - displacement + persistent pulsing rift.
    ///
    /// Phase 4 (30-0%):
    ///   The Shrouded Veil - one shrinking safe zone that becomes a hard DPS check.
    ///
    /// Earlier mechanics accumulate through phase 3. Once The Shrouded Veil
    /// begins at 30%, the encounter becomes a clean shrinking-zone DPS check.
    /// This runtime is fully isolated from Moldermaw's WorldBossEncounterRuntime.
    /// </summary>
    internal static class NhalSeraEncounterRuntime
    {
        internal const float PhaseTwoThreshold = 0.75f;
        internal const float PhaseThreeThreshold = 0.60f;
        internal const float PhaseFourThreshold = 0.30f;

        private const string EventHorizonName = "Event Horizon";
        private const float EventHorizonInitialDelay = 10f;
        private const float EventHorizonRepeat = 28f;
        private const float EventHorizonCastTime = 5f;
        private const float EventHorizonCollapseRadius = 8f;
        private const float EventHorizonEscapeRadius = 22f;
        private const int EventHorizonDamage = 9800;
        private const float EventHorizonPullStart = 0.65f;
        private const float EventHorizonPullEnd = 4.50f;

        private const string RealityRendName = "Reality Rend";
        private const float RealityRendInitialDelay = 6f;
        private const float RealityRendRepeat = 20f;
        private const float RealityRendCastTime = 3f;
        private const float RealityRendHalfLength = 20f;
        private const float RealityRendHalfWidth = 1.50f;
        private const int RealityRendDamage = 9000;

        private const string FracturedRealityName = "Fractured Reality";
        private const float FracturedRealityInitialDelay = 5f;
        private const float FracturedRealityRepeat = 25f;
        private const float FracturedRealityMarkCastTime = 1.25f;
        private const float FracturedRealityDuration = 9f;
        private const float FracturedRealityBreakDistance = 18f;
        private const float FracturedRealitySimEscapePadding = 4f;
        private const int FracturedRealityFailureDamage = 7200;
        private const int FracturedRealityMaxTargets = 6;

        private const string IntoTheVoidName = "Into the Void";
        private const float IntoTheVoidInitialDelay = 8f;
        private const float IntoTheVoidRepeat = 22f;
        private const float IntoTheVoidCastTime = 1.50f;
        private const float IntoTheVoidMinRadius = 12f;
        private const float IntoTheVoidMaxRadius = 18f;
        private const float IntoTheVoidReTargetLockout = 30f;
        private const float VoidRiftDuration = 18f;
        private const float VoidRiftRadius = 4.25f;
        private const float VoidRiftPulseSeconds = 3f;
        private const int VoidRiftPulseDamage = 1800;

        private const string ShatteredReflectionName = "Shattered Reflection";
        private const string UnmakingName = "Unmaking";
        private const float ShatteredReflectionInitialDelay = 3f;
        private const float ShatteredReflectionRepeat = 38f;
        private const float ShatteredReflectionChannel = 20f;
        private const float ReflectionRadius = 12f;
        private const float ReflectionDamageFraction = 0.03f;
        private const int ReflectionFailureDamage = 12000;

        private const string ShroudedVeilName = "The Shrouded Veil";
        private const string LostInVeilName = "Lost in the Veil";
        private const float ShroudedVeilCastTime = 2f;
        private const float VeilInitialSafeRadius = 16f;
        private const float VeilShrinkDuration = 60f;
        private const float VeilTickSeconds = 1f;
        private const float VeilVisualRefreshSeconds = 0.35f;
        private const float VeilSimCheckSeconds = 0.35f;
        private const float VeilSimMoveTimeout = 2.50f;
        private const float VeilSimInnerPadding = 2.0f;
        private const int VeilBaseDamage = 900;
        private const int VeilStackDamage = 250;
        private const int VeilMaxStacks = 10;

        private static NPC _boss;
        private static Character _bossCharacter;
        private static Vector3 _homePosition;
        private static Quaternion _homeRotation;

        private static int _generation;
        private static bool _engaged;
        private static float _combatStartedRealtime;
        private static float _noTargetSinceRealtime = -1f;

        private static bool _phaseTwo;
        private static bool _phaseThree;
        private static bool _phaseFour;
        private static bool _veilPending;

        private static float _nextEventHorizonRealtime;
        private static float _nextRealityRendRealtime;
        private static float _nextFracturedRealityRealtime;
        private static float _nextIntoTheVoidRealtime;
        private static float _nextShatteredReflectionRealtime;

        private static int _eventHorizonCastIndex;
        private static int _realityRendCastIndex;
        private static int _fracturedRealityCastIndex;
        private static int _intoTheVoidCastIndex;
        private static int _shatteredReflectionCastIndex;

        private static bool _castActive;
        private static float _nextBossCombatRepairRealtime = -1f;
        private static NPC _pausedBoss;
        private static Character _savedAggroTarget;
        private static NavMeshAgent _pausedAgent;
        private static bool _savedNpcEnabled;
        private static bool _savedAgentStopped;

        private static readonly List<GameObject> CastTelegraphs =
            new List<GameObject>();

        private sealed class RiftState
        {
            internal Vector3 Position;
            internal float ExpiresRealtime;
            internal float NextPulseRealtime;
            internal GameObject Visual;
        }

        private sealed class TetherState
        {
            internal Character A;
            internal Character B;
            internal bool Broken;
            internal GameObject Beam;
        }

        private sealed class RendLine
        {
            internal Vector3 Center;
            internal Vector3 Forward;
            internal Vector3 Right;
            internal float HalfLength;
            internal float HalfWidth;
            internal GameObject Visual;
        }


        private static readonly List<RiftState> ActiveRifts =
            new List<RiftState>();

        private static readonly Dictionary<int, float> DisplacedUntil =
            new Dictionary<int, float>();

        private static bool _reflectionSucceeded;
        private static int _reflectionStartHP;
        private static int _reflectionDamageRequired;
        private static int _reflectionDamageProgress;
        private static readonly List<GameObject> ReflectionDecoys =
            new List<GameObject>();
        private static GameObject _trueReflectionMarker;

        private static bool _veilActive;
        private static bool _veilExpired;
        private static float _veilStartedRealtime;
        private static float _veilCurrentRadius;
        private static Vector3 _veilCenter;
        private static GameObject _veilVisual;
        private static float _nextVeilTickRealtime;
        private static float _nextVeilVisualRefreshRealtime;
        private static float _nextVeilSimAssignmentCheckRealtime;
        private static float _veilSimMoveStartedRealtime;
        private static readonly Dictionary<int, int> VeilStacks =
            new Dictionary<int, int>();

        private static int _lastDamageFrame = -1;
        private static Character _lastDamageTarget;
        private static int _lastDamageAmount;

        internal static bool IsEngaged
        {
            get { return _engaged; }
        }

        internal static bool IsCurrentBossCharacter(Character character)
        {
            return character != null &&
                character == _bossCharacter &&
                _boss != null;
        }

        internal static bool IsCurrentBossStats(Stats stats)
        {
            return stats != null &&
                _bossCharacter != null &&
                _bossCharacter.MyStats == stats &&
                _boss != null;
        }

        internal static void AttachBoss(NPC boss, Vector3 homePosition)
        {
            Stop("boss attach");

            _generation++;
            _boss = boss;
            _bossCharacter = boss != null ? boss.GetChar() : null;

            if (_bossCharacter == null && boss != null)
            {
                _bossCharacter = boss.GetComponent<Character>();
            }

            _homePosition = homePosition;
            _homeRotation = boss != null ? boss.transform.rotation : Quaternion.identity;

            ResetEncounterState();

            if (_boss == null || _bossCharacter == null)
            {
                return;
            }

            int generation = _generation;
            CoroutineHost.Start(Run(generation));

            Plugin.NativeLog.LogInfo(
                "[NhalSera] encounter attached. Mechanics=[Event Horizon, Reality Rend, Fractured Reality, Into the Void, Shattered Reflection, The Shrouded Veil].");
        }

        internal static void Stop(string reason)
        {
            _generation++;

            if (_castActive)
            {
                RaidBossCastBarRuntime.Hide();
            }

            RestoreBossAfterCast();
            _castActive = false;

            WorldBossSimAvoidanceRuntime.ForceReleaseAll(
                string.IsNullOrEmpty(reason) ? "NhalSera cleanup" : reason);

            DestroyCastTelegraphs();
            ClearRifts();
            ClearReflectionVisuals();
            ClearVeil();

            _boss = null;
            _bossCharacter = null;
            _engaged = false;

            if (!string.IsNullOrEmpty(reason))
            {
                Plugin.NativeLog.LogInfo(
                    "[NhalSera] encounter stopped. Reason=[" + reason + "].");
            }
        }

        private static void ResetEncounterState()
        {
            _engaged = false;
            _combatStartedRealtime = -1f;
            _noTargetSinceRealtime = -1f;

            _phaseTwo = false;
            _phaseThree = false;
            _phaseFour = false;
            _veilPending = false;
            _veilActive = false;
            _veilExpired = false;
            _veilCurrentRadius = 0f;

            _nextEventHorizonRealtime = float.PositiveInfinity;
            _nextRealityRendRealtime = float.PositiveInfinity;
            _nextFracturedRealityRealtime = float.PositiveInfinity;
            _nextIntoTheVoidRealtime = float.PositiveInfinity;
            _nextShatteredReflectionRealtime = float.PositiveInfinity;

            _eventHorizonCastIndex = 0;
            _realityRendCastIndex = 0;
            _fracturedRealityCastIndex = 0;
            _intoTheVoidCastIndex = 0;
            _shatteredReflectionCastIndex = 0;

            _reflectionSucceeded = false;
            _reflectionStartHP = 0;
            _reflectionDamageRequired = 0;
            _reflectionDamageProgress = 0;

            _lastDamageFrame = -1;
            _lastDamageTarget = null;
            _lastDamageAmount = 0;
            _nextBossCombatRepairRealtime = -1f;
        }

        internal static void NotifyBossDamaged(Character target, int incomingDamage)
        {
            if (target == null ||
                target != _bossCharacter ||
                incomingDamage <= 0 ||
                !IsBossAlive())
            {
                return;
            }

            // MagicDamageMe / BleedDamageMe can route into DamageMe in the same
            // frame. Do not count the exact same packet twice for Reflection.
            if (_lastDamageFrame == Time.frameCount &&
                _lastDamageTarget == target &&
                _lastDamageAmount == incomingDamage)
            {
                return;
            }

            _lastDamageFrame = Time.frameCount;
            _lastDamageTarget = target;
            _lastDamageAmount = incomingDamage;

            if (!_engaged)
            {
                StartCombatClock();
            }

        }

        private static void StartCombatClock()
        {
            _engaged = true;
            _combatStartedRealtime = Time.realtimeSinceStartup;

            _nextEventHorizonRealtime =
                _combatStartedRealtime + EventHorizonInitialDelay;

            _nextRealityRendRealtime =
                _combatStartedRealtime + RealityRendInitialDelay;

            _noTargetSinceRealtime = -1f;

            Plugin.NativeLog.LogInfo(
                "[NhalSera] combat clock started. Phase2=75% Phase3=60% Phase4=30%.");
        }

        private static IEnumerator Run(int generation)
        {
            while (IsCurrent(generation))
            {
                if (!IsBossAlive())
                {
                    WorldBossSimAvoidanceRuntime.ForceReleaseAll(
                        "NhalSera death");
                    DestroyCastTelegraphs();
                    ClearRifts();
                    ClearReflectionVisuals();
                    ClearVeil();
                    yield break;
                }

                UpdateRifts();
                UpdateVeil();
                WorldBossSimAvoidanceRuntime.UpdateMovement();

                if (_engaged)
                {
                    MaintainBossCombatState(false);

                    if (ShouldLeashReset())
                    {
                        ResetBossToHome("leash/no living raid targets");
                        yield break;
                    }

                    UpdatePhaseState();

                    // Once the shrinking Veil is active, stop scheduling the
                    // earlier mechanics. The final 30% is intentionally a
                    // clean burn phase / DPS check around one collapsing safe
                    // zone rather than another movement-overlap phase.
                    if (_veilActive)
                    {
                        yield return null;
                        continue;
                    }

                    if (!_castActive)
                    {
                        float now = Time.realtimeSinceStartup;

                        if (_veilPending)
                        {
                            yield return CastShroudedVeil(generation);
                        }
                        else if (_phaseThree &&
                                 now >= _nextShatteredReflectionRealtime)
                        {
                            int castNumber = ++_shatteredReflectionCastIndex;
                            yield return CastShatteredReflection(generation, castNumber);
                            AdvanceSchedule(
                                ref _nextShatteredReflectionRealtime,
                                ShatteredReflectionRepeat);
                        }
                        else if (now >= _nextEventHorizonRealtime)
                        {
                            int castNumber = ++_eventHorizonCastIndex;
                            yield return CastEventHorizon(generation, castNumber);
                            AdvanceSchedule(
                                ref _nextEventHorizonRealtime,
                                EventHorizonRepeat);
                        }
                        else if (now >= _nextRealityRendRealtime)
                        {
                            int castNumber = ++_realityRendCastIndex;
                            yield return CastRealityRend(generation, castNumber);
                            AdvanceSchedule(
                                ref _nextRealityRendRealtime,
                                RealityRendRepeat);
                        }
                        else if (_phaseTwo &&
                                 now >= _nextFracturedRealityRealtime)
                        {
                            int castNumber = ++_fracturedRealityCastIndex;
                            yield return CastFracturedReality(generation, castNumber);
                            AdvanceSchedule(
                                ref _nextFracturedRealityRealtime,
                                FracturedRealityRepeat);
                        }
                        else if (_phaseThree &&
                                 now >= _nextIntoTheVoidRealtime)
                        {
                            int castNumber = ++_intoTheVoidCastIndex;
                            yield return CastIntoTheVoid(generation, castNumber);
                            AdvanceSchedule(
                                ref _nextIntoTheVoidRealtime,
                                IntoTheVoidRepeat);
                        }
                    }
                }

                yield return null;
            }
        }

        private static void UpdatePhaseState()
        {
            float fraction = GetHealthFraction();
            float now = Time.realtimeSinceStartup;

            if (!_phaseTwo && fraction <= PhaseTwoThreshold)
            {
                _phaseTwo = true;
                _nextFracturedRealityRealtime = now + FracturedRealityInitialDelay;

                Plugin.NativeLog.LogInfo(
                    "[NhalSera] PHASE 2: Fractured Reality enabled.");
            }

            if (!_phaseThree && fraction <= PhaseThreeThreshold)
            {
                _phaseThree = true;
                _nextShatteredReflectionRealtime = now + ShatteredReflectionInitialDelay;
                _nextIntoTheVoidRealtime = now + IntoTheVoidInitialDelay;

                Plugin.NativeLog.LogInfo(
                    "[NhalSera] PHASE 3: Shattered Reflection + Into the Void enabled.");
            }

            if (!_phaseFour && fraction <= PhaseFourThreshold)
            {
                _phaseFour = true;
                _veilPending = true;

                Plugin.NativeLog.LogInfo(
                    "[NhalSera] PHASE 4: The Shrouded Veil pending.");
            }
        }

        private static IEnumerator CastEventHorizon(int generation, int castNumber)
        {
            if (!CanBeginCast(generation))
            {
                yield break;
            }

            _castActive = true;

            RaidBossCastBarRuntime.Show(
                EventHorizonName,
                EventHorizonCastTime);

            Vector3 center = _boss.transform.position;
            CastTelegraphs.Add(
                CreateGroundDisc(
                    center,
                    EventHorizonCollapseRadius,
                    new Color(0.28f, 0.02f, 0.55f, 0.45f),
                    "FOTF_NhalSera_EventHorizon"));

            Dictionary<SimPlayer, Vector3> simDestinations =
                new Dictionary<SimPlayer, Vector3>();

            // Build this directly from the authoritative party/raid Sim roster.
            // Do not go through Character -> Sim lookup here; that was the
            // reason only part of the raid received Event Horizon movement.
            List<SimPlayer> horizonSims =
                WorldBossSimAvoidanceRuntime.GetLivingRaidSimsSnapshot();

            for (int i = 0; i < horizonSims.Count; i++)
            {
                SimPlayer sim = horizonSims[i];
                if (sim == null)
                {
                    continue;
                }

                Vector3 away = sim.transform.position - center;
                away.y = 0f;

                if (away.sqrMagnitude < 0.01f)
                {
                    float angle = i * 57f + castNumber * 23f;
                    away = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                }

                away.Normalize();
                simDestinations[sim] = center + away * EventHorizonEscapeRadius;
            }

            List<Character> living = GetLivingRaidCharacters();

            WorldBossSimAvoidanceRuntime.BeginDirectedMovement(
                simDestinations,
                "Event Horizon");

            float started = Time.realtimeSinceStartup;
            float ends = started + EventHorizonCastTime;

            while (IsCurrent(generation) &&
                   IsBossAlive() &&
                   Time.realtimeSinceStartup < ends)
            {
                WorldBossSimAvoidanceRuntime.UpdateMovement();

                float t = Mathf.Clamp01(
                    (Time.realtimeSinceStartup - started) /
                    EventHorizonCastTime);

                float pullSpeed = Mathf.Lerp(
                    EventHorizonPullStart,
                    EventHorizonPullEnd,
                    t);

                ApplyEventHorizonPull(center, pullSpeed);
                UpdateRifts();
                UpdateVeil();

                yield return null;
            }

            if (IsCurrent(generation) && IsBossAlive())
            {
                int hits = 0;
                living = GetLivingRaidCharacters();

                for (int i = 0; i < living.Count; i++)
                {
                    if (PlanarDistance(living[i].transform.position, center) <=
                        EventHorizonCollapseRadius)
                    {
                        if (ApplyNamedDamage(
                                living[i],
                                EventHorizonName,
                                EventHorizonDamage))
                        {
                            hits++;
                        }
                    }
                }

                Plugin.NativeLog.LogInfo(
                    "[NhalSera] Event Horizon collapse. Hit=[" + hits + "].");
            }

            WorldBossSimAvoidanceRuntime.ReleaseAssignments(
                "Event Horizon complete");

            FinishCast();
        }

        private static void ApplyEventHorizonPull(Vector3 center, float speed)
        {
            List<Character> living = GetLivingRaidCharacters();
            float stepDistance = speed * Mathf.Max(0f, Time.deltaTime);

            for (int i = 0; i < living.Count; i++)
            {
                Character target = living[i];
                Vector3 delta = center - target.transform.position;
                delta.y = 0f;

                if (delta.sqrMagnitude < 0.01f)
                {
                    continue;
                }

                Vector3 step = delta.normalized * stepDistance;
                SimPlayer sim = FindSimForCharacter(target);

                if (sim != null)
                {
                    NavMeshAgent agent = sim.GetComponent<NavMeshAgent>();
                    if (agent != null && agent.enabled && agent.isOnNavMesh)
                    {
                        try
                        {
                            agent.Move(step);
                            continue;
                        }
                        catch
                        {
                        }
                    }
                }

                target.transform.position += step;
            }
        }

        private static IEnumerator CastRealityRend(int generation, int castNumber)
        {
            if (!CanBeginCast(generation))
            {
                yield break;
            }

            _castActive = true;
            RaidBossCastBarRuntime.Show(RealityRendName, RealityRendCastTime);

            Vector3 center = _boss.transform.position;
            List<RendLine> lines = BuildRealityRendLines(center, castNumber);

            Dictionary<SimPlayer, Vector3> simDestinations =
                BuildRealityRendSimDestinations(lines);

            WorldBossSimAvoidanceRuntime.BeginDirectedMovement(
                simDestinations,
                "Reality Rend");

            float ends = Time.realtimeSinceStartup + RealityRendCastTime;

            while (IsCurrent(generation) &&
                   IsBossAlive() &&
                   Time.realtimeSinceStartup < ends)
            {
                WorldBossSimAvoidanceRuntime.UpdateMovement();
                UpdateRifts();
                UpdateVeil();
                yield return null;
            }

            if (IsCurrent(generation) && IsBossAlive())
            {
                List<Character> living = GetLivingRaidCharacters();
                int hits = 0;

                for (int i = 0; i < living.Count; i++)
                {
                    if (IsInsideAnyRendLine(living[i].transform.position, lines))
                    {
                        if (ApplyNamedDamage(
                                living[i],
                                RealityRendName,
                                RealityRendDamage))
                        {
                            hits++;
                        }
                    }
                }

                Plugin.NativeLog.LogInfo(
                    "[NhalSera] Reality Rend impact. Lines=[" +
                    lines.Count + "] Hit=[" + hits + "].");
            }

            WorldBossSimAvoidanceRuntime.ReleaseAssignments(
                "Reality Rend complete");

            FinishCast();
        }

        private static List<RendLine> BuildRealityRendLines(
            Vector3 center,
            int castNumber)
        {
            int lineCount = _phaseFour ? 5 : (_phaseThree ? 4 : 3);
            List<RendLine> lines = new List<RendLine>();
            float baseAngle = (castNumber * 31f) % 60f;

            for (int i = 0; i < lineCount; i++)
            {
                float angle = baseAngle + i * (180f / lineCount);
                Vector3 forward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                forward.y = 0f;
                forward.Normalize();

                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                float offset = ((i % 3) - 1) * 2.8f;
                Vector3 lineCenter = center + right * offset;

                RendLine line = new RendLine();
                line.Center = lineCenter;
                line.Forward = forward;
                line.Right = right;
                line.HalfLength = RealityRendHalfLength;
                line.HalfWidth = RealityRendHalfWidth;
                line.Visual = CreateLineTelegraph(
                    lineCenter,
                    forward,
                    RealityRendHalfLength,
                    RealityRendHalfWidth,
                    new Color(0.62f, 0.03f, 0.95f, 0.43f));

                CastTelegraphs.Add(line.Visual);
                lines.Add(line);
            }

            return lines;
        }

        private static Dictionary<SimPlayer, Vector3>
            BuildRealityRendSimDestinations(List<RendLine> lines)
        {
            Dictionary<SimPlayer, Vector3> result =
                new Dictionary<SimPlayer, Vector3>();

            // Use the authoritative Sim roster directly. The previous
            // Character -> Sim resolution silently skipped raid members.
            List<SimPlayer> sims =
                WorldBossSimAvoidanceRuntime.GetLivingRaidSimsSnapshot();

            for (int i = 0; i < sims.Count; i++)
            {
                SimPlayer sim = sims[i];

                if (sim == null ||
                    !IsInsideAnyRendLine(sim.transform.position, lines))
                {
                    continue;
                }

                Vector3 safe;
                if (!TryFindSafePointFromLines(
                        sim.transform.position,
                        lines,
                        out safe))
                {
                    // Never leave a threatened Sim unassigned. Move it
                    // perpendicular to the closest fracture and let the
                    // movement runtime perform final NavMesh projection.
                    safe = BuildEmergencyRendEscape(
                        sim.transform.position,
                        lines);
                }

                result[sim] = safe;
            }

            return result;
        }

        private static Vector3 BuildEmergencyRendEscape(
            Vector3 current,
            List<RendLine> lines)
        {
            RendLine closest = null;
            float closestAcross = float.PositiveInfinity;
            float side = 1f;

            for (int i = 0; i < lines.Count; i++)
            {
                RendLine line = lines[i];
                Vector3 delta = current - line.Center;
                delta.y = 0f;

                float signedAcross =
                    Vector3.Dot(delta, line.Right);

                float across =
                    Mathf.Abs(signedAcross);

                if (across < closestAcross)
                {
                    closestAcross = across;
                    closest = line;
                    side = signedAcross >= 0f ? 1f : -1f;
                }
            }

            if (closest == null)
            {
                return current + Vector3.right * 6f;
            }

            return current +
                closest.Right *
                side *
                (closest.HalfWidth + 5f);
        }

        private static bool TryFindSafePointFromLines(
            Vector3 current,
            List<RendLine> lines,
            out Vector3 safe)
        {
            float[] radii = { 3f, 5f, 7f, 9f, 12f };

            for (int r = 0; r < radii.Length; r++)
            {
                for (int a = 0; a < 16; a++)
                {
                    float angle = a * 22.5f + r * 17f;
                    Vector3 candidate =
                        current +
                        Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radii[r];

                    NavMeshHit hit;
                    if (!NavMesh.SamplePosition(
                            candidate,
                            out hit,
                            3f,
                            NavMesh.AllAreas))
                    {
                        continue;
                    }

                    if (!IsInsideAnyRendLine(hit.position, lines))
                    {
                        safe = hit.position;
                        return true;
                    }
                }
            }

            safe = current;
            return false;
        }

        private static bool IsInsideAnyRendLine(Vector3 point, List<RendLine> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                RendLine line = lines[i];
                Vector3 delta = point - line.Center;
                delta.y = 0f;

                float along = Mathf.Abs(Vector3.Dot(delta, line.Forward));
                float across = Mathf.Abs(Vector3.Dot(delta, line.Right));

                if (along <= line.HalfLength && across <= line.HalfWidth)
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerator CastFracturedReality(int generation, int castNumber)
        {
            if (!CanBeginCast(generation))
            {
                yield break;
            }

            List<Character> selected = SelectTetherTargets(castNumber);
            if (selected.Count < 2)
            {
                yield break;
            }

            _castActive = true;
            RaidBossCastBarRuntime.Show(
                FracturedRealityName,
                FracturedRealityMarkCastTime);

            float markEnds =
                Time.realtimeSinceStartup + FracturedRealityMarkCastTime;

            while (IsCurrent(generation) &&
                   IsBossAlive() &&
                   Time.realtimeSinceStartup < markEnds)
            {
                yield return null;
            }

            RaidBossCastBarRuntime.Hide();
            RestoreBossAfterCast();

            if (!IsCurrent(generation) || !IsBossAlive())
            {
                _castActive = false;
                yield break;
            }

            List<TetherState> tethers = new List<TetherState>();
            Dictionary<SimPlayer, Vector3> simDestinations =
                new Dictionary<SimPlayer, Vector3>();

            for (int i = 0; i + 1 < selected.Count; i += 2)
            {
                Character a = selected[i];
                Character b = selected[i + 1];

                TetherState state = new TetherState();
                state.A = a;
                state.B = b;
                state.Broken = false;
                state.Beam = CreateTetherBeam();
                tethers.Add(state);

                AddTetherPairSimDestinations(
                    simDestinations,
                    a,
                    b,
                    FracturedRealityBreakDistance +
                    FracturedRealitySimEscapePadding,
                    i / 2 + castNumber * 7);
            }

            WorldBossSimAvoidanceRuntime.BeginDirectedMovement(
                simDestinations,
                "Fractured Reality");

            float ends = Time.realtimeSinceStartup + FracturedRealityDuration;

            while (IsCurrent(generation) &&
                   IsBossAlive() &&
                   Time.realtimeSinceStartup < ends)
            {
                bool allBroken = true;

                for (int i = 0; i < tethers.Count; i++)
                {
                    TetherState tether = tethers[i];

                    if (tether.Broken)
                    {
                        continue;
                    }

                    if (!IsLivingCharacter(tether.A) ||
                        !IsLivingCharacter(tether.B))
                    {
                        tether.Broken = true;
                        DestroyObject(tether.Beam);
                        continue;
                    }

                    UpdateTetherBeam(tether);

                    if (PlanarDistance(
                            tether.A.transform.position,
                            tether.B.transform.position) >=
                        FracturedRealityBreakDistance)
                    {
                        tether.Broken = true;
                        DestroyObject(tether.Beam);

                        Plugin.NativeLog.LogInfo(
                            "[NhalSera] Fractured Reality tether broken.");
                    }
                    else
                    {
                        allBroken = false;
                    }
                }

                WorldBossSimAvoidanceRuntime.UpdateMovement();
                UpdateRifts();
                UpdateVeil();

                if (allBroken)
                {
                    break;
                }

                yield return null;
            }

            if (!IsCurrent(generation) || !IsBossAlive())
            {
                for (int i = 0; i < tethers.Count; i++)
                {
                    DestroyObject(tethers[i].Beam);
                }

                WorldBossSimAvoidanceRuntime.ReleaseAssignments(
                    "Fractured Reality interrupted");

                _castActive = false;
                yield break;
            }

            int failures = 0;

            for (int i = 0; i < tethers.Count; i++)
            {
                TetherState tether = tethers[i];

                if (!tether.Broken)
                {
                    failures++;
                    ApplyNamedDamage(
                        tether.A,
                        FracturedRealityName,
                        FracturedRealityFailureDamage);

                    ApplyNamedDamage(
                        tether.B,
                        FracturedRealityName,
                        FracturedRealityFailureDamage);
                }

                DestroyObject(tether.Beam);
            }

            WorldBossSimAvoidanceRuntime.ReleaseAssignments(
                "Fractured Reality complete");

            _castActive = false;

            Plugin.NativeLog.LogInfo(
                "[NhalSera] Fractured Reality complete. FailedPairs=[" +
                failures + "].");
        }

        private static List<Character> SelectTetherTargets(int castNumber)
        {
            List<Character> living = GetLivingRaidCharacters();
            List<Character> selected = new List<Character>();

            if (living.Count < 2)
            {
                return selected;
            }

            int count = Mathf.Min(FracturedRealityMaxTargets, living.Count);
            if ((count & 1) != 0)
            {
                count--;
            }

            int start = Mathf.Abs(castNumber * 3) % living.Count;

            for (int i = 0; i < count; i++)
            {
                selected.Add(living[(start + i) % living.Count]);
            }

            return selected;
        }

        private static void AddTetherPairSimDestinations(
            Dictionary<SimPlayer, Vector3> destinations,
            Character a,
            Character b,
            float desiredSeparation,
            int seed)
        {
            if (a == null || b == null)
            {
                return;
            }

            SimPlayer simA = FindSimForCharacter(a);
            SimPlayer simB = FindSimForCharacter(b);

            if (simA == null && simB == null)
            {
                return;
            }

            Vector3 aPos = a.transform.position;
            Vector3 bPos = b.transform.position;
            Vector3 axis = aPos - bPos;
            axis.y = 0f;

            if (axis.sqrMagnitude < 0.01f)
            {
                float angle = (seed * 73f) % 360f;
                axis = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            }

            axis.Normalize();

            // If both endpoints are Sims, force them to opposite sides of the
            // midpoint. This guarantees a melee+ranged pair actually separates
            // instead of both receiving ineffective same-side destinations.
            if (simA != null && simB != null)
            {
                Vector3 midpoint = (aPos + bPos) * 0.5f;
                float half = desiredSeparation * 0.5f + 1.5f;

                destinations[simA] = midpoint + axis * half;
                destinations[simB] = midpoint - axis * half;
                return;
            }

            if (simA != null)
            {
                destinations[simA] =
                    bPos + axis * (desiredSeparation + 1.5f);
            }

            if (simB != null)
            {
                destinations[simB] =
                    aPos - axis * (desiredSeparation + 1.5f);
            }
        }

        private static IEnumerator CastIntoTheVoid(int generation, int castNumber)
        {
            if (!CanBeginCast(generation))
            {
                yield break;
            }

            Character target = SelectIntoTheVoidTarget(castNumber);
            if (target == null)
            {
                yield break;
            }

            _castActive = true;
            RaidBossCastBarRuntime.Show(IntoTheVoidName, IntoTheVoidCastTime);

            GameObject marker = CreateGroundDisc(
                target.transform.position,
                1.8f,
                new Color(0.55f, 0.05f, 0.95f, 0.50f),
                "FOTF_NhalSera_IntoTheVoidTarget");
            CastTelegraphs.Add(marker);

            float ends = Time.realtimeSinceStartup + IntoTheVoidCastTime;

            while (IsCurrent(generation) &&
                   IsBossAlive() &&
                   IsLivingCharacter(target) &&
                   Time.realtimeSinceStartup < ends)
            {
                yield return null;
            }

            if (IsCurrent(generation) &&
                IsBossAlive() &&
                IsLivingCharacter(target))
            {
                Vector3 origin = target.transform.position;
                Vector3 destination = ResolveDisplacementDestination(
                    castNumber,
                    origin);

                TeleportCharacter(target, destination);

                DisplacedUntil[target.GetInstanceID()] =
                    Time.realtimeSinceStartup + IntoTheVoidReTargetLockout;

                AddVoidRift(origin);

                Plugin.NativeLog.LogInfo(
                    "[NhalSera] Into the Void displaced [" +
                    GetCharacterName(target) + "].");
            }

            FinishCast();
        }

        private static Character SelectIntoTheVoidTarget(int castNumber)
        {
            List<Character> living = GetLivingRaidCharacters();
            if (living.Count == 0)
            {
                return null;
            }

            float now = Time.realtimeSinceStartup;
            int start = Mathf.Abs(castNumber * 5) % living.Count;

            for (int i = 0; i < living.Count; i++)
            {
                Character candidate = living[(start + i) % living.Count];
                float lockout;

                if (DisplacedUntil.TryGetValue(
                        candidate.GetInstanceID(),
                        out lockout) &&
                    lockout > now)
                {
                    continue;
                }

                return candidate;
            }

            return living[start];
        }

        private static Vector3 ResolveDisplacementDestination(
            int castNumber,
            Vector3 fallback)
        {
            Vector3 center = _boss != null ? _boss.transform.position : _homePosition;
            float angle = (castNumber * 137f + 41f) % 360f;
            float t = ((castNumber * 37) % 100) / 100f;
            float radius = Mathf.Lerp(IntoTheVoidMinRadius, IntoTheVoidMaxRadius, t);
            Vector3 desired =
                center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(
                    desired,
                    out hit,
                    6f,
                    NavMesh.AllAreas))
            {
                return hit.position;
            }

            return fallback;
        }

        private static void AddVoidRift(Vector3 position)
        {
            RiftState state = new RiftState();
            state.Position = ProjectToGround(position);
            state.ExpiresRealtime = Time.realtimeSinceStartup + VoidRiftDuration;
            state.NextPulseRealtime = Time.realtimeSinceStartup + VoidRiftPulseSeconds;
            state.Visual = CreateGroundDisc(
                state.Position,
                VoidRiftRadius,
                new Color(0.34f, 0.00f, 0.52f, 0.36f),
                "FOTF_NhalSera_VoidRift");

            ActiveRifts.Add(state);
        }

        private static void UpdateRifts()
        {
            if (ActiveRifts.Count == 0)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;

            for (int i = ActiveRifts.Count - 1; i >= 0; i--)
            {
                RiftState state = ActiveRifts[i];

                if (state == null || now >= state.ExpiresRealtime)
                {
                    if (state != null)
                    {
                        DestroyObject(state.Visual);
                    }

                    ActiveRifts.RemoveAt(i);
                    continue;
                }

                if (now < state.NextPulseRealtime)
                {
                    continue;
                }

                state.NextPulseRealtime += VoidRiftPulseSeconds;

                List<Character> living = GetLivingRaidCharacters();
                for (int c = 0; c < living.Count; c++)
                {
                    if (PlanarDistance(
                            living[c].transform.position,
                            state.Position) <= VoidRiftRadius)
                    {
                        ApplyNamedDamage(
                            living[c],
                            "Void Rift",
                            VoidRiftPulseDamage);
                    }
                }
            }
        }

        private static IEnumerator CastShatteredReflection(
            int generation,
            int castNumber)
        {
            if (!CanBeginCast(generation))
            {
                yield break;
            }

            _castActive = true;

            Vector3 originalPosition = _boss.transform.position;
            Quaternion originalRotation = _boss.transform.rotation;
            Character originalTarget =
                _boss != null
                    ? _boss.CurrentAggroTarget
                    : null;

            Vector3[] positions = BuildReflectionPositions(
                originalPosition,
                castNumber);

            int trueIndex = Mathf.Abs(castNumber + 1) % positions.Length;
            Vector3 truePosition = positions[trueIndex];

            ClearPlayerTargetForReflection();

            TeleportBossTo(
                truePosition,
                Quaternion.LookRotation(
                    (originalPosition - truePosition).normalized,
                    Vector3.up));

            CreateReflectionDecoys(positions, trueIndex, originalPosition);
            CreateTrueReflectionMarker();

            Stats bossStats =
                _bossCharacter != null
                    ? _bossCharacter.MyStats
                    : null;

            _reflectionSucceeded = false;
            _reflectionStartHP =
                bossStats != null
                    ? bossStats.CurrentHP
                    : 0;
            _reflectionDamageRequired =
                bossStats != null
                    ? Mathf.Max(
                        1,
                        Mathf.CeilToInt(
                            bossStats.CurrentMaxHP *
                            ReflectionDamageFraction))
                    : 1;
            _reflectionDamageProgress = 0;

            RaidBossCastBarRuntime.Show(
                UnmakingName,
                ShatteredReflectionChannel);

            float ends =
                Time.realtimeSinceStartup + ShatteredReflectionChannel;

            while (IsCurrent(generation) &&
                   IsBossAlive() &&
                   !_reflectionSucceeded &&
                   Time.realtimeSinceStartup < ends)
            {
                bossStats =
                    _bossCharacter != null
                        ? _bossCharacter.MyStats
                        : null;

                if (bossStats != null)
                {
                    _reflectionDamageProgress =
                        Mathf.Max(
                            0,
                            _reflectionStartHP -
                            bossStats.CurrentHP);

                    if (_reflectionDamageProgress >=
                        _reflectionDamageRequired)
                    {
                        _reflectionSucceeded = true;

                        Plugin.NativeLog.LogInfo(
                            "[NhalSera] Shattered Reflection solved. ActualHPDamage=[" +
                            _reflectionDamageProgress + "/" +
                            _reflectionDamageRequired +
                            "] Threshold=[3% max HP].");
                    }
                }

                UpdateRifts();
                UpdateVeil();
                yield return null;
            }

            if (IsCurrent(generation) &&
                IsBossAlive() &&
                !_reflectionSucceeded)
            {
                ApplyRaidWideDamage(
                    UnmakingName,
                    ReflectionFailureDamage);

                Plugin.NativeLog.LogInfo(
                    "[NhalSera] Unmaking completed. Reflection failed. ActualHPDamage=[" +
                    _reflectionDamageProgress + "/" +
                    _reflectionDamageRequired + "].");
            }
            else if (_reflectionSucceeded)
            {
                Plugin.NativeLog.LogInfo(
                    "[NhalSera] Unmaking interrupted after the true Nhal'Sera lost 3% max HP.");
            }

            ClearReflectionVisuals();

            if (IsCurrent(generation) && IsBossAlive())
            {
                TeleportBossTo(originalPosition, originalRotation);
            }

            RaidBossCastBarRuntime.Hide();
            RestoreBossAfterCast();

            if (_boss != null &&
                originalTarget != null &&
                IsLivingCharacter(originalTarget))
            {
                _boss.CurrentAggroTarget = originalTarget;
            }

            _castActive = false;
            MaintainBossCombatState(true);
        }

        private static Vector3[] BuildReflectionPositions(
            Vector3 center,
            int castNumber)
        {
            Vector3[] result = new Vector3[3];
            float baseAngle = (castNumber * 47f) % 120f;

            for (int i = 0; i < result.Length; i++)
            {
                float angle = baseAngle + i * 120f;
                Vector3 desired =
                    center +
                    Quaternion.Euler(0f, angle, 0f) * Vector3.forward * ReflectionRadius;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(
                        desired,
                        out hit,
                        5f,
                        NavMesh.AllAreas))
                {
                    result[i] = hit.position;
                }
                else
                {
                    result[i] = desired;
                }
            }

            return result;
        }

        private static void CreateReflectionDecoys(
            Vector3[] positions,
            int trueIndex,
            Vector3 center)
        {
            GameObject prefab = AssetBundleRegistry.GetPrefab(
                "fotf_worldbosses",
                "NhalSera");

            if (prefab == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[NhalSera] Reflection decoys skipped: prefab [NhalSera] not found.");
                return;
            }

            for (int i = 0; i < positions.Length; i++)
            {
                if (i == trueIndex)
                {
                    continue;
                }

                Vector3 facing = center - positions[i];
                facing.y = 0f;
                Quaternion rotation =
                    facing.sqrMagnitude > 0.01f
                        ? Quaternion.LookRotation(facing.normalized, Vector3.up)
                        : Quaternion.identity;

                GameObject decoy = UnityEngine.Object.Instantiate(
                    prefab,
                    positions[i],
                    rotation);

                if (decoy == null)
                {
                    continue;
                }

                decoy.name = "FOTF_NhalSera_FalseReflection";
                SetLayerRecursive(decoy, 2); // Ignore Raycast: only the true boss is targetable.

                Collider[] colliders = decoy.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < colliders.Length; c++)
                {
                    colliders[c].enabled = false;
                }

                NavMeshAgent[] agents = decoy.GetComponentsInChildren<NavMeshAgent>(true);
                for (int a = 0; a < agents.Length; a++)
                {
                    agents[a].enabled = false;
                }

                NativeCreatureAnimationBridge.TryApply(decoy);
                ReflectionDecoys.Add(decoy);
            }
        }

        private static void CreateTrueReflectionMarker()
        {
            if (_boss == null)
            {
                return;
            }

            _trueReflectionMarker =
                GameObject.CreatePrimitive(PrimitiveType.Sphere);

            _trueReflectionMarker.name =
                "FOTF_NhalSera_TrueReflectionMarker";

            Collider collider = _trueReflectionMarker.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            _trueReflectionMarker.transform.SetParent(
                _boss.transform,
                false);

            _trueReflectionMarker.transform.localPosition =
                new Vector3(0f, 10.5f, 0f);

            _trueReflectionMarker.transform.localScale =
                Vector3.one * 0.75f;

            Renderer renderer = _trueReflectionMarker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = CreateTelegraphMaterial(
                    new Color(0.10f, 0.85f, 1.00f, 0.90f));
            }
        }

        private static IEnumerator CastShroudedVeil(int generation)
        {
            if (!CanBeginCast(generation))
            {
                yield break;
            }

            _veilPending = false;
            _castActive = true;
            RaidBossCastBarRuntime.Show(
                ShroudedVeilName,
                ShroudedVeilCastTime);

            float ends = Time.realtimeSinceStartup + ShroudedVeilCastTime;

            while (IsCurrent(generation) &&
                   IsBossAlive() &&
                   Time.realtimeSinceStartup < ends)
            {
                yield return null;
            }

            if (IsCurrent(generation) && IsBossAlive())
            {
                BeginVeil();
            }

            FinishCast();
        }

        private static void BeginVeil()
        {
            ClearVeil();

            _veilActive = true;
            _veilExpired = false;
            _veilStartedRealtime = Time.realtimeSinceStartup;
            _veilCurrentRadius = VeilInitialSafeRadius;
            _veilCenter = ProjectToGround(_boss.transform.position);

            _nextVeilTickRealtime =
                _veilStartedRealtime + VeilTickSeconds;

            _nextVeilVisualRefreshRealtime =
                _veilStartedRealtime;

            _nextVeilSimAssignmentCheckRealtime =
                _veilStartedRealtime + 0.10f;

            _veilSimMoveStartedRealtime =
                float.NegativeInfinity;

            VeilStacks.Clear();
            RefreshVeilVisual();
            EnsureSimsInsideShrinkingVeil(true);

            Plugin.NativeLog.LogInfo(
                "[NhalSera] The Shrouded Veil active. One safe zone begins at [" +
                VeilInitialSafeRadius.ToString("0.0") +
                "m] and shrinks to nothing over [" +
                VeilShrinkDuration.ToString("0") +
                "s]. Final phase is now a DPS check.");
        }

        private static void UpdateVeil()
        {
            if (!_veilActive || _boss == null)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            float elapsed = now - _veilStartedRealtime;
            float t = Mathf.Clamp01(elapsed / VeilShrinkDuration);

            _veilCurrentRadius =
                Mathf.Lerp(
                    VeilInitialSafeRadius,
                    0f,
                    t);

            if (!_veilExpired && t >= 1f)
            {
                _veilExpired = true;
                _veilCurrentRadius = 0f;
                DestroyObject(_veilVisual);
                _veilVisual = null;

                if (WorldBossSimAvoidanceRuntime.IsActiveMechanic(
                        ShroudedVeilName))
                {
                    WorldBossSimAvoidanceRuntime.ReleaseAssignments(
                        "Shrouded Veil safe zone expired");
                }

                Plugin.NativeLog.LogInfo(
                    "[NhalSera] The Shrouded Veil safe zone has disappeared. Hard enrage active.");
            }

            if (!_veilExpired &&
                now >= _nextVeilVisualRefreshRealtime)
            {
                _nextVeilVisualRefreshRealtime =
                    now + VeilVisualRefreshSeconds;

                RefreshVeilVisual();
            }

            if (now >= _nextVeilTickRealtime)
            {
                _nextVeilTickRealtime += VeilTickSeconds;
                ApplyVeilTick();
            }

            if (!_veilExpired &&
                WorldBossSimAvoidanceRuntime.IsActiveMechanic(
                    ShroudedVeilName))
            {
                if (WorldBossSimAvoidanceRuntime.AreAllActiveAssignmentsArrived ||
                    now - _veilSimMoveStartedRealtime >=
                        VeilSimMoveTimeout)
                {
                    WorldBossSimAvoidanceRuntime.ReleaseAssignments(
                        "Shrouded Veil Sims reached shrinking safe zone");
                }
            }

            if (!_veilExpired &&
                now >= _nextVeilSimAssignmentCheckRealtime)
            {
                _nextVeilSimAssignmentCheckRealtime =
                    now + VeilSimCheckSeconds;

                if (!WorldBossSimAvoidanceRuntime.HasActiveAssignments)
                {
                    EnsureSimsInsideShrinkingVeil(false);
                }
            }
        }

        private static void RefreshVeilVisual()
        {
            DestroyObject(_veilVisual);
            _veilVisual = null;

            if (!_veilActive ||
                _veilExpired ||
                _veilCurrentRadius <= 0.15f)
            {
                return;
            }

            _veilVisual = CreateGroundDisc(
                _veilCenter,
                _veilCurrentRadius,
                new Color(0.18f, 0.08f, 0.48f, 0.25f),
                "FOTF_NhalSera_ShrinkingVeilSafeZone");
        }

        private static void EnsureSimsInsideShrinkingVeil(
            bool forceAll)
        {
            if (!_veilActive ||
                _veilExpired ||
                _veilCurrentRadius <= 0.25f)
            {
                return;
            }

            List<SimPlayer> sims =
                WorldBossSimAvoidanceRuntime.GetLivingRaidSimsSnapshot();

            Dictionary<SimPlayer, Vector3> destinations =
                new Dictionary<SimPlayer, Vector3>();

            float safeLimit =
                Mathf.Max(
                    0.50f,
                    _veilCurrentRadius - VeilSimInnerPadding);

            for (int i = 0; i < sims.Count; i++)
            {
                SimPlayer sim = sims[i];
                if (sim == null)
                {
                    continue;
                }

                float distance =
                    PlanarDistance(
                        sim.transform.position,
                        _veilCenter);

                if (!forceAll && distance <= safeLimit)
                {
                    continue;
                }

                float angle =
                    (i * 137f + _generation * 29f) % 360f;

                float desiredRadius =
                    Mathf.Min(
                        2.0f,
                        safeLimit * 0.35f);

                Vector3 desired =
                    _veilCenter +
                    Quaternion.Euler(0f, angle, 0f) *
                    Vector3.forward *
                    desiredRadius;

                destinations[sim] = desired;
            }

            if (destinations.Count == 0)
            {
                return;
            }

            WorldBossSimAvoidanceRuntime.BeginDirectedMovement(
                destinations,
                ShroudedVeilName);

            _veilSimMoveStartedRealtime =
                Time.realtimeSinceStartup;
        }

        private static void ApplyVeilTick()
        {
            List<Character> living = GetLivingRaidCharacters();

            for (int i = 0; i < living.Count; i++)
            {
                Character target = living[i];
                int id = target.GetInstanceID();

                if (IsInsideVeilSafeZone(target.transform.position))
                {
                    VeilStacks[id] = 0;
                    continue;
                }

                int stacks;
                if (!VeilStacks.TryGetValue(id, out stacks))
                {
                    stacks = 0;
                }

                stacks = Mathf.Clamp(stacks + 1, 1, VeilMaxStacks);
                VeilStacks[id] = stacks;

                int damage = VeilBaseDamage +
                    (stacks - 1) * VeilStackDamage;

                ApplyNamedDamage(
                    target,
                    LostInVeilName,
                    damage);
            }
        }

        private static bool IsInsideVeilSafeZone(Vector3 position)
        {
            return _veilActive &&
                !_veilExpired &&
                _veilCurrentRadius > 0f &&
                PlanarDistance(position, _veilCenter) <=
                    _veilCurrentRadius;
        }

        private static void AdvanceSchedule(ref float next, float repeat)
        {
            float now = Time.realtimeSinceStartup;

            if (float.IsInfinity(next))
            {
                next = now + repeat;
                return;
            }

            do
            {
                next += repeat;
            }
            while (next <= now);
        }

        private static bool CanBeginCast(int generation)
        {
            return IsCurrent(generation) &&
                !_castActive &&
                IsBossAlive();
        }

        private static void FinishCast()
        {
            RaidBossCastBarRuntime.Hide();
            RestoreBossAfterCast();
            DestroyCastTelegraphs();
            _castActive = false;

            // Scripted mechanics must never leave the native NPC in a dormant
            // combat state. Reassert the minimal state native AI needs without
            // replacing its threat/attack logic.
            MaintainBossCombatState(true);
        }

        private static void MaintainBossCombatState(bool force)
        {
            if (!_engaged ||
                _boss == null ||
                !IsBossAlive())
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (!force && now < _nextBossCombatRepairRealtime)
            {
                return;
            }

            _nextBossCombatRepairRealtime = now + 0.25f;

            try
            {
                // A scripted teleport/cast should never permanently disable
                // the native NPC or leave it in retreat/relax state.
                _boss.enabled = true;
                _boss.retreat = false;

                if (_bossCharacter != null)
                {
                    _bossCharacter.Relax = false;
                }

                Character target = _boss.CurrentAggroTarget;

                if (!IsLivingCharacter(target))
                {
                    target = FindNearestLivingRaidTarget(
                        _boss.transform.position);

                    _boss.CurrentAggroTarget = target;
                }

                if (target == null)
                {
                    return;
                }

                NavMeshAgent agent =
                    _boss.GetComponent<NavMeshAgent>();

                if (agent == null ||
                    !agent.enabled ||
                    !agent.isOnNavMesh)
                {
                    return;
                }

                // Native melee logic normally stops the agent while in attack
                // range. Only wake it when the target is actually outside that
                // range, so we do not create sliding during normal attacks.
                float attackRange =
                    _bossCharacter != null
                        ? Mathf.Max(0.5f, _bossCharacter.AttackRange)
                        : 4.25f;

                float distance =
                    PlanarDistance(
                        _boss.transform.position,
                        target.transform.position);

                if (distance > attackRange + 1.0f &&
                    agent.isStopped)
                {
                    agent.isStopped = false;
                }
            }
            catch
            {
                // Scene teardown and death can invalidate native components.
            }
        }

        private static Character FindNearestLivingRaidTarget(
            Vector3 origin)
        {
            List<Character> living =
                GetLivingRaidCharacters();

            Character best = null;
            float bestDistanceSqr = float.PositiveInfinity;

            for (int i = 0; i < living.Count; i++)
            {
                Character candidate = living[i];
                if (!IsLivingCharacter(candidate))
                {
                    continue;
                }

                Vector3 delta = candidate.transform.position - origin;
                delta.y = 0f;

                float distanceSqr = delta.sqrMagnitude;
                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    best = candidate;
                }
            }

            return best;
        }


        private static void PauseBossForCast()
        {
            RestoreBossAfterCast();

            _pausedBoss = _boss;
            if (_pausedBoss == null)
            {
                return;
            }

            _savedAggroTarget = _pausedBoss.CurrentAggroTarget;
            _savedNpcEnabled = _pausedBoss.enabled;
            _pausedAgent = _pausedBoss.GetComponent<NavMeshAgent>();

            if (_pausedAgent != null &&
                _pausedAgent.enabled &&
                _pausedAgent.isOnNavMesh)
            {
                try
                {
                    _savedAgentStopped = _pausedAgent.isStopped;
                    _pausedAgent.isStopped = true;
                    _pausedAgent.velocity = Vector3.zero;

                    if (_pausedAgent.hasPath)
                    {
                        _pausedAgent.ResetPath();
                    }
                }
                catch
                {
                    _pausedAgent = null;
                }
            }

            _pausedBoss.CurrentAggroTarget = null;
            _pausedBoss.enabled = false;
        }

        private static void HoldPausedBoss()
        {
            if (_pausedBoss == null)
            {
                return;
            }

            _pausedBoss.CurrentAggroTarget = null;

            if (_pausedAgent != null &&
                _pausedAgent.enabled &&
                _pausedAgent.isOnNavMesh)
            {
                try
                {
                    _pausedAgent.isStopped = true;
                    _pausedAgent.velocity = Vector3.zero;

                    if (_pausedAgent.hasPath)
                    {
                        _pausedAgent.ResetPath();
                    }
                }
                catch
                {
                }
            }
        }

        private static void RestoreBossAfterCast()
        {
            NPC boss = _pausedBoss;

            if (boss == null)
            {
                _pausedBoss = null;
                _pausedAgent = null;
                _savedAggroTarget = null;
                return;
            }

            try
            {
                boss.enabled = _savedNpcEnabled;

                if (_pausedAgent != null &&
                    _pausedAgent.enabled &&
                    _pausedAgent.isOnNavMesh)
                {
                    _pausedAgent.isStopped = _savedAgentStopped;
                }

                if (_savedAggroTarget != null &&
                    IsLivingCharacter(_savedAggroTarget))
                {
                    boss.CurrentAggroTarget = _savedAggroTarget;
                }
            }
            catch
            {
            }

            _pausedBoss = null;
            _pausedAgent = null;
            _savedAggroTarget = null;
        }

        private static void ResetBossToHome(string reason)
        {
            NPC boss = _boss;
            if (boss == null)
            {
                return;
            }

            Character character = _bossCharacter;
            Stats stats = boss.GetComponent<Stats>();

            RaidBossCastBarRuntime.Hide();
            RestoreBossAfterCast();
            WorldBossSimAvoidanceRuntime.ForceReleaseAll(
                "NhalSera reset");
            DestroyCastTelegraphs();
            ClearRifts();
            ClearReflectionVisuals();
            ClearVeil();

            if (character != null)
            {
                character.Alive = true;
            }

            if (stats != null)
            {
                stats.CurrentHP = stats.CurrentMaxHP;
            }

            boss.CurrentAggroTarget = null;
            boss.retreat = false;
            TeleportBossTo(_homePosition, _homeRotation);

            Plugin.NativeLog.LogInfo(
                "[NhalSera] reset to home. Reason=[" + reason + "].");

            AttachBoss(boss, _homePosition);
        }

        private static bool ShouldLeashReset()
        {
            if (_boss == null)
            {
                return false;
            }

            WorldBossDefinition definition = WorldBossCatalog.NhalSera;

            if (Vector3.Distance(_boss.transform.position, _homePosition) >
                definition.LeashRadius)
            {
                return true;
            }

            if (HasLivingRaidCharacterWithin(
                    _boss.transform.position,
                    definition.LeashRadius))
            {
                _noTargetSinceRealtime = -1f;
                return false;
            }

            if (_noTargetSinceRealtime < 0f)
            {
                _noTargetSinceRealtime = Time.realtimeSinceStartup;
                return false;
            }

            return Time.realtimeSinceStartup - _noTargetSinceRealtime >=
                definition.ResetNoTargetSeconds;
        }

        private static bool HasLivingRaidCharacterWithin(Vector3 center, float radius)
        {
            float radiusSqr = radius * radius;
            List<Character> living = GetLivingRaidCharacters();

            for (int i = 0; i < living.Count; i++)
            {
                Vector3 delta = living[i].transform.position - center;
                delta.y = 0f;

                if (delta.sqrMagnitude <= radiusSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private static float GetHealthFraction()
        {
            if (_boss == null)
            {
                return 1f;
            }

            Stats stats = _boss.GetComponent<Stats>();
            if (stats == null || stats.CurrentMaxHP <= 0)
            {
                return 1f;
            }

            return Mathf.Clamp01((float)stats.CurrentHP / stats.CurrentMaxHP);
        }

        private static bool IsBossAlive()
        {
            if (_boss == null || _bossCharacter == null)
            {
                return false;
            }

            Stats stats = _boss.GetComponent<Stats>();

            return _bossCharacter.Alive &&
                stats != null &&
                stats.CurrentHP > 0;
        }

        private static bool IsCurrent(int generation)
        {
            return generation == _generation && _boss != null;
        }

        private static bool ApplyNamedDamage(
            Character target,
            string sourceName,
            int damage)
        {
            if (!IsLivingCharacter(target))
            {
                return false;
            }

            try
            {
                RaidMechanicDamageAttribution.SetNamedSource(
                    _bossCharacter,
                    sourceName);

                int resolved = target.DamageMe(
                    damage,
                    false,
                    GameData.DamageType.Magic,
                    _bossCharacter,
                    false,
                    false,
                    0);

                return resolved > 0;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[NhalSera] [" + sourceName + "] damage failed: " +
                    exception);
                return false;
            }
        }

        private static void ApplyRaidWideDamage(string sourceName, int damage)
        {
            List<Character> living = GetLivingRaidCharacters();

            for (int i = 0; i < living.Count; i++)
            {
                ApplyNamedDamage(living[i], sourceName, damage);
            }
        }

        private static List<Character> GetLivingRaidCharacters()
        {
            List<Character> result = new List<Character>();

            PlayerControl playerControl = GameData.PlayerControl;
            Character player = playerControl != null ? playerControl.Myself : null;
            AddLivingCharacter(result, player);

            if (GameData.GroupMembers != null)
            {
                for (int i = 0; i < GameData.GroupMembers.Length; i++)
                {
                    SimPlayerTracking tracking = GameData.GroupMembers[i];
                    SimPlayer sim = tracking != null ? tracking.MyAvatar : null;
                    NPC npc = sim != null ? sim.GetThisNPC() : null;
                    Character character = npc != null ? npc.GetChar() : null;
                    AddLivingCharacter(result, character);
                }
            }

            RaidManager manager = GameData.RaidManager;
            if (manager != null && manager.Raiders != null)
            {
                foreach (RaidMemberSlot slot in manager.Raiders)
                {
                    SimPlayer sim = slot != null ? slot.AssignedAvatar : null;
                    NPC npc = sim != null ? sim.GetThisNPC() : null;
                    Character character = npc != null ? npc.GetChar() : null;
                    AddLivingCharacter(result, character);
                }
            }

            return result;
        }

        private static void AddLivingCharacter(
            List<Character> result,
            Character character)
        {
            if (!IsLivingCharacter(character))
            {
                return;
            }

            for (int i = 0; i < result.Count; i++)
            {
                if (result[i] == character)
                {
                    return;
                }
            }

            result.Add(character);
        }

        private static bool IsLivingCharacter(Character character)
        {
            return character != null &&
                character.Alive &&
                character.MyStats != null &&
                character.MyStats.CurrentHP > 0;
        }

        private static SimPlayer FindSimForCharacter(Character character)
        {
            if (character == null)
            {
                return null;
            }

            // First prefer the actual hierarchy relationship. Native Sim
            // prefabs can place Character and SimPlayer on the same object or
            // on parent/child objects depending on the archetype.
            SimPlayer direct =
                character.GetComponent<SimPlayer>();

            if (direct == null)
            {
                direct = character.GetComponentInParent<SimPlayer>();
            }

            if (direct == null)
            {
                direct = character.GetComponentInChildren<SimPlayer>(true);
            }

            if (direct != null)
            {
                return direct;
            }

            List<SimPlayer> sims =
                WorldBossSimAvoidanceRuntime.GetLivingRaidSimsSnapshot();

            for (int i = 0; i < sims.Count; i++)
            {
                SimPlayer sim = sims[i];
                if (sim == null)
                {
                    continue;
                }

                NPC npc = sim.GetThisNPC();

                Character npcCharacter =
                    npc != null
                        ? npc.GetChar()
                        : null;

                if (npcCharacter == character)
                {
                    return sim;
                }

                Character npcComponent =
                    npc != null
                        ? npc.GetComponent<Character>()
                        : null;

                if (npcComponent == character)
                {
                    return sim;
                }

                if (sim.MyStats != null &&
                    sim.MyStats.Myself == character)
                {
                    return sim;
                }

                Character simComponent =
                    sim.GetComponent<Character>();

                if (simComponent == character)
                {
                    return sim;
                }
            }

            return null;
        }

        private static void TeleportCharacter(Character target, Vector3 destination)
        {
            if (target == null)
            {
                return;
            }

            SimPlayer sim = FindSimForCharacter(target);
            if (sim != null)
            {
                NavMeshAgent agent = sim.GetComponent<NavMeshAgent>();

                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    try
                    {
                        if (agent.Warp(destination))
                        {
                            return;
                        }
                    }
                    catch
                    {
                    }
                }

                sim.transform.position = destination;
                return;
            }

            target.transform.position = destination;
        }

        private static void TeleportBossTo(Vector3 position, Quaternion rotation)
        {
            if (_boss == null)
            {
                return;
            }

            Vector3 resolved = position;
            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                    position,
                    out hit,
                    5f,
                    NavMesh.AllAreas))
            {
                resolved = hit.position;
            }

            NavMeshAgent agent = _boss.GetComponent<NavMeshAgent>();
            bool warped = false;

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                try
                {
                    warped = agent.Warp(resolved);
                }
                catch
                {
                    warped = false;
                }
            }

            if (!warped)
            {
                _boss.transform.position = resolved;
            }

            _boss.transform.rotation = rotation;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static string GetCharacterName(Character character)
        {
            if (character == null)
            {
                return "<null>";
            }

            try
            {
                if (character.MyStats != null &&
                    !string.IsNullOrEmpty(character.MyStats.MyName))
                {
                    return character.MyStats.MyName;
                }
            }
            catch
            {
            }

            return character.name;
        }

        private static void ClearPlayerTargetForReflection()
        {
            PlayerControl playerControl =
                GameData.PlayerControl;

            if (playerControl == null)
            {
                return;
            }

            Character current =
                playerControl.CurrentTarget;

            if (current != null &&
                current.TargetRing != null)
            {
                current.TargetRing.SetActive(
                    false);
            }

            playerControl.CurrentTarget =
                null;
        }

        private static GameObject CreateLineTelegraph(
            Vector3 center,
            Vector3 forward,
            float halfLength,
            float halfWidth,
            Color color)
        {
            GameObject line =
                new GameObject(
                    "FOTF_NhalSera_RealityRendLine");

            MeshFilter filter =
                line.AddComponent<MeshFilter>();

            MeshRenderer renderer =
                line.AddComponent<MeshRenderer>();

            Mesh mesh =
                new Mesh();

            const int lengthSegments = 24;
            const int widthSegments = 4;
            int rowWidth = widthSegments + 1;

            Vector3 planarForward = forward;
            planarForward.y = 0f;

            if (planarForward.sqrMagnitude < 0.0001f)
            {
                planarForward = Vector3.forward;
            }

            planarForward.Normalize();

            Vector3 right =
                Vector3.Cross(
                    Vector3.up,
                    planarForward).normalized;

            Vector3[] vertices =
                new Vector3[
                    (lengthSegments + 1) *
                    (widthSegments + 1)];

            List<int> triangles =
                new List<int>();

            for (int z = 0; z <= lengthSegments; z++)
            {
                float tz =
                    (float)z /
                    lengthSegments;

                float along =
                    Mathf.Lerp(
                        -halfLength,
                        halfLength,
                        tz);

                for (int x = 0; x <= widthSegments; x++)
                {
                    float tx =
                        (float)x /
                        widthSegments;

                    float across =
                        Mathf.Lerp(
                            -halfWidth,
                            halfWidth,
                            tx);

                    Vector3 world =
                        center +
                        planarForward * along +
                        right * across;

                    Vector3 projected =
                        ProjectTelegraphPointToGround(
                            world);

                    vertices[
                        z * rowWidth + x] =
                        projected +
                        Vector3.up * 0.08f;
                }
            }

            for (int z = 0; z < lengthSegments; z++)
            {
                for (int x = 0; x < widthSegments; x++)
                {
                    int i0 = z * rowWidth + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + rowWidth;
                    int i3 = i2 + 1;

                    triangles.Add(i0);
                    triangles.Add(i2);
                    triangles.Add(i3);

                    triangles.Add(i0);
                    triangles.Add(i3);
                    triangles.Add(i1);
                }
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            filter.sharedMesh = mesh;
            renderer.material =
                CreateTelegraphMaterial(
                    color);

            // World-space vertices let every sample independently follow the
            // actual terrain height and slope.
            line.transform.position = Vector3.zero;
            line.transform.rotation = Quaternion.identity;

            return line;
        }

        private static GameObject CreateGroundDisc(
            Vector3 position,
            float radius,
            Color color,
            string name)
        {
            GameObject disc =
                new GameObject(
                    name);

            MeshFilter filter =
                disc.AddComponent<MeshFilter>();

            MeshRenderer renderer =
                disc.AddComponent<MeshRenderer>();

            Mesh mesh =
                new Mesh();

            const int angularSegments = 40;
            const int radialSegments = 8;
            int ringWidth = angularSegments + 1;

            Vector3[] vertices =
                new Vector3[
                    1 +
                    radialSegments *
                    ringWidth];

            List<int> triangles =
                new List<int>();

            Vector3 projectedCenter =
                ProjectTelegraphPointToGround(
                    position);

            vertices[0] =
                projectedCenter +
                Vector3.up * 0.06f;

            for (int ring = 1; ring <= radialSegments; ring++)
            {
                float ringRadius =
                    radius *
                    ring /
                    radialSegments;

                int ringStart =
                    1 +
                    (ring - 1) *
                    ringWidth;

                for (int a = 0; a <= angularSegments; a++)
                {
                    float angle =
                        ((float)a /
                         angularSegments) *
                        Mathf.PI *
                        2f;

                    Vector3 world =
                        new Vector3(
                            position.x +
                            Mathf.Sin(angle) *
                            ringRadius,
                            position.y,
                            position.z +
                            Mathf.Cos(angle) *
                            ringRadius);

                    Vector3 projected =
                        ProjectTelegraphPointToGround(
                            world);

                    vertices[ringStart + a] =
                        projected +
                        Vector3.up * 0.06f;
                }
            }

            int firstRingStart = 1;

            for (int a = 0; a < angularSegments; a++)
            {
                triangles.Add(0);
                triangles.Add(firstRingStart + a);
                triangles.Add(firstRingStart + a + 1);
            }

            for (int ring = 2; ring <= radialSegments; ring++)
            {
                int innerStart =
                    1 +
                    (ring - 2) *
                    ringWidth;

                int outerStart =
                    1 +
                    (ring - 1) *
                    ringWidth;

                for (int a = 0; a < angularSegments; a++)
                {
                    int i0 = innerStart + a;
                    int i1 = innerStart + a + 1;
                    int o0 = outerStart + a;
                    int o1 = outerStart + a + 1;

                    triangles.Add(i0);
                    triangles.Add(o0);
                    triangles.Add(o1);

                    triangles.Add(i0);
                    triangles.Add(o1);
                    triangles.Add(i1);
                }
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            filter.sharedMesh = mesh;
            renderer.material =
                CreateTelegraphMaterial(
                    color);

            disc.transform.position = Vector3.zero;
            disc.transform.rotation = Quaternion.identity;

            return disc;
        }

        private static GameObject CreateTetherBeam()
        {
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "FOTF_NhalSera_FracturedRealityTether";

            Collider collider = beam.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            Renderer renderer = beam.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = CreateTelegraphMaterial(
                    new Color(0.72f, 0.04f, 1.00f, 0.80f));
            }

            return beam;
        }

        private static void UpdateTetherBeam(TetherState tether)
        {
            if (tether == null ||
                tether.Beam == null ||
                tether.A == null ||
                tether.B == null)
            {
                return;
            }

            Vector3 a = tether.A.transform.position + Vector3.up * 1.5f;
            Vector3 b = tether.B.transform.position + Vector3.up * 1.5f;
            Vector3 delta = b - a;
            float distance = delta.magnitude;

            if (distance < 0.01f)
            {
                distance = 0.01f;
                delta = Vector3.forward;
            }

            tether.Beam.transform.position = (a + b) * 0.5f;
            tether.Beam.transform.rotation =
                Quaternion.LookRotation(delta.normalized, Vector3.up);
            tether.Beam.transform.localScale =
                new Vector3(0.12f, 0.12f, distance);
        }

        private static Vector3 ProjectToGround(Vector3 point)
        {
            return ProjectTelegraphPointToGround(
                point);
        }

        private static Vector3 ProjectTelegraphPointToGround(
            Vector3 point)
        {
            const float probeUp = 16f;
            const float probeDown = 40f;

            Vector3 origin =
                point +
                Vector3.up *
                probeUp;

            RaycastHit[] hits =
                Physics.RaycastAll(
                    origin,
                    Vector3.down,
                    probeUp + probeDown,
                    ~0,
                    QueryTriggerInteraction.Ignore);

            if (hits != null &&
                hits.Length > 0)
            {
                Array.Sort(
                    hits,
                    delegate(
                        RaycastHit a,
                        RaycastHit b)
                    {
                        return a.distance.CompareTo(
                            b.distance);
                    });

                for (int i = 0; i < hits.Length; i++)
                {
                    Collider collider =
                        hits[i].collider;

                    if (collider == null)
                    {
                        continue;
                    }

                    // Do not project onto a player, Sim, boss or other NPC.
                    if (collider.GetComponentInParent<Character>() != null ||
                        collider.GetComponentInParent<NPC>() != null)
                    {
                        continue;
                    }

                    return hits[i].point;
                }
            }

            NavMeshHit navHit;

            if (NavMesh.SamplePosition(
                    point,
                    out navHit,
                    8f,
                    NavMesh.AllAreas))
            {
                return navHit.position;
            }

            return point;
        }

        private static Material CreateTelegraphMaterial(Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material = new Material(shader);
            material.color = color;

            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 3f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt(
                    "_SrcBlend",
                    (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt(
                    "_DstBlend",
                    (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;

            return material;
        }

        private static void DestroyCastTelegraphs()
        {
            for (int i = 0; i < CastTelegraphs.Count; i++)
            {
                DestroyObject(CastTelegraphs[i]);
            }

            CastTelegraphs.Clear();
        }

        private static void ClearRifts()
        {
            for (int i = 0; i < ActiveRifts.Count; i++)
            {
                if (ActiveRifts[i] != null)
                {
                    DestroyObject(ActiveRifts[i].Visual);
                }
            }

            ActiveRifts.Clear();
            DisplacedUntil.Clear();
        }

        private static void ClearReflectionVisuals()
        {
            for (int i = 0; i < ReflectionDecoys.Count; i++)
            {
                DestroyObject(ReflectionDecoys[i]);
            }

            ReflectionDecoys.Clear();
            DestroyObject(_trueReflectionMarker);
            _trueReflectionMarker = null;
        }

        private static void ClearVeil()
        {
            DestroyObject(_veilVisual);
            _veilVisual = null;

            VeilStacks.Clear();
            _veilActive = false;
            _veilExpired = false;
            _veilStartedRealtime = 0f;
            _veilCurrentRadius = 0f;
            _veilCenter = Vector3.zero;
            _nextVeilTickRealtime = float.PositiveInfinity;
            _nextVeilVisualRefreshRealtime = float.PositiveInfinity;
            _nextVeilSimAssignmentCheckRealtime = float.PositiveInfinity;
            _veilSimMoveStartedRealtime = float.NegativeInfinity;
        }

        private static void DestroyObject(GameObject obj)
        {
            if (obj != null)
            {
                UnityEngine.Object.Destroy(obj);
            }
        }

        private static void SetLayerRecursive(GameObject root, int layer)
        {
            if (root == null)
            {
                return;
            }

            root.layer = layer;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null)
                {
                    transforms[i].gameObject.layer = layer;
                }
            }
        }
    }

    // -----------------------------------------------------------------
    // Combat-clock + Shattered Reflection damage hooks.
    // These are separate from Moldermaw's patches; NotifyBossDamaged is a
    // no-op unless the damaged Character is the currently attached Nhal'Sera.
    // -----------------------------------------------------------------

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
    internal static class Patch_NhalSeraCombatClockDamage
    {
        private static void Prefix(Character __instance, int _incdmg)
        {
            NhalSeraEncounterRuntime.NotifyBossDamaged(__instance, _incdmg);
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
    internal static class Patch_NhalSeraCombatClockMagicDamage
    {
        private static void Prefix(Character __instance, int _dmg)
        {
            NhalSeraEncounterRuntime.NotifyBossDamaged(__instance, _dmg);
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
    internal static class Patch_NhalSeraCombatClockBleedDamage
    {
        private static void Prefix(Character __instance, int _incdmg)
        {
            NhalSeraEncounterRuntime.NotifyBossDamaged(__instance, _incdmg);
        }
    }
}
