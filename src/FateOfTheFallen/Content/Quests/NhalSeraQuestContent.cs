using System;

namespace FateOfTheFallen
{
    internal static class NhalSeraQuestContent
    {
        internal static Item RegisterItem(ItemDatabase database)
        {
            Item shard = ShardOfLightlessGlass.RegisterItem(database);
            LinkQuestRequirement();
            return shard;
        }

        internal static Item GetItem()
        {
            Item shard = ShardOfLightlessGlass.GetItem();

            if (shard == null && GameData.ItemDB != null)
                shard = RegisterItem(GameData.ItemDB);

            return shard;
        }

        internal static void LinkQuestRequirement()
        {
            Quest quest =
                NhalSeraQuest.Quest;

            Item shard =
                ShardOfLightlessGlass.GetItem();

            if (shard == null &&
                GameData.ItemDB != null)
            {
                shard =
                    ShardOfLightlessGlass.RegisterItem(
                        GameData.ItemDB);
            }

            if (quest == null ||
                shard == null)
            {
                return;
            }

            if (quest.RequiredItems == null)
            {
                quest.RequiredItems =
                    new System.Collections.Generic.List<Item>();
            }

            // Quest 8 requires exactly one canonical Shard of Lightless Glass
            // [1706]. Never create a second quest-item Item instance here:
            // inventory, corpse loot, Quest.RequiredItems and Sereth's native
            // QuestManager must all point at the same singleton.
            quest.RequiredItems.Clear();
            quest.RequiredItems.Add(
                shard);
        }

        internal static bool PlayerHasShard()
        {
            Item shard = GetItem();

            if (shard == null || GameData.PlayerInv == null)
                return false;

            try
            {
                return GameData.PlayerInv.HasItem(shard, false);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryConsumeShard()
        {
            Item shard = GetItem();

            if (shard == null || GameData.PlayerInv == null)
                return false;

            try
            {
                return GameData.PlayerInv.HasItem(shard, true);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[Quest8] failed consuming Shard of Lightless Glass [1706]: " +
                    exception);
                return false;
            }
        }
    }
}
