using HarmonyLib;
using System;
using System.Collections.Generic;

namespace FateOfTheFallen
{
    /// <summary>
    /// Native class loading does not know about Emberweaver. Restore the
    /// saved custom spells/skills explicitly. Emberweaver still cannot
    /// regain native Arcanist spells through the Arcanist-derived equipment
    /// compatibility path, while approved shared skills are restored normally.
    /// </summary>
    [HarmonyPatch(
        typeof(CharSelectManager),
        "LoadSpellsAndSkills")]
    internal static class Patch_EmberweaverLoadSpellsAndSkills
    {
        private static bool Prefix()
        {
            if (GameData.CurrentCharacterSlot == null ||
                !string.Equals(
                    GameData.CurrentCharacterSlot.CharClass,
                    EmberweaverConstants.DisplayName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                EmberweaverModule.EnsureRuntimeContentReady(
                    "LoadSpellsAndSkills");

                if (GameData.GM == null ||
                    GameData.GM.PlayerSpells == null ||
                    GameData.GM.PlayerSpells.KnownSpells == null ||
                    GameData.GM.PlayerSkills == null ||
                    GameData.GM.PlayerSkills.KnownSkills == null ||
                    GameData.GM.PlayerSkills.MyAscensions == null)
                {
                    return true;
                }

                RestoreSpells();
                RestoreSkills();
                RestoreAscensions();

                GameData.GM.PlayerSkills.AscensionPoints =
                    GameData.CurrentCharacterSlot.AscensionPointsUnspent;

                return false;
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Emberweaver: spell/skill restoration failed",
                    exception);

                return true;
            }
        }

        private static void RestoreSpells()
        {
            if (GameData.CurrentCharacterSlot.CharacterSpells == null)
            {
                return;
            }

            foreach (
                string spellId
                in GameData.CurrentCharacterSlot.CharacterSpells)
            {
                if (string.IsNullOrEmpty(spellId))
                {
                    continue;
                }

                EmberweaverSpellDefinition definition;

                /*
                 * Emberweaver has its own spellbook. Do not restore Arcanist
                 * spells simply because its equipment/scaling derives from
                 * the Arcanist archetype.
                 */
                if (!EmberweaverSpellCatalog.TryGetDefinition(
                        spellId,
                        out definition))
                {
                    continue;
                }

                Spell spell =
                    null;

                if (GameData.SpellDatabase != null)
                {
                    spell =
                        GameData.SpellDatabase.GetSpellByID(
                            spellId);
                }

                if (spell == null)
                {
                    spell =
                        definition.RuntimeSpell;
                }

                if (spell == null ||
                    ContainsSpell(
                        GameData.GM.PlayerSpells.KnownSpells,
                        spell.Id))
                {
                    continue;
                }

                GameData.GM.PlayerSpells.KnownSpells.Add(
                    spell);
            }
        }

        private static void RestoreSkills()
        {
            if (GameData.CurrentCharacterSlot.CharacterSkills == null)
            {
                return;
            }

            foreach (
                string skillId
                in GameData.CurrentCharacterSlot.CharacterSkills)
            {
                if (string.IsNullOrEmpty(skillId))
                {
                    continue;
                }

                Skill skill =
                    null;

                if (GameData.SkillDatabase != null)
                {
                    skill =
                        GameData.SkillDatabase.GetSkillByID(
                            skillId);
                }

                if (skill == null)
                {
                    skill =
                        EmberweaverSkillCatalog.GetById(
                            skillId);
                }

                if (skill == null ||
                    ContainsSkill(
                        GameData.GM.PlayerSkills.KnownSkills,
                        skill.Id))
                {
                    continue;
                }

                GameData.GM.PlayerSkills.KnownSkills.Add(
                    skill);
            }
        }

        private static void RestoreAscensions()
        {
            GameData.GM.PlayerSkills.MyAscensions.Clear();

            if (GameData.CurrentCharacterSlot.Ascensions == null)
            {
                return;
            }

            Dictionary<string, int> ranks =
                new Dictionary<string, int>(
                    StringComparer.Ordinal);

            foreach (
                AscensionSkillEntry entry
                in GameData.CurrentCharacterSlot.Ascensions)
            {
                if (entry == null ||
                    string.IsNullOrEmpty(entry.id))
                {
                    continue;
                }

                int existing;

                if (ranks.TryGetValue(
                        entry.id,
                        out existing))
                {
                    if (entry.level > existing)
                    {
                        ranks[entry.id] =
                            entry.level;
                    }

                    continue;
                }

                ranks.Add(
                    entry.id,
                    entry.level);
            }

            foreach (
                KeyValuePair<string, int> pair
                in ranks)
            {
                GameData.GM.PlayerSkills.MyAscensions.Add(
                    new AscensionSkillEntry(
                        pair.Key,
                        pair.Value));
            }
        }

        private static bool ContainsSpell(
            IList<Spell> spells,
            string id)
        {
            if (spells == null ||
                string.IsNullOrEmpty(id))
            {
                return false;
            }

            for (int i = 0;
                 i < spells.Count;
                 i++)
            {
                Spell spell =
                    spells[i];

                if (spell != null &&
                    string.Equals(
                        spell.Id,
                        id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsSkill(
            IList<Skill> skills,
            string id)
        {
            if (skills == null ||
                string.IsNullOrEmpty(id))
            {
                return false;
            }

            for (int i = 0;
                 i < skills.Count;
                 i++)
            {
                Skill skill =
                    skills[i];

                if (skill != null &&
                    string.Equals(
                        skill.Id,
                        id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
