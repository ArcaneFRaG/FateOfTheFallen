using System;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Gorath the Ash-Crowned's final five-item world-boss equipment pool.
    /// These are the original Infernal-themed items designed for the v0.2 loot table.
    /// IDs 1831-1835 are retained from r17 so test-save copies upgrade in place.
    /// </summary>
    internal static class GorathEquipment
    {
        internal const string EmberOfTheDemonKingId = "1831";
        internal const string HellforgedTalonId = "1832";
        internal const string InfernalBastionId = "1833";
        internal const string AshenCovenantId = "1834";
        internal const string StormfireId = "1835";

        // r17 aliases kept for lazy-resolver/source compatibility.
        internal const string AshCrownedBrandId = EmberOfTheDemonKingId;
        internal const string AshenExecutionerId = HellforgedTalonId;
        internal const string FurnaceBulwarkId = InfernalBastionId;
        internal const string LastEmberCenserId = AshenCovenantId;
        internal const string HellstringId = StormfireId;

        internal const string EmberOfTheDemonKingName = "Ember of the Demon King";
        internal const string HellforgedTalonName = "Hellforged Talon";
        internal const string InfernalBastionName = "Infernal Bastion";
        internal const string AshenCovenantName = "Ashen Covenant";
        internal const string StormfireName = "Stormfire";

        internal const string EquipmentBundleName = "fotf_equipment";

        internal const string EmberOfTheDemonKingPrefab = "FOTF_Ember_of_the_Demon_King";
        internal const string HellforgedTalonPrefab = "FOTF_Hellforged_Talon";
        internal const string InfernalBastionPrefab = "FOTF_Infernal_Bastion";
        internal const string StormfirePrefab = "FOTF_Stormfire";

        internal const string EmberOfTheDemonKingIcon = "FOTF_Ember_of_the_Demon_King.png";
        internal const string HellforgedTalonIcon = "FOTF_Hellforged_Talon.png";
        internal const string InfernalBastionIcon = "FOTF_Infernal_Bastion.png";
        internal const string AshenCovenantIcon = "FOTF_Ashen_Covenant.png";
        internal const string StormfireIcon = "FOTF_Stormfire.png";

        internal const int ItemLevel = 60;
        private const int ItemValue = 100000;

        private static Item _ember;
        private static Item _talon;
        private static Item _bastion;
        private static Item _covenant;
        private static Item _stormfire;

        private static Spell _emberTooltip;
        private static Spell _talonTooltip;
        private static Spell _bastionTooltip;
        private static Spell _covenantTooltip;
        private static Spell _stormfireTooltip;

        internal static void RegisterAll(ItemDatabase database)
        {
            RegisterEmberOfTheDemonKing(database);
            RegisterHellforgedTalon(database);
            RegisterInfernalBastion(database);
            RegisterAshenCovenant(database);
            RegisterStormfire(database);
        }

        internal static Item RegisterEmberOfTheDemonKing(ItemDatabase database)
        {
            _ember = LateWorldBossEquipmentCommon.Register(
                database,
                _ember,
                new LateWorldBossEquipmentCommon.ItemSpec
                {
                    Id = EmberOfTheDemonKingId,
                    Name = EmberOfTheDemonKingName,
                    Lore = "The ember has burned since before mortal flame had a name.",
                    IconFile = EmberOfTheDemonKingIcon,
                    ItemLevel = ItemLevel,
                    ItemValue = ItemValue,
                    ExactTemplate = "Corruptor",
                    TemplateFallback = item => item != null && item.IsWand,
                    Role = LateWorldBossEquipmentCommon.EquipmentRole.Caster,
                    Configure = ConfigureEmberOfTheDemonKing
                },
                "GorathEquipment");
            return _ember;
        }

        internal static Item RegisterHellforgedTalon(ItemDatabase database)
        {
            _talon = LateWorldBossEquipmentCommon.Register(
                database,
                _talon,
                new LateWorldBossEquipmentCommon.ItemSpec
                {
                    Id = HellforgedTalonId,
                    Name = HellforgedTalonName,
                    Lore = "An axe hammered into the hooked profile of a demon's claw.",
                    IconFile = HellforgedTalonIcon,
                    ItemLevel = ItemLevel,
                    ItemValue = ItemValue,
                    ExactTemplate = "King's Gambit",
                    TemplateFallback = item => item != null &&
                        item.RequiredSlot == Item.SlotType.PrimaryOrSecondary,
                    Role = LateWorldBossEquipmentCommon.EquipmentRole.Melee,
                    Configure = ConfigureHellforgedTalon
                },
                "GorathEquipment");

            // Windblade is the native Duelist class internally.  Hellforged
            // Talon is intentionally usable by Paladin, Reaver and Windblade.
            LateWorldBossEquipmentCommon.AddWindbladeEligibility(_talon);
            return _talon;
        }

        internal static Item RegisterInfernalBastion(ItemDatabase database)
        {
            _bastion = LateWorldBossEquipmentCommon.Register(
                database,
                _bastion,
                new LateWorldBossEquipmentCommon.ItemSpec
                {
                    Id = InfernalBastionId,
                    Name = InfernalBastionName,
                    Lore = "A furnace-black shield whose seams glow when struck.",
                    IconFile = InfernalBastionIcon,
                    ItemLevel = ItemLevel,
                    ItemValue = ItemValue,
                    ExactTemplate = "Old Buckler",
                    TemplateFallback = item => item != null && item.Shield,
                    Role = LateWorldBossEquipmentCommon.EquipmentRole.Tank,
                    Configure = ConfigureInfernalBastion
                },
                "GorathEquipment");
            return _bastion;
        }

        internal static Item RegisterAshenCovenant(ItemDatabase database)
        {
            _covenant = LateWorldBossEquipmentCommon.Register(
                database,
                _covenant,
                new LateWorldBossEquipmentCommon.ItemSpec
                {
                    Id = AshenCovenantId,
                    Name = AshenCovenantName,
                    Lore = "A pact-ring of ash and gold that grows warm when restorative magic is spent.",
                    IconFile = AshenCovenantIcon,
                    ItemLevel = ItemLevel,
                    ItemValue = ItemValue,
                    ExactTemplate = null,
                    TemplateFallback = item => item != null && item.RequiredSlot == Item.SlotType.Ring,
                    Role = LateWorldBossEquipmentCommon.EquipmentRole.Healer,
                    Configure = ConfigureAshenCovenant
                },
                "GorathEquipment");
            return _covenant;
        }

        internal static Item RegisterStormfire(ItemDatabase database)
        {
            _stormfire = LateWorldBossEquipmentCommon.Register(
                database,
                _stormfire,
                new LateWorldBossEquipmentCommon.ItemSpec
                {
                    Id = StormfireId,
                    Name = StormfireName,
                    Lore = "A bow strung with a line of living storm-flame that never cools.",
                    IconFile = StormfireIcon,
                    ItemLevel = ItemLevel,
                    ItemValue = ItemValue,
                    ExactTemplate = "Honsus' Bow",
                    TemplateFallback = item => item != null && item.IsBow,
                    Role = LateWorldBossEquipmentCommon.EquipmentRole.Stormcaller,
                    Configure = ConfigureStormfire
                },
                "GorathEquipment");
            return _stormfire;
        }

        // r17 lazy-resolver compatibility wrappers.
        internal static Item RegisterAshCrownedBrand(ItemDatabase database) { return RegisterEmberOfTheDemonKing(database); }
        internal static Item RegisterAshenExecutioner(ItemDatabase database) { return RegisterHellforgedTalon(database); }
        internal static Item RegisterFurnaceBulwark(ItemDatabase database) { return RegisterInfernalBastion(database); }
        internal static Item RegisterLastEmberCenser(ItemDatabase database) { return RegisterAshenCovenant(database); }
        internal static Item RegisterHellstring(ItemDatabase database) { return RegisterStormfire(database); }

        internal static bool IsGorathEquipment(Item item)
        {
            if (item == null) return false;
            return IsId(item.Id, EmberOfTheDemonKingId) ||
                   IsId(item.Id, HellforgedTalonId) ||
                   IsId(item.Id, InfernalBastionId) ||
                   IsId(item.Id, AshenCovenantId) ||
                   IsId(item.Id, StormfireId);
        }

        internal static void ApplyClassRestriction(Item item)
        {
            if (item == null) return;
            if (IsId(item.Id, EmberOfTheDemonKingId))
                LateWorldBossEquipmentCommon.ApplyRoleRestriction(item, LateWorldBossEquipmentCommon.EquipmentRole.Caster);
            else if (IsId(item.Id, HellforgedTalonId))
            {
                LateWorldBossEquipmentCommon.ApplyRoleRestriction(item, LateWorldBossEquipmentCommon.EquipmentRole.Melee);
                LateWorldBossEquipmentCommon.AddWindbladeEligibility(item);
            }
            else if (IsId(item.Id, InfernalBastionId))
                LateWorldBossEquipmentCommon.ApplyRoleRestriction(item, LateWorldBossEquipmentCommon.EquipmentRole.Tank);
            else if (IsId(item.Id, AshenCovenantId))
                LateWorldBossEquipmentCommon.ApplyRoleRestriction(item, LateWorldBossEquipmentCommon.EquipmentRole.Healer);
            else if (IsId(item.Id, StormfireId))
                LateWorldBossEquipmentCommon.ApplyRoleRestriction(item, LateWorldBossEquipmentCommon.EquipmentRole.Stormcaller);
        }

        internal static void RefreshClassRestrictions()
        {
            ApplyClassRestriction(CustomItemRegistry.GetItemById(EmberOfTheDemonKingId));
            ApplyClassRestriction(CustomItemRegistry.GetItemById(HellforgedTalonId));
            ApplyClassRestriction(CustomItemRegistry.GetItemById(InfernalBastionId));
            ApplyClassRestriction(CustomItemRegistry.GetItemById(AshenCovenantId));
            ApplyClassRestriction(CustomItemRegistry.GetItemById(StormfireId));
        }

        private static void ConfigureEmberOfTheDemonKing(Item item)
        {
            LateWorldBossEquipmentCommon.ResetStats(item);
            item.RequiredSlot = Item.SlotType.PrimaryOrSecondary;
            item.Shield = false; item.IsBow = false; item.IsWand = true;
            item.Str = 0; item.End = 26; item.Dex = 0; item.Agi = 12;
            item.Int = 98; item.Wis = 38; item.Cha = 34; item.Res = 14;
            item.HP = 925; item.Mana = 875; item.AC = 95;
            item.MR = 14; item.PR = 10; item.ER = 28; item.VR = 14;
            item.WeaponDmg = 66; item.WeaponDly = 1.40f; item.WandRange = 25;
            item.Relic = true;
            item.WeaponProcChance = LateWorldBossEquipmentEffects.HellfireChance;
            item.WeaponProcOnHit = GetEmberTooltip();
        }

        private static void ConfigureHellforgedTalon(Item item)
        {
            LateWorldBossEquipmentCommon.ResetStats(item);
            item.RequiredSlot = Item.SlotType.PrimaryOrSecondary;
            item.Shield = false; item.IsBow = false; item.IsWand = false;
            item.Str = 100; item.End = 32; item.Dex = 62; item.Agi = 32;
            item.Int = 0; item.Wis = 0; item.Cha = 0; item.Res = 12;
            item.HP = 1100; item.Mana = 175; item.AC = 110;
            item.MR = 14; item.PR = 10; item.ER = 26; item.VR = 14;
            item.WeaponDmg = 48; item.WeaponDly = 1.12f;
            item.Relic = true;
            item.WeaponProcChance = LateWorldBossEquipmentEffects.InfernalFrenzyChance;
            item.WeaponProcOnHit = GetTalonTooltip();
        }

        private static void ConfigureInfernalBastion(Item item)
        {
            LateWorldBossEquipmentCommon.ResetStats(item);
            item.RequiredSlot = Item.SlotType.Secondary;
            item.Shield = true; item.IsBow = false; item.IsWand = false;
            item.Str = 40; item.End = 74; item.Dex = 16; item.Agi = 10;
            item.Int = 12; item.Wis = 24; item.Cha = 0; item.Res = 18;
            item.HP = 1450; item.Mana = 350; item.AC = 300;
            item.MR = 18; item.PR = 14; item.ER = 30; item.VR = 18;
            item.Relic = true;
            item.WeaponProcChance = LateWorldBossEquipmentEffects.DemonicChallengeChance;
            item.WeaponProcOnHit = GetBastionTooltip();
        }

        private static void ConfigureAshenCovenant(Item item)
        {
            LateWorldBossEquipmentCommon.ResetStats(item);
            item.RequiredSlot = Item.SlotType.Ring;
            item.Shield = false; item.IsBow = false; item.IsWand = false;
            item.Str = 0; item.End = 38; item.Dex = 0; item.Agi = 6;
            item.Int = 58; item.Wis = 90; item.Cha = 38; item.Res = 16;
            item.HP = 1050; item.Mana = 975; item.AC = 120;
            item.MR = 18; item.PR = 12; item.ER = 26; item.VR = 16;
            item.Relic = true;
            item.WeaponProcChance = LateWorldBossEquipmentEffects.AshenCovenantChance;
            item.WeaponProcOnHit = GetCovenantTooltip();
        }

        private static void ConfigureStormfire(Item item)
        {
            LateWorldBossEquipmentCommon.ResetStats(item);
            item.RequiredSlot = Item.SlotType.Primary;
            item.Shield = false; item.IsWand = false; item.IsBow = true;
            item.ThisWeaponType = Item.WeaponType.TwoHandBow;
            item.Str = 66; item.End = 30; item.Dex = 78; item.Agi = 48;
            item.Int = 74; item.Wis = 24; item.Cha = 16; item.Res = 12;
            item.HP = 1100; item.Mana = 675; item.AC = 115;
            item.MR = 16; item.PR = 10; item.ER = 30; item.VR = 14;
            item.WeaponDmg = 78; item.WeaponDly = 1.25f; item.BowRange = 30;
            item.Relic = true;
            item.WeaponProcChance = LateWorldBossEquipmentEffects.StormfireChance;
            item.WeaponProcOnHit = GetStormfireTooltip();
        }

        private static Spell GetEmberTooltip()
        {
            if (_emberTooltip == null)
                _emberTooltip = LateWorldBossEquipmentCommon.CreateTooltipProc(
                    "FOTF_TOOLTIP_GORATH_HELLFIRE", "Hellfire",
                    "Damage spells have a 20% chance to deal 2,400 Elemental damage and burn for 500 Elemental damage every 2 seconds for 8 seconds. Repeated procs refresh the burn and increase its periodic damage by 10%, up to 3 additional stacks.");
            return _emberTooltip;
        }

        private static Spell GetTalonTooltip()
        {
            if (_talonTooltip == null)
                _talonTooltip = LateWorldBossEquipmentCommon.CreateTooltipProc(
                    "FOTF_TOOLTIP_GORATH_INFERNAL_FRENZY", "Infernal Frenzy",
                    "Physical weapon attacks have a 15% chance to deal 4,400 additional Physical damage and grant 10% attack speed for 6 seconds.");
            return _talonTooltip;
        }

        private static Spell GetBastionTooltip()
        {
            if (_bastionTooltip == null)
                _bastionTooltip = LateWorldBossEquipmentCommon.CreateTooltipProc(
                    "FOTF_TOOLTIP_GORATH_DEMONIC_CHALLENGE", "Demonic Challenge",
                    "Bash has a 90% chance to taunt for 7,200 threat and grant Infernal Resolve, reducing damage taken by 5% for 6 seconds.");
            return _bastionTooltip;
        }

        private static Spell GetCovenantTooltip()
        {
            if (_covenantTooltip == null)
                _covenantTooltip = LateWorldBossEquipmentCommon.CreateTooltipProc(
                    "FOTF_TOOLTIP_GORATH_ASHEN_COVENANT", "Ashen Covenant",
                    "Beneficial spells have a 15% chance to refund 30% of their Mana cost and grant Ashen Grace, increasing healing by 8% for 6 seconds.");
            return _covenantTooltip;
        }

        private static Spell GetStormfireTooltip()
        {
            if (_stormfireTooltip == null)
                _stormfireTooltip = LateWorldBossEquipmentCommon.CreateTooltipProc(
                    "FOTF_TOOLTIP_GORATH_STORMFIRE", "Stormfire",
                    "Ranged attacks have a 20% chance to deal 2,600 Elemental damage and burn for 475 Elemental damage every 2 seconds for 8 seconds. Repeated procs refresh the burn and increase its periodic damage by 10%, up to 3 additional stacks. Spell damage against your Stormfire target has a 15% chance to trigger Arc Discharge for 1,000 Elemental damage (3 second cooldown).");
            return _stormfireTooltip;
        }

        private static bool IsId(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
