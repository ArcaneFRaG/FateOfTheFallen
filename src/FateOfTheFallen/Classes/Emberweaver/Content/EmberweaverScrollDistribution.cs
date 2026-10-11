using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Data-driven Emberweaver spell-scroll distribution. Each eligible
    /// monster gets one 3% Emberweaver-scroll roll. A successful roll grants
    /// one randomly selected scroll from that monster's progression band.
    ///
    /// Normal bands:
    ///   mobs 1-10  -> non-raid spells requiring 1-10
    ///   mobs 11-20 -> non-raid spells requiring 11-20
    ///   mobs 21-34 -> non-raid spells requiring 21-34
    ///
    /// Raid band:
    ///   native Erenshor raid-plane mobs -> RaidTier main-spell ranks only.
    ///
    /// High-level open-world mobs, FOTF world bosses and FOTF raid content
    /// never qualify merely because of their level. Levels 35+ outside the
    /// native raid planes intentionally have no normal Emberweaver scroll pool.
    /// </summary>
    internal static class EmberweaverScrollDistribution
    {
        internal const float ScrollDropChance = 0.03f;

        internal static void TryAddScrollDrop(
            LootTable lootTable)
        {
            if (lootTable == null)
            {
                return;
            }

            Character host =
                lootTable.hostChar;

            if (host == null ||
                !host.isNPC ||
                host.isVendor ||
                host.MyStats == null)
            {
                return;
            }

            int mobLevel =
                host.MyStats.Level;

            bool nativeRaidScene =
                IsNativeErenshorRaidScene(
                    host.gameObject.scene);

            List<Item> eligible =
                BuildEligibleScrollPool(
                    mobLevel,
                    nativeRaidScene);

            if (eligible.Count == 0)
            {
                return;
            }

            if (UnityEngine.Random.value >= ScrollDropChance)
            {
                return;
            }

            Item selected =
                eligible[
                    UnityEngine.Random.Range(
                        0,
                        eligible.Count)];

            if (selected == null)
            {
                return;
            }

            if (lootTable.ActualDrops == null)
            {
                lootTable.ActualDrops =
                    new List<Item>();
            }

            lootTable.ActualDrops.Add(selected);

            if (lootTable.ActualDropsQual == null)
            {
                lootTable.ActualDropsQual =
                    new List<int>();
            }

            lootTable.ActualDropsQual.Add(1);
        }

        private static List<Item> BuildEligibleScrollPool(
            int mobLevel,
            bool nativeRaidScene)
        {
            List<Item> result =
                new List<Item>();

            if (GameData.ItemDB != null &&
                EmberweaverTeachingItems.GetScrollForSpell(
                    EmberweaverSpellCatalog.EmberBoltId) == null)
            {
                /*
                 * Loot-table construction can run for many NPCs in a scene.
                 * Rehydrate teaching items only when they are actually absent;
                 * do not rescan every native item for equipment compatibility
                 * on every monster.
                 */
                EmberweaverTeachingItems.Register(
                    GameData.ItemDB);
            }

            foreach (
                EmberweaverSpellDefinition definition
                in EmberweaverSpellCatalog.All)
            {
                if (definition == null ||
                    !IsEligibleForMobLevel(
                        definition,
                        mobLevel,
                        nativeRaidScene))
                {
                    continue;
                }

                Item scroll =
                    EmberweaverTeachingItems.GetScrollForSpell(
                        definition.Id);

                if (scroll != null)
                {
                    result.Add(scroll);
                }
            }

            return result;
        }

        private static bool IsEligibleForMobLevel(
            EmberweaverSpellDefinition definition,
            int mobLevel,
            bool nativeRaidScene)
        {
            if (definition == null ||
                mobLevel <= 0)
            {
                return false;
            }

            // Raid-tier Emberweaver scrolls are progression rewards from
            // Erenshor's native raid planes only. Never infer raid eligibility
            // from monster level: FOTF bosses/raids and high-level open-world
            // mobs must not leak the level-35 capstone scrolls.
            if (nativeRaidScene)
            {
                return
                    definition.RaidTier &&
                    definition.RequiredLevel <= mobLevel;
            }

            if (definition.RaidTier)
            {
                return false;
            }

            if (mobLevel >= 1 &&
                mobLevel <= 10)
            {
                return
                    definition.RequiredLevel >= 1 &&
                    definition.RequiredLevel <= 10 &&
                    definition.RequiredLevel <= mobLevel;
            }

            if (mobLevel >= 11 &&
                mobLevel <= 20)
            {
                return
                    definition.RequiredLevel >= 11 &&
                    definition.RequiredLevel <= 20 &&
                    definition.RequiredLevel <= mobLevel;
            }

            if (mobLevel >= 21 &&
                mobLevel <= 34)
            {
                return
                    definition.RequiredLevel >= 21 &&
                    definition.RequiredLevel <= 34 &&
                    definition.RequiredLevel <= mobLevel;
            }

            return false;
        }

        internal static bool IsNativeErenshorRaidScene(
            Scene scene)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                string.IsNullOrEmpty(scene.name))
            {
                return false;
            }

            string normalized =
                NormalizeSceneName(
                    scene.name);

            // Explicitly reject every FOTF-owned scene before checking native
            // aliases. This keeps future FOTF raid monsters out of the native
            // raid scroll pool as well as the current Raid PoC.
            if (normalized.StartsWith(
                    "fotf",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            switch (normalized)
            {
                // Brax's Plane of Elements. Current Erenshor source name is
                // PlaneOfBrax; the display-name normalization is retained as
                // a harmless compatibility alias.
                case "planeofbrax":
                case "braxsplaneofelements":
                    return true;

                // Fernalla's native raid.
                case "planeofthewillow":
                case "planeofwillow":
                case "planeoffernalla":
                case "fernallasplaneofthewillow":
                    return true;

                // Soluna's native raid.
                case "solunascelestialplane":
                case "planeofsoluna":
                    return true;

                // Vitheo / Vithean naming differs between some Erenshor
                // surfaces, so accept both native-name forms.
                case "vitheosplaneofvalor":
                case "vitheansplaneofvalor":
                case "planeofvitheo":
                case "planeofvithean":
                    return true;

                default:
                    return false;
            }
        }

        private static string NormalizeSceneName(
            string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return string.Empty;
            }

            return
                sceneName
                    .Replace(" ", string.Empty)
                    .Replace("_", string.Empty)
                    .Replace("-", string.Empty)
                    .Replace("'", string.Empty)
                    .Replace("’", string.Empty)
                    .ToLowerInvariant();
        }
    }
}
