using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 9 - Varkesh the Unburied.
    /// Directly follows Quest 8: Nhal'Sera.
    /// </summary>
    internal static class VarkeshQuest
    {
        internal const string QuestId = "FOTF_MAIN_09_VARKESH";
        internal const string QuestName = "Varkesh the Unburied";

        internal const string ObjectiveArmedCreditId =
            "FOTF_CREDIT_Q09_VARKESH_OBJECTIVE_ARMED";

        internal const string DefeatCreditId =
            "FOTF_CREDIT_Q09_VARKESH_DEFEATED";

        private static Quest _quest;

        internal static Quest Quest => _quest;

        internal static void Register()
        {
            if (!FOTFExpansionGate.IsUnlocked)
                return;

            if (_quest == null)
            {
                _quest = CreateQuest();

                if (_quest == null)
                {
                    Plugin.NativeLog.LogError(
                        "Fate of the Fallen: failed to create Quest 9 [Varkesh the Unburied].");
                    return;
                }
            }

            Quest previous = NhalSeraQuest.Quest;

            if (previous != null)
            {
                previous.AssignNewQuestOnComplete = _quest;
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: Quest 9 registered before Quest 8 was available; Quest 8 -> Quest 9 will retry.");
            }

            // Link the canonical Soul Fragment before and after native quest
            // registration. This mirrors the hardened Quest 7/8 turn-in path.
            VarkeshQuestContent.LinkQuestRequirement();

            CustomQuestRegistry.Register(
                _quest);

            VarkeshQuestContent.LinkQuestRequirement();
        }

        private static Quest CreateQuest()
        {
            Quest quest = ScriptableObject.CreateInstance<Quest>();
            if (quest == null)
                return null;

            quest.name = "FOTF Quest 09 - Varkesh the Unburied";
            quest.DBName = QuestId;
            quest.QuestName = QuestName;

            quest.QuestDesc =
                "Nhal'Sera confirmed that the second fracture can alter the rules around matter itself. " +
                "The third disturbance is centered on the dead, but what remains is not ordinary necromancy.\n\n" +
                "Varkesh the Unburied is moving again, but his soul appears to have been caught before it could pass beyond reach. " +
                "Something is forcing that soul back into a body that should have remained dead.\n\n" +
                "Varkesh the Unburied has risen in Willowwatch Ridge. Search the ridge for the place where the dead refuse to remain still, defeat Varkesh, recover Varkesh's Soul Fragment from his remains, and return it to Archivist Sereth in Port Azure.";

            quest.RequiredItems =
                new List<Item>();

            Item requiredFragment =
                VarkeshSoulFragment.GetItem();

            if (requiredFragment != null)
            {
                quest.RequiredItems.Add(
                    requiredFragment);
            }

            quest.DialogOnSuccess =
                "This fragment should have passed beyond our reach with Varkesh. Instead, something caught it between death and whatever comes after. " +
                "The soul was not summoned back after the fact; it was prevented from leaving in the first place.\n\n" +
                "That distinction matters. The third fracture is not simply raising corpses. It is interfering with the passage of the soul itself.";

            quest.DialogOnPartialSuccess =
                "Varkesh is still active in Willowwatch Ridge. Defeat him, recover Varkesh's Soul Fragment from the corpse, and bring the fragment back to me.";

            quest.AssignThisQuestOnPartialComplete = false;
            quest.XPonComplete = 0;
            quest.GoldOnComplete = 0;
            quest.ItemOnComplete = null;
            quest.repeatable = false;
            quest.DisableQuest = false;
            quest.DisableText = string.Empty;
            quest.AssignNewQuestOnComplete = null; // GorathQuest.Register() attaches Quest 10.
            quest.AffectFactions = new List<WorldFaction>();
            quest.AffectFactionAmts = new List<float>();
            quest.CompleteOtherQuests = new List<Quest>();
            quest.KillTurnInHolder = false;
            quest.DestroyTurnInHolder = false;
            quest.DropInvulnOnHolder = false;
            quest.OncePerSpawnInstance = false;
            quest.SetAchievementOnGet = string.Empty;
            quest.SetAchievementOnFinish = string.Empty;
            quest.UnlockItemForVendor = null;
            quest.hideFlags = HideFlags.HideAndDontSave;

            return quest;
        }

        internal static bool IsActive()
        {
            return ContainsQuestId(GameData.HasQuest, QuestId);
        }

        internal static bool IsComplete()
        {
            return ContainsQuestId(GameData.CompletedQuests, QuestId);
        }

        internal static bool HasDefeatedVarkesh()
        {
            return
                FOTFCharacterMilestoneStore.HasCredit(ObjectiveArmedCreditId) &&
                FOTFCharacterMilestoneStore.HasCredit(DefeatCreditId);
        }

        internal static bool TryAssign()
        {
            Register();

            if (_quest == null)
                return false;

            if (IsComplete())
                return true;

            if (IsActive())
            {
                ArmObjectiveIfNeeded();
                return true;
            }

            bool nativeResolved =
                GameData.QuestDB != null &&
                GameData.QuestDB.GetQuestByName(QuestId) != null;

            if (nativeResolved)
            {
                try
                {
                    GameData.AssignQuest(QuestId);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogWarning(
                        "[Quest9] native AssignQuest threw; using direct active-quest fallback. " +
                        exception.Message);
                }
            }

            if (!IsActive() && !IsComplete())
                AddQuestId(GameData.HasQuest, QuestId);

            if (IsActive())
                ArmObjectiveIfNeeded();

            Plugin.NativeLog.LogInfo(
                "[Quest9] assignment result. Active=[" +
                IsActive() + "] Complete=[" + IsComplete() + "].");

            return IsActive();
        }

        internal static void EnsureChainState()
        {
            if (!FOTFExpansionGate.IsUnlocked)
                return;

            Register();

            if (IsActive())
            {
                ArmObjectiveIfNeeded();
                return;
            }

            if (NhalSeraQuest.IsComplete() && !IsComplete())
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest9] Quest 8 is complete but Quest 9 is not active; repairing quest-chain state.");

                TryAssign();
            }
        }

        internal static void NotifyVarkeshDefeated(string source)
        {
            if (!IsActive() || IsComplete())
                return;

            ArmObjectiveIfNeeded();

            if (!FOTFCharacterMilestoneStore.AwardCredit(DefeatCreditId))
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest9] Varkesh was defeated while Quest 9 was active, but objective credit could not be persisted.");
                return;
            }

            Plugin.NativeLog.LogInfo(
                "[Quest9] Varkesh the Unburied objective complete. Source=[" +
                (source ?? "<unknown>") + "].");

            AddSystemMessage(
                "Quest objective complete: Varkesh the Unburied defeated. Recover Varkesh's Soul Fragment and return to Archivist Sereth.",
                "yellow");
        }

        internal static bool TryComplete()
        {
            Register();

            if (IsComplete())
                return true;

            if (!IsActive() ||
                !HasDefeatedVarkesh() ||
                !VarkeshQuestContent.PlayerHasSoulFragment())
            {
                return false;
            }

            bool nativeResolved =
                GameData.QuestDB != null &&
                GameData.QuestDB.GetQuestByName(QuestId) != null;

            if (nativeResolved)
            {
                try
                {
                    GameData.FinishQuest(QuestId);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogWarning(
                        "[Quest9] native FinishQuest threw; using completed-quest fallback. " +
                        exception.Message);
                }
            }

            if (!IsComplete())
            {
                RemoveQuestId(GameData.HasQuest, QuestId);
                AddQuestId(GameData.CompletedQuests, QuestId);
            }

            if (!IsComplete())
                return false;

            RemoveQuestId(GameData.HasQuest, QuestId);

            // Native FinishQuest usually consumes RequiredItems. The direct
            // fallback does not, so remove the soul fragment if it is still present.
            if (VarkeshQuestContent.PlayerHasSoulFragment())
                VarkeshQuestContent.TryConsumeSoulFragment();

            PlayQuestCompleteSound();

            // Native FinishQuest normally follows AssignNewQuestOnComplete.
            // The direct completion fallback does not, so explicitly preserve
            // the Quest 9 -> Quest 10 handoff on both paths.
            GorathQuest.Register();
            GorathQuest.TryAssign();

            // Quest completion must never turn Varkesh into one-time content.
            // The permanent runtime ignores quest state and will restore the
            // boss once his normal 44-minute respawn timer has elapsed.
            VarkeshWorldSpawnRuntime.EnsurePermanentAvailability(
                "Quest 9 completed");

            Plugin.NativeLog.LogInfo(
                "[Quest9] Varkesh the Unburied completed. Quest 10 chain checked.");

            return true;
        }

        internal static void ResetObjectiveCredits()
        {
            FOTFCharacterMilestoneStore.RevokeCredit(ObjectiveArmedCreditId);
            FOTFCharacterMilestoneStore.RevokeCredit(DefeatCreditId);
        }

        private static void ArmObjectiveIfNeeded()
        {
            if (!IsActive() ||
                IsComplete() ||
                FOTFCharacterMilestoneStore.HasCredit(ObjectiveArmedCreditId))
                return;

            FOTFCharacterMilestoneStore.RevokeCredit(DefeatCreditId);

            if (!FOTFCharacterMilestoneStore.AwardCredit(ObjectiveArmedCreditId))
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest9] failed to persist Varkesh the Unburied objective armed marker.");
            }
        }

        private static bool AddQuestId(object collection, string id)
        {
            if (collection == null || string.IsNullOrEmpty(id))
                return false;

            if (ContainsQuestId(collection, id))
                return true;

            try
            {
                System.Collections.IList list = collection as System.Collections.IList;

                if (list != null)
                {
                    list.Add(id);
                    return ContainsQuestId(collection, id);
                }

                var add = AccessTools.Method(
                    collection.GetType(),
                    "Add",
                    new Type[] { typeof(string) });

                if (add == null)
                    return false;

                add.Invoke(collection, new object[] { id });
                return ContainsQuestId(collection, id);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Quest9] failed adding quest state [" + id + "]: " +
                    exception);
                return false;
            }
        }

        private static bool RemoveQuestId(object collection, string id)
        {
            if (collection == null || string.IsNullOrEmpty(id))
                return false;

            try
            {
                System.Collections.IList list = collection as System.Collections.IList;

                if (list != null)
                {
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        if (string.Equals(
                                list[i] as string,
                                id,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            list.RemoveAt(i);
                        }
                    }

                    return !ContainsQuestId(collection, id);
                }

                var remove = AccessTools.Method(
                    collection.GetType(),
                    "Remove",
                    new Type[] { typeof(string) });

                if (remove == null)
                    return false;

                remove.Invoke(collection, new object[] { id });
                return !ContainsQuestId(collection, id);
            }
            catch
            {
                return false;
            }
        }

        private static bool ContainsQuestId(object collection, string id)
        {
            if (collection == null || string.IsNullOrEmpty(id))
                return false;

            try
            {
                System.Collections.IList list = collection as System.Collections.IList;

                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (string.Equals(
                                list[i] as string,
                                id,
                                StringComparison.OrdinalIgnoreCase))
                            return true;
                    }

                    return false;
                }

                var contains = AccessTools.Method(
                    collection.GetType(),
                    "Contains",
                    new Type[] { typeof(string) });

                if (contains == null)
                    return false;

                object result = contains.Invoke(collection, new object[] { id });
                return result is bool && (bool)result;
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
                    "[Quest9] failed to play quest completion sound: " +
                    exception.Message);
            }
        }

        private static void AddSystemMessage(string text, string color)
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
