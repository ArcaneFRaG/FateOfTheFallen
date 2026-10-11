using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 12 - Names That Were Buried.
    ///
    /// Sereth sends the player back to the Ancient Warding Shrine in Port
    /// Azure. Hidden inscriptions on the rear/lower faces of the four keeper
    /// statues preserve the titles of the Fallen each keeper guarded. The
    /// player must inspect all four statues, then return to Sereth.
    /// </summary>
    internal static class NamesThatWereBuriedQuest
    {
        internal const string QuestId =
            "FOTF_MAIN_12_NAMES_THAT_WERE_BURIED";

        internal const string QuestName =
            "Names That Were Buried";

        // Legacy r9/r10 objective credit. Kept only so test characters that
        // already read the removed stele can be migrated without losing
        // quest progress.
        internal const string ArchiveReadCreditId =
            "FOTF_CREDIT_Q12_WARDEN_ARCHIVE_READ";

        internal const string SolunaInscriptionCreditId =
            "FOTF_CREDIT_Q12_SOLUNA_INSCRIPTION";

        internal const string BraxInscriptionCreditId =
            "FOTF_CREDIT_Q12_BRAX_INSCRIPTION";

        internal const string VitheanInscriptionCreditId =
            "FOTF_CREDIT_Q12_VITHEAN_INSCRIPTION";

        internal const string FernallaInscriptionCreditId =
            "FOTF_CREDIT_Q12_FERNALLA_INSCRIPTION";

        private static Quest _quest;

        internal static Quest Quest =>
            _quest;


        internal static void Register()
        {
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
                        "Fate of the Fallen: failed to create Quest 12 [Names That Were Buried].");

                    return;
                }
            }

            Quest fourWounds =
                FourWoundsQuest.Quest;

            if (fourWounds != null)
            {
                fourWounds.AssignNewQuestOnComplete =
                    _quest;
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: Quest 12 registered before Quest 11 was available; Quest 11 -> Quest 12 will retry.");
            }

            CustomQuestRegistry.Register(
                _quest);
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
                "FOTF Quest 12 - Names That Were Buried";

            quest.DBName =
                QuestId;

            quest.QuestName =
                QuestName;

            quest.QuestDesc =
                "The oldest records identify the powers beyond the four wounds only as the Fallen. " +
                "Sereth believes their names were hidden in plain sight on the Ancient Warding Shrine you examined before.\n\n" +
                "Return to the Ancient Warding Shrine in Port Azure. Inspect all four keeper statues—Soluna, Brax, Vithean, and Fernalla. " +
                "The surviving inscriptions are carved low on the outward/rear faces where a casual visitor would not normally see them. " +
                "Copy all four names, then return to Archivist Sereth.";

            quest.RequiredItems =
                new List<Item>();

            quest.DialogOnSuccess =
                "Hidden on the backs of the statues. Of course. The shrine itself was the record, and only a Warden inspecting it closely would ever think to look there.\n\n" +
                "Fernalla names the Lord of Blight. Soluna names the Shrouded Mother. Vithean names the Deathless One. Brax names the Demon King. " +
                "Perhaps those are titles rather than true names, but they are the names the keepers themselves preserved for the prisoners they guarded.\n\n" +
                "Four keepers. Four prisons. Four Fallen. Now we finally know who is pressing against each wound.";

            quest.DialogOnPartialSuccess =
                "Go back to the Ancient Warding Shrine in Port Azure. You already examined its central seal; this time inspect the four keeper statues themselves. " +
                "Look low and behind them. Anything meant for Wardens alone would have been carved where a visitor standing at the altar would never notice it.";

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

            // Quest 13 attaches during TheWeakestWallQuest.Register().
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
            return
                ContainsQuestId(
                    GameData.HasQuest,
                    QuestId);
        }


        internal static bool IsComplete()
        {
            return
                ContainsQuestId(
                    GameData.CompletedQuests,
                    QuestId);
        }


        internal static bool HasReadArchive()
        {
            // Compatibility alias for the removed r9/r10 stele runtime.
            return HasRecoveredAllNames();
        }


        internal static bool HasRecoveredAllNames()
        {
            return
                HasInscriptionCredit(SolunaInscriptionCreditId) &&
                HasInscriptionCredit(BraxInscriptionCreditId) &&
                HasInscriptionCredit(VitheanInscriptionCreditId) &&
                HasInscriptionCredit(FernallaInscriptionCreditId);
        }


        internal static int GetRecoveredNameCount()
        {
            int count = 0;

            if (HasInscriptionCredit(SolunaInscriptionCreditId)) count++;
            if (HasInscriptionCredit(BraxInscriptionCreditId)) count++;
            if (HasInscriptionCredit(VitheanInscriptionCreditId)) count++;
            if (HasInscriptionCredit(FernallaInscriptionCreditId)) count++;

            return count;
        }


        internal static bool HasInspectedStatue(
            string keeperName)
        {
            string creditId =
                GetStatueCreditId(keeperName);

            return
                !string.IsNullOrEmpty(creditId) &&
                HasInscriptionCredit(creditId);
        }


        internal static bool TryMarkStatueInscription(
            string keeperName)
        {
            Register();
            EnsureChainState();

            if (IsComplete())
            {
                return true;
            }

            if (!IsActive())
            {
                return false;
            }

            string creditId =
                GetStatueCreditId(keeperName);

            if (string.IsNullOrEmpty(creditId))
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest12] unknown keeper statue inscription [" +
                    (keeperName ?? "<null>") +
                    "].");

                return false;
            }

            if (HasInscriptionCredit(creditId))
            {
                return true;
            }

            bool awarded =
                FOTFCharacterMilestoneStore.AwardCredit(
                    creditId);

            if (!awarded &&
                !HasInscriptionCredit(creditId))
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest12] failed to persist hidden inscription credit for [" +
                    keeperName +
                    "].");

                return false;
            }

            Plugin.NativeLog.LogInfo(
                "[Quest12] hidden keeper inscription recovered. Keeper=[" +
                keeperName +
                "] Fallen=[" +
                GetFallenTitle(keeperName) +
                "] Progress=[" +
                GetRecoveredNameCount() +
                "/4].");

            return true;
        }


        internal static string GetFallenTitle(
            string keeperName)
        {
            if (string.Equals(keeperName, "Soluna", StringComparison.OrdinalIgnoreCase))
            {
                return "Shrouded Mother";
            }

            if (string.Equals(keeperName, "Brax", StringComparison.OrdinalIgnoreCase))
            {
                return "Demon King";
            }

            if (string.Equals(keeperName, "Vithean", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(keeperName, "Vitheo", StringComparison.OrdinalIgnoreCase))
            {
                return "Deathless One";
            }

            if (string.Equals(keeperName, "Fernalla", StringComparison.OrdinalIgnoreCase))
            {
                return "Lord of Blight";
            }

            return string.Empty;
        }


        private static string GetStatueCreditId(
            string keeperName)
        {
            if (string.Equals(keeperName, "Soluna", StringComparison.OrdinalIgnoreCase))
            {
                return SolunaInscriptionCreditId;
            }

            if (string.Equals(keeperName, "Brax", StringComparison.OrdinalIgnoreCase))
            {
                return BraxInscriptionCreditId;
            }

            if (string.Equals(keeperName, "Vithean", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(keeperName, "Vitheo", StringComparison.OrdinalIgnoreCase))
            {
                return VitheanInscriptionCreditId;
            }

            if (string.Equals(keeperName, "Fernalla", StringComparison.OrdinalIgnoreCase))
            {
                return FernallaInscriptionCreditId;
            }

            return null;
        }


        private static bool HasInscriptionCredit(
            string creditId)
        {
            return
                !string.IsNullOrEmpty(creditId) &&
                FOTFCharacterMilestoneStore.HasCredit(
                    creditId);
        }


        internal static bool TryAssign()
        {
            Register();

            if (_quest == null)
            {
                return false;
            }

            if (IsComplete() ||
                IsActive())
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
                        "[Quest12] native AssignQuest threw; using direct active-quest fallback. " +
                        exception.Message);
                }
            }
            else
            {
                Plugin.NativeLog.LogWarning(
                    "[Quest12] QuestDB does not yet resolve [" +
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

            Plugin.NativeLog.LogInfo(
                "[Quest12] assignment result. Active=[" +
                IsActive() +
                "] Complete=[" +
                IsComplete() +
                "] NamesRecovered=[" +
                GetRecoveredNameCount() +
                "/4" +
                "] QuestDbResolved=[" +
                nativeResolved +
                "].");

            return IsActive();
        }


        internal static void EnsureChainState()
        {
            if (!FOTFExpansionGate.IsUnlocked)
            {
                return;
            }

            Register();

            MigrateLegacySteleCredit();

            if (FourWoundsQuest.IsComplete() &&
                !IsActive() &&
                !IsComplete())
            {
                Plugin.NativeLog.LogInfo(
                    "[Quest12] Quest 11 is complete but Quest 12 is not active; repairing quest-chain state.");

                TryAssign();
            }
        }


        internal static bool TryMarkArchiveRead()
        {
            // Compatibility entry point for the removed r9/r10 stele runtime.
            // If an old object somehow invokes this method, preserve the old
            // all-at-once completion semantics rather than corrupting progress.
            Register();
            EnsureChainState();

            if (!IsActive() &&
                !IsComplete())
            {
                return false;
            }

            AwardAllStatueCredits();

            return HasRecoveredAllNames();
        }


        internal static bool TryComplete()
        {
            Register();
            EnsureChainState();

            if (IsComplete())
            {
                return true;
            }

            if (!IsActive() ||
                !HasRecoveredAllNames())
            {
                return false;
            }

            bool nativeResolved =
                GameData.QuestDB != null &&
                GameData.QuestDB.GetQuestByName(
                    QuestId) != null;

            if (nativeResolved)
            {
                try
                {
                    GameData.FinishQuest(
                        QuestId);
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogWarning(
                        "[Quest12] native FinishQuest threw; using completed-quest fallback. " +
                        exception.Message);
                }
            }

            if (!IsComplete())
            {
                RemoveQuestId(
                    GameData.HasQuest,
                    QuestId);

                AddQuestId(
                    GameData.CompletedQuests,
                    QuestId);
            }

            if (!IsComplete())
            {
                return false;
            }

            RemoveQuestId(
                GameData.HasQuest,
                QuestId);

            PlayQuestCompleteSound();

            // Reassert/repair the next link immediately so characters that
            // complete Q12 through Sereth receive Q13 without a reload.
            TheWeakestWallQuest.Register();
            TheWeakestWallQuest.EnsureChainState();

            Plugin.NativeLog.LogInfo(
                "[Quest12] Names That Were Buried completed. All four hidden keeper-statue inscriptions were recovered. Quest 13 [The Weakest Wall] Active=[" +
                TheWeakestWallQuest.IsActive() +
                "] Complete=[" +
                TheWeakestWallQuest.IsComplete() +
                "].");

            return true;
        }


        internal static void ResetObjectiveCredit()
        {
            FOTFCharacterMilestoneStore.RevokeCredit(ArchiveReadCreditId);
            FOTFCharacterMilestoneStore.RevokeCredit(SolunaInscriptionCreditId);
            FOTFCharacterMilestoneStore.RevokeCredit(BraxInscriptionCreditId);
            FOTFCharacterMilestoneStore.RevokeCredit(VitheanInscriptionCreditId);
            FOTFCharacterMilestoneStore.RevokeCredit(FernallaInscriptionCreditId);
        }


        private static void MigrateLegacySteleCredit()
        {
            if (GameData.PlayerControl == null ||
                HasRecoveredAllNames() ||
                !FOTFCharacterMilestoneStore.HasCredit(ArchiveReadCreditId))
            {
                return;
            }

            AwardAllStatueCredits();

            Plugin.NativeLog.LogInfo(
                "[Quest12] migrated legacy Warden Archive credit to the four hidden statue inscriptions.");
        }


        private static void AwardAllStatueCredits()
        {
            FOTFCharacterMilestoneStore.AwardCredit(SolunaInscriptionCreditId);
            FOTFCharacterMilestoneStore.AwardCredit(BraxInscriptionCreditId);
            FOTFCharacterMilestoneStore.AwardCredit(VitheanInscriptionCreditId);
            FOTFCharacterMilestoneStore.AwardCredit(FernallaInscriptionCreditId);
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

                    return
                        ContainsQuestId(
                            collection,
                            id);
                }

                var add =
                    AccessTools.Method(
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

                return
                    ContainsQuestId(
                        collection,
                        id);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Quest12] failed adding quest state [" +
                    id +
                    "]: " +
                    exception);

                return false;
            }
        }


        private static bool RemoveQuestId(
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
                    for (int i =
                            list.Count - 1;
                         i >= 0;
                         i--)
                    {
                        if (!string.Equals(
                                list[i] as string,
                                id,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        list.RemoveAt(
                            i);
                    }

                    return
                        !ContainsQuestId(
                            collection,
                            id);
                }

                var remove =
                    AccessTools.Method(
                        collection.GetType(),
                        "Remove",
                        new Type[]
                        {
                            typeof(string)
                        });

                if (remove == null)
                {
                    return false;
                }

                remove.Invoke(
                    collection,
                    new object[]
                    {
                        id
                    });

                return
                    !ContainsQuestId(
                        collection,
                        id);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Quest12] failed removing quest state [" +
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
                    "[Quest12] failed to play quest completion sound: " +
                    exception.Message);
            }
        }
    }
}
