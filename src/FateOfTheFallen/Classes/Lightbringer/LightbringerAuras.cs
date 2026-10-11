using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    internal sealed class LightbringerAuraDefinition
    {
        internal string Key;
        internal string ItemId;
        internal string ItemName;
        internal string SpellName;
        internal string Description;
        internal string Lore;
        internal int Level;
        internal int Price;
        internal int Armor;
        internal int Resist;
        internal Color Color;
    }

    /// <summary>
    /// Lightbringer's five-rank group aura line.
    ///
    /// Native Aura-slot group effects: AC and four magical resistances.
    /// Each rank uses one isolated SpellLine (8702) so native aura upkeep
    /// replaces lower Lightbringer ranks without conflicting with other classes.
    /// </summary>
    internal static class LightbringerAuras
    {
        private const string AuraSpellPrefix =
            "FOTF_LB_AURA_";

        // Use a dedicated out-of-band SpellLine value. Native Erenshor only
        // compares SpellLine values for equality, so this gives Lightbringer a
        // real rank hierarchy without colliding with Blightcaller or any
        // native class aura line.
        private const int AuraSpellLineValue =
            8702;

        internal const string FirstLightKey = "first_light";
        internal const string DawnsEmbraceKey = "dawns_embrace";
        internal const string SanctuaryKey = "sanctuary";
        internal const string RadiantCovenantKey = "radiant_covenant";
        internal const string EternalDawnKey = "eternal_dawn";

        private const string AuraIconFileName = "FOTF_Lightbringer_Aura.png";
        private static Sprite _auraIcon;

        private static readonly List<LightbringerAuraDefinition> Definitions =
            new List<LightbringerAuraDefinition>
            {
                new LightbringerAuraDefinition { Key=FirstLightKey, ItemId=LightbringerItemIds.AuraOfFirstLight,
                    ItemName="Aura of First Light", SpellName="Aura: Aura of First Light", Level=1, Price=5, Armor=15, Resist=4,
                    Lore="The first radiance of dawn guards those who walk beside the Lightbringer.",
                    Description="Increase nearby group members' Armor Class by 15 and Magic, Poison, Elemental and Void Resistance by 4.",
                    Color=new Color(1f,0.84f,0.42f,1f) },
                new LightbringerAuraDefinition { Key=DawnsEmbraceKey, ItemId=LightbringerItemIds.AuraOfDawnsEmbrace,
                    ItemName="Aura of Dawn's Embrace", SpellName="Aura: Aura of Dawn's Embrace", Level=8, Price=1500, Armor=30, Resist=7,
                    Lore="A halo of morning light surrounds the Lightbringer's companions.",
                    Description="Increase nearby group members' Armor Class by 30 and Magic, Poison, Elemental and Void Resistance by 7.",
                    Color=new Color(1f,0.84f,0.42f,1f) },
                new LightbringerAuraDefinition { Key=SanctuaryKey, ItemId=LightbringerItemIds.AuraOfSanctuary,
                    ItemName="Aura of Sanctuary", SpellName="Aura: Aura of Sanctuary", Level=16, Price=2500, Armor=45, Resist=10,
                    Lore="The Lightbringer invokes an unwavering sanctuary against mortal and arcane harm.",
                    Description="Increase nearby group members' Armor Class by 45 and Magic, Poison, Elemental and Void Resistance by 10.",
                    Color=new Color(1f,0.84f,0.42f,1f) },
                new LightbringerAuraDefinition { Key=RadiantCovenantKey, ItemId=LightbringerItemIds.AuraOfRadiantCovenant,
                    ItemName="Aura of Radiant Covenant", SpellName="Aura: Aura of Radiant Covenant", Level=29, Price=5000, Armor=60, Resist=13,
                    Lore="An ancient covenant of celestial protection binds the Lightbringer and companions.",
                    Description="Increase nearby group members' Armor Class by 60 and Magic, Poison, Elemental and Void Resistance by 13.",
                    Color=new Color(1f,0.84f,0.42f,1f) },
                new LightbringerAuraDefinition { Key=EternalDawnKey, ItemId=LightbringerItemIds.AuraOfEternalDawn,
                    ItemName="Aura of Eternal Dawn", SpellName="Aura: Aura of Eternal Dawn", Level=35, Price=15000, Armor=75, Resist=16,
                    Lore="The Lightbringer's divine light endures where all other flames are extinguished.",
                    Description="Increase nearby group members' Armor Class by 75 and Magic, Poison, Elemental and Void Resistance by 16.",
                    Color=new Color(1f,0.84f,0.42f,1f) }
            };

        private static readonly Dictionary<string, Spell>
            SpellsByKey =
                new Dictionary<string, Spell>(
                    StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Item>
            ItemsByKey =
                new Dictionary<string, Item>(
                    StringComparer.OrdinalIgnoreCase);

        internal static bool IsLightbringerAura(
            Spell spell)
        {
            return
                spell != null &&
                !string.IsNullOrEmpty(spell.Id) &&
                spell.Id.StartsWith(
                    AuraSpellPrefix,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsLightbringerAuraItem(
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

            Class lightbringer =
                LightbringerCatalog.TryEnsureClass();

            if (lightbringer == null)
            {
                return;
            }

            Spell template =
                FindNativeAuraTemplate(
                    spellDB.SpellDatabase);

            Sprite icon = _auraIcon;
            if (icon == null)
                _auraIcon = icon = EmbeddedSpriteLoader.LoadByFileName(AuraIconFileName);

            for (int i = 0;
                 i < Definitions.Count;
                 i++)
            {
                LightbringerAuraDefinition definition =
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
                                lightbringer);
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
                        lightbringer
                    };
                // Recover the exact tier stats on Unity hot reload as well.
                auraSpell.MR = definition.Resist;
                auraSpell.PR = definition.Resist;
                auraSpell.ER = definition.Resist;
                auraSpell.VR = definition.Resist;
                SetNativeArmorBonus(auraSpell, definition.Armor);
                // A cached ScriptableObject from a prior session/hot reload
                // must not keep the previous tier tooltip after re-registration.
                auraSpell.SpellDesc = definition.Description;

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
                                lightbringer);
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
                auraItem.ItemLevel = definition.Level;
                auraItem.Lore = definition.Lore + "\n\n" +
                    definition.Description + "\nRequired level: " +
                    definition.Level + ".";

                auraItem.Classes =
                    new List<Class>
                    {
                        lightbringer
                    };

                if (icon != null)
                {
                    auraItem.ItemIcon =
                        icon;
                }

                // Keep both the FOTF canonical registry and native item lists
                // in sync. RegisterWithNativeDatabase is ID-idempotent, so
                // repeated SpellDB/ItemDatabase/vendor startup is safe.
                CustomItemRegistry.RegisterItem(
                    auraItem);
                if (GameData.ItemDB != null)
                    CustomItemRegistry.RegisterWithNativeDatabase(
                        GameData.ItemDB,
                        auraItem);
            }
        }

        // The persistent native StatusEffect uses its Effect.SpellIcon.
        // Native status-effect instances can survive save/load and may refer
        // to a separate Spell instance, so align both on application.
        internal static void EnsurePersistentBuffIcon(Stats stats, Spell spell)
        {
            if (!IsLightbringerAura(spell)) return;
            Sprite icon = _auraIcon;
            if (icon == null)
                _auraIcon = icon = EmbeddedSpriteLoader.LoadByFileName(AuraIconFileName);
            if (icon == null) return;
            spell.SpellIcon = icon;
            if (stats == null || stats.StatusEffects == null) return;
            foreach (StatusEffect effect in stats.StatusEffects)
            {
                if (effect != null && IsLightbringerAura(effect.Effect))
                    effect.Effect.SpellIcon = icon;
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

        internal static bool TryGetDefinition(
            Spell spell,
            out LightbringerAuraDefinition definition)
        {
            definition =
                null;

            if (!IsLightbringerAura(spell))
            {
                return false;
            }

            for (int i = 0;
                 i < Definitions.Count;
                 i++)
            {
                LightbringerAuraDefinition candidate =
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
            LightbringerAuraDefinition definition,
            Spell template,
            Sprite icon,
            Class lightbringer)
        {
            Spell spell =
                ScriptableObject.CreateInstance<Spell>();

            spell.name =
                "FOTF LB AURA - " +
                definition.ItemName;

            spell.Id =
                AuraSpellPrefix +
                definition.Key.ToUpperInvariant();

            spell.UsedBy =
                new List<Class>
                {
                    lightbringer
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

            spell.MR = definition.Resist;
            spell.PR = definition.Resist;
            spell.ER = definition.Resist;
            spell.VR = definition.Resist;
            SetNativeArmorBonus(spell, definition.Armor);

            spell.SpellRange =
                30f;

            spell.CanHitPlayers =
                true;

            spell.SimUsable =
                false;

            spell.NoResonate =
                true;

            spell.StatusEffectMessageOnPlayer =
                "feel the protection of dawn.";

            spell.StatusEffectMessageOnNPC =
                "shines with the protection of dawn.";

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
            LightbringerAuraDefinition definition,
            Spell auraSpell,
            Sprite icon,
            Class lightbringer)
        {
            Item item =
                ScriptableObject.CreateInstance<Item>();

            item.name =
                "FOTF LB AURA ITEM - " +
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
                    lightbringer
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

        // AC property name differs between Erenshor builds. Resolve once per
        // class aura registration; no reflection in the update loop.
        private static void SetNativeArmorBonus(Spell spell, int amount)
        {
            if (spell == null) return;
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance;
            var type = typeof(Spell);
            foreach (string name in new[] { "AC", "ArmorClass", "Armor" })
            {
                var field = type.GetField(name, flags);
                if (field != null && field.FieldType == typeof(int))
                { field.SetValue(spell, amount); return; }
                if (field != null && field.FieldType == typeof(float))
                { field.SetValue(spell, (float)amount); return; }
                var property = type.GetProperty(name, flags);
                if (property != null && property.CanWrite && property.PropertyType == typeof(int))
                { property.SetValue(spell, amount, null); return; }
                if (property != null && property.CanWrite && property.PropertyType == typeof(float))
                { property.SetValue(spell, (float)amount, null); return; }
            }
            Plugin.LogWarning("[LBAuras] Could not find native Spell AC modifier; resistance bonuses remain available.");
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
