using UnityEngine;

namespace FateOfTheFallen
{
    internal sealed class BlightcallerSpellDefinition
    {
        internal string Key;

        internal string Name;

        internal string Description;

        internal string IconResource;

        internal int Level;

        internal Spell.SpellType Type;

        internal Spell.SpellLine Line;

        internal GameData.DamageType DamageType;

        internal BlightcallerSpellKind Kind;

        internal int ManaCost;

        internal int TargetDamage;

        internal int DurationTicks;

        internal int Aggro;

        internal float CastSeconds;

        internal float CooldownSeconds;

        internal float Range = 60f;

        internal float ResistModifier = 0f;

        internal bool Lifetap = false;

        internal bool Fear = false;

        internal bool Root = false;

        internal bool SelfOnly = false;

        internal bool Pbaoe = false;

        internal bool BreakOnAnyAction = false;

        internal bool GrantInvisibility = false;

        // ============================================================
        // STATUS EFFECT STAT MODIFIERS
        // ============================================================

        // Flat resistance modifiers.
        // These are percentage-point changes to the target's
        // Magic / Elemental / Poison / Void resistance.
        internal int MR = 0;

        internal int ER = 0;

        internal int PR = 0;

        internal int VR = 0;

        // Flat modifier applied to 1d20 attack rolls.
        internal int AtkRollModifier = 0;

        internal bool HiddenEffect;

        internal string AppliedEffectKey;

        internal Color VfxColor;
    }
}