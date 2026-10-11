using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    [HarmonyPatch(typeof(ClassDB), "Awake")]
    internal static class Patch_LightbringerClassDB
    {
        private static void Postfix()
        {
            LightbringerCatalog.TryEnsureClass();
            LightbringerClassIcon.Initialize();
        }
    }

    [HarmonyPatch(typeof(SpellDB), "Start")]
    internal static class Patch_LightbringerSpellDatabase
    {
        private static void Postfix(SpellDB __instance)
        {
            if (LightbringerClassIcon.Sprite == null) LightbringerClassIcon.Initialize();
            LightbringerNativeHealing.RegisterSpells(__instance);
            LightbringerResolvedHealWard.Reset();
            LightbringerSpellCatalog.Register(__instance);
            LightbringerAuras.Register(__instance);
            LightbringerAuraPersistence.TryRestoreFromSave("Lightbringer SpellDB.Start");
        }
    }

    [HarmonyPatch(typeof(ItemDatabase), "Start")]
    internal static class Patch_LightbringerItemDatabase
    {
        private static void Postfix(ItemDatabase __instance)
        {
            LightbringerNativeHealing.RegisterItems(__instance);
            LightbringerTeachingItems.Register(__instance);
            LightbringerSkillbooks.Register(__instance);
            if (GameData.SpellDatabase != null)
            {
                LightbringerAuras.Register(GameData.SpellDatabase);
                LightbringerAuraPersistence.TryRestoreFromSave("Lightbringer ItemDatabase.Start");
            }
        }
    }

    [HarmonyPatch(typeof(CharSelectManager), "SelectSlot")]
    internal static class Patch_LightbringerSelectSlot
    {
        private static void Prefix(int _index)
        {
            if (GameData.SaveSlots == null || _index < 0 || _index >= GameData.SaveSlots.Count ||
                !LightbringerCharacterState.IsLightbringer(GameData.SaveSlots[_index]))
            {
                return;
            }

            Class lb = LightbringerCatalog.TryEnsureClass();
            if (lb != null && GameData.PlayerStats != null)
                GameData.PlayerStats.CharacterClass = lb;
        }

        private static void Postfix(CharSelectManager __instance)
        {
            LightbringerCharacterState.Restore(__instance);
        }
    }

    [HarmonyPatch(typeof(CharSelectManager), "Play")]
    internal static class Patch_LightbringerPlay
    {
        private static void Prefix(CharSelectManager __instance)
        {
            LightbringerCharacterState.Restore(__instance);
        }
    }

    [HarmonyPatch(typeof(CharSelectManager), "PlayAlternate")]
    internal static class Patch_LightbringerPlayAlternate
    {
        private static void Prefix(CharSelectManager __instance)
        {
            LightbringerCharacterState.Restore(__instance);
        }
    }

    [HarmonyPatch(typeof(CharSelectManager), "SaveChar")]
    internal static class Patch_LightbringerSaveChar
    {
        private static void Prefix(CharSelectManager __instance)
        {
            // IMPORTANT: never infer a new character from PlayerStats here.
            // PlayerStats may still belong to the previously selected slot,
            // e.g. Emberweaver after switching from Lightbringer.
            if (__instance == null || __instance.selClass == null ||
                !LightbringerCatalog.IsLightbringerClass(__instance.selClass))
                return;
            if (GameData.CurrentCharacterSlot != null)
                GameData.CurrentCharacterSlot.CharClass = LightbringerConstants.DisplayName;
            LightbringerStartingLoadout.QueueForNewCharacter(__instance);
        }

        private static void Postfix(CharSelectManager __instance)
        {
            // Restrict post-save identity mutation to a newly selected
            // Lightbringer; never write Blightcaller/Emberweaver saves.
            if (__instance != null && __instance.selClass != null &&
                LightbringerCatalog.IsLightbringerClass(__instance.selClass) &&
                GameData.CurrentCharacterSlot != null)
                GameData.CurrentCharacterSlot.CharClass = LightbringerConstants.DisplayName;
            // Native SaveChar may construct/replace the slot after its Prefix.
            // Enqueue again here if the Prefix ran before the slot existed.
            LightbringerStartingLoadout.QueueForNewCharacter(__instance);
        }
    }

    [HarmonyPatch(typeof(CharSelectManager), "LoadSpellsAndSkills")]
    internal static class Patch_LightbringerRestoreSharedNativeHeals
    {
        private static void Postfix()
        {
            if (!LightbringerCharacterState.IsLightbringer(GameData.CurrentCharacterSlot) ||
                GameData.CurrentCharacterSlot.CharacterSpells == null ||
                GameData.GM == null || GameData.GM.PlayerSpells == null ||
                GameData.GM.PlayerSpells.KnownSpells == null || GameData.SpellDatabase == null)
            {
                return;
            }

            foreach (string id in GameData.CurrentCharacterSlot.CharacterSpells)
            {
                if (string.IsNullOrEmpty(id))
                    continue;

                Spell spell = GameData.SpellDatabase.GetSpellByID(id);
                if (spell == null)
                    continue;

                if (!LightbringerNativeHealing.IsApproved(spell) && !LightbringerSpellCatalog.IsLightbringerCustomSpell(spell))
                    continue;

                bool known = false;
                foreach (Spell existing in GameData.GM.PlayerSpells.KnownSpells)
                {
                    if (existing != null && existing.Id == spell.Id)
                    {
                        known = true;
                        break;
                    }
                }

                if (!known)
                    GameData.GM.PlayerSpells.KnownSpells.Add(spell);
            }
        }
    }

    [HarmonyPatch(typeof(LootTable), "InitLootTable")]
    internal static class Patch_LightbringerScrollDrops
    {
        private static void Postfix(LootTable __instance)
        {
            try
            {
                LightbringerScrollDistribution.TryAddScrollDrop(__instance);
                LightbringerSkillbooks.TryAddDrop(__instance);
            }
            catch (Exception exception)
            {
                Plugin.LogWarning("[Lightbringer] Scroll-drop injection failed: " + exception.Message);
            }
        }
    }

    [HarmonyPatch(
        typeof(CastSpell),
        "StartSpell",
        new Type[]
        {
            typeof(Spell),
            typeof(Stats)
        })]
    internal static class Patch_LightbringerCastPost
    {
        private static void Prefix(CastSpell __instance, Spell _spell, Stats _target)
        {
            LightbringerAbsorbApi.EndNativeWardCast();
            if (__instance == null || __instance.MyChar == null) return;
            Character caster = __instance.MyChar;
            LightbringerNativeShield.PrepareForCaster(_spell, caster.MyStats);
            LightbringerSpellDefinition definition;
            if (_spell != null && LightbringerSpellCatalog.TryGetDefinition(_spell, out definition) &&
                definition.Kind == LightbringerSpellKind.Shield)
                LightbringerAbsorbApi.BeginNativeWardCast(
                    _spell.SelfOnly ? caster.MyStats : (_target ?? caster.MyStats),
                    caster, _spell,
                    LightbringerNativeShield.GetConfiguredCapacity(_spell, definition.ShieldAmount),
                    definition.BarrierDuration, definition.CastTime);
        }

        private static void Postfix(CastSpell __instance, Spell _spell, Stats _target, bool __result)
        {
            LightbringerAbsorbApi.EndNativeWardCast();
            try
            {
                LightbringerSpellDefinition definition;
                if (!LightbringerSpellCatalog.TryGetDefinition(_spell, out definition))
                    return;

                if (!__result)
                {
                    Plugin.LogInfo("[LBShield] StartSpell rejected [" + definition.Name + "]; no shield applied.");
                    return;
                }

                Character caster = __instance != null ? __instance.MyChar : null;
                if (caster == null || caster.MyStats == null || !LightbringerCatalog.IsLightbringerClass(caster.MyStats.CharacterClass))
                {
                    Plugin.LogWarning("[LBShield] Accepted [" + definition.Name + "] but caster is null/non-Lightbringer.");
                    return;
                }

                // The raid-tier mixed heal/shields are resolved at Stats.HealMe,
                // not merely StartSpell acceptance. See Patch_LightbringerResolvedHealWard.
                // This prevents shield effects on interrupted casts and targets
                // the exact recipients that actually receive the native heal.
                if (LightbringerResolvedHealWard.Handles(definition))
                    Plugin.LogInfo("[LBHealWard] Accepted " + definition.Name +
                        "; waiting for native HealMe recipient resolution.");
                else
                    LightbringerRuntime.OnSuccessfulCast(definition, caster, _target, _spell);
                LightbringerAscensionRuntime.OnDamageCastAccepted(caster, _target, _spell, definition);
                if (definition.Kind == LightbringerSpellKind.Shield && _spell != null)
                {
                    // This is diagnostic only. The engine remains the source of
                    // truth for the actual Shield status effect. Do not create
                    // a custom shield on the assumption that the cast landed.
                    Stats recipient = _spell.SelfOnly ? caster.MyStats : (_target ?? caster.MyStats);
                    Plugin.LogInfo("[LBAllyCast] Accepted [" + definition.Name + "] " +
                        "caster=" + caster.MyStats.MyName +
                        " StartSpellTarget=" + (_target != null ? _target.MyName : "null") +
                        " configuredSelfOnly=" + _spell.SelfOnly +
                        " expectedRecipient=" + (recipient != null ? recipient.MyName : "null"));
                    if (recipient != null)
                    {
                        // This is ONLY a UI estimate of the native shield;
                        // all gameplay absorption is still performed by Erenshor.
                        float acceptedAt = UnityEngine.Time.time;
                        LightbringerAbsorbApi.TrackNativeWard(recipient, caster, _spell,
                            LightbringerNativeShield.GetConfiguredCapacity(_spell, definition.ShieldAmount), definition.CastTime,
                            definition.BarrierDuration);
                        LightbringerAbsorbApi.QueueAcceptedNativeRefresh(recipient,
                            __instance, acceptedAt, definition.CastTime);
                    }
                }
                if (definition.ShieldAmount > 0 && !LightbringerResolvedHealWard.Handles(definition))
                {
                    Stats recipient = _target ?? caster.MyStats;
                    Plugin.LogInfo("[LBShield] Cast accepted [" + definition.Name + "] " +
                        "target=" + (recipient != null ? recipient.MyName : "null") +
                        " mode=" + (definition.Kind == LightbringerSpellKind.Shield ? "NATIVE" : "LEGACY_HYBRID_GROUP") +
                        " uiReported=" + LightbringerAbsorbApi.GetRemainingAbsorb(recipient));
                }
            }
            catch (Exception exception)
            {
                Plugin.LogWarning("[Lightbringer] Post-cast runtime failed: " + exception.Message);
            }
        }
    }

    // The native heal's actual recipient is the only reliable place to attach
    // a mixed heal + ward. Group heals invoke HealMe on each real recipient,
    // so this also avoids guessing membership from a StartSpell target.
    [HarmonyPatch(typeof(Stats), "HealMe", new Type[]
    {
        typeof(Spell), typeof(int), typeof(bool), typeof(bool), typeof(Character)
    })]
    internal static class Patch_LightbringerResolvedHealWard
    {
        private static void Postfix(Stats __instance, Spell __0, Character __4)
        {
            try { LightbringerResolvedHealWard.OnNativeHeal(__instance, __0, __4); }
            catch (Exception ex)
            {
                Plugin.LogWarning("[LBHealWard] Native heal resolution hook failed: " + ex.Message);
            }
        }
    }

    // Refresh at the actual status-application point, not on StartSpell acceptance.
    // Prevents an interrupted cast from stripping an existing shield.
    [HarmonyPatch(typeof(Stats), "AddStatusEffect", new Type[] { typeof(Spell), typeof(bool), typeof(int) })]
    internal static class Patch_LightbringerWardEffect3
    {
        private static void Prefix(Stats __instance, Spell __0) { LightbringerAbsorbApi.BeforeNativeWardApplied(__instance, __0); }
        private static void Postfix(Stats __instance, Spell __0, int __result) { LightbringerAbsorbApi.AfterNativeWardApplied(__instance, __0, __result); LightbringerAuras.EnsurePersistentBuffIcon(__instance, __0); }
    }

    [HarmonyPatch(typeof(Stats), "AddStatusEffect", new Type[] { typeof(Spell), typeof(bool), typeof(int), typeof(Character) })]
    internal static class Patch_LightbringerWardEffect4
    {
        private static void Prefix(Stats __instance, Spell __0) { LightbringerAbsorbApi.BeforeNativeWardApplied(__instance, __0); }
        private static void Postfix(Stats __instance, Spell __0, int __result) { LightbringerAbsorbApi.AfterNativeWardApplied(__instance, __0, __result); LightbringerAuras.EnsurePersistentBuffIcon(__instance, __0); }
    }

    [HarmonyPatch(typeof(Stats), "AddStatusEffect", new Type[] { typeof(Spell), typeof(bool), typeof(int), typeof(Character), typeof(float) })]
    internal static class Patch_LightbringerWardEffect5
    {
        private static void Prefix(Stats __instance, Spell __0) { LightbringerAbsorbApi.BeforeNativeWardApplied(__instance, __0); }
        private static void Postfix(Stats __instance, Spell __0, int __result) { LightbringerAbsorbApi.AfterNativeWardApplied(__instance, __0, __result); LightbringerAuras.EnsurePersistentBuffIcon(__instance, __0); }
    }

    // Housekeeping for depletion paths that do not hit the patched native
    // DamageMe overload (no accounting events are synthesized here).
    [HarmonyPatch(typeof(Stats), "TickEffects")]
    internal static class Patch_LightbringerWardExpiry
    {
        private static void Postfix(Stats __instance)
        {
            LightbringerAbsorbApi.SweepDepletedNativeWard(__instance);
            LightbringerAbsorbApi.FinishAcceptedNativeRefresh(__instance);
        }
    }

    // No HealMe suppression for pure wards: they now retain Erenshor's native
    // Beneficial/Shield status effect instead of a healing template.
    // DamageMe below remains ONLY for legacy hybrid/group shield components,
    // which are not yet proven compatible with native ally/group targeting.

    [HarmonyPatch(
        typeof(Character),
        "DamageMe",
        new Type[]
        {
            typeof(int),
            typeof(bool),
            typeof(GameData.DamageType),
            typeof(Character),
            typeof(bool),
            typeof(bool),
            typeof(int)
        })]
    internal static class Patch_LightbringerShieldIntercept
    {
        // Harmony positional argument binding deliberately avoids depending
        // on native DamageMe argument names. __0 is the first argument
        // (incoming damage) on this exact 7-parameter overload.
        private static void Prefix(Character __instance, ref int __0, out int __state)
        {
            // This patch is on DamageMe's exact seven-argument overload;
            // __0 binds by position, avoiding the old _dam Harmony failure.
            __state = __instance != null && __instance.MyStats != null
                ? LightbringerAbsorbApi.GetNativeBefore(__instance.MyStats) : 0;
            if (__instance != null && __0 > 0)
                __0 = LightbringerRuntime.ConsumeShield(__instance, __0);
        }

        private static void Postfix(Character __instance, int __0, int __result, int __state)
        {
            if (__instance != null && __instance.MyStats != null)
                LightbringerAbsorbApi.AfterNativeDamage(__instance.MyStats, __0,
                    __result, __state);
        }
    }

    // Erenshor has distinct magic and bleed damage entrypoints, which can
    // consume the native SpellShield pool without passing through the patched
    // 7-argument physical DamageMe overload. Capture exact before/after
    // changes with the same deduplicated publisher, not a second shield pool.
    [HarmonyPatch(typeof(Character), "MagicDamageMe", new Type[]
    {
        typeof(int), typeof(bool), typeof(GameData.DamageType),
        typeof(Character), typeof(float), typeof(int)
    })]
    internal static class Patch_LightbringerMagicShieldMeter
    {
        private static void Prefix(Character __instance, out int __state)
        {
            __state = __instance != null && __instance.MyStats != null
                ? LightbringerAbsorbApi.GetNativeBefore(__instance.MyStats) : 0;
        }

        private static void Postfix(Character __instance, int __0,
            int __result, int __state)
        {
            if (__instance != null && __instance.MyStats != null)
                LightbringerAbsorbApi.AfterNativeDamage(__instance.MyStats,
                    __0, __result, __state);
        }
    }

    [HarmonyPatch(typeof(Character), "BleedDamageMe", new Type[]
    {
        typeof(int), typeof(bool), typeof(Character)
    })]
    internal static class Patch_LightbringerBleedShieldMeter
    {
        private static void Prefix(Character __instance, out int __state)
        {
            __state = __instance != null && __instance.MyStats != null
                ? LightbringerAbsorbApi.GetNativeBefore(__instance.MyStats) : 0;
        }

        // This method's return value is not part of the ABI contract needed
        // here. Only the native pool's measured before/after change is used.
        private static void Postfix(Character __instance, int __0, int __state)
        {
            if (__instance != null && __instance.MyStats != null)
                LightbringerAbsorbApi.AfterNativeDamage(__instance.MyStats,
                    __0, 0, __state);
        }
    }

    // Only a freshly created Lightbringer is eligible for the delayed starter
    // grant. Existing saved characters never receive a login-time refill.
    [HarmonyPatch(typeof(Stats), "Update")]
    internal static class Patch_LightbringerFinishStartingInventory
    {
        private static void Postfix(Stats __instance)
        {
            LightbringerAbsorbApi.FinishAcceptedNativeRefresh(__instance);
            if (__instance == GameData.PlayerStats)
            {
                LightbringerStartingLoadout.TryCompletePendingGrant();
                LightbringerNativeShield.RefreshPlayerBonuses();
            }
        }
    }
}
