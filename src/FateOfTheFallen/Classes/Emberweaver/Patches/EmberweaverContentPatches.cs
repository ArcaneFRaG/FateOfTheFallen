using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FateOfTheFallen
{
    // ============================================================
    // DATABASE REGISTRATION
    // ============================================================

    [HarmonyPatch(
        typeof(SpellDB),
        "Start")]
    internal static class Patch_EmberweaverSpellDatabaseStart
    {
        private static void Postfix(
            SpellDB __instance)
        {
            EmberweaverModule.RegisterSpellDatabase(
                __instance);
        }
    }


    [HarmonyPatch(
        typeof(SkillDB),
        "Start")]
    internal static class Patch_EmberweaverSkillDatabaseStart
    {
        private static void Postfix(
            SkillDB __instance)
        {
            EmberweaverModule.RegisterSkillDatabase(
                __instance);
        }
    }


    [HarmonyPatch(
        typeof(SpellDB),
        "GetSpellByID")]
    internal static class Patch_EmberweaverSpellLookup
    {
        private static bool Prefix(
            string __0,
            ref Spell __result)
        {
            if (string.IsNullOrEmpty(__0) ||
                !__0.StartsWith(
                    "FOTF_EW_",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Spell spell =
                EmberweaverSpellCatalog.GetById(
                    __0);

            if (spell == null &&
                GameData.SpellDatabase != null)
            {
                EmberweaverSpellCatalog.Register(
                    GameData.SpellDatabase);

                spell =
                    EmberweaverSpellCatalog.GetById(
                        __0);
            }

            if (spell == null)
            {
                return true;
            }

            __result =
                spell;

            return false;
        }
    }


    [HarmonyPatch(
        typeof(SkillDB),
        "GetSkillByID")]
    internal static class Patch_EmberweaverSkillLookup
    {
        private static bool Prefix(
            string __0,
            ref Skill __result)
        {
            if (string.IsNullOrEmpty(__0) ||
                !__0.StartsWith(
                    "FOTF_EW_SKILL_",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Skill skill =
                EmberweaverSkillCatalog.GetById(
                    __0);

            if (skill == null &&
                GameData.SkillDatabase != null)
            {
                EmberweaverSkillCatalog.Register(
                    GameData.SkillDatabase);

                skill =
                    EmberweaverSkillCatalog.GetById(
                        __0);
            }

            if (skill == null)
            {
                return true;
            }

            __result =
                skill;

            return false;
        }
    }


    // ============================================================
    // SPELL SCROLL / SKILLBOOK LEARNING
    // ============================================================

    [HarmonyPatch(
        typeof(ItemIcon),
        "OnPointerUp")]
    internal static class Patch_EmberweaverTeachingItems
    {
        private static bool Prefix(
            ItemIcon __instance,
            PointerEventData eventData)
        {
            if (__instance == null ||
                __instance.MyItem == null)
            {
                return true;
            }

            Item item =
                __instance.MyItem;

            EmberweaverSpellDefinition spellDefinition;

            if (EmberweaverTeachingItems.TryGetSpellDefinition(
                    item,
                    out spellDefinition))
            {
                return LearnSpell(
                    __instance,
                    spellDefinition);
            }

            EmberweaverSkillDefinition skillDefinition;

            if (EmberweaverTeachingItems.TryGetSkillDefinition(
                    item,
                    out skillDefinition))
            {
                return LearnSkill(
                    __instance,
                    skillDefinition);
            }

            return true;
        }

        private static bool LearnSpell(
            ItemIcon icon,
            EmberweaverSpellDefinition definition)
        {
            if (!EmberweaverCatalog.IsCurrentPlayerEmberweaver())
            {
                SystemMessage(
                    "Only an Emberweaver can learn this spell.",
                    "yellow");

                return false;
            }

            if (GameData.PlayerStats == null ||
                GameData.PlayerStats.Level < definition.RequiredLevel)
            {
                SystemMessage(
                    "You are not experienced enough to learn this spell yet...",
                    "yellow");

                return false;
            }

            Spell spell =
                definition.RuntimeSpell;

            if (spell == null &&
                GameData.SpellDatabase != null)
            {
                EmberweaverSpellCatalog.Register(
                    GameData.SpellDatabase);

                spell =
                    definition.RuntimeSpell;
            }

            if (spell == null ||
                GameData.PlayerStats.Myself == null)
            {
                return false;
            }

            CastSpell playerSpells =
                GameData.PlayerStats.Myself.MySpells;

            if (playerSpells == null ||
                playerSpells.KnownSpells == null)
            {
                return false;
            }

            if (ContainsSpell(
                    playerSpells.KnownSpells,
                    spell.Id))
            {
                SystemMessage(
                    "You already know " +
                    definition.Name +
                    ".",
                    "yellow");

                return false;
            }

            playerSpells.KnownSpells.Add(
                spell);

            if (GameData.GM != null &&
                GameData.GM.PlayerSpells != null &&
                GameData.GM.PlayerSpells.KnownSpells != null &&
                !ContainsSpell(
                    GameData.GM.PlayerSpells.KnownSpells,
                    spell.Id))
            {
                GameData.GM.PlayerSpells.KnownSpells.Add(
                    spell);
            }

            SystemMessage(
                "Learned spell: " +
                definition.Name,
                "lightblue");

            if (GameData.PlayerInv != null)
            {
                GameData.PlayerInv.RemoveItemFromInv(
                    icon);
            }

            return false;
        }

        private static bool LearnSkill(
            ItemIcon icon,
            EmberweaverSkillDefinition definition)
        {
            if (!EmberweaverCatalog.IsCurrentPlayerEmberweaver())
            {
                SystemMessage(
                    "Only an Emberweaver can learn this skill.",
                    "yellow");

                return false;
            }

            if (GameData.PlayerStats == null ||
                GameData.PlayerStats.Level < definition.RequiredLevel)
            {
                SystemMessage(
                    "You are not experienced enough to learn this skill yet...",
                    "yellow");

                return false;
            }

            Skill skill =
                definition.RuntimeSkill;

            if (skill == null &&
                GameData.SkillDatabase != null)
            {
                EmberweaverSkillCatalog.Register(
                    GameData.SkillDatabase);

                skill =
                    definition.RuntimeSkill;
            }

            if (skill == null ||
                GameData.PlayerStats.Myself == null)
            {
                return false;
            }

            UseSkill skills =
                GameData.PlayerStats.Myself.MySkills;

            if (skills == null ||
                skills.KnownSkills == null)
            {
                return false;
            }

            if (ContainsSkill(
                    skills.KnownSkills,
                    skill.Id))
            {
                SystemMessage(
                    "You already know " +
                    definition.Name +
                    ".",
                    "yellow");

                return false;
            }

            skills.KnownSkills.Add(
                skill);

            if (GameData.GM != null &&
                GameData.GM.PlayerSkills != null &&
                GameData.GM.PlayerSkills.KnownSkills != null &&
                !ContainsSkill(
                    GameData.GM.PlayerSkills.KnownSkills,
                    skill.Id))
            {
                GameData.GM.PlayerSkills.KnownSkills.Add(
                    skill);
            }

            SystemMessage(
                "Learned skill: " +
                definition.Name,
                "lightblue");

            try
            {
                skills.LearnSkill.Play();
            }
            catch
            {
            }

            if (GameData.PlayerSkillBook != null &&
                GameData.PlayerSkillBook.Skillbook.activeSelf)
            {
                GameData.PlayerSkillBook.UpdateSkillList(
                    GameData.PlayerSkillBook.GetPage());
            }

            /*
             * Heat, Flashfire and Wildfire are passive skills and therefore
             * are deliberately not assigned to an action-bar hotkey.
             */
            if (GameData.PlayerInv != null)
            {
                GameData.PlayerInv.RemoveItemFromInv(
                    icon);
            }

            return false;
        }

        private static bool ContainsSpell(
            IList<Spell> list,
            string id)
        {
            if (list == null ||
                string.IsNullOrEmpty(id))
            {
                return false;
            }

            for (int i = 0;
                 i < list.Count;
                 i++)
            {
                Spell spell =
                    list[i];

                if (spell != null &&
                    string.Equals(
                        spell.Id,
                        id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsSkill(
            IList<Skill> list,
            string id)
        {
            if (list == null ||
                string.IsNullOrEmpty(id))
            {
                return false;
            }

            for (int i = 0;
                 i < list.Count;
                 i++)
            {
                Skill skill =
                    list[i];

                if (skill != null &&
                    string.Equals(
                        skill.Id,
                        id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SystemMessage(
            string text,
            string color)
        {
            UpdateSocialLog.LogAdd(
                new ChatLogLine(
                    text,
                    ChatLogLine.LogType.SystemMessages,
                    color));
        }
    }


    // ============================================================
    // SPELL SCROLL DROPS
    // ============================================================

    [HarmonyPatch(
        typeof(LootTable),
        "InitLootTable")]
    internal static class Patch_EmberweaverScrollDrops
    {
        private static void Postfix(
            LootTable __instance)
        {
            try
            {
                EmberweaverScrollDistribution.TryAddScrollDrop(
                    __instance);
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Emberweaver: scroll-drop injection failed",
                    exception);
            }
        }
    }


    // ============================================================
    // FLASHFIRE NATIVE HOTKEY GATE
    // ============================================================

    [HarmonyPatch]
    internal static class Patch_EmberweaverFlashfireHotkeys
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            // The exact player-hotkey execution helper has changed between
            // Erenshor builds. Patch the known UI/update entry points plus
            // any declared Hotkeys helper whose name clearly participates in
            // spell/hotkey activation. The prefix itself is a no-op unless
            // this concrete slot is a bound Pyroclasm while Flashfire is ready.
            return AccessTools
                .GetDeclaredMethods(
                    typeof(Hotkeys))
                .Where(
                    method =>
                    {
                        if (method == null ||
                            method.IsAbstract ||
                            method.IsGenericMethod ||
                            method.IsSpecialName)
                        {
                            return false;
                        }

                        string name =
                            method.Name ??
                            string.Empty;

                        string lower =
                            name.ToLowerInvariant();

                        return
                            name == "Update" ||
                            name == "OnPointerDown" ||
                            name == "OnPointerUp" ||
                            name == "OnPointerClick" ||
                            lower.Contains("cast") ||
                            lower.Contains("spell") ||
                            lower.Contains("hotkey") ||
                            lower.Contains("activate") ||
                            lower.Contains("trigger") ||
                            lower.Contains("press") ||
                            lower.StartsWith("use");
                    });
        }

        private static void Prefix(
            Hotkeys __instance,
            ref EmberweaverFlashfireCooldownRuntime.HotkeyInvocationState __state)
        {
            try
            {
                __state =
                    EmberweaverFlashfireCooldownRuntime
                        .BeginHotkeyInvocation(
                            __instance);
            }
            catch (Exception exception)
            {
                __state =
                    null;

                Plugin.LogWarning(
                    "Emberweaver: Flashfire hotkey prefix failed: " +
                    exception.Message);
            }
        }

        private static Exception Finalizer(
            Exception __exception,
            EmberweaverFlashfireCooldownRuntime.HotkeyInvocationState __state)
        {
            try
            {
                EmberweaverFlashfireCooldownRuntime
                    .EndHotkeyInvocation(
                        __state);
            }
            catch (Exception exception)
            {
                Plugin.LogWarning(
                    "Emberweaver: Flashfire hotkey cleanup failed: " +
                    exception.Message);
            }

            return __exception;
        }
    }


    // ============================================================
    // FLASHFIRE / CAST TRACKING
    // ============================================================

    [HarmonyPatch]
    internal static class Patch_EmberweaverStartSpell
    {
        private sealed class State
        {
            internal Spell Spell;
            internal Stats Target;
            internal EmberweaverSpellDefinition Definition;
            internal float OriginalCastTime;
            internal bool TemporarilyChangedSpell;
            internal bool WasFlashfire;
            internal bool ManaGuardActive;
            internal int ProtectedManaBaseline;
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools
                .GetDeclaredMethods(
                    typeof(CastSpell))
                .Where(
                    method =>
                        method.Name == "StartSpell" &&
                        method.ReturnType == typeof(bool))
                .Where(
                    method =>
                    {
                        ParameterInfo[] parameters =
                            method.GetParameters();

                        return
                            parameters.Length >= 2 &&
                            parameters[0].ParameterType == typeof(Spell) &&
                            parameters[1].ParameterType == typeof(Stats);
                    });
        }

        private static bool Prefix(
            CastSpell __instance,
            object[] __args,
            ref bool __result,
            ref State __state)
        {
            if (__args == null ||
                __args.Length < 2)
            {
                return true;
            }

            // StartSpell is shared by the player and SimPlayers. The original
            // Emberweaver patch only checked the local player's class, which
            // meant an authored Emberweaver Sim could accidentally enter the
            // player's Flashfire/mana state whenever the local player was also
            // an Emberweaver. Keep the player runtime strictly player-owned.
            PlayerControl playerControl =
                __instance != null
                    ? __instance.GetComponent<PlayerControl>()
                    : null;

            if (playerControl == null ||
                (GameData.PlayerControl != null &&
                 playerControl != GameData.PlayerControl))
            {
                return true;
            }

            Spell spell =
                __args[0] as Spell;

            Stats target =
                __args[1] as Stats;

            EmberweaverSpellDefinition definition;

            if (!EmberweaverSpellCatalog.TryGetDefinition(
                    spell,
                    out definition) ||
                definition == null ||
                !EmberweaverCatalog.IsCurrentPlayerEmberweaver())
            {
                return true;
            }

            if (definition.IsCombust)
            {
                string denial;

                if (!EmberweaverPassiveRuntime.CanCastCombust(
                        target,
                        out denial))
                {
                    if (!string.IsNullOrEmpty(denial))
                    {
                        UpdateSocialLog.LogAdd(
                            new ChatLogLine(
                                denial,
                                ChatLogLine.LogType.SystemMessages,
                                "yellow"));
                    }

                    __result =
                        false;

                    return false;
                }
            }

            __state =
                new State
                {
                    Spell = spell,
                    Target = target,
                    Definition = definition,
                    OriginalCastTime = spell.SpellChargeTime
                };

            if (!EmberweaverPassiveRuntime
                    .ShouldInstantPyroclasm(
                        spell))
            {
                return true;
            }

            __state.WasFlashfire =
                true;

            // The hotkey has already been allowed through its Flashfire
            // cooldown gate. Restore the spell's normal cooldown now so this
            // proc cast starts a fresh ordinary cooldown after it succeeds.
            EmberweaverFlashfireCooldownRuntime
                .PreparePyroclasmCast(
                    spell);

            // Hotkeys can validate and deduct mana before StartSpell is
            // entered. The hotkey patch records the true pre-interaction mana
            // value. For direct/non-hotkey casts this falls back to the mana
            // value visible here. Temporarily guarantee affordability, then
            // restore this baseline in Postfix so Flashfire is truly free.
            __state.ProtectedManaBaseline =
                EmberweaverFlashfireCooldownRuntime
                    .GetProtectedManaBaseline();

            __state.ManaGuardActive =
                true;

            EmberweaverFlashfireCooldownRuntime
                .EnsureStartSpellCanAfford(
                    definition);

            bool modifiedArgument =
                false;

            // Known StartSpell overloads carry modifiedCastTime as a float
            // after Spell/Stats. Prefer that per-cast argument over mutating
            // the shared Spell asset.
            for (int i = 2;
                 i < __args.Length;
                 i++)
            {
                if (__args[i] is float)
                {
                    __args[i] =
                        0f;

                    modifiedArgument =
                        true;

                    break;
                }
            }

            if (!modifiedArgument)
            {
                spell.SpellChargeTime =
                    0f;

                __state.TemporarilyChangedSpell =
                    true;
            }

            return true;
        }

        private static void Postfix(
            bool __result,
            State __state)
        {
            if (__state == null ||
                __state.Spell == null)
            {
                return;
            }

            if (__state.TemporarilyChangedSpell)
            {
                __state.Spell.SpellChargeTime =
                    __state.OriginalCastTime;
            }

            if (__state.ManaGuardActive &&
                __state.WasFlashfire)
            {
                EmberweaverFlashfireCooldownRuntime
                    .RestoreManaAfterFreeCast(
                        __state.ProtectedManaBaseline);
            }

            if (!__result)
            {
                if (__state.WasFlashfire)
                {
                    // The proc remains available after a failed native cast
                    // (bad target, interrupted start, etc.), so immediately
                    // return the Pyroclasm family to the Flashfire-ready state.
                    EmberweaverFlashfireCooldownRuntime
                        .ResetPyroclasmCooldowns();
                }

                return;
            }

            if (__state.Definition != null &&
                __state.Definition.IsCombust)
            {
                EmberweaverPassiveRuntime.ExecuteCombust(
                    __state.Target);

                return;
            }

            EmberweaverPassiveRuntime.TrackSuccessfulCast(
                __state.Spell,
                __state.Target);

            if (__state.WasFlashfire)
            {
                EmberweaverFlashfireCooldownRuntime
                    .ArmPostCastManaProtection(
                        __state.ProtectedManaBaseline);

                EmberweaverPassiveRuntime
                    .ConsumeFlashfireIfSuccessful(
                        __state.Spell,
                        true);
            }
        }
    }


    // ============================================================
    // DIRECT DAMAGE / HEAT / FLASHFIRE / WILDFIRE
    // ============================================================

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
    internal static class Patch_EmberweaverMagicDamage
    {
        internal sealed class State
        {
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
            EmberweaverSpellDefinition definition;

            if (!EmberweaverPassiveRuntime.TryMatchDirectDamage(
                    __instance,
                    _attacker,
                    out definition))
            {
                return;
            }

            __state =
                new State
                {
                    Definition = definition,
                    Target = __instance
                };

            EmberweaverPassiveRuntime.BeginDirectDamage(
                definition,
                __instance);

            _dmg =
                EmberweaverPassiveRuntime.ApplyDirectDamageModifiers(
                    _dmg,
                    definition);

            // MagicDamageMe uses _baseDmg for its native damage cap. Apply the
            // same class modifiers so a Heat/crit/Ascension-boosted spell is
            // not clipped back to the unmodified base ceiling.
            _baseDmg =
                EmberweaverPassiveRuntime.ApplyDirectDamageModifiers(
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
                EmberweaverPassiveRuntime.OnDirectDamageApplied(
                    __state.Target,
                    __state.Definition,
                    Mathf.Max(
                        0,
                        __result));
            }
            finally
            {
                EmberweaverPassiveRuntime.EndDirectDamage();
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
    internal static class Patch_EmberweaverReduceHpCrit
    {
        private static void Prefix(
            Stats __instance,
            ref bool _isCritical)
        {
            EmberweaverPassiveRuntime.MarkCurrentReduceHpCritical(
                __instance,
                ref _isCritical);
        }
    }
}
