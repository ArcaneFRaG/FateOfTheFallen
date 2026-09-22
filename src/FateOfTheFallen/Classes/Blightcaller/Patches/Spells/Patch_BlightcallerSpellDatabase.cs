using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    [HarmonyPatch(
            typeof(SpellDB),
            "Start")]
    internal static class Patch_BlightcallerSpellDatabase
        {
            private static void Postfix(
                SpellDB __instance)
            {
                if (__instance == null)
                {
                    Plugin.NativeLog.LogError(
                        "Blightcaller: SpellDB instance was null.");
    
                    return;
                }
    
                if (__instance.SpellDatabase == null)
                {
                    Plugin.NativeLog.LogError(
                        "Blightcaller: native SpellDatabase was null.");
    
                    return;
                }
    
                try
                {
                    BlightcallerCatalog.EnsureClass();
    
                    BlightcallerCatalog.RegisterSpells(
                        __instance);
    
                    BlightcallerScrolls.Register();
    
                    BlightcallerAuras.Register(
                        __instance);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "Blightcaller: failed during SpellDB initialization: " +
                        exception);
                }
            }
        }
}
