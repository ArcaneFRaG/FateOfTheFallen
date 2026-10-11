using System;
using System.Collections.Generic;

namespace FateOfTheFallen
{
    /// <summary>
    /// Mirrors the working Blightcaller equipment rule:
    /// - anything explicitly usable by Arcanist becomes usable by Emberweaver;
    /// - universal native General-slot items also recognize Emberweaver.
    ///
    /// This only edits Item.Classes. It never changes the Arcanist or
    /// Blightcaller Class objects themselves.
    /// </summary>
    internal static class ArcanistDerivedCompatibility
    {
        internal const string ArcaneProficiencySkillName =
            "Arcane Proficiency";

        internal const string MultifocusSkillName =
            "Multifocus";

        internal const string ArcaneRecoverySkillName =
            "Arcane Recovery";

        internal static void RegisterEquipmentCompatibility(
            ItemDatabase database)
        {
            if (database == null)
            {
                return;
            }

            Class arcanist =
                EmberweaverCatalog.ArcanistClass;

            Class emberweaver =
                EmberweaverCatalog.TryEnsureClass();

            if (arcanist == null ||
                emberweaver == null)
            {
                return;
            }

            HashSet<Item> visited =
                new HashSet<Item>();

            if (database.ItemDB != null)
            {
                for (int i = 0;
                     i < database.ItemDB.Length;
                     i++)
                {
                    AddCompatibility(
                        database.ItemDB[i],
                        arcanist,
                        emberweaver,
                        visited);
                }
            }

            if (database.ItemDBList != null)
            {
                for (int i = 0;
                     i < database.ItemDBList.Count;
                     i++)
                {
                    AddCompatibility(
                        database.ItemDBList[i],
                        arcanist,
                        emberweaver,
                        visited);
                }
            }

            if (database.GenericItems != null)
            {
                for (int i = 0;
                     i < database.GenericItems.Count;
                     i++)
                {
                    AddCompatibility(
                        database.GenericItems[i],
                        arcanist,
                        emberweaver,
                        visited);
                }
            }

            Plugin.LogInfo(
                "Applied Arcanist equipment eligibility to Emberweaver.");
        }

        internal static bool CanEmberweaverUseItem(
            Item item)
        {
            if (item == null)
            {
                return false;
            }

            Class emberweaver =
                EmberweaverCatalog.TryEnsureClass();

            bool teachingItem =
                item.TeachSpell != null ||
                item.TeachSkill != null;

            // Teaching items are ability eligibility rather than ordinary
            // equipment compatibility. Emberweaver owns its spell scrolls and
            // passive-skill books, but Arcane Proficiency, Multifocus, and
            // Arcane Recovery are intentionally shared with the
            // Arcanist/Blightcaller caster archetype.
            if (emberweaver != null &&
                item.Classes != null &&
                (item.Classes.Contains(emberweaver) ||
                 HasClassByName(
                     item.Classes,
                     EmberweaverConstants.DisplayName)))
            {
                return true;
            }

            if (teachingItem)
            {
                if (item.TeachSpell != null)
                {
                    return false;
                }

                if (!IsApprovedSharedSkill(
                        item.TeachSkill))
                {
                    return false;
                }

                Class arcanistTeachingClass =
                    EmberweaverCatalog.ArcanistClass;

                return
                    item.Classes != null &&
                    arcanistTeachingClass != null &&
                    (item.Classes.Contains(
                         arcanistTeachingClass) ||
                     HasClassByName(
                         item.Classes,
                         "Arcanist"));
            }

            Class arcanist =
                EmberweaverCatalog.ArcanistClass;

            if (item.Classes != null &&
                arcanist != null &&
                (item.Classes.Contains(arcanist) ||
                 HasClassByName(
                     item.Classes,
                     "Arcanist")))
            {
                return true;
            }

            return IsUniversalNativeGeneralItem(item);
        }

        private static void AddCompatibility(
            Item item,
            Class arcanist,
            Class emberweaver,
            HashSet<Item> visited)
        {
            if (item == null ||
                visited == null ||
                visited.Contains(item))
            {
                return;
            }

            visited.Add(item);

            // Never inherit Arcanist spell-scroll eligibility. Emberweaver
            // has its own spell scrolls. Native skillbooks are also excluded
            // by default, with Arcane Proficiency, Multifocus, and Arcane
            // Recovery as the deliberate shared-skill exceptions.
            if (item.TeachSpell != null)
            {
                return;
            }

            if (item.TeachSkill != null &&
                !IsApprovedSharedSkill(
                    item.TeachSkill))
            {
                return;
            }

            if (item.Classes == null)
            {
                return;
            }

            bool arcanistItem =
                item.Classes.Contains(arcanist) ||
                HasClassByName(
                    item.Classes,
                    "Arcanist");

            bool universalGeneral =
                IsUniversalNativeGeneralItem(item);

            if (!arcanistItem &&
                !universalGeneral)
            {
                return;
            }

            EnsureCanonicalClassReference(
                item.Classes,
                emberweaver);
        }


        internal static bool IsApprovedSharedSkill(
            Skill skill)
        {
            if (skill == null ||
                string.IsNullOrEmpty(skill.SkillName))
            {
                return false;
            }

            return
                string.Equals(
                    skill.SkillName,
                    ArcaneProficiencySkillName,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    skill.SkillName,
                    MultifocusSkillName,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    skill.SkillName,
                    ArcaneRecoverySkillName,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUniversalNativeGeneralItem(
            Item item)
        {
            if (item == null ||
                item.Classes == null ||
                item.Classes.Count == 0)
            {
                return false;
            }

            // Keep this build-safe across enum revisions by comparing the
            // native slot's display name rather than hardcoding an enum value.
            string slotName =
                item.RequiredSlot.ToString();

            if (!string.Equals(
                    slotName,
                    "General",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // A native General item intended for every normal class should
            // remain universal for FOTF custom classes as well.
            for (int i = 0;
                 i < EmberweaverConstants.NativeClassNames.Length;
                 i++)
            {
                if (!HasClassByName(
                        item.Classes,
                        EmberweaverConstants.NativeClassNames[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static void EnsureCanonicalClassReference(
            IList<Class> classes,
            Class emberweaver)
        {
            if (classes == null ||
                emberweaver == null)
            {
                return;
            }

            for (int i = 0;
                 i < classes.Count;
                 i++)
            {
                Class existing =
                    classes[i];

                if (existing == emberweaver)
                {
                    return;
                }

                if (existing == null)
                {
                    continue;
                }

                string displayName =
                    EmberweaverCatalog.GetClassDisplayName(
                        existing);

                if (string.Equals(
                        displayName,
                        EmberweaverConstants.DisplayName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    /*
                     * Lunaris hot reload can leave a previous custom Class
                     * ScriptableObject referenced by native items. Replace the
                     * stale object rather than treating the matching name as
                     * sufficient; native equipment checks can use reference
                     * equality.
                     */
                    classes[i] =
                        emberweaver;

                    return;
                }
            }

            classes.Add(
                emberweaver);
        }

        private static bool HasClassByName(
            IList<Class> classes,
            string wantedName)
        {
            if (classes == null ||
                string.IsNullOrEmpty(wantedName))
            {
                return false;
            }

            for (int i = 0;
                 i < classes.Count;
                 i++)
            {
                Class characterClass =
                    classes[i];

                if (characterClass == null)
                {
                    continue;
                }

                string displayName =
                    EmberweaverCatalog.GetClassDisplayName(
                        characterClass);

                if (string.Equals(
                        displayName,
                        wantedName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
