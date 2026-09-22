using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    [HarmonyPatch(
            typeof(CastSpell),
            "StartSpell",
            new Type[]
            {
                typeof(Spell),
                typeof(Stats)
            })]
    internal static class Patch_BlightcallerCast
    {
        private static bool Prefix(
            CastSpell __instance,
            Spell _spell,
            ref Stats _target,
            ref bool __result)
        {
            try
            {
                BlightcallerSpellDefinition definition;

                if (!BlightcallerCatalog.TryGetDefinition(
                        _spell,
                        out definition))
                {
                    return true;
                }

                string denial;

                if (BlightcallerRuntime.PrepareCast(
                    __instance,
                    _spell,
                    ref _target,
                    out denial))
                {
                    return true;
                }

                __result = false;

                return false;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: spell cast patch failed: " +
                    exception);

                return true;
            }
        }

        private static void Postfix(
            Spell _spell,
            bool __result)
        {
            if (!__result)
            {
                return;
            }

            try
            {
                BlightcallerSpellDefinition definition;

                if (!BlightcallerCatalog.TryGetDefinition(
                        _spell,
                        out definition))
                {
                    return;
                }

                if (definition.HiddenEffect)
                {
                    return;
                }

                if (GameData.PlayerCombat == null)
                {
                    return;
                }

                if (GameData.PlayerStats == null)
                {
                    return;
                }

                if (!BlightcallerCatalog.IsBlightcallerClass(
                        GameData.PlayerStats.CharacterClass))
                {
                    return;
                }

                GameData.PlayerCombat.ForceAttackOn();
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: post-cast handling failed: " +
                    exception);
            }
        }
    }
}
