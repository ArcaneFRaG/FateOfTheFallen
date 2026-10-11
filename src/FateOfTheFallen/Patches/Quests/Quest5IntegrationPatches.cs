using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 5 Sereth integration only.
    ///
    /// Quest 5 runtime/bootstrap is now owned directly by FateContentModule,
    /// matching the proven Quest 4 grave architecture.
    ///
    /// Replace the older Quest5IntegrationPatches.cs with this file.
    /// </summary>
    [HarmonyPatch(
        typeof(ArchivistSerethQuestBridge),
        "Configure")]
    internal static class Patch_Quest5_ArchivistSerethBridge
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
                AshBeneathEarthQuest.Register();
                AshBeneathEarthQuest.EnsureChainState();

                if (GameData.ItemDB != null)
                {
                    CinderedRiftShard.RegisterItem(
                        GameData.ItemDB);
                }

                AshBeneathEarthContent.LinkQuestRequirement();

                Quest quest =
                    AshBeneathEarthQuest.Quest;

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
                             AshBeneathEarthQuest.QuestId,
                             StringComparison.OrdinalIgnoreCase)))
                    {
                        return;
                    }
                }

                questManager.NPCQuests.Add(
                    quest);

                Plugin.NativeLog.LogInfo(
                    "[Quest5] Archivist Sereth linked to Ash Beneath the Earth.");
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Quest5] failed linking Quest 5 to Archivist Sereth: " +
                    exception);
            }
        }
    }
}
