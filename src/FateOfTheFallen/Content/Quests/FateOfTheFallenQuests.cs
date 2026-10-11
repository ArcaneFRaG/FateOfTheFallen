using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class FateOfTheFallenQuests
    {
        internal const string AfterLastWardenQuestId =
            "FOTF_MAIN_01_AFTER_THE_LAST_WARDEN";

        internal const string SignsOfDecayQuestId =
            "FOTF_MAIN_02_SIGNS_OF_DECAY";

        internal const string WhereLightFailsQuestId =
            "FOTF_MAIN_03_WHERE_LIGHT_FAILS";

        internal const string RestlessDeadQuestId =
            "FOTF_MAIN_04_THE_RESTLESS_DEAD";

        internal const string AshBeneathEarthQuestId =
            "FOTF_MAIN_05_ASH_BENEATH_THE_EARTH";


        private static Quest _afterLastWardenQuest;
        private static Quest _signsOfDecayQuest;
        private static Quest _whereLightFailsQuest;
        private static Quest _restlessDeadQuest;
        private static Quest _ashBeneathEarthQuest;


        internal static Quest AfterLastWardenQuest =>
            _afterLastWardenQuest;

        internal static Quest SignsOfDecayQuest =>
            _signsOfDecayQuest;

        internal static Quest WhereLightFailsQuest =>
            _whereLightFailsQuest;

        internal static Quest RestlessDeadQuest =>
            _restlessDeadQuest;

        internal static Quest AshBeneathEarthQuest =>
            _ashBeneathEarthQuest;


        internal static void Register()
        {
            LegacyFOTFQuestCleanup
                .RemoveObsoleteQuestState();

            if (_ashBeneathEarthQuest == null)
            {
                _ashBeneathEarthQuest =
                    CreateAshBeneathEarthQuest();

                if (_ashBeneathEarthQuest == null)
                {
                    Plugin.NativeLog.LogError(
                        "Fate of the Fallen: failed to create Quest 5 [Ash Beneath the Earth].");

                    return;
                }
            }

            if (_restlessDeadQuest == null)
            {
                _restlessDeadQuest =
                    CreateRestlessDeadQuest();

                if (_restlessDeadQuest == null)
                {
                    Plugin.NativeLog.LogError(
                        "Fate of the Fallen: failed to create Quest 4 [The Restless Dead].");

                    return;
                }
            }

            if (_whereLightFailsQuest == null)
            {
                _whereLightFailsQuest =
                    CreateWhereLightFailsQuest();

                if (_whereLightFailsQuest == null)
                {
                    Plugin.NativeLog.LogError(
                        "Fate of the Fallen: failed to create Quest 3 [Where Light Fails].");

                    return;
                }
            }

            if (_signsOfDecayQuest == null)
            {
                _signsOfDecayQuest =
                    CreateSignsOfDecayQuest();

                if (_signsOfDecayQuest == null)
                {
                    Plugin.NativeLog.LogError(
                        "Fate of the Fallen: failed to create Quest 2 [Signs of Decay].");

                    return;
                }
            }

            if (_afterLastWardenQuest == null)
            {
                _afterLastWardenQuest =
                    CreateAfterLastWardenQuest();

                if (_afterLastWardenQuest == null)
                {
                    Plugin.NativeLog.LogError(
                        "Fate of the Fallen: failed to create Quest 1 [After the Last Warden].");

                    return;
                }
            }

            _afterLastWardenQuest
                .AssignNewQuestOnComplete =
                    _signsOfDecayQuest;

            _signsOfDecayQuest
                .AssignNewQuestOnComplete =
                    _whereLightFailsQuest;

            _whereLightFailsQuest
                .AssignNewQuestOnComplete =
                    _restlessDeadQuest;

            _restlessDeadQuest
                .AssignNewQuestOnComplete =
                    _ashBeneathEarthQuest;

            CustomQuestRegistry.Register(
                _afterLastWardenQuest);

            CustomQuestRegistry.Register(
                _signsOfDecayQuest);

            CustomQuestRegistry.Register(
                _whereLightFailsQuest);

            CustomQuestRegistry.Register(
                _restlessDeadQuest);

            CustomQuestRegistry.Register(
                _ashBeneathEarthQuest);

            SignsOfDecayContent
                .LinkQuestRequirement();

            WhereLightFailsContent
                .LinkQuestRequirement();

            RestlessDeadContent
                .LinkQuestRequirement();
        }


        private static Quest CreateAfterLastWardenQuest()
        {
            return
                CreateBaseQuest(
                    "FOTF Quest 01 - After the Last Warden",
                    AfterLastWardenQuestId,
                    "After the Last Warden",
                    "Archivist Sereth has been comparing reports written in " +
                    "the days following the fall of the last corrupted Warden. " +
                    "Several apparently unrelated disturbances began at nearly " +
                    "the same time.\n\n" +
                    "Speak with Archivist Sereth in Port Azure and ask him " +
                    "about the disturbances.\n\n" +
                    "Say: disturbances");
        }


        private static Quest CreateSignsOfDecayQuest()
        {
            Quest quest =
                CreateBaseQuest(
                    "FOTF Quest 02 - Signs of Decay",
                    SignsOfDecayQuestId,
                    "Signs of Decay",
                    "Sereth believes the blight reports are the most consistent " +
                    "of the disturbances recorded after the fall of the Wardens.\n\n" +
                    "Travel to Loomingwood and investigate the patch of vegetation " +
                    "that has rapidly collapsed into unnatural decay. Recover a " +
                    "fresh sample of Blighted Spores, then return the sample to " +
                    "Archivist Sereth in Port Azure.");

            if (quest != null)
            {
                quest.DialogOnSuccess =
                    "These spores are unfamiliar. There is blight in them, " +
                    "certainly, but it is translated poorly into our world—as " +
                    "though whatever produced them was never meant to take root " +
                    "here. That matters. If the other disturbances carry " +
                    "different signatures, then we are not looking at a single " +
                    "spreading corruption.";

                quest.DialogOnPartialSuccess =
                    "The affected ground should be easy enough to recognise. " +
                    "Find whatever continues to grow there and bring me a sample.";
            }

            return
                quest;
        }


        private static Quest CreateWhereLightFailsQuest()
        {
            Quest quest =
                CreateBaseQuest(
                    "FOTF Quest 03 - Where Light Fails",
                    WhereLightFailsQuestId,
                    "Where Light Fails",
                    "The spores from Loomingwood carry a distinct blight signature. " +
                    "Sereth now wants evidence from a second disturbance: a place " +
                    "where daylight itself seems diminished.\n\n" +
                    "Investigate the dark disturbance in Soluna's Landing " +
                    "and recover Veiled Residue for Archivist Sereth.");

            if (quest != null)
            {
                quest.DialogOnSuccess =
                    "This is not blight. There is no rot in it, no growth, no hunger. " +
                    "Light around it simply disappears. Two disturbances, two different " +
                    "signatures. Whatever has begun, we are not dealing with a single breach.";

                quest.DialogOnPartialSuccess =
                    "Bring me the residue from the heart of that dark disturbance. " +
                    "I need something I can examine here, away from whatever is affecting the light.";
            }

            return
                quest;
        }


        private static Quest CreateRestlessDeadQuest()
        {
            Quest quest =
                CreateBaseQuest(
                    "FOTF Quest 04 - The Restless Dead",
                    RestlessDeadQuestId,
                    "The Restless Dead",
                    "Sereth's third report concerns graves disturbed from within. " +
                    "Several bodies have risen without any visible ritual, binding, " +
                    "or known necromancer nearby.\n\n" +
                    "Investigate one of the disturbed graves within Fernalla's Revival Plains, destroy one of the risen dead, " +
                    "recover the Lingering Soul-Essence that remains, and return it to " +
                    "Archivist Sereth in Port Azure.");

            if (quest != null)
            {
                quest.DialogOnSuccess =
                    "No necromantic binding. No summoning mark. This soul appears to have " +
                    "been prevented from leaving rather than called back. Three disturbances, " +
                    "and three entirely different signatures. Unfortunately, there is a fourth.";

                quest.DialogOnPartialSuccess =
                    "The reports describe the dead rising without ritual. Destroy one of them " +
                    "and bring me whatever remains when its body can no longer hold it.";
            }

            return
                quest;
        }


        private static Quest CreateAshBeneathEarthQuest()
        {
            Quest quest =
                CreateBaseQuest(
                    "FOTF Quest 05 - Ash Beneath the Earth",
                    AshBeneathEarthQuestId,
                    "Ash Beneath the Earth",
                    "A fourth disturbance remains. A section of stone has split open and begun " +
                    "radiating heat without any natural source. Small embers have reportedly " +
                    "formed around the fracture.\n\n" +
                    "Recover an Infernal Ember from the scorched fracture and return it to " +
                    "Archivist Sereth in Port Azure.");

            if (quest != null)
            {
                quest.DialogOnSuccess =
                    "Demonic. Quite unmistakably. Four incidents. Blight. Something resembling " +
                    "the Void. Unbound soul-energy. Infernal corruption. None alike. All beginning " +
                    "after the fall of the gods. There is an old inscription I need you to retrieve.";
            }

            return
                quest;
        }


        private static Quest CreateBaseQuest(
            string unityName,
            string dbName,
            string questName,
            string description)
        {
            Quest quest =
                ScriptableObject.CreateInstance<Quest>();

            if (quest == null)
            {
                return null;
            }

            quest.name =
                unityName;

            quest.DBName =
                dbName;

            quest.QuestName =
                questName;

            quest.QuestDesc =
                description;

            quest.RequiredItems =
                new List<Item>();

            quest.DialogOnSuccess =
                string.Empty;

            quest.DialogOnPartialSuccess =
                string.Empty;

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

            return
                quest;
        }


        internal static bool IsAfterLastWardenActive()
        {
            return
                ContainsQuestId(
                    GameData.HasQuest,
                    AfterLastWardenQuestId);
        }


        internal static bool IsAfterLastWardenComplete()
        {
            return
                ContainsQuestId(
                    GameData.CompletedQuests,
                    AfterLastWardenQuestId);
        }


        internal static bool IsSignsOfDecayActive()
        {
            return
                ContainsQuestId(
                    GameData.HasQuest,
                    SignsOfDecayQuestId);
        }


        internal static bool IsSignsOfDecayComplete()
        {
            return
                ContainsQuestId(
                    GameData.CompletedQuests,
                    SignsOfDecayQuestId);
        }


        internal static bool IsWhereLightFailsActive()
        {
            return
                ContainsQuestId(
                    GameData.HasQuest,
                    WhereLightFailsQuestId);
        }


        internal static bool IsWhereLightFailsComplete()
        {
            return
                ContainsQuestId(
                    GameData.CompletedQuests,
                    WhereLightFailsQuestId);
        }


        internal static bool IsRestlessDeadActive()
        {
            return
                ContainsQuestId(
                    GameData.HasQuest,
                    RestlessDeadQuestId);
        }


        internal static bool IsRestlessDeadComplete()
        {
            return
                ContainsQuestId(
                    GameData.CompletedQuests,
                    RestlessDeadQuestId);
        }


        internal static bool IsAshBeneathEarthActive()
        {
            return
                ContainsQuestId(
                    GameData.HasQuest,
                    AshBeneathEarthQuestId);
        }


        internal static bool IsAshBeneathEarthComplete()
        {
            return
                ContainsQuestId(
                    GameData.CompletedQuests,
                    AshBeneathEarthQuestId);
        }


        internal static bool TryAssignAfterLastWarden()
        {
            Register();

            if (_afterLastWardenQuest == null)
            {
                return false;
            }

            if (IsAfterLastWardenComplete() ||
                IsAfterLastWardenActive())
            {
                return true;
            }

            if (!CanNativeQuestDbResolve(
                    AfterLastWardenQuestId))
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: Quest 1 is registered locally but could not be resolved through QuestDB.");

                return false;
            }

            try
            {
                GameData.AssignQuest(
                    AfterLastWardenQuestId);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: native AssignQuest threw for Quest 1; attempting direct quest-state fallback. " +
                    exception.Message);
            }

            if (!IsAfterLastWardenActive() &&
                !IsAfterLastWardenComplete())
            {
                AddQuestId(
                    GameData.HasQuest,
                    AfterLastWardenQuestId);
            }

            return
                IsAfterLastWardenActive();
        }


        internal static bool TryCompleteAfterLastWarden()
        {
            Register();

            if (IsAfterLastWardenComplete())
            {
                return true;
            }

            if (!IsAfterLastWardenActive())
            {
                return false;
            }

            if (!CanNativeQuestDbResolve(
                    AfterLastWardenQuestId))
            {
                return false;
            }

            try
            {
                GameData.FinishQuest(
                    AfterLastWardenQuestId);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: native FinishQuest threw for Quest 1; attempting direct quest-state fallback. " +
                    exception.Message);
            }

            if (!IsAfterLastWardenComplete())
            {
                RemoveQuestId(
                    GameData.HasQuest,
                    AfterLastWardenQuestId);

                AddQuestId(
                    GameData.CompletedQuests,
                    AfterLastWardenQuestId);
            }

            if (!IsAfterLastWardenComplete())
            {
                return false;
            }

            RemoveQuestId(
                GameData.HasQuest,
                AfterLastWardenQuestId);

            PlayQuestCompleteSound();

            return true;
        }


        internal static bool TryAssignSignsOfDecay()
        {
            Register();

            if (_signsOfDecayQuest == null)
            {
                return false;
            }

            if (IsSignsOfDecayComplete() ||
                IsSignsOfDecayActive())
            {
                return true;
            }

            if (!CanNativeQuestDbResolve(
                    SignsOfDecayQuestId))
            {
                return false;
            }

            try
            {
                GameData.AssignQuest(
                    SignsOfDecayQuestId);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: native AssignQuest threw for Quest 2; attempting direct quest-state fallback. " +
                    exception.Message);
            }

            if (!IsSignsOfDecayActive() &&
                !IsSignsOfDecayComplete())
            {
                AddQuestId(
                    GameData.HasQuest,
                    SignsOfDecayQuestId);
            }

            return
                IsSignsOfDecayActive();
        }


        internal static bool TryCompleteSignsOfDecay()
        {
            Register();

            if (IsSignsOfDecayComplete())
            {
                return true;
            }

            if (!IsSignsOfDecayActive())
            {
                return false;
            }

            if (!CanNativeQuestDbResolve(
                    SignsOfDecayQuestId))
            {
                return false;
            }

            try
            {
                GameData.FinishQuest(
                    SignsOfDecayQuestId);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: native FinishQuest threw for Quest 2; attempting direct quest-state fallback. " +
                    exception.Message);
            }

            if (!IsSignsOfDecayComplete())
            {
                RemoveQuestId(
                    GameData.HasQuest,
                    SignsOfDecayQuestId);

                AddQuestId(
                    GameData.CompletedQuests,
                    SignsOfDecayQuestId);
            }

            if (!IsSignsOfDecayComplete())
            {
                return false;
            }

            RemoveQuestId(
                GameData.HasQuest,
                SignsOfDecayQuestId);

            PlayQuestCompleteSound();

            return true;
        }


        internal static bool TryAssignWhereLightFails()
        {
            Register();

            if (_whereLightFailsQuest == null)
            {
                return false;
            }

            if (IsWhereLightFailsComplete() ||
                IsWhereLightFailsActive())
            {
                return true;
            }

            if (!CanNativeQuestDbResolve(
                    WhereLightFailsQuestId))
            {
                return false;
            }

            try
            {
                GameData.AssignQuest(
                    WhereLightFailsQuestId);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: native AssignQuest threw for Quest 3; attempting direct quest-state fallback. " +
                    exception.Message);
            }

            if (!IsWhereLightFailsActive() &&
                !IsWhereLightFailsComplete())
            {
                AddQuestId(
                    GameData.HasQuest,
                    WhereLightFailsQuestId);
            }

            return
                IsWhereLightFailsActive();
        }


        internal static bool TryAssignRestlessDead()
        {
            Register();

            if (_restlessDeadQuest == null)
            {
                return false;
            }

            if (IsRestlessDeadComplete() ||
                IsRestlessDeadActive())
            {
                return true;
            }

            if (!CanNativeQuestDbResolve(
                    RestlessDeadQuestId))
            {
                return false;
            }

            try
            {
                GameData.AssignQuest(
                    RestlessDeadQuestId);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: native AssignQuest threw for Quest 4; attempting direct quest-state fallback. " +
                    exception.Message);
            }

            if (!IsRestlessDeadActive() &&
                !IsRestlessDeadComplete())
            {
                AddQuestId(
                    GameData.HasQuest,
                    RestlessDeadQuestId);
            }

            return
                IsRestlessDeadActive();
        }


        internal static bool TryAssignAshBeneathEarth()
        {
            Register();

            if (_ashBeneathEarthQuest == null)
            {
                return false;
            }

            if (IsAshBeneathEarthComplete() ||
                IsAshBeneathEarthActive())
            {
                return true;
            }

            if (!CanNativeQuestDbResolve(
                    AshBeneathEarthQuestId))
            {
                return false;
            }

            try
            {
                GameData.AssignQuest(
                    AshBeneathEarthQuestId);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: native AssignQuest threw for Quest 5; attempting direct quest-state fallback. " +
                    exception.Message);
            }

            if (!IsAshBeneathEarthActive() &&
                !IsAshBeneathEarthComplete())
            {
                AddQuestId(
                    GameData.HasQuest,
                    AshBeneathEarthQuestId);
            }

            return
                IsAshBeneathEarthActive();
        }


        private static bool CanNativeQuestDbResolve(
            string questId)
        {
            if (GameData.QuestDB == null)
            {
                return false;
            }

            Quest resolved =
                GameData.QuestDB.GetQuestByName(
                    questId);

            return
                resolved != null;
        }


        private static bool AddQuestId(
            object collection,
            string id)
        {
            if (collection == null ||
                string.IsNullOrEmpty(
                    id))
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
                    collection as
                        System.Collections.IList;

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

                return
                    ContainsQuestId(
                        collection,
                        id);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: failed adding quest state [" +
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
                string.IsNullOrEmpty(
                    id))
            {
                return false;
            }

            bool removed =
                false;

            try
            {
                System.Collections.IList list =
                    collection as
                        System.Collections.IList;

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

                        removed =
                            true;
                    }

                    return
                        removed ||
                        !ContainsQuestId(
                            collection,
                            id);
                }

                var remove =
                    HarmonyLib.AccessTools.Method(
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

                while (ContainsQuestId(
                    collection,
                    id))
                {
                    object result =
                        remove.Invoke(
                            collection,
                            new object[]
                            {
                                id
                            });

                    removed =
                        true;

                    if (result is bool &&
                        !(bool)result)
                    {
                        break;
                    }
                }

                return
                    removed ||
                    !ContainsQuestId(
                        collection,
                        id);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: failed removing quest state [" +
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
                string.IsNullOrEmpty(
                    id))
            {
                return false;
            }

            try
            {
                System.Collections.IList list =
                    collection as
                        System.Collections.IList;

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
                    "Fate of the Fallen: failed to play quest completion sound: " +
                    exception.Message);
            }
        }
    }
}
