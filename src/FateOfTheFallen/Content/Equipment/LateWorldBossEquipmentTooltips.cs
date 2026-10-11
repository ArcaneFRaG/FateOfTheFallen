using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace FateOfTheFallen
{
    [HarmonyPatch(
        typeof(ItemInfoWindow),
        "DisplayItem",
        new Type[]
        {
            typeof(Item),
            typeof(Vector2),
            typeof(int)
        })]
    internal static class Patch_LateWorldBossEquipmentTooltips
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Item item, ItemInfoWindow __instance)
        {
            if (item == null)
                return;

            if (VarkeshEquipment.IsVarkeshEquipment(item))
                VarkeshEquipment.ApplyClassRestriction(item);
            else if (GorathEquipment.IsGorathEquipment(item))
                GorathEquipment.ApplyClassRestriction(item);
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(
            Item item,
            ItemInfoWindow __instance)
        {
            if (__instance == null)
                return;

            LateWorldBossNativeTooltipTextFix controller =
                __instance.GetComponent<LateWorldBossNativeTooltipTextFix>();

            if (item == null ||
                (!VarkeshEquipment.IsVarkeshEquipment(item) &&
                 !GorathEquipment.IsGorathEquipment(item)))
            {
                if (controller != null)
                    controller.Clear();
                return;
            }

            if (controller == null)
                controller = __instance.gameObject.AddComponent<LateWorldBossNativeTooltipTextFix>();

            controller.Track(item, __instance);
        }
    }

    internal sealed class LateWorldBossNativeTooltipTextFix : MonoBehaviour
    {
        private string _itemId;
        private ItemInfoWindow _window;
        private TMP_Text[] _texts;
        private float _nextRewrite;
        // Tooltip labels occasionally arrive one or two frames after DisplayItem.
        // Retry only briefly; do not allocate component arrays forever.
        private int _pendingRefreshes;
        // Erenshor's native hover title is exactly:
        // UI/ItemInfo/OnClick/Image (1)/SpellName
        // Its text is a single TMP component that can contain both the correct
        // current proc line and a newline-delimited stale proc name. Never
        // search unrelated Canvas labels or rely on the previously hovered item.
        private TMP_Text _ringSpellName;
        private bool _ringSpellNameMissingLogged;

        internal void Track(Item item, ItemInfoWindow window)
        {
            string nextId = item != null ? item.Id : null;
            if (_window == window &&
                string.Equals(_itemId, nextId, StringComparison.Ordinal) &&
                _texts != null)
            {
                // The same hover can be re-rendered by native DisplayItem.
                Rewrite();
                return;
            }

            _itemId = nextId;
            _window = window;
            _texts = window != null ? window.GetComponentsInChildren<TMP_Text>(true) : null;
            _ringSpellName = null;
            _ringSpellNameMissingLogged = false;
            if (window != null && IsCustomRing(nextId))
                ResolveRingSpellName();

            _pendingRefreshes = 3;
            _nextRewrite = Time.unscaledTime + 0.15f;
            Rewrite();
        }

        private void ResolveRingSpellName()
        {
            if (_window == null || _ringSpellName != null)
                return;

            // The r46 diagnostic identified the exact hierarchy as
            // UI/ItemInfo/OnClick/Image (1)/SpellName.  ItemInfoWindow does
            // not necessarily live on the ItemInfo transform itself: in the
            // user's build it also includes ~1,937 TMP components beneath it.
            // Thus Find("OnClick/...") relative to _window is NOT reliable.
            // Walk known ancestors and check only the exact tooltip path.
            Transform current = _window.transform;
            for (int depth = 0; current != null && depth < 12;
                 ++depth, current = current.parent)
            {
                Transform candidate =
                    current.Find("ItemInfo/OnClick/Image (1)/SpellName");
                if (candidate == null)
                    candidate = current.Find("OnClick/Image (1)/SpellName");
                if (candidate == null)
                    candidate = current.Find("Image (1)/SpellName");
                if (candidate == null && current.name == "Image (1)")
                    candidate = current.Find("SpellName");
                if (candidate == null && current.name == "SpellName")
                    candidate = current;

                if (IsRingSpellNamePath(candidate))
                {
                    TMP_Text label = candidate.GetComponent<TMP_Text>();
                    if (label != null)
                    {
                        _ringSpellName = label;
                        return;
                    }
                }
            }

            // Narrow safety net for UI builds with a different component
            // ownership root. _texts is ALREADY cached by Track/LateUpdate;
            // do not allocate or enumerate the full Canvas again.  Match the
            // distinctive parent chain so unrelated SpellName widgets cannot
            // be changed by this ring-only hover correction.
            for (int i = 0; _texts != null && i < _texts.Length; ++i)
            {
                TMP_Text label = _texts[i];
                if (label != null && IsRingSpellNamePath(label.transform))
                {
                    _ringSpellName = label;
                    return;
                }
            }
        }

        private static bool IsRingSpellNamePath(Transform field)
        {
            if (field == null || field.name != "SpellName")
                return false;
            Transform image = field.parent;
            Transform onClick = image != null ? image.parent : null;
            Transform info = onClick != null ? onClick.parent : null;
            return image != null && image.name == "Image (1)" &&
                   onClick != null && onClick.name == "OnClick" &&
                   info != null && info.name == "ItemInfo";
        }

        private void OnDisable()
        {
            Clear();
        }

        internal void Clear()
        {
            _itemId = null;
            _window = null;
            _texts = null;
            _ringSpellName = null;
            _ringSpellNameMissingLogged = false;
            _pendingRefreshes = 0;
        }

        private void LateUpdate()
        {
            if (_pendingRefreshes <= 0 || Time.unscaledTime < _nextRewrite)
                return;

            _nextRewrite = Time.unscaledTime + 0.2f;
            --_pendingRefreshes;
            if (_window != null)
            {
                _texts = _window.GetComponentsInChildren<TMP_Text>(true);
                if (IsCustomRing(_itemId) && _ringSpellName == null)
                    ResolveRingSpellName();
            }
            Rewrite();
        }

        private void Rewrite()
        {
            if (_window == null || string.IsNullOrEmpty(_itemId))
                return;

            if (_texts == null)
                return;

            bool replaceRingProc = IsCustomRing(_itemId) &&
                !IsExpandedProcDetailVisible();
            string ringProcName = replaceRingProc ? GetRingProcName(_itemId) : null;

            for (int i = 0; i < _texts.Length; i++)
            {
                TMP_Text text = _texts[i];
                if (text == null || string.IsNullOrEmpty(text.text))
                    continue;

                string current = text.text;
                string corrected = current;

                if (IsCaster(_itemId))
                {
                    corrected = ReplaceIgnoreCase(corrected, "chance on ATTACK", "chance on SPELL DAMAGE");
                }
                else if (IsPhysicalMelee(_itemId))
                {
                    corrected = ReplaceIgnoreCase(corrected, "chance on ATTACK", "chance on PHYSICAL HIT");
                }
                else if (IsHeart(_itemId))
                {
                    corrected = ReplaceIgnoreCase(corrected, "25% chance on ATTACK", "Activates below 25% Health");
                    corrected = ReplaceIgnoreCase(corrected, "chance on ATTACK", "HEALTH threshold");
                }
                else if (IsLantern(_itemId))
                {
                    corrected = ReplaceIgnoreCase(corrected, "100% chance on ATTACK", "Activates on LOW-HEALTH DIRECT HEAL");
                    corrected = ReplaceIgnoreCase(corrected, "chance on ATTACK", "LOW-HEALTH DIRECT HEAL");
                }
                else if (IsRanged(_itemId))
                {
                    corrected = ReplaceIgnoreCase(corrected, "chance on ATTACK", "chance on RANGED HIT");
                }
                else if (IsBastion(_itemId))
                {
                    corrected = ReplaceIgnoreCase(corrected, "chance on ATTACK", "chance on BASH");
                }
                else if (IsCovenant(_itemId))
                {
                    corrected = ReplaceIgnoreCase(corrected, "chance on ATTACK", "chance on BENEFICIAL SPELL");
                }
                if (!string.Equals(current, corrected, StringComparison.Ordinal))
                {
                    text.text = corrected;
                    text.ForceMeshUpdate();
                }
            }

            // Fix the exact native field verified by the r46 diagnostic.
            // The same TMP label may contain:
            //   Equip Effect: Gravewind\nCorrupt
            // Set its complete HOVER text to the current ring's proc, even if
            // the correct name was already on the first line. Never modify the
            // expanded right-click details.
            if (replaceRingProc && !string.IsNullOrEmpty(ringProcName))
                RewriteNativeRingSpellName(ringProcName);
        }

        private void RewriteNativeRingSpellName(string ringProcName)
        {
            if (_window == null || string.IsNullOrEmpty(ringProcName)) return;
            if (_ringSpellName == null) ResolveRingSpellName();
            if (_ringSpellName == null)
            {
                // Native tooltip children can appear after DisplayItem.
                // Report a single miss only once the existing three short
                // retries have finished; never start a Canvas-wide scan.
                if (_pendingRefreshes <= 0 && !_ringSpellNameMissingLogged)
                {
                    _ringSpellNameMissingLogged = true;
                    Plugin.NativeLog.LogWarning(
                        "[FOTF/RingHover] native SpellName not found for ring [" +
                        _itemId + "] after bounded retries; window=[" +
                        _window.transform.name + "]; hover unchanged.");
                }
                return;
            }

            string wanted = "<color=#7CFF8C>Equip Effect:</color> " + ringProcName;
            if (!string.Equals(_ringSpellName.text, wanted, StringComparison.Ordinal))
            {
                // This is an *assignment*, never an append, trim, or split.
                // Consequently any stale 'Corrupt', 'Soul Rend', or other
                // previously hovered title disappears from the same field.
                _ringSpellName.text = wanted;
            }
        }

        private bool IsExpandedProcDetailVisible()
        {
            if (_texts == null) return false;
            for (int i = 0; i < _texts.Length; i++)
            {
                TMP_Text child = _texts[i];
                if (child != null && child.gameObject.activeInHierarchy &&
                    !string.IsNullOrEmpty(child.text) &&
                    (child.text.IndexOf(CustomEquipmentEffectDescriptions.DetailMarker,
                        StringComparison.Ordinal) >= 0 ||
                     child.text.IndexOf("Cast Time", StringComparison.OrdinalIgnoreCase) >= 0))
                    return true;
            }
            return false;
        }

        private static bool IsCustomRing(string id)
        {
            return string.Equals(id, VarkeshEquipment.HeartOfTheDeathlessId,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, VarkeshEquipment.BandOfTheGravewindId,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, GorathEquipment.AshenCovenantId,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRingProcName(string id)
        {
            // The right-click spell proxy owns the authoritative displayed name.
            // Never copy its full description into the compact hover tooltip.
            Item item = CustomItemRegistry.GetItemById(id);
            Spell proc = item != null ? item.WeaponProcOnHit : null;
            if (proc != null && !string.IsNullOrEmpty(proc.SpellName))
                return proc.SpellName;

            if (string.Equals(id, VarkeshEquipment.HeartOfTheDeathlessId,
                StringComparison.OrdinalIgnoreCase)) return "Refuse Death";
            if (string.Equals(id, VarkeshEquipment.BandOfTheGravewindId,
                StringComparison.OrdinalIgnoreCase)) return "Gravewind";
            if (string.Equals(id, GorathEquipment.AshenCovenantId,
                StringComparison.OrdinalIgnoreCase)) return "Ashen Covenant";
            return null;
        }

        private static bool IsCaster(string id)
        {
            return string.Equals(id, VarkeshEquipment.SoulbinderGrimoireId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, GorathEquipment.EmberOfTheDemonKingId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPhysicalMelee(string id)
        {
            return string.Equals(id, VarkeshEquipment.GravetideReaverId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, GorathEquipment.HellforgedTalonId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHeart(string id)
        {
            return string.Equals(id, VarkeshEquipment.HeartOfTheDeathlessId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLantern(string id)
        {
            return string.Equals(id, VarkeshEquipment.LanternOfDepartedSoulsId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRanged(string id)
        {
            return string.Equals(id, VarkeshEquipment.BandOfTheGravewindId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, GorathEquipment.StormfireId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBastion(string id)
        {
            return string.Equals(id, GorathEquipment.InfernalBastionId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCovenant(string id)
        {
            return string.Equals(id, GorathEquipment.AshenCovenantId, StringComparison.OrdinalIgnoreCase);
        }

        private static string ReplaceIgnoreCase(
            string source,
            string oldValue,
            string newValue)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(oldValue))
                return source;

            int index = source.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return source;

            return source.Substring(0, index) +
                   newValue +
                   source.Substring(index + oldValue.Length);
        }
    }

    internal static class LateWorldBossTooltipProxySuppression
    {
        internal static Spell Suppress(Item item)
        {
            if (item == null ||
                (!VarkeshEquipment.IsVarkeshEquipment(item) &&
                 !GorathEquipment.IsGorathEquipment(item)))
            {
                return null;
            }

            Spell original = item.WeaponProcOnHit;
            item.WeaponProcOnHit = null;
            return original;
        }

        internal static void Restore(Item item, Spell original)
        {
            if (item == null || original == null ||
                (!VarkeshEquipment.IsVarkeshEquipment(item) &&
                 !GorathEquipment.IsGorathEquipment(item)))
            {
                return;
            }

            item.WeaponProcOnHit = original;
        }
    }

    [HarmonyPatch(
        typeof(Stats),
        "CheckProc",
        new Type[]
        {
            typeof(ItemIcon),
            typeof(Character)
        })]
    internal static class Patch_LateWorldBossTooltipProxyItemIconProc
    {
        [HarmonyPrefix]
        private static void Prefix(ItemIcon slot, out Spell __state)
        {
            __state = LateWorldBossTooltipProxySuppression.Suppress(slot != null ? slot.MyItem : null);
        }

        [HarmonyPostfix]
        private static void Postfix(ItemIcon slot, Spell __state)
        {
            LateWorldBossTooltipProxySuppression.Restore(slot != null ? slot.MyItem : null, __state);
        }
    }

    [HarmonyPatch(
        typeof(Stats),
        "CheckProc",
        new Type[]
        {
            typeof(Item),
            typeof(Character)
        })]
    internal static class Patch_LateWorldBossTooltipProxyItemProc
    {
        [HarmonyPrefix]
        private static void Prefix(Item slot, out Spell __state)
        {
            __state = LateWorldBossTooltipProxySuppression.Suppress(slot);
        }

        [HarmonyPostfix]
        private static void Postfix(Item slot, Spell __state)
        {
            LateWorldBossTooltipProxySuppression.Restore(slot, __state);
        }
    }
}
