using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FateOfTheFallen
{
    /// <summary>
    /// Fate of the Fallen character-creation pager.
    ///
    /// The native Erenshor class grid is one chronological class set.
    /// FOTF classes reuse that same footprint as the next set. A single
    /// cycle button to the left advances Native -> FOTF_1 -> FOTF_2...
    /// and wraps back to Native. For the current build only Native and
    /// FOTF_1 exist. Nothing below the class grid is moved or resized.
    /// </summary>
    internal static class FOTFCharacterCreationPager
    {
        private const string RootName =
            "FOTF_CharacterCreationPager";

        private const string BlightcallerButtonName =
            "FOTF_CC_Blightcaller";

        private const string EmberweaverButtonName =
            "FOTF_CC_Emberweaver";

        private const string LightbringerButtonName =
            "FOTF_CC_Lightbringer";

        private const string PageCycleButtonName =
            "FOTF_CC_PageCycle";

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

        internal static void Install(
            CharSelectManager manager)
        {
            if (manager == null ||
                manager.CharCreate == null)
            {
                return;
            }

            BlightcallerCatalog.EnsureClass();
            EmberweaverCatalog.EnsureClass();
            LightbringerCatalog.EnsureClass();

            Button reaverButton =
                manager.CharCreate
                    .GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(
                        button =>
                            string.Equals(
                                GetButtonLabel(button),
                                "Reaver",
                                StringComparison.OrdinalIgnoreCase));

            if (reaverButton == null ||
                reaverButton.transform.parent == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[CharacterCreator] could not locate the native class grid; FOTF paging was not installed.");
                return;
            }

            Transform classContainer =
                reaverButton.transform.parent;

            List<Button> nativeButtons =
                classContainer
                    .GetComponentsInChildren<Button>(true)
                    .Where(
                        button =>
                            button != null &&
                            button.transform.parent == classContainer &&
                            NativeClassLabels.Contains(
                                GetButtonLabel(button)))
                    .Distinct()
                    .ToList();

            if (nativeButtons.Count !=
                NativeClassLabels.Count)
            {
                Plugin.NativeLog.LogWarning(
                    "[CharacterCreator] expected 6 native class buttons but found [" +
                    nativeButtons.Count +
                    "]. FOTF paging was not installed.");
                return;
            }

            FOTFCharacterCreationPagerBehaviour existing =
                manager.CharCreate.GetComponent<
                    FOTFCharacterCreationPagerBehaviour>();

            if (existing != null)
            {
                existing.Rebind(
                    manager,
                    classContainer,
                    nativeButtons);
                return;
            }

            // A Lunaris hot reload can leave the previous r15 overlay
            // object alive even though its behaviour type has unloaded.
            // Remove only our own stale overlay before creating r16.
            Transform stalePagerRoot =
                classContainer.Find(RootName);

            if (stalePagerRoot != null)
            {
                UnityEngine.Object.Destroy(
                    stalePagerRoot.gameObject);
            }

            CleanupLegacyCustomButtons(
                classContainer);

            GameObject root =
                new GameObject(
                    RootName,
                    typeof(RectTransform),
                    typeof(LayoutElement));

            RectTransform rootRect =
                root.GetComponent<RectTransform>();

            rootRect.SetParent(
                classContainer,
                false);

            rootRect.anchorMin =
                Vector2.zero;
            rootRect.anchorMax =
                Vector2.one;
            rootRect.offsetMin =
                Vector2.zero;
            rootRect.offsetMax =
                Vector2.zero;
            rootRect.SetAsLastSibling();

            LayoutElement rootLayout =
                root.GetComponent<LayoutElement>();

            if (rootLayout != null)
            {
                rootLayout.ignoreLayout =
                    true;
            }

            Button template =
                reaverButton;

            Button blightcallerButton =
                CreateClassButton(
                    template,
                    rootRect,
                    BlightcallerButtonName,
                    "Blightcaller",
                    delegate
                    {
                        BlightcallerCharacterCreation
                            .SelectBlightcaller(manager);

                        SelectButtonForNavigation(
                            blightcallerButton: rootRect
                                .Find(BlightcallerButtonName)
                                ?.GetComponent<Button>());
                    });

            Button emberweaverButton =
                CreateClassButton(
                    template,
                    rootRect,
                    EmberweaverButtonName,
                    EmberweaverConstants.DisplayName,
                    delegate
                    {
                        EmberweaverCharacterCreation
                            .SelectEmberweaver(manager);

                        SelectButtonForNavigation(
                            blightcallerButton: rootRect
                                .Find(EmberweaverButtonName)
                                ?.GetComponent<Button>());
                    });

            Button lightbringerButton =
                CreateClassButton(
                    template,
                    rootRect,
                    LightbringerButtonName,
                    LightbringerConstants.DisplayName,
                    delegate
                    {
                        LightbringerCharacterCreation.SelectLightbringer(manager);
                        SelectButtonForNavigation(
                            rootRect.Find(LightbringerButtonName)?.GetComponent<Button>());
                    });

            List<RectTransform> nativeRects =
                nativeButtons
                    .Select(
                        button =>
                            button.GetComponent<RectTransform>())
                    .Where(
                        rect =>
                            rect != null)
                    .ToList();

            PositionFOTFClassButtons(
                nativeRects,
                blightcallerButton,
                emberweaverButton,
                lightbringerButton);

            Button pageCycleButton =
                CreatePageCycleButton(
                    template,
                    rootRect);

            PositionPageCycleButton(
                nativeRects,
                pageCycleButton);

            FOTFCharacterCreationPagerBehaviour behaviour =
                manager.CharCreate.AddComponent<
                    FOTFCharacterCreationPagerBehaviour>();

            behaviour.Initialize(
                manager,
                classContainer,
                nativeButtons,
                blightcallerButton,
                emberweaverButton,
                lightbringerButton,
                pageCycleButton);

            if (pageCycleButton != null)
            {
                pageCycleButton.onClick.AddListener(
                    behaviour.CyclePage);
            }

            behaviour.ResetPageForCurrentSelection();

            Plugin.NativeLog.LogInfo(
                "[CharacterCreator] chronological class-set selector installed. Sets=[Native, FOTF_1]. FOTF_1=[Blightcaller, Emberweaver, Lightbringer].");
        }


        internal static void ShowFOTFPage(
            CharSelectManager manager)
        {
            if (manager == null ||
                manager.CharCreate == null)
            {
                return;
            }

            FOTFCharacterCreationPagerBehaviour behaviour =
                manager.CharCreate.GetComponent<
                    FOTFCharacterCreationPagerBehaviour>();

            if (behaviour != null)
            {
                behaviour.ShowFOTFPage();
            }
        }


        internal static void ShowNativePage(
            CharSelectManager manager)
        {
            if (manager == null ||
                manager.CharCreate == null)
            {
                return;
            }

            FOTFCharacterCreationPagerBehaviour behaviour =
                manager.CharCreate.GetComponent<
                    FOTFCharacterCreationPagerBehaviour>();

            if (behaviour != null)
            {
                behaviour.ShowNativePage();
            }
        }


        private static Button CreateClassButton(
            Button template,
            Transform parent,
            string objectName,
            string labelText,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject clone =
                UnityEngine.Object.Instantiate(
                    template.gameObject,
                    parent,
                    false);

            clone.name =
                objectName;

            Button button =
                clone.GetComponent<Button>();

            if (button == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            button.onClick =
                new Button.ButtonClickedEvent();

            if (onClick != null)
            {
                button.onClick.AddListener(
                    onClick);
            }

            ConfigureButtonLabel(
                button,
                labelText,
                10f);

            clone.SetActive(false);

            return button;
        }


        private static Button CreatePageCycleButton(
            Button template,
            Transform parent)
        {
            GameObject clone =
                UnityEngine.Object.Instantiate(
                    template.gameObject,
                    parent,
                    false);

            clone.name =
                PageCycleButtonName;

            Button button =
                clone.GetComponent<Button>();

            if (button == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            button.onClick =
                new Button.ButtonClickedEvent();

            button.navigation =
                new Navigation
                {
                    mode = Navigation.Mode.None
                };

            ConfigureButtonLabel(
                button,
                "Native",
                8f);

            clone.SetActive(true);

            return button;
        }


        private static void PositionFOTFClassButtons(
            List<RectTransform> nativeRects,
            Button blightcallerButton,
            Button emberweaverButton,
            Button lightbringerButton)
        {
            if (nativeRects == null ||
                nativeRects.Count == 0)
            {
                return;
            }

            RectTransform blightRect =
                blightcallerButton != null
                    ? blightcallerButton.GetComponent<RectTransform>()
                    : null;

            RectTransform emberRect =
                emberweaverButton != null
                    ? emberweaverButton.GetComponent<RectTransform>()
                    : null;

            RectTransform lightbringerRect =
                lightbringerButton != null
                    ? lightbringerButton.GetComponent<RectTransform>()
                    : null;

            List<float> xPositions =
                DistinctAxisValues(
                    nativeRects
                        .Select(rect => rect.anchoredPosition.x)
                        .OrderBy(value => value));

            float topY =
                nativeRects.Max(
                    rect =>
                        rect.anchoredPosition.y);

            if (xPositions.Count >= 2)
            {
                float leftX =
                    xPositions.First();

                float rightX =
                    xPositions.Last();

                if (blightRect != null)
                {
                    CopySlotTransform(
                        FindClosestRect(
                            nativeRects,
                            leftX,
                            topY),
                        blightRect);

                    blightRect.anchoredPosition =
                        new Vector2(
                            leftX,
                            topY);
                }

                if (emberRect != null)
                {
                    CopySlotTransform(
                        FindClosestRect(
                            nativeRects,
                            rightX,
                            topY),
                        emberRect);

                    emberRect.anchoredPosition =
                        new Vector2(
                            rightX,
                            topY);
                }

                if (lightbringerRect != null)
                {
                    float secondRowY = nativeRects
                        .Where(rect => rect.anchoredPosition.y < topY - 4f)
                        .Select(rect => rect.anchoredPosition.y)
                        .DefaultIfEmpty(topY - Mathf.Max(36f, lightbringerRect.rect.height + 4f))
                        .Max();
                    CopySlotTransform(FindClosestRect(nativeRects, leftX, secondRowY), lightbringerRect);
                    lightbringerRect.anchoredPosition = new Vector2(leftX, secondRowY);
                }
                return;
            }

            float centerX =
                nativeRects.Average(
                    rect =>
                        rect.anchoredPosition.x);

            float width =
                nativeRects
                    .Select(rect => rect.rect.width)
                    .Where(value => value > 1f)
                    .DefaultIfEmpty(120f)
                    .Average();

            float gap =
                Mathf.Max(
                    8f,
                    width * 0.08f);

            float offset =
                (width + gap) * 0.5f;

            if (blightRect != null)
            {
                blightRect.anchoredPosition =
                    new Vector2(
                        centerX - offset,
                        topY);
            }

            if (emberRect != null)
            {
                emberRect.anchoredPosition =
                    new Vector2(
                        centerX + offset,
                        topY);
            }
            if (lightbringerRect != null)
            {
                lightbringerRect.anchoredPosition =
                    new Vector2(centerX - offset,
                        topY - Mathf.Max(lightbringerRect.rect.height + 4f, 36f));
            }
        }


        private static void PositionPageCycleButton(
            List<RectTransform> nativeRects,
            Button pageCycleButton)
        {
            if (nativeRects == null ||
                nativeRects.Count == 0 ||
                pageCycleButton == null)
            {
                return;
            }

            float minX =
                nativeRects.Min(
                    rect =>
                        rect.anchoredPosition.x -
                        (rect.rect.width * 0.5f));

            float topY =
                nativeRects.Max(
                    rect =>
                        rect.anchoredPosition.y +
                        (rect.rect.height * 0.5f));

            float bottomY =
                nativeRects.Min(
                    rect =>
                        rect.anchoredPosition.y -
                        (rect.rect.height * 0.5f));

            float averageHeight =
                nativeRects
                    .Select(rect => rect.rect.height)
                    .Where(value => value > 1f)
                    .DefaultIfEmpty(30f)
                    .Average();

            RectTransform rect =
                pageCycleButton.GetComponent<RectTransform>();

            if (rect == null)
            {
                return;
            }

            const float buttonWidth = 54f;
            const float gap = 7f;

            rect.anchorMin =
                rect.anchorMax =
                    new Vector2(0.5f, 0.5f);
            rect.pivot =
                new Vector2(0.5f, 0.5f);
            rect.sizeDelta =
                new Vector2(
                    buttonWidth,
                    Mathf.Clamp(
                        averageHeight,
                        28f,
                        38f));
            rect.anchoredPosition =
                new Vector2(
                    minX - gap - (buttonWidth * 0.5f),
                    (topY + bottomY) * 0.5f);
        }


        private static void CopySlotTransform(
            RectTransform source,
            RectTransform target)
        {
            if (source == null ||
                target == null)
            {
                return;
            }

            target.anchorMin =
                source.anchorMin;
            target.anchorMax =
                source.anchorMax;
            target.pivot =
                source.pivot;
            target.sizeDelta =
                source.sizeDelta;
            target.localScale =
                source.localScale;
            target.localRotation =
                source.localRotation;
        }


        private static RectTransform FindClosestRect(
            IEnumerable<RectTransform> rects,
            float x,
            float y)
        {
            return rects
                .OrderBy(
                    rect =>
                        Mathf.Abs(
                            rect.anchoredPosition.x - x) +
                        Mathf.Abs(
                            rect.anchoredPosition.y - y))
                .FirstOrDefault();
        }


        private static List<float> DistinctAxisValues(
            IEnumerable<float> values)
        {
            List<float> distinct =
                new List<float>();

            foreach (float value in values)
            {
                if (distinct.All(
                        existing =>
                            Mathf.Abs(existing - value) > 4f))
                {
                    distinct.Add(value);
                }
            }

            return distinct;
        }


        private static void ConfigureButtonLabel(
            Button button,
            string text,
            float minimumFontSize)
        {
            if (button == null)
            {
                return;
            }

            TextMeshProUGUI label =
                button.GetComponentInChildren<TextMeshProUGUI>(true);

            if (label == null)
            {
                return;
            }

            label.text =
                text;
            label.enableWordWrapping =
                false;
            label.enableAutoSizing =
                true;
            label.fontSizeMin =
                minimumFontSize;
            label.fontSizeMax =
                Mathf.Max(
                    minimumFontSize,
                    label.fontSize);
            label.alignment =
                TextAlignmentOptions.Center;
        }


        private static string GetButtonLabel(
            Button button)
        {
            if (button == null)
            {
                return string.Empty;
            }

            TextMeshProUGUI label =
                button.GetComponentInChildren<TextMeshProUGUI>(true);

            return label != null &&
                   label.text != null
                ? label.text.Trim()
                : string.Empty;
        }


        private static void CleanupLegacyCustomButtons(
            Transform classContainer)
        {
            if (classContainer == null)
            {
                return;
            }

            string[] legacyNames =
            {
                "CS_BlightcallerClassButton",
                "CS_EmberweaverClassButton"
            };

            for (int i = 0;
                 i < legacyNames.Length;
                 i++)
            {
                Transform legacy =
                    classContainer.Find(
                        legacyNames[i]);

                if (legacy != null)
                {
                    UnityEngine.Object.Destroy(
                        legacy.gameObject);
                }
            }
        }


        private static void SelectButtonForNavigation(
            Button blightcallerButton)
        {
            if (blightcallerButton == null ||
                EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(
                blightcallerButton.gameObject);
        }
    }


    /// <summary>
    /// Small state holder attached only to the native CharCreate root.
    /// It owns visibility for chronological class sets and the left-side
    /// cycle selector.
    /// </summary>
    internal sealed class FOTFCharacterCreationPagerBehaviour :
        MonoBehaviour
    {
        private CharSelectManager _manager;
        private Transform _classContainer;
        private readonly List<Button> _nativeButtons =
            new List<Button>();
        private readonly Dictionary<Button, bool> _nativeInitialState =
            new Dictionary<Button, bool>();

        private Button _blightcallerButton;
        private Button _emberweaverButton;
        private Button _lightbringerButton;
        private Button _pageCycleButton;

        // Chronological set index:
        // 0 = Native, 1 = FOTF_1. Future FOTF pages append here.
        private const int PageCount = 2;
        private int _page;
        private bool _initialized;


        internal void Initialize(
            CharSelectManager manager,
            Transform classContainer,
            IEnumerable<Button> nativeButtons,
            Button blightcallerButton,
            Button emberweaverButton,
            Button lightbringerButton,
            Button pageCycleButton)
        {
            _manager =
                manager;
            _classContainer =
                classContainer;

            _nativeButtons.Clear();
            _nativeInitialState.Clear();

            if (nativeButtons != null)
            {
                foreach (Button button in nativeButtons)
                {
                    if (button == null)
                    {
                        continue;
                    }

                    _nativeButtons.Add(button);
                    _nativeInitialState[button] =
                        button.gameObject.activeSelf;
                }
            }

            _blightcallerButton =
                blightcallerButton;
            _emberweaverButton =
                emberweaverButton;
            _lightbringerButton =
                lightbringerButton;
            _pageCycleButton =
                pageCycleButton;

            _initialized =
                true;
        }


        internal void Rebind(
            CharSelectManager manager,
            Transform classContainer,
            IEnumerable<Button> nativeButtons)
        {
            _manager =
                manager;
            _classContainer =
                classContainer;

            if (_nativeButtons.Count == 0 &&
                nativeButtons != null)
            {
                foreach (Button button in nativeButtons)
                {
                    if (button == null)
                    {
                        continue;
                    }

                    _nativeButtons.Add(button);

                    if (!_nativeInitialState.ContainsKey(button))
                    {
                        _nativeInitialState[button] =
                            true;
                    }
                }
            }

            ApplyPage();
        }


        private void OnEnable()
        {
            if (_initialized)
            {
                ResetPageForCurrentSelection();
            }
        }


        private void OnDestroy()
        {
            RestoreNativeButtons();
        }


        internal void ResetPageForCurrentSelection()
        {
            bool customSelected =
                IsCustomClassSelected();

            _page =
                customSelected && (FOTFContentOptions.Active.NewBlightcallers ||
                    FOTFContentOptions.Active.NewEmberweavers || FOTFContentOptions.Active.NewLightbringers)
                    ? 1
                    : 0;

            ApplyPage();
        }


        internal void CyclePage()
        {
            _page =
                (FOTFContentOptions.Active.NewBlightcallers || FOTFContentOptions.Active.NewEmberweavers ||
                 FOTFContentOptions.Active.NewLightbringers) ? (_page + 1) % PageCount : 0;

            ApplyPage();

            if (_page == 0)
            {
                Button first =
                    _nativeButtons.FirstOrDefault(
                        button =>
                            button != null &&
                            button.gameObject.activeInHierarchy);

                SelectForNavigation(first);
            }
            else
            {
                SelectForNavigation(
                    _blightcallerButton != null &&
                    _blightcallerButton.gameObject.activeInHierarchy
                        ? _blightcallerButton
                        : _emberweaverButton);
            }
        }


        internal void ShowNativePage()
        {
            _page =
                0;

            ApplyPage();

            Button first =
                _nativeButtons.FirstOrDefault(
                    button =>
                        button != null &&
                        button.gameObject.activeInHierarchy);

            SelectForNavigation(
                first);
        }


        internal void ShowFOTFPage()
        {
            _page =
                1;

            ApplyPage();

            SelectForNavigation(
                _blightcallerButton != null &&
                _blightcallerButton.gameObject.activeInHierarchy
                    ? _blightcallerButton
                    : _emberweaverButton);
        }


        private void ApplyPage()
        {
            if (!_initialized)
            {
                return;
            }

            bool nativePage =
                _page == 0;

            for (int i = 0;
                 i < _nativeButtons.Count;
                 i++)
            {
                Button button =
                    _nativeButtons[i];

                if (button == null)
                {
                    continue;
                }

                bool originallyActive;

                if (!_nativeInitialState.TryGetValue(
                        button,
                        out originallyActive))
                {
                    originallyActive =
                        true;
                }

                button.gameObject.SetActive(
                    nativePage &&
                    originallyActive);
            }

            if (_blightcallerButton != null)
            {
                _blightcallerButton.gameObject.SetActive(
                    !nativePage && FOTFContentOptions.Active.NewBlightcallers);
            }

            if (_emberweaverButton != null)
            {
                _emberweaverButton.gameObject.SetActive(
                    !nativePage && FOTFContentOptions.Active.NewEmberweavers);
            }

            if (_lightbringerButton != null)
            {
                _lightbringerButton.gameObject.SetActive(!nativePage && FOTFContentOptions.Active.NewLightbringers);
            }

            if (_pageCycleButton != null)
            {
                _pageCycleButton.gameObject.SetActive(true);
                _pageCycleButton.interactable =
                    PageCount > 1 && (FOTFContentOptions.Active.NewBlightcallers ||
                        FOTFContentOptions.Active.NewEmberweavers ||
                        FOTFContentOptions.Active.NewLightbringers);

                TextMeshProUGUI label =
                    _pageCycleButton.GetComponentInChildren<
                        TextMeshProUGUI>(true);

                if (label != null)
                {
                    label.text =
                        nativePage
                            ? "Native"
                            : "FOTF_1";
                }
            }
        }


        private bool IsCustomClassSelected()
        {
            try
            {
                if (_manager != null &&
                    _manager.selClass != null)
                {
                    if (BlightcallerCatalog.IsBlightcallerClass(
                            _manager.selClass) ||
                        EmberweaverCatalog.IsEmberweaverClass(
                            _manager.selClass) ||
                        LightbringerCatalog.IsLightbringerClass(
                            _manager.selClass))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            SaveGameData slot =
                GameData.CurrentCharacterSlot;

            if (slot == null ||
                string.IsNullOrEmpty(
                    slot.CharClass))
            {
                return false;
            }

            return
                string.Equals(
                    slot.CharClass,
                    "Blightcaller",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    slot.CharClass,
                    EmberweaverConstants.DisplayName,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    slot.CharClass,
                    LightbringerConstants.DisplayName,
                    StringComparison.OrdinalIgnoreCase);
        }


        private void RestoreNativeButtons()
        {
            foreach (
                KeyValuePair<Button, bool> pair
                in _nativeInitialState)
            {
                if (pair.Key != null)
                {
                    pair.Key.gameObject.SetActive(
                        pair.Value);
                }
            }
        }


        private static void SelectForNavigation(
            Button button)
        {
            if (button == null ||
                EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(
                button.gameObject);
        }
    }
}
