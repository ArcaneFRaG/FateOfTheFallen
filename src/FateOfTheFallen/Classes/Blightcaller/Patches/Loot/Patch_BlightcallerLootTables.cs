using HarmonyLib;
using System;
using System.Collections.Generic;

namespace FateOfTheFallen
{
    [HarmonyPatch(typeof(LootTable))]
    internal static class Patch_BlightcallerLowLevelDrops
        {
            private const float ScrollDropChance =
                0.05f;
    
            private const int MaximumMobLevel =
                10;
    
            [HarmonyPatch("InitLootTable")]
            [HarmonyPostfix]
            private static void InitLootTablePostfix(
                LootTable __instance)
            {
                if (__instance == null)
                {
                    return;
                }
    
                try
                {
                    Character host =
                        __instance.hostChar;
    
                    if (host == null ||
                        !host.isNPC ||
                        host.isVendor ||
                        host.MyStats == null)
                    {
                        return;
                    }
    
                    int mobLevel =
                        host.MyStats.Level;
    
                    if (mobLevel < 1 ||
                        mobLevel > MaximumMobLevel)
                    {
                        return;
                    }
    
                    if (UnityEngine.Random.value >= ScrollDropChance)
                    {
                        return;
                    }
    
                    List<Item> eligibleScrolls =
                        new List<Item>();
    
                    foreach (
                        BlightcallerSpellDefinition definition
                        in BlightcallerSpellData.All)
                    {
                        if (definition == null ||
                            definition.HiddenEffect ||
                            definition.Level < 1 ||
                            definition.Level > mobLevel)
                        {
                            continue;
                        }
    
                        Item scroll =
                            BlightcallerScrolls.GetScroll(
                                definition.Key);
    
                        if (scroll != null)
                        {
                            eligibleScrolls.Add(
                                scroll);
                        }
                    }
    
                    if (eligibleScrolls.Count == 0)
                    {
                        return;
                    }
    
                    Item selectedScroll =
                        eligibleScrolls[
                            UnityEngine.Random.Range(
                                0,
                                eligibleScrolls.Count)];
    
                    if (selectedScroll == null)
                    {
                        return;
                    }
    
                    if (__instance.ActualDrops == null)
                    {
                        __instance.ActualDrops =
                            new List<Item>();
                    }
    
                    __instance.ActualDrops.Add(
                        selectedScroll);
    
                    if (__instance.ActualDropsQual == null)
                    {
                        __instance.ActualDropsQual =
                            new List<int>();
                    }
    
                    __instance.ActualDropsQual.Add(
                        1);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "Blightcaller: loot-table modification failed: " +
                        exception);
                }
            }
        }
    
    [HarmonyPatch(typeof(LootTable))]
    internal static class Patch_BlightcallerMidLevelDrops
        {
            private const float ScrollDropChance =
                0.05f;
    
            private const int MinimumMobLevel =
                11;
    
            private const int MaximumMobLevel =
                20;
    
            [HarmonyPatch("InitLootTable")]
            [HarmonyPostfix]
            private static void InitLootTablePostfix(
                LootTable __instance)
            {
                if (__instance == null)
                {
                    return;
                }
    
                try
                {
                    Character host =
                        __instance.hostChar;
    
                    if (host == null ||
                        !host.isNPC ||
                        host.isVendor ||
                        host.MyStats == null)
                    {
                        return;
                    }
    
                    int mobLevel =
                        host.MyStats.Level;
    
                    if (mobLevel < MinimumMobLevel ||
                        mobLevel > MaximumMobLevel)
                    {
                        return;
                    }
    
                    if (UnityEngine.Random.value >= ScrollDropChance)
                    {
                        return;
                    }
    
                    List<Item> eligibleScrolls =
                        new List<Item>();
    
                    foreach (
                        BlightcallerSpellDefinition definition
                        in BlightcallerSpellData.All)
                    {
                        if (definition == null ||
                            definition.HiddenEffect ||
                            definition.Level < MinimumMobLevel ||
                            definition.Level > MaximumMobLevel ||
                            definition.Level > mobLevel)
                        {
                            continue;
                        }
    
                        Item scroll =
                            BlightcallerScrolls.GetScroll(
                                definition.Key);
    
                        if (scroll != null)
                        {
                            eligibleScrolls.Add(
                                scroll);
                        }
                    }
    
                    if (eligibleScrolls.Count == 0)
                    {
                        return;
                    }
    
                    Item selectedScroll =
                        eligibleScrolls[
                            UnityEngine.Random.Range(
                                0,
                                eligibleScrolls.Count)];
    
                    if (selectedScroll == null)
                    {
                        return;
                    }
    
                    if (__instance.ActualDrops == null)
                    {
                        __instance.ActualDrops =
                            new List<Item>();
                    }
    
                    __instance.ActualDrops.Add(
                        selectedScroll);
    
                    if (__instance.ActualDropsQual == null)
                    {
                        __instance.ActualDropsQual =
                            new List<int>();
                    }
    
                    __instance.ActualDropsQual.Add(
                        1);
                }
                catch (Exception exception)
                {
                    Plugin.ModLog.Error(
                        "Blightcaller: mid-level loot-table modification failed.",
                        exception);
                }
            }
        }
    
    [HarmonyPatch(typeof(LootTable))]
    internal static class Patch_BlightcallerLateLevelDrops
        {
            private const float ScrollDropChance =
                0.05f;
    
            private const int MinimumMobLevel =
                21;
    
            private const int MaximumMobLevel =
                34;
    
            [HarmonyPatch("InitLootTable")]
            [HarmonyPostfix]
            private static void InitLootTablePostfix(
                LootTable __instance)
            {
                if (__instance == null)
                {
                    return;
                }
    
                try
                {
                    Character host =
                        __instance.hostChar;
    
                    if (host == null ||
                        !host.isNPC ||
                        host.isVendor ||
                        host.MyStats == null)
                    {
                        return;
                    }
    
                    int mobLevel =
                        host.MyStats.Level;
    
                    if (mobLevel < MinimumMobLevel ||
                        mobLevel > MaximumMobLevel)
                    {
                        return;
                    }
    
                    if (UnityEngine.Random.value >= ScrollDropChance)
                    {
                        return;
                    }
    
                    List<Item> eligibleScrolls =
                        new List<Item>();
    
                    foreach (
                        BlightcallerSpellDefinition definition
                        in BlightcallerSpellData.All)
                    {
                        if (definition == null ||
                            definition.HiddenEffect ||
                            definition.Level < MinimumMobLevel ||
                            definition.Level > MaximumMobLevel ||
                            definition.Level > mobLevel)
                        {
                            continue;
                        }
    
                        Item scroll =
                            BlightcallerScrolls.GetScroll(
                                definition.Key);
    
                        if (scroll != null)
                        {
                            eligibleScrolls.Add(
                                scroll);
                        }
                    }
    
                    if (eligibleScrolls.Count == 0)
                    {
                        return;
                    }
    
                    Item selectedScroll =
                        eligibleScrolls[
                            UnityEngine.Random.Range(
                                0,
                                eligibleScrolls.Count)];
    
                    if (selectedScroll == null)
                    {
                        return;
                    }
    
                    if (__instance.ActualDrops == null)
                    {
                        __instance.ActualDrops =
                            new List<Item>();
                    }
    
                    __instance.ActualDrops.Add(
                        selectedScroll);
    
                    if (__instance.ActualDropsQual == null)
                    {
                        __instance.ActualDropsQual =
                            new List<int>();
                    }
    
                    __instance.ActualDropsQual.Add(
                        1);
                }
                catch (Exception exception)
                {
                    Plugin.ModLog.Error(
                        "Blightcaller: late-level loot-table modification failed.",
                        exception);
                }
            }
        }
    
    [HarmonyPatch(typeof(LootTable))]
    internal static class Patch_BlightcallerRaidDrops
        {
            private const float ScrollDropChance =
                0.05f;
    
            private const int MinimumMobLevel =
                40;
    
            [HarmonyPatch("InitLootTable")]
            [HarmonyPostfix]
            private static void InitLootTablePostfix(
                LootTable __instance)
            {
                if (__instance == null)
                {
                    return;
                }
    
                try
                {
                    Character host =
                        __instance.hostChar;
    
                    if (host == null ||
                        !host.isNPC ||
                        host.isVendor ||
                        host.MyStats == null)
                    {
                        return;
                    }
    
                    int mobLevel =
                        host.MyStats.Level;
    
                    if (mobLevel < MinimumMobLevel)
                    {
                        return;
                    }
    
                    if (UnityEngine.Random.value >= ScrollDropChance)
                    {
                        return;
                    }
    
                    List<Item> eligibleScrolls =
                        new List<Item>();
    
                    foreach (
                        BlightcallerSpellDefinition definition
                        in BlightcallerSpellData.All)
                    {
                        if (definition == null ||
                            definition.HiddenEffect ||
                            definition.Level < MinimumMobLevel ||
                            definition.Level > mobLevel)
                        {
                            continue;
                        }
    
                        Item scroll =
                            BlightcallerScrolls.GetScroll(
                                definition.Key);
    
                        if (scroll != null)
                        {
                            eligibleScrolls.Add(
                                scroll);
                        }
                    }
    
                    if (eligibleScrolls.Count == 0)
                    {
                        return;
                    }
    
                    Item selectedScroll =
                        eligibleScrolls[
                            UnityEngine.Random.Range(
                                0,
                                eligibleScrolls.Count)];
    
                    if (selectedScroll == null)
                    {
                        return;
                    }
    
                    if (__instance.ActualDrops == null)
                    {
                        __instance.ActualDrops =
                            new List<Item>();
                    }
    
                    __instance.ActualDrops.Add(
                        selectedScroll);
    
                    if (__instance.ActualDropsQual == null)
                    {
                        __instance.ActualDropsQual =
                            new List<int>();
                    }
    
                    __instance.ActualDropsQual.Add(
                        1);
                }
                catch (Exception exception)
                {
                    Plugin.ModLog.Error(
                        "Blightcaller: raid loot-table modification failed.",
                        exception);
                }
            }
        }
    
    [HarmonyPatch(typeof(LootTable))]
    internal static class Patch_BlightcallerFernallenHighPriest
        {
            private const float AuraDropChance =
                0.25f;
    
            private const string TargetBossName =
                "Fernallan High Priest";
    
            [HarmonyPatch("InitLootTable")]
            [HarmonyPostfix]
            private static void InitLootTablePostfix(
                LootTable __instance)
            {
                if (__instance == null)
                {
                    return;
                }
    
                try
                {
                    Character host =
                        __instance.hostChar;
    
                    if (host == null)
                    {
                        return;
                    }
    
                    string objectName =
                        host.gameObject.name;
    
                    if (string.IsNullOrEmpty(objectName))
                    {
                        return;
                    }
    
                    bool isFernallenHighPriest =
                        objectName.Equals(
                            TargetBossName,
                            StringComparison.OrdinalIgnoreCase) ||
                        objectName.Equals(
                            TargetBossName + "(Clone)",
                            StringComparison.OrdinalIgnoreCase);
    
                    if (!isFernallenHighPriest)
                    {
                        return;
                    }
    
                    if (UnityEngine.Random.value >= AuraDropChance)
                    {
                        return;
                    }
    
                    Item eternalBlight =
                        BlightcallerAuras.GetItem(
                            "eternal_blight");
    
                    if (eternalBlight == null)
                    {
                        Plugin.ModLog.Error(
                            "Blightcaller: Eternal Blight aura item was unavailable.");
    
                        return;
                    }
    
                    if (__instance.ActualDrops == null)
                    {
                        __instance.ActualDrops =
                            new List<Item>();
                    }
    
                    if (__instance.ActualDrops.Contains(
                            eternalBlight))
                    {
                        return;
                    }
    
                    __instance.ActualDrops.Add(
                        eternalBlight);
    
                    if (__instance.ActualDropsQual == null)
                    {
                        __instance.ActualDropsQual =
                            new List<int>();
                    }
    
                    __instance.ActualDropsQual.Add(1);
                }
                catch (Exception exception)
                {
                    Plugin.ModLog.Error(
                        "Blightcaller: Fernallan High Priest loot modification failed.",
                        exception);
                }
            }
        }
}
