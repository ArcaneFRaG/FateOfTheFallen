using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace FateOfTheFallen
{
    /// <summary>
    /// Three-stage / six-mechanic open-world encounter for the
    /// Blight-Touched Colossus.
    ///
    /// Phase 1:
    /// - Fracturing Roar: raid-wide magic damage.
    /// - Crushing Blight: current-target physical tank buster.
    ///
    /// Phase 2 at 70%:
    /// - Blighted Rupture: telegraphed avoidable cone.
    /// - Withering Affliction: three raid members receive an 8-second
    ///   visible debuff and take scripted damage every 2 seconds.
    ///
    /// Phase 3 at 40%:
    /// - Blight Bloom: three telegraphed ground eruptions.
    /// - Blighted Frenzy: temporary melee damage / attack-speed enrage.
    ///
    /// Earlier-phase mechanics continue in later phases.
    /// All player/raid discovery uses Erenshor's native RaidManager roster.
    /// </summary>
    internal static class WorldBossEncounterRuntime
    {
        internal const string MechanicName =
            "Fracturing Roar";

        internal const float FirstCastSeconds =
            10f;

        internal const float RepeatCastSeconds =
            10f;

        internal const float CastTimeSeconds =
            1.5f;

        internal const int RaidWideDamage =
            5200;

        private const string CrushingBlightMechanicName =
            "Crushing Blight";

        private const float CrushingBlightInitialDelaySeconds =
            6f;

        private const float CrushingBlightRepeatSeconds =
            14f;

        private const float CrushingBlightCastTimeSeconds =
            1.75f;

        private const int CrushingBlightDamage =
            11500;

        internal const float PhaseTwoThreshold =
            0.70f;

        internal const float PhaseThreeThreshold =
            0.40f;

        private const string ConeMechanicName =
            "Blighted Rupture";

        private const float ConeInitialDelaySeconds =
            5f;

        private const float ConeRepeatSeconds =
            18f;

        private const float ConeCastTimeSeconds =
            2.5f;

        private const float ConeAngleDegrees =
            100f;

        private const float ConeRange =
            16f;

        private const int ConeDamage =
            9100;

        private const string WitheringAfflictionMechanicName =
            "Withering Affliction";

        private const string WitheringAfflictionSpellId =
            "FOTF_WORLD_BOSS_WITHERING_AFFLICTION";

        private const string WitheringAfflictionIconResourceName =
            "FateOfTheFallen.Resources.BossAbilityIcons.BlightboundColossus.FOTF_Withering_Affliction.png";


        private const float WitheringAfflictionInitialDelaySeconds =
            7f;

        private const float WitheringAfflictionRepeatSeconds =
            18f;

        private const float WitheringAfflictionCastTimeSeconds =
            1.5f;

        private const int WitheringAfflictionTargetCount =
            3;

        private const float WitheringAfflictionDurationSeconds =
            8f;

        private const float WitheringAfflictionTickSeconds =
            2f;

        private const int WitheringAfflictionTickDamage =
            2000;

        private const string BloomMechanicName =
            "Blight Bloom";

        private const float BloomInitialDelaySeconds =
            5f;

        private const float BloomRepeatSeconds =
            20f;

        private const float BloomCastTimeSeconds =
            2.5f;

        private const int BloomCount =
            3;

        private const float BloomRadius =
            4.5f;

        private const int BloomDamage =
            8450;

        private const string BlightedFrenzyMechanicName =
            "Blighted Frenzy";

        private const float BlightedFrenzyInitialDelaySeconds =
            8f;

        private const float BlightedFrenzyRepeatSeconds =
            24f;

        private const float BlightedFrenzyCastTimeSeconds =
            1.25f;

        private const float BlightedFrenzyDurationSeconds =
            8f;

        private const float BlightedFrenzyAttackSpeedMultiplier =
            1.45f;

        private const float BlightedFrenzyDamageMultiplier =
            1.20f;

        private enum BossCastMode
        {
            CombatCast,
            PausedDirectionalCast
        }


        private static NPC _boss;
        private static Character _bossCharacter;
        private static Vector3 _homePosition;

        private static int _generation;
        private static bool _engaged;
        private static float _combatStartedRealtime;
        private static float _nextRoarRealtime;
        private static float _nextCrushingBlightRealtime;
        private static float _nextConeRealtime;
        private static float _nextWitheringAfflictionRealtime;
        private static float _nextBloomRealtime;
        private static float _nextBlightedFrenzyRealtime;
        private static float _noTargetSinceRealtime =
            -1f;

        private static bool _phaseTwo;
        private static bool _phaseThree;
        private static int _roarCastIndex;
        private static int _crushingBlightCastIndex;
        private static int _coneCastIndex;
        private static int _witheringAfflictionCastIndex;
        private static int _bloomCastIndex;
        private static int _blightedFrenzyCastIndex;

        private static bool _castActive;
        private static NPC _pausedBoss;
        private static Character _savedAggroTarget;
        private static NavMeshAgent _pausedAgent;
        private static bool _savedNpcEnabled;
        private static bool _savedAgentStopped;

        private static readonly List<GameObject> TelegraphObjects =
            new List<GameObject>();

        private sealed class WitheringAfflictionState
        {
            internal Character Target;
            internal int StatusSlot;
            internal float NextTickRealtime;
            internal float ExpiresRealtime;
        }

        private static readonly List<WitheringAfflictionState>
            ActiveWitheringAfflictions =
                new List<WitheringAfflictionState>();

        private static Spell _witheringAfflictionDebuffSpell;
        private static Sprite _witheringAfflictionIconSprite;

        private static bool _blightedFrenzyActive;
        private static float _blightedFrenzyEndsRealtime;
        private static int _savedFrenzyBaseAttackDamage;
        private static Vector2 _savedFrenzyDamageRange;
        private static float _savedFrenzyBaseMhDelay;
        private static Animator _frenzyAnimator;
        private static bool _frenzyAnimatorSpeedCaptured;
        private static float _savedFrenzyAnimatorAttackSpeed;

        internal static bool IsEngaged
        {
            get
            {
                return _engaged;
            }
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

            ResetEncounterState();

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
            WorldBossSimAvoidanceRuntime.ForceReleaseAll(
                "encounter stop: " +
                (reason ?? "<unknown>"));

            ClearWitheringAfflictions(
                "encounter stop");

            RestoreBlightedFrenzy(
                "encounter stop");

            _generation++;

            RestoreBossAfterCast();
            WorldBossSimAvoidanceRuntime.ReleaseAssignments(
                "encounter stopped");
            DestroyTelegraphs();

            if (_castActive)
            {
                RaidBossCastBarRuntime.Hide();
            }

            _castActive =
                false;

            ResetEncounterState();

            _boss =
                null;

            _bossCharacter =
                null;

            if (!string.IsNullOrEmpty(
                    reason))
            {
            }
        }


        private static void ResetEncounterState()
        {
            _engaged =
                false;

            _combatStartedRealtime =
                -1f;

            _nextRoarRealtime =
                0f;

            _nextCrushingBlightRealtime =
                0f;

            _nextConeRealtime =
                0f;

            _nextWitheringAfflictionRealtime =
                0f;

            _nextBloomRealtime =
                0f;

            _nextBlightedFrenzyRealtime =
                0f;

            _noTargetSinceRealtime =
                -1f;

            _phaseTwo =
                false;

            _phaseThree =
                false;

            _roarCastIndex =
                0;

            _crushingBlightCastIndex =
                0;

            _coneCastIndex =
                0;

            _witheringAfflictionCastIndex =
                0;

            _bloomCastIndex =
                0;

            _blightedFrenzyCastIndex =
                0;
        }


        internal static void NotifyBossDamaged(
            Character target,
            int incomingDamage)
        {
            if (_engaged ||
                incomingDamage <= 0 ||
                target == null ||
                _bossCharacter == null ||
                target != _bossCharacter ||
                _boss == null ||
                !IsBossAlive())
            {
                return;
            }

            _engaged =
                true;

            _combatStartedRealtime =
                Time.realtimeSinceStartup;

            _nextRoarRealtime =
                _combatStartedRealtime +
                FirstCastSeconds;

            _nextCrushingBlightRealtime =
                _combatStartedRealtime +
                CrushingBlightInitialDelaySeconds;

            _noTargetSinceRealtime =
                -1f;

        }


        private static IEnumerator Run(
            int generation)
        {
            while (IsCurrent(
                       generation))
            {
                if (!IsBossAlive())
                {
                    ClearWitheringAfflictions(
                        "boss death");

                    RestoreBlightedFrenzy(
                        "boss death");

                    WorldBossSimAvoidanceRuntime.ForceReleaseAll(
                        "boss death");

                    yield break;
                }

                UpdateWitheringAfflictions();
                UpdateBlightedFrenzy();

                if (_engaged)
                {
                    if (ShouldLeashReset())
                    {
                        WorldBossRuntime.ResetCurrentBossToHome(
                            "open-world leash/no living raid targets");

                        yield break;
                    }

                    UpdatePhaseState();

                    if (!_castActive)
                    {
                        float now =
                            Time.realtimeSinceStartup;

                        if (_phaseThree &&
                            now >= _nextBloomRealtime)
                        {
                            int castNumber =
                                ++_bloomCastIndex;

                            yield return CastBlightBloom(
                                generation,
                                castNumber);

                            AdvanceSchedule(
                                ref _nextBloomRealtime,
                                BloomRepeatSeconds);
                        }
                        else if (_phaseThree &&
                                 now >= _nextBlightedFrenzyRealtime)
                        {
                            int castNumber =
                                ++_blightedFrenzyCastIndex;

                            yield return CastBlightedFrenzy(
                                generation,
                                castNumber);

                            AdvanceSchedule(
                                ref _nextBlightedFrenzyRealtime,
                                BlightedFrenzyRepeatSeconds);
                        }
                        else if (_phaseTwo &&
                                 now >= _nextConeRealtime)
                        {
                            int castNumber =
                                ++_coneCastIndex;

                            yield return CastBlightedRupture(
                                generation,
                                castNumber);

                            AdvanceSchedule(
                                ref _nextConeRealtime,
                                ConeRepeatSeconds);
                        }
                        else if (_phaseTwo &&
                                 now >= _nextWitheringAfflictionRealtime)
                        {
                            int castNumber =
                                ++_witheringAfflictionCastIndex;

                            yield return CastWitheringAffliction(
                                generation,
                                castNumber);

                            AdvanceSchedule(
                                ref _nextWitheringAfflictionRealtime,
                                WitheringAfflictionRepeatSeconds);
                        }
                        else if (now >=
                                 _nextCrushingBlightRealtime)
                        {
                            int castNumber =
                                ++_crushingBlightCastIndex;

                            yield return CastCrushingBlight(
                                generation,
                                castNumber);

                            AdvanceSchedule(
                                ref _nextCrushingBlightRealtime,
                                CrushingBlightRepeatSeconds);
                        }
                        else if (now >=
                                 _nextRoarRealtime)
                        {
                            int castNumber =
                                ++_roarCastIndex;

                            yield return CastFracturingRoar(
                                generation,
                                castNumber);

                            AdvanceSchedule(
                                ref _nextRoarRealtime,
                                RepeatCastSeconds);
                        }
                    }
                }

                yield return null;
            }
        }


        private static void UpdatePhaseState()
        {
            Stats stats =
                _boss != null
                    ? _boss.GetComponent<Stats>()
                    : null;

            if (stats == null ||
                stats.CurrentMaxHP <= 0)
            {
                return;
            }

            float hpFraction =
                (float)stats.CurrentHP /
                (float)stats.CurrentMaxHP;

            float now =
                Time.realtimeSinceStartup;

            if (!_phaseTwo &&
                hpFraction <=
                    PhaseTwoThreshold)
            {
                _phaseTwo =
                    true;

                _nextConeRealtime =
                    now +
                    ConeInitialDelaySeconds;

                _nextWitheringAfflictionRealtime =
                    now +
                    WitheringAfflictionInitialDelaySeconds;

            }

            if (!_phaseThree &&
                hpFraction <=
                    PhaseThreeThreshold)
            {
                _phaseThree =
                    true;

                _nextBloomRealtime =
                    now +
                    BloomInitialDelaySeconds;

                _nextBlightedFrenzyRealtime =
                    now +
                    BlightedFrenzyInitialDelaySeconds;

            }
        }


        private static void AdvanceSchedule(
            ref float dueTime,
            float interval)
        {
            float now =
                Time.realtimeSinceStartup;

            if (dueTime <= 0f)
            {
                dueTime =
                    now +
                    interval;

                return;
            }

            do
            {
                dueTime +=
                    interval;
            }
            while (dueTime <= now);
        }


        private static IEnumerator CastCrushingBlight(
            int generation,
            int castNumber)
        {
            Character target =
                ResolveCurrentLivingTarget();

            if (!IsLivingCharacter(
                    target))
            {
                yield break;
            }

            Vector3 direction =
                target.transform.position -
                _boss.transform.position;

            direction.y =
                0f;

            if (!BeginCast(
                    generation,
                    CrushingBlightMechanicName,
                    CrushingBlightCastTimeSeconds,
                    BossCastMode.CombatCast))
            {
                yield break;
            }

            if (direction.sqrMagnitude >
                0.01f)
            {
                FaceDirection(
                    direction);
            }


            // Crushing Blight is a combat cast: native melee AI remains
            // active while the cast bar runs.
            yield return HoldCast(
                generation,
                CrushingBlightCastTimeSeconds);

            if (!FinishCastGate(
                    generation))
            {
                yield break;
            }

            ApplyNamedPhysicalDamage(
                target,
                CrushingBlightMechanicName,
                CrushingBlightDamage);

            EndCast();

        }


        private static IEnumerator CastWitheringAffliction(
            int generation,
            int castNumber)
        {
            List<Character> targets =
                SelectRandomLivingRaidTargets(
                    WitheringAfflictionTargetCount);

            if (targets.Count == 0)
            {
                yield break;
            }

            if (!BeginCast(
                    generation,
                    WitheringAfflictionMechanicName,
                    WitheringAfflictionCastTimeSeconds,
                    BossCastMode.CombatCast))
            {
                yield break;
            }


            yield return HoldCast(
                generation,
                WitheringAfflictionCastTimeSeconds);

            if (!FinishCastGate(
                    generation))
            {
                yield break;
            }

            for (int i = 0;
                 i < targets.Count;
                 i++)
            {
                ApplyWitheringAffliction(
                    targets[i]);
            }

            EndCast();

        }


        private static IEnumerator CastBlightedFrenzy(
            int generation,
            int castNumber)
        {
            if (!BeginCast(
                    generation,
                    BlightedFrenzyMechanicName,
                    BlightedFrenzyCastTimeSeconds,
                    BossCastMode.CombatCast))
            {
                yield break;
            }


            yield return HoldCast(
                generation,
                BlightedFrenzyCastTimeSeconds);

            if (!FinishCastGate(
                    generation))
            {
                yield break;
            }

            EndCast();

            ActivateBlightedFrenzy();

        }


        private static IEnumerator CastFracturingRoar(
            int generation,
            int castNumber)
        {
            if (!BeginCast(
                    generation,
                    MechanicName,
                    CastTimeSeconds,
                    BossCastMode.CombatCast))
            {
                yield break;
            }


            yield return HoldCast(
                generation,
                CastTimeSeconds);

            if (!FinishCastGate(
                    generation))
            {
                yield break;
            }

            ApplyRaidWideDamage();

            EndCast();

        }


        private static IEnumerator CastBlightedRupture(
            int generation,
            int castNumber)
        {
            Vector3 origin =
                _boss.transform.position;

            // Blighted Rupture does not track a raid member. At cast start it
            // chooses one random fixed world-space heading anywhere in the
            // boss's full 360-degree turn radius. That heading is locked for
            // the entire telegraph and impact.
            float randomHeading =
                UnityEngine.Random.Range(
                    0f,
                    360f);

            Vector3 direction =
                Quaternion.Euler(
                    0f,
                    randomHeading,
                    0f) *
                Vector3.forward;

            direction.y =
                0f;

            direction.Normalize();


            if (!BeginCast(
                    generation,
                    ConeMechanicName,
                    ConeCastTimeSeconds,
                    BossCastMode.PausedDirectionalCast))
            {
                yield break;
            }

            FaceDirection(
                direction);

            CreateConeTelegraph(
                origin,
                direction,
                ConeRange,
                ConeAngleDegrees);

            WorldBossSimAvoidanceRuntime.BeginConeAvoidance(
                _boss,
                origin,
                direction,
                ConeRange,
                ConeAngleDegrees);


            yield return HoldPausedDirectionalCast(
                generation,
                ConeCastTimeSeconds,
                direction);

            if (!FinishCastGate(
                    generation))
            {
                yield break;
            }

            ApplyConeDamage(
                origin,
                direction);

            EndCast();

        }


        private static IEnumerator CastBlightBloom(
            int generation,
            int castNumber)
        {
            List<Character> living =
                GetLivingNativeRaidCharacters();

            if (living.Count == 0)
            {
                yield break;
            }

            List<Vector3> positions =
                new List<Vector3>();

            int count =
                Mathf.Min(
                    BloomCount,
                    living.Count);

            int start =
                living.Count > 0
                    ? (castNumber - 1) %
                      living.Count
                    : 0;

            for (int i = 0;
                 i < count;
                 i++)
            {
                Character target =
                    living[
                        (start + i) %
                        living.Count];

                if (target == null)
                {
                    continue;
                }

                Vector3 position =
                    target.transform.position;

                positions.Add(
                    position);

                CreateCircleTelegraph(
                    position,
                    BloomRadius);
            }

            if (positions.Count == 0)
            {
                DestroyTelegraphs();
                yield break;
            }

            WorldBossSimAvoidanceRuntime.BeginCircleAvoidance(
                positions,
                BloomRadius);

            if (!BeginCast(
                    generation,
                    BloomMechanicName,
                    BloomCastTimeSeconds,
                    BossCastMode.CombatCast))
            {
                WorldBossSimAvoidanceRuntime.ReleaseAssignments(
                    "Blight Bloom cast could not start");
                DestroyTelegraphs();
                yield break;
            }


            yield return HoldCast(
                generation,
                BloomCastTimeSeconds);

            if (!FinishCastGate(
                    generation))
            {
                yield break;
            }

            ApplyBloomDamage(
                positions);

            EndCast();

        }


        private static bool BeginCast(
            int generation,
            string mechanicName,
            float castTime,
            BossCastMode castMode)
        {
            if (!IsCurrent(
                    generation) ||
                _castActive ||
                !IsBossAlive())
            {
                return false;
            }

            _castActive =
                true;

            if (castMode ==
                BossCastMode.PausedDirectionalCast)
            {
                PauseBossForCast();
            }
            else
            {
                // Combat casts deliberately leave the boss's native combat
                // systems untouched. NPC.Update(), aggro, NavMesh movement
                // and melee attack timers continue normally.
                RestoreBossAfterCast();
            }

            RaidBossCastBarRuntime.Show(
                mechanicName,
                castTime);


            return true;
        }


        private static IEnumerator HoldCast(
            int generation,
            float castTime)
        {
            float end =
                Time.realtimeSinceStartup +
                castTime;

            while (IsCurrent(
                       generation) &&
                   IsBossAlive() &&
                   Time.realtimeSinceStartup <
                       end)
            {
                // Standard combat cast: keep the native boss combat loop live
                // so auto-attacks and ordinary combat movement can continue.
                UpdateWitheringAfflictions();
                UpdateBlightedFrenzy();
                WorldBossSimAvoidanceRuntime.UpdateMovement();
                yield return null;
            }
        }


        private static IEnumerator HoldPausedDirectionalCast(
            int generation,
            float castTime,
            Vector3 direction)
        {
            float end =
                Time.realtimeSinceStartup +
                castTime;

            while (IsCurrent(
                       generation) &&
                   IsBossAlive() &&
                   Time.realtimeSinceStartup <
                       end)
            {
                // Blighted Rupture is intentionally the sole blocking cast.
                // Keep the boss frozen and the cone heading locked.
                HoldPausedBoss();
                FaceDirection(
                    direction);
                UpdateWitheringAfflictions();
                UpdateBlightedFrenzy();
                WorldBossSimAvoidanceRuntime.UpdateMovement();
                yield return null;
            }
        }


        private static bool FinishCastGate(
            int generation)
        {
            if (IsCurrent(
                    generation) &&
                IsBossAlive())
            {
                return true;
            }

            EndCast();
            return false;
        }


        private static void EndCast()
        {
            RaidBossCastBarRuntime.Hide();
            WorldBossSimAvoidanceRuntime.ReleaseAssignments(
                "mechanic complete");
            DestroyTelegraphs();
            RestoreBossAfterCast();

            _castActive =
                false;
        }


        private static void ApplyRaidWideDamage()
        {
            List<Character> targets =
                GetLivingNativeRaidCharacters();

            int hitCount =
                0;

            for (int i = 0;
                 i < targets.Count;
                 i++)
            {
                Character target =
                    targets[i];

                if (ApplyNamedDamage(
                        target,
                        MechanicName,
                        RaidWideDamage))
                {
                    hitCount++;
                }
            }

        }


        private static void ApplyConeDamage(
            Vector3 origin,
            Vector3 direction)
        {
            List<Character> targets =
                GetLivingNativeRaidCharacters();

            int hitCount =
                0;

            for (int i = 0;
                 i < targets.Count;
                 i++)
            {
                Character target =
                    targets[i];

                if (target == null)
                {
                    continue;
                }

                Vector3 offset =
                    target.transform.position -
                    origin;

                offset.y =
                    0f;

                float distance =
                    offset.magnitude;

                if (distance >
                        ConeRange ||
                    distance <=
                        0.01f)
                {
                    continue;
                }

                float angle =
                    Vector3.Angle(
                        direction,
                        offset.normalized);

                if (angle >
                    ConeAngleDegrees *
                    0.5f)
                {
                    continue;
                }

                if (ApplyNamedDamage(
                        target,
                        ConeMechanicName,
                        ConeDamage))
                {
                    hitCount++;
                }
            }

        }


        private static void ApplyBloomDamage(
            List<Vector3> positions)
        {
            List<Character> targets =
                GetLivingNativeRaidCharacters();

            int hitCount =
                0;

            for (int i = 0;
                 i < targets.Count;
                 i++)
            {
                Character target =
                    targets[i];

                if (target == null)
                {
                    continue;
                }

                bool inside =
                    false;

                for (int p = 0;
                     p < positions.Count;
                     p++)
                {
                    Vector3 offset =
                        target.transform.position -
                        positions[p];

                    offset.y =
                        0f;

                    if (offset.sqrMagnitude <=
                        BloomRadius *
                        BloomRadius)
                    {
                        inside =
                            true;

                        break;
                    }
                }

                if (!inside)
                {
                    continue;
                }

                if (ApplyNamedDamage(
                        target,
                        BloomMechanicName,
                        BloomDamage))
                {
                    hitCount++;
                }
            }

        }


        private static bool ApplyNamedPhysicalDamage(
            Character target,
            string sourceName,
            int damage)
        {
            if (target == null ||
                !target.Alive ||
                target.MyStats == null ||
                target.MyStats.CurrentHP <= 0)
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
                        GameData.DamageType.Physical,
                        _bossCharacter,
                        false,
                        false,
                        0);


                return resolved >
                    0;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[WorldBoss] [" +
                    sourceName +
                    "] physical damage failed for [" +
                    target.name +
                    "]: " +
                    exception);

                return false;
            }
        }


        private static List<Character> SelectRandomLivingRaidTargets(
            int requestedCount)
        {
            List<Character> pool =
                GetLivingNativeRaidCharacters();

            List<Character> result =
                new List<Character>();

            int count =
                Mathf.Min(
                    requestedCount,
                    pool.Count);

            for (int i = 0;
                 i < count;
                 i++)
            {
                if (pool.Count == 0)
                {
                    break;
                }

                int index =
                    UnityEngine.Random.Range(
                        0,
                        pool.Count);

                Character selected =
                    pool[index];

                pool.RemoveAt(
                    index);

                if (IsLivingCharacter(
                        selected))
                {
                    result.Add(
                        selected);
                }
            }

            return result;
        }


        private static string GetCharacterNames(
            List<Character> characters)
        {
            if (characters == null ||
                characters.Count == 0)
            {
                return "<none>";
            }

            string value =
                "";

            for (int i = 0;
                 i < characters.Count;
                 i++)
            {
                if (i > 0)
                {
                    value +=
                        ", ";
                }

                Character character =
                    characters[i];

                value +=
                    character != null
                        ? character.name
                        : "<null>";
            }

            return value;
        }


        private static void ApplyWitheringAffliction(
            Character target)
        {
            if (!IsLivingCharacter(
                    target) ||
                target.MyStats == null ||
                _bossCharacter == null)
            {
                return;
            }

            Spell debuff =
                GetWitheringAfflictionDebuffSpell();

            if (debuff == null)
            {
                Plugin.NativeLog.LogError(
                    "[WorldBoss] [" +
                    WitheringAfflictionMechanicName +
                    "] could not create the native debuff Spell.");
                return;
            }

            Stats stats =
                target.MyStats;

            int slot =
                -1;

            try
            {
                slot =
                    stats.AddStatusEffect(
                        debuff,
                        false,
                        0,
                        _bossCharacter,
                        WitheringAfflictionDurationSeconds);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[WorldBoss] [" +
                    WitheringAfflictionMechanicName +
                    "] native AddStatusEffect failed for [" +
                    target.name +
                    "]: " +
                    exception.Message +
                    ". Trying direct status-slot fallback.");
            }

            if (slot < 0)
            {
                slot =
                    FindWitheringAfflictionSlot(
                        stats);
            }

            if (slot < 0)
            {
                slot =
                    AddWitheringAfflictionStatusFallback(
                        stats,
                        debuff);
            }

            if (slot >= 0 &&
                slot <
                    stats.StatusEffects.Length &&
                stats.StatusEffects[slot] != null)
            {
                stats.StatusEffects[slot].Duration =
                    WitheringAfflictionDurationSeconds;
            }

            for (int i =
                     ActiveWitheringAfflictions.Count - 1;
                 i >= 0;
                 i--)
            {
                WitheringAfflictionState existing =
                    ActiveWitheringAfflictions[i];

                if (existing != null &&
                    existing.Target ==
                        target)
                {
                    ActiveWitheringAfflictions.RemoveAt(
                        i);
                }
            }

            float now =
                Time.realtimeSinceStartup;

            ActiveWitheringAfflictions.Add(
                new WitheringAfflictionState
                {
                    Target =
                        target,

                    StatusSlot =
                        slot,

                    NextTickRealtime =
                        now +
                        WitheringAfflictionTickSeconds,

                    ExpiresRealtime =
                        now +
                        WitheringAfflictionDurationSeconds
                });

        }


        private static void UpdateWitheringAfflictions()
        {
            if (ActiveWitheringAfflictions.Count == 0)
            {
                return;
            }

            float now =
                Time.realtimeSinceStartup;

            for (int i =
                     ActiveWitheringAfflictions.Count - 1;
                 i >= 0;
                 i--)
            {
                WitheringAfflictionState state =
                    ActiveWitheringAfflictions[i];

                if (state == null ||
                    !IsLivingCharacter(
                        state.Target) ||
                    state.Target.MyStats == null)
                {
                    RemoveWitheringAfflictionStatus(
                        state);

                    ActiveWitheringAfflictions.RemoveAt(
                        i);

                    continue;
                }

                if (now >=
                    state.ExpiresRealtime)
                {
                    RemoveWitheringAfflictionStatus(
                        state);


                    ActiveWitheringAfflictions.RemoveAt(
                        i);

                    continue;
                }

                RefreshWitheringAfflictionVisualDuration(
                    state,
                    state.ExpiresRealtime -
                    now);

                while (now >=
                           state.NextTickRealtime &&
                       state.NextTickRealtime <=
                           state.ExpiresRealtime)
                {
                    if (!IsLivingCharacter(
                            state.Target))
                    {
                        break;
                    }

                    ApplyNamedDamage(
                        state.Target,
                        WitheringAfflictionMechanicName,
                        WitheringAfflictionTickDamage);

                    state.NextTickRealtime +=
                        WitheringAfflictionTickSeconds;
                }
            }
        }


        private static void ClearWitheringAfflictions(
            string reason)
        {
            int count =
                ActiveWitheringAfflictions.Count;

            for (int i =
                     ActiveWitheringAfflictions.Count - 1;
                 i >= 0;
                 i--)
            {
                RemoveWitheringAfflictionStatus(
                    ActiveWitheringAfflictions[i]);
            }

            ActiveWitheringAfflictions.Clear();

            if (count > 0)
            {
            }
        }


        private static void RefreshWitheringAfflictionVisualDuration(
            WitheringAfflictionState state,
            float remainingSeconds)
        {
            if (state == null ||
                state.Target == null ||
                state.Target.MyStats == null)
            {
                return;
            }

            Stats stats =
                state.Target.MyStats;

            int slot =
                state.StatusSlot;

            if (slot < 0 ||
                slot >=
                    stats.StatusEffects.Length ||
                stats.StatusEffects[slot] == null ||
                stats.StatusEffects[slot].Effect !=
                    _witheringAfflictionDebuffSpell)
            {
                slot =
                    FindWitheringAfflictionSlot(
                        stats);

                state.StatusSlot =
                    slot;
            }

            if (slot >= 0 &&
                slot <
                    stats.StatusEffects.Length &&
                stats.StatusEffects[slot] != null &&
                stats.StatusEffects[slot].Effect ==
                    _witheringAfflictionDebuffSpell)
            {
                // Native StatusEffect.Duration is normally tick-based.
                // For this scripted boss debuff, keep it synchronized to
                // real remaining seconds so custom raid-frame duration text
                // and the underlying state stay intuitive.
                stats.StatusEffects[slot].Duration =
                    Mathf.Max(
                        0.1f,
                        remainingSeconds);
            }
        }


        private static void RemoveWitheringAfflictionStatus(
            WitheringAfflictionState state)
        {
            if (state == null ||
                state.Target == null ||
                state.Target.MyStats == null)
            {
                return;
            }

            Stats stats =
                state.Target.MyStats;

            int slot =
                state.StatusSlot;

            if (slot < 0 ||
                slot >=
                    stats.StatusEffects.Length ||
                stats.StatusEffects[slot] == null ||
                stats.StatusEffects[slot].Effect !=
                    _witheringAfflictionDebuffSpell)
            {
                slot =
                    FindWitheringAfflictionSlot(
                        stats);
            }

            if (slot < 0 ||
                slot >=
                    stats.StatusEffects.Length ||
                stats.StatusEffects[slot] == null ||
                stats.StatusEffects[slot].Effect !=
                    _witheringAfflictionDebuffSpell)
            {
                return;
            }

            try
            {
                stats.RemoveStatusEffect(
                    slot);
            }
            catch
            {
                // The native tick can expire the same slot during this frame.
            }
        }


        private static int FindWitheringAfflictionSlot(
            Stats stats)
        {
            if (stats == null ||
                stats.StatusEffects == null ||
                _witheringAfflictionDebuffSpell == null)
            {
                return -1;
            }

            for (int i = 0;
                 i < stats.StatusEffects.Length;
                 i++)
            {
                StatusEffect effect =
                    stats.StatusEffects[i];

                if (effect != null &&
                    effect.Effect ==
                        _witheringAfflictionDebuffSpell)
                {
                    return i;
                }
            }

            return -1;
        }


        private static int AddWitheringAfflictionStatusFallback(
            Stats stats,
            Spell debuff)
        {
            if (stats == null ||
                stats.StatusEffects == null ||
                debuff == null)
            {
                return -1;
            }

            for (int i = 0;
                 i < stats.StatusEffects.Length;
                 i++)
            {
                StatusEffect status =
                    stats.StatusEffects[i];

                if (status == null ||
                    status.Effect != null)
                {
                    continue;
                }

                status.Effect =
                    debuff;

                status.Duration =
                    WitheringAfflictionDurationSeconds;

                status.Owner =
                    _bossCharacter;

                status.CreditDPS =
                    _bossCharacter;

                status.fromPlayer =
                    false;

                status.CastedByPC =
                    false;

                status.bonusDmg =
                    0;

                try
                {
                    stats.CalcStats();
                }
                catch
                {
                }

                return i;
            }

            return -1;
        }


        private static Spell GetWitheringAfflictionDebuffSpell()
        {
            if (_witheringAfflictionDebuffSpell != null)
            {
                return
                    _witheringAfflictionDebuffSpell;
            }

            Spell spell =
                null;

            try
            {
                spell =
                    ScriptableObject.CreateInstance<Spell>();
            }
            catch
            {
            }

            if (spell == null)
            {
                Plugin.NativeLog.LogError(
                    "[WorldBoss] failed to create Withering Affliction Spell object.");

                return null;
            }

            spell.name =
                "FOTF - Withering Affliction";

            spell.Id =
                WitheringAfflictionSpellId;

            spell.SpellName =
                WitheringAfflictionMechanicName;

            spell.SpellDesc =
                "A wasting blight gnaws at the victim, dealing damage every 2 seconds.";

            spell.Type =
                Spell.SpellType.StatusEffect;

            spell.Line =
                Spell.SpellLine.Generic;

            spell.RequiredLevel =
                1;

            spell.ManaCost =
                0;

            spell.TargetDamage =
                0;

            spell.TargetHealing =
                0;

            spell.Aggro =
                0;

            spell.SpellChargeTime =
                0f;

            spell.Cooldown =
                0f;

            spell.SpellDurationInTicks =
                Mathf.CeilToInt(
                    WitheringAfflictionDurationSeconds);

            spell.StatusEffectToApply =
                null;

            spell.MyDamageType =
                GameData.DamageType.Magic;

            spell.SpellRange =
                100f;

            spell.SelfOnly =
                false;

            spell.CrowdControlSpell =
                false;

            spell.SimUsable =
                false;

            spell.NoResonate =
                true;

            spell.CanHitPlayers =
                true;

            spell.RootTarget =
                false;

            spell.StunTarget =
                false;

            spell.FearTarget =
                false;

            spell.CharmTarget =
                false;

            spell.BreakOnDamage =
                false;

            spell.UnstableDuration =
                false;

            spell.ShieldingAmt =
                0;

            spell.Mana =
                0;

            spell.LevelScaledManaRestoration =
                0f;

            spell.PercentManaRestoration =
                0;

            spell.BleedDamagePercent =
                0;

            spell.ReapAndRenew =
                false;

            spell.WornEffect =
                false;

            spell.UsedBy =
                new List<Class>();

            Sprite customIcon =
                LoadWitheringAfflictionIcon();

            if (customIcon != null)
            {
                ApplySpellSprite(
                    spell,
                    customIcon);
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "[WorldBoss] [" +
                    WitheringAfflictionMechanicName +
                    "] custom icon could not be loaded. Group Frames will fall back to its text indicator.");
            }

            _witheringAfflictionDebuffSpell =
                spell;


            return
                _witheringAfflictionDebuffSpell;
        }


        private static Sprite LoadWitheringAfflictionIcon()
        {
            if (_witheringAfflictionIconSprite != null)
            {
                return
                    _witheringAfflictionIconSprite;
            }

            try
            {
                Assembly assembly =
                    typeof(WorldBossEncounterRuntime).Assembly;

                using (Stream stream =
                       assembly.GetManifestResourceStream(
                           WitheringAfflictionIconResourceName))
                {
                    if (stream == null)
                    {
                        Plugin.NativeLog.LogError(
                            "[WorldBoss] [" +
                            WitheringAfflictionMechanicName +
                            "] embedded icon resource was not found: [" +
                            WitheringAfflictionIconResourceName +
                            "].");

                        try
                        {
                            string[] available =
                                assembly.GetManifestResourceNames();

                            Plugin.NativeLog.LogWarning(
                                "[WorldBoss] available manifest resources: " +
                                (available != null
                                    ? string.Join(
                                        ", ",
                                        available)
                                    : "<null>"));
                        }
                        catch
                        {
                        }

                        return null;
                    }

                    byte[] bytes;

                    using (MemoryStream memory =
                           new MemoryStream())
                    {
                        stream.CopyTo(
                            memory);

                        bytes =
                            memory.ToArray();
                    }

                    Texture2D texture =
                        new Texture2D(
                            2,
                            2,
                            TextureFormat.RGBA32,
                            false);

                    texture.name =
                        "FOTF_Withering_Affliction_Texture";

                    texture.filterMode =
                        FilterMode.Bilinear;

                    texture.wrapMode =
                        TextureWrapMode.Clamp;

                    if (!ImageConversion.LoadImage(
                            texture,
                            bytes,
                            false))
                    {
                        UnityEngine.Object.Destroy(
                            texture);

                        Plugin.NativeLog.LogError(
                            "[WorldBoss] [" +
                            WitheringAfflictionMechanicName +
                            "] Unity failed to decode embedded icon PNG.");

                        return null;
                    }

                    _witheringAfflictionIconSprite =
                        Sprite.Create(
                            texture,
                            new Rect(
                                0f,
                                0f,
                                texture.width,
                                texture.height),
                            new Vector2(
                                0.5f,
                                0.5f),
                            64f);

                    _witheringAfflictionIconSprite.name =
                        "FOTF_Withering_Affliction";


                    return
                        _witheringAfflictionIconSprite;
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[WorldBoss] [" +
                    WitheringAfflictionMechanicName +
                    "] failed to load custom icon: " +
                    exception);

                return null;
            }
        }


        private static void ApplySpellSprite(
            Spell spell,
            Sprite sprite)
        {
            if (spell == null ||
                sprite == null)
            {
                return;
            }

            try
            {
                FieldInfo[] fields =
                    typeof(Spell).GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                int assigned =
                    0;

                for (int i = 0;
                     i < fields.Length;
                     i++)
                {
                    FieldInfo field =
                        fields[i];

                    if (field == null ||
                        field.FieldType !=
                            typeof(Sprite))
                    {
                        continue;
                    }

                    field.SetValue(
                        spell,
                        sprite);

                    assigned++;
                }

            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[WorldBoss] [" +
                    WitheringAfflictionMechanicName +
                    "] failed assigning custom Spell sprite: " +
                    exception);
            }
        }


        private static void ActivateBlightedFrenzy()
        {
            RestoreBlightedFrenzy(
                "refresh");

            if (_boss == null)
            {
                return;
            }

            Stats stats =
                _boss.GetComponent<Stats>();

            if (stats == null)
            {
                return;
            }

            _savedFrenzyBaseAttackDamage =
                _boss.BaseAtkDmg;

            _savedFrenzyDamageRange =
                _boss.DamageRange;

            _savedFrenzyBaseMhDelay =
                stats.BaseMHAtkDelay;

            _boss.BaseAtkDmg =
                Mathf.RoundToInt(
                    _savedFrenzyBaseAttackDamage *
                    BlightedFrenzyDamageMultiplier);

            _boss.DamageRange =
                new Vector2(
                    _savedFrenzyDamageRange.x *
                    BlightedFrenzyDamageMultiplier,
                    _savedFrenzyDamageRange.y *
                    BlightedFrenzyDamageMultiplier);

            stats.BaseMHAtkDelay =
                _savedFrenzyBaseMhDelay /
                BlightedFrenzyAttackSpeedMultiplier;

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

            _frenzyAnimator =
                _boss.GetComponent<Animator>();

            _frenzyAnimatorSpeedCaptured =
                false;

            if (_frenzyAnimator != null &&
                HasAnimatorParameter(
                    _frenzyAnimator,
                    "AttackSpeed"))
            {
                try
                {
                    _savedFrenzyAnimatorAttackSpeed =
                        _frenzyAnimator.GetFloat(
                            "AttackSpeed");

                    _frenzyAnimatorSpeedCaptured =
                        true;

                    float baseSpeed =
                        Mathf.Max(
                            1f,
                            _savedFrenzyAnimatorAttackSpeed);

                    _frenzyAnimator.SetFloat(
                        "AttackSpeed",
                        baseSpeed *
                        BlightedFrenzyAttackSpeedMultiplier);
                }
                catch
                {
                    _frenzyAnimatorSpeedCaptured =
                        false;
                }
            }

            _blightedFrenzyEndsRealtime =
                Time.realtimeSinceStartup +
                BlightedFrenzyDurationSeconds;

            _blightedFrenzyActive =
                true;

        }


        private static void UpdateBlightedFrenzy()
        {
            if (!_blightedFrenzyActive)
            {
                return;
            }

            if (!IsBossAlive() ||
                Time.realtimeSinceStartup >=
                    _blightedFrenzyEndsRealtime)
            {
                RestoreBlightedFrenzy(
                    !IsBossAlive()
                        ? "boss death"
                        : "duration complete");
            }
        }


        private static void RestoreBlightedFrenzy(
            string reason)
        {
            if (!_blightedFrenzyActive)
            {
                return;
            }

            NPC boss =
                _boss;

            Stats stats =
                boss != null
                    ? boss.GetComponent<Stats>()
                    : null;

            if (boss != null)
            {
                boss.BaseAtkDmg =
                    _savedFrenzyBaseAttackDamage;

                boss.DamageRange =
                    _savedFrenzyDamageRange;
            }

            if (stats != null)
            {
                stats.BaseMHAtkDelay =
                    _savedFrenzyBaseMhDelay;

                try
                {
                    stats.CalcStats();
                }
                catch
                {
                }
            }

            if (_frenzyAnimator != null &&
                _frenzyAnimatorSpeedCaptured)
            {
                try
                {
                    _frenzyAnimator.SetFloat(
                        "AttackSpeed",
                        _savedFrenzyAnimatorAttackSpeed);
                }
                catch
                {
                }
            }

            _frenzyAnimator =
                null;

            _frenzyAnimatorSpeedCaptured =
                false;

            _blightedFrenzyActive =
                false;

            _blightedFrenzyEndsRealtime =
                0f;

        }


        private static bool HasAnimatorParameter(
            Animator animator,
            string parameterName)
        {
            if (animator == null ||
                string.IsNullOrEmpty(
                    parameterName))
            {
                return false;
            }

            AnimatorControllerParameter[] parameters =
                animator.parameters;

            if (parameters == null)
            {
                return false;
            }

            for (int i = 0;
                 i < parameters.Length;
                 i++)
            {
                if (parameters[i] != null &&
                    parameters[i].name ==
                        parameterName)
                {
                    return true;
                }
            }

            return false;
        }


        private static bool ApplyNamedDamage(
            Character target,
            string sourceName,
            int damage)
        {
            if (target == null ||
                !target.Alive ||
                target.MyStats == null ||
                target.MyStats.CurrentHP <= 0)
            {
                return false;
            }

            try
            {
                RaidMechanicDamageAttribution.SetNamedSource(
                    _bossCharacter,
                    sourceName);

                // Scripted mechanics use the normal Character.DamageMe boundary with
                // Magic damage. Poison was resolving to zero against every
                // player/Sim target in the open-world boss runtime. Ability
                // naming is provided separately by Combat Meter attribution.
                int resolved =
                    target.DamageMe(
                        damage,
                        false,
                        GameData.DamageType.Magic,
                        _bossCharacter,
                        false,
                        false,
                        0);


                return resolved >
                    0;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[WorldBoss] [" +
                    sourceName +
                    "] damage failed for [" +
                    target.name +
                    "]: " +
                    exception);

                return false;
            }
        }


        private static Character ResolveCurrentLivingTarget()
        {
            Character target =
                _boss != null
                    ? _boss.CurrentAggroTarget
                    : null;

            if (IsLivingCharacter(
                    target))
            {
                return target;
            }

            List<Character> living =
                GetLivingNativeRaidCharacters();

            Character nearest =
                null;

            float nearestDistance =
                float.PositiveInfinity;

            for (int i = 0;
                 i < living.Count;
                 i++)
            {
                Character candidate =
                    living[i];

                if (candidate == null ||
                    _boss == null)
                {
                    continue;
                }

                float distance =
                    Vector3.Distance(
                        _boss.transform.position,
                        candidate.transform.position);

                if (distance <
                    nearestDistance)
                {
                    nearest =
                        candidate;

                    nearestDistance =
                        distance;
                }
            }

            return nearest;
        }


        private static void FaceDirection(
            Vector3 direction)
        {
            if (_boss == null ||
                direction.sqrMagnitude <
                    0.01f)
            {
                return;
            }

            direction.y =
                0f;

            _boss.transform.rotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up);
        }


        private static void CreateConeTelegraph(
            Vector3 origin,
            Vector3 direction,
            float range,
            float angleDegrees)
        {
            GameObject telegraph =
                new GameObject(
                    "FOTF_WorldBoss_ConeTelegraph");

            MeshFilter filter =
                telegraph.AddComponent<MeshFilter>();

            MeshRenderer renderer =
                telegraph.AddComponent<MeshRenderer>();

            Mesh mesh =
                new Mesh();

            const int angularSegments =
                28;

            const int radialSegments =
                8;

            int ringWidth =
                angularSegments +
                1;

            int vertexCount =
                1 +
                radialSegments *
                ringWidth;

            Vector3[] vertices =
                new Vector3[
                    vertexCount];

            List<int> triangles =
                new List<int>();

            Vector3 planarDirection =
                direction;

            planarDirection.y =
                0f;

            if (planarDirection.sqrMagnitude <
                0.0001f)
            {
                planarDirection =
                    Vector3.forward;
            }

            planarDirection.Normalize();

            Quaternion facing =
                Quaternion.LookRotation(
                    planarDirection,
                    Vector3.up);

            Vector3 projectedCenter =
                ProjectTelegraphPointToGround(
                    origin);

            vertices[0] =
                projectedCenter +
                Vector3.up *
                0.08f;

            float half =
                angleDegrees *
                0.5f;

            for (int ring = 1;
                 ring <= radialSegments;
                 ring++)
            {
                float ringDistance =
                    range *
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
                    float t =
                        (float)a /
                        angularSegments;

                    float angle =
                        Mathf.Lerp(
                            -half,
                            half,
                            t) *
                        Mathf.Deg2Rad;

                    Vector3 local =
                        new Vector3(
                            Mathf.Sin(
                                angle) *
                            ringDistance,
                            0f,
                            Mathf.Cos(
                                angle) *
                            ringDistance);

                    Vector3 world =
                        origin +
                        facing *
                        local;

                    Vector3 projected =
                        ProjectTelegraphPointToGround(
                            world);

                    vertices[
                        ringStart +
                        a] =
                        projected +
                        Vector3.up *
                        0.08f;
                }
            }

            // Center fan.
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

            // Remaining radial strips.
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
                        innerStart +
                        a +
                        1;

                    int o0 =
                        outerStart +
                        a;

                    int o1 =
                        outerStart +
                        a +
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

            mesh.vertices =
                vertices;

            mesh.triangles =
                triangles.ToArray();

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            filter.sharedMesh =
                mesh;

            renderer.material =
                CreateTelegraphMaterial(
                    new Color(
                        0.90f,
                        0.08f,
                        0.08f,
                        0.42f));

            // Vertices are generated in world coordinates so the telegraph
            // can conform independently to every terrain sample.
            telegraph.transform.position =
                Vector3.zero;

            telegraph.transform.rotation =
                Quaternion.identity;

            TelegraphObjects.Add(
                telegraph);
        }


        private static void CreateCircleTelegraph(
            Vector3 position,
            float radius)
        {
            GameObject telegraph =
                new GameObject(
                    "FOTF_WorldBoss_BloomTelegraph");

            MeshFilter filter =
                telegraph.AddComponent<MeshFilter>();

            MeshRenderer renderer =
                telegraph.AddComponent<MeshRenderer>();

            Mesh mesh =
                new Mesh();

            const int angularSegments =
                32;

            const int radialSegments =
                7;

            int ringWidth =
                angularSegments +
                1;

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
                Vector3.up *
                0.06f;

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
                        0.06f;
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
                        innerStart +
                        a +
                        1;

                    int o0 =
                        outerStart +
                        a;

                    int o1 =
                        outerStart +
                        a +
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

            mesh.vertices =
                vertices;

            mesh.triangles =
                triangles.ToArray();

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            filter.sharedMesh =
                mesh;

            renderer.material =
                CreateTelegraphMaterial(
                    new Color(
                        0.90f,
                        0.08f,
                        0.08f,
                        0.38f));

            telegraph.transform.position =
                Vector3.zero;

            TelegraphObjects.Add(
                telegraph);
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

                    if (collider.GetComponentInParent<Character>() != null ||
                        collider.GetComponentInParent<NPC>() != null)
                    {
                        continue;
                    }

                    return hits[i].point;
                }
            }

            // If there is no ordinary terrain/mesh collider here, use the
            // NavMesh as a visual-height fallback.
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


        private static Material CreateTelegraphMaterial(
            Color color)
        {
            Shader shader =
                Shader.Find(
                    "Standard");

            Material material =
                new Material(
                    shader);

            material.color =
                color;

            material.SetFloat(
                "_Mode",
                3f);

            material.SetInt(
                "_SrcBlend",
                (int)UnityEngine.Rendering.BlendMode.SrcAlpha);

            material.SetInt(
                "_DstBlend",
                (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            material.SetInt(
                "_ZWrite",
                0);

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


        private static void DestroyTelegraphs()
        {
            for (int i = 0;
                 i < TelegraphObjects.Count;
                 i++)
            {
                GameObject telegraph =
                    TelegraphObjects[i];

                if (telegraph != null)
                {
                    UnityEngine.Object.Destroy(
                        telegraph);
                }
            }

            TelegraphObjects.Clear();
        }


        private static bool ShouldLeashReset()
        {
            NPC boss =
                _boss;

            if (boss == null)
            {
                return false;
            }

            WorldBossDefinition definition =
                WorldBossCatalog.BlightTouchedColossus;

            float distanceFromHome =
                Vector3.Distance(
                    boss.transform.position,
                    _homePosition);

            if (distanceFromHome >
                definition.LeashRadius)
            {

                return true;
            }

            if (HasLivingNativeRaidCharacterWithin(
                    boss.transform.position,
                    definition.LeashRadius))
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

            float absentFor =
                Time.realtimeSinceStartup -
                _noTargetSinceRealtime;

            if (absentFor <
                definition.ResetNoTargetSeconds)
            {
                return false;
            }

            return true;
        }


        private static bool HasLivingNativeRaidCharacterWithin(
            Vector3 center,
            float radius)
        {
            float radiusSqr =
                radius *
                radius;

            List<Character> characters =
                GetLivingNativeRaidCharacters();

            for (int i = 0;
                 i < characters.Count;
                 i++)
            {
                Character character =
                    characters[i];

                if (character != null &&
                    (character.transform.position -
                     center).sqrMagnitude <=
                    radiusSqr)
                {
                    return true;
                }
            }

            return false;
        }


        private static List<Character> GetLivingNativeRaidCharacters()
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

            RaidManager manager =
                GameData.RaidManager;

            if (manager != null &&
                manager.Raiders != null)
            {
                foreach (RaidMemberSlot slot in
                         manager.Raiders)
                {
                    SimPlayer sim =
                        slot != null
                            ? slot.AssignedAvatar
                            : null;

                    if (sim == null ||
                        !sim.InRaid)
                    {
                        continue;
                    }

                    NPC npc =
                        sim.GetThisNPC();

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


        private static void AddLivingCharacter(
            List<Character> result,
            Character character)
        {
            if (!IsLivingCharacter(
                    character))
            {
                return;
            }

            for (int i = 0;
                 i < result.Count;
                 i++)
            {
                if (result[i] ==
                    character)
                {
                    return;
                }
            }

            result.Add(
                character);
        }


        private static bool IsLivingCharacter(
            Character character)
        {
            return character != null &&
                character.Alive &&
                character.MyStats != null &&
                character.MyStats.CurrentHP >
                    0;
        }


        private static void PauseBossForCast()
        {
            RestoreBossAfterCast();

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

            _pausedBoss.CurrentAggroTarget =
                null;

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
                }
            }
        }


        private static void RestoreBossAfterCast()
        {
            NPC boss =
                _pausedBoss;

            if (boss == null)
            {
                _pausedBoss =
                    null;

                _pausedAgent =
                    null;

                _savedAggroTarget =
                    null;

                return;
            }

            try
            {
                boss.enabled =
                    _savedNpcEnabled;

                if (_pausedAgent != null &&
                    _pausedAgent.enabled &&
                    _pausedAgent.isOnNavMesh)
                {
                    _pausedAgent.isStopped =
                        _savedAgentStopped;
                }

                if (_savedAggroTarget != null &&
                    _savedAggroTarget.Alive)
                {
                    boss.CurrentAggroTarget =
                        _savedAggroTarget;
                }
            }
            catch
            {
            }

            _pausedBoss =
                null;

            _pausedAgent =
                null;

            _savedAggroTarget =
                null;
        }


        private static bool IsBossAlive()
        {
            NPC boss =
                _boss;

            if (boss == null)
            {
                return false;
            }

            Stats stats =
                boss.GetComponent<Stats>();

            return _bossCharacter != null &&
                _bossCharacter.Alive &&
                stats != null &&
                stats.CurrentHP >
                    0;
        }


        private static bool IsCurrent(
            int generation)
        {
            return generation ==
                    _generation &&
                _boss != null;
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
    internal static class Patch_WorldBossCombatClockDamage
    {
        private static void Prefix(
            Character __instance,
            int _incdmg)
        {
            WorldBossEncounterRuntime.NotifyBossDamaged(
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
    internal static class Patch_WorldBossCombatClockMagicDamage
    {
        private static void Prefix(
            Character __instance,
            int _dmg)
        {
            WorldBossEncounterRuntime.NotifyBossDamaged(
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
    internal static class Patch_WorldBossCombatClockBleedDamage
    {
        private static void Prefix(
            Character __instance,
            int _incdmg)
        {
            WorldBossEncounterRuntime.NotifyBossDamaged(
                __instance,
                _incdmg);
        }
    }
}
