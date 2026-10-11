using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>Lightbringer native Ascension records and authoritative rank lookups.
    /// IDs are save-stable. Only the active Lightbringer's ranks provide bonuses.</summary>
    internal static class LightbringerAscensions
    {
        internal const string BastionOfLightId = "CS_LB_AA_BASTION_OF_LIGHT";
        internal const string RestorativeDawnId = "CS_LB_AA_RESTORATIVE_DAWN";
        internal const string SanctumDisciplineId = "CS_LB_AA_SANCTUM_DISCIPLINE";
        internal const string LastingGraceId = "CS_LB_AA_LASTING_GRACE";
        internal const string JudgementsMercyId = "CS_LB_AA_JUDGEMENTS_MERCY";
        private const int MaxRank = 3;
        private static readonly List<Ascension> Registered = new List<Ascension>();
        private static SkillDB _database;
        internal static IReadOnlyList<Ascension> All { get { return Registered; } }

        internal static bool Register()
        {
            SkillDB db = GameData.SkillDatabase;
            if (db == null || db.AscensionDatabase == null) return false;
            if (db == _database && Registered.Count == 5 && Registered.All(a =>
                a != null && db.GetAscensionByID(a.Id) == a)) return true;
            List<Ascension> existing = db.AscensionDatabase.Where(a =>
                a != null && !IsCustomId(a.Id)).ToList();
            Registered.Clear();
            Registered.Add(Create(BastionOfLightId, "Bastion of Light",
                "Lightbringer wards absorb 5% more damage per rank (5% / 10% / 15%)."));
            Registered.Add(Create(RestorativeDawnId, "Restorative Dawn",
                "Direct Lightbringer and approved native healing spells heal for 5% more per rank (5% / 10% / 15%)."));
            Registered.Add(Create(SanctumDisciplineId, "Sanctum Discipline",
                "Lightbringer healing and ward spells cost 5% less mana per rank (5% / 10% / 15%)."));
            Registered.Add(Create(LastingGraceId, "Lasting Grace",
                "When a Lightbringer ward is fully depleted by damage, heal its recipient for 4% / 8% / 12% of its original capacity. 20s cooldown per recipient. Does not trigger on expiry."));
            Registered.Add(Create(JudgementsMercyId, "Judgement's Mercy",
                "A direct Lightbringer damage spell heals the lowest-health injured group member for 2% / 4% / 6% of damage actually dealt. 4s internal cooldown."));
            existing.AddRange(Registered);
            db.AscensionDatabase = existing.ToArray();
            _database = db;
            return true;
        }

        private static Ascension Create(string id, string name, string description)
        {
            Ascension aa = ScriptableObject.CreateInstance<Ascension>();
            aa.name = "CS LB AA - " + name;
            aa.Id = id;
            aa.SkillName = name;
            aa.SkillDesc = description;
            aa.MaxRank = MaxRank;
            aa.SimPlayerWeight = 5;
            aa.UsedBy = (Ascension.Class)7;
            aa.hideFlags = HideFlags.HideAndDontSave;
            return aa;
        }

        internal static bool IsCustomId(string id)
        {
            return !string.IsNullOrEmpty(id) && id.StartsWith("CS_LB_AA_", StringComparison.Ordinal);
        }

        internal static bool IsLightbringerPlayer()
        {
            return LightbringerCatalog.IsCurrentPlayerLightbringer();
        }

        internal static int GetRank(UseSkill skills, string id)
        {
            if (skills == null || skills.MyAscensions == null || !IsCustomId(id)) return 0;
            Stats ownerStats = skills.GetComponent<Stats>();
            if (ownerStats == null)
            {
                Character owner = skills.GetComponent<Character>();
                if (owner != null) ownerStats = owner.MyStats;
            }
            // Sim skill controllers are sometimes on the Sim root while the
            // Stats and Character objects live on a separate child. Resolve
            // the authored Sim itself before declaring the rank unavailable.
            if (ownerStats == null)
            {
                SimPlayer sim = skills.GetComponent<SimPlayer>();
                if (sim == null) sim = skills.GetComponentInParent<SimPlayer>();
                if (sim != null) ownerStats = sim.MyStats;
            }
            if (ownerStats == null ||
                !LightbringerCatalog.IsLightbringerClass(ownerStats.CharacterClass)) return 0;
            int best = 0;
            foreach (AscensionSkillEntry entry in skills.MyAscensions)
                if (entry != null && string.Equals(entry.id, id, StringComparison.Ordinal))
                    best = Math.Max(best, entry.level);
            return Mathf.Clamp(best, 0, MaxRank);
        }

        internal static int GetPlayerRank(string id)
        {
            Character player = GameData.PlayerControl != null ? GameData.PlayerControl.Myself : null;
            return player != null ? GetRank(player.MySkills, id) : 0;
        }

        internal static float WardMultiplier(UseSkill skills)
        {
            return 1f + 0.05f * GetRank(skills, BastionOfLightId);
        }

        internal static float HealingMultiplier(UseSkill skills)
        {
            return 1f + 0.05f * GetRank(skills, RestorativeDawnId);
        }

        internal static float ManaCostMultiplier(UseSkill skills)
        {
            return 1f - 0.05f * GetRank(skills, SanctumDisciplineId);
        }
    }
}
