using System;

namespace FateOfTheFallen
{
    internal static class LightbringerCharacterState
    {
        internal static bool IsLightbringer(SaveGameData slot)
        {
            return slot != null && string.Equals(slot.CharClass,
                LightbringerConstants.DisplayName, StringComparison.OrdinalIgnoreCase);
        }
        internal static void Restore(CharSelectManager manager = null)
        {
            if (!IsLightbringer(GameData.CurrentCharacterSlot)) return;
            Class lightbringer = LightbringerCatalog.TryEnsureClass();
            if (lightbringer == null) return;
            if (GameData.PlayerStats != null)
                GameData.PlayerStats.CharacterClass = lightbringer;
            if (manager != null)
                LightbringerCharacterCreation.RestoreSelectedClass(manager);
        }
        internal static void PreserveSaveIdentity()
        {
            if (GameData.PlayerStats != null &&
                LightbringerCatalog.IsLightbringerClass(GameData.PlayerStats.CharacterClass) &&
                GameData.CurrentCharacterSlot != null)
                GameData.CurrentCharacterSlot.CharClass = LightbringerConstants.DisplayName;
        }
    }
}
