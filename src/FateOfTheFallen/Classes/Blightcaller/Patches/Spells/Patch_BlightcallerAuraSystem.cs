using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    [HarmonyPatch(
            typeof(Stats),
            "CheckForHigherLevelSE",
            new Type[]
            {
                typeof(Spell)
            })]
    internal static class Patch_BlightcallerAuraSpellLine
        {
            private const string BlightcallerAuraPrefix =
                "ARCBLC_AURA_";
    
            private static bool Prefix(
                Stats __instance,
                Spell spell,
                ref bool __result)
            {
                if (__instance == null)
                {
                    return true;
                }
    
                if (!IsBlightcallerAura(
                        spell))
                {
                    return true;
                }
    
                if (__instance.StatusEffects == null)
                {
                    __result = false;
                    return false;
                }
    
                try
                {
                    foreach (
                        StatusEffect statusEffect
                        in __instance.StatusEffects)
                    {
                        if (statusEffect == null)
                        {
                            continue;
                        }
    
                        Spell existingSpell =
                            statusEffect.Effect;
    
                        if (existingSpell == null)
                        {
                            continue;
                        }
    
                        if (!IsBlightcallerAura(
                                existingSpell))
                        {
                            continue;
                        }
    
                        if (existingSpell.RequiredLevel >
                            spell.RequiredLevel)
                        {
                            __result = true;
                            return false;
                        }
                    }
    
                    __result = false;
                    return false;
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "Blightcaller: aura hierarchy check failed: " +
                        exception);
    
                    return true;
                }
            }
    
            private static bool IsBlightcallerAura(
                Spell spell)
            {
                if (spell == null ||
                    string.IsNullOrEmpty(
                        spell.Id))
                {
                    return false;
                }
    
                return spell.Id.StartsWith(
                    BlightcallerAuraPrefix,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
}
