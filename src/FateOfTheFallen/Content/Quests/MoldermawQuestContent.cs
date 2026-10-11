using System;

namespace FateOfTheFallen
{
    /// <summary>
    /// Owns Quest 7's canonical Blighted Core requirement and hand-in helpers.
    ///
    /// Quest 7 originally created/dropped item 1705 but never attached that
    /// item to Quest.RequiredItems. That made the tooltip/corpse drop exist
    /// while Archivist Sereth's native QuestManager had nothing to accept.
    /// </summary>
    internal static class MoldermawQuestContent
    {
        internal static Item RegisterItem(
            ItemDatabase database)
        {
            Item core =
                MoldermawBlightedCore.RegisterItem(
                    database);

            LinkQuestRequirement();

            return core;
        }


        internal static Item GetItem()
        {
            Item core =
                MoldermawBlightedCore.GetItem();

            if (core == null &&
                GameData.ItemDB != null)
            {
                core =
                    RegisterItem(
                        GameData.ItemDB);
            }

            return core;
        }


        internal static void LinkQuestRequirement()
        {
            Quest quest =
                MoldermawQuest.Quest;

            Item core =
                MoldermawBlightedCore.GetItem();

            if (core == null &&
                GameData.ItemDB != null)
            {
                core =
                    MoldermawBlightedCore.RegisterItem(
                        GameData.ItemDB);
            }

            if (quest == null ||
                core == null)
            {
                return;
            }

            if (quest.RequiredItems == null)
            {
                quest.RequiredItems =
                    new System.Collections.Generic.List<Item>();
            }

            // Quest 7 requires exactly one canonical Blighted Core.
            quest.RequiredItems.Clear();
            quest.RequiredItems.Add(
                core);
        }


        internal static bool PlayerHasCore()
        {
            Item core =
                GetItem();

            if (core == null ||
                GameData.PlayerInv == null)
            {
                return false;
            }

            try
            {
                return
                    GameData.PlayerInv.HasItem(
                        core,
                        false);
            }
            catch
            {
                return false;
            }
        }


        internal static bool TryConsumeCore()
        {
            Item core =
                GetItem();

            if (core == null ||
                GameData.PlayerInv == null)
            {
                return false;
            }

            try
            {
                return
                    GameData.PlayerInv.HasItem(
                        core,
                        true);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Quest7] failed consuming Moldermaw's Blighted Core [1705]: " +
                    exception);

                return false;
            }
        }
    }
}
