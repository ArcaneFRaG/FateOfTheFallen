using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FateOfTheFallen
{
    [HarmonyPatch(
            typeof(ItemDatabase),
            "Start")]
    internal static class Patch_BlightcallerItemDatabaseStart
        {
            private static void Postfix()
            {
                try
                {
                    BlightcallerEquipment.RegisterArcanistEquipment();
    
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
    
    [HarmonyPatch(
            typeof(ItemDatabase),
            "GetItemByID")]
    internal static class Patch_BlightcallerItemDatabase
        {
            private static bool Prefix(
                ItemDatabase __instance,
                string id,
                ref Item __result)
            {
                try
                {
                    Item item =
                        BlightcallerItemFactory.GetItemById(
                            id);
    
                    if (item == null)
                    {
                        return true;
                    }
    
                    __result = item;
    
                    return false;
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "Blightcaller: custom ItemDatabase lookup failed: " +
                        exception);
    
                    return true;
                }
            }
        }
}
