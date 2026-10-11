using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Owns the temporary native-hotkey overrides used by Flashfire.
    ///
    /// The first implementation tried to reset cooldown by calling
    /// Hotkeys.AssignSpellFromBook. In Erenshor that only rebuilds the slot;
    /// it does not reliably clear the live cooldown gate. Flashfire now keeps
    /// the Pyroclasm-family spell definition and the bound hotkey's native
    /// cooldown members at zero while the proc is waiting.
    ///
    /// Native hotkeys may also validate/deduct mana before CastSpell.StartSpell
    /// is entered. For a Flashfire Pyroclasm invocation we therefore protect
    /// the player's mana around the hotkey method itself, then restore the
    /// exact pre-interaction value. StartSpell has a second guard for casts
    /// initiated from a path other than a bound hotkey.
    /// </summary>
    internal static class EmberweaverFlashfireCooldownRuntime
    {
        internal sealed class HotkeyInvocationState
        {
            internal bool Active;
            internal int ManaBefore;
        }

        private static readonly Dictionary<Type, FieldInfo[]>
            CooldownFieldCache =
                new Dictionary<Type, FieldInfo[]>();

        private static readonly Dictionary<Type, PropertyInfo[]>
            CooldownPropertyCache =
                new Dictionary<Type, PropertyInfo[]>();

        private static int _guardDepth;
        private static int _guardManaBaseline;
        private static int _postCastManaFloor = -1;
        private static int _postCastManaFloorUntilFrame = -1;

        internal static void ResetPyroclasmCooldowns()
        {
            if (!EmberweaverPassiveRuntime.FlashfireReady)
            {
                return;
            }

            SetPyroclasmDefinitionCooldowns(
                0f);

            ResetBoundPyroclasmHotkeys(
                true);
        }

        /// <summary>
        /// Called every Emberweaver update while Flashfire is ready. Keeping
        /// the native cooldown values at zero prevents Erenshor from restoring
        /// or advancing a stale timer on the next hotkey update.
        /// </summary>
        internal static void MaintainFlashfireReadyState()
        {
            if (!EmberweaverPassiveRuntime.FlashfireReady)
            {
                return;
            }

            SetPyroclasmDefinitionCooldowns(
                0f);

            ResetBoundPyroclasmHotkeys(
                false);
        }

        /// <summary>
        /// The hotkey has already passed its cooldown gate by the time
        /// StartSpell is reached. Restore the normal spell cooldown now so the
        /// Flashfire Pyroclasm itself starts a fresh ordinary cooldown.
        /// </summary>
        internal static void PreparePyroclasmCast(
            Spell spell)
        {
            EmberweaverSpellDefinition definition;

            if (spell == null ||
                !EmberweaverSpellCatalog.TryGetDefinition(
                    spell,
                    out definition) ||
                definition == null ||
                !definition.IsPyroclasm)
            {
                return;
            }

            spell.Cooldown =
                definition.Cooldown;
        }

        internal static void RestoreNormalPyroclasmCooldowns()
        {
            IReadOnlyList<EmberweaverSpellDefinition> definitions =
                EmberweaverSpellCatalog.All;

            if (definitions == null)
            {
                return;
            }

            for (int i = 0;
                 i < definitions.Count;
                 i++)
            {
                EmberweaverSpellDefinition definition =
                    definitions[i];

                if (definition == null ||
                    !definition.IsPyroclasm)
                {
                    continue;
                }

                Spell spell =
                    ResolveSpell(
                        definition);

                if (spell != null)
                {
                    spell.Cooldown =
                        definition.Cooldown;
                }
            }
        }

        internal static HotkeyInvocationState BeginHotkeyInvocation(
            Hotkeys hotkey)
        {
            HotkeyInvocationState state =
                new HotkeyInvocationState();

            EmberweaverSpellDefinition definition;

            if (!EmberweaverPassiveRuntime.FlashfireReady ||
                !EmberweaverCatalog.IsCurrentPlayerEmberweaver() ||
                !TryGetBoundPyroclasmDefinition(
                    hotkey,
                    out definition))
            {
                return state;
            }

            state.Active =
                true;

            if (GameData.PlayerStats != null)
            {
                state.ManaBefore =
                    GameData.PlayerStats.CurrentMana;

                if (_guardDepth == 0)
                {
                    _guardManaBaseline =
                        state.ManaBefore;
                }

                _guardDepth++;

                int requiredMana =
                    Math.Max(
                        0,
                        definition.ManaCost);

                if (GameData.PlayerStats.CurrentMana < requiredMana)
                {
                    GameData.PlayerStats.CurrentMana =
                        requiredMana;
                }
            }

            Spell spell =
                ResolveSpell(
                    definition);

            if (spell != null)
            {
                spell.Cooldown =
                    0f;
            }

            ForceHotkeyReady(
                hotkey);

            return state;
        }

        internal static void EndHotkeyInvocation(
            HotkeyInvocationState state)
        {
            if (state == null ||
                !state.Active)
            {
                return;
            }

            if (_guardDepth > 0)
            {
                _guardDepth--;
            }

            if (_guardDepth == 0 &&
                GameData.PlayerStats != null)
            {
                GameData.PlayerStats.CurrentMana =
                    _guardManaBaseline;
            }
        }

        internal static int GetProtectedManaBaseline()
        {
            if (_guardDepth > 0)
            {
                return _guardManaBaseline;
            }

            return GameData.PlayerStats != null
                ? GameData.PlayerStats.CurrentMana
                : 0;
        }

        internal static void EnsureStartSpellCanAfford(
            EmberweaverSpellDefinition definition)
        {
            if (definition == null ||
                !definition.IsPyroclasm ||
                GameData.PlayerStats == null)
            {
                return;
            }

            int requiredMana =
                Math.Max(
                    0,
                    definition.ManaCost);

            if (GameData.PlayerStats.CurrentMana < requiredMana)
            {
                GameData.PlayerStats.CurrentMana =
                    requiredMana;
            }
        }

        internal static void RestoreManaAfterFreeCast(
            int baseline)
        {
            if (GameData.PlayerStats == null)
            {
                return;
            }

            GameData.PlayerStats.CurrentMana =
                Math.Max(
                    0,
                    Math.Min(
                        baseline,
                        GameData.PlayerStats.GetCurrentMaxMana()));
        }

        internal static void ArmPostCastManaProtection(
            int baseline)
        {
            if (GameData.PlayerStats == null)
            {
                return;
            }

            _postCastManaFloor =
                Math.Max(
                    0,
                    Math.Min(
                        baseline,
                        GameData.PlayerStats.GetCurrentMaxMana()));

            // Covers a native mana deduction deferred until the frame after
            // the hotkey/StartSpell call stack completes. Two frames is long
            // enough for that path without turning Flashfire into a general
            // mana-refund window for subsequent casts.
            _postCastManaFloorUntilFrame =
                Time.frameCount + 2;
        }

        internal static void UpdateRuntime()
        {
            if (_postCastManaFloor < 0 ||
                _postCastManaFloorUntilFrame < 0)
            {
                return;
            }

            if (Time.frameCount >
                _postCastManaFloorUntilFrame)
            {
                _postCastManaFloor = -1;
                _postCastManaFloorUntilFrame = -1;
                return;
            }

            if (GameData.PlayerStats != null &&
                GameData.PlayerStats.CurrentMana < _postCastManaFloor)
            {
                GameData.PlayerStats.CurrentMana =
                    _postCastManaFloor;
            }
        }

        internal static void ClearTransientManaProtection()
        {
            _guardDepth = 0;
            _guardManaBaseline = 0;
            _postCastManaFloor = -1;
            _postCastManaFloorUntilFrame = -1;
        }

        private static void SetPyroclasmDefinitionCooldowns(
            float cooldown)
        {
            IReadOnlyList<EmberweaverSpellDefinition> definitions =
                EmberweaverSpellCatalog.All;

            if (definitions == null)
            {
                return;
            }

            for (int i = 0;
                 i < definitions.Count;
                 i++)
            {
                EmberweaverSpellDefinition definition =
                    definitions[i];

                if (definition == null ||
                    !definition.IsPyroclasm)
                {
                    continue;
                }

                Spell spell =
                    ResolveSpell(
                        definition);

                if (spell != null)
                {
                    spell.Cooldown =
                        cooldown;
                }
            }
        }

        private static void ResetBoundPyroclasmHotkeys(
            bool refreshBinding)
        {
            if (GameData.CurrentCharacterSlot == null ||
                GameData.GM == null ||
                GameData.GM.HKManager == null)
            {
                return;
            }

            ResetBar(
                false,
                refreshBinding);

            ResetBar(
                true,
                refreshBinding);
        }

        private static void ResetBar(
            bool secondary,
            bool refreshBinding)
        {
            string[] spellIds =
                secondary
                    ? GameData.CurrentCharacterSlot.SecHKSpells
                    : GameData.CurrentCharacterSlot.HKSpells;

            if (spellIds == null)
            {
                return;
            }

            int maximum =
                Math.Min(
                    10,
                    spellIds.Length);

            for (int index = 0;
                 index < maximum;
                 index++)
            {
                EmberweaverSpellDefinition definition;

                if (string.IsNullOrEmpty(
                        spellIds[index]) ||
                    !EmberweaverSpellCatalog.TryGetDefinition(
                        spellIds[index],
                        out definition) ||
                    definition == null ||
                    !definition.IsPyroclasm)
                {
                    continue;
                }

                Hotkeys hotkey =
                    GetHotkey(
                        secondary,
                        index);

                if (hotkey == null)
                {
                    continue;
                }

                if (refreshBinding)
                {
                    Spell spell =
                        ResolveSpell(
                            definition);

                    if (spell != null)
                    {
                        try
                        {
                            // Rebuild once at proc activation so the visible
                            // hotkey reflects the now-ready spell. The real
                            // live cooldown members are still cleared below.
                            hotkey.AssignSpellFromBook(
                                spell);
                        }
                        catch (Exception exception)
                        {
                            Plugin.LogWarning(
                                "Emberweaver: Flashfire hotkey refresh failed for " +
                                definition.Name +
                                ": " +
                                exception.Message);
                        }
                    }
                }

                ForceHotkeyReady(
                    hotkey);
            }
        }

        private static bool TryGetBoundPyroclasmDefinition(
            Hotkeys hotkey,
            out EmberweaverSpellDefinition definition)
        {
            definition =
                null;

            if (hotkey == null ||
                GameData.CurrentCharacterSlot == null ||
                GameData.GM == null ||
                GameData.GM.HKManager == null)
            {
                return false;
            }

            if (TryGetDefinitionFromBar(
                    hotkey,
                    false,
                    GameData.CurrentCharacterSlot.HKSpells,
                    out definition))
            {
                return true;
            }

            return TryGetDefinitionFromBar(
                hotkey,
                true,
                GameData.CurrentCharacterSlot.SecHKSpells,
                out definition);
        }

        private static bool TryGetDefinitionFromBar(
            Hotkeys hotkey,
            bool secondary,
            string[] spellIds,
            out EmberweaverSpellDefinition definition)
        {
            definition =
                null;

            if (spellIds == null)
            {
                return false;
            }

            int maximum =
                Math.Min(
                    10,
                    spellIds.Length);

            for (int index = 0;
                 index < maximum;
                 index++)
            {
                if (GetHotkey(
                        secondary,
                        index) != hotkey)
                {
                    continue;
                }

                string spellId =
                    spellIds[index];

                return
                    !string.IsNullOrEmpty(spellId) &&
                    EmberweaverSpellCatalog.TryGetDefinition(
                        spellId,
                        out definition) &&
                    definition != null &&
                    definition.IsPyroclasm;
            }

            return false;
        }

        private static Spell ResolveSpell(
            EmberweaverSpellDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            if (definition.RuntimeSpell != null)
            {
                return definition.RuntimeSpell;
            }

            return GameData.SpellDatabase != null
                ? GameData.SpellDatabase.GetSpellByID(
                    definition.Id)
                : null;
        }

        private static void ForceHotkeyReady(
            Hotkeys hotkey)
        {
            if (hotkey == null)
            {
                return;
            }

            Type type =
                hotkey.GetType();

            FieldInfo[] fields;

            if (!CooldownFieldCache.TryGetValue(
                    type,
                    out fields))
            {
                List<FieldInfo> matches =
                    new List<FieldInfo>();

                FieldInfo[] all =
                    type.GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                for (int i = 0;
                     i < all.Length;
                     i++)
                {
                    FieldInfo field =
                        all[i];

                    if (field != null &&
                        IsCooldownMemberName(
                            field.Name) &&
                        IsSupportedNumberType(
                            field.FieldType))
                    {
                        matches.Add(
                            field);
                    }
                }

                fields =
                    matches.ToArray();

                CooldownFieldCache[type] =
                    fields;
            }

            for (int i = 0;
                 i < fields.Length;
                 i++)
            {
                TrySetZero(
                    fields[i],
                    hotkey);
            }

            PropertyInfo[] properties;

            if (!CooldownPropertyCache.TryGetValue(
                    type,
                    out properties))
            {
                List<PropertyInfo> matches =
                    new List<PropertyInfo>();

                PropertyInfo[] all =
                    type.GetProperties(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                for (int i = 0;
                     i < all.Length;
                     i++)
                {
                    PropertyInfo property =
                        all[i];

                    if (property != null &&
                        property.CanWrite &&
                        property.GetIndexParameters().Length == 0 &&
                        IsCooldownMemberName(
                            property.Name) &&
                        IsSupportedNumberType(
                            property.PropertyType))
                    {
                        matches.Add(
                            property);
                    }
                }

                properties =
                    matches.ToArray();

                CooldownPropertyCache[type] =
                    properties;
            }

            for (int i = 0;
                 i < properties.Length;
                 i++)
            {
                TrySetZero(
                    properties[i],
                    hotkey);
            }
        }

        private static bool IsCooldownMemberName(
            string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string lower =
                name.ToLowerInvariant();

            return
                lower == "cd" ||
                lower.StartsWith("cd") ||
                lower.EndsWith("cd") ||
                lower.Contains("cooldown");
        }

        private static bool IsSupportedNumberType(
            Type type)
        {
            return
                type == typeof(float) ||
                type == typeof(double) ||
                type == typeof(int) ||
                type == typeof(long);
        }

        private static void TrySetZero(
            FieldInfo field,
            object instance)
        {
            try
            {
                if (field.FieldType == typeof(float))
                {
                    field.SetValue(instance, 0f);
                }
                else if (field.FieldType == typeof(double))
                {
                    field.SetValue(instance, 0d);
                }
                else if (field.FieldType == typeof(int))
                {
                    field.SetValue(instance, 0);
                }
                else if (field.FieldType == typeof(long))
                {
                    field.SetValue(instance, 0L);
                }
            }
            catch
            {
            }
        }

        private static void TrySetZero(
            PropertyInfo property,
            object instance)
        {
            try
            {
                if (property.PropertyType == typeof(float))
                {
                    property.SetValue(instance, 0f, null);
                }
                else if (property.PropertyType == typeof(double))
                {
                    property.SetValue(instance, 0d, null);
                }
                else if (property.PropertyType == typeof(int))
                {
                    property.SetValue(instance, 0, null);
                }
                else if (property.PropertyType == typeof(long))
                {
                    property.SetValue(instance, 0L, null);
                }
            }
            catch
            {
            }
        }

        private static Hotkeys GetHotkey(
            bool secondary,
            int index)
        {
            if (GameData.GM == null ||
                GameData.GM.HKManager == null ||
                index < 0)
            {
                return null;
            }

            if (secondary)
            {
                if (GameData.GM.HKManager.SecondHotkeys == null ||
                    GameData.GM.HKManager.SecondHotkeys.Count <= index)
                {
                    return null;
                }

                return GameData.GM.HKManager.SecondHotkeys[index];
            }

            if (GameData.GM.HKManager.FirstHotkeys == null ||
                GameData.GM.HKManager.FirstHotkeys.Count <= index)
            {
                return null;
            }

            return GameData.GM.HKManager.FirstHotkeys[index];
        }
    }
}
