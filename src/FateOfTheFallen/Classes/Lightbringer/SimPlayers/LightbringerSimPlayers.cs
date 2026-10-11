using System;
using System.Collections.Generic;

namespace FateOfTheFallen
{
    /// <summary>Authored Lightbringer SimPlayer spell/Ascension progression.
    /// Runs AFTER the native Sim loader so custom class spells are not lost.
    /// Never edits native Erenshor SimPlayers.</summary>
    internal static class LightbringerSimPlayers
    {
        internal static bool IsAuthored(SimPlayer sim)
        {
            if (sim == null || sim.MyStats == null ||
                !LightbringerCatalog.IsLightbringerClass(sim.MyStats.CharacterClass)) return false;
            FOTFSimPlayerDefinition definition;
            return FOTFSimPlayerRoster.TryGetDefinitionForSim(sim, out definition) &&
                definition != null && string.Equals(definition.ClassName,
                    FOTFSimPlayerRoster.LightbringerClassName, StringComparison.OrdinalIgnoreCase);
        }

        internal static void ConfigureSpellsForSimPlayers()
        {
            Class lb = LightbringerCatalog.TryEnsureClass();
            if (lb == null) return;
            foreach (LightbringerSpellDefinition definition in LightbringerSpellCatalog.All)
            {
                Spell spell = definition != null ? definition.RuntimeSpell : null;
                if (spell == null) continue;
                if (spell.UsedBy == null) spell.UsedBy = new List<Class>();
                if (!spell.UsedBy.Contains(lb)) spell.UsedBy.Add(lb);
                spell.SimUsable = true;
                spell.SimsNeedHelpToLearn = false;
            }
            // Native heal spells keep their own native Sim flags. They are
            // selected from the authored Sim's restored spellbook below.
        }

        internal static void EnsureSpellbook(SimPlayer sim)
        {
            if (!IsAuthored(sim) || sim.Spellbook == null) return;
            ConfigureSpellsForSimPlayers();
            int restored = 0;
            foreach (LightbringerSpellDefinition definition in LightbringerSpellCatalog.All)
            {
                if (definition == null || definition.RequiredLevel > sim.MyStats.Level) continue;
                Spell spell = definition.RuntimeSpell;
                if (spell == null && GameData.SpellDatabase != null)
                    spell = GameData.SpellDatabase.GetSpellByID(definition.Id);
                if (AddSpellIfMissing(sim, spell)) restored++;
            }
            if (GameData.SpellDatabase != null && GameData.SpellDatabase.SpellDatabase != null)
            {
                foreach (Spell spell in GameData.SpellDatabase.SpellDatabase)
                {
                    // Erenshor native level gates must also be respected;
                    // spell progression is not inferred from arbitrary drops.
                    if (LightbringerNativeHealing.IsApproved(spell) &&
                        spell.RequiredLevel <= sim.MyStats.Level &&
                        AddSpellIfMissing(sim, spell)) restored++;
                }
            }
            if (restored > 0)
                Plugin.LogInfo("[LBSim] Restored " + restored + " level-appropriate spells for " +
                    sim.MyStats.MyName + " (level " + sim.MyStats.Level + ").");
            EnsureSkills(sim);
        }

        private static bool AddSpellIfMissing(SimPlayer sim, Spell spell)
        {
            if (spell == null || sim.Spellbook == null) return false;
            foreach (SimPlayerSpellSlot slot in sim.Spellbook)
                if (slot != null && slot.spell != null &&
                    string.Equals(slot.spell.Id, spell.Id, StringComparison.OrdinalIgnoreCase))
                    return false;
            sim.Spellbook.Add(new SimPlayerSpellSlot(spell, false));
            return true;
        }

        // Native all-class skills are granted by their actual native eligibility,
        // not by inheriting every Druid skill from our save-compatible template.
        // Also respect learnable skillbooks whose item allows all six classes.
        private static ItemDatabase _cachedItemDB;
        private static float _nextUniversalScan;
        private static HashSet<string> _universalSkills;

