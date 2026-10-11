using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Canonical CustomItemRegistry definitions for Moldermaw's five equipment
    /// drops. Native items are used only as equip/animation/audio templates;
    /// all balance stats, restrictions and FOTF proc links are overwritten.
    /// </summary>
    internal static class MoldermawEquipment
    {
        internal const string SporeboundScepterId = "1801";
        internal const string RotcleaverId = "1802";
        internal const string BulwarkId = "1803";
        internal const string BloomId = "1804";
        internal const string SporepiercerId = "1805";

        internal const string SporeboundScepterName = "Sporebound Scepter";
        internal const string RotcleaverName = "Rotcleaver";
        internal const string BulwarkName = "Bulwark of the Blighted Earth";
        internal const string BloomName = "Bloom of the Withered Grove";
        internal const string SporepiercerName = "Sporepiercer";

        internal const string EquipmentBundleName = "fotf_equipment";

        internal const string SporeboundScepterPrefab = "FOTF_Sporebound_Scepter";
        internal const string RotcleaverPrefab = "FOTF_Rotcleaver";
        internal const string BulwarkPrefab = "FOTF_Bulwark_Blighted_Earth";
        internal const string BloomPrefab = "FOTF_Bloom_of_the_Withered_Grove";
        internal const string SporepiercerPrefab = "FOTF_Sporepiercer";

        internal const int ItemLevel = 45;

        private const int DefaultItemValue = 75000;

        private static Item _sporeboundScepter;
        private static Item _rotcleaver;
        private static Item _bulwark;
        private static Item _bloom;
        private static Item _sporepiercer;

        // Tooltip-only native proc proxies for effects whose real gameplay
        // trigger is not an ordinary weapon attack. Stats.CheckProc is patched
        // to ignore these two items, so the proxy spells affect tooltip
        // presentation only.
        private static Spell _bulwarkTooltipProc;
        private static Spell _bloomTooltipProc;

        private sealed class ItemSpec
        {
            internal string Id;
            internal string Name;
            internal string Lore;
            internal string IconFile;
            internal string ExactTemplate;
            internal Func<Item, bool> TemplateFallback;
            internal Action<Item> Configure;
        }


        // ============================================================
        // PUBLIC REGISTRATION ENTRY POINTS
        // ============================================================

        internal static void RegisterAll(
            ItemDatabase database)
        {
            RegisterSporeboundScepter(database);
            RegisterRotcleaver(database);
            RegisterBulwark(database);
            RegisterBloom(database);
            RegisterSporepiercer(database);
        }

        internal static Item RegisterSporeboundScepter(
            ItemDatabase database)
        {
            _sporeboundScepter =
                Register(
                    database,
                    _sporeboundScepter,
                    new ItemSpec
                    {
                        Id = SporeboundScepterId,
                        Name = SporeboundScepterName,
                        ExactTemplate = "Corruptor",
                        TemplateFallback = item =>
                            item != null &&
                            item.IsWand,
                        IconFile = "FOTF_Sporebound_Scepter.png",
                        Lore =
                            "A blight-caked scepter grown around a diseased stone core.",
                        Configure = ConfigureSporeboundScepter
                    });

            return _sporeboundScepter;
        }

        internal static Item RegisterRotcleaver(
            ItemDatabase database)
        {
            _rotcleaver =
                Register(
                    database,
                    _rotcleaver,
                    new ItemSpec
                    {
                        Id = RotcleaverId,
                        Name = RotcleaverName,
                        ExactTemplate = null,
                        TemplateFallback = item =>
                            item != null &&
                            item.RequiredSlot == Item.SlotType.Primary &&
                            item.ThisWeaponType == Item.WeaponType.TwoHandMelee &&
                            ContainsIgnoreCase(item.ItemName, "axe"),
                        IconFile = "FOTF_Rotcleaver.png",
                        Lore =
                            "A brutal two-handed axe of fractured stone and living corruption.",
                        Configure = ConfigureRotcleaver
                    });

            return _rotcleaver;
        }

        internal static Item RegisterBulwark(
            ItemDatabase database)
        {
            _bulwark =
                Register(
                    database,
                    _bulwark,
                    new ItemSpec
                    {
                        Id = BulwarkId,
                        Name = BulwarkName,
                        ExactTemplate = null,
                        TemplateFallback = item =>
                            item != null &&
                            item.Shield &&
                            item.RequiredSlot == Item.SlotType.Secondary,
                        IconFile = "FOTF_Bulwark_Blighted_Earth.png",
                        Lore =
                            "A slab of blighted earth hardened into an immense kite shield.",
                        Configure = ConfigureBulwark
                    });

            return _bulwark;
        }

        internal static Item RegisterBloom(
            ItemDatabase database)
        {
            _bloom =
                Register(
                    database,
                    _bloom,
                    new ItemSpec
                    {
                        Id = BloomId,
                        Name = BloomName,
                        ExactTemplate = null,
                        TemplateFallback = item =>
                            item != null &&
                            item.RequiredSlot == Item.SlotType.Neck,
                        IconFile = "FOTF_Bloom_Withered_Grove.png",
                        Lore =
                            "A withered stone bloom wrapped around a reservoir of concentrated blight.",
                        Configure = ConfigureBloom
                    });

            return _bloom;
        }

        internal static Item RegisterSporepiercer(
            ItemDatabase database)
        {
            _sporepiercer =
                Register(
                    database,
                    _sporepiercer,
                    new ItemSpec
                    {
                        Id = SporepiercerId,
                        Name = SporepiercerName,
                        ExactTemplate = "Honsus' Bow",
                        TemplateFallback = item =>
                            item != null &&
                            item.IsBow,
                        IconFile = "FOTF_Sporepiercer.png",
                        Lore =
                            "A recurved bow of diseased stone, its limbs split by glowing fungal veins.",
                        Configure = ConfigureSporepiercer
                    });

            return _sporepiercer;
        }


        // ============================================================
        // GETTERS / IDENTIFICATION
        // ============================================================

        internal static Item GetById(
            string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return CustomItemRegistry.GetItemById(id);
        }

        internal static bool IsMoldermawEquipment(
            Item item)
        {
            if (item == null)
            {
                return false;
            }

            return
                string.Equals(item.Id, SporeboundScepterId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Id, RotcleaverId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Id, BulwarkId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Id, BloomId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Id, SporepiercerId, StringComparison.OrdinalIgnoreCase);
        }


        internal static void RefreshClassRestrictions()
        {
            ApplyClassRestriction(
                GetById(SporeboundScepterId));

            ApplyClassRestriction(
                GetById(RotcleaverId));

            ApplyClassRestriction(
                GetById(BulwarkId));

            ApplyClassRestriction(
                GetById(BloomId));

            ApplyClassRestriction(
                GetById(SporepiercerId));
        }

        internal static void ApplyClassRestriction(
            Item item)
        {
            if (item == null)
            {
                return;
            }

            if (string.Equals(
                    item.Id,
                    SporeboundScepterId,
                    StringComparison.OrdinalIgnoreCase))
            {
                SetArcanistDruidBlightcallerOnly(
                    item);
                return;
            }

            if (string.Equals(
                    item.Id,
                    RotcleaverId,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    item.Id,
                    BulwarkId,
                    StringComparison.OrdinalIgnoreCase))
            {
                SetPaladinReaverOnly(
                    item);
                return;
            }

            if (string.Equals(
                    item.Id,
                    BloomId,
                    StringComparison.OrdinalIgnoreCase))
            {
                SetDruidOnly(
                    item);
                return;
            }

            if (string.Equals(
                    item.Id,
                    SporepiercerId,
                    StringComparison.OrdinalIgnoreCase))
            {
                SetStormcallerOnly(
                    item);
            }
        }


        // ============================================================
        // SHARED REGISTRATION
        // ============================================================

        private static Item Register(
            ItemDatabase database,
            Item current,
            ItemSpec spec)
        {
            if (database == null ||
                spec == null)
            {
                return null;
            }

            Item existing =
                CustomItemRegistry.GetItemById(
                    spec.Id);

            if (existing != null)
            {
                current = existing;
                ConfigureCommon(current, spec);
                spec.Configure?.Invoke(current);
                CustomItemRegistry.RegisterWithNativeDatabase(database, current);
                return current;
            }

            Item template =
                ResolveTemplate(
                    database,
                    spec);

            if (template == null)
            {
                Plugin.NativeLog.LogError(
                    "[MoldermawEquipment] no native template could be resolved for [" +
                    spec.Name +
                    "] [" +
                    spec.Id +
                    "].");

                return null;
            }

            Item item =
                UnityEngine.Object.Instantiate(
                    template);

            if (item == null)
            {
                Plugin.NativeLog.LogError(
                    "[MoldermawEquipment] failed cloning template [" +
                    template.ItemName +
                    "] for [" +
                    spec.Name +
                    "].");

                return null;
            }

            ConfigureCommon(item, spec);
            spec.Configure?.Invoke(item);

            CustomItemRegistry.RegisterItem(item);
            CustomItemRegistry.RegisterWithNativeDatabase(database, item);

            Plugin.NativeLog.LogInfo(
                "[MoldermawEquipment] registered [" +
                item.ItemName +
                "] Id=[" +
                item.Id +
                "] Template=[" +
                template.ItemName +
                "].");

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
                {
                    return exact;
                }
            }

            Item fallback =
                FindNativeItem(
                    database,
                    spec.TemplateFallback);

            if (fallback != null)
            {
                return fallback;
            }

            // Rotcleaver's exact preferred predicate includes "axe". If a
            // given game build has renamed its native axes, any 2H melee item
            // is still a safe animation/equip template.
            if (string.Equals(
                    spec.Id,
                    RotcleaverId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return FindNativeItem(
                    database,
                    item =>
                        item != null &&
                        item.RequiredSlot == Item.SlotType.Primary &&
                        item.ThisWeaponType == Item.WeaponType.TwoHandMelee);
            }

            return null;
        }

        private static Item FindNativeItem(
            ItemDatabase database,
            Func<Item, bool> predicate)
        {
            if (database == null ||
                predicate == null)
            {
                return null;
            }

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
            item.ItemLevel = ItemLevel;
            item.ItemValue = DefaultItemValue;
            item.Lore = spec.Lore ?? string.Empty;
            item.Stackable = false;
            item.Disposable = false;
            item.Unique = false;
            item.PlayerCannotSell = false;
            item.NoTradeNoDestroy = false;
            item.SimPlayersCantGet = false;
            item.RareItem = true;

            // Clear inherited click/teaching behavior and procs. Each item
            // below explicitly links only the effect that belongs to it.
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

            Sprite icon =
                MoldermawEquipmentIconLoader.Load(
                    spec.IconFile);

            if (icon != null)
            {
                item.ItemIcon = icon;
            }

            item.hideFlags =
                HideFlags.HideAndDontSave;
        }


        // ============================================================
        // FINAL STATS
        // ============================================================

        private static void ConfigureSporeboundScepter(
            Item item)
        {
            ResetStats(item);

            item.RequiredSlot = Item.SlotType.PrimaryOrSecondary;
            item.Shield = false;
            item.IsBow = false;
            item.IsWand = true;
            item.End = 20;
            item.Agi = 10;
            item.Int = 78;
            item.Wis = 35;
            item.Cha = 30;
            item.Res = 10;
            item.HP = 800;
            item.Mana = 625;
            item.AC = 75;
            item.MR = 8;
            item.PR = 18;
            item.ER = 6;
            item.VR = 8;
            item.WeaponDmg = 60;
            item.WeaponDly = 1.45f;
            item.IsWand = true;
            item.WandRange = 25;
            item.Relic = false;

            SetArcanistDruidBlightcallerOnly(item);
            LinkProcSpells(item);
        }

        private static void ConfigureRotcleaver(
            Item item)
        {
            ResetStats(item);

            item.RequiredSlot = Item.SlotType.Primary;
            item.Shield = false;
            item.IsBow = false;
            item.IsWand = false;
            item.ThisWeaponType = Item.WeaponType.TwoHandMelee;
            item.Str = 90;
            item.End = 32;
            item.Dex = 52;
            item.Agi = 15;
            item.Res = 8;
            item.HP = 1350;
            item.Mana = 150;
            item.AC = 150;
            item.MR = 8;
            item.PR = 18;
            item.ER = 8;
            item.VR = 8;
            item.WeaponDmg = 50;
            item.WeaponDly = 1.35f;
            item.Relic = false;

            SetPaladinReaverOnly(item);
            LinkProcSpells(item);
        }

        private static void ConfigureBulwark(
            Item item)
        {
            ResetStats(item);

            item.RequiredSlot = Item.SlotType.Secondary;
            item.Shield = true;
            item.IsBow = false;
            item.IsWand = false;
            item.Str = 30;
            item.End = 58;
            item.Dex = 15;
            item.Agi = 10;
            item.Int = 10;
            item.Wis = 25;
            item.Res = 14;
            item.HP = 1150;
            item.Mana = 300;
            item.AC = 245;
            item.MR = 12;
            item.PR = 22;
            item.ER = 10;
            item.VR = 14;
            item.Relic = false;

            SetPaladinReaverOnly(item);

            // Native tooltip proxy only. The actual Roots of Defiance /
            // Stonebark gameplay is still owned by MoldermawEquipmentEffects.
            item.WeaponProcChance = 90f;
            item.WeaponProcOnHit = GetBulwarkTooltipProc();
        }

        private static void ConfigureBloom(
            Item item)
        {
            ResetStats(item);

            item.RequiredSlot = Item.SlotType.Neck;
            item.Shield = false;
            item.IsBow = false;
            item.IsWand = false;
            item.End = 30;
            item.Agi = 10;
            item.Int = 48;
            item.Wis = 78;
            item.Cha = 38;
            item.Res = 12;
            item.HP = 850;
            item.Mana = 775;
            item.AC = 100;
            item.MR = 12;
            item.PR = 18;
            item.ER = 10;
            item.VR = 14;
            item.Relic = false;

            SetDruidOnly(item);

            // Native tooltip proxy only. The actual direct-heal trigger and
            // Lingering Bloom gameplay remain custom.
            item.WeaponProcChance = 15f;
            item.WeaponProcOnHit = GetBloomTooltipProc();
        }

        private static void ConfigureSporepiercer(
            Item item)
        {
            ResetStats(item);

            item.RequiredSlot = Item.SlotType.Primary;
            item.Shield = false;
            item.IsWand = false;
            item.IsBow = true;
            // Native player and SimPlayer bow visual/combat paths key off
            // TwoHandBow, not IsBow alone.  Make this explicit instead of
            // relying on the cloned Honsus template to retain it.
            item.ThisWeaponType = Item.WeaponType.TwoHandBow;
            item.Str = 60;
            item.End = 25;
            item.Dex = 68;
            item.Agi = 45;
            item.Int = 35;
            item.Wis = 15;
            item.Res = 10;
            item.HP = 950;
            item.Mana = 375;
            item.AC = 100;
            item.MR = 10;
            item.PR = 20;
            item.ER = 8;
            item.VR = 8;
            item.WeaponDmg = 72;
            item.WeaponDly = 1.35f;
            item.IsBow = true;
            item.BowRange = 30;
            item.Relic = true;

            SetStormcallerOnly(item);
            LinkProcSpells(item);
        }

        private static void ResetStats(
            Item item)
        {
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


        // ============================================================
        // TOOLTIP-ONLY NATIVE PROC PROXIES
        // ============================================================

        private static Spell GetBulwarkTooltipProc()
        {
            if (_bulwarkTooltipProc != null)
            {
                return _bulwarkTooltipProc;
            }

            Spell spell =
                ScriptableObject.CreateInstance<Spell>();

            spell.Id =
                "FOTF_TOOLTIP_MOLDERMAW_ROOTS_OF_DEFIANCE";

            spell.SpellName =
                "Roots of Defiance";

            spell.SpellDesc =
                "A successful Bash forces the target's attention and grants " +
                "Stonebark for 6 seconds. Stonebark grants +100 Armor and " +
                "8% physical damage reduction.";

            spell.hideFlags =
                HideFlags.HideAndDontSave;

            _bulwarkTooltipProc =
                spell;

            return _bulwarkTooltipProc;
        }

        private static Spell GetBloomTooltipProc()
        {
            if (_bloomTooltipProc != null)
            {
                return _bloomTooltipProc;
            }

            Spell spell =
                ScriptableObject.CreateInstance<Spell>();

            spell.Id =
                "FOTF_TOOLTIP_MOLDERMAW_LINGERING_BLOOM";

            spell.SpellName =
                "Lingering Bloom";

            spell.SpellDesc =
                "Blooming Reservoir grants +400 Mana while equipped. Direct " +
                "heals have a 15% chance to apply Lingering Bloom, healing " +
                "250 HP every 2 seconds for 6 seconds (750 total). Reprocs " +
                "refresh rather than stack.";

            spell.hideFlags =
                HideFlags.HideAndDontSave;

            _bloomTooltipProc =
                spell;

            return _bloomTooltipProc;
        }


        // ============================================================
        // PROC LINKS
        // ============================================================

        internal static void RelinkProcSpells()
        {
            LinkProcSpells(GetById(SporeboundScepterId));
            LinkProcSpells(GetById(RotcleaverId));
            LinkProcSpells(GetById(SporepiercerId));
        }

        private static void LinkProcSpells(
            Item item)
        {
            if (item == null)
            {
                return;
            }

            if (string.Equals(item.Id, SporeboundScepterId, StringComparison.OrdinalIgnoreCase))
            {
                item.IsWand = true;
                item.WandRange = 25;
                item.WandProcChance = 20f;
                item.WandEffect = MoldermawEquipmentEffects.SporeboundProcSpell;
                return;
            }

            if (string.Equals(item.Id, RotcleaverId, StringComparison.OrdinalIgnoreCase))
            {
                item.WeaponProcChance = 15f;
                item.WeaponProcOnHit = MoldermawEquipmentEffects.RotcleaverProcSpell;
                return;
            }

            if (string.Equals(item.Id, SporepiercerId, StringComparison.OrdinalIgnoreCase))
            {
                item.IsBow = true;
                item.ThisWeaponType = Item.WeaponType.TwoHandBow;
                item.BowRange = 30;
                item.BowProcChance = 18f;
                item.BowEffect = MoldermawEquipmentEffects.SporepiercerProcSpell;
            }
        }


        // ============================================================
        // CLASS RESTRICTIONS
        // ============================================================

        private static void SetAllClasses(
            Item item)
        {
            if (item == null)
            {
                return;
            }

            item.Classes =
                new List<Class>();

            object classDb =
                GameData.ClassDB;

            if (classDb != null)
            {
                Type type =
                    classDb.GetType();

                FieldInfo[] fields =
                    type.GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                for (int i = 0; i < fields.Length; i++)
                {
                    if (!typeof(Class).IsAssignableFrom(fields[i].FieldType))
                    {
                        continue;
                    }

                    Class value =
                        fields[i].GetValue(classDb) as Class;

                    AddClassIfMissing(
                        item.Classes,
                        value);
                }
            }

            AddClassIfMissing(
                item.Classes,
                BlightcallerCatalog.BlightcallerClass);

            AddClassIfMissing(
                item.Classes,
                EmberweaverCatalog.TryEnsureClass());
        }

        private static void SetArcanistDruidBlightcallerOnly(
            Item item)
        {
            List<Class> previous =
                item.Classes != null
                    ? new List<Class>(item.Classes)
                    : new List<Class>();

            item.Classes =
                new List<Class>();

            AddClassIfMissing(
                item.Classes,
                ResolveClass(
                    previous,
                    "Arcanist",
                    GameData.ClassDB != null
                        ? GameData.ClassDB.Arcanist
                        : null));

            AddClassIfMissing(
                item.Classes,
                ResolveClass(
                    previous,
                    "Druid",
                    GameData.ClassDB != null
                        ? GameData.ClassDB.Druid
                        : null));

            AddClassIfMissing(
                item.Classes,
                BlightcallerCatalog.BlightcallerClass);

            AddClassIfMissing(
                item.Classes,
                EmberweaverCatalog.TryEnsureClass());
        }

        private static void SetPaladinReaverOnly(
            Item item)
        {
            List<Class> previous =
                item.Classes != null
                    ? new List<Class>(item.Classes)
                    : new List<Class>();

            item.Classes =
                new List<Class>();

            AddClassIfMissing(
                item.Classes,
                ResolveClass(
                    previous,
                    "Paladin",
                    GameData.ClassDB != null
                        ? GameData.ClassDB.Paladin
                        : null));

            AddClassIfMissing(
                item.Classes,
                ResolveClass(
                    previous,
                    "Reaver",
                    GameData.ClassDB != null
                        ? GameData.ClassDB.Reaver
                        : null));
        }

        private static void SetDruidOnly(
            Item item)
        {
            List<Class> previous =
                item.Classes != null
                    ? new List<Class>(item.Classes)
                    : new List<Class>();

            item.Classes =
                new List<Class>();

            AddClassIfMissing(
                item.Classes,
                ResolveClass(
                    previous,
                    "Druid",
                    GameData.ClassDB != null
                        ? GameData.ClassDB.Druid
                        : null));
        }

        private static void SetStormcallerOnly(
            Item item)
        {
            List<Class> previous =
                item.Classes != null
                    ? new List<Class>(item.Classes)
                    : new List<Class>();

            item.Classes =
                new List<Class>();

            AddClassIfMissing(
                item.Classes,
                ResolveClass(
                    previous,
                    "Stormcaller",
                    GameData.ClassDB != null
                        ? GameData.ClassDB.Stormcaller
                        : null));
        }

        private static Class ResolveClass(
            List<Class> existing,
            string className,
            Class preferred)
        {
            if (preferred != null)
            {
                return preferred;
            }

            if (existing == null ||
                string.IsNullOrEmpty(
                    className))
            {
                return null;
            }

            for (int i = 0;
                 i < existing.Count;
                 i++)
            {
                Class candidate =
                    existing[i];

                if (candidate == null)
                {
                    continue;
                }

                if (string.Equals(
                        candidate.DisplayName,
                        className,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        candidate.name,
                        className,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void AddClassIfMissing(
            List<Class> classes,
            Class characterClass)
        {
            if (classes == null ||
                characterClass == null ||
                classes.Contains(characterClass))
            {
                return;
            }

            classes.Add(characterClass);
        }

        private static bool ContainsIgnoreCase(
            string value,
            string search)
        {
            return
                !string.IsNullOrEmpty(value) &&
                !string.IsNullOrEmpty(search) &&
                value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
