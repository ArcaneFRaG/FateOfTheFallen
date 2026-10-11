using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class BlightcallerScrolls
    {
        private const string NativeScrollTemplateId =
            "15302448";

        private static bool _registered;

        private static readonly Dictionary<string, Item> ScrollsByKey =
            new Dictionary<string, Item>(
                StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, int> ScrollIds =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase)
            {
                // ============================================================
                // EXISTING SPELLS
                // ============================================================

                { "blighted_bolt", 1600 },
                { "shadow_rend", 1601 },
                { "withering_touch", 1602 },
                { "blight", 1603 },
                { "agony", 1604 },

                // ============================================================
                // TIER II
                // ============================================================

                { "putrid_bolt", 1605 },
                { "shadow_rupture", 1606 },
                { "greater_withering_touch", 1607 },
                { "greater_blight", 1608 },
                { "greater_agony", 1609 },

                // ============================================================
                // TIER III
                // ============================================================

                { "festering_bolt", 1610 },
                { "shadow_devastation", 1611 },
                { "potent_withering_touch", 1612 },
                { "virulent_blight", 1613 },
                { "lingering_agony", 1614 },
                { "soul_rend", 1615 },

                // ============================================================
                // TIER IV - RAID
                // ============================================================

                { "apocalyptic_bolt", 1616 },
                { "void_rend", 1617 },
                { "eternal_withering", 1618 },
                { "cataclysmic_blight", 1619 },
                { "endless_agony", 1620 },

                // ============================================================
                // DEBUFF SPELLS
                // ============================================================

                { "festering_weakness", 1621 },
                { "greater_festering_weakness", 1622 },
                { "devastating_festering_weakness", 1623 },
                { "withering_curse", 1624 },
                { "greater_withering_curse", 1625 },
                { "eternal_withering_curse", 1626 }
            };

        internal static bool IsRegistered
        {
            get
            {
                return _registered;
            }
        }

        internal static void Register()
        {
            /*
             * Do not early-return just because this class registered once.
             *
             * Plugin.OnDestroy() clears CustomItemRegistry during Lunaris hot
             * unload/reload. Depending on the loader/runtime, these static
             * scroll objects can survive while the shared registry does not.
             * Re-running this method must therefore rebind every surviving
             * scroll into CustomItemRegistry instead of assuming the first
             * registration is still valid.
             */

            if (BlightcallerCatalog.BlightcallerClass == null)
            {
                BlightcallerCatalog.EnsureClass();
            }

            if (BlightcallerCatalog.BlightcallerClass == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller scroll registration waiting for class.");

                _registered =
                    false;

                return;
            }

            if (GameData.ItemDB == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller scroll registration waiting for ItemDB.");

                _registered =
                    false;

                return;
            }

            if (GameData.ItemDB.ItemDB == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller scroll registration waiting for ItemDB contents.");

                _registered =
                    false;

                return;
            }

            int expectedScrolls =
                0;

            int registeredScrolls =
                0;

            foreach (
                BlightcallerSpellDefinition definition
                in BlightcallerSpellData.All)
            {
                if (definition.HiddenEffect)
                {
                    continue;
                }

                expectedScrolls++;

                string scrollId =
                    BuildScrollId(
                        definition.Key);

                if (string.IsNullOrEmpty(scrollId))
                {
                    Plugin.NativeLog.LogWarning(
                        string.Format(
                            "Blightcaller: no item ID assigned for scroll '{0}'.",
                            definition.Name));

                    continue;
                }

                Spell spell =
                    BlightcallerCatalog.GetSpell(
                        definition.Key);

                if (spell == null)
                {
                    Plugin.NativeLog.LogWarning(
                        string.Format(
                            "Blightcaller: cannot create scroll for '{0}' because the spell is unavailable.",
                            definition.Name));

                    continue;
                }

                Item scroll;

                if (!ScrollsByKey.TryGetValue(
                        definition.Key,
                        out scroll) ||
                    scroll == null)
                {
                    /*
                     * If the shared registry already has the stable ID, adopt
                     * that object first. This avoids creating a second object
                     * when registration is retried during normal database
                     * lifecycle events.
                     */
                    scroll =
                        CustomItemRegistry.GetItemById(
                            scrollId);

                    if (scroll == null)
                    {
                        scroll =
                            CreateScroll(
                                definition,
                                spell,
                                scrollId);
                    }

                    if (scroll == null)
                    {
                        continue;
                    }

                    ScrollsByKey[
                        definition.Key] =
                        scroll;
                }

                /*
                 * Refresh the two reference-sensitive fields. A hot reload may
                 * create a new canonical Blightcaller Class / Spell object
                 * even when a scroll object itself survives.
                 */
                scroll.TeachSpell =
                    spell;

                scroll.Classes =
                    new List<Class>
                    {
                        BlightcallerCatalog.BlightcallerClass
                    };

                /*
                 * Critical hot-reload step: CustomItemRegistry is cleared on
                 * plugin unload, so always re-register the canonical scroll.
                 */
                CustomItemRegistry.RegisterItem(
                    scroll);

                registeredScrolls++;
            }

            _registered =
                expectedScrolls > 0 &&
                registeredScrolls == expectedScrolls;

            if (!_registered)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller scroll registration incomplete. Registered=[" +
                    registeredScrolls +
                    "] Expected=[" +
                    expectedScrolls +
                    "].");
            }
        }


        private static Item CreateScroll(
            BlightcallerSpellDefinition definition,
            Spell spell,
            string scrollId)
        {
            Item scroll =
                ScriptableObject.CreateInstance<Item>();

            scroll.name =
                "Spell: " +
                definition.Name;

            scroll.ItemName =
                "Spell: " +
                definition.Name;

            scroll.Id =
                scrollId;

            scroll.ItemLevel =
                definition.Level;

            scroll.ItemValue =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        (definition.Level * definition.Level) *
                        0.75f +
                        definition.Level * 2f));

            Sprite customScrollIcon =
                BlightcallerAssets.GetScrollIcon();

            if (customScrollIcon != null)
            {
                scroll.ItemIcon =
                    customScrollIcon;
            }
            else
            {
                Item nativeScroll =
                    GameData.ItemDB.GetItemByID(
                        NativeScrollTemplateId);

                if (nativeScroll != null &&
                    nativeScroll.ItemIcon != null)
                {
                    scroll.ItemIcon =
                        nativeScroll.ItemIcon;
                }
                else
                {
                    scroll.ItemIcon =
                        spell.SpellIcon;
                }
            }

            scroll.RequiredSlot =
                Item.SlotType.General;

            scroll.Classes =
                new List<Class>
                {
                    BlightcallerCatalog.BlightcallerClass
                };

            scroll.TeachSpell =
                spell;

            scroll.Lore =
                "A teaching scroll containing the knowledge of " +
                definition.Name +
                ".";

            scroll.Stackable =
                false;

            scroll.Disposable =
                false;

            scroll.NoTradeNoDestroy =
                false;

            scroll.PlayerCannotSell =
                false;

            scroll.SimPlayersCantGet =
                true;

            scroll.hideFlags =
                HideFlags.HideAndDontSave;

            return scroll;
        }

        internal static Item GetScroll(
            string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            Item scroll;

            if (ScrollsByKey.TryGetValue(
                key,
                out scroll))
            {
                return scroll;
            }

            return null;
        }

        internal static Item GetScrollById(
            int id)
        {
            string idString =
                id.ToString();

            foreach (
                Item scroll
                in ScrollsByKey.Values)
            {
                if (scroll == null)
                {
                    continue;
                }

                if (string.Equals(
                    scroll.Id,
                    idString,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return scroll;
                }
            }

            return null;
        }

        internal static bool IsCustomScrollId(
            int id)
        {
            return GetScrollById(id) != null;
        }

        internal static void GiveStartingScrolls()
        {
            if (!_registered)
            {
                Register();
            }

            if (!_registered)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller: could not give starting scrolls because registration is incomplete.");

                return;
            }

            AddOnce(
                GetScroll("blighted_bolt"));

            AddOnce(
                GetScroll("withering_touch"));
        }

        private static void AddOnce(
            Item item)
        {
            if (item == null)
            {
                return;
            }

            if (GameData.PlayerInv == null)
            {
                Plugin.NativeLog.LogWarning(
                    string.Format(
                        "Blightcaller: cannot add starting scroll '{0}' because PlayerInv is unavailable.",
                        item.ItemName));

                return;
            }

            try
            {
                GameData.PlayerInv.AddItemToInv(
                    item);

                return;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    string.Format(
                        "Blightcaller: AddItemToInv failed for '{0}': {1}",
                        item.ItemName,
                        exception.Message));
            }

            try
            {
                GameData.PlayerInv.ForceItemToInv(
                    item);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    string.Format(
                        "Blightcaller: failed to add starting scroll '{0}': {1}",
                        item.ItemName,
                        exception));
            }
        }

        private static string BuildScrollId(
            string key)
        {
            int id;

            if (ScrollIds.TryGetValue(
                key,
                out id))
            {
                return id.ToString();
            }

            return string.Empty;
        }
    }
}