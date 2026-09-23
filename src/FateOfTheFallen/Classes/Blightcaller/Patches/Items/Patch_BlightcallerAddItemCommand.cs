using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FateOfTheFallen
{
    [HarmonyPatch(
        typeof(TypeText),
        "CheckCommands")]
    internal static class Patch_BlightcallerAddItemCommand
    {
        private const int FateOfTheFallenItemMin =
            1600;

        private const int FateOfTheFallenItemMax =
            1800;


        private static readonly Dictionary<int, string>
            LastHandledAddItemCommands =
            new Dictionary<int, string>();


        // ============================================================
        // /ADDITEM COMMAND
        // ============================================================

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


            if (string.IsNullOrWhiteSpace(
                    command))
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


            if (!IsFateOfTheFallenItemId(
                    itemId))
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
                    out previousCommand))
            {
                if (string.Equals(
                        previousCommand,
                        trimmed,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }


            try
            {
                Item customItem =
                    ResolveFateOfTheFallenItem(
                        itemId);


                if (customItem == null)
                {
                    LastHandledAddItemCommands[instanceId] =
                        trimmed;


                    Plugin.NativeLog.LogWarning(
                        "Fate of the Fallen: /additem ID " +
                        itemId +
                        " is reserved for Fate of the Fallen " +
                        "(" +
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


        // ============================================================
        // FATE OF THE FALLEN ITEM RANGE
        // ============================================================

        private static bool IsFateOfTheFallenItemId(
            int itemId)
        {
            return
                itemId >= FateOfTheFallenItemMin &&
                itemId <= FateOfTheFallenItemMax;
        }


        // ============================================================
        // RESOLVE CUSTOM ITEM
        // ============================================================
        //
        // IMPORTANT:
        //
        // Check the ItemFactory registry FIRST.
        //
        // Anything created through:
        //
        //     BlightcallerItemFactory.CreateItem(...)
        //
        // automatically becomes available to /additem without
        // needing another hard-coded entry here.
        //
        // This will cover:
        //
        //     1700+ quest/world items
        //     future equipment
        //     consumables
        //     notes
        //     miscellaneous items
        //
        // Existing Blightcaller scroll/aura handling remains as a
        // fallback for compatibility.
        // ============================================================

        private static Item ResolveFateOfTheFallenItem(
            int itemId)
        {
            // ========================================================
            // ITEM FACTORY REGISTRY
            // ========================================================

            Item registeredItem =
                BlightcallerItemFactory.GetItemById(
                    itemId.ToString());


            if (registeredItem != null)
            {
                return registeredItem;
            }


            // ========================================================
            // BLIGHTCALLER SPELL SCROLL FALLBACK
            // ========================================================

            if (BlightcallerScrolls.IsCustomScrollId(
                    itemId))
            {
                return BlightcallerScrolls.GetScrollById(
                    itemId);
            }


            // ========================================================
            // BLIGHTCALLER AURA FALLBACK
            // ========================================================

            Item aura =
                GetAuraItemById(
                    itemId);


            if (aura != null)
            {
                return aura;
            }


            return null;
        }


        // ============================================================
        // BLIGHTCALLER AURA LOOKUP
        // ============================================================

        private static Item GetAuraItemById(
            int itemIndex)
        {
            switch (itemIndex)
            {
                case 1650:
                    return BlightcallerAuras.GetItem(
                        "withering");

                case 1651:
                    return BlightcallerAuras.GetItem(
                        "pestilent_essence");

                case 1652:
                    return BlightcallerAuras.GetItem(
                        "festering_power");

                case 1653:
                    return BlightcallerAuras.GetItem(
                        "blighted_covenant");

                case 1654:
                    return BlightcallerAuras.GetItem(
                        "eternal_blight");

                default:
                    return null;
            }
        }


        // ============================================================
        // ADD CUSTOM ITEM TO INVENTORY
        // ============================================================

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