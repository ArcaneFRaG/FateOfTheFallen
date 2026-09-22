using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    [HarmonyPatch(
            typeof(ClassDB),
            "Awake")]
    internal static class Patch_BlightcallerClassDatabase
        {
            private static void Postfix()
            {
                BlightcallerCatalog.EnsureClass();
            }
        }
}
