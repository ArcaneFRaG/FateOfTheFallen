using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 6 - The Pattern Beneath.
    ///
    /// Kept additive rather than modifying FateOfTheFallenQuests directly,
    /// matching the current Quest 5 registration pattern.
    /// </summary>
    internal static class ThePatternBeneathQuest
    {
        internal const string QuestId =
            "FOTF_MAIN_06_THE_PATTERN_BENEATH";

        internal const string QuestName =
            "The Pattern Beneath";

        private static Quest _quest;

        internal static Quest Quest =>
            _quest;


        internal static void Register()
        {
            // Expansion content remains hidden until the server gate opens.
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
                        "Fate of the Fallen: failed to create Quest 6 [The Pattern Beneath].");

                    return;
                }
            }

            // Quest 5 is additive too. Reassert the chain every time
            // registration runs so native database refreshes cannot leave
            // Quest 5 -> Quest 6 disconnected.
            Quest ashBeneathEarth =
                AshBeneathEarthQuest.Quest;

            if (ashBeneathEarth != null)
            {
                ashBeneathEarth.AssignNewQuestOnComplete =
                    _quest;
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: Quest 6 registered before Quest 5 was available; Quest 5 -> Quest 6 link will retry on the next registration pass.");
            }

            CustomQuestRegistry.Register(
                _quest);

            ThePatternBeneathContent
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
                "FOTF Quest 06 - The Pattern Beneath";

            quest.DBName =
                QuestId;

            quest.QuestName =
                QuestName;

            quest.QuestDesc =
                "Sereth has compared the four samples recovered from the disturbances. " +
                "Blight, void, undeath, and infernal corruption are fundamentally different, " +
                "yet each carries the same distortion around its edge. He believes the incidents " +
                "are symptoms of damage to an older boundary.\n\n" +
                "Inspect the Ancient Warding Shrine in Port Azure. Recover a Fractured Warding " +
                "Fragment from the central seal, then return it to Archivist Sereth.";

            quest.RequiredItems =
                new List<Item>();

            quest.DialogOnSuccess =
                "There it is. The same fracture. These marks predate the disturbances by centuries. " +
                "The Wardens did not merely stand watch over places; they maintained a boundary. " +
                "Four prisons, four separate pressures, all bearing against the same wounded system. " +
                "When the Wardens fell, that pressure began escaping through the cracks.\n\n" +
                "But this damage troubles me. Fractures can spread under strain. They can also be widened. " +
                "We need to know which happened here.";

            quest.DialogOnPartialSuccess =
                "The shrine is old enough to predate the disturbances. Examine the central seal closely. " +
                "If its fracture matches the samples, bring me a piece of the damaged warding stone.";

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
            return ContainsQuestId(
                GameData.HasQuest,
                QuestId);
        }


        internal static bool IsComplete()
        {
            return ContainsQuestId(
                GameData.CompletedQuests,
                QuestId);
        }


        internal static bool TryAssign()
        {
            Register();

            if (_quest == null)
            {
                return false;
            }

            if (IsActive() ||
                IsComplete())
            {
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
                        "[Quest6] native AssignQuest threw; attempting direct active-quest fallback. " +
                        exception.Message);
                }
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest6] QuestDB does not yet resolve [" +
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

            bool active =
                IsActive();

            Plugin.NativeLog.LogInfo(
                "[Quest6] assignment result. Active=[" +
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

            // Repair characters that completed Quest 5 before Quest 6 existed.
            if (AshBeneathEarthQuest.IsComplete() &&
                !IsActive() &&
                !IsComplete())
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest6] Quest 5 is complete but Quest 6 is not active; repairing quest-chain state.");

                TryAssign();
            }
        }


        private static bool AddQuestId(
            object collection,
            string id)
        {
            if (collection == null ||
                string.IsNullOrEmpty(id))
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
                    HarmonyLib.AccessTools.Method(
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
                    "[Quest6] failed adding quest state [" +
                    id +
                    "]: " +
                    exception);

                return false;
            }
        }


        private static bool ContainsQuestId(
            object collection,
            string id)
        {
            if (collection == null ||
                string.IsNullOrEmpty(id))
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
                    HarmonyLib.AccessTools.Method(
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
    }
}
