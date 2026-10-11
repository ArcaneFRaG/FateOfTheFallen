using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class LightbringerSkillbooks
    {
        private static readonly Dictionary<string, Item> Cache =
            new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);

        internal static Item GetById(string id)
        {
            Item item;
            return !string.IsNullOrEmpty(id) && Cache.TryGetValue(id, out item) ? item : null;
        }

        // Low-rate native open-world skillbook distribution. This is distinct
        // from the 3% Lightbringer SPELL scroll pool and excludes raid scenes.
        internal static void TryAddDrop(LootTable loot)
        {
            if (loot == null || loot.hostChar == null || !loot.hostChar.isNPC ||
                loot.hostChar.isVendor || loot.hostChar.MyStats == null ||
                GameData.ItemDB == null)
                return;
            int level = loot.hostChar.MyStats.Level;
            if (level <= 0 || level >= 35 ||
                EmberweaverScrollDistribution.IsNativeErenshorRaidScene(loot.hostChar.gameObject.scene) ||
                UnityEngine.Random.value >= .01f)
                return;
            Register(GameData.ItemDB);
            var eligible = new List<Item>();
            int bandMin = level <= 10 ? 1 : level <= 20 ? 11 : 21;
            int i = 0;
            foreach (LightbringerSkillDefinition def in LightbringerSkillCatalog.All)
            {
                Item item = GetById((1925 + i++).ToString());
                if (item != null && def.RequiredLevel >= bandMin && def.RequiredLevel <= level)
                    eligible.Add(item);
            }
            if (eligible.Count == 0) return;
            if (loot.ActualDrops == null) loot.ActualDrops = new List<Item>();
            if (loot.ActualDropsQual == null) loot.ActualDropsQual = new List<int>();
            loot.ActualDrops.Add(eligible[UnityEngine.Random.Range(0, eligible.Count)]);
            loot.ActualDropsQual.Add(1);
        }

        internal static void Register(ItemDatabase db)
        {
            if (db == null) return;
            Class lb = LightbringerCatalog.TryEnsureClass();
            if (lb == null) return;
            LightbringerSkillCatalog.Register(GameData.SkillDatabase);
            Item template = null;
            if (db.ItemDB != null)
                foreach (Item item in db.ItemDB)
                    if (item != null && item.TeachSkill != null) { template = item; break; }
            if (template == null && db.ItemDBList != null)
                foreach (Item item in db.ItemDBList)
                    if (item != null && item.TeachSkill != null) { template = item; break; }
            if (template == null)
            {
                Plugin.LogWarning("[Lightbringer] No native skillbook template; passive skillbooks deferred.");
                return;
            }
            int i = 0;
            foreach (LightbringerSkillDefinition def in LightbringerSkillCatalog.All)
            {
                string id = (1925 + i++).ToString();
                if (def.RuntimeSkill == null) continue;
                Item item;
                if (!Cache.TryGetValue(id, out item) || item == null)
                {
                    item = CustomItemRegistry.GetItemById(id);
                    if (item == null)
                    {
                        item = UnityEngine.Object.Instantiate(template);
                        item.Id = id;
                    }
                    Cache[id] = item;
                }
                item.name = "FOTF - Lightbringer Skillbook: " + def.Name;
                item.ItemName = "Skillbook: " + def.Name;
                item.Lore = def.Description;
                item.ItemLevel = def.RequiredLevel;
                item.Classes = new List<Class> { lb };
                item.TeachSpell = null;
                item.TeachSkill = def.RuntimeSkill;
                item.Disposable = true;
                item.Stackable = false;
                item.Unique = false;
                item.hideFlags = HideFlags.HideAndDontSave;
                // Intentionally preserve Erenshor's native skillbook ItemIcon.
                CustomItemRegistry.RegisterItem(item);
                CustomItemRegistry.RegisterWithNativeDatabase(db, item);
            }
        }
    }
}
