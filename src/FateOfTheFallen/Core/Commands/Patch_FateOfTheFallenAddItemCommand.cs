using HarmonyLib;
using System;
using System.Collections.Generic;

namespace FateOfTheFallen
{
    [HarmonyPatch(
        typeof(TypeText),
        "CheckCommands")]
    internal static class Patch_FateOfTheFallenAddItemCommand
    {
        private const int FateOfTheFallenItemMin =
            1600;

        private const int FateOfTheFallenItemMax =
            1800;

        private static readonly Dictionary<int, string>
            LastHandledAddItemCommands =
            new Dictionary<int, string>();

        [HarmonyPrefix]
        private static bool CheckCommandsPrefix(
            TypeText __instance)
        {
            if (__instance == null ||
                __instance.typed == null)
            {
                return true;
            }

            string command =
                __instance.typed.text;

            if (string.IsNullOrWhiteSpace(command))
            {
                LastHandledAddItemCommands.Remove(
                    __instance.GetInstanceID());

                return true;
            }

            string trimmed =
                command.Trim();

            if (!trimmed.StartsWith(
                    "/additem",
                    StringComparison.OrdinalIgnoreCase))
            {
                LastHandledAddItemCommands.Remove(
                    __instance.GetInstanceID());

                return true;
            }

            string[] parts =
                trimmed.Split(
                    new char[]
                    {
                        ' '
                    },
                    StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                return true;
            }

            int itemId;

            if (!int.TryParse(
                    parts[1],
                    out itemId))
            {
                return true;
            }

            if (!IsFateOfTheFallenItemId(itemId))
            {
                LastHandledAddItemCommands.Remove(
                    __instance.GetInstanceID());

                return true;
            }

            int instanceId =
                __instance.GetInstanceID();

            string previousCommand;

            if (LastHandledAddItemCommands.TryGetValue(
                    instanceId,
                    out previousCommand) &&
                string.Equals(
                    previousCommand,
                    trimmed,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                Item customItem =
                    CustomItemRegistry.Resolve(
                        itemId.ToString(),
                        GameData.ItemDB);

                if (customItem == null)
                {
                    LastHandledAddItemCommands[instanceId] =
                        trimmed;

                    Plugin.NativeLog.LogWarning(
                        "Fate of the Fallen: /additem ID " +
                        itemId +
                        " is reserved for Fate of the Fallen (" +
                        FateOfTheFallenItemMin +
                        "-" +
                        FateOfTheFallenItemMax +
                        ") but is currently unused.");

                    UpdateSocialLog.LogAdd(
                        new ChatLogLine(
                            "Fate of the Fallen item ID " +
                            itemId +
                            " is currently unused.",
                            ChatLogLine.LogType.SystemMessages,
                            "yellow"));

                    return false;
                }

                LastHandledAddItemCommands[instanceId] =
                    trimmed;

                AddCustomItem(
                    customItem);

                return false;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: /additem handling failed: " +
                    exception);

                return false;
            }
        }

        private static bool IsFateOfTheFallenItemId(
            int itemId)
        {
            return
                itemId >= FateOfTheFallenItemMin &&
                itemId <= FateOfTheFallenItemMax;
        }

        private static void AddCustomItem(
            Item item)
        {
            if (item == null)
            {
                return;
            }

            if (GameData.PlayerInv == null)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: PlayerInv was null while adding custom item.");

                return;
            }

            GameData.PlayerInv.AddItemToInv(
                item);

            UpdateSocialLog.LogAdd(
                new ChatLogLine(
                    "Adding Fate of the Fallen item " +
                    item.Id +
                    ": " +
                    item.ItemName,
                    ChatLogLine.LogType.SystemMessages,
                    ""));
        }
    }
}
