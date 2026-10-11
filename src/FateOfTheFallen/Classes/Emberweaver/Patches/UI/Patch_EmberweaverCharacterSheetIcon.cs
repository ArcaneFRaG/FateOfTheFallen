using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FateOfTheFallen
{
    /// <summary>
    /// Native ClassIcon.Start() only recognizes native classes. Let native
    /// setup finish, then replace the character-sheet icon for Emberweaver.
    /// </summary>
    [HarmonyPatch(
        typeof(ClassIcon),
        "Start")]
    internal static class Patch_EmberweaverCharacterSheetIcon
    {
        [HarmonyPostfix]
        private static void Postfix(
            ClassIcon __instance)
        {
            try
            {
                if (__instance == null ||
                    !IsPlayerEmberweaver())
                {
                    return;
                }

                Sprite sprite =
                    EmberweaverClassIcon.Sprite;

                if (sprite == null)
                {
                    Plugin.LogWarning(
                        "Emberweaver CharacterSheet: class icon sprite was null.");

                    return;
                }

                Image image =
                    __instance.GetComponent<Image>();

                if (image == null)
                {
                    Plugin.LogWarning(
                        "Emberweaver CharacterSheet: ClassIcon had no Image component.");

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
                    "Emberweaver CharacterSheet: failed to apply class icon: " +
                    exception);
            }
        }

        private static bool IsPlayerEmberweaver()
        {
            /*
             * Saved identity is authoritative during the short period in
             * which Erenshor may temporarily restore a native class object.
             */
            if (GameData.CurrentCharacterSlot != null &&
                string.Equals(
                    GameData.CurrentCharacterSlot.CharClass,
                    EmberweaverConstants.DisplayName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Stats playerStats =
                GameData.PlayerStats;

            return
                playerStats != null &&
                EmberweaverCatalog.IsEmberweaverClass(
                    playerStats.CharacterClass);
        }
    }
}
