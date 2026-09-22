using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FateOfTheFallen
{
    [HarmonyPatch(
            typeof(CharSelectManager),
            "SaveChar")]
    internal static class Patch_BlightcallerStartingItems
        {
            private static void Prefix(
                CharSelectManager __instance)
            {
                try
                {
                    BlightcallerCharacterCreation
                        .GrantStartingItemsIfValid(
                            __instance);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "Blightcaller: failed to grant starting items: " +
                        exception);
                }
            }
        }
}
