using System;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Varkesh the Unburied's final five-item world-boss equipment pool.
    /// These are the original Death-themed items designed for the v0.2 loot table.
    /// IDs 1821-1825 are retained from r17 so any test-save copies are upgraded
    /// in place rather than orphaned.
    /// </summary>
    internal static class VarkeshEquipment
    {
        internal const string SoulbinderGrimoireId = "1821";
        internal const string GravetideReaverId = "1822";
        internal const string HeartOfTheDeathlessId = "1823";
        internal const string LanternOfDepartedSoulsId = "1824";
        internal const string BandOfTheGravewindId = "1825";

        // r17 aliases kept so lazy-resolver call sites and hot-upgraded saves
        // continue resolving the same canonical IDs.
        internal const string LastBreathScepterId = SoulbinderGrimoireId;
        internal const string UnburiedReaperId = GravetideReaverId;
        internal const string DeathlessShroudId = HeartOfTheDeathlessId;
        internal const string LastRiteHandsId = LanternOfDepartedSoulsId;
        internal const string BonewindRecurveId = BandOfTheGravewindId;

        internal const string SoulbinderGrimoireName = "Soulbinder's Grimoire";
        internal const string GravetideReaverName = "Gravetide Reaver";
        internal const string HeartOfTheDeathlessName = "Heart of the Deathless";
        internal const string LanternOfDepartedSoulsName = "Lantern of Departed Souls";
        internal const string BandOfTheGravewindName = "Band of the Gravewind";

        internal const string EquipmentBundleName = "fotf_equipment";

        internal const string SoulbinderGrimoirePrefab = "FOTF_Soulbinders_Grimoire";
        internal const string GravetideReaverPrefab = "FOTF_Gravetide_Reaver";
        internal const string LanternOfDepartedSoulsPrefab = "FOTF_Lantern_of_Departed_Souls";

        internal const string SoulbinderGrimoireIcon = "FOTF_Soulbinders_Grimoire.png";
        internal const string GravetideReaverIcon = "FOTF_Gravetide_Reaver.png";
        internal const string HeartOfTheDeathlessIcon = "FOTF_Heart_of_the_Deathless.png";
        internal const string LanternOfDepartedSoulsIcon = "FOTF_Lantern_of_Departed_Souls.png";
        internal const string BandOfTheGravewindIcon = "FOTF_Band_of_the_Gravewind.png";

        internal const int ItemLevel = 55;
        private const int ItemValue = 90000;

        private static Item _grimoire;
        private static Item _reaver;
        private static Item _heart;
        private static Item _lantern;
        private static Item _band;

        private static Spell _grimoireTooltip;
        private static Spell _reaverTooltip;
        private static Spell _heartTooltip;
        private static Spell _lanternTooltip;
        private static Spell _bandTooltip;

        internal static void RegisterAll(ItemDatabase database)
        {
            RegisterSoulbinderGrimoire(database);
            RegisterGravetideReaver(database);
            RegisterHeartOfTheDeathless(database);
            RegisterLanternOfDepartedSouls(database);
            RegisterBandOfTheGravewind(database);
        }

        internal static Item RegisterSoulbinderGrimoire(ItemDatabase database)
        {
            _grimoire = LateWorldBossEquipmentCommon.Register(
                database,
                _grimoire,
                new LateWorldBossEquipmentCommon.ItemSpec
                {
                    Id = SoulbinderGrimoireId,
                    Name = SoulbinderGrimoireName,
                    Lore = "A funerary tome whose pages whisper with souls that never reached their rest.",
                    IconFile = SoulbinderGrimoireIcon,
                    ItemLevel = ItemLevel,
                    ItemValue = ItemValue,
                    ExactTemplate = "Corruptor",
                    TemplateFallback = item => item != null &&
                        (item.RequiredSlot == Item.SlotType.Secondary || item.RequiredSlot == Item.SlotType.PrimaryOrSecondary),
                    Role = LateWorldBossEquipmentCommon.EquipmentRole.Caster,
                    Configure = ConfigureSoulbinderGrimoire
                },
                "VarkeshEquipment");
            return _grimoire;
        }

        internal static Item RegisterGravetideReaver(ItemDatabase database)
        {
            _reaver = LateWorldBossEquipmentCommon.Register(
                database,
                _reaver,
                new LateWorldBossEquipmentCommon.ItemSpec
                {
                    Id = GravetideReaverId,
                    Name = GravetideReaverName,
                    Lore = "A grave-forged one-handed blade that leaves the chill breath of the crypt behind every strike.",
                    IconFile = GravetideReaverIcon,
                    ItemLevel = ItemLevel,
                    ItemValue = ItemValue,
                    ExactTemplate = null,
                    TemplateFallback = item => item != null &&
                        item.RequiredSlot == Item.SlotType.PrimaryOrSecondary &&
                        !item.Shield &&
                        !item.IsBow &&
                        !item.IsWand,
                    Role = LateWorldBossEquipmentCommon.EquipmentRole.Melee,
                    Configure = ConfigureGravetideReaver
                },
                "VarkeshEquipment");
            return _reaver;
        }

        internal static Item RegisterHeartOfTheDeathless(ItemDatabase database)
        {
            _heart = LateWorldBossEquipmentCommon.Register(
                database,
                _heart,
                new LateWorldBossEquipmentCommon.ItemSpec
                {
                    Id = HeartOfTheDeathlessId,
                    Name = HeartOfTheDeathlessName,
                    Lore = "A blackened ring whose pulse begins only when the wearer's own heartbeat falters.",
                    IconFile = HeartOfTheDeathlessIcon,
                    ItemLevel = ItemLevel,
                    ItemValue = ItemValue,
                    ExactTemplate = null,
                    TemplateFallback = item => item != null && item.RequiredSlot == Item.SlotType.Ring,
                    Role = LateWorldBossEquipmentCommon.EquipmentRole.Tank,
                    Configure = ConfigureHeartOfTheDeathless
                },
                "VarkeshEquipment");
            return _heart;
        }

        internal static Item RegisterLanternOfDepartedSouls(ItemDatabase database)
        {
            _lantern = LateWorldBossEquipmentCommon.Register(
                database,
                _lantern,
                new LateWorldBossEquipmentCommon.ItemSpec
                {
                    Id = LanternOfDepartedSoulsId,
                    Name = LanternOfDepartedSoulsName,
                    Lore = "Its cold flame brightens when a nearby soul is close to slipping away.",
                    IconFile = LanternOfDepartedSoulsIcon,
                    ItemLevel = ItemLevel,
                    ItemValue = ItemValue,
                    ExactTemplate = null,
                    TemplateFallback = item => item != null && item.RequiredSlot == Item.SlotType.Secondary,
                    Role = LateWorldBossEquipmentCommon.EquipmentRole.Healer,
                    Configure = ConfigureLanternOfDepartedSouls
                },
                "VarkeshEquipment");
            return _lantern;
        }

        internal static Item RegisterBandOfTheGravewind(ItemDatabase database)
        {
            _band = LateWorldBossEquipmentCommon.Register(
                database,
                _band,
                new LateWorldBossEquipmentCommon.ItemSpec
                {
                    Id = BandOfTheGravewindId,
                    Name = BandOfTheGravewindName,
                    Lore = "A pale ring that hums with the thin wind that crosses forgotten graves.",
                    IconFile = BandOfTheGravewindIcon,
                    ItemLevel = ItemLevel,
                    ItemValue = ItemValue,
                    ExactTemplate = null,
                    TemplateFallback = item => item != null && item.RequiredSlot == Item.SlotType.Ring,
                    Role = LateWorldBossEquipmentCommon.EquipmentRole.Stormcaller,
                    Configure = ConfigureBandOfTheGravewind
                },
                "VarkeshEquipment");
            return _band;
        }

        // r17 lazy-resolver compatibility wrappers.
        internal static Item RegisterLastBreathScepter(ItemDatabase database) { return RegisterSoulbinderGrimoire(database); }
        internal static Item RegisterUnburiedReaper(ItemDatabase database) { return RegisterGravetideReaver(database); }
        internal static Item RegisterDeathlessShroud(ItemDatabase database) { return RegisterHeartOfTheDeathless(database); }
        internal static Item RegisterLastRiteHands(ItemDatabase database) { return RegisterLanternOfDepartedSouls(database); }
        internal static Item RegisterBonewindRecurve(ItemDatabase database) { return RegisterBandOfTheGravewind(database); }

        internal static bool IsVarkeshEquipment(Item item)
        {
            if (item == null) return false;
            return IsId(item.Id, SoulbinderGrimoireId) ||
                   IsId(item.Id, GravetideReaverId) ||
                   IsId(item.Id, HeartOfTheDeathlessId) ||
                   IsId(item.Id, LanternOfDepartedSoulsId) ||
                   IsId(item.Id, BandOfTheGravewindId);
        }

        internal static void ApplyClassRestriction(Item item)
        {
            if (item == null) return;
            if (IsId(item.Id, SoulbinderGrimoireId))
                LateWorldBossEquipmentCommon.ApplyRoleRestriction(item, LateWorldBossEquipmentCommon.EquipmentRole.Caster);
            else if (IsId(item.Id, GravetideReaverId))
                LateWorldBossEquipmentCommon.ApplyRoleRestriction(item, LateWorldBossEquipmentCommon.EquipmentRole.Melee);
            else if (IsId(item.Id, HeartOfTheDeathlessId))
                LateWorldBossEquipmentCommon.ApplyRoleRestriction(item, LateWorldBossEquipmentCommon.EquipmentRole.Tank);
            else if (IsId(item.Id, LanternOfDepartedSoulsId))
                LateWorldBossEquipmentCommon.ApplyRoleRestriction(item, LateWorldBossEquipmentCommon.EquipmentRole.Healer);
            else if (IsId(item.Id, BandOfTheGravewindId))
                LateWorldBossEquipmentCommon.ApplyRoleRestriction(item, LateWorldBossEquipmentCommon.EquipmentRole.Stormcaller);
        }

        internal static void RefreshClassRestrictions()
        {
            ApplyClassRestriction(CustomItemRegistry.GetItemById(SoulbinderGrimoireId));
            ApplyClassRestriction(CustomItemRegistry.GetItemById(GravetideReaverId));
            ApplyClassRestriction(CustomItemRegistry.GetItemById(HeartOfTheDeathlessId));
            ApplyClassRestriction(CustomItemRegistry.GetItemById(LanternOfDepartedSoulsId));
            ApplyClassRestriction(CustomItemRegistry.GetItemById(BandOfTheGravewindId));
        }

        private static void ConfigureSoulbinderGrimoire(Item item)
        {
            LateWorldBossEquipmentCommon.ResetStats(item);
            item.RequiredSlot = Item.SlotType.Secondary;
            item.Shield = false; item.IsBow = false; item.IsWand = false;
            item.Str = 0; item.End = 24; item.Dex = 0; item.Agi = 6;
            item.Int = 88; item.Wis = 48; item.Cha = 28; item.Res = 16;
            item.HP = 900; item.Mana = 825; item.AC = 120;
            item.MR = 18; item.PR = 12; item.ER = 12; item.VR = 24;
            item.Relic = true;
            item.WeaponProcChance = LateWorldBossEquipmentEffects.SoulRendChance;
            item.WeaponProcOnHit = GetGrimoireTooltip();
        }

        private static void ConfigureGravetideReaver(Item item)
        {
            LateWorldBossEquipmentCommon.ResetStats(item);
            item.RequiredSlot = Item.SlotType.PrimaryOrSecondary;
            item.Shield = false; item.IsBow = false; item.IsWand = false;
            item.Str = 92; item.End = 34; item.Dex = 58; item.Agi = 26;
            item.Int = 0; item.Wis = 0; item.Cha = 0; item.Res = 12;
            item.HP = 1400; item.Mana = 175; item.AC = 160;
            item.MR = 14; item.PR = 12; item.ER = 12; item.VR = 24;
            item.WeaponDmg = 44; item.WeaponDly = 1.15f;
            item.Relic = true;
            item.WeaponProcChance = LateWorldBossEquipmentEffects.DeathsMomentumChance;
            item.WeaponProcOnHit = GetReaverTooltip();
        }

        private static void ConfigureHeartOfTheDeathless(Item item)
        {
            LateWorldBossEquipmentCommon.ResetStats(item);
            item.RequiredSlot = Item.SlotType.Ring;
            item.Shield = false; item.IsBow = false; item.IsWand = false;
            item.Str = 24; item.End = 78; item.Dex = 0; item.Agi = 0;
            item.Int = 10; item.Wis = 34; item.Cha = 0; item.Res = 20;
            item.HP = 1550; item.Mana = 325; item.AC = 140;
            item.MR = 20; item.PR = 20; item.ER = 18; item.VR = 26;
            item.Relic = true;
            item.WeaponProcChance = 25f;
            item.WeaponProcOnHit = GetHeartTooltip();
        }

        private static void ConfigureLanternOfDepartedSouls(Item item)
        {
            LateWorldBossEquipmentCommon.ResetStats(item);
            item.RequiredSlot = Item.SlotType.Secondary;
            item.Shield = false; item.IsBow = false; item.IsWand = false;
            item.Str = 0; item.End = 28; item.Dex = 0; item.Agi = 6;
            item.Int = 56; item.Wis = 92; item.Cha = 36; item.Res = 16;
            item.HP = 1000; item.Mana = 950; item.AC = 125;
            item.MR = 18; item.PR = 14; item.ER = 14; item.VR = 26;
            item.Relic = true;
            item.WeaponProcChance = LateWorldBossEquipmentEffects.SoulkeepersGraceChance;
            item.WeaponProcOnHit = GetLanternTooltip();
        }

        private static void ConfigureBandOfTheGravewind(Item item)
        {
            LateWorldBossEquipmentCommon.ResetStats(item);
            item.RequiredSlot = Item.SlotType.Ring;
            item.Shield = false; item.IsBow = false; item.IsWand = false;
            item.Str = 34; item.End = 34; item.Dex = 70; item.Agi = 54;
            item.Int = 30; item.Wis = 12; item.Cha = 0; item.Res = 14;
            item.HP = 975; item.Mana = 350; item.AC = 105;
            item.MR = 16; item.PR = 14; item.ER = 14; item.VR = 24;
            item.Relic = true;
            item.WeaponProcChance = LateWorldBossEquipmentEffects.GravewindChance;
            item.WeaponProcOnHit = GetBandTooltip();
        }

        private static Spell GetGrimoireTooltip()
        {
            if (_grimoireTooltip == null)
                _grimoireTooltip = LateWorldBossEquipmentCommon.CreateTooltipProc(
                    "FOTF_TOOLTIP_VARKESH_SOUL_REND", "Soul Rend",
                    "Damage spells have a 15% chance to deal 3,600 Magic damage and restore 180 Mana.");
            return _grimoireTooltip;
        }

        private static Spell GetReaverTooltip()
        {
            if (_reaverTooltip == null)
                _reaverTooltip = LateWorldBossEquipmentCommon.CreateTooltipProc(
                    "FOTF_TOOLTIP_VARKESH_DEATHS_MOMENTUM", "Death's Momentum",
                    "One-handed physical weapon attacks have a 10% chance to deal 5,000 additional Physical damage and grant 12% attack speed for 6 seconds.");
            return _reaverTooltip;
        }

        private static Spell GetHeartTooltip()
        {
            if (_heartTooltip == null)
                _heartTooltip = LateWorldBossEquipmentCommon.CreateTooltipProc(
                    "FOTF_TOOLTIP_VARKESH_REFUSE_DEATH", "Refuse Death",
                    "Falling below 25% Health grants 150 Armor, 15% damage reduction and 25% increased healing received for 8 seconds. 90 second cooldown.");
            return _heartTooltip;
        }

        private static Spell GetLanternTooltip()
        {
            if (_lanternTooltip == null)
                _lanternTooltip = LateWorldBossEquipmentCommon.CreateTooltipProc(
                    "FOTF_TOOLTIP_VARKESH_SOULKEEPERS_GRACE", "Soulkeeper's Grace",
                    "Directly healing an ally below 35% Health increases that heal by 25% and grants 10% increased healing for 6 seconds. 12 second internal cooldown.");
            return _lanternTooltip;
        }

        private static Spell GetBandTooltip()
        {
            if (_bandTooltip == null)
                _bandTooltip = LateWorldBossEquipmentCommon.CreateTooltipProc(
                    "FOTF_TOOLTIP_VARKESH_GRAVEWIND", "Gravewind",
                    "Successful ranged attacks have a 12% chance to grant Gravewind for 8 seconds: 12% faster ranged attacks, +10 Dexterity and +10 Agility. While active, ranged attacks have a 20% chance to deal 1,200 Magic damage. 15 second internal cooldown.");
            return _bandTooltip;
        }

        private static bool IsId(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
