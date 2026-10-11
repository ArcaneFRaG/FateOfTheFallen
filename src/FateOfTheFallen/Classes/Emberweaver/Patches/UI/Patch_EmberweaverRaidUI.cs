using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FateOfTheFallen
{
    /// <summary>
    /// Native RaidManager class visual handling has no Emberweaver branch.
    /// This mirrors the proven Blightcaller UI path while keeping detection
    /// isolated to Emberweaver.
    /// </summary>
    internal static class EmberweaverRaidUI
    {
        private const float RaidIconScale =
            1.0f;

        internal static bool IsPlayerEmberweaver()
        {
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

        internal static bool IsEmberweaverRaidSlot(
            RaidMemberSlot slot)
        {
            if (slot == null)
            {
                return false;
            }

            if (slot.AssignedAvatar != null)
            {
                Stats stats =
                    slot.AssignedAvatar.MyStats;

                if (stats != null)
                {
                    if (EmberweaverCatalog.IsEmberweaverClass(
                            stats.CharacterClass))
                    {
                        return true;
                    }

                    if (stats == GameData.PlayerStats &&
                        IsPlayerEmberweaver())
                    {
                        return true;
                    }
                }
            }

            SimPlayerTracking tracking =
                slot.AssignedSimTracking;

            if (tracking != null &&
                string.Equals(
                    tracking.ClassName,
                    EmberweaverConstants.DisplayName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        internal static void ApplyPlayerCard(
            RaidManager manager)
        {
            if (manager == null ||
                manager.PlayerNameCard == null ||
                !IsPlayerEmberweaver())
            {
                return;
            }

            Image playerIcon =
                manager.PlayerNameCard.PlayerIcon;

            if (playerIcon != null)
            {
                if (EmberweaverClassIcon.Sprite != null)
                {
                    playerIcon.sprite =
                        EmberweaverClassIcon.Sprite;
                }

                playerIcon.color =
                    EmberweaverClassIcon.IconColor;

                playerIcon.enabled = true;

                if (!playerIcon.gameObject.activeSelf)
                {
                    playerIcon.gameObject.SetActive(true);
                }

                ScaleRaidIcon(playerIcon);
            }

            if (manager.PlayerNameCard.MyNamePlate != null)
            {
                manager.PlayerNameCard.MyNamePlate.color =
                    EmberweaverClassIcon.NameplateColor;
            }
        }

        internal static void ApplyRaidSlot(
            RaidMemberSlot slot)
        {
            if (slot == null ||
                !IsEmberweaverRaidSlot(slot))
            {
                return;
            }

            if (slot.ClassIcon != null)
            {
                if (EmberweaverClassIcon.Sprite != null)
                {
                    slot.ClassIcon.sprite =
                        EmberweaverClassIcon.Sprite;
                }

                slot.ClassIcon.color =
                    EmberweaverClassIcon.IconColor;

                slot.ClassIcon.enabled = true;

                if (!slot.ClassIcon.gameObject.activeSelf)
                {
                    slot.ClassIcon.gameObject.SetActive(true);
                }

                ScaleRaidIcon(slot.ClassIcon);
            }

            if (slot.MyNameplate != null)
            {
                slot.MyNameplate.color =
                    EmberweaverClassIcon.NameplateColor;
            }
        }

        private static void ScaleRaidIcon(
            Image image)
        {
            if (image == null ||
                image.rectTransform == null)
            {
                return;
            }

            image.rectTransform.localScale =
                new Vector3(
                    RaidIconScale,
                    RaidIconScale,
                    1f);
        }
    }

    [HarmonyPatch(
        typeof(RaidManager),
        "AssignClassIcon")]
    internal static class Patch_EmberweaverRaidClassIcon
    {
        private static bool Prefix(
            RaidMemberSlot slot)
        {
            try
            {
                if (slot == null ||
                    !EmberweaverRaidUI.IsEmberweaverRaidSlot(slot))
                {
                    return true;
                }

                EmberweaverRaidUI.ApplyRaidSlot(slot);

                /*
                 * Skip the native method for Emberweaver. Native Erenshor
                 * does not recognize the custom class and may clear/replace
                 * the sprite or colour we just assigned.
                 */
                return false;
            }
            catch (Exception exception)
            {
                Plugin.LogError(
                    "Emberweaver: failed to apply RaidUI class visuals: " +
                    exception);

                return true;
            }
        }
    }
}
