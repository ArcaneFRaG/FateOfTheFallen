using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class BlightcallerCatalog
    {
        private static Class _blightcallerClass;

        private static readonly Dictionary<string, Spell>
            _spellsByKey =
                new Dictionary<string, Spell>();

        private static readonly Dictionary<int, Spell>
            _spellsByInstanceId =
                new Dictionary<int, Spell>();

        internal static Class BlightcallerClass
        {
            get
            {
                return _blightcallerClass;
            }
        }

        internal static IReadOnlyDictionary<string, Spell>
            SpellsByKey
        {
            get
            {
                return _spellsByKey;
            }
        }

        internal static void EnsureClass()
        {
            if (_blightcallerClass != null)
            {
                return;
            }

            _blightcallerClass =
                ScriptableObject.CreateInstance<Class>();

            _blightcallerClass.name =
                "CS - Blightcaller Class";

            _blightcallerClass.ClassName =
                "Blightcaller";

            _blightcallerClass.DisplayName =
                "Blightcaller";

            /*
             * Blightcaller base class scaling.
             *
             * These mirror the native Arcanist-style
             * caster scaling values currently confirmed
             * to work correctly for Blightcaller.
             */
            _blightcallerClass.MitigationBonus =
                1f;

            _blightcallerClass.StrBenefit =
                3;

            _blightcallerClass.EndBenefit =
                10;

            _blightcallerClass.DexBenefit =
                3;

            _blightcallerClass.AgiBenefit =
                3;

            _blightcallerClass.IntBenefit =
                14;

            _blightcallerClass.WisBenefit =
                9;

            _blightcallerClass.ChaBenefit =
                10;

            _blightcallerClass.AggroMod =
                1.6f;

            _blightcallerClass.hideFlags =
                HideFlags.HideAndDontSave;
        }

        internal static void EnsureSpells(
            Spell[] nativeSpells)
        {
            EnsureClass();

            if (nativeSpells == null)
            {
                return;
            }

            foreach (
                BlightcallerSpellDefinition definition
                in BlightcallerSpellData.All)
            {
                Spell spell;

                if (_spellsByKey.TryGetValue(
                        definition.Key,
                        out spell))
                {
                    continue;
                }

                spell = CreateSpell(
                    definition,
                    nativeSpells);

                spell.UsedBy =
                    new List<Class>
                    {
                        _blightcallerClass
                    };

                _spellsByKey.Add(
                    definition.Key,
                    spell);

                _spellsByInstanceId[
                    spell.GetInstanceID()] =
                    spell;
            }

            BindAppliedEffects();
        }

        internal static void RegisterSpells(
            SpellDB spellDB)
        {
            if (spellDB == null)
            {
                return;
            }

            if (spellDB.SpellDatabase == null)
            {
                return;
            }

            EnsureSpells(
                spellDB.SpellDatabase);

            List<Spell> combinedSpells =
                new List<Spell>(
                    spellDB.SpellDatabase);

            foreach (
                KeyValuePair<string, Spell> pair
                in _spellsByKey)
            {
                Spell spell = pair.Value;

                if (spell == null)
                {
                    continue;
                }

                if (combinedSpells.Contains(spell))
                {
                    continue;
                }

                combinedSpells.Add(spell);
            }

            spellDB.SpellDatabase =
                combinedSpells.ToArray();
        }

        internal static bool TryGetSpell(
            string key,
            out Spell spell)
        {
            if (string.IsNullOrEmpty(key))
            {
                spell = null;
                return false;
            }

            return _spellsByKey.TryGetValue(
                key,
                out spell);
        }

        internal static Spell GetSpell(
            string key)
        {
            Spell spell;

            if (!TryGetSpell(
                    key,
                    out spell))
            {
                return null;
            }

            return spell;
        }

        internal static Spell GetSpellById(
            string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            Spell spell;

            foreach (
                KeyValuePair<string, Spell> pair
                in _spellsByKey)
            {
                spell = pair.Value;

                if (spell == null)
                {
                    continue;
                }

                if (string.Equals(
                        spell.Id,
                        id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return spell;
                }
            }

            return null;
        }

        internal static int RestoreKnownSpells()
        {
            return 0;
        }

        internal static bool TryGetDefinition(
            Spell spell,
            out BlightcallerSpellDefinition definition)
        {
            definition = null;

            if (spell == null)
            {
                return false;
            }

            foreach (
                BlightcallerSpellDefinition candidate
                in BlightcallerSpellData.All)
            {
                if (string.Equals(
                        spell.Id,
                        "ARCBLC_" +
                        candidate.Key.ToUpperInvariant(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    definition = candidate;
                    return true;
                }
            }

            return false;
        }

        internal static bool IsBlightcallerClass(
            Class characterClass)
        {
            if (characterClass == null)
            {
                return false;
            }

            if (ReferenceEquals(
                    characterClass,
                    _blightcallerClass))
            {
                return true;
            }

            return string.Equals(
                characterClass.ClassName,
                "Blightcaller",
                StringComparison.OrdinalIgnoreCase);
        }

        private static Spell CreateSpell(
            BlightcallerSpellDefinition definition,
            Spell[] nativeSpells)
        {
            Spell spell =
                ScriptableObject.CreateInstance<Spell>();

            spell.name =
                "CS - Blightcaller - " +
                definition.Name;

            spell.Id =
                "ARCBLC_" +
                definition.Key.ToUpperInvariant();

            spell.UsedBy =
                new List<Class>();

            spell.Type =
                definition.Type;

            spell.Line =
                definition.Line;

            spell.RequiredLevel =
                definition.Level;

            spell.SpellName =
                definition.Name;

            spell.SpellDesc =
                definition.Description;

            spell.SpellChargeTime =
                definition.CastSeconds *
                60f;

            spell.SpellDurationInTicks =
                definition.DurationTicks;

            spell.ManaCost =
                definition.ManaCost;

            spell.Aggro =
                definition.Aggro;

            spell.TargetDamage =
                definition.TargetDamage;

            spell.Cooldown =
                definition.CooldownSeconds;

            spell.Lifetap =
                definition.Lifetap;

            spell.FearTarget =
                definition.Fear;

            spell.RootTarget =
                definition.Root;

            spell.SelfOnly =
                definition.SelfOnly;

            spell.BreakOnAnyAction =
                definition.BreakOnAnyAction;

            spell.GrantInvisibility =
                definition.GrantInvisibility;

            spell.CrowdControlSpell =
                definition.HiddenEffect;

            spell.MyDamageType =
                definition.DamageType;

            spell.ResistModifier =
                definition.ResistModifier;

            spell.MR = definition.MR;

            spell.ER = definition.ER;

            spell.PR = definition.PR;

            spell.VR = definition.VR;

            spell.AtkRollModifier = definition.AtkRollModifier;


            spell.SpellRange =
                definition.Range;

            spell.CanHitPlayers =
                true;

            spell.NoResonate =
                false;

            spell.SimUsable =
                !definition.HiddenEffect;

            spell.StatusEffectMessageOnPlayer =
                definition.Name;

            spell.StatusEffectMessageOnNPC =
                definition.Name;

            spell.ChargeVariations =
                new List<AudioClip>();

            spell.CompleteVariations =
                new List<AudioClip>();

            Spell template =
                FindNativeTemplate(
                    definition,
                    nativeSpells);

            if (template != null)
            {
                spell.SpellIcon =
                    template.SpellIcon;

                spell.ChargeSound =
                    template.ChargeSound;

                spell.CompleteSound =
                    template.CompleteSound;

                spell.SpellResolveFXIndex =
                    template.SpellResolveFXIndex;

                spell.SpellChargeFXIndex =
                    template.SpellChargeFXIndex;
            }

            if (!string.IsNullOrEmpty(
                    definition.IconResource))
            {
                Sprite customIcon =
                    BlightcallerItemFactory.Load(
                        definition.IconResource);

                if (customIcon != null)
                {
                    spell.SpellIcon =
                        customIcon;
                }
            }

            spell.hideFlags =
                HideFlags.HideAndDontSave;

            return spell;
        }

        private static Spell FindNativeTemplate(
            BlightcallerSpellDefinition definition,
            Spell[] nativeSpells)
        {
            if (nativeSpells == null)
            {
                return null;
            }

            // First preference:
            // Same spell type AND same spell line.
            foreach (Spell spell in nativeSpells)
            {
                if (spell == null)
                {
                    continue;
                }

                if (spell.Type != definition.Type)
                {
                    continue;
                }

                if (spell.Line != definition.Line)
                {
                    continue;
                }

                if (spell.SpellIcon == null)
                {
                    continue;
                }

                return spell;
            }

            // Second preference:
            // Same spell type with a usable icon.
            foreach (Spell spell in nativeSpells)
            {
                if (spell == null)
                {
                    continue;
                }

                if (spell.Type != definition.Type)
                {
                    continue;
                }

                if (spell.SpellIcon == null)
                {
                    continue;
                }

                return spell;
            }

            // Final fallback:
            // Any native spell with an icon.
            foreach (Spell spell in nativeSpells)
            {
                if (spell == null)
                {
                    continue;
                }

                if (spell.SpellIcon == null)
                {
                    continue;
                }

                return spell;
            }

            return null;
        }

        private static void BindAppliedEffects()
        {
            foreach (
                BlightcallerSpellDefinition definition
                in BlightcallerSpellData.All)
            {
                if (string.IsNullOrEmpty(
                        definition.AppliedEffectKey))
                {
                    continue;
                }

                Spell visibleSpell;

                if (!TryGetSpell(
                        definition.Key,
                        out visibleSpell))
                {
                    continue;
                }

                Spell appliedEffect;

                if (!TryGetSpell(
                        definition.AppliedEffectKey,
                        out appliedEffect))
                {
                    continue;
                }

                visibleSpell.StatusEffectToApply =
                    appliedEffect;
            }
        }
    }
}