using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    /// <summary>
    /// Blightcaller-only ItemDatabase initialization.
    /// Shared item registration lives under Core; this patch only
    /// applies class-specific compatibility/content.
    /// </summary>
    [HarmonyPatch(
        typeof(ItemDatabase),
        "Start")]
    internal static class Patch_BlightcallerItemDatabaseStart
    {
        private static void Postfix()
        {
            try
            {
                BlightcallerEquipment
                    .RegisterArcanistEquipment();

                if (GameData.SpellDatabase != null)
                {
                    BlightcallerAuras.Register(
                        GameData.SpellDatabase);
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: failed during ItemDatabase initialization: " +
                    exception);
            }
        }
    }
}
