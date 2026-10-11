using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Shared registration helpers for the Varkesh/Gorath world-boss gear.
    /// Native Erenshor items supply only a proven slot/animation/icon template;
    /// every FOTF stat, restriction and effect link is overwritten here.
    /// </summary>
    internal static class LateWorldBossEquipmentCommon
    {
        internal enum EquipmentRole
        {
            Caster,
            Melee,
            Tank,
            Healer,
            Stormcaller
        }

        internal sealed class ItemSpec
        {
            internal string Id;
            internal string Name;
            internal string Lore;
            internal string IconFile;
            internal int ItemLevel;
            internal int ItemValue;
            internal string ExactTemplate;
            internal Func<Item, bool> TemplateFallback;
            internal EquipmentRole Role;
            internal Action<Item> Configure;
        }

        internal static Item Register(
            ItemDatabase database,
            Item current,
            ItemSpec spec,
            string logTag)
        {
            if (database == null || spec == null)
                return null;

            Item existing =
                CustomItemRegistry.GetItemById(spec.Id);

            if (existing != null)
            {
                ConfigureCommon(existing, spec);
                if (spec.Configure != null)
                    spec.Configure(existing);
                ApplyRoleRestriction(existing, spec.Role);
                CustomItemRegistry.RegisterWithNativeDatabase(database, existing);
                return existing;
            }

            Item template = ResolveTemplate(database, spec);

            if (template == null)
            {
                Plugin.NativeLog.LogError(
                    "[" + logTag + "] no native item template could be resolved for [" +
                    spec.Name + "] Id=[" + spec.Id + "].");
                return null;
            }

            Item item = UnityEngine.Object.Instantiate(template);
            if (item == null)
                return null;

            ConfigureCommon(item, spec);
            if (spec.Configure != null)
                spec.Configure(item);
            ApplyRoleRestriction(item, spec.Role);

            CustomItemRegistry.RegisterItem(item);
            CustomItemRegistry.RegisterWithNativeDatabase(database, item);

            Plugin.NativeLog.LogInfo(
                "[" + logTag + "] registered [" + item.ItemName + "] Id=[" + item.Id +
                "] ItemLevel=[" + item.ItemLevel + "] Template=[" + template.ItemName + "].");

            return item;
        }

        private static Item ResolveTemplate(
            ItemDatabase database,
            ItemSpec spec)
        {
            if (!string.IsNullOrEmpty(spec.ExactTemplate))
            {
                Item exact =
                    CustomItemRegistry.FindNativeItemByName(
                        database,
                        spec.ExactTemplate);

                if (exact != null)
                    return exact;
            }

            return FindNativeItem(database, spec.TemplateFallback);
        }

        private static Item FindNativeItem(
            ItemDatabase database,
            Func<Item, bool> predicate)
        {
            if (database == null || predicate == null)
                return null;

            if (database.ItemDBList != null)
            {
                for (int i = 0; i < database.ItemDBList.Count; i++)
                {
                    Item item = database.ItemDBList[i];
                    if (item != null &&
                        !CustomItemRegistry.IsRegistered(item.Id) &&
                        predicate(item))
                    {
                        return item;
                    }
                }
            }

            if (database.ItemDB != null)
            {
                for (int i = 0; i < database.ItemDB.Length; i++)
                {
                    Item item = database.ItemDB[i];
                    if (item != null &&
                        !CustomItemRegistry.IsRegistered(item.Id) &&
                        predicate(item))
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        private static void ConfigureCommon(
            Item item,
            ItemSpec spec)
        {
            item.name = "FOTF - " + spec.Name;
            item.Id = spec.Id;
            item.ItemName = spec.Name;
            item.ItemLevel = spec.ItemLevel;
            item.ItemValue = spec.ItemValue;
            item.Lore = spec.Lore ?? string.Empty;

            if (!string.IsNullOrEmpty(spec.IconFile))
            {
                Sprite icon = LateWorldBossEquipmentIconLoader.Load(spec.IconFile);
                if (icon != null)
                    item.ItemIcon = icon;
            }

            item.Stackable = false;
            item.Disposable = false;
            item.Unique = false;
            item.PlayerCannotSell = false;
            item.NoTradeNoDestroy = false;
            item.SimPlayersCantGet = false;
            item.RareItem = true;

            item.TeachSpell = null;
            item.TeachSkill = null;
            item.ItemEffectOnClick = null;
            item.ItemSkillUse = null;

            item.WeaponProcOnHit = null;
            item.WeaponProcChance = 0f;
            item.WandEffect = null;
            item.WandProcChance = 0f;
            item.BowEffect = null;
            item.BowProcChance = 0f;

            item.hideFlags = HideFlags.HideAndDontSave;
        }

        internal static void ResetStats(Item item)
        {
            if (item == null)
                return;

            item.HP = 0;
            item.AC = 0;
            item.Mana = 0;
            item.WeaponDmg = 0;
            item.Str = 0;
            item.End = 0;
            item.Dex = 0;
            item.Agi = 0;
            item.Int = 0;
            item.Wis = 0;
            item.Cha = 0;
            item.Res = 0;
            item.MR = 0;
            item.ER = 0;
            item.PR = 0;
            item.VR = 0;

            item.WeaponProcOnHit = null;
            item.WeaponProcChance = 0f;
            item.WandEffect = null;
            item.WandProcChance = 0f;
            item.BowEffect = null;
            item.BowProcChance = 0f;
        }

        internal static Spell CreateTooltipProc(
            string id,
            string spellName,
            string description)
        {
            Spell spell = ScriptableObject.CreateInstance<Spell>();
            spell.name = "FOTF Tooltip - " + spellName;
            spell.Id = id;
            spell.SpellName = spellName;
            spell.SpellDesc = description;

            // Never leave tooltip-only proc spells at enum/default-zero values.
            // CustomEquipmentEffectDescriptions applies the item-specific
            // damage/heal/cooldown fields immediately before the item window
            // renders, while these defaults keep every other inspection path
            // sane as well.
            spell.Type = Spell.SpellType.Beneficial;
            spell.Line = Spell.SpellLine.Generic;
            spell.RequiredLevel = 1;
            spell.ManaCost = 0;
            spell.TargetDamage = 0;
            spell.TargetHealing = 0;
            spell.Aggro = 0;
            spell.SpellChargeTime = 0f;
            spell.Cooldown = 0f;
            spell.SpellDurationInTicks = 0;
            spell.StatusEffectToApply = null;
            spell.MyDamageType = GameData.DamageType.Magic;
            spell.SpellRange = 100f;
            spell.SelfOnly = false;
            spell.CrowdControlSpell = false;
            spell.SimUsable = false;
            spell.SimsNeedHelpToLearn = true;

            spell.hideFlags = HideFlags.HideAndDontSave;
            return spell;
        }

        internal static void ApplyRoleRestriction(
            Item item,
            EquipmentRole role)
        {
            if (item == null)
                return;

            List<Class> previous =
                item.Classes != null
                    ? new List<Class>(item.Classes)
                    : new List<Class>();

            item.Classes = new List<Class>();

            if (role == EquipmentRole.Caster)
            {
                AddClassIfMissing(item.Classes, ResolveClass(previous, "Arcanist", GameData.ClassDB != null ? GameData.ClassDB.Arcanist : null));
                AddClassIfMissing(item.Classes, ResolveClass(previous, "Druid", GameData.ClassDB != null ? GameData.ClassDB.Druid : null));
                AddClassIfMissing(item.Classes, BlightcallerCatalog.BlightcallerClass);
                AddClassIfMissing(item.Classes, EmberweaverCatalog.TryEnsureClass());
                return;
            }

            if (role == EquipmentRole.Melee ||
                role == EquipmentRole.Tank)
            {
                AddClassIfMissing(item.Classes, ResolveClass(previous, "Paladin", GameData.ClassDB != null ? GameData.ClassDB.Paladin : null));
                AddClassIfMissing(item.Classes, ResolveClass(previous, "Reaver", GameData.ClassDB != null ? GameData.ClassDB.Reaver : null));
                return;
            }

            if (role == EquipmentRole.Healer)
            {
                AddClassIfMissing(item.Classes, ResolveClass(previous, "Druid", GameData.ClassDB != null ? GameData.ClassDB.Druid : null));
                return;
            }

            if (role == EquipmentRole.Stormcaller)
            {
                AddClassIfMissing(item.Classes, ResolveClass(previous, "Stormcaller", GameData.ClassDB != null ? GameData.ClassDB.Stormcaller : null));
            }
        }


        internal static void AddWindbladeEligibility(Item item)
        {
            if (item == null)
                return;

            if (item.Classes == null)
                item.Classes = new List<Class>();

            AddClassIfMissing(
                item.Classes,
                ResolveClass(
                    item.Classes,
                    "Windblade",
                    GameData.ClassDB != null ? GameData.ClassDB.Duelist : null));
        }

        private static Class ResolveClass(
            List<Class> existing,
            string className,
            Class preferred)
        {
            if (preferred != null)
                return preferred;

            if (existing == null || string.IsNullOrEmpty(className))
                return null;

            for (int i = 0; i < existing.Count; i++)
            {
                Class candidate = existing[i];
                if (candidate == null)
                    continue;

                if (string.Equals(candidate.DisplayName, className, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.name, className, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void AddClassIfMissing(
            List<Class> list,
            Class value)
        {
            if (list == null || value == null)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                    return;
            }

            list.Add(value);
        }
    }
}
