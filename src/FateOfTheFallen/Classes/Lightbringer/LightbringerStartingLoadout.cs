using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class LightbringerStartingLoadout
    {
        // Native Druid's new-character starting equipment and provisions.
        // The native Weak Poison teaching scroll is deliberately NOT granted;
        // the two Lightbringer starting spell scrolls take its place.
        private static readonly string[] NativeDruidStartingItemNames =
        {
            "Rusty Dagger",
            "Tattered Leather Leggings",
            "Bread",
            "Water"
        };
        private static readonly string[] StartingItemIds =
        {
            LightbringerItemIds.FlickerWardScroll,
            LightbringerItemIds.RadiantBoltScroll
        };

        // This grant is session-scoped. Never refill existing saved Lightbringers.
        // Each entry is marked complete separately so retrying a missing item
        // cannot duplicate a scroll that the player has already received.
        private static bool _pendingNewCharacter;
        private static string _pendingCharacterName;
        private static readonly bool[] Granted = new bool[6];
        private static float _nextRetry;
        private static int _attempts;

        internal static void QueueForNewCharacter(CharSelectManager manager)
        {
            if (manager == null || manager.selClass == null ||
                !LightbringerCatalog.IsLightbringerClass(manager.selClass) ||
                GameData.CurrentCharacterSlot == null ||
                !LightbringerCharacterState.IsLightbringer(GameData.CurrentCharacterSlot))
                return;

            string selectedName = manager.Name != null ? manager.Name.text : null;
            if (string.IsNullOrWhiteSpace(selectedName) || selectedName == "Name Character")
                return;

            // Repeated SaveChar callbacks during creation must not reset the
            // successful-item flags or grant a second set of teaching scrolls.
            if (_pendingNewCharacter &&
                string.Equals(_pendingCharacterName, selectedName, StringComparison.OrdinalIgnoreCase))
                return;

            _pendingCharacterName = selectedName.Trim();
            _pendingNewCharacter = true;
            _attempts = 0;
            _nextRetry = 0f;
            Array.Clear(Granted, 0, Granted.Length);
            Plugin.LogInfo("[LBStarter] queued for newly-created Lightbringer [" + _pendingCharacterName + "].");
        }

        internal static void TryCompletePendingGrant()
        {
            if (!_pendingNewCharacter || GameData.InCharSelect ||
                GameData.ItemDB == null || GameData.PlayerInv == null ||
                GameData.PlayerStats == null ||
                !LightbringerCharacterState.IsLightbringer(GameData.CurrentCharacterSlot) ||
                !LightbringerCatalog.IsCurrentPlayerLightbringer())
                return;

            // The outgoing character's PlayerStats may still be present while
            // the newly selected save slot is loading. Never grant to them.
            if (!string.IsNullOrEmpty(_pendingCharacterName) &&
                !string.Equals(GameData.PlayerStats.MyName, _pendingCharacterName,
                    StringComparison.OrdinalIgnoreCase))
                return;

            if (Time.unscaledTime < _nextRetry) return;
            _nextRetry = Time.unscaledTime + 1.2f;
            _attempts++;

            try
            {
                LightbringerTeachingItems.Register(GameData.ItemDB);
                for (int i = 0; i < NativeDruidStartingItemNames.Length; i++)
                {
                    if (Granted[i]) continue;
                    string name = NativeDruidStartingItemNames[i];
                    Item item = FindNativeStartingItem(name);
                    if (item == null && name == "Tattered Leather Leggings")
                        item = FindStarterLeggingsFallback();
                    Granted[i] = TryGrant(item);
                    if (!Granted[i] && (_attempts == 1 || _attempts % 10 == 0))
                        Plugin.LogWarning("[LBStarter] missing/ungranted [" + name + "] attempt=" + _attempts);
                }

                for (int i = 0; i < StartingItemIds.Length; i++)
                {
                    int index = NativeDruidStartingItemNames.Length + i;
                    if (Granted[index]) continue;
                    Item item = CustomItemRegistry.Resolve(StartingItemIds[i], GameData.ItemDB) ??
                                LightbringerTeachingItems.GetScrollForSpell(
                                    i == 0 ? LightbringerSpellCatalog.FlickerWardId : LightbringerSpellCatalog.RadiantBoltId);
                    Granted[index] = TryGrant(item);
                    if (!Granted[index] && (_attempts == 1 || _attempts % 10 == 0))
                        Plugin.LogWarning("[LBStarter] missing/ungranted scroll [" + StartingItemIds[i] + "] attempt=" + _attempts);
                }

                bool complete = true;
                for (int i = 0; i < Granted.Length; i++) complete &= Granted[i];
                if (complete)
                {
                    Plugin.LogInfo("[LBStarter] all 6 starter items verified for [" + _pendingCharacterName + "].");
                    ClearPending();
                }
                else if (_attempts >= 50)
                {
                    Plugin.LogWarning("[LBStarter] incomplete after 50 attempts; leaving inventory unchanged rather than spawning duplicates. Review missing-item logs.");
                    ClearPending();
                }
            }
            catch (Exception ex)
            {
                if (_attempts == 1 || _attempts % 10 == 0)
                    Plugin.LogWarning("[LBStarter] grant pending after exception: " + ex.Message);
            }
        }

        private static void ClearPending()
        {
            _pendingNewCharacter = false;
            _pendingCharacterName = null;
        }

        private static Item FindStarterLeggingsFallback()
        {
            // Same low-level pants fallback used by the Blightcaller creator,
            // only if this game version renamed Druid's native starter pants.
            Item[] items = GameData.ItemDB != null ? GameData.ItemDB.ItemDB : null;
            if (items == null) return null;
            Item best = null;
            for (int i = 0; i < items.Length; i++)
            {
                Item item = items[i];
                if (item == null || string.IsNullOrEmpty(item.ItemName) ||
                    item.ItemName.IndexOf("Pants", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (best == null || item.ItemLevel < best.ItemLevel) best = item;
            }
            return best;
        }

        private static Item FindNativeStartingItem(string itemName)
        {
            ItemDatabase database = GameData.ItemDB;
            if (database == null || string.IsNullOrEmpty(itemName))
                return null;

            // The native item DB retains the canonical item instances;
            // don't create copies, mutate native item data, or rely on IDs
            // that could change with an Erenshor content update.
            if (database.ItemDB != null)
            {
                for (int i = 0; i < database.ItemDB.Length; i++)
                {
                    Item item = database.ItemDB[i];
                    if (MatchesName(item, itemName)) return item;
                }
            }

            if (database.ItemDBList != null)
            {
                for (int i = 0; i < database.ItemDBList.Count; i++)
                {
                    Item item = database.ItemDBList[i];
                    if (MatchesName(item, itemName)) return item;
                }
            }

            if (database.GenericItems != null)
            {
                foreach (Item item in database.GenericItems)
                    if (MatchesName(item, itemName)) return item;
            }

            return null;
        }

        private static bool MatchesName(Item item, string itemName)
        {
            return item != null &&
                string.Equals((item.ItemName ?? string.Empty).Trim(), itemName,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGrant(Item item)
        {
            if (item == null || GameData.PlayerInv == null) return false;
            if (HasItem(item)) return true;
            try
            {
                // Match the successful Emberweaver starter path first.
                bool added = GameData.PlayerInv.AddItemToInv(item);
                if (!added && !HasItem(item)) GameData.PlayerInv.ForceItemToInv(item);
            }
            catch (Exception exception)
            {
                Plugin.LogWarning("[LBStarter] AddItemToInv failed for " + (item.ItemName ?? item.Id) + ": " + exception.Message);
                try { GameData.PlayerInv.ForceItemToInv(item); }
                catch (Exception ex) { Plugin.LogWarning("[LBStarter] ForceItemToInv failed: " + ex.Message); }
            }
            bool verified = HasItem(item);
            if (verified) Plugin.LogInfo("[LBStarter] granted [" + (item.ItemName ?? item.Id) + "] id=" + item.Id);
            return verified;
        }

        private static bool HasItem(Item item)
        {
            if (item == null || GameData.PlayerInv == null)
                return false;

            try
            {
                if (GameData.PlayerInv.HasItem(item, false))
                    return true;
            }
            catch
            {
            }

            return ContainsById(GameData.PlayerInv.StoredItems, item.Id) ||
                   ContainsById(GameData.PlayerInv.EquippedItems, item.Id);
        }

        private static bool ContainsById(IList<Item> items, string id)
        {
            if (items == null || string.IsNullOrEmpty(id))
                return false;

            for (int i = 0; i < items.Count; i++)
            {
                Item current = items[i];
                if (current != null && string.Equals(current.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
