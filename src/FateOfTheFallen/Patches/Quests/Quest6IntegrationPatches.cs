using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 6 Sereth integration only.
    ///
    /// Quest 6 runtime/bootstrap is owned directly by FateContentModule,
    /// matching the current Quest 5 architecture.
    /// </summary>
    [HarmonyPatch(
        typeof(ArchivistSerethQuestBridge),
        "Configure")]
    internal static class Patch_Quest6_ArchivistSerethBridge
    {
        private static void Postfix(
            GameObject sereth)
        {
            if (!FOTFExpansionGate.IsUnlocked ||
                sereth == null)
            {
                return;
            }

            try
            {
                ThePatternBeneathQuest.Register();
                ThePatternBeneathQuest.EnsureChainState();

                if (GameData.ItemDB != null)
                {
                    FracturedWardingFragment.RegisterItem(
                        GameData.ItemDB);
                }

                ThePatternBeneathContent.LinkQuestRequirement();

                Quest quest =
                    ThePatternBeneathQuest.Quest;

                if (quest == null)
                {
                    return;
                }

                QuestManager questManager =
                    sereth.GetComponent<QuestManager>();

                if (questManager == null)
                {
                    questManager =
                        sereth.AddComponent<QuestManager>();
                }

                if (questManager.NPCQuests == null)
                {
                    questManager.NPCQuests =
                        new List<Quest>();
                }

                for (int i = 0;
                     i < questManager.NPCQuests.Count;
                     i++)
                {
                    Quest existing =
                        questManager.NPCQuests[i];

                    if (existing == quest ||
                        (existing != null &&
                         string.Equals(
                             existing.DBName,
                             ThePatternBeneathQuest.QuestId,
                             StringComparison.OrdinalIgnoreCase)))
                    {
                        return;
                    }
                }

                questManager.NPCQuests.Add(
                    quest);

                Plugin.NativeLog.LogInfo(
                    "[Quest6] Archivist Sereth linked to The Pattern Beneath.");
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Quest6] failed linking Quest 6 to Archivist Sereth: " +
                    exception);
            }
        }
    }
}
