using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    internal enum LightbringerSpellKind
    {
        Shield,
        Damage,
        HybridHealShield,
        GroupHealShield
    }

    // These families are independent offensive progressions, not extra heal ranks.
    // Keep stable spell IDs so learned r57 spells remain available.
    internal enum LightbringerDamageFamily
    {
        None,
        RadiantBolt,
        Judgement,
        SolarSpark
    }

    internal sealed class LightbringerSpellDefinition
    {
        internal string Id;
        internal string Name;
        internal string Description;
        internal int RequiredLevel;
        internal int ManaCost;
        internal float CastTime;
        internal float Cooldown;
        internal int TargetDamage;
        internal int TargetHealing;
        internal int ShieldAmount;
        internal float BarrierDuration;
        internal bool RaidTier;
        internal LightbringerSpellKind Kind;
        internal string IconFile;
        internal Spell RuntimeSpell;
        internal LightbringerDamageFamily DamageFamily;

        internal bool IsFriendlySpell
        {
            get
            {
                return Kind != LightbringerSpellKind.Damage;
            }
        }
    }

    internal static class LightbringerSpellCatalog
    {
        internal const string FlickerWardId = "FOTF_LB_SPELL_FLICKER_WARD";
        internal const string RadiantBoltId = "FOTF_LB_SPELL_RADIANT_BOLT";
        internal const string GuardiansBlessingId = "FOTF_LB_SPELL_GUARDIANS_BLESSING";
        internal const string RadiantEmbraceId = "FOTF_LB_SPELL_RADIANT_EMBRACE";
        internal const string AegisOfFaithId = "FOTF_LB_SPELL_AEGIS_OF_FAITH";
        internal const string DawnBoltId = "FOTF_LB_SPELL_DAWN_BOLT";
        internal const string LuminousMendingId = "FOTF_LB_SPELL_LUMINOUS_MENDING";
        internal const string RadiantCommunionId = "FOTF_LB_SPELL_RADIANT_COMMUNION";
        internal const string AegisOfDawnId = "FOTF_LB_SPELL_AEGIS_OF_DAWN";
        internal const string JudgementOfLightId = "FOTF_LB_SPELL_JUDGEMENT_OF_LIGHT";
        internal const string DivineIntercessionId = "FOTF_LB_SPELL_DIVINE_INTERCESSION";
        internal const string GreaterRadiantCommunionId = "FOTF_LB_SPELL_GREATER_RADIANT_COMMUNION";
        internal const string DawnsSanctuaryId = "FOTF_LB_SPELL_DAWNS_SANCTUARY";
        internal const string AegisOfEternityId = "FOTF_LB_SPELL_AEGIS_OF_ETERNITY";
        internal const string DivineInterventionId = "FOTF_LB_SPELL_DIVINE_INTERVENTION";
        internal const string SunsVerdictId = "FOTF_LB_SPELL_SUNS_VERDICT";

        // r58: second and third damage families + high-rank Radiant Bolt fillers.
        // Existing four offensive spell IDs deliberately remain unchanged.
        internal const string SolarSparkId = "FOTF_LB_SPELL_SOLAR_SPARK";
        internal const string JudgementId = "FOTF_LB_SPELL_JUDGEMENT";
        internal const string SolarFlareId = "FOTF_LB_SPELL_SOLAR_FLARE";
        internal const string GreaterJudgementId = "FOTF_LB_SPELL_GREATER_JUDGEMENT";
        internal const string SolarIgnitionId = "FOTF_LB_SPELL_SOLAR_IGNITION";
        internal const string RadiantLanceId = "FOTF_LB_SPELL_RADIANT_LANCE";
        internal const string CelestialLanceId = "FOTF_LB_SPELL_CELESTIAL_LANCE";
        internal const string SolarWrathId = "FOTF_LB_SPELL_SOLAR_WRATH";

        // r89 mana balance: intentionally tune class-authored spell costs only.
        // Hybrid wards and multi-target barriers carry a premium over direct
        // heals; raid-tier spells remain expensive but no longer overwhelm
        // a healer's mana pool. Native Druid spell costs are never modified.
        // The existing Sanctum Discipline Ascension applies on top of these
        // base costs, and saved spell IDs remain unchanged.
        private static readonly List<LightbringerSpellDefinition> Definitions =
            new List<LightbringerSpellDefinition>
            {
                Def(FlickerWardId, "Flicker Ward", "Friendly target absorbs 40 damage.", 1, 20, 1.0f, 4f, 0, 0, 40, 12f, false, LightbringerSpellKind.Shield, "FOTF_LB_FlickerWard.png"),
                Def(RadiantBoltId, "Radiant Bolt", "Single enemy takes 30 damage.", 1, 20, 2.0f, 5f, 30, 0, 0, 0f, false, LightbringerSpellKind.Damage, "FOTF_LB_RadiantLance.png"),
                Def(SolarSparkId, "Solar Spark", "Instantly strikes one enemy with a flash of solar light.", 4, 35, 0f, 10f, 35, 0, 0, 0f, false, LightbringerSpellKind.Damage, "FOTF_LB_SolarSpark.png"),
                Def(JudgementId, "Judgement", "Calls down concentrated holy judgement on a single enemy.", 7, 70, 2.0f, 8f, 110, 0, 0, 0f, false, LightbringerSpellKind.Damage, "FOTF_LB_Judgement.png"),
                Def(GuardiansBlessingId, "Guardian's Blessing", "Instantly shields an ally for 110 damage.", 7, 75, 0f, 30f, 0, 0, 110, 8f, false, LightbringerSpellKind.Shield, "FOTF_LB_GuardiansBlessing.png"),
                Def(RadiantEmbraceId, "Radiant Embrace", "Heals nearby group members for 60 and shields them for 30.", 11, 150, 2.7f, 6f, 0, 60, 30, 12f, false, LightbringerSpellKind.GroupHealShield, "FOTF_LB_RadiantEmbrace.png"),
                Def(SolarFlareId, "Solar Flare", "Instantly sears one enemy with a burst of light.", 12, 115, 0f, 10f, 230, 0, 0, 0f, false, LightbringerSpellKind.Damage, "FOTF_LB_SolarFlare.png"),
                Def(AegisOfFaithId, "Aegis of Faith", "Friendly target absorbs 225 damage.", 12, 80, 1.2f, 5f, 0, 0, 225, 12f, false, LightbringerSpellKind.Shield, "FOTF_LB_AegisOfFaith.png"),
                Def(DawnBoltId, "Dawn Bolt", "Single enemy takes 620 damage.", 14, 150, 2.0f, 6f, 620, 0, 0, 0f, false, LightbringerSpellKind.Damage, "FOTF_LB_CelestialLance.png"),
                Def(LuminousMendingId, "Luminous Mending", "Heals one ally for 225 and grants 175 absorb.", 18, 145, 1.6f, 5f, 0, 225, 175, 12f, false, LightbringerSpellKind.HybridHealShield, "FOTF_LB_LuminousMending.png"),
                Def(GreaterJudgementId, "Greater Judgement", "Unleashes a stronger holy judgement against one enemy.", 18, 215, 2.0f, 8f, 650, 0, 0, 0f, false, LightbringerSpellKind.Damage, "FOTF_LB_GreaterJudgement.png"),
                Def(RadiantCommunionId, "Radiant Communion", "Heals nearby group members for 375 and shields them for 200.", 22, 540, 2.3f, 6f, 0, 375, 200, 12f, false, LightbringerSpellKind.GroupHealShield, "FOTF_LB_RadiantCommunion.png"),
                Def(SolarIgnitionId, "Solar Ignition", "Instantly strikes one enemy with concentrated solar fire.", 24, 275, 0f, 11f, 710, 0, 0, 0f, false, LightbringerSpellKind.Damage, "FOTF_LB_SolarIgnition.png"),
                Def(AegisOfDawnId, "Aegis of Dawn", "Friendly target absorbs 1,150 damage.", 24, 320, 1.5f, 5f, 0, 0, 1150, 15f, false, LightbringerSpellKind.Shield, "FOTF_LB_AegisOfDawn.png"),
                Def(RadiantLanceId, "Radiant Lance", "Hurls focused light at one enemy; an efficient upgraded Radiant Bolt.", 25, 255, 2.0f, 5f, 1125, 0, 0, 0f, false, LightbringerSpellKind.Damage, "FOTF_LB_RadiantLance.png"),
                Def(JudgementOfLightId, "Judgement of Light", "Single enemy takes 1,450 damage.", 25, 280, 2.0f, 6f, 1450, 0, 0, 0f, false, LightbringerSpellKind.Damage, "FOTF_LB_GreaterJudgement.png"),
                Def(DivineIntercessionId, "Divine Intercession", "Instantly heals one ally for 350 and grants 1,500 absorb.", 29, 620, 0f, 45f, 0, 350, 1500, 8f, false, LightbringerSpellKind.HybridHealShield, "FOTF_LB_DivineIntercession.png"),
                Def(GreaterRadiantCommunionId, "Greater Radiant Communion", "Heals nearby group members for 650 and shields them for 350.", 30, 690, 1.5f, 6f, 0, 650, 350, 12f, false, LightbringerSpellKind.GroupHealShield, "FOTF_LB_GreaterRadiantCommunion.png"),
                Def(DawnsSanctuaryId, "Dawn's Sanctuary", "Heals nearby group members for 1,000 and shields them for 2,000.", 35, 1350, 2.0f, 30f, 0, 1000, 2000, 12f, true, LightbringerSpellKind.GroupHealShield, "FOTF_LB_DawnsSanctuary.png"),
                Def(AegisOfEternityId, "Aegis of Eternity", "Friendly target absorbs 3,150 damage.", 35, 800, 1.5f, 7f, 0, 0, 3150, 15f, true, LightbringerSpellKind.Shield, "FOTF_LB_AegisOfEternity.png"),
                Def(DivineInterventionId, "Divine Intervention", "Instantly heals one ally for 600 and grants 2,800 absorb.", 35, 1200, 0f, 45f, 0, 600, 2800, 10f, true, LightbringerSpellKind.HybridHealShield, "FOTF_LB_DivineIntervention.png"),
                Def(CelestialLanceId, "Celestial Lance", "Hurls a brilliant lance of celestial light at one enemy.", 35, 680, 2.0f, 5f, 2850, 0, 0, 0f, true, LightbringerSpellKind.Damage, "FOTF_LB_CelestialLance.png"),
                Def(SolarWrathId, "Solar Wrath", "Instantly burns one enemy with focused solar power.", 35, 620, 0f, 12f, 1800, 0, 0, 0f, true, LightbringerSpellKind.Damage, "FOTF_LB_SolarWrath.png"),
                Def(SunsVerdictId, "Sun's Verdict", "Single enemy takes 4,000 damage.", 35, 880, 2.3f, 8f, 4000, 0, 0, 0f, true, LightbringerSpellKind.Damage, "FOTF_LB_SolarWrath.png")
            };

        internal static IReadOnlyList<LightbringerSpellDefinition> All
        {
            get { return Definitions; }
        }

        internal static void Register(SpellDB database)
        {
            if (database == null || database.SpellDatabase == null)
                return;

            Class lightbringer = LightbringerCatalog.TryEnsureClass();
            if (lightbringer == null)
                return;

            Spell singleHealTemplate = FindSingleTargetHealTemplate(database);
            Spell groupHealTemplate = FindGroupHealTemplate(database, "Group Heal");
            Spell groupRevitalizeTemplate = FindGroupHealTemplate(database, "Group Revitalize");
            Spell majorGroupHealTemplate = FindGroupHealTemplate(database, "Major Group Heal");
            Spell damageTemplate = FindDamageTemplate(database);
            LightbringerNativeShield.Initialize(database);

            for (int i = 0; i < Definitions.Count; i++)
            {
                LightbringerSpellDefinition definition = Definitions[i];
                // Pure wards MUST originate from a confirmed native Shield-line
                // template. Never fall back to a heal/damage template here.
                bool pureWard = definition.Kind == LightbringerSpellKind.Shield;
                Spell template = pureWard ? null : ResolveTemplateForDefinition(
                    definition,
                    singleHealTemplate,
                    groupHealTemplate,
                    groupRevitalizeTemplate,
                    majorGroupHealTemplate,
                    damageTemplate);
                if (!pureWard && template == null)
                    continue;

                Spell existing = FindNativeOrCustom(database, definition.Id);
                Spell spell = pureWard
                    ? (existing != null && existing.Type == Spell.SpellType.Beneficial &&
                       string.Equals(existing.Line.ToString(), "Shield", StringComparison.OrdinalIgnoreCase)
                        ? existing : LightbringerNativeShield.Create(definition))
                    : existing;
                if (!pureWard && spell == null)
                    spell = UnityEngine.Object.Instantiate(template);
                if (spell == null)
                {
                    if (pureWard)
                        Plugin.LogWarning("[LBNativeShield] Skipped unsafe pure ward [" + definition.Name + "] (native template/amount unavailable).");
                    continue;
                }

                ApplyDefinition(spell, definition, lightbringer);
                CustomSpellRegistry.Register(database, spell);
                definition.RuntimeSpell = spell;
                if (pureWard)
                    Plugin.LogInfo("[LBNativeShield] Registered [" + definition.Name + "] Type=" +
                        spell.Type + " Line=" + spell.Line + " SelfOnly=" + spell.SelfOnly +
                        " DurationTicks=" + spell.SpellDurationInTicks);
            }
        }

        internal static Spell GetById(string id)
        {
            LightbringerSpellDefinition def;
            return TryGetDefinition(id, out def) ? def.RuntimeSpell : null;
        }

        internal static bool TryGetDefinition(Spell spell, out LightbringerSpellDefinition definition)
        {
            definition = null;
            return spell != null && TryGetDefinition(spell.Id, out definition);
        }

        internal static bool TryGetDefinition(string id, out LightbringerSpellDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(id))
                return false;

            for (int i = 0; i < Definitions.Count; i++)
            {
                if (string.Equals(Definitions[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    definition = Definitions[i];
                    return true;
                }
            }

            return false;
        }

        internal static bool IsLightbringerCustomSpell(Spell spell)
        {
            LightbringerSpellDefinition unused;
            return TryGetDefinition(spell, out unused);
        }

        // Pure wards use native Erenshor Beneficial/Shield registration.
        internal static bool IsPureShield(Spell spell)
        {
            LightbringerSpellDefinition definition;
            return TryGetDefinition(spell, out definition) &&
                   definition.Kind == LightbringerSpellKind.Shield;
        }

        private static LightbringerSpellDefinition Def(
            string id,
            string name,
            string description,
            int level,
            int mana,
            float castTime,
            float cooldown,
            int damage,
            int healing,
            int shield,
            float barrierDuration,
            bool raidTier,
            LightbringerSpellKind kind,
            string iconFile)
        {
            return new LightbringerSpellDefinition
            {
                Id = id,
                Name = name,
                Description = description,
                RequiredLevel = level,
                ManaCost = mana,
                CastTime = castTime,
                Cooldown = cooldown,
                TargetDamage = damage,
                TargetHealing = healing,
                ShieldAmount = shield,
                BarrierDuration = barrierDuration,
                RaidTier = raidTier,
                Kind = kind,
                IconFile = iconFile,
                DamageFamily = kind == LightbringerSpellKind.Damage
                    ? ResolveDamageFamily(id)
                    : LightbringerDamageFamily.None
            };
        }

        // Source-defined family membership stays stable across re-registration,
        // scroll acquisition and saved spellbook restoration. Native SpellLine
        // remains Direct_Damage, matching the existing damage-caster pipeline.
        private static LightbringerDamageFamily ResolveDamageFamily(string id)
        {
            switch (id)
            {
                case RadiantBoltId:
                case DawnBoltId:
                case RadiantLanceId:
                case CelestialLanceId:
                    return LightbringerDamageFamily.RadiantBolt;

                case JudgementId:
                case GreaterJudgementId:
                case JudgementOfLightId:
                case SunsVerdictId:
                    return LightbringerDamageFamily.Judgement;

                case SolarSparkId:
                case SolarFlareId:
                case SolarIgnitionId:
                case SolarWrathId:
                    return LightbringerDamageFamily.SolarSpark;

                default:
                    return LightbringerDamageFamily.None;
            }
        }

        private static Spell ResolveTemplateForDefinition(
            LightbringerSpellDefinition definition,
            Spell singleHealTemplate,
            Spell groupHealTemplate,
            Spell groupRevitalizeTemplate,
            Spell majorGroupHealTemplate,
            Spell damageTemplate)
        {
            switch (definition.Kind)
            {
                case LightbringerSpellKind.Damage:
                    return damageTemplate;

                case LightbringerSpellKind.GroupHealShield:
                    if (string.Equals(definition.Id, RadiantEmbraceId, StringComparison.OrdinalIgnoreCase))
                        return groupHealTemplate ?? singleHealTemplate;
                    if (string.Equals(definition.Id, RadiantCommunionId, StringComparison.OrdinalIgnoreCase))
                        return groupRevitalizeTemplate ?? groupHealTemplate ?? singleHealTemplate;
                    return majorGroupHealTemplate ?? groupRevitalizeTemplate ?? groupHealTemplate ?? singleHealTemplate;

                default:
                    return singleHealTemplate;
            }
        }

        private static void ApplyDefinition(
            Spell spell,
            LightbringerSpellDefinition definition,
            Class lightbringer)
        {
            spell.name = "FOTF - Lightbringer - " + definition.Name;
            spell.Id = definition.Id;
            spell.SpellName = definition.Name;
            // The spellbook reads SpellDesc. Native shield spells also show
            // metadata from their Shield line; describe the actual absorb here.
            string readableDescription = definition.Kind == LightbringerSpellKind.Shield
                ? "Absorbs up to " + definition.ShieldAmount.ToString("N0") +
                  " incoming damage for " + definition.BarrierDuration.ToString("0.#") +
                  " seconds (base; +1% capacity per 10 caster WIS). Prevents damage before it reaches HP; does not heal."
                : definition.Description;
            spell.SpellDesc = readableDescription;
            SetNativeTooltipDescription(spell, readableDescription);
            spell.RequiredLevel = definition.RequiredLevel;
            spell.ManaCost = definition.ManaCost;
            spell.Cooldown = definition.Cooldown;
            spell.SpellChargeTime = definition.CastTime * 60f;
            // The native Hardened Skin template is self-only. That flag is
            // spell metadata, not the Shield status effect itself. Let native
            // target validation and status-effect application process an ally
            // before falling back to a separate custom cast implementation.
            // Do NOT change Beneficial/Shield, native capacity or status effect.
            spell.SpellRange = 60f;
            spell.SelfOnly = false;
            spell.CanHitPlayers = true;
            if (definition.Kind != LightbringerSpellKind.Shield)
            {
                spell.SpellDurationInTicks = 0;
                spell.StatusEffectToApply = null;
            }
            // Preserve native Shield effect, resistance and damage flags.
            spell.CrowdControlSpell = false;
            spell.SimUsable = false;
            spell.SimsNeedHelpToLearn = false;
            spell.hideFlags = HideFlags.HideAndDontSave;
            spell.UsedBy = new List<Class> { lightbringer };
            // Preserve the native shield template's damage/resistance flags.
            if (definition.Kind != LightbringerSpellKind.Shield)
                spell.MyDamageType = GameData.DamageType.Magic;

            if (definition.Kind == LightbringerSpellKind.Damage)
            {
                spell.Type = Spell.SpellType.Damage;
                spell.Line = Spell.SpellLine.Direct_Damage;
                spell.TargetDamage = definition.TargetDamage;
                spell.TargetHealing = 0;
                spell.Aggro = definition.TargetDamage;
            }
            else if (definition.Kind == LightbringerSpellKind.Shield)
            {
                // Intentionally DO NOT change Type, Line, TargetDamage,
                // TargetHealing, Shield status effect or other native effect
                // fields: this is a clone of actual Hardened Skin, with its
                // absorption capacity adjusted by the validated native binding.
                // Beneficial + Shield is correct ONLY with that native clone.
            }
            else
            {
                // Hybrid/group healing retains the native heal component;
                // only the extra shield is applied through the absorb API.
                spell.Type = Spell.SpellType.Heal;
                spell.Line = Spell.SpellLine.Generic;
                spell.TargetDamage = 0;
                spell.TargetHealing = definition.TargetHealing;
                spell.Aggro = definition.TargetHealing;
            }

            EmbeddedSpriteLoader.TryAssignSpriteMember(
                spell,
                EmbeddedSpriteLoader.LoadByFileName(definition.IconFile));
        }

        private static bool _tooltipDescriptionLogged;
        private static void SetNativeTooltipDescription(Spell spell, string value)
        {
            // Native spellbook rows may ignore SpellDesc. The wiki's tooltip
            // schema exposes a special descriptor; set it only if an actual
            // compatible writable member exists on this Erenshor build.
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (string name in new[] { "SpecialDescriptor", "SpecialDesc", "SpecialDescription" })
            {
                try
                {
                    FieldInfo field = spell.GetType().GetField(name, flags);
                    if (field != null && field.FieldType == typeof(string))
                    {
                        field.SetValue(spell, value);
                        return;
                    }
                    PropertyInfo property = spell.GetType().GetProperty(name, flags);
                    if (property != null && property.CanWrite && property.PropertyType == typeof(string))
                    {
                        property.SetValue(spell, value, null);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.LogWarning("[LBSpellbook] Native descriptor " + name + " failed: " + ex.Message);
                }
            }
            if (!_tooltipDescriptionLogged)
            {
                _tooltipDescriptionLogged = true;
                Plugin.LogWarning("[LBSpellbook] No writable native SpecialDescriptor found. " +
                    "SpellDesc is populated, but the native spellbook may not render it.");
            }
        }

        private static Spell FindNativeOrCustom(SpellDB database, string id)
        {
            if (database == null || database.SpellDatabase == null || string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < database.SpellDatabase.Length; i++)
            {
                Spell spell = database.SpellDatabase[i];
                if (spell != null && string.Equals(spell.Id, id, StringComparison.OrdinalIgnoreCase))
                    return spell;
            }

            return null;
        }

        private static Spell FindSingleTargetHealTemplate(SpellDB database)
        {
            Spell spell = FindBySpellName(database, "Minor Healing");
            if (spell != null)
                return spell;
            spell = FindBySpellName(database, "Healing");
            if (spell != null)
                return spell;

            return FindFirst(database, candidate => candidate != null && candidate.Type == Spell.SpellType.Heal && candidate.TargetHealing > 0);
        }

        private static Spell FindGroupHealTemplate(SpellDB database, string spellName)
        {
            Spell spell = FindBySpellName(database, spellName);
            if (spell != null)
                return spell;
            return FindSingleTargetHealTemplate(database);
        }

        private static Spell FindDamageTemplate(SpellDB database)
        {
            Spell fallback = null;
            string[] preferred = { "smite", "bolt", "judg", "holy", "fire", "light" };

            if (database == null || database.SpellDatabase == null)
                return null;

            for (int i = 0; i < database.SpellDatabase.Length; i++)
            {
                Spell spell = database.SpellDatabase[i];
                if (spell == null || spell.Type != Spell.SpellType.Damage || spell.TargetDamage <= 0)
                    continue;
                if (!string.IsNullOrEmpty(spell.Id) && spell.Id.StartsWith("FOTF_", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (fallback == null)
                    fallback = spell;

                string name = (spell.SpellName ?? spell.name ?? string.Empty).ToLowerInvariant();
                for (int p = 0; p < preferred.Length; p++)
                    if (name.Contains(preferred[p]))
                        return spell;
            }

            return fallback;
        }

        private static Spell FindBySpellName(SpellDB database, string spellName)
        {
            if (database == null || database.SpellDatabase == null || string.IsNullOrEmpty(spellName))
                return null;

            for (int i = 0; i < database.SpellDatabase.Length; i++)
            {
                Spell spell = database.SpellDatabase[i];
                if (spell != null && string.Equals(spell.SpellName, spellName, StringComparison.OrdinalIgnoreCase))
                    return spell;
            }

            return null;
        }

        private static Spell FindFirst(SpellDB database, Func<Spell, bool> predicate)
        {
            if (database == null || database.SpellDatabase == null || predicate == null)
                return null;

            for (int i = 0; i < database.SpellDatabase.Length; i++)
            {
                Spell spell = database.SpellDatabase[i];
                if (predicate(spell))
                    return spell;
            }

            return null;
        }
    }
}
