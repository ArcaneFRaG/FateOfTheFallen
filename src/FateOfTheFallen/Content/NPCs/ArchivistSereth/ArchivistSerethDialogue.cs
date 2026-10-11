using System;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FateOfTheFallen
{
    internal static class ArchivistSerethDialogue
    {
        internal const string QuestOneKeyword =
            "disturbances";

        internal const string QuestOneKeywordSingular =
            "disturbance";

        internal const string QuestOneLinkId =
            "FOTF_SERETH_DISTURBANCES";

        internal const string QuestElevenKeyword =
            "four wounds";

        internal const string QuestElevenLinkId =
            "FOTF_SERETH_FOUR_WOUNDS";

        internal const string QuestThirteenKeyword =
            "weakest wall";

        internal const string QuestThirteenLinkId =
            "FOTF_SERETH_WEAKEST_WALL";

        internal const string QuestFourteenKeyword =
            "the fracturing";

        internal const string QuestFourteenLinkId =
            "FOTF_SERETH_THE_FRACTURING";


        private const string QuestOneClickableKeyword =
            "<link=\"" +
            QuestOneLinkId +
            "\"><color=#55FF55>disturbances</color></link>";

        private const string QuestElevenClickableKeyword =
            "<link=\"" +
            QuestElevenLinkId +
            "\"><color=#55FF55>four wounds</color></link>";


        private const string QuestThirteenClickableKeyword =
            "<link=\"" +
            QuestThirteenLinkId +
            "\"><color=#55FF55>weakest wall</color></link>";

        private const string QuestFourteenClickableKeyword =
            "<link=\"" +
            QuestFourteenLinkId +
            "\"><color=#55FF55>the fracturing</color></link>";


        private static readonly string QuestOneOfferDialogue =
            "Ah. You may be precisely the person I needed. " +
            "I have been comparing accounts filed after the defeat of the " +
            "last corrupted Warden. The reports disagree on almost every " +
            "detail, but they agree on one thing: timing. " +
            "Ask me about the " +
            QuestOneClickableKeyword +
            ".";

        private static readonly string QuestOneActiveDialogue =
            "The dates still trouble me. Events recorded hundreds of miles " +
            "apart begin within the same narrow span after the last Warden " +
            "fell. Ask me about the " +
            QuestOneClickableKeyword +
            " and I will show you what I mean.";

        private const string QuestOneCompletionDialogue =
            "That is the problem: they were not one event. Within days of " +
            "the last Warden's fall, reports arrived from distant regions. " +
            "Crops blackened without ordinary disease. Shadows moved against " +
            "their light. Graves opened where no necromancer had been seen. " +
            "Stone grew hot where no fire burned.\n\n" +
            "Separately, each account can be explained away. Together, the " +
            "timing is difficult to dismiss. I will not call it a pattern " +
            "without evidence, however. The blight reports are the most " +
            "consistent. A fresh sample of the unnatural spores described " +
            "in them would give us somewhere concrete to begin.";

        private const string QuestTwoActiveHail =
            "The affected ground should be easy enough to recognise. " +
            "Travel to Loomingwood, find whatever continues to grow among " +
            "the decayed vegetation, and bring me a sample.";

        private const string QuestTwoCompletionDialogue =
            "These spores are unfamiliar. There is blight in them, certainly, " +
            "but it is translated poorly into our world—as though whatever " +
            "produced them was never meant to take root here. That matters. " +
            "If the other disturbances carry different signatures, then we " +
            "are not looking at a single spreading corruption.";

        private const string QuestThreeActiveHail =
            "The spores gave us one signature. Now I need another point of " +
            "comparison. One report describes a place where daylight itself " +
            "seems diminished. That is where we look next.";

        private const string QuestFourActiveHail =
            "The third report concerns graves. Several bodies have risen without " +
            "any visible ritual, and no necromancer has been seen nearby. Destroy " +
            "one of the risen dead and bring me whatever remains when it falls.";

        private const string QuestFiveActiveHail =
            "There is one report left. A section of stone has split open and begun " +
            "radiating heat with no fire beneath it. Small embers have formed around " +
            "the fracture. Bring me one.";

        private const string QuestSixActiveHail =
            "I have compared all four samples side by side. Their substance is different, " +
            "but the distortion around each is the same—a fracture in something beneath them. " +
            "There is an ancient warding shrine here in Port Azure. Examine its central seal. " +
            "If the same damage is present there, bring me a fragment.";

        private const string QuestSixCompleteHail =
            "The four disturbances are not one corruption. They are four separate pressures escaping " +
            "through the same damaged boundary. What matters now is whether those fractures spread " +
            "naturally after the Wardens fell—or whether something is deliberately widening them.";

        private const string QuestSevenActiveHail =
            "The Blight has produced something far more dangerous than the growths we examined before. " +
            "Moldermaw is a massive stone golem, not a living beast. Blight burns through fractures in its rock body " +
            "and whatever lies beyond that breach is driving the construct with purpose. Destroy Moldermaw, recover its Blighted Core, and bring the Core to me.";

        private const string QuestSevenKilledNoCoreHail =
            "Moldermaw is dead, but I need the Blighted Core from its remains. The Core is the part most saturated by whatever force was driving the construct. " +
            "Recover Moldermaw's Blighted Core and bring it to me.";

        private const string QuestSevenObjectiveCompleteHail =
            "Moldermaw has fallen? Good. A stone body should have offered the Blight nothing to infect, yet the corruption " +
            "animated it all the same. That changes what we know about these breaches.";

        private const string QuestSevenCompleteHail =
            "Moldermaw proved that the Blight can seize and direct more than living matter. The other fractures may be capable " +
            "of the same. The void disturbance is the next one we must understand.";

        private const string QuestEightActiveHail =
            "The Void disturbance has produced Nhal'Sera. Its body does not behave like Moldermaw's did; light and distance distort around it. " +
            "Destroy Nhal'Sera, recover a Shard of Lightless Glass from the corpse, and bring the shard to me.";

        private const string QuestEightKilledNoShardHail =
            "Nhal'Sera is dead, but I need something physical from the construct if we are going to understand what the Void did to it. " +
            "Recover the Shard of Lightless Glass from its remains and bring it to me.";

        private const string QuestEightObjectiveCompleteHail =
            "So this is what remained inside Nhal'Sera. It resembles glass, but light refuses to cross it normally and the edge never seems " +
            "to occupy quite the same place twice. The Void is not merely corrupting matter. It is changing the rules around it.";

        private const string QuestEightCompleteHail =
            "The Shard of Lightless Glass confirms the second pressure is fundamentally different from the Blight. " +
            "There are still two fractures left to understand.";

        private const string QuestNineActiveHail =
            "The third disturbance is tied to the dead, but this is not ordinary necromancy. Varkesh the Unburied is moving again, " +
            "and the reports suggest his soul was caught before it could pass beyond reach. Defeat Varkesh, recover Varkesh's Soul Fragment, " +
            "and bring it to me.";

        private const string QuestNineKilledNoFragmentHail =
            "Varkesh is dead, but I need the fragment that held his soul in place. Recover Varkesh's Soul Fragment from the corpse and bring it to me.";

        private const string QuestNineObjectiveCompleteHail =
            "This fragment should have passed beyond our reach with Varkesh. Instead, something caught it between death and whatever comes after. " +
            "The soul was not summoned back later; it was prevented from leaving in the first place.";

        private const string QuestNineCompleteHail =
            "Varkesh confirms the third pressure can interfere with the passage of a soul itself. One fracture remains to understand: the infernal disturbance.";

        private const string QuestTenActiveHail =
            "The final disturbance is infernal. Its signature matches the scorched fracture we examined earlier, but it is now concentrated around Gorath. " +
            "Defeat Gorath, recover Gorath's Infernal Heart from the corpse, and bring the heart to me.";

        private const string QuestTenKilledNoHeartHail =
            "Gorath is dead, but I need the infernal heart that sustained him. Recover Gorath's Infernal Heart from the corpse and bring it to me.";

        private const string QuestTenObjectiveCompleteHail =
            "This is the fourth signature. Blight, Void, undeath, and now the infernal. Four different powers, all finding purchase through the same damaged boundary.";

        private const string QuestTenCompleteHail =
            "All four disturbances are now confirmed. They were never four expressions of one corruption. They are four separate powers pressing through the same wounded boundary. " +
            "Whatever comes next, we finally know what we are facing.";

        private static readonly string QuestElevenActiveHail =
            "I have set the four relics together now. They disagree with one another in substance, resonance, and behaviour, yet each pulls against the same damaged boundary from a different direction. " +
            "There is a phrase in the oldest warding records that fits what we are seeing. Ask me about the " +
            QuestElevenClickableKeyword +
            ".";

        private const string QuestElevenCompletionDialogue =
            "They are four wounds, but they were not made by one power. Each relic answers to a different place beyond our boundary. " +
            "The Blight, the Void, the power that held Varkesh's soul, and the infernal pressure behind Gorath are separate forces, each pressing against the same damaged ward.\n\n" +
            "The oldest records have a name for the powers confined beyond those wounds. Not their true names—a classification: the Fallen. " +
            "Someone went to considerable effort to erase the rest. If we are going to understand what is trying to cross into Erenshor, we need the names that were buried.";

        private const string QuestElevenCompleteHail =
            "The oldest records call the powers beyond the four wounds the Fallen. Their true names were deliberately buried. That is the next thing we need to recover.";

        private const string QuestTwelveActiveHail =
            "Go back to the Ancient Warding Shrine here in Port Azure. You examined the central seal before; now inspect the four keeper statues themselves. " +
            "Look behind them and along the lower outward edges. Anything meant only for Wardens would have been carved where someone standing at the altar would never notice it.";

        private const string QuestTwelveReadyHail =
            "You found inscriptions on all four statues? Good. Tell me exactly what each keeper was charged with guarding.";

        private static readonly string QuestTwelveCompletionDialogue =
            "Hidden on the backs of the statues. Of course. The shrine itself was the record, and only a Warden inspecting it closely would ever think to look there.\n\n" +
            "Fernalla names the Lord of Blight. Soluna names the Shrouded Mother. Vithean names the Deathless One. Brax names the Demon King. " +
            "Perhaps those are titles rather than true names, but they are the names the keepers themselves preserved for the prisoners they guarded.\n\n" +
            "Four keepers. Four prisons. Four Fallen. Now we finally know who is pressing against each wound. But they did not all show themselves at once. " +
            "That order may tell us which prison is failing first. Ask me about the " +
            QuestThirteenClickableKeyword +
            ".";

        private const string QuestTwelveCompleteHail =
            "The hidden shrine inscriptions give us the four Fallen: Lord of Blight, Shrouded Mother, Deathless One, and Demon King. " +
            "The Wardens hid the names in plain sight, on the outward faces of the statues where worshippers would never look.";

        private static readonly string QuestThirteenActiveHail =
            "The four prisons are not failing evenly. Think back to the order in which their champions appeared. Ask me which one has become the " +
            QuestThirteenClickableKeyword +
            ".";

        private static readonly string QuestThirteenCompletionDialogue =
            "The Blight prison. Moldermaw was the first of the four champions to rear its head, before Nhal'Sera, Varkesh, or Gorath. " +
            "That order matters. If all four prisons were failing at the same rate, I would expect their influence to surface together. It did not.\n\n" +
            "The Lord of Blight found purchase first. Its prison is weakening faster than the others. If we are going to act before a Fallen crosses the boundary itself, the Blight prison is where we begin. " +
            "There is one last thing I can do with the warding fragment you recovered. Ask me about " +
            QuestFourteenClickableKeyword +
            ".";

        private const string QuestThirteenCompleteHail =
            "Moldermaw was the first warning. The Lord of Blight's prison is weakening faster than the other three. I can use the warding fragment to isolate that failing boundary's signature.";

        private static readonly string QuestFourteenActiveHail =
            "The Fractured Warding Fragment from the shrine is reacting differently now that we can separate the four pressures. The Blight line is widening. Ask me about " +
            QuestFourteenClickableKeyword +
            ".";

        private const string QuestFourteenCompletionDialogue =
            "The fracture was never random. Now that we know what each pressure belongs to, the old warding fragment resolves into four distinct stresses—and the Blight line is wider than the others.\n\n" +
            "I have bound that resonance to your essence. You should be able to recognise the Lord of Blight's boundary now, even where it is too faint for ordinary senses. Call it a planar attunement if you like. It is not a key. It will not open the prison, and it will not carry you across.\n\n" +
            "But when we find a safe way through, you will know the path when it answers you. The surviving Warden records give the realm around that prison a name: the Blighted Expanse. That is where this road leads next.";

        private const string QuestFourteenCompleteHail =
            "The Blight signature is bound to you now. You can sense the Lord of Blight's boundary, but you cannot open or cross it yet. When we find a safe way through, the first road beyond Erenshor will lead into the Blighted Expanse.";

        private const string QuestOneCompleteHail =
            "The disturbances remain connected by timing, but timing alone is " +
            "not evidence. We should begin with the reports of unnatural decay.";


        internal static bool IsSereth(
            NPCDialogManager manager)
        {
            return
                manager != null &&
                manager.gameObject != null &&
                string.Equals(
                    manager.gameObject.name,
                    ArchivistSerethDefinition.NpcName,
                    StringComparison.OrdinalIgnoreCase);
        }


        internal static void HandleGenericHail(
            NPCDialogManager manager)
        {
            if (!IsSereth(
                    manager))
            {
                return;
            }

            FateOfTheFallenQuests.Register();
            AshBeneathEarthQuest.Register();
            ThePatternBeneathQuest.Register();
            ThePatternBeneathQuest.EnsureChainState();
            MoldermawQuest.Register();
            MoldermawQuest.EnsureChainState();
            NhalSeraQuest.Register();
            NhalSeraQuest.EnsureChainState();
            VarkeshQuest.Register();
            VarkeshQuest.EnsureChainState();
            GorathQuest.Register();
            GorathQuest.EnsureChainState();
            FourWoundsQuest.Register();
            FourWoundsQuest.EnsureChainState();
            NamesThatWereBuriedQuest.Register();
            NamesThatWereBuriedQuest.EnsureChainState();
            TheWeakestWallQuest.Register();
            TheWeakestWallQuest.EnsureChainState();
            TheFracturingQuest.Register();
            TheFracturingQuest.EnsureChainState();

            string dialogue;

            if (FateOfTheFallenQuests
                .IsSignsOfDecayActive())
            {
                string questTwoDialogue;

                if (TryHandleSignsOfDecayTurnIn(
                        out questTwoDialogue))
                {
                    dialogue =
                        questTwoDialogue;
                }
                else
                {
                    dialogue =
                        QuestTwoActiveHail;
                }
            }
            else if (FateOfTheFallenQuests
                .IsWhereLightFailsActive())
            {
                dialogue =
                    QuestThreeActiveHail;
            }
            else if (FateOfTheFallenQuests
                .IsRestlessDeadActive())
            {
                dialogue =
                    QuestFourActiveHail;
            }
            else if (FateOfTheFallenQuests
                .IsAshBeneathEarthActive())
            {
                dialogue =
                    QuestFiveActiveHail;
            }
            else if (ThePatternBeneathQuest
                .IsActive())
            {
                dialogue =
                    QuestSixActiveHail;
            }
            else if (MoldermawQuest
                .IsActive())
            {
                if (MoldermawQuest
                    .HasDefeatedMoldermaw())
                {
                    if (!MoldermawQuestContent.PlayerHasCore())
                    {
                        dialogue =
                            QuestSevenKilledNoCoreHail;
                    }
                    else
                    {
                        bool completedQuestSeven =
                            MoldermawQuest
                                .TryComplete();

                        dialogue =
                            completedQuestSeven
                                ? QuestSevenObjectiveCompleteHail
                                : "You have Moldermaw's Blighted Core and the construct is dead, but something is preventing me from closing the record.";
                    }
                }
                else
                {
                    dialogue =
                        QuestSevenActiveHail;
                }
            }
            else if (NhalSeraQuest
                .IsActive())
            {
                if (NhalSeraQuest.HasDefeatedNhalSera())
                {
                    if (!NhalSeraQuestContent.PlayerHasShard())
                    {
                        dialogue =
                            QuestEightKilledNoShardHail;
                    }
                    else
                    {
                        bool completedQuestEight =
                            NhalSeraQuest.TryComplete();

                        dialogue =
                            completedQuestEight
                                ? QuestEightObjectiveCompleteHail
                                : "You have the shard and Nhal'Sera is dead, but something is preventing me from closing the record.";
                    }
                }
                else
                {
                    dialogue =
                        QuestEightActiveHail;
                }
            }
            else if (VarkeshQuest.IsActive())
            {
                if (VarkeshQuest.HasDefeatedVarkesh())
                {
                    if (!VarkeshQuestContent.PlayerHasSoulFragment())
                    {
                        dialogue =
                            QuestNineKilledNoFragmentHail;
                    }
                    else
                    {
                        bool completedQuestNine =
                            VarkeshQuest.TryComplete();

                        dialogue =
                            completedQuestNine
                                ? QuestNineObjectiveCompleteHail
                                : "You have the soul fragment and Varkesh is dead, but something is preventing me from closing the record.";
                    }
                }
                else
                {
                    dialogue =
                        QuestNineActiveHail;
                }
            }
            else if (GorathQuest.IsActive())
            {
                if (GorathQuest.HasDefeatedGorath())
                {
                    if (!GorathQuestContent.PlayerHasInfernalHeart())
                    {
                        dialogue =
                            QuestTenKilledNoHeartHail;
                    }
                    else
                    {
                        bool completedQuestTen =
                            GorathQuest.TryComplete();

                        dialogue =
                            completedQuestTen
                                ? QuestTenObjectiveCompleteHail
                                : "You have Gorath's Infernal Heart and Gorath is dead, but something is preventing me from closing the record.";
                    }
                }
                else
                {
                    dialogue =
                        QuestTenActiveHail;
                }
            }
            else if (FourWoundsQuest.IsActive())
            {
                dialogue =
                    QuestElevenActiveHail;
            }
            else if (NamesThatWereBuriedQuest.IsActive())
            {
                if (NamesThatWereBuriedQuest.HasRecoveredAllNames())
                {
                    bool completedQuestTwelve =
                        NamesThatWereBuriedQuest.TryComplete();

                    dialogue =
                        completedQuestTwelve
                            ? QuestTwelveCompletionDialogue
                            : QuestTwelveReadyHail;
                }
                else
                {
                    dialogue =
                        QuestTwelveActiveHail;
                }
            }
            else if (TheWeakestWallQuest.IsActive())
            {
                dialogue =
                    QuestThirteenActiveHail;
            }
            else if (TheFracturingQuest.IsActive())
            {
                dialogue =
                    QuestFourteenActiveHail;
            }
            else if (FateOfTheFallenQuests
                .IsWhereLightFailsComplete() &&
                !FateOfTheFallenQuests.IsRestlessDeadComplete())
            {
                bool assignedQuestFour =
                    FateOfTheFallenQuests
                        .TryAssignRestlessDead();

                dialogue =
                    assignedQuestFour
                        ? QuestFourActiveHail
                        : "I have the next report in front of me, but something is preventing me from entering it into my records.";
            }
            else if (FateOfTheFallenQuests
                .IsRestlessDeadComplete() &&
                !FateOfTheFallenQuests.IsAshBeneathEarthComplete())
            {
                bool assignedQuestFive =
                    FateOfTheFallenQuests
                        .TryAssignAshBeneathEarth();

                dialogue =
                    assignedQuestFive
                        ? QuestFiveActiveHail
                        : "There is still one disturbance left to investigate, but my records are not accepting it yet.";
            }
            else if (FateOfTheFallenQuests
                .IsAshBeneathEarthComplete() &&
                !ThePatternBeneathQuest.IsComplete())
            {
                bool assignedQuestSix =
                    ThePatternBeneathQuest
                        .TryAssign();

                dialogue =
                    assignedQuestSix
                        ? QuestSixActiveHail
                        : "The four samples form a pattern, but something is preventing me from entering the next investigation into my records.";
            }
            else if (ThePatternBeneathQuest.IsComplete() &&
                !MoldermawQuest.IsComplete())
            {
                bool assignedQuestSeven =
                    MoldermawQuest
                        .TryAssign();

                dialogue =
                    assignedQuestSeven
                        ? QuestSevenActiveHail
                        : "The Blight has produced a larger threat, but something is preventing me from entering the Moldermaw investigation into my records.";
            }
            else if (MoldermawQuest.IsComplete() &&
                !NhalSeraQuest.IsComplete())
            {
                bool assignedQuestEight =
                    NhalSeraQuest.TryAssign();

                dialogue =
                    assignedQuestEight
                        ? QuestEightActiveHail
                        : "The Void disturbance has escalated, but something is preventing me from entering the Nhal'Sera investigation into my records.";
            }
            else if (NhalSeraQuest.IsComplete() &&
                !VarkeshQuest.IsComplete())
            {
                bool assignedQuestNine =
                    VarkeshQuest.TryAssign();

                dialogue =
                    assignedQuestNine
                        ? QuestNineActiveHail
                        : "The undeath disturbance has escalated, but something is preventing me from entering the Varkesh investigation into my records.";
            }
            else if (VarkeshQuest.IsComplete() &&
                !GorathQuest.IsComplete())
            {
                bool assignedQuestTen =
                    GorathQuest.TryAssign();

                dialogue =
                    assignedQuestTen
                        ? QuestTenActiveHail
                        : "The infernal disturbance has escalated, but something is preventing me from entering the Gorath investigation into my records.";
            }
            else if (GorathQuest.IsComplete() &&
                !FourWoundsQuest.IsComplete())
            {
                bool assignedQuestEleven =
                    FourWoundsQuest.TryAssign();

                dialogue =
                    assignedQuestEleven
                        ? QuestElevenActiveHail
                        : "I have the four relics together, but something is preventing me from entering the next analysis into my records.";
            }
            else if (FourWoundsQuest.IsComplete() &&
                !NamesThatWereBuriedQuest.IsComplete())
            {
                bool assignedQuestTwelve =
                    NamesThatWereBuriedQuest.TryAssign();

                dialogue =
                    assignedQuestTwelve
                        ? QuestTwelveActiveHail
                        : "The hidden inscriptions point us back to the Ancient Warding Shrine, but something is preventing me from entering the next investigation into my records.";
            }
            else if (NamesThatWereBuriedQuest.IsComplete() &&
                !TheWeakestWallQuest.IsComplete())
            {
                bool assignedQuestThirteen =
                    TheWeakestWallQuest.TryAssign();

                dialogue =
                    assignedQuestThirteen
                        ? QuestThirteenActiveHail
                        : "The order of the four manifestations matters, but something is preventing me from entering the next analysis into my records.";
            }
            else if (TheWeakestWallQuest.IsComplete() &&
                !TheFracturingQuest.IsComplete())
            {
                bool assignedQuestFourteen =
                    TheFracturingQuest.TryAssign();

                dialogue =
                    assignedQuestFourteen
                        ? QuestFourteenActiveHail
                        : "The Blight boundary is the first to fail, but something is preventing me from entering the final attunement into my records.";
            }
            else if (TheFracturingQuest.IsComplete())
            {
                dialogue =
                    QuestFourteenCompleteHail;
            }
            else if (TheWeakestWallQuest.IsComplete())
            {
                dialogue =
                    QuestThirteenCompleteHail;
            }
            else if (NamesThatWereBuriedQuest.IsComplete())
            {
                dialogue =
                    QuestTwelveCompleteHail;
            }
            else if (FourWoundsQuest.IsComplete())
            {
                dialogue =
                    QuestElevenCompleteHail;
            }
            else if (GorathQuest.IsComplete())
            {
                dialogue =
                    QuestTenCompleteHail;
            }
            else if (FateOfTheFallenQuests
                .IsAfterLastWardenComplete())
            {
                dialogue =
                    QuestOneCompleteHail;
            }
            else if (FateOfTheFallenQuests
                .IsAfterLastWardenActive())
            {
                dialogue =
                    QuestOneActiveDialogue;
            }
            else
            {
                bool assigned =
                    FateOfTheFallenQuests
                        .TryAssignAfterLastWarden();

                dialogue =
                    assigned
                        ? QuestOneOfferDialogue
                        : "I have several reports I would like you to examine, " +
                          "but my records are not ready yet.";
            }

            SetDialogReturnString(
                manager,
                dialogue);

            EnsureClickableDialogueHandler();
        }


        // Hail-based fallback. The primary Quest 2 completion path is the
        // native item hand-in exposed through Sereth's QuestManager. Keeping
        // this fallback allows a player already holding the sample to finish
        // by hailing Sereth as well.
        private static bool TryHandleSignsOfDecayTurnIn(
            out string dialogue)
        {
            dialogue =
                null;

            if (!FateOfTheFallenQuests
                .IsSignsOfDecayActive())
            {
                return false;
            }

            if (!SignsOfDecayContent
                .PlayerHasSample())
            {
                return false;
            }

            if (!SignsOfDecayContent
                .TryConsumeSample())
            {
                dialogue =
                    "Keep the sample sealed for now. Something prevented me from examining it.";

                return true;
            }

            bool completed =
                FateOfTheFallenQuests
                    .TryCompleteSignsOfDecay();

            if (!completed)
            {
                SignsOfDecayContent
                    .TryGiveSample(
                        false);

                dialogue =
                    "Something is wrong with my records. Hold onto the sample until I can correct it.";

                return true;
            }

            bool assignedQuestThree =
                FateOfTheFallenQuests
                    .TryAssignWhereLightFails();

            dialogue =
                QuestTwoCompletionDialogue;

            if (!assignedQuestThree &&
                !FateOfTheFallenQuests
                    .IsWhereLightFailsActive())
            {
                Plugin.NativeLog.LogError(
                    "Archivist Sereth: Quest 2 completed but Quest 3 [Where Light Fails] could not be assigned.");
            }

            return true;
        }


        // ============================================================
        // SHARED QUEST PROGRESSION
        // ============================================================

        private static bool TryProgressDisturbances()
        {
            FateOfTheFallenQuests.Register();

            Plugin.NativeLog.LogInfo(
                "Archivist Sereth: progressing disturbances. Q1Active=" +
                FateOfTheFallenQuests.IsAfterLastWardenActive() +
                " Q1Complete=" +
                FateOfTheFallenQuests.IsAfterLastWardenComplete() +
                " Q2Active=" +
                FateOfTheFallenQuests.IsSignsOfDecayActive() +
                " Q2Complete=" +
                FateOfTheFallenQuests.IsSignsOfDecayComplete() +
                ".");

            // Repair/repeat path: Q1 may already be complete from an earlier
            // test build while Q2 was never assigned.
            if (FateOfTheFallenQuests
                .IsAfterLastWardenComplete())
            {
                bool assignedExisting =
                    FateOfTheFallenQuests
                        .TryAssignSignsOfDecay();

                EmitSerethSpeech(
                    assignedExisting
                        ? QuestOneCompletionDialogue
                        : QuestOneCompleteHail);

                return
                    assignedExisting ||
                    FateOfTheFallenQuests
                        .IsSignsOfDecayActive();
            }

            if (!FateOfTheFallenQuests
                .IsAfterLastWardenActive())
            {
                return false;
            }

            bool completed =
                FateOfTheFallenQuests
                    .TryCompleteAfterLastWarden();

            if (!completed)
            {
                return false;
            }

            bool assignedQuestTwo =
                FateOfTheFallenQuests
                    .TryAssignSignsOfDecay();

            EmitSerethSpeech(
                QuestOneCompletionDialogue);

            if (!assignedQuestTwo &&
                !FateOfTheFallenQuests
                    .IsSignsOfDecayActive())
            {
                Plugin.NativeLog.LogError(
                    "Archivist Sereth: Quest 1 completed but Quest 2 [Signs of Decay] could not be assigned.");

                return false;
            }

            return true;
        }


        private static bool TryProgressFourWounds()
        {
            FourWoundsQuest.Register();
            FourWoundsQuest.EnsureChainState();

            Plugin.NativeLog.LogInfo(
                "Archivist Sereth: progressing Four Wounds. Q11Active=" +
                FourWoundsQuest.IsActive() +
                " Q11Complete=" +
                FourWoundsQuest.IsComplete() +
                ".");

            if (FourWoundsQuest.IsComplete())
            {
                EmitSerethSpeech(
                    QuestElevenCompleteHail);

                return true;
            }

            if (!FourWoundsQuest.IsActive())
            {
                return false;
            }

            bool completed =
                FourWoundsQuest.TryComplete();

            if (!completed)
            {
                return false;
            }

            NamesThatWereBuriedQuest.Register();
            NamesThatWereBuriedQuest.EnsureChainState();

            EmitSerethSpeech(
                QuestElevenCompletionDialogue);

            return true;
        }


        private static bool TryProgressWeakestWall()
        {
            TheWeakestWallQuest.Register();
            TheWeakestWallQuest.EnsureChainState();

            Plugin.NativeLog.LogInfo(
                "Archivist Sereth: progressing The Weakest Wall. Q13Active=" +
                TheWeakestWallQuest.IsActive() +
                " Q13Complete=" +
                TheWeakestWallQuest.IsComplete() +
                ".");

            if (TheWeakestWallQuest.IsComplete())
            {
                EmitSerethSpeech(
                    QuestThirteenCompleteHail);

                return true;
            }

            if (!TheWeakestWallQuest.IsActive())
            {
                return false;
            }

            bool completed =
                TheWeakestWallQuest.TryComplete();

            if (!completed)
            {
                return false;
            }

            TheFracturingQuest.Register();
            TheFracturingQuest.EnsureChainState();

            EmitSerethSpeech(
                QuestThirteenCompletionDialogue);

            return true;
        }


        private static bool TryProgressFracturing()
        {
            TheFracturingQuest.Register();
            TheFracturingQuest.EnsureChainState();

            Plugin.NativeLog.LogInfo(
                "Archivist Sereth: progressing The Fracturing. Q14Active=" +
                TheFracturingQuest.IsActive() +
                " Q14Complete=" +
                TheFracturingQuest.IsComplete() +
                " Attuned=" +
                TheFracturingQuest.HasBlightPlanarAttunement() +
                ".");

            if (TheFracturingQuest.IsComplete())
            {
                EmitSerethSpeech(
                    QuestFourteenCompleteHail);

                return true;
            }

            if (!TheFracturingQuest.IsActive())
            {
                return false;
            }

            bool completed =
                TheFracturingQuest.TryComplete();

            if (!completed)
            {
                return false;
            }

            EmitSerethSpeech(
                QuestFourteenCompletionDialogue);

            return true;
        }


        private static bool IsFracturingKeyword(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return false;
            }

            string trimmed =
                value.Trim();

            return
                string.Equals(
                    trimmed,
                    QuestFourteenKeyword,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    trimmed,
                    "fracturing",
                    StringComparison.OrdinalIgnoreCase);
        }


        private static bool IsWeakestWallKeyword(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return false;
            }

            return
                string.Equals(
                    value.Trim(),
                    QuestThirteenKeyword,
                    StringComparison.OrdinalIgnoreCase);
        }


        private static bool IsFourWoundsKeyword(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return false;
            }

            return
                string.Equals(
                    value.Trim(),
                    QuestElevenKeyword,
                    StringComparison.OrdinalIgnoreCase);
        }


        private static bool IsDisturbancesKeyword(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return false;
            }

            string trimmed =
                value.Trim();

            return
                string.Equals(
                    trimmed,
                    QuestOneKeyword,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    trimmed,
                    QuestOneKeywordSingular,
                    StringComparison.OrdinalIgnoreCase);
        }


        // ============================================================
        // TYPED KEYWORD
        // ============================================================

        internal static bool TryHandleTypedPhrase(
            TypeText typeText)
        {
            if (typeText == null ||
                typeText.typed == null)
            {
                return false;
            }

            string raw =
                typeText.typed.text;

            bool isFracturing =
                IsFracturingKeyword(
                    raw);

            bool isWeakestWall =
                IsWeakestWallKeyword(
                    raw);

            bool isFourWounds =
                IsFourWoundsKeyword(
                    raw);

            bool isDisturbances =
                IsDisturbancesKeyword(
                    raw);

            if (!isFracturing &&
                !isWeakestWall &&
                !isFourWounds &&
                !isDisturbances)
            {
                return false;
            }

            bool serethTargeted =
                GameData.PlayerControl != null &&
                ArchivistSerethTargeting.IsSereth(
                    GameData.PlayerControl.CurrentTarget);

            bool serethDialogueOpen =
                IsSerethLocalDialogueOpen();

            if (!serethTargeted &&
                !serethDialogueOpen)
            {
                return false;
            }

            Plugin.NativeLog.LogInfo(
                "Archivist Sereth: typed [" +
                (isFracturing
                    ? QuestFourteenKeyword
                    : (isWeakestWall
                        ? QuestThirteenKeyword
                        : (isFourWounds
                            ? QuestElevenKeyword
                            : QuestOneKeyword))) +
                "] keyword accepted. Targeted=" +
                serethTargeted +
                " DialogueOpen=" +
                serethDialogueOpen +
                ".");

            bool handled =
                isFracturing
                    ? TryProgressFracturing()
                    : (isWeakestWall
                        ? TryProgressWeakestWall()
                        : (isFourWounds
                            ? TryProgressFourWounds()
                            : TryProgressDisturbances()));

            if (handled)
            {
                typeText.typed.text =
                    string.Empty;
            }

            return handled;
        }


        // ============================================================
        // CLICKED DIALOGUE KEYWORD
        // ============================================================

        internal static bool TryHandleClickedKeyword(
            string linkId)
        {
            bool isQuestOneLink =
                string.Equals(
                    linkId,
                    QuestOneLinkId,
                    StringComparison.Ordinal);

            bool isQuestElevenLink =
                string.Equals(
                    linkId,
                    QuestElevenLinkId,
                    StringComparison.Ordinal);

            bool isQuestThirteenLink =
                string.Equals(
                    linkId,
                    QuestThirteenLinkId,
                    StringComparison.Ordinal);

            bool isQuestFourteenLink =
                string.Equals(
                    linkId,
                    QuestFourteenLinkId,
                    StringComparison.Ordinal);

            if (!isQuestOneLink &&
                !isQuestElevenLink &&
                !isQuestThirteenLink &&
                !isQuestFourteenLink)
            {
                return false;
            }

            // The clickable word lives inside Sereth's native private dialogue
            // window. Clicking UI can change/clear CurrentTarget, so the link
            // must NOT depend on the combat-target reference.
            if (!IsSerethLocalDialogueOpen())
            {
                Plugin.NativeLog.LogWarning(
                    "Archivist Sereth: quest dialogue link was clicked but the active private-dialogue header was not Sereth.");

                return false;
            }

            Plugin.NativeLog.LogInfo(
                "Archivist Sereth: clickable [" +
                (isQuestFourteenLink
                    ? QuestFourteenKeyword
                    : (isQuestThirteenLink
                        ? QuestThirteenKeyword
                        : (isQuestElevenLink
                            ? QuestElevenKeyword
                            : QuestOneKeyword))) +
                "] dialogue link activated. Header=[" +
                (GameData.LocalLog != null &&
                 GameData.LocalLog.DialogHeader != null
                    ? GameData.LocalLog.DialogHeader.text
                    : "<null>") +
                "].");

            return
                isQuestFourteenLink
                    ? TryProgressFracturing()
                    : (isQuestThirteenLink
                        ? TryProgressWeakestWall()
                        : (isQuestElevenLink
                            ? TryProgressFourWounds()
                            : TryProgressDisturbances()));
        }


        private static bool IsSerethLocalDialogueOpen()
        {
            try
            {
                if (GameData.LocalLog == null ||
                    GameData.LocalLog.DialogHeader == null)
                {
                    return false;
                }

                string header =
                    GameData.LocalLog.DialogHeader.text;

                return
                    !string.IsNullOrEmpty(
                        header) &&
                    header.IndexOf(
                        ArchivistSerethDefinition.NpcName,
                        StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }


        // ============================================================
        // CLICKABLE LOCAL-DIALOGUE LINK INSTALL
        // ============================================================

        private static void EnsureClickableDialogueHandler()
        {
            try
            {
                if (GameData.LocalLog == null ||
                    GameData.LocalLog.myText == null)
                {
                    return;
                }

                TextMeshProUGUI text =
                    GameData.LocalLog.myText;

                ArchivistSerethDialogueLinkHandler handler =
                    text.GetComponent<
                        ArchivistSerethDialogueLinkHandler>();

                if (handler == null)
                {
                    handler =
                        text.gameObject.AddComponent<
                            ArchivistSerethDialogueLinkHandler>();
                }

                handler.Bind(
                    text);

                text.raycastTarget =
                    true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "Archivist Sereth: could not install clickable dialogue handler: " +
                    exception.Message);
            }
        }


        private static void EmitSerethSpeech(
            string dialogue)
        {
            if (string.IsNullOrEmpty(
                    dialogue))
            {
                return;
            }

            try
            {
                string line =
                    ArchivistSerethDefinition.NpcName +
                    " says: " +
                    dialogue;

                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        line,
                        ChatLogLine.LogType.Say,
                        ""));

                UpdateSocialLog.LocalLogAdd(
                    line,
                    "white");
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Archivist Sereth: failed to emit quest dialogue",
                    exception);
            }
        }


        private static void SetDialogReturnString(
            NPCDialogManager manager,
            string dialogue)
        {
            if (manager == null ||
                string.IsNullOrEmpty(
                    dialogue))
            {
                return;
            }

            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            try
            {
                FieldInfo field =
                    typeof(NPCDialogManager).GetField(
                        "ReturnString",
                        flags);

                if (field != null &&
                    field.FieldType == typeof(string))
                {
                    field.SetValue(
                        manager,
                        dialogue);

                    return;
                }

                PropertyInfo property =
                    typeof(NPCDialogManager).GetProperty(
                        "ReturnString",
                        flags);

                if (property != null &&
                    property.CanWrite &&
                    property.PropertyType == typeof(string))
                {
                    property.SetValue(
                        manager,
                        dialogue,
                        null);
                }
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Archivist Sereth: failed to set dialogue",
                    exception);
            }
        }
    }


    // ================================================================
    // TMP CLICK HANDLER
    // ================================================================
    //
    // Erenshor's private dialogue UI can consume pointer events before they
    // reach the TMP child. We support BOTH:
    //
    // 1. IPointerClickHandler
    // 2. direct Input.GetMouseButtonDown(0) polling
    //
    // Both routes resolve the actual TMP <link> under the cursor and are
    // frame-deduplicated.
    // ================================================================

    internal sealed class ArchivistSerethDialogueLinkHandler :
        MonoBehaviour,
        IPointerClickHandler
    {
        private TextMeshProUGUI _text;

        private int _lastHandledFrame =
            -1;


        internal void Bind(
            TextMeshProUGUI text)
        {
            _text =
                text;
        }


        private void Update()
        {
            if (_text == null ||
                !Input.GetMouseButtonDown(
                    0))
            {
                return;
            }

            TryHandleScreenClick(
                Input.mousePosition,
                ResolveEventCamera());
        }


        public void OnPointerClick(
            PointerEventData eventData)
        {
            if (_text == null ||
                eventData == null)
            {
                return;
            }

            Camera eventCamera =
                eventData.pressEventCamera;

            if (eventCamera == null)
            {
                eventCamera =
                    ResolveEventCamera();
            }

            TryHandleScreenClick(
                eventData.position,
                eventCamera);
        }


        private void TryHandleScreenClick(
            Vector2 screenPosition,
            Camera eventCamera)
        {
            if (_text == null ||
                _lastHandledFrame ==
                    Time.frameCount)
            {
                return;
            }

            try
            {
                _text.ForceMeshUpdate();

                int linkIndex =
                    FindLinkIndex(
                        screenPosition,
                        eventCamera);

                if (linkIndex < 0 ||
                    linkIndex >=
                        _text.textInfo.linkCount)
                {
                    return;
                }

                TMP_LinkInfo linkInfo =
                    _text.textInfo
                        .linkInfo[linkIndex];

                string linkId =
                    linkInfo.GetLinkID();

                bool supportedLink =
                    string.Equals(
                        linkId,
                        ArchivistSerethDialogue
                            .QuestOneLinkId,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        linkId,
                        ArchivistSerethDialogue
                            .QuestElevenLinkId,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        linkId,
                        ArchivistSerethDialogue
                            .QuestThirteenLinkId,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        linkId,
                        ArchivistSerethDialogue
                            .QuestFourteenLinkId,
                        StringComparison.Ordinal);

                if (!supportedLink)
                {
                    return;
                }

                _lastHandledFrame =
                    Time.frameCount;

                bool handled =
                    ArchivistSerethDialogue
                        .TryHandleClickedKeyword(
                            linkId);

                if (!handled)
                {
                    Plugin.NativeLog.LogWarning(
                        "Archivist Sereth: quest dialogue link was detected but quest progression did not run.");
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "Archivist Sereth: clickable dialogue link failed: " +
                    exception.Message);
            }
        }


        private int FindLinkIndex(
            Vector2 screenPosition,
            Camera preferredCamera)
        {
            // TextMeshPro performs its own hit test against the generated
            // link geometry, so we do not need UnityEngine.UI helpers here.
            //
            // Try the event-system camera first when one exists.
            if (preferredCamera != null)
            {
                int preferred =
                    TMP_TextUtilities
                        .FindIntersectingLink(
                            _text,
                            screenPosition,
                            preferredCamera);

                if (preferred >= 0)
                {
                    return preferred;
                }
            }

            // Screen-space overlay canvases use a null event camera.
            int overlay =
                TMP_TextUtilities
                    .FindIntersectingLink(
                        _text,
                        screenPosition,
                        null);

            if (overlay >= 0)
            {
                return overlay;
            }

            // Final fallback for camera/world-space UI without importing
            // UnityEngine.UIModule types.
            Camera mainCamera =
                Camera.main;

            if (mainCamera != null &&
                mainCamera != preferredCamera)
            {
                return
                    TMP_TextUtilities
                        .FindIntersectingLink(
                            _text,
                            screenPosition,
                            mainCamera);
            }

            return -1;
        }


        private Camera ResolveEventCamera()
        {
            // Avoid a compile-time dependency on UnityEngine.UIModule.
            // The pointer-event path supplies pressEventCamera when available.
            // The polled mouse path will try null and Camera.main itself.
            return null;
        }
    }


    [HarmonyPatch(
        typeof(NPCDialogManager),
        "GenericHail")]
    internal static class Patch_ArchivistSerethDialogue
    {
        [HarmonyPostfix]
        private static void Postfix(
            NPCDialogManager __instance)
        {
            ArchivistSerethDialogue
                .HandleGenericHail(
                    __instance);
        }
    }


    /// <summary>
    /// Ordinary NPC dialogue text is submitted through TypeText.SubmitString,
    /// while slash/dev commands use CheckCommands. Patch both so the keyword
    /// works regardless of which native path Erenshor routes the input through.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_ArchivistSerethQuestPhrase
    {
        private static System.Collections.Generic.IEnumerable<MethodBase>
            TargetMethods()
        {
            MethodInfo[] methods =
                typeof(TypeText).GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            for (int i = 0;
                 i < methods.Length;
                 i++)
            {
                MethodInfo method =
                    methods[i];

                if (method == null)
                {
                    continue;
                }

                if (string.Equals(
                        method.Name,
                        "SubmitString",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        method.Name,
                        "CheckCommands",
                        StringComparison.Ordinal))
                {
                    yield return method;
                }
            }
        }


        [HarmonyPrefix]
        private static bool Prefix(
            TypeText __instance)
        {
            if (__instance == null ||
                __instance.typed == null)
            {
                return true;
            }

            if (!IsCandidateKeyword(
                    __instance.typed.text))
            {
                return true;
            }

            bool handled =
                ArchivistSerethDialogue
                    .TryHandleTypedPhrase(
                        __instance);

            if (!handled)
            {
                return true;
            }

            return false;
        }


        private static bool IsCandidateKeyword(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return false;
            }

            string trimmed =
                value.Trim();

            return
                string.Equals(
                    trimmed,
                    ArchivistSerethDialogue
                        .QuestOneKeyword,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    trimmed,
                    ArchivistSerethDialogue
                        .QuestOneKeywordSingular,
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
