using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace FateOfTheFallen
{
    internal static class GorathEncounterRuntime
    {
        internal const float PhaseTwoThreshold = 0.70f;
        internal const float PhaseThreeThreshold = 0.40f;
        internal const float FinalPhaseThreshold = 0.10f;

        internal const float NativeAggroDetectionRange = 28f;

        private const float CombatRetentionRadius = 50f;
        private const float MechanicParticipantRadius = 50f;

        private const string InfernalCleaveName = "Infernal Cleave";
        private const string HellfireRainName = "Hellfire Rain";
        private const string InfernalChainsName = "Infernal Chains";
        private const string FurnaceBrandName = "Brand of the Furnace";

        private const float InfernalCleaveInitialDelay = 6f;
        private const float InfernalCleaveRepeatP1 = 16f;
        private const float InfernalCleaveRepeatP2 = 14f;
        private const float InfernalCleaveRepeatP3 = 12f;
        private const float InfernalCleaveCastTime = 2.5f;
        private const float InfernalCleaveRange = 13f;
        private const float InfernalCleaveHalfAngle = 45f;
        private const int InfernalCleaveDamage = 10500;

        private const float HellfireRainInitialDelay = 10f;
        private const float HellfireRainRepeatP1 = 20f;
        private const float HellfireRainRepeatP2 = 17f;
        private const float HellfireRainRepeatP3 = 15f;
        private const float HellfireRainCastTime = 3f;
        private const float HellfireRadius = 4.5f;
        private const int HellfireImpactDamage = 8500;

        private const float BurningGroundDuration = 8f;
        private const float BurningGroundPulse = 1f;
        private const int BurningGroundDamage = 2400;

        private const float ChainsInitialDelayAfterP2 = 8f;
        private const float ChainsRepeatP2 = 26f;
        private const float ChainsRepeatP3 = 21f;
        private const float ChainsDuration = 5f;
        private const float ChainsBreakDistance = 16f;
        private const int ChainsFailureDamage = 12500;

        private const float FurnaceBrandInitialDelay = 5f;
        private const float FurnaceBrandRepeat = 8f;
        private const float FurnaceBrandReset = 30f;
        private const int FurnaceBrandBaseDamage = 2200;
        private const int FurnaceBrandPerStack = 1300;
        private const int FurnaceBrandMaxStacks = 5;

        private const float FinalDamageMultiplier = 1.35f;
        private const float FinalAttackSpeedMultiplier = 1.25f;

        private const float TankSoftBoundary = 40f;
        private const float TankReturnRadius = 24f;
        private const float TankTetherCheckInterval = 0.40f;
        private const float TankTetherTimeout = 3f;
        private const string TankTetherMechanic = "Gorath Tank Tether";

        private const float BossCombatRepairInterval = 0.25f;

        // Telegraphs are intentionally lifted above Erenshor's dense grass
        // layer and use a bright unlit outline so the danger area remains
        // readable from normal combat camera angles.
        private const float TelegraphSurfaceLift = 0.38f;
        private const float TelegraphOutlineLift = 0.52f;
        private const float TelegraphOutlineWidth = 0.16f;

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
        private static bool _tankTetherActive;

        private static float _combatStartedRealtime = -1f;
        private static float _noTargetSinceRealtime = -1f;
        private static float _nextCleaveRealtime = float.PositiveInfinity;
        private static float _nextHellfireRealtime = float.PositiveInfinity;
        private static float _nextChainsRealtime = float.PositiveInfinity;
        private static float _nextBrandRealtime = float.PositiveInfinity;
        private static float _brandResetRealtime = float.PositiveInfinity;
        private static float _nextBossCombatRepairRealtime;
        private static float _nextTankTetherCheckRealtime;

        private static int _hellfireCastIndex;
        private static int _chainsCastIndex;

        private static int _baseAttackDamage;
        private static Vector2 _baseDamageRange;
        private static float _baseMhDelay;

        private sealed class BurningPool
        {
            internal Vector3 Position;
            internal float ExpiresRealtime;
            internal float NextPulseRealtime;
            internal GameObject Visual;
        }

        private static readonly List<BurningPool> BurningPools =
            new List<BurningPool>();

        private static readonly List<GameObject> CastVisuals =
            new List<GameObject>();

        private static readonly Dictionary<int, int> FurnaceStacks =
            new Dictionary<int, int>();

        internal static bool IsCurrentBossCharacter(Character character)
        {
            return
                character != null &&
                character == _bossCharacter &&
                _boss != null;
        }

        internal static bool IsCurrentBossStats(Stats stats)
        {
            return
                stats != null &&
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
                _bossCharacter = boss.GetComponent<Character>();

            _homePosition = homePosition;
            _homeRotation =
                boss != null
                    ? boss.transform.rotation
                    : Quaternion.identity;

            ResetEncounterState();

            if (_boss == null || _bossCharacter == null)
                return;

            Stats stats = _bossCharacter.MyStats;

            _baseAttackDamage = _boss.BaseAtkDmg;
            _baseDamageRange = _boss.DamageRange;
            _baseMhDelay =
                stats != null
                    ? stats.BaseMHAtkDelay
                    : 0f;

            int generation = _generation;
            CoroutineHost.Start(Run(generation));

            Plugin.NativeLog.LogInfo(
                "[Gorath] encounter attached. Mechanics=[Infernal Cleave, Hellfire Rain, Infernal Chains, Brand of the Furnace, Unbound Fury].");
        }

        internal static void Stop(string reason)
        {
            _generation++;

            if (_castActive)
                RaidBossCastBarRuntime.Hide();

            _castActive = false;
            _tankTetherActive = false;

            WorldBossSimAvoidanceRuntime.ForceReleaseAll(
                string.IsNullOrEmpty(reason)
                    ? "Gorath cleanup"
                    : reason);

            DestroyCastVisuals();
            ClearBurningPools();
            RestoreBossOffensiveState();

            _boss = null;
            _bossCharacter = null;
            _engaged = false;

            if (!string.IsNullOrEmpty(reason) &&
                reason.IndexOf(
                    "boss attach",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                Plugin.NativeLog.LogInfo(
                    "[Gorath] encounter stopped. Reason=[" +
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
                StartCombatClock();
        }

        private static void ResetEncounterState()
        {
            _engaged = false;
            _castActive = false;
            _phaseTwo = false;
            _phaseThree = false;
            _finalPhase = false;
            _tankTetherActive = false;

            _combatStartedRealtime = -1f;
            _noTargetSinceRealtime = -1f;
            _nextCleaveRealtime = float.PositiveInfinity;
            _nextHellfireRealtime = float.PositiveInfinity;
            _nextChainsRealtime = float.PositiveInfinity;
            _nextBrandRealtime = float.PositiveInfinity;
            _brandResetRealtime = float.PositiveInfinity;
            _nextBossCombatRepairRealtime = 0f;
            _nextTankTetherCheckRealtime = 0f;

            _hellfireCastIndex = 0;
            _chainsCastIndex = 0;

            FurnaceStacks.Clear();
            DestroyCastVisuals();
            ClearBurningPools();
        }

        private static void StartCombatClock()
        {
            if (_engaged)
                return;

            _engaged = true;
            _combatStartedRealtime = Time.realtimeSinceStartup;

            _nextCleaveRealtime =
                _combatStartedRealtime +
                InfernalCleaveInitialDelay;

            _nextHellfireRealtime =
                _combatStartedRealtime +
                HellfireRainInitialDelay;

            _nextChainsRealtime =
                float.PositiveInfinity;

            _nextBrandRealtime =
                _combatStartedRealtime +
                FurnaceBrandInitialDelay;

            _brandResetRealtime =
                _combatStartedRealtime +
                FurnaceBrandReset;

            _noTargetSinceRealtime = -1f;
            _nextBossCombatRepairRealtime = 0f;
            _nextTankTetherCheckRealtime = 0f;

            Plugin.NativeLog.LogInfo(
                "[Gorath] combat started. Phase2=70% Phase3=40% Final=10%.");
        }

        private static IEnumerator Run(int generation)
        {
            while (IsCurrent(generation))
            {
                if (!IsBossAlive())
                {
                    WorldBossSimAvoidanceRuntime.ForceReleaseAll(
                        "Gorath death");

                    DestroyCastVisuals();
                    ClearBurningPools();
                    yield break;
                }

                UpdateBurningPools();
                WorldBossSimAvoidanceRuntime.UpdateMovement();

                if (!_engaged)
                {
                    if (_boss != null &&
                        IsLivingCharacter(_boss.CurrentAggroTarget))
                    {
                        StartCombatClock();
                    }

                    yield return null;
                    continue;
                }

                MaintainBossCombatState(false);
                MaintainTankSimTether(generation);

                if (ShouldLeashReset())
                {
                    ResetBossToHome(
                        "hard leash/no living raid targets");

                    yield return null;
                    continue;
                }

                UpdatePhases();
                UpdateFurnaceBrand();

                if (!_castActive)
                {
                    float now = Time.realtimeSinceStartup;

                    if (now >= _nextCleaveRealtime)
                    {
                        _nextCleaveRealtime =
                            now + GetCleaveRepeat();

                        CoroutineHost.Start(
                            CastInfernalCleave(generation));
                    }
                    else if (now >= _nextHellfireRealtime)
                    {
                        _nextHellfireRealtime =
                            now + GetHellfireRepeat();

                        _hellfireCastIndex++;

                        CoroutineHost.Start(
                            CastHellfireRain(
                                generation,
                                _hellfireCastIndex));
                    }
                    else if (_phaseTwo &&
                             now >= _nextChainsRealtime)
                    {
                        _nextChainsRealtime =
                            now +
                            (_phaseThree
                                ? ChainsRepeatP3
                                : ChainsRepeatP2);

                        _chainsCastIndex++;

                        CoroutineHost.Start(
                            CastInfernalChains(
                                generation,
                                _chainsCastIndex));
                    }
                }

                yield return null;
            }
        }

        private static void UpdatePhases()
        {
            Stats stats =
                _bossCharacter != null
                    ? _bossCharacter.MyStats
                    : null;

            if (stats == null || stats.CurrentMaxHP <= 0)
                return;

            float percent =
                (float)stats.CurrentHP /
                stats.CurrentMaxHP;

            if (!_phaseTwo && percent <= PhaseTwoThreshold)
            {
                _phaseTwo = true;
                _nextChainsRealtime =
                    Time.realtimeSinceStartup +
                    ChainsInitialDelayAfterP2;

                Plugin.NativeLog.LogInfo(
                    "[Gorath] 70% phase active: Infernal Chains enabled.");
            }

            if (!_phaseThree && percent <= PhaseThreeThreshold)
            {
                _phaseThree = true;

                Plugin.NativeLog.LogInfo(
                    "[Gorath] 40% phase active: Hellfire Rain now leaves burning ground.");
            }

            if (!_finalPhase && percent <= FinalPhaseThreshold)
            {
                _finalPhase = true;
                ApplyBossOffensiveState();

                Plugin.NativeLog.LogInfo(
                    "[Gorath] 10% phase active: Unbound Fury.");
            }
        }

        private static float GetCleaveRepeat()
        {
            return
                _phaseThree
                    ? InfernalCleaveRepeatP3
                    : (_phaseTwo
                        ? InfernalCleaveRepeatP2
                        : InfernalCleaveRepeatP1);
        }

        private static float GetHellfireRepeat()
        {
            return
                _phaseThree
                    ? HellfireRainRepeatP3
                    : (_phaseTwo
                        ? HellfireRainRepeatP2
                        : HellfireRainRepeatP1);
        }

        // ------------------------------------------------------------
        // Infernal Cleave
        // ------------------------------------------------------------

        private static IEnumerator CastInfernalCleave(int generation)
        {
            if (!CanBeginCast(generation))
                yield break;

            _castActive = true;

            Vector3 center = _boss.transform.position;
            Vector3 forward = _boss.transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;

            forward.Normalize();

            RaidBossCastBarRuntime.Show(
                InfernalCleaveName,
                InfernalCleaveCastTime);

            GameObject telegraph =
                CreateGroundSector(
                    center,
                    forward,
                    InfernalCleaveRange,
                    InfernalCleaveHalfAngle,
                    new Color(
                        0.95f,
                        0.20f,
                        0.02f,
                        0.46f),
                    "FOTF_Gorath_InfernalCleave");

            CastVisuals.Add(telegraph);

            WorldBossSimAvoidanceRuntime.BeginConeAvoidance(
                _boss,
                center,
                forward,
                InfernalCleaveRange,
                InfernalCleaveHalfAngle * 2f);

            float end =
                Time.realtimeSinceStartup +
                InfernalCleaveCastTime;

            while (IsCurrent(generation) &&
                   IsBossAlive() &&
                   Time.realtimeSinceStartup < end)
            {
                WorldBossSimAvoidanceRuntime.UpdateMovement();
                UpdateBurningPools();
                yield return null;
            }

            RaidBossCastBarRuntime.Hide();

            if (IsCurrent(generation) && IsBossAlive())
            {
                List<Character> living =
                    GetLivingRaidCharactersWithin(
                        center,
                        MechanicParticipantRadius);

                int hits = 0;

                for (int i = 0; i < living.Count; i++)
                {
                    Character target = living[i];

                    if (IsInsideSector(
                            target.transform.position,
                            center,
                            forward,
                            InfernalCleaveRange,
                            InfernalCleaveHalfAngle) &&
                        ApplyNamedDamage(
                            target,
                            InfernalCleaveName,
                            InfernalCleaveDamage,
                            GameData.DamageType.Elemental))
                    {
                        hits++;
                    }
                }

                Plugin.NativeLog.LogInfo(
                    "[Gorath] Infernal Cleave resolved. Hit=[" +
                    hits +
                    "].");
            }

            WorldBossSimAvoidanceRuntime.ReleaseAssignments(
                "Gorath Infernal Cleave complete");

            DestroyCastVisuals();
            _castActive = false;
        }

        // ------------------------------------------------------------
        // Hellfire Rain
        // ------------------------------------------------------------

        private static IEnumerator CastHellfireRain(
            int generation,
            int castIndex)
        {
            if (!CanBeginCast(generation))
                yield break;

            _castActive = true;

            RaidBossCastBarRuntime.Show(
                HellfireRainName,
                HellfireRainCastTime);

            List<Character> living =
                GetLivingRaidCharactersWithin(
                    _boss.transform.position,
                    MechanicParticipantRadius);

            int desired =
                _phaseThree
                    ? 4
                    : (_phaseTwo
                        ? 3
                        : 2);

            List<Character> targets =
                SelectTargets(
                    living,
                    desired,
                    castIndex,
                    true);

            List<Vector3> impactPoints =
                new List<Vector3>();

            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];

                Vector3 point =
                    ProjectToGround(
                        target.transform.position);

                impactPoints.Add(point);

                GameObject marker =
                    CreateGroundDisc(
                        point,
                        HellfireRadius,
                        new Color(
                            0.98f,
                            0.16f,
                            0.015f,
                            0.48f),
                        "FOTF_Gorath_HellfireRain");

                CastVisuals.Add(marker);

            }

            WorldBossSimAvoidanceRuntime.BeginCircleAvoidance(
                impactPoints,
                HellfireRadius);

            float end =
                Time.realtimeSinceStartup +
                HellfireRainCastTime;

            while (IsCurrent(generation) &&
                   IsBossAlive() &&
                   Time.realtimeSinceStartup < end)
            {
                WorldBossSimAvoidanceRuntime.UpdateMovement();
                UpdateBurningPools();
                yield return null;
            }

            RaidBossCastBarRuntime.Hide();

            if (IsCurrent(generation) && IsBossAlive())
            {
                List<Character> participants =
                    GetLivingRaidCharactersWithin(
                        _boss.transform.position,
                        MechanicParticipantRadius);

                int hits = 0;

                for (int p = 0; p < impactPoints.Count; p++)
                {
                    Vector3 point = impactPoints[p];

                    for (int i = 0; i < participants.Count; i++)
                    {
                        Character target = participants[i];

                        if (PlanarDistance(
                                target.transform.position,
                                point) <=
                            HellfireRadius)
                        {
                            if (ApplyNamedDamage(
                                    target,
                                    HellfireRainName,
                                    HellfireImpactDamage,
                                    GameData.DamageType.Elemental))
                            {
                                hits++;
                            }
                        }
                    }

                    if (_phaseThree)
                        AddBurningPool(point);
                }

                Plugin.NativeLog.LogInfo(
                    "[Gorath] Hellfire Rain resolved. Zones=[" +
                    impactPoints.Count +
                    "] Hits=[" +
                    hits +
                    "] BurningGround=[" +
                    _phaseThree +
                    "].");
            }

            WorldBossSimAvoidanceRuntime.ReleaseAssignments(
                "Gorath Hellfire Rain complete");

            DestroyCastVisuals();
            _castActive = false;
        }

        private static void AddBurningPool(Vector3 position)
        {
            GameObject visual =
                CreateGroundDisc(
                    position,
                    HellfireRadius,
                    new Color(
                        0.70f,
                        0.06f,
                        0.01f,
                        0.42f),
                    "FOTF_Gorath_BurningGround");

            BurningPools.Add(
                new BurningPool
                {
                    Position = position,
                    ExpiresRealtime =
                        Time.realtimeSinceStartup +
                        BurningGroundDuration,
                    NextPulseRealtime =
                        Time.realtimeSinceStartup,
                    Visual = visual
                });
        }

        private static void UpdateBurningPools()
        {
            if (BurningPools.Count == 0)
                return;

            float now = Time.realtimeSinceStartup;

            for (int i = BurningPools.Count - 1; i >= 0; i--)
            {
                BurningPool pool = BurningPools[i];

                if (pool == null ||
                    now >= pool.ExpiresRealtime)
                {
                    if (pool != null)
                        DestroyObject(pool.Visual);

                    BurningPools.RemoveAt(i);
                    continue;
                }

                if (now < pool.NextPulseRealtime)
                    continue;

                pool.NextPulseRealtime =
                    now +
                    BurningGroundPulse;

                List<Character> living =
                    GetLivingRaidCharactersWithin(
                        pool.Position,
                        HellfireRadius);

                for (int t = 0; t < living.Count; t++)
                {
                    ApplyNamedDamage(
                        living[t],
                        "Burning Ground",
                        BurningGroundDamage,
                        GameData.DamageType.Elemental);
                }
            }
        }

        // ------------------------------------------------------------
        // Infernal Chains
        // ------------------------------------------------------------

        private static IEnumerator CastInfernalChains(
            int generation,
            int castIndex)
        {
            if (!CanBeginCast(generation))
                yield break;

            List<Character> living =
                GetLivingRaidCharactersWithin(
                    _boss.transform.position,
                    MechanicParticipantRadius);

            List<Character> targets =
                SelectTargets(
                    living,
                    2,
                    castIndex + 9001,
                    true);

            if (targets.Count < 2)
                yield break;

            _castActive = true;

            Character first = targets[0];
            Character second = targets[1];

            RaidBossCastBarRuntime.Show(
                InfernalChainsName,
                ChainsDuration);

            GameObject firstMarker =
                CreateGroundDisc(
                    first.transform.position,
                    2.2f,
                    new Color(
                        0.82f,
                        0.05f,
                        0.45f,
                        0.52f),
                    "FOTF_Gorath_ChainA");

            GameObject secondMarker =
                CreateGroundDisc(
                    second.transform.position,
                    2.2f,
                    new Color(
                        0.82f,
                        0.05f,
                        0.45f,
                        0.52f),
                    "FOTF_Gorath_ChainB");

            CastVisuals.Add(firstMarker);
            CastVisuals.Add(secondMarker);

            Dictionary<SimPlayer, Vector3> destinations =
                new Dictionary<SimPlayer, Vector3>();

            Vector3 midpoint =
                (first.transform.position +
                 second.transform.position) *
                0.5f;

            Vector3 axis =
                first.transform.position -
                second.transform.position;

            axis.y = 0f;

            if (axis.sqrMagnitude < 0.01f)
                axis = Vector3.right;

            axis.Normalize();

            SimPlayer firstSim = FindSimForCharacter(first);
            SimPlayer secondSim = FindSimForCharacter(second);

            float halfSpread =
                ChainsBreakDistance *
                0.65f;

            if (firstSim != null)
            {
                destinations[firstSim] =
                    ProjectToNavMesh(
                        midpoint +
                        axis *
                        halfSpread);
            }

            if (secondSim != null)
            {
                destinations[secondSim] =
                    ProjectToNavMesh(
                        midpoint -
                        axis *
                        halfSpread);
            }

            WorldBossSimAvoidanceRuntime.BeginDirectedMovement(
                destinations,
                InfernalChainsName);

            float end =
                Time.realtimeSinceStartup +
                ChainsDuration;

            while (IsCurrent(generation) &&
                   IsBossAlive() &&
                   Time.realtimeSinceStartup < end)
            {
                WorldBossSimAvoidanceRuntime.UpdateMovement();
                UpdateBurningPools();

                if (IsLivingCharacter(first))
                {
                    PositionGroundDisc(
                        firstMarker,
                        first.transform.position,
                        2.2f);
                }

                if (IsLivingCharacter(second))
                {
                    PositionGroundDisc(
                        secondMarker,
                        second.transform.position,
                        2.2f);
                }

                yield return null;
            }

            RaidBossCastBarRuntime.Hide();

            bool broken =
                IsLivingCharacter(first) &&
                IsLivingCharacter(second) &&
                PlanarDistance(
                    first.transform.position,
                    second.transform.position) >=
                ChainsBreakDistance;

            if (!broken)
            {
                if (IsLivingCharacter(first))
                {
                    ApplyNamedDamage(
                        first,
                        InfernalChainsName,
                        ChainsFailureDamage,
                        GameData.DamageType.Magic);
                }

                if (IsLivingCharacter(second))
                {
                    ApplyNamedDamage(
                        second,
                        InfernalChainsName,
                        ChainsFailureDamage,
                        GameData.DamageType.Magic);
                }
            }

            Plugin.NativeLog.LogInfo(
                "[Gorath] Infernal Chains resolved. Broken=[" +
                broken +
                "] Distance=[" +
                (IsLivingCharacter(first) &&
                 IsLivingCharacter(second)
                    ? PlanarDistance(
                        first.transform.position,
                        second.transform.position)
                        .ToString("0.0")
                    : "n/a") +
                "].");

            WorldBossSimAvoidanceRuntime.ReleaseAssignments(
                "Gorath Infernal Chains complete");

            DestroyCastVisuals();
            _castActive = false;
        }

        // ------------------------------------------------------------
        // Brand of the Furnace
        // ------------------------------------------------------------

        private static void UpdateFurnaceBrand()
        {
            float now = Time.realtimeSinceStartup;

            if (now >= _brandResetRealtime)
            {
                FurnaceStacks.Clear();
                _brandResetRealtime =
                    now +
                    FurnaceBrandReset;
            }

            if (now < _nextBrandRealtime)
                return;

            _nextBrandRealtime =
                now +
                FurnaceBrandRepeat;

            Character target =
                _boss != null
                    ? _boss.CurrentAggroTarget
                    : null;

            if (!IsLivingCharacter(target) ||
                PlanarDistance(
                    target.transform.position,
                    _boss.transform.position) >
                CombatRetentionRadius)
            {
                return;
            }

            int id = target.GetInstanceID();
            int stacks = 0;

            FurnaceStacks.TryGetValue(id, out stacks);

            stacks =
                Mathf.Clamp(
                    stacks + 1,
                    1,
                    FurnaceBrandMaxStacks);

            FurnaceStacks[id] = stacks;

            int damage =
                FurnaceBrandBaseDamage +
                (stacks - 1) *
                FurnaceBrandPerStack;

            ApplyNamedDamage(
                target,
                FurnaceBrandName,
                damage,
                GameData.DamageType.Elemental);

            Plugin.NativeLog.LogInfo(
                "[Gorath] Brand of the Furnace target=[" +
                GetCharacterDisplayName(target) +
                "] Stacks=[" +
                stacks +
                "] Damage=[" +
                damage +
                "].");
        }

        // ------------------------------------------------------------
        // Native combat retention / tank leash
        // ------------------------------------------------------------

        private static void MaintainBossCombatState(bool force)
        {
            if (!_engaged ||
                _boss == null ||
                _bossCharacter == null ||
                !IsBossAlive())
            {
                return;
            }

            float now = Time.realtimeSinceStartup;

            if (!force &&
                now < _nextBossCombatRepairRealtime)
            {
                return;
            }

            _nextBossCombatRepairRealtime =
                now +
                BossCombatRepairInterval;

            try
            {
                _boss.retreat = false;
                _bossCharacter.Relax = false;
                _bossCharacter.AggroRange =
                    NativeAggroDetectionRange;

                Character target =
                    _boss.CurrentAggroTarget;

                bool valid =
                    IsLivingCharacter(target) &&
                    PlanarDistance(
                        target.transform.position,
                        _boss.transform.position) <=
                    CombatRetentionRadius;

                if (!valid)
                {
                    target =
                        FindNearestLivingRaidTargetWithin(
                            _boss.transform.position,
                            CombatRetentionRadius);

                    _boss.CurrentAggroTarget = target;
                }

                if (target == null)
                    return;

                NavMeshAgent agent =
                    _boss.GetComponent<NavMeshAgent>();

                if (agent == null)
                {
                    agent =
                        _boss.GetComponentInChildren<NavMeshAgent>(
                            true);
                }

                if (agent != null &&
                    agent.enabled &&
                    agent.isOnNavMesh)
                {
                    float attackRange =
                        Mathf.Max(
                            1f,
                            _bossCharacter.AttackRange);

                    if (PlanarDistance(
                            _boss.transform.position,
                            target.transform.position) >
                        attackRange + 1f)
                    {
                        agent.isStopped = false;

                        if (!_castActive)
                            agent.SetDestination(target.transform.position);
                    }
                }
            }
            catch
            {
            }
        }

        private static void MaintainTankSimTether(int generation)
        {
            if (!_engaged ||
                !IsCurrent(generation) ||
                !IsBossAlive() ||
                _castActive ||
                _tankTetherActive ||
                WorldBossSimAvoidanceRuntime.HasActiveAssignments)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;

            if (now < _nextTankTetherCheckRealtime)
                return;

            _nextTankTetherCheckRealtime =
                now +
                TankTetherCheckInterval;

            Character target = _boss.CurrentAggroTarget;
            SimPlayer tank = FindSimForCharacter(target);

            // Human tanks are intentionally never auto-moved.
            if (tank == null)
                return;

            float tankHomeDistance =
                PlanarDistance(
                    tank.transform.position,
                    _homePosition);

            float bossHomeDistance =
                PlanarDistance(
                    _boss.transform.position,
                    _homePosition);

            if (tankHomeDistance < TankSoftBoundary &&
                bossHomeDistance < TankSoftBoundary)
            {
                return;
            }

            Vector3 outward =
                _boss.transform.position -
                _homePosition;

            outward.y = 0f;

            if (outward.sqrMagnitude < 0.01f)
            {
                outward =
                    tank.transform.position -
                    _homePosition;

                outward.y = 0f;
            }

            if (outward.sqrMagnitude < 0.01f)
                outward = Vector3.forward;

            outward.Normalize();

            Vector3 destination =
                ProjectToNavMesh(
                    _homePosition +
                    outward *
                    TankReturnRadius);

            Dictionary<SimPlayer, Vector3> assignments =
                new Dictionary<SimPlayer, Vector3>();

            assignments[tank] = destination;

            _tankTetherActive = true;

            WorldBossSimAvoidanceRuntime.BeginDirectedMovement(
                assignments,
                TankTetherMechanic);

            CoroutineHost.Start(
                FinishTankTether(generation));
        }

        private static IEnumerator FinishTankTether(int generation)
        {
            float end =
                Time.realtimeSinceStartup +
                TankTetherTimeout;

            while (IsCurrent(generation) &&
                   IsBossAlive() &&
                   Time.realtimeSinceStartup < end &&
                   WorldBossSimAvoidanceRuntime.IsActiveMechanic(
                       TankTetherMechanic))
            {
                WorldBossSimAvoidanceRuntime.UpdateMovement();

                if (WorldBossSimAvoidanceRuntime
                    .AreAllActiveAssignmentsArrived)
                {
                    break;
                }

                yield return null;
            }

            if (WorldBossSimAvoidanceRuntime.IsActiveMechanic(
                    TankTetherMechanic))
            {
                WorldBossSimAvoidanceRuntime.ReleaseAssignments(
                    "Gorath tank soft tether complete",
                    WorldBossSimAvoidanceRuntime
                        .ReleaseMode
                        .ResumeEncounter);
            }

            _tankTetherActive = false;
            MaintainBossCombatState(true);
        }

        private static bool ShouldLeashReset()
        {
            if (_boss == null)
                return false;

            WorldBossDefinition definition =
                WorldBossCatalog.Gorath;

            if (PlanarDistance(
                    _boss.transform.position,
                    _homePosition) >
                definition.LeashRadius)
            {
                return true;
            }

            if (HasLivingRaidCharacterWithin(
                    _boss.transform.position,
                    CombatRetentionRadius))
            {
                _noTargetSinceRealtime = -1f;
                return false;
            }

            if (_noTargetSinceRealtime < 0f)
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

        private static void ResetBossToHome(string reason)
        {
            if (_boss == null ||
                _bossCharacter == null)
            {
                return;
            }

            RaidBossCastBarRuntime.Hide();

            _castActive = false;
            _tankTetherActive = false;

            WorldBossSimAvoidanceRuntime.ForceReleaseAll(
                "Gorath reset");

            DestroyCastVisuals();
            ClearBurningPools();

            Stats stats = _bossCharacter.MyStats;

            if (stats != null)
                stats.CurrentHP = stats.CurrentMaxHP;

            _boss.CurrentAggroTarget = null;
            _boss.retreat = false;
            _bossCharacter.Relax = false;
            _bossCharacter.Alive = true;

            NavMeshAgent agent =
                _boss.GetComponent<NavMeshAgent>();

            if (agent == null)
                agent = _boss.GetComponentInChildren<NavMeshAgent>(true);

            Vector3 home = ProjectToNavMesh(_homePosition);

            if (agent != null &&
                agent.enabled &&
                agent.isOnNavMesh)
            {
                try
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;

                    if (agent.hasPath)
                        agent.ResetPath();

                    agent.Warp(home);
                    agent.isStopped = false;
                }
                catch
                {
                    _boss.transform.position = home;
                }
            }
            else
            {
                _boss.transform.position = home;
            }

            _boss.transform.rotation = _homeRotation;

            RestoreBossOffensiveState();
            ResetEncounterState();

            Plugin.NativeLog.LogInfo(
                "[Gorath] encounter reset to home. Reason=[" +
                (reason ?? "<unknown>") +
                "].");
        }

        private static void ApplyBossOffensiveState()
        {
            if (_boss == null)
                return;

            float damageMultiplier =
                _finalPhase
                    ? FinalDamageMultiplier
                    : 1f;

            _boss.BaseAtkDmg =
                Mathf.RoundToInt(
                    _baseAttackDamage *
                    damageMultiplier);

            _boss.DamageRange =
                new Vector2(
                    _baseDamageRange.x *
                    damageMultiplier,
                    _baseDamageRange.y *
                    damageMultiplier);

            Stats stats =
                _bossCharacter != null
                    ? _bossCharacter.MyStats
                    : null;

            if (stats != null && _baseMhDelay > 0f)
            {
                stats.BaseMHAtkDelay =
                    _baseMhDelay /
                    (_finalPhase
                        ? FinalAttackSpeedMultiplier
                        : 1f);
            }
        }

        private static void RestoreBossOffensiveState()
        {
            if (_boss != null)
            {
                _boss.BaseAtkDmg = _baseAttackDamage;
                _boss.DamageRange = _baseDamageRange;
            }

            Stats stats =
                _bossCharacter != null
                    ? _bossCharacter.MyStats
                    : null;

            if (stats != null && _baseMhDelay > 0f)
                stats.BaseMHAtkDelay = _baseMhDelay;
        }

        // ------------------------------------------------------------
        // Targeting / damage
        // ------------------------------------------------------------

        private static List<Character> SelectTargets(
            List<Character> candidates,
            int desired,
            int seed,
            bool deprioritizeTank)
        {
            List<Character> pool =
                new List<Character>();

            Character tank =
                _boss != null
                    ? _boss.CurrentAggroTarget
                    : null;

            for (int i = 0;
                 candidates != null && i < candidates.Count;
                 i++)
            {
                Character candidate = candidates[i];

                if (!IsLivingCharacter(candidate))
                    continue;

                if (deprioritizeTank &&
                    candidate == tank)
                {
                    continue;
                }

                pool.Add(candidate);
            }

            if (pool.Count < desired)
            {
                for (int i = 0;
                     candidates != null && i < candidates.Count;
                     i++)
                {
                    Character candidate = candidates[i];

                    if (!IsLivingCharacter(candidate) ||
                        pool.Contains(candidate))
                    {
                        continue;
                    }

                    pool.Add(candidate);
                }
            }

            System.Random random =
                new System.Random(seed);

            for (int i = pool.Count - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                Character temp = pool[i];
                pool[i] = pool[swap];
                pool[swap] = temp;
            }

            if (pool.Count > desired)
            {
                pool.RemoveRange(
                    desired,
                    pool.Count - desired);
            }

            return pool;
        }

        private static bool ApplyNamedDamage(
            Character target,
            string sourceName,
            int damage,
            GameData.DamageType damageType)
        {
            if (!IsLivingCharacter(target))
                return false;

            try
            {
                RaidMechanicDamageAttribution.SetNamedSource(
                    _bossCharacter,
                    sourceName);

                int resolved =
                    target.DamageMe(
                        damage,
                        false,
                        damageType,
                        _bossCharacter,
                        false,
                        false,
                        0);

                return resolved > 0;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Gorath] [" +
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

            AddLivingCharacter(
                result,
                playerControl != null
                    ? playerControl.Myself
                    : null);

            if (GameData.GroupMembers != null)
            {
                for (int i = 0;
                     i < GameData.GroupMembers.Length;
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

                    AddLivingCharacter(
                        result,
                        npc != null
                            ? npc.GetChar()
                            : null);
                }
            }

            RaidManager manager = GameData.RaidManager;

            if (manager != null &&
                manager.Raiders != null)
            {
                foreach (RaidMemberSlot slot in manager.Raiders)
                {
                    SimPlayer sim =
                        slot != null
                            ? slot.AssignedAvatar
                            : null;

                    NPC npc =
                        sim != null
                            ? sim.GetThisNPC()
                            : null;

                    AddLivingCharacter(
                        result,
                        npc != null
                            ? npc.GetChar()
                            : null);
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
                Mathf.Max(0f, radius) *
                Mathf.Max(0f, radius);

            for (int i = 0; i < all.Count; i++)
            {
                Character character = all[i];

                if (!IsLivingCharacter(character))
                    continue;

                Vector3 delta =
                    character.transform.position -
                    center;

                delta.y = 0f;

                if (delta.sqrMagnitude <= radiusSqr)
                    result.Add(character);
            }

            return result;
        }

        private static Character FindNearestLivingRaidTargetWithin(
            Vector3 origin,
            float maxDistance)
        {
            List<Character> living =
                GetLivingRaidCharacters();

            Character best = null;
            float bestDistance =
                Mathf.Max(0f, maxDistance);

            for (int i = 0; i < living.Count; i++)
            {
                Character candidate = living[i];

                float distance =
                    PlanarDistance(
                        candidate.transform.position,
                        origin);

                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private static bool HasLivingRaidCharacterWithin(
            Vector3 center,
            float radius)
        {
            List<Character> living =
                GetLivingRaidCharacters();

            for (int i = 0; i < living.Count; i++)
            {
                if (PlanarDistance(
                        living[i].transform.position,
                        center) <= radius)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddLivingCharacter(
            List<Character> result,
            Character character)
        {
            if (!IsLivingCharacter(character) ||
                result.Contains(character))
            {
                return;
            }

            result.Add(character);
        }

        private static bool IsLivingCharacter(Character character)
        {
            return
                character != null &&
                character.Alive &&
                character.MyStats != null &&
                character.MyStats.CurrentHP > 0;
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

                if (sim.gameObject ==
                    character.gameObject)
                {
                    return sim;
                }

                Character directCharacter =
                    sim.GetComponent<Character>();

                if (directCharacter ==
                    character)
                {
                    return sim;
                }

                Character childCharacter =
                    sim.GetComponentInChildren<Character>(
                        true);

                if (childCharacter ==
                    character)
                {
                    return sim;
                }

                NPC npc =
                    sim.GetThisNPC();

                if (npc == null)
                {
                    continue;
                }

                Character npcCharacter =
                    npc.GetChar();

                if (npcCharacter == null)
                {
                    npcCharacter =
                        npc.GetComponent<Character>();

                    if (npcCharacter == null)
                    {
                        npcCharacter =
                            npc.GetComponentInChildren<Character>(
                                true);
                    }
                }

                if (npcCharacter ==
                    character)
                {
                    return sim;
                }
            }

            return null;
        }


        private static string GetCharacterDisplayName(Character character)
        {
            if (character == null)
                return "<null>";

            if (character.MyStats != null &&
                !string.IsNullOrEmpty(character.MyStats.MyName))
            {
                return character.MyStats.MyName;
            }

            return character.name ?? "<unnamed>";
        }

        // ------------------------------------------------------------
        // Terrain-following telegraphs
        // ------------------------------------------------------------

        private static GameObject CreateGroundDisc(
            Vector3 position,
            float radius,
            Color color,
            string name)
        {
            GameObject disc = new GameObject(name);

            MeshFilter filter =
                disc.AddComponent<MeshFilter>();

            MeshRenderer renderer =
                disc.AddComponent<MeshRenderer>();

            Mesh mesh = new Mesh();
            mesh.name = name + "_TerrainMesh";
            mesh.MarkDynamic();

            filter.sharedMesh = mesh;
            renderer.material =
                CreateTelegraphMaterial(
                    color);

            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            renderer.receiveShadows =
                false;

            renderer.sortingOrder =
                1000;

            RebuildGroundDisc(
                disc,
                position,
                radius);

            EnsureDiscOutline(
                disc,
                position,
                radius,
                color);

            return disc;
        }

        private static void PositionGroundDisc(
            GameObject disc,
            Vector3 position,
            float radius)
        {
            if (disc != null)
                RebuildGroundDisc(disc, position, radius);
        }

        private static void RebuildGroundDisc(
            GameObject disc,
            Vector3 position,
            float radius)
        {
            if (disc == null)
                return;

            MeshFilter filter =
                disc.GetComponent<MeshFilter>();

            if (filter == null)
                return;

            Mesh mesh = filter.sharedMesh;

            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.MarkDynamic();
                filter.sharedMesh = mesh;
            }

            radius = Mathf.Max(0.25f, radius);

            const int angularSegments = 36;
            const int radialSegments = 6;
            float surfaceLift = TelegraphSurfaceLift;

            int ringWidth = angularSegments + 1;
            int vertexCount =
                1 +
                radialSegments *
                ringWidth;

            Vector3[] vertices =
                new Vector3[vertexCount];

            List<int> triangles =
                new List<int>();

            disc.transform.position = Vector3.zero;
            disc.transform.rotation = Quaternion.identity;
            disc.transform.localScale = Vector3.one;

            vertices[0] =
                ProjectToGround(position) +
                Vector3.up *
                surfaceLift;

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

                    vertices[ringStart + a] =
                        ProjectToGround(world) +
                        Vector3.up *
                        surfaceLift;
                }
            }

            int firstRing = 1;

            for (int a = 0; a < angularSegments; a++)
            {
                triangles.Add(0);
                triangles.Add(firstRing + a);
                triangles.Add(firstRing + a + 1);
            }

            for (int ring = 2; ring <= radialSegments; ring++)
            {
                int inner =
                    1 +
                    (ring - 2) *
                    ringWidth;

                int outer =
                    1 +
                    (ring - 1) *
                    ringWidth;

                for (int a = 0; a < angularSegments; a++)
                {
                    int i0 = inner + a;
                    int i1 = i0 + 1;
                    int o0 = outer + a;
                    int o1 = o0 + 1;

                    triangles.Add(i0);
                    triangles.Add(o0);
                    triangles.Add(o1);

                    triangles.Add(i0);
                    triangles.Add(o1);
                    triangles.Add(i1);
                }
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            UpdateDiscOutline(
                disc,
                position,
                radius);
        }

        private static GameObject CreateGroundSector(
            Vector3 center,
            Vector3 forward,
            float radius,
            float halfAngle,
            Color color,
            string name)
        {
            GameObject sector = new GameObject(name);

            MeshFilter filter =
                sector.AddComponent<MeshFilter>();

            MeshRenderer renderer =
                sector.AddComponent<MeshRenderer>();

            Mesh mesh = new Mesh();
            mesh.name = name + "_TerrainMesh";
            mesh.MarkDynamic();

            filter.sharedMesh = mesh;
            renderer.material =
                CreateTelegraphMaterial(
                    color);

            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            renderer.receiveShadows =
                false;

            renderer.sortingOrder =
                1000;

            const int angularSegments = 32;
            const int radialSegments = 6;
            float surfaceLift = TelegraphSurfaceLift;

            int ringWidth = angularSegments + 1;
            int vertexCount =
                1 +
                radialSegments *
                ringWidth;

            Vector3[] vertices =
                new Vector3[vertexCount];

            List<int> triangles =
                new List<int>();

            sector.transform.position = Vector3.zero;
            sector.transform.rotation = Quaternion.identity;
            sector.transform.localScale = Vector3.one;

            vertices[0] =
                ProjectToGround(center) +
                Vector3.up *
                surfaceLift;

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
                    float t =
                        (float)a /
                        angularSegments;

                    float angle =
                        Mathf.Lerp(
                            -halfAngle,
                            halfAngle,
                            t);

                    Vector3 direction =
                        Quaternion.Euler(
                            0f,
                            angle,
                            0f) *
                        forward;

                    Vector3 world =
                        center +
                        direction.normalized *
                        ringRadius;

                    vertices[ringStart + a] =
                        ProjectToGround(world) +
                        Vector3.up *
                        surfaceLift;
                }
            }

            int firstRing = 1;

            for (int a = 0; a < angularSegments; a++)
            {
                triangles.Add(0);
                triangles.Add(firstRing + a);
                triangles.Add(firstRing + a + 1);
            }

            for (int ring = 2; ring <= radialSegments; ring++)
            {
                int inner =
                    1 +
                    (ring - 2) *
                    ringWidth;

                int outer =
                    1 +
                    (ring - 1) *
                    ringWidth;

                for (int a = 0; a < angularSegments; a++)
                {
                    int i0 = inner + a;
                    int i1 = i0 + 1;
                    int o0 = outer + a;
                    int o1 = o0 + 1;

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

            EnsureSectorOutline(
                sector,
                center,
                forward,
                radius,
                halfAngle,
                color);

            return sector;
        }

        private static bool IsInsideSector(
            Vector3 point,
            Vector3 center,
            Vector3 forward,
            float radius,
            float halfAngle)
        {
            Vector3 delta =
                point -
                center;

            delta.y = 0f;

            if (delta.sqrMagnitude > radius * radius)
                return false;

            if (delta.sqrMagnitude < 0.01f)
                return true;

            float angle =
                Vector3.Angle(
                    forward,
                    delta.normalized);

            return angle <= halfAngle;
        }

        private static Vector3 ProjectToGround(Vector3 point)
        {
            Vector3 origin =
                point +
                Vector3.up *
                16f;

            RaycastHit[] hits =
                Physics.RaycastAll(
                    origin,
                    Vector3.down,
                    56f,
                    ~0,
                    QueryTriggerInteraction.Ignore);

            if (hits != null && hits.Length > 0)
            {
                Array.Sort(
                    hits,
                    delegate(RaycastHit a, RaycastHit b)
                    {
                        return a.distance.CompareTo(b.distance);
                    });

                for (int i = 0; i < hits.Length; i++)
                {
                    Collider collider = hits[i].collider;

                    if (collider == null ||
                        collider.GetComponentInParent<Character>() != null ||
                        collider.GetComponentInParent<NPC>() != null)
                    {
                        continue;
                    }

                    return hits[i].point;
                }
            }

            return ProjectToNavMesh(point);
        }

        private static Vector3 ProjectToNavMesh(Vector3 point)
        {
            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                    point,
                    out hit,
                    8f,
                    NavMesh.AllAreas))
            {
                return hit.position;
            }

            return point;
        }

        private static Material CreateTelegraphMaterial(
            Color color)
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

            Material material =
                new Material(
                    shader);

            color.a =
                Mathf.Max(
                    color.a,
                    0.68f);

            material.color =
                color;

            if (material.HasProperty(
                    "_Color"))
            {
                material.SetColor(
                    "_Color",
                    color);
            }

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

            if (material.HasProperty(
                    "_EmissionColor"))
            {
                Color emission =
                    new Color(
                        Mathf.Clamp01(
                            color.r * 1.45f),
                        Mathf.Clamp01(
                            color.g * 1.45f),
                        Mathf.Clamp01(
                            color.b * 1.45f),
                        color.a);

                material.SetColor(
                    "_EmissionColor",
                    emission);

                material.EnableKeyword(
                    "_EMISSION");
            }

            material.DisableKeyword(
                "_ALPHATEST_ON");

            material.EnableKeyword(
                "_ALPHABLEND_ON");

            material.DisableKeyword(
                "_ALPHAPREMULTIPLY_ON");

            // Keep the translucent hazard late in the transparent queue so it
            // remains readable against foliage and terrain detail.
            material.renderQueue =
                3100;

            return material;
        }


        private static Material CreateTelegraphOutlineMaterial(
            Color color)
        {
            color.a =
                Mathf.Max(
                    color.a,
                    0.96f);

            Color bright =
                new Color(
                    Mathf.Clamp01(
                        color.r * 1.35f + 0.15f),
                    Mathf.Clamp01(
                        color.g * 1.35f + 0.08f),
                    Mathf.Clamp01(
                        color.b * 1.35f + 0.04f),
                    color.a);

            Material material =
                CreateTelegraphMaterial(
                    bright);

            material.renderQueue =
                3150;

            return material;
        }


        private static void EnsureDiscOutline(
            GameObject disc,
            Vector3 position,
            float radius,
            Color color)
        {
            if (disc == null)
            {
                return;
            }

            Transform child =
                disc.transform.Find(
                    "FOTF_TelegraphOutline");

            LineRenderer line;

            if (child != null)
            {
                line =
                    child.GetComponent<LineRenderer>();
            }
            else
            {
                GameObject outline =
                    new GameObject(
                        "FOTF_TelegraphOutline");

                outline.transform.SetParent(
                    disc.transform,
                    false);

                line =
                    outline.AddComponent<LineRenderer>();

                line.useWorldSpace =
                    true;

                line.loop =
                    true;

                line.widthMultiplier =
                    TelegraphOutlineWidth;

                line.numCapVertices =
                    2;

                line.numCornerVertices =
                    2;

                line.shadowCastingMode =
                    UnityEngine.Rendering
                        .ShadowCastingMode
                        .Off;

                line.receiveShadows =
                    false;

                line.sortingOrder =
                    1100;

                line.material =
                    CreateTelegraphOutlineMaterial(
                        color);
            }

            UpdateDiscOutline(
                disc,
                position,
                radius);
        }


        private static void UpdateDiscOutline(
            GameObject disc,
            Vector3 position,
            float radius)
        {
            if (disc == null)
            {
                return;
            }

            Transform child =
                disc.transform.Find(
                    "FOTF_TelegraphOutline");

            if (child == null)
            {
                return;
            }

            LineRenderer line =
                child.GetComponent<LineRenderer>();

            if (line == null)
            {
                return;
            }

            const int segments =
                48;

            line.positionCount =
                segments;

            for (int i = 0;
                 i < segments;
                 i++)
            {
                float angle =
                    ((float)i /
                     segments) *
                    Mathf.PI *
                    2f;

                Vector3 world =
                    new Vector3(
                        position.x +
                        Mathf.Sin(
                            angle) *
                        radius,
                        position.y,
                        position.z +
                        Mathf.Cos(
                            angle) *
                        radius);

                line.SetPosition(
                    i,
                    ProjectToGround(
                        world) +
                    Vector3.up *
                    TelegraphOutlineLift);
            }
        }


        private static void EnsureSectorOutline(
            GameObject sector,
            Vector3 center,
            Vector3 forward,
            float radius,
            float halfAngle,
            Color color)
        {
            if (sector == null)
            {
                return;
            }

            GameObject outline =
                new GameObject(
                    "FOTF_TelegraphOutline");

            outline.transform.SetParent(
                sector.transform,
                false);

            LineRenderer line =
                outline.AddComponent<LineRenderer>();

            line.useWorldSpace =
                true;

            line.loop =
                false;

            line.widthMultiplier =
                TelegraphOutlineWidth;

            line.numCapVertices =
                2;

            line.numCornerVertices =
                2;

            line.shadowCastingMode =
                UnityEngine.Rendering
                    .ShadowCastingMode
                    .Off;

            line.receiveShadows =
                false;

            line.sortingOrder =
                1100;

            line.material =
                CreateTelegraphOutlineMaterial(
                    color);

            const int arcSegments =
                40;

            line.positionCount =
                arcSegments +
                3;

            Vector3 liftedCenter =
                ProjectToGround(
                    center) +
                Vector3.up *
                TelegraphOutlineLift;

            line.SetPosition(
                0,
                liftedCenter);

            for (int i = 0;
                 i <= arcSegments;
                 i++)
            {
                float t =
                    (float)i /
                    arcSegments;

                float angle =
                    Mathf.Lerp(
                        -halfAngle,
                        halfAngle,
                        t);

                Vector3 direction =
                    Quaternion.Euler(
                        0f,
                        angle,
                        0f) *
                    forward;

                Vector3 world =
                    center +
                    direction.normalized *
                    radius;

                line.SetPosition(
                    i + 1,
                    ProjectToGround(
                        world) +
                    Vector3.up *
                    TelegraphOutlineLift);
            }

            line.SetPosition(
                arcSegments + 2,
                liftedCenter);
        }


        private static void DestroyCastVisuals()
        {
            for (int i = 0; i < CastVisuals.Count; i++)
                DestroyObject(CastVisuals[i]);

            CastVisuals.Clear();
        }

        private static void ClearBurningPools()
        {
            for (int i = 0; i < BurningPools.Count; i++)
            {
                if (BurningPools[i] != null)
                    DestroyObject(BurningPools[i].Visual);
            }

            BurningPools.Clear();
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target != null)
                UnityEngine.Object.Destroy(target);
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            Vector3 delta = a - b;
            delta.y = 0f;
            return delta.magnitude;
        }

        private static bool IsBossAlive()
        {
            if (_boss == null || _bossCharacter == null)
                return false;

            Stats stats = _bossCharacter.MyStats;

            return
                _bossCharacter.Alive &&
                stats != null &&
                stats.CurrentHP > 0;
        }

        private static bool IsCurrent(int generation)
        {
            return
                generation == _generation &&
                _boss != null;
        }

        private static bool CanBeginCast(int generation)
        {
            return
                IsCurrent(generation) &&
                IsBossAlive() &&
                !_castActive;
        }
    }

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
    internal static class Patch_GorathEncounter_DamageMe
    {
        [HarmonyPostfix]
        private static void Postfix(
            Character __instance,
            int _incdmg)
        {
            GorathEncounterRuntime.NotifyBossDamaged(
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
    internal static class Patch_GorathEncounter_MagicDamageMe
    {
        [HarmonyPostfix]
        private static void Postfix(
            Character __instance,
            int _dmg)
        {
            GorathEncounterRuntime.NotifyBossDamaged(
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
    internal static class Patch_GorathEncounter_BleedDamageMe
    {
        [HarmonyPostfix]
        private static void Postfix(
            Character __instance,
            int _incdmg)
        {
            GorathEncounterRuntime.NotifyBossDamaged(
                __instance,
                _incdmg);
        }
    }
}
