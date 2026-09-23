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
         *
         * 1.35f is a good first match for the current asset.
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

            return playerStats != null &&
                   BlightcallerCatalog.IsBlightcallerClass(
                       playerStats.CharacterClass);
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
            if (slot == null ||
                slot.AssignedAvatar == null)
            {
                return;
            }

            bool isBlightcaller =
                false;

            Stats stats =
                slot.AssignedAvatar.MyStats;


            // ========================================================
            // NORMAL CLASS CHECK
            // ========================================================

            if (stats != null &&
                BlightcallerCatalog.IsBlightcallerClass(
                    stats.CharacterClass))
            {
                isBlightcaller =
                    true;
            }


            // ========================================================
            // PLAYER SLOT FALLBACK
            // ========================================================
            //
            // During some parts of initialization the player's
            // CharacterClass can temporarily contain a native class.
            // ========================================================

            if (!isBlightcaller &&
                stats != null &&
                stats == GameData.PlayerStats &&
                IsPlayerBlightcaller())
            {
                isBlightcaller =
                    true;
            }

            if (!isBlightcaller)
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
}