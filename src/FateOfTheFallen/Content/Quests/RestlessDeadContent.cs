using System;

namespace FateOfTheFallen
{
    /// <summary>
    /// Quest 4 inventory/quest helpers.
    ///
    /// The canonical quest item is Lingering Soul-Essence [1702].
    /// The future risen-dead encounter should call TryGiveEssence() when the
    /// designated quest corpse is destroyed; Sereth's native trade path then
    /// consumes this exact singleton through Quest.RequiredItems.
    /// </summary>
    internal static class RestlessDeadContent
    {
        internal const string EssenceItemId =
            LingeringSoulEssence.ItemId;

        internal const string EssenceItemName =
            LingeringSoulEssence.ItemName;

        internal static Item RegisterItem(
            ItemDatabase database)
        {
            Item essence =
                LingeringSoulEssence.RegisterItem(
                    database);

            if (essence == null)
            {
                Plugin.NativeLog.LogError(
                    "The Restless Dead: failed registering Lingering Soul-Essence [1702].");

                return null;
            }

            LinkQuestRequirement();

            return essence;
        }

        internal static Item GetItem()
        {
            Item essence =
                LingeringSoulEssence.GetItem();

            if (essence == null &&
                GameData.ItemDB != null)
            {
                essence =
                    RegisterItem(
                        GameData.ItemDB);
            }

            return essence;
        }

        internal static void LinkQuestRequirement()
        {
            Quest quest =
                FateOfTheFallenQuests
                    .RestlessDeadQuest;

            Item essence =
                LingeringSoulEssence.GetItem();

            if (essence == null &&
                GameData.ItemDB != null)
            {
                essence =
                    LingeringSoulEssence.RegisterItem(
                        GameData.ItemDB);
            }

            if (quest == null ||
                essence == null)
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
                essence);
        }

        internal static bool PlayerHasEssence()
        {
            Item essence =
                GetItem();

            if (essence == null ||
                GameData.PlayerInv == null)
            {
                return false;
            }

            try
            {
                return
                    GameData.PlayerInv.HasItem(
                        essence,
                        false);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryGiveEssence(
            bool announce)
        {
            if (!FateOfTheFallenQuests.IsRestlessDeadActive() ||
                FateOfTheFallenQuests.IsRestlessDeadComplete())
            {
                return false;
            }

            if (PlayerHasEssence())
            {
                return true;
            }

            Item essence =
                GetItem();

            if (essence == null ||
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
                            essence);

                if (!added)
                {
                    GameData.PlayerInv
                        .ForceItemToInv(
                            essence);

                    added =
                        PlayerHasEssence();
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "The Restless Dead: failed giving Lingering Soul-Essence [1702] to the player: " +
                    exception);

                return false;
            }

            if (added &&
                announce)
            {
                AddSystemMessage(
                    "You recover Lingering Soul-Essence.",
                    "green");

                AddLocalMessage(
                    "As the risen dead collapses, a knot of pale-violet soul-energy refuses to dissipate. " +
                    "You contain it for Archivist Sereth.",
                    "white");
            }

            return added;
        }

        internal static bool TryConsumeEssence()
        {
            Item essence =
                GetItem();

            if (essence == null ||
                GameData.PlayerInv == null)
            {
                return false;
            }

            try
            {
                return
                    GameData.PlayerInv.HasItem(
                        essence,
                        true);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "The Restless Dead: failed consuming Lingering Soul-Essence [1702] during turn-in: " +
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
