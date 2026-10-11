using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    [HarmonyPatch(typeof(SkillDB), "Start")]
    internal static class Patch_LightbringerAscensionDatabase
    {
        private static void Postfix() { LightbringerAscensions.Register(); }
    }

    [HarmonyPatch(typeof(AAScreen), "LoadAllAAButtonData")]
    internal static class Patch_LightbringerAscensionClassTab
    {
        private static int _snapshotLogs;
        private static void Postfix(AAScreen __instance, bool ___classSpecific)
        {
            if (!___classSpecific || __instance == null || __instance.AllAASlots == null ||
                !LightbringerAscensions.IsLightbringerPlayer()) return;
            LightbringerAscensions.Register();
            UseSkill skills = GameData.PlayerControl != null && GameData.PlayerControl.Myself != null
                ? GameData.PlayerControl.Myself.MySkills : null;
            int count = Mathf.Min(__instance.AllAASlots.Count, LightbringerAscensions.All.Count);
            for (int i = 0; i < count; i++)
            {
                AAButton button = __instance.AllAASlots[i];
                Ascension ascension = LightbringerAscensions.All[i];
                if (button == null || ascension == null) continue;
                button.Name.text = ascension.SkillName;
                button.SkillDescription = ascension.SkillDesc;
                button.id = ascension.Id;
                button.MaxLevel.text = " / " + ascension.MaxRank;
                button.Level.text = LightbringerAscensions.GetRank(skills, ascension.Id).ToString();
            }
            if (_snapshotLogs++ < 8)
            {
                Plugin.LogInfo("[LBAscensionAudit] Native class tab loaded. " +
                    string.Join(", ", LightbringerAscensions.All.Select(a =>
                        a.SkillName + "=" + LightbringerAscensions.GetRank(skills, a.Id)).ToArray()));
            }
        }
    }

    [HarmonyPatch(typeof(AAScreen), "CountSpent")]
    internal static class Patch_LightbringerAscensionSpent
    {
        private static void Postfix(AAScreen __instance, ref int ___spentPoints)
        {
            if (!LightbringerAscensions.IsLightbringerPlayer()) return;
            UseSkill skills = GameData.PlayerControl != null && GameData.PlayerControl.Myself != null
                ? GameData.PlayerControl.Myself.MySkills : null;
            ___spentPoints += LightbringerAscensions.All.Sum(a =>
                LightbringerAscensions.GetRank(skills, a.Id));
            if (__instance != null && __instance.spent != null)
                __instance.spent.text = "Points spent: " + ___spentPoints;
        }
    }

    /// <summary>Native Druid load can filter out custom Ascensions. Restore only
    /// the Lightbringer IDs, preserving native/other records and existing points.</summary>
    [HarmonyPatch(typeof(CharSelectManager), "LoadSpellsAndSkills")]
    internal static class Patch_LightbringerAscensionLoad
    {
        private static void Postfix()
        {
            if (!LightbringerCharacterState.IsLightbringer(GameData.CurrentCharacterSlot) ||
                GameData.GM == null || GameData.GM.PlayerSkills == null ||
                GameData.GM.PlayerSkills.MyAscensions == null ||
                GameData.CurrentCharacterSlot.Ascensions == null) return;
            LightbringerAscensions.Register();
            Dictionary<string, int> best = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (AscensionSkillEntry saved in GameData.CurrentCharacterSlot.Ascensions)
                if (saved != null && LightbringerAscensions.IsCustomId(saved.id))
                {
                    int previous;
                    best.TryGetValue(saved.id, out previous);
                    best[saved.id] = Math.Max(previous, Mathf.Clamp(saved.level, 0, 3));
                }
            var active = GameData.GM.PlayerSkills.MyAscensions;
            // Remove duplicate custom ranks, but never touch other class/native entries.
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i] != null && LightbringerAscensions.IsCustomId(active[i].id))
                    active.RemoveAt(i);
            foreach (KeyValuePair<string, int> pair in best)
                if (pair.Value > 0) active.Add(new AscensionSkillEntry(pair.Key, pair.Value));
            Plugin.LogInfo("[LBAscensionAudit] Restored " + best.Count +
                " saved Lightbringer Ascension IDs from character slot.");
        }
    }

    [HarmonyPatch(typeof(Stats), "HealMe", new Type[]
    {
        typeof(Spell), typeof(int), typeof(bool), typeof(bool), typeof(Character)
    })]
    internal static class Patch_LightbringerRestorativeDawn
    {
        private static int _healingLogs;
        private static void Prefix(Spell __0, ref int __1, Character __4)
        {
            int baseHealing = __1;
            __1 = LightbringerAscensionRuntime.AdjustHealing(__0, __1, __4);
            if (__1 > baseHealing && _healingLogs++ < 8)
                Plugin.LogInfo("[LBHealing] Direct heal scaling " +
                    (__0 != null ? __0.SpellName : "unknown") +
                    " base=" + baseHealing + " scaled=" + __1);
        }
    }

    /// <summary>Use Erenshor's normal mana payment and spell eligibility,
    /// with a temporary, per-call cost adjustment. Never alter saved cost.</summary>
    [HarmonyPatch(typeof(CastSpell), "StartSpell", new Type[] { typeof(Spell), typeof(Stats) })]
    internal static class Patch_LightbringerSanctumDiscipline
    {
        internal sealed class State
        {
            internal Spell Spell;
            internal int Original;
            internal Character Caster;
            internal int ManaBefore;
        }
        private static int _manaLogs;
        private static void Prefix(CastSpell __instance, Spell _spell, ref State __state)
        {
            Character caster = __instance != null ? __instance.MyChar : null;
            __state = LightbringerAscensionRuntime.DiscountManaCost(_spell, caster);
            if (__state != null)
            {
                __state.Caster = caster;
                __state.ManaBefore = caster != null && caster.MyStats != null
                    ? caster.MyStats.CurrentMana : 0;
            }
        }
        private static void Postfix(State __state)
        {
            if (__state != null && __state.Caster != null && __state.Caster.MyStats != null &&
                _manaLogs++ < 8)
                Plugin.LogInfo("[LBAscensionAudit] Sanctum Discipline " +
                    (__state.Spell != null ? __state.Spell.SpellName : "unknown") +
                    " configuredCost=" + (__state.Spell != null ? __state.Spell.ManaCost : 0) +
                    " originalCost=" + __state.Original +
                    " manaDeltaAtStartSpell=" +
                    (__state.ManaBefore - __state.Caster.MyStats.CurrentMana));
            LightbringerAscensionRuntime.RestoreManaCost(__state);
        }
        private static Exception Finalizer(Exception __exception, State __state)
        {
            LightbringerAscensionRuntime.RestoreManaCost(__state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Character), "MagicDamageMe", new Type[]
    {
        typeof(int), typeof(bool), typeof(GameData.DamageType),
        typeof(Character), typeof(float), typeof(int)
    })]
    internal static class Patch_LightbringerJudgementsMercy
    {
        private static void Prefix(Character __instance, out int __state)
        {
            __state = __instance != null && __instance.MyStats != null
                ? __instance.MyStats.CurrentHP : 0;
        }

        private static void Postfix(Character __instance, Character __3, int __state)
        {
            // Confirm only HP actually removed, never damage absorbed by a
            // native ward or an ambiguous native return code.
            if (__instance == null || __instance.MyStats == null) return;
            int confirmed = Math.Max(0, __state - __instance.MyStats.CurrentHP);
            if (confirmed > 0)
                LightbringerAscensionRuntime.OnMagicDamage(__3, __instance, confirmed);
        }
    }
}
