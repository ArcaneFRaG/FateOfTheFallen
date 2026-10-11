using System;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 5 inventory / requirement helpers.
    ///
    /// The exact canonical Cindered Rift Shard Item singleton is used for both
    /// the player's inventory and Quest.RequiredItems because Erenshor's native
    /// TradeWindow compares item references during quest turn-in.
    /// </summary>
    internal static class AshBeneathEarthContent
    {
        internal const string ShardItemId =
            CinderedRiftShard.ItemId;

        internal const string ShardItemName =
            CinderedRiftShard.ItemName;


        internal static Item RegisterItem(
            ItemDatabase database)
        {
            Item shard =
                CinderedRiftShard.RegisterItem(
                    database);

            if (shard == null)
            {
                Plugin.NativeLog.LogError(
                    "Ash Beneath the Earth: failed registering Cindered Rift Shard [1703].");

                return null;
            }

            LinkQuestRequirement();

            return shard;
        }


        internal static Item GetItem()
        {
            Item shard =
                CinderedRiftShard.GetItem();

            if (shard == null &&
                GameData.ItemDB != null)
            {
                shard =
                    RegisterItem(
                        GameData.ItemDB);
            }

            return shard;
        }


        internal static void LinkQuestRequirement()
        {
            Quest quest =
                AshBeneathEarthQuest.Quest;

            Item shard =
                CinderedRiftShard.GetItem();

            if (shard == null &&
                GameData.ItemDB != null)
            {
                shard =
                    CinderedRiftShard.RegisterItem(
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

            quest.RequiredItems.Clear();
            quest.RequiredItems.Add(
                shard);
        }


        internal static bool PlayerHasShard()
        {
            Item shard =
                GetItem();

            if (shard == null ||
                GameData.PlayerInv == null)
            {
                return false;
            }

            try
            {
                return
                    GameData.PlayerInv.HasItem(
                        shard,
                        false);
            }
            catch
            {
                return false;
            }
        }


        internal static bool TryGiveShard(
            bool announce)
        {
            if (!AshBeneathEarthQuest.IsActive() ||
                AshBeneathEarthQuest.IsComplete())
            {
                return false;
            }

            if (PlayerHasShard())
            {
                return true;
            }

            Item shard =
                GetItem();

            if (shard == null ||
                GameData.PlayerInv == null)
            {
                return false;
            }

            bool added =
                false;

            try
            {
                added =
                    GameData.PlayerInv.AddItemToInv(
                        shard);

                if (!added)
                {
                    GameData.PlayerInv.ForceItemToInv(
                        shard);

                    added =
                        PlayerHasShard();
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Ash Beneath the Earth: failed giving Cindered Rift Shard [1703] to the player: " +
                    exception);

                return false;
            }

            if (added &&
                announce)
            {
                AddSystemMessage(
                    "You recover the Cindered Rift Shard.",
                    "green");

                AddLocalMessage(
                    "The fragment comes free with a shower of embers. " +
                    "Its heat does not fade in your hand.",
                    "white");
            }

            return added;
        }


        private static void AddSystemMessage(
            string text,
            string color)
        {
            try
            {
                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        text,
                        ChatLogLine.LogType.SystemMessages,
                        color ?? ""));
            }
            catch
            {
            }
        }


        private static void AddLocalMessage(
            string text,
            string color)
        {
            try
            {
                UpdateSocialLog.LocalLogAdd(
                    text,
                    color ?? "white");
            }
            catch
            {
            }
        }
    }
}
