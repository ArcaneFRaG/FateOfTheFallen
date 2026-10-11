using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 7 - Moldermaw.
    ///
    /// This is the first direct world-boss quest after The Pattern Beneath.
    /// Moldermaw is a rock golem corrupted and empowered by Blight. It is not
    /// an organic creature and this quest deliberately avoids the discarded
    /// "feeding / flesh / root-devoured" story direction.
    ///
    /// The boss kill is tracked with a quest-scoped character milestone so a
    /// Moldermaw kill from before Quest 7 was accepted cannot satisfy the quest.
    /// The quest completes only when the player returns to Archivist Sereth.
    /// </summary>
    internal static class MoldermawQuest
    {
        internal const string QuestId =
            "FOTF_MAIN_07_MOLDERMAW";

        internal const string QuestName =
            "Moldermaw";

        internal const string ObjectiveArmedCreditId =
            "FOTF_CREDIT_Q07_MOLDERMAW_OBJECTIVE_ARMED";

        internal const string DefeatCreditId =
            "FOTF_CREDIT_Q07_MOLDERMAW_DEFEATED";

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
                        "Fate of the Fallen: failed to create Quest 7 [Moldermaw].");

                    return;
                }
            }

            // Quest 7 now follows Quest 6 directly. Reassert the link whenever
            // registration runs so database refreshes cannot break the chain.
            Quest patternBeneath =
                ThePatternBeneathQuest.Quest;

            if (patternBeneath != null)
            {
                patternBeneath.AssignNewQuestOnComplete =
                    _quest;
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: Quest 7 registered before Quest 6 was available; Quest 6 -> Quest 7 will retry on the next registration pass.");
            }

            CustomQuestRegistry.Register(
                _quest);

            MoldermawQuestContent
                .LinkQuestRequirement();
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
                "FOTF Quest 07 - Moldermaw";

            quest.DBName =
                QuestId;

            quest.QuestName =
                QuestName;

            quest.QuestDesc =
                "The fractured warding system has revealed four separate pressures pushing into Erenshor. " +
                "The Blight is the first to produce something large enough to threaten the world directly.\n\n" +
                "A massive stone golem has become saturated with Blight corruption. Green fissures burn " +
                "through its rock body and the force behind the Blight appears to be driving the construct " +
                "with purpose. Archivist Sereth has named the golem Moldermaw.\n\n" +
                "Moldermaw has been sighted in Loomingwood Forest, where the Blight has taken hold of the old stone and root-choked paths. Find and defeat Moldermaw there. Recover Moldermaw's Blighted Core from its remains and return the Core to Archivist Sereth in Port Azure.";

            quest.RequiredItems =
                new List<Item>();

            quest.DialogOnSuccess =
                "A stone construct. No flesh, no blood, nothing alive for the Blight to infect in the ordinary sense—" +
                "and yet it moved under the corruption's direction. That tells us the breach is doing more than allowing " +
                "foreign matter to seep through. Power from the other side can take hold of what is already here and give " +
                "it purpose.\n\n" +
                "Moldermaw was the Blight's first answer. We need to learn what the other fractures are capable of.";

            quest.DialogOnPartialSuccess =
                "Moldermaw is not a beast. It is a stone golem, now split by Blight-filled fissures and driven by the force " +
                "behind that corruption. Search Loomingwood Forest, destroy it, recover Moldermaw's Blighted Core from the remains, and bring the Core to me.";

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

            // Quest 8 will attach itself here when implemented.
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


        internal static bool HasDefeatedMoldermaw()
        {
            // The armed marker prevents an old/pre-quest test kill from
            // satisfying this objective after Quest 7 is first assigned.
            return
                FOTFCharacterMilestoneStore.HasCredit(
                    ObjectiveArmedCreditId) &&
                FOTFCharacterMilestoneStore.HasCredit(
                    DefeatCreditId);
        }


        internal static bool TryAssign()
        {
            Register();

            if (_quest == null)
            {
                return false;
            }

            if (IsComplete())
            {
                return true;
            }

            if (IsActive())
            {
                ArmObjectiveIfNeeded();
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
                        "[Quest7] native AssignQuest threw; attempting direct active-quest fallback. " +
                        exception.Message);
                }
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest7] QuestDB does not yet resolve [" +
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

            if (IsActive())
            {
                ArmObjectiveIfNeeded();
            }

            bool active =
                IsActive();

            Plugin.NativeLog.LogInfo(
                "[Quest7] assignment result. Active=[" +
                active +
                "] Complete=[" +
                IsComplete() +
                "] QuestDbResolved=[" +
                nativeResolved +
                "].");

            return active;
        }


        internal static void EnsureChainState()
        {
            if (!FOTFExpansionGate.IsUnlocked)
            {
                return;
            }

            Register();

            if (IsActive())
            {
                ArmObjectiveIfNeeded();
                return;
            }

            // Repair characters that completed Quest 6 before this direct
            // world-boss Quest 7 existed.
            if (ThePatternBeneathQuest.IsComplete() &&
                !IsComplete())
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest7] Quest 6 is complete but Quest 7 is not active; repairing quest-chain state.");

                TryAssign();
            }
        }


        internal static void NotifyMoldermawDefeated(
            string source)
        {
            if (!IsActive() ||
                IsComplete())
            {
                return;
            }

            ArmObjectiveIfNeeded();

            bool awarded =
                FOTFCharacterMilestoneStore.AwardCredit(
                    DefeatCreditId);

            if (!awarded)
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest7] Moldermaw was defeated while Quest 7 was active, but the quest objective credit could not be persisted.");

                return;
            }

            Plugin.NativeLog.LogInfo(
                "[Quest7] Moldermaw objective complete. Source=[" +
                (source ?? "<unknown>") +
                "].");

            AddSystemMessage(
                "Quest objective complete: Moldermaw defeated. Return to Archivist Sereth in Port Azure.",
                "yellow");
        }


        internal static bool TryComplete()
        {
            Register();

            if (IsComplete())
            {
                return true;
            }

            if (!IsActive() ||
                !HasDefeatedMoldermaw() ||
                !MoldermawQuestContent.PlayerHasCore())
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
                        "[Quest7] native FinishQuest threw; attempting direct completed-quest fallback. " +
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

            // Native FinishQuest normally consumes RequiredItems. The direct
            // completed-quest fallback does not, so consume the Core if it is
            // still in inventory.
            if (MoldermawQuestContent.PlayerHasCore())
            {
                MoldermawQuestContent.TryConsumeCore();
            }

            PlayQuestCompleteSound();

            // Native FinishQuest normally follows AssignNewQuestOnComplete.
            // The direct fallback does not, so explicitly keep the world-boss
            // chain identical on both paths.
            NhalSeraQuest.Register();
            NhalSeraQuest.TryAssign();

            Plugin.NativeLog.LogInfo(
                "[Quest7] Moldermaw completed. Blighted Core hand-in consumed and Quest 8 chain checked.");

            return true;
        }


        /// <summary>
        /// Clears only Quest 7's objective markers. The general world-boss
        /// completion credit is deliberately left alone.
        /// </summary>
        internal static void ResetObjectiveCredits()
        {
            FOTFCharacterMilestoneStore.RevokeCredit(
                ObjectiveArmedCreditId);

            FOTFCharacterMilestoneStore.RevokeCredit(
                DefeatCreditId);
        }


        private static void ArmObjectiveIfNeeded()
        {
            if (!IsActive() ||
                IsComplete() ||
                FOTFCharacterMilestoneStore.HasCredit(
                    ObjectiveArmedCreditId))
            {
                return;
            }

            // Remove any stale objective credit left by an earlier development
            // build/test before arming this particular Quest 7 attempt.
            FOTFCharacterMilestoneStore.RevokeCredit(
                DefeatCreditId);

            if (!FOTFCharacterMilestoneStore.AwardCredit(
                    ObjectiveArmedCreditId))
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest7] failed to persist Moldermaw objective armed marker.");
            }
        }


        private static bool AddQuestId(
            object collection,
            string id)
        {
            if (collection == null ||
                string.IsNullOrEmpty(
                    id))
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

                    return ContainsQuestId(
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

                return ContainsQuestId(
                    collection,
                    id);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Quest7] failed adding quest state [" +
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
                string.IsNullOrEmpty(
                    id))
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
                        if (!string.Equals(
                                list[i] as string,
                                id,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        list.RemoveAt(
                            i);
                    }

                    return !ContainsQuestId(
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

                return !ContainsQuestId(
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
                string.IsNullOrEmpty(
                    id))
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
                    "[Quest7] failed to play quest completion sound: " +
                    exception.Message);
            }
        }


        private static void AddSystemMessage(
            string text,
            string color)
        {
            try
            {
                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        text,
                        ChatLogLine.LogType.SystemMessages,
                        color ?? ""));
            }
            catch
            {
            }
        }
    }
}
