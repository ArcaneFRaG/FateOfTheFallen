using System;
using System.Collections.Generic;
using HarmonyLib;

namespace FateOfTheFallen
{
    [HarmonyPatch(typeof(VendorInventory), "Start")]
    internal static class Patch_LightbringerAuraVendors
    {
        [HarmonyPostfix]
        private static void Postfix(VendorInventory __instance)
        {
            if (__instance == null || __instance.transform == null) return;
            bool amanda = HasParentNamed(__instance.transform, "Amanda Rostley");
            bool mid = HasParentNamed(__instance.transform, "Juctin Mysia");
            bool high = HasParentNamed(__instance.transform, "Edwin Ansegg");
            if (!amanda && !mid && !high) return;
            try
            {
                if (GameData.SpellDatabase != null && LightbringerAuras.GetItem(LightbringerAuras.FirstLightKey) == null)
                    LightbringerAuras.Register(GameData.SpellDatabase);
                if (amanda) Add(__instance, LightbringerAuras.FirstLightKey);
                if (mid) { Add(__instance, LightbringerAuras.DawnsEmbraceKey); Add(__instance, LightbringerAuras.SanctuaryKey); }
                if (high) Add(__instance, LightbringerAuras.RadiantCovenantKey);
            }
            catch (Exception e) { Plugin.LogError("[LBAuraVendor] " + e); }
        }
        private static void Add(VendorInventory vendor, string key)
        {
            Item item = LightbringerAuras.GetItem(key);
            if (item == null) { Plugin.LogWarning("[LBAuraVendor] Missing aura " + key); return; }
            if (vendor.ItemsForSale == null) vendor.ItemsForSale = new List<Item>();
            foreach (Item existing in vendor.ItemsForSale)
                if (existing != null && string.Equals(existing.Id,item.Id,StringComparison.OrdinalIgnoreCase)) return;
            vendor.ItemsForSale.Add(item);
        }
        private static bool HasParentNamed(UnityEngine.Transform transform, string name)
        {
            while (transform != null)
            {
                if (string.Equals(transform.name, name, StringComparison.OrdinalIgnoreCase)) return true;
                transform = transform.parent;
            }
            return false;
        }
    }
    // Unique native Soluna boss loot. Do not drop from open-world or FOTF raids.
    [HarmonyPatch(typeof(LootTable), "InitLootTable")]
    internal static class Patch_LightbringerEternalDawnDrop
    {
        private const float Chance = 0.25f;
        [HarmonyPostfix]
        private static void Postfix(LootTable __instance)
        {
            if (__instance == null || __instance.hostChar == null || __instance.hostChar.MyNPC == null) return;
            Character host = __instance.hostChar;
            if (!host.isNPC || host.isVendor ||
                !EmberweaverScrollDistribution.IsNativeErenshorRaidScene(host.gameObject.scene) ||
                !string.Equals(host.MyNPC.SetAchievementOnDefeat, "GODESSSOLUNA", StringComparison.OrdinalIgnoreCase)) return;
            if (UnityEngine.Random.value >= Chance) return;
            if (GameData.SpellDatabase != null) LightbringerAuras.Register(GameData.SpellDatabase);
            Item aura = LightbringerAuras.GetItem(LightbringerAuras.EternalDawnKey);
            if (aura == null) { Plugin.LogWarning("[LBAuraDrop] Aura of Eternal Dawn was not registered."); return; }
            if (__instance.ActualDrops == null) __instance.ActualDrops = new List<Item>();
            foreach (Item existing in __instance.ActualDrops)
                if (existing != null && existing.Id == aura.Id) return;
            __instance.ActualDrops.Add(aura);
            if (__instance.ActualDropsQual == null) __instance.ActualDropsQual = new List<int>();
            __instance.ActualDropsQual.Add(1);
        }
    }
}
