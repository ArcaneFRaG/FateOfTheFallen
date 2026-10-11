using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Moldermaw tooltip integration.
    ///
    /// All five items now use Erenshor's own proc-tooltip pipeline, including
    /// the native "(Hold RIGHT MOUSE for details)" behavior and the native
    /// layout/expansion logic.
    ///
    /// Bulwark and Bloom use tooltip-only WeaponProcOnHit proxies because
    /// their real triggers are Bash and direct healing. Stats.CheckProc is
    /// suppressed for those two item IDs so the proxy never changes combat.
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
    internal static class Patch_MoldermawEquipmentTooltips
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(
            Item item)
        {
            if (item == null ||
                !MoldermawEquipment.IsMoldermawEquipment(
                    item))
            {
                return;
            }

            MoldermawEquipment.ApplyClassRestriction(
                item);
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

            MoldermawNativeTooltipTextFix controller =
                __instance.GetComponent<MoldermawNativeTooltipTextFix>();

            if (item == null ||
                !MoldermawEquipment.IsMoldermawEquipment(
                    item))
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
                    __instance.gameObject.AddComponent<MoldermawNativeTooltipTextFix>();
            }

            controller.Track(
                item,
                __instance);
        }
    }


    /// <summary>
    /// The native WeaponProc tooltip says "chance on ATTACK". That is correct
    /// for Rotcleaver, but the Bulwark proxy actually triggers on Bash and the
    /// Bloom proxy actually triggers on direct heals. Keep the native layout
    /// and RMB expansion, and only correct those trigger words in-place.
    /// </summary>
    internal sealed class MoldermawNativeTooltipTextFix :
        MonoBehaviour
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
            _itemId =
                item != null
                    ? item.Id
                    : null;

            _window =
                window;

            _cachedTexts =
                _window != null
                    ? _window.GetComponentsInChildren<TextMeshProUGUI>(true)
                    : null;
            _nextTextCacheRefresh = Time.unscaledTime + 0.50f;

            RewriteNativeText();
        }


        internal void Clear()
        {
            _itemId =
                null;

            _window =
                null;
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
                string.IsNullOrEmpty(
                    _itemId))
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

            for (int i = 0;
                 i < texts.Length;
                 i++)
            {
                TextMeshProUGUI text =
                    texts[i];

                if (text == null ||
                    string.IsNullOrEmpty(
                        text.text))
                {
                    continue;
                }

                string current =
                    text.text;

                string corrected =
                    current;

                if (string.Equals(
                        _itemId,
                        MoldermawEquipment.BulwarkId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    corrected =
                        ReplaceIgnoreCase(
                            corrected,
                            "chance on ATTACK",
                            "chance on BASH");
                }
                else if (string.Equals(
                             _itemId,
                             MoldermawEquipment.BloomId,
                             StringComparison.OrdinalIgnoreCase))
                {
                    corrected =
                        ReplaceIgnoreCase(
                            corrected,
                            "chance on ATTACK",
                            "chance on DIRECT HEAL");
                }

                if (!string.Equals(
                        current,
                        corrected,
                        StringComparison.Ordinal))
                {
                    text.text =
                        corrected;

                    text.ForceMeshUpdate();
                }
            }
        }


        private static string ReplaceIgnoreCase(
            string source,
            string oldValue,
            string newValue)
        {
            if (string.IsNullOrEmpty(
                    source) ||
                string.IsNullOrEmpty(
                    oldValue))
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
                source.Substring(
                    0,
                    index) +
                newValue +
                source.Substring(
                    index +
                    oldValue.Length);
        }
    }


    /// <summary>
    /// Prevent the tooltip-only Bulwark/Bloom WeaponProc proxies from ever
    /// firing through Erenshor's normal attack proc system.
    /// Their actual effects are handled by the dedicated FOTF Bash/heal hooks.
    /// </summary>
    [HarmonyPatch(
        typeof(Stats),
        "CheckProc",
        new Type[]
        {
            typeof(Item),
            typeof(Character)
        })]
    internal static class Patch_MoldermawTooltipProxyProcSuppression
    {
        [HarmonyPrefix]
        private static bool Prefix(
            Item slot)
        {
            if (slot == null)
            {
                return true;
            }

            if (string.Equals(
                    slot.Id,
                    MoldermawEquipment.BulwarkId,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    slot.Id,
                    MoldermawEquipment.BloomId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
    }
}
