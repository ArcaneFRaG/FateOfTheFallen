using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    /// <summary>
    /// Development-only live calibration for Moldermaw equipped visuals.
    ///
    /// Examples:
    /// /fotfvisual primary status
    /// /fotfvisual primary scale 0.42
    /// /fotfvisual primary rot 0 0 90
    /// /fotfvisual primary turn 0 10 0
    /// /fotfvisual primary pos 0 0 0
    /// /fotfvisual primary move 1 0 0
    /// /fotfvisual secondary reset
    /// /fotfvisual chest status
    /// /fotfvisual chest scale 100
    /// /fotfvisual hands status
    ///
    /// Values are session-only; once a transform is confirmed it should be
    /// baked into FateEquipmentVisuals' transform tuning defaults.
    /// </summary>
    [HarmonyPatch(
        typeof(TypeText),
        "CheckCommands")]
    internal static class Patch_FateOfTheFallenVisualTuningCommand
    {
        [HarmonyPrefix]
        private static bool CheckCommandsPrefix(
            TypeText __instance)
        {
            if (__instance == null ||
                __instance.typed == null)
            {
                return true;
            }

            string raw =
                __instance.typed.text;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return true;
            }

            string trimmed =
                raw.Trim();

            if (!trimmed.StartsWith(
                    "/fotfvisual",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string[] parts =
                trimmed.Split(
                    new char[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0 ||
                !string.Equals(
                    parts[0],
                    "/fotfvisual",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            __instance.typed.text =
                string.Empty;

            string slotToken =
                null;

            string operation =
                "status";

            int valueStart =
                1;

            if (parts.Length > 1 &&
                (string.Equals(parts[1], "primary", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[1], "secondary", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[1], "p", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[1], "s", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[1], "chest", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[1], "c", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[1], "head", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[1], "h", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[1], "hands", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[1], "hand", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[1], "gloves", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[1], "waist", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[1], "belt", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parts[1], "w", StringComparison.OrdinalIgnoreCase)))
            {
                slotToken =
                    parts[1];

                valueStart =
                    2;
            }

            if (parts.Length > valueStart)
            {
                operation =
                    parts[valueStart];

                valueStart++;
            }

            int valueCount =
                Math.Max(
                    0,
                    parts.Length - valueStart);

            string[] values =
                new string[valueCount];

            for (int i = 0;
                 i < valueCount;
                 i++)
            {
                values[i] =
                    parts[valueStart + i];
            }

            try
            {
                string message;

                FateEquipmentVisuals.TryTuneEquippedVisual(
                    slotToken,
                    operation,
                    values,
                    out message);

                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        message ??
                        "FOTF visual command completed.",
                        ChatLogLine.LogType.SystemMessages,
                        ""));
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "FOTF /fotfvisual command failed: " +
                    exception);

                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        "FOTF visual command failed. See the log.",
                        ChatLogLine.LogType.SystemMessages,
                        "yellow"));
            }

            return false;
        }
    }
}
