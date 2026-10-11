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
            2;

        private const int LegacySaveVersion =
            1;

        private const string NativeFallbackClassName =
            "Arcanist";

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

        private static readonly Dictionary<string, string> OriginalNativeClasses =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> LoggedFallbackOriginals =
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

        private static readonly MethodInfo ManagerCategorizeAllMethod =
            AccessTools.Method(
                typeof(SimPlayerMngr),
                "CategorizeAll");

        private static bool _loaded;
        private static bool _restoringNativeClasses;


        // ============================================================
        // SAVE DATA
        // ============================================================

        [Serializable]
        private sealed class AssignmentEntry
        {
            public string SimName =
                string.Empty;

            public string OriginalNativeClass =
                string.Empty;
        }


        [Serializable]
        private sealed class AssignmentFile
        {
            public int Version =
                SaveVersion;

            public List<AssignmentEntry> Assignments =
                new List<AssignmentEntry>();

            /*
             * Version 1 compatibility field.
             *
             * Keep this field so JsonUtility can deserialize the original
             * sidecar. Version 2 writes Assignments instead.
             */
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
            // RETIRED BY THE FOTF AUTHORED SIMPLAYER ROSTER.
            //
            // Intentionally do nothing here. Keeping this compatibility
            // method prevents an old call site from failing to compile, while
            // making it impossible for FOTF to assign Blightcaller to a
            // random/native Erenshor SimPlayer ever again.
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
            // Random native-Sim assignment is retired. Only authored FOTF
            // roster members are treated as live Blightcaller SimPlayers.
            return FOTFSimPlayerRoster.IsClassMember(
                simName,
                FOTFSimPlayerRoster.BlightcallerClassName);
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
            if (_restoringNativeClasses ||
                sim == null ||
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
            if (_restoringNativeClasses ||
                manager == null ||
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
            if (_restoringNativeClasses ||
                sim == null ||
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
        // ORIGINAL NATIVE CLASS TRACKING
        // ============================================================

        private static bool CaptureMissingOriginalNativeClasses(
            List<SimPlayerTracking> sims)
        {
            if (sims == null ||
                sims.Count == 0)
            {
                return false;
            }

            bool changed =
                false;

            for (int i = 0;
                 i < sims.Count;
                 i++)
            {
                SimPlayerTracking sim =
                    sims[i];

                if (sim == null ||
                    string.IsNullOrWhiteSpace(
                        sim.SimName) ||
                    !IsAssignedInternal(
                        sim.SimName) ||
                    OriginalNativeClasses.ContainsKey(
                        sim.SimName))
                {
                    continue;
                }

                string originalNativeClass =
                    ResolveOriginalNativeClass(
                        sim.SimName,
                        sim.ClassName,
                        true);

                if (string.IsNullOrEmpty(
                        originalNativeClass))
                {
                    continue;
                }

                OriginalNativeClasses[
                    sim.SimName] =
                        originalNativeClass;

                changed =
                    true;
            }

            return changed;
        }


        private static void CaptureOriginalNativeClass(
            string simName,
            string candidateClassName)
        {
            if (string.IsNullOrWhiteSpace(
                    simName) ||
                OriginalNativeClasses.ContainsKey(
                    simName))
            {
                return;
            }

            string originalNativeClass =
                ResolveOriginalNativeClass(
                    simName,
                    candidateClassName,
                    true);

            if (string.IsNullOrEmpty(
                    originalNativeClass))
            {
                return;
            }

            OriginalNativeClasses[
                simName] =
                    originalNativeClass;
        }


        private static string ResolveOriginalNativeClass(
            string simName,
            string candidateClassName,
            bool remember)
        {
            Load();

            if (!string.IsNullOrWhiteSpace(
                    simName))
            {
                string storedClass;

                if (OriginalNativeClasses.TryGetValue(
                        simName,
                        out storedClass))
                {
                    storedClass =
                        NormalizeNativeClassName(
                            storedClass);

                    if (!string.IsNullOrEmpty(
                            storedClass))
                    {
                        return storedClass;
                    }
                }
            }

            string resolvedClass =
                TryResolveNativeClassFromActualSimTemplate(
                    simName);

            if (string.IsNullOrEmpty(
                    resolvedClass))
            {
                resolvedClass =
                    NormalizeNativeClassName(
                        candidateClassName);
            }

            if (string.IsNullOrEmpty(
                    resolvedClass))
            {
                SimPlayerSaveData savedData =
                    TryGetLoadedSimSaveData(
                        simName);

                resolvedClass =
                    GetNativeClassNameFromSaveData(
                        savedData);
            }

            if (string.IsNullOrEmpty(
                    resolvedClass))
            {
                resolvedClass =
                    NativeFallbackClassName;

                if (!string.IsNullOrWhiteSpace(
                        simName) &&
                    LoggedFallbackOriginals.Add(
                        simName))
                {
                    Plugin.NativeLog.LogWarning(
                        "Blightcaller SimPlayers: original native class for legacy assignment [" +
                        simName +
                        "] could not be recovered. Falling back to [" +
                        NativeFallbackClassName +
                        "].");
                }
            }

            if (remember &&
                !string.IsNullOrWhiteSpace(
                    simName))
            {
                OriginalNativeClasses[
                    simName] =
                        resolvedClass;
            }

            return resolvedClass;
        }


        private static string TryResolveNativeClassFromActualSimTemplate(
            string simName)
        {
            if (string.IsNullOrWhiteSpace(
                    simName) ||
                GameData.SimMngr == null ||
                GameData.SimMngr.ActualSims == null)
            {
                return null;
            }

            for (int i = 0;
                 i < GameData.SimMngr.ActualSims.Count;
                 i++)
            {
                GameObject template =
                    GameData.SimMngr.ActualSims[i];

                if (template == null)
                {
                    continue;
                }

                NPC npc =
                    template.GetComponent<NPC>();

                string templateName =
                    npc != null &&
                    !string.IsNullOrWhiteSpace(
                        npc.NPCName)
                        ? npc.NPCName
                        : template.name;

                if (!string.Equals(
                        templateName,
                        simName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Stats stats =
                    template.GetComponent<Stats>();

                if (stats == null ||
                    stats.CharacterClass == null)
                {
                    return null;
                }

                return NormalizeNativeClassName(
                    stats.CharacterClass.ClassName);
            }

            return null;
        }


        private static SimPlayerSaveData TryGetLoadedSimSaveData(
            string simName)
        {
            if (string.IsNullOrWhiteSpace(
                    simName))
            {
                return null;
            }

            try
            {
                return
                    SimPlayerDataManager.FindMyDataInList(
                        simName);
            }
            catch
            {
                return null;
            }
        }


        private static string GetNativeClassNameFromSaveData(
            SimPlayerSaveData data)
        {
            if (data == null)
            {
                return null;
            }

            if (data.War)
            {
                return "Paladin";
            }

            if (data.Arc)
            {
                return "Arcanist";
            }

            if (data.Dru)
            {
                return "Druid";
            }

            if (data.Duel)
            {
                return "Duelist";
            }

            if (data.Storm)
            {
                return "Stormcaller";
            }

            if (data.Reav)
            {
                return "Reaver";
            }

            return null;
        }


        private static string NormalizeNativeClassName(
            string className)
        {
            if (string.IsNullOrWhiteSpace(
                    className))
            {
                return null;
            }

            if (string.Equals(
                    className,
                    "Paladin",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Paladin";
            }

            if (string.Equals(
                    className,
                    "Arcanist",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Arcanist";
            }

            if (string.Equals(
                    className,
                    "Druid",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Druid";
            }

            if (string.Equals(
                    className,
                    "Duelist",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Duelist";
            }

            if (string.Equals(
                    className,
                    "Stormcaller",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Stormcaller";
            }

            if (string.Equals(
                    className,
                    "Reaver",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Reaver";
            }

            return null;
        }


        private static Class ResolveNativeClassObject(
            string className)
        {
            if (GameData.ClassDB == null)
            {
                return null;
            }

            string normalized =
                NormalizeNativeClassName(
                    className);

            if (string.Equals(
                    normalized,
                    "Paladin",
                    StringComparison.Ordinal))
            {
                return GameData.ClassDB.Paladin;
            }

            if (string.Equals(
                    normalized,
                    "Arcanist",
                    StringComparison.Ordinal))
            {
                return GameData.ClassDB.Arcanist;
            }

            if (string.Equals(
                    normalized,
                    "Druid",
                    StringComparison.Ordinal))
            {
                return GameData.ClassDB.Druid;
            }

            if (string.Equals(
                    normalized,
                    "Duelist",
                    StringComparison.Ordinal))
            {
                return GameData.ClassDB.Duelist;
            }

            if (string.Equals(
                    normalized,
                    "Stormcaller",
                    StringComparison.Ordinal))
            {
                return GameData.ClassDB.Stormcaller;
            }

            if (string.Equals(
                    normalized,
                    "Reaver",
                    StringComparison.Ordinal))
            {
                return GameData.ClassDB.Reaver;
            }

            return null;
        }


        private static void SetNativeClassFlags(
            SimPlayerSaveData data,
            string className)
        {
            if (data == null)
            {
                return;
            }

            string normalized =
                NormalizeNativeClassName(
                    className) ??
                NativeFallbackClassName;

            data.War =
                string.Equals(
                    normalized,
                    "Paladin",
                    StringComparison.Ordinal);

            data.Arc =
                string.Equals(
                    normalized,
                    "Arcanist",
                    StringComparison.Ordinal);

            data.Dru =
                string.Equals(
                    normalized,
                    "Druid",
                    StringComparison.Ordinal);

            data.Duel =
                string.Equals(
                    normalized,
                    "Duelist",
                    StringComparison.Ordinal);

            data.Storm =
                string.Equals(
                    normalized,
                    "Stormcaller",
                    StringComparison.Ordinal);

            data.Reav =
                string.Equals(
                    normalized,
                    "Reaver",
                    StringComparison.Ordinal);
        }


        // ============================================================
        // LEGACY RANDOM-ASSIGNMENT RETIREMENT
        // ============================================================

        internal static void RetireLegacyAssignments()
        {
            Load();

            if (AssignedSimNames.Count == 0)
            {
                return;
            }

            int retiredCount =
                AssignedSimNames.Count;

            // Restore live tracking/runtime/save state first. The helper uses
            // IsAssignedInternal(), so it still sees the legacy sidecar even
            // though public IsAssigned() now recognizes only authored FOTF
            // roster members.
            RestoreNativeClassesOnShutdown();

            AssignedSimNames.Clear();
            OriginalNativeClasses.Clear();
            LoggedFallbackOriginals.Clear();
            Save();

            Plugin.NativeLog.LogInfo(
                "Blightcaller SimPlayers: retired [" +
                retiredCount +
                "] legacy native-Sim class assignments. Future custom Sims are FOTF-authored roster members only.");
        }


        // ============================================================
        // MOD UNLOAD RESTORATION
        // ============================================================

        internal static void RestoreNativeClassesOnShutdown()
        {
            Load();

            if (AssignedSimNames.Count == 0)
            {
                return;
            }

            _restoringNativeClasses =
                true;

            int trackingRestored =
                0;

            int activeRestored =
                0;

            int savesRestored =
                0;

            try
            {
                SimPlayerMngr manager =
                    GameData.SimMngr;

                if (manager != null &&
                    manager.Sims != null)
                {
                    CaptureMissingOriginalNativeClasses(
                        manager.Sims);

                    for (int i = 0;
                         i < manager.Sims.Count;
                         i++)
                    {
                        SimPlayerTracking tracking =
                            manager.Sims[i];

                        if (tracking == null ||
                            string.IsNullOrWhiteSpace(
                                tracking.SimName) ||
                            !IsAssignedInternal(
                                tracking.SimName))
                        {
                            continue;
                        }

                        string originalNativeClass =
                            ResolveOriginalNativeClass(
                                tracking.SimName,
                                tracking.ClassName,
                                true);

                        tracking.ClassName =
                            originalNativeClass;

                        trackingRestored++;
                    }
                }

                if (manager != null &&
                    manager.ActiveSimInstances != null &&
                    manager.ActiveSimInstances.Count > 0)
                {
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
                            string.IsNullOrWhiteSpace(
                                npc.NPCName) ||
                            !IsAssignedInternal(
                                npc.NPCName))
                        {
                            continue;
                        }

                        string originalNativeClass =
                            ResolveOriginalNativeClass(
                                npc.NPCName,
                                null,
                                true);

                        Class nativeClass =
                            ResolveNativeClassObject(
                                originalNativeClass);

                        if (nativeClass == null)
                        {
                            Plugin.NativeLog.LogWarning(
                                "Blightcaller SimPlayers: could not resolve native Class object [" +
                                originalNativeClass +
                                "] while unloading [" +
                                npc.NPCName +
                                "].");

                            continue;
                        }

                        sim.MyStats.CharacterClass =
                            nativeClass;

                        if (sim.myIndex >= 0 &&
                            manager.Sims != null &&
                            sim.myIndex <
                                manager.Sims.Count &&
                            manager.Sims[
                                sim.myIndex] != null)
                        {
                            manager.Sims[
                                sim.myIndex]
                                .ClassName =
                                    originalNativeClass;
                        }

                        try
                        {
                            sim.MyStats.CalcStats();
                        }
                        catch (Exception exception)
                        {
                            Plugin.NativeLog.LogWarning(
                                "Blightcaller SimPlayers: CalcStats failed while reverting [" +
                                npc.NPCName +
                                "] to [" +
                                originalNativeClass +
                                "]: " +
                                exception.Message);
                        }

                        try
                        {
                            if (sim.Skillbook != null)
                            {
                                sim.Skillbook.Clear();
                            }

                            sim.LoadSimSkills();
                            sim.LoadSimSpells();
                        }
                        catch (Exception exception)
                        {
                            Plugin.NativeLog.LogWarning(
                                "Blightcaller SimPlayers: native skill/spell reload failed while reverting [" +
                                npc.NPCName +
                                "]: " +
                                exception.Message);
                        }

                        activeRestored++;
                    }
                }

                /*
                 * Rebuild native role pools while restoration mode prevents
                 * the Harmony prefix/postfix from putting Blightcaller back.
                 */
                if (manager != null &&
                    ManagerCategorizeAllMethod != null)
                {
                    try
                    {
                        ManagerCategorizeAllMethod.Invoke(
                            manager,
                            null);
                    }
                    catch (Exception exception)
                    {
                        Plugin.NativeLog.LogWarning(
                            "Blightcaller SimPlayers: native SimPlayer role recategorization failed during unload: " +
                            exception.Message);
                    }
                }

                /*
                 * Persist the original native class flags for every assigned
                 * SimPlayer. If FOTF is then removed completely, Erenshor can
                 * load the SimPlayer using only its native save data.
                 */
                string[] assignedNames =
                    AssignedSimNames
                        .OrderBy(
                            value =>
                                value,
                            StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                for (int i = 0;
                     i < assignedNames.Length;
                     i++)
                {
                    string simName =
                        assignedNames[i];

                    string originalNativeClass =
                        ResolveOriginalNativeClass(
                            simName,
                            null,
                            true);

                    SimPlayerSaveData data =
                        TryGetLoadedSimSaveData(
                            simName);

                    if (data == null)
                    {
                        try
                        {
                            data =
                                SimPlayerDataManager.GetMyData(
                                    simName);
                        }
                        catch (Exception exception)
                        {
                            Plugin.NativeLog.LogWarning(
                                "Blightcaller SimPlayers: could not load native save while reverting [" +
                                simName +
                                "]: " +
                                exception.Message);
                        }
                    }

                    if (data == null)
                    {
                        continue;
                    }

                    SetNativeClassFlags(
                        data,
                        originalNativeClass);

                    try
                    {
                        SimPlayerDataManager.SaveSimData(
                            data);

                        savesRestored++;
                    }
                    catch (Exception exception)
                    {
                        Plugin.NativeLog.LogWarning(
                            "Blightcaller SimPlayers: failed persisting native class [" +
                            originalNativeClass +
                            "] for [" +
                            simName +
                            "] during unload: " +
                            exception.Message);
                    }
                }

                Save();

                Plugin.NativeLog.LogInfo(
                    "Blightcaller SimPlayers: unload restoration complete. Tracking=[" +
                    trackingRestored +
                    "] Active=[" +
                    activeRestored +
                    "] NativeSaves=[" +
                    savesRestored +
                    "] Assignments=[" +
                    AssignedSimNames.Count +
                    "].");
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller SimPlayers: unload restoration failed: " +
                    exception);
            }
            finally
            {
                _restoringNativeClasses =
                    false;
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

            // Authored Blightcaller Sims use Arcanist as the native save
            // representation. The real Blightcaller Class object is restored
            // immediately after SimPlayer.LoadSimData().
            SetNativeClassFlags(
                data,
                NativeFallbackClassName);
        }


        // ============================================================
        // SIM MANAGER DPS ROLE
        // ============================================================

        internal static void AddManagerDpsRoles(
            SimPlayerMngr manager)
        {
            if (_restoringNativeClasses ||
                manager == null ||
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
            if (_restoringNativeClasses ||
                grouping == null ||
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

            OriginalNativeClasses.Remove(
                simName);

            LoggedFallbackOriginals.Remove(
                simName);

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
            OriginalNativeClasses.Clear();
            LoggedFallbackOriginals.Clear();

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

                if (file == null)
                {
                    return false;
                }

                AssignedSimNames.Clear();
                OriginalNativeClasses.Clear();

                if (file.Version ==
                    SaveVersion)
                {
                    if (file.Assignments == null)
                    {
                        return false;
                    }

                    for (int i = 0;
                         i < file.Assignments.Count;
                         i++)
                    {
                        AssignmentEntry entry =
                            file.Assignments[i];

                        if (entry == null ||
                            string.IsNullOrWhiteSpace(
                                entry.SimName))
                        {
                            continue;
                        }

                        AssignedSimNames.Add(
                            entry.SimName);

                        string nativeClass =
                            NormalizeNativeClassName(
                                entry.OriginalNativeClass);

                        if (!string.IsNullOrEmpty(
                                nativeClass))
                        {
                            OriginalNativeClasses[
                                entry.SimName] =
                                    nativeClass;
                        }
                    }

                    return true;
                }

                if (file.Version ==
                        LegacySaveVersion &&
                    file.BlightcallerSimNames != null)
                {
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

                    Plugin.NativeLog.LogInfo(
                        "Blightcaller SimPlayers: loaded legacy version 1 assignment sidecar. Original native classes will be recovered where possible and migrated to version 2.");

                    return true;
                }

                return false;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller SimPlayers: failed to load assignment sidecar [" +
                    path +
                    "]: " +
                    exception.Message);

                AssignedSimNames.Clear();
                OriginalNativeClasses.Clear();

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

                file.Assignments =
                    AssignedSimNames
                        .OrderBy(
                            value =>
                                value,
                            StringComparer.OrdinalIgnoreCase)
                        .Select(
                            simName =>
                            {
                                AssignmentEntry entry =
                                    new AssignmentEntry();

                                entry.SimName =
                                    simName;

                                entry.OriginalNativeClass =
                                    ResolveOriginalNativeClass(
                                        simName,
                                        null,
                                        true);

                                return entry;
                            })
                        .ToList();

                file.BlightcallerSimNames.Clear();

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