        private static HashSet<string> GetUniversalSkillIds()
        {
            if (_universalSkills != null && GameData.ItemDB == _cachedItemDB &&
                UnityEngine.Time.unscaledTime < _nextUniversalScan) return _universalSkills;
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (GameData.ItemDB == null) return ids;
            if (GameData.ItemDB.ItemDB != null)
                foreach (Item item in GameData.ItemDB.ItemDB)
                    if (LightbringerNativeHealing.IsUniversalSkillbook(item) &&
                        item.TeachSkill != null && !string.IsNullOrEmpty(item.TeachSkill.Id))
                        ids.Add(item.TeachSkill.Id);
            if (GameData.ItemDB.ItemDBList != null)
                foreach (Item item in GameData.ItemDB.ItemDBList)
                    if (LightbringerNativeHealing.IsUniversalSkillbook(item) &&
                        item.TeachSkill != null && !string.IsNullOrEmpty(item.TeachSkill.Id))
                        ids.Add(item.TeachSkill.Id);
            if (GameData.ItemDB.GenericItems != null)
                foreach (Item item in GameData.ItemDB.GenericItems)
                    if (LightbringerNativeHealing.IsUniversalSkillbook(item) &&
                        item.TeachSkill != null && !string.IsNullOrEmpty(item.TeachSkill.Id))
                        ids.Add(item.TeachSkill.Id);
            _cachedItemDB = GameData.ItemDB;
            _universalSkills = ids;
            _nextUniversalScan = UnityEngine.Time.unscaledTime + 30f;
            return ids;
        }

