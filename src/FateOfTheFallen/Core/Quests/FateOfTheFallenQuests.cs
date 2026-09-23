using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class FateOfTheFallenQuests
    {
        // ============================================================
        // QUEST IDS
        // ============================================================

        internal const string WeatheredNoteQuestId =
            "FOTF_TEST_WEATHERED_NOTE";


        // ============================================================
        // NATIVE ITEM NAMES
        // ============================================================

        private const string PlanarStoneItemName =
            "Planar Stone";


        // ============================================================
        // QUESTS
        // ============================================================

        private static Quest _weatheredNoteQuest;


        // ============================================================
        // ACCESSORS
        // ============================================================

        internal static Quest WeatheredNoteQuest
        {
            get
            {
                return _weatheredNoteQuest;
            }
        }


        // ============================================================
        // REGISTER
        // ============================================================

        internal static void Register()
        {
            if (_weatheredNoteQuest == null)
            {
                _weatheredNoteQuest =
                    CreateWeatheredNoteQuest();

                if (_weatheredNoteQuest == null)
                {
                    Plugin.NativeLog.LogError(
                        "Fate of the Fallen: failed to create Weathered Note quest.");

                    return;
                }
            }

            RegisterWithNativeQuestDatabase();

            Plugin.NativeLog.LogInfo(
                "Fate of the Fallen: registered quest [" +
                _weatheredNoteQuest.DBName +
                "] " +
                _weatheredNoteQuest.QuestName +
                ".");
        }


        // ============================================================
        // CREATE WEATHERED NOTE QUEST
        // ============================================================

        private static Quest CreateWeatheredNoteQuest()
        {
            Quest quest =
                ScriptableObject.CreateInstance<Quest>();

            if (quest == null)
            {
                return null;
            }


            // ========================================================
            // UNITY IDENTITY
            // ========================================================

            quest.name =
                "FOTF Quest - A Strange Discovery";


            // ========================================================
            // QUEST IDENTITY
            // ========================================================

            quest.DBName =
                WeatheredNoteQuestId;

            quest.QuestName =
                "A Strange Discovery";

            quest.QuestDesc =
                "A weathered note speaks of something stirring beneath " +
                "the ruins.\n\n" +
                "The warning may mean something to Kaelith the Blighted " +
                "in Azure. Bring the Weathered Note to him.";


            // ========================================================
            // REQUIRED ITEMS
            // ========================================================
            //
            // Weathered Note is linked later, after the item has been
            // created.
            // ========================================================

            quest.RequiredItems =
                new List<Item>();


            // ========================================================
            // KAELITH DIALOGUE
            // ========================================================

            quest.DialogOnSuccess =
                "Let me see that note...\n\n" +
                "Hm. This is no idle warning. I've seen markings like " +
                "these before, tied to things best left buried.\n\n" +
                "You did well bringing this to me. Take this Planar Stone. " +
                "If what is written here is true, you may have need of it.";

            quest.DialogOnPartialSuccess =
                "You mentioned a note. Bring it to me and I'll have a look.";

            quest.AssignThisQuestOnPartialComplete =
                false;


            // ========================================================
            // REWARDS
            // ========================================================
            //
            // Planar Stone is linked later from the native ItemDB.
            // ========================================================

            quest.XPonComplete =
                0;

            quest.GoldOnComplete =
                0;

            quest.ItemOnComplete =
                null;


            // ========================================================
            // QUEST BEHAVIOUR
            // ========================================================

            quest.repeatable =
                false;

            quest.DisableQuest =
                false;

            quest.DisableText =
                string.Empty;

            quest.AssignNewQuestOnComplete =
                null;


            // ========================================================
            // FACTION EFFECTS
            // ========================================================

            quest.AffectFactions =
                new List<WorldFaction>();

            quest.AffectFactionAmts =
                new List<float>();


            // ========================================================
            // OTHER QUEST EFFECTS
            // ========================================================

            quest.CompleteOtherQuests =
                new List<Quest>();


            // ========================================================
            // TURN-IN HOLDER BEHAVIOUR
            // ========================================================

            quest.KillTurnInHolder =
                false;

            quest.DestroyTurnInHolder =
                false;

            quest.DropInvulnOnHolder =
                false;

            quest.OncePerSpawnInstance =
                false;


            // ========================================================
            // ACHIEVEMENTS
            // ========================================================

            quest.SetAchievementOnGet =
                string.Empty;

            quest.SetAchievementOnFinish =
                string.Empty;


            // ========================================================
            // VENDOR UNLOCK
            // ========================================================

            quest.UnlockItemForVendor =
                null;


            // ========================================================
            // UNITY LIFETIME
            // ========================================================

            quest.hideFlags =
                HideFlags.HideAndDontSave;


            return quest;
        }


        // ============================================================
        // REGISTER WITH NATIVE QUEST DATABASE
        // ============================================================

        private static void RegisterWithNativeQuestDatabase()
        {
            if (_weatheredNoteQuest == null)
            {
                return;
            }

            if (GameData.QuestDB == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: GameData.QuestDB was null while registering custom quest.");

                return;
            }

            try
            {
                FieldInfo questDatabaseField =
                    AccessTools.Field(
                        GameData.QuestDB.GetType(),
                        "QuestDatabase");

                if (questDatabaseField == null)
                {
                    Plugin.NativeLog.LogError(
                        "Fate of the Fallen: could not find QuestDB.QuestDatabase field.");

                    return;
                }

                object databaseObject =
                    questDatabaseField.GetValue(
                        GameData.QuestDB);

                if (databaseObject == null)
                {
                    Plugin.NativeLog.LogError(
                        "Fate of the Fallen: native QuestDatabase field value was null.");

                    return;
                }


                // ========================================================
                // LIST<QUEST>
                // ========================================================

                List<Quest> questList =
                    databaseObject as List<Quest>;

                if (questList != null)
                {
                    for (int i = 0;
                         i < questList.Count;
                         i++)
                    {
                        Quest existingQuest =
                            questList[i];

                        if (existingQuest == null)
                        {
                            continue;
                        }

                        if (!string.Equals(
                                existingQuest.DBName,
                                WeatheredNoteQuestId,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (existingQuest !=
                            _weatheredNoteQuest)
                        {
                            questList[i] =
                                _weatheredNoteQuest;
                        }

                        Plugin.NativeLog.LogInfo(
                            "Fate of the Fallen: custom quest already present in native QuestDB [" +
                            WeatheredNoteQuestId +
                            "].");

                        return;
                    }

                    questList.Add(
                        _weatheredNoteQuest);

                    Plugin.NativeLog.LogInfo(
                        "Fate of the Fallen: added custom quest to native QuestDB List<Quest> [" +
                        WeatheredNoteQuestId +
                        "].");

                    return;
                }


                // ========================================================
                // QUEST[]
                // ========================================================

                Quest[] questArray =
                    databaseObject as Quest[];

                if (questArray != null)
                {
                    for (int i = 0;
                         i < questArray.Length;
                         i++)
                    {
                        Quest existingQuest =
                            questArray[i];

                        if (existingQuest == null)
                        {
                            continue;
                        }

                        if (!string.Equals(
                                existingQuest.DBName,
                                WeatheredNoteQuestId,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (existingQuest !=
                            _weatheredNoteQuest)
                        {
                            questArray[i] =
                                _weatheredNoteQuest;

                            questDatabaseField.SetValue(
                                GameData.QuestDB,
                                questArray);
                        }

                        Plugin.NativeLog.LogInfo(
                            "Fate of the Fallen: custom quest already present in native QuestDB Quest[] [" +
                            WeatheredNoteQuestId +
                            "].");

                        return;
                    }


                    Quest[] expandedArray =
                        new Quest[
                            questArray.Length + 1];

                    Array.Copy(
                        questArray,
                        expandedArray,
                        questArray.Length);

                    expandedArray[
                        expandedArray.Length - 1] =
                        _weatheredNoteQuest;

                    questDatabaseField.SetValue(
                        GameData.QuestDB,
                        expandedArray);

                    Plugin.NativeLog.LogInfo(
                        "Fate of the Fallen: added custom quest to native QuestDB Quest[] [" +
                        WeatheredNoteQuestId +
                        "]. Native count: " +
                        questArray.Length +
                        " -> " +
                        expandedArray.Length +
                        ".");

                    return;
                }


                // ========================================================
                // UNKNOWN DATABASE TYPE
                // ========================================================

                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: unsupported QuestDatabase field type [" +
                    databaseObject.GetType().FullName +
                    "].");
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: failed registering custom quest with native QuestDB: " +
                    exception);
            }
        }

        // ============================================================
        // LINK ITEMS AND REWARDS
        // ============================================================

        internal static void LinkItems()
        {
            if (_weatheredNoteQuest == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: cannot link quest data because Weathered Note quest is null.");

                return;
            }


            // ========================================================
            // REQUIRED ITEM - WEATHERED NOTE
            // ========================================================

            Item weatheredNote =
                FateOfTheFallenNotes.GetWeatheredNote();

            if (weatheredNote == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: Weathered Note [1700] was null while linking quest requirements.");
            }
            else
            {
                if (_weatheredNoteQuest.RequiredItems == null)
                {
                    _weatheredNoteQuest.RequiredItems =
                        new List<Item>();
                }

                if (!_weatheredNoteQuest.RequiredItems.Contains(
                        weatheredNote))
                {
                    _weatheredNoteQuest.RequiredItems.Add(
                        weatheredNote);
                }

                Plugin.NativeLog.LogInfo(
                    "Fate of the Fallen: Weathered Note [1700] linked as required item for quest [" +
                    _weatheredNoteQuest.DBName +
                    "].");
            }


            // ========================================================
            // REWARD - PLANAR STONE
            // ========================================================

            Item planarStone =
                FindNativeItemByName(
                    PlanarStoneItemName);

            if (planarStone == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: native Planar Stone could not be found. " +
                    "Quest reward will remain empty.");
            }
            else
            {
                _weatheredNoteQuest.ItemOnComplete =
                    planarStone;

                Plugin.NativeLog.LogInfo(
                    "Fate of the Fallen: quest reward linked: [" +
                    planarStone.ItemName +
                    "] ID [" +
                    planarStone.Id +
                    "].");
            }
        }


        // ============================================================
        // KAELITH QUEST TURN-IN
        // ============================================================

        internal static bool TryHandleKaelithTurnIn(
            out string dialogue)
        {
            dialogue =
                null;

            Quest quest =
                _weatheredNoteQuest;

            if (quest == null)
            {
                return false;
            }


            // ========================================================
            // ALREADY COMPLETE
            // ========================================================

            if (GameData.CompletedQuests != null &&
                GameData.CompletedQuests.Contains(
                    quest.DBName))
            {
                return false;
            }


            // ========================================================
            // QUEST MUST BE ACTIVE
            // ========================================================

            if (GameData.HasQuest == null ||
                !GameData.HasQuest.Contains(
                    quest.DBName))
            {
                return false;
            }


            // ========================================================
            // PLAYER INVENTORY
            // ========================================================

            if (GameData.PlayerInv == null)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: PlayerInv was null while attempting Weathered Note quest turn-in.");

                return false;
            }


            // ========================================================
            // VERIFY QUEST EXISTS IN NATIVE DATABASE
            // ========================================================

            if (GameData.QuestDB == null)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: QuestDB was null during Weathered Note quest turn-in.");

                return false;
            }

            Quest nativeQuest =
                GameData.QuestDB.GetQuestByName(
                    quest.DBName);

            if (nativeQuest == null)
            {
                RegisterWithNativeQuestDatabase();

                nativeQuest =
                    GameData.QuestDB.GetQuestByName(
                        quest.DBName);
            }

            if (nativeQuest == null)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: Weathered Note quest could not be resolved from native QuestDB.");

                return false;
            }


            // ========================================================
            // CHECK REQUIRED ITEMS
            // ========================================================

            if (quest.RequiredItems != null)
            {
                foreach (
                    Item requiredItem
                    in quest.RequiredItems)
                {
                    if (requiredItem == null)
                    {
                        continue;
                    }

                    bool playerHasItem =
                        GameData.PlayerInv.HasItem(
                            requiredItem,
                            false);

                    if (!playerHasItem)
                    {
                        dialogue =
                            !string.IsNullOrEmpty(
                                quest.DialogOnPartialSuccess)
                                ? quest.DialogOnPartialSuccess
                                : "You don't have what I need.";

                        return true;
                    }
                }
            }


            // ========================================================
            // VALIDATE REWARD BEFORE CONSUMING NOTE
            // ========================================================

            if (quest.ItemOnComplete == null)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: Weathered Note quest reward was null. " +
                    "Turn-in cancelled so the player does not lose the note.");

                dialogue =
                    "Something isn't right. Hold onto that note for now.";

                return true;
            }


            // ========================================================
            // REMOVE REQUIRED ITEMS
            // ========================================================

            List<Item> removedItems =
                new List<Item>();

            if (quest.RequiredItems != null)
            {
                foreach (
                    Item requiredItem
                    in quest.RequiredItems)
                {
                    if (requiredItem == null)
                    {
                        continue;
                    }

                    bool removed =
                        GameData.PlayerInv.HasItem(
                            requiredItem,
                            true);

                    if (!removed)
                    {
                        RestoreRemovedItems(
                            removedItems);

                        dialogue =
                            !string.IsNullOrEmpty(
                                quest.DialogOnPartialSuccess)
                                ? quest.DialogOnPartialSuccess
                                : "You don't have what I need.";

                        return true;
                    }

                    removedItems.Add(
                        requiredItem);
                }
            }


            // ========================================================
            // COMPLETE THROUGH NATIVE QUEST SYSTEM
            // ========================================================

            try
            {
                GameData.FinishQuest(
                    quest.DBName);
            }
            catch (Exception exception)
            {
                RestoreRemovedItems(
                    removedItems);

                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: native FinishQuest failed for [" +
                    quest.DBName +
                    "]: " +
                    exception);

                dialogue =
                    "Something isn't right. Hold onto that note for now.";

                return true;
            }


            // ========================================================
            // VERIFY COMPLETION
            // ========================================================

            if (GameData.CompletedQuests == null ||
                !GameData.CompletedQuests.Contains(
                    quest.DBName))
            {
                RestoreRemovedItems(
                    removedItems);

                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: FinishQuest returned without marking [" +
                    quest.DBName +
                    "] complete.");

                dialogue =
                    "Something isn't right. Hold onto that note for now.";

                return true;
            }


            // ========================================================
            // AWARD PLANAR STONE
            // ========================================================

            Item reward =
                quest.ItemOnComplete;

            bool rewardAdded =
                GameData.PlayerInv.AddItemToInv(
                    reward);

            if (!rewardAdded)
            {
                GameData.PlayerInv.ForceItemToInv(
                    reward);
            }

            Plugin.NativeLog.LogInfo(
                "Fate of the Fallen: completed quest [" +
                quest.DBName +
                "] and awarded [" +
                reward.ItemName +
                "].");


            // ========================================================
            // QUEST COMPLETE SOUND
            // ========================================================

            try
            {
                if (GameData.PlayerAud != null &&
                    GameData.Misc != null &&
                    GameData.Misc.QuestComplete != null)
                {
                    GameData.PlayerAud.PlayOneShot(
                        GameData.Misc.QuestComplete,
                        GameData.PlayerAud.volume *
                        GameData.SFXVol *
                        GameData.MasterVol);
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: failed to play quest completion sound: " +
                    exception.Message);
            }


            // ========================================================
            // SUCCESS DIALOGUE
            // ========================================================

            dialogue =
                !string.IsNullOrEmpty(
                    quest.DialogOnSuccess)
                    ? quest.DialogOnSuccess
                    : "You did well bringing this to me.";

            return true;
        }


        // ============================================================
        // RESTORE ITEMS IF TURN-IN FAILS
        // ============================================================

        private static void RestoreRemovedItems(
            List<Item> items)
        {
            if (items == null ||
                GameData.PlayerInv == null)
            {
                return;
            }

            foreach (Item item in items)
            {
                if (item == null)
                {
                    continue;
                }

                bool restored =
                    GameData.PlayerInv.AddItemToInv(
                        item);

                if (!restored)
                {
                    GameData.PlayerInv.ForceItemToInv(
                        item);
                }
            }
        }


        // ============================================================
        // NATIVE ITEM LOOKUP
        // ============================================================

        private static Item FindNativeItemByName(
            string itemName)
        {
            if (string.IsNullOrEmpty(
                    itemName))
            {
                return null;
            }

            if (GameData.ItemDB == null ||
                GameData.ItemDB.ItemDB == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: ItemDB unavailable while resolving [" +
                    itemName +
                    "].");

                return null;
            }

            Item[] nativeItems =
                GameData.ItemDB.ItemDB;

            for (int i = 0;
                 i < nativeItems.Length;
                 i++)
            {
                Item item =
                    nativeItems[i];

                if (item == null ||
                    string.IsNullOrEmpty(
                        item.ItemName))
                {
                    continue;
                }

                if (!string.Equals(
                        item.ItemName,
                        itemName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Plugin.NativeLog.LogInfo(
                    "Fate of the Fallen: resolved native item [" +
                    itemName +
                    "] at ItemDB index [" +
                    i +
                    "], ID [" +
                    item.Id +
                    "].");

                return item;
            }

            Plugin.NativeLog.LogWarning(
                "Fate of the Fallen: native item [" +
                itemName +
                "] was not found in ItemDB.");

            return null;
        }
    }
}