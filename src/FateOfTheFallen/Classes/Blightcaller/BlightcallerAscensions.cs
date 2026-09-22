using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class BlightcallerAscensions
    {
        // ============================================================
        // IDS
        // ============================================================

        internal const string PotentAfflictionId =
            "CS_BLC_AA_POTENT_AFFLICTION";

        internal const string AcceleratedDecayId =
            "CS_BLC_AA_ACCELERATED_DECAY";

        internal const string ExtendedAfflictionId =
            "CS_BLC_AA_EXTENDED_AFFLICTION";

        internal const string PestilentRejuvenationId =
            "CS_BLC_AA_PESTILENT_REJUVENATION";


        // ============================================================
        // CONFIGURATION
        // ============================================================

        private const int PotentAfflictionMaxRank = 3;
        private const int AcceleratedDecayMaxRank = 3;
        private const int ExtendedAfflictionMaxRank = 3;
        private const int PestilentRejuvinationMaxRank = 3;

        private const int ExpectedCount = 4;


        // ============================================================
        // REGISTRY
        // ============================================================

        private static readonly List<Ascension> Registered =
            new List<Ascension>();

        private static SkillDB _registeredDatabase;


        internal static IReadOnlyList<Ascension> All
        {
            get
            {
                return Registered;
            }
        }


        // ============================================================
        // REGISTER
        // ============================================================

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


            // ========================================================
            // POTENT AFFLICTION
            // ========================================================

            Registered.Add(
                Create(
                    PotentAfflictionId,
                    "Potent Affliction",
                    "Your Spells deal 10% more damage per rank.",
                    5,
                    PotentAfflictionMaxRank));


            // ========================================================
            // ACCELERATED DECAY
            // ========================================================

            Registered.Add(
                Create(
                    AcceleratedDecayId,
                    "Accelerated Decay",
                    "Your damage-over-time effects tick 33% faster per rank without reducing their duration.",
                    5,
                    AcceleratedDecayMaxRank));


            // ========================================================
            // EXTENDED AFFLICTION
            // ========================================================

            Registered.Add(
                Create(
                    ExtendedAfflictionId,
                    "Extended Affliction",
                    "Your damage-over-time effects and debuff auras last 15% longer per rank.",
                    5,
                    ExtendedAfflictionMaxRank));



            // ============================================================
            // PESTILENT REJUVENATION
            // ============================================================

            Registered.Add(
                Create(
                    PestilentRejuvenationId,
                    "Pestilent Rejuvenation",
                    "Your damage-over-time effects have a 2% chance to restore 5% of your maximum mana when they deal damage.",
                    5,
                    PestilentRejuvinationMaxRank));

            // ========================================================
            // ADD TO NATIVE DATABASE
            // ========================================================

            nativeAscensions.AddRange(
                Registered);

            database.AscensionDatabase =
                nativeAscensions.ToArray();

            _registeredDatabase =
                database;

            Plugin.ModLog.Loading(
                "Blightcaller: registered " +
                Registered.Count +
                " Ascension perks.");

            return true;
        }


        // ============================================================
        // CREATE
        // ============================================================

        private static Ascension Create(
            string id,
            string name,
            string description,
            int weight,
            int maxRank)
        {
            Ascension ascension =
                ScriptableObject.CreateInstance<Ascension>();

            ascension.name =
                "CS BLC AA - " + name;

            ascension.Id =
                id;

            ascension.SkillName =
                name;

            ascension.SkillDesc =
                description;

            ascension.MaxRank =
                maxRank;

            ascension.SimPlayerWeight =
                weight;

            /*
             * The Necromancer source uses 7 here.
             *
             * Because Blightcaller is a custom class, the actual
             * class-specific display is handled by our AAScreen patch.
             */
            ascension.UsedBy =
                (Ascension.Class)7;

            ascension.hideFlags =
                HideFlags.HideAndDontSave;

            return ascension;
        }


        // ============================================================
        // CUSTOM ID CHECK
        // ============================================================

        internal static bool IsCustomId(
            string id)
        {
            return
                !string.IsNullOrEmpty(id) &&
                id.StartsWith(
                    "CS_BLC_AA_",
                    StringComparison.Ordinal);
        }


        // ============================================================
        // GET RANK
        // ============================================================

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
                    : 3;

            int rank =
                entry != null
                    ? entry.level
                    : 0;

            if (entry != null &&
                rank > maxRank &&
                IsBlightcallerSkills(skills))
            {
                int refund =
                    rank - maxRank;

                entry.level =
                    maxRank;

                skills.AscensionPoints +=
                    refund;

                Plugin.ModLog.Info(
                    "Blightcaller: capped " +
                    customId +
                    " at rank " +
                    maxRank +
                    " and refunded " +
                    refund +
                    " Ascension point(s).");

                rank =
                    maxRank;
            }

            return Mathf.Clamp(
                rank,
                0,
                maxRank);
        }


        // ============================================================
        // PLAYER RANK
        // ============================================================

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


        // ============================================================
        // CLASS CHECK
        // ============================================================

        internal static bool IsBlightcallerSkills(
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
                BlightcallerCatalog.IsBlightcallerClass(
                    stats.CharacterClass);
        }


        // ============================================================
        // PLAYER CHECK
        // ============================================================

        internal static bool IsBlightcallerPlayer()
        {
            PlayerControl playerControl =
                GameData.PlayerControl;

            if (playerControl == null ||
                playerControl.Myself == null ||
                playerControl.Myself.MyStats == null)
            {
                return false;
            }

            return
                BlightcallerCatalog.IsBlightcallerClass(
                    playerControl.Myself.MyStats.CharacterClass);
        }


        // ============================================================
        // DOT CHECK
        // ============================================================

        internal static bool IsBlightcallerDoT(
            Spell spell)
        {
            if (spell == null ||
                string.IsNullOrEmpty(spell.Id))
            {
                return false;
            }

            return
                spell.Id.StartsWith(
                    "ARCBLC_",
                    StringComparison.OrdinalIgnoreCase) &&
                spell.Id.EndsWith(
                    "_DOT",
                    StringComparison.OrdinalIgnoreCase);
        }


        // ============================================================
        // CUSTOM DOT TICK CHECK
        // ============================================================

        internal static bool UsesCustomDotTicks(
            Spell spell)
        {
            return IsBlightcallerDoT(spell);
        }


        // ============================================================
        // POTENT AFFLICTION
        // ============================================================

        internal static float GetPotentAfflictionMultiplier(
            UseSkill skills)
        {
            if (!IsBlightcallerSkills(skills))
            {
                return 1f;
            }

            int rank =
                GetRank(
                    skills,
                    PotentAfflictionId);

            if (rank <= 0)
            {
                return 1f;
            }

            return
                1f +
                (rank * 0.10f);
        }


        // ============================================================
        // ACCELERATED DECAY
        // ============================================================

        internal static float GetAcceleratedDecayMultiplier(
            UseSkill skills)
        {
            if (!IsBlightcallerSkills(skills))
            {
                return 1f;
            }

            int rank =
                GetRank(
                    skills,
                    AcceleratedDecayId);

            if (rank <= 0)
            {
                return 1f;
            }

            return
                1f +
                (rank * 0.33f);
        }


        // ============================================================
        // EXTENDED AFFLICTION
        // ============================================================

        internal static float GetExtendedAfflictionMultiplier(
            UseSkill skills)
        {
            if (!IsBlightcallerSkills(skills))
            {
                return 1f;
            }

            int rank =
                GetRank(
                    skills,
                    ExtendedAfflictionId);

            if (rank <= 0)
            {
                return 1f;
            }

            return
                1f +
                (rank * 0.15f);
        }
    

        // ============================================================
        // PESTILENT REJUVENATION
        // ============================================================

        internal static bool HasPestilentRejuvenation(
           UseSkill skills)
        {
            if (!IsBlightcallerSkills(skills))
            {
                return false;
            }

            return
                GetRank(
                    skills,
                    PestilentRejuvenationId) > 0;
        }


    }
}