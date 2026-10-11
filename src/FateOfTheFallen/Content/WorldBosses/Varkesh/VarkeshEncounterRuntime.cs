using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace FateOfTheFallen
{
    /// <summary>
    /// Varkesh the Unburied - Willowwatch Ridge world-boss encounter.
    ///
    /// Encounter identity:
    /// - melee / death-magic spellblade;
    /// - movement pressure rather than Nhal'Sera-style safe-zone puzzles;
    /// - killable marching soul adds;
    /// - persistent burial zones;
    /// - escalating tank pressure;
    /// - aggressive final 10% burn.
    ///
    /// Phase 1 (100-70%):
    ///     Death's Grasp -> Soul Rupture
    ///     Marked for Burial
    ///     Grave March
    ///     Reaper's Brand
    ///
    /// Phase 2 (70-40%):
    ///     Grave March remains one priority soul;
    ///     more Burial marks;
    ///     shorter schedules.
    ///
    /// Phase 3 (40-10%):
    ///     the single Grave March soul bursts on death;
    ///     mechanics accelerate again.
    ///
    /// Final 10%:
    ///     Refusal to Die - melee damage/attack-speed frenzy.
    /// </summary>
    internal static class VarkeshEncounterRuntime
    {
        internal const float PhaseTwoThreshold =
            0.70f;

        internal const float PhaseThreeThreshold =
            0.40f;

        internal const float FinalPhaseThreshold =
            0.10f;

        private const string DeathsGraspName =
            "Death's Grasp";

        private const string SoulRuptureName =
            "Soul Rupture";

        private const string MarkedForBurialName =
            "Marked for Burial";

        private const string GraveMarchName =
            "Grave March";

        private const string ReapersBrandName =
            "Reaper's Brand";

        private const string SoulBurstName =
            "Deathbound Soul Burst";

        private const float DeathsGraspInitialDelay =
            8f;

        private const float DeathsGraspRepeatPhaseOne =
            22f;

        private const float DeathsGraspRepeatPhaseTwo =
            18f;

        private const float DeathsGraspRepeatPhaseThree =
            15f;

        private const float DeathsGraspCastTime =
            3.25f;

        private const float DeathsGraspPullStart =
            2.2f;

        private const float DeathsGraspPullEnd =
            6.5f;

        // Death's Grasp is an encounter-area mechanic, not a zone-wide pull.
        // Only living raid members inside this radius are assigned movement or
        // physically pulled by the mechanic.
        private const float DeathsGraspMaxRange =
            30f;

        private const float SoulRuptureRadius =
            10.5f;

        private const float SoulRuptureEscapeRadius =
            16.5f;

        private const int SoulRuptureDamage =
            11000;

        private const float BurialInitialDelay =
            13f;

        private const float BurialRepeatPhaseOne =
            20f;

        private const float BurialRepeatPhaseTwo =
            17f;

        private const float BurialRepeatPhaseThree =
            14f;

        private const float BurialCastTime =
            3f;

        private const float BurialPoolRadius =
            4.2f;

        private const float BurialPoolDuration =
            10f;

        private const float BurialPulseInterval =
            1f;

        private const int BurialPulseDamage =
            3600;

        private const float GraveMarchInitialDelay =
            19f;

        private const float GraveMarchRepeatPhaseOne =
            32f;

        private const float GraveMarchRepeatPhaseTwo =
            27f;

        private const float GraveMarchRepeatPhaseThree =
            30f;

        private const float GraveMarchCastTime =
            2.25f;

        private const float GraveSoulSpawnRadius =
            40f;

        private const float GraveSoulConsumeRadius =
            2.8f;

        // Waking Nightmare's native run speed made the priority add difficult
        // to identify and react to before it reached Varkesh. Grave March owns
        // this movement, so use a deliberately readable encounter speed.
        private const float GraveSoulMarchSpeed =
            2.0f;

        // The native model remains intact, but a tall violet world marker is
        // attached so the single priority add stays visible behind Varkesh,
        // foliage and other raid members.
        private const float GraveSoulBeaconHeight =
            6.5f;

        private const int GraveSoulHP =
            1250000;

        private const int GraveSoulHeal =
            2250000;

        private const float GraveSoulDamageBuffPerStack =
            0.08f;

        private const int GraveSoulDamageBuffMaxStacks =
            5;

        private const int GraveSoulBurstDamage =
            4800;

        private const float GraveSoulBurstRadius =
            4.5f;

        private const float ReapersBrandInitialDelay =
            6f;

        private const float ReapersBrandRepeat =
            8f;

        private const float ReapersBrandResetTime =
            30f;

        private const int ReapersBrandBaseDamage =
            2400;

        private const int ReapersBrandPerStackDamage =
            1100;

        private const int ReapersBrandMaxStacks =
            5;

        private const float FinalDamageMultiplier =
            1.35f;

        private const float FinalAttackSpeedMultiplier =
            1.25f;

        private const float BurialMarkerMoveDistance =
            18f;

        // Two 4.2m Burial pools need >8.4m centre separation to avoid
        // overlapping. Give the Sim solution extra clearance so targeted Sims
        // also remain visibly separated from the rest of the raid.
        private const float BurialSimMinimumSeparation =
            10.5f;

        private const float BurialSimMaximumRingDistance =
            28f;

        private const int BurialSimCandidateDirections =
            24;

        // Sim tanks can gradually kite a native melee boss away from the
        // encounter centre. Keep normal tanking inside this soft boundary while
        // retaining a much larger hard reset leash as a true fail-safe.
        private const float TankSimSoftBoundaryFromHome =
            42f;

        private const float TankSimReturnRadiusFromHome =
            24f;

        private const float TankSimTetherCheckInterval =
            0.35f;

        private const float TankSimTetherTimeout =
            3.0f;

        private const string TankSimTetherMechanic =
            "Varkesh Tank Tether";

        // Native proximity detection should only start combat when a player
        // or Sim is actually close to Varkesh. The previous 80m value made him
        // notice units far outside the intended encounter footprint.
        internal const float NativeAggroDetectionRange =
            28f;

        // Once combat has started, allow some extra room for tank movement and
        // mechanics, but never reacquire/chase raid members across the zone.
        private const float BossCombatRetentionRadius =
            50f;

        // Marked for Burial can only choose raid members participating within
        // the encounter footprint. This also prevents future targeted mechanics
        // from accidentally selecting someone on the opposite side of the zone.
        private const float EncounterMechanicParticipantRadius =
            50f;

        // Reassert minimal native boss-combat state periodically. Scripted
        // movement and native retreat logic can transiently clear the target or
        // stop the NavMeshAgent even though the raid is still fighting.
        private const float BossCombatRepairInterval =
            0.25f;

        private static NPC _boss;
        private static Character _bossCharacter;
        private static Vector3 _homePosition;
        private static Quaternion _homeRotation;

        private static int _generation;
        private static bool _engaged;
        private static bool _castActive;
        private static bool _phaseTwo;
        private static bool _phaseThree;
        private static bool _finalPhase;

        private static float _combatStartedRealtime =
            -1f;

        private static float _noTargetSinceRealtime =
            -1f;

        private static float _nextDeathsGraspRealtime =
            float.PositiveInfinity;

        private static float _nextBurialRealtime =
            float.PositiveInfinity;

        private static float _nextGraveMarchRealtime =
            float.PositiveInfinity;

        private static float _nextReapersBrandRealtime =
            float.PositiveInfinity;

        private static float _brandResetRealtime =
            float.PositiveInfinity;

        private static int _burialCastIndex;
        private static int _graveMarchCastIndex;

        private static float _nextBossCombatRepairRealtime;
        private static float _nextTankTetherCheckRealtime;
        private static bool _tankTetherActive;

        private static int _baseAttackDamage;
        private static Vector2 _baseDamageRange;
        private static float _baseMhDelay;
        private static int _graveMarchBuffStacks;

        private static readonly Dictionary<int, int>
            BrandStacks =
                new Dictionary<int, int>();

        private sealed class BurialPool
        {
            internal Vector3 Position;
            internal float ExpiresRealtime;
            internal float NextPulseRealtime;
            internal GameObject Visual;
        }

        private sealed class GraveSoul
        {
            internal GameObject Root;
            internal GameObject ControllerRoot;
            internal NPC Npc;
            internal Character Character;
            internal NavMeshAgent Agent;
            internal GameObject PriorityVisual;
            internal bool Resolved;
        }

        private sealed class PendingGraveSoulSpawn
        {
            internal GameObject ControllerRoot;
            internal SpawnPoint SpawnPoint;
            internal Vector3 Position;
            internal int Ordinal;
            internal int NpcSeenFrame = -1;
        }

        private static readonly List<BurialPool>
            ActiveBurialPools =
                new List<BurialPool>();

        private static readonly List<GraveSoul>
            ActiveGraveSouls =
                new List<GraveSoul>();

        private static readonly List<GameObject>
            CastVisuals =
                new List<GameObject>();


        internal static bool IsEngaged
        {
            get
            {
                return _engaged;
            }
        }


        internal static bool IsCurrentBossCharacter(
            Character character)
        {
            return
                character != null &&
                character == _bossCharacter &&
                _boss != null;
        }


        internal static bool IsCurrentBossStats(
            Stats stats)
        {
            return
                stats != null &&
                _bossCharacter != null &&
                _bossCharacter.MyStats == stats &&
                _boss != null;
        }


        internal static void AttachBoss(
            NPC boss,
            Vector3 homePosition)
        {
            Stop(
                "boss attach");

            _generation++;

            _boss =
                boss;

            _bossCharacter =
                boss != null
                    ? boss.GetChar()
                    : null;

            if (_bossCharacter == null &&
                boss != null)
            {
                _bossCharacter =
                    boss.GetComponent<Character>();
            }

            _homePosition =
                homePosition;

            _homeRotation =
                boss != null
                    ? boss.transform.rotation
                    : Quaternion.identity;

            ResetEncounterState();

            if (_boss == null ||
                _bossCharacter == null)
            {
                return;
            }

            Stats stats =
                _bossCharacter.MyStats;

            if (stats == null)
            {
                stats =
                    _boss.GetComponent<Stats>();
            }

            _baseAttackDamage =
                _boss.BaseAtkDmg;

            _baseDamageRange =
                _boss.DamageRange;

            _baseMhDelay =
                stats != null
                    ? stats.BaseMHAtkDelay
                    : 0f;

            int generation =
                _generation;

            CoroutineHost.Start(
                Run(
                    generation));

            Plugin.NativeLog.LogInfo(
                "[Varkesh] encounter attached. Mechanics=[Death's Grasp, Soul Rupture, Marked for Burial, Grave March, Reaper's Brand, Refusal to Die].");
        }


        internal static void Stop(
            string reason)
        {
            _generation++;

            if (_castActive)
            {
                RaidBossCastBarRuntime.Hide();
            }

            _castActive =
                false;

            _tankTetherActive =
                false;

            WorldBossSimAvoidanceRuntime.ForceReleaseAll(
                string.IsNullOrEmpty(
                    reason)
                    ? "Varkesh cleanup"
                    : reason);

            DestroyCastVisuals();
            ClearBurialPools();
            ClearGraveSouls();
            RestoreBossOffensiveState();

            _boss =
                null;

            _bossCharacter =
                null;

            _engaged =
                false;

            if (!string.IsNullOrEmpty(
                    reason) &&
                reason.IndexOf(
                    "boss attach",
                    StringComparison.OrdinalIgnoreCase) <
                0)
            {
                Plugin.NativeLog.LogInfo(
                    "[Varkesh] encounter stopped. Reason=[" +
                    reason +
                    "].");
            }
        }


        internal static void NotifyBossDamaged(
            Character target,
            int incomingDamage)
        {
            if (target == null ||
                target != _bossCharacter ||
                incomingDamage <= 0 ||
                !IsBossAlive())
            {
                return;
            }

            if (!_engaged)
            {
                StartCombatClock();
            }
        }


        private static void ResetEncounterState()
        {
            _engaged =
                false;

            _castActive =
                false;

            _phaseTwo =
                false;

            _phaseThree =
                false;

            _finalPhase =
                false;

            _combatStartedRealtime =
                -1f;

            _noTargetSinceRealtime =
                -1f;

            _nextDeathsGraspRealtime =
                float.PositiveInfinity;

            _nextBurialRealtime =
                float.PositiveInfinity;

            _nextGraveMarchRealtime =
                float.PositiveInfinity;

            _nextReapersBrandRealtime =
                float.PositiveInfinity;

            _brandResetRealtime =
                float.PositiveInfinity;

            _burialCastIndex =
                0;

            _graveMarchCastIndex =
                0;

            _graveMarchBuffStacks =
                0;

            _nextBossCombatRepairRealtime =
                0f;

            _nextTankTetherCheckRealtime =
                0f;

            _tankTetherActive =
                false;

            BrandStacks.Clear();
            ClearBurialPools();
            ClearGraveSouls();
            DestroyCastVisuals();
        }


        private static void StartCombatClock()
        {
            if (_engaged)
            {
                return;
            }

            _engaged =
                true;

            _combatStartedRealtime =
                Time.realtimeSinceStartup;

            _nextDeathsGraspRealtime =
                _combatStartedRealtime +
                DeathsGraspInitialDelay;

            _nextBurialRealtime =
                _combatStartedRealtime +
                BurialInitialDelay;

            _nextGraveMarchRealtime =
                _combatStartedRealtime +
                GraveMarchInitialDelay;

            _nextReapersBrandRealtime =
                _combatStartedRealtime +
                ReapersBrandInitialDelay;

            _brandResetRealtime =
                _combatStartedRealtime +
                ReapersBrandResetTime;

            _noTargetSinceRealtime =
                -1f;

            _nextBossCombatRepairRealtime =
                0f;

            _nextTankTetherCheckRealtime =
                0f;

            Plugin.NativeLog.LogInfo(
                "[Varkesh] combat started. Phase2=70% Phase3=40% Final=10%.");
        }


        private static IEnumerator Run(
            int generation)
        {
            while (IsCurrent(
                       generation))
            {
                if (!IsBossAlive())
                {
                    WorldBossSimAvoidanceRuntime.ForceReleaseAll(
                        "Varkesh death");

                    DestroyCastVisuals();
                    ClearBurialPools();
                    ClearGraveSouls();

                    yield break;
                }

                UpdateBurialPools();
                UpdateGraveSouls();

                WorldBossSimAvoidanceRuntime.UpdateMovement();

                if (!_engaged)
                {
                    if ((_boss != null &&
                         _boss.CurrentAggroTarget != null) ||
                        BossHasTakenDamage())
                    {
                        StartCombatClock();
                    }

                    yield return null;
                    continue;
                }

                MaintainBossCombatState(
                    false);

                MaintainTankSimTether(
                    generation);

                if (ShouldLeashReset())
                {
                    ResetBossToHome(
                        "hard leash/no living raid targets");

                    yield break;
                }

                UpdatePhaseState();
                UpdateReapersBrand();

                if (!_castActive)
                {
                    float now =
                        Time.realtimeSinceStartup;

                    if (now >=
                        _nextDeathsGraspRealtime)
                    {
                        yield return CastDeathsGrasp(
                            generation);

                        ScheduleDeathsGrasp();
                    }
                    else if (now >=
                             _nextBurialRealtime)
                    {
                        int castNumber =
                            ++_burialCastIndex;

                        yield return CastMarkedForBurial(
                            generation,
                            castNumber);

                        ScheduleBurial();
                    }
                    else if (now >=
                             _nextGraveMarchRealtime)
                    {
                        int castNumber =
                            ++_graveMarchCastIndex;

                        yield return CastGraveMarch(
                            generation,
                            castNumber);

                        ScheduleGraveMarch();
                    }
                }

                yield return null;
            }
        }


        private static bool BossHasTakenDamage()
        {
            if (_bossCharacter == null ||
                _bossCharacter.MyStats == null)
            {
                return false;
            }

            return
                _bossCharacter.MyStats.CurrentHP <
                _bossCharacter.MyStats.CurrentMaxHP;
        }


        private static void UpdatePhaseState()
        {
            Stats stats =
                _bossCharacter != null
                    ? _bossCharacter.MyStats
                    : null;

            if (stats == null ||
                stats.CurrentMaxHP <= 0)
            {
                return;
            }

            float ratio =
                Mathf.Clamp01(
                    (float)stats.CurrentHP /
                    stats.CurrentMaxHP);

            if (!_phaseTwo &&
                ratio <= PhaseTwoThreshold)
            {
                _phaseTwo =
                    true;

                PullSchedulesCloser(
                    3f);

                Plugin.NativeLog.LogInfo(
                    "[Varkesh] 70% phase active: Deathless Covenant. Burial pressure increases; Grave March remains one priority soul.");
            }

            if (!_phaseThree &&
                ratio <= PhaseThreeThreshold)
            {
                _phaseThree =
                    true;

                PullSchedulesCloser(
                    3f);

                Plugin.NativeLog.LogInfo(
                    "[Varkesh] 40% phase active: Soul Unbound. Destroyed Grave March souls now burst.");
            }

            if (!_finalPhase &&
                ratio <= FinalPhaseThreshold)
            {
                _finalPhase =
                    true;

                ApplyBossOffensiveState();

                Plugin.NativeLog.LogInfo(
                    "[Varkesh] 10% phase active: Refusal to Die.");
            }
        }


        private static void PullSchedulesCloser(
            float seconds)
        {
            float now =
                Time.realtimeSinceStartup;

            _nextDeathsGraspRealtime =
                Mathf.Max(
                    now + 1f,
                    _nextDeathsGraspRealtime -
                    seconds);

            _nextBurialRealtime =
                Mathf.Max(
                    now + 2f,
                    _nextBurialRealtime -
                    seconds);

            _nextGraveMarchRealtime =
                Mathf.Max(
                    now + 3f,
                    _nextGraveMarchRealtime -
                    seconds);
        }


        private static void ScheduleDeathsGrasp()
        {
            _nextDeathsGraspRealtime =
                Time.realtimeSinceStartup +
                (_phaseThree
                    ? DeathsGraspRepeatPhaseThree
                    : (_phaseTwo
                        ? DeathsGraspRepeatPhaseTwo
                        : DeathsGraspRepeatPhaseOne));
        }


        private static void ScheduleBurial()
        {
            _nextBurialRealtime =
                Time.realtimeSinceStartup +
                (_phaseThree
                    ? BurialRepeatPhaseThree
                    : (_phaseTwo
                        ? BurialRepeatPhaseTwo
                        : BurialRepeatPhaseOne));
        }


        private static void ScheduleGraveMarch()
        {
            _nextGraveMarchRealtime =
                Time.realtimeSinceStartup +
                (_phaseThree
                    ? GraveMarchRepeatPhaseThree
                    : (_phaseTwo
                        ? GraveMarchRepeatPhaseTwo
                        : GraveMarchRepeatPhaseOne));
        }


        // ============================================================
        // DEATH'S GRASP -> SOUL RUPTURE
        // ============================================================

        private static IEnumerator CastDeathsGrasp(
            int generation)
        {
            if (!CanBeginCast(
                    generation))
            {
                yield break;
            }

            _castActive =
                true;

            RaidBossCastBarRuntime.Show(
                DeathsGraspName,
                DeathsGraspCastTime);

            Vector3 center =
                _boss.transform.position;

            GameObject visual =
                CreateGroundDisc(
                    center,
                    SoulRuptureRadius,
                    new Color(
                        0.36f,
                        0.02f,
                        0.44f,
                        0.45f),
                    "FOTF_Varkesh_SoulRupture");

            CastVisuals.Add(
                visual);

            Dictionary<SimPlayer, Vector3>
                simDestinations =
                    new Dictionary<SimPlayer, Vector3>();

            List<SimPlayer> sims =
                WorldBossSimAvoidanceRuntime
                    .GetLivingRaidSimsSnapshot();

            int graspEligibleSims =
                0;

            for (int i = 0;
                 i < sims.Count;
                 i++)
            {
                SimPlayer sim =
                    sims[i];

                if (sim == null)
                {
                    continue;
                }

                NPC simNpc =
                    sim.GetThisNPC();

                Character simCharacter =
                    simNpc != null
                        ? simNpc.GetChar()
                        : null;

                if (!IsLivingCharacter(
                        simCharacter) ||
                    PlanarDistance(
                        simCharacter.transform.position,
                        center) >
                    DeathsGraspMaxRange)
                {
                    continue;
                }

                graspEligibleSims++;

                Vector3 away =
                    simCharacter.transform.position -
                    center;

                away.y =
                    0f;

                if (away.sqrMagnitude <
                    0.01f)
                {
                    away =
                        Quaternion.Euler(
                            0f,
                            i * 67f,
                            0f) *
                        Vector3.forward;
                }

                away.Normalize();

                simDestinations[sim] =
                    center +
                    away *
                    SoulRuptureEscapeRadius;
            }

            WorldBossSimAvoidanceRuntime.BeginDirectedMovement(
                simDestinations,
                DeathsGraspName);

            Plugin.NativeLog.LogInfo(
                "[Varkesh] Death's Grasp hard range=[" +
                DeathsGraspMaxRange.ToString("0") +
                "m] EligibleSims=[" +
                graspEligibleSims +
                "].");

            float started =
                Time.realtimeSinceStartup;

            float ends =
                started +
                DeathsGraspCastTime;

            while (IsCurrent(
                       generation) &&
                   IsBossAlive() &&
                   Time.realtimeSinceStartup <
                   ends)
            {
                WorldBossSimAvoidanceRuntime.UpdateMovement();

                float t =
                    Mathf.Clamp01(
                        (Time.realtimeSinceStartup -
                         started) /
                        DeathsGraspCastTime);

                float pullSpeed =
                    Mathf.Lerp(
                        DeathsGraspPullStart,
                        DeathsGraspPullEnd,
                        t);

                ApplyGraspPull(
                    center,
                    pullSpeed);

                UpdateBurialPools();
                UpdateGraveSouls();

                yield return null;
            }

            RaidBossCastBarRuntime.Hide();

            if (IsCurrent(
                    generation) &&
                IsBossAlive())
            {
                int hits =
                    0;

                List<Character> living =
                    GetLivingRaidCharacters();

                for (int i = 0;
                     i < living.Count;
                     i++)
                {
                    Character target =
                        living[i];

                    if (PlanarDistance(
                            target.transform.position,
                            center) <=
                        SoulRuptureRadius)
                    {
                        if (ApplyNamedDamage(
                                target,
                                SoulRuptureName,
                                SoulRuptureDamage))
                        {
                            hits++;
                        }
                    }
                }

                Plugin.NativeLog.LogInfo(
                    "[Varkesh] Soul Rupture resolved. Hit=[" +
                    hits +
                    "].");
            }

            WorldBossSimAvoidanceRuntime.ReleaseAssignments(
                "Varkesh Death's Grasp complete");

            DestroyCastVisuals();

            _castActive =
                false;
        }


        private static void ApplyGraspPull(
            Vector3 center,
            float speed)
        {
            List<Character> living =
                GetLivingRaidCharacters();

            float stepDistance =
                speed *
                Mathf.Max(
                    0f,
                    Time.deltaTime);

            for (int i = 0;
                 i < living.Count;
                 i++)
            {
                Character target =
                    living[i];

                if (PlanarDistance(
                        target.transform.position,
                        center) >
                    DeathsGraspMaxRange)
                {
                    continue;
                }

                Vector3 delta =
                    center -
                    target.transform.position;

                delta.y =
                    0f;

                if (delta.sqrMagnitude <
                    0.01f)
                {
                    continue;
                }

                Vector3 step =
                    delta.normalized *
                    stepDistance;

                SimPlayer sim =
                    FindSimForCharacter(
                        target);

                if (sim != null)
                {
                    NavMeshAgent agent =
                        sim.GetComponent<NavMeshAgent>();

                    if (agent != null &&
                        agent.enabled &&
                        agent.isOnNavMesh)
                    {
                        try
                        {
                            agent.Move(
                                step);

                            continue;
                        }
                        catch
                        {
                        }
                    }
                }

                target.transform.position +=
                    step;
            }
        }


        // ============================================================
        // MARKED FOR BURIAL
        // ============================================================

        private static IEnumerator CastMarkedForBurial(
            int generation,
            int castNumber)
        {
            if (!CanBeginCast(
                    generation))
            {
                yield break;
            }

            _castActive =
                true;

            RaidBossCastBarRuntime.Show(
                MarkedForBurialName,
                BurialCastTime);

            List<Character> living =
                GetLivingRaidCharactersWithin(
                    _boss.transform.position,
                    EncounterMechanicParticipantRadius);

            int desiredTargets =
                _phaseThree
                    ? 4
                    : (_phaseTwo
                        ? 3
                        : 2);

            List<Character> marked =
                SelectBurialTargets(
                    living,
                    desiredTargets,
                    castNumber);

            List<GameObject> markers =
                new List<GameObject>();

            Dictionary<SimPlayer, Vector3>
                simDestinations =
                    new Dictionary<SimPlayer, Vector3>();

            for (int i = 0;
                 i < marked.Count;
                 i++)
            {
                Character target =
                    marked[i];

                GameObject marker =
                    CreateGroundDisc(
                        target.transform.position,
                        BurialPoolRadius,
                        new Color(
                            0.46f,
                            0.05f,
                            0.18f,
                            0.42f),
                        "FOTF_Varkesh_BurialMarker");

                markers.Add(
                    marker);

                CastVisuals.Add(
                    marker);

                SimPlayer sim =
                    FindSimForCharacter(
                        target);

                if (sim != null)
                {
                    Vector3 destination =
                        ResolveSeparatedBurialDestination(
                            sim,
                            target,
                            living,
                            simDestinations.Values,
                            castNumber,
                            i);

                    simDestinations[sim] =
                        destination;

                    Plugin.NativeLog.LogInfo(
                        "[Varkesh] Marked for Burial Sim=[" +
                        GetCharacterDisplayName(
                            target) +
                        "] assigned separated drop point [" +
                        destination +
                        "].");
                }
            }

            WorldBossSimAvoidanceRuntime.BeginDirectedMovement(
                simDestinations,
                MarkedForBurialName);

            float ends =
                Time.realtimeSinceStartup +
                BurialCastTime;

            while (IsCurrent(
                       generation) &&
                   IsBossAlive() &&
                   Time.realtimeSinceStartup <
                   ends)
            {
                for (int i = 0;
                     i < marked.Count &&
                     i < markers.Count;
                     i++)
                {
                    Character target =
                        marked[i];

                    GameObject marker =
                        markers[i];

                    if (target == null ||
                        marker == null)
                    {
                        continue;
                    }

                    PositionGroundDisc(
                        marker,
                        target.transform.position,
                        BurialPoolRadius);
                }

                WorldBossSimAvoidanceRuntime.UpdateMovement();
                UpdateBurialPools();
                UpdateGraveSouls();

                yield return null;
            }

            RaidBossCastBarRuntime.Hide();

            float now =
                Time.realtimeSinceStartup;

            for (int i = 0;
                 i < marked.Count;
                 i++)
            {
                Character target =
                    marked[i];

                if (target == null)
                {
                    continue;
                }

                Vector3 position =
                    ProjectToGround(
                        target.transform.position);

                BurialPool pool =
                    new BurialPool
                    {
                        Position =
                            position,

                        ExpiresRealtime =
                            now +
                            BurialPoolDuration,

                        NextPulseRealtime =
                            now +
                            0.35f,

                        Visual =
                            CreateGroundDisc(
                                position,
                                BurialPoolRadius,
                                new Color(
                                    0.30f,
                                    0.00f,
                                    0.12f,
                                    0.52f),
                                "FOTF_Varkesh_BurialPool")
                    };

                ActiveBurialPools.Add(
                    pool);
            }

            WorldBossSimAvoidanceRuntime.ReleaseAssignments(
                "Varkesh Marked for Burial complete");

            DestroyCastVisuals();

            _castActive =
                false;
        }


        private static Vector3 ResolveSeparatedBurialDestination(
            SimPlayer sim,
            Character target,
            List<Character> living,
            ICollection<Vector3> alreadyChosen,
            int castNumber,
            int markedIndex)
        {
            Vector3 center =
                _boss != null
                    ? _boss.transform.position
                    : target.transform.position;

            float baseAngle =
                (castNumber * 53f +
                 markedIndex * 97f) %
                360f;

            Vector3 best =
                ProjectToNavMesh(
                    center +
                    Vector3.forward *
                    BurialMarkerMoveDistance);

            float bestScore =
                float.NegativeInfinity;

            for (int c = 0;
                 c < BurialSimCandidateDirections;
                 c++)
            {
                float angle =
                    baseAngle +
                    c *
                    (360f /
                     BurialSimCandidateDirections);

                Vector3 direction =
                    Quaternion.Euler(
                        0f,
                        angle,
                        0f) *
                    Vector3.forward;

                float ringDistance =
                    BurialMarkerMoveDistance;

                Vector3 candidate =
                    best;

                // If a candidate is still too close to another Sim/selected
                // drop point, walk farther outward along the same ray until it
                // clears or reaches the configured maximum ring.
                for (;;)
                {
                    candidate =
                        ProjectToNavMesh(
                            center +
                            direction *
                            ringDistance);

                    float separation =
                        GetBurialDestinationSeparation(
                            candidate,
                            target,
                            living,
                            alreadyChosen);

                    if (separation >=
                            BurialSimMinimumSeparation ||
                        ringDistance >=
                            BurialSimMaximumRingDistance)
                    {
                        break;
                    }

                    ringDistance =
                        Mathf.Min(
                            BurialSimMaximumRingDistance,
                            ringDistance +
                            2f);
                }

                float minimumSeparation =
                    GetBurialDestinationSeparation(
                        candidate,
                        target,
                        living,
                        alreadyChosen);

                float travelDistance =
                    PlanarDistance(
                        sim.transform.position,
                        candidate);

                // Separation is overwhelmingly more important than travel
                // distance. Travel is only a tie-breaker between similarly
                // clear destinations.
                float score =
                    minimumSeparation *
                    10f -
                    travelDistance *
                    0.15f;

                if (score >
                    bestScore)
                {
                    bestScore =
                        score;

                    best =
                        candidate;
                }
            }

            return best;
        }


        private static float GetBurialDestinationSeparation(
            Vector3 candidate,
            Character movingTarget,
            List<Character> living,
            ICollection<Vector3> alreadyChosen)
        {
            float minimum =
                float.PositiveInfinity;

            if (living != null)
            {
                for (int i = 0;
                     i < living.Count;
                     i++)
                {
                    Character other =
                        living[i];

                    if (other == null ||
                        other ==
                        movingTarget)
                    {
                        continue;
                    }

                    float distance =
                        PlanarDistance(
                            candidate,
                            other.transform.position);

                    if (distance <
                        minimum)
                    {
                        minimum =
                            distance;
                    }
                }
            }

            if (alreadyChosen != null)
            {
                foreach (Vector3 chosen
                         in alreadyChosen)
                {
                    float distance =
                        PlanarDistance(
                            candidate,
                            chosen);

                    if (distance <
                        minimum)
                    {
                        minimum =
                            distance;
                    }
                }
            }

            // Also keep newly dropped circles away from still-active older
            // Burial pools whenever terrain allows it.
            for (int i = 0;
                 i < ActiveBurialPools.Count;
                 i++)
            {
                BurialPool pool =
                    ActiveBurialPools[i];

                if (pool == null)
                {
                    continue;
                }

                float distance =
                    PlanarDistance(
                        candidate,
                        pool.Position);

                if (distance <
                    minimum)
                {
                    minimum =
                        distance;
                }
            }

            if (float.IsPositiveInfinity(
                    minimum))
            {
                minimum =
                    BurialSimMaximumRingDistance;
            }

            return minimum;
        }


        private static string GetCharacterDisplayName(
            Character character)
        {
            if (character == null)
            {
                return "<null>";
            }

            if (character.MyStats != null &&
                !string.IsNullOrEmpty(
                    character.MyStats.MyName))
            {
                return
                    character.MyStats.MyName;
            }

            if (character.MyNPC != null &&
                !string.IsNullOrEmpty(
                    character.MyNPC.NPCName))
            {
                return
                    character.MyNPC.NPCName;
            }

            return
                character.name;
        }


        private static List<Character> SelectBurialTargets(
            List<Character> living,
            int desiredCount,
            int castNumber)
        {
            List<Character> result =
                new List<Character>();

            if (living == null ||
                living.Count == 0 ||
                desiredCount <= 0)
            {
                return result;
            }

            Character currentTank =
                _boss != null
                    ? _boss.CurrentAggroTarget
                    : null;

            List<Character> preferred =
                new List<Character>();

            List<Character> fallback =
                new List<Character>();

            for (int i = 0;
                 i < living.Count;
                 i++)
            {
                Character candidate =
                    living[i];

                if (candidate == null)
                {
                    continue;
                }

                if (candidate ==
                    currentTank)
                {
                    fallback.Add(
                        candidate);
                }
                else
                {
                    preferred.Add(
                        candidate);
                }
            }

            int start =
                preferred.Count > 0
                    ? Math.Abs(
                        castNumber *
                        37) %
                      preferred.Count
                    : 0;

            for (int offset = 0;
                 offset < preferred.Count &&
                 result.Count < desiredCount;
                 offset++)
            {
                Character candidate =
                    preferred[
                        (start + offset) %
                        preferred.Count];

                if (!result.Contains(
                        candidate))
                {
                    result.Add(
                        candidate);
                }
            }

            for (int i = 0;
                 i < fallback.Count &&
                 result.Count < desiredCount;
                 i++)
            {
                if (!result.Contains(
                        fallback[i]))
                {
                    result.Add(
                        fallback[i]);
                }
            }

            return result;
        }


        private static void UpdateBurialPools()
        {
            if (ActiveBurialPools.Count ==
                0)
            {
                return;
            }

            float now =
                Time.realtimeSinceStartup;

            for (int i =
                     ActiveBurialPools.Count -
                     1;
                 i >= 0;
                 i--)
            {
                BurialPool pool =
                    ActiveBurialPools[i];

                if (pool == null ||
                    now >=
                    pool.ExpiresRealtime)
                {
                    if (pool != null)
                    {
                        DestroyObject(
                            pool.Visual);
                    }

                    ActiveBurialPools.RemoveAt(
                        i);

                    continue;
                }

                if (now <
                    pool.NextPulseRealtime)
                {
                    continue;
                }

                pool.NextPulseRealtime =
                    now +
                    BurialPulseInterval;

                List<Character> living =
                    GetLivingRaidCharacters();

                for (int c = 0;
                     c < living.Count;
                     c++)
                {
                    Character target =
                        living[c];

                    if (PlanarDistance(
                            target.transform.position,
                            pool.Position) <=
                        BurialPoolRadius)
                    {
                        ApplyNamedDamage(
                            target,
                            MarkedForBurialName,
                            BurialPulseDamage);
                    }
                }
            }
        }


        // ============================================================
        // GRAVE MARCH
        // ============================================================

        private static IEnumerator CastGraveMarch(
            int generation,
            int castNumber)
        {
            if (!CanBeginCast(
                    generation))
            {
                yield break;
            }

            _castActive =
                true;

            RaidBossCastBarRuntime.Show(
                GraveMarchName,
                GraveMarchCastTime);

            float ends =
                Time.realtimeSinceStartup +
                GraveMarchCastTime;

            while (IsCurrent(
                       generation) &&
                   IsBossAlive() &&
                   Time.realtimeSinceStartup <
                   ends)
            {
                UpdateBurialPools();
                UpdateGraveSouls();
                yield return null;
            }

            RaidBossCastBarRuntime.Hide();

            if (IsCurrent(
                    generation) &&
                IsBossAlive())
            {
                // Grave March is intentionally one high-priority add in
                // every phase. Multiple Waking Nightmare-sized adds were too
                // visually noisy and their combined boss healing made the
                // encounter effectively unwinnable before players could
                // identify/intercept them.
                const int soulCount =
                    1;

                yield return SpawnGraveMarchSouls(
                    soulCount,
                    castNumber,
                    generation);
            }

            _castActive =
                false;
        }


        private static IEnumerator SpawnGraveMarchSouls(
            int count,
            int castNumber,
            int generation)
        {
            if (_boss == null ||
                count <= 0)
            {
                yield break;
            }

            float baseAngle =
                (castNumber *
                 47f) %
                360f;

            List<PendingGraveSoulSpawn> pending =
                new List<PendingGraveSoulSpawn>();

            int directSpawned =
                0;

            UnityEngine.SceneManagement.Scene scene =
                UnityEngine.SceneManagement
                    .SceneManager
                    .GetActiveScene();

            for (int i = 0;
                 i < count;
                 i++)
            {
                float angle =
                    baseAngle +
                    i *
                    (360f /
                     count);

                Vector3 direction =
                    Quaternion.Euler(
                        0f,
                        angle,
                        0f) *
                    Vector3.forward;

                Vector3 desired =
                    _boss.transform.position +
                    direction *
                    GraveSoulSpawnRadius;

                Vector3 spawnPosition =
                    ProjectToNavMesh(
                        desired);

                GameObject controller =
                    VarkeshWorldSpawnRuntime
                        .CreateGraveMarchSpawnPointClone();

                SpawnPoint runtimeSpawnPoint =
                    controller != null
                        ? controller.GetComponent<SpawnPoint>()
                        : null;

                if (controller != null &&
                    runtimeSpawnPoint != null)
                {
                    try
                    {
                        controller.name =
                            "FOTF_Varkesh_GraveMarch_SpawnPoint_" +
                            i;

                        controller.transform.SetParent(
                            null,
                            true);

                        controller.transform.SetPositionAndRotation(
                            spawnPosition,
                            Quaternion.identity);

                        if (scene.IsValid() &&
                            scene.isLoaded)
                        {
                            UnityEngine.SceneManagement
                                .SceneManager
                                .MoveGameObjectToScene(
                                    controller,
                                    scene);
                        }

                        controller.SetActive(
                            true);

                        pending.Add(
                            new PendingGraveSoulSpawn
                            {
                                ControllerRoot =
                                    controller,

                                SpawnPoint =
                                    runtimeSpawnPoint,

                                Position =
                                    spawnPosition,

                                Ordinal =
                                    i
                            });

                        continue;
                    }
                    catch (Exception exception)
                    {
                        Plugin.NativeLog.LogWarning(
                            "[Varkesh] Grave March native SpawnPoint activation failed [" +
                            i +
                            "]: " +
                            exception.Message);

                        DestroyObject(
                            controller);
                    }
                }
                else if (controller != null)
                {
                    DestroyObject(
                        controller);
                }

                // Fallback only when no native SpawnPoint template is
                // available. This keeps older resource layouts functional.
                GameObject direct =
                    VarkeshWorldSpawnRuntime
                        .CreateGraveMarchAddClone();

                GraveSoul directSoul =
                    CreateDirectGraveSoul(
                        direct,
                        spawnPosition,
                        i);

                if (directSoul != null)
                {
                    ActiveGraveSouls.Add(
                        directSoul);

                    directSpawned++;
                }
            }

            const int spawnWaitFrames =
                180;

            int resolvedFromSpawnPoints =
                0;

            for (int frame = 0;
                 frame < spawnWaitFrames &&
                 pending.Count > 0;
                 frame++)
            {
                if (!IsCurrent(
                        generation) ||
                    !IsBossAlive())
                {
                    for (int i = 0;
                         i < pending.Count;
                         i++)
                    {
                        DestroyObject(
                            pending[i].ControllerRoot);
                    }

                    yield break;
                }

                for (int i =
                         pending.Count -
                         1;
                     i >= 0;
                     i--)
                {
                    PendingGraveSoulSpawn item =
                        pending[i];

                    if (item == null ||
                        item.ControllerRoot == null ||
                        item.SpawnPoint == null)
                    {
                        if (item != null)
                        {
                            DestroyObject(
                                item.ControllerRoot);
                        }

                        pending.RemoveAt(
                            i);

                        continue;
                    }

                    NPC spawned =
                        item.SpawnPoint.SpawnedNPC;

                    if (spawned == null)
                    {
                        continue;
                    }

                    if (item.NpcSeenFrame <
                        0)
                    {
                        item.NpcSeenFrame =
                            frame;

                        continue;
                    }

                    // Match the proven native SpawnPoint spawning pattern used
                    // elsewhere in FOTF: let Awake/OnEnable/Start settle for
                    // two additional frames after SpawnedNPC becomes available.
                    if (frame -
                        item.NpcSeenFrame <
                        2)
                    {
                        continue;
                    }

                    GraveSoul soul =
                        ConfigureGraveSoulFromNpc(
                            spawned,
                            item.ControllerRoot,
                            item.Position,
                            item.Ordinal);

                    if (soul != null)
                    {
                        ActiveGraveSouls.Add(
                            soul);

                        resolvedFromSpawnPoints++;

                        // We only needed the native SpawnPoint to construct the
                        // Waking Nightmare. Disable its lifecycle so it cannot
                        // independently respawn another add.
                        item.SpawnPoint.enabled =
                            false;
                    }
                    else
                    {
                        DestroyObject(
                            item.ControllerRoot);
                    }

                    pending.RemoveAt(
                        i);
                }

                yield return null;
            }

            // A SpawnPoint that never produced SpawnedNPC should not make the
            // mechanic silently do nothing. Clean it up and attempt the direct
            // NPC fallback for that slot.
            for (int i = 0;
                 i < pending.Count;
                 i++)
            {
                PendingGraveSoulSpawn item =
                    pending[i];

                if (item == null)
                {
                    continue;
                }

                Plugin.NativeLog.LogWarning(
                    "[Varkesh] Grave March timed out waiting for native [" +
                    VarkeshWorldSpawnRuntime.GraveMarchTemplateNpcName +
                    "] SpawnPoint [" +
                    item.Ordinal +
                    "]. Trying direct NPC fallback.");

                DestroyObject(
                    item.ControllerRoot);

                GameObject direct =
                    VarkeshWorldSpawnRuntime
                        .CreateGraveMarchAddClone();

                GraveSoul directSoul =
                    CreateDirectGraveSoul(
                        direct,
                        item.Position,
                        item.Ordinal);

                if (directSoul != null)
                {
                    ActiveGraveSouls.Add(
                        directSoul);

                    directSpawned++;
                }
            }

            pending.Clear();

            Plugin.NativeLog.LogInfo(
                "[Varkesh] Grave March requested [" +
                count +
                "] Deathbound Souls using native [" +
                VarkeshWorldSpawnRuntime.GraveMarchTemplateNpcName +
                "]. SpawnPointResolved=[" +
                resolvedFromSpawnPoints +
                "] DirectFallback=[" +
                directSpawned +
                "] ActiveSouls=[" +
                ActiveGraveSouls.Count +
                "].");
        }


        private static GraveSoul CreateDirectGraveSoul(
            GameObject clone,
            Vector3 position,
            int ordinal)
        {
            if (clone == null)
            {
                return null;
            }

            try
            {
                UnityEngine.SceneManagement.Scene scene =
                    UnityEngine.SceneManagement
                        .SceneManager
                        .GetActiveScene();

                clone.name =
                    "Deathbound Soul";

                clone.transform.SetParent(
                    null,
                    true);

                clone.transform.position =
                    position;

                if (scene.IsValid() &&
                    scene.isLoaded)
                {
                    UnityEngine.SceneManagement
                        .SceneManager
                        .MoveGameObjectToScene(
                            clone,
                            scene);
                }

                clone.SetActive(
                    true);

                NPC npc =
                    clone.GetComponent<NPC>();

                if (npc == null)
                {
                    npc =
                        clone.GetComponentInChildren<NPC>(
                            true);
                }

                GraveSoul soul =
                    ConfigureGraveSoulFromNpc(
                        npc,
                        null,
                        position,
                        ordinal);

                if (soul == null)
                {
                    DestroyObject(
                        clone);
                }

                return soul;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Varkesh] direct Waking Nightmare fallback failed [" +
                    ordinal +
                    "]: " +
                    exception.Message);

                DestroyObject(
                    clone);

                return null;
            }
        }


        private static GraveSoul ConfigureGraveSoulFromNpc(
            NPC npc,
            GameObject controllerRoot,
            Vector3 position,
            int ordinal)
        {
            if (npc == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[Varkesh] Grave March produced no NPC component [" +
                    ordinal +
                    "].");

                return null;
            }

            GameObject root =
                npc.gameObject;

            if (root == null)
            {
                return null;
            }

            string stage =
                "begin";

            try
            {
                stage =
                    "identity";

                root.name =
                    "Deathbound Soul";

                root.transform.position =
                    position;

                stage =
                    "character resolve";

                Character character =
                    npc.GetChar();

                if (character == null)
                {
                    character =
                        root.GetComponent<Character>();
                }

                if (character == null)
                {
                    character =
                        root.GetComponentInChildren<Character>(
                            true);
                }

                stage =
                    "stats resolve";

                Stats stats =
                    character != null
                        ? character.MyStats
                        : null;

                if (stats == null)
                {
                    stats =
                        root.GetComponent<Stats>();
                }

                if (stats == null)
                {
                    stats =
                        root.GetComponentInChildren<Stats>(
                            true);
                }

                if (character == null ||
                    stats == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "[Varkesh] Waking Nightmare spawned but did not expose Character/Stats [" +
                        ordinal +
                        "]. Character=[" +
                        (character != null) +
                        "] Stats=[" +
                        (stats != null) +
                        "].");

                    return null;
                }

                // Reassert the native component cross-links before changing
                // anything. Direct/native SpawnPoint construction can expose
                // the components before every convenience reference has settled.
                stage =
                    "component links";

                if (character.MyStats == null)
                {
                    character.MyStats =
                        stats;
                }

                if (character.MyNPC == null)
                {
                    character.MyNPC =
                        npc;
                }

                if (stats.Myself == null)
                {
                    stats.Myself =
                        character;
                }

                stage =
                    "npc configuration";

                npc.NPCName =
                    "Deathbound Soul";

                npc.SimPlayer =
                    false;

                npc.GroupEncounter =
                    false;

                npc.noClick =
                    false;

                // Match the proven native hostile click/target layer used by
                // other FOTF encounter NPCs.
                root.layer =
                    9;

                CapsuleCollider capsule =
                    root.GetComponent<CapsuleCollider>();

                if (capsule != null)
                {
                    capsule.enabled =
                        true;
                }

                npc.retreat =
                    false;

                npc.CurrentAggroTarget =
                    null;

                npc.BaseAtkDmg =
                    0;

                npc.DamageRange =
                    Vector2.zero;

                npc.DamageMult =
                    0f;

                stage =
                    "stats configuration";

                stats.MyName =
                    "Deathbound Soul";

                stats.Level =
                    WorldBossCatalog
                        .Varkesh
                        .Level;

                stats.BaseHP =
                    GraveSoulHP;

                stats.OverrideHPforNPC =
                    true;

                // IMPORTANT:
                // Do not call Stats.CalcStats() here.
                //
                // Erenshor's native CalcStats() immediately dereferences
                // Stats.Myself.MySkills. Waking Nightmare is being repurposed
                // as a mechanic-owned pathing add and does not need a complete
                // player-style stat rebuild. Calling CalcStats here is the
                // source of the Grave March NullReferenceException seen after
                // the native SpawnPoint successfully created each add.
                //
                // CurrentMaxHP and CurrentHP are public native fields, so set
                // the encounter HP pool directly and leave the already-settled
                // native skill/inventory state untouched.
                stats.CurrentMaxHP =
                    GraveSoulHP;

                stats.CurrentHP =
                    GraveSoulHP;

                character.Alive =
                    true;

                stage =
                    "nameplate";

                RefreshGraveSoulNameplate(
                    npc);

                stage =
                    "loot removal";

                RemoveSoulLoot(
                    character,
                    root);

                stage =
                    "navmesh resolve";

                NavMeshAgent agent =
                    root.GetComponent<NavMeshAgent>();

                if (agent == null)
                {
                    agent =
                        root.GetComponentInChildren<NavMeshAgent>(
                            true);
                }

                if (agent != null &&
                    agent.enabled)
                {
                    agent.speed =
                        GraveSoulMarchSpeed;

                    Vector3 navPosition =
                        ProjectToNavMesh(
                            position);

                    try
                    {
                        if (agent.isOnNavMesh)
                        {
                            agent.Warp(
                                navPosition);

                            agent.isStopped =
                                false;

                            if (_boss != null)
                            {
                                agent.SetDestination(
                                    _boss.transform.position);
                            }
                        }
                        else
                        {
                            root.transform.position =
                                navPosition;
                        }
                    }
                    catch (Exception navException)
                    {
                        root.transform.position =
                            navPosition;

                        Plugin.NativeLog.LogWarning(
                            "[Varkesh] Deathbound Soul [" +
                            ordinal +
                            "] NavMesh setup fell back to transform movement: " +
                            navException.Message);
                    }
                }

                stage =
                    "priority visual";

                GameObject priorityVisual =
                    CreateGraveSoulPriorityVisual(
                        root);

                stage =
                    "raid engagement";

                EngageGraveSoulWithRaid(
                    npc,
                    character,
                    ordinal);

                stage =
                    "native ai suppression";

                // These are mechanic-owned pathing adds rather than normal
                // combatants. AggroOn has already placed the soul into native
                // combat state so raid/group target commands recognize it.
                // Disable NPC.Update only after that engagement is established;
                // Character/renderers/NavMeshAgent remain active/targetable and
                // UpdateGraveSouls owns movement toward Varkesh.
                npc.enabled =
                    false;

                Plugin.NativeLog.LogInfo(
                    "[Varkesh] Deathbound Soul [" +
                    ordinal +
                    "] created from native [" +
                    VarkeshWorldSpawnRuntime.GraveMarchTemplateNpcName +
                    "] at [" +
                    root.transform.position +
                    "] HP=[" +
                    stats.CurrentHP +
                    "/" +
                    stats.CurrentMaxHP +
                    "].");

                return
                    new GraveSoul
                    {
                        Root =
                            root,

                        ControllerRoot =
                            controllerRoot,

                        Npc =
                            npc,

                        Character =
                            character,

                        Agent =
                            agent,

                        PriorityVisual =
                            priorityVisual,

                        Resolved =
                            false
                    };
            }
            catch (Exception exception)
            {
                // Include the complete exception + stage so any future native
                // prefab-specific issue is immediately actionable.
                Plugin.NativeLog.LogWarning(
                    "[Varkesh] failed configuring Deathbound Soul [" +
                    ordinal +
                    "] at stage [" +
                    stage +
                    "]: " +
                    exception);

                return null;
            }
        }


        private static void RefreshGraveSoulNameplate(
            NPC npc)
        {
            if (npc == null ||
                npc.NamePlate == null)
            {
                return;
            }

            try
            {
                npc.NamePlate.gameObject.SetActive(
                    true);

                // Slightly enlarge the native overhead label. This does not
                // replace Erenshor's nameplate system; it only makes this
                // short-lived priority add easier to pick out.
                npc.NamePlate.localScale *=
                    1.25f;

                TMP_Text[] labels =
                    npc.NamePlate.GetComponentsInChildren<TMP_Text>(
                        true);

                for (int i = 0;
                     labels != null &&
                     i < labels.Length;
                     i++)
                {
                    TMP_Text label =
                        labels[i];

                    if (label == null)
                    {
                        continue;
                    }

                    string current =
                        label.text ??
                        "";

                    string objectName =
                        label.gameObject.name ??
                        "";

                    bool nativeName =
                        current.IndexOf(
                            VarkeshWorldSpawnRuntime
                                .GraveMarchTemplateNpcName,
                            StringComparison.OrdinalIgnoreCase) >=
                        0;

                    bool nameField =
                        objectName.IndexOf(
                            "name",
                            StringComparison.OrdinalIgnoreCase) >=
                        0;

                    if (nativeName ||
                        (nameField &&
                         current.IndexOf(
                             "Level",
                             StringComparison.OrdinalIgnoreCase) <
                         0))
                    {
                        label.text =
                            "Deathbound Soul";
                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Varkesh] Deathbound Soul nameplate refresh failed: " +
                    exception.Message);
            }
        }


        private static GameObject CreateGraveSoulPriorityVisual(
            GameObject soulRoot)
        {
            if (soulRoot == null)
            {
                return null;
            }

            try
            {
                GameObject visualRoot =
                    new GameObject(
                        "FOTF_DeathboundSoul_PriorityMarker");

                visualRoot.transform.SetParent(
                    soulRoot.transform,
                    false);

                visualRoot.transform.localPosition =
                    Vector3.zero;

                visualRoot.transform.localRotation =
                    Quaternion.identity;

                visualRoot.transform.localScale =
                    Vector3.one;

                Material markerMaterial =
                    CreateTelegraphMaterial(
                        new Color(
                            0.72f,
                            0.08f,
                            0.95f,
                            0.68f));

                // Tall narrow beacon. It remains visible even when the native
                // Waking Nightmare body is hidden behind Varkesh's 7m model.
                GameObject beam =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cylinder);

                beam.name =
                    "FOTF_DeathboundSoul_Beacon";

                beam.transform.SetParent(
                    visualRoot.transform,
                    false);

                beam.transform.localPosition =
                    new Vector3(
                        0f,
                        GraveSoulBeaconHeight *
                        0.5f,
                        0f);

                beam.transform.localRotation =
                    Quaternion.identity;

                beam.transform.localScale =
                    new Vector3(
                        0.16f,
                        GraveSoulBeaconHeight *
                        0.5f,
                        0.16f);

                Collider beamCollider =
                    beam.GetComponent<Collider>();

                if (beamCollider != null)
                {
                    UnityEngine.Object.Destroy(
                        beamCollider);
                }

                Renderer beamRenderer =
                    beam.GetComponent<Renderer>();

                if (beamRenderer != null)
                {
                    beamRenderer.material =
                        markerMaterial;
                }

                // Large overhead orb provides a clear raid-marker-style focus
                // point without obscuring the model itself.
                GameObject orb =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Sphere);

                orb.name =
                    "FOTF_DeathboundSoul_Orb";

                orb.transform.SetParent(
                    visualRoot.transform,
                    false);

                orb.transform.localPosition =
                    new Vector3(
                        0f,
                        GraveSoulBeaconHeight +
                        0.45f,
                        0f);

                orb.transform.localScale =
                    Vector3.one *
                    0.72f;

                Collider orbCollider =
                    orb.GetComponent<Collider>();

                if (orbCollider != null)
                {
                    UnityEngine.Object.Destroy(
                        orbCollider);
                }

                Renderer orbRenderer =
                    orb.GetComponent<Renderer>();

                if (orbRenderer != null)
                {
                    orbRenderer.material =
                        markerMaterial;
                }

                return visualRoot;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Varkesh] failed creating Deathbound Soul priority marker: " +
                    exception.Message);

                return null;
            }
        }


        private static void EngageGraveSoulWithRaid(
            NPC npc,
            Character character,
            int ordinal)
        {
            if (npc == null ||
                character == null)
            {
                return;
            }

            Character target =
                _boss != null
                    ? _boss.CurrentAggroTarget
                    : null;

            if (!IsLivingCharacter(
                    target))
            {
                PlayerControl player =
                    GameData.PlayerControl;

                target =
                    player != null
                        ? player.Myself
                        : null;
            }

            if (!IsLivingCharacter(
                    target))
            {
                target =
                    FindNearestLivingRaidTarget(
                        character.transform.position);
            }

            if (!IsLivingCharacter(
                    target))
            {
                Plugin.NativeLog.LogWarning(
                    "[Varkesh] Deathbound Soul [" +
                    ordinal +
                    "] spawned with no living raid member available for combat engagement.");

                return;
            }

            try
            {
                // Use Erenshor's real hostile-engagement entry point first.
                // This establishes native combat/threat state, which is what
                // raid Sim logic checks before treating an add as engaged.
                npc.AggroOn(
                    target);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Varkesh] Deathbound Soul [" +
                    ordinal +
                    "] native AggroOn failed; applying target fallback: " +
                    exception.Message);
            }

            // Keep the explicit target even if the source prefab's native
            // AggroOn rejects/rewrites it. IsPullTargetAggrodOnRaid() checks
            // this exact CurrentAggroTarget relationship.
            npc.CurrentAggroTarget =
                target;

            Plugin.NativeLog.LogInfo(
                "[Varkesh] Deathbound Soul [" +
                ordinal +
                "] auto-engaged raid target [" +
                GetCharacterDisplayName(
                    target) +
                "].");
        }


        private static void MaintainGraveSoulCombatLink(
            GraveSoul soul)
        {
            if (soul == null ||
                soul.Npc == null ||
                soul.Character == null)
            {
                return;
            }

            Character target =
                soul.Npc.CurrentAggroTarget;

            if (IsLivingCharacter(
                    target))
            {
                return;
            }

            target =
                _boss != null
                    ? _boss.CurrentAggroTarget
                    : null;

            if (!IsLivingCharacter(
                    target))
            {
                target =
                    FindNearestLivingRaidTarget(
                        soul.Root != null
                            ? soul.Root.transform.position
                            : _homePosition);
            }

            if (IsLivingCharacter(
                    target))
            {
                soul.Npc.CurrentAggroTarget =
                    target;
            }
        }


        private static void RemoveSoulLoot(
            Character character,
            GameObject root)
        {
            if (root == null)
            {
                return;
            }

            try
            {
                LootTable[] tables =
                    root.GetComponentsInChildren<LootTable>(
                        true);

                for (int i = 0;
                     tables != null &&
                     i < tables.Length;
                     i++)
                {
                    LootTable table =
                        tables[i];

                    if (table == null)
                    {
                        continue;
                    }

                    table.enabled =
                        false;

                    if (table.ActualDrops != null)
                    {
                        table.ActualDrops.Clear();
                    }

                    if (table.ActualDropsQual != null)
                    {
                        table.ActualDropsQual.Clear();
                    }

                    table.MaxNumberDrops =
                        0;

                    table.MaxNonCommonDrops =
                        0;

                    table.NumberOfGuaranteedDrops =
                        0;

                    table.MaxGold =
                        0;

                    table.MinGold =
                        0;

                    table.MyGold =
                        0;
                }

                if (character != null)
                {
                    FieldInfo lootField =
                        AccessTools.Field(
                            typeof(Character),
                            "MyLootTable");

                    if (lootField != null)
                    {
                        lootField.SetValue(
                            character,
                            null);
                    }
                }
            }
            catch
            {
            }
        }


        private static void UpdateGraveSouls()
        {
            if (ActiveGraveSouls.Count ==
                0)
            {
                return;
            }

            for (int i =
                     ActiveGraveSouls.Count -
                     1;
                 i >= 0;
                 i--)
            {
                GraveSoul soul =
                    ActiveGraveSouls[i];

                if (soul == null ||
                    soul.Root == null ||
                    soul.Character == null)
                {
                    if (soul != null)
                    {
                        DestroyGraveSoulObjects(
                            soul);
                    }

                    ActiveGraveSouls.RemoveAt(
                        i);

                    continue;
                }

                bool dead =
                    !soul.Character.Alive ||
                    soul.Character.MyStats == null ||
                    soul.Character.MyStats.CurrentHP <=
                    0;

                if (dead)
                {
                    ResolveDestroyedSoul(
                        soul);

                    ActiveGraveSouls.RemoveAt(
                        i);

                    continue;
                }

                if (_boss == null ||
                    !IsBossAlive())
                {
                    DestroyGraveSoulObjects(
                        soul);

                    ActiveGraveSouls.RemoveAt(
                        i);

                    continue;
                }

                MaintainGraveSoulCombatLink(
                    soul);

                float distance =
                    PlanarDistance(
                        soul.Root.transform.position,
                        _boss.transform.position);

                if (distance <=
                    GraveSoulConsumeRadius)
                {
                    ConsumeSoul(
                        soul);

                    ActiveGraveSouls.RemoveAt(
                        i);

                    continue;
                }

                if (soul.Agent != null &&
                    soul.Agent.enabled &&
                    soul.Agent.isOnNavMesh)
                {
                    try
                    {
                        soul.Agent.speed =
                            GraveSoulMarchSpeed;

                        soul.Agent.isStopped =
                            false;

                        soul.Agent.SetDestination(
                            _boss.transform.position);
                    }
                    catch
                    {
                    }
                }
                else
                {
                    Vector3 position =
                        soul.Root.transform.position;

                    Vector3 delta =
                        _boss.transform.position -
                        position;

                    delta.y =
                        0f;

                    if (delta.sqrMagnitude >
                        0.001f)
                    {
                        soul.Root.transform.position +=
                            delta.normalized *
                            (GraveSoulMarchSpeed *
                             Mathf.Max(
                                 Time.deltaTime,
                                 0f));
                    }
                }
            }
        }


        private static void ResolveDestroyedSoul(
            GraveSoul soul)
        {
            if (soul == null ||
                soul.Resolved)
            {
                return;
            }

            soul.Resolved =
                true;

            Vector3 position =
                soul.Root != null
                    ? soul.Root.transform.position
                    : Vector3.zero;

            if (_phaseThree &&
                _boss != null)
            {
                GameObject burst =
                    CreateGroundDisc(
                        position,
                        GraveSoulBurstRadius,
                        new Color(
                            0.58f,
                            0.06f,
                            0.72f,
                            0.50f),
                        "FOTF_Varkesh_DeathboundSoulBurst");

                CoroutineHost.Start(
                    DestroyAfterSeconds(
                        burst,
                        0.65f));

                List<Character> living =
                    GetLivingRaidCharacters();

                for (int i = 0;
                     i < living.Count;
                     i++)
                {
                    if (PlanarDistance(
                            living[i].transform.position,
                            position) <=
                        GraveSoulBurstRadius)
                    {
                        ApplyNamedDamage(
                            living[i],
                            SoulBurstName,
                            GraveSoulBurstDamage);
                    }
                }
            }

            DestroyGraveSoulObjects(
                soul);
        }


        private static void ConsumeSoul(
            GraveSoul soul)
        {
            if (soul == null ||
                soul.Resolved)
            {
                return;
            }

            soul.Resolved =
                true;

            Stats bossStats =
                _bossCharacter != null
                    ? _bossCharacter.MyStats
                    : null;

            if (bossStats != null &&
                bossStats.CurrentHP >
                0)
            {
                bossStats.CurrentHP =
                    Mathf.Min(
                        bossStats.CurrentMaxHP,
                        bossStats.CurrentHP +
                        GraveSoulHeal);
            }

            _graveMarchBuffStacks =
                Mathf.Min(
                    GraveSoulDamageBuffMaxStacks,
                    _graveMarchBuffStacks +
                    1);

            ApplyBossOffensiveState();

            Plugin.NativeLog.LogInfo(
                "[Varkesh] consumed a Deathbound Soul. Heal=[" +
                GraveSoulHeal +
                "] DamageBuffStacks=[" +
                _graveMarchBuffStacks +
                "].");

            DestroyGraveSoulObjects(
                soul);
        }


        private static void DestroyGraveSoulObjects(
            GraveSoul soul)
        {
            if (soul == null)
            {
                return;
            }

            GameObject root =
                soul.Root;

            GameObject controller =
                soul.ControllerRoot;

            GameObject priorityVisual =
                soul.PriorityVisual;

            if (priorityVisual != null &&
                (root == null ||
                 priorityVisual.transform.parent == null))
            {
                DestroyObject(
                    priorityVisual);
            }

            if (root != null)
            {
                DestroyObject(
                    root);
            }

            if (controller != null &&
                controller != root)
            {
                DestroyObject(
                    controller);
            }

            soul.Root =
                null;

            soul.ControllerRoot =
                null;

            soul.PriorityVisual =
                null;
        }


        // ============================================================
        // REAPER'S BRAND
        // ============================================================

        private static void UpdateReapersBrand()
        {
            float now =
                Time.realtimeSinceStartup;

            if (now >=
                _brandResetRealtime)
            {
                BrandStacks.Clear();

                _brandResetRealtime =
                    now +
                    ReapersBrandResetTime;
            }

            if (now <
                _nextReapersBrandRealtime)
            {
                return;
            }

            _nextReapersBrandRealtime =
                now +
                ReapersBrandRepeat;

            Character target =
                _boss != null
                    ? _boss.CurrentAggroTarget
                    : null;

            if (!IsLivingCharacter(
                    target))
            {
                return;
            }

            int id =
                target.GetInstanceID();

            int stacks =
                0;

            BrandStacks.TryGetValue(
                id,
                out stacks);

            stacks =
                Mathf.Clamp(
                    stacks + 1,
                    1,
                    ReapersBrandMaxStacks);

            BrandStacks[id] =
                stacks;

            int damage =
                ReapersBrandBaseDamage +
                (stacks -
                 1) *
                ReapersBrandPerStackDamage;

            ApplyNamedDamage(
                target,
                ReapersBrandName,
                damage);
        }


        // ============================================================
        // FINAL PHASE / BOSS OFFENSE
        // ============================================================

        private static void ApplyBossOffensiveState()
        {
            if (_boss == null)
            {
                return;
            }

            float soulMultiplier =
                1f +
                _graveMarchBuffStacks *
                GraveSoulDamageBuffPerStack;

            float finalMultiplier =
                _finalPhase
                    ? FinalDamageMultiplier
                    : 1f;

            float totalMultiplier =
                soulMultiplier *
                finalMultiplier;

            _boss.BaseAtkDmg =
                Mathf.RoundToInt(
                    _baseAttackDamage *
                    totalMultiplier);

            _boss.DamageRange =
                new Vector2(
                    _baseDamageRange.x *
                    totalMultiplier,
                    _baseDamageRange.y *
                    totalMultiplier);

            Stats stats =
                _bossCharacter != null
                    ? _bossCharacter.MyStats
                    : null;

            if (stats != null &&
                _baseMhDelay >
                0f)
            {
                stats.BaseMHAtkDelay =
                    _finalPhase
                        ? _baseMhDelay /
                          FinalAttackSpeedMultiplier
                        : _baseMhDelay;

                try
                {
                    stats.CalcStats();

                    if (stats.GetMHAtkDelay() >
                        stats.CurrentMHAtkDelay)
                    {
                        stats.ResetMHAtkDelay(
                            stats.CurrentMHAtkDelay);
                    }
                }
                catch
                {
                }
            }
        }


        private static void RestoreBossOffensiveState()
        {
            if (_boss == null)
            {
                return;
            }

            _boss.BaseAtkDmg =
                _baseAttackDamage;

            _boss.DamageRange =
                _baseDamageRange;

            Stats stats =
                _bossCharacter != null
                    ? _bossCharacter.MyStats
                    : null;

            if (stats != null &&
                _baseMhDelay >
                0f)
            {
                stats.BaseMHAtkDelay =
                    _baseMhDelay;

                try
                {
                    stats.CalcStats();
                }
                catch
                {
                }
            }
        }


        // ============================================================
        // BOSS COMBAT RETENTION / SIM TANK SOFT TETHER
        // ============================================================

        private static void MaintainBossCombatState(
            bool force)
        {
            if (!_engaged ||
                _boss == null ||
                !IsBossAlive())
            {
                return;
            }

            float now =
                Time.realtimeSinceStartup;

            if (!force &&
                now <
                _nextBossCombatRepairRealtime)
            {
                return;
            }

            _nextBossCombatRepairRealtime =
                now +
                BossCombatRepairInterval;

            try
            {
                _boss.enabled =
                    true;

                _boss.retreat =
                    false;

                if (_bossCharacter != null)
                {
                    _bossCharacter.Relax =
                        false;

                    // Native source NPCs can restore their authored aggro
                    // range during state changes. Keep Varkesh's proximity
                    // detection intentionally local to the encounter.
                    _bossCharacter.AggroRange =
                        NativeAggroDetectionRange;
                }

                Character target =
                    _boss.CurrentAggroTarget;

                bool targetInRetentionRange =
                    IsLivingCharacter(
                        target) &&
                    PlanarDistance(
                        target.transform.position,
                        _boss.transform.position) <=
                    BossCombatRetentionRadius;

                if (!targetInRetentionRange)
                {
                    target =
                        FindNearestLivingRaidTargetWithin(
                            _boss.transform.position,
                            BossCombatRetentionRadius);

                    _boss.CurrentAggroTarget =
                        target;
                }

                if (target == null)
                {
                    return;
                }

                NavMeshAgent agent =
                    _boss.GetComponent<NavMeshAgent>();

                if (agent == null)
                {
                    agent =
                        _boss.GetComponentInChildren<NavMeshAgent>(
                            true);
                }

                if (agent == null ||
                    !agent.enabled ||
                    !agent.isOnNavMesh)
                {
                    return;
                }

                float attackRange =
                    _bossCharacter != null
                        ? Mathf.Max(
                            0.5f,
                            _bossCharacter.AttackRange)
                        : 4.3f;

                float distance =
                    PlanarDistance(
                        _boss.transform.position,
                        target.transform.position);

                if (distance >
                    attackRange +
                    1.0f)
                {
                    if (agent.isStopped)
                    {
                        agent.isStopped =
                            false;
                    }

                    // Native NPC AI still owns attacks/threat. This merely
                    // prevents a transient stopped/retreat state from letting a
                    // Sim tank walk out of combat while Varkesh stands still.
                    if (!_castActive)
                    {
                        agent.SetDestination(
                            target.transform.position);
                    }
                }
            }
            catch
            {
                // Death/scene teardown can invalidate native components.
            }
        }


        private static Character FindNearestLivingRaidTargetWithin(
            Vector3 origin,
            float maxDistance)
        {
            List<Character> living =
                GetLivingRaidCharacters();

            Character best =
                null;

            float maxDistanceSqr =
                Mathf.Max(
                    0f,
                    maxDistance) *
                Mathf.Max(
                    0f,
                    maxDistance);

            float bestDistanceSqr =
                maxDistanceSqr;

            for (int i = 0;
                 i < living.Count;
                 i++)
            {
                Character candidate =
                    living[i];

                if (!IsLivingCharacter(
                        candidate))
                {
                    continue;
                }

                Vector3 delta =
                    candidate.transform.position -
                    origin;

                delta.y =
                    0f;

                float sqr =
                    delta.sqrMagnitude;

                if (sqr <=
                        bestDistanceSqr)
                {
                    bestDistanceSqr =
                        sqr;

                    best =
                        candidate;
                }
            }

            return best;
        }


        private static Character FindNearestLivingRaidTarget(
            Vector3 origin)
        {
            List<Character> living =
                GetLivingRaidCharacters();

            Character best =
                null;

            float bestDistanceSqr =
                float.PositiveInfinity;

            for (int i = 0;
                 i < living.Count;
                 i++)
            {
                Character candidate =
                    living[i];

                if (!IsLivingCharacter(
                        candidate))
                {
                    continue;
                }

                Vector3 delta =
                    candidate.transform.position -
                    origin;

                delta.y =
                    0f;

                float sqr =
                    delta.sqrMagnitude;

                if (sqr <
                    bestDistanceSqr)
                {
                    bestDistanceSqr =
                        sqr;

                    best =
                        candidate;
                }
            }

            return best;
        }


        private static void MaintainTankSimTether(
            int generation)
        {
            if (!_engaged ||
                _boss == null ||
                !IsBossAlive() ||
                _castActive ||
                _tankTetherActive ||
                WorldBossSimAvoidanceRuntime.HasActiveAssignments)
            {
                return;
            }

            float now =
                Time.realtimeSinceStartup;

            if (now <
                _nextTankTetherCheckRealtime)
            {
                return;
            }

            _nextTankTetherCheckRealtime =
                now +
                TankSimTetherCheckInterval;

            Character target =
                _boss.CurrentAggroTarget;

            if (!IsLivingCharacter(
                    target))
            {
                return;
            }

            SimPlayer tankSim =
                FindSimForCharacter(
                    target);

            if (tankSim == null)
            {
                // Never auto-position the human player.
                return;
            }

            float targetHomeDistance =
                PlanarDistance(
                    target.transform.position,
                    _homePosition);

            float bossHomeDistance =
                PlanarDistance(
                    _boss.transform.position,
                    _homePosition);

            if (targetHomeDistance <=
                    TankSimSoftBoundaryFromHome &&
                bossHomeDistance <=
                    TankSimSoftBoundaryFromHome)
            {
                return;
            }

            Vector3 outward =
                _boss.transform.position -
                _homePosition;

            outward.y =
                0f;

            if (outward.sqrMagnitude <
                0.01f)
            {
                outward =
                    target.transform.position -
                    _homePosition;

                outward.y =
                    0f;
            }

            if (outward.sqrMagnitude <
                0.01f)
            {
                outward =
                    Vector3.forward;
            }

            outward.Normalize();

            Vector3 desired =
                _homePosition +
                outward *
                TankSimReturnRadiusFromHome;

            Vector3 returnPoint =
                ProjectToNavMesh(
                    desired);

            Dictionary<SimPlayer, Vector3>
                destination =
                    new Dictionary<SimPlayer, Vector3>();

            destination[tankSim] =
                returnPoint;

            _tankTetherActive =
                true;

            WorldBossSimAvoidanceRuntime.BeginDirectedMovement(
                destination,
                TankSimTetherMechanic);

            Plugin.NativeLog.LogInfo(
                "[Varkesh] tank soft tether engaged for Sim=[" +
                GetCharacterDisplayName(
                    target) +
                "] BossHomeDistance=[" +
                bossHomeDistance.ToString("0.0") +
                "] TankHomeDistance=[" +
                targetHomeDistance.ToString("0.0") +
                "] ReturnPoint=[" +
                returnPoint +
                "].");

            CoroutineHost.Start(
                FinishTankSimTether(
                    generation));
        }


        private static IEnumerator FinishTankSimTether(
            int generation)
        {
            float expires =
                Time.realtimeSinceStartup +
                TankSimTetherTimeout;

            while (IsCurrent(
                       generation) &&
                   IsBossAlive() &&
                   Time.realtimeSinceStartup <
                       expires &&
                   WorldBossSimAvoidanceRuntime.IsActiveMechanic(
                       TankSimTetherMechanic))
            {
                WorldBossSimAvoidanceRuntime.UpdateMovement();

                if (WorldBossSimAvoidanceRuntime
                    .AreAllActiveAssignmentsArrived)
                {
                    break;
                }

                yield return null;
            }

            // A real boss mechanic may have replaced this directed movement.
            // Never release that newer mechanic from the tank-tether coroutine.
            if (WorldBossSimAvoidanceRuntime.IsActiveMechanic(
                    TankSimTetherMechanic))
            {
                WorldBossSimAvoidanceRuntime.ReleaseAssignments(
                    "Varkesh tank soft tether complete",
                    WorldBossSimAvoidanceRuntime
                        .ReleaseMode
                        .ResumeEncounter);
            }

            _tankTetherActive =
                false;

            // Reassert Varkesh's target/chase state immediately after the Sim
            // returns to the encounter footprint.
            MaintainBossCombatState(
                true);
        }


        // ============================================================
        // LEASH / RESET
        // ============================================================

        private static bool ShouldLeashReset()
        {
            if (_boss == null)
            {
                return false;
            }

            WorldBossDefinition definition =
                WorldBossCatalog.Varkesh;

            if (Vector3.Distance(
                    _boss.transform.position,
                    _homePosition) >
                definition.LeashRadius)
            {
                return true;
            }

            if (HasLivingRaidCharacterWithin(
                    _boss.transform.position,
                    BossCombatRetentionRadius))
            {
                _noTargetSinceRealtime =
                    -1f;

                return false;
            }

            if (_noTargetSinceRealtime <
                0f)
            {
                _noTargetSinceRealtime =
                    Time.realtimeSinceStartup;

                return false;
            }

            return
                Time.realtimeSinceStartup -
                _noTargetSinceRealtime >=
                definition.ResetNoTargetSeconds;
        }


        private static bool HasLivingRaidCharacterWithin(
            Vector3 center,
            float radius)
        {
            List<Character> living =
                GetLivingRaidCharacters();

            float sqr =
                radius *
                radius;

            for (int i = 0;
                 i < living.Count;
                 i++)
            {
                Vector3 delta =
                    living[i].transform.position -
                    center;

                delta.y =
                    0f;

                if (delta.sqrMagnitude <=
                    sqr)
                {
                    return true;
                }
            }

            return false;
        }


        private static void ResetBossToHome(
            string reason)
        {
            NPC boss =
                _boss;

            if (boss == null)
            {
                return;
            }

            Character character =
                _bossCharacter;

            Stats stats =
                character != null
                    ? character.MyStats
                    : null;

            RaidBossCastBarRuntime.Hide();

            WorldBossSimAvoidanceRuntime.ForceReleaseAll(
                "Varkesh reset");

            DestroyCastVisuals();
            ClearBurialPools();
            ClearGraveSouls();

            _graveMarchBuffStacks =
                0;

            _tankTetherActive =
                false;

            BrandStacks.Clear();

            RestoreBossOffensiveState();

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

            boss.CurrentAggroTarget =
                null;

            boss.retreat =
                false;

            TeleportBossTo(
                _homePosition,
                _homeRotation);

            Plugin.NativeLog.LogInfo(
                "[Varkesh] reset to home. Reason=[" +
                reason +
                "].");

            AttachBoss(
                boss,
                _homePosition);
        }


        private static void TeleportBossTo(
            Vector3 position,
            Quaternion rotation)
        {
            if (_boss == null)
            {
                return;
            }

            NavMeshAgent agent =
                _boss.GetComponent<NavMeshAgent>();

            if (agent == null)
            {
                agent =
                    _boss.GetComponentInChildren<NavMeshAgent>(
                        true);
            }

            Vector3 target =
                ProjectToNavMesh(
                    position);

            if (agent != null &&
                agent.enabled &&
                agent.isOnNavMesh)
            {
                try
                {
                    agent.Warp(
                        target);
                }
                catch
                {
                    _boss.transform.position =
                        target;
                }
            }
            else
            {
                _boss.transform.position =
                    target;
            }

            _boss.transform.rotation =
                rotation;
        }


        // ============================================================
        // DAMAGE / RAID ROSTER
        // ============================================================

        private static bool ApplyNamedDamage(
            Character target,
            string sourceName,
            int damage)
        {
            if (!IsLivingCharacter(
                    target))
            {
                return false;
            }

            try
            {
                RaidMechanicDamageAttribution.SetNamedSource(
                    _bossCharacter,
                    sourceName);

                int resolved =
                    target.DamageMe(
                        damage,
                        false,
                        GameData.DamageType.Magic,
                        _bossCharacter,
                        false,
                        false,
                        0);

                return
                    resolved >
                    0;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Varkesh] [" +
                    sourceName +
                    "] damage failed: " +
                    exception);

                return false;
            }
        }


        private static List<Character> GetLivingRaidCharacters()
        {
            List<Character> result =
                new List<Character>();

            PlayerControl playerControl =
                GameData.PlayerControl;

            Character player =
                playerControl != null
                    ? playerControl.Myself
                    : null;

            AddLivingCharacter(
                result,
                player);

            if (GameData.GroupMembers != null)
            {
                for (int i = 0;
                     i <
                     GameData.GroupMembers.Length;
                     i++)
                {
                    SimPlayerTracking tracking =
                        GameData.GroupMembers[i];

                    SimPlayer sim =
                        tracking != null
                            ? tracking.MyAvatar
                            : null;

                    NPC npc =
                        sim != null
                            ? sim.GetThisNPC()
                            : null;

                    Character character =
                        npc != null
                            ? npc.GetChar()
                            : null;

                    AddLivingCharacter(
                        result,
                        character);
                }
            }

            RaidManager manager =
                GameData.RaidManager;

            if (manager != null &&
                manager.Raiders != null)
            {
                foreach (RaidMemberSlot slot
                         in manager.Raiders)
                {
                    SimPlayer sim =
                        slot != null
                            ? slot.AssignedAvatar
                            : null;

                    NPC npc =
                        sim != null
                            ? sim.GetThisNPC()
                            : null;

                    Character character =
                        npc != null
                            ? npc.GetChar()
                            : null;

                    AddLivingCharacter(
                        result,
                        character);
                }
            }

            return result;
        }


        private static List<Character> GetLivingRaidCharactersWithin(
            Vector3 center,
            float radius)
        {
            List<Character> all =
                GetLivingRaidCharacters();

            List<Character> result =
                new List<Character>();

            float radiusSqr =
                Mathf.Max(
                    0f,
                    radius) *
                Mathf.Max(
                    0f,
                    radius);

            for (int i = 0;
                 i < all.Count;
                 i++)
            {
                Character character =
                    all[i];

                if (!IsLivingCharacter(
                        character))
                {
                    continue;
                }

                Vector3 delta =
                    character.transform.position -
                    center;

                delta.y =
                    0f;

                if (delta.sqrMagnitude <=
                    radiusSqr)
                {
                    result.Add(
                        character);
                }
            }

            return result;
        }


        private static void AddLivingCharacter(
            List<Character> result,
            Character character)
        {
            if (!IsLivingCharacter(
                    character) ||
                result.Contains(
                    character))
            {
                return;
            }

            result.Add(
                character);
        }


        private static bool IsLivingCharacter(
            Character character)
        {
            return
                character != null &&
                character.Alive &&
                character.MyStats != null &&
                character.MyStats.CurrentHP >
                0;
        }


        private static SimPlayer FindSimForCharacter(
            Character character)
        {
            if (character == null)
            {
                return null;
            }

            List<SimPlayer> sims =
                WorldBossSimAvoidanceRuntime
                    .GetLivingRaidSimsSnapshot();

            for (int i = 0;
                 i < sims.Count;
                 i++)
            {
                SimPlayer sim =
                    sims[i];

                if (sim == null)
                {
                    continue;
                }

                NPC npc =
                    sim.GetThisNPC();

                Character candidate =
                    npc != null
                        ? npc.GetChar()
                        : null;

                if (candidate ==
                    character)
                {
                    return sim;
                }
            }

            return null;
        }


        // ============================================================
        // TELEGRAPHS / POSITION
        // ============================================================

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

            mesh.name =
                name +
                "_TerrainMesh";

            mesh.MarkDynamic();

            filter.sharedMesh =
                mesh;

            renderer.material =
                CreateTelegraphMaterial(
                    color);

            // The actual mesh is built from independently sampled terrain
            // points. This means the warning surface follows hills, dips and
            // uneven ground instead of intersecting the terrain as a flat
            // primitive cylinder.
            RebuildTerrainFollowingDisc(
                disc,
                position,
                radius);

            return disc;
        }


        private static void PositionGroundDisc(
            GameObject disc,
            Vector3 position,
            float radius)
        {
            if (disc == null)
            {
                return;
            }

            RebuildTerrainFollowingDisc(
                disc,
                position,
                radius);
        }


        private static void RebuildTerrainFollowingDisc(
            GameObject disc,
            Vector3 position,
            float radius)
        {
            if (disc == null)
            {
                return;
            }

            MeshFilter filter =
                disc.GetComponent<MeshFilter>();

            if (filter == null)
            {
                return;
            }

            Mesh mesh =
                filter.sharedMesh;

            if (mesh == null)
            {
                mesh =
                    new Mesh();

                mesh.MarkDynamic();

                filter.sharedMesh =
                    mesh;
            }

            radius =
                Mathf.Max(
                    0.25f,
                    radius);

            // Similar density to Nhal'Sera's proven 3D ground-following
            // telegraphs. Six radial samples are enough to conform over
            // Willowwatch's rolling terrain while remaining cheap enough for
            // the moving Marked for Burial indicators.
            const int angularSegments =
                36;

            const int radialSegments =
                6;

            const float surfaceLift =
                0.085f;

            // Give the outer edge real visible depth. The upper face follows
            // the terrain; this short skirt hangs down from that sampled edge.
            // The projection is therefore a 3D volume, not a flat 2D decal.
            const float edgeDepth =
                0.16f;

            int ringWidth =
                angularSegments +
                1;

            int topVertexCount =
                1 +
                radialSegments *
                ringWidth;

            int skirtTopStart =
                topVertexCount;

            int skirtBottomStart =
                skirtTopStart +
                ringWidth;

            Vector3[] vertices =
                new Vector3[
                    topVertexCount +
                    ringWidth *
                    2];

            List<int> triangles =
                new List<int>(
                    angularSegments *
                    radialSegments *
                    6 +
                    angularSegments *
                    6);

            Vector3 projectedCenter =
                ProjectTelegraphPointToGround(
                    position);

            // Keep the GameObject transform at world identity. The mesh stores
            // world-space sampled vertices, exactly like Nhal'Sera's working
            // terrain projections, so every vertex can have its own Y value.
            disc.transform.position =
                Vector3.zero;

            disc.transform.rotation =
                Quaternion.identity;

            disc.transform.localScale =
                Vector3.one;

            vertices[0] =
                projectedCenter +
                Vector3.up *
                surfaceLift;

            for (int ring = 1;
                 ring <= radialSegments;
                 ring++)
            {
                float ringRadius =
                    radius *
                    ring /
                    radialSegments;

                int ringStart =
                    1 +
                    (ring - 1) *
                    ringWidth;

                for (int a = 0;
                     a <= angularSegments;
                     a++)
                {
                    float angle =
                        ((float)a /
                         angularSegments) *
                        Mathf.PI *
                        2f;

                    Vector3 world =
                        new Vector3(
                            position.x +
                            Mathf.Sin(
                                angle) *
                            ringRadius,
                            position.y,
                            position.z +
                            Mathf.Cos(
                                angle) *
                            ringRadius);

                    Vector3 projected =
                        ProjectTelegraphPointToGround(
                            world);

                    vertices[
                        ringStart +
                        a] =
                        projected +
                        Vector3.up *
                        surfaceLift;
                }
            }

            int firstRingStart =
                1;

            for (int a = 0;
                 a < angularSegments;
                 a++)
            {
                triangles.Add(
                    0);

                triangles.Add(
                    firstRingStart +
                    a);

                triangles.Add(
                    firstRingStart +
                    a +
                    1);
            }

            for (int ring = 2;
                 ring <= radialSegments;
                 ring++)
            {
                int innerStart =
                    1 +
                    (ring - 2) *
                    ringWidth;

                int outerStart =
                    1 +
                    (ring - 1) *
                    ringWidth;

                for (int a = 0;
                     a < angularSegments;
                     a++)
                {
                    int i0 =
                        innerStart +
                        a;

                    int i1 =
                        i0 +
                        1;

                    int o0 =
                        outerStart +
                        a;

                    int o1 =
                        o0 +
                        1;

                    triangles.Add(
                        i0);

                    triangles.Add(
                        o0);

                    triangles.Add(
                        o1);

                    triangles.Add(
                        i0);

                    triangles.Add(
                        o1);

                    triangles.Add(
                        i1);
                }
            }

            int outerRingStart =
                1 +
                (radialSegments - 1) *
                ringWidth;

            for (int a = 0;
                 a <= angularSegments;
                 a++)
            {
                Vector3 top =
                    vertices[
                        outerRingStart +
                        a];

                vertices[
                    skirtTopStart +
                    a] =
                    top;

                vertices[
                    skirtBottomStart +
                    a] =
                    top -
                    Vector3.up *
                    edgeDepth;
            }

            for (int a = 0;
                 a < angularSegments;
                 a++)
            {
                int top0 =
                    skirtTopStart +
                    a;

                int top1 =
                    top0 +
                    1;

                int bottom0 =
                    skirtBottomStart +
                    a;

                int bottom1 =
                    bottom0 +
                    1;

                triangles.Add(
                    top0);

                triangles.Add(
                    bottom0);

                triangles.Add(
                    bottom1);

                triangles.Add(
                    top0);

                triangles.Add(
                    bottom1);

                triangles.Add(
                    top1);
            }

            mesh.Clear();

            mesh.vertices =
                vertices;

            mesh.triangles =
                triangles.ToArray();

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }


        private static Vector3 ProjectToGround(
            Vector3 point)
        {
            return
                ProjectTelegraphPointToGround(
                    point);
        }


        private static Vector3 ProjectTelegraphPointToGround(
            Vector3 point)
        {
            const float probeUp =
                16f;

            const float probeDown =
                40f;

            Vector3 origin =
                point +
                Vector3.up *
                probeUp;

            RaycastHit[] hits =
                Physics.RaycastAll(
                    origin,
                    Vector3.down,
                    probeUp +
                    probeDown,
                    ~0,
                    QueryTriggerInteraction.Ignore);

            if (hits != null &&
                hits.Length >
                0)
            {
                Array.Sort(
                    hits,
                    delegate(
                        RaycastHit a,
                        RaycastHit b)
                    {
                        return
                            a.distance.CompareTo(
                                b.distance);
                    });

                for (int i = 0;
                     i < hits.Length;
                     i++)
                {
                    Collider collider =
                        hits[i].collider;

                    if (collider == null)
                    {
                        continue;
                    }

                    // Never let the projection ride on top of the boss,
                    // players, Sims or other NPCs. We want the actual world
                    // surface beneath them.
                    if (collider.GetComponentInParent<Character>() !=
                            null ||
                        collider.GetComponentInParent<NPC>() !=
                            null)
                    {
                        continue;
                    }

                    return
                        hits[i].point;
                }
            }

            NavMeshHit navHit;

            if (NavMesh.SamplePosition(
                    point,
                    out navHit,
                    8f,
                    NavMesh.AllAreas))
            {
                return
                    navHit.position;
            }

            return point;
        }


        private static Vector3 ProjectToNavMesh(
            Vector3 point)
        {
            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                    point,
                    out hit,
                    8f,
                    NavMesh.AllAreas))
            {
                return
                    hit.position;
            }

            return point;
        }


        private static Material CreateTelegraphMaterial(
            Color color)
        {
            Shader shader =
                Shader.Find(
                    "Standard");

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Sprites/Default");
            }

            Material material =
                new Material(
                    shader);

            material.color =
                color;

            if (material.HasProperty(
                    "_Mode"))
            {
                material.SetFloat(
                    "_Mode",
                    3f);
            }

            if (material.HasProperty(
                    "_SrcBlend"))
            {
                material.SetInt(
                    "_SrcBlend",
                    (int)UnityEngine.Rendering
                        .BlendMode
                        .SrcAlpha);
            }

            if (material.HasProperty(
                    "_DstBlend"))
            {
                material.SetInt(
                    "_DstBlend",
                    (int)UnityEngine.Rendering
                        .BlendMode
                        .OneMinusSrcAlpha);
            }

            if (material.HasProperty(
                    "_ZWrite"))
            {
                material.SetInt(
                    "_ZWrite",
                    0);
            }

            material.DisableKeyword(
                "_ALPHATEST_ON");

            material.EnableKeyword(
                "_ALPHABLEND_ON");

            material.DisableKeyword(
                "_ALPHAPREMULTIPLY_ON");

            material.renderQueue =
                3000;

            return material;
        }


        private static IEnumerator DestroyAfterSeconds(
            GameObject target,
            float seconds)
        {
            float end =
                Time.realtimeSinceStartup +
                Mathf.Max(
                    0f,
                    seconds);

            while (target != null &&
                   Time.realtimeSinceStartup <
                   end)
            {
                yield return null;
            }

            DestroyObject(
                target);
        }


        private static void DestroyCastVisuals()
        {
            for (int i = 0;
                 i < CastVisuals.Count;
                 i++)
            {
                DestroyObject(
                    CastVisuals[i]);
            }

            CastVisuals.Clear();
        }


        private static void ClearBurialPools()
        {
            for (int i = 0;
                 i < ActiveBurialPools.Count;
                 i++)
            {
                BurialPool pool =
                    ActiveBurialPools[i];

                if (pool != null)
                {
                    DestroyObject(
                        pool.Visual);
                }
            }

            ActiveBurialPools.Clear();
        }


        private static void ClearGraveSouls()
        {
            for (int i = 0;
                 i < ActiveGraveSouls.Count;
                 i++)
            {
                GraveSoul soul =
                    ActiveGraveSouls[i];

                if (soul != null)
                {
                    DestroyGraveSoulObjects(
                        soul);
                }
            }

            ActiveGraveSouls.Clear();
        }


        private static void DestroyObject(
            UnityEngine.Object target)
        {
            if (target != null)
            {
                UnityEngine.Object.Destroy(
                    target);
            }
        }


        private static float PlanarDistance(
            Vector3 a,
            Vector3 b)
        {
            Vector3 delta =
                a -
                b;

            delta.y =
                0f;

            return
                delta.magnitude;
        }


        private static bool IsBossAlive()
        {
            if (_boss == null ||
                _bossCharacter == null)
            {
                return false;
            }

            Stats stats =
                _bossCharacter.MyStats;

            return
                _bossCharacter.Alive &&
                stats != null &&
                stats.CurrentHP >
                0;
        }


        private static bool IsCurrent(
            int generation)
        {
            return
                generation ==
                _generation &&
                _boss !=
                null;
        }


        private static bool CanBeginCast(
            int generation)
        {
            return
                IsCurrent(
                    generation) &&
                IsBossAlive() &&
                !_castActive;
        }
    }


    // ============================================================
    // COMBAT ENGAGEMENT / CC IDENTITY
    // ============================================================

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
    internal static class Patch_VarkeshEncounter_DamageMe
    {
        [HarmonyPostfix]
        private static void Postfix(
            Character __instance,
            int _incdmg)
        {
            VarkeshEncounterRuntime.NotifyBossDamaged(
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
    internal static class Patch_VarkeshEncounter_MagicDamageMe
    {
        [HarmonyPostfix]
        private static void Postfix(
            Character __instance,
            int _dmg)
        {
            VarkeshEncounterRuntime.NotifyBossDamaged(
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
    internal static class Patch_VarkeshEncounter_BleedDamageMe
    {
        [HarmonyPostfix]
        private static void Postfix(
            Character __instance,
            int _incdmg)
        {
            VarkeshEncounterRuntime.NotifyBossDamaged(
                __instance,
                _incdmg);
        }
    }
}
