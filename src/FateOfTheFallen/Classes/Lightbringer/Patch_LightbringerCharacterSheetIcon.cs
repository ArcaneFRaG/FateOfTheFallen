using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FateOfTheFallen
{
    /// <summary>
    /// Native ClassIcon.Start() only recognizes native classes. Let native
    /// setup finish, then replace the character-sheet icon for Lightbringer.
    /// </summary>
    [HarmonyPatch(
        typeof(ClassIcon),
        "Start")]
    internal static class Patch_LightbringerCharacterSheetIcon
    {
        [HarmonyPostfix]
        private static void Postfix(
            ClassIcon __instance)
        {
            try
            {
                if (__instance == null ||
                    !IsPlayerLightbringer())
                {
                    return;
                }

                Sprite sprite =
                    LightbringerClassIcon.Sprite;

                if (sprite == null)
                {
                    Plugin.LogWarning(
                        "Lightbringer CharacterSheet: class icon sprite was null.");

                    return;
                }

                Image image =
                    __instance.GetComponent<Image>();

                if (image == null)
                {
                    Plugin.LogWarning(
                        "Lightbringer CharacterSheet: ClassIcon had no Image component.");

                    return;
                }

                image.sprite = sprite;
                image.color = Color.white;
                image.enabled = true;

                if (!image.gameObject.activeSelf)
                {
                    image.gameObject.SetActive(true);
                }
            }
            catch (Exception exception)
            {
                Plugin.LogError(
                    "Lightbringer CharacterSheet: failed to apply class icon: " +
                    exception);
            }
        }

        private static bool IsPlayerLightbringer()
        {
            /*
             * Saved identity is authoritative during the short period in
             * which Erenshor may temporarily restore a native class object.
             */
            if (GameData.CurrentCharacterSlot != null &&
                string.Equals(
                    GameData.CurrentCharacterSlot.CharClass,
                    LightbringerConstants.DisplayName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Stats playerStats =
                GameData.PlayerStats;

            return
                playerStats != null &&
                LightbringerCatalog.IsLightbringerClass(
                    playerStats.CharacterClass);
        }
    }
}
