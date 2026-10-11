using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 8 - Nhal'Sera.
    /// Directly follows Quest 7: Moldermaw.
    /// </summary>
    internal static class NhalSeraQuest
    {
        internal const string QuestId = "FOTF_MAIN_08_NHALSERA";
        internal const string QuestName = "Nhal'Sera";

        internal const string ObjectiveArmedCreditId =
            "FOTF_CREDIT_Q08_NHALSERA_OBJECTIVE_ARMED";

        internal const string DefeatCreditId =
            "FOTF_CREDIT_Q08_NHALSERA_DEFEATED";

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
                        "Fate of the Fallen: failed to create Quest 8 [Nhal'Sera].");
                    return;
                }
            }

            Quest previous = MoldermawQuest.Quest;

            if (previous != null)
            {
                previous.AssignNewQuestOnComplete = _quest;
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: Quest 8 registered before Quest 7 was available; Quest 7 -> Quest 8 will retry.");
            }

            // Make sure the canonical Shard of Lightless Glass is attached
            // to the actual Quest object before it enters the native quest
            // registry, then reassert once more afterward in case the item
            // database only became available during registration.
            NhalSeraQuestContent.LinkQuestRequirement();

            CustomQuestRegistry.Register(_quest);

            NhalSeraQuestContent.LinkQuestRequirement();
        }

        private static Quest CreateQuest()
        {
            Quest quest = ScriptableObject.CreateInstance<Quest>();
            if (quest == null)
                return null;

            quest.name = "FOTF Quest 08 - Nhal'Sera";
            quest.DBName = QuestId;
            quest.QuestName = QuestName;

            quest.QuestDesc =
                "Moldermaw proved that the fractures are capable of more than leaking foreign matter into Erenshor. " +
                "The Void disturbance has now produced its own champion.\n\n" +
                "Nhal'Sera is a towering construct warped by a pressure that bends light, distance and position around it. " +
                "Unlike the Blight that animated Moldermaw, the Void seems to erode the boundary between where the construct is " +
                "and where it should be.\n\n" +
                "Nhal'Sera has manifested in Soluna's Landing, where the Void disturbance is bending light and distance around the landscape. Find and defeat Nhal'Sera there. Recover a Shard of Lightless Glass from its remains and return it to Archivist Sereth in Port Azure.";

            quest.RequiredItems = new List<Item>();

            // Attach the canonical quest-item singleton immediately when it is
            // already available. Register() will retry the link later if the
            // ItemDatabase has not initialized yet.
            Item requiredShard =
                ShardOfLightlessGlass.GetItem();

            if (requiredShard != null)
            {
                quest.RequiredItems.Add(
                    requiredShard);
            }

            quest.DialogOnSuccess =
                "This is not glass. It only resembles it because our eyes are trying to give shape to something that does not belong here. " +
                "The shard is cold, but the space around it is colder still. Light bends away from its surface and the edge refuses to remain " +
                "in exactly one place.\n\n" +
                "Moldermaw showed that the breaches can seize matter. Nhal'Sera shows that the Void can distort the rules that matter obeys. " +
                "That is a far more dangerous kind of pressure.";

            quest.DialogOnPartialSuccess =
                "Nhal'Sera is still active in Soluna's Landing. Destroy it, recover a Shard of Lightless Glass from the corpse, and bring the shard back to me.";

            quest.AssignThisQuestOnPartialComplete = false;
            quest.XPonComplete = 0;
            quest.GoldOnComplete = 0;
            quest.ItemOnComplete = null;
            quest.repeatable = false;
            quest.DisableQuest = false;
            quest.DisableText = string.Empty;
            quest.AssignNewQuestOnComplete = null; // Quest 9 attaches later.
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

        internal static bool HasDefeatedNhalSera()
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
                        "[Quest8] native AssignQuest threw; using direct active-quest fallback. " +
                        exception.Message);
                }
            }

            if (!IsActive() && !IsComplete())
                AddQuestId(GameData.HasQuest, QuestId);

            if (IsActive())
                ArmObjectiveIfNeeded();

            Plugin.NativeLog.LogInfo(
                "[Quest8] assignment result. Active=[" +
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

            if (MoldermawQuest.IsComplete() && !IsComplete())
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest8] Quest 7 is complete but Quest 8 is not active; repairing quest-chain state.");

                TryAssign();
            }
        }

        internal static void NotifyNhalSeraDefeated(string source)
        {
            if (!IsActive() || IsComplete())
                return;

            ArmObjectiveIfNeeded();

            if (!FOTFCharacterMilestoneStore.AwardCredit(DefeatCreditId))
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest8] Nhal'Sera was defeated while Quest 8 was active, but objective credit could not be persisted.");
                return;
            }

            Plugin.NativeLog.LogInfo(
                "[Quest8] Nhal'Sera objective complete. Source=[" +
                (source ?? "<unknown>") + "].");

            AddSystemMessage(
                "Quest objective complete: Nhal'Sera defeated. Recover the Shard of Lightless Glass and return to Archivist Sereth.",
                "yellow");
        }

        internal static bool TryComplete()
        {
            Register();

            if (IsComplete())
                return true;

            if (!IsActive() ||
                !HasDefeatedNhalSera() ||
                !NhalSeraQuestContent.PlayerHasShard())
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
                        "[Quest8] native FinishQuest threw; using completed-quest fallback. " +
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
            // fallback does not, so remove the shard if it is still present.
            if (NhalSeraQuestContent.PlayerHasShard())
                NhalSeraQuestContent.TryConsumeShard();

            PlayQuestCompleteSound();

            // Native FinishQuest normally follows AssignNewQuestOnComplete,
            // but the direct quest-state fallback does not. Explicitly ensure
            // Quest 9 is present so both completion paths remain identical.
            VarkeshQuest.Register();
            VarkeshQuest.TryAssign();

            Plugin.NativeLog.LogInfo("[Quest8] Nhal'Sera completed. Quest 9 chain checked.");
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
                    "[Quest8] failed to persist Nhal'Sera objective armed marker.");
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
                    "[Quest8] failed adding quest state [" + id + "]: " +
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
                    "[Quest8] failed to play quest completion sound: " +
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
