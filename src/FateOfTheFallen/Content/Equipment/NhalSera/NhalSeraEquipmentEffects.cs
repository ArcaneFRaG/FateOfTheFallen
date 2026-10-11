using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Gameplay effects for Nhal'Sera's five final equipment drops.
    ///
    /// These effects deliberately use the shared CustomItemRegistry items as
    /// the source of truth.  The runtime only checks canonical item IDs in
    /// equipped inventories; no duplicate item registry is maintained here.
    /// </summary>
    internal static class NhalSeraEquipmentEffects
    {
        internal const string ManaRegenVSpellId =
            "FOTF_MANA_REGEN_V";

        // Rank V is fully owned by FOTF; Erenshor has no native Mana Regen V.
        // Stats.TickEffects consumes LevelScaledManaRestoration once per
        // three-second status tick. A coefficient of 5 restores 5 mana per
        // character level per tick (e.g. 250/tick at level 50).
        internal const float ManaRegenVPerLevelPerTick = 5f;

        internal const float LightlessEchoProcChance = 18f;
        internal const int LightlessEchoDamage = 3400;
        internal const int LightlessEchoManaRestore = 150;
        internal const float LightlessEchoCooldown = 5f;

        internal const float EventHorizonHealthThreshold = 0.35f;
        internal const float EventHorizonDuration = 10f;
        internal const float EventHorizonCooldown = 45f;
        internal const int EventHorizonArmor = 250;
        internal const float EventHorizonDamageMultiplier = 0.80f;

        internal const float VeiledBenedictionProcChance = 15f;
        internal const int VeiledBenedictionHealing = 900;
        internal const int VeiledBenedictionMagicResist = 8;
        internal const int VeiledBenedictionVoidResist = 8;
        internal const float VeiledBenedictionDuration = 8f;
        internal const float VeiledBenedictionCooldown = 20f;

        internal const float FracturedMomentumProcChance = 15f;
        internal const int FracturedMomentumDexterity = 20;
        internal const float FracturedMomentumHastePercent = 12f;
        internal const float FracturedMomentumArmorPenPercent = 8f;
        internal const float FracturedMomentumDuration = 8f;
        internal const float FracturedMomentumCooldown = 20f;

        internal const float ThresholdCommunionProcChance = 10f;
        internal const float ThresholdCommunionDuration = 12f;
        internal const float ThresholdCommunionCooldown = 30f;

        private static readonly Dictionary<Character, float> LightlessCooldowns =
            new Dictionary<Character, float>();

        private static readonly Dictionary<Character, float> EventGuardExpiries =
            new Dictionary<Character, float>();

        private static readonly Dictionary<Character, float> EventGuardCooldowns =
            new Dictionary<Character, float>();

        private static readonly Dictionary<Character, float> VeiledCooldowns =
            new Dictionary<Character, float>();

        private static readonly Dictionary<Character, float> VeiledResistExpiries =
            new Dictionary<Character, float>();

        private static readonly Dictionary<Character, float> MomentumExpiries =
            new Dictionary<Character, float>();

        private static readonly Dictionary<Character, float> MomentumCooldowns =
            new Dictionary<Character, float>();

        private static readonly Dictionary<Character, float> CommunionCooldowns =
            new Dictionary<Character, float>();

        private static readonly FieldInfo CurrentDexField =
            AccessTools.Field(
                typeof(Stats),
                "CurrentDex");

        private static readonly FieldInfo CurrentMRField =
            AccessTools.Field(
                typeof(Stats),
                "CurrentMR");

        private static readonly FieldInfo CurrentVRField =
            AccessTools.Field(
                typeof(Stats),
                "CurrentVR");

        private static NhalSeraEquipmentEffectsController _controller;
        private static bool _applyingLightlessEcho;
        private static bool _applyingVeiledBenediction;

        private static SimPlayer[] _cachedGroupSims =
            new SimPlayer[0];

        private static float _nextGroupSimCacheRefresh;

        internal static Spell ThresholdCommunionSpell
        {
            get;
            private set;
        }

        internal static bool IsApplyingGeneratedDamage
        {
            get { return _applyingLightlessEcho; }
        }

        internal static bool IsApplyingGeneratedHeal
        {
            get { return _applyingVeiledBenediction; }
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

            GameObject obj =
                new GameObject(
                    "FOTF_NhalSeraEquipmentEffects");

            UnityEngine.Object.DontDestroyOnLoad(
                obj);

            _controller =
                obj.AddComponent<NhalSeraEquipmentEffectsController>();
        }

        internal static void Shutdown()
        {
            LightlessCooldowns.Clear();
            EventGuardExpiries.Clear();
            EventGuardCooldowns.Clear();
            VeiledCooldowns.Clear();
            VeiledResistExpiries.Clear();
            MomentumExpiries.Clear();
            MomentumCooldowns.Clear();
            CommunionCooldowns.Clear();
            _cachedGroupSims = new SimPlayer[0];
            _nextGroupSimCacheRefresh = 0f;

            ThresholdCommunionSpell = null;

            if (_controller != null)
            {
                UnityEngine.Object.Destroy(
                    _controller.gameObject);

                _controller = null;
            }
        }

        // ============================================================
        // SPELL REGISTRATION
        // ============================================================

        internal static void RegisterProcSpells(
            SpellDB database)
        {
            if (database == null ||
                database.SpellDatabase == null)
            {
                return;
            }

            ThresholdCommunionSpell =
                ResolveOrCreateManaRegenV(database);
        }

        /// <summary>
        /// Creates the rank-V mana regeneration effect entirely in FOTF.
        /// There is deliberately no native spell lookup or clone dependency.
        /// </summary>
        private static Spell ResolveOrCreateManaRegenV(
            SpellDB database)
        {
            Spell existing =
                FindSpellById(
                    database,
                    ManaRegenVSpellId);

            if (existing != null)
            {
                ConfigureManaRegenV(existing);
                return existing;
            }

            Spell spell = null;

            try
            {
                spell = ScriptableObject.CreateInstance<Spell>();
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[NhalSeraEquipment] failed creating custom Mana Regen V Spell object: " +
                    exception);
            }

            if (spell == null)
            {
                Plugin.NativeLog.LogError(
                    "[NhalSeraEquipment] custom Mana Regen V could not be created.");
                return null;
            }

            ConfigureManaRegenV(spell);

            CustomSpellRegistry.Register(
                database,
                spell);

            Plugin.NativeLog.LogInfo(
                "[NhalSeraEquipment] registered FOTF-owned [Mana Regen V] Id=[" +
                ManaRegenVSpellId +
                "] RegenPerLevelPerTick=[" +
                ManaRegenVPerLevelPerTick.ToString("0.##") +
                "] DurationTicks=[" +
                spell.SpellDurationInTicks +
                "].");

            return spell;
        }

        private static void ConfigureManaRegenV(
            Spell spell)
        {
            if (spell == null)
            {
                return;
            }

            spell.name =
                "FOTF - Mana Regen V";

            spell.Id =
                ManaRegenVSpellId;

            spell.SpellName =
                "Mana Regen V";

            spell.SpellDesc =
                "Restores 5 Mana per level every 3 seconds for 12 seconds.";

            spell.Type =
                Spell.SpellType.Beneficial;

            // Generic prevents this custom rank from replacing unrelated
            // native status families and allows a re-proc to refresh itself.
            spell.Line =
                Spell.SpellLine.Generic;

            spell.RequiredLevel = 1;
            spell.ManaCost = 0;
            spell.TargetDamage = 0;
            spell.TargetHealing = 0;
            spell.Aggro = 0;
            spell.SpellChargeTime = 0f;
            spell.Cooldown = 0f;

            // Stats.TickEffects uses three-second status ticks.
            spell.SpellDurationInTicks = 4;

            // Do not use Spell.Mana here: native Stats.CountStatusEffects also
            // treats that field as a maximum-mana stat bonus. The level-scaled
            // restoration field gives us regen only.
            spell.Mana = 0;
            spell.LevelScaledManaRestoration =
                ManaRegenVPerLevelPerTick;

            spell.StatusEffectToApply = null;
            spell.MyDamageType = GameData.DamageType.Magic;
            spell.SpellRange = 100f;
            spell.SelfOnly = false;
            spell.CrowdControlSpell = false;
            spell.SimUsable = false;
            spell.NoResonate = true;
            spell.CanHitPlayers = true;
            spell.RootTarget = false;
            spell.StunTarget = false;
            spell.FearTarget = false;
            spell.BreakOnAnyAction = false;
            spell.GrantInvisibility = false;

            spell.StatusEffectMessageOnPlayer =
                "feel mana surge through you.";

            spell.StatusEffectMessageOnNPC =
                "is surrounded by a surge of mana.";

            spell.UsedBy =
                new List<Class>();

            spell.hideFlags =
                HideFlags.HideAndDontSave;
        }

        private static Spell FindSpellById(
            SpellDB database,
            string id)
        {
            if (database == null ||
                database.SpellDatabase == null ||
                string.IsNullOrEmpty(id))
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

        private static Spell FindSpellByName(
            SpellDB database,
            string name)
        {
            if (database == null ||
                database.SpellDatabase == null ||
                string.IsNullOrEmpty(name))
            {
                return null;
            }

            Spell partial =
                null;

            for (int i = 0;
                 i < database.SpellDatabase.Length;
                 i++)
            {
                Spell spell =
                    database.SpellDatabase[i];

                if (spell == null)
                {
                    continue;
                }

                if (string.Equals(
                        spell.SpellName,
                        name,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        spell.name,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return spell;
                }

                if (partial == null &&
                    !string.IsNullOrEmpty(spell.SpellName) &&
                    spell.SpellName.IndexOf(
                        name,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    partial = spell;
                }
            }

            return partial;
        }

        // ============================================================
        // LIGHTLESS ECHO
        // ============================================================

        internal static void TryProcLightlessEcho(
            Character target,
            Character source)
        {
            if (_applyingLightlessEcho ||
                target == null ||
                source == null ||
                !target.Alive ||
                !source.Alive ||
                !IsEquipped(
                    source,
                    NhalSeraEquipment.LightlessScepterId))
            {
                return;
            }

            float now =
                Time.time;

            float readyAt;

            if (LightlessCooldowns.TryGetValue(
                    source,
                    out readyAt) &&
                now < readyAt)
            {
                return;
            }

            if (UnityEngine.Random.Range(
                    0f,
                    100f) >=
                LightlessEchoProcChance)
            {
                return;
            }

            LightlessCooldowns[source] =
                now +
                LightlessEchoCooldown;

            _applyingLightlessEcho =
                true;

            try
            {
                target.MagicDamageMe(
                    LightlessEchoDamage,
                    !source.isNPC,
                    GameData.DamageType.Void,
                    source,
                    0f,
                    LightlessEchoDamage);

                if (source.MyStats != null)
                {
                    source.MyStats.CurrentMana =
                        Mathf.Min(
                            source.MyStats.GetCurrentMaxMana(),
                            source.MyStats.CurrentMana +
                            LightlessEchoManaRestore);
                }

                AddCombatMessageForPlayerSource(
                    source,
                    "Lightless Echo tears through the target and restores " +
                    LightlessEchoManaRestore +
                    " mana!",
                    "purple");
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[NhalSeraEquipment] Lightless Echo failed: " +
                    exception.Message);
            }
            finally
            {
                _applyingLightlessEcho =
                    false;
            }
        }

        // ============================================================
        // EVENT HORIZON GUARD
        // ============================================================

        internal static void TryActivateEventHorizonGuard(
            Character character,
            int hpBeforeHit)
        {
            if (character == null ||
                character.MyStats == null ||
                !character.Alive ||
                !IsEquipped(
                    character,
                    NhalSeraEquipment.CarapaceId))
            {
                return;
            }

            int maxHp =
                Mathf.Max(
                    1,
                    character.MyStats.CurrentMaxHP);

            int threshold =
                Mathf.RoundToInt(
                    maxHp *
                    EventHorizonHealthThreshold);

            if (hpBeforeHit <= threshold ||
                character.MyStats.CurrentHP > threshold)
            {
                return;
            }

            float now =
                Time.time;

            float readyAt;

            if (EventGuardCooldowns.TryGetValue(
                    character,
                    out readyAt) &&
                now < readyAt)
            {
                return;
            }

            EventGuardExpiries[character] =
                now +
                EventHorizonDuration;

            EventGuardCooldowns[character] =
                now +
                EventHorizonCooldown;

            try
            {
                character.MyStats.CalcStats();
            }
            catch
            {
            }

            AddCombatMessageForPlayerSource(
                character,
                "Event Horizon Guard surrounds you!",
                "purple");
        }

        internal static bool HasEventHorizonGuard(
            Character character)
        {
            if (character == null)
            {
                return false;
            }

            float expiry;

            return
                EventGuardExpiries.TryGetValue(
                    character,
                    out expiry) &&
                Time.time < expiry;
        }

        // ============================================================
        // VEILED BENEDICTION
        // ============================================================

        internal static void TryProcVeiledBenediction(
            Character source,
            Spell triggeringSpell)
        {
            if (_applyingVeiledBenediction ||
                source == null ||
                triggeringSpell == null ||
                !source.Alive ||
                !IsEquipped(
                    source,
                    NhalSeraEquipment.CrownId))
            {
                return;
            }

            float now =
                Time.time;

            float readyAt;

            if (VeiledCooldowns.TryGetValue(
                    source,
                    out readyAt) &&
                now < readyAt)
            {
                return;
            }

            if (UnityEngine.Random.Range(
                    0f,
                    100f) >=
                VeiledBenedictionProcChance)
            {
                return;
            }

            VeiledCooldowns[source] =
                now +
                VeiledBenedictionCooldown;

            List<Character> group =
                GetCurrentGroupMembers(
                    source);

            if (group.Count == 0)
            {
                return;
            }

            _applyingVeiledBenediction =
                true;

            try
            {
                for (int i = 0;
                     i < group.Count;
                     i++)
                {
                    Character target =
                        group[i];

                    if (!IsAlive(
                            target))
                    {
                        continue;
                    }

                    try
                    {
                        target.MyStats.HealMe(
                            triggeringSpell,
                            VeiledBenedictionHealing,
                            false,
                            false,
                            source);
                    }
                    catch
                    {
                    }

                    VeiledResistExpiries[target] =
                        now +
                        VeiledBenedictionDuration;

                    try
                    {
                        target.MyStats.CalcStats();
                    }
                    catch
                    {
                    }
                }

                AddCombatMessageForPlayerSource(
                    source,
                    "Veiled Benediction washes over your group!",
                    "green");
            }
            finally
            {
                _applyingVeiledBenediction =
                    false;
            }
        }

        // ============================================================
        // FRACTURED MOMENTUM
        // ============================================================

        internal static void TryProcFracturedMomentum(
            Character source)
        {
            if (source == null ||
                !source.Alive ||
                !IsEquipped(
                    source,
                    NhalSeraEquipment.GraspId))
            {
                return;
            }

            float now =
                Time.time;

            float readyAt;

            if (MomentumCooldowns.TryGetValue(
                    source,
                    out readyAt) &&
                now < readyAt)
            {
                return;
            }

            if (UnityEngine.Random.Range(
                    0f,
                    100f) >=
                FracturedMomentumProcChance)
            {
                return;
            }

            MomentumExpiries[source] =
                now +
                FracturedMomentumDuration;

            MomentumCooldowns[source] =
                now +
                FracturedMomentumCooldown;

            try
            {
                if (source.MyStats != null)
                {
                    source.MyStats.CalcStats();
                }
            }
            catch
            {
            }

            AddCombatMessageForPlayerSource(
                source,
                "Fractured Momentum accelerates your attacks!",
                "purple");
        }

        // ============================================================
        // THRESHOLD COMMUNION
        // ============================================================

        internal static void TryProcThresholdCommunion(
            Character source)
        {
            if (source == null ||
                !source.Alive ||
                ThresholdCommunionSpell == null ||
                !IsEquipped(
                    source,
                    NhalSeraEquipment.GirdleId))
            {
                return;
            }

            float now =
                Time.time;

            float readyAt;

            if (CommunionCooldowns.TryGetValue(
                    source,
                    out readyAt) &&
                now < readyAt)
            {
                return;
            }

            if (UnityEngine.Random.Range(
                    0f,
                    100f) >=
                ThresholdCommunionProcChance)
            {
                return;
            }

            CommunionCooldowns[source] =
                now +
                ThresholdCommunionCooldown;

            List<Character> group =
                GetCurrentGroupMembers(
                    source);

            for (int i = 0;
                 i < group.Count;
                 i++)
            {
                Character target =
                    group[i];

                if (!IsAlive(
                        target))
                {
                    continue;
                }

                try
                {
                    target.MyStats.AddStatusEffect(
                        ThresholdCommunionSpell,
                        !source.isNPC,
                        0,
                        source);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogWarning(
                        "[NhalSeraEquipment] Threshold Communion failed on [" +
                        target.name +
                        "]: " +
                        exception.Message);
                }
            }

            AddCombatMessageForPlayerSource(
                source,
                "Threshold Communion grants Mana Regen V to your group!",
                "lightblue");
        }

        // ============================================================
        // CALCULATED BONUSES / EXPIRY
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

            float now =
                Time.time;

            float expiry;

            if (EventGuardExpiries.TryGetValue(
                    character,
                    out expiry) &&
                now < expiry)
            {
                stats.CurrentAC +=
                    EventHorizonArmor;
            }

            if (VeiledResistExpiries.TryGetValue(
                    character,
                    out expiry) &&
                now < expiry)
            {
                AddPrivateInt(
                    stats,
                    CurrentMRField,
                    VeiledBenedictionMagicResist);

                AddPrivateInt(
                    stats,
                    CurrentVRField,
                    VeiledBenedictionVoidResist);
            }

            if (MomentumExpiries.TryGetValue(
                    character,
                    out expiry) &&
                now < expiry)
            {
                AddPrivateInt(
                    stats,
                    CurrentDexField,
                    FracturedMomentumDexterity);

                stats.ArmorPenPercentage +=
                    FracturedMomentumArmorPenPercent;

                float hasteMultiplier =
                    1f -
                    FracturedMomentumHastePercent /
                    100f;

                stats.CurrentMHAtkDelay =
                    Mathf.Max(
                        1f,
                        stats.CurrentMHAtkDelay *
                        hasteMultiplier);

                stats.CurrentOHAtkDelay =
                    Mathf.Max(
                        1f,
                        stats.CurrentOHAtkDelay *
                        hasteMultiplier);
            }
        }

        internal static void UpdateEffects()
        {
            float now =
                Time.time;

            ExpireCalculatedState(
                EventGuardExpiries,
                now);

            ExpireCalculatedState(
                VeiledResistExpiries,
                now);

            ExpireCalculatedState(
                MomentumExpiries,
                now);
        }

        private static void ExpireCalculatedState(
            Dictionary<Character, float> states,
            float now)
        {
            if (states == null ||
                states.Count == 0)
            {
                return;
            }

            List<Character> expired =
                null;

            foreach (KeyValuePair<Character, float> pair in states)
            {
                if (pair.Key != null &&
                    now < pair.Value)
                {
                    continue;
                }

                if (expired == null)
                {
                    expired =
                        new List<Character>();
                }

                expired.Add(
                    pair.Key);
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

                states.Remove(
                    character);

                if (character == null ||
                    character.MyStats == null)
                {
                    continue;
                }

                try
                {
                    character.MyStats.CalcStats();
                }
                catch
                {
                }
            }
        }

        private static void AddPrivateInt(
            Stats stats,
            FieldInfo field,
            int amount)
        {
            if (stats == null ||
                field == null ||
                amount == 0)
            {
                return;
            }

            try
            {
                object raw =
                    field.GetValue(
                        stats);

                int current =
                    raw is int
                        ? (int)raw
                        : 0;

                field.SetValue(
                    stats,
                    current +
                    amount);
            }
            catch
            {
            }
        }

        // ============================================================
        // EQUIPMENT / GROUP HELPERS
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

        private static List<Character> GetCurrentGroupMembers(
            Character source)
        {
            List<Character> result =
                new List<Character>();

            HashSet<Character> seen =
                new HashSet<Character>();

            if (source == null)
            {
                return result;
            }

            int raidGroup =
                ResolveRaidGroup(
                    source);

            if (raidGroup > 0 &&
                GameData.RaidManager != null &&
                GameData.RaidManager.Raiders != null)
            {
                if (raidGroup == 1 &&
                    GameData.PlayerControl != null &&
                    IsAlive(
                        GameData.PlayerControl.Myself))
                {
                    AddUnique(
                        result,
                        seen,
                        GameData.PlayerControl.Myself);
                }

                for (int i = 0;
                     i < GameData.RaidManager.Raiders.Count;
                     i++)
                {
                    RaidMemberSlot slot =
                        GameData.RaidManager.Raiders[i];

                    if (slot == null ||
                        slot.GroupNumber != raidGroup ||
                        slot.AssignedAvatar == null)
                    {
                        continue;
                    }

                    NPC npc =
                        slot.AssignedAvatar.GetThisNPC();

                    Character character =
                        npc != null
                            ? npc.GetChar()
                            : null;

                    if (IsAlive(
                            character))
                    {
                        AddUnique(
                            result,
                            seen,
                            character);
                    }
                }

                if (IsAlive(
                        source))
                {
                    AddUnique(
                        result,
                        seen,
                        source);
                }

                return result;
            }

            // Fallback for ordinary player groups outside raid mode.
            if (GameData.PlayerControl != null &&
                IsAlive(
                    GameData.PlayerControl.Myself))
            {
                AddUnique(
                    result,
                    seen,
                    GameData.PlayerControl.Myself);
            }

            if (_cachedGroupSims == null ||
                Time.unscaledTime >= _nextGroupSimCacheRefresh)
            {
                _nextGroupSimCacheRefresh =
                    Time.unscaledTime + 0.75f;

                _cachedGroupSims =
                    UnityEngine.Object.FindObjectsOfType<SimPlayer>();

                if (_cachedGroupSims == null)
                {
                    _cachedGroupSims =
                        new SimPlayer[0];
                }
            }

            SimPlayer[] sims =
                _cachedGroupSims;

            if (sims != null &&
                GameData.SimMngr != null)
            {
                for (int i = 0;
                     i < sims.Length;
                     i++)
                {
                    SimPlayer sim =
                        sims[i];

                    if (sim == null ||
                        sim.GetThisNPC() == null)
                    {
                        continue;
                    }

                    bool inPlayerGroup =
                        false;

                    try
                    {
                        inPlayerGroup =
                            GameData.SimMngr.IsSimInPlayerGroup(
                                sim.transform.name);
                    }
                    catch
                    {
                    }

                    if (!inPlayerGroup)
                    {
                        continue;
                    }

                    Character character =
                        sim.GetThisNPC().GetChar();

                    if (IsAlive(
                            character))
                    {
                        AddUnique(
                            result,
                            seen,
                            character);
                    }
                }
            }

            if (IsAlive(
                    source))
            {
                AddUnique(
                    result,
                    seen,
                    source);
            }

            return result;
        }

        private static int ResolveRaidGroup(
            Character source)
        {
            if (source == null)
            {
                return 0;
            }

            if (GameData.PlayerControl != null &&
                source == GameData.PlayerControl.Myself)
            {
                return 1;
            }

            // Resolve through RaidManager.Raiders rather than relying on
            // SimPlayer/NPC private implementation details.
            if (GameData.RaidManager != null &&
                GameData.RaidManager.Raiders != null)
            {
                for (int i = 0;
                     i < GameData.RaidManager.Raiders.Count;
                     i++)
                {
                    RaidMemberSlot slot =
                        GameData.RaidManager.Raiders[i];

                    if (slot == null ||
                        slot.AssignedAvatar == null ||
                        slot.AssignedAvatar.GetThisNPC() == null)
                    {
                        continue;
                    }

                    Character member =
                        slot.AssignedAvatar.GetThisNPC().GetChar();

                    if (member == source)
                    {
                        return slot.GroupNumber;
                    }
                }
            }

            return 0;
        }

        private static void AddUnique(
            List<Character> result,
            HashSet<Character> seen,
            Character character)
        {
            if (result == null ||
                seen == null ||
                character == null ||
                seen.Contains(
                    character))
            {
                return;
            }

            seen.Add(
                character);

            result.Add(
                character);
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

        private static void AddCombatMessageForPlayerSource(
            Character source,
            string text,
            string color)
        {
            if (source == null ||
                GameData.PlayerControl == null ||
                source != GameData.PlayerControl.Myself)
            {
                return;
            }

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
    }

    internal sealed class NhalSeraEquipmentEffectsController :
        MonoBehaviour
    {
        private void Update()
        {
            NhalSeraEquipmentEffects.UpdateEffects();
        }
    }

    // ================================================================
    // PHYSICAL DAMAGE: CARAPACE REDUCTION + SCEPTER/GRASP PROCS
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
    internal static class Patch_NhalSeraEquipmentPhysicalDamage
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
            __state =
                __instance != null &&
                __instance.MyStats != null
                    ? __instance.MyStats.CurrentHP
                    : 0;

            if (__instance == null ||
                _incdmg <= 0)
            {
                return;
            }

            if (_dmgType == GameData.DamageType.Physical &&
                NhalSeraEquipmentEffects.HasEventHorizonGuard(
                    __instance))
            {
                _incdmg =
                    Mathf.Max(
                        1,
                        Mathf.RoundToInt(
                            _incdmg *
                            NhalSeraEquipmentEffects.EventHorizonDamageMultiplier));
            }
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
            if (__instance != null)
            {
                NhalSeraEquipmentEffects.TryActivateEventHorizonGuard(
                    __instance,
                    __state);
            }

            if (__result <= 0 ||
                _attacker == null ||
                !_animEffect ||
                _dmgType != GameData.DamageType.Physical)
            {
                return;
            }

            NhalSeraEquipmentEffects.TryProcLightlessEcho(
                __instance,
                _attacker);

            NhalSeraEquipmentEffects.TryProcFracturedMomentum(
                _attacker);
        }
    }

    // ================================================================
    // MAGIC / VOID DAMAGE: CARAPACE REDUCTION + SPELL PROCS
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
    internal static class Patch_NhalSeraEquipmentMagicDamage
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
            __state =
                __instance != null &&
                __instance.MyStats != null
                    ? __instance.MyStats.CurrentHP
                    : 0;

            if (__instance == null ||
                _dmg <= 0)
            {
                return;
            }

            if (_dmgType == GameData.DamageType.Void &&
                NhalSeraEquipmentEffects.HasEventHorizonGuard(
                    __instance))
            {
                _dmg =
                    Mathf.Max(
                        1,
                        Mathf.RoundToInt(
                            _dmg *
                            NhalSeraEquipmentEffects.EventHorizonDamageMultiplier));
            }
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
            if (__instance != null)
            {
                NhalSeraEquipmentEffects.TryActivateEventHorizonGuard(
                    __instance,
                    __state);
            }

            if (__result <= 0 ||
                _attacker == null)
            {
                return;
            }

            bool generatedDamage =
                NhalSeraEquipmentEffects.IsApplyingGeneratedDamage;

            NhalSeraEquipmentEffects.TryProcLightlessEcho(
                __instance,
                _attacker);

            if (!generatedDamage)
            {
                NhalSeraEquipmentEffects.TryProcThresholdCommunion(
                    _attacker);
            }
        }
    }

    // ================================================================
    // DIRECT HEAL PROCS: CROWN + GIRDLE
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
    internal static class Patch_NhalSeraEquipmentHeal
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
            if (NhalSeraEquipmentEffects.IsApplyingGeneratedHeal ||
                __result <= 0 ||
                _isMana ||
                _source == null ||
                _spell == null ||
                _spell.SpellDurationInTicks > 0)
            {
                return;
            }

            NhalSeraEquipmentEffects.TryProcVeiledBenediction(
                _source,
                _spell);

            NhalSeraEquipmentEffects.TryProcThresholdCommunion(
                _source);
        }
    }

    // ================================================================
    // ACTIVE BUFF CALCULATED STATS
    // ================================================================

    [HarmonyPatch(
        typeof(Stats),
        "CalcStats")]
    internal static class Patch_NhalSeraEquipmentCalcStats
    {
        [HarmonyPostfix]
        private static void Postfix(
            Stats __instance)
        {
            NhalSeraEquipmentEffects.ApplyCalculatedBonuses(
                __instance);
        }
    }
}
