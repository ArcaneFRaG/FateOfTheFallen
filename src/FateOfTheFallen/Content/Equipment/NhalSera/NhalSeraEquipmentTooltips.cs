using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Routes all Nhal'Sera equipment through Erenshor's native proc-tooltip
    /// presentation instead of injecting multiline effect text into Item.Lore.
    /// This restores native layout and Hold RIGHT MOUSE expansion behavior.
    /// </summary>
    [HarmonyPatch(
        typeof(ItemInfoWindow),
        "DisplayItem",
        new Type[]
        {
            typeof(Item),
            typeof(Vector2),
            typeof(int)
        })]
    internal static class Patch_NhalSeraEquipmentTooltips
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Item item)
        {
            if (item == null ||
                !NhalSeraEquipment.IsNhalSeraEquipment(item))
            {
                return;
            }

            NhalSeraEquipment.ApplyClassRestriction(item);
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(
            Item item,
            ItemInfoWindow __instance)
        {
            if (__instance == null)
            {
                return;
            }

            NhalSeraNativeTooltipTextFix controller =
                __instance.GetComponent<NhalSeraNativeTooltipTextFix>();

            if (item == null ||
                !NhalSeraEquipment.IsNhalSeraEquipment(item))
            {
                if (controller != null)
                {
                    controller.Clear();
                }

                return;
            }

            if (controller == null)
            {
                controller =
                    __instance.gameObject.AddComponent<NhalSeraNativeTooltipTextFix>();
            }

            controller.Track(item, __instance);
        }
    }


    /// <summary>
    /// Keeps the native tooltip layout while correcting the native generic
    /// "chance on ATTACK" wording to match each custom trigger.
    /// </summary>
    internal sealed class NhalSeraNativeTooltipTextFix : MonoBehaviour
    {
        private string _itemId;
        private ItemInfoWindow _window;
        private TextMeshProUGUI[] _cachedTexts;
        private float _nextTextCacheRefresh;
        private float _nextRewriteTime;

        internal void Track(
            Item item,
            ItemInfoWindow window)
        {
            _itemId = item != null ? item.Id : null;
            _window = window;
            _cachedTexts =
                _window != null
                    ? _window.GetComponentsInChildren<TextMeshProUGUI>(true)
                    : null;
            _nextTextCacheRefresh = Time.unscaledTime + 0.50f;
            RewriteNativeText();
        }

        internal void Clear()
        {
            _itemId = null;
            _window = null;
            _cachedTexts = null;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < _nextRewriteTime)
            {
                return;
            }

            _nextRewriteTime = Time.unscaledTime + 0.20f;
            RewriteNativeText();
        }

        private void RewriteNativeText()
        {
            if (_window == null ||
                string.IsNullOrEmpty(_itemId))
            {
                return;
            }

            if (_cachedTexts == null ||
                Time.unscaledTime >= _nextTextCacheRefresh)
            {
                _cachedTexts =
                    _window.GetComponentsInChildren<TextMeshProUGUI>(true);
                _nextTextCacheRefresh = Time.unscaledTime + 0.50f;
            }

            TextMeshProUGUI[] texts =
                _cachedTexts;

            if (texts == null)
            {
                return;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                TextMeshProUGUI text = texts[i];

                if (text == null ||
                    string.IsNullOrEmpty(text.text))
                {
                    continue;
                }

                string current = text.text;
                string corrected = current;

                if (string.Equals(
                        _itemId,
                        NhalSeraEquipment.LightlessScepterId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    corrected = ReplaceIgnoreCase(
                        corrected,
                        "chance on ATTACK",
                        "chance on DAMAGE");
                }
                else if (string.Equals(
                             _itemId,
                             NhalSeraEquipment.CarapaceId,
                             StringComparison.OrdinalIgnoreCase))
                {
                    corrected = ReplaceIgnoreCase(
                        corrected,
                        "35% chance on ATTACK",
                        "Activates below 35% Health");

                    corrected = ReplaceIgnoreCase(
                        corrected,
                        "chance on ATTACK",
                        "HEALTH threshold");
                }
                else if (string.Equals(
                             _itemId,
                             NhalSeraEquipment.CrownId,
                             StringComparison.OrdinalIgnoreCase))
                {
                    corrected = ReplaceIgnoreCase(
                        corrected,
                        "chance on ATTACK",
                        "chance on DIRECT HEAL");
                }
                else if (string.Equals(
                             _itemId,
                             NhalSeraEquipment.GraspId,
                             StringComparison.OrdinalIgnoreCase))
                {
                    corrected = ReplaceIgnoreCase(
                        corrected,
                        "chance on ATTACK",
                        "chance on PHYSICAL HIT");
                }
                else if (string.Equals(
                             _itemId,
                             NhalSeraEquipment.GirdleId,
                             StringComparison.OrdinalIgnoreCase))
                {
                    corrected = ReplaceIgnoreCase(
                        corrected,
                        "chance on ATTACK",
                        "chance on SPELL / HEAL");
                }

                if (!string.Equals(
                        current,
                        corrected,
                        StringComparison.Ordinal))
                {
                    text.text = corrected;
                    text.ForceMeshUpdate();
                }
            }
        }

        private static string ReplaceIgnoreCase(
            string source,
            string oldValue,
            string newValue)
        {
            if (string.IsNullOrEmpty(source) ||
                string.IsNullOrEmpty(oldValue))
            {
                return source;
            }

            int index =
                source.IndexOf(
                    oldValue,
                    StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                return source;
            }

            return
                source.Substring(0, index) +
                newValue +
                source.Substring(index + oldValue.Length);
        }
    }


    /// <summary>
    /// Nhal'Sera items use WeaponProcOnHit only as a tooltip proxy. Temporarily
    /// clear that field while native CheckProc runs so the proxy can never fire,
    /// while preserving the rest of native CheckProc (class/skill procs etc.).
    /// </summary>
    internal static class NhalSeraTooltipProxySuppression
    {
        internal static Spell Suppress(Item item)
        {
            if (item == null ||
                !NhalSeraEquipment.IsNhalSeraEquipment(item))
            {
                return null;
            }

            Spell original = item.WeaponProcOnHit;
            item.WeaponProcOnHit = null;
            return original;
        }

        internal static void Restore(
            Item item,
            Spell original)
        {
            if (item == null ||
                original == null ||
                !NhalSeraEquipment.IsNhalSeraEquipment(item))
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
    internal static class Patch_NhalSeraTooltipProxyItemIconProc
    {
        [HarmonyPrefix]
        private static void Prefix(
            ItemIcon slot,
            out Spell __state)
        {
            __state =
                NhalSeraTooltipProxySuppression.Suppress(
                    slot != null ? slot.MyItem : null);
        }

        [HarmonyPostfix]
        private static void Postfix(
            ItemIcon slot,
            Spell __state)
        {
            NhalSeraTooltipProxySuppression.Restore(
                slot != null ? slot.MyItem : null,
                __state);
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
    internal static class Patch_NhalSeraTooltipProxyItemProc
    {
        [HarmonyPrefix]
        private static void Prefix(
            Item slot,
            out Spell __state)
        {
            __state =
                NhalSeraTooltipProxySuppression.Suppress(slot);
        }

        [HarmonyPostfix]
        private static void Postfix(
            Item slot,
            Spell __state)
        {
            NhalSeraTooltipProxySuppression.Restore(
                slot,
                __state);
        }
    }
}
