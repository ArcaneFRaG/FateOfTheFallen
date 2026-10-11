using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FateOfTheFallen
{
    /// <summary>
    /// Non-interactive presentation of FOTF's existing confirmed absorption.
    /// Does not modify native health, heal/shield mechanics, UI prefabs or Combat Meter.
    /// Anchors are vanilla HUD objects; replacement frames are not modified.
    /// </summary>
    internal static class LightbringerNativeAbsorbOverlay
    {
        private const string PrefKey = "FOTF.Lightbringer.NativeAbsorbUI.";
        private const float RefreshInterval = .16f;
        // A numeric badge sits inside the HP row. Never draw a separate strip
        // between native health and mana; those bars belong to Erenshor.
        private static readonly Color ShieldBadgeColor = new Color(.055f, .28f, .43f, .95f);
        private static readonly Dictionary<RectTransform, Entry> Entries =
            new Dictionary<RectTransform, Entry>();
        private static readonly Dictionary<RectTransform, RectTransform> HealthBarCache =
            new Dictionary<RectTransform, RectTransform>();
        // Failed lookups are throttled: never scan an entire raid card every Tick.
        private static readonly Dictionary<RectTransform, float> HealthBarNextProbe =
            new Dictionary<RectTransform, float>();
        private static readonly HashSet<RectTransform> MissingBarLogged =
            new HashSet<RectTransform>();
        private static readonly HashSet<RectTransform> Active = new HashSet<RectTransform>();
        private static readonly List<RectTransform> Expired = new List<RectTransform>();
        private static RectTransform _playerHud;
        private static RectTransform _targetHud;
        private static float _nextFind;
        private static float _nextRefresh;
        private static bool _initialized;
        private static float _nextReplacementCheck;
        private static bool _customPlayer, _customTarget, _customParty, _customRaid;
        private static bool _player = true, _target = true, _party = true, _raid = true;
        private static bool _enabled = true;
        private static Font _font;
        private static bool _loggedPlayer, _loggedTarget, _loggedParty, _loggedRaid;

        private sealed class Entry
        {
            internal RectTransform Anchor;
            internal GameObject Root;
            internal Text Label;
            internal GameObject Badge;
            internal Stats LastStats;
            internal string Kind;
        }

        internal static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _enabled = PlayerPrefs.GetInt(PrefKey + "Enabled", 1) != 0;
            _player = PlayerPrefs.GetInt(PrefKey + "Player", 1) != 0;
            _target = PlayerPrefs.GetInt(PrefKey + "Target", 1) != 0;
            _party = PlayerPrefs.GetInt(PrefKey + "Party", 1) != 0;
            _raid = PlayerPrefs.GetInt(PrefKey + "Raid", 1) != 0;
            Plugin.LogInfo("[NativeAbsorbUI] r101 initialized; " + Status());
        }

        internal static string Status()
        {
            return "enabled=" + _enabled + " player=" + _player + " target=" + _target +
                   " party=" + _party + " raid=" + _raid;
        }

        internal static void Configure(string part, bool enabled)
        {
            if (part == "all")
            {
                _enabled = enabled;
                PlayerPrefs.SetInt(PrefKey + "Enabled", enabled ? 1 : 0);
            }
            else if (part == "player") { _player = enabled; PlayerPrefs.SetInt(PrefKey + "Player", enabled ? 1 : 0); }
            else if (part == "target") { _target = enabled; PlayerPrefs.SetInt(PrefKey + "Target", enabled ? 1 : 0); }
            else if (part == "party") { _party = enabled; PlayerPrefs.SetInt(PrefKey + "Party", enabled ? 1 : 0); }
            else if (part == "raid") { _raid = enabled; PlayerPrefs.SetInt(PrefKey + "Raid", enabled ? 1 : 0); }
            else return;
            PlayerPrefs.Save();
            _nextRefresh = 0f;
            if (!_enabled) HideAll();
            Plugin.LogInfo("[NativeAbsorbUI] configuration " + Status());
        }

        internal static void Tick()
        {
            if (!_initialized) Initialize();
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            try
            {
                if (!_enabled || GameData.InCharSelect || GameData.PlayerControl == null ||
                    GameData.PlayerStats == null)
                {
                    HideAll();
                    return;
                }

                if (Time.unscaledTime >= _nextFind)
                {
                    _nextFind = Time.unscaledTime + 3f;
                    LocateNativeRoots();
                }

                if (Time.unscaledTime >= _nextReplacementCheck)
                {
                    _nextReplacementCheck = Time.unscaledTime + .75f;
                    _customPlayer = IsCustomFrameVisible("ArcaneUI_PlayerFrame", "PlayerFrame");
                    _customTarget = IsCustomFrameVisible("ArcaneUI_TargetFrame", "TargetFrame");
                    _customParty = IsCustomPanelVisible("ArcanesGroupFrames_PartyFramesCanvas", "PartyFramesPanel");
                    _customRaid = IsCustomPanelVisible("ArcanesGroupFrames_RaidFramesCanvas", "RaidFramesPanel");
                }
                Active.Clear();
                if (_player && !_customPlayer)
                {
                    Stats stats = GameData.PlayerControl.Myself != null
                        ? GameData.PlayerControl.Myself.MyStats : GameData.PlayerStats;
                    Show("player", FindHealthBar(_playerHud), stats, ref _loggedPlayer);
                }
                if (_target && !_customTarget)
                {
                    // Native target HUDs are often empty/inactive before a
                    // character is selected. Do not probe or warn until a
                    // real target with Stats is available.
                    Character selected = GameData.PlayerControl.CurrentTarget;
                    if (selected != null && selected.MyStats != null)
                        Show("target", FindHealthBar(_targetHud),
                            selected.MyStats, ref _loggedTarget);
                }
                if (_party && !GameData.RaidActive && !_customParty)
                    UpdateParty();
                if (_raid && GameData.RaidActive && !_customRaid)
                    UpdateRaid();

                Expired.Clear();
                foreach (KeyValuePair<RectTransform, Entry> pair in Entries)
                {
                    if (pair.Key == null || pair.Value.Root == null)
                        Expired.Add(pair.Key);
                    else if (!Active.Contains(pair.Key))
                        pair.Value.Root.SetActive(false);
                }
                foreach (RectTransform key in Expired)
                    Entries.Remove(key);
            }
            catch (Exception exception)
            {
                // A transient native HUD rebuild must never stop FOTF gameplay.
                if (Time.unscaledTime >= _nextError)
                {
                    _nextError = Time.unscaledTime + 15f;
                    Plugin.LogWarning("[NativeAbsorbUI] deferred after HUD change: " + exception.Message);
                }
            }
        }
        private static float _nextError;

        private static void UpdateParty()
        {
            SimPlayerGrouping grouping = GameData.SimPlayerGrouping;
            if (grouping == null || GameData.GroupMembers == null) return;
            GameObject[] holders = { grouping.ParOne, grouping.ParTwo, grouping.ParThree, grouping.ParFour };
            for (int i = 0; i < holders.Length && i < GameData.GroupMembers.Length; i++)
            {
                SimPlayerTracking member = GameData.GroupMembers[i];
                if (member == null || member.MyAvatar == null || holders[i] == null) continue;
                Show("party", FindHealthBar(holders[i].transform as RectTransform),
                    member.MyAvatar.MyStats, ref _loggedParty);
            }
        }

        private static void UpdateRaid()
        {
            RaidManager manager = GameData.RaidManager;
            if (manager == null) return;
            if (manager.PlayerNameCard != null && manager.PlayerNameCard.PlayerIcon != null)
            {
                // Native raid-player card belongs to the player even if the
                // character is currently not Lightbringer; wards can protect any class.
                Show("raid", FindHealthBar(RaidCardRoot(manager.PlayerNameCard.PlayerIcon)),
                    GameData.PlayerControl.Myself != null ? GameData.PlayerControl.Myself.MyStats : GameData.PlayerStats,
                    ref _loggedRaid);
            }
            if (manager.Raiders == null) return;
            foreach (RaidMemberSlot slot in manager.Raiders)
            {
                if (slot == null || slot.GroupNumber < 1) continue;
                // Native raid slots hold SimPlayer avatars, not Character objects.
                // Resolve either the assigned avatar or the tracking fallback,
                // then read the shield directly from that SimPlayer's Stats.
                SimPlayer avatar = slot.AssignedAvatar;
                if (avatar == null && slot.AssignedSimTracking != null)
                    avatar = slot.AssignedSimTracking.MyAvatar;
                if (avatar == null || avatar.MyStats == null) continue;
                RectTransform root = null;
                if (slot.ClassIcon != null) root = RaidCardRoot(slot.ClassIcon);
                if (root == null && slot.MyNameplate != null) root = RaidCardRoot(slot.MyNameplate);
                Show("raid", FindHealthBar(root), avatar.MyStats, ref _loggedRaid);
            }
        }

        private static RectTransform RaidCardRoot(Component graphic)
        {
            if (graphic == null) return null;
            RectTransform root = graphic.transform as RectTransform;
            if (root == null) return null;
            // Icon/name components live under the native card; never climb
            // above the single member card into the global RaidManager UI.
            if (root.parent is RectTransform) root = (RectTransform)root.parent;
            return root;
        }

        private static void Show(string kind, RectTransform anchor, Stats stats, ref bool logged)
        {
            if (anchor == null || stats == null || stats.CurrentHP <= 0 ||
                stats.CurrentMaxHP <= 0 || !anchor.gameObject.activeInHierarchy ||
                IsHiddenByParentCanvasGroup(anchor)) return;
            int shield = LightbringerAbsorbApi.GetRemainingAbsorb(stats);
            if (shield <= 0) return;
            if (!logged)
            {
                logged = true;
                Plugin.LogInfo("[NativeAbsorbUI] native " + kind + " indicator bound at " + HierarchyPath(anchor));
            }
            Entry entry;
            if (!Entries.TryGetValue(anchor, out entry) || entry.Root == null)
            {
                entry = Create(anchor, kind);
                if (entry == null) return;
                Entries[anchor] = entry;
            }
            if (entry.LastStats != stats) entry.LastStats = stats;
            // Absorb is an independent pool; present the amount *inside* the
            // existing HP bar without changing or obscuring native HP/mana.
            UpdateGeometry(entry, shield);
            if (!entry.Root.activeSelf) entry.Root.SetActive(true);
            Active.Add(anchor);
        }

        private static string FormatAbsorb(int amount)
        {
            // Keep the right-side label short enough to remain INSIDE the bar.
            if (amount >= 1000000) return "+" + (amount / 1000000f).ToString("0.#") + "m";
            if (amount >= 10000) return "+" + (amount / 1000f).ToString("0.#") + "k";
            return "+" + amount.ToString();
        }

        /// <summary>
        /// Native player LifeBG is 250x22, but its actual HP pixels occupy
        /// roughly x=10..190. Place a compact absorption *badge* at the
        /// right-hand end of that HP region. No separate track, no blue
        /// divider between health and mana, and no impact on the HP fill.
        /// On smaller party/raid bars use a small blue corner marker so the
        /// shield remains discoverable without obscuring native HP numbers.
        /// </summary>
        private static void UpdateGeometry(Entry entry, int remaining)
        {
            if (entry == null || entry.Anchor == null || entry.Badge == null ||
                entry.Label == null) return;
            RectTransform anchor = entry.Anchor;
            float outerWidth = Mathf.Max(0f, anchor.rect.width);
            bool playerLifeBg = entry.Kind == "player" &&
                string.Equals(anchor.name, "LifeBG", StringComparison.OrdinalIgnoreCase);
            float inset = playerLifeBg ? outerWidth * .04f : 0f;
            float usable = playerLifeBg ? outerWidth * .72f : outerWidth;
            usable = Mathf.Max(0f, Mathf.Min(usable, outerWidth - inset));
            if (usable < 40f || anchor.rect.height < 6f)
            {
                entry.Badge.SetActive(false);
                return;
            }

            // All sizes stay within the real HP rectangle. Avoid overlaying
            // text in tiny raid cards; those get only a modest corner marker.
            bool full = usable >= 145f && anchor.rect.height >= 12f;
            bool compact = !full && usable >= 86f && anchor.rect.height >= 11f;
            float badgeWidth = full ? 45f : compact ? 33f : 7f;
            float badgeHeight = full ? 12f : compact ? 10f : 6f;
            float rightInset = playerLifeBg ? 7f : 3f;
            float y = playerLifeBg ? 2.5f : 0f;
            RectTransform badgeRect = entry.Badge.transform as RectTransform;
            if (badgeRect == null) return;
            badgeRect.anchorMin = new Vector2(0f, .5f);
            badgeRect.anchorMax = new Vector2(0f, .5f);
            badgeRect.pivot = new Vector2(1f, .5f);
            badgeRect.anchoredPosition = new Vector2(inset + usable - rightInset, y);
            badgeRect.sizeDelta = new Vector2(badgeWidth, badgeHeight);
            entry.Label.enabled = full || compact;
            if (entry.Label.enabled)
            {
                entry.Label.fontSize = full ? 9 : 8;
                entry.Label.text = full ? FormatAbsorb(remaining) : FormatCompactAbsorb(remaining);
            }
            if (!entry.Badge.activeSelf) entry.Badge.SetActive(true);
        }

        private static string FormatCompactAbsorb(int amount)
        {
            if (amount >= 1000000) return "+" + (amount / 1000000f).ToString("0.#") + "m";
            if (amount >= 1000) return "+" + (amount / 1000f).ToString("0.#") + "k";
            return "+" + amount.ToString();
        }

        private static Entry Create(RectTransform anchor, string kind)
        {
            if (anchor == null) return null;
            try
            {
                GameObject root = new GameObject("FOTF_NativeAbsorb_" + kind, typeof(RectTransform));
                root.transform.SetParent(anchor, false);
                RectTransform rect = (RectTransform)root.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.pivot = new Vector2(.5f, .5f);
                // Clip descendants if native layout changes after character
                // loading. The overlay never clips or modifies native siblings.
                root.AddComponent<RectMask2D>();
                root.transform.SetAsLastSibling();

                // No track/fill objects: the shield value belongs *inside*
                // the native HP row, never in the gap above the mana bar.
                GameObject badge = new GameObject("AbsorbValueBadge", typeof(RectTransform));
                badge.transform.SetParent(root.transform, false);
                RectTransform badgeRect = (RectTransform)badge.transform;
                badgeRect.anchorMin = new Vector2(0f, .5f);
                badgeRect.anchorMax = new Vector2(0f, .5f);
                badgeRect.pivot = new Vector2(1f, .5f);
                badgeRect.sizeDelta = new Vector2(43f, 11f);
                Image badgeBackground = badge.AddComponent<Image>();
                badgeBackground.color = ShieldBadgeColor;
                badgeBackground.raycastTarget = false;

                GameObject label = new GameObject("AbsorptionAmount", typeof(RectTransform));
                label.transform.SetParent(badge.transform, false);
                RectTransform textRect = (RectTransform)label.transform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(1f, 0f);
                textRect.offsetMax = new Vector2(-1f, 0f);
                Text text = label.AddComponent<Text>();
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (_font != null) text.font = _font;
                text.fontSize = 9;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(.86f, .97f, 1f, 1f);
                text.raycastTarget = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.supportRichText = false;
                var shadow = label.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, .85f);
                shadow.effectDistance = new Vector2(1f, -1f);
                root.SetActive(false);
                return new Entry { Anchor = anchor, Root = root,
                    Label = text, Badge = badge, Kind = kind };
            }
            catch (Exception ex)
            {
                Plugin.LogWarning("[NativeAbsorbUI] Could not attach native overlay: " + ex.Message);
                return null;
            }
        }

        private static RectTransform FindHealthBar(RectTransform root)
        {
            if (root == null || !root.gameObject.activeInHierarchy) return null;
            RectTransform cached;
            if (HealthBarCache.TryGetValue(root, out cached))
            {
                if (IsUsableHealthRect(root, cached)) return cached;
                float retryAt;
                if (HealthBarNextProbe.TryGetValue(root, out retryAt) && Time.unscaledTime < retryAt)
                    return null;
            }
            HealthBarNextProbe[root] = Time.unscaledTime + 3f;

            // Select an actual HP graphic or native health Slider, not the
            // encompassing PlayerLifePar/NewTargetWindow/raid-card canvas.
            // The former r98 whole-HUD fallback made the +5100 float outside.
            RectTransform best = null;
            int bestScore = 0;
            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            foreach (Graphic graphic in graphics)
            {
                if (graphic == null || graphic is Text ||
                    !graphic.gameObject.activeInHierarchy ||
                    IsOurOverlayChild(graphic.transform)) continue;
                RectTransform candidate = graphic.rectTransform;
                if (candidate == null || !IsBarShape(candidate)) continue;
                string name = candidate.name.ToLowerInvariant();
                string parentName = candidate.parent != null
                    ? candidate.parent.name.ToLowerInvariant() : "";
                if (IsNonHealthName(name) || IsNonHealthName(parentName)) continue;
                bool namedHealth = IsHealthName(name);
                bool parentHealth = IsHealthName(parentName);
                Color tint = graphic.color;
                bool red = tint.a > .1f && tint.r >= .45f &&
                    tint.r > tint.g * 1.28f && tint.r > tint.b * 1.18f;
                if (!namedHealth && !parentHealth && !red) continue;
                int score = 0;
                if (namedHealth) score += 80;
                if (parentHealth) score += 40;
                if (red) score += 46;
                if (name.Contains("fill")) score += 8;
                Image img = graphic as Image;
                if (img != null && img.type == Image.Type.Filled) score += 10;
                // Real HP bars are normally short, wide strips.
                if (candidate.rect.height <= 23f) score += 12;
                score += Mathf.Min(18, Mathf.RoundToInt(candidate.rect.width / 18f));
                RectTransform fixedWidth = FullWidthHealthRect(candidate);
                if (score > bestScore && IsUsableHealthRect(root, fixedWidth))
                { best = fixedWidth; bestScore = score; }
            }

            // Some native layouts use a Slider with generic Image names.
            if (best == null)
            {
                Slider[] sliders = root.GetComponentsInChildren<Slider>(true);
                foreach (Slider slider in sliders)
                {
                    if (slider == null || !slider.gameObject.activeInHierarchy ||
                        !IsHealthName(slider.name.ToLowerInvariant())) continue;
                    RectTransform candidate = slider.transform as RectTransform;
                    if (IsUsableHealthRect(root, candidate))
                    { best = candidate; break; }
                }
            }

            HealthBarCache[root] = best;  // null is a safe, throttled miss.
            if (best != null)
            {
                MissingBarLogged.Remove(root);
                Plugin.LogInfo("[NativeAbsorbUI] r101 health anchor " +
                    HierarchyPath(best) + " size=[" +
                    best.rect.width.ToString("0.#") + "x" + best.rect.height.ToString("0.#") + "]");
            }
            else if (MissingBarLogged.Add(root))
                Plugin.LogWarning("[NativeAbsorbUI] r101 native HP bar not identified at " +
                    HierarchyPath(root) + "; skipped overlay rather than drawing outside frame.");
            return best;
        }

        private static bool IsBarShape(RectTransform rect)
        {
            if (rect == null) return false;
            float width = rect.rect.width, height = rect.rect.height;
            return width >= 45f && width <= 750f && height >= 5f && height <= 45f &&
                width >= height * 3f;
        }

        private static bool IsUsableHealthRect(RectTransform root, RectTransform rect)
        {
            if (root == null || rect == null || !rect.gameObject.activeInHierarchy ||
                !IsBarShape(rect) || IsOurOverlayChild(rect)) return false;
            for (Transform t = rect; t != null; t = t.parent)
                if (t == root) return true;
            return false;
        }

        private static RectTransform FullWidthHealthRect(RectTransform rect)
        {
            if (rect == null) return null;
            RectTransform parent = rect.parent as RectTransform;
            if (parent == null || !IsBarShape(parent)) return rect;
            float childWidth = rect.rect.width;
            float parentWidth = parent.rect.width;
            float childHeight = rect.rect.height;
            // The actual fill might shrink with HP; its background is a
            // better full-capacity anchor only when it has similar height.
            if (parentWidth >= childWidth * .90f && parentWidth <= childWidth * 2.6f &&
                parent.rect.height <= childHeight * 1.7f)
                return parent;
            return rect;
        }

        private static bool IsHealthName(string value)
        {
            return value.Contains("health") || value.Contains("hpbar") ||
                value.Contains("hpfill") || value.Contains("lifebar") ||
                value.Contains("lifefill") || value == "hp" || value == "life" ||
                value.Contains("playerhp") || value.Contains("hitpoint");
        }

        private static bool IsNonHealthName(string value)
        {
            return value.Contains("mana") || value.Contains("power") ||
                value.Contains("cast") || value.Contains("absorb") ||
                value.Contains("stamina") || value.Contains("energy") ||
                value.Contains("portrait") || value.Contains("icon") ||
                value.Contains("skill") || value.Contains("targetring") ||
                value.StartsWith("fotf_", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOurOverlayChild(Transform candidate)
        {
            for (Transform cursor = candidate; cursor != null; cursor = cursor.parent)
                if (cursor.name.StartsWith("FOTF_NativeAbsorb_", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static void LocateNativeRoots()
        {
            bool playerFound = false, targetFound = false;
            RectTransform[] rects = UnityEngine.Object.FindObjectsOfType<RectTransform>();
            foreach (RectTransform rect in rects)
            {
                if (rect == null || !rect.gameObject.activeInHierarchy) continue;
                if (rect.name == "PlayerLifePar" &&
                    HierarchyPath(rect) == "DontDestroyOnLoad:UI/UIElements/PlayerLifePar")
                { _playerHud = rect; playerFound = true; }
                else if (rect.name == "NewTargetWindow" &&
                    HierarchyPath(rect) == "DontDestroyOnLoad:UI/UIElements/TargCanv/NewTargetWindow")
                { _targetHud = rect; targetFound = true; }
            }
            if (!playerFound) _playerHud = null;
            if (!targetFound) _targetHud = null;
        }

        private static string HierarchyPath(Transform root)
        {
            if (root == null) return "";
            var segments = new List<string>();
            Transform cursor = root;
            while (cursor != null) { segments.Add(cursor.name); cursor = cursor.parent; }
            segments.Reverse();
            return root.gameObject.scene.name + ":" + string.Join("/", segments.ToArray());
        }

        private static bool IsHiddenByParentCanvasGroup(Transform anchor)
        {
            for (Transform parent = anchor; parent != null; parent = parent.parent)
            {
                CanvasGroup group = parent.GetComponent<CanvasGroup>();
                if (group != null && group.alpha < .05f) return true;
            }
            return false;
        }

        private static bool IsCustomFrameVisible(string layerName, string panelName)
        {
            GameObject layer = GameObject.Find(layerName);
            if (layer == null || !layer.activeInHierarchy) return false;
            foreach (RectTransform rect in layer.GetComponentsInChildren<RectTransform>(true))
                if (rect != null && rect.name == panelName && rect.gameObject.activeInHierarchy) return true;
            return false;
        }

        private static bool IsCustomPanelVisible(string canvasName, string panelName)
        {
            GameObject canvas = GameObject.Find(canvasName);
            if (canvas == null || !canvas.activeInHierarchy) return false;
            foreach (RectTransform rect in canvas.GetComponentsInChildren<RectTransform>(true))
                if (rect != null && rect.name == panelName && rect.gameObject.activeInHierarchy) return true;
            return false;
        }

        private static void HideAll()
        {
            foreach (Entry entry in Entries.Values)
                if (entry != null && entry.Root != null) entry.Root.SetActive(false);
        }

        internal static void Shutdown()
        {
            foreach (Entry entry in Entries.Values)
                if (entry != null && entry.Root != null) UnityEngine.Object.Destroy(entry.Root);
            Entries.Clear();
            HealthBarCache.Clear();
            HealthBarNextProbe.Clear();
            MissingBarLogged.Clear();
            Active.Clear();
            Expired.Clear();
            _playerHud = null;
            _targetHud = null;
            _initialized = false;
            _nextFind = 0f;
            _nextRefresh = 0f;
            _nextReplacementCheck = 0f;
            _customPlayer = _customTarget = _customParty = _customRaid = false;
            _loggedPlayer = _loggedTarget = _loggedParty = _loggedRaid = false;
        }
    }

    [HarmonyPatch(typeof(TypeText), "CheckCommands")]
    internal static class Patch_FOTFNativeAbsorbUICommand
    {
        [HarmonyPrefix]
        private static bool Prefix(TypeText __instance)
        {
            if (__instance == null || __instance.typed == null) return true;
            string command = (__instance.typed.text ?? "").Trim();
            string[] parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !string.Equals(parts[0], "/fotfabsorbui", StringComparison.OrdinalIgnoreCase))
                return true;
            __instance.typed.text = "";
            if (parts.Length == 1 || (parts.Length == 2 && parts[1].Equals("status", StringComparison.OrdinalIgnoreCase)))
                Message("Native absorption display: " + LightbringerNativeAbsorbOverlay.Status());
            else
            {
                string section = "all";
                string value;
                if (parts.Length == 2) value = parts[1];
                else if (parts.Length == 3) { section = parts[1].ToLowerInvariant(); value = parts[2]; }
                else { Usage(); return false; }
                if (section != "all" && section != "player" && section != "target" && section != "party" && section != "raid")
                { Usage(); return false; }
                bool enabled;
                if (value.Equals("on", StringComparison.OrdinalIgnoreCase)) enabled = true;
                else if (value.Equals("off", StringComparison.OrdinalIgnoreCase)) enabled = false;
                else { Usage(); return false; }
                LightbringerNativeAbsorbOverlay.Configure(section, enabled);
                Message("Native absorption display: " + LightbringerNativeAbsorbOverlay.Status());
            }
            return false;
        }

        private static void Usage()
        {
            Message("Usage: /fotfabsorbui status | on | off | player|target|party|raid on|off");
        }

        private static void Message(string text)
        {
            UpdateSocialLog.LogAdd(new ChatLogLine(text, ChatLogLine.LogType.SystemMessages, "yellow"));
        }
    }
}
