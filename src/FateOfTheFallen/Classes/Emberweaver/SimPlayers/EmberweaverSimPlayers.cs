using System;
using System.Collections.Generic;
using System.Linq;

namespace FateOfTheFallen
{
    /// <summary>
    /// Emberweaver-specific SimPlayer content bridge.
    ///
    /// This is deliberately not the combat brain. It only makes the normal
    /// Emberweaver spell/skill progression available to authored FOTF Sims so
    /// the generic native SimPlayer lifecycle can load and display the class.
    /// Heat/Flashfire/Wildfire decision-making will live in the later
    /// Emberweaver Sim combat controller.
    /// </summary>
    internal static class EmberweaverSimPlayers
    {
        private static readonly HashSet<string> SharedSkillNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                "Arcane Proficiency",
                "Arcane Recovery",
                "Multifocus"
            };

        internal static void ConfigureSpellsForSimPlayers()
        {
            Class emberweaver =
                EmberweaverCatalog.TryEnsureClass();

            if (emberweaver == null)
            {
                return;
            }

            IReadOnlyList<EmberweaverSpellDefinition> definitions =
                EmberweaverSpellCatalog.All;

            for (int i = 0;
                 i < definitions.Count;
                 i++)
            {
                EmberweaverSpellDefinition definition =
                    definitions[i];

                if (definition == null ||
                    definition.RuntimeSpell == null)
                {
                    continue;
                }

                Spell spell =
                    definition.RuntimeSpell;

                if (spell.UsedBy == null)
                {
                    spell.UsedBy =
                        new List<Class>();
                }

                if (!spell.UsedBy.Contains(
                        emberweaver))
                {
                    spell.UsedBy.Add(
                        emberweaver);
                }

                spell.SimUsable =
                    true;

                spell.SimsNeedHelpToLearn =
                    false;
            }
        }

        /// <summary>
        /// Reconcile an authored Emberweaver spellbook after the native loader.
        /// A repeated SpellCatalog.Register() used to reset SimUsable to false,
        /// which left these Sims with a valid custom class but no spells.
        /// This pass is idempotent and does not modify native SimPlayers.
        /// </summary>
        internal static void EnsureSpellbook(
            SimPlayer sim)
        {
            if (sim == null ||
                sim.MyStats == null ||
                sim.Spellbook == null ||
                !EmberweaverCatalog.IsEmberweaverClass(
                    sim.MyStats.CharacterClass))
            {
                return;
            }

            FOTFSimPlayerDefinition authored;
            if (!FOTFSimPlayerRoster.TryGetDefinitionForSim(
                    sim,
                    out authored) ||
                authored == null ||
                !string.Equals(
                    authored.ClassName,
                    FOTFSimPlayerRoster.EmberweaverClassName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ConfigureSpellsForSimPlayers();

            IReadOnlyList<EmberweaverSpellDefinition> definitions =
                EmberweaverSpellCatalog.All;

            int restored = 0;
            for (int i = 0; i < definitions.Count; i++)
            {
                EmberweaverSpellDefinition definition = definitions[i];
                if (definition == null ||
                    definition.RequiredLevel > sim.MyStats.Level)
                {
                    continue;
                }

                Spell spell = definition.RuntimeSpell;
                if (spell == null && GameData.SpellDatabase != null)
                {
                    spell = GameData.SpellDatabase.GetSpellByID(
                        definition.Id);
                }
                if (spell == null)
                {
                    continue;
                }

                bool exists = false;
                foreach (SimPlayerSpellSlot slot in sim.Spellbook)
                {
                    if (slot != null &&
                        slot.spell != null &&
                        string.Equals(
                            slot.spell.Id,
                            definition.Id,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    sim.Spellbook.Add(
                        new SimPlayerSpellSlot(spell, false));
                    restored++;
                }
            }

            if (restored > 0)
            {
                NPC npc = sim.GetThisNPC();
                Plugin.LogInfo(
                    "FOTF SimPlayers: restored [" + restored +
                    "] Emberweaver spellbook entries for [" +
                    (npc != null ? npc.NPCName : sim.name) + "].");
            }
        }

        internal static void AddSkills(
            SimPlayer sim)
        {
            if (sim == null ||
                sim.MyStats == null ||
                !EmberweaverCatalog.IsEmberweaverClass(
                    sim.MyStats.CharacterClass))
            {
                return;
            }

            EmberweaverSkillCatalog.EnsureCreated();

            UseSkill useSkill =
                sim.GetComponent<UseSkill>();

            if (useSkill == null &&
                sim.MyStats.Myself != null)
            {
                useSkill =
                    sim.MyStats.Myself.MySkills;
            }

            IReadOnlyList<EmberweaverSkillDefinition> customDefinitions =
                EmberweaverSkillCatalog.All;

            for (int i = 0;
                 i < customDefinitions.Count;
                 i++)
            {
                EmberweaverSkillDefinition definition =
                    customDefinitions[i];

                if (definition == null ||
                    definition.RuntimeSkill == null ||
                    sim.MyStats.Level <
                        definition.RequiredLevel)
                {
                    continue;
                }

                AddKnownSkill(
                    sim,
                    useSkill,
                    definition.RuntimeSkill);
            }

            if (GameData.SkillDatabase != null &&
                GameData.SkillDatabase.SkillDatabase != null)
            {
                foreach (Skill skill
                         in GameData.SkillDatabase.SkillDatabase)
                {
                    if (skill == null ||
                        !SharedSkillNames.Contains(
                            skill.SkillName) ||
                        skill.ArcanistRequiredLevel <= 0 ||
                        sim.MyStats.Level <
                            skill.ArcanistRequiredLevel)
                    {
                        continue;
                    }

                    AddKnownSkill(
                        sim,
                        useSkill,
                        skill);
                }
            }

            DeduplicateSkillbook(
                sim);
        }

        private static void AddKnownSkill(
            SimPlayer sim,
            UseSkill useSkill,
            Skill skill)
        {
            if (sim == null ||
                skill == null)
            {
                return;
            }

            if (useSkill != null &&
                useSkill.KnownSkills != null &&
                !useSkill.KnownSkills.Contains(
                    skill))
            {
                useSkill.KnownSkills.Add(
                    skill);
            }

            if (sim.Skillbook != null &&
                !sim.Skillbook.Any(
                    slot =>
                        slot != null &&
                        slot.skill == skill))
            {
                sim.Skillbook.Add(
                    new SimPlayerSkillSlot(
                        skill));
            }
        }

        private static void DeduplicateSkillbook(
            SimPlayer sim)
        {
            if (sim == null ||
                sim.Skillbook == null)
            {
                return;
            }

            HashSet<string> seen =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            List<SimPlayerSkillSlot> clean =
                new List<SimPlayerSkillSlot>();

            for (int i = 0;
                 i < sim.Skillbook.Count;
                 i++)
            {
                SimPlayerSkillSlot slot =
                    sim.Skillbook[i];

                if (slot == null ||
                    slot.skill == null)
                {
                    continue;
                }

                string id =
                    slot.skill.Id ??
                    slot.skill.SkillName;

                if (string.IsNullOrEmpty(
                        id) ||
                    !seen.Add(
                        id))
                {
                    continue;
                }

                clean.Add(
                    slot);
            }

            sim.Skillbook =
                clean;
        }
    }
}
