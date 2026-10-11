using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Owns the authored Fate of the Fallen SimPlayer roster.
    ///
    /// Native Erenshor SimPlayers are never converted into FOTF classes.
    /// Instead, fixed FOTF definitions are appended to Erenshor's normal
    /// SimPlayer tracking population after the native population has loaded.
    /// Their underlying runtime avatars still use Erenshor's BlankSPTemplate,
    /// so grouping, raids, inventory, movement and ordinary Sim lifecycle all
    /// remain native-owned.
    /// </summary>
    internal static class FOTFSimPlayerRoster
    {
        internal const string BlightcallerClassName =
            "Blightcaller";

        internal const string EmberweaverClassName =
            "Emberweaver";

        internal const string LightbringerClassName = "Lightbringer";

        // v0.2 authored class roster targets. These are additive to native Sims.
        internal const int BlightcallerRosterTarget = 12;
        internal const int EmberweaverRosterTarget = 12;
        internal const int LightbringerRosterTarget = 12;

        private const int StateVersion =
            1;

        private const string SaveDirectoryName =
            "FateOfTheFallen";

        private const string StateFileName =
            "fotf_sim_roster.json";

        private const string SimSaveDirectoryName =
            "SimPlayers";

        private static readonly FieldInfo ManagerDpsField =
            AccessTools.Field(
                typeof(SimPlayerMngr),
                "DPS");

        private static readonly MethodInfo ManagerCategorizeAllMethod =
            AccessTools.Method(
                typeof(SimPlayerMngr),
                "CategorizeAll");

        private static readonly List<FOTFSimPlayerDefinition> Definitions =
            new List<FOTFSimPlayerDefinition>
            {
                // ----------------------------------------------------
                // BLIGHTCALLER
                // ----------------------------------------------------
                Def(
                    "FOTF_SIM_BC_VEYDRAN",
                    BlightcallerClassName,
                    new[] { "Veydran", "Veydris", "Vaedran" },
                    "Male",
                    6,
                    "Chr_Hair_07",
                    1,
                    1,
                    2,
                    0),

                Def(
                    "FOTF_SIM_BC_MORITHE",
                    BlightcallerClassName,
                    new[] { "Morithe", "Morivra", "Moryth" },
                    "Female",
                    14,
                    "Chr_Hair_21",
                    2,
                    2,
                    3,
                    0),

                Def(
                    "FOTF_SIM_BC_KELVRAX",
                    BlightcallerClassName,
                    new[] { "Kelvrax", "Kelvarn", "Kelvren" },
                    "Male",
                    24,
                    "Chr_Hair_13",
                    0,
                    3,
                    2,
                    0),

                Def(
                    "FOTF_SIM_BC_SELYRA",
                    BlightcallerClassName,
                    new[] { "Selyra", "Selyth", "Selivra" },
                    "Female",
                    35,
                    "Chr_Hair_31",
                    3,
                    1,
                    1,
                    0),

                // Eight additional Blightcallers; original four IDs and
                // persisted names are kept intact across roster upgrades.
                Def(
                    "FOTF_SIM_BC_RAVETH",
                    BlightcallerClassName,
                    new[] { "Raveth", "Ravren", "Ravethan" },
                    "Male",
                    2,
                    "Chr_Hair_05",
                    0,
                    2,
                    1,
                    0),

                Def(
                    "FOTF_SIM_BC_NYSSARA",
                    BlightcallerClassName,
                    new[] { "Nyssara", "Nyssira", "Nysara" },
                    "Female",
                    8,
                    "Chr_Hair_18",
                    3,
                    1,
                    2,
                    0),

                Def(
                    "FOTF_SIM_BC_DRENVAR",
                    BlightcallerClassName,
                    new[] { "Drenvar", "Drenvor", "Drenvek" },
                    "Male",
                    11,
                    "Chr_Hair_11",
                    1,
                    3,
                    3,
                    0),

                Def(
                    "FOTF_SIM_BC_VAELITH",
                    BlightcallerClassName,
                    new[] { "Vaelith", "Vaelyra", "Veylith" },
                    "Female",
                    18,
                    "Chr_Hair_27",
                    2,
                    2,
                    1,
                    0),

                Def(
                    "FOTF_SIM_BC_KORITH",
                    BlightcallerClassName,
                    new[] { "Korith", "Koreth", "Koriven" },
                    "Male",
                    21,
                    "Chr_Hair_13",
                    0,
                    1,
                    2,
                    0),

                Def(
                    "FOTF_SIM_BC_MYRREN",
                    BlightcallerClassName,
                    new[] { "Myrren", "Myrrith", "Myressa" },
                    "Female",
                    27,
                    "Chr_Hair_31",
                    1,
                    3,
                    3,
                    0),

                Def(
                    "FOTF_SIM_BC_THALREK",
                    BlightcallerClassName,
                    new[] { "Thalrek", "Thalren", "Thalvar" },
                    "Male",
                    30,
                    "Chr_Hair_07",
                    2,
                    2,
                    1,
                    0),

                Def(
                    "FOTF_SIM_BC_ZYVESSA",
                    BlightcallerClassName,
                    new[] { "Zyvessa", "Zyverra", "Zyvelis" },
                    "Female",
                    33,
                    "Chr_Hair_21",
                    3,
                    1,
                    2,
                    0),

                // ----------------------------------------------------
                // EMBERWEAVER
                // ----------------------------------------------------
                Def(
                    "FOTF_SIM_EW_CALYRIX",
                    EmberweaverClassName,
                    new[] { "Calyrix", "Calyren", "Caldris" },
                    "Male",
                    6,
                    "Chr_Hair_05",
                    2,
                    2,
                    1,
                    0),

                Def(
                    "FOTF_SIM_EW_THESARA",
                    EmberweaverClassName,
                    new[] { "Thesara", "Therysa", "Thesira" },
                    "Female",
                    14,
                    "Chr_Hair_18",
                    3,
                    1,
                    1,
                    0),

                Def(
                    "FOTF_SIM_EW_BRANNOC",
                    EmberweaverClassName,
                    new[] { "Brannoc", "Branneth", "Branvek" },
                    "Male",
                    24,
                    "Chr_Hair_11",
                    1,
                    3,
                    2,
                    0),

                Def(
                    "FOTF_SIM_EW_ELYNDRA",
                    EmberweaverClassName,
                    new[] { "Elyndra", "Elvaris", "Elyvara" },
                    "Female",
                    35,
                    "Chr_Hair_27",
                    0,
                    2,
                    3,
                    0),

                // Eight additional Emberweavers; retain the existing four.
                Def(
                    "FOTF_SIM_EW_IGNIVAR",
                    EmberweaverClassName,
                    new[] { "Ignivar", "Igniren", "Ignaros" },
                    "Male",
                    2,
                    "Chr_Hair_07",
                    2,
                    2,
                    2,
                    0),

                Def(
                    "FOTF_SIM_EW_SERALYA",
                    EmberweaverClassName,
                    new[] { "Seralya", "Seralyn", "Serylia" },
                    "Female",
                    9,
                    "Chr_Hair_21",
                    1,
                    1,
                    3,
                    0),

                Def(
                    "FOTF_SIM_EW_PYRREN",
                    EmberweaverClassName,
                    new[] { "Pyrren", "Pyrvek", "Pyranor" },
                    "Male",
                    12,
                    "Chr_Hair_13",
                    3,
                    3,
                    1,
                    0),

                Def(
                    "FOTF_SIM_EW_CAELORA",
                    EmberweaverClassName,
                    new[] { "Caelora", "Caelira", "Caelyth" },
                    "Female",
                    18,
                    "Chr_Hair_31",
                    0,
                    2,
                    2,
                    0),

                Def(
                    "FOTF_SIM_EW_RHAVIK",
                    EmberweaverClassName,
                    new[] { "Rhavik", "Rhaelor", "Rhavren" },
                    "Male",
                    21,
                    "Chr_Hair_11",
                    1,
                    1,
                    3,
                    0),

                Def(
                    "FOTF_SIM_EW_ELITHRA",
                    EmberweaverClassName,
                    new[] { "Elithra", "Elaryn", "Elathra" },
                    "Female",
                    27,
                    "Chr_Hair_27",
                    2,
                    3,
                    1,
                    0),

                Def(
                    "FOTF_SIM_EW_VOLREN",
                    EmberweaverClassName,
                    new[] { "Volren", "Volric", "Voleran" },
                    "Male",
                    30,
                    "Chr_Hair_05",
                    0,
                    2,
                    2,
                    0),

                Def(
                    "FOTF_SIM_EW_SOLMIRA",
                    EmberweaverClassName,
                    new[] { "Solmira", "Solirra", "Solmina" },
                    "Female",
                    33,
                    "Chr_Hair_18",
                    3,
                    1,
                    3,
                    0),
                // LIGHTBRINGER — 12 authored support / healer SimPlayers.
                Def("FOTF_SIM_LB_ALDREN", LightbringerClassName,
                    new[] { "Aldren", "Aldrenen", "Aldrenis" }, "Male", 2, "Chr_Hair_07", 0, 1, 1, 0),
                Def("FOTF_SIM_LB_LIORA", LightbringerClassName,
                    new[] { "Liora", "Lioraen", "Liorais" }, "Female", 6, "Chr_Hair_18", 1, 2, 2, 0),
                Def("FOTF_SIM_LB_VAELOR", LightbringerClassName,
                    new[] { "Vaelor", "Vaeloren", "Vaeloris" }, "Male", 9, "Chr_Hair_05", 2, 3, 3, 0),
                Def("FOTF_SIM_LB_SERENYA", LightbringerClassName,
                    new[] { "Serenya", "Serenyaen", "Serenyais" }, "Female", 12, "Chr_Hair_21", 3, 0, 1, 0),
                Def("FOTF_SIM_LB_ELDRIC", LightbringerClassName,
                    new[] { "Eldric", "Eldricen", "Eldricis" }, "Male", 15, "Chr_Hair_11", 0, 1, 2, 0),
                Def("FOTF_SIM_LB_AURELIA", LightbringerClassName,
                    new[] { "Aureliah", "Aureliahen", "Aureliahis" }, "Female", 18, "Chr_Hair_27", 1, 2, 3, 0),
                Def("FOTF_SIM_LB_CAELEN", LightbringerClassName,
                    new[] { "Caelen", "Caelenen", "Caelenis" }, "Male", 21, "Chr_Hair_13", 2, 3, 1, 0),
                Def("FOTF_SIM_LB_ILYS", LightbringerClassName,
                    new[] { "Ilysara", "Ilysaraen", "Ilysarais" }, "Female", 24, "Chr_Hair_31", 3, 0, 2, 0),
                Def("FOTF_SIM_LB_ORION", LightbringerClassName,
                    new[] { "Orionis", "Orionisen", "Orionisis" }, "Male", 27, "Chr_Hair_07", 0, 1, 3, 0),
                Def("FOTF_SIM_LB_LYSARA", LightbringerClassName,
                    new[] { "Lysara", "Lysaraen", "Lysarais" }, "Female", 30, "Chr_Hair_21", 1, 2, 1, 0),
                Def("FOTF_SIM_LB_THAERON", LightbringerClassName,
                    new[] { "Thaeron", "Thaeronen", "Thaeronis" }, "Male", 33, "Chr_Hair_11", 2, 3, 2, 0),
                Def("FOTF_SIM_LB_SOLYRA", LightbringerClassName,
                    new[] { "Solyra", "Solyraen", "Solyrais" }, "Female", 35, "Chr_Hair_27", 3, 0, 3, 0)
            };

        private static readonly Dictionary<string, FOTFSimPlayerDefinition>
            DefinitionsById =
                new Dictionary<string, FOTFSimPlayerDefinition>(
                    StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, FOTFSimPlayerDefinition>
            DefinitionsByAssignedName =
                new Dictionary<string, FOTFSimPlayerDefinition>(
                    StringComparer.OrdinalIgnoreCase);

        private static RosterState _state;
        private static bool _initialized;
        private static bool _stateLoaded;
        private static bool _loggedRoster;
        private static bool _suppressPopulation;

        [Serializable]
        private sealed class RosterStateEntry
        {
            public string DefinitionId = string.Empty;
            public string AssignedName = string.Empty;
            // One-time migration flag. This must never re-guild a Sim
            // that deliberately leaves the original starter guild.
            public bool InitialGuildSeeded;
        }

        [Serializable]
        private sealed class RosterState
        {
            public int Version = StateVersion;
            public List<RosterStateEntry> Members =
                new List<RosterStateEntry>();
        }

        internal static IReadOnlyList<FOTFSimPlayerDefinition> All
        {
            get { return Definitions; }
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

        private static string StatePath
        {
            get
            {
                return Path.Combine(
                    SaveDirectory,
                    StateFileName);
            }
        }

        private static string SimSaveDirectory
        {
            get
            {
                return Path.Combine(
                    SaveDirectory,
                    SimSaveDirectoryName);
            }
        }

        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _suppressPopulation = false;

            BuildDefinitionIndex();
            LoadState();

            // The old implementation assigned Blightcaller to random native
            // SimPlayers. Retire that sidecar and restore any live legacy
            // assignment before the authored FOTF roster takes ownership.
            BlightcallerSimPlayers.RetireLegacyAssignments();

            SimPlayerMngr manager =
                GameData.SimMngr;

            if (manager != null &&
                manager.Sims != null &&
                manager.Sims.Count > 0)
            {
                EnsurePopulation(
                    manager);

                ReapplyActiveSimPlayers(
                    manager);

                // On a Lunaris hot reload Erenshor's initial CategorizeAll()
                // has already run. Re-run that native categorization once so
                // the new FOTF roster participates in normal role/group logic.
                if (ManagerCategorizeAllMethod != null)
                {
                    try
                    {
                        ManagerCategorizeAllMethod.Invoke(
                            manager,
                            null);
                    }
                    catch (Exception exception)
                    {
                        Plugin.LogWarning(
                            "FOTF SimPlayers: hot-reload role recategorization failed: " +
                            exception.Message);
                    }
                }
            }
        }

        internal static void Shutdown()
        {
            _suppressPopulation = true;

            SaveState();

            try
            {
                RemoveRuntimePopulation(
                    GameData.SimMngr);
            }
            catch (Exception exception)
            {
                Plugin.LogWarning(
                    "FOTF SimPlayers: runtime roster cleanup failed during unload: " +
                    exception.Message);
            }

            RemoveOwnedSaveCacheEntries();

            _initialized = false;
            _loggedRoster = false;
        }

        internal static void EnsurePopulation(
            SimPlayerMngr manager)
        {
            if (_suppressPopulation ||
                !FOTFContentOptions.Active.CustomSimPlayers ||
                manager == null ||
                manager.Sims == null)
            {
                return;
            }

            BuildDefinitionIndex();
            LoadState();

            EnsureClassContentReady();

            bool stateChanged = false;
            int created = 0;
            int restored = 0;

            for (int i = 0;
                 i < Definitions.Count;
                 i++)
            {
                FOTFSimPlayerDefinition definition =
                    Definitions[i];

                if (definition == null)
                {
                    continue;
                }

                string assignedName =
                    GetAssignedName(
                        definition.Id);

                if (string.IsNullOrEmpty(
                        assignedName))
                {
                    assignedName =
                        ChooseUnusedAuthoredName(
                            manager,
                            definition);

                    if (string.IsNullOrEmpty(
                            assignedName))
                    {
                        Plugin.LogError(
                            "FOTF SimPlayers: every authored name candidate for definition [" +
                            definition.Id +
                            "] is already used by a non-FOTF SimPlayer. This roster member was not created; no native Sim was modified.");

                        continue;
                    }

                    SetAssignedName(
                        definition.Id,
                        assignedName);

                    stateChanged = true;
                }

                SimPlayerTracking tracking =
                    FindTrackingByName(
                        manager,
                        assignedName);

                // A persisted FOTF name must never claim a native SimPlayer
                // that happens to appear with the same name on a later load.
                // Existing authored tracking is recognizable by both its
                // custom class and its FOTF-owned save file. Anything else is
                // treated as a collision and left completely untouched.
                if (tracking != null &&
                    !IsExistingOwnedTracking(
                        tracking,
                        definition))
                {
                    string collidedName =
                        assignedName;

                    string replacementName =
                        ChooseUnusedAuthoredName(
                            manager,
                            definition);

                    if (string.IsNullOrEmpty(
                            replacementName))
                    {
                        Plugin.LogError(
                            "FOTF SimPlayers: persisted authored name [" +
                            collidedName +
                            "] now belongs to a non-FOTF SimPlayer, and no fallback authored name is free for definition [" +
                            definition.Id +
                            "]. The native Sim was left untouched and this FOTF roster member was skipped.");

                        continue;
                    }

                    SetAssignedName(
                        definition.Id,
                        replacementName);

                    MigrateOwnedSaveName(
                        collidedName,
                        replacementName,
                        definition);

                    assignedName =
                        replacementName;

                    tracking =
                        null;

                    stateChanged =
                        true;

                    Plugin.LogWarning(
                        "FOTF SimPlayers: authored name collision detected for [" +
                        collidedName +
                        "]. Reassigned definition [" +
                        definition.Id +
                        "] to authored fallback [" +
                        replacementName +
                        "] without modifying the native SimPlayer.");
                }

                DefinitionsByAssignedName[
                    assignedName] =
                        definition;

                if (tracking == null)
                {
                    tracking =
                        CreateTracking(
                            manager,
                            definition,
                            assignedName);

                    if (tracking != null)
                    {
                        created++;
                    }
                }
                else
                {
                    ConfigureExistingTracking(
                        manager,
                        tracking,
                        definition);

                    restored++;
                }
            }

            ReindexPopulation(
                manager);

            if (stateChanged)
            {
                SaveState();
            }

            if (!_loggedRoster ||
                created > 0)
            {
                _loggedRoster = true;

                int blightcallers = CountRegisteredClassMembers(
                    manager, BlightcallerClassName);
                int emberweavers = CountRegisteredClassMembers(
                    manager, EmberweaverClassName);
                int lightbringers = CountRegisteredClassMembers(manager, LightbringerClassName);

                Plugin.LogInfo(
                    "FOTF SimPlayers: authored roster ready. Members=[" +
                    CountRegisteredMembers(manager) +
                    "] Blightcallers=[" + blightcallers + "/" +
                    BlightcallerRosterTarget +
                    "] Emberweavers=[" + emberweavers + "/" +
                    EmberweaverRosterTarget +
                    "] Lightbringers=[" + lightbringers + "/" + LightbringerRosterTarget +
                    "] Created=[" + created +
                    "] Restored=[" + restored +
                    "]. Native SimPlayers were not reassigned.");

                if (blightcallers != BlightcallerRosterTarget ||
                    emberweavers != EmberweaverRosterTarget ||
                    lightbringers != LightbringerRosterTarget)
                {
                    Plugin.LogWarning(
                        "FOTF SimPlayers: authored roster does not match expected " +
                        "12 Blightcallers + 12 Emberweavers + 12 Lightbringers. " +
                        "Check the preceding name collision/save warnings.");
                }
            }
        }

        internal static bool IsOwnedName(
            string simName)
        {
            FOTFSimPlayerDefinition definition;

            return TryGetDefinitionByAssignedName(
                simName,
                out definition);
        }

        internal static bool IsClassMember(
            string simName,
            string className)
        {
            FOTFSimPlayerDefinition definition;

            return
                TryGetDefinitionByAssignedName(
                    simName,
                    out definition) &&
                definition != null &&
                string.Equals(
                    definition.ClassName,
                    className,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryGetDefinitionForSim(
            SimPlayer sim,
            out FOTFSimPlayerDefinition definition)
        {
            definition = null;

            if (sim == null)
            {
                return false;
            }

            NPC npc =
                sim.GetThisNPC();

            return
                npc != null &&
                TryGetDefinitionByAssignedName(
                    npc.NPCName,
                    out definition);
        }

        internal static bool TryGetDefinitionByAssignedName(
            string simName,
            out FOTFSimPlayerDefinition definition)
        {
            definition = null;

            if (string.IsNullOrWhiteSpace(
                    simName))
            {
                return false;
            }

            BuildDefinitionIndex();
            LoadState();

            if (DefinitionsByAssignedName.TryGetValue(
                    simName,
                    out definition))
            {
                return definition != null;
            }

            if (_state == null ||
                _state.Members == null)
            {
                return false;
            }

            for (int i = 0;
                 i < _state.Members.Count;
                 i++)
            {
                RosterStateEntry entry =
                    _state.Members[i];

                if (entry == null ||
                    !string.Equals(
                        entry.AssignedName,
                        simName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (DefinitionsById.TryGetValue(
                        entry.DefinitionId,
                        out definition) &&
                    definition != null)
                {
                    DefinitionsByAssignedName[
                        entry.AssignedName] =
                            definition;

                    return true;
                }
            }

            definition = null;
            return false;
        }

        internal static void ApplyRuntimeClass(
            SimPlayer sim)
        {
            FOTFSimPlayerDefinition definition;

            if (!TryGetDefinitionForSim(
                    sim,
                    out definition) ||
                definition == null ||
                sim.MyStats == null)
            {
                return;
            }

            Class customClass =
                ResolveClass(
                    definition.ClassName);

            if (customClass == null)
            {
                return;
            }

            sim.MyStats.CharacterClass =
                customClass;

            NPC npc =
                sim.GetThisNPC();

            if (npc != null)
            {
                npc.NPCName =
                    GetAssignedName(
                        definition.Id);
            }

            SimPlayerTracking tracking =
                GetTrackingForSim(
                    sim);

            if (tracking != null)
            {
                tracking.ClassName =
                    definition.ClassName;
            }

            FOTFSimPlayerIdentity identity =
                sim.GetComponent<FOTFSimPlayerIdentity>();

            if (identity == null)
            {
                identity =
                    sim.gameObject.AddComponent<FOTFSimPlayerIdentity>();
            }

            identity.DefinitionId =
                definition.Id;

            identity.ClassName =
                definition.ClassName;

            identity.SimName =
                npc != null
                    ? npc.NPCName
                    : GetAssignedName(
                        definition.Id);

            try
            {
                sim.MyStats.CalcStats();
            }
            catch (Exception exception)
            {
                Plugin.LogWarning(
                    "FOTF SimPlayers: CalcStats failed while applying [" +
                    definition.ClassName +
                    "] to [" +
                    identity.SimName +
                    "]: " +
                    exception.Message);
            }
        }

        internal static void ReapplyActiveSimPlayers(
            SimPlayerMngr manager)
        {
            if (manager == null ||
                manager.ActiveSimInstances == null)
            {
                return;
            }

            SimPlayer[] active =
                manager.ActiveSimInstances.ToArray();

            for (int i = 0;
                 i < active.Length;
                 i++)
            {
                SimPlayer sim =
                    active[i];

                if (sim == null)
                {
                    continue;
                }

                FOTFSimPlayerDefinition definition;

                if (!TryGetDefinitionForSim(
                        sim,
                        out definition))
                {
                    continue;
                }

                ApplyRuntimeClass(
                    sim);

                try
                {
                    sim.LoadSimSkills();
                    sim.LoadSimSpells();
                }
                catch (Exception exception)
                {
                    Plugin.LogWarning(
                        "FOTF SimPlayers: skill/spell reconciliation failed for [" +
                        (definition != null
                            ? GetAssignedName(definition.Id)
                            : "unknown") +
                        "]: " +
                        exception.Message);
                }
            }
        }

        internal static void ApplyNativeFallback(
            SimPlayerSaveData data)
        {
            if (data == null ||
                !IsOwnedName(
                    data.NPCName))
            {
                return;
            }

            // Both current FOTF classes are Arcanist-derived for native save
            // compatibility. The custom class is restored after LoadSimData.
            data.War = false;
            FOTFSimPlayerDefinition authored;
            bool isLightbringer = TryGetDefinitionByAssignedName(data.NPCName, out authored) &&
                authored != null && string.Equals(authored.ClassName, LightbringerClassName,
                    StringComparison.OrdinalIgnoreCase);
            data.Arc = !isLightbringer;
            data.Dru = isLightbringer;
            data.Duel = false;
            data.Storm = false;
            data.Reav = false;
        }

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
                    manager) as List<string>;

            if (dps == null)
            {
                return;
            }

            for (int i = 0;
                 i < manager.Sims.Count;
                 i++)
            {
                SimPlayerTracking tracking =
                    manager.Sims[i];

                if (tracking == null ||
                    !IsOwnedName(
                        tracking.SimName))
                {
                    continue;
                }

                if (IsClassMember(tracking.SimName, LightbringerClassName))
                {
                    dps.Remove(tracking.SimName);
                    continue;
                }

                if (!dps.Contains(
                        tracking.SimName))
                {
                    dps.Add(
                        tracking.SimName);
                }
            }
        }

        internal static void AddGroupingDpsRoles(
            SimPlayerGrouping grouping)
        {
            if (grouping == null ||
                grouping.DPS == null ||
                GameData.GroupMembers == null)
            {
                return;
            }

            foreach (SimPlayerTracking member
                     in GameData.GroupMembers)
            {
                if (member == null ||
                    !IsOwnedName(
                        member.SimName))
                {
                    continue;
                }

                if (IsClassMember(member.SimName, LightbringerClassName))
                {
                    grouping.DPS.Remove(member);
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

        // Native healer lists vary between Erenshor versions. Resolve only a
        // correctly-typed, healer-named list; never put these Sims into DPS.
        private static FieldInfo FindHealerRoleField(Type type, Type element)
        {
            if (type == null) return null;
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.Name.IndexOf("heal", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    typeof(List<>).MakeGenericType(element).IsAssignableFrom(field.FieldType))
                    return field;
            }
            return null;
        }

        internal static void AddManagerHealerRoles(SimPlayerMngr manager)
        {
            if (manager == null || manager.Sims == null) return;
            FieldInfo field = FindHealerRoleField(typeof(SimPlayerMngr), typeof(string));
            List<string> healers = field != null ? field.GetValue(manager) as List<string> : null;
            if (healers == null) return;
            foreach (SimPlayerTracking tracking in manager.Sims)
                if (tracking != null && IsClassMember(tracking.SimName, LightbringerClassName) &&
                    !healers.Contains(tracking.SimName)) healers.Add(tracking.SimName);
        }

        internal static void AddGroupingHealerRoles(SimPlayerGrouping grouping)
        {
            if (grouping == null || GameData.GroupMembers == null) return;
            FieldInfo field = FindHealerRoleField(typeof(SimPlayerGrouping), typeof(SimPlayerTracking));
            List<SimPlayerTracking> healers = field != null ? field.GetValue(grouping) as List<SimPlayerTracking> : null;
            if (healers == null) return;
            foreach (SimPlayerTracking member in GameData.GroupMembers)
                if (member != null && IsClassMember(member.SimName, LightbringerClassName) &&
                    !healers.Contains(member)) healers.Add(member);
        }

        private static void RemoveRuntimePopulation(
            SimPlayerMngr manager)
        {
            if (manager == null ||
                manager.Sims == null)
            {
                return;
            }

            // Remove authored FOTF members from the player's active group.
            if (GameData.GroupMembers != null)
            {
                for (int i = 0;
                     i < GameData.GroupMembers.Length;
                     i++)
                {
                    SimPlayerTracking member =
                        GameData.GroupMembers[i];

                    if (member != null &&
                        IsOwnedName(
                            member.SimName))
                    {
                        GameData.GroupMembers[i] =
                            null;
                    }
                }
            }

            // Clear native raid slots that point at authored FOTF members.
            if (GameData.RaidManager != null &&
                GameData.RaidManager.Raiders != null)
            {
                foreach (RaidMemberSlot slot
                         in GameData.RaidManager.Raiders)
                {
                    if (slot == null ||
                        slot.AssignedSimTracking == null ||
                        !IsOwnedName(
                            slot.AssignedSimTracking.SimName))
                    {
                        continue;
                    }

                    slot.AssignedAvatar =
                        null;

                    slot.AssignedSimTracking =
                        null;
                }
            }

            // Destroy only FOTF-authored runtime avatars. Native avatars are
            // never touched by this cleanup.
            if (manager.ActiveSimInstances != null)
            {
                for (int i =
                        manager.ActiveSimInstances.Count - 1;
                     i >= 0;
                     i--)
                {
                    SimPlayer sim =
                        manager.ActiveSimInstances[i];

                    NPC npc =
                        sim != null
                            ? sim.GetThisNPC()
                            : null;

                    if (npc == null ||
                        !IsOwnedName(
                            npc.NPCName))
                    {
                        continue;
                    }

                    manager.ActiveSimInstances.RemoveAt(
                        i);

                    if (sim != null &&
                        sim.gameObject != null)
                    {
                        UnityEngine.Object.Destroy(
                            sim.gameObject);
                    }
                }
            }

            List<string> removedNames =
                new List<string>();

            for (int i =
                    manager.Sims.Count - 1;
                 i >= 0;
                 i--)
            {
                SimPlayerTracking tracking =
                    manager.Sims[i];

                if (tracking == null ||
                    !IsOwnedName(
                        tracking.SimName))
                {
                    continue;
                }

                removedNames.Add(
                    tracking.SimName);

                if (tracking.MyAvatar != null &&
                    tracking.MyAvatar.gameObject != null)
                {
                    UnityEngine.Object.Destroy(
                        tracking.MyAvatar.gameObject);
                }

                manager.Sims.RemoveAt(
                    i);

                if (manager.SimsInZones != null &&
                    i < manager.SimsInZones.Count)
                {
                    manager.SimsInZones.RemoveAt(
                        i);
                }
            }

            for (int i = 0;
                 i < removedNames.Count;
                 i++)
            {
                string name =
                    removedNames[i];

                if (manager.SimDict != null)
                {
                    manager.SimDict.Remove(
                        name);
                }

                if (manager.NamesUsed != null)
                {
                    manager.NamesUsed.Remove(
                        name);
                }
            }

            if (ManagerDpsField != null)
            {
                List<string> dps =
                    ManagerDpsField.GetValue(
                        manager) as List<string>;

                if (dps != null)
                {
                    for (int i =
                            dps.Count - 1;
                         i >= 0;
                         i--)
                    {
                        if (IsOwnedName(
                                dps[i]))
                        {
                            dps.RemoveAt(
                                i);
                        }
                    }
                }
            }

            FieldInfo healerField = FindHealerRoleField(typeof(SimPlayerMngr), typeof(string));
            List<string> healerNames = healerField != null
                ? healerField.GetValue(manager) as List<string> : null;
            if (healerNames != null)
                healerNames.RemoveAll(name => removedNames.Contains(name));

            ReindexPopulation(
                manager);

            if (removedNames.Count > 0)
            {
                Plugin.LogInfo(
                    "FOTF SimPlayers: removed [" +
                    removedNames.Count +
                    "] authored runtime roster members during unload. Native SimPlayers were unchanged.");
            }
        }

        private static void RemoveOwnedSaveCacheEntries()
        {
            if (SimPlayerDataManager.SimPlayerData == null)
            {
                return;
            }

            for (int i =
                    SimPlayerDataManager.SimPlayerData.Count - 1;
                 i >= 0;
                 i--)
            {
                SimPlayerSaveData data =
                    SimPlayerDataManager.SimPlayerData[i];

                if (data != null &&
                    IsOwnedName(
                        data.NPCName))
                {
                    SimPlayerDataManager.SimPlayerData.RemoveAt(
                        i);
                }
            }
        }


        private static void EnsureClassContentReady()
        {
            try
            {
                BlightcallerCatalog.EnsureClass();
                BlightcallerSimPlayers.ConfigureSpellsForSimPlayers();
            }
            catch (Exception exception)
            {
                Plugin.LogWarning(
                    "FOTF SimPlayers: Blightcaller content preparation failed: " +
                    exception.Message);
            }

            try
            {
                EmberweaverCatalog.TryEnsureClass();
                EmberweaverSimPlayers.ConfigureSpellsForSimPlayers();
            }
            catch (Exception exception)
            {
                Plugin.LogWarning(
                    "FOTF SimPlayers: Emberweaver content preparation failed: " +
                    exception.Message);
            }
            EnsureLightbringerContentReady();
        }

        private static void EnsureLightbringerContentReady()
        {
            try
            {
                LightbringerCatalog.TryEnsureClass();
                LightbringerSimPlayers.ConfigureSpellsForSimPlayers();
            }
            catch (Exception exception)
            {
                Plugin.LogWarning("FOTF SimPlayers: Lightbringer content preparation failed: " + exception.Message);
            }
        }

        private static Class ResolveClass(
            string className)
        {
            if (string.Equals(
                    className,
                    BlightcallerClassName,
                    StringComparison.OrdinalIgnoreCase))
            {
                BlightcallerCatalog.EnsureClass();
                return BlightcallerCatalog.BlightcallerClass;
            }

            if (string.Equals(
                    className,
                    EmberweaverClassName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return EmberweaverCatalog.TryEnsureClass();
            }

            if (string.Equals(className, LightbringerClassName,
                    StringComparison.OrdinalIgnoreCase))
                return LightbringerCatalog.TryEnsureClass();

            return null;
        }

        private static SimPlayerTracking CreateTracking(
            SimPlayerMngr manager,
            FOTFSimPlayerDefinition definition,
            string assignedName)
        {
            if (manager == null ||
                definition == null ||
                string.IsNullOrEmpty(
                    assignedName))
            {
                return null;
            }

            bool firstCreation;

            SimPlayerSaveData data =
                GetOrCreateOwnedSave(
                    definition,
                    assignedName,
                    out firstCreation);

            if (data == null)
            {
                return null;
            }

            ConfigureNativeSave(
                data,
                definition,
                assignedName,
                firstCreation);

            // Native generated Sims begin in Erenshor Young. Seed the
            // authored roster once, then preserve any later guild changes.
            SeedInitialGuildMembership(
                definition,
                data);

            string zone =
                ResolveSpawnZone(
                    data.MyLevel);

            SimPlayerTracking tracking =
                new SimPlayerTracking(
                    assignedName,
                    0f,
                    zone,
                    manager.Sims.Count);

            tracking.SimName =
                assignedName;

            tracking.ClassName =
                definition.ClassName;

            tracking.Gender =
                definition.Gender;

            tracking.Level =
                Mathf.Clamp(
                    data.MyLevel > 0
                        ? data.MyLevel
                        : definition.StartingLevel,
                    1,
                    35);

            tracking.TiedToSlot =
                12;

            tracking.HairIndex =
                definition.HairIndex;

            tracking.HairColor =
                definition.HairColorIndex;

            tracking.SkinColor =
                definition.SkinColorIndex;

            tracking.Personality =
                definition.Personality;

            tracking.BioIndex =
                definition.BioIndex;

            tracking.HideHelm =
                definition.HideHelm;

            tracking.OpinionOfPlayer =
                data.OpinionOfPlayer;

            tracking.KnowsPlayer =
                data.HasMetPlayer;

            tracking.FriendedBy =
                data.FriendedBy;

            tracking.Greed =
                data.Greed;

            tracking.Planars =
                data.PlanarStones;

            tracking.Sivaks =
                data.Sivakruxes;

            tracking.GuildID =
                data.GuildID;

            tracking.IsGuildLeader =
                data.IsGuildLeader;

            tracking.ATeam =
                data.ATeam;

            tracking.GearScore =
                data.GearScore;

            tracking.CurScene =
                zone;

            tracking.online =
                1;

            manager.Sims.Add(
                tracking);

            EnsureZoneIndex(
                manager,
                tracking.simIndex,
                zone);

            if (manager.NamesUsed != null &&
                !manager.NamesUsed.Contains(
                    assignedName))
            {
                manager.NamesUsed.Add(
                    assignedName);
            }

            if (manager.SimDict != null)
            {
                manager.SimDict[
                    assignedName] =
                        tracking;
            }

            SaveOwnedData(
                data);

            return tracking;
        }

        private static void ConfigureExistingTracking(
            SimPlayerMngr manager,
            SimPlayerTracking tracking,
            FOTFSimPlayerDefinition definition)
        {
            if (manager == null ||
                tracking == null ||
                definition == null)
            {
                return;
            }

            tracking.ClassName =
                definition.ClassName;

            tracking.Gender =
                definition.Gender;

            tracking.TiedToSlot =
                12;

            tracking.HairIndex =
                definition.HairIndex;

            tracking.HairColor =
                definition.HairColorIndex;

            tracking.SkinColor =
                definition.SkinColorIndex;

            tracking.Personality =
                definition.Personality;

            tracking.BioIndex =
                definition.BioIndex;

            tracking.HideHelm =
                definition.HideHelm;

            if (tracking.Level <= 0)
            {
                tracking.Level =
                    definition.StartingLevel;
            }

            tracking.Level =
                Mathf.Clamp(
                    tracking.Level,
                    1,
                    35);

            if (string.IsNullOrEmpty(
                    tracking.CurScene))
            {
                tracking.CurScene =
                    ResolveSpawnZone(
                        tracking.Level);
            }

            EnsureZoneIndex(
                manager,
                tracking.simIndex,
                tracking.CurScene);

            if (manager.NamesUsed != null &&
                !manager.NamesUsed.Contains(
                    tracking.SimName))
            {
                manager.NamesUsed.Add(
                    tracking.SimName);
            }

            if (manager.SimDict != null)
            {
                manager.SimDict[
                    tracking.SimName] =
                        tracking;
            }

            SimPlayerSaveData data =
                LoadOwnedSave(
                    tracking.SimName);

            if (data != null)
            {
                ConfigureNativeSave(
                    data,
                    definition,
                    tracking.SimName,
                    false);

                bool seeded = SeedInitialGuildMembership(
                    definition,
                    data);
                if (seeded ||
                    (string.IsNullOrEmpty(tracking.GuildID) &&
                     !string.IsNullOrEmpty(data.GuildID)))
                {
                    // An already-seeded Sim can enter a session with the
                    // tracking.guild ID empty even though its owned save has
                    // a guild. Restore only that missing tracking field; do
                    // NOT re-guild Sims who intentionally left or switched.
                    tracking.GuildID = data.GuildID;
                }

                SaveOwnedData(
                    data);
            }
        }

        internal static SimPlayerSaveData GetOwnedSave(
            string simName)
        {
            FOTFSimPlayerDefinition definition;

            if (!TryGetDefinitionByAssignedName(
                    simName,
                    out definition) ||
                definition == null)
            {
                return null;
            }

            bool firstCreation;

            return GetOrCreateOwnedSave(
                definition,
                simName,
                out firstCreation);
        }

        internal static bool SaveOwnedData(
            SimPlayerSaveData data)
        {
            if (data == null ||
                !IsOwnedName(
                    data.NPCName))
            {
                return false;
            }

            try
            {
                ApplyNativeFallback(
                    data);

                Directory.CreateDirectory(
                    SimSaveDirectory);

                string path =
                    GetOwnedSavePath(
                        data.NPCName);

                string temporary =
                    path +
                    ".tmp";

                string json =
                    JsonUtility.ToJson(
                        data,
                        true);

                File.WriteAllText(
                    temporary,
                    json);

                if (File.Exists(
                        path))
                {
                    File.Delete(
                        path);
                }

                File.Move(
                    temporary,
                    path);

                CacheOwnedSave(
                    data);

                return true;
            }
            catch (Exception exception)
            {
                Plugin.LogWarning(
                    "FOTF SimPlayers: owned save failed for [" +
                    data.NPCName +
                    "]: " +
                    exception.Message);

                return false;
            }
        }

        private static SimPlayerSaveData GetOrCreateOwnedSave(
            FOTFSimPlayerDefinition definition,
            string assignedName,
            out bool firstCreation)
        {
            firstCreation =
                false;

            SimPlayerSaveData existing =
                LoadOwnedSave(
                    assignedName);

            if (existing != null)
            {
                return existing;
            }

            firstCreation =
                true;

            SimPlayerSaveData created =
                new SimPlayerSaveData(
                    assignedName,
                    definition != null
                        ? definition.StartingLevel
                        : 1,
                    new List<Item>(),
                    40f);

            created.Year =
                DateTime.Now.Year -
                1;

            created.Day =
                DateTime.Now.DayOfYear;

            created.FriendedBy =
                -1;

            if (definition != null)
            {
                ConfigureNativeSave(
                    created,
                    definition,
                    assignedName,
                    true);
            }

            SaveOwnedData(
                created);

            return created;
        }

        private static SimPlayerSaveData LoadOwnedSave(
            string simName)
        {
            if (string.IsNullOrWhiteSpace(
                    simName))
            {
                return null;
            }

            try
            {
                string path =
                    GetOwnedSavePath(
                        simName);

                if (!File.Exists(
                        path))
                {
                    return null;
                }

                string json =
                    File.ReadAllText(
                        path);

                if (string.IsNullOrWhiteSpace(
                        json))
                {
                    return null;
                }

                SimPlayerSaveData data =
                    JsonUtility.FromJson<SimPlayerSaveData>(
                        json);

                if (data != null)
                {
                    CacheOwnedSave(
                        data);
                }

                return data;
            }
            catch (Exception exception)
            {
                Plugin.LogWarning(
                    "FOTF SimPlayers: owned save load failed for [" +
                    simName +
                    "]: " +
                    exception.Message);

                return null;
            }
        }

        private static string GetOwnedSavePath(
            string simName)
        {
            return Path.Combine(
                SimSaveDirectory,
                "Sims" +
                simName +
                ".json");
        }

        private static void CacheOwnedSave(
            SimPlayerSaveData data)
        {
            if (data == null ||
                SimPlayerDataManager.SimPlayerData == null)
            {
                return;
            }

            for (int i =
                    SimPlayerDataManager.SimPlayerData.Count - 1;
                 i >= 0;
                 i--)
            {
                SimPlayerSaveData cached =
                    SimPlayerDataManager.SimPlayerData[i];

                if (cached != null &&
                    string.Equals(
                        cached.NPCName,
                        data.NPCName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    SimPlayerDataManager.SimPlayerData[i] =
                        data;

                    return;
                }
            }

            SimPlayerDataManager.SimPlayerData.Add(
                data);
        }


        private static bool SeedInitialGuildMembership(
            FOTFSimPlayerDefinition definition,
            SimPlayerSaveData data)
        {
            if (definition == null ||
                data == null ||
                GameData.GuildManager == null ||
                GameData.GuildManager.ErenshorYoung == null ||
                string.IsNullOrEmpty(
                    GameData.GuildManager.ErenshorYoung.Id))
            {
                return false;
            }

            LoadState();
            if (_state == null || _state.Members == null)
            {
                return false;
            }

            foreach (RosterStateEntry entry in _state.Members)
            {
                if (entry == null ||
                    !string.Equals(
                        entry.DefinitionId,
                        definition.Id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entry.InitialGuildSeeded)
                {
                    return false;
                }

                if (string.IsNullOrEmpty(data.GuildID))
                {
                    data.GuildID =
                        GameData.GuildManager.ErenshorYoung.Id;
                }

                // Explicitly record migration so a later intentional guild
                // departure is not undone by a FOTF reload.
                entry.InitialGuildSeeded = true;
                SaveOwnedData(data);
                SaveState();
                return true;
            }

            return false;
        }

        private static void ConfigureNativeSave(
            SimPlayerSaveData data,
            FOTFSimPlayerDefinition definition,
            string assignedName,
            bool firstCreation)
        {
            if (data == null ||
                definition == null)
            {
                return;
            }

            data.NPCName =
                assignedName;

            if (data.MyLevel <= 0)
            {
                data.MyLevel =
                    definition.StartingLevel;
            }

            data.MyLevel =
                Mathf.Clamp(
                    data.MyLevel,
                    1,
                    35);

            data.Male =
                string.Equals(
                    definition.Gender,
                    "Male",
                    StringComparison.OrdinalIgnoreCase)
                    ? 1
                    : 0;

            data.TiedToSlot =
                12;

            data.HairColorIndex =
                definition.HairColorIndex;

            data.SkinColorIndex =
                definition.SkinColorIndex;

            data.hairName =
                definition.HairIndex;

            data.PersonalityType =
                definition.Personality;

            data.BioIndex =
                definition.BioIndex;

            data.HideHelm =
                definition.HideHelm;

            if (data.Greed <= 1f)
            {
                data.Greed =
                    2f;
            }

            if (firstCreation)
            {
                data.newlyGenerated =
                    true;
            }

            ApplyNativeFallback(
                data);
        }

        private static string ResolveSpawnZone(
            int level)
        {
            try
            {
                string zone =
                    ZoneAtlas.FindSuitableSpawnZone(
                        Mathf.Clamp(
                            level,
                            1,
                            35));

                if (!string.IsNullOrEmpty(
                        zone))
                {
                    return zone;
                }
            }
            catch
            {
            }

            return "Azure";
        }

        private static void EnsureZoneIndex(
            SimPlayerMngr manager,
            int index,
            string zone)
        {
            if (manager == null ||
                manager.SimsInZones == null ||
                index < 0)
            {
                return;
            }

            while (manager.SimsInZones.Count <=
                   index)
            {
                manager.SimsInZones.Add(
                    zone ?? string.Empty);
            }

            manager.SimsInZones[index] =
                zone ?? string.Empty;
        }

        private static void ReindexPopulation(
            SimPlayerMngr manager)
        {
            if (manager == null ||
                manager.Sims == null)
            {
                return;
            }

            for (int i = 0;
                 i < manager.Sims.Count;
                 i++)
            {
                SimPlayerTracking tracking =
                    manager.Sims[i];

                if (tracking == null)
                {
                    continue;
                }

                tracking.simIndex =
                    i;

                EnsureZoneIndex(
                    manager,
                    i,
                    tracking.CurScene);

                if (tracking.MyAvatar != null)
                {
                    tracking.MyAvatar.myIndex =
                        i;
                }

                if (manager.SimDict != null &&
                    !string.IsNullOrEmpty(
                        tracking.SimName))
                {
                    manager.SimDict[
                        tracking.SimName] =
                            tracking;
                }
            }
        }

        private static SimPlayerTracking GetTrackingForSim(
            SimPlayer sim)
        {
            if (sim == null ||
                GameData.SimMngr == null ||
                GameData.SimMngr.Sims == null)
            {
                return null;
            }

            if (sim.myIndex >= 0 &&
                sim.myIndex <
                    GameData.SimMngr.Sims.Count)
            {
                SimPlayerTracking indexed =
                    GameData.SimMngr.Sims[
                        sim.myIndex];

                if (indexed != null)
                {
                    return indexed;
                }
            }

            NPC npc =
                sim.GetThisNPC();

            return npc == null
                ? null
                : FindTrackingByName(
                    GameData.SimMngr,
                    npc.NPCName);
        }

        private static SimPlayerTracking FindTrackingByName(
            SimPlayerMngr manager,
            string name)
        {
            if (manager == null ||
                manager.Sims == null ||
                string.IsNullOrEmpty(
                    name))
            {
                return null;
            }

            for (int i = 0;
                 i < manager.Sims.Count;
                 i++)
            {
                SimPlayerTracking tracking =
                    manager.Sims[i];

                if (tracking != null &&
                    string.Equals(
                        tracking.SimName,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return tracking;
                }
            }

            return null;
        }

        private static bool IsExistingOwnedTracking(
            SimPlayerTracking tracking,
            FOTFSimPlayerDefinition definition)
        {
            if (tracking == null ||
                definition == null ||
                string.IsNullOrWhiteSpace(
                    tracking.SimName) ||
                !string.Equals(
                    tracking.ClassName,
                    definition.ClassName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                return File.Exists(
                    GetOwnedSavePath(
                        tracking.SimName));
            }
            catch
            {
                return false;
            }
        }

        private static void MigrateOwnedSaveName(
            string oldName,
            string newName,
            FOTFSimPlayerDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(
                    oldName) ||
                string.IsNullOrWhiteSpace(
                    newName) ||
                string.Equals(
                    oldName,
                    newName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                string oldPath =
                    GetOwnedSavePath(
                        oldName);

                if (!File.Exists(
                        oldPath))
                {
                    return;
                }

                string json =
                    File.ReadAllText(
                        oldPath);

                SimPlayerSaveData data =
                    string.IsNullOrWhiteSpace(
                        json)
                        ? null
                        : JsonUtility.FromJson<SimPlayerSaveData>(
                            json);

                if (data == null)
                {
                    return;
                }

                data.NPCName =
                    newName;

                if (definition != null)
                {
                    ConfigureNativeSave(
                        data,
                        definition,
                        newName,
                        false);
                }

                if (SaveOwnedData(
                        data))
                {
                    try
                    {
                        File.Delete(
                            oldPath);
                    }
                    catch (Exception exception)
                    {
                        Plugin.LogWarning(
                            "FOTF SimPlayers: old owned save [" +
                            oldName +
                            "] could not be removed after collision migration: " +
                            exception.Message);
                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.LogWarning(
                    "FOTF SimPlayers: could not migrate owned save from [" +
                    oldName +
                    "] to [" +
                    newName +
                    "]: " +
                    exception.Message);
            }
        }

        private static int CountRegisteredMembers(
            SimPlayerMngr manager)
        {
            if (manager == null ||
                manager.Sims == null)
            {
                return 0;
            }

            int count =
                0;

            for (int i = 0;
                 i < manager.Sims.Count;
                 i++)
            {
                SimPlayerTracking tracking =
                    manager.Sims[i];

                if (tracking != null &&
                    IsOwnedName(
                        tracking.SimName))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountRegisteredClassMembers(
            SimPlayerMngr manager,
            string className)
        {
            if (manager == null || manager.Sims == null)
                return 0;

            int count = 0;
            for (int i = 0; i < manager.Sims.Count; i++)
            {
                SimPlayerTracking tracking = manager.Sims[i];
                if (tracking == null)
                    continue;

                FOTFSimPlayerDefinition definition;
                if (TryGetDefinitionByAssignedName(tracking.SimName,
                        out definition) &&
                    definition != null &&
                    string.Equals(definition.ClassName, className,
                        StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private static string ChooseUnusedAuthoredName(
            SimPlayerMngr manager,
            FOTFSimPlayerDefinition definition)
        {
            if (definition == null ||
                definition.NameCandidates == null)
            {
                return null;
            }

            HashSet<string> reserved =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            if (_state != null &&
                _state.Members != null)
            {
                for (int i = 0;
                     i < _state.Members.Count;
                     i++)
                {
                    RosterStateEntry entry =
                        _state.Members[i];

                    if (entry != null &&
                        !string.IsNullOrEmpty(
                            entry.AssignedName) &&
                        !string.Equals(
                            entry.DefinitionId,
                            definition.Id,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        reserved.Add(
                            entry.AssignedName);
                    }
                }
            }

            for (int i = 0;
                 i < definition.NameCandidates.Length;
                 i++)
            {
                string candidate =
                    definition.NameCandidates[i];

                if (string.IsNullOrWhiteSpace(
                        candidate) ||
                    reserved.Contains(
                        candidate))
                {
                    continue;
                }

                if (FindTrackingByName(
                        manager,
                        candidate) != null)
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private static void BuildDefinitionIndex()
        {
            if (DefinitionsById.Count ==
                Definitions.Count)
            {
                return;
            }

            DefinitionsById.Clear();

            for (int i = 0;
                 i < Definitions.Count;
                 i++)
            {
                FOTFSimPlayerDefinition definition =
                    Definitions[i];

                if (definition == null ||
                    string.IsNullOrEmpty(
                        definition.Id))
                {
                    continue;
                }

                if (DefinitionsById.ContainsKey(
                        definition.Id))
                {
                    Plugin.LogError(
                        "FOTF SimPlayers: duplicate definition ID [" +
                        definition.Id +
                        "].");

                    continue;
                }

                DefinitionsById[
                    definition.Id] =
                        definition;
            }
        }

        private static void LoadState()
        {
            if (_stateLoaded)
            {
                return;
            }

            _stateLoaded = true;
            _state =
                new RosterState();

            DefinitionsByAssignedName.Clear();

            try
            {
                if (File.Exists(
                        StatePath))
                {
                    string json =
                        File.ReadAllText(
                            StatePath);

                    RosterState loaded =
                        JsonUtility.FromJson<RosterState>(
                            json);

                    if (loaded != null)
                    {
                        _state =
                            loaded;
                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.LogWarning(
                    "FOTF SimPlayers: roster state could not be loaded; a new state will be used: " +
                    exception.Message);
            }

            if (_state.Members == null)
            {
                _state.Members =
                    new List<RosterStateEntry>();
            }

            _state.Version =
                StateVersion;

            for (int i = 0;
                 i < _state.Members.Count;
                 i++)
            {
                RosterStateEntry entry =
                    _state.Members[i];

                FOTFSimPlayerDefinition definition;

                if (entry == null ||
                    string.IsNullOrEmpty(
                        entry.AssignedName) ||
                    !DefinitionsById.TryGetValue(
                        entry.DefinitionId,
                        out definition) ||
                    definition == null)
                {
                    continue;
                }

                DefinitionsByAssignedName[
                    entry.AssignedName] =
                        definition;
            }
        }

        private static void SaveState()
        {
            if (!_stateLoaded ||
                _state == null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(
                    SaveDirectory);

                string json =
                    JsonUtility.ToJson(
                        _state,
                        true);

                string temporary =
                    StatePath +
                    ".tmp";

                File.WriteAllText(
                    temporary,
                    json);

                if (File.Exists(
                        StatePath))
                {
                    File.Delete(
                        StatePath);
                }

                File.Move(
                    temporary,
                    StatePath);
            }
            catch (Exception exception)
            {
                Plugin.LogWarning(
                    "FOTF SimPlayers: roster state save failed: " +
                    exception.Message);
            }
        }

        private static string GetAssignedName(
            string definitionId)
        {
            LoadState();

            if (_state == null ||
                _state.Members == null ||
                string.IsNullOrEmpty(
                    definitionId))
            {
                return null;
            }

            for (int i = 0;
                 i < _state.Members.Count;
                 i++)
            {
                RosterStateEntry entry =
                    _state.Members[i];

                if (entry != null &&
                    string.Equals(
                        entry.DefinitionId,
                        definitionId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return entry.AssignedName;
                }
            }

            return null;
        }

        private static void SetAssignedName(
            string definitionId,
            string assignedName)
        {
            LoadState();

            if (_state == null ||
                string.IsNullOrEmpty(
                    definitionId) ||
                string.IsNullOrEmpty(
                    assignedName))
            {
                return;
            }

            for (int i = 0;
                 i < _state.Members.Count;
                 i++)
            {
                RosterStateEntry entry =
                    _state.Members[i];

                if (entry != null &&
                    string.Equals(
                        entry.DefinitionId,
                        definitionId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    string previousName =
                        entry.AssignedName;

                    entry.AssignedName =
                        assignedName;

                    if (!string.IsNullOrEmpty(
                            previousName) &&
                        !string.Equals(
                            previousName,
                            assignedName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        FOTFSimPlayerDefinition previousDefinition;

                        if (DefinitionsByAssignedName.TryGetValue(
                                previousName,
                                out previousDefinition) &&
                            previousDefinition != null &&
                            string.Equals(
                                previousDefinition.Id,
                                definitionId,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            DefinitionsByAssignedName.Remove(
                                previousName);
                        }
                    }

                    FOTFSimPlayerDefinition existingDefinition;

                    if (DefinitionsById.TryGetValue(
                            definitionId,
                            out existingDefinition) &&
                        existingDefinition != null)
                    {
                        DefinitionsByAssignedName[
                            assignedName] =
                                existingDefinition;
                    }

                    return;
                }
            }

            _state.Members.Add(
                new RosterStateEntry
                {
                    DefinitionId = definitionId,
                    AssignedName = assignedName
                });

            FOTFSimPlayerDefinition definition;

            if (DefinitionsById.TryGetValue(
                    definitionId,
                    out definition) &&
                definition != null)
            {
                DefinitionsByAssignedName[
                    assignedName] =
                        definition;
            }
        }

        private static FOTFSimPlayerDefinition Def(
            string id,
            string className,
            string[] nameCandidates,
            string gender,
            int startingLevel,
            string hairIndex,
            int hairColorIndex,
            int skinColorIndex,
            int personality,
            int bioIndex)
        {
            return new FOTFSimPlayerDefinition
            {
                Id = id,
                ClassName = className,
                NameCandidates = nameCandidates,
                Gender = gender,
                StartingLevel = startingLevel,
                HairIndex = hairIndex,
                HairColorIndex = hairColorIndex,
                SkinColorIndex = skinColorIndex,
                Personality = personality,
                BioIndex = bioIndex,
                HideHelm = 0
            };
        }
    }

    internal sealed class FOTFSimPlayerIdentity : MonoBehaviour
    {
        internal string DefinitionId;
        internal string ClassName;
        internal string SimName;
    }
}
