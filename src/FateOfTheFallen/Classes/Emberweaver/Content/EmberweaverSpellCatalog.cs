using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    internal sealed class EmberweaverSpellDefinition
    {
        internal string Id;
        internal string Name;
        internal string Description;
        internal int RequiredLevel;
        internal int Damage;
        internal int ManaCost;
        internal float CastTime;
        internal float Cooldown;
        internal string IconFile;
        internal bool IsPyroclasm;
        internal bool GeneratesTwoHeat;
        internal bool RaidTier;

        // Wildfire tuning is spell-family specific. A value <= 0 uses the
        // class-wide base/critical Wildfire contribution.
        internal float WildfireShareOverride;

        // Scorch-family ranks front-load this fraction of the current rolling
        // Wildfire bank into the next scheduled Wildfire tick. This accelerates
        // the burn without creating bonus damage or bypassing spell attribution.
        internal float WildfirePulseFraction;

        internal Spell RuntimeSpell;

        internal bool IsCombust
        {
            get
            {
                return string.Equals(
                    Id,
                    EmberweaverSpellCatalog.CombustId,
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        internal bool IsKindleFamily
        {
            get
            {
                return
                    string.Equals(Id, EmberweaverSpellCatalog.KindleId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Id, EmberweaverSpellCatalog.StokeId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Id, EmberweaverSpellCatalog.ConflagrateId, StringComparison.OrdinalIgnoreCase);
            }
        }

        internal bool IsScorchFamily
        {
            get { return WildfirePulseFraction > 0f; }
        }
    }

    /// <summary>
    /// Canonical Emberweaver spell registry.
    ///
    /// Spell IDs are stable FOTF content IDs. Item IDs for the corresponding
    /// teaching scrolls live separately in EmberweaverItemIds.
    /// </summary>
    internal static class EmberweaverSpellCatalog
    {
        internal const string EmberBoltId =
            "FOTF_EW_SPELL_EMBER_BOLT";

        internal const string PyroclasmId =
            "FOTF_EW_SPELL_PYROCLASM";

        internal const string ScorchId =
            "FOTF_EW_SPELL_SCORCH";

        internal const string KindleId =
            "FOTF_EW_SPELL_KINDLE";

        internal const string FlameSurgeId =
            "FOTF_EW_SPELL_FLAME_SURGE";

        internal const string SearingScorchId =
            "FOTF_EW_SPELL_SEARING_SCORCH";

        internal const string BlazingSurgeId =
            "FOTF_EW_SPELL_BLAZING_SURGE";

        internal const string StokeId =
            "FOTF_EW_SPELL_STOKE";

        internal const string CombustId =
            "FOTF_EW_SPELL_COMBUST";

        internal const string InfernalScorchId =
            "FOTF_EW_SPELL_INFERNAL_SCORCH";

        internal const string CataclysmicSurgeId =
            "FOTF_EW_SPELL_CATACLYSMIC_SURGE";

        internal const string ConflagrateId =
            "FOTF_EW_SPELL_CONFLAGRATE";

        internal const string SearingBoltId =
            "FOTF_EW_SPELL_SEARING_BOLT";

        internal const string GreaterPyroclasmId =
            "FOTF_EW_SPELL_GREATER_PYROCLASM";

        internal const string InfernoBoltId =
            "FOTF_EW_SPELL_INFERNO_BOLT";

        internal const string DevastatingPyroclasmId =
            "FOTF_EW_SPELL_DEVASTATING_PYROCLASM";

        internal const string CataclysmicBoltId =
            "FOTF_EW_SPELL_CATACLYSMIC_BOLT";

        internal const string ApocalypticPyroclasmId =
            "FOTF_EW_SPELL_APOCALYPTIC_PYROCLASM";

        internal const string WildfireEffectId =
            "FOTF_EW_EFFECT_WILDFIRE";

        private static Spell _wildfireEffectSpell;

        internal static Spell WildfireEffectSpell
        {
            get { return _wildfireEffectSpell; }
        }

        private static readonly List<EmberweaverSpellDefinition>
            Definitions =
                new List<EmberweaverSpellDefinition>
                {
                    Def(
                        EmberBoltId,
                        "Ember Bolt",
                        "Hurls a focused ember at a single enemy.",
                        1,
                        20,
                        20,
                        2.0f,
                        5f,
                        "FireMage_27.png",
                        false,
                        false,
                        false,
                        0f,
                        0f),

                    Def(
                        PyroclasmId,
                        "Pyroclasm",
                        "Unleashes a concentrated blast of fire at a single enemy, feeding Wildfire especially strongly.",
                        2,
                        32,
                        25,
                        3.0f,
                        8f,
                        "FireMage_28.png",
                        true,
                        false,
                        false,
                        0.50f,
                        0f),

                    Def(
                        ScorchId,
                        "Scorch",
                        "Instantly scorches a single enemy and front-loads part of its stored Wildfire into the next burn tick.",
                        4,
                        32,
                        25,
                        0f,
                        4f,
                        "FireMage_6.png",
                        false,
                        false,
                        false,
                        0f,
                        0.15f),

                    Def(
                        KindleId,
                        "Kindle",
                        "Kindles a focused flame inside one enemy. It deals moderate direct damage and stores an unusually large share of that damage in Wildfire.",
                        6,
                        63,
                        45,
                        1.5f,
                        6f,
                        "FireMage_33.png",
                        false,
                        false,
                        false,
                        0.60f,
                        0f),

                    Def(
                        FlameSurgeId,
                        "Flame Surge",
                        "A sudden surge of fire that strikes a single enemy, generates additional Heat, and heavily feeds Wildfire.",
                        8,
                        126,
                        80,
                        0f,
                        12f,
                        "FireMage_11.png",
                        false,
                        true,
                        false,
                        0.50f,
                        0f),

                    Def(
                        SearingBoltId,
                        "Searing Bolt",
                        "A stronger Ember Bolt that sears a single enemy.",
                        12,
                        365,
                        180,
                        2.0f,
                        6f,
                        "FireMage_10.png",
                        false,
                        false,
                        false,
                        0f,
                        0f),

                    Def(
                        GreaterPyroclasmId,
                        "Greater Pyroclasm",
                        "A more destructive Pyroclasm focused on a single enemy, strongly feeding Wildfire.",
                        12,
                        648,
                        200,
                        3.0f,
                        8f,
                        "FireMage_18.png",
                        true,
                        false,
                        false,
                        0.50f,
                        0f),

                    Def(
                        SearingScorchId,
                        "Searing Scorch",
                        "An improved instant Scorch that accelerates a larger portion of the target's stored Wildfire.",
                        16,
                        324,
                        150,
                        0f,
                        4f,
                        "FireMage_8.png",
                        false,
                        false,
                        false,
                        0f,
                        0.20f),

                    Def(
                        BlazingSurgeId,
                        "Blazing Surge",
                        "An explosive instant surge that generates additional Heat and heavily feeds Wildfire.",
                        18,
                        765,
                        260,
                        0f,
                        12f,
                        "FireMage_31.png",
                        false,
                        true,
                        false,
                        0.50f,
                        0f),

                    Def(
                        StokeId,
                        "Stoke",
                        "Stokes the flame inside one enemy, dealing direct damage while feeding a large portion into Wildfire.",
                        18,
                        585,
                        220,
                        1.5f,
                        6f,
                        "FireMage_33.png",
                        false,
                        false,
                        false,
                        0.60f,
                        0f),

                    Def(
                        CombustId,
                        "Combust",
                        "Consumes all Heat to detonate 10% of the target's stored Wildfire per Heat consumed. Requires at least 5 Heat.",
                        20,
                        0,
                        225,
                        0f,
                        30f,
                        "FireMage_34.png",
                        false,
                        false,
                        false,
                        0f,
                        0f),

                    Def(
                        InfernoBoltId,
                        "Inferno Bolt",
                        "A concentrated inferno hurled at a single enemy.",
                        24,
                        1242,
                        300,
                        2.0f,
                        6f,
                        "FireMage_19.png",
                        false,
                        false,
                        false,
                        0f,
                        0f),

                    Def(
                        DevastatingPyroclasmId,
                        "Devastating Pyroclasm",
                        "A devastating single-target blast of fire that strongly feeds Wildfire.",
                        24,
                        1296,
                        450,
                        3.0f,
                        8f,
                        "FireMage_26.png",
                        true,
                        false,
                        false,
                        0.50f,
                        0f),

                    Def(
                        InfernalScorchId,
                        "Infernal Scorch",
                        "A powerful instant Scorch that aggressively accelerates the target's rolling Wildfire.",
                        28,
                        1080,
                        300,
                        0f,
                        4f,
                        "FireMage_20.png",
                        false,
                        false,
                        false,
                        0f,
                        0.25f),

                    Def(
                        CataclysmicSurgeId,
                        "Cataclysmic Surge",
                        "A catastrophic instant surge that generates additional Heat and pours damage into Wildfire.",
                        30,
                        2160,
                        750,
                        0f,
                        12f,
                        "FireMage_34.png",
                        false,
                        true,
                        false,
                        0.50f,
                        0f),

                    Def(
                        ConflagrateId,
                        "Conflagrate",
                        "A high-level Kindle rank that burns one enemy and pours a large share of its direct damage into Wildfire.",
                        30,
                        1710,
                        575,
                        1.5f,
                        6f,
                        "FireMage_33.png",
                        false,
                        false,
                        false,
                        0.60f,
                        0f),

                    Def(
                        CataclysmicBoltId,
                        "Cataclysmic Bolt",
                        "Raid-tier Ember Bolt rank, condensing catastrophic heat into one target.",
                        35,
                        6003,
                        1355,
                        2.0f,
                        6f,
                        "FireMage_2.png",
                        false,
                        false,
                        true,
                        0f,
                        0f),

                    Def(
                        ApocalypticPyroclasmId,
                        "Apocalyptic Pyroclasm",
                        "Raid-tier Pyroclasm rank, annihilating a single target and feeding an enormous Wildfire.",
                        35,
                        6120,
                        1700,
                        3.0f,
                        8f,
                        "FireMage_39.png",
                        true,
                        false,
                        true,
                        0.50f,
                        0f)
                };

        internal static IReadOnlyList<EmberweaverSpellDefinition> All
        {
            get
            {
                return Definitions;
            }
        }

        internal static void Register(
            SpellDB database)
        {
            if (database == null ||
                database.SpellDatabase == null)
            {
                return;
            }

            Class emberweaver =
                EmberweaverCatalog.TryEnsureClass();

            if (emberweaver == null)
            {
                return;
            }

            Spell template =
                FindDirectDamageTemplate(
                    database);

            if (template == null)
            {
                Plugin.ModLog.Error(
                    "Emberweaver: no native direct-damage spell template was available.");

                return;
            }

            for (int i = 0;
                 i < Definitions.Count;
                 i++)
            {
                EmberweaverSpellDefinition definition =
                    Definitions[i];

                Spell spell =
                    FindNativeOrCustom(
                        database,
                        definition.Id);

                if (spell == null)
                {
                    spell =
                        UnityEngine.Object.Instantiate(
                            template);
                }

                if (spell == null)
                {
                    continue;
                }

                ApplyDefinition(
                    spell,
                    definition,
                    emberweaver);

                CustomSpellRegistry.Register(
                    database,
                    spell);

                definition.RuntimeSpell =
                    spell;
            }

            RegisterWildfireEffect(
                database);
        }

        internal static Spell GetById(
            string id)
        {
            if (string.Equals(
                    id,
                    WildfireEffectId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return _wildfireEffectSpell;
            }

            EmberweaverSpellDefinition definition;

            return TryGetDefinition(
                    id,
                    out definition)
                ? definition.RuntimeSpell
                : null;
        }

        internal static bool TryGetDefinition(
            Spell spell,
            out EmberweaverSpellDefinition definition)
        {
            definition = null;

            return
                spell != null &&
                TryGetDefinition(
                    spell.Id,
                    out definition);
        }

        internal static bool TryGetDefinition(
            string id,
            out EmberweaverSpellDefinition definition)
        {
            definition = null;

            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            for (int i = 0;
                 i < Definitions.Count;
                 i++)
            {
                if (string.Equals(
                        Definitions[i].Id,
                        id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    definition =
                        Definitions[i];

                    return true;
                }
            }

            return false;
        }

        internal static bool IsEmberweaverSpell(
            Spell spell)
        {
            EmberweaverSpellDefinition unused;

            return TryGetDefinition(
                spell,
                out unused);
        }

        private static EmberweaverSpellDefinition Def(
            string id,
            string name,
            string description,
            int level,
            int damage,
            int mana,
            float castTime,
            float cooldown,
            string iconFile,
            bool isPyroclasm,
            bool generatesTwoHeat,
            bool raidTier,
            float wildfireShareOverride,
            float wildfirePulseFraction)
        {
            return new EmberweaverSpellDefinition
            {
                Id = id,
                Name = name,
                Description = description,
                RequiredLevel = level,
                Damage = damage,
                ManaCost = mana,
                CastTime = castTime,
                Cooldown = cooldown,
                IconFile = iconFile,
                IsPyroclasm = isPyroclasm,
                GeneratesTwoHeat = generatesTwoHeat,
                RaidTier = raidTier,
                WildfireShareOverride = wildfireShareOverride,
                WildfirePulseFraction = wildfirePulseFraction
            };
        }

        private static void ApplyDefinition(
            Spell spell,
            EmberweaverSpellDefinition definition,
            Class emberweaver)
        {
            spell.name =
                "FOTF - Emberweaver - " +
                definition.Name;

            spell.Id =
                definition.Id;

            spell.SpellName =
                definition.Name;

            spell.SpellDesc =
                definition.Description;

            spell.Type =
                Spell.SpellType.Damage;

            spell.Line =
                Spell.SpellLine.Direct_Damage;

            spell.RequiredLevel =
                definition.RequiredLevel;

            spell.ManaCost =
                definition.ManaCost;

            spell.TargetDamage =
                definition.IsCombust
                    ? 0
                    : definition.Damage;

            spell.Aggro =
                definition.IsCombust
                    ? 0
                    : definition.Damage;

            // Erenshor stores spell cast time in 60-Hz ticks, not seconds.
            // Blightcaller uses the same seconds * 60 conversion.
            spell.SpellChargeTime =
                definition.CastTime *
                60f;

            // Cooldown is stored by Erenshor in seconds.
            spell.Cooldown =
                definition.Cooldown;

            spell.SpellDurationInTicks =
                0;

            spell.StatusEffectToApply =
                null;

            spell.MyDamageType =
                GameData.DamageType.Elemental;

            spell.SpellRange =
                60f;

            spell.SelfOnly =
                false;

            spell.CrowdControlSpell =
                false;

            // Direct Emberweaver spells must survive repeated catalog
            // registration as Sim-usable. The internal Wildfire carrier below
            // explicitly remains unavailable to SimPlayer spellbooks.
            spell.SimUsable =
                true;

            spell.SimsNeedHelpToLearn =
                false;

            spell.CanHitPlayers =
                true;

            spell.UsedBy =
                new List<Class>
                {
                    emberweaver
                };

            spell.hideFlags =
                HideFlags.HideAndDontSave;

            /*
             * Retain the native template's projectile/audio presentation,
             * while forcing all Emberweaver direct spells through Erenshor's
             * Elemental resistance path.
             */
            EmbeddedSpriteLoader.TryAssignSpriteMember(
                spell,
                EmbeddedSpriteLoader.LoadByFileName(
                    definition.IconFile));
        }

        private static void RegisterWildfireEffect(
            SpellDB database)
        {
            Spell spell =
                FindNativeOrCustom(
                    database,
                    WildfireEffectId);

            if (spell == null)
            {
                spell =
                    ScriptableObject.CreateInstance<Spell>();
            }

            if (spell == null)
            {
                return;
            }

            spell.name =
                "FOTF - Emberweaver - Wildfire Effect";

            spell.Id =
                WildfireEffectId;

            spell.SpellName =
                "Wildfire";

            spell.SpellDesc =
                "A rolling Emberweaver burn fed by direct fire damage.";

            spell.Type =
                Spell.SpellType.StatusEffect;

            // Generic prevents Wildfire from colliding with unrelated native
            // status-effect spell lines while still allowing the same effect
            // from the same caster to refresh normally.
            spell.Line =
                Spell.SpellLine.Generic;

            spell.RequiredLevel =
                1;

            spell.ManaCost =
                0;

            // Wildfire now owns an independent 1.5-second scheduler, mirroring
            // the proven Blightcaller secondary-tick architecture. Keep this
            // native status as the ownership/icon carrier only; TargetDamage=0
            // prevents Erenshor's 3-second TickEffects pass from double-ticking.
            spell.TargetDamage =
                0;

            spell.Aggro =
                0;

            spell.SpellChargeTime =
                0f;

            spell.Cooldown =
                0f;

            // The custom scheduler removes the carrier when its reservoir is
            // exhausted. A long native duration prevents the carrier from
            // expiring while sustained casting continually feeds the reservoir.
            spell.SpellDurationInTicks =
                999;

            spell.StatusEffectToApply =
                null;

            spell.MyDamageType =
                GameData.DamageType.Elemental;

            spell.SpellRange =
                60f;

            spell.SelfOnly =
                false;

            spell.CrowdControlSpell =
                false;

            spell.SimUsable =
                false;

            spell.CanHitPlayers =
                true;

            // This is an internal carrier spell, not a spell that can be
            // learned or shown as an Emberweaver spellbook entry.
            spell.UsedBy =
                new List<Class>();

            spell.hideFlags =
                HideFlags.HideAndDontSave;

            EmbeddedSpriteLoader.TryAssignSpriteMember(
                spell,
                EmbeddedSpriteLoader.LoadByFileName(
                    "FireMage_5.png"));

            CustomSpellRegistry.Register(
                database,
                spell);

            _wildfireEffectSpell =
                spell;
        }

        private static Spell FindNativeOrCustom(
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
            if (database == null ||
                database.SpellDatabase == null)
            {
                return null;
            }

            Spell fallback =
                null;

            string[] preferred =
            {
                "fire",
                "flame",
                "ember",
                "scorch",
                "burn"
            };

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

                if (!string.IsNullOrEmpty(spell.Id) &&
                    spell.Id.StartsWith(
                        "FOTF_",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback =
                        spell;
                }

                string name =
                    (spell.SpellName ??
                     spell.name ??
                     string.Empty)
                    .ToLowerInvariant();

                for (int p = 0;
                     p < preferred.Length;
                     p++)
                {
                    if (name.Contains(
                            preferred[p]))
                    {
                        return spell;
                    }
                }
            }

            return fallback;
        }
    }
}
