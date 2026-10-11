using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    internal sealed class LightbringerSkillDefinition
    {
        internal string Id;
        internal string Name;
        internal string Description;
        internal int RequiredLevel;
        internal string IconFile;
        internal Skill RuntimeSkill;
    }

    /// <summary>
    /// Passive, learnable SKILLS, not spells. They have no direct cast action;
    /// learning their native-icon skillbooks changes ward calculations.
    /// </summary>
    internal static class LightbringerSkillCatalog
    {
        internal const string RadiantFortitudeId = "FOTF_LB_SKILL_RADIANT_FORTITUDE";
        internal const string EnduringLightId = "FOTF_LB_SKILL_ENDURING_LIGHT";
        internal const string SanctifiedCommunionId = "FOTF_LB_SKILL_SANCTIFIED_COMMUNION";
        internal const string GuidingLightId = "FOTF_LB_SKILL_GUIDING_LIGHT";

        private static readonly List<LightbringerSkillDefinition> Entries = new List<LightbringerSkillDefinition>
        {
            new LightbringerSkillDefinition { Id = RadiantFortitudeId, Name = "Radiant Fortitude",
                RequiredLevel = 8, IconFile = "FOTF_LB_Skill_RadiantFortitude.png", Description = "Passive: your Lightbringer absorb shields are 10% stronger." },
            new LightbringerSkillDefinition { Id = EnduringLightId, Name = "Enduring Light",
                RequiredLevel = 16, IconFile = "FOTF_LB_Skill_EnduringLight.png", Description = "Passive: your Lightbringer absorb shields last 3 seconds longer." },
            new LightbringerSkillDefinition { Id = SanctifiedCommunionId, Name = "Sanctified Communion",
                RequiredLevel = 28, IconFile = "FOTF_LB_Skill_SanctifiedCommunion.png", Description = "Passive: your group Lightbringer shields absorb 15% more damage." },
            // Append rather than insert: the existing skillbooks must retain
            // item IDs 1925, 1926 and 1927 across saved characters.
            new LightbringerSkillDefinition { Id = GuidingLightId, Name = "Guiding Light",
                RequiredLevel = 1, IconFile = "FOTF_LB_Skill_GuidingLight.png", Description = "Passive: your direct Lightbringer and approved native healing spells restore 5% more health." }
        };

        internal static IList<LightbringerSkillDefinition> All { get { return Entries; } }

        internal static void Register(SkillDB db)
        {
            Skill template = null;
            if (db != null && db.SkillDatabase != null)
            {
                foreach (Skill native in db.SkillDatabase)
                {
                    if (native != null && !string.IsNullOrEmpty(native.Id) &&
                        !native.Id.StartsWith("FOTF_", StringComparison.OrdinalIgnoreCase))
                    { template = native; break; }
                }
            }
            foreach (LightbringerSkillDefinition definition in Entries)
            {
                if (definition.RuntimeSkill == null)
                {
                    Skill skill = template != null ? UnityEngine.Object.Instantiate(template) : ScriptableObject.CreateInstance<Skill>();
                    skill.name = "FOTF - Lightbringer Skill - " + definition.Name;
                    skill.Id = definition.Id;
                    skill.SkillName = definition.Name;
                    skill.hideFlags = HideFlags.HideAndDontSave;
                    foreach (string field in new[] { "ArcanistRequiredLevel", "DuelistRequiredLevel", "DruidRequiredLevel",
                        "PaladinRequiredLevel", "StormcallerRequiredLevel", "ReaverRequiredLevel" })
                        TrySetInt(skill, field, 0);
                    TrySetText(skill, "SkillDesc", definition.Description);
                    TrySetText(skill, "Description", definition.Description);
                    definition.RuntimeSkill = skill;
                }
                // The SKILL page reads its Sprite from the learned Skill,
                // not the teaching item. Reassign even for cached skills.
                Sprite icon = EmbeddedSpriteLoader.LoadByFileName(definition.IconFile);
                if (icon != null)
                    EmbeddedSpriteLoader.TryAssignSpriteMember(definition.RuntimeSkill, icon);
                else
                    Plugin.LogWarning("[LBSkillIcon] Embedded icon unavailable: " + definition.IconFile);
                if (db != null) CustomSkillRegistry.Register(db, definition.RuntimeSkill);
            }
        }

        private static void TrySetInt(object obj, string name, int value)
        {
            FieldInfo f = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(int)) f.SetValue(obj, value);
            PropertyInfo p = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite && p.PropertyType == typeof(int)) p.SetValue(obj, value, null);
        }

        private static void TrySetText(object obj, string name, string value)
        {
            FieldInfo f = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(string)) f.SetValue(obj, value);
            PropertyInfo p = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite && p.PropertyType == typeof(string)) p.SetValue(obj, value, null);
        }

        internal static bool TryGet(string id, out LightbringerSkillDefinition def)
        {
            foreach (var entry in Entries)
                if (string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase))
                { def = entry; return true; }
            def = null;
            return false;
        }

        internal static Skill GetById(string id)
        {
            LightbringerSkillDefinition def;
            return TryGet(id, out def) ? def.RuntimeSkill : null;
        }

        internal static bool IsLightbringerSkill(Skill skill)
        {
            LightbringerSkillDefinition def;
            return skill != null && TryGet(skill.Id, out def);
        }

        // Caster-aware check: authored Lightbringer Sims have their own learned
        // skillbooks and must not inherit the local player's passive bonuses.
        internal static bool HasCasterSkill(Stats caster, string id)
        {
            if (caster == null || !LightbringerCatalog.IsLightbringerClass(caster.CharacterClass))
                return false;
            if (caster.Myself != null)
            {
                if (caster.Myself.MySkills != null &&
                    Contains(caster.Myself.MySkills.KnownSkills, id)) return true;
                SimPlayer sim = caster.Myself.GetComponent<SimPlayer>();
                if (sim == null) sim = caster.Myself.GetComponentInParent<SimPlayer>();
                if (sim != null)
                {
                    UseSkill skills = sim.GetComponent<UseSkill>();
                    if (skills != null && Contains(skills.KnownSkills, id)) return true;
                }
            }
            // Player load can restore to GameManager skills before the
            // Character's UseSkill instance is rebuilt.
            if (GameData.PlayerStats == caster && GameData.GM != null &&
                GameData.GM.PlayerSkills != null)
                return Contains(GameData.GM.PlayerSkills.KnownSkills, id);
            return false;
        }

        internal static bool HasCasterSkill(string id)
        {
            if (!LightbringerCatalog.IsCurrentPlayerLightbringer())
                return false;
            if (GameData.PlayerStats != null && GameData.PlayerStats.Myself != null &&
                GameData.PlayerStats.Myself.MySkills != null &&
                Contains(GameData.PlayerStats.Myself.MySkills.KnownSkills, id))
                return true;
            return GameData.GM != null && GameData.GM.PlayerSkills != null &&
                Contains(GameData.GM.PlayerSkills.KnownSkills, id);
        }

        private static bool Contains(IList<Skill> skills, string id)
        {
            if (skills == null) return false;
            foreach (Skill known in skills)
                if (known != null && string.Equals(known.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
