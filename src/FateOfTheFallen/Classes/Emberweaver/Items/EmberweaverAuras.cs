using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    internal sealed class EmberweaverAuraDefinition
    {
        internal string Key;
        internal string ItemId;
        internal string ItemName;
        internal string SpellName;
        internal string Description;
        internal string Lore;
        internal int Level;
        internal int Price;
        internal int Haste;
        internal int CastSpeedPercent;
        internal Color Color;
    }

    /// <summary>
    /// Emberweaver's five-rank group aura line.
    ///
    /// Haste is carried by the native StatusEffect Haste stat, so attack-speed
    /// behaviour stays completely native. Spell cast speed is read from the
    /// active aura StatusEffect by Patch_EmberweaverAuraCastSpeed.
    /// </summary>
    internal static class EmberweaverAuras
    {
        private const string AuraSpellPrefix =
            "FOTF_EW_AURA_";

        // Use a dedicated out-of-band SpellLine value. Native Erenshor only
        // compares SpellLine values for equality, so this gives Emberweaver a
        // real rank hierarchy without colliding with Blightcaller or any
        // native class aura line.
        private const int AuraSpellLineValue =
            8701;

        internal const string KindlingKey =
            "kindling";

        internal const string EmbersKey =
            "embers";

        internal const string FurnaceKey =
            "furnace";

        internal const string IncandescenceKey =
            "incandescence";

        internal const string EternalFlameKey =
            "eternal_flame";

        private const string AuraIconFileName =
            "FOTF_Emberweaver_Aura.png";

        private static readonly List<EmberweaverAuraDefinition>
            Definitions =
                new List<EmberweaverAuraDefinition>
                {
                    new EmberweaverAuraDefinition
                    {
                        Key = KindlingKey,
                        ItemId = EmberweaverItemIds.AuraOfKindling,
                        ItemName = "Aura of Kindling",
                        SpellName = "Aura: Aura of Kindling",
                        Description = "Increase party Haste by 1 and spell cast speed by 1%.",
                        Lore = "A controlled ember passes from the Emberweaver to nearby allies, quickening weapon and spell alike.",
                        Level = 1,
                        Price = 5,
                        Haste = 1,
                        CastSpeedPercent = 1,
                        Color = new Color(0.90f, 0.28f, 0.08f, 1f)
                    },
                    new EmberweaverAuraDefinition
                    {
                        Key = EmbersKey,
                        ItemId = EmberweaverItemIds.AuraOfEmbers,
                        ItemName = "Aura of Embers",
                        SpellName = "Aura: Aura of Embers",
                        Description = "Increase party Haste by 2 and spell cast speed by 2%.",
                        Lore = "The Emberweaver's heat spreads through the group in a steady current of sparks and cinders.",
                        Level = 8,
                        Price = 1500,
                        Haste = 2,
                        CastSpeedPercent = 2,
                        Color = new Color(0.94f, 0.34f, 0.08f, 1f)
                    },
                    new EmberweaverAuraDefinition
                    {
                        Key = FurnaceKey,
                        ItemId = EmberweaverItemIds.AuraOfTheFurnace,
                        ItemName = "Aura of the Furnace",
                        SpellName = "Aura: Aura of the Furnace",
                        Description = "Increase party Haste by 3 and spell cast speed by 3%.",
                        Lore = "Allies fight within the rhythm of an unseen furnace, every motion driven by gathering heat.",
                        Level = 16,
                        Price = 2500,
                        Haste = 3,
                        CastSpeedPercent = 3,
                        Color = new Color(0.98f, 0.42f, 0.08f, 1f)
                    },
                    new EmberweaverAuraDefinition
                    {
                        Key = IncandescenceKey,
                        ItemId = EmberweaverItemIds.AuraOfIncandescence,
                        ItemName = "Aura of Incandescence",
                        SpellName = "Aura: Aura of Incandescence",
                        Description = "Increase party Haste by 4 and spell cast speed by 4%.",
                        Lore = "The air around the Emberweaver becomes incandescent, sharpening every strike and shortening every incantation.",
                        Level = 29,
                        Price = 5000,
                        Haste = 4,
                        CastSpeedPercent = 4,
                        Color = new Color(1.00f, 0.52f, 0.10f, 1f)
                    },
                    new EmberweaverAuraDefinition
                    {
                        Key = EternalFlameKey,
                        ItemId = EmberweaverItemIds.AuraOfEternalFlame,
                        ItemName = "Aura of Eternal Flame",
                        SpellName = "Aura: Aura of Eternal Flame",
                        Description = "Increase party Haste by 5 and spell cast speed by 5%.",
                        Lore = "A fragment of Brax's endless fire burns around the Emberweaver, driving the entire group beyond mortal cadence.",
                        Level = 35,
                        Price = 15000,
                        Haste = 5,
                        CastSpeedPercent = 5,
                        Color = new Color(1.00f, 0.64f, 0.14f, 1f)
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

        internal static bool IsEmberweaverAura(
            Spell spell)
        {
            return
                spell != null &&
                !string.IsNullOrEmpty(spell.Id) &&
                spell.Id.StartsWith(
                    AuraSpellPrefix,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsEmberweaverAuraItem(
            Item item)
        {
            if (item == null ||
                string.IsNullOrEmpty(item.Id))
            {
                return false;
            }

            for (int i = 0;
                 i < Definitions.Count;
                 i++)
            {
                if (string.Equals(
                        item.Id,
                        Definitions[i].ItemId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        internal static void Register(
            SpellDB spellDB)
        {
            if (spellDB == null ||
                spellDB.SpellDatabase == null)
            {
                return;
            }

            Class emberweaver =
                EmberweaverCatalog.TryEnsureClass();

            if (emberweaver == null)
            {
                return;
            }

            Spell template =
                FindNativeAuraTemplate(
                    spellDB.SpellDatabase);

            Sprite icon =
                EmbeddedSpriteLoader.LoadByFileName(
                    AuraIconFileName);

            for (int i = 0;
                 i < Definitions.Count;
                 i++)
            {
                EmberweaverAuraDefinition definition =
                    Definitions[i];

                Spell auraSpell;

                if (!SpellsByKey.TryGetValue(
                        definition.Key,
                        out auraSpell) ||
                    auraSpell == null)
                {
                    auraSpell =
                        FindExistingAuraSpell(
                            spellDB.SpellDatabase,
                            definition.Key);

                    if (auraSpell == null)
                    {
                        auraSpell =
                            CreateSpell(
                                definition,
                                template,
                                icon,
                                emberweaver);
                    }

                    if (auraSpell == null)
                    {
                        continue;
                    }

                    SpellsByKey[definition.Key] =
                        auraSpell;
                }

                auraSpell.UsedBy =
                    new List<Class>
                    {
                        emberweaver
                    };

                if (icon != null)
                {
                    auraSpell.SpellIcon =
                        icon;
                }

                CustomSpellRegistry.Register(
                    spellDB,
                    auraSpell);

                Item auraItem;

                if (!ItemsByKey.TryGetValue(
                        definition.Key,
                        out auraItem) ||
                    auraItem == null)
                {
                    auraItem =
                        CustomItemRegistry.GetItemById(
                            definition.ItemId);

                    if (auraItem == null)
                    {
                        auraItem =
                            CreateItem(
                                definition,
                                auraSpell,
                                icon,
                                emberweaver);
                    }

                    if (auraItem == null)
                    {
                        continue;
                    }

                    ItemsByKey[definition.Key] =
                        auraItem;
                }

                auraItem.Aura =
                    auraSpell;

                auraItem.Classes =
                    new List<Class>
                    {
                        emberweaver
                    };

                if (icon != null)
                {
                    auraItem.ItemIcon =
                        icon;
                }

                CustomItemRegistry.RegisterItem(
                    auraItem);
            }
        }

        internal static Item GetItem(
            string key)
        {
            Item item;

            if (string.IsNullOrEmpty(key) ||
                !ItemsByKey.TryGetValue(
                    key,
                    out item))
            {
                return null;
            }

            return item;
        }

        internal static Spell GetSpell(
            string key)
        {
            Spell spell;

            if (string.IsNullOrEmpty(key) ||
                !SpellsByKey.TryGetValue(
                    key,
                    out spell))
            {
                return null;
            }

            return spell;
        }

        internal static int GetActiveCastSpeedPercent(
            Stats stats)
        {
            if (stats == null ||
                stats.StatusEffects == null)
            {
                return 0;
            }

            int best =
                0;

            for (int i = 0;
                 i < stats.StatusEffects.Length;
                 i++)
            {
                StatusEffect status =
                    stats.StatusEffects[i];

                if (status == null ||
                    status.Effect == null)
                {
                    continue;
                }

                EmberweaverAuraDefinition definition;

                if (!TryGetDefinition(
                        status.Effect,
                        out definition))
                {
                    continue;
                }

                if (definition.CastSpeedPercent > best)
                {
                    best =
                        definition.CastSpeedPercent;
                }
            }

            return best;
        }

        internal static bool TryGetDefinition(
            Spell spell,
            out EmberweaverAuraDefinition definition)
        {
            definition =
                null;

            if (!IsEmberweaverAura(spell))
            {
                return false;
            }

            for (int i = 0;
                 i < Definitions.Count;
                 i++)
            {
                EmberweaverAuraDefinition candidate =
                    Definitions[i];

                string expectedId =
                    AuraSpellPrefix +
                    candidate.Key.ToUpperInvariant();

                if (string.Equals(
                        spell.Id,
                        expectedId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    definition =
                        candidate;

                    return true;
                }
            }

            return false;
        }

        private static Spell CreateSpell(
            EmberweaverAuraDefinition definition,
            Spell template,
            Sprite icon,
            Class emberweaver)
        {
            Spell spell =
                ScriptableObject.CreateInstance<Spell>();

            spell.name =
                "FOTF EW AURA - " +
                definition.ItemName;

            spell.Id =
                AuraSpellPrefix +
                definition.Key.ToUpperInvariant();

            spell.UsedBy =
                new List<Class>
                {
                    emberweaver
                };

            spell.Line =
                (Spell.SpellLine)AuraSpellLineValue;

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

            spell.Haste =
                definition.Haste;

            spell.SpellRange =
                30f;

            spell.CanHitPlayers =
                true;

            spell.SimUsable =
                false;

            spell.NoResonate =
                true;

            spell.StatusEffectMessageOnPlayer =
                "feel the rhythm of living flame.";

            spell.StatusEffectMessageOnNPC =
                "moves with the rhythm of living flame.";

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

            if (icon != null)
            {
                spell.SpellIcon =
                    icon;
            }

            spell.color =
                definition.Color;

            spell.hideFlags =
                HideFlags.HideAndDontSave;

            return spell;
        }

        private static Item CreateItem(
            EmberweaverAuraDefinition definition,
            Spell auraSpell,
            Sprite icon,
            Class emberweaver)
        {
            Item item =
                ScriptableObject.CreateInstance<Item>();

            item.name =
                "FOTF EW AURA ITEM - " +
                definition.ItemName;

            item.Id =
                definition.ItemId;

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
                    emberweaver
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
                icon;

            item.hideFlags =
                HideFlags.HideAndDontSave;

            return item;
        }

        private static Spell FindExistingAuraSpell(
            Spell[] spells,
            string key)
        {
            if (spells == null ||
                string.IsNullOrEmpty(key))
            {
                return null;
            }

            string expectedId =
                AuraSpellPrefix +
                key.ToUpperInvariant();

            for (int i = 0;
                 i < spells.Length;
                 i++)
            {
                Spell spell =
                    spells[i];

                if (spell == null ||
                    string.IsNullOrEmpty(spell.Id))
                {
                    continue;
                }

                if (string.Equals(
                        spell.Id,
                        expectedId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return spell;
                }
            }

            return null;
        }

        private static Spell FindNativeAuraTemplate(
            Spell[] spells)
        {
            if (spells == null)
            {
                return null;
            }

            for (int i = 0;
                 i < spells.Length;
                 i++)
            {
                Spell spell =
                    spells[i];

                if (spell == null ||
                    spell.SpellIcon == null)
                {
                    continue;
                }

                if (spell.Line == Spell.SpellLine.Aura_Arcanist)
                {
                    return spell;
                }
            }

            for (int i = 0;
                 i < spells.Length;
                 i++)
            {
                Spell spell =
                    spells[i];

                if (spell == null ||
                    spell.SpellIcon == null)
                {
                    continue;
                }

                if (spell.Line == Spell.SpellLine.Aura_AC ||
                    spell.Line == Spell.SpellLine.Aura_Druid ||
                    spell.Line == Spell.SpellLine.Aura_Paladin ||
                    spell.Line == Spell.SpellLine.Aura_Reaver ||
                    spell.Line == Spell.SpellLine.Aura_Stormcaller)
                {
                    return spell;
                }
            }

            return null;
        }
    }
}
