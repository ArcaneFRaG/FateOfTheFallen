using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace FateOfTheFallen
{
    // ============================================================
    // ITEM DATABASE - MAIN INITIALIZATION
    // ============================================================

    [HarmonyPatch(
        typeof(ItemDatabase),
        "Start")]
    internal static class Patch_BlightcallerItemDatabaseStart
    {
        private static void Postfix(
            ItemDatabase __instance)
        {
            try
            {
                if (__instance == null)
                {
                    Plugin.NativeLog.LogError(
                        "Fate of the Fallen: ItemDatabase.Start instance was null.");

                    return;
                }


                // ====================================================
                // BLIGHTCALLER EQUIPMENT ELIGIBILITY
                // ====================================================

                BlightcallerEquipment
                    .RegisterArcanistEquipment();


                // ====================================================
                // QUEST REGISTRATION
                // ====================================================

                FateOfTheFallenQuests.Register();


                // ====================================================
                // CUSTOM ITEM REGISTRATION
                // ====================================================

                FateOfTheFallenNotes.Register(
                    __instance);

                Item weatheredNote =
                    FateOfTheFallenNotes
                        .GetWeatheredNote();

                if (weatheredNote == null)
                {
                    Plugin.NativeLog.LogError(
                        "Fate of the Fallen: Weathered Note was null during native ItemDatabase registration.");
                }
                else
                {
                    RegisterNativeCustomItem(
                        __instance,
                        weatheredNote);
                }


                // ====================================================
                // QUEST / ITEM LINKING
                // ====================================================

                FateOfTheFallenNotes.LinkQuest();

                FateOfTheFallenQuests.LinkItems();


                // ====================================================
                // BLIGHTCALLER AURAS
                // ====================================================

                if (GameData.SpellDatabase != null)
                {
                    BlightcallerAuras.Register(
                        GameData.SpellDatabase);
                }


                // ====================================================
                // DIAGNOSTICS
                // ====================================================

                Plugin.NativeLog.LogInfo(
                    "Fate of the Fallen: ItemDatabase initialization complete. Weathered Note=" +
                    (
                        weatheredNote != null
                            ? weatheredNote.Id
                            : "<null>"
                    ) +
                    ".");
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: failed during ItemDatabase initialization: " +
                    exception);
            }
        }


        // ============================================================
        // REGISTER CUSTOM ITEM WITH NATIVE DATABASE
        // ============================================================

        private static void RegisterNativeCustomItem(
            ItemDatabase database,
            Item item)
        {
            if (database == null ||
                item == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(
                    item.Id))
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: cannot register custom item because Item.Id is empty.");

                return;
            }


            // ========================================================
            // ITEMDB ARRAY
            // ========================================================

            if (database.ItemDB == null)
            {
                database.ItemDB =
                    new Item[]
                    {
                        item
                    };

                Plugin.NativeLog.LogInfo(
                    "Fate of the Fallen: native ItemDB was null; created database containing [" +
                    item.ItemName +
                    "].");
            }
            else
            {
                bool found =
                    false;

                for (int i = 0;
                     i < database.ItemDB.Length;
                     i++)
                {
                    Item existing =
                        database.ItemDB[i];

                    if (existing == item)
                    {
                        found =
                            true;

                        break;
                    }

                    if (existing == null)
                    {
                        continue;
                    }

                    if (!string.Equals(
                            existing.Id,
                            item.Id,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    database.ItemDB[i] =
                        item;

                    found =
                        true;

                    Plugin.NativeLog.LogInfo(
                        "Fate of the Fallen: replaced native ItemDB entry for ID [" +
                        item.Id +
                        "] with canonical custom item.");

                    break;
                }

                if (!found)
                {
                    int oldLength =
                        database.ItemDB.Length;

                    Item[] expanded =
                        new Item[
                            oldLength + 1];

                    Array.Copy(
                        database.ItemDB,
                        expanded,
                        oldLength);

                    expanded[
                        oldLength] =
                        item;

                    database.ItemDB =
                        expanded;

                    Plugin.NativeLog.LogInfo(
                        "Fate of the Fallen: added [" +
                        item.ItemName +
                        "] ID [" +
                        item.Id +
                        "] to native ItemDB at index [" +
                        oldLength +
                        "].");
                }
            }


            // ========================================================
            // ITEMDB LIST
            // ========================================================

            if (database.ItemDBList == null)
            {
                database.ItemDBList =
                    new List<Item>();
            }

            bool foundInList =
                false;

            for (int i = 0;
                 i < database.ItemDBList.Count;
                 i++)
            {
                Item existing =
                    database.ItemDBList[i];

                if (existing == item)
                {
                    foundInList =
                        true;

                    break;
                }

                if (existing == null)
                {
                    continue;
                }

                if (!string.Equals(
                        existing.Id,
                        item.Id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                database.ItemDBList[i] =
                    item;

                foundInList =
                    true;

                break;
            }

            if (!foundInList)
            {
                database.ItemDBList.Add(
                    item);
            }


            // ========================================================
            // PRIVATE ITEM DICTIONARY
            // ========================================================

            FieldInfo itemDictField =
                AccessTools.Field(
                    typeof(ItemDatabase),
                    "itemDict");

            if (itemDictField == null)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: ItemDatabase.itemDict field was not found.");

                return;
            }

            Dictionary<string, Item> itemDict =
                itemDictField.GetValue(
                    database)
                as Dictionary<string, Item>;

            if (itemDict == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: ItemDatabase.itemDict was null during custom item registration.");

                return;
            }

            itemDict[item.Id] =
                item;

            Plugin.NativeLog.LogInfo(
                "Fate of the Fallen: registered [" +
                item.ItemName +
                "] ID [" +
                item.Id +
                "] with native ItemDatabase.");
        }
    }


    // ============================================================
    // CUSTOM ITEM LOOKUP
    // ============================================================
    //
    // Native character loading performs:
    //
    //     StoredSlots[num].MyItem =
    //         GameData.ItemDB.GetItemByID(savedId);
    //
    // Therefore GetItemByID("1700") MUST return the Weathered Note
    // even if ItemDatabase.Start has not yet registered its native
    // visual data.
    // ============================================================

    [HarmonyPatch(
        typeof(ItemDatabase),
        "GetItemByID")]
    internal static class Patch_BlightcallerItemDatabase
    {
        private static bool Prefix(
            ItemDatabase __instance,
            string id,
            ref Item __result)
        {
            try
            {
                if (string.IsNullOrEmpty(
                        id))
                {
                    return true;
                }


                // ====================================================
                // NORMAL CUSTOM ITEM LOOKUP
                // ====================================================

                Item item =
                    BlightcallerItemFactory
                        .GetItemById(
                            id);

                if (item != null)
                {
                    __result =
                        item;

                    return false;
                }


                // ====================================================
                // WEATHERED NOTE LOAD FALLBACK
                // ====================================================
                //
                // Character inventory may be restored before our main
                // ItemDatabase.Start postfix has completed.
                //
                // The Weathered Note must therefore be constructible
                // immediately when its saved ID is requested.
                // ====================================================

                if (string.Equals(
                        id,
                        FateOfTheFallenNotes
                            .WeatheredNoteItemId
                            .ToString(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.NativeLog.LogInfo(
                        "Fate of the Fallen: native inventory loader requested Weathered Note [1700].");

                    FateOfTheFallenNotes.Register(
                        __instance);

                    item =
                        FateOfTheFallenNotes
                            .GetWeatheredNote();

                    if (item == null)
                    {
                        Plugin.NativeLog.LogError(
                            "Fate of the Fallen: failed to construct Weathered Note [1700] during native inventory load.");

                        return true;
                    }

                    __result =
                        item;

                    Plugin.NativeLog.LogInfo(
                        "Fate of the Fallen: resolved saved Weathered Note [1700] during native inventory load.");

                    return false;
                }


                // ====================================================
                // NOT OUR ITEM
                // ====================================================

                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: custom ItemDatabase lookup failed for ID [" +
                    id +
                    "]: " +
                    exception);

                return true;
            }
        }
    }
}