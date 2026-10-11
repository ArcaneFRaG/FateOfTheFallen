using System;

namespace FateOfTheFallen
{
    internal static class EmberweaverCharacterState
    {
        internal static bool IsEmberweaver(
            SaveGameData slot)
        {
            return slot != null &&
                   string.Equals(
                       slot.CharClass,
                       EmberweaverConstants.DisplayName,
                       StringComparison.OrdinalIgnoreCase);
        }

        internal static void SelectForCreation(
            CharSelectManager manager)
        {
            EmberweaverCharacterCreation.SelectEmberweaver(
                manager);
        }

        internal static void RestoreRuntimeClass()
        {
            Class emberweaver =
                EmberweaverCatalog.EnsureClass();

            if (emberweaver == null ||
                GameData.PlayerStats == null)
            {
                return;
            }

            GameData.PlayerStats.CharacterClass =
                emberweaver;

            // Refresh the native proficiency/stat scale modifiers immediately
            // after restoring the custom class so existing Emberweavers pick up
            // the same scaling table as Blightcaller without waiting for a
            // later inventory/stat refresh.
            try
            {
                GameData.PlayerStats.CalcStats();
            }
            catch
            {
            }
        }

        internal static void RestoreIfCurrentSlotIsEmberweaver()
        {
            RestoreIfCurrentSlotIsEmberweaver(
                null);
        }

        internal static void RestoreIfCurrentSlotIsEmberweaver(
            CharSelectManager manager)
        {
            if (!IsEmberweaver(
                    GameData.CurrentCharacterSlot))
            {
                return;
            }

            RestoreRuntimeClass();

            if (manager != null)
            {
                EmberweaverCharacterCreation.RestoreSelectedClass(
                    manager);
            }
        }

        internal static void ForceCurrentSaveIdentityIfSelected()
        {
            if (GameData.PlayerStats == null ||
                !EmberweaverCatalog.IsEmberweaverClass(
                    GameData.PlayerStats.CharacterClass))
            {
                return;
            }

            if (GameData.CurrentCharacterSlot != null)
            {
                GameData.CurrentCharacterSlot.CharClass =
                    EmberweaverConstants.DisplayName;
            }
        }
    }
}