        private static int NativeLevel(Skill skill, string name)
        {
            if (skill == null) return 0;
            var field = skill.GetType().GetField(name,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(int))
                return (int)field.GetValue(skill);
            var property = skill.GetType().GetProperty(name,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (property != null && property.CanRead && property.PropertyType == typeof(int))
                return (int)property.GetValue(skill, null);
            return 0;
        }

        private static bool IsNativeUniversalSkill(Skill skill, int simLevel,
            HashSet<string> universalBookIds)
        {
            if (skill == null || string.IsNullOrEmpty(skill.Id) ||
                skill.Id.StartsWith("FOTF_", StringComparison.OrdinalIgnoreCase)) return false;
            int required = NativeLevel(skill, "DruidRequiredLevel");
            // A native all-class skillbook is authoritative even when the
            // native skill uses a level-zero (starting-skill) requirement.
            if (universalBookIds.Contains(skill.Id))
                return required <= 0 || simLevel >= required;
            // Without a verified universal book, require explicit eligibility
            // for every native class; do not inherit Druid-only abilities.
            if (required <= 0 || simLevel < required) return false;
            foreach (string field in new[] { "ArcanistRequiredLevel", "DuelistRequiredLevel",
                "PaladinRequiredLevel", "ReaverRequiredLevel", "StormcallerRequiredLevel" })
            {
                int level = NativeLevel(skill, field);
                if (level <= 0 || level > simLevel) return false;
            }
            return true;
        }

        private static bool AddKnownSkill(SimPlayer sim, UseSkill skills, Skill skill)
        {
            if (sim == null || skill == null || string.IsNullOrEmpty(skill.Id)) return false;
            bool added = false;
            if (skills != null && skills.KnownSkills != null)
            {
                bool found = false;
                foreach (Skill known in skills.KnownSkills)
                    if (known != null && string.Equals(known.Id, skill.Id,
                        StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                if (!found) { skills.KnownSkills.Add(skill); added = true; }
            }
            // Native combat skills should be usable by native Sim combat AI.
            // The three FOTF passives deliberately have no cast action.
            if (!LightbringerSkillCatalog.IsLightbringerSkill(skill))
            {
                NPC npc = sim.GetThisNPC();
                if (npc != null && npc.MyAttackSkills != null &&
                    (skill.TypeOfSkill == Skill.SkillType.Attack ||
                     skill.TypeOfSkill == Skill.SkillType.Ranged) &&
                    !npc.MyAttackSkills.Contains(skill))
                    npc.MyAttackSkills.Add(skill);
                if (skill.TypeOfSkill == Skill.SkillType.Utility &&
                    skill.StanceToUse != null && sim.KnownStances != null &&
                    !sim.KnownStances.Contains(skill.StanceToUse))
                    sim.KnownStances.Add(skill.StanceToUse);
            }
            if (sim.Skillbook != null)
            {
                bool found = false;
                foreach (SimPlayerSkillSlot slot in sim.Skillbook)
                    if (slot != null && slot.skill != null &&
                        string.Equals(slot.skill.Id, skill.Id, StringComparison.OrdinalIgnoreCase))
                    { found = true; break; }
                if (!found) { sim.Skillbook.Add(new SimPlayerSkillSlot(skill)); added = true; }
            }
            return added;
        }

        internal static void EnsureSkills(SimPlayer sim)
        {
            if (!IsAuthored(sim)) return;
            if (sim.Skillbook == null) sim.Skillbook = new List<SimPlayerSkillSlot>();
            UseSkill skills = sim.GetComponent<UseSkill>();
            UseSkill characterSkills = sim.MyStats.Myself != null
                ? sim.MyStats.Myself.MySkills : null;
            if (skills == null) skills = characterSkills;

            // A SkillDB may register after the native Sim loader; safely retry
            // in the later reconciliation pass rather than losing the skills.
            LightbringerSkillCatalog.Register(GameData.SkillDatabase);
            int lbAdded = 0, sharedAdded = 0;
            foreach (LightbringerSkillDefinition definition in LightbringerSkillCatalog.All)
            {
                if (definition == null || definition.RequiredLevel > sim.MyStats.Level ||
                    definition.RuntimeSkill == null) continue;
                if (AddKnownSkill(sim, skills, definition.RuntimeSkill)) lbAdded++;
                if (characterSkills != null && characterSkills != skills &&
                    characterSkills.KnownSkills != null)
                    AddKnownSkill(sim, characterSkills, definition.RuntimeSkill);
            }
            if (GameData.SkillDatabase != null && GameData.SkillDatabase.SkillDatabase != null)
            {
                HashSet<string> universalIds = GetUniversalSkillIds();
                foreach (Skill skill in GameData.SkillDatabase.SkillDatabase)
                {
                    if (!IsNativeUniversalSkill(skill, sim.MyStats.Level, universalIds)) continue;
                    if (AddKnownSkill(sim, skills, skill)) sharedAdded++;
                    if (characterSkills != null && characterSkills != skills &&
                        characterSkills.KnownSkills != null)
                        AddKnownSkill(sim, characterSkills, skill);
                }
            }
            if (lbAdded > 0 || sharedAdded > 0)
                Plugin.LogInfo("[LBSimSkills] " + sim.MyStats.MyName + " level=" +
                    sim.MyStats.Level + " granted=" + lbAdded + " Lightbringer + " +
                    sharedAdded + " all-class skills.");

            EnsureAscensions(sim, skills, characterSkills);
        }

        /// <summary>
        /// Native Sim loading may create a UseSkill on the Sim root and a
        /// different UseSkill on Stats.Myself. Grant ranks to BOTH, because
        /// native skill inspection and FOTF's actual caster calculations can
        /// resolve different instances. This is idempotent and retried after
        /// the native LoadSimSkills/LoadSimSpells sequence.
        /// </summary>
        private static void EnsureAscensions(SimPlayer sim, UseSkill simSkills,
            UseSkill characterSkills)
        {
            if (sim == null || sim.MyStats == null || !LightbringerAscensions.Register())
                return;
            int rank = sim.MyStats.Level >= 35 ? 3 : sim.MyStats.Level >= 27 ? 2 :
                sim.MyStats.Level >= 18 ? 1 : 0;
            if (rank <= 0) return;

            // Keep the character controller authoritative for active spells.
            UseSkill rootSkills = sim.GetComponent<UseSkill>();
            EnsureAscensionRanks(sim, characterSkills, rank);
            if (simSkills != characterSkills)
                EnsureAscensionRanks(sim, simSkills, rank);
            if (rootSkills != characterSkills && rootSkills != simSkills)
                EnsureAscensionRanks(sim, rootSkills, rank);
        }

        private static void EnsureAscensionRanks(SimPlayer sim, UseSkill skills, int rank)
        {
            if (skills == null) return;
            if (skills.MyAscensions == null)
                skills.MyAscensions = new List<AscensionSkillEntry>();
            int inserted = 0;
            foreach (Ascension ascension in LightbringerAscensions.All)
            {
                if (ascension == null) continue;
                AscensionSkillEntry existing = null;
                foreach (AscensionSkillEntry entry in skills.MyAscensions)
                    if (entry != null && string.Equals(entry.id, ascension.Id,
                        StringComparison.Ordinal)) { existing = entry; break; }
                if (existing == null)
                {
                    skills.MyAscensions.Add(new AscensionSkillEntry(ascension.Id, rank));
                    inserted++;
                }
                else if (existing.level < rank)
                {
                    existing.level = rank;
                    inserted++;
                }
            }
            if (inserted > 0)
                Plugin.LogInfo("[LBSimAscensions] " + sim.MyStats.MyName +
                    " assigned=" + inserted + " rank=" + rank +
                    " controller=" + skills.gameObject.name);
        }
    }
}
