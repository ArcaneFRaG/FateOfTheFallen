using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    [HarmonyPatch(typeof(NPCDialogManager), "GenericHail")]
        internal static class Patch_BlightcallerVendorDialogue
        {
            [HarmonyPostfix]
            private static void Postfix(
                NPCDialogManager __instance)
            {
                BlightcallerVendor.HandleGenericHail(__instance);
            }
        }
}
