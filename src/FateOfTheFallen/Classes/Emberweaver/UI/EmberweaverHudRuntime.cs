using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Compact Emberweaver class HUD for Heat and Flashfire.
    ///
    /// The HUD intentionally uses Unity IMGUI rather than adding another
    /// scene Canvas. That keeps it independent of Erenshor scene UI lifetime
    /// and lets it survive zone changes through one DontDestroyOnLoad host.
    ///
    /// MOVE mode explicitly owns/releases the cursor and uses screen-position
    /// dragging so gameplay mouse-look cannot steal the drag operation.
    /// Position, scale, visibility and lock state persist through PlayerPrefs.
    /// Those values are deliberately isolated behind this class so the future
    /// shared FOTF settings screen can bind to the same controls without
    /// changing the actual HUD implementation.
    /// </summary>
    internal static class EmberweaverHudRuntime
    {
        private const string PrefPrefix =
            "FOTF.Emberweaver.HUD.";

        private const string PrefVisible =
            PrefPrefix + "Visible";

        private const string PrefLocked =
            PrefPrefix + "Locked";

        private const string PrefScale =
            PrefPrefix + "Scale";

        private const string PrefX =
            PrefPrefix + "X";

        private const string PrefY =
            PrefPrefix + "Y";

        internal const float MinimumScale = 0.65f;
        internal const float MaximumScale = 1.60f;

        private static GameObject _host;
        private static EmberweaverHudBehaviour _behaviour;

        // Preferences are hot-path state for the HUD. Keep them in memory
        // instead of calling PlayerPrefs from every IMGUI event.
        private static bool _preferencesLoaded;
        private static bool _visible = true;
        private static bool _locked = true;
        private static float _scale = 1f;

        // Character-select discovery is intentionally throttled. The old HUD
        // could invoke FindObjectOfType<CharSelectManager>() from OnGUI, which
        // means keyboard/mouse IMGUI events could trigger a global search.
        private static CharSelectManager _cachedCharSelect;
        private static float _nextCharSelectLookup;

        internal static void Initialize()
        {
            LoadPreferences();
            EnsureAlive();
        }

        internal static void EnsureAlive()
        {
            if (_behaviour != null)
            {
                return;
            }

            try
            {
                if (_host != null)
                {
                    UnityEngine.Object.Destroy(_host);
                    _host = null;
                }

                _host =
                    new GameObject(
                        "FOTF_Emberweaver_HUD");

                UnityEngine.Object.DontDestroyOnLoad(
                    _host);

                _behaviour =
                    _host.AddComponent<EmberweaverHudBehaviour>();
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Emberweaver: failed to create Heat/Flashfire HUD",
                    exception);
            }
        }

        internal static void Shutdown()
        {
            if (_host != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(_host);
                }
                catch
                {
                }
            }

            _behaviour = null;
            _host = null;
        }

        private static void LoadPreferences()
        {
            if (_preferencesLoaded)
            {
                return;
            }

            _visible =
                PlayerPrefs.GetInt(
                    PrefVisible,
                    1) != 0;

            _locked =
                PlayerPrefs.GetInt(
                    PrefLocked,
                    1) != 0;

            _scale =
                Mathf.Clamp(
                    PlayerPrefs.GetFloat(
                        PrefScale,
                        1f),
                    MinimumScale,
                    MaximumScale);

            _preferencesLoaded =
                true;
        }


        internal static bool IsVisible
        {
            get
            {
                LoadPreferences();
                return _visible;
            }
        }

        internal static bool IsLocked
        {
            get
            {
                LoadPreferences();
                return _locked;
            }
        }

        internal static float Scale
        {
            get
            {
                LoadPreferences();
                return _scale;
            }
        }

        internal static void SetVisible(
            bool visible)
        {
            LoadPreferences();

            _visible =
                visible;

            PlayerPrefs.SetInt(
                PrefVisible,
                visible ? 1 : 0);

            PlayerPrefs.Save();

            if (_behaviour != null)
            {
                _behaviour.OnVisibilityPreferenceChanged(
                    visible);
            }
        }

        internal static void SetLocked(
            bool locked)
        {
            LoadPreferences();

            _locked =
                locked;

            PlayerPrefs.SetInt(
                PrefLocked,
                locked ? 1 : 0);

            PlayerPrefs.Save();

            if (_behaviour != null)
            {
                _behaviour.OnLockPreferenceChanged(
                    locked);
            }
        }

        internal static void ToggleLocked()
        {
            SetLocked(
                !IsLocked);
        }

        internal static void SetScale(
            float scale)
        {
            LoadPreferences();

            float clamped =
                Mathf.Clamp(
                    scale,
                    MinimumScale,
                    MaximumScale);

            _scale =
                clamped;

            PlayerPrefs.SetFloat(
                PrefScale,
                clamped);

            PlayerPrefs.Save();

            if (_behaviour != null)
            {
                _behaviour.RefreshFromPreferences(
                    false);
            }
        }

        internal static void ResetPosition()
        {
            PlayerPrefs.DeleteKey(
                PrefX);

            PlayerPrefs.DeleteKey(
                PrefY);

            PlayerPrefs.Save();

            if (_behaviour != null)
            {
                _behaviour.RefreshFromPreferences(
                    true);
            }
        }


        internal static bool IsInPlayableWorld()
        {
            try
            {
                if (GetGameDataBool("InCharSelect") ||
                    GetGameDataBool("Zoning") ||
                    GameData.PlayerControl == null ||
                    GameData.PlayerStats == null ||
                    GameData.PlayerCombat == null ||
                    GameData.PlayerControl.Myself == null ||
                    GameData.PlayerStats.Myself == null)
                {
                    return false;
                }

                Character player =
                    GameData.PlayerControl.Myself;

                if (player.isNPC ||
                    GameData.PlayerStats.Myself != player ||
                    !GameData.PlayerControl.gameObject.activeInHierarchy ||
                    !player.gameObject.activeInHierarchy)
                {
                    return false;
                }

                // Character-select preview objects can leave apparently valid
                // PlayerStats/PlayerControl references behind. The same active
                // CharSelectManager exclusion already proved reliable for the
                // Combat Meter's world-only UI gate.
                float now =
                    Time.unscaledTime;

                if (_cachedCharSelect == null &&
                    now >= _nextCharSelectLookup)
                {
                    _nextCharSelectLookup =
                        now + 1f;

                    _cachedCharSelect =
                        UnityEngine.Object
                            .FindObjectOfType<CharSelectManager>();
                }

                if (_cachedCharSelect != null &&
                    _cachedCharSelect.gameObject != null &&
                    _cachedCharSelect.gameObject.activeInHierarchy)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool GetGameDataBool(
            string memberName)
        {
            try
            {
                BindingFlags flags =
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic;

                FieldInfo field =
                    typeof(GameData).GetField(
                        memberName,
                        flags);

                if (field != null &&
                    field.FieldType == typeof(bool))
                {
                    return (bool)field.GetValue(null);
                }

                PropertyInfo property =
                    typeof(GameData).GetProperty(
                        memberName,
                        flags);

                if (property != null &&
                    property.PropertyType == typeof(bool) &&
                    property.CanRead)
                {
                    return (bool)property.GetValue(
                        null,
                        null);
                }
            }
            catch
            {
            }

            return false;
        }

        internal static string BuildStatusText()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Emberweaver HUD: {0}, {1}, scale {2:0.00}. " +
                "Commands: /ewhud show|hide|lock|unlock|reset|scale <0.65-1.60>",
                IsVisible
                    ? "shown"
                    : "hidden",
                IsLocked
                    ? "locked"
                    : "movable",
                Scale);
        }

        internal static bool TryHandleCommand(
            string[] parts,
            out string message)
        {
            message = null;

            if (parts == null ||
                parts.Length <= 1 ||
                string.Equals(
                    parts[1],
                    "status",
                    StringComparison.OrdinalIgnoreCase))
            {
                message =
                    BuildStatusText();

                return true;
            }

            string operation =
                parts[1];

            if (string.Equals(
                    operation,
                    "show",
                    StringComparison.OrdinalIgnoreCase))
            {
                SetVisible(true);
                EnsureAlive();
                message = "Emberweaver HUD shown.";
                return true;
            }

            if (string.Equals(
                    operation,
                    "hide",
                    StringComparison.OrdinalIgnoreCase))
            {
                SetVisible(false);
                message = "Emberweaver HUD hidden. Use /ewhud show to restore it.";
                return true;
            }

            if (string.Equals(
                    operation,
                    "lock",
                    StringComparison.OrdinalIgnoreCase))
            {
                SetLocked(true);
                message = "Emberweaver HUD locked.";
                return true;
            }

            if (string.Equals(
                    operation,
                    "unlock",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    operation,
                    "move",
                    StringComparison.OrdinalIgnoreCase))
            {
                SetLocked(false);
                message = "Emberweaver HUD unlocked. Drag it by the header.";
                return true;
            }

            if (string.Equals(
                    operation,
                    "reset",
                    StringComparison.OrdinalIgnoreCase))
            {
                ResetPosition();
                message = "Emberweaver HUD position reset.";
                return true;
            }

            if (string.Equals(
                    operation,
                    "scale",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length < 3)
                {
                    message =
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Current Emberweaver HUD scale: {0:0.00}. Usage: /ewhud scale <0.65-1.60>",
                            Scale);

                    return true;
                }

                float requested;

                if (!float.TryParse(
                        parts[2],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out requested))
                {
                    message =
                        "Invalid HUD scale. Usage: /ewhud scale <0.65-1.60>";

                    return true;
                }

                SetScale(requested);

                message =
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Emberweaver HUD scale set to {0:0.00}.",
                        Scale);

                return true;
            }

            message =
                "Usage: /ewhud status | show | hide | lock | unlock | reset | scale <0.65-1.60>";

            return true;
        }

        internal static void SavePosition(
            float x,
            float y)
        {
            PlayerPrefs.SetFloat(
                PrefX,
                x);

            PlayerPrefs.SetFloat(
                PrefY,
                y);
        }

        internal static bool TryLoadPosition(
            out float x,
            out float y)
        {
            if (!PlayerPrefs.HasKey(PrefX) ||
                !PlayerPrefs.HasKey(PrefY))
            {
                x = 0f;
                y = 0f;
                return false;
            }

            x =
                PlayerPrefs.GetFloat(
                    PrefX,
                    0f);

            y =
                PlayerPrefs.GetFloat(
                    PrefY,
                    0f);

            return true;
        }
    }

    [DefaultExecutionOrder(32000)]
    internal sealed class EmberweaverHudBehaviour : MonoBehaviour
    {
        private const int WindowId =
            0x45574855;

        private const float BaseWidth =
            334f;

        private const float BaseLockedHeight =
            112f;

        private const float BaseUnlockedHeight =
            134f;

        private Rect _windowRect;
        private bool _positionLoaded;
        private bool _positionDirty;
        private bool _dragging;
        private Vector2 _lastDragMouse;
        private float _lastScale = -1f;

        // Sample game state outside OnGUI. OnGUI can execute multiple times per
        // rendered frame, including for keyboard events generated by movement.
        private const float EligibilityRefreshInterval = 0.25f;
        private const float DisplayRefreshInterval = 0.10f;

        private float _nextEligibilityRefresh;
        private float _nextDisplayRefresh;
        private bool _canRender;

        // ArcaneUI uses a top-sorted uGUI modal. This HUD is Unity IMGUI,
        // which can render above that Canvas irrespective of sortingOrder.
        // Yield drawing AND edit-cursor ownership while the modal is open.
        // A cached scene-object reference avoids global searches in OnGUI.
        private GameObject _arcaneUiSettingsLayer;
        private float _nextArcaneUiSettingsLookup;
        private bool _arcaneUiSettingsOpen;

        private bool _cachedHasHeat;
        private bool _cachedHasFlashfire;
        private bool _cachedHasWildfire;
        private int _cachedHeat;
        private bool _cachedFlashfireReady;
        private string _cachedHeatLabel = "HEAT  —  UNLEARNED";
        private string _cachedDecayText = string.Empty;
        private bool _cachedShowDecay;
        private string _cachedWildfireStoredText = "WILDFIRE  —  UNLEARNED";
        private string _cachedWildfireCombustText = string.Empty;
        private bool _cachedWildfireHasDetail;

        private GUIStyle _headerStyle;
        private GUIStyle _heatStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _flashfireStyle;
        private GUIStyle _wildfireStyle;
        private GUIStyle _wildfireDetailStyle;
        private GUIStyle _buttonStyle;

        internal void RefreshFromPreferences(
            bool forceDefaultPosition)
        {
            _lastScale = -1f;
            _nextEligibilityRefresh = 0f;
            _nextDisplayRefresh = 0f;

            if (forceDefaultPosition)
            {
                _positionLoaded = false;
            }

            ClampWindowToScreen();
        }

        internal void OnLockPreferenceChanged(
            bool locked)
        {
            if (locked)
            {
                FinishDrag(true);
                return;
            }

            MaintainEditCursor();
        }

        internal void OnVisibilityPreferenceChanged(
            bool visible)
        {
            if (!visible)
            {
                _canRender = false;
                FinishDrag(true);
                return;
            }

            _nextEligibilityRefresh = 0f;
            _nextDisplayRefresh = 0f;
        }

        private void Awake()
        {
            hideFlags =
                HideFlags.HideAndDontSave;
        }

        private void Update()
        {
            RefreshArcaneUiSettingsVisibility();
            RefreshCachedHudState();

            bool editMode =
                ShouldOwnEditCursor();

            if (!editMode)
            {
                if (_dragging)
                {
                    FinishDrag(true);
                }

                return;
            }

            MaintainEditCursor();

            if (Input.GetKeyDown(
                    KeyCode.Escape))
            {
                EmberweaverHudRuntime.SetLocked(true);
                return;
            }

            UpdateManualDrag();
        }

        // The ArcaneUI configuration layer is named by ArcaneCanvasManager.
        // Find it at most four times per second when not yet cached. Once
        // discovered, activeInHierarchy changes immediately on open/close.
        private void RefreshArcaneUiSettingsVisibility()
        {
            if (_arcaneUiSettingsLayer == null &&
                Time.unscaledTime >= _nextArcaneUiSettingsLookup)
            {
                _nextArcaneUiSettingsLookup = Time.unscaledTime + 0.25f;
                _arcaneUiSettingsLayer = GameObject.Find("ArcaneUI_Configuration");
            }

            _arcaneUiSettingsOpen =
                _arcaneUiSettingsLayer != null &&
                _arcaneUiSettingsLayer.activeInHierarchy;

            if (_arcaneUiSettingsOpen && _dragging)
                FinishDrag(true);
        }

        private void RefreshCachedHudState()
        {
            float now =
                Time.unscaledTime;

            if (now >= _nextEligibilityRefresh)
            {
                _nextEligibilityRefresh =
                    now + EligibilityRefreshInterval;

                _canRender =
                    EmberweaverHudRuntime.IsVisible &&
                    EmberweaverHudRuntime.IsInPlayableWorld() &&
                    EmberweaverCatalog.IsCurrentPlayerEmberweaver();

                if (!_canRender)
                {
                    ClearCachedDisplayState();
                    return;
                }
            }

            if (!_canRender ||
                now < _nextDisplayRefresh)
            {
                return;
            }

            _nextDisplayRefresh =
                now + DisplayRefreshInterval;

            _cachedHasHeat =
                EmberweaverSkillCatalog.HasPlayerSkill(
                    EmberweaverSkillCatalog.HeatId);

            _cachedHasFlashfire =
                EmberweaverSkillCatalog.HasPlayerSkill(
                    EmberweaverSkillCatalog.FlashfireId);

            _cachedHasWildfire =
                EmberweaverSkillCatalog.HasPlayerSkill(
                    EmberweaverSkillCatalog.WildfireId);

            _cachedHeat =
                _cachedHasHeat
                    ? EmberweaverPassiveRuntime.HeatStacks
                    : 0;

            _cachedFlashfireReady =
                _cachedHasFlashfire &&
                EmberweaverPassiveRuntime.FlashfireReady;

            if (_cachedHasHeat)
            {
                _cachedHeatLabel =
                    "HEAT  " +
                    _cachedHeat +
                    "/" +
                    EmberweaverPassiveRuntime.MaxHeat +
                    "    +" +
                    Mathf.RoundToInt(
                        _cachedHeat *
                        EmberweaverPassiveRuntime.CurrentDamagePerHeat *
                        100f) +
                    "% DMG";

                _cachedShowDecay =
                    _cachedHeat > 0;

                if (_cachedShowDecay)
                {
                    float decay =
                        EmberweaverPassiveRuntime.SecondsUntilNextHeatDecay;

                    _cachedDecayText =
                        _cachedHeat >= EmberweaverPassiveRuntime.MaxHeat
                            ? "MAX HEAT  •  DECAY " +
                              decay.ToString(
                                  "0.0",
                                  CultureInfo.InvariantCulture) +
                              "s"
                            : "NEXT DECAY " +
                              decay.ToString(
                                  "0.0",
                                  CultureInfo.InvariantCulture) +
                              "s";
                }
                else
                {
                    _cachedDecayText =
                        string.Empty;
                }
            }
            else
            {
                _cachedHeatLabel =
                    "HEAT  —  UNLEARNED";

                _cachedShowDecay =
                    false;

                _cachedDecayText =
                    string.Empty;
            }

            RefreshCachedWildfireState();
        }

        private void RefreshCachedWildfireState()
        {
            if (!_cachedHasWildfire)
            {
                _cachedWildfireStoredText =
                    "WILDFIRE  —  UNLEARNED";

                _cachedWildfireCombustText =
                    string.Empty;

                _cachedWildfireHasDetail =
                    false;

                return;
            }

            float stored;
            float combustFraction;
            int projectedCombust;

            bool hasBank =
                EmberweaverPassiveRuntime.TryGetCurrentTargetWildfire(
                    out stored,
                    out combustFraction,
                    out projectedCombust);

            _cachedWildfireStoredText =
                "WILDFIRE  " +
                FormatDamageValue(
                    hasBank
                        ? stored
                        : 0f) +
                " STORED";

            _cachedWildfireHasDetail =
                true;

            if (!hasBank)
            {
                _cachedWildfireCombustText =
                    "CURRENT TARGET • NO ACTIVE WILDFIRE";
            }
            else if (_cachedHeat < 5)
            {
                _cachedWildfireCombustText =
                    "COMBUST • NEED 5 HEAT";
            }
            else
            {
                _cachedWildfireCombustText =
                    "COMBUST " +
                    Mathf.RoundToInt(
                        combustFraction *
                        100f) +
                    "%  →  " +
                    FormatDamageValue(
                        projectedCombust);
            }
        }

        private void ClearCachedDisplayState()
        {
            _cachedHasHeat = false;
            _cachedHasFlashfire = false;
            _cachedHasWildfire = false;
            _cachedHeat = 0;
            _cachedFlashfireReady = false;
            _cachedHeatLabel = "HEAT  —  UNLEARNED";
            _cachedDecayText = string.Empty;
            _cachedShowDecay = false;
            _cachedWildfireStoredText = "WILDFIRE  —  UNLEARNED";
            _cachedWildfireCombustText = string.Empty;
            _cachedWildfireHasDetail = false;
        }


        private void LateUpdate()
        {
            if (ShouldOwnEditCursor())
            {
                // Erenshor normally owns the gameplay cursor. Reassert this
                // after ordinary gameplay updates so MOVE mode cannot be
                // pulled back into mouse-look while the HUD is being placed.
                MaintainEditCursor();
            }
        }

        private void OnApplicationFocus(
            bool hasFocus)
        {
            if (hasFocus &&
                ShouldOwnEditCursor())
            {
                MaintainEditCursor();
            }
        }

        private void OnGUI()
        {
            Event current =
                Event.current;

            if (current == null)
            {
                return;
            }

            EventType eventType =
                current.type;

            // This is the critical movement-FPS fix.
            //
            // The old HUD ran its full GUI path for every IMGUI event.
            // Holding movement keys generates keyboard events, so walking could
            // cause additional complete HUD passes containing world checks,
            // skill scans, string creation and Wildfire queries.
            //
            // All controls in this HUD use explicit Rects, so no Layout pass is
            // required. Repaint plus mouse events are sufficient.
            if (eventType != EventType.Repaint &&
                eventType != EventType.MouseDown &&
                eventType != EventType.MouseUp &&
                eventType != EventType.MouseDrag)
            {
                return;
            }

            // An IMGUI Window ignores the sorting priority of uGUI canvases.
            // Don't render or process pointer events through ArcaneUI's modal.
            if (!_canRender || _arcaneUiSettingsOpen)
                return;

            bool locked =
                EmberweaverHudRuntime.IsLocked;

            if (!locked)
            {
                // OnGUI can run after the game has reacted to a mouse event.
                MaintainEditCursor();
            }

            float scale =
                EmberweaverHudRuntime.Scale;

            EnsureStyles(
                scale);

            EnsurePosition(
                scale,
                locked);

            Rect before =
                _windowRect;

            _windowRect.width =
                BaseWidth * scale;

            _windowRect.height =
                (locked
                    ? BaseLockedHeight
                    : BaseUnlockedHeight) * scale;

            ClampWindowToScreen();

            _windowRect =
                GUI.Window(
                    WindowId,
                    _windowRect,
                    DrawWindow,
                    string.Empty,
                    GUIStyle.none);

            ClampWindowToScreen();

            if (Mathf.Abs(
                    before.x - _windowRect.x) > 0.01f ||
                Mathf.Abs(
                    before.y - _windowRect.y) > 0.01f)
            {
                EmberweaverHudRuntime.SavePosition(
                    _windowRect.x,
                    _windowRect.y);

                _positionDirty = true;
            }
        }


        private void DrawWindow(
            int windowId)
        {
            float scale =
                EmberweaverHudRuntime.Scale;

            bool locked =
                EmberweaverHudRuntime.IsLocked;

            float width =
                _windowRect.width;

            float height =
                _windowRect.height;

            DrawSolidRect(
                new Rect(
                    0f,
                    0f,
                    width,
                    height),
                new Color(
                    0.055f,
                    0.035f,
                    0.028f,
                    0.94f));

            float border =
                1.5f * scale;

            Color borderColor =
                new Color(
                    0.83f,
                    0.30f,
                    0.10f,
                    0.95f);

            DrawBorder(
                new Rect(
                    0f,
                    0f,
                    width,
                    height),
                border,
                borderColor);

            float padding =
                9f * scale;

            float headerHeight =
                21f * scale;

            GUI.Label(
                new Rect(
                    padding,
                    3f * scale,
                    180f * scale,
                    18f * scale),
                "EMBERWEAVER",
                _headerStyle);

            string lockText =
                locked
                    ? "MOVE"
                    : "LOCK";

            if (GUI.Button(
                    new Rect(
                        width - (53f * scale),
                        3f * scale,
                        45f * scale,
                        16f * scale),
                    lockText,
                    _buttonStyle))
            {
                EmberweaverHudRuntime.ToggleLocked();
            }

            DrawSolidRect(
                new Rect(
                    padding,
                    headerHeight,
                    width - (padding * 2f),
                    1f * scale),
                new Color(
                    0.46f,
                    0.18f,
                    0.08f,
                    0.92f));

            int heat =
                _cachedHeat;

            float leftWidth =
                222f * scale;

            GUI.Label(
                new Rect(
                    padding,
                    26f * scale,
                    leftWidth,
                    17f * scale),
                _cachedHeatLabel,
                _heatStyle);

            DrawHeatSegments(
                new Rect(
                    padding,
                    46f * scale,
                    leftWidth,
                    15f * scale),
                heat,
                _cachedHasHeat,
                scale);

            if (_cachedShowDecay)
            {
                GUI.Label(
                    new Rect(
                        padding,
                        62f * scale,
                        leftWidth,
                        14f * scale),
                    _cachedDecayText,
                    _smallStyle);
            }

            DrawFlashfire(
                new Rect(
                    240f * scale,
                    27f * scale,
                    84f * scale,
                    48f * scale),
                _cachedHasFlashfire,
                _cachedFlashfireReady,
                scale);

            DrawWildfireReadout(
                new Rect(
                    padding,
                    80f * scale,
                    width - (padding * 2f),
                    27f * scale),
                scale);

            if (!locked)
            {
                DrawSolidRect(
                    new Rect(
                        padding,
                        111f * scale,
                        width - (padding * 2f),
                        1f * scale),
                    new Color(
                        0.46f,
                        0.18f,
                        0.08f,
                        0.92f));

                GUI.Label(
                    new Rect(
                        padding,
                        115f * scale,
                        width - (padding * 2f),
                        15f * scale),
                    "MOVE MODE • drag header • Esc locks • /ewhud reset",
                    _smallStyle);

                HandleHeaderMouseDown(
                    new Rect(
                        0f,
                        0f,
                        width - (60f * scale),
                        headerHeight));
            }
        }

        private void DrawWildfireReadout(
            Rect area,
            float scale)
        {
            DrawSolidRect(
                new Rect(
                    area.x,
                    area.y - (2f * scale),
                    area.width,
                    1f * scale),
                new Color(
                    0.46f,
                    0.18f,
                    0.08f,
                    0.92f));

            GUI.Label(
                new Rect(
                    area.x,
                    area.y,
                    area.width,
                    14f * scale),
                _cachedWildfireStoredText,
                _wildfireStyle);

            if (!_cachedWildfireHasDetail)
            {
                return;
            }

            GUI.Label(
                new Rect(
                    area.x,
                    area.y + (13f * scale),
                    area.width,
                    13f * scale),
                _cachedWildfireCombustText,
                _wildfireDetailStyle);
        }


        private static string FormatDamageValue(
            float value)
        {
            return Mathf.Max(
                    0,
                    Mathf.RoundToInt(value))
                .ToString(
                    "N0",
                    CultureInfo.InvariantCulture);
        }

        private void HandleHeaderMouseDown(
            Rect localHeaderRect)
        {
            Event current =
                Event.current;

            if (current == null ||
                current.type != EventType.MouseDown ||
                current.button != 0 ||
                !localHeaderRect.Contains(
                    current.mousePosition))
            {
                return;
            }

            MaintainEditCursor();

            Vector2 mouse =
                GetGuiMousePosition();

            if (!IsFinite(mouse.x) ||
                !IsFinite(mouse.y))
            {
                return;
            }

            _lastDragMouse = mouse;

            _dragging = true;
            _positionDirty = true;

            // Prevent other IMGUI controls from treating this header press as
            // their own click. Gameplay input is handled separately by cursor
            // ownership throughout MOVE mode.
            current.Use();
        }

        private void UpdateManualDrag()
        {
            if (!_dragging)
            {
                return;
            }

            if (!Input.GetMouseButton(0))
            {
                FinishDrag(true);
                return;
            }

            Vector2 mouse =
                GetGuiMousePosition();

            if (!IsFinite(mouse.x) ||
                !IsFinite(mouse.y))
            {
                return;
            }

            Vector2 delta =
                mouse - _lastDragMouse;

            if (!IsFinite(delta.x) ||
                !IsFinite(delta.y))
            {
                return;
            }

            // Cursor recapture can warp Input.mousePosition to a corner or
            // screen centre for one frame. Never translate that warp into HUD
            // movement; this is what previously threw the panel to top-left.
            float warpGuard =
                Mathf.Max(
                    180f,
                    Mathf.Min(
                        Screen.width,
                        Screen.height) * 0.35f);

            if (delta.sqrMagnitude >
                warpGuard * warpGuard)
            {
                MaintainEditCursor();
                return;
            }

            _lastDragMouse = mouse;

            _windowRect.x += delta.x;
            _windowRect.y += delta.y;
            ClampWindowToScreen();

            EmberweaverHudRuntime.SavePosition(
                _windowRect.x,
                _windowRect.y);

            _positionDirty = true;
        }

        private void FinishDrag(
            bool save)
        {
            if (!_dragging &&
                !_positionDirty)
            {
                return;
            }

            _dragging = false;

            if (save &&
                _positionLoaded)
            {
                ClampWindowToScreen();

                EmberweaverHudRuntime.SavePosition(
                    _windowRect.x,
                    _windowRect.y);

                PlayerPrefs.Save();
            }

            _positionDirty = false;
        }

        private bool ShouldOwnEditCursor()
        {
            return _canRender &&
                   !_arcaneUiSettingsOpen &&
                   !EmberweaverHudRuntime.IsLocked;
        }

        private void MaintainEditCursor()
        {

            if (Cursor.lockState !=
                CursorLockMode.None)
            {
                Cursor.lockState =
                    CursorLockMode.None;
            }

            if (!Cursor.visible)
            {
                Cursor.visible = true;
            }
        }

        private static Vector2 GetGuiMousePosition()
        {
            Vector3 input =
                Input.mousePosition;

            return new Vector2(
                input.x,
                Screen.height - input.y);
        }

        private static bool IsFinite(
            float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }

        private void DrawHeatSegments(
            Rect area,
            int heat,
            bool learned,
            float scale)
        {
            const int count = 10;

            float gap =
                2f * scale;

            float segmentWidth =
                (area.width -
                 (gap * (count - 1))) /
                count;

            for (int i = 0;
                 i < count;
                 i++)
            {
                Rect segment =
                    new Rect(
                        area.x +
                        (i * (segmentWidth + gap)),
                        area.y,
                        segmentWidth,
                        area.height);

                bool filled =
                    learned &&
                    i < heat;

                Color fill;

                if (!learned)
                {
                    fill =
                        new Color(
                            0.14f,
                            0.12f,
                            0.11f,
                            0.90f);
                }
                else if (!filled)
                {
                    fill =
                        new Color(
                            0.18f,
                            0.09f,
                            0.055f,
                            0.96f);
                }
                else
                {
                    float t =
                        i /
                        9f;

                    fill =
                        Color.Lerp(
                            new Color(
                                0.94f,
                                0.30f,
                                0.06f,
                                1f),
                            new Color(
                                1f,
                                0.72f,
                                0.10f,
                                1f),
                            t);
                }

                DrawSolidRect(
                    segment,
                    fill);

                DrawBorder(
                    segment,
                    Mathf.Max(
                        1f,
                        scale),
                    new Color(
                        0.52f,
                        0.22f,
                        0.08f,
                        0.92f));
            }
        }

        private void DrawFlashfire(
            Rect area,
            bool learned,
            bool ready,
            float scale)
        {
            Color fill;
            Color border;
            string text;

            if (!learned)
            {
                fill =
                    new Color(
                        0.12f,
                        0.11f,
                        0.10f,
                        0.92f);

                border =
                    new Color(
                        0.30f,
                        0.28f,
                        0.25f,
                        0.92f);

                text =
                    "FLASHFIRE\nUNLEARNED";
            }
            else if (!ready)
            {
                fill =
                    new Color(
                        0.20f,
                        0.075f,
                        0.04f,
                        0.94f);

                border =
                    new Color(
                        0.55f,
                        0.20f,
                        0.07f,
                        0.94f);

                text =
                    "FLASHFIRE\nIDLE";
            }
            else
            {
                float pulse =
                    0.5f +
                    (0.5f *
                     Mathf.Sin(
                         Time.unscaledTime *
                         5f));

                fill =
                    Color.Lerp(
                        new Color(
                            0.78f,
                            0.21f,
                            0.02f,
                            0.98f),
                        new Color(
                            1f,
                            0.60f,
                            0.08f,
                            1f),
                        pulse);

                border =
                    Color.Lerp(
                        new Color(
                            1f,
                            0.54f,
                            0.10f,
                            1f),
                        new Color(
                            1f,
                            0.88f,
                            0.35f,
                            1f),
                        pulse);

                text =
                    "FLASHFIRE\nREADY!";
            }

            DrawSolidRect(
                area,
                fill);

            DrawBorder(
                area,
                1.5f * scale,
                border);

            GUI.Label(
                area,
                text,
                _flashfireStyle);
        }

        private void EnsurePosition(
            float scale,
            bool locked)
        {
            if (_positionLoaded)
            {
                return;
            }

            float width =
                BaseWidth * scale;

            float height =
                (locked
                    ? BaseLockedHeight
                    : BaseUnlockedHeight) * scale;

            float x;
            float y;

            if (!EmberweaverHudRuntime.TryLoadPosition(
                    out x,
                    out y))
            {
                x =
                    Mathf.Max(
                        10f,
                        (Screen.width - width) * 0.5f);

                y =
                    Mathf.Max(
                        10f,
                        Screen.height - height - 145f);
            }

            _windowRect =
                new Rect(
                    x,
                    y,
                    width,
                    height);

            _positionLoaded = true;
            ClampWindowToScreen();
        }

        private void ClampWindowToScreen()
        {
            if (!_positionLoaded)
            {
                return;
            }

            float maxX =
                Mathf.Max(
                    0f,
                    Screen.width - _windowRect.width);

            float maxY =
                Mathf.Max(
                    0f,
                    Screen.height - _windowRect.height);

            _windowRect.x =
                Mathf.Clamp(
                    _windowRect.x,
                    0f,
                    maxX);

            _windowRect.y =
                Mathf.Clamp(
                    _windowRect.y,
                    0f,
                    maxY);
        }

        private void EnsureStyles(
            float scale)
        {
            if (_headerStyle != null &&
                Mathf.Abs(
                    _lastScale - scale) < 0.001f)
            {
                return;
            }

            _lastScale =
                scale;

            _headerStyle =
                new GUIStyle(
                    GUI.skin.label);

            _headerStyle.fontSize =
                Mathf.Max(
                    8,
                    Mathf.RoundToInt(
                        12f * scale));

            _headerStyle.fontStyle =
                FontStyle.Bold;

            _headerStyle.alignment =
                TextAnchor.MiddleLeft;

            _headerStyle.normal.textColor =
                new Color(
                    1f,
                    0.70f,
                    0.28f,
                    1f);

            _heatStyle =
                new GUIStyle(
                    GUI.skin.label);

            _heatStyle.fontSize =
                Mathf.Max(
                    8,
                    Mathf.RoundToInt(
                        11f * scale));

            _heatStyle.fontStyle =
                FontStyle.Bold;

            _heatStyle.alignment =
                TextAnchor.MiddleLeft;

            _heatStyle.normal.textColor =
                Color.white;

            _smallStyle =
                new GUIStyle(
                    GUI.skin.label);

            _smallStyle.fontSize =
                Mathf.Max(
                    7,
                    Mathf.RoundToInt(
                        9f * scale));

            _smallStyle.alignment =
                TextAnchor.MiddleLeft;

            _smallStyle.normal.textColor =
                new Color(
                    0.78f,
                    0.69f,
                    0.62f,
                    1f);

            _flashfireStyle =
                new GUIStyle(
                    GUI.skin.label);

            _flashfireStyle.fontSize =
                Mathf.Max(
                    7,
                    Mathf.RoundToInt(
                        9.5f * scale));

            _flashfireStyle.fontStyle =
                FontStyle.Bold;

            _flashfireStyle.alignment =
                TextAnchor.MiddleCenter;

            _flashfireStyle.wordWrap =
                true;

            _flashfireStyle.normal.textColor =
                Color.white;

            _wildfireStyle =
                new GUIStyle(
                    GUI.skin.label);

            _wildfireStyle.fontSize =
                Mathf.Max(
                    8,
                    Mathf.RoundToInt(
                        10f * scale));

            _wildfireStyle.fontStyle =
                FontStyle.Bold;

            _wildfireStyle.alignment =
                TextAnchor.MiddleLeft;

            _wildfireStyle.normal.textColor =
                new Color(
                    1f,
                    0.55f,
                    0.14f,
                    1f);

            _wildfireDetailStyle =
                new GUIStyle(
                    GUI.skin.label);

            _wildfireDetailStyle.fontSize =
                Mathf.Max(
                    7,
                    Mathf.RoundToInt(
                        8.5f * scale));

            _wildfireDetailStyle.fontStyle =
                FontStyle.Bold;

            _wildfireDetailStyle.alignment =
                TextAnchor.MiddleLeft;

            _wildfireDetailStyle.normal.textColor =
                new Color(
                    0.88f,
                    0.76f,
                    0.62f,
                    1f);

            _buttonStyle =
                new GUIStyle(
                    GUI.skin.button);

            _buttonStyle.fontSize =
                Mathf.Max(
                    7,
                    Mathf.RoundToInt(
                        8f * scale));

            _buttonStyle.fontStyle =
                FontStyle.Bold;

            _buttonStyle.alignment =
                TextAnchor.MiddleCenter;

            _buttonStyle.padding =
                new RectOffset(
                    0,
                    0,
                    0,
                    0);
        }

        private static void DrawBorder(
            Rect rect,
            float thickness,
            Color color)
        {
            DrawSolidRect(
                new Rect(
                    rect.x,
                    rect.y,
                    rect.width,
                    thickness),
                color);

            DrawSolidRect(
                new Rect(
                    rect.x,
                    rect.yMax - thickness,
                    rect.width,
                    thickness),
                color);

            DrawSolidRect(
                new Rect(
                    rect.x,
                    rect.y,
                    thickness,
                    rect.height),
                color);

            DrawSolidRect(
                new Rect(
                    rect.xMax - thickness,
                    rect.y,
                    thickness,
                    rect.height),
                color);
        }

        private static void DrawSolidRect(
            Rect rect,
            Color color)
        {
            Color previous =
                GUI.color;

            GUI.color =
                color;

            GUI.DrawTexture(
                rect,
                Texture2D.whiteTexture);

            GUI.color =
                previous;
        }
    }
}
