using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FateOfTheFallen
{
    [HarmonyPatch(
            typeof(CharSelectManager),
            "Start")]
    internal static class Patch_BlightcallerCharacterCreationStart
        {
            private static void Postfix(
                CharSelectManager __instance)
            {
                try
                {
                    FOTFCharacterCreationPager
                        .Install(__instance);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "FOTF: failed to install custom character creation buttons: " +
                        exception);
                }
            }
        }
}
