using System;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class FateOfTheFallenNotes
    {
        // ============================================================
        // ITEM IDS
        // ============================================================

        internal const int WeatheredNoteItemId =
            1700;

        private const string WeatheredNoteId =
            "1700";


        // ============================================================
        // NATIVE ITEM REFERENCES
        // ============================================================

        private const int NativeTornNoteIndex =
            434;


        // ============================================================
        // REGISTERED ITEMS
        // ============================================================

        private static Item _weatheredNote;


        // ============================================================
        // REGISTER
        // ============================================================
        //
        // IMPORTANT:
        //
        // The Weathered Note must be able to exist BEFORE the native
        // Torn Note icon is available.
        //
        // Native inventory loading restores items with:
        //
        //     GameData.ItemDB.GetItemByID(savedId)
        //
        // Therefore GetItemByID("1700") must always be capable of
        // returning our Item object.
        //
        // Native visual data such as the Torn Note icon can be applied
        // later once ItemDatabase has finished initializing.
        // ============================================================

        internal static void Register(
            ItemDatabase itemDatabase)
        {
            if (_weatheredNote == null)
            {
                _weatheredNote =
                    CreateWeatheredNote(
                        itemDatabase);

                if (_weatheredNote == null)
                {
                    Plugin.NativeLog.LogError(
                        "Fate of the Fallen: failed to create Weathered Note.");

                    return;
                }

                Plugin.NativeLog.LogInfo(
                    "Fate of the Fallen: registered Weathered Note [" +
                    _weatheredNote.Id +
                    "].");
            }


            // ========================================================
            // REFRESH NATIVE DATA
            // ========================================================
            //
            // If the item was created early during inventory loading,
            // its native icon may not have been available yet.
            //
            // Refresh it whenever Register() is called with an
            // initialized ItemDatabase.
            // ========================================================

            RefreshNativeData(
                itemDatabase);
        }


        // ============================================================
        // CREATE WEATHERED NOTE
        // ============================================================

        private static Item CreateWeatheredNote(
            ItemDatabase itemDatabase)
        {
            Sprite noteIcon =
                ResolveNativeNoteIcon(
                    itemDatabase);


            // ========================================================
            // CREATE CUSTOM ITEM
            // ========================================================
            //
            // IMPORTANT:
            //
            // noteIcon is allowed to be null here.
            //
            // We must never prevent creation of the actual Item object
            // merely because native icon data is not ready.
            // ========================================================

            Item note =
                BlightcallerItemFactory.CreateItem(
                    id:
                        WeatheredNoteId,

                    itemName:
                        "Weathered Note",

                    lore:
                        "The writing is faded, but still legible.\n\n" +
                        "\"If you found this, then I was right. " +
                        "Something beneath these ruins is stirring. " +
                        "Do not trust the silence.\"\n\n" +
                        "The note ends abruptly.",

                    slot:
                        Item.SlotType.General,

                    icon:
                        noteIcon,

                    itemLevel:
                        1,

                    itemValue:
                        0,

                    stackable:
                        false,

                    disposable:
                        false,

                    unique:
                        true,

                    playerCannotSell:
                        true,

                    noTradeNoDestroy:
                        true,

                    simPlayersCantGet:
                        true);

            if (note == null)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: BlightcallerItemFactory.CreateItem returned null for Weathered Note.");

                return null;
            }


            // ========================================================
            // BOOK DATA
            // ========================================================

            note.BookTitle =
                "A Weathered Note";


            // ========================================================
            // QUEST DATA
            // ========================================================
            //
            // Quest linking happens separately after the custom Quest
            // has been registered.
            // ========================================================

            note.AssignQuestOnRead =
                null;

            note.CompleteOnRead =
                null;


            // ========================================================
            // CREATION DIAGNOSTICS
            // ========================================================

            if (note.ItemIcon != null)
            {
                Plugin.NativeLog.LogInfo(
                    "Fate of the Fallen: Weathered Note created with native icon [" +
                    note.ItemIcon.name +
                    "].");
            }
            else
            {
                Plugin.NativeLog.LogInfo(
                    "Fate of the Fallen: Weathered Note created before native icon data was available.");
            }

            return note;
        }


        // ============================================================
        // RESOLVE NATIVE NOTE ICON
        // ============================================================

        private static Sprite ResolveNativeNoteIcon(
            ItemDatabase itemDatabase)
        {
            if (itemDatabase == null)
            {
                return null;
            }

            Item tornNote =
                null;


            // ========================================================
            // NORMAL NATIVE LOOKUP
            // ========================================================

            try
            {
                tornNote =
                    itemDatabase.GetItemFromDB(
                        NativeTornNoteIndex);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogDebug(
                    "Fate of the Fallen: Torn Note lookup was not yet available: " +
                    exception.Message);
            }


            // ========================================================
            // DIRECT ARRAY FALLBACK
            // ========================================================

            if (tornNote == null &&
                itemDatabase.ItemDB != null &&
                itemDatabase.ItemDB.Length >
                    NativeTornNoteIndex)
            {
                tornNote =
                    itemDatabase.ItemDB[
                        NativeTornNoteIndex];
            }

            if (tornNote == null)
            {
                return null;
            }

            if (tornNote.ItemIcon == null)
            {
                return null;
            }

            Plugin.NativeLog.LogDebug(
                "Fate of the Fallen: native note template resolved: [" +
                tornNote.ItemName +
                "] index [" +
                NativeTornNoteIndex +
                "], icon [" +
                tornNote.ItemIcon.name +
                "].");

            return tornNote.ItemIcon;
        }


        // ============================================================
        // REFRESH NATIVE DATA
        // ============================================================

        private static void RefreshNativeData(
            ItemDatabase itemDatabase)
        {
            if (_weatheredNote == null ||
                itemDatabase == null)
            {
                return;
            }

            Sprite nativeIcon =
                ResolveNativeNoteIcon(
                    itemDatabase);

            if (nativeIcon == null)
            {
                return;
            }

            if (_weatheredNote.ItemIcon ==
                nativeIcon)
            {
                return;
            }

            _weatheredNote.ItemIcon =
                nativeIcon;

            Plugin.NativeLog.LogInfo(
                "Fate of the Fallen: refreshed Weathered Note icon from native Torn Note [" +
                nativeIcon.name +
                "].");
        }


        // ============================================================
        // LINK WEATHERED NOTE TO QUEST
        // ============================================================

        internal static void LinkQuest()
        {
            if (_weatheredNote == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: cannot link Weathered Note quest because the note is null.");

                return;
            }

            Quest quest =
                FateOfTheFallenQuests
                    .WeatheredNoteQuest;

            if (quest == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: cannot link Weathered Note because the quest is null.");

                return;
            }

            _weatheredNote.AssignQuestOnRead =
                quest;

            Plugin.NativeLog.LogInfo(
                "Fate of the Fallen: Weathered Note [" +
                WeatheredNoteId +
                "] linked to quest [" +
                quest.DBName +
                "].");
        }


        // ============================================================
        // GET WEATHERED NOTE
        // ============================================================

        internal static Item GetWeatheredNote()
        {
            return _weatheredNote;
        }


        // ============================================================
        // NUMERIC ITEM LOOKUP
        // ============================================================

        internal static Item GetItemById(
            int itemId)
        {
            switch (itemId)
            {
                case WeatheredNoteItemId:

                    return _weatheredNote;

                default:

                    return null;
            }
        }


        // ============================================================
        // ITEM IDENTIFICATION
        // ============================================================

        internal static bool IsNoteItem(
            Item item)
        {
            if (item == null ||
                string.IsNullOrEmpty(
                    item.Id))
            {
                return false;
            }

            return string.Equals(
                item.Id,
                WeatheredNoteId,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}