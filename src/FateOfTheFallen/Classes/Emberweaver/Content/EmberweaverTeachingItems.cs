using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Owns Emberweaver spell scrolls and passive-skill books.
    ///
    /// Item IDs use FOTF's numeric custom-item model so /additem, persistence,
    /// shared item lookup and future vendors all resolve the same canonical
    /// objects. Spell/Skill IDs remain symbolic because those databases use
    /// stable content IDs rather than the numeric Item.Id allocation.
    /// </summary>
    internal static class EmberweaverTeachingItems
    {
        private const string NativeSpellScrollTemplateId =
            "15302448";

        private const string SpellScrollIconFileName =
            "FOTF_Emberweaver_Scroll.png";

        private static readonly Dictionary<string, Item>
            ItemsById =
                new Dictionary<string, Item>(
                    StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string>
            ScrollIdsBySpellId =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    { EmberweaverSpellCatalog.EmberBoltId, EmberweaverItemIds.EmberBoltScroll },
                    { EmberweaverSpellCatalog.PyroclasmId, EmberweaverItemIds.PyroclasmScroll },
                    { EmberweaverSpellCatalog.ScorchId, EmberweaverItemIds.ScorchScroll },
                    { EmberweaverSpellCatalog.KindleId, EmberweaverItemIds.KindleScroll },
                    { EmberweaverSpellCatalog.FlameSurgeId, EmberweaverItemIds.FlameSurgeScroll },
                    { EmberweaverSpellCatalog.SearingBoltId, EmberweaverItemIds.SearingBoltScroll },
                    { EmberweaverSpellCatalog.SearingScorchId, EmberweaverItemIds.SearingScorchScroll },
                    { EmberweaverSpellCatalog.BlazingSurgeId, EmberweaverItemIds.BlazingSurgeScroll },
                    { EmberweaverSpellCatalog.StokeId, EmberweaverItemIds.StokeScroll },
                    { EmberweaverSpellCatalog.CombustId, EmberweaverItemIds.CombustScroll },
                    { EmberweaverSpellCatalog.GreaterPyroclasmId, EmberweaverItemIds.GreaterPyroclasmScroll },
                    { EmberweaverSpellCatalog.InfernoBoltId, EmberweaverItemIds.InfernoBoltScroll },
                    { EmberweaverSpellCatalog.DevastatingPyroclasmId, EmberweaverItemIds.DevastatingPyroclasmScroll },
                    { EmberweaverSpellCatalog.InfernalScorchId, EmberweaverItemIds.InfernalScorchScroll },
                    { EmberweaverSpellCatalog.CataclysmicSurgeId, EmberweaverItemIds.CataclysmicSurgeScroll },
                    { EmberweaverSpellCatalog.ConflagrateId, EmberweaverItemIds.ConflagrateScroll },
                    { EmberweaverSpellCatalog.CataclysmicBoltId, EmberweaverItemIds.CataclysmicBoltScroll },
                    { EmberweaverSpellCatalog.ApocalypticPyroclasmId, EmberweaverItemIds.ApocalypticPyroclasmScroll }
                };

        private static readonly Dictionary<string, string>
            SkillbookIdsBySkillId =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    { EmberweaverSkillCatalog.HeatId, EmberweaverItemIds.HeatSkillbook },
                    { EmberweaverSkillCatalog.FlashfireId, EmberweaverItemIds.FlashfireSkillbook },
                    { EmberweaverSkillCatalog.WildfireId, EmberweaverItemIds.WildfireSkillbook }
                };

        internal static void RegisterLazyResolvers()
        {
            foreach (
                KeyValuePair<string, string> pair
                in ScrollIdsBySpellId)
            {
                RegisterLazyResolver(
                    pair.Value);
            }

            foreach (
                KeyValuePair<string, string> pair
                in SkillbookIdsBySkillId)
            {
                RegisterLazyResolver(
                    pair.Value);
            }
        }

        private static void RegisterLazyResolver(
            string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return;
            }

            string stableId =
                itemId;

            CustomItemRegistry.RegisterLazyResolver(
                stableId,
                database =>
                {
                    Register(
                        database);

                    return GetById(
                        stableId);
                });
        }

        internal static void Register(
            ItemDatabase database)
        {
            if (database == null)
            {
                return;
            }

            Class emberweaver =
                EmberweaverCatalog.TryEnsureClass();

            if (emberweaver == null)
            {
                return;
            }

            if (GameData.SpellDatabase != null &&
                GameData.SpellDatabase.SpellDatabase != null)
            {
                EmberweaverSpellCatalog.Register(
                    GameData.SpellDatabase);
            }

            if (GameData.SkillDatabase != null)
            {
                EmberweaverSkillCatalog.Register(
                    GameData.SkillDatabase);
            }
            else
            {
                EmberweaverSkillCatalog.EnsureCreated();
            }

            Item spellScrollTemplate =
                FindSpellScrollTemplate(
                    database);

            Item skillbookTemplate =
                FindSkillbookTemplate(
                    database);

            if (spellScrollTemplate == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Emberweaver: native spell-scroll template was not found.");
            }

            if (skillbookTemplate == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Emberweaver: native skillbook template was not found.");
            }

            if (spellScrollTemplate != null)
            {
                Sprite spellScrollIcon =
                    EmbeddedSpriteLoader.LoadByFileName(
                        SpellScrollIconFileName);

                foreach (
                    EmberweaverSpellDefinition definition
                    in EmberweaverSpellCatalog.All)
                {
                    if (definition == null ||
                        definition.RuntimeSpell == null)
                    {
                        continue;
                    }

                    string itemId =
                        ScrollId(
                            definition.Id);

                    if (string.IsNullOrEmpty(itemId))
                    {
                        continue;
                    }

                    Item item =
                        GetOrCreateClone(
                            spellScrollTemplate,
                            itemId,
                            "Spell: " + definition.Name);

                    if (item == null)
                    {
                        continue;
                    }

                    ConfigureTeachingItem(
                        item,
                        itemId,
                        "Spell: " + definition.Name,
                        "A teaching scroll containing the knowledge of " +
                        definition.Name +
                        ".",
                        definition.RequiredLevel,
                        emberweaver);

                    item.TeachSpell =
                        definition.RuntimeSpell;

                    item.TeachSkill =
                        null;

                    /*
                     * Every Emberweaver spell scroll uses the shared
                     * Emberweaver fire-scroll artwork. The taught spell keeps
                     * its own spellbook/action-bar icon; only the physical
                     * teaching scroll item uses this sprite.
                     */
                    if (spellScrollIcon != null)
                    {
                        item.ItemIcon =
                            spellScrollIcon;
                    }

                    RegisterCanonicalItem(
                        database,
                        item);
                }
            }

            if (skillbookTemplate != null)
            {
                foreach (
                    EmberweaverSkillDefinition definition
                    in EmberweaverSkillCatalog.All)
                {
                    if (definition == null ||
                        definition.RuntimeSkill == null)
                    {
                        continue;
                    }

                    string itemId =
                        SkillbookId(
                            definition.Id);

                    if (string.IsNullOrEmpty(itemId))
                    {
                        continue;
                    }

                    Item item =
                        GetOrCreateClone(
                            skillbookTemplate,
                            itemId,
                            "Skillbook: " + definition.Name);

                    if (item == null)
                    {
                        continue;
                    }

                    ConfigureTeachingItem(
                        item,
                        itemId,
                        "Skillbook: " + definition.Name,
                        definition.Description,
                        definition.RequiredLevel,
                        emberweaver);

                    item.TeachSpell =
                        null;

                    item.TeachSkill =
                        definition.RuntimeSkill;

                    /*
                     * Intentionally retain Erenshor's native skillbook icon.
                     */
                    RegisterCanonicalItem(
                        database,
                        item);
                }
            }
        }

        internal static string ScrollId(
            string spellId)
        {
            if (string.IsNullOrEmpty(spellId))
            {
                return string.Empty;
            }

            string id;

            return ScrollIdsBySpellId.TryGetValue(
                    spellId,
                    out id)
                ? id
                : string.Empty;
        }

        internal static string SkillbookId(
            string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return string.Empty;
            }

            string id;

            return SkillbookIdsBySkillId.TryGetValue(
                    skillId,
                    out id)
                ? id
                : string.Empty;
        }

        internal static Item GetById(
            string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            Item item;

            if (ItemsById.TryGetValue(
                    id,
                    out item) &&
                item != null)
            {
                return item;
            }

            item =
                CustomItemRegistry.GetItemById(
                    id);

            if (item != null &&
                IsReservedEmberweaverItemId(
                    item.Id))
            {
                ItemsById[id] =
                    item;

                return item;
            }

            return null;
        }

        internal static Item GetScrollForSpell(
            string spellId)
        {
            string id =
                ScrollId(
                    spellId);

            return string.IsNullOrEmpty(id)
                ? null
                : GetById(id);
        }

        internal static Item GetSkillbookForSkill(
            string skillId)
        {
            string id =
                SkillbookId(
                    skillId);

            return string.IsNullOrEmpty(id)
                ? null
                : GetById(id);
        }

        internal static bool IsCustomTeachingItem(
            Item item)
        {
            return
                item != null &&
                IsReservedEmberweaverItemId(
                    item.Id) &&
                GetById(item.Id) != null;
        }

        internal static bool TryGetSpellDefinition(
            Item item,
            out EmberweaverSpellDefinition definition)
        {
            definition = null;

            return
                item != null &&
                item.TeachSpell != null &&
                IsCustomTeachingItem(item) &&
                EmberweaverSpellCatalog.TryGetDefinition(
                    item.TeachSpell,
                    out definition);
        }

        internal static bool TryGetSkillDefinition(
            Item item,
            out EmberweaverSkillDefinition definition)
        {
            definition = null;

            return
                item != null &&
                item.TeachSkill != null &&
                IsCustomTeachingItem(item) &&
                EmberweaverSkillCatalog.TryGetDefinition(
                    item.TeachSkill,
                    out definition);
        }

        private static Item GetOrCreateClone(
            Item template,
            string id,
            string itemName)
        {
            Item item =
                GetById(id);

            if (item != null)
            {
                return item;
            }

            if (template == null)
            {
                return null;
            }

            item =
                UnityEngine.Object.Instantiate(
                    template);

            if (item == null)
            {
                return null;
            }

            item.name =
                "FOTF - Emberweaver - " +
                itemName;

            item.Id =
                id;

            item.ItemName =
                itemName;

            item.hideFlags =
                HideFlags.HideAndDontSave;

            ItemsById[id] =
                item;

            return item;
        }

        private static void ConfigureTeachingItem(
            Item item,
            string id,
            string itemName,
            string lore,
            int level,
            Class emberweaver)
        {
            item.name =
                "FOTF - Emberweaver - " +
                itemName;

            item.Id =
                id;

            item.ItemName =
                itemName;

            item.ItemLevel =
                level;

            item.Lore =
                lore;

            item.RequiredSlot =
                Item.SlotType.General;

            item.Stackable =
                false;

            item.Disposable =
                true;

            item.Unique =
                false;

            item.PlayerCannotSell =
                false;

            item.SimPlayersCantGet =
                true;

            item.Classes =
                new List<Class>
                {
                    emberweaver
                };

            item.hideFlags =
                HideFlags.HideAndDontSave;
        }

        private static Item FindSpellScrollTemplate(
            ItemDatabase database)
        {
            if (database == null)
            {
                return null;
            }

            try
            {
                Item knownTemplate =
                    database.GetItemByID(
                        NativeSpellScrollTemplateId);

                if (knownTemplate != null)
                {
                    return knownTemplate;
                }
            }
            catch
            {
            }

            return FindTemplate(
                database,
                delegate(Item item)
                {
                    return
                        item != null &&
                        item.TeachSpell != null;
                });
        }

        private static Item FindSkillbookTemplate(
            ItemDatabase database)
        {
            return FindTemplate(
                database,
                delegate(Item item)
                {
                    return
                        item != null &&
                        item.TeachSkill != null;
                });
        }

        private static Item FindTemplate(
            ItemDatabase database,
            Predicate<Item> predicate)
        {
            if (database == null ||
                predicate == null)
            {
                return null;
            }

            if (database.ItemDB != null)
            {
                for (int i = 0;
                     i < database.ItemDB.Length;
                     i++)
                {
                    Item candidate =
                        database.ItemDB[i];

                    if (predicate(candidate))
                    {
                        return candidate;
                    }
                }
            }

            if (database.ItemDBList != null)
            {
                for (int i = 0;
                     i < database.ItemDBList.Count;
                     i++)
                {
                    Item candidate =
                        database.ItemDBList[i];

                    if (predicate(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static void RegisterCanonicalItem(
            ItemDatabase database,
            Item item)
        {
            if (database == null ||
                item == null)
            {
                return;
            }

            ItemsById[item.Id] =
                item;

            CustomItemRegistry.RegisterItem(
                item);

            CustomItemRegistry.RegisterWithNativeDatabase(
                database,
                item);
        }

        private static bool IsReservedEmberweaverItemId(
            string id)
        {
            int numericId;

            if (!int.TryParse(
                    id,
                    out numericId))
            {
                return false;
            }

            return
                (numericId >= EmberweaverItemIds.ReservedStart &&
                 numericId <= EmberweaverItemIds.ReservedEnd) ||
                numericId == EmberweaverItemIds.CombustReservedId;
        }
    }
}
