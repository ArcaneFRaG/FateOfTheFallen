using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    [HarmonyPatch(
        typeof(ItemDatabase),
        "Start")]
    internal static class Patch_FateContentItemDatabaseStart
    {
        private static void Postfix(
            ItemDatabase __instance)
        {
            try
            {
                if (__instance == null ||
                    !FOTFExpansionGate.IsUnlocked)
                {
                    return;
                }

                FateContentModule
                    .RegisterItemDatabaseContent(
                        __instance);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: failed during gated ItemDatabase initialization: " +
                    exception);
            }
        }
    }
}
