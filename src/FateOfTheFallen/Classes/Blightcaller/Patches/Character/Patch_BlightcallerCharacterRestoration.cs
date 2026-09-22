using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FateOfTheFallen
{
    [HarmonyPatch(typeof(CharSelectManager), "SelectSlot")]
    internal static class Patch_BlightcallerSelectSlotClass
        {
            private static void Prefix(int _index)
            {
                try
                {
                    if (GameData.SaveSlots == null)
                        return;
    
                    if (_index < 0 || _index >= GameData.SaveSlots.Count)
                        return;
    
                    SaveGameData slot = GameData.SaveSlots[_index];
    
                    if (slot == null)
                        return;
    
                    if (!string.Equals(
                            slot.CharClass,
                            "Blightcaller",
                            StringComparison.OrdinalIgnoreCase))
                        return;
    
                    BlightcallerCatalog.EnsureClass();
    
                    Class blightcaller = BlightcallerCatalog.BlightcallerClass;
    
                    if (blightcaller == null)
                    {
                        Plugin.NativeLog.LogError(
                            "CharSelect: Blightcaller class is null before SelectSlot.");
    
                        return;
                    }
    
                    if (GameData.PlayerStats == null)
                    {
                        Plugin.NativeLog.LogWarning(
                            "CharSelect: PlayerStats is null before SelectSlot.");
    
                        return;
                    }
    
                    // IMPORTANT:
                    // Set this BEFORE the native SelectSlot() code executes.
                    //
                    // Native SelectSlot() only recognizes:
                    // Paladin
                    // Arcanist
                    // Duelist
                    // Druid
                    // Stormcaller
                    // Reaver
                    //
                    // Therefore "Blightcaller" causes none of the native
                    // assignments to execute, leaving our class intact.
    
                    GameData.PlayerStats.CharacterClass = blightcaller;
    
                    Plugin.NativeLog.LogDebug(
                        "CharSelect: set PlayerStats.CharacterClass to Blightcaller BEFORE native SelectSlot.");
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "CharSelect: failed to set Blightcaller before SelectSlot: " +
                        exception);
                }
            }
        }
    
    [HarmonyPatch(typeof(CharSelectManager), "Play")]
    internal static class Patch_BlightcallerPlayClass
        {
            private static void Prefix()
            {
                try
                {
                    if (GameData.CurrentCharacterSlot == null)
                        return;
    
                    if (!BlightcallerCharacterClass.IsBlightcaller(
                            GameData.CurrentCharacterSlot))
                        return;
    
                    Plugin.NativeLog.LogDebug(
                        "CharSelect: restoring Blightcaller before Play.");
    
                    BlightcallerCharacterClass.RestoreBlightcaller();
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "CharSelect: failed to restore Blightcaller before Play: " +
                        exception);
                }
            }
        }
    
    [HarmonyPatch(typeof(CharSelectManager), "PlayAlternate")]
    internal static class Patch_BlightcallerPlayAlternateClass
        {
            private static void Prefix()
            {
                try
                {
                    if (GameData.CurrentCharacterSlot == null)
                        return;
    
                    if (!BlightcallerCharacterClass.IsBlightcaller(
                            GameData.CurrentCharacterSlot))
                        return;
    
                    Plugin.NativeLog.LogDebug(
                        "CharSelect: restoring Blightcaller before PlayAlternate.");
    
                    BlightcallerCharacterClass.RestoreBlightcaller();
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "CharSelect: failed to restore Blightcaller before PlayAlternate: " +
                        exception);
                }
            }
        }
    
    [HarmonyPatch(typeof(CharSelectManager), "SelectBackupSlot")]
    internal static class Patch_BlightcallerSelectBackupSlotClass
        {
            private static void Postfix()
            {
                try
                {
                    if (GameData.CurrentCharacterSlot == null)
                        return;
    
                    if (!BlightcallerCharacterClass.IsBlightcaller(
                            GameData.CurrentCharacterSlot))
                        return;
    
                    Plugin.NativeLog.LogDebug(
                        "CharSelect: restoring Blightcaller after SelectBackupSlot.");
    
                    BlightcallerCharacterClass.RestoreBlightcaller();
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "CharSelect: failed to restore Blightcaller after SelectBackupSlot: " +
                        exception);
                }
            }
        }
}
