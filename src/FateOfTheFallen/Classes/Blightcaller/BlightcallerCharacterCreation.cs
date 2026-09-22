using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateOfTheFallen
{
    internal static class BlightcallerCharacterCreation
    {
        private const string ButtonName =
            "CS_BlightcallerClassButton";

        private static readonly HashSet<string> NativeClassLabels =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                "Arcanist",
                "Windblade",
                "Druid",
                "Paladin",
                "Stormcaller",
                "Reaver"
            };

        internal static void InstallButton(
            CharSelectManager manager)
        {
            if (manager == null)
            {
                return;
            }

            BlightcallerCatalog.EnsureClass();

            if (BlightcallerCatalog.BlightcallerClass == null)
            {
                return;
            }

            if (manager.CharCreate == null)
            {
                return;
            }

            Button button =
                manager.CharCreate
                    .GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(
                        candidate =>
                            string.Equals(
                                GetButtonLabel(candidate),
                                "Reaver",
                                StringComparison.OrdinalIgnoreCase));

            if (button == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Could not find the native Reaver class button.");

                return;
            }

            Transform parent =
                button.transform.parent;

            if (parent == null)
            {
                return;
            }

            if (parent.Find(ButtonName) != null)
            {
                return;
            }

            GameObject clone =
                UnityEngine.Object.Instantiate(
                    button.gameObject,
                    parent,
                    false);

            clone.name =
                ButtonName;

            RectTransform rect =
                clone.GetComponent<RectTransform>();

            Button cloneButton =
                clone.GetComponent<Button>();

            if (cloneButton == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller button clone has no Button component.");

                UnityEngine.Object.Destroy(clone);
                return;
            }

            cloneButton.onClick =
                new Button.ButtonClickedEvent();

            cloneButton.onClick.AddListener(
                delegate
                {
                    SelectBlightcaller(manager);
                });

            TextMeshProUGUI label =
                clone.GetComponentInChildren<TextMeshProUGUI>(
                    true);

            if (label != null)
            {
                label.text = "Blightcaller";
                label.enableWordWrapping = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 10f;
                label.fontSizeMax = label.fontSize;
            }

            PositionClassLayout(
                manager,
                parent,
                rect);

            clone.SetActive(true);
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

            if (label == null)
            {
                return string.Empty;
            }

            return label.text == null
                ? string.Empty
                : label.text.Trim();
        }

        private static void PositionClassLayout(
            CharSelectManager manager,
            Transform container,
            RectTransform clone)
        {
            if (clone == null ||
                container == null)
            {
                return;
            }

            List<RectTransform> nativeButtons =
                container
                    .GetComponentsInChildren<Button>(true)
                    .Where(
                        button =>
                            button.transform.parent == container &&
                            NativeClassLabels.Contains(
                                GetButtonLabel(button)))
                    .Select(
                        button =>
                            button.GetComponent<RectTransform>())
                    .Where(
                        rect =>
                            rect != null &&
                            rect != clone)
                    .ToList();

            if (nativeButtons.Count <
                NativeClassLabels.Count)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller class button layout could not identify all native class buttons.");

                return;
            }

            List<float> rowPositions =
                new List<float>();

            foreach (
                float y
                in nativeButtons
                    .Select(rect => rect.anchoredPosition.y)
                    .OrderByDescending(y => y))
            {
                if (rowPositions.All(
                        existing =>
                            Mathf.Abs(existing - y) > 4f))
                {
                    rowPositions.Add(y);
                }
            }

            float spacing;

            if (rowPositions.Count <= 1)
            {
                spacing =
                    clone.rect.height + 2f;
            }
            else
            {
                spacing =
                    rowPositions
                        .Zip(
                            rowPositions.Skip(1),
                            (upper, lower) =>
                                upper - lower)
                        .Average();
            }

            spacing =
                Mathf.Max(
                    clone.rect.height + 1f,
                    spacing);

            float bottomY =
                nativeButtons
                    .Min(
                        rect =>
                            rect.anchoredPosition.y);

            List<RectTransform> bottomRow =
                nativeButtons
                    .Where(
                        rect =>
                            Mathf.Abs(
                                rect.anchoredPosition.y -
                                bottomY) <= 4f)
                    .ToList();

            float centerX =
                bottomRow.Count == 0
                    ? 0f
                    : bottomRow
                        .Average(
                            rect =>
                                rect.anchoredPosition.x);

            clone.anchoredPosition =
                new Vector2(
                    centerX,
                    bottomY - spacing);

            RectTransform containerRect =
                container as RectTransform;

            if (containerRect != null)
            {
                containerRect.sizeDelta +=
                    new Vector2(
                        0f,
                        spacing * 1.75f);
            }

            RectTransform personalizer =
                manager.Personalizer != null
                    ? manager.Personalizer
                        .GetComponent<RectTransform>()
                    : null;

            if (personalizer != null &&
                personalizer.parent == container)
            {
                personalizer.anchoredPosition +=
                    Vector2.down * spacing;

                foreach (
                    RectTransform child
                    in personalizer
                        .Cast<Transform>()
                        .Select(
                            item =>
                                item as RectTransform)
                        .Where(
                            item =>
                                item != null &&
                                (item.name.StartsWith(
                                    "InputField",
                                    StringComparison.Ordinal) ||
                                 item.name.StartsWith(
                                    "EditName",
                                    StringComparison.Ordinal))))
                {
                    child.anchoredPosition +=
                        Vector2.up * spacing;
                }
            }

            foreach (
                RectTransform child
                in container
                    .Cast<Transform>()
                    .Select(
                        item =>
                            item as RectTransform)
                    .Where(
                        item =>
                            item != null &&
                            item != personalizer &&
                            item != clone &&
                            item.anchoredPosition.y < 0f))
            {
                child.anchoredPosition +=
                    Vector2.down * spacing;
            }
        }

        internal static void SelectBlightcaller(
            CharSelectManager manager)
        {
            if (manager == null ||
                GameData.CurrentCharacterSlot == null)
            {
                return;
            }

            try
            {
                AudioSource playerAud =
                    GameData.PlayerAud;

                if (playerAud != null)
                {
                    playerAud.PlayOneShot(
                        GameData.Misc.Click,
                        GameData.UIVolume *
                        GameData.MasterVol);
                }
            }
            catch
            {
            }

            BlightcallerCatalog.EnsureClass();

            if (BlightcallerCatalog.BlightcallerClass == null)
            {
                return;
            }

            SetClassDescription(manager);

            GameData.CurrentCharacterSlot.CharClass =
                "Blightcaller";

            GameData.PlayerStats.CharacterClass =
                BlightcallerCatalog.BlightcallerClass;

            manager.selClass =
                BlightcallerCatalog.BlightcallerClass;

            GameData.PlayerStats.ClearSpentProfPoints();
            GameData.PlayerStats.CalcStats();
        }

        internal static void RestoreSelectedClass(
            CharSelectManager manager)
        {
            if (manager == null ||
                GameData.CurrentCharacterSlot == null ||
                !string.Equals(
                    GameData.CurrentCharacterSlot.CharClass,
                    "Blightcaller",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            BlightcallerCatalog.EnsureClass();

            Class blightcallerClass =
                BlightcallerCatalog.BlightcallerClass;

            if (blightcallerClass == null)
            {
                return;
            }

            GameData.PlayerStats.CharacterClass =
                blightcallerClass;

            manager.PlayerStats.CharacterClass =
                blightcallerClass;

            manager.selClass =
                blightcallerClass;

            if (manager.CharCreate != null &&
                manager.CharCreate.activeInHierarchy &&
                manager.ClassDesc != null)
            {
                SetClassDescription(manager);
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
                        blightcallerClass.DisplayName,
                        "\n",
                        GameData.SceneName);
            }

            try
            {
                manager.PlayerStats.CalcStats();
            }
            catch
            {
            }
        }

        internal static void GrantStartingItemsIfValid(
            CharSelectManager manager)
        {
            if (manager == null ||
                manager.selClass == null ||
                !BlightcallerCatalog.IsBlightcallerClass(
                    manager.selClass) ||
                GameData.CurrentCharacterSlot == null ||
                manager.Name == null)
            {
                return;
            }

            string characterName =
                manager.Name.text;

            if (string.IsNullOrWhiteSpace(
                    characterName) ||
                characterName == "Name Character")
            {
                return;
            }

            try
            {
                BlightcallerCatalog.EnsureClass();

                BlightcallerScrolls.Register();

                BlightcallerScrolls.GiveStartingScrolls();

                Item pants =
                    FindStarterPants();

                Item dagger =
                    FindStarterDagger();

                AddOnce(
                    pants);

                AddOnce(
                    dagger);

                if (pants == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "Blightcaller starter gear: no suitable cloth pants found.");
                }

                if (dagger == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "Blightcaller starter gear: Rusty Dagger was not found.");
                }
            }
            catch (
                Exception ex)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller starter item setup failed: " +
                    ex);
            }
        }

        private static Item FindStarterPants()
        {
            if (GameData.ItemDB == null ||
                GameData.ItemDB.ItemDB == null)
            {
                return null;
            }

            return GameData.ItemDB.ItemDB
                .Where(
                    item =>
                        item != null &&
                        !string.IsNullOrEmpty(
                            item.ItemName) &&
                        item.ItemName.IndexOf(
                            "Pants",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(
                    item =>
                        item.ItemLevel)
                .FirstOrDefault();
        }

        private static Item FindStarterDagger()
        {
            if (GameData.ItemDB == null ||
                GameData.ItemDB.ItemDB == null)
            {
                return null;
            }

            return GameData.ItemDB.ItemDB
                .FirstOrDefault(
                    item =>
                        item != null &&
                        !string.IsNullOrEmpty(
                            item.ItemName) &&
                        string.Equals(
                            item.ItemName.Trim(),
                            "Rusty Dagger",
                            StringComparison.OrdinalIgnoreCase));
        }

        private static void AddOnce(
            Item item)
        {
            if (item == null ||
                GameData.PlayerInv == null)
            {
                return;
            }

            List<ItemIcon> storedSlots =
                GameData.PlayerInv.StoredSlots;

            if (storedSlots == null ||
                !storedSlots.Any(
                    slot =>
                        slot != null &&
                        slot.MyItem == item))
            {
                if (!GameData.PlayerInv.AddItemToInv(
                        item))
                {
                    GameData.PlayerInv.ForceItemToInv(
                        item);
                }
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

            description.enableWordWrapping = true;
            description.enableAutoSizing = true;
            description.fontSizeMin = 14f;
            description.fontSizeMax =
                Mathf.Max(
                    14f,
                    description.fontSize);

            description.overflowMode =
                TextOverflowModes.Overflow;

            description.text =
                "The Blightcaller commands corrupting forces " +
                "through direct damage and lingering disease.\n\n" +
                "Blightcallers weaken their enemies over time, " +
                "spreading decay through the battlefield.\n\n" +
                "A Blightcaller should focus on Intelligence, " +
                "Charisma, and Wisdom.";
        }
    }
}