using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// r62: configure pure wards by cloning Erenshor's ACTUAL native Shield-line
    /// Arcanist spell, rather than inventing a Beneficial/Heal shield spell.
    ///
    /// IMPORTANT: the game's managed assemblies were not available for offline
    /// inspection. Every native layout assumption is checked at runtime. If
    /// the absorb-value member cannot be established, we do not register a
    /// broken 'shield' that might heal or hurt its caster.
    /// </summary>
    internal static class LightbringerNativeShield
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static Spell _hardened;
        private static Spell _magical;
        private static MemberInfo _capacityMember;
        private static bool _capacityOnEffect;
        private static bool _initialized;
        private static bool _readbackLogged;
        private static int _lastPlayerBonus = -1;
        private static bool _wisdomWarningIssued;
        // +1% ward capacity for each complete 10 points of the caster's WIS.
        internal const int WisdomPerPercent = 10;
        private static MemberInfo _readbackMember;
        private static bool _readbackOnCharacter;

        internal static void Initialize(SpellDB database)
        {
            _hardened = null;
            _magical = null;
            _capacityMember = null;
            _lastPlayerBonus = -1;
            _readbackLogged = false;
            _readbackMember = null;
            _initialized = true;
            if (database == null || database.SpellDatabase == null)
                return;

            foreach (Spell candidate in database.SpellDatabase)
            {
                if (candidate == null || candidate.Type != Spell.SpellType.Beneficial ||
                    !string.Equals(candidate.Line.ToString(), "Shield", StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(candidate.Id) && candidate.Id.StartsWith("FOTF_", StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (string.Equals(candidate.SpellName, "Hardened Skin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.SpellName, "Hardened Flesh", StringComparison.OrdinalIgnoreCase))
                    _hardened = candidate;
                else if (string.Equals(candidate.SpellName, "Magical Skin", StringComparison.OrdinalIgnoreCase))
                    _magical = candidate;
            }

            if (_hardened == null)
            {
                Plugin.LogWarning("[LBNativeShield] Erenshor native Hardened Skin (Beneficial/Shield) not found. Pure wards will not be registered.");
                return;
            }

            // The two reference native shields absorb 200 and 400. Comparing
            // their live managed objects identifies a member without making a
            // compile-time guess about the proprietary engine's field name.
            if (_magical != null)
            {
                _capacityMember = FindAmountMember(_hardened, _magical, 200, 400);
                _capacityOnEffect = false;
                if (_capacityMember == null && _hardened.StatusEffectToApply != null &&
                    _magical.StatusEffectToApply != null)
                {
                    _capacityMember = FindAmountMember(_hardened.StatusEffectToApply,
                        _magical.StatusEffectToApply, 200, 400);
                    _capacityOnEffect = _capacityMember != null;
                }
            }
            if (_capacityMember == null)
            {
                _capacityMember = FindNamedAmountMember(_hardened, 200);
                _capacityOnEffect = false;
                if (_capacityMember == null && _hardened.StatusEffectToApply != null)
                {
                    _capacityMember = FindNamedAmountMember(_hardened.StatusEffectToApply, 200);
                    _capacityOnEffect = _capacityMember != null;
                }
            }

            if (_capacityMember == null)
            {
                Plugin.LogWarning("[LBNativeShield] Native Shield found, but its 200-point absorption member could not be identified safely. No pure ward will be registered. Please inspect Assembly-CSharp.dll.");
                LogNumericCandidates(_hardened);
                if (_hardened.StatusEffectToApply != null)
                    LogNumericCandidates(_hardened.StatusEffectToApply);
            }
            else
            {
                Plugin.LogInfo("[LBNativeShield] Native template=Hardened Skin; capacity=" +
                    (_capacityOnEffect ? "StatusEffect." : "Spell.") + _capacityMember.Name +
                    "; line=" + _hardened.Line + "; selfOnly=" + _hardened.SelfOnly);
            }
        }

        internal static bool Ready { get { return _initialized && _hardened != null && _capacityMember != null; } }
        internal static object NativeTemplateEffect { get { return _hardened != null ? (object)_hardened.StatusEffectToApply : null; } }

        internal static Spell Create(LightbringerSpellDefinition definition)
        {
            if (!Ready || definition == null || definition.ShieldAmount <= 0)
                return null;
            Spell spell = UnityEngine.Object.Instantiate(_hardened);
            if (spell == null)
                return null;
            if (_capacityOnEffect)
            {
                object effectClone = CloneReference(_hardened.StatusEffectToApply);
                if (effectClone == null || !SetMemberValue(spell, FindMember(spell.GetType(), "StatusEffectToApply"), effectClone))
                {
                    UnityEngine.Object.Destroy(spell);
                    Plugin.LogWarning("[LBNativeShield] Failed to isolate cloned native status effect; cancelled ward registration.");
                    return null;
                }
            }
            object capacityOwner = _capacityOnEffect ? (object)spell.StatusEffectToApply : spell;
            int amount = definition.ShieldAmount;
            if (!SetMemberNumber(capacityOwner, _capacityMember, amount))
            {
                UnityEngine.Object.Destroy(spell);
                return null;
            }
            // Preserve the native Shield line, type and status effect. The
            // registration step configures ally targeting on the LIGHTBRINGER
            // clone only. The original Hardened Skin remains unchanged.
            spell.SpellDurationInTicks = Mathf.RoundToInt(
                _hardened.SpellDurationInTicks * (definition.BarrierDuration / 24f));
            return spell;
        }


        /// <summary>
        /// FOTF passive skills are player-specific. Keep the registered native
        /// Lightbringer spell copies in sync with the player's skillbook while
        /// the game is active. Does NOT touch the original Arcanist templates.
        /// </summary>
        internal static void RefreshPlayerBonuses()
        {
            if (!Ready || GameData.PlayerStats == null ||
                !LightbringerCatalog.IsLightbringerClass(GameData.PlayerStats.CharacterClass))
                return;
            bool empowered = LightbringerSkillCatalog.HasCasterSkill(LightbringerSkillCatalog.RadiantFortitudeId);
            bool longer = LightbringerSkillCatalog.HasCasterSkill(LightbringerSkillCatalog.EnduringLightId);
            Stats casterStats = GameData.PlayerControl != null && GameData.PlayerControl.Myself != null
                ? GameData.PlayerControl.Myself.MyStats : GameData.PlayerStats;
            int wisdom = GetWisdom(casterStats);
            int bastionRank = LightbringerAscensions.GetPlayerRank(LightbringerAscensions.BastionOfLightId);
            int flags = ((empowered ? 1 : 0) | (longer ? 2 : 0)) + wisdom * 4 + bastionRank * 1048576;
            if (flags == _lastPlayerBonus) return;
            _lastPlayerBonus = flags;
            foreach (LightbringerSpellDefinition definition in LightbringerSpellCatalog.All)
            {
                if (definition == null || definition.Kind != LightbringerSpellKind.Shield || definition.RuntimeSpell == null)
                    continue;
                Spell spell = definition.RuntimeSpell;
                object owner = _capacityOnEffect ? (object)spell.StatusEffectToApply : spell;
                int capacity = Mathf.RoundToInt(definition.ShieldAmount * WisdomMultiplier(wisdom) *
                    (empowered ? 1.10f : 1f) * (1f + bastionRank * 0.05f));
                if (!SetMemberNumber(owner, _capacityMember, capacity))
                    Plugin.LogWarning("[LBNativeShield] Could not set native capacity for " + definition.Name);
                spell.SpellDurationInTicks = Mathf.RoundToInt(_hardened.SpellDurationInTicks *
                    ((definition.BarrierDuration + (longer ? 3f : 0f)) / 24f));
            }
            Plugin.LogInfo("[LBNativeShield] Updated native ward capacity: WIS=" + wisdom +
                " multiplier=" + WisdomMultiplier(wisdom).ToString("0.000", CultureInfo.InvariantCulture) +
                " Fortitude=" + empowered + " EnduringLight=" + longer);
        }

        /// <summary>Capture the casting Lightbringer's current WIS and
        /// skill bonuses before the native cast snapshots its spell data.
        /// Only Lightbringer Shield-line clones are modified.</summary>
        internal static void PrepareForCaster(Spell spell, Stats caster)
        {
            LightbringerSpellDefinition definition;
            if (spell == null || caster == null || !Ready ||
                !LightbringerSpellCatalog.TryGetDefinition(spell, out definition) ||
                definition.Kind != LightbringerSpellKind.Shield ||
                !LightbringerCatalog.IsLightbringerClass(caster.CharacterClass)) return;
            bool fortified = LightbringerSkillCatalog.HasCasterSkill(caster,
                LightbringerSkillCatalog.RadiantFortitudeId);
            bool enduring = LightbringerSkillCatalog.HasCasterSkill(caster,
                LightbringerSkillCatalog.EnduringLightId);
            float multiplier = WisdomMultiplier(caster) *
                (fortified ? 1.10f : 1f) *
                LightbringerAscensions.WardMultiplier(caster.Myself != null ? caster.Myself.MySkills : null);
            object owner = _capacityOnEffect ? (object)spell.StatusEffectToApply : spell;
            int amount = Mathf.RoundToInt(definition.ShieldAmount * multiplier);
            if (!SetMemberNumber(owner, _capacityMember, amount)) return;
            float duration = definition.BarrierDuration +
                (enduring ? 3f : 0f);
            spell.SpellDurationInTicks = Mathf.RoundToInt(_hardened.SpellDurationInTicks * (duration / 24f));
            Plugin.LogInfo("[LBWisShield] Prepared " + definition.Name + " caster=" + caster.MyName +
                " WIS=" + GetWisdom(caster) + " capacity=" + amount);
        }

        /// <summary>
        /// Configure a dedicated status-only native ward clone for a resolved
        /// healing cast. Unlike PrepareForCaster this accepts hybrid/group
        /// wards; it never changes the registered healing spell or Erenshor's
        /// native Hardened Skin reference asset.
        /// </summary>
        internal static bool ConfigureResolvedHealWard(Spell ward, int capacity, float durationSeconds)
        {
            if (!Ready || ward == null || capacity <= 0 || durationSeconds <= 0f)
                return false;
            object owner = _capacityOnEffect ? (object)ward.StatusEffectToApply : ward;
            if (!SetMemberNumber(owner, _capacityMember, capacity))
                return false;
            ward.SpellDurationInTicks = Mathf.RoundToInt(
                _hardened.SpellDurationInTicks * (durationSeconds / 24f));
            return true;
        }

        /// <summary>Reset native remaining shield to a confirmed, newly applied
        /// Lightbringer ward's capacity. This is native SpellShield, not a
        /// separate pool; it fixes engines that keep depleted capacity on a
        /// re-applied status.</summary>
        internal static bool RefreshNativeRemaining(Stats target, int capacity)
        {
            if (target == null || capacity <= 0) return false;
            MemberInfo member = FindMember(target.GetType(), "SpellShield");
            if (member == null) return false;
            return SetMemberNumber(target, member, capacity);
        }

        internal static int GetConfiguredCapacity(Spell spell, int fallback)
        {
            if (spell == null || _capacityMember == null) return fallback;
            object owner = _capacityOnEffect ? (object)spell.StatusEffectToApply : spell;
            double result;
            return owner != null && TryReadNumber(owner, _capacityMember, out result)
                ? Math.Max(0, (int)Math.Round(result)) : fallback;
        }

        internal static float WisdomMultiplier(Stats caster)
        {
            return WisdomMultiplier(GetWisdom(caster));
        }

        private static float WisdomMultiplier(int wisdom)
        {
            return 1f + (Math.Max(0, wisdom) / WisdomPerPercent) * .01f;
        }

        /// <summary>Read WIS on the caster, not the recipient. The native
        /// Stats ABI is game-version-specific, so read only recognized numeric
        /// WIS names and fail to base strength if they are absent.</summary>
        internal static int GetWisdom(Stats caster)
        {
            if (caster == null) return 0;
            string[] names = { "TotalWis", "TotalWIS", "EffectiveWis", "CurrentWis",
                "Wisdom", "Wis", "WIS", "WisStat", "WisdomStat" };
            for (int i = 0; i < names.Length; i++)
            {
                MemberInfo member = FindMember(caster.GetType(), names[i]);
                double number;
                if (member != null && TryReadNumber(caster, member, out number) &&
                    !double.IsNaN(number) && number >= 0)
                    return Mathf.Clamp((int)Math.Round(number), 0, 100000);
            }
            if (!_wisdomWarningIssued)
            {
                _wisdomWarningIssued = true;
                Plugin.LogWarning("[LBWisShield] Could not locate effective WIS on Stats. " +
                    "Using base shield strengths; please supply the Stats member name from Assembly-CSharp.dll.");
            }
            return 0;
        }

        private static object CloneReference(object value)
        {
            if (value == null) return null;
            UnityEngine.Object unityObj = value as UnityEngine.Object;
            if (unityObj != null) return UnityEngine.Object.Instantiate(unityObj);
            MethodInfo clone = value.GetType().GetMethod("MemberwiseClone", Flags);
            try { return clone != null ? clone.Invoke(value, null) : null; }
            catch { return null; }
        }

        private static MemberInfo FindAmountMember(object first, object second, int firstValue, int secondValue)
        {
            foreach (MemberInfo member in NumericMembers(first.GetType()))
            {
                if (member is FieldInfo && ((FieldInfo)member).IsInitOnly) continue;
                if (member is PropertyInfo && !((PropertyInfo)member).CanWrite) continue;
                if (!member.Name.Equals("TargetDamage", StringComparison.OrdinalIgnoreCase) &&
                    !member.Name.Equals("TargetHealing", StringComparison.OrdinalIgnoreCase) &&
                    !member.Name.Equals("ShieldAmount", StringComparison.OrdinalIgnoreCase) &&
                    !member.Name.Equals("AbsorbAmount", StringComparison.OrdinalIgnoreCase) &&
                    !member.Name.Equals("ShieldValue", StringComparison.OrdinalIgnoreCase) &&
                    !member.Name.Equals("Shield", StringComparison.OrdinalIgnoreCase) &&
                    !member.Name.Equals("Absorb", StringComparison.OrdinalIgnoreCase) &&
                    member.Name.IndexOf("shield", StringComparison.OrdinalIgnoreCase) < 0 &&
                    member.Name.IndexOf("absorb", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                double a, b;
                if (TryReadNumber(first, member, out a) && TryReadNumber(second, member, out b) &&
                    Math.Abs(a - firstValue) < 0.001 && Math.Abs(b - secondValue) < 0.001)
                    return member;
            }
            // If the engine uses an oddly named scalar, only adopt it if the
            // 200->400 comparison identifies exactly one matching member.
            MemberInfo only = null;
            foreach (MemberInfo member in NumericMembers(first.GetType()))
            {
                if (member is FieldInfo && ((FieldInfo)member).IsInitOnly) continue;
                if (member is PropertyInfo && !((PropertyInfo)member).CanWrite) continue;
                double a, b;
                if (!TryReadNumber(first, member, out a) || !TryReadNumber(second, member, out b) ||
                    Math.Abs(a - firstValue) > .001 || Math.Abs(b - secondValue) > .001)
                    continue;
                if (only != null) return null;
                only = member;
            }
            return only;
        }

        private static MemberInfo FindNamedAmountMember(object obj, int expected)
        {
            MemberInfo match = null;
            foreach (MemberInfo member in NumericMembers(obj.GetType()))
            {
                if (member is FieldInfo && ((FieldInfo)member).IsInitOnly) continue;
                if (member is PropertyInfo && !((PropertyInfo)member).CanWrite) continue;
                string name = member.Name.ToLowerInvariant();
                if (!name.Contains("shield") && !name.Contains("absorb")) continue;
                double number;
                if (TryReadNumber(obj, member, out number) && Math.Abs(number - expected) < .001)
                {
                    if (match != null) return null; // ambiguous layout: fail safely
                    match = member;
                }
            }
            return match;
        }

        internal static int GetRemainingNative(Stats target)
        {
            if (target == null) return 0;
            if (!_readbackLogged)
            {
                _readbackLogged = true;
                _readbackMember = DiscoverReadback(target.GetType());
                _readbackOnCharacter = false;
                if (_readbackMember == null && GameData.PlayerControl != null && GameData.PlayerControl.Myself != null)
                {
                    _readbackMember = DiscoverReadback(GameData.PlayerControl.Myself.GetType());
                    _readbackOnCharacter = _readbackMember != null;
                }
                Plugin.LogInfo("[LBNativeShield] UI readback: " +
                    (_readbackMember != null ? (_readbackOnCharacter ? "Character." : "Stats.") + _readbackMember.Name :
                     "unavailable; cannot report native absorb values to UI without native field mapping"));
            }
            if (_readbackMember == null) return 0;
            object owner = _readbackOnCharacter ? FindOwnerCharacter(target) : (object)target;
            double n;
            return owner != null && TryReadNumber(owner, _readbackMember, out n)
                ? Math.Max(0, (int)Math.Round(n)) : 0;
        }

        // A found numeric readback must be preferred over the UI-only estimate,
        // including when the value is zero after the ward has been consumed.
        internal static bool HasNativeReadback { get { return _readbackMember != null; } }

        private static Character FindOwnerCharacter(Stats stats)
        {
            if (GameData.PlayerControl != null && GameData.PlayerControl.Myself != null &&
                GameData.PlayerControl.Myself.MyStats == stats)
                return GameData.PlayerControl.Myself;
            return null; // Do not return the PLAYER'S shield for a group member.
        }

        private static MemberInfo DiscoverReadback(Type type)
        {
            foreach (MemberInfo m in NumericMembers(type))
            {
                string name = m.Name.ToLowerInvariant();
                if ((name.Contains("shield") || name.Contains("absorb")) &&
                    !name.Contains("max") && !name.Contains("bonus") && !name.Contains("cooldown") &&
                    !name.Contains("duration") && !name.Contains("timer") && !name.Contains("regen"))
                    return m;
            }
            return null;
        }

        private static IEnumerable<MemberInfo> NumericMembers(Type type)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                foreach (FieldInfo f in t.GetFields(Flags | BindingFlags.DeclaredOnly))
                    if (IsNumeric(f.FieldType)) yield return f;
                foreach (PropertyInfo p in t.GetProperties(Flags | BindingFlags.DeclaredOnly))
                    if (IsNumeric(p.PropertyType) && p.GetIndexParameters().Length == 0 && p.CanRead)
                        yield return p;
            }
        }
        private static bool IsNumeric(Type t)
        {
            return t == typeof(int) || t == typeof(float) || t == typeof(double) ||
                t == typeof(long) || t == typeof(short);
        }
        private static MemberInfo FindMember(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, Flags | BindingFlags.DeclaredOnly);
                if (f != null) return f;
                PropertyInfo p = t.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
                if (p != null) return p;
            }
            return null;
        }
        private static bool TryReadNumber(object obj, MemberInfo member, out double value)
        {
            value = 0;
            try
            {
                object read = member is FieldInfo ? ((FieldInfo)member).GetValue(obj) :
                    ((PropertyInfo)member).GetValue(obj, null);
                if (read == null) return false;
                value = Convert.ToDouble(read, CultureInfo.InvariantCulture);
                return true;
            }
            catch { return false; }
        }
        private static bool SetMemberNumber(object obj, MemberInfo member, int value)
        {
            if (member == null) return false;
            Type type = member is FieldInfo ? ((FieldInfo)member).FieldType : ((PropertyInfo)member).PropertyType;
            try { return SetMemberValue(obj, member, Convert.ChangeType(value, type, CultureInfo.InvariantCulture)); }
            catch { return false; }
        }
        private static bool SetMemberValue(object obj, MemberInfo member, object value)
        {
            if (obj == null || member == null) return false;
            try
            {
                FieldInfo f = member as FieldInfo;
                if (f != null) { f.SetValue(obj, value); return true; }
                PropertyInfo p = member as PropertyInfo;
                if (p != null && p.CanWrite) { p.SetValue(obj, value, null); return true; }
            }
            catch (Exception e) { Plugin.LogWarning("[LBNativeShield] Set member failed: " + e.Message); }
            return false;
        }
        private static void LogNumericCandidates(object obj)
        {
            if (obj == null) return;
            List<string> parts = new List<string>();
            foreach (MemberInfo m in NumericMembers(obj.GetType()))
            {
                double n;
                if (TryReadNumber(obj, m, out n) &&
                    (m.Name.ToLowerInvariant().Contains("shield") ||
                     m.Name.ToLowerInvariant().Contains("absorb") ||
                     Math.Abs(n - 200) < .001))
                    parts.Add(m.Name + "=" + n.ToString(CultureInfo.InvariantCulture));
            }
            Plugin.LogInfo("[LBNativeShield] Native numeric diagnostics: " + string.Join(", ", parts.ToArray()));
        }
    }
}
