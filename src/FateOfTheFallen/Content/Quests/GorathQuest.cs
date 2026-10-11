using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 10 - Gorath.
    /// Directly follows Quest 9: Varkesh the Unburied.
    /// </summary>
    internal static class GorathQuest
    {
        internal const string QuestId = "FOTF_MAIN_10_GORATH";
        internal const string QuestName = "Gorath";

        internal const string ObjectiveArmedCreditId =
            "FOTF_CREDIT_Q10_GORATH_OBJECTIVE_ARMED";

        internal const string DefeatCreditId =
            "FOTF_CREDIT_Q10_GORATH_DEFEATED";

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
                        "Fate of the Fallen: failed to create Quest 10 [Gorath].");
                    return;
                }
            }

            Quest previous = VarkeshQuest.Quest;

            if (previous != null)
            {
                previous.AssignNewQuestOnComplete = _quest;
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: Quest 10 registered before Quest 9 was available; Quest 9 -> Quest 10 will retry.");
            }

            // Link the canonical Infernal Heart before and after native quest
            // registration. This mirrors the hardened Quest 7/8 turn-in path.
            GorathQuestContent.LinkQuestRequirement();

            CustomQuestRegistry.Register(
                _quest);

            GorathQuestContent.LinkQuestRequirement();
        }

        private static Quest CreateQuest()
        {
            Quest quest = ScriptableObject.CreateInstance<Quest>();
            if (quest == null)
                return null;

            quest.name = "FOTF Quest 10 - Gorath";
            quest.DBName = QuestId;
            quest.QuestName = QuestName;

            quest.QuestDesc =
                "Three of the four disturbances have now produced champions tied to powers beyond Erenshor. " +
                "The final fracture is infernal. Its signature matches the scorched breach examined earlier, but it is now concentrated around Gorath.\n\n" +
                "Gorath has appeared in Vitheo's Watch, where the infernal fracture has concentrated around the old watchlands. Find and defeat Gorath there. Recover Gorath's Infernal Heart from his remains and return it to Archivist Sereth in Port Azure.";

            quest.RequiredItems =
                new List<Item>();

            Item requiredHeart =
                GorathInfernalHeart.GetItem();

            if (requiredHeart != null)
            {
                quest.RequiredItems.Add(
                    requiredHeart);
            }

            quest.DialogOnSuccess =
                "This is the fourth signature. Blight, Void, undeath, and now the infernal. Four different powers are pressing through the same damaged boundary.";

            quest.DialogOnPartialSuccess =
                "Gorath is still active in Vitheo's Watch. Defeat him, recover Gorath's Infernal Heart from the corpse, and bring the heart back to me.";

            quest.AssignThisQuestOnPartialComplete = false;
            quest.XPonComplete = 0;
            quest.GoldOnComplete = 0;
            quest.ItemOnComplete = null;
            quest.repeatable = false;
            quest.DisableQuest = false;
            quest.DisableText = string.Empty;
            // Quest 11 attaches during FourWoundsQuest.Register().
            quest.AssignNewQuestOnComplete = null;
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

        internal static bool HasDefeatedGorath()
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
                        "[Quest10] native AssignQuest threw; using direct active-quest fallback. " +
                        exception.Message);
                }
            }

            if (!IsActive() && !IsComplete())
                AddQuestId(GameData.HasQuest, QuestId);

            if (IsActive())
                ArmObjectiveIfNeeded();

            Plugin.NativeLog.LogInfo(
                "[Quest10] assignment result. Active=[" +
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

            if (VarkeshQuest.IsComplete() && !IsComplete())
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest10] Quest 9 is complete but Quest 10 is not active; repairing quest-chain state.");

                TryAssign();
            }
        }

        internal static void NotifyGorathDefeated(string source)
        {
            if (!IsActive() || IsComplete())
                return;

            ArmObjectiveIfNeeded();

            if (!FOTFCharacterMilestoneStore.AwardCredit(DefeatCreditId))
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest10] Gorath was defeated while Quest 10 was active, but objective credit could not be persisted.");
                return;
            }

            Plugin.NativeLog.LogInfo(
                "[Quest10] Gorath objective complete. Source=[" +
                (source ?? "<unknown>") + "].");

            AddSystemMessage(
                "Quest objective complete: Gorath defeated. Recover Gorath's Infernal Heart and return to Archivist Sereth.",
                "yellow");
        }

        internal static bool TryComplete()
        {
            Register();

            if (IsComplete())
                return true;

            if (!IsActive() ||
                !HasDefeatedGorath() ||
                !GorathQuestContent.PlayerHasInfernalHeart())
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
                        "[Quest10] native FinishQuest threw; using completed-quest fallback. " +
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
            // fallback does not, so remove the infernal heart if it is still present.
            if (GorathQuestContent.PlayerHasInfernalHeart())
                GorathQuestContent.TryConsumeInfernalHeart();

            PlayQuestCompleteSound();

            // Native FinishQuest normally follows AssignNewQuestOnComplete,
            // but the direct quest-state fallback does not. Explicitly ensure
            // Quest 11 is present so both completion paths remain identical.
            FourWoundsQuest.Register();
            FourWoundsQuest.TryAssign();

            // Quest completion must never turn Gorath into one-time content.
            // The permanent runtime ignores quest state and will restore the
            // boss once his normal 44-minute respawn timer has elapsed.
            GorathWorldSpawnRuntime.EnsurePermanentAvailability(
                "Quest 10 completed");

            Plugin.NativeLog.LogInfo(
                "[Quest10] Gorath completed. Quest 11 chain checked.");
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
                    "[Quest10] failed to persist Gorath objective armed marker.");
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
                    "[Quest10] failed adding quest state [" + id + "]: " +
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
                    "[Quest10] failed to play quest completion sound: " +
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
