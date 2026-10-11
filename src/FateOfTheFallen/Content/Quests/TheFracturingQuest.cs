using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 14 - The Fracturing.
    ///
    /// v0.2 finale. With the Lord of Blight identified as the first prison
    /// likely to fail, Sereth returns to the Fractured Warding Fragment from
    /// Quest 6 and isolates the Blight resonance within it. The player's
    /// single action is to ask Sereth about "the fracturing". Completion
    /// grants the persistent Blight Planar Attunement progression state, but
    /// does not open the Blighted Expanse yet.
    /// </summary>
    internal static class TheFracturingQuest
    {
        internal const string QuestId =
            "FOTF_MAIN_14_THE_FRACTURING";

        internal const string QuestName =
            "The Fracturing";

        internal const string FracturingCompleteFlagId =
            "FOTF_FRACTURING_COMPLETE";

        internal const string BlightPlanarAttunementId =
            "FOTF_ATTUNEMENT_BLIGHT_PLANAR";

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
                        "Fate of the Fallen: failed to create Quest 14 [The Fracturing].");

                    return;
                }
            }

            // Quest 14 follows The Weakest Wall directly. Reassert the
            // link whenever registration runs so QuestDB refreshes cannot
            // break the Q13 -> Q14 handoff.
            Quest weakestWall =
                TheWeakestWallQuest.Quest;

            if (weakestWall != null)
            {
                weakestWall.AssignNewQuestOnComplete =
                    _quest;
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: Quest 14 registered before Quest 13 was available; Quest 13 -> Quest 14 will retry.");
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
                "FOTF Quest 14 - The Fracturing";

            quest.DBName =
                QuestId;

            quest.QuestName =
                QuestName;

            quest.QuestDesc =
                "The Lord of Blight's prison is weakening faster than the other three. With the four Fallen identified and the order of their manifestations understood, Sereth has returned to the Fractured Warding Fragment recovered from the Ancient Warding Shrine. He believes the fracture can now be separated into four distinct planar signatures.\n\n" +
                "Return to Archivist Sereth in Port Azure and ask him what the widening Blight fracture means.\n\n" +
                "Say: the fracturing";

            quest.RequiredItems =
                new List<Item>();

            quest.DialogOnSuccess =
                "The fracture was never random. Now that we know what each pressure belongs to, the old warding fragment resolves into four distinct stresses—and the Blight line is wider than the others.\n\n" +
                "I have bound that resonance to your essence. You should be able to recognise the Lord of Blight's boundary now, even where it is too faint for ordinary senses. Call it a planar attunement if you like. It is not a key. It will not open the prison, and it will not carry you across.\n\n" +
                "But when we find a safe way through, you will know the path when it answers you. The surviving Warden records give the realm around that prison a name: the Blighted Expanse. That is where this road leads next.";

            quest.DialogOnPartialSuccess =
                "The warding fragment is reacting differently now that we know which pressure belongs to which Fallen. Ask me about the fracturing.";

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

            // v0.2 ends here. Future content will branch from the persistent
            // planar-attunement / fracturing progression state instead.
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


        internal static bool HasFracturingCompleteFlag()
        {
            return
                FOTFCharacterMilestoneStore.HasCredit(
                    FracturingCompleteFlagId);
        }


        internal static bool HasBlightPlanarAttunement()
        {
            return
                FOTFCharacterMilestoneStore.HasCredit(
                    BlightPlanarAttunementId);
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
                        "[Quest14] native AssignQuest threw; using direct active-quest fallback. " +
                        exception.Message);
                }
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest14] QuestDB does not yet resolve [" +
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
                "[Quest14] assignment result. Active=[" +
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

            // Repair characters that completed Quest 13 before Quest 14 was
            // added, or whose native AssignNewQuestOnComplete did not fire.
            if (TheWeakestWallQuest.IsComplete() &&
                !IsActive() &&
                !IsComplete())
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest14] Quest 13 is complete but Quest 14 is not active; repairing quest-chain state.");

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

            // These two markers are the actual v0.2 -> future-content handoff.
            // Award them before closing the native quest so a persistence
            // failure leaves the quest retryable instead of silently losing
            // the attunement state.
            if (!FOTFCharacterMilestoneStore.AwardCredit(
                    BlightPlanarAttunementId) ||
                !FOTFCharacterMilestoneStore.AwardCredit(
                    FracturingCompleteFlagId))
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest14] could not persist Blight Planar Attunement / fracturing completion markers; quest completion deferred.");

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
                        "[Quest14] native FinishQuest threw; using completed-quest fallback. " +
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
                "[Quest14] The Fracturing completed. Blight Planar Attunement granted; the Blighted Expanse remains inaccessible in v0.2.");

            return true;
        }


        internal static void ResetProgressionFlags()
        {
            FOTFCharacterMilestoneStore.RevokeCredit(
                BlightPlanarAttunementId);

            FOTFCharacterMilestoneStore.RevokeCredit(
                FracturingCompleteFlagId);
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
                    "[Quest14] failed adding quest state [" +
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
                    "[Quest14] failed to play quest completion sound: " +
                    exception.Message);
            }
        }
    }
}
