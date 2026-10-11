using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 13 - The Weakest Wall.
    ///
    /// With the four Fallen identified, Sereth compares the order in which
    /// their influence became manifest. Moldermaw appeared first, so the
    /// player's single action is to ask Sereth which prison is failing fastest.
    /// </summary>
    internal static class TheWeakestWallQuest
    {
        internal const string QuestId =
            "FOTF_MAIN_13_THE_WEAKEST_WALL";

        internal const string QuestName =
            "The Weakest Wall";

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
                        "Fate of the Fallen: failed to create Quest 13 [The Weakest Wall].");

                    return;
                }
            }

            // Quest 13 follows Names That Were Buried directly. Reassert the
            // link whenever registration runs so QuestDB refreshes cannot
            // break the Q12 -> Q13 handoff.
            Quest namesThatWereBuried =
                NamesThatWereBuriedQuest.Quest;

            if (namesThatWereBuried != null)
            {
                namesThatWereBuried.AssignNewQuestOnComplete =
                    _quest;
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: Quest 13 registered before Quest 12 was available; Quest 12 -> Quest 13 will retry.");
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
                "FOTF Quest 13 - The Weakest Wall";

            quest.DBName =
                QuestId;

            quest.QuestName =
                QuestName;

            quest.QuestDesc =
                "The hidden inscriptions identify four Fallen and the four prisons once guarded by the keepers. " +
                "Sereth believes the order in which their influence appeared may reveal which prison is failing first.\n\n" +
                "Return to Archivist Sereth in Port Azure and ask him which wall is weakest.\n\n" +
                "Say: weakest wall";

            quest.RequiredItems =
                new List<Item>();

            quest.DialogOnSuccess =
                "The Blight prison. Moldermaw was the first of the four champions to rear its head, before Nhal'Sera, Varkesh, or Gorath. " +
                "That order matters. If all four prisons were failing at the same rate, I would expect their influence to surface together. It did not.\n\n" +
                "The Lord of Blight found purchase first. Its prison is weakening faster than the others, and every day we spend treating the four wounds as equal gives that one more time to open. " +
                "If we are going to act before a Fallen crosses the boundary itself, the Blight prison is where we begin.";

            quest.DialogOnPartialSuccess =
                "The order matters. Ask me which of the four prisons I believe is the weakest wall.";

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

            // Quest 14 [The Fracturing] attaches during
            // TheFracturingQuest.Register().
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
                        "[Quest13] native AssignQuest threw; using direct active-quest fallback. " +
                        exception.Message);
                }
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest13] QuestDB does not yet resolve [" +
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
                "[Quest13] assignment result. Active=[" +
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

            // Repair characters that completed Quest 12 before Quest 13 was
            // added, or whose native AssignNewQuestOnComplete did not fire.
            if (NamesThatWereBuriedQuest.IsComplete() &&
                !IsActive() &&
                !IsComplete())
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest13] Quest 12 is complete but Quest 13 is not active; repairing quest-chain state.");

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
                        "[Quest13] native FinishQuest threw; using completed-quest fallback. " +
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

            Plugin.NativeLog.LogInfo(
                "[Quest13] The Weakest Wall completed. Sereth identified the Lord of Blight's prison as the fastest-weakening prison because Moldermaw manifested first.");

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
                    "[Quest13] failed adding quest state [" +
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
                    "[Quest13] failed to play quest completion sound: " +
                    exception.Message);
            }
        }
    }
}
