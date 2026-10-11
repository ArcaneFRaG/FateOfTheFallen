using System;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 3 inventory/quest helpers.
    ///
    /// The canonical item definition belongs to VeiledResidue [1701].
    /// Every inventory, save/load, world-harvest and quest-turn-in path
    /// resolves the same Item instance through CustomItemRegistry.
    /// </summary>
    internal static class WhereLightFailsContent
    {
        internal const string ResidueItemId =
            VeiledResidue.ItemId;

        internal const string ResidueItemName =
            VeiledResidue.ItemName;

        internal static Item RegisterItem(
            ItemDatabase database)
        {
            Item residue =
                VeiledResidue.RegisterItem(
                    database);

            if (residue == null)
            {
                Plugin.NativeLog.LogError(
                    "Where Light Fails: failed registering Veiled Residue [1701].");

                return null;
            }

            LinkQuestRequirement();

            return residue;
        }

        internal static Item GetItem()
        {
            Item residue =
                VeiledResidue.GetItem();

            if (residue == null &&
                GameData.ItemDB != null)
            {
                residue =
                    RegisterItem(
                        GameData.ItemDB);
            }

            return residue;
        }

        internal static void LinkQuestRequirement()
        {
            Quest quest =
                FateOfTheFallenQuests
                    .WhereLightFailsQuest;

            Item residue =
                VeiledResidue.GetItem();

            if (residue == null &&
                GameData.ItemDB != null)
            {
                residue =
                    VeiledResidue.RegisterItem(
                        GameData.ItemDB);
            }

            if (quest == null ||
                residue == null)
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
                residue);
        }

        internal static bool PlayerHasResidue()
        {
            Item residue =
                GetItem();

            if (residue == null ||
                GameData.PlayerInv == null)
            {
                return false;
            }

            try
            {
                return
                    GameData.PlayerInv.HasItem(
                        residue,
                        false);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryGiveResidue(
            bool announce)
        {
            if (PlayerHasResidue())
            {
                return true;
            }

            Item residue =
                GetItem();

            if (residue == null ||
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
                            residue);

                if (!added)
                {
                    GameData.PlayerInv
                        .ForceItemToInv(
                            residue);

                    added =
                        PlayerHasResidue();
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Where Light Fails: failed giving Veiled Residue [1701] to the player: " +
                    exception);

                return false;
            }

            if (added &&
                announce)
            {
                AddSystemMessage(
                    "You recover the Veiled Residue.",
                    "green");

                AddLocalMessage(
                    "The floating orb collapses into a cold, weightless fragment as you draw it from the disturbance.",
                    "white");
            }

            return added;
        }

        internal static bool TryConsumeResidue()
        {
            Item residue =
                GetItem();

            if (residue == null ||
                GameData.PlayerInv == null)
            {
                return false;
            }

            try
            {
                return
                    GameData.PlayerInv.HasItem(
                        residue,
                        true);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Where Light Fails: failed consuming Veiled Residue [1701] during turn-in: " +
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
