using System;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 6 inventory / requirement helpers.
    ///
    /// The exact canonical Fractured Warding Fragment Item singleton is used
    /// for both the player's inventory and Quest.RequiredItems because
    /// Erenshor's native TradeWindow compares item references during quest
    /// turn-in.
    /// </summary>
    internal static class ThePatternBeneathContent
    {
        internal const string FragmentItemId =
            FracturedWardingFragment.ItemId;

        internal const string FragmentItemName =
            FracturedWardingFragment.ItemName;


        internal static Item RegisterItem(
            ItemDatabase database)
        {
            Item fragment =
                FracturedWardingFragment.RegisterItem(
                    database);

            if (fragment == null)
            {
                Plugin.NativeLog.LogError(
                    "The Pattern Beneath: failed registering Fractured Warding Fragment [1704].");

                return null;
            }

            LinkQuestRequirement();

            return fragment;
        }


        internal static Item GetItem()
        {
            Item fragment =
                FracturedWardingFragment.GetItem();

            if (fragment == null &&
                GameData.ItemDB != null)
            {
                fragment =
                    RegisterItem(
                        GameData.ItemDB);
            }

            return fragment;
        }


        internal static void LinkQuestRequirement()
        {
            Quest quest =
                ThePatternBeneathQuest.Quest;

            Item fragment =
                FracturedWardingFragment.GetItem();

            if (fragment == null &&
                GameData.ItemDB != null)
            {
                fragment =
                    FracturedWardingFragment.RegisterItem(
                        GameData.ItemDB);
            }

            if (quest == null ||
                fragment == null)
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
                fragment);
        }


        internal static bool PlayerHasFragment()
        {
            Item fragment =
                GetItem();

            if (fragment == null ||
                GameData.PlayerInv == null)
            {
                return false;
            }

            try
            {
                return
                    GameData.PlayerInv.HasItem(
                        fragment,
                        false);
            }
            catch
            {
                return false;
            }
        }


        internal static bool TryGiveFragment(
            bool announce)
        {
            if (!ThePatternBeneathQuest.IsActive() ||
                ThePatternBeneathQuest.IsComplete())
            {
                return false;
            }

            if (PlayerHasFragment())
            {
                return true;
            }

            Item fragment =
                GetItem();

            if (fragment == null ||
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
                        fragment);

                if (!added)
                {
                    GameData.PlayerInv.ForceItemToInv(
                        fragment);

                    added =
                        PlayerHasFragment();
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "The Pattern Beneath: failed giving Fractured Warding Fragment [1704] to the player: " +
                    exception);

                return false;
            }

            if (added &&
                announce)
            {
                AddSystemMessage(
                    "You recover the Fractured Warding Fragment.",
                    "green");

                AddLocalMessage(
                    "A brittle splinter breaks free from the ancient seal. " +
                    "For an instant, four incompatible energies pulse beneath its surface.",
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
