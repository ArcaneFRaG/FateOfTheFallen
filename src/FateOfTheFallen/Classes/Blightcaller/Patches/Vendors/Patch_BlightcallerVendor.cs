using HarmonyLib;
using System;
using System.Collections.Generic;

namespace FateOfTheFallen
{
    [HarmonyPatch(
            typeof(VendorInventory),
            "Start")]
    internal static class Patch_BlightcallerAmandaRostleyVendor
        {
            private const string VendorName =
                "Amanda Rostley";
    
            private const string FirstAuraKey =
                "withering";
    
            private static void Postfix(
                VendorInventory __instance)
            {
                if (__instance == null)
                {
                    return;
                }
    
                try
                {
                    if (!IsAmandaRostley(__instance))
                    {
                        return;
                    }
    
                    Item auraItem =
                        BlightcallerAuras.GetItem(
                            FirstAuraKey);
    
                    if (auraItem == null)
                    {
                        Plugin.NativeLog.LogError(
                            "Blightcaller aura vendor: Aura of Withering is not registered.");
    
                        return;
                    }
    
                    if (__instance.ItemsForSale == null)
                    {
                        __instance.ItemsForSale =
                            new List<Item>();
                    }
    
                    if (__instance.ItemsForSale.Contains(
                            auraItem))
                    {
                        return;
                    }
    
                    __instance.ItemsForSale.Add(
                        auraItem);
                }
                catch (Exception ex)
                {
                    Plugin.NativeLog.LogError(
                        "Blightcaller aura vendor setup failed: " +
                        ex);
                }
            }
    
            private static bool IsAmandaRostley(
                VendorInventory vendor)
            {
                Transform current =
                    vendor.transform;
    
                while (current != null)
                {
                    if (string.Equals(
                            current.name,
                            VendorName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
    
                    current =
                        current.parent;
                }
    
                return false;
            }
        }
}
