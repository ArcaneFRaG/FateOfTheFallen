using System;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 2 inventory/quest helpers.
    ///
    /// The canonical item definition belongs to BlightedSpores [1700].
    /// This class deliberately does not create a second quest-item object;
    /// every inventory, quest requirement, save/load and world-harvest path
    /// resolves the same Item instance through CustomItemRegistry.
    /// </summary>
    internal static class SignsOfDecayContent
    {
        internal const string SampleItemId =
            BlightedSpores.ItemId;

        internal const string SampleItemName =
            BlightedSpores.ItemName;

        internal static Item RegisterItem(
            ItemDatabase database)
        {
            Item sample =
                BlightedSpores.RegisterItem(
                    database);

            if (sample == null)
            {
                Plugin.NativeLog.LogError(
                    "Signs of Decay: failed registering Blighted Spores [1700].");

                return null;
            }

            LinkQuestRequirement();

            return sample;
        }

        internal static Item GetItem()
        {
            Item sample =
                BlightedSpores.GetItem();

            if (sample == null &&
                GameData.ItemDB != null)
            {
                sample =
                    RegisterItem(
                        GameData.ItemDB);
            }

            return sample;
        }

        internal static void LinkQuestRequirement()
        {
            Quest quest =
                FateOfTheFallenQuests
                    .SignsOfDecayQuest;

            Item sample =
                BlightedSpores.GetItem();

            if (sample == null &&
                GameData.ItemDB != null)
            {
                sample =
                    BlightedSpores.RegisterItem(
                        GameData.ItemDB);
            }

            if (quest == null ||
                sample == null)
            {
                return;
            }

            if (quest.RequiredItems == null)
            {
                quest.RequiredItems =
                    new System.Collections.Generic.List<Item>();
            }

            // Quest 2 requires exactly one canonical Blighted Spores sample.
            quest.RequiredItems.Clear();
            quest.RequiredItems.Add(
                sample);
        }

        internal static bool PlayerHasSample()
        {
            Item sample =
                GetItem();

            if (sample == null ||
                GameData.PlayerInv == null)
            {
                return false;
            }

            try
            {
                return
                    GameData.PlayerInv.HasItem(
                        sample,
                        false);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryGiveSample(
            bool announce)
        {
            if (PlayerHasSample())
            {
                return true;
            }

            Item sample =
                GetItem();

            if (sample == null ||
                GameData.PlayerInv == null)
            {
                return false;
            }

            bool added =
                false;

            try
            {
                added =
                    GameData.PlayerInv
                        .AddItemToInv(
                            sample);

                if (!added)
                {
                    GameData.PlayerInv
                        .ForceItemToInv(
                            sample);

                    added =
                        PlayerHasSample();
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Signs of Decay: failed giving Blighted Spores [1700] to the player: " +
                    exception);

                return false;
            }

            if (added &&
                announce)
            {
                AddSystemMessage(
                    "You collect a sample of Blighted Spores.",
                    "green");

                AddLocalMessage(
                    "The swollen growth releases a fine green-black dust. " +
                    "You seal a fresh sample for Archivist Sereth.",
                    "white");
            }

            return added;
        }

        internal static bool TryConsumeSample()
        {
            Item sample =
                GetItem();

            if (sample == null ||
                GameData.PlayerInv == null)
            {
                return false;
            }

            try
            {
                return
                    GameData.PlayerInv.HasItem(
                        sample,
                        true);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Signs of Decay: failed consuming Blighted Spores [1700] during turn-in: " +
                    exception);

                return false;
            }
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
