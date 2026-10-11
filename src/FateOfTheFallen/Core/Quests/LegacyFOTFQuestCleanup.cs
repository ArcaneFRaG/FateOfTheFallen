using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace FateOfTheFallen
{
    internal static class LegacyFOTFQuestCleanup
    {
        internal const string RemovedLegacyQuestId =
            "FOTF_TEST_WEATHERED_NOTE";

        private static bool _loggedRemoval;

        internal static void RemoveObsoleteQuestState()
        {
            bool removed = false;

            removed |= RemoveStringFromCollection(
                GameData.HasQuest,
                RemovedLegacyQuestId);

            removed |= RemoveStringFromCollection(
                GameData.CompletedQuests,
                RemovedLegacyQuestId);

            if (removed && !_loggedRemoval)
            {
                _loggedRemoval = true;
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: removed obsolete test quest state [" +
                    RemovedLegacyQuestId + "] from this character.");
            }
        }

        private static bool RemoveStringFromCollection(
            object collection,
            string value)
        {
            if (collection == null || string.IsNullOrEmpty(value))
            {
                return false;
            }

            bool removedAny = false;
            IList list = collection as IList;

            if (list != null)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (!string.Equals(
                            list[i] as string,
                            value,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    list.RemoveAt(i);
                    removedAny = true;
                }

                return removedAny;
            }

            try
            {
                MethodInfo remove = AccessTools.Method(
                    collection.GetType(),
                    "Remove",
                    new Type[] { typeof(string) });

                MethodInfo contains = AccessTools.Method(
                    collection.GetType(),
                    "Contains",
                    new Type[] { typeof(string) });

                if (remove == null)
                {
                    return false;
                }

                while (true)
                {
                    if (contains != null)
                    {
                        object containsResult = contains.Invoke(
                            collection,
                            new object[] { value });

                        if (!(containsResult is bool) || !(bool)containsResult)
                        {
                            break;
                        }
                    }

                    object result = remove.Invoke(
                        collection,
                        new object[] { value });

                    if (result is bool && !(bool)result)
                    {
                        break;
                    }

                    removedAny = true;

                    if (contains == null)
                    {
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: failed cleaning obsolete test quest state: " +
                    exception.Message);
            }

            return removedAny;
        }
    }


    /// <summary>
    /// Native QuestLog.LoadQuests dereferences every CompletedQuests entry as
    /// a real Quest with no null check. Older FOTF builds placed non-quest
    /// world-boss/raid credit IDs in that collection, which crashes the
    /// journal before active quests are drawn.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_FOTFQuestLog_LegacyCleanup
    {
        private sealed class RemovedEntry
        {
            internal int Index;
            internal string Value;
        }

        private sealed class JournalLoadState
        {
            internal object Collection;
            internal readonly List<RemovedEntry> Removed =
                new List<RemovedEntry>();
            internal bool Restored;
        }

        private static MethodBase TargetMethod()
        {
            Type questLogType = AccessTools.TypeByName("QuestLog");

            return questLogType == null
                ? null
                : AccessTools.Method(questLogType, "LoadQuests");
        }

        [HarmonyPrefix]
        private static void Prefix(out JournalLoadState __state)
        {
            __state = new JournalLoadState();

            LegacyFOTFQuestCleanup.RemoveObsoleteQuestState();

            // Permanently move old non-quest markers into FOTF's own
            // per-character store whenever possible.
            FOTFCharacterMilestoneStore
                .MigrateLegacyCompletedQuestMarkers();

            FateOfTheFallenQuests.Register();

            bool registered =
                CustomQuestRegistry.ReapplyToNativeDatabase();

            if (!registered)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: native Quest Log opened but FOTF quests could not be fully injected into QuestDB. " +
                    CustomQuestRegistry.BuildNativeDebugText());
            }

            // If migration could not yet persist a marker, hide it only for
            // this native LoadQuests call and restore it afterward.
            TemporarilyRemoveNonQuestMarkers(__state);

            LogUnresolvedNativeQuestIds();
        }

        [HarmonyPostfix]
        private static void Postfix(JournalLoadState __state)
        {
            RestoreTemporaryMarkers(__state);
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(
            Exception __exception,
            JournalLoadState __state)
        {
            RestoreTemporaryMarkers(__state);
            return __exception;
        }

        private static void TemporarilyRemoveNonQuestMarkers(
            JournalLoadState state)
        {
            if (state == null)
            {
                return;
            }

            object completed = GameData.CompletedQuests;
            state.Collection = completed;

            IList list = completed as IList;
            if (list == null)
            {
                return;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                string value = list[i] as string;

                if (!FOTFCharacterMilestoneStore.IsNonQuestMarker(value))
                {
                    continue;
                }

                state.Removed.Add(
                    new RemovedEntry
                    {
                        Index = i,
                        Value = value
                    });

                list.RemoveAt(i);
            }
        }

        private static void RestoreTemporaryMarkers(
            JournalLoadState state)
        {
            if (state == null || state.Restored)
            {
                return;
            }

            state.Restored = true;

            IList list = state.Collection as IList;
            if (list == null || state.Removed.Count == 0)
            {
                return;
            }

            state.Removed.Sort(
                delegate(RemovedEntry a, RemovedEntry b)
                {
                    return a.Index.CompareTo(b.Index);
                });

            for (int i = 0; i < state.Removed.Count; i++)
            {
                RemovedEntry entry = state.Removed[i];

                if (entry == null || string.IsNullOrEmpty(entry.Value))
                {
                    continue;
                }

                bool alreadyPresent = false;

                for (int j = 0; j < list.Count; j++)
                {
                    if (string.Equals(
                            list[j] as string,
                            entry.Value,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyPresent = true;
                        break;
                    }
                }

                if (alreadyPresent)
                {
                    continue;
                }

                int index = entry.Index;
                if (index < 0 || index > list.Count)
                {
                    index = list.Count;
                }

                list.Insert(index, entry.Value);
            }
        }

        private static void LogUnresolvedNativeQuestIds()
        {
            if (GameData.QuestDB == null)
            {
                return;
            }

            LogUnresolvedCollection("Active", GameData.HasQuest);
            LogUnresolvedCollection("Completed", GameData.CompletedQuests);
        }

        private static void LogUnresolvedCollection(
            string label,
            object collection)
        {
            IEnumerable enumerable = collection as IEnumerable;
            if (enumerable == null)
            {
                return;
            }

            foreach (object entry in enumerable)
            {
                string id = entry as string;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                Quest quest = null;

                try
                {
                    quest = GameData.QuestDB.GetQuestByName(id);
                }
                catch
                {
                }

                if (quest != null)
                {
                    continue;
                }

                Plugin.NativeLog.LogWarning(
                    "[QuestJournal] unresolved " + label +
                    " quest ID [" + id +
                    "]. Native QuestLog would null-reference this entry.");
            }
        }
    }
}
