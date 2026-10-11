using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Canonical CustomItemRegistry definitions for Nhal'Sera's final five-item
    /// equipment pool.
    ///
    /// Native Erenshor items are used only as equip/animation templates.
    /// Every FOTF stat, class restriction, icon, ID and equip effect is owned
    /// by this class/runtime.
    /// </summary>
    internal static class NhalSeraEquipment
    {
        internal const string LightlessScepterId = "1811";
        internal const string CarapaceId = "1812";
        internal const string CrownId = "1813";
        internal const string GraspId = "1814";
        internal const string GirdleId = "1815";

        internal const string LightlessScepterName = "Scepter of the Lightless Deep";
        internal const string CarapaceName = "Carapace of the Event Horizon";
        internal const string CrownName = "Crown of the Unmade";
        internal const string GraspName = "Grasp of Fractured Reality";
        internal const string GirdleName = "Girdle of the Shrouded Threshold";

        internal const string EquipmentBundleName = "fotf_equipment";

        // Exact prefab names in the bundled Unity AssetBundle.
        internal const string LightlessScepterPrefab = "FOTF_Scepter_of_the_Lightless_Deep";
        // Carapace no longer ships/loads a custom body prefab. Its appearance is
        // built from Erenshor's native Eventide Chestguard at runtime, then
        // recolored by FateEquipmentVisuals.EventideVariants.cs.
        internal const string CarapaceNativeTemplate = "Eventide Chestguard";
        internal const string CarapacePrefab = null;
        internal const string CrownPrefab = "FOTF_Crown_of_the_Unmade";
        internal const string GraspPrefab = "FOTF_Grasp_of_Fractured_Reality";
        internal const string GirdlePrefab = "FOTF_Girdle_of_the_Shrouded_Threshold";

        internal const int ItemLevel = 50;
        private const int DefaultItemValue = 90000;

        private static Item _lightlessScepter;
        private static Item _carapace;
        private static Item _crown;
        private static Item _grasp;
        private static Item _girdle;

        // Native tooltip proxies. Gameplay is still handled entirely by
        // NhalSeraEquipmentEffects; these spells exist only so Erenshor can
        // render its normal proc line / Hold RIGHT MOUSE details UI.
        private static Spell _lightlessTooltipProc;
        private static Spell _carapaceTooltipProc;
        private static Spell _crownTooltipProc;
        private static Spell _graspTooltipProc;
        private static Spell _girdleTooltipProc;

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

        internal static void RegisterAll(ItemDatabase database)
        {
            RegisterLightlessScepter(database);
            RegisterCarapace(database);
            RegisterCrown(database);
            RegisterGrasp(database);
            RegisterGirdle(database);
        }

        internal static Item RegisterLightlessScepter(ItemDatabase database)
        {
            _lightlessScepter = Register(
                database,
                _lightlessScepter,
                new ItemSpec
                {
                    Id = LightlessScepterId,
                    Name = LightlessScepterName,
                    ExactTemplate = "Corruptor",
                    TemplateFallback = item => item != null && item.IsWand,
                    IconFile = "FOTF_Scepter_of_the_Lightless_Deep.png",
                    Lore =
                        "An obsidian scepter wrapped around a lightless core.",
                    Configure = ConfigureLightlessScepter
                });

            return _lightlessScepter;
        }

        internal static Item RegisterCarapace(ItemDatabase database)
        {
            _carapace = Register(
                database,
                _carapace,
                new ItemSpec
                {
                    Id = CarapaceId,
                    Name = CarapaceName,

                    // Use the native Eventide chest as the actual equipment
                    // visual source. This preserves Erenshor's native Synty
                    // skinning, shoulder placement and animation.
                    ExactTemplate = CarapaceNativeTemplate,
                    TemplateFallback = item =>
                        item != null &&
                        item.RequiredSlot == Item.SlotType.Chest &&
                        !string.IsNullOrEmpty(item.ItemName) &&
                        item.ItemName.IndexOf(
                            "Eventide",
                            StringComparison.OrdinalIgnoreCase) >= 0,

                    IconFile = "FOTF_Carapace_of_the_Event_Horizon.png",
                    Lore =
                        "Void-forged armour shaped around a captive event horizon.",
                    Configure = ConfigureCarapace
                });

            return _carapace;
        }

        internal static Item RegisterCrown(ItemDatabase database)
        {
            _crown = Register(
                database,
                _crown,
                new ItemSpec
                {
                    Id = CrownId,
                    Name = CrownName,
                    TemplateFallback = item =>
                        item != null &&
                        item.RequiredSlot == Item.SlotType.Head,
                    IconFile = "FOTF_Crown_of_the_Unmade.png",
                    Lore =
                        "A broken void-crown whose gems hum with stolen possibility.",
                    Configure = ConfigureCrown
                });

            return _crown;
        }

        internal static Item RegisterGrasp(ItemDatabase database)
        {
            _grasp = Register(
                database,
                _grasp,
                new ItemSpec
                {
                    Id = GraspId,
                    Name = GraspName,
                    TemplateFallback = item =>
                        item != null &&
                        item.RequiredSlot == Item.SlotType.Hand,
                    IconFile = "FOTF_Grasp_of_Fractured_Reality.png",
                    Lore =
                        "Void-split gauntlets that seem to arrive a fraction of a second before " +
                        "their wearer's hands.",
                    Configure = ConfigureGrasp
                });

            return _grasp;
        }

        internal static Item RegisterGirdle(ItemDatabase database)
        {
            _girdle = Register(
                database,
                _girdle,
                new ItemSpec
                {
                    Id = GirdleId,
                    Name = GirdleName,
                    TemplateFallback = item =>
                        item != null &&
                        item.RequiredSlot == Item.SlotType.Waist,
                    IconFile = "FOTF_Girdle_of_the_Shrouded_Threshold.png",
                    Lore =
                        "A void-bound girdle set with crystals that drink from the space between " +
                        "worlds.",
                    Configure = ConfigureGirdle
                });

            return _girdle;
        }

        internal static Item GetById(string id)
        {
            return string.IsNullOrEmpty(id)
                ? null
                : CustomItemRegistry.GetItemById(id);
        }

        internal static bool IsNhalSeraEquipment(Item item)
        {
            if (item == null ||
                string.IsNullOrEmpty(item.Id))
            {
                return false;
            }

            return
                string.Equals(item.Id, LightlessScepterId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Id, CarapaceId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Id, CrownId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Id, GraspId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Id, GirdleId, StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetPrefabName(Item item)
        {
            if (item == null ||
                string.IsNullOrEmpty(item.Id))
            {
                return null;
            }

            if (string.Equals(item.Id, LightlessScepterId, StringComparison.OrdinalIgnoreCase))
                return LightlessScepterPrefab;

            if (string.Equals(item.Id, CarapaceId, StringComparison.OrdinalIgnoreCase))
                return CarapacePrefab;

            if (string.Equals(item.Id, CrownId, StringComparison.OrdinalIgnoreCase))
                return CrownPrefab;

            if (string.Equals(item.Id, GraspId, StringComparison.OrdinalIgnoreCase))
                return GraspPrefab;

            if (string.Equals(item.Id, GirdleId, StringComparison.OrdinalIgnoreCase))
                return GirdlePrefab;

            return null;
        }

        internal static void RefreshClassRestrictions()
        {
            ApplyClassRestriction(GetById(LightlessScepterId));
            ApplyClassRestriction(GetById(CarapaceId));
            ApplyClassRestriction(GetById(CrownId));
            ApplyClassRestriction(GetById(GraspId));
            ApplyClassRestriction(GetById(GirdleId));
        }

        internal static void ApplyClassRestriction(Item item)
        {
            if (item == null)
                return;

            if (string.Equals(item.Id, LightlessScepterId, StringComparison.OrdinalIgnoreCase))
            {
                SetArcanistDruidBlightcallerOnly(item);
                return;
            }

            if (string.Equals(item.Id, CarapaceId, StringComparison.OrdinalIgnoreCase))
            {
                SetPaladinReaverOnly(item);
                return;
            }

            if (string.Equals(item.Id, CrownId, StringComparison.OrdinalIgnoreCase))
            {
                SetDruidOnly(item);
                return;
            }

            if (string.Equals(item.Id, GraspId, StringComparison.OrdinalIgnoreCase))
            {
                SetPhysicalDpsOnly(item);
                return;
            }

            if (string.Equals(item.Id, GirdleId, StringComparison.OrdinalIgnoreCase))
            {
                SetCasterSupportOnly(item);
            }
        }

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
                CustomItemRegistry.GetItemById(spec.Id);

            if (existing != null)
            {
                current = existing;
                ConfigureCommon(current, spec);
                spec.Configure?.Invoke(current);
                CustomItemRegistry.RegisterWithNativeDatabase(database, current);
                return current;
            }

            Item template =
                ResolveTemplate(database, spec);

            if (template == null)
            {
                Plugin.NativeLog.LogError(
                    "[NhalSeraEquipment] no native template could be resolved for [" +
                    spec.Name +
                    "] [" +
                    spec.Id +
                    "].");

                return null;
            }

            Item item =
                UnityEngine.Object.Instantiate(template);

            if (item == null)
            {
                return null;
            }

            ConfigureCommon(item, spec);
            spec.Configure?.Invoke(item);

            CustomItemRegistry.RegisterItem(item);
            CustomItemRegistry.RegisterWithNativeDatabase(database, item);

            string visualSource =
                string.Equals(
                    item.Id,
                    CarapaceId,
                    StringComparison.OrdinalIgnoreCase)
                    ? "Native Eventide Chestguard / FOTF runtime palette"
                    : GetPrefabName(item);

            Plugin.NativeLog.LogInfo(
                "[NhalSeraEquipment] registered [" +
                item.ItemName +
                "] Id=[" +
                item.Id +
                "] Template=[" +
                template.ItemName +
                "] Visual=[" +
                (visualSource ?? "<native>") +
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

            return FindNativeItem(database, spec.TemplateFallback);
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
                NhalSeraEquipmentIconLoader.Load(
                    spec.IconFile);

            if (icon != null)
            {
                item.ItemIcon = icon;
            }

            item.hideFlags =
                HideFlags.HideAndDontSave;
        }

        private static void ConfigureLightlessScepter(Item item)
        {
            ResetStats(item);

            item.RequiredSlot = Item.SlotType.PrimaryOrSecondary;
            item.Shield = false;
            item.IsBow = false;
            item.IsWand = true;

            item.End = 24;
            item.Agi = 12;
            item.Int = 86;
            item.Wis = 42;
            item.Cha = 32;
            item.Res = 12;

            item.HP = 850;
            item.Mana = 750;
            item.AC = 85;

            item.MR = 12;
            item.PR = 10;
            item.ER = 10;
            item.VR = 24;

            item.WeaponDmg = 62;
            item.WeaponDly = 1.45f;
            item.WandRange = 25;
            item.Relic = false;

            SetArcanistDruidBlightcallerOnly(item);

            item.WeaponProcChance = 18f;
            item.WeaponProcOnHit = GetLightlessTooltipProc();
        }

        private static void ConfigureCarapace(Item item)
        {
            ResetStats(item);

            item.RequiredSlot = Item.SlotType.Chest;
            item.Shield = false;
            item.IsBow = false;
            item.IsWand = false;

            item.Str = 42;
            item.End = 84;
            item.Dex = 18;
            item.Agi = 8;
            item.Int = 10;
            item.Wis = 20;
            item.Res = 18;

            item.HP = 1750;
            item.Mana = 250;
            item.AC = 340;

            item.MR = 16;
            item.PR = 18;
            item.ER = 14;
            item.VR = 28;

            item.Relic = false;

            // Native Synty armour palette.
            //
            // POLYGON_CustomCharacters exposes these exact armour channels:
            //   _Color_Primary
            //   _Color_Secondary
            //   _Color_Leather_Primary
            //   _Color_Leather_Secondary
            //   _Color_Metal_Primary
            //   _Color_Metal_Secondary
            //   _Color_Metal_Dark
            //
            // Erenshor mirrors them on Item through the fields below. Keeping
            // the recolour in these native channels means the colour follows
            // the actual Eventide polygons/plate seams instead of floating
            // procedural geometry.
            // Native Eventide palette tuned from the debug slot-map pass.
            //
            // Debug mapping showed:
            //   Primary            -> chest V/chevron + waist band
            //   Secondary          -> small accent details
            //   Leather Primary    -> thin belt/trim band
            //   Leather Secondary  -> minor leather detail
            //   Metal Primary      -> main torso shell + shoulder spikes
            //   Metal Secondary    -> broad pauldron plates
            //   Metal Dark         -> deepest metal regions
            //
            // The mockup can therefore be approximated natively by making the
            // Primary slot the luminous Void accent while keeping both metal
            // slots nearly black. No detached chest geometry is required.
            // MOVEMENT PERFORMANCE SPLIT
            //
            // HeavyPlate5 now receives the final working CHEST palette directly
            // through Erenshor's own Item colour path. This means FOTF no
            // longer has to call SetPropertyBlock on the animated torso every
            // LateUpdate while the player is moving.
            //
            // The native shoulder attachment is recoloured independently in
            // FateEquipmentVisuals.EventideVariants.cs.
            item.ItemPrimaryColor =
                new Color(
                    2.35f,
                    0.24f,
                    3.60f,
                    1f);

            item.ItemSecondaryColor =
                new Color(
                    0.72f,
                    0.10f,
                    1.05f,
                    1f);

            item.ItemLeatherPrimary =
                new Color(
                    0.34f,
                    0.06f,
                    0.50f,
                    1f);

            item.ItemLeatherSecondary =
                new Color(
                    0.08f,
                    0.025f,
                    0.11f,
                    1f);

            item.ItemMetalPrimary =
                new Color(
                    0.018f,
                    0.014f,
                    0.024f,
                    1f);

            item.ItemMetalSecondary =
                new Color(
                    0.040f,
                    0.034f,
                    0.052f,
                    1f);

            item.ItemMetalDark =
                new Color(
                    0.006f,
                    0.005f,
                    0.008f,
                    1f);

            SetPaladinReaverOnly(item);

            // 35 mirrors the health threshold in the collapsed native line.
            // This proxy is suppressed from Stats.CheckProc at runtime.
            item.WeaponProcChance = 35f;
            item.WeaponProcOnHit = GetCarapaceTooltipProc();
        }

        private static void ConfigureCrown(Item item)
        {
            ResetStats(item);

            item.RequiredSlot = Item.SlotType.Head;
            item.Shield = false;
            item.IsBow = false;
            item.IsWand = false;

            item.End = 32;
            item.Agi = 10;
            item.Int = 52;
            item.Wis = 90;
            item.Cha = 46;
            item.Res = 14;

            item.HP = 900;
            item.Mana = 925;
            item.AC = 110;

            item.MR = 14;
            item.PR = 14;
            item.ER = 12;
            item.VR = 26;

            // The custom crown is intended to sit on the model rather than
            // hide the entire head/hair mesh.
            item.HideHeadWhenEquipped = false;
            item.HideHairWhenEquipped = false;
            item.Relic = false;

            SetDruidOnly(item);

            item.WeaponProcChance = 15f;
            item.WeaponProcOnHit = GetCrownTooltipProc();
        }

        private static void ConfigureGrasp(Item item)
        {
            ResetStats(item);

            item.RequiredSlot = Item.SlotType.Hand;
            item.Shield = false;
            item.IsBow = false;
            item.IsWand = false;

            item.Str = 72;
            item.End = 28;
            item.Dex = 68;
            item.Agi = 44;
            item.Res = 10;

            item.HP = 950;
            item.Mana = 175;
            item.AC = 130;

            item.MR = 8;
            item.PR = 16;
            item.ER = 12;
            item.VR = 20;

            item.Relic = false;

            SetPhysicalDpsOnly(item);

            item.WeaponProcChance = 15f;
            item.WeaponProcOnHit = GetGraspTooltipProc();
        }

        private static void ConfigureGirdle(Item item)
        {
            ResetStats(item);

            item.RequiredSlot = Item.SlotType.Waist;
            item.Shield = false;
            item.IsBow = false;
            item.IsWand = false;

            item.End = 28;
            item.Agi = 18;
            item.Int = 68;
            item.Wis = 54;
            item.Cha = 28;
            item.Res = 12;

            item.HP = 800;
            item.Mana = 725;
            item.AC = 120;

            item.MR = 14;
            item.PR = 10;
            item.ER = 12;
            item.VR = 24;

            item.Relic = false;

            SetCasterSupportOnly(item);

            item.WeaponProcChance = 10f;
            item.WeaponProcOnHit = GetGirdleTooltipProc();
        }

        private static void ResetStats(Item item)
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

        private static Spell GetLightlessTooltipProc()
        {
            if (_lightlessTooltipProc == null)
            {
                _lightlessTooltipProc = CreateTooltipProc(
                    "FOTF_TOOLTIP_NHALSERA_LIGHTLESS_ECHO",
                    "Lightless Echo",
                    "Offensive spell damage or a successful scepter hit has an 18% chance to deal 3,400 Void damage and restore 150 Mana. 5 second internal cooldown.");
            }

            return _lightlessTooltipProc;
        }

        private static Spell GetCarapaceTooltipProc()
        {
            if (_carapaceTooltipProc == null)
            {
                _carapaceTooltipProc = CreateTooltipProc(
                    "FOTF_TOOLTIP_NHALSERA_EVENT_HORIZON_GUARD",
                    "Event Horizon Guard",
                    "Falling below 35% Health grants +250 Armor and 20% damage reduction against Physical and Void damage for 10 seconds. 45 second internal cooldown.");
            }

            return _carapaceTooltipProc;
        }

        private static Spell GetCrownTooltipProc()
        {
            if (_crownTooltipProc == null)
            {
                _crownTooltipProc = CreateTooltipProc(
                    "FOTF_TOOLTIP_NHALSERA_VEILED_BENEDICTION",
                    "Veiled Benediction",
                    "Direct heals have a 15% chance to heal every living member of your group for 900 and grant +8 Magic Resist and +8 Void Resist for 8 seconds. 20 second internal cooldown.");
            }

            return _crownTooltipProc;
        }

        private static Spell GetGraspTooltipProc()
        {
            if (_graspTooltipProc == null)
            {
                _graspTooltipProc = CreateTooltipProc(
                    "FOTF_TOOLTIP_NHALSERA_FRACTURED_MOMENTUM",
                    "Fractured Momentum",
                    "Physical weapon attacks have a 15% chance to grant +12% Weapon Haste, +8% Armor Penetration and +20 Dexterity for 8 seconds. 20 second internal cooldown.");
            }

            return _graspTooltipProc;
        }

        private static Spell GetGirdleTooltipProc()
        {
            if (_girdleTooltipProc == null)
            {
                _girdleTooltipProc = CreateTooltipProc(
                    "FOTF_TOOLTIP_NHALSERA_THRESHOLD_COMMUNION",
                    "Threshold Communion",
                    "Damaging spells or direct heals have a 10% chance to grant Mana Regen V to every living member of your group for 12 seconds. 30 second internal cooldown.");
            }

            return _girdleTooltipProc;
        }

        private static Spell CreateTooltipProc(
            string id,
            string spellName,
            string description)
        {
            Spell spell =
                ScriptableObject.CreateInstance<Spell>();

            spell.Id = id;
            spell.SpellName = spellName;
            spell.SpellDesc = description;
            spell.hideFlags = HideFlags.HideAndDontSave;

            return spell;
        }


        private static void SetArcanistDruidBlightcallerOnly(Item item)
        {
            List<Class> previous =
                item.Classes != null
                    ? new List<Class>(item.Classes)
                    : new List<Class>();

            item.Classes = new List<Class>();

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

        private static void SetPaladinReaverOnly(Item item)
        {
            List<Class> previous =
                item.Classes != null
                    ? new List<Class>(item.Classes)
                    : new List<Class>();

            item.Classes = new List<Class>();

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

        private static void SetDruidOnly(Item item)
        {
            List<Class> previous =
                item.Classes != null
                    ? new List<Class>(item.Classes)
                    : new List<Class>();

            item.Classes = new List<Class>();

            AddClassIfMissing(
                item.Classes,
                ResolveClass(
                    previous,
                    "Druid",
                    GameData.ClassDB != null
                        ? GameData.ClassDB.Druid
                        : null));
        }

        private static void SetPhysicalDpsOnly(Item item)
        {
            List<Class> previous =
                item.Classes != null
                    ? new List<Class>(item.Classes)
                    : new List<Class>();

            item.Classes = new List<Class>();

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

            AddClassIfMissing(
                item.Classes,
                ResolveClass(
                    previous,
                    "Stormcaller",
                    GameData.ClassDB != null
                        ? GameData.ClassDB.Stormcaller
                        : null));
        }

        private static void SetCasterSupportOnly(Item item)
        {
            List<Class> previous =
                item.Classes != null
                    ? new List<Class>(item.Classes)
                    : new List<Class>();

            item.Classes = new List<Class>();

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
                string.IsNullOrEmpty(className))
            {
                return null;
            }

            for (int i = 0; i < existing.Count; i++)
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
    }
}
