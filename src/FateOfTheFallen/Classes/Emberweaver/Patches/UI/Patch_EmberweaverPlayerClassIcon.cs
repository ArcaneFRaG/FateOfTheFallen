using System;
using HarmonyLib;

namespace FateOfTheFallen
{
    /// <summary>
    /// Applies Emberweaver visuals to the player's native RaidUI card after
    /// Erenshor has completed its normal class-card initialization.
    /// </summary>
    [HarmonyPatch(
        typeof(RaidManager),
        "InitPlayerCard")]
    internal static class Patch_EmberweaverPlayerClassIcon
    {
        [HarmonyPostfix]
        private static void Postfix(
            RaidManager __instance)
        {
            try
            {
                EmberweaverRaidUI.ApplyPlayerCard(
                    __instance);
            }
            catch (Exception exception)
            {
                Plugin.LogError(
                    "Emberweaver: failed to apply player RaidUI class icon: " +
                    exception);
            }
        }
    }
}
