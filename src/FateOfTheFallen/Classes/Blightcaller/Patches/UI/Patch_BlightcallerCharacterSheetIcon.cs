using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FateOfTheFallen
{
    // ============================================================
    // BLIGHTCALLER CHARACTER SHEET CLASS ICON
    // ============================================================
    //
    // ClassIcon.Start() assigns one of Erenshor's native class
    // sprites.
    //
    // Let the native method finish first, then replace that sprite
    // when the active character is a Blightcaller.
    // ============================================================

    [HarmonyPatch(
        typeof(ClassIcon),
        "Start")]
    internal static class Patch_BlightcallerCharacterSheetIcon
    {
        [HarmonyPostfix]
        private static void Postfix(
            ClassIcon __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                if (!IsPlayerBlightcaller())
                {
                    return;
                }

                Sprite sprite =
                    BlightcallerClassIcon.Sprite;

                if (sprite == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "Blightcaller CharacterSheet: class icon sprite was null.");

                    return;
                }

                Image image =
                    __instance.GetComponent<Image>();

                if (image == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "Blightcaller CharacterSheet: ClassIcon had no Image component.");

                    return;
                }

                image.sprite =
                    sprite;

                image.enabled =
                    true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller CharacterSheet: failed to apply class icon: " +
                    exception);
            }
        }


        // ========================================================
        // CLASS CHECK
        // ========================================================
        //
        // Prefer the saved class identity because PlayerStats can
        // briefly contain a native class during loading.
        // ========================================================

        private static bool IsPlayerBlightcaller()
        {
            if (GameData.CurrentCharacterSlot != null &&
                string.Equals(
                    GameData.CurrentCharacterSlot.CharClass,
                    "Blightcaller",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Stats playerStats =
                GameData.PlayerStats;

            if (playerStats == null ||
                playerStats.CharacterClass == null)
            {
                return false;
            }

            return BlightcallerCatalog.IsBlightcallerClass(
                playerStats.CharacterClass);
        }
    }
}