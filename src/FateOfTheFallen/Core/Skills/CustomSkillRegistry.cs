using System;
using System.Collections.Generic;

namespace FateOfTheFallen
{
    /// <summary>
    /// Shared native SkillDB registration helper for custom FOTF class skills.
    /// Mirrors CustomSpellRegistry so future classes do not need to maintain
    /// their own database-array mutation code.
    /// </summary>
    internal static class CustomSkillRegistry
    {
        internal static void Register(
            SkillDB database,
            Skill skill)
        {
            if (database == null ||
                skill == null)
            {
                return;
            }

            Skill[] skills =
                database.SkillDatabase ??
                new Skill[0];

            for (int i = 0;
                 i < skills.Length;
                 i++)
            {
                Skill existing =
                    skills[i];

                if (existing == skill)
                {
                    return;
                }

                if (existing == null ||
                    string.IsNullOrEmpty(skill.Id) ||
                    !string.Equals(
                        existing.Id,
                        skill.Id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                skills[i] = skill;
                database.SkillDatabase = skills;
                return;
            }

            Skill[] expanded =
                new Skill[skills.Length + 1];

            Array.Copy(
                skills,
                expanded,
                skills.Length);

            expanded[skills.Length] =
                skill;

            database.SkillDatabase =
                expanded;
        }

        internal static void RegisterRange(
            SkillDB database,
            IEnumerable<Skill> skills)
        {
            if (database == null ||
                skills == null)
            {
                return;
            }

            foreach (Skill skill in skills)
            {
                Register(
                    database,
                    skill);
            }
        }
    }
}
