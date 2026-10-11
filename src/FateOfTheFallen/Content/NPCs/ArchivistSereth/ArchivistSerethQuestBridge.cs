using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Single ownership point for quests offered/turned in through
    /// Archivist Sereth.
    ///
    /// Quest 1 remains dialogue-driven and is deliberately NOT placed in
    /// QuestManager.NPCQuests because it has no item requirement.
    ///
    /// Quests 2-4 and Quests 7-10 are item-turn-in quests and are registered
    /// with Sereth's QuestManager so Erenshor's native trade window can accept
    /// their canonical quest-item singletons. Quests 11-12 are dialogue/interaction
    /// driven and deliberately stay out of NPCQuests because they have zero
    /// required items. Quests 13-14 are also dialogue-driven and stay out of
    /// NPCQuests.
    /// </summary>
    internal static class ArchivistSerethQuestBridge
    {
        internal static void Configure(
            GameObject sereth)
        {
            if (sereth == null)
            {
                return;
            }

            FateOfTheFallenQuests.Register();
            SignsOfDecayContent.LinkQuestRequirement();
            WhereLightFailsContent.LinkQuestRequirement();
            RestlessDeadContent.LinkQuestRequirement();
            MoldermawQuest.Register();
            MoldermawQuestContent.LinkQuestRequirement();

            NhalSeraQuest.Register();
            NhalSeraQuestContent.LinkQuestRequirement();

            VarkeshQuest.Register();
            VarkeshQuestContent.LinkQuestRequirement();

            GorathQuest.Register();
            GorathQuestContent.LinkQuestRequirement();

            FourWoundsQuest.Register();
            FourWoundsQuest.EnsureChainState();

            NamesThatWereBuriedQuest.Register();
            NamesThatWereBuriedQuest.EnsureChainState();

            TheWeakestWallQuest.Register();
            TheWeakestWallQuest.EnsureChainState();

            TheFracturingQuest.Register();
            TheFracturingQuest.EnsureChainState();

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
            else
            {
                questManager.NPCQuests.Clear();
            }

            // IMPORTANT:
            // Only item-turn-in quests belong here. Adding Quest 1 would let
            // the native trade window treat its zero required items as an
            // immediately satisfied hand-in.
            Quest signsOfDecay =
                FateOfTheFallenQuests.SignsOfDecayQuest;

            if (signsOfDecay != null)
            {
                questManager.NPCQuests.Add(
                    signsOfDecay);
            }

            Quest whereLightFails =
                FateOfTheFallenQuests.WhereLightFailsQuest;

            if (whereLightFails != null)
            {
                questManager.NPCQuests.Add(
                    whereLightFails);
            }

            Quest restlessDead =
                FateOfTheFallenQuests.RestlessDeadQuest;

            if (restlessDead != null)
            {
                questManager.NPCQuests.Add(
                    restlessDead);
            }

            Quest moldermaw =
                MoldermawQuest.Quest;

            if (moldermaw != null)
            {
                questManager.NPCQuests.Add(
                    moldermaw);
            }

            Quest nhalSera =
                NhalSeraQuest.Quest;

            if (nhalSera != null)
            {
                questManager.NPCQuests.Add(
                    nhalSera);
            }

            Quest varkesh =
                VarkeshQuest.Quest;

            if (varkesh != null)
            {
                questManager.NPCQuests.Add(
                    varkesh);
            }

            Quest gorath =
                GorathQuest.Quest;

            if (gorath != null)
            {
                questManager.NPCQuests.Add(
                    gorath);
            }
        }
    }
}
