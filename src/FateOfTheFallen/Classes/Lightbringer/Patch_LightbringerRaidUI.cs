using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FateOfTheFallen
{
    /// <summary>
    /// Native RaidManager class visual handling has no Lightbringer branch.
    /// This mirrors the proven Blightcaller UI path while keeping detection
    /// isolated to Lightbringer.
    /// </summary>
    internal static class LightbringerRaidUI
    {
        private const float RaidIconScale =
            1.0f;

        internal static bool IsPlayerLightbringer()
        {
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

        internal static bool IsLightbringerRaidSlot(
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
                    if (LightbringerCatalog.IsLightbringerClass(
                            stats.CharacterClass))
                    {
                        return true;
                    }

                    if (stats == GameData.PlayerStats &&
                        IsPlayerLightbringer())
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
                    LightbringerConstants.DisplayName,
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
                !IsPlayerLightbringer())
            {
                return;
            }

            Image playerIcon =
                manager.PlayerNameCard.PlayerIcon;

            if (playerIcon != null)
            {
                if (LightbringerClassIcon.Sprite != null)
                {
                    playerIcon.sprite =
                        LightbringerClassIcon.Sprite;
                }

                playerIcon.color =
                    LightbringerClassIcon.IconColor;

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
                    LightbringerClassIcon.NameplateColor;
            }
        }

        internal static void ApplyRaidSlot(
            RaidMemberSlot slot)
        {
            if (slot == null ||
                !IsLightbringerRaidSlot(slot))
            {
                return;
            }

            if (slot.ClassIcon != null)
            {
                if (LightbringerClassIcon.Sprite != null)
                {
                    slot.ClassIcon.sprite =
                        LightbringerClassIcon.Sprite;
                }

                slot.ClassIcon.color =
                    LightbringerClassIcon.IconColor;

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
                    LightbringerClassIcon.NameplateColor;
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
    internal static class Patch_LightbringerRaidClassIcon
    {
        private static bool Prefix(
            RaidMemberSlot slot)
        {
            try
            {
                if (slot == null ||
                    !LightbringerRaidUI.IsLightbringerRaidSlot(slot))
                {
                    return true;
                }

                LightbringerRaidUI.ApplyRaidSlot(slot);

                /*
                 * Skip the native method for Lightbringer. Native Erenshor
                 * does not recognize the custom class and may clear/replace
                 * the sprite or colour we just assigned.
                 */
                return false;
            }
            catch (Exception exception)
            {
                Plugin.LogError(
                    "Lightbringer: failed to apply RaidUI class visuals: " +
                    exception);

                return true;
            }
        }
    }
}
