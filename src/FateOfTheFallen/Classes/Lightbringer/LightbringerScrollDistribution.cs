using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    internal static class LightbringerScrollDistribution
    {
        internal const float ScrollDropChance = 0.03f;

        internal static void TryAddScrollDrop(LootTable lootTable)
        {
            if (lootTable == null)
                return;

            Character host = lootTable.hostChar;
            if (host == null || !host.isNPC || host.isVendor || host.MyStats == null)
                return;

            int mobLevel = host.MyStats.Level;
            bool nativeRaidScene = EmberweaverScrollDistribution.IsNativeErenshorRaidScene(host.gameObject.scene);
            List<Item> eligible = BuildEligibleScrollPool(mobLevel, nativeRaidScene);
            if (eligible.Count == 0)
                return;
            if (UnityEngine.Random.value >= ScrollDropChance)
                return;

            Item selected = eligible[UnityEngine.Random.Range(0, eligible.Count)];
            if (selected == null)
                return;

            if (lootTable.ActualDrops == null)
                lootTable.ActualDrops = new List<Item>();
            lootTable.ActualDrops.Add(selected);

            if (lootTable.ActualDropsQual == null)
                lootTable.ActualDropsQual = new List<int>();
            lootTable.ActualDropsQual.Add(1);
        }

        private static List<Item> BuildEligibleScrollPool(int mobLevel, bool nativeRaidScene)
        {
            List<Item> result = new List<Item>();

            if (GameData.ItemDB != null && LightbringerTeachingItems.GetScrollForSpell(LightbringerSpellCatalog.FlickerWardId) == null)
                LightbringerTeachingItems.Register(GameData.ItemDB);

            foreach (LightbringerSpellDefinition definition in LightbringerSpellCatalog.All)
            {
                if (definition == null || !IsEligibleForMobLevel(definition, mobLevel, nativeRaidScene))
                    continue;

                Item scroll = LightbringerTeachingItems.GetScrollForSpell(definition.Id);
                if (scroll != null)
                    result.Add(scroll);
            }

            return result;
        }

        private static bool IsEligibleForMobLevel(LightbringerSpellDefinition definition, int mobLevel, bool nativeRaidScene)
        {
            if (definition == null || mobLevel <= 0)
                return false;

            if (nativeRaidScene)
                return definition.RaidTier && definition.RequiredLevel <= mobLevel;

            if (definition.RaidTier)
                return false;

            if (mobLevel >= 1 && mobLevel <= 10)
                return definition.RequiredLevel >= 1 && definition.RequiredLevel <= 10 && definition.RequiredLevel <= mobLevel;

            if (mobLevel >= 11 && mobLevel <= 20)
                return definition.RequiredLevel >= 11 && definition.RequiredLevel <= 20 && definition.RequiredLevel <= mobLevel;

            if (mobLevel >= 21 && mobLevel <= 34)
                return definition.RequiredLevel >= 21 && definition.RequiredLevel <= 34 && definition.RequiredLevel <= mobLevel;

            return false;
        }
    }
}
