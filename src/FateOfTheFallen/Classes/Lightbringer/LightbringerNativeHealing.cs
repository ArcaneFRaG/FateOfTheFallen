using System;
using System.Collections.Generic;

namespace FateOfTheFallen
{
    internal static class LightbringerNativeHealing
    {
        internal static bool IsUniversalSkillbook(Item item)
        {
            if (item == null || item.TeachSkill == null || item.TeachSpell != null ||
                item.Classes == null || item.Classes.Count == 0)
                return false;

            // This is intentionally strict: all six native class identities
            // must be present, otherwise it is a class-specific skillbook.
            string[] all = { "Arcanist", "Duelist", "Druid", "Paladin", "Reaver", "Stormcaller" };
            for (int n = 0; n < all.Length; n++)
            {
                bool found = false;
                foreach (Class allowed in item.Classes)
                {
                    if (allowed != null &&
                        string.Equals(LightbringerCatalog.GetClassDisplayName(allowed), all[n], StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }

        internal static bool CanUseItem(Item item)
        {
            if (item == null) return false;
            Class lb = LightbringerCatalog.TryEnsureClass();
            if (lb != null && item.Classes != null && item.Classes.Contains(lb)) return true;
            if (item.TeachSpell != null) return IsApproved(item.TeachSpell) ||
                LightbringerSpellCatalog.IsLightbringerCustomSpell(item.TeachSpell);
            if (item.TeachSkill != null) return IsUniversalSkillbook(item) ||
                LightbringerSkillCatalog.IsLightbringerSkill(item.TeachSkill);
            if (item.Classes == null) return false;
            Class druid = LightbringerCatalog.DruidClass;
            Class paladin = LightbringerCatalog.PaladinClass;
            return (druid != null && item.Classes.Contains(druid)) ||
                (paladin != null && item.Classes.Contains(paladin));
        }

        internal static void EnsureItemCompatibility(Item item)
        {
            if (item == null || item.Classes == null || !CanUseItem(item)) return;
            Class lb = LightbringerCatalog.TryEnsureClass();
            if (lb != null && !item.Classes.Contains(lb)) item.Classes.Add(lb);
        }

        // Approved native direct heals. The three native group-heal ranks are
        // intentionally explicit: Group Regrowth (and all Regrowth variants)
        // remain Druid-only. Group Major Heal is a separate item-proc spell,
        // not a learnable member of this spell progression.
        private static readonly HashSet<string> ApprovedNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Minor Healing", "Antidote", "Healing", "Major Healing", "Supreme Healing",
            "Group Heal", "Group Revitalize", "Major Group Heal"
        };

        internal static bool IsApproved(Spell spell)
        {
            return spell != null && !string.IsNullOrEmpty(spell.SpellName) &&
                ApprovedNames.Contains(spell.SpellName.Trim());
        }

        internal static void RegisterSpells(SpellDB database)
        {
            Class lightbringer = LightbringerCatalog.TryEnsureClass();
            if (lightbringer == null || database == null || database.SpellDatabase == null)
                return;
            int count = 0;
            foreach (Spell spell in database.SpellDatabase)
            {
                if (!IsApproved(spell)) continue;
                if (spell.UsedBy == null) spell.UsedBy = new List<Class>();
                if (!spell.UsedBy.Contains(lightbringer)) spell.UsedBy.Add(lightbringer);
                count++;
            }
            Plugin.LogInfo("[Lightbringer] Approved native heal records found: " + count + "/" + ApprovedNames.Count + ".");
        }

        internal static void RegisterItems(ItemDatabase database)
        {
            Class lightbringer = LightbringerCatalog.TryEnsureClass();
            if (database == null || lightbringer == null) return;
            HashSet<Item> items = new HashSet<Item>();
            if (database.ItemDB != null)
                foreach (Item item in database.ItemDB) if (item != null) items.Add(item);
            if (database.ItemDBList != null)
                foreach (Item item in database.ItemDBList) if (item != null) items.Add(item);
            if (database.GenericItems != null)
                foreach (Item item in database.GenericItems) if (item != null) items.Add(item);

            int changed = 0;
            foreach (Item item in items)
            {
                if (!CanUseItem(item) || item.Classes == null || item.Classes.Contains(lightbringer))
                    continue;
                item.Classes.Add(lightbringer);
                changed++;
            }
            Plugin.LogInfo("[Lightbringer] Druid/Paladin equipment + all-class skillbooks eligible: " + changed);
        }
    }
}
