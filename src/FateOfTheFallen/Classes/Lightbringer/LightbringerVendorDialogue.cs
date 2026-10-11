using HarmonyLib;

namespace FateOfTheFallen
{
    [HarmonyPatch(typeof(NPCDialogManager), "GenericHail")]
    internal static class Patch_LightbringerVendorDialogue
    {
        [HarmonyPostfix]
        private static void Postfix(NPCDialogManager __instance)
        {
            LightbringerVendor.HandleGenericHail(__instance);
        }
    }
}
