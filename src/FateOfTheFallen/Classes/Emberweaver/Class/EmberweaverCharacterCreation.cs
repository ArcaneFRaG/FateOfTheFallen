using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateOfTheFallen
{
    /// <summary>
    /// Character-creation presentation for Emberweaver inside the main FOTF
    /// plugin. Blightcaller still owns the proven vertical expansion of the
    /// native class area; Emberweaver shares that same custom row instead of
    /// introducing another independent character-creation layout system.
    /// </summary>
    internal static class EmberweaverCharacterCreation
    {
        private const string ButtonName =
            "CS_EmberweaverClassButton";

        private const string BlightcallerButtonName =
            "CS_BlightcallerClassButton";

        internal static void InstallButton(
            CharSelectManager manager)
        {
            if (manager == null ||
                manager.CharCreate == null)
            {
                return;
            }

            Class emberweaver =
                EmberweaverCatalog.EnsureClass();

            if (emberweaver == null)
            {
                return;
            }

            Button reaverButton =
                manager.CharCreate
                    .GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(
                        candidate =>
                            string.Equals(
                                GetButtonLabel(candidate),
                                "Reaver",
                                StringComparison.OrdinalIgnoreCase));

            if (reaverButton == null ||
                reaverButton.transform.parent == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Emberweaver: could not find the native Reaver class button.");
                return;
            }

            Transform parent =
                reaverButton.transform.parent;

            Transform existing =
                parent.Find(ButtonName);

            Button emberweaverButton =
                existing != null
                    ? existing.GetComponent<Button>()
                    : null;

            if (emberweaverButton == null)
            {
                GameObject templateObject =
                    parent.Find(BlightcallerButtonName) != null
                        ? parent.Find(BlightcallerButtonName).gameObject
                        : reaverButton.gameObject;

                GameObject clone =
                    UnityEngine.Object.Instantiate(
                        templateObject,
                        parent,
                        false);

                clone.name =
                    ButtonName;

                emberweaverButton =
                    clone.GetComponent<Button>();

                if (emberweaverButton == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "Emberweaver class-button clone has no Button component.");

                    UnityEngine.Object.Destroy(clone);
                    return;
                }
            }

            ConfigureButton(
                emberweaverButton,
                manager);

            PositionCustomRow(
                parent,
                emberweaverButton);

            emberweaverButton.gameObject.SetActive(true);
        }

        private static void ConfigureButton(
            Button button,
            CharSelectManager manager)
        {
            button.onClick =
                new Button.ButtonClickedEvent();

            button.onClick.AddListener(
                delegate
                {
                    SelectEmberweaver(manager);
                });

            TextMeshProUGUI label =
                button.GetComponentInChildren<TextMeshProUGUI>(
                    true);

            if (label != null)
            {
                label.text =
                    EmberweaverConstants.DisplayName;

                label.enableWordWrapping =
                    false;

                label.enableAutoSizing =
                    true;

                label.fontSizeMin =
                    10f;

                label.fontSizeMax =
                    label.fontSize;
            }
        }

        /// <summary>
        /// Blightcaller already expands the character-creation panel downward
        /// by one class row. Put Blightcaller and Emberweaver into the two
        /// native column positions on that row rather than expanding again.
        /// </summary>
        private static void PositionCustomRow(
            Transform parent,
            Button emberweaverButton)
        {
            RectTransform emberRect =
                emberweaverButton != null
                    ? emberweaverButton.GetComponent<RectTransform>()
                    : null;

            Transform blightTransform =
                parent != null
                    ? parent.Find(BlightcallerButtonName)
                    : null;

            RectTransform blightRect =
                blightTransform != null
                    ? blightTransform.GetComponent<RectTransform>()
                    : null;

            if (parent == null ||
                emberRect == null)
            {
                return;
            }

            Button stormcallerButton =
                parent
                    .GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(
                        button =>
                            button.transform.parent == parent &&
                            string.Equals(
                                GetButtonLabel(button),
                                "Stormcaller",
                                StringComparison.OrdinalIgnoreCase));

            Button reaverButton =
                parent
                    .GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(
                        button =>
                            button.transform.parent == parent &&
                            string.Equals(
                                GetButtonLabel(button),
                                "Reaver",
                                StringComparison.OrdinalIgnoreCase));

            RectTransform stormRect =
                stormcallerButton != null
                    ? stormcallerButton.GetComponent<RectTransform>()
                    : null;

            RectTransform reaverRect =
                reaverButton != null
                    ? reaverButton.GetComponent<RectTransform>()
                    : null;

            float customY =
                blightRect != null
                    ? blightRect.anchoredPosition.y
                    : emberRect.anchoredPosition.y;

            if (stormRect != null &&
                reaverRect != null)
            {
                if (blightRect != null)
                {
                    blightRect.anchoredPosition =
                        new Vector2(
                            stormRect.anchoredPosition.x,
                            customY);
                }

                emberRect.anchoredPosition =
                    new Vector2(
                        reaverRect.anchoredPosition.x,
                        customY);

                return;
            }

            float width =
                Mathf.Max(
                    80f,
                    emberRect.rect.width);

            float halfGap =
                (width + 8f) * 0.5f;

            if (blightRect != null)
            {
                float centerX =
                    blightRect.anchoredPosition.x;

                blightRect.anchoredPosition =
                    new Vector2(
                        centerX - halfGap,
                        customY);

                emberRect.anchoredPosition =
                    new Vector2(
                        centerX + halfGap,
                        customY);
            }
        }

        internal static void SelectEmberweaver(
            CharSelectManager manager)
        {
            if (!FOTFContentOptions.Active.NewEmberweavers) return;

            if (manager == null ||
                GameData.CurrentCharacterSlot == null)
            {
                return;
            }

            try
            {
                if (GameData.PlayerAud != null)
                {
                    GameData.PlayerAud.PlayOneShot(
                        GameData.Misc.Click,
                        GameData.UIVolume *
                        GameData.MasterVol);
                }
            }
            catch
            {
            }

            Class emberweaver =
                EmberweaverCatalog.EnsureClass();

            if (emberweaver == null)
            {
                return;
            }

            GameData.CurrentCharacterSlot.CharClass =
                EmberweaverConstants.DisplayName;

            if (GameData.PlayerStats != null)
            {
                GameData.PlayerStats.CharacterClass =
                    emberweaver;
            }

            if (manager.PlayerStats != null)
            {
                manager.PlayerStats.CharacterClass =
                    emberweaver;
            }

            manager.selClass =
                emberweaver;

            SetClassDescription(
                manager);

            try
            {
                if (GameData.PlayerStats != null)
                {
                    GameData.PlayerStats.ClearSpentProfPoints();
                    GameData.PlayerStats.CalcStats();
                }
                else if (manager.PlayerStats != null)
                {
                    manager.PlayerStats.ClearSpentProfPoints();
                    manager.PlayerStats.CalcStats();
                }
            }
            catch
            {
            }
        }

        internal static void RestoreSelectedClass(
            CharSelectManager manager)
        {
            if (manager == null ||
                !EmberweaverCharacterState.IsEmberweaver(
                    GameData.CurrentCharacterSlot))
            {
                return;
            }

            Class emberweaver =
                EmberweaverCatalog.EnsureClass();

            if (emberweaver == null)
            {
                return;
            }

            if (GameData.PlayerStats != null)
            {
                GameData.PlayerStats.CharacterClass =
                    emberweaver;
            }

            if (manager.PlayerStats != null)
            {
                manager.PlayerStats.CharacterClass =
                    emberweaver;
            }

            manager.selClass =
                emberweaver;

            if (manager.CharCreate != null &&
                manager.CharCreate.activeInHierarchy)
            {
                SetClassDescription(
                    manager);
            }

            if (manager.CharInfo != null &&
                manager.PlayerStats != null &&
                !string.IsNullOrEmpty(
                    manager.PlayerStats.MyName))
            {
                manager.CharInfo.text =
                    string.Concat(
                        manager.PlayerStats.MyName,
                        " - Level ",
                        manager.PlayerStats.Level,
                        " ",
                        EmberweaverConstants.DisplayName,
                        "\n",
                        GameData.SceneName);
            }

            try
            {
                if (manager.PlayerStats != null)
                {
                    manager.PlayerStats.CalcStats();
                }
            }
            catch
            {
            }
        }

        private static void SetClassDescription(
            CharSelectManager manager)
        {
            TextMeshProUGUI description =
                manager != null
                    ? manager.ClassDesc
                    : null;

            if (description == null)
            {
                return;
            }

            description.enableWordWrapping =
                true;

            description.enableAutoSizing =
                true;

            description.fontSizeMin =
                14f;

            description.fontSizeMax =
                Mathf.Max(
                    14f,
                    description.fontSize);

            description.overflowMode =
                TextOverflowModes.Overflow;

            description.text =
                "The Emberweaver is a single-target fire caster who builds momentum through sustained spellcasting.\n\n" +
                "Heat, Flashfire, and Wildfire are learned as passive skills from skillbooks, allowing the class to grow into its full rotation over time.\n\n" +
                "An Emberweaver should focus on Intelligence and other caster-oriented attributes.";
        }

        private static string GetButtonLabel(
            Button button)
        {
            if (button == null)
            {
                return string.Empty;
            }

            TextMeshProUGUI label =
                button.GetComponentInChildren<TextMeshProUGUI>(
                    true);

            return label != null &&
                   label.text != null
                ? label.text.Trim()
                : string.Empty;
        }
    }
}
