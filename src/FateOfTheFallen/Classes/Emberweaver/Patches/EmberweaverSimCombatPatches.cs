using HarmonyLib;
using System;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Sim-only bridge between native spell resolution and the per-Sim
    /// Emberweaver mechanic runtime. The local player's Emberweaver runtime is
    /// deliberately separate.
    /// </summary>
    [HarmonyPatch(
        typeof(CastSpell),
        "StartSpell",
        new Type[]
        {
            typeof(Spell),
            typeof(Stats)
        })]
    internal static class Patch_EmberweaverSimStartSpell
    {
        private sealed class State
        {
            internal SimPlayer Sim;
            internal Spell Spell;
            internal float OriginalCastTime;
            internal bool ChangedCastTime;
            internal bool Flashfire;
            internal int ManaBaseline;
        }

        private static bool Prefix(
            CastSpell __instance,
            Spell _spell,
            Stats _target,
            ref bool __result,
            ref State __state)
        {
            SimPlayer sim = ResolveSim(__instance);

            if (sim == null ||
                !EmberweaverSimCombatRuntime.IsAuthoredEmberweaver(sim))
            {
                return true;
            }

            EmberweaverSpellDefinition definition;

            if (!EmberweaverSpellCatalog.TryGetDefinition(
                    _spell,
                    out definition) ||
                definition == null)
            {
                return true;
            }

            if (definition.IsCombust &&
                !EmberweaverSimCombatRuntime.CanCombust(
                    sim,
                    _target))
            {
                __result = false;
                return false;
            }

            __state =
                new State
                {
                    Sim = sim,
                    Spell = _spell,
                    OriginalCastTime = _spell.SpellChargeTime
                };

            if (!EmberweaverSimCombatRuntime.ShouldInstantPyroclasm(
                    sim,
                    _spell))
            {
                return true;
            }

            __state.Flashfire = true;
            __state.ManaBaseline =
                sim.MyStats != null
                    ? sim.MyStats.CurrentMana
                    : 0;

            // Sim casts enter the simple StartSpell overload directly. Make
            // this one invocation instant, then restore the shared Spell asset
            // immediately in Postfix.
            _spell.SpellChargeTime = 0f;
            __state.ChangedCastTime = true;

            if (sim.MyStats != null &&
                sim.MyStats.CurrentMana < definition.ManaCost)
            {
                sim.MyStats.CurrentMana = definition.ManaCost;
            }

            return true;
        }

        private static void Postfix(
            Stats _target,
            bool __result,
            State __state)
        {
            if (__state == null ||
                __state.Sim == null ||
                __state.Spell == null)
            {
                return;
            }

            if (__state.ChangedCastTime)
            {
                __state.Spell.SpellChargeTime =
                    __state.OriginalCastTime;
            }

            if (__state.Flashfire &&
                __state.Sim.MyStats != null)
            {
                // Flashfire is free. Restore exactly the Sim's pre-cast mana
                // after native StartSpell has performed its affordability and
                // deduction work.
                __state.Sim.MyStats.CurrentMana =
                    __state.ManaBaseline;
            }

            if (!__result)
            {
                return;
            }

            EmberweaverSimCombatRuntime.SetSpellCooldown(
                __state.Sim,
                __state.Spell);

            EmberweaverSimCombatRuntime.TrackSuccessfulCast(
                __state.Sim,
                __state.Spell,
                _target);

            if (__state.Flashfire)
            {
                EmberweaverSimCombatRuntime.ArmPostCastManaProtection(
                    __state.Sim,
                    __state.ManaBaseline);

                EmberweaverSimCombatRuntime.ConsumeFlashfire(
                    __state.Sim,
                    __state.Spell,
                    true);
            }
        }

        private static SimPlayer ResolveSim(
            CastSpell caster)
        {
            if (caster == null)
            {
                return null;
            }

            SimPlayer sim =
                caster.GetComponent<SimPlayer>();

            if (sim == null)
            {
                sim = caster.GetComponentInParent<SimPlayer>();
            }

            return sim;
        }
    }


    [HarmonyPatch(
        typeof(Character),
        "MagicDamageMe",
        new Type[]
        {
            typeof(int),
            typeof(bool),
            typeof(GameData.DamageType),
            typeof(Character),
            typeof(float),
            typeof(int)
        })]
    internal static class Patch_EmberweaverSimMagicDamage
    {
        internal sealed class State
        {
            internal SimPlayer Sim;
            internal EmberweaverSpellDefinition Definition;
            internal Character Target;
        }

        private static void Prefix(
            Character __instance,
            ref int _dmg,
            Character _attacker,
            ref int _baseDmg,
            ref State __state)
        {
            SimPlayer sim;
            EmberweaverSpellDefinition definition;

            if (!EmberweaverSimCombatRuntime.TryMatchDirectDamage(
                    __instance,
                    _attacker,
                    out sim,
                    out definition))
            {
                return;
            }

            __state =
                new State
                {
                    Sim = sim,
                    Definition = definition,
                    Target = __instance
                };

            EmberweaverSimCombatRuntime.BeginDirectDamage(
                sim,
                definition,
                __instance);

            _dmg =
                EmberweaverSimCombatRuntime.ApplyDirectDamageModifiers(
                    _dmg,
                    definition);

            _baseDmg =
                EmberweaverSimCombatRuntime.ApplyDirectDamageModifiers(
                    _baseDmg,
                    definition);
        }

        private static void Postfix(
            int __result,
            State __state)
        {
            if (__state == null)
            {
                return;
            }

            try
            {
                EmberweaverSimCombatRuntime.OnDirectDamageApplied(
                    __state.Sim,
                    __state.Target,
                    __state.Definition,
                    Mathf.Max(0, __result));
            }
            finally
            {
                EmberweaverSimCombatRuntime.EndDirectDamage();
            }
        }
    }


    [HarmonyPatch(
        typeof(Stats),
        "ReduceHP",
        new Type[]
        {
            typeof(int),
            typeof(GameData.DamageType),
            typeof(bool),
            typeof(bool)
        })]
    internal static class Patch_EmberweaverSimReduceHpCrit
    {
        private static void Prefix(
            Stats __instance,
            ref bool _isCritical)
        {
            EmberweaverSimCombatRuntime.MarkCurrentReduceHpCritical(
                __instance,
                ref _isCritical);
        }
    }
}
