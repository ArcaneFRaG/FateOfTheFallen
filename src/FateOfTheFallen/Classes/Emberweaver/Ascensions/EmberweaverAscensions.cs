using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Emberweaver class-specific Ascensions.
    ///
    /// This deliberately mirrors BlightcallerAscensions: the Ascension objects
    /// live in SkillDB.AscensionDatabase, ranks remain in UseSkill.MyAscensions,
    /// and the class tab is populated by the matching AAScreen patches.
    /// </summary>
    internal static class EmberweaverAscensions
    {
        internal const string ThermalInertiaId =
            "CS_EW_AA_THERMAL_INERTIA";

        internal const string IncendiaryPrecisionId =
            "CS_EW_AA_INCENDIARY_PRECISION";

        internal const string ControlledBurnId =
            "CS_EW_AA_CONTROLLED_BURN";

        internal const string RekindlingId =
            "CS_EW_AA_REKINDLING";

        internal const string FeedingTheWildfireId =
            "CS_EW_AA_FEEDING_THE_WILDFIRE";

        private const int MaxRank = 3;
        private const int ExpectedCount = 5;

        private static readonly List<Ascension> Registered =
            new List<Ascension>();

        private static SkillDB _registeredDatabase;

        internal static IReadOnlyList<Ascension> All
        {
            get { return Registered; }
        }

        internal static bool Register()
        {
            SkillDB database =
                GameData.SkillDatabase;

            if (database == null ||
                database.AscensionDatabase == null)
            {
                return false;
            }

            if (database == _registeredDatabase &&
                Registered.Count == ExpectedCount &&
                Registered.All(
                    value =>
                        value != null &&
                        database.GetAscensionByID(value.Id) == value))
            {
                return true;
            }

            List<Ascension> nativeAscensions =
                database.AscensionDatabase
                    .Where(
                        value =>
                            value != null &&
                            !IsCustomId(value.Id))
                    .ToList();

            Registered.Clear();

            Registered.Add(
                Create(
                    ThermalInertiaId,
                    "Thermal Inertia",
                    "Heat decays more slowly and each Heat stack increases direct Emberweaver spell damage by an additional 0.5% per rank.",
                    5));

            Registered.Add(
                Create(
                    IncendiaryPrecisionId,
                    "Incendiary Precision",
                    "Increases Emberweaver spell critical chance by 3 percentage points per rank. Critical spell damage becomes 160% / 170% / 180% at ranks 1 / 2 / 3.",
                    5));

            Registered.Add(
                Create(
                    ControlledBurnId,
                    "Controlled Burn",
                    "Scorch-family spells deal 10% more direct damage per rank and accelerate an additional 5 percentage points of stored Wildfire per rank.",
                    5));

            Registered.Add(
                Create(
                    RekindlingId,
                    "Rekindling",
                    "After Combust, your next 1 / 2 / 3 damaging Emberweaver spells generate 1 additional Heat and deal 10% / 20% / 30% more direct damage.",
                    5));

            Registered.Add(
                Create(
                    FeedingTheWildfireId,
                    "Feeding the Wildfire",
                    "Your direct Emberweaver spells store an additional 5 percentage points of their final damage in Wildfire per rank.",
                    5));

            nativeAscensions.AddRange(
                Registered);

            database.AscensionDatabase =
                nativeAscensions.ToArray();

            _registeredDatabase =
                database;

            return true;
        }

        private static Ascension Create(
            string id,
            string name,
            string description,
            int weight)
        {
            Ascension ascension =
                ScriptableObject.CreateInstance<Ascension>();

            ascension.name =
                "CS EW AA - " + name;

            ascension.Id =
                id;

            ascension.SkillName =
                name;

            ascension.SkillDesc =
                description;

            ascension.MaxRank =
                MaxRank;

            ascension.SimPlayerWeight =
                weight;

            // Custom-class display is supplied by our AAScreen patch, matching
            // the Blightcaller implementation.
            ascension.UsedBy =
                (Ascension.Class)7;

            ascension.hideFlags =
                HideFlags.HideAndDontSave;

            return ascension;
        }

        internal static bool IsCustomId(
            string id)
        {
            return
                !string.IsNullOrEmpty(id) &&
                id.StartsWith(
                    "CS_EW_AA_",
                    StringComparison.Ordinal);
        }

        internal static int GetRank(
            UseSkill skills,
            string customId)
        {
            if (skills == null ||
                skills.MyAscensions == null ||
                string.IsNullOrEmpty(customId))
            {
                return 0;
            }

            AscensionSkillEntry entry =
                skills.MyAscensions.FirstOrDefault(
                    value =>
                        value != null &&
                        string.Equals(
                            value.id,
                            customId,
                            StringComparison.Ordinal));

            Ascension ascension =
                Registered.FirstOrDefault(
                    value =>
                        value != null &&
                        string.Equals(
                            value.Id,
                            customId,
                            StringComparison.Ordinal));

            int maxRank =
                ascension != null
                    ? ascension.MaxRank
                    : MaxRank;

            int rank =
                entry != null
                    ? entry.level
                    : 0;

            if (entry != null &&
                rank > maxRank &&
                IsEmberweaverSkills(skills))
            {
                int refund =
                    rank - maxRank;

                entry.level =
                    maxRank;

                skills.AscensionPoints +=
                    refund;

                rank =
                    maxRank;
            }

            return Mathf.Clamp(
                rank,
                0,
                maxRank);
        }

        internal static int GetPlayerRank(
            string customId)
        {
            PlayerControl playerControl =
                GameData.PlayerControl;

            if (playerControl == null ||
                playerControl.Myself == null)
            {
                return 0;
            }

            return GetRank(
                playerControl.Myself.MySkills,
                customId);
        }

        internal static bool IsEmberweaverSkills(
            UseSkill skills)
        {
            if (skills == null)
            {
                return false;
            }

            Stats stats =
                skills.GetComponent<Stats>();

            return
                stats != null &&
                EmberweaverCatalog.IsEmberweaverClass(
                    stats.CharacterClass);
        }

        internal static bool IsEmberweaverPlayer()
        {
            PlayerControl playerControl =
                GameData.PlayerControl;

            if (playerControl == null ||
                playerControl.Myself == null ||
                playerControl.Myself.MyStats == null)
            {
                return false;
            }

            return EmberweaverCatalog.IsEmberweaverClass(
                playerControl.Myself.MyStats.CharacterClass);
        }

        internal static float GetHeatDamagePerStack(
            UseSkill skills)
        {
            int rank =
                IsEmberweaverSkills(skills)
                    ? GetRank(skills, ThermalInertiaId)
                    : 0;

            return 0.03f +
                   (rank * 0.005f);
        }

        internal static float GetHeatDecaySeconds(
            UseSkill skills)
        {
            int rank =
                IsEmberweaverSkills(skills)
                    ? GetRank(skills, ThermalInertiaId)
                    : 0;

            switch (rank)
            {
                case 1:
                    return 2.5f;

                case 2:
                    return 3.0f;

                case 3:
                    return 3.5f;

                default:
                    return 2.0f;
            }
        }

        internal static float GetSpellCriticalChance(
            UseSkill skills)
        {
            int rank =
                IsEmberweaverSkills(skills)
                    ? GetRank(skills, IncendiaryPrecisionId)
                    : 0;

            return EmberweaverPassiveRuntime.BaseSpellCriticalChance +
                   (rank * 0.03f);
        }

        internal static float GetSpellCriticalDamageMultiplier(
            UseSkill skills)
        {
            int rank =
                IsEmberweaverSkills(skills)
                    ? GetRank(skills, IncendiaryPrecisionId)
                    : 0;

            return 1.50f +
                   (rank * 0.10f);
        }

        internal static float GetControlledBurnDamageMultiplier(
            UseSkill skills)
        {
            int rank =
                IsEmberweaverSkills(skills)
                    ? GetRank(skills, ControlledBurnId)
                    : 0;

            return 1f +
                   (rank * 0.10f);
        }

        internal static float GetControlledBurnPulseBonus(
            UseSkill skills)
        {
            int rank =
                IsEmberweaverSkills(skills)
                    ? GetRank(skills, ControlledBurnId)
                    : 0;

            return rank * 0.05f;
        }

        internal static int GetRekindlingRank(
            UseSkill skills)
        {
            return IsEmberweaverSkills(skills)
                ? GetRank(skills, RekindlingId)
                : 0;
        }

        internal static float GetFeedingTheWildfireBonus(
            UseSkill skills)
        {
            int rank =
                IsEmberweaverSkills(skills)
                    ? GetRank(skills, FeedingTheWildfireId)
                    : 0;

            return rank * 0.05f;
        }
    }
}
