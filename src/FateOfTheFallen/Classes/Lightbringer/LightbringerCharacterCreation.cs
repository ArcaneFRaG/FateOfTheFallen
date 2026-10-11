using System;
using TMPro;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class LightbringerCharacterCreation
    {
        internal static void SelectLightbringer(CharSelectManager manager)
        {
            if (!FOTFContentOptions.Active.NewLightbringers) return;

            if (manager == null || GameData.CurrentCharacterSlot == null)
                return;

            Class lightbringer = LightbringerCatalog.EnsureClass();
            if (lightbringer == null) return;

            GameData.CurrentCharacterSlot.CharClass = LightbringerConstants.DisplayName;
            if (GameData.PlayerStats != null)
                GameData.PlayerStats.CharacterClass = lightbringer;
            if (manager.PlayerStats != null)
                manager.PlayerStats.CharacterClass = lightbringer;
            manager.selClass = lightbringer;
            UpdateDescription(manager);

            try
            {
                Stats stats = manager.PlayerStats ?? GameData.PlayerStats;
                if (stats != null)
                {
                    stats.ClearSpentProfPoints();
                    stats.CalcStats();
                }
            }
            catch (Exception e) { Plugin.LogWarning("[Lightbringer] Character selection stats: " + e.Message); }
        }

        internal static void RestoreSelectedClass(CharSelectManager manager)
        {
            if (manager == null || !LightbringerCharacterState.IsLightbringer(GameData.CurrentCharacterSlot))
                return;
            Class lightbringer = LightbringerCatalog.TryEnsureClass();
            if (lightbringer == null) return;
            if (GameData.PlayerStats != null)
                GameData.PlayerStats.CharacterClass = lightbringer;
            if (manager.PlayerStats != null)
                manager.PlayerStats.CharacterClass = lightbringer;
            manager.selClass = lightbringer;
            if (manager.CharCreate != null && manager.CharCreate.activeInHierarchy)
                UpdateDescription(manager);
            if (manager.CharInfo != null && manager.PlayerStats != null &&
                !string.IsNullOrEmpty(manager.PlayerStats.MyName))
                manager.CharInfo.text = manager.PlayerStats.MyName + " - Level " +
                    manager.PlayerStats.Level + " Lightbringer\n" + GameData.SceneName;
        }

        private static void UpdateDescription(CharSelectManager manager)
        {
            TextMeshProUGUI label = manager != null ? manager.ClassDesc : null;
            if (label == null) return;
            label.enableWordWrapping = true;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = Mathf.Max(14f, label.fontSize);
            label.overflowMode = TextOverflowModes.Overflow;
            label.text = LightbringerConstants.Description;
        }
    }
}
