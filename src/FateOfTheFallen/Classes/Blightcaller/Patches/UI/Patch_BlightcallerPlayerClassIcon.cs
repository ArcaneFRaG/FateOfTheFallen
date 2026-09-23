using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    // ============================================================
    // BLIGHTCALLER PLAYER RAID CARD
    // ============================================================
    //
    // Mirrors the Necromancer implementation:
    //
    // RaidManager.InitPlayerCard()
    //          ↓
    // native player card setup completes
    //          ↓
    // Postfix
    //          ↓
    // apply Blightcaller icon + class colour
    // ============================================================

    [HarmonyPatch(
        typeof(RaidManager),
        "InitPlayerCard")]
    internal static class Patch_BlightcallerPlayerClassIcon
    {
        [HarmonyPostfix]
        private static void Postfix(
            RaidManager __instance)
        {
            try
            {
                BlightcallerRaidUI.ApplyPlayerCard(
                    __instance);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: failed to apply player RaidUI class icon: " +
                    exception);
            }
        }
    }
}