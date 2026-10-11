using HarmonyLib;

namespace FateOfTheFallen
{
    /// <summary>
    /// Keeps vendor dialogue isolated from the existing Blightcaller vendor
    /// patch. Harmony can safely run both postfixes; each handler ignores NPCs
    /// it does not own.
    /// </summary>
    [HarmonyPatch(typeof(NPCDialogManager), "GenericHail")]
    internal static class Patch_EmberweaverVendorDialogue
    {
        [HarmonyPostfix]
        private static void Postfix(
            NPCDialogManager __instance)
        {
            EmberweaverVendor.HandleGenericHail(
                __instance);
        }
    }
}
