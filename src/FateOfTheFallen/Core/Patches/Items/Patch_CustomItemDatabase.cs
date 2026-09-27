using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    // ============================================================
    // SHARED CUSTOM ITEM LOOKUP
    // ============================================================
    //
    // Every Fate of the Fallen item is resolved through the shared
    // registry. Content modules can register lazy resolvers for items
    // that must exist during native inventory restoration.
    // ============================================================

    [HarmonyPatch(
        typeof(ItemDatabase),
        "GetItemByID")]
    internal static class Patch_FateItemDatabaseLookup
    {
        private static bool Prefix(
            ItemDatabase __instance,
            string id,
            ref Item __result)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return true;
                }

                Item item =
                    CustomItemRegistry.Resolve(
                        id,
                        __instance);

                if (item == null)
                {
                    return true;
                }

                __result =
                    item;

                return false;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: custom ItemDatabase lookup failed for ID [" +
                    id +
                    "]: " +
                    exception);

                return true;
            }
        }
    }
}
