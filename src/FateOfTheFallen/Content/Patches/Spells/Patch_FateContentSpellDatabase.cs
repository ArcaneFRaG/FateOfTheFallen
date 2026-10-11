using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    [HarmonyPatch(
        typeof(SpellDB),
        "Start")]
    internal static class Patch_FateContentSpellDatabaseStart
    {
        private static void Postfix(
            SpellDB __instance)
        {
            try
            {
                if (__instance == null ||
                    !FOTFExpansionGate.IsUnlocked)
                {
                    return;
                }

                FateContentModule
                    .RegisterSpellDatabaseContent(
                        __instance);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: failed during gated SpellDB initialization: " +
                    exception);
            }
        }
    }
}
