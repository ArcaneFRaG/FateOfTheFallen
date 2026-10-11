using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Per-Sim Emberweaver mechanic state. The player runtime is intentionally
    /// left separate because its HUD/hotkey state is global to the local
    /// player, while authored SimPlayers can have several Emberweavers active
    /// at the same time.
    /// </summary>
    internal static class EmberweaverSimCombatRuntime
    {
        internal sealed class PendingCast
        {
            internal Spell Spell;
            internal Character Target;
            internal float Time;
        }

        internal sealed class WildfireState
        {
            internal Character Target;
            internal float Pool;
            internal float PendingPulse;
            internal float LastDepositTime;
            internal float NextTickTime;
            internal int BurnoutStage;
        }

        internal sealed class SimState
        {
            internal SimPlayer Sim;
            internal Character Caster;
            internal int Heat;
            internal float LastDirectHit = -999f;
            internal float NextHeatDecay;
            internal bool FlashfireReady;
            internal int PostCastManaFloor = -1;
            internal int PostCastManaFloorUntilFrame = -1;
            internal readonly List<PendingCast> Pending =
                new List<PendingCast>();
            internal readonly Dictionary<Character, WildfireState> Wildfires =
                new Dictionary<Character, WildfireState>();
        }

        private sealed class ActiveDamageState
        {
            internal SimState Owner;
            internal EmberweaverSpellDefinition Definition;
            internal Character Target;
            internal bool WasCritical;
        }

        private static readonly Dictionary<int, SimState> States =
            new Dictionary<int, SimState>();

        private static readonly Dictionary<Character, SimState> StatesByCaster =
            new Dictionary<Character, SimState>();

        [ThreadStatic]
        private static ActiveDamageState _activeDamage;

        internal static SimState Register(
            SimPlayer sim)
        {
            if (sim == null ||
                sim.MyStats == null ||
                sim.MyStats.Myself == null ||
                !IsAuthoredEmberweaver(sim))
            {
                return null;
            }

            int id = sim.GetInstanceID();
            SimState state;

            if (!States.TryGetValue(id, out state) ||
                state == null)
            {
                state = new SimState();
                States[id] = state;
            }

            if (state.Caster != null &&
                state.Caster != sim.MyStats.Myself)
            {
                StatesByCaster.Remove(state.Caster);
            }

            state.Sim = sim;
            state.Caster = sim.MyStats.Myself;
            StatesByCaster[state.Caster] = state;

            return state;
        }

        internal static void Unregister(
            SimPlayer sim)
        {
            if (sim == null)
            {
                return;
            }

            SimState state;
            int id = sim.GetInstanceID();

            if (!States.TryGetValue(id, out state))
            {
                return;
            }

            if (state != null &&
                state.Caster != null)
            {
                StatesByCaster.Remove(state.Caster);
            }

            States.Remove(id);
        }

        internal static void Update(
            SimPlayer sim)
        {
            SimState state = Register(sim);

            if (state == null)
            {
                return;
            }

            CleanupPending(state);
            UpdateFlashfireManaProtection(state);
            UpdateHeat(state);
            UpdateWildfires(state);
        }

        internal static int GetHeat(
            SimPlayer sim)
        {
            SimState state = Register(sim);
            return state != null ? state.Heat : 0;
        }

        internal static bool IsFlashfireReady(
            SimPlayer sim)
        {
            SimState state = Register(sim);
            return state != null && state.FlashfireReady;
        }

        internal static float GetWildfireBank(
            SimPlayer sim,
            Character target)
        {
            SimState state = Register(sim);

            if (state == null ||
                target == null)
            {
                return 0f;
            }

            WildfireState wildfire;

            if (!state.Wildfires.TryGetValue(target, out wildfire) ||
                wildfire == null)
            {
                return 0f;
            }

            return GetOutstandingWildfire(wildfire);
        }

        internal static bool CanCombust(
            SimPlayer sim,
            Stats targetStats)
        {
            SimState state = Register(sim);

            if (state == null ||
                state.Heat < 5 ||
                !HasSkill(sim, EmberweaverSkillCatalog.WildfireId))
            {
                return false;
            }

            Character target =
                targetStats != null
                    ? targetStats.Myself
                    : null;

            return target != null &&
                   GetWildfireBank(sim, target) > 0.01f;
        }

        internal static bool ShouldAICombust(
            SimPlayer sim,
            Character target,
            int referenceDirectDamage)
        {
            SimState state = Register(sim);

            if (state == null ||
                target == null ||
                state.Heat < 8)
            {
                return false;
            }

            float bank = GetWildfireBank(sim, target);

            if (bank <= 0.01f)
            {
                return false;
            }

            if (state.Heat >= EmberweaverPassiveRuntime.MaxHeat)
            {
                return true;
            }

            float threshold = Mathf.Max(
                25f,
                Mathf.Max(1, referenceDirectDamage) * 0.50f);

            return bank >= threshold;
        }

        internal static bool ShouldAIScorch(
            SimPlayer sim,
            Character target,
            int referenceDirectDamage)
        {
            if (sim == null ||
                target == null ||
                !HasSkill(sim, EmberweaverSkillCatalog.WildfireId))
            {
                return false;
            }

            float bank = GetWildfireBank(sim, target);
            float threshold = Mathf.Max(
                10f,
                Mathf.Max(1, referenceDirectDamage) * 0.30f);

            return bank >= threshold;
        }

        internal static void TrackSuccessfulCast(
            SimPlayer sim,
            Spell spell,
            Stats targetStats)
        {
            SimState state = Register(sim);
            EmberweaverSpellDefinition definition;

            if (state == null ||
                !EmberweaverSpellCatalog.TryGetDefinition(spell, out definition) ||
                definition == null)
            {
                return;
            }

            if (definition.IsCombust)
            {
                ExecuteCombust(
                    state,
                    targetStats != null
                        ? targetStats.Myself
                        : null);

                return;
            }

            if (definition.Damage <= 0)
            {
                return;
            }

            state.Pending.Add(
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
            out SimPlayer sim,
            out EmberweaverSpellDefinition definition)
        {
            sim = null;
            definition = null;

            if (attacker == null)
            {
                return false;
            }

            SimState state;

            if (!StatesByCaster.TryGetValue(attacker, out state) ||
                state == null ||
                state.Sim == null ||
                !IsAuthoredEmberweaver(state.Sim))
            {
                return false;
            }

            float now = Time.unscaledTime;

            for (int i = state.Pending.Count - 1;
                 i >= 0;
                 i--)
            {
                PendingCast pending = state.Pending[i];

                if (pending == null ||
                    now - pending.Time > 4f)
                {
                    state.Pending.RemoveAt(i);
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
                    state.Pending.RemoveAt(i);
                    sim = state.Sim;
                    return true;
                }
            }

            return false;
        }

        internal static void BeginDirectDamage(
            SimPlayer sim,
            EmberweaverSpellDefinition definition,
            Character target)
        {
            SimState state = Register(sim);

            if (state == null ||
                definition == null)
            {
                _activeDamage = null;
                return;
            }

            _activeDamage =
                new ActiveDamageState
                {
                    Owner = state,
                    Definition = definition,
                    Target = target,
                    WasCritical =
                        UnityEngine.Random.Range(0f, 1f) <
                        EmberweaverPassiveRuntime.BaseSpellCriticalChance
                };
        }

        internal static int ApplyDirectDamageModifiers(
            int incoming,
            EmberweaverSpellDefinition definition)
        {
            ActiveDamageState active = _activeDamage;

            if (incoming <= 0 ||
                definition == null ||
                definition.IsCombust ||
                active == null ||
                active.Owner == null)
            {
                return incoming;
            }

            float multiplier = 1f;

            if (HasSkill(active.Owner.Sim, EmberweaverSkillCatalog.HeatId) &&
                active.Owner.Heat > 0)
            {
                multiplier *=
                    1f +
                    (active.Owner.Heat * EmberweaverPassiveRuntime.DamagePerHeat);
            }

            if (active.WasCritical)
            {
                multiplier *=
                    EmberweaverPassiveRuntime.BaseSpellCriticalDamageMultiplier;
            }

            if (active.Target != null &&
                HasSkill(active.Owner.Sim, EmberweaverSkillCatalog.WildfireId))
            {
                multiplier *= EmberweaverWildfireDamageSplit.ImmediateFraction(
                    GetWildfireShare(definition, active.WasCritical));
            }

            return Mathf.Max(
                0,
                Mathf.RoundToInt(incoming * multiplier));
        }

        internal static void OnDirectDamageApplied(
            SimPlayer sim,
            Character target,
            EmberweaverSpellDefinition definition,
            int finalDamage)
        {
            SimState state = Register(sim);
            ActiveDamageState active = _activeDamage;

            if (state == null ||
                definition == null ||
                definition.IsCombust ||
                finalDamage <= 0)
            {
                return;
            }

            bool critical =
                active != null &&
                active.Owner == state &&
                active.WasCritical;

            if (HasSkill(sim, EmberweaverSkillCatalog.HeatId))
            {
                int heatGain =
                    definition.GeneratesTwoHeat
                        ? 2
                        : 1;

                if (critical)
                {
                    heatGain++;
                }

                AddHeat(state, heatGain);
            }

            if (critical &&
                HasSkill(sim, EmberweaverSkillCatalog.FlashfireId) &&
                !state.FlashfireReady)
            {
                state.FlashfireReady = true;
                ResetPyroclasmCooldowns(sim);
            }

            if (target != null &&
                HasSkill(sim, EmberweaverSkillCatalog.WildfireId))
            {
                AddToWildfirePool(
                    state,
                    target,
                    finalDamage,
                    definition,
                    critical);
            }
        }

        internal static void EndDirectDamage()
        {
            _activeDamage = null;
        }

        internal static void MarkCurrentReduceHpCritical(
            Stats targetStats,
            ref bool isCritical)
        {
            ActiveDamageState active = _activeDamage;

            if (active == null ||
                !active.WasCritical ||
                targetStats == null ||
                targetStats.Myself == null ||
                targetStats.Myself != active.Target)
            {
                return;
            }

            isCritical = true;
        }

        internal static bool ShouldInstantPyroclasm(
            SimPlayer sim,
            Spell spell)
        {
            SimState state = Register(sim);
            EmberweaverSpellDefinition definition;

            return state != null &&
                   state.FlashfireReady &&
                   HasSkill(sim, EmberweaverSkillCatalog.FlashfireId) &&
                   EmberweaverSpellCatalog.TryGetDefinition(spell, out definition) &&
                   definition != null &&
                   definition.IsPyroclasm;
        }

        internal static void ConsumeFlashfire(
            SimPlayer sim,
            Spell spell,
            bool success)
        {
            SimState state = Register(sim);

            if (state == null ||
                !success ||
                !ShouldInstantPyroclasm(sim, spell))
            {
                return;
            }

            state.FlashfireReady = false;
        }

        internal static void ArmPostCastManaProtection(
            SimPlayer sim,
            int baseline)
        {
            SimState state = Register(sim);

            if (state == null ||
                sim == null ||
                sim.MyStats == null)
            {
                return;
            }

            state.PostCastManaFloor =
                Math.Max(
                    0,
                    Math.Min(
                        baseline,
                        sim.MyStats.GetCurrentMaxMana()));

            // Match the proven player Flashfire protection window. Native can
            // defer its mana deduction by a frame after StartSpell returns.
            // Two frames protects only this proc cast; the combat controller's
            // normal cast lockout is much longer than this window.
            state.PostCastManaFloorUntilFrame =
                Time.frameCount + 2;
        }

        internal static void ResetPyroclasmCooldowns(
            SimPlayer sim)
        {
            if (sim == null ||
                sim.Spellbook == null)
            {
                return;
            }

            for (int i = 0;
                 i < sim.Spellbook.Count;
                 i++)
            {
                SimPlayerSpellSlot slot = sim.Spellbook[i];
                EmberweaverSpellDefinition definition;

                if (slot == null ||
                    slot.spell == null ||
                    !EmberweaverSpellCatalog.TryGetDefinition(
                        slot.spell,
                        out definition) ||
                    definition == null ||
                    !definition.IsPyroclasm)
                {
                    continue;
                }

                slot.CD = 0f;
            }
        }

        internal static void SetSpellCooldown(
            SimPlayer sim,
            Spell spell)
        {
            if (sim == null ||
                spell == null)
            {
                return;
            }

            EmberweaverSpellDefinition definition;

            if (!EmberweaverSpellCatalog.TryGetDefinition(
                    spell,
                    out definition) ||
                definition == null)
            {
                return;
            }

            sim.SetSpellCooldownBySpell(
                spell,
                definition.Cooldown);
        }

        internal static bool IsSpellReady(
            SimPlayer sim,
            Spell spell,
            bool ignoreCooldown)
        {
            if (sim == null ||
                spell == null ||
                sim.Spellbook == null)
            {
                return false;
            }

            int index = sim.GetSpellIndexInBook(spell);

            if (index < 0 ||
                index >= sim.Spellbook.Count)
            {
                return false;
            }

            if (!ignoreCooldown &&
                sim.GetSpellCooldownByIndex(index) > 0f)
            {
                return false;
            }

            return true;
        }

        internal static bool IsAuthoredEmberweaver(
            SimPlayer sim)
        {
            if (sim == null ||
                sim.MyStats == null ||
                !EmberweaverCatalog.IsEmberweaverClass(
                    sim.MyStats.CharacterClass))
            {
                return false;
            }

            NPC npc = sim.GetThisNPC();

            return npc != null &&
                   FOTFSimPlayerRoster.IsClassMember(
                       npc.NPCName,
                       FOTFSimPlayerRoster.EmberweaverClassName);
        }

        private static bool HasSkill(
            SimPlayer sim,
            string skillId)
        {
            if (sim == null ||
                sim.MyStats == null ||
                sim.MyStats.Myself == null ||
                sim.MyStats.Myself.MySkills == null ||
                sim.MyStats.Myself.MySkills.KnownSkills == null ||
                string.IsNullOrEmpty(skillId))
            {
                return false;
            }

            List<Skill> known =
                sim.MyStats.Myself.MySkills.KnownSkills;

            for (int i = 0;
                 i < known.Count;
                 i++)
            {
                Skill skill = known[i];

                if (skill != null &&
                    string.Equals(
                        skill.Id,
                        skillId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddHeat(
            SimState state,
            int amount)
        {
            if (state == null ||
                amount <= 0)
            {
                return;
            }

            state.Heat = Mathf.Clamp(
                state.Heat + amount,
                0,
                EmberweaverPassiveRuntime.MaxHeat);

            state.LastDirectHit = Time.unscaledTime;
            state.NextHeatDecay =
                state.LastDirectHit +
                EmberweaverPassiveRuntime.HeatGraceSeconds;
        }

        private static void UpdateFlashfireManaProtection(
            SimState state)
        {
            if (state == null ||
                state.PostCastManaFloor < 0 ||
                state.PostCastManaFloorUntilFrame < 0)
            {
                return;
            }

            if (Time.frameCount > state.PostCastManaFloorUntilFrame)
            {
                state.PostCastManaFloor = -1;
                state.PostCastManaFloorUntilFrame = -1;
                return;
            }

            if (state.Sim != null &&
                state.Sim.MyStats != null &&
                state.Sim.MyStats.CurrentMana < state.PostCastManaFloor)
            {
                state.Sim.MyStats.CurrentMana =
                    state.PostCastManaFloor;
            }
        }


        private static void UpdateHeat(
            SimState state)
        {
            if (state == null ||
                state.Heat <= 0)
            {
                return;
            }

            float now = Time.unscaledTime;

            if (now < state.NextHeatDecay)
            {
                return;
            }

            state.Heat = Mathf.Max(0, state.Heat - 1);
            state.NextHeatDecay =
                now + EmberweaverPassiveRuntime.HeatDecaySeconds;
        }

        private static void AddToWildfirePool(
            SimState owner,
            Character target,
            int finalDirectDamage,
            EmberweaverSpellDefinition definition,
            bool wasCritical)
        {
            if (owner == null ||
                target == null ||
                target.MyStats == null ||
                finalDirectDamage <= 0)
            {
                return;
            }

            WildfireState state;

            if (!owner.Wildfires.TryGetValue(target, out state) ||
                state == null)
            {
                float now = Time.unscaledTime;

                state =
                    new WildfireState
                    {
                        Target = target,
                        LastDepositTime = now,
                        NextTickTime = now + EmberweaverPassiveRuntime.WildfireTickSeconds,
                        BurnoutStage = 0
                    };

                owner.Wildfires[target] = state;
            }

            float share = GetWildfireShare(definition, wasCritical);

            state.Pool +=
                EmberweaverWildfireDamageSplit.StoredDamageFromImmediate(
                    finalDirectDamage,
                    share);

            state.LastDepositTime = Time.unscaledTime;
            state.BurnoutStage = 0;

            if (definition != null &&
                definition.WildfirePulseFraction > 0f)
            {
                float pulse =
                    state.Pool *
                    Mathf.Clamp01(definition.WildfirePulseFraction);

                state.Pool = Mathf.Max(0f, state.Pool - pulse);
                state.PendingPulse += pulse;
            }
        }

        private static float GetWildfireShare(
            EmberweaverSpellDefinition definition,
            bool wasCritical)
        {
            if (definition != null && definition.WildfireShareOverride > 0f)
            {
                return definition.WildfireShareOverride;
            }

            return wasCritical
                ? EmberweaverPassiveRuntime.WildfireCriticalShare
                : EmberweaverPassiveRuntime.WildfireBaseShare;
        }

        private static void UpdateWildfires(
            SimState owner)
        {
            if (owner == null ||
                owner.Wildfires.Count == 0)
            {
                return;
            }

            float now = Time.unscaledTime;
            List<Character> remove = null;

            foreach (KeyValuePair<Character, WildfireState> pair
                     in owner.Wildfires)
            {
                WildfireState state = pair.Value;

                if (!IsValidWildfireState(state))
                {
                    AddRemoval(ref remove, pair.Key);
                    continue;
                }

                int safety = 0;

                while (now >= state.NextTickTime &&
                       safety < 8)
                {
                    safety++;
                    float scheduled = state.NextTickTime;

                    if (!PerformWildfireTick(owner, state, scheduled))
                    {
                        AddRemoval(ref remove, pair.Key);
                        break;
                    }

                    state.NextTickTime +=
                        EmberweaverPassiveRuntime.WildfireTickSeconds;
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
                owner.Wildfires.Remove(remove[i]);
            }
        }

        private static bool PerformWildfireTick(
            SimState owner,
            WildfireState state,
            float scheduledTime)
        {
            if (!IsValidWildfireState(state))
            {
                return false;
            }

            bool burnout =
                scheduledTime - state.LastDepositTime >
                EmberweaverPassiveRuntime.WildfireBurnoutDelaySeconds + 0.001f;

            int damage;

            if (burnout)
            {
                float outstanding = GetOutstandingWildfire(state);
                state.Pool = outstanding;
                state.PendingPulse = 0f;

                if (state.BurnoutStage <= 0)
                {
                    damage = Mathf.Clamp(
                        Mathf.RoundToInt(
                            state.Pool *
                            EmberweaverPassiveRuntime.WildfireFirstBurnoutFraction),
                        1,
                        Mathf.CeilToInt(state.Pool));

                    state.Pool = Mathf.Max(0f, state.Pool - damage);
                    state.BurnoutStage = 1;
                }
                else
                {
                    damage = Mathf.Max(1, Mathf.CeilToInt(state.Pool));
                    state.Pool = 0f;
                    state.BurnoutStage = 2;
                }
            }
            else
            {
                int poolDamage =
                    state.Pool > 0.01f
                        ? Mathf.Clamp(
                            Mathf.CeilToInt(
                                state.Pool *
                                EmberweaverPassiveRuntime.WildfireTickFraction),
                            1,
                            Mathf.CeilToInt(state.Pool))
                        : 0;

                int pulseDamage =
                    state.PendingPulse > 0.01f
                        ? Mathf.CeilToInt(state.PendingPulse)
                        : 0;

                state.Pool = Mathf.Max(0f, state.Pool - poolDamage);
                state.PendingPulse = Mathf.Max(0f, state.PendingPulse - pulseDamage);
                damage = poolDamage + pulseDamage;
            }

            if (damage > 0)
            {
                ApplyWildfireDamage(owner, state, damage);
            }

            return state.BurnoutStage < 2 &&
                   GetOutstandingWildfire(state) > 0.01f &&
                   state.Target != null &&
                   state.Target.Alive;
        }

        private static void ExecuteCombust(
            SimState owner,
            Character target)
        {
            if (owner == null ||
                target == null ||
                owner.Heat < 5)
            {
                return;
            }

            WildfireState state;

            if (!owner.Wildfires.TryGetValue(target, out state) ||
                state == null)
            {
                return;
            }

            float outstanding = GetOutstandingWildfire(state);

            if (outstanding <= 0.01f)
            {
                return;
            }

            state.Pool = outstanding;
            state.PendingPulse = 0f;

            int heat = Mathf.Clamp(
                owner.Heat,
                0,
                EmberweaverPassiveRuntime.MaxHeat);

            float fraction = Mathf.Clamp01(heat * 0.10f);
            int detonation = Mathf.Clamp(
                Mathf.RoundToInt(state.Pool * fraction),
                1,
                Mathf.CeilToInt(state.Pool));

            state.Pool = Mathf.Max(0f, state.Pool - detonation);
            owner.Heat = 0;
            owner.LastDirectHit = -999f;
            owner.NextHeatDecay = 0f;

            ApplyWildfireDamage(owner, state, detonation);

            if (state.Pool <= 0.01f)
            {
                owner.Wildfires.Remove(target);
            }
        }

        private static void ApplyWildfireDamage(
            SimState owner,
            WildfireState state,
            int damage)
        {
            if (owner == null ||
                owner.Caster == null ||
                state == null ||
                state.Target == null ||
                !state.Target.Alive ||
                damage <= 0)
            {
                return;
            }

            RaidMechanicDamageAttribution.SetNamedSource(
                owner.Caster,
                "Wildfire");

            state.Target.DamageMe(
                damage,
                false,
                GameData.DamageType.Elemental,
                owner.Caster,
                false,
                false,
                0);
        }

        private static float GetOutstandingWildfire(
            WildfireState state)
        {
            if (state == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, state.Pool) +
                   Mathf.Max(0f, state.PendingPulse);
        }

        private static bool IsValidWildfireState(
            WildfireState state)
        {
            return state != null &&
                   state.Target != null &&
                   state.Target.Alive &&
                   state.Target.MyStats != null &&
                   GetOutstandingWildfire(state) > 0.01f;
        }

        private static void AddRemoval(
            ref List<Character> remove,
            Character target)
        {
            if (remove == null)
            {
                remove = new List<Character>();
            }

            if (!remove.Contains(target))
            {
                remove.Add(target);
            }
        }

        private static void CleanupPending(
            SimState state)
        {
            if (state == null ||
                state.Pending.Count == 0)
            {
                return;
            }

            float now = Time.unscaledTime;

            for (int i = state.Pending.Count - 1;
                 i >= 0;
                 i--)
            {
                PendingCast pending = state.Pending[i];

                if (pending == null ||
                    now - pending.Time > 4f)
                {
                    state.Pending.RemoveAt(i);
                }
            }
        }
    }
}
