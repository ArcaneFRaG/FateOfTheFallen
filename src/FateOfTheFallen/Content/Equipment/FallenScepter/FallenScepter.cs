using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Level-35 custom equipment proof of concept.
    ///
    /// The item clones the native Corruptor scepter so it inherits
    /// Erenshor's known-good scepter slot, animation, sound and restrictions,
    /// then applies Fate of the Fallen stats, proc, icon and custom visual.
    /// </summary>
    internal static class FallenScepter
    {
        internal const string ItemId =
            "1710";

        internal const string ProcSpellId =
            "FOTF_PROC_FALLEN_SPARK";

        private const string ItemName =
            "Fallen Scepter";

        private const string NativeItemTemplateName =
            "Corruptor";


        // ============================================================
        // TUNABLE ITEM VALUES
        // ============================================================
        //
        // Edit these values directly while balancing the item.
        //
        // For nullable values:
        //     null = keep the value inherited from the native Corruptor
        //     0    = explicitly set the stat to zero
        //     N    = explicitly set the stat to N
        //
        // This lets you change one stat at a time without having to know
        // every original Corruptor value up front.
        // ============================================================

        private const int ItemLevel =
            35;

        private static readonly int? HitPoints =
            1000;

        private static readonly int? ArmorClass =
            150;

        private static readonly int? Mana =
            600;

        private static readonly int? WeaponDamage =
            60;

        private static readonly float? WeaponDelay =
            2f;


        // Primary stats.

        private static readonly int? Strength =
            0;

        private static readonly int? Endurance =
            0;

        private static readonly int? Dexterity =
            0;

        private static readonly int? Agility =
            15;

        private static readonly int? Intelligence =
            50;

        private static readonly int? Wisdom =
            25;

        private static readonly int? Charisma =
            25;

        // Native Item.Res is Resonance. Erenshor applies its normal
        // item-quality scaling through Item.CalcRes(...).
        private static readonly int? Resonance =
            10;


        // General / elemental resistances.

        private static readonly int? MagicResistance =
            10;

        private static readonly int? ElementalResistance =
            10;

        private static readonly int? PoisonResistance =
            10;

        private static readonly int? VoidResistance =
            10;


        // Economy.

        private static readonly int? ItemValue =
            50000;


        // Stat scaling.

        private static readonly float? StrengthScaling =
            null;

        private static readonly float? EnduranceScaling =
            null;

        private static readonly float? DexterityScaling =
            null;

        private static readonly float? AgilityScaling =
            null;

        private static readonly float? IntelligenceScaling =
            null;

        private static readonly float? WisdomScaling =
            null;

        private static readonly float? CharismaScaling =
            null;

        private static readonly float? ResistScaling =
            null;

        private static readonly float? MitigationScaling =
            null;


        // Proc tuning.

        private const float ProcChance =
            5f;

        // Intentionally obvious during testing.
        private const int ProcDamage =
            1000;


        private static Item _item;
        private static Spell _procSpell;

        internal static Item Item
        {
            get
            {
                return _item;
            }
        }

        internal static Spell ProcSpell
        {
            get
            {
                return _procSpell;
            }
        }

        // ============================================================
        // SPELL REGISTRATION
        // ============================================================

        internal static void RegisterProcSpell(
            SpellDB database)
        {
            if (database == null ||
                database.SpellDatabase == null)
            {
                return;
            }

            Spell existing =
                FindSpellById(
                    database,
                    ProcSpellId);

            if (existing != null)
            {
                _procSpell =
                    existing;

                LinkProcToItem();

                return;
            }

            Spell template =
                FindProcTemplate(
                    database);

            if (template == null)
            {
                Plugin.NativeLog.LogError(
                    "Fallen Scepter: could not find a native direct-damage spell template for Fallen Spark.");

                return;
            }

            Spell spell =
                UnityEngine.Object.Instantiate(
                    template);

            if (spell == null)
            {
                Plugin.NativeLog.LogError(
                    "Fallen Scepter: failed to clone the Fallen Spark spell template.");

                return;
            }

            spell.name =
                "FOTF - Fallen Spark";

            spell.Id =
                ProcSpellId;

            spell.SpellName =
                "Fallen Spark";

            spell.SpellDesc =
                "5% chance on a weapon hit: Fallen Spark deals 1,000 Magic damage at no Mana cost.";

            spell.Type =
                Spell.SpellType.Damage;

            spell.Line =
                Spell.SpellLine.Direct_Damage;

            spell.RequiredLevel =
                1;

            spell.ManaCost =
                0;

            spell.TargetDamage =
                ProcDamage;

            spell.Aggro =
                ProcDamage;

            spell.SpellChargeTime =
                0f;

            spell.Cooldown =
                0f;

            spell.SpellDurationInTicks =
                0;

            spell.StatusEffectToApply =
                null;

            spell.MyDamageType =
                GameData.DamageType.Void;

            spell.SpellRange =
                60f;

            spell.SelfOnly =
                false;

            spell.CrowdControlSpell =
                false;

            spell.SimUsable =
                false;

            spell.NoResonate =
                true;

            spell.CanHitPlayers =
                true;

            spell.UsedBy =
                new List<Class>();

            spell.hideFlags =
                HideFlags.HideAndDontSave;

            _procSpell =
                spell;

            CustomSpellRegistry.Register(
                database,
                spell);

            LinkProcToItem();

        }

        // ============================================================
        // ITEM REGISTRATION
        // ============================================================

        internal static Item RegisterItem(
            ItemDatabase database)
        {
            if (database == null)
            {
                return null;
            }

            Item registered =
                CustomItemRegistry.GetItemById(
                    ItemId);

            if (registered != null)
            {
                _item =
                    registered;

                ConfigureItem(
                    _item);

                CustomItemRegistry.RegisterWithNativeDatabase(
                    database,
                    _item);

                return _item;
            }

            Item item =
                CustomItemRegistry.CloneNativeItem(
                    database,
                    NativeItemTemplateName,
                    ItemId,
                    ItemName);

            if (item == null)
            {
                Plugin.NativeLog.LogError(
                    "Fallen Scepter: native template [" +
                    NativeItemTemplateName +
                    "] was not found. Item registration aborted.");

                return null;
            }

            _item =
                item;

            ConfigureItem(
                item);

            CustomItemRegistry.RegisterItem(
                item);

            CustomItemRegistry.RegisterWithNativeDatabase(
                database,
                item);


            return item;
        }

        internal static Item GetItem()
        {
            if (_item != null)
            {
                return _item;
            }

            _item =
                CustomItemRegistry.GetItemById(
                    ItemId);

            return _item;
        }

        // ============================================================
        // ITEM CONFIGURATION
        // ============================================================

        private static void ConfigureItem(
            Item item)
        {
            if (item == null)
            {
                return;
            }

            item.name =
                "FOTF - Fallen Scepter";

            item.Id =
                ItemId;

            item.ItemName =
                ItemName;

            item.ItemLevel =
                ItemLevel;

            ApplyTunableStats(
                item);

            item.Lore =
                "A corrupted scepter whose core crackles with unstable void energy. " +
                "Each strike carries a chance to release a Fallen Spark.";

            item.Stackable =
                false;

            item.Disposable =
                false;

            item.WeaponProcChance =
                ProcChance;

            // Do not retain any proc that may exist on the Corruptor
            // template while SpellDB is still initializing. The custom
            // Fallen Spark reference is linked as soon as it exists.
            item.WeaponProcOnHit =
                _procSpell;


            // ========================================================
            // CUSTOM ITEM ICON
            // ========================================================
            //
            // Load the 64x64 Fallen Scepter icon that is embedded
            // directly into FateOfTheFallen.dll by the icon loader.
            // ========================================================

            Sprite customIcon =
                FallenScepterIconLoader.Load();

            if (customIcon != null)
            {
                item.ItemIcon =
                    customIcon;
            }

            item.hideFlags =
                HideFlags.HideAndDontSave;
        }

        // ============================================================
        // TUNABLE STAT APPLICATION
        // ============================================================

        private static void ApplyTunableStats(
            Item item)
        {
            if (item == null)
            {
                return;
            }

            if (HitPoints.HasValue)
            {
                item.HP =
                    HitPoints.Value;
            }

            if (ArmorClass.HasValue)
            {
                item.AC =
                    ArmorClass.Value;
            }

            if (Mana.HasValue)
            {
                item.Mana =
                    Mana.Value;
            }

            if (WeaponDamage.HasValue)
            {
                item.WeaponDmg =
                    WeaponDamage.Value;
            }

            if (WeaponDelay.HasValue)
            {
                item.WeaponDly =
                    WeaponDelay.Value;
            }


            if (Strength.HasValue)
            {
                item.Str =
                    Strength.Value;
            }

            if (Endurance.HasValue)
            {
                item.End =
                    Endurance.Value;
            }

            if (Dexterity.HasValue)
            {
                item.Dex =
                    Dexterity.Value;
            }

            if (Agility.HasValue)
            {
                item.Agi =
                    Agility.Value;
            }

            if (Intelligence.HasValue)
            {
                item.Int =
                    Intelligence.Value;
            }

            if (Wisdom.HasValue)
            {
                item.Wis =
                    Wisdom.Value;
            }

            if (Charisma.HasValue)
            {
                item.Cha =
                    Charisma.Value;
            }

            if (Resonance.HasValue)
            {
                item.Res =
                    Resonance.Value;
            }

            if (MagicResistance.HasValue)
            {
                item.MR =
                    MagicResistance.Value;
            }

            if (ElementalResistance.HasValue)
            {
                item.ER =
                    ElementalResistance.Value;
            }

            if (PoisonResistance.HasValue)
            {
                item.PR =
                    PoisonResistance.Value;
            }

            if (VoidResistance.HasValue)
            {
                item.VR =
                    VoidResistance.Value;
            }


            if (ItemValue.HasValue)
            {
                item.ItemValue =
                    ItemValue.Value;
            }


            if (StrengthScaling.HasValue)
            {
                item.StrScaling =
                    StrengthScaling.Value;
            }

            if (EnduranceScaling.HasValue)
            {
                item.EndScaling =
                    EnduranceScaling.Value;
            }

            if (DexterityScaling.HasValue)
            {
                item.DexScaling =
                    DexterityScaling.Value;
            }

            if (AgilityScaling.HasValue)
            {
                item.AgiScaling =
                    AgilityScaling.Value;
            }

            if (IntelligenceScaling.HasValue)
            {
                item.IntScaling =
                    IntelligenceScaling.Value;
            }

            if (WisdomScaling.HasValue)
            {
                item.WisScaling =
                    WisdomScaling.Value;
            }

            if (CharismaScaling.HasValue)
            {
                item.ChaScaling =
                    CharismaScaling.Value;
            }

            if (ResistScaling.HasValue)
            {
                item.ResistScaling =
                    ResistScaling.Value;
            }

            if (MitigationScaling.HasValue)
            {
                item.MitigationScaling =
                    MitigationScaling.Value;
            }
        }


        private static void LinkProcToItem()
        {
            if (_item == null)
            {
                _item =
                    CustomItemRegistry.GetItemById(
                        ItemId);
            }

            if (_item == null ||
                _procSpell == null)
            {
                return;
            }

            _item.WeaponProcChance =
                ProcChance;

            _item.WeaponProcOnHit =
                _procSpell;
        }

        // ============================================================
        // SPELL HELPERS
        // ============================================================

        private static Spell FindSpellById(
            SpellDB database,
            string id)
        {
            if (database == null ||
                database.SpellDatabase == null ||
                string.IsNullOrEmpty(id))
            {
                return null;
            }

            Spell[] spells =
                database.SpellDatabase;

            for (int i = 0;
                 i < spells.Length;
                 i++)
            {
                Spell spell =
                    spells[i];

                if (spell == null)
                {
                    continue;
                }

                if (string.Equals(
                        spell.Id,
                        id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return spell;
                }
            }

            return null;
        }

        private static Spell FindProcTemplate(
            SpellDB database)
        {
            if (database == null ||
                database.SpellDatabase == null)
            {
                return null;
            }

            Spell[] spells =
                database.SpellDatabase;

            // First choice: a native direct Void-damage spell.
            for (int i = 0;
                 i < spells.Length;
                 i++)
            {
                Spell spell =
                    spells[i];

                if (!IsUsableNativeDamageTemplate(
                        spell))
                {
                    continue;
                }

                if (spell.MyDamageType ==
                    GameData.DamageType.Void)
                {
                    return spell;
                }
            }

            // Fallback: any native direct-damage spell. We still
            // overwrite the actual damage type to Void below.
            for (int i = 0;
                 i < spells.Length;
                 i++)
            {
                Spell spell =
                    spells[i];

                if (IsUsableNativeDamageTemplate(
                        spell))
                {
                    return spell;
                }
            }

            return null;
        }

        private static bool IsUsableNativeDamageTemplate(
            Spell spell)
        {
            if (spell == null)
            {
                return false;
            }

            if (spell.Type !=
                Spell.SpellType.Damage)
            {
                return false;
            }

            if (spell.TargetDamage <= 0)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(
                    spell.Id))
            {
                if (spell.Id.StartsWith(
                        "ARCBLC_",
                        StringComparison.OrdinalIgnoreCase) ||
                    spell.Id.StartsWith(
                        "FOTF_",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
