using System;
using HarmonyLib;

namespace FateOfTheFallen
{
    /// <summary>
    /// Applies Lightbringer visuals to the player's native RaidUI card after
    /// Erenshor has completed its normal class-card initialization.
    /// </summary>
    [HarmonyPatch(
        typeof(RaidManager),
        "InitPlayerCard")]
    internal static class Patch_LightbringerPlayerClassIcon
    {
        [HarmonyPostfix]
        private static void Postfix(
            RaidManager __instance)
        {
            try
            {
                LightbringerRaidUI.ApplyPlayerCard(
                    __instance);
            }
            catch (Exception exception)
            {
                Plugin.LogError(
                    "Lightbringer: failed to apply player RaidUI class icon: " +
                    exception);
            }
        }
    }
}
