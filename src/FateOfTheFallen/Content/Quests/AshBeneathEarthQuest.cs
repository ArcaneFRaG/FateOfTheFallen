using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 5 - Ash Beneath the Earth.
    ///
    /// Kept additive rather than modifying FateOfTheFallenQuests directly.
    /// A Harmony postfix on FateOfTheFallenQuests.Register() creates/registers
    /// this quest and links Quest 4 -> Quest 5.
    /// </summary>
    internal static class AshBeneathEarthQuest
    {
        internal const string QuestId =
            "FOTF_MAIN_05_ASH_BENEATH_THE_EARTH";

        internal const string QuestName =
            "Ash Beneath the Earth";

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
                        "Fate of the Fallen: failed to create Quest 5 [Ash Beneath the Earth].");

                    return;
                }
            }

            // Quest 4 is owned by the existing core quest registry.
            // Reassert this link every time registration runs so native
            // database refreshes cannot leave the chain disconnected.
            Quest restlessDead =
                FateOfTheFallenQuests.RestlessDeadQuest;

            if (restlessDead != null)
            {
                restlessDead.AssignNewQuestOnComplete =
                    _quest;
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: Quest 5 registered before Quest 4 was available; Quest 4 -> Quest 5 link will retry on the next registration pass.");
            }

            CustomQuestRegistry.Register(
                _quest);

            AshBeneathEarthContent
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
                "FOTF Quest 05 - Ash Beneath the Earth";

            quest.DBName =
                QuestId;

            quest.QuestName =
                QuestName;

            quest.QuestDesc =
                "The first three disturbances carried distinct signatures: " +
                "blight, void, and undeath. Sereth's final report describes " +
                "scorched ground in Blacksalt Strand where fire burns from a " +
                "fissure in the earth without consuming the surrounding shore.\n\n" +
                "Travel to Blacksalt Strand and investigate the Flame Rift. " +
                "Recover a Cindered Rift Shard from the disturbance, then return " +
                "it to Archivist Sereth in Port Azure.";

            quest.RequiredItems =
                new List<Item>();

            quest.DialogOnSuccess =
                "This is no natural fire. The heat is real, but the residue " +
                "beneath it carries something older—deliberate, hostile, and " +
                "unmistakably demonic. Blight. Void. Undeath. Now this. Four " +
                "disturbances, four different signatures, all appearing after " +
                "the Wardens fell. We are no longer looking at isolated failures.";

            quest.DialogOnPartialSuccess =
                "The shard I need will be close to the heart of the rift. " +
                "Bring me a piece that still carries whatever is feeding the flame.";

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
                        "[Quest5] native AssignQuest threw; attempting direct active-quest fallback. " +
                        exception.Message);
                }
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest5] QuestDB does not yet resolve [" +
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
                "[Quest5] assignment result. Active=[" +
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

            // If Quest 4 was already completed before this Quest 5 code was
            // installed, AssignNewQuestOnComplete had no opportunity to fire.
            // Repair that state automatically on startup / scene changes.
            if (FateOfTheFallenQuests.IsRestlessDeadComplete() &&
                !IsActive() &&
                !IsComplete())
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest5] Quest 4 is complete but Quest 5 is not active; repairing quest-chain state.");

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
                    "[Quest5] failed adding quest state [" +
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
