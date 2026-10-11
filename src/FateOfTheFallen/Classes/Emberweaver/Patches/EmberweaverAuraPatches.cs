using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Applies the non-native half of Emberweaver auras: spell cast speed.
    /// Native Haste is already handled by Stats.CountStatusEffects().
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_EmberweaverAuraCastSpeed
    {
        private sealed class State
        {
            internal bool GuardEntered;
            internal Spell Spell;
            internal bool ChangedSharedSpellTime;
            internal float OriginalSpellTime;
        }

        [ThreadStatic]
        private static int _activeDepth;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo[] methods =
                typeof(CastSpell).GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            for (int i = 0;
                 i < methods.Length;
                 i++)
            {
                MethodInfo method =
                    methods[i];

                if (method == null ||
                    method.Name != "StartSpell")
                {
                    continue;
                }

                ParameterInfo[] parameters =
                    method.GetParameters();

                if (parameters == null ||
                    parameters.Length < 2 ||
                    parameters[0].ParameterType != typeof(Spell) ||
                    parameters[1].ParameterType != typeof(Stats))
                {
                    continue;
                }

                yield return method;
            }
        }

        private static void Prefix(
            CastSpell __instance,
            object[] __args,
            ref State __state)
        {
            __state =
                null;

            if (__instance == null ||
                __args == null ||
                __args.Length < 2 ||
                _activeDepth > 0)
            {
                return;
            }

            Spell spell =
                __args[0] as Spell;

            if (spell == null)
            {
                return;
            }

            Stats casterStats =
                __instance.GetComponent<Stats>();

            if (casterStats == null)
            {
                Character character =
                    __instance.GetComponent<Character>();

                if (character != null)
                {
                    casterStats =
                        character.MyStats;
                }
            }

            int castSpeedPercent =
                EmberweaverAuras.GetActiveCastSpeedPercent(
                    casterStats);

            if (castSpeedPercent <= 0)
            {
                return;
            }

            float multiplier =
                1f -
                (castSpeedPercent / 100f);

            State state =
                new State
                {
                    GuardEntered = true,
                    Spell = spell
                };

            _activeDepth++;

            // Every StartSpell overload after Spell/Stats that carries a float
            // uses its first float as modifiedCastTime. Changing the argument
            // is per-cast and avoids touching the shared Spell ScriptableObject.
            if (__args.Length >= 3 &&
                __args[2] is float)
            {
                float modifiedCastTime =
                    (float)__args[2];

                // Zero is intentionally left alone so Flashfire instant casts
                // remain instant regardless of Harmony patch ordering.
                if (modifiedCastTime > 0.001f)
                {
                    __args[2] =
                        modifiedCastTime *
                        multiplier;
                }

                __state =
                    state;

                return;
            }

            // Two-argument StartSpell has no per-call cast-time argument.
            // Temporarily scale the Spell asset for only the native call chain;
            // the thread-local guard stops nested overloads from scaling twice.
            if (spell.SpellChargeTime > 0.001f)
            {
                state.OriginalSpellTime =
                    spell.SpellChargeTime;

                spell.SpellChargeTime =
                    spell.SpellChargeTime *
                    multiplier;

                state.ChangedSharedSpellTime =
                    true;
            }

            __state =
                state;
        }

        private static Exception Finalizer(
            Exception __exception,
            State __state)
        {
            if (__state != null)
            {
                if (__state.ChangedSharedSpellTime &&
                    __state.Spell != null)
                {
                    __state.Spell.SpellChargeTime =
                        __state.OriginalSpellTime;
                }

                if (__state.GuardEntered &&
                    _activeDepth > 0)
                {
                    _activeDepth--;
                }
            }

            return __exception;
        }
    }

    /// <summary>
    /// Blightcaller's raid aura uses a 25% boss-drop roll. Emberweaver mirrors
    /// that acquisition rule, but only for Warden/Warder of Flame in Brax's
    /// Plane of Elements.
    /// </summary>
    [HarmonyPatch(typeof(LootTable), "InitLootTable")]
    internal static class Patch_EmberweaverEternalFlameDrop
    {
        private const float AuraDropChance =
            0.25f;

        private const string RequiredScene =
            "PlaneOfBrax";

        private static readonly string[] BossNames =
        {
            "Warden of Flame",
            "Warder of Flame"
        };

        [HarmonyPostfix]
        private static void Postfix(
            LootTable __instance)
        {
            if (__instance == null ||
                __instance.hostChar == null)
            {
                return;
            }

            try
            {
                Character host =
                    __instance.hostChar;

                if (!host.isNPC ||
                    host.isVendor)
                {
                    return;
                }

                string sceneName =
                    host.gameObject != null &&
                    host.gameObject.scene.IsValid()
                        ? host.gameObject.scene.name
                        : SceneManager.GetActiveScene().name;

                if (!string.Equals(
                        sceneName,
                        RequiredScene,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (!IsTargetBoss(host))
                {
                    return;
                }

                if (UnityEngine.Random.value >=
                    AuraDropChance)
                {
                    return;
                }

                Item aura =
                    EmberweaverAuras.GetItem(
                        EmberweaverAuras.EternalFlameKey);

                if (aura == null)
                {
                    EmberweaverModule.EnsureRuntimeContentReady(
                        "Warden of Flame aura drop");

                    aura =
                        EmberweaverAuras.GetItem(
                            EmberweaverAuras.EternalFlameKey);
                }

                if (aura == null)
                {
                    Plugin.ModLog.Error(
                        "Emberweaver: Aura of Eternal Flame was unavailable for Warden of Flame loot.");

                    return;
                }

                if (__instance.ActualDrops == null)
                {
                    __instance.ActualDrops =
                        new List<Item>();
                }

                if (__instance.ActualDrops.Contains(aura))
                {
                    return;
                }

                __instance.ActualDrops.Add(
                    aura);

                if (__instance.ActualDropsQual == null)
                {
                    __instance.ActualDropsQual =
                        new List<int>();
                }

                __instance.ActualDropsQual.Add(1);
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Emberweaver: Warden of Flame aura loot modification failed.",
                    exception);
            }
        }

        private static bool IsTargetBoss(
            Character host)
        {
            if (host == null)
            {
                return false;
            }

            if (MatchesBossName(
                    host.gameObject != null
                        ? host.gameObject.name
                        : null))
            {
                return true;
            }

            if (host.MyNPC != null &&
                MatchesBossName(
                    host.MyNPC.NPCName))
            {
                return true;
            }

            return false;
        }

        private static bool MatchesBossName(
            string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string normalized =
                value.Replace(
                    "(Clone)",
                    string.Empty)
                     .Trim();

            for (int i = 0;
                 i < BossNames.Length;
                 i++)
            {
                if (string.Equals(
                        normalized,
                        BossNames[i],
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
