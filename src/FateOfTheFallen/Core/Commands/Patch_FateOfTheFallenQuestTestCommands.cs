using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace FateOfTheFallen
{
    /// <summary>
    /// Development commands for resetting/retesting Fate of the Fallen quests.
    ///
    /// /fotfquests reset
    ///     Clears active + completed FOTF QUEST progression for this character.
    ///
    /// /fotfquests restart
    ///     Clears FOTF quest progression and immediately reassigns Quest 1.
    ///
    /// /fotfquests status
    ///     Prints the current state of the implemented FOTF story quests.
    ///
    /// /fotfquestreset
    ///     Convenience alias for /fotfquests reset.
    ///
    /// IMPORTANT:
    /// This resets only FOTF quest state. World-boss/raid completion credits
    /// live in the separate FOTF character milestone store and are untouched.
    /// </summary>
    [HarmonyPatch(
        typeof(TypeText),
        "CheckCommands")]
    internal static class Patch_FateOfTheFallenQuestTestCommands
    {
        private const string CommandRoot =
            "/fotfquests";

        private const string ResetAlias =
            "/fotfquestreset";


        [HarmonyPrefix]
        private static bool Prefix(
            TypeText __instance)
        {
            if (__instance == null ||
                __instance.typed == null)
            {
                return true;
            }

            string raw =
                __instance.typed.text;

            if (string.IsNullOrWhiteSpace(
                    raw))
            {
                return true;
            }

            string trimmed =
                raw.Trim();

            bool aliasReset =
                string.Equals(
                    trimmed,
                    ResetAlias,
                    StringComparison.OrdinalIgnoreCase);

            bool isQuestCommand =
                string.Equals(
                    trimmed,
                    CommandRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(
                    CommandRoot + " ",
                    StringComparison.OrdinalIgnoreCase);

            if (!aliasReset &&
                !isQuestCommand)
            {
                return true;
            }

            __instance.typed.text =
                string.Empty;

            try
            {
                if (aliasReset)
                {
                    ResetAndReport(
                        false);

                    return false;
                }

                string[] parts =
                    trimmed.Split(
                        new char[]
                        {
                            ' '
                        },
                        StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 1)
                {
                    WriteUsage();
                    return false;
                }

                string mode =
                    parts[1];

                if (string.Equals(
                        mode,
                        "reset",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ResetAndReport(
                        false);

                    return false;
                }

                if (string.Equals(
                        mode,
                        "restart",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ResetAndReport(
                        true);

                    return false;
                }

                if (string.Equals(
                        mode,
                        "status",
                        StringComparison.OrdinalIgnoreCase))
                {
                    WriteStatus();
                    return false;
                }

                if (string.Equals(
                        mode,
                        "debug",
                        StringComparison.OrdinalIgnoreCase))
                {
                    WriteDebug();
                    return false;
                }

                if (string.Equals(
                        mode,
                        "q2sample",
                        StringComparison.OrdinalIgnoreCase))
                {
                    GiveQuestTwoSample();
                    return false;
                }

                if (string.Equals(
                        mode,
                        "q4essence",
                        StringComparison.OrdinalIgnoreCase))
                {
                    GiveQuestFourEssence();
                    return false;
                }

                if (string.Equals(
                        mode,
                        "q6fragment",
                        StringComparison.OrdinalIgnoreCase))
                {
                    GiveQuestSixFragment();
                    return false;
                }

                if (string.Equals(
                        mode,
                        "q7kill",
                        StringComparison.OrdinalIgnoreCase))
                {
                    MarkQuestSevenBossDefeated();
                    return false;
                }

                WriteUsage();
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "FOTF quest testing command failed: " +
                    exception);

                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        "FOTF quest command failed. See the log.",
                        ChatLogLine.LogType.SystemMessages,
                        "red"));
            }

            return false;
        }


        private static void ResetAndReport(
            bool restartQuestOne)
        {
            FateOfTheFallenQuests.Register();
            ThePatternBeneathQuest.Register();
            MoldermawQuest.Register();

            int activeRemoved =
                RemoveFOTFQuestIds(
                    GameData.HasQuest);

            int completedRemoved =
                RemoveFOTFQuestIds(
                    GameData.CompletedQuests);

            MoldermawQuest.ResetObjectiveCredits();
            NhalSeraQuest.ResetObjectiveCredits();
            VarkeshQuest.ResetObjectiveCredits();
            GorathQuest.ResetObjectiveCredits();
            NamesThatWereBuriedQuest.ResetObjectiveCredit();
            TheFracturingQuest.ResetProgressionFlags();

            // Reapply canonical Quest objects after the state mutation so the
            // native quest log can immediately resolve them on the next read.
            FateOfTheFallenQuests.Register();

            CustomQuestRegistry
                .ReapplyToNativeDatabase();

            bool restarted =
                false;

            if (restartQuestOne)
            {
                restarted =
                    FateOfTheFallenQuests
                        .TryAssignAfterLastWarden();
            }

            string message =
                "FOTF quest progression reset for this character. " +
                "Removed Active=" +
                activeRemoved +
                ", Completed=" +
                completedRemoved +
                ".";

            if (restartQuestOne)
            {
                message +=
                    restarted
                        ? " Quest 1 [After the Last Warden] was reassigned."
                        : " Quest 1 could not be reassigned; re-hail Archivist Sereth and check the log.";
            }
            else
            {
                message +=
                    " Re-hail Archivist Sereth to begin Quest 1 again.";
            }

            UpdateSocialLog.LogAdd(
                new ChatLogLine(
                    message,
                    ChatLogLine.LogType.SystemMessages,
                    "yellow"));

            WriteStatus();
        }


        private static int RemoveFOTFQuestIds(
            object collection)
        {
            if (collection == null)
            {
                return 0;
            }

            HashSet<string> registeredQuestIds =
                GetRegisteredQuestIds();

            int removed =
                0;

            try
            {
                IList list =
                    collection as IList;

                if (list != null)
                {
                    for (int i =
                            list.Count - 1;
                         i >= 0;
                         i--)
                    {
                        string value =
                            list[i] as string;

                        if (!IsFOTFQuestId(
                                value,
                                registeredQuestIds))
                        {
                            continue;
                        }

                        list.RemoveAt(
                            i);

                        removed++;
                    }

                    return removed;
                }

                List<string> valuesToRemove =
                    new List<string>();

                IEnumerable enumerable =
                    collection as IEnumerable;

                if (enumerable != null)
                {
                    foreach (object entry in enumerable)
                    {
                        string value =
                            entry as string;

                        if (IsFOTFQuestId(
                                value,
                                registeredQuestIds))
                        {
                            valuesToRemove.Add(
                                value);
                        }
                    }
                }

                MethodInfo remove =
                    AccessTools.Method(
                        collection.GetType(),
                        "Remove",
                        new Type[]
                        {
                            typeof(string)
                        });

                if (remove == null)
                {
                    return 0;
                }

                for (int i = 0;
                     i < valuesToRemove.Count;
                     i++)
                {
                    object result =
                        remove.Invoke(
                            collection,
                            new object[]
                            {
                                valuesToRemove[i]
                            });

                    if (!(result is bool) ||
                        (bool)result)
                    {
                        removed++;
                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "FOTF quest reset could not modify a quest-state collection: " +
                    exception);
            }

            return removed;
        }


        private static HashSet<string>
            GetRegisteredQuestIds()
        {
            HashSet<string> result =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            result.Add(
                FateOfTheFallenQuests
                    .AfterLastWardenQuestId);

            result.Add(
                FateOfTheFallenQuests
                    .SignsOfDecayQuestId);

            result.Add(
                FateOfTheFallenQuests
                    .WhereLightFailsQuestId);

            result.Add(
                LegacyFOTFQuestCleanup
                    .RemovedLegacyQuestId);

            return result;
        }


        private static bool IsFOTFQuestId(
            string value,
            HashSet<string> registeredQuestIds)
        {
            if (string.IsNullOrEmpty(
                    value))
            {
                return false;
            }

            if (registeredQuestIds != null &&
                registeredQuestIds.Contains(
                    value))
            {
                return true;
            }

            // Story/test quest namespaces only. World-boss/raid credits now
            // live in the separate FOTF character milestone store and are
            // intentionally untouched by this quest reset.
            return
                value.StartsWith(
                    "FOTF_MAIN_",
                    StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith(
                    "FOTF_SIDE_",
                    StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith(
                    "FOTF_QUEST_",
                    StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith(
                    "FOTF_TEST_",
                    StringComparison.OrdinalIgnoreCase);
        }


        private static void WriteStatus()
        {
            FateOfTheFallenQuests.Register();

            string questOneState =
                GetQuestState(
                    FateOfTheFallenQuests
                        .AfterLastWardenQuestId);

            string questTwoState =
                GetQuestState(
                    FateOfTheFallenQuests
                        .SignsOfDecayQuestId);

            string questThreeState =
                GetQuestState(
                    FateOfTheFallenQuests
                        .WhereLightFailsQuestId);

            string questFourState =
                GetQuestState(
                    FateOfTheFallenQuests
                        .RestlessDeadQuestId);

            string questFiveState =
                GetQuestState(
                    FateOfTheFallenQuests
                        .AshBeneathEarthQuestId);

            string questSixState =
                GetQuestState(
                    ThePatternBeneathQuest
                        .QuestId);

            string questSevenState =
                GetQuestState(
                    MoldermawQuest
                        .QuestId);

            string questEightState =
                GetQuestState(
                    NhalSeraQuest
                        .QuestId);

            string questNineState =
                GetQuestState(
                    VarkeshQuest
                        .QuestId);

            string questTenState =
                GetQuestState(
                    GorathQuest
                        .QuestId);

            string questElevenState =
                GetQuestState(
                    FourWoundsQuest
                        .QuestId);

            string questTwelveState =
                GetQuestState(
                    NamesThatWereBuriedQuest
                        .QuestId);

            string questThirteenState =
                GetQuestState(
                    TheWeakestWallQuest
                        .QuestId);

            string questFourteenState =
                GetQuestState(
                    TheFracturingQuest
                        .QuestId);

            string legacyState =
                GetQuestState(
                    LegacyFOTFQuestCleanup
                        .RemovedLegacyQuestId);

            string message =
                "FOTF quests | " +
                "Q1 After the Last Warden=" +
                questOneState +
                " | Q2 Signs of Decay=" +
                questTwoState +
                " | Q3 Where Light Fails=" +
                questThreeState +
                " | Q4 The Restless Dead=" +
                questFourState +
                " | Q5 Ash Beneath the Earth=" +
                questFiveState +
                " | Q6 The Pattern Beneath=" +
                questSixState +
                " | Q7 Moldermaw=" +
                questSevenState +
                " (Kill=" +
                (MoldermawQuest.HasDefeatedMoldermaw()
                    ? "DONE"
                    : "PENDING") +
                ")" +
                " | Q8 Nhal'Sera=" +
                questEightState +
                " | Q9 Varkesh=" +
                questNineState +
                " | Q10 Gorath=" +
                questTenState +
                " | Q11 Four Wounds=" +
                questElevenState +
                " | Q12 Names That Were Buried=" +
                questTwelveState +
                " (HiddenNames=" +
                NamesThatWereBuriedQuest.GetRecoveredNameCount() +
                "/4)" +
                " | Q13 The Weakest Wall=" +
                questThirteenState +
                " | Q14 The Fracturing=" +
                questFourteenState +
                " (BlightAttuned=" +
                (TheFracturingQuest.HasBlightPlanarAttunement()
                    ? "YES"
                    : "NO") +
                ", Flag=" +
                (TheFracturingQuest.HasFracturingCompleteFlag()
                    ? "SET"
                    : "UNSET") +
                ")";

            if (!string.Equals(
                    legacyState,
                    "NOT STARTED",
                    StringComparison.Ordinal))
            {
                message +=
                    " | LegacyTest=" +
                    legacyState;
            }

            UpdateSocialLog.LogAdd(
                new ChatLogLine(
                    message,
                    ChatLogLine.LogType.SystemMessages,
                    ""));
        }


        private static string GetQuestState(
            string questId)
        {
            if (CollectionContains(
                    GameData.CompletedQuests,
                    questId))
            {
                return
                    "COMPLETED";
            }

            if (CollectionContains(
                    GameData.HasQuest,
                    questId))
            {
                return
                    "ACTIVE";
            }

            return
                "NOT STARTED";
        }


        private static bool CollectionContains(
            object collection,
            string value)
        {
            if (collection == null ||
                string.IsNullOrEmpty(
                    value))
            {
                return false;
            }

            try
            {
                IList list =
                    collection as IList;

                if (list != null)
                {
                    for (int i = 0;
                         i < list.Count;
                         i++)
                    {
                        if (string.Equals(
                                list[i] as string,
                                value,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                MethodInfo contains =
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
                            value
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


        private static void WriteDebug()
        {
            FateOfTheFallenQuests
                .Register();

            bool injected =
                CustomQuestRegistry
                    .ReapplyToNativeDatabase();

            string activeQ1 =
                CollectionContains(
                    GameData.HasQuest,
                    FateOfTheFallenQuests
                        .AfterLastWardenQuestId)
                    ? "YES"
                    : "NO";

            string activeQ2 =
                CollectionContains(
                    GameData.HasQuest,
                    FateOfTheFallenQuests
                        .SignsOfDecayQuestId)
                    ? "YES"
                    : "NO";

            string completeQ1 =
                CollectionContains(
                    GameData.CompletedQuests,
                    FateOfTheFallenQuests
                        .AfterLastWardenQuestId)
                    ? "YES"
                    : "NO";

            string completeQ2 =
                CollectionContains(
                    GameData.CompletedQuests,
                    FateOfTheFallenQuests
                        .SignsOfDecayQuestId)
                    ? "YES"
                    : "NO";

            string message =
                "FOTF quest debug | Inject=" +
                (injected ? "OK" : "FAILED") +
                " | ActiveQ1=" +
                activeQ1 +
                " ActiveQ2=" +
                activeQ2 +
                " | CompleteQ1=" +
                completeQ1 +
                " CompleteQ2=" +
                completeQ2 +
                " | " +
                CustomQuestRegistry
                    .BuildNativeDebugText();

            UpdateSocialLog.LogAdd(
                new ChatLogLine(
                    message,
                    ChatLogLine.LogType.SystemMessages,
                    ""));
        }


        private static void GiveQuestTwoSample()
        {
            FateOfTheFallenQuests.Register();

            if (!FateOfTheFallenQuests
                .IsSignsOfDecayActive())
            {
                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        "Quest 2 [Signs of Decay] is not active.",
                        ChatLogLine.LogType.SystemMessages,
                        "yellow"));

                return;
            }

            bool granted =
                SignsOfDecayContent
                    .TryGiveSample(
                        true);

            UpdateSocialLog.LogAdd(
                new ChatLogLine(
                    granted
                        ? "Quest 2 test sample is now in your inventory."
                        : "Quest 2 sample could not be granted. See the log.",
                    ChatLogLine.LogType.SystemMessages,
                    granted ? "green" : "red"));
        }


        private static void GiveQuestFourEssence()
        {
            FateOfTheFallenQuests.Register();

            if (!FateOfTheFallenQuests
                .IsRestlessDeadActive())
            {
                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        "Quest 4 [The Restless Dead] is not active.",
                        ChatLogLine.LogType.SystemMessages,
                        "yellow"));

                return;
            }

            bool granted =
                RestlessDeadContent
                    .TryGiveEssence(
                        true);

            UpdateSocialLog.LogAdd(
                new ChatLogLine(
                    granted
                        ? "Quest 4 test essence is now in your inventory."
                        : "Quest 4 essence could not be granted. See the log.",
                    ChatLogLine.LogType.SystemMessages,
                    granted ? "green" : "red"));
        }


        private static void GiveQuestSixFragment()
        {
            ThePatternBeneathQuest.Register();
            ThePatternBeneathQuest.EnsureChainState();

            if (!ThePatternBeneathQuest.IsActive())
            {
                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        "Quest 6 [The Pattern Beneath] is not active.",
                        ChatLogLine.LogType.SystemMessages,
                        "yellow"));

                return;
            }

            bool granted =
                ThePatternBeneathContent
                    .TryGiveFragment(
                        true);

            UpdateSocialLog.LogAdd(
                new ChatLogLine(
                    granted
                        ? "Quest 6 test fragment is now in your inventory."
                        : "Quest 6 fragment could not be granted. See the log.",
                    ChatLogLine.LogType.SystemMessages,
                    granted ? "green" : "red"));
        }


        private static void MarkQuestSevenBossDefeated()
        {
            MoldermawQuest.Register();
            MoldermawQuest.EnsureChainState();

            if (!MoldermawQuest.IsActive())
            {
                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        "Quest 7 [Moldermaw] is not active.",
                        ChatLogLine.LogType.SystemMessages,
                        "yellow"));

                return;
            }

            MoldermawQuest.NotifyMoldermawDefeated(
                "quest test command");

            UpdateSocialLog.LogAdd(
                new ChatLogLine(
                    MoldermawQuest.HasDefeatedMoldermaw()
                        ? "Quest 7 test: Moldermaw kill objective marked complete. Hail Archivist Sereth."
                        : "Quest 7 test kill credit could not be recorded. See the log.",
                    ChatLogLine.LogType.SystemMessages,
                    MoldermawQuest.HasDefeatedMoldermaw()
                        ? "green"
                        : "red"));
        }


        private static void WriteUsage()
        {
            UpdateSocialLog.LogAdd(
                new ChatLogLine(
                    "FOTF quest testing: /fotfquests reset | restart | status | debug | q2sample | q4essence | q6fragment | q7kill | /fotfquestreset",
                    ChatLogLine.LogType.SystemMessages,
                    "yellow"));
        }
    }
}
