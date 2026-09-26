using HarmonyLib;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace FateOfTheFallen
{
    internal static class BlightcallerRaidUI
    {
        // ============================================================
        // RAID ICON SCALE
        // ============================================================

        /*
         * The Blightcaller icon PNG is 256x256, but the actual
         * non-transparent artwork occupies considerably less of the
         * canvas than the native class icons.
         *
         * Scale only the RaidUI Image so that:
         *
         * - the character-sheet icon is unaffected
         * - the original artwork does not need modifying
         */
        private const float RaidIconScale =
            1.7f;


        // ============================================================
        // PLAYER CLASS CHECK
        // ============================================================

        internal static bool IsPlayerBlightcaller()
        {
            /*
             * Saved identity is authoritative for our custom class.
             */
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

            return
                playerStats != null &&
                BlightcallerCatalog.IsBlightcallerClass(
                    playerStats.CharacterClass);
        }


        // ============================================================
        // RAID SLOT CLASS CHECK
        // ============================================================
        //
        // Raid slots do not always have a fully initialized
        // AssignedAvatar when their class visuals are populated.
        //
        // Blightcaller SimPlayers therefore need to be recognized
        // through:
        //
        // 1. The live SimPlayer avatar, when available.
        // 2. AssignedSimTracking.ClassName.
        // 3. The persistent Blightcaller SimPlayer assignment sidecar.
        //
        // This also protects against the native Arcanist save fallback
        // temporarily appearing on a custom-class SimPlayer.
        // ============================================================

        internal static bool IsBlightcallerRaidSlot(
            RaidMemberSlot slot)
        {
            if (slot == null)
            {
                return false;
            }


            // ========================================================
            // LIVE AVATAR
            // ========================================================

            if (slot.AssignedAvatar != null)
            {
                Stats stats =
                    slot.AssignedAvatar.MyStats;

                if (stats != null)
                {
                    /*
                     * Actual runtime Blightcaller class.
                     */
                    if (BlightcallerCatalog.IsBlightcallerClass(
                            stats.CharacterClass))
                    {
                        return true;
                    }


                    /*
                     * Player raid-slot fallback.
                     *
                     * During some initialization paths the player's
                     * runtime class can temporarily contain a native
                     * class object.
                     */
                    if (stats == GameData.PlayerStats &&
                        IsPlayerBlightcaller())
                    {
                        return true;
                    }
                }
            }


            // ========================================================
            // SIMPLAYER TRACKING DATA
            // ========================================================

            SimPlayerTracking tracking =
                slot.AssignedSimTracking;

            if (tracking != null)
            {
                /*
                 * The normal tracking identity should already contain
                 * "Blightcaller" once our population reconciliation
                 * has run.
                 */
                if (string.Equals(
                        tracking.ClassName,
                        "Blightcaller",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }


                /*
                 * Persistent assignment fallback.
                 *
                 * Native save data stores Blightcaller SimPlayers as
                 * Arcanists because SimPlayerSaveData has no custom
                 * class field.
                 *
                 * The sidecar remains authoritative for the real
                 * custom-class identity.
                 */
                if (!string.IsNullOrWhiteSpace(
                        tracking.SimName) &&
                    BlightcallerSimPlayers.IsAssigned(
                        tracking.SimName))
                {
                    return true;
                }
            }


            return false;
        }


        // ============================================================
        // PLAYER RAID CARD
        // ============================================================

        internal static void ApplyPlayerCard(
            RaidManager manager)
        {
            if (manager == null ||
                manager.PlayerNameCard == null)
            {
                return;
            }

            if (!IsPlayerBlightcaller())
            {
                return;
            }


            // ========================================================
            // CLASS ICON
            // ========================================================

            Image playerIcon =
                manager.PlayerNameCard.PlayerIcon;

            if (playerIcon != null)
            {
                if (BlightcallerClassIcon.Sprite != null)
                {
                    playerIcon.sprite =
                        BlightcallerClassIcon.Sprite;
                }

                /*
                 * Use the centralized icon colour.
                 */
                playerIcon.color =
                    BlightcallerClassIcon.IconColor;

                /*
                 * Explicitly restore visibility in case native class
                 * handling disabled the icon while no class matched.
                 */
                playerIcon.enabled =
                    true;

                if (!playerIcon.gameObject.activeSelf)
                {
                    playerIcon.gameObject.SetActive(
                        true);
                }

                ScaleRaidIcon(
                    playerIcon);
            }


            // ========================================================
            // CLASS BAR COLOUR
            // ========================================================

            if (manager.PlayerNameCard.MyNamePlate != null)
            {
                manager.PlayerNameCard.MyNamePlate.color =
                    BlightcallerClassIcon.NameplateColor;
            }
        }


        // ============================================================
        // RAID MEMBER SLOT
        // ============================================================

        internal static void ApplyRaidSlot(
            RaidMemberSlot slot)
        {
            if (slot == null)
            {
                return;
            }

            /*
             * Do not require AssignedAvatar here.
             *
             * Raid slots can be initialized from AssignedSimTracking
             * before their SimPlayer avatar is fully spawned.
             */
            if (!IsBlightcallerRaidSlot(
                    slot))
            {
                return;
            }


            // ========================================================
            // CLASS ICON
            // ========================================================

            if (slot.ClassIcon != null)
            {
                if (BlightcallerClassIcon.Sprite != null)
                {
                    slot.ClassIcon.sprite =
                        BlightcallerClassIcon.Sprite;
                }

                slot.ClassIcon.color =
                    BlightcallerClassIcon.IconColor;


                /*
                 * Native RaidManager has no Blightcaller class branch.
                 *
                 * Ensure the Image itself has not been left disabled
                 * by native processing.
                 */
                slot.ClassIcon.enabled =
                    true;

                if (!slot.ClassIcon.gameObject.activeSelf)
                {
                    slot.ClassIcon.gameObject.SetActive(
                        true);
                }


                ScaleRaidIcon(
                    slot.ClassIcon);
            }


            // ========================================================
            // CLASS BAR COLOUR
            // ========================================================

            if (slot.MyNameplate != null)
            {
                slot.MyNameplate.color =
                    BlightcallerClassIcon.NameplateColor;
            }
        }


        // ============================================================
        // RAID ICON SCALING
        // ============================================================

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

    // ============================================================
    // BLIGHTCALLER RAID MEMBER CLASS VISUALS
    // ============================================================
    //
    // Native RaidManager.AssignClassIcon() only understands Erenshor's
    // native classes.
    //
    // Blightcaller SimPlayers therefore need to be intercepted before
    // native class handling runs.
    //
    // Detection supports:
    // - the live SimPlayer runtime class
    // - AssignedSimTracking.ClassName
    // - the persistent Blightcaller SimPlayer sidecar assignment
    // ============================================================

    [HarmonyPatch(
        typeof(RaidManager),
        "AssignClassIcon")]
    internal static class Patch_BlightcallerRaidClassIcon
    {
        private static bool Prefix(
            RaidMemberSlot slot)
        {
            try
            {
                if (slot == null)
                {
                    return true;
                }

                if (!BlightcallerRaidUI
                        .IsBlightcallerRaidSlot(
                            slot))
                {
                    /*
                     * Not ours.
                     *
                     * Allow native RaidManager handling.
                     */
                    return true;
                }


                // ====================================================
                // APPLY BLIGHTCALLER VISUALS
                // ====================================================

                BlightcallerRaidUI
                    .ApplyRaidSlot(
                        slot);


                /*
                 * Skip native AssignClassIcon().
                 *
                 * Native Erenshor has no Blightcaller branch and would
                 * otherwise overwrite/clear our custom visuals.
                 */
                return false;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: failed to apply SimPlayer RaidUI class visuals: " +
                    exception);

                /*
                 * Fail open so a UI patch failure does not break
                 * RaidManager completely.
                 */
                return true;
            }
        }
    }



}