using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class BlightcallerSpellData
    {
        internal static readonly IReadOnlyList<BlightcallerSpellDefinition> All =
            new List<BlightcallerSpellDefinition>
            {
                // ============================================================
                // DIRECT DAMAGE
                // ============================================================

                // ------------------------------------------------------------
                // BLIGHTED BOLT
                // ------------------------------------------------------------

                new BlightcallerSpellDefinition
                {
                    Key = "blighted_bolt",
                    Name = "Blighted Bolt",
                    Description =
                        "Hurls a bolt of corrupting energy at the target, dealing void damage.",
                    Level = 1,
                    Type = Spell.SpellType.Damage,
                    Line = Spell.SpellLine.Direct_Damage,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 20,
                    TargetDamage = 25,
                    Aggro = 25,
                    CastSeconds = 2.0f,
                    CooldownSeconds = 5f,
                    Range = 60f,
                    IconResource = "FateOfTheFallen.Assets.Blighted_Bolt.png",
                    VfxColor = new Color(0.38f, 0.01f, 0.98f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "putrid_bolt",
                    Name = "Putrid Bolt",
                    Description =
                        "Hurls a heavily corrupted bolt at the target, dealing increased void damage.",
                    Level = 12,
                    Type = Spell.SpellType.Damage,
                    Line = Spell.SpellLine.Direct_Damage,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 180,
                    TargetDamage = 455,
                    Aggro = 200,
                    CastSeconds = 2.0f,
                    CooldownSeconds = 6f,
                    Range = 60f,
                    IconResource = "FateOfTheFallen.Assets.Putrid_Bolt.png",
                    VfxColor = new Color(0.38f, 0.01f, 0.98f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "festering_bolt",
                    Name = "Festering Bolt",
                    Description =
                        "Launches a festering bolt of void energy that tears through the target with immense force.",
                    Level = 24,
                    Type = Spell.SpellType.Damage,
                    Line = Spell.SpellLine.Direct_Damage,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 300,
                    TargetDamage = 1550,
                    Aggro = 200,
                    CastSeconds = 2.0f,
                    CooldownSeconds = 6f,
                    Range = 60f,
                    IconResource = "FateOfTheFallen.Assets.Festering_Bolt.png",
                    VfxColor = new Color(0.45f, 0.01f, 1.00f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "apocalyptic_bolt",
                    Name = "Apocalyptic Bolt",
                    Description =
                        "Unleashes a devastating bolt of corrupting void energy upon the target.",
                    Level = 35,
                    Type = Spell.SpellType.Damage,
                    Line = Spell.SpellLine.Direct_Damage,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 1355,
                    TargetDamage = 7500,
                    Aggro = 200,
                    CastSeconds = 2.0f,
                    CooldownSeconds = 6f,
                    Range = 60f,
                    IconResource = "FateOfTheFallen.Assets.Apocalyptic_Bolt.png",
                    VfxColor = new Color(0.55f, 0.02f, 1.00f)
                },

                // ------------------------------------------------------------
                // SHADOW REND
                // ------------------------------------------------------------

                new BlightcallerSpellDefinition
                {
                    Key = "shadow_rend",
                    Name = "Shadow Rend",
                    Description =
                        "Tears into the target with concentrated shadow energy, dealing void damage.",
                    Level = 2,
                    Type = Spell.SpellType.Damage,
                    Line = Spell.SpellLine.Direct_Damage,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 25,
                    TargetDamage = 45,
                    Aggro = 25,
                    CastSeconds = 1f,
                    CooldownSeconds = 8f,
                    Range = 60f,
                    IconResource = "FateOfTheFallen.Assets.Shadow_Rend.png",
                    VfxColor = new Color(0.20f, 0.04f, 0.30f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "shadow_rupture",
                    Name = "Shadow Rupture",
                    Description =
                        "Rips open the target's defenses with concentrated shadow energy, dealing heavy void damage.",
                    Level = 14,
                    Type = Spell.SpellType.Damage,
                    Line = Spell.SpellLine.Direct_Damage,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 200,
                    TargetDamage = 900,
                    Aggro = 200,
                    CastSeconds = 2f,
                    CooldownSeconds = 8f,
                    Range = 60f,
                    IconResource = "FateOfTheFallen.Assets.Shadow_Rupture.png",
                    VfxColor = new Color(0.23f, 0.04f, 0.38f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "shadow_devastation",
                    Name = "Shadow Devastation",
                    Description =
                        "Devastates the target with a violent eruption of concentrated shadow energy.",
                    Level = 26,
                    Type = Spell.SpellType.Damage,
                    Line = Spell.SpellLine.Direct_Damage,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 150,
                    TargetDamage = 700,
                    Aggro = 200,
                    CastSeconds = 1f,
                    CooldownSeconds = 8f,
                    Range = 60f,
                    IconResource = "FateOfTheFallen.Assets.Shadow_Devastation.png",
                    VfxColor = new Color(0.28f, 0.03f, 0.45f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "void_rend",
                    Name = "Void Rend",
                    Description =
                        "Tears a catastrophic wound through the target with pure destructive void energy.",
                    Level = 35,
                    Type = Spell.SpellType.Damage,
                    Line = Spell.SpellLine.Direct_Damage,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 1700,
                    TargetDamage = 5500,
                    Aggro = 200,
                    CastSeconds = 1.0f,
                    CooldownSeconds = 8f,
                    Range = 60f,
                    IconResource = "FateOfTheFallen.Assets.Void_Rend.png",
                    VfxColor = new Color(0.35f, 0.02f, 0.55f)
                },

                // ------------------------------------------------------------
                // SOUL REND
                // ------------------------------------------------------------

                new BlightcallerSpellDefinition
                {
                    Key = "soul_rend",
                    Name = "Soul Rend",
                    Description =
                        "Tears violently into the target's soul with concentrated void energy, inflicting devastating direct damage.",
                    Level = 30,
                    Type = Spell.SpellType.Damage,
                    Line = Spell.SpellLine.Direct_Damage,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 1000,
                    TargetDamage = 4500,
                    Aggro = 125,
                    CastSeconds = 2.3f,
                    CooldownSeconds = 8f,
                    Range = 60f,
                    IconResource = "FateOfTheFallen.Assets.Soul_Rend.png",
                    VfxColor = new Color(0.50f, 0.01f, 0.75f)
                },

                // ============================================================
                // DAMAGE OVER TIME
                // ============================================================

                // ------------------------------------------------------------
                // WITHERING TOUCH
                // ------------------------------------------------------------

                new BlightcallerSpellDefinition
                {
                    Key = "withering_touch",
                    Name = "Withering Touch",
                    Description =
                        "Channels corrupting energy into the target, causing it to wither over time.",
                    Level = 1,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Void_DOT,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 25,
                    TargetDamage = 12,
                    DurationTicks = 8,
                    Aggro = 20,
                    CastSeconds = 1.7f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    AppliedEffectKey = "withering_touch_dot",
                    IconResource = "FateOfTheFallen.Assets.Withering_Touch.png",
                    VfxColor = new Color(0.42f, 0.08f, 0.55f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "greater_withering_touch",
                    Name = "Greater Withering Touch",
                    Description =
                        "Channels a deeper corruption into the target, causing it to wither for increased damage over time.",
                    Level = 12,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Void_DOT,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 300,
                    TargetDamage = 450,
                    DurationTicks = 8,
                    Aggro = 300,
                    CastSeconds = 1.7f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    AppliedEffectKey = "greater_withering_touch_dot",
                    IconResource = "FateOfTheFallen.Assets.Greater_Withering_Touch.png",
                    VfxColor = new Color(0.46f, 0.08f, 0.62f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "potent_withering_touch",
                    Name = "Potent Withering Touch",
                    Description =
                        "Channels potent corrupting energy into the target, causing severe withering over time.",
                    Level = 24,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Void_DOT,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 750,
                    TargetDamage = 600,
                    DurationTicks = 8,
                    Aggro = 600,
                    CastSeconds = 1.7f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    AppliedEffectKey = "potent_withering_touch_dot",
                    IconResource = "FateOfTheFallen.Assets.Potent_Withering_Touch.png",
                    VfxColor = new Color(0.50f, 0.06f, 0.70f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "eternal_withering",
                    Name = "Eternal Withering",
                    Description =
                        "Inflicts an enduring corruption that relentlessly withers the target.",
                    Level = 35,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Void_DOT,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 1400,
                    TargetDamage = 1400,
                    DurationTicks = 8,
                    Aggro = 600,
                    CastSeconds = 1.70f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    AppliedEffectKey = "eternal_withering_dot",
                    IconResource = "FateOfTheFallen.Assets.Eternal_Withering_Touch.png",
                    VfxColor = new Color(0.58f, 0.05f, 0.82f)
                },

                // ------------------------------------------------------------
                // WITHERING TOUCH - HIDDEN EFFECTS
                // ------------------------------------------------------------

                new BlightcallerSpellDefinition
                {
                    Key = "withering_touch_dot",
                    Name = "Withering Touch: Affliction",
                    Description =
                        "Internal persistent effect for Withering Touch.",
                    Level = 1,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Void_DOT,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 0,
                    TargetDamage = 12,
                    DurationTicks = 8,
                    Aggro = 20,
                    CastSeconds = 0f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    HiddenEffect = true,
                    IconResource = "FateOfTheFallen.Assets.Withering_Touch.png",
                    VfxColor = new Color(0.42f, 0.08f, 0.55f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "greater_withering_touch_dot",
                    Name = "Greater Withering Touch: Affliction",
                    Description =
                        "Internal persistent effect for Greater Withering Touch.",
                    Level = 12,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Void_DOT,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 0,
                    TargetDamage = 450,
                    DurationTicks = 8,
                    Aggro = 300,
                    CastSeconds = 0f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    HiddenEffect = true,
                    IconResource = "FateOfTheFallen.Assets.Greater_Withering_Touch.png",
                    VfxColor = new Color(0.46f, 0.08f, 0.62f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "potent_withering_touch_dot",
                    Name = "Potent Withering Touch: Affliction",
                    Description =
                        "Internal persistent effect for Potent Withering Touch.",
                    Level = 24,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Void_DOT,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 0,
                    TargetDamage = 600,
                    DurationTicks = 8,
                    Aggro = 600,
                    CastSeconds = 0f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    HiddenEffect = true,
                    IconResource = "FateOfTheFallen.Assets.Potent_Withering_Touch.png",
                    VfxColor = new Color(0.50f, 0.06f, 0.70f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "eternal_withering_dot",
                    Name = "Eternal Withering: Affliction",
                    Description =
                        "Internal persistent effect for Eternal Withering.",
                    Level = 35,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Void_DOT,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 0,
                    TargetDamage = 1400,
                    DurationTicks = 8,
                    Aggro = 600,
                    CastSeconds = 0f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    HiddenEffect = true,
                    IconResource = "FateOfTheFallen.Assets.Eternal_Withering_Touch.png",
                    VfxColor = new Color(0.58f, 0.05f, 0.82f)
                },

                // ------------------------------------------------------------
                // BLIGHT
                // ------------------------------------------------------------

                new BlightcallerSpellDefinition
                {
                    Key = "blight",
                    Name = "Blight",
                    Description =
                        "Afflicts the target with a lingering disease that deals damage over time.",
                    Level = 4,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Poison_DOT,
                    DamageType = GameData.DamageType.Poison,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 155,
                    TargetDamage = 50,
                    DurationTicks = 8,
                    Aggro = 200,
                    CastSeconds = 1.70f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    AppliedEffectKey = "blight_dot",
                    IconResource = "FateOfTheFallen.Assets.Blight.png",
                    VfxColor = new Color(0.25f, 0.65f, 0.08f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "greater_blight",
                    Name = "Greater Blight",
                    Description =
                        "Afflicts the target with a stronger disease that deals increased damage over time.",
                    Level = 15,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Poison_DOT,
                    DamageType = GameData.DamageType.Poison,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 300,
                    TargetDamage = 450,
                    DurationTicks = 8,
                    Aggro = 300,
                    CastSeconds = 1.70f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    AppliedEffectKey = "greater_blight_dot",
                    IconResource = "FateOfTheFallen.Assets.Greater_Blight.png",
                    VfxColor = new Color(0.30f, 0.72f, 0.06f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "virulent_blight",
                    Name = "Virulent Blight",
                    Description =
                        "Infects the target with a virulent corruption that causes severe poison damage over time.",
                    Level = 26,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Poison_DOT,
                    DamageType = GameData.DamageType.Poison,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 750,
                    TargetDamage = 600,
                    DurationTicks = 8,
                    Aggro = 600,
                    CastSeconds = 1.70f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    AppliedEffectKey = "virulent_blight_dot",
                    IconResource = "FateOfTheFallen.Assets.Virulent_Blight.png",
                    VfxColor = new Color(0.36f, 0.82f, 0.04f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "cataclysmic_blight",
                    Name = "Cataclysmic Blight",
                    Description =
                        "Unleashes a catastrophic plague upon the target, inflicting devastating poison damage over time.",
                    Level = 35,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Poison_DOT,
                    DamageType = GameData.DamageType.Poison,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 1400,
                    TargetDamage = 1400,
                    DurationTicks = 8,
                    Aggro = 600,
                    CastSeconds = 1.70f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    AppliedEffectKey = "cataclysmic_blight_dot",
                    IconResource = "FateOfTheFallen.Assets.Cataclysmic_Blight.png",
                    VfxColor = new Color(0.42f, 0.90f, 0.02f)
                },

                // ------------------------------------------------------------
                // BLIGHT - HIDDEN EFFECTS
                // ------------------------------------------------------------

                new BlightcallerSpellDefinition
                {
                    Key = "blight_dot",
                    Name = "Blight: Affliction",
                    Description =
                        "Internal persistent effect for Blight.",
                    Level = 4,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Poison_DOT,
                    DamageType = GameData.DamageType.Poison,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 0,
                    TargetDamage = 50,
                    DurationTicks = 8,
                    Aggro = 200,
                    CastSeconds = 0f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    HiddenEffect = true,
                    IconResource = "FateOfTheFallen.Assets.Blight.png",
                    VfxColor = new Color(0.25f, 0.65f, 0.08f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "greater_blight_dot",
                    Name = "Greater Blight: Affliction",
                    Description =
                        "Internal persistent effect for Greater Blight.",
                    Level = 15,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Poison_DOT,
                    DamageType = GameData.DamageType.Poison,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 0,
                    TargetDamage = 450,
                    DurationTicks = 8,
                    Aggro = 300,
                    CastSeconds = 0f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    HiddenEffect = true,
                    IconResource = "FateOfTheFallen.Assets.Greater_Blight.png",
                    VfxColor = new Color(0.30f, 0.72f, 0.06f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "virulent_blight_dot",
                    Name = "Virulent Blight: Affliction",
                    Description =
                        "Internal persistent effect for Virulent Blight.",
                    Level = 26,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Poison_DOT,
                    DamageType = GameData.DamageType.Poison,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 0,
                    TargetDamage = 600,
                    DurationTicks = 10,
                    Aggro = 600,
                    CastSeconds = 0f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    HiddenEffect = true,
                    IconResource = "FateOfTheFallen.Assets.Virulent_Blight.png",
                    VfxColor = new Color(0.36f, 0.82f, 0.04f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "cataclysmic_blight_dot",
                    Name = "Cataclysmic Blight: Affliction",
                    Description =
                        "Internal persistent effect for Cataclysmic Blight.",
                    Level = 35,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Poison_DOT,
                    DamageType = GameData.DamageType.Poison,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 0,
                    TargetDamage = 1400,
                    DurationTicks = 8,
                    Aggro = 600,
                    CastSeconds = 0f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    HiddenEffect = true,
                    IconResource = "FateOfTheFallen.Assets.Cataclysmic_Blight.png",
                    VfxColor = new Color(0.42f, 0.90f, 0.02f)
                },

                // ------------------------------------------------------------
                // AGONY
                // ------------------------------------------------------------

                new BlightcallerSpellDefinition
                {
                    Key = "agony",
                    Name = "Agony",
                    Description =
                        "Inflicts the target with supernatural agony, causing repeated damage over time.",
                    Level = 8,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Magic_DOT,
                    DamageType = GameData.DamageType.Magic,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 120,
                    TargetDamage = 100,
                    DurationTicks = 8,
                    Aggro = 200,
                    CastSeconds = 1.70f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    AppliedEffectKey = "agony_dot",
                    IconResource = "FateOfTheFallen.Assets.Agony.png",
                    VfxColor = new Color(0.55f, 0.05f, 0.25f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "greater_agony",
                    Name = "Greater Agony",
                    Description =
                        "Inflicts the target with intensified supernatural agony, causing increased damage over time.",
                    Level = 16,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Magic_DOT,
                    DamageType = GameData.DamageType.Magic,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 300,
                    TargetDamage = 450,
                    DurationTicks = 8,
                    Aggro = 300,
                    CastSeconds = 1.7f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    AppliedEffectKey = "greater_agony_dot",
                    IconResource = "FateOfTheFallen.Assets.Greater_Agony.png",
                    VfxColor = new Color(0.62f, 0.05f, 0.30f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "lingering_agony",
                    Name = "Lingering Agony",
                    Description =
                        "Inflicts a deep and lingering supernatural agony that continues to damage the target.",
                    Level = 28,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Magic_DOT,
                    DamageType = GameData.DamageType.Magic,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 750,
                    TargetDamage = 600,
                    DurationTicks = 8,
                    Aggro = 600,
                    CastSeconds = 1.70f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    AppliedEffectKey = "lingering_agony_dot",
                    IconResource = "FateOfTheFallen.Assets.Lingering_Agony.png",
                    VfxColor = new Color(0.70f, 0.04f, 0.34f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "endless_agony",
                    Name = "Endless Agony",
                    Description =
                        "Condemns the target to an endless supernatural agony that inflicts devastating damage over time.",
                    Level = 35,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Magic_DOT,
                    DamageType = GameData.DamageType.Magic,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 1400,
                    TargetDamage = 1400,
                    DurationTicks = 8,
                    Aggro = 600,
                    CastSeconds = 1.70f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    AppliedEffectKey = "endless_agony_dot",
                    IconResource = "FateOfTheFallen.Assets.Endless_Agony.png",
                    VfxColor = new Color(0.80f, 0.03f, 0.40f)
                },

                // ------------------------------------------------------------
                // AGONY - HIDDEN EFFECTS
                // ------------------------------------------------------------

                new BlightcallerSpellDefinition
                {
                    Key = "agony_dot",
                    Name = "Agony: Affliction",
                    Description =
                        "Internal persistent effect for Agony.",
                    Level = 8,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Magic_DOT,
                    DamageType = GameData.DamageType.Magic,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 0,
                    TargetDamage = 100,
                    DurationTicks = 8,
                    Aggro = 200,
                    CastSeconds = 0f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    HiddenEffect = true,
                    IconResource = "FateOfTheFallen.Assets.Agony.png",
                    VfxColor = new Color(0.55f, 0.05f, 0.25f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "greater_agony_dot",
                    Name = "Greater Agony: Affliction",
                    Description =
                        "Internal persistent effect for Greater Agony.",
                    Level = 16,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Magic_DOT,
                    DamageType = GameData.DamageType.Magic,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 300,
                    TargetDamage = 450,
                    DurationTicks = 8,
                    Aggro = 300,
                    CastSeconds = 0f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    HiddenEffect = true,
                    IconResource = "FateOfTheFallen.Assets.Greater_Agony.png",
                    VfxColor = new Color(0.62f, 0.05f, 0.30f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "lingering_agony_dot",
                    Name = "Lingering Agony: Affliction",
                    Description =
                        "Internal persistent effect for Lingering Agony.",
                    Level = 28,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Magic_DOT,
                    DamageType = GameData.DamageType.Magic,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 750,
                    TargetDamage = 600,
                    DurationTicks = 8,
                    Aggro = 600,
                    CastSeconds = 0f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    HiddenEffect = true,
                    IconResource = "FateOfTheFallen.Assets.Lingering_Agony.png",
                    VfxColor = new Color(0.70f, 0.04f, 0.34f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "endless_agony_dot",
                    Name = "Endless Agony: Affliction",
                    Description =
                        "Internal persistent effect for Endless Agony.",
                    Level = 35,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Magic_DOT,
                    DamageType = GameData.DamageType.Magic,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 0,
                    TargetDamage = 1400,
                    DurationTicks = 8,
                    Aggro = 600,
                    CastSeconds = 0f,
                    CooldownSeconds = 12f,
                    Range = 60f,
                    HiddenEffect = true,
                    IconResource = "FateOfTheFallen.Assets.Endless_Agony.png",
                    VfxColor = new Color(0.80f, 0.03f, 0.40f)
                },

                // ============================================================
                // DEBUFFS
                // ============================================================

                // ------------------------------------------------------------
                // FESTERING WEAKNESS
                // Reduces Magic, Elemental, Poison and Void resistance.
                // ------------------------------------------------------------

                new BlightcallerSpellDefinition
                {
                    Key = "festering_weakness",
                    Name = "Festering Weakness",
                    Description =
                        "Festering corruption weakens the target's defenses, reducing its Magic, Elemental, Poison, and Void resistances.",
                    Level = 6,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Resists,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 200,
                    TargetDamage = 0,
                    DurationTicks = 4,
                    Aggro = 200,
                    CastSeconds = 2.0f,
                    CooldownSeconds = 10f,
                    Range = 60f,

                    MR = -10,
                    ER = -10,
                    PR = -10,
                    VR = -10,

                    IconResource = "FateOfTheFallen.Assets.Festering_Weakness.png",
                    VfxColor = new Color(0.34f, 0.05f, 0.48f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "greater_festering_weakness",
                    Name = "Greater Festering Weakness",
                    Description =
                        "Deepens the corruption within the target, substantially reducing its Magic, Elemental, Poison, and Void resistances.",
                    Level = 20,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Resists,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 450,
                    TargetDamage = 0,
                    DurationTicks = 8,
                    Aggro = 300,
                    CastSeconds = 2.0f,
                    CooldownSeconds = 12f,
                    Range = 60f,

                    MR = -15,
                    ER = -15,
                    PR = -15,
                    VR = -15,

                    IconResource = "FateOfTheFallen.Assets.Greater_Festering_Weakness.png",
                    VfxColor = new Color(0.40f, 0.04f, 0.56f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "devastating_festering_weakness",
                    Name = "Devastating Festering Weakness",
                    Description =
                        "Overwhelms the target with devastating corruption, severely reducing its Magic, Elemental, Poison, and Void resistances.",
                    Level = 32,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Resists,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 800,
                    TargetDamage = 0,
                    DurationTicks = 10,
                    Aggro = 600,
                    CastSeconds = 2.0f,
                    CooldownSeconds = 15f,
                    Range = 60f,

                    MR = -20,
                    ER = -20,
                    PR = -20,
                    VR = -20,

                    IconResource = "FateOfTheFallen.Assets.Devastating_Festering_Weakness.png",
                    VfxColor = new Color(0.48f, 0.03f, 0.68f)
                },

                // ------------------------------------------------------------
                // WITHERING CURSE
                // Reduces the target's flat 1d20 attack-roll modifier.
                // ------------------------------------------------------------

                new BlightcallerSpellDefinition
                {
                    Key = "withering_curse",
                    Name = "Withering Curse",
                    Description =
                        "Curses the target with withering corruption, reducing its Attack Bonus and weakening its ability to land accurate and powerful attacks.",
                    Level = 8,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Other_Debuff,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 275,
                    TargetDamage = 0,
                    DurationTicks = 4,
                    Aggro = 200,
                    CastSeconds = 2.0f,
                    CooldownSeconds = 10f,
                    Range = 60f,

                    AtkRollModifier = -2,

                    IconResource = "FateOfTheFallen.Assets.Withering_Curse.png",
                    VfxColor = new Color(0.38f, 0.08f, 0.50f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "greater_withering_curse",
                    Name = "Greater Withering Curse",
                    Description =
                        "Deepens the withering curse, substantially reducing the target's Attack Bonus and weakening its attacks.",
                    Level = 21,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Other_Debuff,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 500,
                    TargetDamage = 0,
                    DurationTicks = 8,
                    Aggro = 300,
                    CastSeconds = 2.0f,
                    CooldownSeconds = 12f,
                    Range = 60f,

                    AtkRollModifier = -3,

                    IconResource = "FateOfTheFallen.Assets.Greater_Withering_Curse.png",
                    VfxColor = new Color(0.44f, 0.06f, 0.60f)
                },

                new BlightcallerSpellDefinition
                {
                    Key = "eternal_withering_curse",
                    Name = "Eternal Withering Curse",
                    Description =
                        "Places an enduring curse upon the target, heavily reducing its Attack Bonus and weakening its ability to strike with force and accuracy.",
                    Level = 34,
                    Type = Spell.SpellType.StatusEffect,
                    Line = Spell.SpellLine.Global_Other_Debuff,
                    DamageType = GameData.DamageType.Void,
                    Kind = BlightcallerSpellKind.Native,
                    ManaCost = 800,
                    TargetDamage = 0,
                    DurationTicks = 10,
                    Aggro = 600,
                    CastSeconds = 2.0f,
                    CooldownSeconds = 15f,
                    Range = 60f,

                    AtkRollModifier = -4,

                    IconResource = "FateOfTheFallen.Assets.Eternal_Withering_Curse.png",
                    VfxColor = new Color(0.52f, 0.04f, 0.72f)
                }
            };
    }
}