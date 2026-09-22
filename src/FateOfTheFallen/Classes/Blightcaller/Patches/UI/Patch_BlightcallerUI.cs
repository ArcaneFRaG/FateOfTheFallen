using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace FateOfTheFallen
{
    [HarmonyPatch(typeof(ClassIcon), "Start")]
    internal static class Patch_BlightcallerCharacterSheetIcon
        {
            private static void Postfix(
                ClassIcon __instance)
            {
                try
                {
                    if (__instance == null)
                        return;
    
                    if (GameData.PlayerStats == null)
                        return;
    
                    Class characterClass =
                        GameData.PlayerStats.CharacterClass;
    
                    if (!BlightcallerCatalog.IsBlightcallerClass(
                            characterClass))
                    {
                        return;
                    }
    
                    Sprite sprite =
                        BlightcallerClassIcon.Sprite;
    
                    if (sprite == null)
                    {
                        Plugin.NativeLog.LogWarning(
                            "ClassIcon: Blightcaller icon sprite is null.");
                        return;
                    }
    
                    Image image =
                        __instance.GetComponent<Image>();
    
                    if (image == null)
                    {
                        Plugin.NativeLog.LogWarning(
                            "ClassIcon: could not find Image component.");
                        return;
                    }
    
                    image.sprite = sprite;
                    image.enabled = true;
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "ClassIcon: failed to apply Blightcaller icon: " +
                        exception);
                }
            }
        }
    
    internal static class BlightcallerRaidUI
        {
            // --------------------------------------------------------
            // Determine whether the player's own character is a
            // Blightcaller.
            // --------------------------------------------------------
    
            internal static bool IsPlayerBlightcaller()
            {
                if (GameData.PlayerStats == null)
                {
                    return false;
                }
    
                if (GameData.PlayerStats.CharacterClass == null)
                {
                    return false;
                }
    
                return BlightcallerCatalog.IsBlightcallerClass(
                    GameData.PlayerStats.CharacterClass);
            }
    
    
            // --------------------------------------------------------
            // Determine whether a raid slot represents the actual
            // player character.
            // --------------------------------------------------------
    
            internal static bool IsPlayerSlot(
                RaidMemberSlot slot)
            {
                if (slot == null ||
                    slot.AssignedAvatar == null ||
                    GameData.PlayerStats == null)
                {
                    return false;
                }
    
                if (slot.AssignedAvatar.MyStats ==
                    GameData.PlayerStats)
                {
                    return true;
                }
    
                return false;
            }
    
    
            // --------------------------------------------------------
            // Apply Blightcaller appearance to a raid member slot.
            // --------------------------------------------------------
    
            internal static void ApplyMember(
                RaidMemberSlot slot)
            {
                if (slot == null)
                {
                    return;
                }
    
                bool isBlightcaller = false;
    
                // ----------------------------------------------------
                // The player's raid slot must use GameData.PlayerStats.
                // This avoids the temporary native Paladin class.
                // ----------------------------------------------------
    
                if (IsPlayerSlot(slot))
                {
                    isBlightcaller =
                        IsPlayerBlightcaller();
                }
                else
                {
                    // ------------------------------------------------
                    // Non-player raid members can use their actual
                    // SimPlayer stats.
                    // ------------------------------------------------
    
                    if (slot.AssignedAvatar != null &&
                        slot.AssignedAvatar.MyStats != null &&
                        slot.AssignedAvatar.MyStats.CharacterClass != null)
                    {
                        isBlightcaller =
                            BlightcallerCatalog.IsBlightcallerClass(
                                slot.AssignedAvatar.MyStats.CharacterClass);
                    }
                }
    
                if (!isBlightcaller)
                {
                    return;
                }
    
                Sprite icon =
                    BlightcallerClassIcon.Sprite;
    
                // ----------------------------------------------------
                // Apply the colour even if the custom sprite has not
                // loaded yet.
                // ----------------------------------------------------
    
                if (slot.ClassIcon != null)
                {
                    if (icon != null)
                    {
                        slot.ClassIcon.sprite =
                            icon;
                    }
    
                    slot.ClassIcon.color =
                        BlightcallerClassIcon.IconColor;
                }
    
                if (slot.MyNameplate != null)
                {
                    slot.MyNameplate.color =
                        BlightcallerClassIcon.NameplateColor;
                }
            }
    
    
            // --------------------------------------------------------
            // Apply Blightcaller appearance to the player's raid card.
            // --------------------------------------------------------
    
            internal static void ApplyPlayerCard(
                RaidManager manager)
            {
                if (manager == null)
                {
                    return;
                }
    
                if (!IsPlayerBlightcaller())
                {
                    return;
                }
    
                object playerNameCard =
                    GetMemberValue(
                        manager,
                        "PlayerNameCard");
    
                if (playerNameCard == null)
                {
                    Plugin.ModLog.Error(
                        "Blightcaller: RaidManager PlayerNameCard could not be resolved.");
    
                    return;
                }
    
                Image playerIcon =
                    GetMemberValue(
                        playerNameCard,
                        "PlayerIcon") as Image;
    
                Image namePlate =
                    GetMemberValue(
                        playerNameCard,
                        "MyNamePlate") as Image;
    
                Sprite icon =
                    BlightcallerClassIcon.Sprite;
    
                if (playerIcon != null)
                {
                    if (icon != null)
                    {
                        playerIcon.sprite =
                            icon;
                    }
    
                    playerIcon.color =
                        BlightcallerClassIcon.IconColor;
                }
    
                if (namePlate != null)
                {
                    namePlate.color =
                        BlightcallerClassIcon.NameplateColor;
                }
            }
    
    
            // --------------------------------------------------------
            // Reflection helper.
            // --------------------------------------------------------
    
            private static object GetMemberValue(
                object instance,
                string memberName)
            {
                if (instance == null)
                {
                    return null;
                }
    
                Type type =
                    instance.GetType();
    
                FieldInfo field =
                    type.GetField(
                        memberName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);
    
                if (field != null)
                {
                    return field.GetValue(
                        instance);
                }
    
                PropertyInfo property =
                    type.GetProperty(
                        memberName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);
    
                if (property != null &&
                    property.CanRead)
                {
                    return property.GetValue(
                        instance,
                        null);
                }
    
                return null;
            }
        }
    
    [HarmonyPatch(
            typeof(RaidManager),
            "InitPlayerCard")]
    internal static class Patch_BlightcallerRaidPlayerCardIcon
        {
            private static bool Prefix(
                RaidManager __instance)
            {
                if (__instance == null)
                {
                    return true;
                }
    
                if (!BlightcallerRaidUI.IsPlayerBlightcaller())
                {
                    return true;
                }
    
                try
                {
                    BlightcallerRaidUI.ApplyPlayerCard(
                        __instance);
    
                    // Prevent the native Paladin/Arcanist/etc.
                    // implementation from running.
                    return false;
                }
                catch (Exception exception)
                {
                    Plugin.ModLog.Error(
                        "Blightcaller: raid player-card UI patch failed.",
                        exception);
    
                    return true;
                }
            }
        }
    
    [HarmonyPatch(
            typeof(RaidManager),
            "AssignClassIcon")]
    internal static class Patch_BlightcallerRaidClassIcon
        {
            private static bool Prefix(
                RaidMemberSlot slot)
            {
                if (slot == null)
                {
                    return true;
                }
    
                bool isBlightcaller = false;
    
                if (BlightcallerRaidUI.IsPlayerSlot(slot))
                {
                    isBlightcaller =
                        BlightcallerRaidUI.IsPlayerBlightcaller();
                }
                else if (slot.AssignedAvatar != null &&
                         slot.AssignedAvatar.MyStats != null &&
                         slot.AssignedAvatar.MyStats.CharacterClass != null)
                {
                    isBlightcaller =
                        BlightcallerCatalog.IsBlightcallerClass(
                            slot.AssignedAvatar.MyStats.CharacterClass);
                }
    
                if (!isBlightcaller)
                {
                    return true;
                }
    
                try
                {
                    BlightcallerRaidUI.ApplyMember(
                        slot);
    
                    // Prevent native Paladin/Arcanist/etc. handling.
                    return false;
                }
                catch (Exception exception)
                {
                    Plugin.ModLog.Error(
                        "Blightcaller: raid member UI patch failed.",
                        exception);
    
                    return true;
                }
            }
        }
}
