using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    internal static class BlightcallerSceneLoad
    {
        private static bool _installed;

        internal static void Install()
        {
            if (_installed)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            _installed = true;
        }

        internal static void Uninstall()
        {
            if (!_installed)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            _installed = false;
        }

        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            try
            {
                if (GameData.CurrentCharacterSlot == null)
                {
                    return;
                }

                if (!string.Equals(
                        GameData.CurrentCharacterSlot.CharClass,
                        "Blightcaller",
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                BlightcallerCatalog.EnsureClass();

                if (BlightcallerCatalog.BlightcallerClass == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "Blightcaller class could not be restored.");

                    return;
                }

                if (GameData.PlayerStats == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "PlayerStats unavailable during Blightcaller restoration.");

                    return;
                }

                GameData.PlayerStats.CharacterClass =
                    BlightcallerCatalog.BlightcallerClass;

                BlightcallerCatalog.RestoreKnownSpells();

                try
                {
                    GameData.PlayerStats.CalcStats();
                }
                catch (System.Exception ex)
                {
                    Plugin.NativeLog.LogWarning(
                        "Blightcaller CalcStats after scene load failed: " +
                        ex.Message);
                }
            }
            catch (System.Exception ex)
            {
                if (Plugin.NativeLog != null)
                {
                    Plugin.NativeLog.LogError(
                        "Blightcaller scene-load restoration failed: " +
                        ex);
                }
            }
        }
    }
}