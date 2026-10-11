using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Gameplay effects for Moldermaw equipment.
    ///
    /// Native Erenshor weapon/bow/wand proc fields own the proc chance and
    /// initial hit. FOTF only owns the timed follow-up states that cannot be
    /// expressed exactly by the native three-second status-effect tick loop.
    /// </summary>
    internal static class MoldermawEquipmentEffects
    {
        internal const string SporeboundProcSpellId =
            "FOTF_MOLDERMAW_BLIGHTED_ERUPTION";

        internal const string RotcleaverProcSpellId =
            "FOTF_MOLDERMAW_FESTERING_CLEAVE";

        internal const string SporepiercerProcSpellId =
            "FOTF_MOLDERMAW_SPOREBURST";

        internal const int SporeboundInitialDamage = 2200;
        internal const int SporeboundTickDamage = 500;
        internal const float SporeboundDuration = 8f;
        internal const int SporeboundTicks = 4;

        internal const int RotcleaverInitialDamage = 4700;
        internal const float FesteringWoundDuration = 8f;
        internal const float FesteringWoundMeleeMultiplier = 1.06f;

        internal const int SporepiercerInitialDamage = 2800;
        internal const int SporepiercerTickDamage = 450;
        internal const float SporepiercerDuration = 8f;
        internal const int SporepiercerTicks = 4;

        internal const float LingeringBloomProcChance = 15f;
        internal const int LingeringBloomTickHealing = 250;
        internal const int LingeringBloomTicks = 3;
        internal const float LingeringBloomDuration = 6f;
        internal const int BloomingReservoirMana = 400;

        internal const float RootsOfDefianceProcChance = 90f;
        internal const int RootsOfDefianceAggro = 6800;
        internal const float StonebarkDuration = 6f;
        internal const int StonebarkArmor = 100;
        internal const float StonebarkPhysicalMultiplier = 0.92f;

        private const float PeriodicTickInterval = 2f;
        private const float BashDebounceSeconds = 0.30f;

        private static readonly List<PeriodicDamageState> DamageStates =
            new List<PeriodicDamageState>();

        private static readonly List<FesteringWoundState> FesteringWounds =
            new List<FesteringWoundState>();

        private static readonly List<LingeringBloomState> BloomStates =
            new List<LingeringBloomState>();

        private static readonly Dictionary<Character, float> StonebarkExpiries =
            new Dictionary<Character, float>();

        private static readonly Dictionary<int, float> LastBashInvocation =
            new Dictionary<int, float>();

        private static readonly FieldInfo CurrentMaxManaField =
            AccessTools.Field(
                typeof(Stats),
                "CurrentMaxMana");

        private static MoldermawEquipmentEffectsController _controller;
        private static bool _applyingLingeringBloom;

        internal static Spell SporeboundProcSpell
        {
            get;
            private set;
        }

        internal static Spell RotcleaverProcSpell
        {
            get;
            private set;
        }

        internal static Spell SporepiercerProcSpell
        {
            get;
            private set;
        }


        // ============================================================
        // LIFECYCLE
        // ============================================================

        internal static void Initialize()
        {
            if (_controller != null)
            {
                return;
            }

            GameObject gameObject =
                new GameObject(
                    "FOTF_MoldermawEquipmentEffects");

            UnityEngine.Object.DontDestroyOnLoad(
                gameObject);

            _controller =
                gameObject.AddComponent<MoldermawEquipmentEffectsController>();
        }

        internal static void Shutdown()
        {
            DamageStates.Clear();
            FesteringWounds.Clear();
            BloomStates.Clear();
            StonebarkExpiries.Clear();
            LastBashInvocation.Clear();

            if (_controller != null)
            {
                UnityEngine.Object.Destroy(
                    _controller.gameObject);

                _controller = null;
            }
        }


        // ============================================================
        // PROC SPELLS
        // ============================================================

        internal static void RegisterProcSpells(
            SpellDB database)
        {
            if (database == null ||
                database.SpellDatabase == null)
            {
                return;
            }

            SporeboundProcSpell =
                ResolveOrCreateProcSpell(
                    database,
                    SporeboundProcSpellId,
                    "Blighted Eruption",
                    SporeboundInitialDamage,
                    GameData.DamageType.Magic);

            RotcleaverProcSpell =
                ResolveOrCreateProcSpell(
                    database,
                    RotcleaverProcSpellId,
                    "Festering Cleave",
                    RotcleaverInitialDamage,
                    GameData.DamageType.Physical);

            SporepiercerProcSpell =
                ResolveOrCreateProcSpell(
                    database,
                    SporepiercerProcSpellId,
                    "Sporeburst",
                    SporepiercerInitialDamage,
                    GameData.DamageType.Poison);

            MoldermawEquipment.RelinkProcSpells();
        }

        private static Spell ResolveOrCreateProcSpell(
            SpellDB database,
            string id,
            string name,
            int damage,
            GameData.DamageType damageType)
        {
            Spell existing =
                FindSpellById(
                    database,
                    id);

            if (existing != null)
            {
                ConfigureProcSpell(
                    existing,
                    id,
                    name,
                    damage,
                    damageType);

                return existing;
            }

            Spell template =
                FindDirectDamageTemplate(
                    database);

            if (template == null)
            {
                Plugin.NativeLog.LogError(
                    "[MoldermawEquipment] could not find a direct-damage spell template for [" +
                    name +
                    "].");

                return null;
            }

            Spell spell =
                UnityEngine.Object.Instantiate(
                    template);

            if (spell == null)
            {
                return null;
            }

            ConfigureProcSpell(
                spell,
                id,
                name,
                damage,
                damageType);

            CustomSpellRegistry.Register(
                database,
                spell);

            return spell;
        }

        private static void ConfigureProcSpell(
            Spell spell,
            string id,
            string name,
            int damage,
            GameData.DamageType damageType)
        {
            spell.name =
                "FOTF - " +
                name;

            spell.Id = id;
            spell.SpellName = name;
            spell.SpellDesc =
                GetProcSpellDescription(
                    id,
                    name);

            spell.Type = Spell.SpellType.Damage;
            spell.Line = Spell.SpellLine.Direct_Damage;
            spell.RequiredLevel = 1;
            spell.ManaCost = 0;
            spell.TargetDamage = damage;
            spell.Aggro = damage;
            spell.SpellChargeTime = 0f;
            spell.Cooldown = 0f;
            spell.SpellDurationInTicks = 0;
            spell.StatusEffectToApply = null;
            spell.MyDamageType = damageType;
            spell.SpellRange = 60f;
            spell.SelfOnly = false;
            spell.CrowdControlSpell = false;
            spell.SimUsable = false;
            spell.NoResonate = true;
            spell.CanHitPlayers = true;
            spell.UsedBy = new List<Class>();
            spell.hideFlags = HideFlags.HideAndDontSave;
        }

        private static string GetProcSpellDescription(
            string id,
            string name)
        {
            if (string.Equals(
                    id,
                    SporeboundProcSpellId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    "Deals 2,200 Magic damage and infects the target with " +
                    "Blighted Eruption, dealing 500 Magic damage every " +
                    "2 seconds for 8 seconds. Reprocs refresh the infection " +
                    "rather than stacking.";
            }

            if (string.Equals(
                    id,
                    RotcleaverProcSpellId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    "Deals 4,700 Physical damage and applies Festering Wound " +
                    "for 8 seconds. While wounded, the target takes 6% " +
                    "increased melee damage from the wielder. Reprocs refresh " +
                    "the wound rather than stacking.";
            }

            if (string.Equals(
                    id,
                    SporepiercerProcSpellId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    "Deals 2,800 Poison damage and applies Blighted Spores, " +
                    "dealing 450 Poison damage every 2 seconds for 8 seconds. " +
                    "Reprocs refresh the spores rather than stacking.";
            }

            return
                name +
                " is triggered by Moldermaw equipment.";
        }


        private static Spell FindSpellById(
            SpellDB database,
            string id)
        {
            if (database == null ||
                database.SpellDatabase == null)
            {
                return null;
            }

            for (int i = 0;
                 i < database.SpellDatabase.Length;
                 i++)
            {
                Spell spell =
                    database.SpellDatabase[i];

                if (spell != null &&
                    string.Equals(
                        spell.Id,
                        id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return spell;
                }
            }

            return null;
        }

        private static Spell FindDirectDamageTemplate(
            SpellDB database)
        {
            for (int i = 0;
                 i < database.SpellDatabase.Length;
                 i++)
            {
                Spell spell =
                    database.SpellDatabase[i];

                if (spell == null ||
                    spell.Type != Spell.SpellType.Damage ||
                    spell.TargetDamage <= 0)
                {
                    continue;
                }

                return spell;
            }

            return null;
        }


        // ============================================================
        // PERIODIC EFFECT STATE
        // ============================================================

        internal static void NotifyProcDamage(
            Character target,
            Character source,
            int baseDamage)
        {
            if (target == null ||
                source == null)
            {
                return;
            }

            if (baseDamage == SporeboundInitialDamage &&
                IsEquipped(source, MoldermawEquipment.SporeboundScepterId))
            {
                ApplyOrRefreshDamageState(
                    target,
                    source,
                    MoldermawEquipment.SporeboundScepterId,
                    SporeboundTickDamage,
                    SporeboundTicks,
                    GameData.DamageType.Magic);

                return;
            }

            if (baseDamage == RotcleaverInitialDamage &&
                IsEquipped(source, MoldermawEquipment.RotcleaverId))
            {
                ApplyOrRefreshFesteringWound(
                    target,
                    source);

                return;
            }

            if (baseDamage == SporepiercerInitialDamage &&
                IsEquipped(source, MoldermawEquipment.SporepiercerId))
            {
                ApplyOrRefreshDamageState(
                    target,
                    source,
                    MoldermawEquipment.SporepiercerId,
                    SporepiercerTickDamage,
                    SporepiercerTicks,
                    GameData.DamageType.Poison);
            }
        }

        private static void ApplyOrRefreshDamageState(
            Character target,
            Character source,
            string effectId,
            int tickDamage,
            int ticks,
            GameData.DamageType damageType)
        {
            float now =
                Time.time;

            for (int i = 0;
                 i < DamageStates.Count;
                 i++)
            {
                PeriodicDamageState state =
                    DamageStates[i];

                if (state.Target == target &&
                    state.Source == source &&
                    string.Equals(
                        state.EffectId,
                        effectId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    state.TickDamage = tickDamage;
                    state.DamageType = damageType;
                    state.TicksRemaining = ticks;
                    state.NextTickAt = now + PeriodicTickInterval;
                    state.ExpiresAt = now + ticks * PeriodicTickInterval;
                    return;
                }
            }

            DamageStates.Add(
                new PeriodicDamageState
                {
                    Target = target,
                    Source = source,
                    EffectId = effectId,
                    TickDamage = tickDamage,
                    DamageType = damageType,
                    TicksRemaining = ticks,
                    NextTickAt = now + PeriodicTickInterval,
                    ExpiresAt = now + ticks * PeriodicTickInterval
                });
        }

        private static void ApplyOrRefreshFesteringWound(
            Character target,
            Character source)
        {
            float expires =
                Time.time +
                FesteringWoundDuration;

            for (int i = 0;
                 i < FesteringWounds.Count;
                 i++)
            {
                FesteringWoundState state =
                    FesteringWounds[i];

                if (state.Target == target &&
                    state.Source == source)
                {
                    state.ExpiresAt = expires;
                    return;
                }
            }

            FesteringWounds.Add(
                new FesteringWoundState
                {
                    Target = target,
                    Source = source,
                    ExpiresAt = expires
                });
        }

        internal static bool HasFesteringWound(
            Character target,
            Character source)
        {
            if (target == null ||
                source == null)
            {
                return false;
            }

            float now =
                Time.time;

            for (int i = FesteringWounds.Count - 1;
                 i >= 0;
                 i--)
            {
                FesteringWoundState state =
                    FesteringWounds[i];

                if (!IsAlive(state.Target) ||
                    !IsAlive(state.Source) ||
                    state.ExpiresAt <= now)
                {
                    FesteringWounds.RemoveAt(i);
                    continue;
                }

                if (state.Target == target &&
                    state.Source == source)
                {
                    return true;
                }
            }

            return false;
        }


        // ============================================================
        // LINGERING BLOOM
        // ============================================================

        internal static void TryProcLingeringBloom(
            Spell spell,
            Stats recipient,
            Character source,
            int attemptedHealing,
            bool isMana)
        {
            if (_applyingLingeringBloom ||
                spell == null ||
                recipient == null ||
                recipient.Myself == null ||
                source == null ||
                isMana ||
                attemptedHealing <= 0 ||
                !IsEquipped(source, MoldermawEquipment.BloomId))
            {
                return;
            }

            // Direct heals only. Beneficial/stat-only spells do not proc it.
            if (spell.Type != Spell.SpellType.Heal &&
                spell.TargetHealing <= 0)
            {
                return;
            }

            if (UnityEngine.Random.Range(0f, 100f) >=
                LingeringBloomProcChance)
            {
                return;
            }

            float now =
                Time.time;

            for (int i = 0;
                 i < BloomStates.Count;
                 i++)
            {
                LingeringBloomState state =
                    BloomStates[i];

                if (state.Target == recipient.Myself &&
                    state.Source == source)
                {
                    state.Spell = spell;
                    state.TicksRemaining = LingeringBloomTicks;
                    state.NextTickAt = now + PeriodicTickInterval;
                    state.ExpiresAt = now + LingeringBloomDuration;
                    return;
                }
            }

            BloomStates.Add(
                new LingeringBloomState
                {
                    Target = recipient.Myself,
                    Source = source,
                    Spell = spell,
                    TicksRemaining = LingeringBloomTicks,
                    NextTickAt = now + PeriodicTickInterval,
                    ExpiresAt = now + LingeringBloomDuration
                });
        }


        // ============================================================
        // STONEBARK / ROOTS OF DEFIANCE
        // ============================================================

        internal static bool HasStonebark(
            Character character)
        {
            if (character == null)
            {
                return false;
            }

            float expires;

            if (!StonebarkExpiries.TryGetValue(
                    character,
                    out expires))
            {
                return false;
            }

            if (expires <= Time.time ||
                !IsAlive(character))
            {
                StonebarkExpiries.Remove(character);
                return false;
            }

            return true;
        }

        internal static void TryRootsOfDefiance(
            UseSkill useSkill,
            Skill skill)
        {
            if (useSkill == null ||
                skill == null ||
                string.IsNullOrEmpty(skill.SkillName) ||
                skill.SkillName.IndexOf(
                    "Bash",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            PlayerControl playerControl =
                useSkill.GetComponent<PlayerControl>();

            if (playerControl == null ||
                (GameData.PlayerControl != null &&
                 playerControl != GameData.PlayerControl))
            {
                return;
            }

            Character player =
                playerControl.Myself;

            if (player == null ||
                !IsEquipped(
                    player,
                    MoldermawEquipment.BulwarkId))
            {
                return;
            }

            int instanceId =
                useSkill.GetInstanceID();

            float lastTime;

            if (LastBashInvocation.TryGetValue(
                    instanceId,
                    out lastTime) &&
                Time.time - lastTime < BashDebounceSeconds)
            {
                return;
            }

            LastBashInvocation[instanceId] =
                Time.time;

            if (UnityEngine.Random.Range(0f, 100f) >=
                RootsOfDefianceProcChance)
            {
                return;
            }

            StonebarkExpiries[player] =
                Time.time +
                StonebarkDuration;

            try
            {
                if (player.MyStats != null)
                {
                    player.MyStats.CalcStats();
                }
            }
            catch
            {
            }

            Character target =
                GameData.PlayerControl != null
                    ? GameData.PlayerControl.CurrentTarget
                    : null;

            TryApplyTaunt(
                target,
                player,
                RootsOfDefianceAggro);

            AddCombatMessage(
                "Stonebark hardens around you!",
                "green");
        }

        private static void TryApplyTaunt(
            Character target,
            Character source,
            int aggroAmount)
        {
            if (target == null ||
                source == null ||
                target.MyNPC == null)
            {
                return;
            }

            NPC npc =
                target.MyNPC;

            bool numericThreatApplied =
                TryInvokeNumericThreatApi(
                    npc,
                    source,
                    aggroAmount);

            try
            {
                // AggroOn is the native hostile entry point. Even when this
                // build exposes no callable numeric threat method, this makes
                // Roots of Defiance behave as an actual taunt instead of only
                // displaying an aggro number in text.
                npc.AggroOn(
                    source);
            }
            catch
            {
                try
                {
                    npc.ForceAggroOn(
                        source);
                }
                catch
                {
                }
            }

            Plugin.NativeLog.LogInfo(
                "[MoldermawEquipment] Roots of Defiance taunt applied. RequestedAggro=[" +
                aggroAmount +
                "] NumericThreatApi=[" +
                numericThreatApplied +
                "].");
        }

        private static bool TryInvokeNumericThreatApi(
            NPC npc,
            Character source,
            int amount)
        {
            if (npc == null ||
                source == null)
            {
                return false;
            }

            MethodInfo[] methods =
                npc.GetType().GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            for (int i = 0;
                 i < methods.Length;
                 i++)
            {
                MethodInfo method =
                    methods[i];

                if (method == null)
                {
                    continue;
                }

                string name =
                    method.Name ??
                    string.Empty;

                if (name.IndexOf("aggro", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("hate", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("threat", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                ParameterInfo[] parameters =
                    method.GetParameters();

                if (parameters.Length != 2)
                {
                    continue;
                }

                int characterIndex = -1;
                int numericIndex = -1;

                for (int p = 0;
                     p < parameters.Length;
                     p++)
                {
                    Type parameterType =
                        parameters[p].ParameterType;

                    if (typeof(Character).IsAssignableFrom(parameterType))
                    {
                        characterIndex = p;
                    }
                    else if (parameterType == typeof(int) ||
                             parameterType == typeof(float))
                    {
                        numericIndex = p;
                    }
                }

                if (characterIndex < 0 ||
                    numericIndex < 0)
                {
                    continue;
                }

                object[] args =
                    new object[2];

                args[characterIndex] =
                    source;

                args[numericIndex] =
                    parameters[numericIndex].ParameterType == typeof(float)
                        ? (object)(float)amount
                        : amount;

                try
                {
                    method.Invoke(
                        npc,
                        args);

                    return true;
                }
                catch
                {
                }
            }

            return false;
        }


        // ============================================================
        // CALCSTATS BONUSES
        // ============================================================

        internal static void ApplyCalculatedBonuses(
            Stats stats)
        {
            if (stats == null ||
                stats.Myself == null)
            {
                return;
            }

            Character character =
                stats.Myself;

            if (IsEquipped(
                    character,
                    MoldermawEquipment.BloomId) &&
                CurrentMaxManaField != null)
            {
                try
                {
                    int currentMax =
                        (int)CurrentMaxManaField.GetValue(stats);

                    CurrentMaxManaField.SetValue(
                        stats,
                        currentMax + BloomingReservoirMana);
                }
                catch
                {
                }
            }

            if (HasStonebark(
                    character))
            {
                stats.CurrentAC +=
                    StonebarkArmor;
            }
        }


        // ============================================================
        // UPDATE
        // ============================================================

        internal static void UpdateEffects()
        {
            UpdateDamageStates();
            UpdateFesteringWounds();
            UpdateBloomStates();
            UpdateStonebark();
        }

        private static void UpdateDamageStates()
        {
            float now =
                Time.time;

            for (int i = DamageStates.Count - 1;
                 i >= 0;
                 i--)
            {
                PeriodicDamageState state =
                    DamageStates[i];

                if (state == null ||
                    !IsAlive(state.Target) ||
                    !IsAlive(state.Source) ||
                    state.TicksRemaining <= 0 ||
                    now > state.ExpiresAt + 0.25f)
                {
                    DamageStates.RemoveAt(i);
                    continue;
                }

                if (now < state.NextTickAt)
                {
                    continue;
                }

                try
                {
                    state.Target.MagicDamageMe(
                        state.TickDamage,
                        !state.Source.isNPC,
                        state.DamageType,
                        state.Source,
                        0f,
                        state.TickDamage);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogWarning(
                        "[MoldermawEquipment] periodic damage tick failed: " +
                        exception.Message);
                }

                state.TicksRemaining--;
                state.NextTickAt += PeriodicTickInterval;

                if (state.TicksRemaining <= 0)
                {
                    DamageStates.RemoveAt(i);
                }
            }
        }

        private static void UpdateFesteringWounds()
        {
            float now =
                Time.time;

            for (int i = FesteringWounds.Count - 1;
                 i >= 0;
                 i--)
            {
                FesteringWoundState state =
                    FesteringWounds[i];

                if (state == null ||
                    !IsAlive(state.Target) ||
                    !IsAlive(state.Source) ||
                    state.ExpiresAt <= now)
                {
                    FesteringWounds.RemoveAt(i);
                }
            }
        }

        private static void UpdateBloomStates()
        {
            float now =
                Time.time;

            for (int i = BloomStates.Count - 1;
                 i >= 0;
                 i--)
            {
                LingeringBloomState state =
                    BloomStates[i];

                if (state == null ||
                    !IsAlive(state.Target) ||
                    !IsAlive(state.Source) ||
                    state.TicksRemaining <= 0 ||
                    now > state.ExpiresAt + 0.25f)
                {
                    BloomStates.RemoveAt(i);
                    continue;
                }

                if (now < state.NextTickAt)
                {
                    continue;
                }

                Stats targetStats =
                    state.Target.MyStats;

                if (targetStats != null)
                {
                    try
                    {
                        _applyingLingeringBloom = true;

                        targetStats.HealMe(
                            state.Spell,
                            LingeringBloomTickHealing,
                            false,
                            false,
                            state.Source);
                    }
                    catch (Exception exception)
                    {
                        Plugin.NativeLog.LogWarning(
                            "[MoldermawEquipment] Lingering Bloom tick failed: " +
                            exception.Message);
                    }
                    finally
                    {
                        _applyingLingeringBloom = false;
                    }
                }

                state.TicksRemaining--;
                state.NextTickAt += PeriodicTickInterval;

                if (state.TicksRemaining <= 0)
                {
                    BloomStates.RemoveAt(i);
                }
            }
        }

        private static void UpdateStonebark()
        {
            if (StonebarkExpiries.Count == 0)
            {
                return;
            }

            float now =
                Time.time;

            List<Character> expired =
                null;

            foreach (
                KeyValuePair<Character, float> pair
                in StonebarkExpiries)
            {
                if (pair.Key == null ||
                    pair.Value <= now ||
                    !IsAlive(pair.Key))
                {
                    if (expired == null)
                    {
                        expired =
                            new List<Character>();
                    }

                    expired.Add(pair.Key);
                }
            }

            if (expired == null)
            {
                return;
            }

            for (int i = 0;
                 i < expired.Count;
                 i++)
            {
                Character character =
                    expired[i];

                if (character != null)
                {
                    StonebarkExpiries.Remove(character);

                    try
                    {
                        if (character.MyStats != null)
                        {
                            character.MyStats.CalcStats();
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }


        // ============================================================
        // EQUIPMENT HELPERS
        // ============================================================

        internal static bool IsEquipped(
            Character character,
            string itemId)
        {
            if (character == null ||
                string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            Inventory inventory =
                character.GetComponent<Inventory>();

            if (inventory == null &&
                GameData.PlayerControl != null &&
                character == GameData.PlayerControl.Myself)
            {
                inventory =
                    GameData.PlayerInv;
            }

            if (inventory == null ||
                inventory.EquippedItems == null)
            {
                return false;
            }

            for (int i = 0;
                 i < inventory.EquippedItems.Count;
                 i++)
            {
                Item item =
                    inventory.EquippedItems[i];

                if (item != null &&
                    string.Equals(
                        item.Id,
                        itemId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAlive(
            Character character)
        {
            return
                character != null &&
                character.Alive &&
                character.MyStats != null &&
                character.MyStats.CurrentHP > 0;
        }

        private static void AddCombatMessage(
            string text,
            string color)
        {
            try
            {
                UpdateSocialLog.CombatLogAdd(
                    new ChatLogLine(
                        text,
                        ChatLogLine.LogType.PlayerSpellResults,
                        color ?? ""));
            }
            catch
            {
            }
        }


        // ============================================================
        // STATE TYPES
        // ============================================================

        private sealed class PeriodicDamageState
        {
            internal Character Target;
            internal Character Source;
            internal string EffectId;
            internal int TickDamage;
            internal GameData.DamageType DamageType;
            internal int TicksRemaining;
            internal float NextTickAt;
            internal float ExpiresAt;
        }

        private sealed class FesteringWoundState
        {
            internal Character Target;
            internal Character Source;
            internal float ExpiresAt;
        }

        private sealed class LingeringBloomState
        {
            internal Character Target;
            internal Character Source;
            internal Spell Spell;
            internal int TicksRemaining;
            internal float NextTickAt;
            internal float ExpiresAt;
        }
    }


    internal sealed class MoldermawEquipmentEffectsController :
        MonoBehaviour
    {
        private void Update()
        {
            MoldermawEquipmentEffects.UpdateEffects();
        }
    }


    // ================================================================
    // PROC FOLLOW-UP DETECTION
    // ================================================================

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
    internal static class Patch_MoldermawEquipmentProcDamage
    {
        [HarmonyPostfix]
        private static void Postfix(
            Character __instance,
            int _dmg,
            bool _fromPlayer,
            GameData.DamageType _dmgType,
            Character _attacker,
            float resistMod,
            int _baseDmg,
            int __result)
        {
            if (__result <= 0 ||
                __instance == null ||
                _attacker == null)
            {
                return;
            }

            MoldermawEquipmentEffects.NotifyProcDamage(
                __instance,
                _attacker,
                _baseDmg);
        }
    }


    // ================================================================
    // FESTERING WOUND + STONEBARK PHYSICAL MODIFIERS
    // ================================================================

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
    internal static class Patch_MoldermawEquipmentPhysicalDamage
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
            int _bonusDmg)
        {
            if (__instance == null ||
                _incdmg <= 0 ||
                _dmgType != GameData.DamageType.Physical)
            {
                return;
            }

            if (MoldermawEquipmentEffects.HasStonebark(
                    __instance))
            {
                _incdmg =
                    Mathf.Max(
                        1,
                        Mathf.RoundToInt(
                            _incdmg *
                            MoldermawEquipmentEffects.StonebarkPhysicalMultiplier));
            }

            if (_attacker != null &&
                _animEffect &&
                MoldermawEquipmentEffects.IsEquipped(
                    _attacker,
                    MoldermawEquipment.RotcleaverId) &&
                MoldermawEquipmentEffects.HasFesteringWound(
                    __instance,
                    _attacker))
            {
                _incdmg =
                    Mathf.Max(
                        1,
                        Mathf.RoundToInt(
                            _incdmg *
                            MoldermawEquipmentEffects.FesteringWoundMeleeMultiplier));
            }
        }
    }


    // ================================================================
    // LINGERING BLOOM
    // ================================================================

    [HarmonyPatch(
        typeof(Stats),
        "HealMe",
        new Type[]
        {
            typeof(Spell),
            typeof(int),
            typeof(bool),
            typeof(bool),
            typeof(Character)
        })]
    internal static class Patch_MoldermawEquipmentHealProc
    {
        [HarmonyPostfix]
        private static void Postfix(
            Stats __instance,
            Spell _spell,
            int _amt,
            bool _isCrit,
            bool _isMana,
            Character _source,
            int __result)
        {
            if (__result <= 0)
            {
                return;
            }

            MoldermawEquipmentEffects.TryProcLingeringBloom(
                _spell,
                __instance,
                _source,
                _amt,
                _isMana);
        }
    }


    // ================================================================
    // BLOOMING RESERVOIR / STONEBARK CALCULATED STATS
    // ================================================================

    [HarmonyPatch(
        typeof(Stats),
        "CalcStats")]
    internal static class Patch_MoldermawEquipmentCalcStats
    {
        [HarmonyPostfix]
        private static void Postfix(
            Stats __instance)
        {
            MoldermawEquipmentEffects.ApplyCalculatedBonuses(
                __instance);
        }
    }


    // ================================================================
    // ROOTS OF DEFIANCE — BASH DETECTION
    // ================================================================

    [HarmonyPatch]
    internal static class Patch_MoldermawEquipmentBash
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo[] methods =
                typeof(UseSkill).GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            for (int i = 0;
                 i < methods.Length;
                 i++)
            {
                MethodInfo method =
                    methods[i];

                if (method == null ||
                    method.IsAbstract ||
                    method.IsGenericMethod)
                {
                    continue;
                }

                ParameterInfo[] parameters =
                    method.GetParameters();

                bool hasSkill =
                    false;

                for (int p = 0;
                     p < parameters.Length;
                     p++)
                {
                    if (parameters[p].ParameterType == typeof(Skill))
                    {
                        hasSkill = true;
                        break;
                    }
                }

                if (hasSkill)
                {
                    yield return method;
                }
            }
        }

        [HarmonyPostfix]
        private static void Postfix(
            UseSkill __instance,
            object[] __args)
        {
            if (__instance == null ||
                __args == null)
            {
                return;
            }

            Skill skill =
                null;

            for (int i = 0;
                 i < __args.Length;
                 i++)
            {
                skill =
                    __args[i] as Skill;

                if (skill != null)
                {
                    break;
                }
            }

            if (skill != null)
            {
                MoldermawEquipmentEffects.TryRootsOfDefiance(
                    __instance,
                    skill);
            }
        }
    }
}
