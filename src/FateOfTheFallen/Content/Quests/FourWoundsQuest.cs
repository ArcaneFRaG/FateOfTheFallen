using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 11 - Four Wounds.
    ///
    /// This is the synthesis beat after the four world-boss quests. There is
    /// deliberately no item hand-in: Sereth already received and examined the
    /// four relics during Quests 7-10. The player's single action is to return
    /// to Sereth and ask about the phrase "four wounds".
    /// </summary>
    internal static class FourWoundsQuest
    {
        internal const string QuestId =
            "FOTF_MAIN_11_FOUR_WOUNDS";

        internal const string QuestName =
            "Four Wounds";

        private static Quest _quest;

        internal static Quest Quest =>
            _quest;


        internal static void Register()
        {
            if (!FOTFExpansionGate.IsUnlocked)
            {
                return;
            }

            if (_quest == null)
            {
                _quest =
                    CreateQuest();

                if (_quest == null)
                {
                    Plugin.NativeLog.LogError(
                        "Fate of the Fallen: failed to create Quest 11 [Four Wounds].");

                    return;
                }
            }

            // Quest 11 follows Gorath directly. Reassert the link whenever
            // registration runs so native QuestDB refreshes cannot break the
            // Q10 -> Q11 handoff.
            Quest gorath =
                GorathQuest.Quest;

            if (gorath != null)
            {
                gorath.AssignNewQuestOnComplete =
                    _quest;
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: Quest 11 registered before Quest 10 was available; Quest 10 -> Quest 11 will retry.");
            }

            CustomQuestRegistry.Register(
                _quest);
        }


        private static Quest CreateQuest()
        {
            Quest quest =
                ScriptableObject.CreateInstance<Quest>();

            if (quest == null)
            {
                return null;
            }

            quest.name =
                "FOTF Quest 11 - Four Wounds";

            quest.DBName =
                QuestId;

            quest.QuestName =
                QuestName;

            quest.QuestDesc =
                "With Gorath defeated, Archivist Sereth now has evidence from all four champions: " +
                "Moldermaw's Blighted Core, Nhal'Sera's Shard of Lightless Glass, Varkesh's Soul Fragment, " +
                "and Gorath's Infernal Heart. The relics do not share one corruption. Instead, each seems " +
                "to pull against the same damaged boundary from a different direction.\n\n" +
                "Return to Archivist Sereth in Port Azure and ask him what the four relics reveal.\n\n" +
                "Say: four wounds";

            quest.RequiredItems =
                new List<Item>();

            quest.DialogOnSuccess =
                "They are four wounds, but they were not made by one power. Each relic answers to a different place beyond our boundary. " +
                "The Blight, the Void, the power that held Varkesh's soul, and the infernal pressure behind Gorath are separate forces, each pressing against the same damaged ward.\n\n" +
                "The oldest records have a name for the powers confined beyond those wounds. Not their true names—a classification: the Fallen. " +
                "Someone went to considerable effort to erase the rest. If we are going to understand what is trying to cross into Erenshor, " +
                "we need the names that were buried.";

            quest.DialogOnPartialSuccess =
                "I have the four relics together now. Ask me what I mean by the four wounds.";

            quest.AssignThisQuestOnPartialComplete =
                false;

            quest.XPonComplete =
                0;

            quest.GoldOnComplete =
                0;

            quest.ItemOnComplete =
                null;

            quest.repeatable =
                false;

            quest.DisableQuest =
                false;

            quest.DisableText =
                string.Empty;

            // Quest 12 attaches during NamesThatWereBuriedQuest.Register().
            quest.AssignNewQuestOnComplete =
                null;

            quest.AffectFactions =
                new List<WorldFaction>();

            quest.AffectFactionAmts =
                new List<float>();

            quest.CompleteOtherQuests =
                new List<Quest>();

            quest.KillTurnInHolder =
                false;

            quest.DestroyTurnInHolder =
                false;

            quest.DropInvulnOnHolder =
                false;

            quest.OncePerSpawnInstance =
                false;

            quest.SetAchievementOnGet =
                string.Empty;

            quest.SetAchievementOnFinish =
                string.Empty;

            quest.UnlockItemForVendor =
                null;

            quest.hideFlags =
                HideFlags.HideAndDontSave;

            return quest;
        }


        internal static bool IsActive()
        {
            return
                ContainsQuestId(
                    GameData.HasQuest,
                    QuestId);
        }


        internal static bool IsComplete()
        {
            return
                ContainsQuestId(
                    GameData.CompletedQuests,
                    QuestId);
        }


        internal static bool TryAssign()
        {
            Register();

            if (_quest == null)
            {
                return false;
            }

            if (IsComplete() ||
                IsActive())
            {
                return true;
            }

            bool nativeResolved =
                GameData.QuestDB != null &&
                GameData.QuestDB.GetQuestByName(
                    QuestId) != null;

            if (nativeResolved)
            {
                try
                {
                    GameData.AssignQuest(
                        QuestId);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogWarning(
                        "[Quest11] native AssignQuest threw; using direct active-quest fallback. " +
                        exception.Message);
                }
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest11] QuestDB does not yet resolve [" +
                    QuestId +
                    "]; applying direct active-quest fallback.");
            }

            if (!IsActive() &&
                !IsComplete())
            {
                AddQuestId(
                    GameData.HasQuest,
                    QuestId);
            }

            Plugin.NativeLog.LogInfo(
                "[Quest11] assignment result. Active=[" +
                IsActive() +
                "] Complete=[" +
                IsComplete() +
                "] QuestDbResolved=[" +
                nativeResolved +
                "].");

            return IsActive();
        }


        internal static void EnsureChainState()
        {
            if (!FOTFExpansionGate.IsUnlocked)
            {
                return;
            }

            Register();

            // Repair characters that completed Gorath before Quest 11 was
            // added, or whose native AssignNewQuestOnComplete did not fire.
            if (GorathQuest.IsComplete() &&
                !IsActive() &&
                !IsComplete())
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest11] Quest 10 is complete but Quest 11 is not active; repairing quest-chain state.");

                TryAssign();
            }
        }


        internal static bool TryComplete()
        {
            Register();

            if (IsComplete())
            {
                return true;
            }

            if (!IsActive())
            {
                return false;
            }

            bool nativeResolved =
                GameData.QuestDB != null &&
                GameData.QuestDB.GetQuestByName(
                    QuestId) != null;

            if (nativeResolved)
            {
                try
                {
                    GameData.FinishQuest(
                        QuestId);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogWarning(
                        "[Quest11] native FinishQuest threw; using completed-quest fallback. " +
                        exception.Message);
                }
            }

            if (!IsComplete())
            {
                RemoveQuestId(
                    GameData.HasQuest,
                    QuestId);

                AddQuestId(
                    GameData.CompletedQuests,
                    QuestId);
            }

            if (!IsComplete())
            {
                return false;
            }

            RemoveQuestId(
                GameData.HasQuest,
                QuestId);

            PlayQuestCompleteSound();

            // Reassert/repair the next link immediately so characters that
            // complete Q11 through the dialogue keyword receive Q12 without
            // needing a database reload or another scene transition.
            NamesThatWereBuriedQuest.Register();
            NamesThatWereBuriedQuest.EnsureChainState();

            Plugin.NativeLog.LogInfo(
                "[Quest11] Four Wounds completed. Quest 12 [Names That Were Buried] Active=[" +
                NamesThatWereBuriedQuest.IsActive() +
                "] Complete=[" +
                NamesThatWereBuriedQuest.IsComplete() +
                "].");

            return true;
        }


        private static bool AddQuestId(
            object collection,
            string id)
        {
            if (collection == null ||
                string.IsNullOrEmpty(id))
            {
                return false;
            }

            if (ContainsQuestId(
                    collection,
                    id))
            {
                return true;
            }

            try
            {
                System.Collections.IList list =
                    collection as System.Collections.IList;

                if (list != null)
                {
                    list.Add(
                        id);

                    return
                        ContainsQuestId(
                            collection,
                            id);
                }

                var add =
                    AccessTools.Method(
                        collection.GetType(),
                        "Add",
                        new Type[]
                        {
                            typeof(string)
                        });

                if (add == null)
                {
                    return false;
                }

                add.Invoke(
                    collection,
                    new object[]
                    {
                        id
                    });

                return
                    ContainsQuestId(
                        collection,
                        id);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Quest11] failed adding quest state [" +
                    id +
                    "]: " +
                    exception);

                return false;
            }
        }


        private static bool RemoveQuestId(
            object collection,
            string id)
        {
            if (collection == null ||
                string.IsNullOrEmpty(id))
            {
                return false;
            }

            try
            {
                System.Collections.IList list =
                    collection as System.Collections.IList;

                if (list != null)
                {
                    for (int i =
                            list.Count - 1;
                         i >= 0;
                         i--)
                    {
                        if (string.Equals(
                                list[i] as string,
                                id,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            list.RemoveAt(
                                i);
                        }
                    }

                    return
                        !ContainsQuestId(
                            collection,
                            id);
                }

                var remove =
                    AccessTools.Method(
                        collection.GetType(),
                        "Remove",
                        new Type[]
                        {
                            typeof(string)
                        });

                if (remove == null)
                {
                    return false;
                }

                remove.Invoke(
                    collection,
                    new object[]
                    {
                        id
                    });

                return
                    !ContainsQuestId(
                        collection,
                        id);
            }
            catch
            {
                return false;
            }
        }


        private static bool ContainsQuestId(
            object collection,
            string id)
        {
            if (collection == null ||
                string.IsNullOrEmpty(id))
            {
                return false;
            }

            try
            {
                System.Collections.IList list =
                    collection as System.Collections.IList;

                if (list != null)
                {
                    for (int i = 0;
                         i < list.Count;
                         i++)
                    {
                        if (string.Equals(
                                list[i] as string,
                                id,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                var contains =
                    AccessTools.Method(
                        collection.GetType(),
                        "Contains",
                        new Type[]
                        {
                            typeof(string)
                        });

                if (contains == null)
                {
                    return false;
                }

                object result =
                    contains.Invoke(
                        collection,
                        new object[]
                        {
                            id
                        });

                return
                    result is bool &&
                    (bool)result;
            }
            catch
            {
                return false;
            }
        }


        private static void PlayQuestCompleteSound()
        {
            try
            {
                if (GameData.PlayerAud == null ||
                    GameData.Misc == null ||
                    GameData.Misc.QuestComplete == null)
                {
                    return;
                }

                GameData.PlayerAud.PlayOneShot(
                    GameData.Misc.QuestComplete,
                    GameData.PlayerAud.volume *
                    GameData.SFXVol *
                    GameData.MasterVol);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest11] failed to play quest completion sound: " +
                    exception.Message);
            }
        }
    }
}
