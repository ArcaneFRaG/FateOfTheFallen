using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class LightbringerTeachingItems
    {
        private const string ScrollIconFile = "FOTF_Lightbringer_Scroll.png";
        private static readonly Dictionary<string, Item> ItemsById =
            new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> ScrollIdsBySpellId =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { LightbringerSpellCatalog.FlickerWardId, LightbringerItemIds.FlickerWardScroll },
                { LightbringerSpellCatalog.RadiantBoltId, LightbringerItemIds.RadiantBoltScroll },
                { LightbringerSpellCatalog.GuardiansBlessingId, LightbringerItemIds.GuardiansBlessingScroll },
                { LightbringerSpellCatalog.RadiantEmbraceId, LightbringerItemIds.RadiantEmbraceScroll },
                { LightbringerSpellCatalog.AegisOfFaithId, LightbringerItemIds.AegisOfFaithScroll },
                { LightbringerSpellCatalog.DawnBoltId, LightbringerItemIds.DawnBoltScroll },
                { LightbringerSpellCatalog.LuminousMendingId, LightbringerItemIds.LuminousMendingScroll },
                { LightbringerSpellCatalog.RadiantCommunionId, LightbringerItemIds.RadiantCommunionScroll },
                { LightbringerSpellCatalog.AegisOfDawnId, LightbringerItemIds.AegisOfDawnScroll },
                { LightbringerSpellCatalog.JudgementOfLightId, LightbringerItemIds.JudgementOfLightScroll },
                { LightbringerSpellCatalog.DivineIntercessionId, LightbringerItemIds.DivineIntercessionScroll },
                { LightbringerSpellCatalog.GreaterRadiantCommunionId, LightbringerItemIds.GreaterRadiantCommunionScroll },
                { LightbringerSpellCatalog.DawnsSanctuaryId, LightbringerItemIds.DawnsSanctuaryScroll },
                { LightbringerSpellCatalog.AegisOfEternityId, LightbringerItemIds.AegisOfEternityScroll },
                { LightbringerSpellCatalog.DivineInterventionId, LightbringerItemIds.DivineInterventionScroll },
                { LightbringerSpellCatalog.SunsVerdictId, LightbringerItemIds.SunsVerdictScroll },
                { LightbringerSpellCatalog.SolarSparkId, LightbringerItemIds.SolarSparkScroll },
                { LightbringerSpellCatalog.JudgementId, LightbringerItemIds.JudgementScroll },
                { LightbringerSpellCatalog.SolarFlareId, LightbringerItemIds.SolarFlareScroll },
                { LightbringerSpellCatalog.GreaterJudgementId, LightbringerItemIds.GreaterJudgementScroll },
                { LightbringerSpellCatalog.SolarIgnitionId, LightbringerItemIds.SolarIgnitionScroll },
                { LightbringerSpellCatalog.RadiantLanceId, LightbringerItemIds.RadiantLanceScroll },
                { LightbringerSpellCatalog.CelestialLanceId, LightbringerItemIds.CelestialLanceScroll },
                { LightbringerSpellCatalog.SolarWrathId, LightbringerItemIds.SolarWrathScroll }
            };

        internal static void Register(ItemDatabase database)
        {
            if (database == null)
                return;

            Class lightbringer = LightbringerCatalog.TryEnsureClass();
            if (lightbringer == null)
                return;

            if (GameData.SpellDatabase != null)
                LightbringerSpellCatalog.Register(GameData.SpellDatabase);

            Item template = FindSpellScrollTemplate(database);
            if (template == null)
            {
                Plugin.LogWarning("[Lightbringer] Native spell-scroll template was not found.");
                return;
            }

            foreach (LightbringerSpellDefinition definition in LightbringerSpellCatalog.All)
            {
                if (definition == null || definition.RuntimeSpell == null)
                    continue;

                string itemId = ScrollId(definition.Id);
                if (string.IsNullOrEmpty(itemId))
                    continue;

                Item item = GetOrCreateClone(template, itemId, "Spell: " + definition.Name);
                if (item == null)
                    continue;

                ConfigureTeachingItem(item, definition, lightbringer);
                RegisterCanonicalItem(database, item);
            }
        }

        internal static Item GetScrollForSpell(string spellId)
        {
            string itemId = ScrollId(spellId);
            if (string.IsNullOrEmpty(itemId))
                return null;

            Item item;
            return ItemsById.TryGetValue(itemId, out item) ? item : CustomItemRegistry.GetItemById(itemId);
        }

        private static void ConfigureTeachingItem(Item item, LightbringerSpellDefinition definition, Class lightbringer)
        {
            item.name = "FOTF - Lightbringer - Spell: " + definition.Name;
            item.ItemName = "Spell: " + definition.Name;
            item.Lore = "A teaching scroll containing the knowledge of " + definition.Name + ".";
            item.ItemLevel = definition.RequiredLevel;
            item.Classes = new List<Class> { lightbringer };
            item.TeachSpell = definition.RuntimeSpell;
            item.TeachSkill = null;

            // One shared, embedded 100x100 scroll icon for every Lightbringer
            // teaching item. The actual learned spells retain their own icons.
            Sprite icon = EmbeddedSpriteLoader.LoadByFileName(ScrollIconFile);
            if (icon != null)
                item.ItemIcon = icon;
            item.Disposable = true;
            item.Stackable = false;
            item.Unique = false;
            item.RequiredSlot = Item.SlotType.General;
            item.hideFlags = HideFlags.HideAndDontSave;
        }

        private static string ScrollId(string spellId)
        {
            string result;
            return !string.IsNullOrEmpty(spellId) && ScrollIdsBySpellId.TryGetValue(spellId, out result)
                ? result
                : null;
        }

        private static Item FindSpellScrollTemplate(ItemDatabase database)
        {
            if (database == null)
                return null;

            Item[] arrays = database.ItemDB;
            if (arrays != null)
            {
                for (int i = 0; i < arrays.Length; i++)
                {
                    Item item = arrays[i];
                    if (IsSpellScrollTemplate(item))
                        return item;
                }
            }

            if (database.ItemDBList != null)
            {
                for (int i = 0; i < database.ItemDBList.Count; i++)
                {
                    Item item = database.ItemDBList[i];
                    if (IsSpellScrollTemplate(item))
                        return item;
                }
            }

            return null;
        }

        private static bool IsSpellScrollTemplate(Item item)
        {
            return item != null && item.TeachSpell != null && item.TeachSkill == null &&
                   !string.IsNullOrEmpty(item.ItemName) && item.ItemName.StartsWith("Spell: ", StringComparison.OrdinalIgnoreCase);
        }

        private static Item GetOrCreateClone(Item template, string itemId, string itemName)
        {
            Item existing;
            if (ItemsById.TryGetValue(itemId, out existing) && existing != null)
                return existing;

            existing = CustomItemRegistry.GetItemById(itemId);
            if (existing != null)
            {
                ItemsById[itemId] = existing;
                return existing;
            }

            Item clone = UnityEngine.Object.Instantiate(template);
            if (clone == null)
                return null;

            clone.Id = itemId;
            clone.name = "FOTF - " + itemName;
            clone.ItemName = itemName;
            clone.hideFlags = HideFlags.HideAndDontSave;
            ItemsById[itemId] = clone;
            CustomItemRegistry.RegisterItem(clone);
            return clone;
        }

        private static void RegisterCanonicalItem(ItemDatabase database, Item item)
        {
            if (item == null)
                return;

            ItemsById[item.Id] = item;
            CustomItemRegistry.RegisterItem(item);
            CustomItemRegistry.RegisterWithNativeDatabase(database, item);
        }
    }
}
