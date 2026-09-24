using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class BlightcallerSimPlayers
    {
        // ============================================================
        // CONFIGURATION
        // ============================================================

        private const string BlightcallerName =
            "Blightcaller";

        private const int PopulationDivisor =
            8;

        private const int SaveVersion =
            1;

        private const string SaveDirectoryName =
            "FateOfTheFallen";

        private const string SaveFileName =
            "blightcaller_sim_classes.json";


        // ============================================================
        // ASSIGNMENTS
        // ============================================================

        private static readonly HashSet<string> AssignedSimNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> SharedSkillNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                "Arcane Proficiency",
                "Dodge",
                "Arcane Recovery",
                "Block",
                "Block II",
                "Dodge II",
                "Multifocus"
            };

        private static readonly FieldInfo ManagerDpsField =
            AccessTools.Field(
                typeof(SimPlayerMngr),
                "DPS");

        private static bool _loaded;
        private static bool _loggedPopulation;


        // ============================================================
        // SAVE DATA
        // ============================================================

        [Serializable]
        private sealed class AssignmentFile
        {
            public int Version =
                SaveVersion;

            public List<string> BlightcallerSimNames =
                new List<string>();
        }


        private static string SaveDirectory
        {
            get
            {
                return Path.Combine(
                    Application.persistentDataPath,
                    SaveDirectoryName);
            }
        }


        private static string SavePath
        {
            get
            {
                return Path.Combine(
                    SaveDirectory,
                    SaveFileName);
            }
        }


        private static string BackupPath
        {
            get
            {
                return SavePath +
                    ".bak";
            }
        }


        private static string TemporaryPath
        {
            get
            {
                return SavePath +
                    ".tmp";
            }
        }


        // ============================================================
        // POPULATION ASSIGNMENT
        // ============================================================

        internal static void EnsurePopulation(
            List<SimPlayerTracking> sims)
        {
            Load();

            if (sims == null ||
                sims.Count == 0)
            {
                return;
            }

            List<SimPlayerTracking> eligible =
                sims
                    .Where(IsEligible)
                    .OrderBy(
                        sim =>
                            StableHash(
                                sim.SimName))
                    .ThenBy(
                        sim =>
                            sim.SimName,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (eligible.Count == 0)
            {
                return;
            }

            int targetCount =
                Math.Max(
                    1,
                    eligible.Count /
                    PopulationDivisor);

            int assignedCount =
                eligible.Count(
                    sim =>
                        IsAssignedInternal(
                            sim.SimName));

            bool changed =
                false;

            if (assignedCount <
                targetCount)
            {
                for (int i = 0;
                     i < eligible.Count &&
                     assignedCount < targetCount;
                     i++)
                {
                    SimPlayerTracking sim =
                        eligible[i];

                    if (sim == null ||
                        string.IsNullOrWhiteSpace(
                            sim.SimName))
                    {
                        continue;
                    }

                    if (AssignedSimNames.Contains(
                            sim.SimName))
                    {
                        continue;
                    }

                    AssignedSimNames.Add(
                        sim.SimName);

                    sim.ClassName =
                        BlightcallerName;

                    assignedCount++;
                    changed = true;

                }
            }

            for (int i = 0;
                 i < eligible.Count;
                 i++)
            {
                SimPlayerTracking sim =
                    eligible[i];

                if (sim == null ||
                    !IsAssignedInternal(
                        sim.SimName))
                {
                    continue;
                }

                sim.ClassName =
                    BlightcallerName;
            }

            if (changed)
            {
                Save();
            }

            if (!_loggedPopulation ||
                changed)
            {
                _loggedPopulation =
                    true;

                
            }
        }


        private static bool IsEligible(
            SimPlayerTracking sim)
        {
            if (sim == null ||
                string.IsNullOrWhiteSpace(
                    sim.SimName))
            {
                return false;
            }

            if (sim.IsGMCharacter ||
                sim.Rival ||
                sim.TiedToSlot == 99)
            {
                return false;
            }

            if (AssignedSimNames.Contains(
                    sim.SimName))
            {
                return true;
            }

            return IsNativeClassName(
                sim.ClassName);
        }


        private static bool IsNativeClassName(
            string className)
        {
            if (string.IsNullOrWhiteSpace(
                    className))
            {
                return false;
            }

            return
                string.Equals(
                    className,
                    "Paladin",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    className,
                    "Arcanist",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    className,
                    "Duelist",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    className,
                    "Druid",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    className,
                    "Stormcaller",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    className,
                    "Reaver",
                    StringComparison.OrdinalIgnoreCase);
        }


        // ============================================================
        // ASSIGNMENT LOOKUP
        // ============================================================

        internal static bool IsAssigned(
            string simName)
        {
            Load();

            return IsAssignedInternal(
                simName);
        }


        private static bool IsAssignedInternal(
            string simName)
        {
            if (string.IsNullOrWhiteSpace(
                    simName))
            {
                return false;
            }

            return AssignedSimNames.Contains(
                simName);
        }


        // ============================================================
        // RUNTIME CLASS RESTORATION
        // ============================================================

        internal static void ApplyRuntimeClass(
            SimPlayer sim)
        {
            if (sim == null ||
                sim.MyStats == null)
            {
                return;
            }

            NPC npc =
                sim.GetThisNPC();

            if (npc == null ||
                !IsAssigned(
                    npc.NPCName))
            {
                return;
            }

            BlightcallerCatalog.EnsureClass();

            Class blightcaller =
                BlightcallerCatalog.BlightcallerClass;

            if (blightcaller == null)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller SimPlayers: Blightcaller class was unavailable while restoring [" +
                    npc.NPCName +
                    "].");

                return;
            }

            sim.MyStats.CharacterClass =
                blightcaller;

            if (sim.myIndex >= 0 &&
                GameData.SimMngr != null &&
                GameData.SimMngr.Sims != null &&
                sim.myIndex <
                    GameData.SimMngr.Sims.Count)
            {
                SimPlayerTracking tracking =
                    GameData.SimMngr.Sims[
                        sim.myIndex];

                if (tracking != null)
                {
                    tracking.ClassName =
                        BlightcallerName;
                }
            }

            try
            {
                sim.MyStats.CalcStats();
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller SimPlayers: CalcStats failed while restoring [" +
                    npc.NPCName +
                    "]: " +
                    exception.Message);
            }
        }


        // ============================================================
        // ACTIVE SIM RECONCILIATION
        // ============================================================

        internal static void ReapplyActiveSimPlayers(
            SimPlayerMngr manager)
        {
            if (manager == null ||
                manager.ActiveSimInstances == null ||
                manager.ActiveSimInstances.Count == 0)
            {
                return;
            }

            SimPlayer[] active =
                manager.ActiveSimInstances
                    .ToArray();

            for (int i = 0;
                 i < active.Length;
                 i++)
            {
                SimPlayer sim =
                    active[i];

                if (sim == null ||
                    sim.MyStats == null)
                {
                    continue;
                }

                NPC npc =
                    sim.GetThisNPC();

                if (npc == null ||
                    !IsAssigned(
                        npc.NPCName))
                {
                    continue;
                }

                bool needsReload =
                    !BlightcallerCatalog
                        .IsBlightcallerClass(
                            sim.MyStats.CharacterClass);

                ApplyRuntimeClass(
                    sim);

                if (!needsReload)
                {
                    continue;
                }

                sim.LoadSimSkills();
                sim.LoadSimSpells();

            }
        }


        // ============================================================
        // SPELL CONFIGURATION
        // ============================================================

        internal static void ConfigureSpellsForSimPlayers()
        {
            BlightcallerCatalog.EnsureClass();

            Class blightcaller =
                BlightcallerCatalog.BlightcallerClass;

            if (blightcaller == null)
            {
                return;
            }

            int visibleCount =
                0;

            int hiddenCount =
                0;

            foreach (
                KeyValuePair<string, Spell> pair
                in BlightcallerCatalog.SpellsByKey)
            {
                Spell spell =
                    pair.Value;

                if (spell == null)
                {
                    continue;
                }

                if (spell.UsedBy == null)
                {
                    spell.UsedBy =
                        new List<Class>();
                }

                if (!spell.UsedBy.Contains(
                        blightcaller))
                {
                    spell.UsedBy.Add(
                        blightcaller);
                }

                BlightcallerSpellDefinition definition;

                if (!BlightcallerCatalog.TryGetDefinition(
                        spell,
                        out definition))
                {
                    continue;
                }

                spell.SimUsable =
                    !definition.HiddenEffect;

                spell.SimsNeedHelpToLearn =
                    false;

                if (definition.HiddenEffect)
                {
                    hiddenCount++;
                }
                else
                {
                    visibleCount++;
                }
            }

            
        }


        // ============================================================
        // SHARED SKILLS
        // ============================================================

        internal static void AddBlightcallerSkills(
            SimPlayer sim)
        {
            if (sim == null ||
                sim.MyStats == null)
            {
                return;
            }

            NPC npc =
                sim.GetThisNPC();

            if (npc == null ||
                !IsAssigned(
                    npc.NPCName))
            {
                return;
            }

            if (!BlightcallerCatalog
                    .IsBlightcallerClass(
                        sim.MyStats.CharacterClass))
            {
                return;
            }

            if (GameData.SkillDatabase == null ||
                GameData.SkillDatabase.SkillDatabase == null)
            {
                return;
            }

            foreach (
                Skill skill
                in GameData.SkillDatabase.SkillDatabase)
            {
                if (!CanBlightcallerSimUseSkill(
                        sim,
                        skill))
                {
                    continue;
                }

                if ((skill.TypeOfSkill ==
                        Skill.SkillType.Attack ||
                     skill.TypeOfSkill ==
                        Skill.SkillType.Ranged) &&
                    npc.MyAttackSkills != null &&
                    !npc.MyAttackSkills.Contains(
                        skill))
                {
                    npc.MyAttackSkills.Add(
                        skill);
                }

                if (skill.TypeOfSkill ==
                        Skill.SkillType.Utility &&
                    skill.StanceToUse != null &&
                    sim.KnownStances != null &&
                    !sim.KnownStances.Contains(
                        skill.StanceToUse))
                {
                    sim.KnownStances.Add(
                        skill.StanceToUse);
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

            DeduplicateSkillbook(
                sim);
        }


        private static bool CanBlightcallerSimUseSkill(
            SimPlayer sim,
            Skill skill)
        {
            if (sim == null ||
                sim.MyStats == null ||
                skill == null)
            {
                return false;
            }

            if (!SharedSkillNames.Contains(
                    skill.SkillName))
            {
                return false;
            }

            if (skill.ArcanistRequiredLevel <=
                0)
            {
                return false;
            }

            return sim.MyStats.Level >=
                skill.ArcanistRequiredLevel;
        }


        private static void DeduplicateSkillbook(
            SimPlayer sim)
        {
            if (sim == null ||
                sim.Skillbook == null)
            {
                return;
            }

            HashSet<string> seenIds =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            List<SimPlayerSkillSlot> deduplicated =
                new List<SimPlayerSkillSlot>();

            for (int i = 0;
                 i < sim.Skillbook.Count;
                 i++)
            {
                SimPlayerSkillSlot slot =
                    sim.Skillbook[i];

                if (slot == null ||
                    slot.skill == null ||
                    string.IsNullOrEmpty(
                        slot.skill.Id))
                {
                    continue;
                }

                if (!seenIds.Add(
                        slot.skill.Id))
                {
                    continue;
                }

                deduplicated.Add(
                    slot);
            }

            sim.Skillbook =
                deduplicated;
        }


        // ============================================================
        // BLIGHTCALLER SIMPLAYER ASCENSION SELECTION
        // ============================================================

        internal static bool ChooseBlightcallerAscension(
            Stats stats)
        {
            if (stats == null ||
                stats.Myself == null ||
                stats.Myself.MySkills == null)
            {
                return false;
            }

            if (!BlightcallerCatalog.IsBlightcallerClass(
                    stats.CharacterClass))
            {
                return false;
            }

            NPC npc =
                stats.Myself.MyNPC;

            if (npc == null ||
                !npc.SimPlayer)
            {
                return false;
            }

            UseSkill skills =
                stats.Myself.MySkills;

            if (skills.AscensionPoints <= 0)
            {
                return true;
            }

            if (!BlightcallerAscensions.Register())
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller SimPlayers: could not register Ascensions while selecting a SimPlayer Ascension.");

                return true;
            }

            List<Ascension> candidates =
                new List<Ascension>();

            /*
             * Native SimPlayers do not begin receiving class-specific
             * Ascensions until eight total points have already been spent.
             */
            bool classAscensionsUnlocked =
                skills.GetPointsSpent() >= 8;

            if (classAscensionsUnlocked)
            {
                foreach (
                    Ascension ascension
                    in BlightcallerAscensions.All)
                {
                    if (ascension == null ||
                        string.IsNullOrEmpty(
                            ascension.Id))
                    {
                        continue;
                    }

                    int currentRank =
                        BlightcallerAscensions.GetRank(
                            skills,
                            ascension.Id);

                    if (currentRank >=
                        ascension.MaxRank)
                    {
                        continue;
                    }

                    candidates.Add(
                        ascension);
                }
            }

            /*
             * Before the class-specific pool unlocks, or once every
             * Blightcaller Ascension is capped, fall back to General.
             */
            if (candidates.Count == 0 &&
                GameData.SkillDatabase != null &&
                GameData.SkillDatabase.AscensionDatabase != null)
            {
                foreach (
                    Ascension ascension
                    in GameData.SkillDatabase.AscensionDatabase)
                {
                    if (ascension == null)
                    {
                        continue;
                    }

                    if (ascension.UsedBy !=
                        Ascension.Class.General)
                    {
                        continue;
                    }

                    if (skills.GetAscensionRank(
                            ascension.Id) >=
                        ascension.MaxRank)
                    {
                        continue;
                    }

                    candidates.Add(
                        ascension);
                }
            }

            if (candidates.Count == 0)
            {

                return true;
            }

            /*
             * Match the native SimPlayer selection method:
             *
             *     SimPlayerWeight - current rank
             *
             * Highest value wins.
             */
            int bestWeight =
                -99;

            Ascension selected =
                null;

            foreach (
                Ascension ascension
                in candidates)
            {
                if (ascension == null)
                {
                    continue;
                }

                int currentRank =
                    skills.GetAscensionRank(
                        ascension.Id);

                int weight =
                    ascension.SimPlayerWeight -
                    currentRank;

                if (weight <=
                    bestWeight)
                {
                    continue;
                }

                selected =
                    ascension;

                bestWeight =
                    weight;
            }

            if (selected == null)
            {
                return true;
            }

            if (!skills.HasAscension(
                    selected.Id))
            {
                skills.AddAscension(
                    selected.Id);
            }
            else
            {
                skills.LevelUpAscension(
                    selected.Id);
            }

            skills.AscensionPoints--;

            int newRank =
                skills.GetAscensionRank(
                    selected.Id);

            

            return true;
        }


        // ============================================================
        // SIMPLAYER ASCENSION INSPECTION UI
        // ============================================================
        //
        // Native SimInspect.DoAscensionsView() displays:
        //
        // - General Ascensions for everyone
        // - native class-specific Ascensions for the six native classes
        //
        // It has no "Blightcaller" branch, so our custom Ascensions are
        // omitted from the inspection window despite existing correctly
        // in the SimPlayer's UseSkill.MyAscensions list.
        //
        // This method is called from the SimInspect Harmony postfix.
        // ============================================================

        internal static void PopulateAscensionInspection(
            SimInspect inspect)
        {
            if (inspect == null ||
                inspect.Who == null ||
                inspect.Who.MyStats == null ||
                inspect.AscList == null)
            {
                return;
            }

            SimPlayer sim =
                inspect.Who;

            if (!BlightcallerCatalog
                    .IsBlightcallerClass(
                        sim.MyStats.CharacterClass))
            {
                return;
            }

            if (!BlightcallerAscensions.Register())
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller SimPlayers: could not register Ascensions while populating inspection UI.");

                return;
            }

            UseSkill skills =
                sim.GetComponent<UseSkill>();

            if (skills == null &&
                sim.MyStats.Myself != null)
            {
                skills =
                    sim.MyStats.Myself.MySkills;
            }

            if (skills == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller SimPlayers: inspected Blightcaller [" +
                    sim.transform.name +
                    "] has no UseSkill component.");

                return;
            }

            /*
             * Native DoAscensionsView() has already cleared the text and
             * populated all General Ascensions.
             *
             * We append only the four Blightcaller-specific Ascensions.
             */
            foreach (
                Ascension ascension
                in BlightcallerAscensions.All)
            {
                if (ascension == null ||
                    string.IsNullOrEmpty(
                        ascension.Id))
                {
                    continue;
                }

                int rank =
                    BlightcallerAscensions.GetRank(
                        skills,
                        ascension.Id);

                inspect.AscList.text +=
                    ascension.SkillName +
                    " - Rank: " +
                    rank +
                    " / " +
                    ascension.MaxRank +
                    "\n";
            }
        }


        // ============================================================
        // NATIVE SAVE FALLBACK
        // ============================================================

        internal static void ApplyNativeFallback(
            SimPlayerSaveData data)
        {
            if (data == null ||
                !IsAssigned(
                    data.NPCName))
            {
                return;
            }

            data.War = false;
            data.Arc = false;
            data.Dru = false;
            data.Duel = false;
            data.Storm = false;
            data.Reav = false;

            /*
             * Native SimPlayer save data cannot store a custom class.
             *
             * Save Blightcaller Sims as Arcanists and restore the actual
             * runtime class through LoadSimData.
             */
            data.Arc = true;
        }


        // ============================================================
        // SIM MANAGER DPS ROLE
        // ============================================================

        internal static void AddManagerDpsRoles(
            SimPlayerMngr manager)
        {
            if (manager == null ||
                manager.Sims == null ||
                ManagerDpsField == null)
            {
                return;
            }

            List<string> dps =
                ManagerDpsField.GetValue(
                    manager)
                as List<string>;

            if (dps == null)
            {
                return;
            }

            for (int i = 0;
                 i < manager.Sims.Count;
                 i++)
            {
                SimPlayerTracking sim =
                    manager.Sims[i];

                if (sim == null ||
                    string.IsNullOrWhiteSpace(
                        sim.SimName))
                {
                    continue;
                }

                if (!string.Equals(
                        sim.ClassName,
                        BlightcallerName,
                        StringComparison.OrdinalIgnoreCase) &&
                    !IsAssigned(
                        sim.SimName))
                {
                    continue;
                }

                if (!dps.Contains(
                        sim.SimName))
                {
                    dps.Add(
                        sim.SimName);
                }
            }
        }


        // ============================================================
        // ACTIVE GROUP DPS ROLE
        // ============================================================

        internal static void AddGroupingDpsRoles(
            SimPlayerGrouping grouping)
        {
            if (grouping == null ||
                grouping.DPS == null ||
                GameData.GroupMembers == null)
            {
                return;
            }

            foreach (
                SimPlayerTracking member
                in GameData.GroupMembers)
            {
                if (member == null)
                {
                    continue;
                }

                bool isBlightcaller =
                    string.Equals(
                        member.ClassName,
                        BlightcallerName,
                        StringComparison.OrdinalIgnoreCase);

                if (!isBlightcaller &&
                    member.MyStats != null)
                {
                    isBlightcaller =
                        BlightcallerCatalog
                            .IsBlightcallerClass(
                                member.MyStats.CharacterClass);
                }

                if (!isBlightcaller)
                {
                    continue;
                }

                if (!grouping.DPS.Contains(
                        member))
                {
                    grouping.DPS.Add(
                        member);
                }
            }
        }


        // ============================================================
        // DELETE ASSIGNMENT
        // ============================================================

        internal static void RemoveAssignment(
            string simName)
        {
            Load();

            if (string.IsNullOrWhiteSpace(
                    simName))
            {
                return;
            }

            if (!AssignedSimNames.Remove(
                    simName))
            {
                return;
            }

            Save();

        }


        // ============================================================
        // LOAD SIDECAR
        // ============================================================

        private static void Load()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;

            AssignedSimNames.Clear();

            if (TryLoadFile(
                    SavePath))
            {
                return;
            }

            if (!TryLoadFile(
                    BackupPath))
            {
                return;
            }

            Plugin.NativeLog.LogWarning(
                "Blightcaller SimPlayers: recovered assignments from backup sidecar.");

            try
            {
                Directory.CreateDirectory(
                    SaveDirectory);

                File.Copy(
                    BackupPath,
                    SavePath,
                    true);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller SimPlayers: could not restore backup sidecar to primary path: " +
                    exception.Message);
            }
        }


        private static bool TryLoadFile(
            string path)
        {
            if (string.IsNullOrEmpty(
                    path) ||
                !File.Exists(
                    path))
            {
                return false;
            }

            try
            {
                string json =
                    File.ReadAllText(
                        path);

                if (string.IsNullOrWhiteSpace(
                        json))
                {
                    return false;
                }

                AssignmentFile file =
                    JsonUtility.FromJson<AssignmentFile>(
                        json);

                if (file == null ||
                    file.Version != SaveVersion ||
                    file.BlightcallerSimNames == null)
                {
                    return false;
                }

                AssignedSimNames.Clear();

                for (int i = 0;
                     i < file.BlightcallerSimNames.Count;
                     i++)
                {
                    string simName =
                        file.BlightcallerSimNames[i];

                    if (string.IsNullOrWhiteSpace(
                            simName))
                    {
                        continue;
                    }

                    AssignedSimNames.Add(
                        simName);
                }

                

                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller SimPlayers: failed to load assignment sidecar [" +
                    path +
                    "]: " +
                    exception.Message);

                AssignedSimNames.Clear();

                return false;
            }
        }


        // ============================================================
        // SAVE SIDECAR
        // ============================================================

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(
                    SaveDirectory);

                AssignmentFile file =
                    new AssignmentFile();

                file.BlightcallerSimNames =
                    AssignedSimNames
                        .OrderBy(
                            value =>
                                value,
                            StringComparer.OrdinalIgnoreCase)
                        .ToList();

                string json =
                    JsonUtility.ToJson(
                        file,
                        true);

                if (File.Exists(
                        TemporaryPath))
                {
                    File.Delete(
                        TemporaryPath);
                }

                File.WriteAllText(
                    TemporaryPath,
                    json);

                if (File.Exists(
                        SavePath))
                {
                    try
                    {
                        if (File.Exists(
                                BackupPath))
                        {
                            File.Delete(
                                BackupPath);
                        }

                        File.Replace(
                            TemporaryPath,
                            SavePath,
                            BackupPath);
                    }
                    catch
                    {
                        File.Copy(
                            SavePath,
                            BackupPath,
                            true);

                        File.Delete(
                            SavePath);

                        File.Move(
                            TemporaryPath,
                            SavePath);
                    }
                }
                else
                {
                    File.Move(
                        TemporaryPath,
                        SavePath);
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller SimPlayers: failed to save assignment sidecar: " +
                    exception);

                try
                {
                    if (File.Exists(
                            TemporaryPath))
                    {
                        File.Delete(
                            TemporaryPath);
                    }
                }
                catch
                {
                }
            }
        }


        // ============================================================
        // STABLE HASH
        // ============================================================

        private static uint StableHash(
            string value)
        {
            uint hash =
                2166136261U;

            foreach (
                char character
                in value ??
                   string.Empty)
            {
                hash ^=
                    (uint)char.ToUpperInvariant(
                        character);

                hash *=
                    16777619U;
            }

            return hash;
        }
    }
}