using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    internal sealed class EmberweaverSkillDefinition
    {
        internal string Id;
        internal string Name;
        internal string Description;
        internal int RequiredLevel;
        internal string IconFile;
        internal Skill RuntimeSkill;
    }

    /// <summary>
    /// Emberweaver's class mechanics are real learned Erenshor Skills.
    /// None of these mechanics are innate to the Class object: the runtime
    /// only enables Heat, Flashfire or Wildfire when the corresponding Skill
    /// exists in UseSkill.KnownSkills.
    /// </summary>
    internal static class EmberweaverSkillCatalog
    {
        internal const string HeatId =
            "FOTF_EW_SKILL_HEAT";

        internal const string FlashfireId =
            "FOTF_EW_SKILL_FLASHFIRE";

        internal const string WildfireId =
            "FOTF_EW_SKILL_WILDFIRE";

        /*
         * Passive-skill icons intentionally use artwork that is NOT used by
         * any Emberweaver spell:
         *
         *   Heat      -> FireMage_12.png
         *   Flashfire -> FireMage_22.png
         *   Wildfire  -> FireMage_5.png
         *
         * The physical skillbook ITEMS still retain Erenshor's native
         * skillbook icon. These icons are for the learned Skill entries in
         * the player's skill book/hotbar.
         */

        private static readonly List<EmberweaverSkillDefinition>
            Definitions =
                new List<EmberweaverSkillDefinition>
                {
                    Def(
                        HeatId,
                        "Heat",
                        "Direct Emberweaver fire spells build Heat. Each stack increases Emberweaver damage by 3%, up to 10 stacks. Heat begins to decay after 3 seconds without a direct hit.",
                        1,
                        "FireMage_12.png"),

                    Def(
                        FlashfireId,
                        "Flashfire",
                        "Direct Emberweaver spells have an 8% base critical chance. A critical hit causes your next Pyroclasm-family spell to cast instantly, resets its cooldown, and makes it cost no mana.",
                        4,
                        "FireMage_22.png"),

                    Def(
                        WildfireId,
                        "Wildfire",
                        "Converts part of each direct Emberweaver fire hit into a rolling single-target Wildfire burn instead of dealing that portion immediately. Wildfire releases 10% of its bank every 1.5 seconds while fed, then burns out after 6 seconds without a new deposit.",
                        8,
                        "FireMage_5.png")
                };

        internal static IReadOnlyList<EmberweaverSkillDefinition> All
        {
            get
            {
                return Definitions;
            }
        }

        internal static void Register(
            SkillDB database)
        {
            if (database == null)
            {
                return;
            }

            Skill template =
                FindNativeSkillTemplate(
                    database);

            EnsureCreated(
                template);

            for (int i = 0;
                 i < Definitions.Count;
                 i++)
            {
                Skill skill =
                    Definitions[i].RuntimeSkill;

                if (skill == null)
                {
                    continue;
                }

                CustomSkillRegistry.Register(
                    database,
                    skill);
            }
        }

        internal static void EnsureCreated()
        {
            EnsureCreated(
                FindNativeSkillTemplate(
                    GameData.SkillDatabase));
        }

        private static void EnsureCreated(
            Skill template)
        {
            for (int i = 0;
                 i < Definitions.Count;
                 i++)
            {
                EmberweaverSkillDefinition definition =
                    Definitions[i];

                if (definition.RuntimeSkill != null)
                {
                    continue;
                }

                Skill skill =
                    template != null
                        ? UnityEngine.Object.Instantiate(
                            template)
                        : ScriptableObject.CreateInstance<Skill>();

                skill.name =
                    "FOTF - Emberweaver Skill - " +
                    definition.Name;

                skill.Id =
                    definition.Id;

                skill.SkillName =
                    definition.Name;

                skill.hideFlags =
                    HideFlags.HideAndDontSave;

                /*
                 * These are Emberweaver-only passive skills. Do not make
                 * them Arcanist skills just because Arcanist is used as the
                 * class/equipment compatibility source.
                 */
                ClearNativeClassRequiredLevels(
                    skill);

                TrySetStringMember(
                    skill,
                    "SkillDesc",
                    definition.Description);

                TrySetStringMember(
                    skill,
                    "Description",
                    definition.Description);

                EmbeddedSpriteLoader.TryAssignSpriteMember(
                    skill,
                    EmbeddedSpriteLoader.LoadByFileName(
                        definition.IconFile));

                definition.RuntimeSkill =
                    skill;
            }
        }

        internal static bool TryGetDefinition(
            string id,
            out EmberweaverSkillDefinition definition)
        {
            EnsureCreated();

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

        internal static bool TryGetDefinition(
            Skill skill,
            out EmberweaverSkillDefinition definition)
        {
            definition = null;

            return
                skill != null &&
                TryGetDefinition(
                    skill.Id,
                    out definition);
        }

        internal static Skill GetById(
            string id)
        {
            EmberweaverSkillDefinition definition;

            return TryGetDefinition(
                    id,
                    out definition)
                ? definition.RuntimeSkill
                : null;
        }

        internal static bool HasPlayerSkill(
            string id)
        {
            if (!EmberweaverCatalog.IsCurrentPlayerEmberweaver())
            {
                return false;
            }

            EmberweaverSkillDefinition definition;

            if (!TryGetDefinition(
                    id,
                    out definition) ||
                definition.RuntimeSkill == null)
            {
                return false;
            }

            UseSkill skills =
                null;

            if (GameData.PlayerStats != null &&
                GameData.PlayerStats.Myself != null)
            {
                skills =
                    GameData.PlayerStats.Myself.MySkills;
            }

            if (skills == null &&
                GameData.PlayerControl != null)
            {
                skills =
                    GameData.PlayerControl.GetComponent<UseSkill>();
            }

            if (skills == null ||
                skills.KnownSkills == null)
            {
                return false;
            }

            for (int i = 0;
                 i < skills.KnownSkills.Count;
                 i++)
            {
                Skill known =
                    skills.KnownSkills[i];

                if (known == definition.RuntimeSkill ||
                    (known != null &&
                     string.Equals(
                         known.Id,
                         id,
                         StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private static EmberweaverSkillDefinition Def(
            string id,
            string name,
            string description,
            int requiredLevel,
            string iconFile)
        {
            return new EmberweaverSkillDefinition
            {
                Id = id,
                Name = name,
                Description = description,
                RequiredLevel = requiredLevel,
                IconFile = iconFile
            };
        }

        private static Skill FindNativeSkillTemplate(
            SkillDB database)
        {
            if (database != null &&
                database.SkillDatabase != null)
            {
                Skill fallback =
                    null;

                for (int i = 0;
                     i < database.SkillDatabase.Length;
                     i++)
                {
                    Skill skill =
                        database.SkillDatabase[i];

                    if (skill == null ||
                        string.IsNullOrEmpty(skill.Id) ||
                        skill.Id.StartsWith(
                            "FOTF_",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (fallback == null)
                    {
                        fallback =
                            skill;
                    }

                    if (string.Equals(
                            skill.SkillName,
                            "Arcane Proficiency",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return skill;
                    }
                }

                if (fallback != null)
                {
                    return fallback;
                }
            }

            Skill[] skills =
                Resources.FindObjectsOfTypeAll<Skill>();

            if (skills == null)
            {
                return null;
            }

            for (int i = 0;
                 i < skills.Length;
                 i++)
            {
                Skill skill =
                    skills[i];

                if (skill != null &&
                    !string.IsNullOrEmpty(skill.Id) &&
                    !skill.Id.StartsWith(
                        "FOTF_",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return skill;
                }
            }

            return null;
        }

        private static void ClearNativeClassRequiredLevels(
            Skill skill)
        {
            string[] names =
            {
                "ArcanistRequiredLevel",
                "DuelistRequiredLevel",
                "DruidRequiredLevel",
                "PaladinRequiredLevel",
                "StormcallerRequiredLevel",
                "ReaverRequiredLevel"
            };

            for (int i = 0;
                 i < names.Length;
                 i++)
            {
                TrySetIntMember(
                    skill,
                    names[i],
                    0);
            }
        }

        private static void TrySetIntMember(
            object instance,
            string name,
            int value)
        {
            if (instance == null)
            {
                return;
            }

            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            FieldInfo field =
                instance.GetType().GetField(
                    name,
                    flags);

            if (field != null &&
                field.FieldType == typeof(int))
            {
                try
                {
                    field.SetValue(
                        instance,
                        value);
                }
                catch
                {
                }
            }

            PropertyInfo property =
                instance.GetType().GetProperty(
                    name,
                    flags);

            if (property != null &&
                property.CanWrite &&
                property.PropertyType == typeof(int))
            {
                try
                {
                    property.SetValue(
                        instance,
                        value,
                        null);
                }
                catch
                {
                }
            }
        }

        private static void TrySetStringMember(
            object instance,
            string name,
            string value)
        {
            if (instance == null)
            {
                return;
            }

            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            FieldInfo field =
                instance.GetType().GetField(
                    name,
                    flags);

            if (field != null &&
                field.FieldType == typeof(string))
            {
                try
                {
                    field.SetValue(
                        instance,
                        value);
                }
                catch
                {
                }
            }

            PropertyInfo property =
                instance.GetType().GetProperty(
                    name,
                    flags);

            if (property != null &&
                property.CanWrite &&
                property.PropertyType == typeof(string))
            {
                try
                {
                    property.SetValue(
                        instance,
                        value,
                        null);
                }
                catch
                {
                }
            }
        }
    }
}
