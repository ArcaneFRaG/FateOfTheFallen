using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Runtime for Emberweaver's learned class mechanics and their Ascension
    /// interactions. Heat / Flashfire / Wildfire remain gated by their learned
    /// Skill entries; the class-specific spell critical floor is innate.
    /// </summary>
    internal static class EmberweaverPassiveRuntime
    {
        internal const int MaxHeat = 10;
        internal const float DamagePerHeat = 0.03f;
        internal const float HeatGraceSeconds = 3f;
        internal const float HeatDecaySeconds = 2f;

        internal const float BaseSpellCriticalChance = 0.08f;
        internal const float BaseSpellCriticalDamageMultiplier = 1.50f;

        internal const float WildfireBaseShare = 0.35f;
        internal const float WildfireCriticalShare = 0.45f;
        internal const float WildfireTickSeconds = 1.5f;
        internal const float WildfireTickFraction = 0.10f;
        internal const float WildfireBurnoutDelaySeconds = 6f;
        internal const float WildfireFirstBurnoutFraction = 0.50f;
        internal const float WildfireCarrierDurationTicks = 999f;

        private sealed class PendingCast
        {
            internal Spell Spell;
            internal Character Target;
            internal float Time;
        }

        private sealed class WildfireState
        {
            internal Character Target;
            internal Stats Stats;
            internal float Pool;
            internal float PendingPulse;
            internal float LastDepositTime;
            internal float NextTickTime;
            internal int BurnoutStage;
        }

        private static readonly List<PendingCast> Pending =
            new List<PendingCast>();

        private static readonly Dictionary<Character, WildfireState> Wildfires =
            new Dictionary<Character, WildfireState>();

        private static int _heat;
        private static float _lastDirectHit = -999f;
        private static float _nextHeatDecay;
        private static bool _flashfireReady;
        private static int _rekindlingCharges;

        private static EmberweaverSpellDefinition _activeDamageDefinition;
        private static Character _activeDamageTarget;
        private static bool _activeDamageWasCritical;
        private static bool _activeRekindlingEmpowered;

        internal static int HeatStacks
        {
            get { return _heat; }
        }

        internal static bool FlashfireReady
        {
            get { return _flashfireReady; }
        }

        internal static float CurrentDamagePerHeat
        {
            get
            {
                UseSkill skills =
                    GetPlayerSkills();

                return skills != null
                    ? EmberweaverAscensions.GetHeatDamagePerStack(skills)
                    : DamagePerHeat;
            }
        }

        internal static float CurrentSpellCriticalChance
        {
            get
            {
                UseSkill skills =
                    GetPlayerSkills();

                return skills != null
                    ? EmberweaverAscensions.GetSpellCriticalChance(skills)
                    : BaseSpellCriticalChance;
            }
        }

        /// <summary>
        /// Returns the outstanding Wildfire reservoir on the player's current
        /// target. Pending Scorch acceleration is included because Combust
        /// folds that queued pulse back into the reservoir before detonating.
        /// The projected Combust value therefore matches the amount the spell
        /// would cash out at the player's current Heat, subject only to the
        /// target still being alive when the cast resolves.
        /// </summary>
        internal static bool TryGetCurrentTargetWildfire(
            out float storedDamage,
            out float combustFraction,
            out int projectedCombustDamage)
        {
            storedDamage = 0f;
            combustFraction = 0f;
            projectedCombustDamage = 0;

            if (GameData.PlayerControl == null)
            {
                return false;
            }

            Character target =
                GameData.PlayerControl.CurrentTarget;

            WildfireState state;

            if (target == null ||
                !Wildfires.TryGetValue(
                    target,
                    out state) ||
                !IsValidWildfireState(state))
            {
                return false;
            }

            storedDamage =
                GetOutstandingWildfire(
                    state);

            if (_heat < 5 ||
                storedDamage <= 0.01f)
            {
                return storedDamage > 0.01f;
            }

            combustFraction =
                Mathf.Clamp01(
                    _heat * 0.10f);

            projectedCombustDamage =
                Mathf.Clamp(
                    Mathf.RoundToInt(
                        storedDamage *
                        combustFraction),
                    1,
                    Mathf.CeilToInt(
                        storedDamage));

            return true;
        }

        internal static float SecondsUntilNextHeatDecay
        {
            get
            {
                if (_heat <= 0)
                {
                    return 0f;
                }

                return Mathf.Max(
                    0f,
                    _nextHeatDecay - Time.unscaledTime);
            }
        }

        internal static void Shutdown()
        {
            ResetTransient();
        }

        internal static void Update()
        {
            CleanupPending();

            if (!EmberweaverCatalog.IsCurrentPlayerEmberweaver())
            {
                ResetTransient();
                return;
            }

            if (_flashfireReady)
            {
                EmberweaverFlashfireCooldownRuntime
                    .MaintainFlashfireReadyState();
            }

            UpdateHeat();
            UpdateWildfires();
        }

        internal static void TrackSuccessfulCast(
            Spell spell,
            Stats targetStats)
        {
            EmberweaverSpellDefinition definition;

            if (!EmberweaverSpellCatalog.TryGetDefinition(
                    spell,
                    out definition) ||
                definition == null ||
                definition.IsCombust ||
                definition.Damage <= 0)
            {
                return;
            }

            Pending.Add(
                new PendingCast
                {
                    Spell = spell,
                    Target = targetStats != null
                        ? targetStats.Myself
                        : null,
                    Time = Time.unscaledTime
                });
        }

        internal static bool TryMatchDirectDamage(
            Character target,
            Character attacker,
            out EmberweaverSpellDefinition definition)
        {
            definition = null;

            if (!EmberweaverCatalog.IsCurrentPlayerEmberweaver() ||
                GameData.PlayerStats == null ||
                GameData.PlayerStats.Myself == null ||
                attacker != GameData.PlayerStats.Myself)
            {
                return false;
            }

            float now =
                Time.unscaledTime;

            for (int i = Pending.Count - 1;
                 i >= 0;
                 i--)
            {
                PendingCast pending =
                    Pending[i];

                if (pending == null ||
                    now - pending.Time > 4f)
                {
                    Pending.RemoveAt(i);
                    continue;
                }

                if (pending.Target != null &&
                    target != null &&
                    pending.Target != target)
                {
                    continue;
                }

                if (EmberweaverSpellCatalog.TryGetDefinition(
                        pending.Spell,
                        out definition) &&
                    definition != null &&
                    !definition.IsCombust &&
                    definition.Damage > 0)
                {
                    Pending.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        internal static void BeginDirectDamage(
            EmberweaverSpellDefinition definition,
            Character target)
        {
            _activeDamageDefinition =
                definition;

            _activeDamageTarget =
                target;

            _activeRekindlingEmpowered =
                definition != null &&
                !definition.IsCombust &&
                _rekindlingCharges > 0;

            _activeDamageWasCritical =
                definition != null &&
                !definition.IsCombust &&
                UnityEngine.Random.Range(0f, 1f) <
                CurrentSpellCriticalChance;
        }

        internal static void EndDirectDamage()
        {
            _activeDamageDefinition = null;
            _activeDamageTarget = null;
            _activeDamageWasCritical = false;
            _activeRekindlingEmpowered = false;
        }

        internal static int ApplyDirectDamageModifiers(
            int incoming,
            EmberweaverSpellDefinition definition)
        {
            if (incoming <= 0 ||
                definition == null ||
                definition.IsCombust)
            {
                return incoming;
            }

            float multiplier =
                1f;

            if (EmberweaverSkillCatalog.HasPlayerSkill(
                    EmberweaverSkillCatalog.HeatId) &&
                _heat > 0)
            {
                multiplier *=
                    1f +
                    (_heat * CurrentDamagePerHeat);
            }

            UseSkill skills =
                GetPlayerSkills();

            if (definition.IsScorchFamily &&
                skills != null)
            {
                multiplier *=
                    EmberweaverAscensions
                        .GetControlledBurnDamageMultiplier(
                            skills);
            }

            if (_activeRekindlingEmpowered &&
                skills != null)
            {
                int rank =
                    EmberweaverAscensions
                        .GetRekindlingRank(
                            skills);

                if (rank > 0)
                {
                    multiplier *=
                        1f +
                        (rank * 0.10f);
                }
            }

            if (_activeDamageWasCritical)
            {
                multiplier *=
                    skills != null
                        ? EmberweaverAscensions
                            .GetSpellCriticalDamageMultiplier(
                                skills)
                        : BaseSpellCriticalDamageMultiplier;
            }

            // Wildfire transfers a percentage of the outgoing spell hit into
            // the rolling damage reservoir. Previously the hit stayed at 100%
            // and Wildfire created extra damage on top, greatly over-scaling
            // Emberweaver at level 35 with raid gear.
            if (_activeDamageTarget != null &&
                EmberweaverSpellCatalog.WildfireEffectSpell != null &&
                EmberweaverSkillCatalog.HasPlayerSkill(
                    EmberweaverSkillCatalog.WildfireId))
            {
                multiplier *= EmberweaverWildfireDamageSplit.ImmediateFraction(
                    GetWildfireShare(definition, _activeDamageWasCritical));
            }

            return Mathf.Max(
                0,
                Mathf.RoundToInt(
                    incoming * multiplier));
        }

        // Kept as a compatibility helper for any older call sites. New direct
        // damage code should use ApplyDirectDamageModifiers so Ascensions and
        // the spell-critical floor are included in the same calculation.
        internal static int ApplyHeatDamageModifier(
            int incoming)
        {
            if (incoming <= 0 ||
                !EmberweaverSkillCatalog.HasPlayerSkill(
                    EmberweaverSkillCatalog.HeatId) ||
                _heat <= 0)
            {
                return incoming;
            }

            return Mathf.Max(
                0,
                Mathf.RoundToInt(
                    incoming *
                    (1f +
                     (_heat * CurrentDamagePerHeat))));
        }

        internal static void MarkCurrentReduceHpCritical(
            Stats targetStats,
            ref bool isCritical)
        {
            if (!_activeDamageWasCritical ||
                _activeDamageDefinition == null ||
                targetStats == null ||
                targetStats.Myself == null ||
                targetStats.Myself != _activeDamageTarget)
            {
                return;
            }

            // Character.MagicDamageMe passes false to ReduceHP in native
            // Erenshor. Emberweaver owns its spell-crit roll, so surface that
            // real result to the native popup path here.
            isCritical =
                true;
        }

        internal static void OnDirectDamageApplied(
            Character target,
            EmberweaverSpellDefinition definition,
            int finalDamage)
        {
            if (definition == null ||
                definition.IsCombust ||
                finalDamage <= 0)
            {
                return;
            }

            if (EmberweaverSkillCatalog.HasPlayerSkill(
                    EmberweaverSkillCatalog.HeatId))
            {
                int gain =
                    definition.GeneratesTwoHeat
                        ? 2
                        : 1;

                if (_activeDamageWasCritical)
                {
                    gain++;
                }

                if (_activeRekindlingEmpowered)
                {
                    gain++;
                }

                AddHeat(
                    gain);
            }

            if (_activeDamageWasCritical &&
                EmberweaverSkillCatalog.HasPlayerSkill(
                    EmberweaverSkillCatalog.FlashfireId) &&
                !_flashfireReady)
            {
                _flashfireReady =
                    true;

                // Flashfire is a true proc transition: when it first becomes
                // ready, every currently bound Pyroclasm-family rank is made
                // immediately available. Further criticals while the proc is
                // already waiting do not repeatedly reset the cooldown.
                EmberweaverFlashfireCooldownRuntime
                    .ResetPyroclasmCooldowns();
            }

            if (EmberweaverSkillCatalog.HasPlayerSkill(
                    EmberweaverSkillCatalog.WildfireId) &&
                target != null)
            {
                AddToWildfirePool(
                    target,
                    finalDamage,
                    definition,
                    _activeDamageWasCritical);
            }

            if (_activeRekindlingEmpowered &&
                _rekindlingCharges > 0)
            {
                _rekindlingCharges--;
            }
        }

        internal static bool ShouldInstantPyroclasm(
            Spell spell)
        {
            EmberweaverSpellDefinition definition;

            return _flashfireReady &&
                   EmberweaverSkillCatalog.HasPlayerSkill(
                       EmberweaverSkillCatalog.FlashfireId) &&
                   EmberweaverSpellCatalog.TryGetDefinition(
                       spell,
                       out definition) &&
                   definition != null &&
                   definition.IsPyroclasm;
        }

        internal static void ConsumeFlashfireIfSuccessful(
            Spell spell,
            bool success)
        {
            if (success &&
                ShouldInstantPyroclasm(spell))
            {
                _flashfireReady =
                    false;

                EmberweaverFlashfireCooldownRuntime
                    .RestoreNormalPyroclasmCooldowns();
            }
        }

        internal static bool CanCastCombust(
            Stats targetStats,
            out string denial)
        {
            denial =
                null;

            if (_heat < 5)
            {
                denial =
                    "Combust requires at least 5 Heat.";

                return false;
            }

            if (!EmberweaverSkillCatalog.HasPlayerSkill(
                    EmberweaverSkillCatalog.WildfireId))
            {
                denial =
                    "Combust requires the Wildfire skill.";

                return false;
            }

            Character target =
                targetStats != null
                    ? targetStats.Myself
                    : null;

            WildfireState state;

            if (target == null ||
                !Wildfires.TryGetValue(
                    target,
                    out state) ||
                state == null ||
                GetOutstandingWildfire(state) <= 0.01f)
            {
                denial =
                    "That target has no Wildfire to combust.";

                return false;
            }

            return true;
        }

        internal static void ExecuteCombust(
            Stats targetStats)
        {
            Character target =
                targetStats != null
                    ? targetStats.Myself
                    : null;

            WildfireState state;

            if (target == null ||
                !Wildfires.TryGetValue(
                    target,
                    out state) ||
                state == null)
            {
                return;
            }

            int consumedHeat =
                Mathf.Clamp(
                    _heat,
                    0,
                    MaxHeat);

            if (consumedHeat < 5)
            {
                return;
            }

            float outstanding =
                GetOutstandingWildfire(
                    state);

            if (outstanding <= 0.01f)
            {
                return;
            }

            // A pending Scorch pulse is still stored Wildfire. Fold it back
            // into the reservoir before calculating the Combust cash-out.
            state.Pool =
                outstanding;

            state.PendingPulse =
                0f;

            float fraction =
                Mathf.Clamp01(
                    consumedHeat * 0.10f);

            int detonation =
                Mathf.Clamp(
                    Mathf.RoundToInt(
                        state.Pool * fraction),
                    1,
                    Mathf.CeilToInt(
                        state.Pool));

            state.Pool =
                Mathf.Max(
                    0f,
                    state.Pool - detonation);

            _heat =
                0;

            _lastDirectHit =
                -999f;

            _nextHeatDecay =
                0f;

            UseSkill skills =
                GetPlayerSkills();

            _rekindlingCharges =
                skills != null
                    ? EmberweaverAscensions
                        .GetRekindlingRank(
                            skills)
                    : 0;

            ApplyWildfireDamage(
                state,
                detonation);

            if (state.Pool <= 0.01f)
            {
                RemoveWildfireState(
                    target,
                    state);
            }
        }

        // ============================================================
        // WILDFIRE RESERVOIR / 1.5-SECOND SCHEDULER
        // ============================================================

        private static void AddToWildfirePool(
            Character target,
            int finalDirectDamage,
            EmberweaverSpellDefinition definition,
            bool wasCritical)
        {
            if (target == null ||
                target.MyStats == null ||
                finalDirectDamage <= 0)
            {
                return;
            }

            Character player =
                GetPlayerCharacter();

            if (player == null)
            {
                return;
            }

            Spell wildfireSpell =
                ResolveWildfireSpell();

            if (wildfireSpell == null)
            {
                return;
            }

            WildfireState state;

            if (!Wildfires.TryGetValue(
                    target,
                    out state) ||
                state == null)
            {
                float now =
                    Time.unscaledTime;

                state =
                    new WildfireState
                    {
                        Target = target,
                        Stats = target.MyStats,
                        LastDepositTime = now,
                        NextTickTime = now + WildfireTickSeconds,
                        BurnoutStage = 0
                    };

                Wildfires[target] =
                    state;
            }

            state.Stats =
                target.MyStats;

            float share =
                GetWildfireShare(
                    definition,
                    wasCritical);

            // finalDirectDamage is now only the immediate portion of the
            // spell. Reconstruct the transferred portion using the same
            // share that reduced the original hit, rather than adding a
            // second copy of damage.
            state.Pool +=
                EmberweaverWildfireDamageSplit.StoredDamageFromImmediate(
                    finalDirectDamage,
                    share);

            state.LastDepositTime =
                Time.unscaledTime;

            state.BurnoutStage =
                0;

            QueueWildfirePulse(
                state,
                GetWildfirePulseFraction(
                    definition));

            EnsureWildfireStatus(
                state,
                wildfireSpell,
                player);
        }

        private static void UpdateWildfires()
        {
            if (Wildfires.Count == 0)
            {
                return;
            }

            float now =
                Time.unscaledTime;

            List<Character> remove =
                null;

            foreach (KeyValuePair<Character, WildfireState> pair in Wildfires)
            {
                WildfireState state =
                    pair.Value;

                if (!IsValidWildfireState(state))
                {
                    AddRemoval(
                        ref remove,
                        pair.Key);

                    continue;
                }

                int safety =
                    0;

                while (now >= state.NextTickTime &&
                       safety < 8)
                {
                    safety++;

                    // Resolve against the scheduled tick timestamp rather than
                    // the current frame time. This preserves the exact
                    // 1.5-second cadence across frame hitches and guarantees
                    // normal ticks at 1.5 / 3.0 / 4.5 / 6.0 seconds before
                    // burnout begins on the following scheduled tick.
                    float scheduledTickTime =
                        state.NextTickTime;

                    if (!PerformScheduledWildfireTick(
                            state,
                            scheduledTickTime))
                    {
                        AddRemoval(
                            ref remove,
                            pair.Key);

                        break;
                    }

                    state.NextTickTime +=
                        WildfireTickSeconds;
                }
            }

            if (remove == null)
            {
                return;
            }

            for (int i = 0;
                 i < remove.Count;
                 i++)
            {
                Character target =
                    remove[i];

                WildfireState state;

                if (Wildfires.TryGetValue(
                        target,
                        out state))
                {
                    RemoveWildfireState(
                        target,
                        state);
                }
            }
        }

        private static bool PerformScheduledWildfireTick(
            WildfireState state,
            float now)
        {
            if (!IsValidWildfireState(state))
            {
                return false;
            }

            bool burnout =
                now - state.LastDepositTime >
                WildfireBurnoutDelaySeconds + 0.001f;

            int damage;

            if (burnout)
            {
                float outstanding =
                    GetOutstandingWildfire(
                        state);

                state.Pool =
                    outstanding;

                state.PendingPulse =
                    0f;

                if (state.BurnoutStage <= 0)
                {
                    damage =
                        Mathf.Clamp(
                            Mathf.RoundToInt(
                                state.Pool *
                                WildfireFirstBurnoutFraction),
                            1,
                            Mathf.CeilToInt(
                                state.Pool));

                    state.Pool =
                        Mathf.Max(
                            0f,
                            state.Pool - damage);

                    state.BurnoutStage =
                        1;
                }
                else
                {
                    damage =
                        Mathf.Max(
                            1,
                            Mathf.CeilToInt(
                                state.Pool));

                    state.Pool =
                        0f;

                    state.BurnoutStage =
                        2;
                }
            }
            else
            {
                int poolDamage =
                    state.Pool > 0.01f
                        ? Mathf.Clamp(
                            Mathf.CeilToInt(
                                state.Pool *
                                WildfireTickFraction),
                            1,
                            Mathf.CeilToInt(
                                state.Pool))
                        : 0;

                int pulseDamage =
                    state.PendingPulse > 0.01f
                        ? Mathf.Clamp(
                            Mathf.CeilToInt(
                                state.PendingPulse),
                            1,
                            Mathf.CeilToInt(
                                state.PendingPulse))
                        : 0;

                state.Pool =
                    Mathf.Max(
                        0f,
                        state.Pool - poolDamage);

                state.PendingPulse =
                    Mathf.Max(
                        0f,
                        state.PendingPulse - pulseDamage);

                damage =
                    poolDamage +
                    pulseDamage;
            }

            if (damage > 0)
            {
                ApplyWildfireDamage(
                    state,
                    damage);
            }

            if (state.BurnoutStage >= 2 ||
                GetOutstandingWildfire(state) <= 0.01f)
            {
                return false;
            }

            return state.Target != null &&
                   state.Target.Alive;
        }

        private static void ApplyWildfireDamage(
            WildfireState state,
            int damage)
        {
            if (state == null ||
                state.Target == null ||
                !state.Target.Alive ||
                damage <= 0)
            {
                return;
            }

            Character player =
                GetPlayerCharacter();

            if (player == null)
            {
                return;
            }

            // Optional Combat Meter bridge already used by FOTF raid mechanics.
            // This keeps the custom 1.5s scheduler attributed as Spell:Wildfire
            // without taking a compile-time dependency on the meter.
            RaidMechanicDamageAttribution.SetNamedSource(
                player,
                "Wildfire");

            state.Target.DamageMe(
                damage,
                true,
                GameData.DamageType.Elemental,
                player,
                false,
                false,
                0);
        }

        private static float GetWildfireShare(
            EmberweaverSpellDefinition definition,
            bool wasCritical)
        {
            float share;

            if (definition != null &&
                definition.WildfireShareOverride > 0f)
            {
                share =
                    definition.WildfireShareOverride;
            }
            else
            {
                share =
                    wasCritical
                        ? WildfireCriticalShare
                        : WildfireBaseShare;
            }

            UseSkill skills =
                GetPlayerSkills();

            if (skills != null)
            {
                share +=
                    EmberweaverAscensions
                        .GetFeedingTheWildfireBonus(
                            skills);
            }

            return Mathf.Clamp01(
                share);
        }

        private static float GetWildfirePulseFraction(
            EmberweaverSpellDefinition definition)
        {
            if (definition == null ||
                definition.WildfirePulseFraction <= 0f)
            {
                return 0f;
            }

            float fraction =
                definition.WildfirePulseFraction;

            UseSkill skills =
                GetPlayerSkills();

            if (skills != null)
            {
                fraction +=
                    EmberweaverAscensions
                        .GetControlledBurnPulseBonus(
                            skills);
            }

            return Mathf.Clamp01(
                fraction);
        }

        private static void QueueWildfirePulse(
            WildfireState state,
            float fraction)
        {
            if (state == null ||
                state.Pool <= 0.01f ||
                fraction <= 0f)
            {
                return;
            }

            float pulse =
                state.Pool *
                Mathf.Clamp01(
                    fraction);

            if (pulse <= 0.01f)
            {
                return;
            }

            state.Pool =
                Mathf.Max(
                    0f,
                    state.Pool - pulse);

            state.PendingPulse +=
                pulse;
        }

        private static void EnsureWildfireStatus(
            WildfireState state,
            Spell wildfireSpell,
            Character player)
        {
            if (state == null ||
                state.Stats == null ||
                wildfireSpell == null ||
                player == null)
            {
                return;
            }

            StatusEffect status =
                FindWildfireStatus(
                    state.Stats);

            if (status == null)
            {
                int slot =
                    state.Stats.AddStatusEffect(
                        wildfireSpell,
                        true,
                        0,
                        player);

                if (slot >= 0 &&
                    state.Stats.StatusEffects != null &&
                    slot < state.Stats.StatusEffects.Length)
                {
                    status =
                        state.Stats.StatusEffects[slot];
                }
            }

            if (status == null ||
                status.Effect == null ||
                !IsWildfireEffect(
                    status.Effect))
            {
                return;
            }

            status.Duration =
                WildfireCarrierDurationTicks;

            status.bonusDmg =
                0;

            status.Owner =
                player;

            status.CreditDPS =
                player;

            status.fromPlayer =
                true;
        }

        private static Spell ResolveWildfireSpell()
        {
            Spell spell =
                EmberweaverSpellCatalog.WildfireEffectSpell;

            if (spell == null &&
                GameData.SpellDatabase != null)
            {
                EmberweaverSpellCatalog.Register(
                    GameData.SpellDatabase);

                spell =
                    EmberweaverSpellCatalog.WildfireEffectSpell;
            }

            return spell;
        }

        private static float GetOutstandingWildfire(
            WildfireState state)
        {
            if (state == null)
            {
                return 0f;
            }

            return Mathf.Max(
                0f,
                state.Pool) +
                Mathf.Max(
                    0f,
                    state.PendingPulse);
        }

        private static bool IsValidWildfireState(
            WildfireState state)
        {
            return state != null &&
                   state.Target != null &&
                   state.Target.Alive &&
                   state.Stats != null &&
                   GetOutstandingWildfire(state) > 0.01f;
        }

        private static StatusEffect FindWildfireStatus(
            Stats stats)
        {
            if (stats == null ||
                stats.StatusEffects == null)
            {
                return null;
            }

            for (int i = 0;
                 i < stats.StatusEffects.Length;
                 i++)
            {
                StatusEffect status =
                    stats.StatusEffects[i];

                if (status != null &&
                    status.Effect != null &&
                    IsWildfireEffect(
                        status.Effect))
                {
                    return status;
                }
            }

            return null;
        }

        private static bool IsWildfireEffect(
            Spell spell)
        {
            return spell != null &&
                   string.Equals(
                       spell.Id,
                       EmberweaverSpellCatalog.WildfireEffectId,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static void RemoveWildfireState(
            Character target,
            WildfireState state)
        {
            if (state != null)
            {
                RemoveWildfireStatus(
                    state.Stats);
            }

            if (target != null)
            {
                Wildfires.Remove(
                    target);
            }
        }

        private static void RemoveWildfireStatus(
            Stats stats)
        {
            if (stats == null ||
                stats.StatusEffects == null)
            {
                return;
            }

            for (int i = 0;
                 i < stats.StatusEffects.Length;
                 i++)
            {
                StatusEffect status =
                    stats.StatusEffects[i];

                if (status == null ||
                    status.Effect == null ||
                    !IsWildfireEffect(
                        status.Effect))
                {
                    continue;
                }

                stats.RemoveStatusEffect(i);
                return;
            }
        }

        private static void AddRemoval(
            ref List<Character> remove,
            Character target)
        {
            if (remove == null)
            {
                remove =
                    new List<Character>();
            }

            if (!remove.Contains(target))
            {
                remove.Add(target);
            }
        }

        // ============================================================
        // HEAT
        // ============================================================

        private static void AddHeat(
            int amount)
        {
            _heat =
                Mathf.Clamp(
                    _heat + amount,
                    0,
                    MaxHeat);

            _lastDirectHit =
                Time.unscaledTime;

            _nextHeatDecay =
                _lastDirectHit +
                HeatGraceSeconds +
                GetCurrentHeatDecaySeconds();
        }

        private static void UpdateHeat()
        {
            if (!EmberweaverSkillCatalog.HasPlayerSkill(
                    EmberweaverSkillCatalog.HeatId))
            {
                _heat = 0;
                return;
            }

            if (_heat <= 0)
            {
                return;
            }

            float now =
                Time.unscaledTime;

            if (now - _lastDirectHit <
                HeatGraceSeconds ||
                now < _nextHeatDecay)
            {
                return;
            }

            _heat =
                Mathf.Max(
                    0,
                    _heat - 1);

            _nextHeatDecay =
                now +
                GetCurrentHeatDecaySeconds();
        }

        private static float GetCurrentHeatDecaySeconds()
        {
            UseSkill skills =
                GetPlayerSkills();

            return skills != null
                ? EmberweaverAscensions.GetHeatDecaySeconds(skills)
                : HeatDecaySeconds;
        }

        private static Character GetPlayerCharacter()
        {
            return GameData.PlayerStats != null
                ? GameData.PlayerStats.Myself
                : null;
        }

        private static UseSkill GetPlayerSkills()
        {
            Character player =
                GetPlayerCharacter();

            return player != null
                ? player.MySkills
                : null;
        }

        private static void CleanupPending()
        {
            float now =
                Time.unscaledTime;

            for (int i = Pending.Count - 1;
                 i >= 0;
                 i--)
            {
                if (Pending[i] == null ||
                    now - Pending[i].Time > 4f)
                {
                    Pending.RemoveAt(i);
                }
            }
        }

        private static void ResetTransient()
        {
            _heat = 0;
            _flashfireReady = false;
            _rekindlingCharges = 0;

            EmberweaverFlashfireCooldownRuntime
                .RestoreNormalPyroclasmCooldowns();

            EmberweaverFlashfireCooldownRuntime
                .ClearTransientManaProtection();

            Pending.Clear();

            if (Wildfires.Count > 0)
            {
                List<WildfireState> states =
                    new List<WildfireState>(
                        Wildfires.Values);

                for (int i = 0;
                     i < states.Count;
                     i++)
                {
                    WildfireState state =
                        states[i];

                    if (state != null)
                    {
                        RemoveWildfireStatus(
                            state.Stats);
                    }
                }
            }

            Wildfires.Clear();
            EndDirectDamage();
        }
    }
}
