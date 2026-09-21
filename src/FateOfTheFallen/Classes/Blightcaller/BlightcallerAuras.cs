using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    internal sealed class BlightcallerAuraDefinition
    {
        internal string Key;
        internal int ItemId;

        internal string ItemName;
        internal string SpellName;
        internal string Description;
        internal string Lore;

        internal int Level;
        internal int Price;
        internal int Mana;

        internal int End;
        internal int Int;
        internal int Wis;
        internal int Cha;

        internal Color Color;
    }

    internal static class BlightcallerAuras
    {
        private const string AuraSpellPrefix =
            "ARCBLC_AURA_";

        private const string AuraItemPrefix =
            "ARCBLC_AURA_ITEM_";

        /*
         * Blightcaller custom item ID allocation:
         *
         * 1600-1649 = Spell Scrolls
         * 1650+     = Equipment / Other Items
         *
         * Aura items currently occupy 1650-1654.
         */

        private static readonly List<BlightcallerAuraDefinition>
            Definitions =
                new List<BlightcallerAuraDefinition>
                {
                    new BlightcallerAuraDefinition
                    {
                        Key = "withering",
                        ItemId = 1650,

                        ItemName = "Aura of Withering",
                        SpellName =
                            "Aura: Aura of Withering",

                        Description =
                            "Increase party Intelligence, Wisdom, Charisma, and Endurance.",

                        Lore =
                            "The air around the Blightcaller grows heavy with the patient decay of the Void.",

                        Level = 1,
                        Price = 5,
                        Mana = 5,

                        End = 2,
                        Int = 4,
                        Wis = 4,
                        Cha = 4,

                        Color =
                            new Color(
                                0.36f,
                                0.12f,
                                0.48f,
                                1f)
                    },

                    new BlightcallerAuraDefinition
                    {
                        Key = "pestilent_essence",
                        ItemId = 1651,

                        ItemName =
                            "Aura of Pestilent Essence",

                        SpellName =
                            "Aura: Aura of Pestilent Essence",

                        Description =
                            "Increase party Intelligence, Wisdom, Charisma, and Endurance.",

                        Lore =
                            "The Blightcaller radiates a corrupting presence that seeps into the minds and bodies of nearby allies.",

                        Level = 8,
                        Price = 1500,
                        Mana = 8,

                        End = 4,
                        Int = 8,
                        Wis = 8,
                        Cha = 8,

                        Color =
                            new Color(
                                0.40f,
                                0.14f,
                                0.52f,
                                1f)
                    },

                    new BlightcallerAuraDefinition
                    {
                        Key = "festering_power",
                        ItemId = 1652,

                        ItemName =
                            "Aura of Festering Power",

                        SpellName =
                            "Aura: Aura of Festering Power",

                        Description =
                            "Increase party Intelligence, Wisdom, Charisma, and Endurance.",

                        Lore =
                            "The power of the Blight deepens, turning decay into a weapon against the enemies of the covenant.",

                        Level = 16,
                        Price = 2500,
                        Mana = 12,

                        End = 6,
                        Int = 12,
                        Wis = 15,
                        Cha = 12,

                        Color =
                            new Color(
                                0.44f,
                                0.16f,
                                0.56f,
                                1f)
                    },

                    new BlightcallerAuraDefinition
                    {
                        Key = "blighted_covenant",
                        ItemId = 1653,

                        ItemName =
                            "Aura of the Blighted Covenant",

                        SpellName =
                            "Aura: Aura of the Blighted Covenant",

                        Description =
                            "Increase party Intelligence, Wisdom, Charisma, and Endurance.",

                        Lore =
                            "Those who stand beside the Blightcaller become bound by a covenant written in decay and shadow.",

                        Level = 29,
                        Price = 5000,
                        Mana = 16,

                        End = 10,
                        Int = 16,
                        Wis = 22,
                        Cha = 16,

                        Color =
                            new Color(
                                0.48f,
                                0.18f,
                                0.60f,
                                1f)
                    },

                    new BlightcallerAuraDefinition
                    {
                        Key = "eternal_blight",
                        ItemId = 1654,

                        ItemName =
                            "Aura of Eternal Blight",

                        SpellName =
                            "Aura: Aura of Eternal Blight",

                        Description =
                            "Increase party Intelligence, Wisdom, Charisma, and Endurance.",

                        Lore =
                            "At the height of the Blightcaller's power, the boundary between the living world and the eternal Blight begins to disappear.",

                        Level = 35,
                        Price = 15000,
                        Mana = 20,

                        End = 15,
                        Int = 40,
                        Wis = 40,
                        Cha = 40,

                        Color =
                            new Color(
                                0.54f,
                                0.20f,
                                0.68f,
                                1f)
                    }
                };

        private static readonly Dictionary<string, Spell>
            SpellsByKey =
                new Dictionary<string, Spell>(
                    StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Item>
            ItemsByKey =
                new Dictionary<string, Item>(
                    StringComparer.OrdinalIgnoreCase);

        internal static IReadOnlyDictionary<string, Spell>
            AllSpells
        {
            get
            {
                return SpellsByKey;
            }
        }

        internal static IReadOnlyDictionary<string, Item>
            AllItems
        {
            get
            {
                return ItemsByKey;
            }
        }

        internal static bool IsBlightcallerAura(
            Spell spell)
        {
            if (spell == null ||
                string.IsNullOrEmpty(spell.Id))
            {
                return false;
            }

            return spell.Id.StartsWith(
                AuraSpellPrefix,
                StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsBlightcallerAuraItem(
            Item item)
        {
            if (item == null ||
                string.IsNullOrEmpty(item.Id))
            {
                return false;
            }

            foreach (
                BlightcallerAuraDefinition definition
                in Definitions)
            {
                if (string.Equals(
                        item.Id,
                        definition.ItemId.ToString(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return item.Id.StartsWith(
                AuraItemPrefix,
                StringComparison.OrdinalIgnoreCase);
        }

        internal static void Register(
            SpellDB spellDB)
        {
            if (spellDB == null ||
                spellDB.SpellDatabase == null)
            {
                return;
            }

            BlightcallerCatalog.EnsureClass();

            Spell template =
                FindNativeAuraTemplate(
                    spellDB.SpellDatabase);

            List<Spell> combinedSpells =
                new List<Spell>(
                    spellDB.SpellDatabase);

            foreach (
                BlightcallerAuraDefinition definition
                in Definitions)
            {
                Spell auraSpell;

                if (!SpellsByKey.TryGetValue(
                        definition.Key,
                        out auraSpell))
                {
                    auraSpell =
                        CreateSpell(
                            definition,
                            template);

                    SpellsByKey.Add(
                        definition.Key,
                        auraSpell);
                }

                if (!combinedSpells.Contains(
                        auraSpell))
                {
                    combinedSpells.Add(
                        auraSpell);
                }

                Item auraItem;

                if (!ItemsByKey.TryGetValue(
                        definition.Key,
                        out auraItem))
                {
                    auraItem =
                        CreateItem(
                            definition,
                            auraSpell);

                    ItemsByKey.Add(
                        definition.Key,
                        auraItem);

                    BlightcallerItemFactory.RegisterItem(
                        auraItem);
                }
            }

            spellDB.SpellDatabase =
                combinedSpells.ToArray();
        }

        internal static Spell GetSpell(
            string key)
        {
            Spell spell;

            if (string.IsNullOrEmpty(key))
                return null;

            if (!SpellsByKey.TryGetValue(
                    key,
                    out spell))
            {
                return null;
            }

            return spell;
        }

        internal static Item GetItem(
            string key)
        {
            Item item;

            if (string.IsNullOrEmpty(key))
                return null;

            if (!ItemsByKey.TryGetValue(
                    key,
                    out item))
            {
                return null;
            }

            return item;
        }

        private static Spell CreateSpell(
            BlightcallerAuraDefinition definition,
            Spell template)
        {
            Spell spell =
                ScriptableObject.CreateInstance<Spell>();

            spell.name =
                "CS BLC AURA - " +
                definition.ItemName;

            spell.Id =
                AuraSpellPrefix +
                definition.Key.ToUpperInvariant();

            spell.UsedBy =
                new List<Class>
                {
                    BlightcallerCatalog.BlightcallerClass
                };

            /*
             * Duelist_Armor_Pen is only the native enum
             * placeholder. Patch_BlightcallerAuraSpellLine
             * supplies the actual Blightcaller aura hierarchy.
             */
            spell.Line =
                Spell.SpellLine.Duelist_Armor_Pen;

            spell.Type =
                Spell.SpellType.Beneficial;

            spell.RequiredLevel =
                definition.Level;

            spell.SpellName =
                definition.SpellName;

            spell.SpellDesc =
                definition.Description;

            spell.SpellChargeFXIndex =
                template != null
                    ? template.SpellChargeFXIndex
                    : 0;

            spell.SpellResolveFXIndex =
                template != null
                    ? template.SpellResolveFXIndex
                    : 24;

            spell.SpellChargeTime =
                template != null
                    ? template.SpellChargeTime
                    : 60f;

            spell.SpellDurationInTicks =
                2;

            spell.Mana =
                definition.Mana;

            spell.End =
                definition.End;

            spell.Int =
                definition.Int;

            spell.Wis =
                definition.Wis;

            spell.Cha =
                definition.Cha;

            spell.SpellRange =
                30f;

            spell.CanHitPlayers =
                true;

            spell.SimUsable =
                false;

            spell.NoResonate =
                true;

            spell.StatusEffectMessageOnPlayer =
                "feel the power of the Blight.";

            spell.StatusEffectMessageOnNPC =
                "feels the power of the Blight.";

            if (template != null)
            {
                spell.ChargeSound =
                    template.ChargeSound;

                spell.CompleteSound =
                    template.CompleteSound;

                spell.ChargeVariations =
                    template.ChargeVariations != null
                        ? new List<AudioClip>(
                            template.ChargeVariations)
                        : new List<AudioClip>();

                spell.CompleteVariations =
                    template.CompleteVariations != null
                        ? new List<AudioClip>(
                            template.CompleteVariations)
                        : new List<AudioClip>();

                spell.SpellIcon =
                    template.SpellIcon;
            }
            else
            {
                spell.ChargeVariations =
                    new List<AudioClip>();

                spell.CompleteVariations =
                    new List<AudioClip>();
            }

            /*
             * Use the custom Blightcaller aura icon for
             * the aura spell itself.
             *
             * This is the icon used by the active aura/
             * buff display as well as spell UI that reads
             * SpellIcon.
             */
            Sprite auraIcon =
                BlightcallerItemFactory.GetCustomAuraIcon();

            if (auraIcon != null)
            {
                spell.SpellIcon =
                    auraIcon;
            }

            spell.color =
                definition.Color;

            spell.hideFlags =
                HideFlags.HideAndDontSave;

            return spell;
        }

        private static Item CreateItem(
            BlightcallerAuraDefinition definition,
            Spell auraSpell)
        {
            Item item =
                ScriptableObject.CreateInstance<Item>();

            item.name =
                "CS BLC AURA ITEM - " +
                definition.ItemName;

            /*
             * IMPORTANT:
             *
             * Aura items use the 1650+ general item range.
             * Their stable item ID is numeric so the native
             * /additem command can use it.
             */
            item.Id =
                definition.ItemId.ToString();

            item.ItemName =
                definition.ItemName;

            item.ItemLevel =
                definition.Level;

            item.ItemValue =
                definition.Price;

            item.RequiredSlot =
                Item.SlotType.Aura;

            item.Classes =
                new List<Class>
                {
                    BlightcallerCatalog.BlightcallerClass
                };

            item.Aura =
                auraSpell;

            item.Lore =
                definition.Lore +
                "\n\n" +
                definition.Description +
                "\nRequired level: " +
                definition.Level +
                ".";

            item.Stackable =
                false;

            item.Disposable =
                false;

            item.Unique =
                false;

            item.PlayerCannotSell =
                false;

            item.SimPlayersCantGet =
                false;

            item.ItemIcon =
                BlightcallerItemFactory.GetCustomAuraIcon();

            item.hideFlags =
                HideFlags.HideAndDontSave;

            return item;
        }

        private static Spell FindNativeAuraTemplate(
            Spell[] nativeSpells)
        {
            if (nativeSpells == null)
                return null;

            foreach (
                Spell spell
                in nativeSpells)
            {
                if (spell == null)
                    continue;

                if (spell.Line !=
                    Spell.SpellLine.Aura_Arcanist)
                {
                    continue;
                }

                if (spell.SpellIcon == null)
                    continue;

                return spell;
            }

            foreach (
                Spell spell
                in nativeSpells)
            {
                if (spell == null)
                    continue;

                if (spell.SpellIcon == null)
                    continue;

                if (spell.Line ==
                        Spell.SpellLine.Aura_AC ||
                    spell.Line ==
                        Spell.SpellLine.Aura_Druid ||
                    spell.Line ==
                        Spell.SpellLine.Aura_Paladin ||
                    spell.Line ==
                        Spell.SpellLine.Aura_Reaver ||
                    spell.Line ==
                        Spell.SpellLine.Aura_Stormcaller)
                {
                    return spell;
                }
            }

            return null;
        }
    }
}