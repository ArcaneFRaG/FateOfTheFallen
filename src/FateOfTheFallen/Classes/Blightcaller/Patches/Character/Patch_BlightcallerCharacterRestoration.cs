using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    // ============================================================
    // BLIGHTCALLER CHARACTER CLASS HELPER
    // ============================================================

    internal static class BlightcallerCharacterClass
    {
        internal static bool IsBlightcaller(
            SaveGameData slot)
        {
            if (slot == null)
            {
                return false;
            }

            return string.Equals(
                slot.CharClass,
                "Blightcaller",
                StringComparison.OrdinalIgnoreCase);
        }


        internal static void RestoreBlightcaller()
        {
            try
            {
                BlightcallerCatalog.EnsureClass();

                Class blightcaller =
                    BlightcallerCatalog.BlightcallerClass;

                if (blightcaller == null)
                {
                    Plugin.NativeLog.LogError(
                        "CharSelect: Blightcaller class is unavailable during restoration.");

                    return;
                }

                if (GameData.PlayerStats == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "CharSelect: PlayerStats is null during Blightcaller restoration.");

                    return;
                }

                GameData.PlayerStats.CharacterClass =
                    blightcaller;

                
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "CharSelect: failed to restore Blightcaller class: " +
                    exception);
            }
        }
    }


    // ============================================================
    // SELECT SLOT
    // ============================================================

    [HarmonyPatch(
        typeof(CharSelectManager),
        "SelectSlot")]
    internal static class Patch_BlightcallerSelectSlotClass
    {
        private static void Prefix(
            int _index)
        {
            try
            {
                if (GameData.SaveSlots == null)
                {
                    return;
                }

                if (_index < 0 ||
                    _index >= GameData.SaveSlots.Count)
                {
                    return;
                }

                SaveGameData slot =
                    GameData.SaveSlots[_index];

                if (slot == null)
                {
                    return;
                }

                if (!string.Equals(
                        slot.CharClass,
                        "Blightcaller",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                BlightcallerCatalog.EnsureClass();

                Class blightcaller =
                    BlightcallerCatalog.BlightcallerClass;

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

                /*
                 * Set this BEFORE native SelectSlot().
                 *
                 * Native SelectSlot() only recognizes the game's
                 * built-in classes. Because it does not recognize
                 * "Blightcaller", none of its native class assignments
                 * overwrite this value.
                 */

                GameData.PlayerStats.CharacterClass =
                    blightcaller;

               
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "CharSelect: failed to set Blightcaller before SelectSlot: " +
                    exception);
            }
        }
    }


    // ============================================================
    // PLAY
    // ============================================================

    [HarmonyPatch(
        typeof(CharSelectManager),
        "Play")]
    internal static class Patch_BlightcallerPlayClass
    {
        private static void Prefix()
        {
            try
            {
                if (GameData.CurrentCharacterSlot == null)
                {
                    return;
                }

                if (!BlightcallerCharacterClass.IsBlightcaller(
                        GameData.CurrentCharacterSlot))
                {
                    return;
                }

                

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


    // ============================================================
    // PLAY ALTERNATE
    // ============================================================

    [HarmonyPatch(
        typeof(CharSelectManager),
        "PlayAlternate")]
    internal static class Patch_BlightcallerPlayAlternateClass
    {
        private static void Prefix()
        {
            try
            {
                if (GameData.CurrentCharacterSlot == null)
                {
                    return;
                }

                if (!BlightcallerCharacterClass.IsBlightcaller(
                        GameData.CurrentCharacterSlot))
                {
                    return;
                }

                

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


    // ============================================================
    // BACKUP SLOT
    // ============================================================

    [HarmonyPatch(
        typeof(CharSelectManager),
        "SelectBackupSlot")]
    internal static class Patch_BlightcallerSelectBackupSlotClass
    {
        private static void Postfix()
        {
            try
            {
                if (GameData.CurrentCharacterSlot == null)
                {
                    return;
                }

                if (!BlightcallerCharacterClass.IsBlightcaller(
                        GameData.CurrentCharacterSlot))
                {
                    return;
                }

                

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