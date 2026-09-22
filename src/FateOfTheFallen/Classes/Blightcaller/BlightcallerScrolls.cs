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
            if (_registered)
            {
                return;
            }

            if (BlightcallerCatalog.BlightcallerClass == null)
            {
                BlightcallerCatalog.EnsureClass();
            }

            if (BlightcallerCatalog.BlightcallerClass == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller scroll registration waiting for class.");

                return;
            }

            if (GameData.ItemDB == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller scroll registration waiting for ItemDB.");

                return;
            }

            if (GameData.ItemDB.ItemDB == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Blightcaller scroll registration waiting for ItemDB contents.");

                return;
            }

            foreach (
                BlightcallerSpellDefinition definition
                in BlightcallerSpellData.All)
            {
                if (definition.HiddenEffect)
                {
                    continue;
                }

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

                Item scroll =
                    CreateScroll(
                        definition,
                        spell,
                        scrollId);

                if (scroll == null)
                {
                    continue;
                }

                BlightcallerItemFactory.RegisterItem(
                    scroll);

                ScrollsByKey[
                    definition.Key] =
                    scroll;
            }

            _registered =
                ScrollsByKey.Count > 0;
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
                BlightcallerItemFactory.GetCustomScrollIcon();

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