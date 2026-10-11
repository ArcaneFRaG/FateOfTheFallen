using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Runtime effects for the original Varkesh/Gorath v0.2 equipment pools.
    /// Item IDs are canonical; r17 test items with the same IDs are upgraded
    /// in-place to these effects during registration.
    /// </summary>
    internal static class LateWorldBossEquipmentEffects
    {
        // ============================================================
        // VARKESH / DEATH
        // ============================================================

        internal const float SoulRendChance = 15f;
        internal const int SoulRendDamage = 3600;
        internal const int SoulRendMana = 180;

        internal const float DeathsMomentumChance = 10f;
        internal const int DeathsMomentumDamage = 5000;
        internal const float DeathsMomentumHastePercent = 12f;
        internal const float DeathsMomentumDuration = 6f;

        internal const float RefuseDeathThreshold = 0.25f;
        internal const int RefuseDeathArmor = 150;
        internal const float RefuseDeathDamageMultiplier = 0.85f;
        internal const float RefuseDeathHealingMultiplier = 1.25f;
        internal const float RefuseDeathDuration = 8f;
        internal const float RefuseDeathCooldown = 90f;

        internal const float SoulkeepersGraceThreshold = 0.35f;
        internal const float SoulkeepersGraceChance = 100f;
        internal const float SoulkeepersGraceEmergencyMultiplier = 1.25f;
        internal const float SoulkeepersGraceHealingMultiplier = 1.10f;
        internal const float SoulkeepersGraceDuration = 6f;
        internal const float SoulkeepersGraceCooldown = 12f;

        internal const float GravewindChance = 12f;
        internal const int GravewindDexterity = 10;
        internal const int GravewindAgility = 10;
        internal const float GravewindHastePercent = 12f;
        internal const float GravewindDuration = 8f;
        internal const float GravewindCooldown = 15f;
        internal const float GravewindDamageChance = 20f;
        internal const int GravewindDamage = 1200;

        // ============================================================
        // GORATH / INFERNAL
        // ============================================================

        internal const float HellfireChance = 20f;
        internal const int HellfireInitialDamage = 2400;
        internal const int HellfireTickDamage = 500;
        internal const int HellfireTicks = 4;
        internal const int HellfireMaxBonusStacks = 3;

        internal const float InfernalFrenzyChance = 15f;
        internal const int InfernalFrenzyDamage = 4400;
        internal const float InfernalFrenzyHastePercent = 10f;
        internal const float InfernalFrenzyDuration = 6f;

        internal const float DemonicChallengeChance = 90f;
        internal const int DemonicChallengeThreat = 7200;
        internal const float InfernalResolveDamageMultiplier = 0.95f;
        internal const float InfernalResolveDuration = 6f;

        internal const float AshenCovenantChance = 15f;
        internal const float AshenCovenantManaRefundPercent = 0.30f;
        internal const float AshenGraceHealingMultiplier = 1.08f;
        internal const float AshenGraceDuration = 6f;

        internal const float StormfireChance = 20f;
        internal const int StormfireInitialDamage = 2600;
        internal const int StormfireTickDamage = 475;
        internal const int StormfireTicks = 4;
        internal const int StormfireMaxBonusStacks = 3;
        internal const float ArcDischargeChance = 15f;
        internal const int ArcDischargeDamage = 1000;
        internal const float ArcDischargeCooldown = 3f;

        private const float PeriodicTickInterval = 2f;
        private const float BashDebounceSeconds = 0.30f;

        private sealed class PeriodicDamageState
        {
            internal Character Target;
            internal Character Source;
            internal string EffectId;
            internal int BaseDamage;
            internal int BonusStacks;
            internal int MaxBonusStacks;
            internal int TicksRemaining;
            internal float NextTickAt;
            internal GameData.DamageType DamageType;
        }

        private static readonly List<PeriodicDamageState> PeriodicDamage =
            new List<PeriodicDamageState>();

        private static readonly Dictionary<Character, float> DeathsMomentumExpiries = new Dictionary<Character, float>();
        private static readonly Dictionary<Character, float> RefuseDeathExpiries = new Dictionary<Character, float>();
        private static readonly Dictionary<Character, float> RefuseDeathCooldowns = new Dictionary<Character, float>();
        private static readonly Dictionary<Character, float> SoulkeepersGraceExpiries = new Dictionary<Character, float>();
        private static readonly Dictionary<Character, float> SoulkeepersGraceCooldowns = new Dictionary<Character, float>();
        private static readonly Dictionary<Character, float> GravewindExpiries = new Dictionary<Character, float>();
        private static readonly Dictionary<Character, float> GravewindCooldowns = new Dictionary<Character, float>();

        private static readonly Dictionary<Character, float> InfernalFrenzyExpiries = new Dictionary<Character, float>();
        private static readonly Dictionary<Character, float> InfernalResolveExpiries = new Dictionary<Character, float>();
        private static readonly Dictionary<Character, float> AshenGraceExpiries = new Dictionary<Character, float>();
        private static readonly Dictionary<Character, float> ArcDischargeCooldowns = new Dictionary<Character, float>();
        private static readonly Dictionary<int, float> LastBashInvocation = new Dictionary<int, float>();

        private static readonly FieldInfo CurrentDexField =
            AccessTools.Field(typeof(Stats), "CurrentDex");

        private static readonly FieldInfo CurrentAgiField =
            AccessTools.Field(typeof(Stats), "CurrentAgi");

        private static LateWorldBossEquipmentEffectsController _controller;
        private static bool _applyingGeneratedDamage;
        private static bool _applyingGeneratedHeal = false;

        internal static bool IsApplyingGeneratedDamage
        {
            get { return _applyingGeneratedDamage; }
        }

        internal static bool IsApplyingGeneratedHeal
        {
            get { return _applyingGeneratedHeal; }
        }

        internal static void Initialize()
        {
            if (_controller != null)
                return;

            GameObject host = new GameObject("FOTF_LateWorldBossEquipmentEffects");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            _controller = host.AddComponent<LateWorldBossEquipmentEffectsController>();
        }

        internal static void Shutdown()
        {
            PeriodicDamage.Clear();
            DeathsMomentumExpiries.Clear();
            RefuseDeathExpiries.Clear();
            RefuseDeathCooldowns.Clear();
            SoulkeepersGraceExpiries.Clear();
            SoulkeepersGraceCooldowns.Clear();
            GravewindExpiries.Clear();
            GravewindCooldowns.Clear();
            InfernalFrenzyExpiries.Clear();
            InfernalResolveExpiries.Clear();
            AshenGraceExpiries.Clear();
            ArcDischargeCooldowns.Clear();
            LastBashInvocation.Clear();

            if (_controller != null)
            {
                UnityEngine.Object.Destroy(_controller.gameObject);
                _controller = null;
            }
        }

        internal static void UpdateEffects()
        {
            float now = Time.time;

            for (int i = PeriodicDamage.Count - 1; i >= 0; i--)
            {
                PeriodicDamageState state = PeriodicDamage[i];
                if (state == null ||
                    state.Target == null ||
                    state.Source == null ||
                    !state.Target.Alive ||
                    state.Target.MyStats == null ||
                    state.Target.MyStats.CurrentHP <= 0 ||
                    state.TicksRemaining <= 0)
                {
                    PeriodicDamage.RemoveAt(i);
                    continue;
                }

                if (now < state.NextTickAt)
                    continue;

                state.NextTickAt = now + PeriodicTickInterval;
                state.TicksRemaining--;

                float multiplier = 1f + (0.10f * Mathf.Clamp(state.BonusStacks, 0, state.MaxBonusStacks));
                int damage = Mathf.RoundToInt(state.BaseDamage * multiplier);

                ApplyMagicDamage(
                    state.Target,
                    state.Source,
                    damage,
                    state.DamageType);

                if (state.TicksRemaining <= 0)
                    PeriodicDamage.RemoveAt(i);
            }

            ExpireCalculatedState(DeathsMomentumExpiries, now);
            ExpireCalculatedState(RefuseDeathExpiries, now);
            ExpireCalculatedState(SoulkeepersGraceExpiries, now);
            ExpireCalculatedState(GravewindExpiries, now);
            ExpireCalculatedState(InfernalFrenzyExpiries, now);
            ExpireCalculatedState(InfernalResolveExpiries, now);
            ExpireCalculatedState(AshenGraceExpiries, now);
            TrimExpired(RefuseDeathCooldowns, now, false);
            TrimExpired(SoulkeepersGraceCooldowns, now, false);
            TrimExpired(GravewindCooldowns, now, false);
            TrimExpired(ArcDischargeCooldowns, now, false);
        }

        private static void ExpireCalculatedState(
            Dictionary<Character, float> map,
            float now)
        {
            TrimExpired(map, now, true);
        }

        private static void TrimExpired(
            Dictionary<Character, float> map,
            float now,
            bool recalc)
        {
            if (map == null || map.Count == 0)
                return;

            List<Character> remove = null;
            foreach (KeyValuePair<Character, float> pair in map)
            {
                if (pair.Key == null || pair.Value <= now)
                {
                    if (remove == null)
                        remove = new List<Character>();
                    remove.Add(pair.Key);
                }
            }

            if (remove == null)
                return;

            for (int i = 0; i < remove.Count; i++)
            {
                Character character = remove[i];
                map.Remove(character);
                if (recalc && character != null && character.MyStats != null)
                {
                    try { character.MyStats.CalcStats(); }
                    catch { }
                }
            }
        }

        // ============================================================
        // DAMAGE PROCS
        // ============================================================

        internal static void TryProcPhysicalDamage(
            Character target,
            Character source)
        {
            if (_applyingGeneratedDamage ||
                target == null || source == null ||
                !target.Alive || !source.Alive)
                return;

            TryDeathsMomentum(target, source);
            TryGravewind(target, source);
            TryInfernalFrenzy(target, source);
            TryStormfire(target, source);
        }

        internal static void TryProcMagicDamage(
            Character target,
            Character source)
        {
            if (_applyingGeneratedDamage ||
                target == null || source == null ||
                !target.Alive || !source.Alive)
                return;

            TrySoulRend(target, source);
            TryHellfire(target, source);
            TryArcDischarge(target, source);
        }

        private static void TrySoulRend(Character target, Character source)
        {
            if (!IsEquipped(source, VarkeshEquipment.SoulbinderGrimoireId) ||
                !Roll(SoulRendChance))
                return;

            ApplyMagicDamage(target, source, SoulRendDamage, GameData.DamageType.Magic);
            RestoreMana(source, SoulRendMana);
            AddCombatMessageForPlayerSource(source, "Soul Rend tears through the target and restores " + SoulRendMana + " mana!", "purple");
        }

        private static void TryDeathsMomentum(Character target, Character source)
        {
            if (!IsEquipped(source, VarkeshEquipment.GravetideReaverId) ||
                !Roll(DeathsMomentumChance))
                return;

            ApplyPhysicalDamage(target, source, DeathsMomentumDamage);
            DeathsMomentumExpiries[source] = Time.time + DeathsMomentumDuration;
            Recalculate(source);
            AddCombatMessageForPlayerSource(source, "Death's Momentum surges through the Gravetide Reaver!", "purple");
        }

        private static void TryGravewind(Character target, Character source)
        {
            if (!IsEquipped(source, VarkeshEquipment.BandOfTheGravewindId) ||
                !HasBowEquipped(source))
                return;

            if (HasActive(GravewindExpiries, source) &&
                Roll(GravewindDamageChance))
            {
                ApplyMagicDamage(target, source, GravewindDamage, GameData.DamageType.Magic);
            }

            if (!CooldownReady(GravewindCooldowns, source) ||
                !Roll(GravewindChance))
                return;

            GravewindCooldowns[source] = Time.time + GravewindCooldown;
            GravewindExpiries[source] = Time.time + GravewindDuration;
            Recalculate(source);
            AddCombatMessageForPlayerSource(source, "Gravewind quickens your ranged attacks!", "purple");
        }

        private static void TryHellfire(Character target, Character source)
        {
            if (!IsEquipped(source, GorathEquipment.EmberOfTheDemonKingId) ||
                !Roll(HellfireChance))
                return;

            ApplyMagicDamage(target, source, HellfireInitialDamage, GameData.DamageType.Elemental);
            ApplyOrStackPeriodicDamage(
                target,
                source,
                "GORATH_HELLFIRE",
                HellfireTickDamage,
                HellfireTicks,
                HellfireMaxBonusStacks,
                GameData.DamageType.Elemental);
            AddCombatMessageForPlayerSource(source, "Hellfire consumes the target!", "orange");
        }

        private static void TryInfernalFrenzy(Character target, Character source)
        {
            if (!IsEquipped(source, GorathEquipment.HellforgedTalonId) ||
                !Roll(InfernalFrenzyChance))
                return;

            ApplyPhysicalDamage(target, source, InfernalFrenzyDamage);
            InfernalFrenzyExpiries[source] = Time.time + InfernalFrenzyDuration;
            Recalculate(source);
            AddCombatMessageForPlayerSource(source, "Infernal Frenzy accelerates your attacks!", "orange");
        }

        private static void TryStormfire(Character target, Character source)
        {
            if (!IsEquipped(source, GorathEquipment.StormfireId) ||
                !HasBowEquipped(source) ||
                !Roll(StormfireChance))
                return;

            ApplyMagicDamage(target, source, StormfireInitialDamage, GameData.DamageType.Elemental);
            ApplyOrStackPeriodicDamage(
                target,
                source,
                "GORATH_STORMFIRE",
                StormfireTickDamage,
                StormfireTicks,
                StormfireMaxBonusStacks,
                GameData.DamageType.Elemental);
            AddCombatMessageForPlayerSource(source, "Stormfire ignites the target!", "orange");
        }

        private static void TryArcDischarge(Character target, Character source)
        {
            if (!IsEquipped(source, GorathEquipment.StormfireId) ||
                !HasPeriodicDamage(target, source, "GORATH_STORMFIRE") ||
                !CooldownReady(ArcDischargeCooldowns, source) ||
                !Roll(ArcDischargeChance))
                return;

            ArcDischargeCooldowns[source] = Time.time + ArcDischargeCooldown;
            ApplyMagicDamage(target, source, ArcDischargeDamage, GameData.DamageType.Elemental);
            AddCombatMessageForPlayerSource(source, "Arc Discharge leaps through your Stormfire!", "cyan");
        }

        // ============================================================
        // DEFENSIVE / HEALING EFFECTS
        // ============================================================

        internal static int ApplyIncomingReduction(
            Character defender,
            int incoming,
            GameData.DamageType damageType)
        {
            if (defender == null || incoming <= 0)
                return incoming;

            float multiplier = 1f;

            if (HasActive(RefuseDeathExpiries, defender))
                multiplier *= RefuseDeathDamageMultiplier;

            if (HasActive(InfernalResolveExpiries, defender))
                multiplier *= InfernalResolveDamageMultiplier;

            return Mathf.Max(1, Mathf.RoundToInt(incoming * multiplier));
        }

        internal static void TryActivateDefensiveEffects(
            Character defender,
            Character attacker,
            int hpBeforeHit)
        {
            if (defender == null ||
                defender.MyStats == null ||
                !defender.Alive ||
                defender.MyStats.CurrentHP <= 0 ||
                defender.MyStats.CurrentMaxHP <= 0)
                return;

            if (!IsEquipped(defender, VarkeshEquipment.HeartOfTheDeathlessId))
                return;

            float beforeFraction = Mathf.Clamp01((float)hpBeforeHit / defender.MyStats.CurrentMaxHP);
            float afterFraction = Mathf.Clamp01((float)defender.MyStats.CurrentHP / defender.MyStats.CurrentMaxHP);

            if (beforeFraction > RefuseDeathThreshold &&
                afterFraction <= RefuseDeathThreshold &&
                CooldownReady(RefuseDeathCooldowns, defender))
            {
                RefuseDeathCooldowns[defender] = Time.time + RefuseDeathCooldown;
                RefuseDeathExpiries[defender] = Time.time + RefuseDeathDuration;
                Recalculate(defender);
                AddCombatMessageForPlayerSource(defender, "The Heart of the Deathless refuses death!", "purple");
            }
        }

        internal static void ModifyHealing(
            Stats targetStats,
            Spell spell,
            ref int amount,
            bool isMana,
            Character source)
        {
            if (_applyingGeneratedHeal ||
                isMana ||
                amount <= 0 ||
                targetStats == null ||
                source == null ||
                spell == null)
                return;

            Character target = targetStats.GetComponent<Character>();
            if (target == null)
                return;

            // Heart of the Deathless: +25% healing received during Refuse Death.
            if (HasActive(RefuseDeathExpiries, target))
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * RefuseDeathHealingMultiplier));

            // Existing Soulkeeper's Grace buff affects subsequent heals.
            if (HasActive(SoulkeepersGraceExpiries, source))
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * SoulkeepersGraceHealingMultiplier));

            // Lantern emergency heal: the triggering direct heal itself gets +25%.
            if (IsEquipped(source, VarkeshEquipment.LanternOfDepartedSoulsId) &&
                spell.SpellDurationInTicks <= 0 &&
                target.MyStats != null &&
                target.MyStats.CurrentMaxHP > 0 &&
                ((float)target.MyStats.CurrentHP / target.MyStats.CurrentMaxHP) < SoulkeepersGraceThreshold &&
                CooldownReady(SoulkeepersGraceCooldowns, source))
            {
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * SoulkeepersGraceEmergencyMultiplier));
                SoulkeepersGraceCooldowns[source] = Time.time + SoulkeepersGraceCooldown;
                SoulkeepersGraceExpiries[source] = Time.time + SoulkeepersGraceDuration;
                AddCombatMessageForPlayerSource(source, "Soulkeeper's Grace answers a wounded ally!", "green");
            }

            // Ashen Grace is a simple outgoing healing multiplier.
            if (HasActive(AshenGraceExpiries, source))
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * AshenGraceHealingMultiplier));

        }


        internal static void TryAshenCovenantOnCast(
            CastSpell castSpell,
            Spell spell,
            bool castSucceeded)
        {
            if (!castSucceeded ||
                castSpell == null ||
                spell == null ||
                spell.Type != Spell.SpellType.Beneficial)
                return;

            Character source = castSpell.MyChar;
            if (source == null ||
                !IsEquipped(source, GorathEquipment.AshenCovenantId) ||
                !Roll(AshenCovenantChance))
                return;

            int refund = Mathf.Max(
                0,
                Mathf.RoundToInt(spell.ManaCost * AshenCovenantManaRefundPercent));

            RestoreMana(source, refund);
            AshenGraceExpiries[source] = Time.time + AshenGraceDuration;
            AddCombatMessageForPlayerSource(
                source,
                "Ashen Covenant refunds " + refund + " mana and grants Ashen Grace!",
                "orange");
        }

        // ============================================================
        // CALCULATED BUFF STATS
        // ============================================================

        internal static void ApplyCalculatedBonuses(Stats stats)
        {
            if (stats == null || stats.Myself == null)
                return;

            Character character = stats.Myself;

            if (HasActive(DeathsMomentumExpiries, character))
                ApplyAttackHaste(stats, DeathsMomentumHastePercent);

            if (HasActive(RefuseDeathExpiries, character))
                stats.CurrentAC += RefuseDeathArmor;

            if (HasActive(GravewindExpiries, character))
            {
                AddPrivateInt(stats, CurrentDexField, GravewindDexterity);
                AddPrivateInt(stats, CurrentAgiField, GravewindAgility);
                ApplyAttackHaste(stats, GravewindHastePercent);
            }

            if (HasActive(InfernalFrenzyExpiries, character))
                ApplyAttackHaste(stats, InfernalFrenzyHastePercent);
        }

        private static void ApplyAttackHaste(Stats stats, float percent)
        {
            if (stats == null || percent <= 0f)
                return;

            float multiplier = 1f - (percent / 100f);
            stats.CurrentMHAtkDelay = Mathf.Max(1f, stats.CurrentMHAtkDelay * multiplier);
            stats.CurrentOHAtkDelay = Mathf.Max(1f, stats.CurrentOHAtkDelay * multiplier);
        }

        private static void AddPrivateInt(Stats stats, FieldInfo field, int amount)
        {
            if (stats == null || field == null || amount == 0)
                return;
            try
            {
                int value = (int)field.GetValue(stats);
                field.SetValue(stats, value + amount);
            }
            catch { }
        }

        // ============================================================
        // INFERNAL BASTION — BASH
        // ============================================================

        internal static void TryDemonicChallenge(UseSkill useSkill, Skill skill)
        {
            if (useSkill == null ||
                skill == null ||
                string.IsNullOrEmpty(skill.SkillName) ||
                skill.SkillName.IndexOf("Bash", StringComparison.OrdinalIgnoreCase) < 0)
                return;

            PlayerControl playerControl = useSkill.GetComponent<PlayerControl>();
            if (playerControl == null ||
                (GameData.PlayerControl != null && playerControl != GameData.PlayerControl))
                return;

            Character player = playerControl.Myself;
            if (player == null || !IsEquipped(player, GorathEquipment.InfernalBastionId))
                return;

            int instanceId = useSkill.GetInstanceID();
            float lastTime;
            if (LastBashInvocation.TryGetValue(instanceId, out lastTime) &&
                Time.time - lastTime < BashDebounceSeconds)
                return;

            LastBashInvocation[instanceId] = Time.time;

            if (!Roll(DemonicChallengeChance))
                return;

            InfernalResolveExpiries[player] = Time.time + InfernalResolveDuration;

            Character target = GameData.PlayerControl != null
                ? GameData.PlayerControl.CurrentTarget
                : null;

            TryApplyTaunt(target, player, DemonicChallengeThreat);
            AddCombatMessageForPlayerSource(player, "Demonic Challenge grants Infernal Resolve!", "orange");
        }

        private static void TryApplyTaunt(Character target, Character source, int aggroAmount)
        {
            if (target == null || source == null || target.MyNPC == null)
                return;

            NPC npc = target.MyNPC;
            bool numericThreatApplied = TryInvokeNumericThreatApi(npc, source, aggroAmount);

            try { npc.AggroOn(source); }
            catch
            {
                try { npc.ForceAggroOn(source); }
                catch { }
            }

            Plugin.NativeLog.LogInfo(
                "[GorathEquipment] Demonic Challenge taunt applied. RequestedAggro=[" +
                aggroAmount + "] NumericThreatApi=[" + numericThreatApplied + "].");
        }

        private static bool TryInvokeNumericThreatApi(NPC npc, Character source, int amount)
        {
            if (npc == null || source == null)
                return false;

            MethodInfo[] methods = npc.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method == null)
                    continue;

                string name = method.Name ?? string.Empty;
                if (name.IndexOf("aggro", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("hate", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("threat", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 2)
                    continue;

                int characterIndex = -1;
                int numericIndex = -1;

                for (int p = 0; p < parameters.Length; p++)
                {
                    Type parameterType = parameters[p].ParameterType;
                    if (typeof(Character).IsAssignableFrom(parameterType))
                        characterIndex = p;
                    else if (parameterType == typeof(int) || parameterType == typeof(float))
                        numericIndex = p;
                }

                if (characterIndex < 0 || numericIndex < 0)
                    continue;

                object[] args = new object[2];
                args[characterIndex] = source;
                args[numericIndex] = parameters[numericIndex].ParameterType == typeof(float)
                    ? (object)(float)amount
                    : amount;

                try
                {
                    method.Invoke(npc, args);
                    return true;
                }
                catch { }
            }

            return false;
        }

        // ============================================================
        // PERIODIC / DAMAGE HELPERS
        // ============================================================

        private static void ApplyOrStackPeriodicDamage(
            Character target,
            Character source,
            string effectId,
            int baseDamage,
            int ticks,
            int maxBonusStacks,
            GameData.DamageType damageType)
        {
            if (target == null || source == null || ticks <= 0)
                return;

            float nextTick = Time.time + PeriodicTickInterval;

            for (int i = 0; i < PeriodicDamage.Count; i++)
            {
                PeriodicDamageState state = PeriodicDamage[i];
                if (state != null &&
                    state.Target == target &&
                    state.Source == source &&
                    string.Equals(state.EffectId, effectId, StringComparison.OrdinalIgnoreCase))
                {
                    state.BonusStacks = Mathf.Min(state.MaxBonusStacks, state.BonusStacks + 1);
                    state.BaseDamage = baseDamage;
                    state.TicksRemaining = ticks;
                    state.NextTickAt = nextTick;
                    state.DamageType = damageType;
                    return;
                }
            }

            PeriodicDamage.Add(new PeriodicDamageState
            {
                Target = target,
                Source = source,
                EffectId = effectId,
                BaseDamage = baseDamage,
                BonusStacks = 0,
                MaxBonusStacks = maxBonusStacks,
                TicksRemaining = ticks,
                NextTickAt = nextTick,
                DamageType = damageType
            });
        }

        private static bool HasPeriodicDamage(Character target, Character source, string effectId)
        {
            for (int i = 0; i < PeriodicDamage.Count; i++)
            {
                PeriodicDamageState state = PeriodicDamage[i];
                if (state != null &&
                    state.Target == target &&
                    state.Source == source &&
                    state.TicksRemaining > 0 &&
                    string.Equals(state.EffectId, effectId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void ApplyPhysicalDamage(Character target, Character source, int damage)
        {
            if (target == null || source == null || damage <= 0)
                return;

            bool previous = _applyingGeneratedDamage;
            _applyingGeneratedDamage = true;
            try
            {
                target.DamageMe(
                    damage,
                    !source.isNPC,
                    GameData.DamageType.Physical,
                    source,
                    true,
                    false,
                    0);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning("[LateWorldBossEquipment] generated physical damage failed: " + exception.Message);
            }
            finally
            {
                _applyingGeneratedDamage = previous;
            }
        }

        private static void ApplyMagicDamage(
            Character target,
            Character source,
            int damage,
            GameData.DamageType damageType)
        {
            if (target == null || source == null || damage <= 0)
                return;

            bool previous = _applyingGeneratedDamage;
            _applyingGeneratedDamage = true;
            try
            {
                target.MagicDamageMe(
                    damage,
                    !source.isNPC,
                    damageType,
                    source,
                    0f,
                    damage);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning("[LateWorldBossEquipment] generated magic damage failed: " + exception.Message);
            }
            finally
            {
                _applyingGeneratedDamage = previous;
            }
        }

        private static void RestoreMana(Character source, int amount)
        {
            if (source == null || source.MyStats == null || amount <= 0)
                return;

            try
            {
                source.MyStats.CurrentMana = Mathf.Min(
                    source.MyStats.GetCurrentMaxMana(),
                    source.MyStats.CurrentMana + amount);
            }
            catch { }
        }

        private static bool Roll(float chance)
        {
            return chance >= 100f || UnityEngine.Random.Range(0f, 100f) < chance;
        }

        private static bool CooldownReady(Dictionary<Character, float> cooldowns, Character character)
        {
            if (character == null)
                return false;
            float readyAt;
            return !cooldowns.TryGetValue(character, out readyAt) || Time.time >= readyAt;
        }

        private static bool HasActive(Dictionary<Character, float> expiries, Character character)
        {
            if (character == null)
                return false;
            float expiry;
            return expiries.TryGetValue(character, out expiry) && Time.time < expiry;
        }

        private static void Recalculate(Character character)
        {
            if (character == null || character.MyStats == null)
                return;
            try { character.MyStats.CalcStats(); }
            catch { }
        }

        private static bool HasBowEquipped(Character character)
        {
            if (character == null)
                return false;

            Inventory inventory = character.GetComponent<Inventory>();
            if (inventory == null &&
                GameData.PlayerControl != null &&
                character == GameData.PlayerControl.Myself)
                inventory = GameData.PlayerInv;

            if (inventory == null || inventory.EquippedItems == null)
                return false;

            for (int i = 0; i < inventory.EquippedItems.Count; i++)
            {
                Item item = inventory.EquippedItems[i];
                if (item != null && item.IsBow)
                    return true;
            }
            return false;
        }

        internal static bool IsEquipped(Character character, string itemId)
        {
            if (character == null || string.IsNullOrEmpty(itemId))
                return false;

            Inventory inventory = character.GetComponent<Inventory>();
            if (inventory == null &&
                GameData.PlayerControl != null &&
                character == GameData.PlayerControl.Myself)
                inventory = GameData.PlayerInv;

            if (inventory == null || inventory.EquippedItems == null)
                return false;

            for (int i = 0; i < inventory.EquippedItems.Count; i++)
            {
                Item item = inventory.EquippedItems[i];
                if (item != null &&
                    string.Equals(item.Id, itemId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void AddCombatMessageForPlayerSource(Character source, string text, string color)
        {
            if (source == null ||
                GameData.PlayerControl == null ||
                source != GameData.PlayerControl.Myself)
                return;

            try
            {
                UpdateSocialLog.CombatLogAdd(
                    new ChatLogLine(
                        text,
                        ChatLogLine.LogType.PlayerSpellResults,
                        color ?? string.Empty));
            }
            catch { }
        }
    }

    internal sealed class LateWorldBossEquipmentEffectsController : MonoBehaviour
    {
        private void Update()
        {
            LateWorldBossEquipmentEffects.UpdateEffects();
        }
    }

    // ================================================================
    // DAMAGE HOOKS
    // ================================================================

    [HarmonyPatch(
        typeof(Character),
        "DamageMe",
        new Type[]
        {
            typeof(int), typeof(bool), typeof(GameData.DamageType), typeof(Character),
            typeof(bool), typeof(bool), typeof(int)
        })]
    internal static class Patch_LateWorldBossEquipmentPhysicalDamage
    {
        [HarmonyPrefix]
        private static void Prefix(
            Character __instance,
            ref int _incdmg,
            bool _fromPlayer,
            GameData.DamageType _dmgType,
            Character _attacker,
            bool _animEffect,
            bool _criticalHit,
            int _bonusDmg,
            out int __state)
        {
            __state = __instance != null && __instance.MyStats != null
                ? __instance.MyStats.CurrentHP
                : 0;

            if (__instance != null && _incdmg > 0)
                _incdmg = LateWorldBossEquipmentEffects.ApplyIncomingReduction(__instance, _incdmg, _dmgType);
        }

        [HarmonyPostfix]
        private static void Postfix(
            Character __instance,
            int _incdmg,
            bool _fromPlayer,
            GameData.DamageType _dmgType,
            Character _attacker,
            bool _animEffect,
            bool _criticalHit,
            int _bonusDmg,
            int __result,
            int __state)
        {
            if (__instance != null && !LateWorldBossEquipmentEffects.IsApplyingGeneratedDamage)
                LateWorldBossEquipmentEffects.TryActivateDefensiveEffects(__instance, _attacker, __state);

            if (__result <= 0 ||
                _attacker == null ||
                LateWorldBossEquipmentEffects.IsApplyingGeneratedDamage ||
                _dmgType != GameData.DamageType.Physical)
                return;

            LateWorldBossEquipmentEffects.TryProcPhysicalDamage(__instance, _attacker);
        }
    }

    [HarmonyPatch(
        typeof(Character),
        "MagicDamageMe",
        new Type[]
        {
            typeof(int), typeof(bool), typeof(GameData.DamageType), typeof(Character),
            typeof(float), typeof(int)
        })]
    internal static class Patch_LateWorldBossEquipmentMagicDamage
    {
        [HarmonyPrefix]
        private static void Prefix(
            Character __instance,
            ref int _dmg,
            bool _fromPlayer,
            GameData.DamageType _dmgType,
            Character _attacker,
            float resistMod,
            int _baseDmg,
            out int __state)
        {
            __state = __instance != null && __instance.MyStats != null
                ? __instance.MyStats.CurrentHP
                : 0;

            if (__instance != null && _dmg > 0)
                _dmg = LateWorldBossEquipmentEffects.ApplyIncomingReduction(__instance, _dmg, _dmgType);
        }

        [HarmonyPostfix]
        private static void Postfix(
            Character __instance,
            int _dmg,
            bool _fromPlayer,
            GameData.DamageType _dmgType,
            Character _attacker,
            float resistMod,
            int _baseDmg,
            int __result,
            int __state)
        {
            if (__instance != null && !LateWorldBossEquipmentEffects.IsApplyingGeneratedDamage)
                LateWorldBossEquipmentEffects.TryActivateDefensiveEffects(__instance, _attacker, __state);

            if (__result <= 0 ||
                _attacker == null ||
                LateWorldBossEquipmentEffects.IsApplyingGeneratedDamage)
                return;

            LateWorldBossEquipmentEffects.TryProcMagicDamage(__instance, _attacker);
        }
    }

    // ================================================================
    // HEALING HOOK
    // ================================================================

    [HarmonyPatch(
        typeof(Stats),
        "HealMe",
        new Type[]
        {
            typeof(Spell), typeof(int), typeof(bool), typeof(bool), typeof(Character)
        })]
    internal static class Patch_LateWorldBossEquipmentHeal
    {
        [HarmonyPrefix]
        private static void Prefix(
            Stats __instance,
            Spell _spell,
            ref int _amt,
            bool _isCrit,
            bool _isMana,
            Character _source)
        {
            LateWorldBossEquipmentEffects.ModifyHealing(
                __instance,
                _spell,
                ref _amt,
                _isMana,
                _source);
        }
    }


    // ================================================================
    // ASHEN COVENANT — BENEFICIAL SPELL CAST
    // ================================================================

    [HarmonyPatch(
        typeof(CastSpell),
        "StartSpell",
        new Type[]
        {
            typeof(Spell), typeof(Stats)
        })]
    internal static class Patch_LateWorldBossEquipmentBeneficialCast
    {
        [HarmonyPostfix]
        private static void Postfix(
            CastSpell __instance,
            Spell _spell,
            Stats _target,
            bool __result)
        {
            LateWorldBossEquipmentEffects.TryAshenCovenantOnCast(
                __instance,
                _spell,
                __result);
        }
    }

    // ================================================================
    // ACTIVE BUFF CALCULATED STATS
    // ================================================================

    [HarmonyPatch(typeof(Stats), "CalcStats")]
    internal static class Patch_LateWorldBossEquipmentCalcStats
    {
        [HarmonyPostfix]
        private static void Postfix(Stats __instance)
        {
            LateWorldBossEquipmentEffects.ApplyCalculatedBonuses(__instance);
        }
    }

    // ================================================================
    // INFERNAL BASTION BASH DETECTION
    // ================================================================

    [HarmonyPatch]
    internal static class Patch_LateWorldBossEquipmentBash
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo[] methods = typeof(UseSkill).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method == null || method.IsAbstract || method.IsGenericMethod)
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                bool hasSkill = false;
                for (int p = 0; p < parameters.Length; p++)
                {
                    if (parameters[p].ParameterType == typeof(Skill))
                    {
                        hasSkill = true;
                        break;
                    }
                }

                if (hasSkill)
                    yield return method;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(UseSkill __instance, object[] __args)
        {
            if (__instance == null || __args == null)
                return;

            Skill skill = null;
            for (int i = 0; i < __args.Length; i++)
            {
                skill = __args[i] as Skill;
                if (skill != null)
                    break;
            }

            if (skill != null)
                LateWorldBossEquipmentEffects.TryDemonicChallenge(__instance, skill);
        }
    }
}
