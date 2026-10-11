using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    [HarmonyPatch(
        typeof(TypeText),
        "CheckCommands")]
    internal static class Patch_FateOfTheFallenExpansionGateCommand
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

            if (string.IsNullOrWhiteSpace(
                    raw))
            {
                return true;
            }

            string trimmed =
                raw.Trim();

            // Erenshor's native /server1 command resets its server-first
            // records. Mirror that operation into FOTF's per-server gate
            // state, then let the native command continue normally.
            if (string.Equals(
                    trimmed,
                    "/server1",
                    StringComparison.OrdinalIgnoreCase))
            {
                FOTFExpansionGate
                    .ClearServerProgress(
                        true,
                        true);

                return true;
            }

            if (!trimmed.StartsWith(
                    "/fotflock",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string[] parts =
                trimmed.Split(
                    new char[]
                    {
                        ' '
                    },
                    StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0 ||
                !string.Equals(
                    parts[0],
                    "/fotflock",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            __instance.typed.text =
                string.Empty;

            try
            {
                if (parts.Length == 1)
                {
                    FOTFExpansionGate
                        .ToggleDevelopmentLock();

                    WriteStatus();
                    return false;
                }

                string mode =
                    parts[1];

                if (string.Equals(
                        mode,
                        "dev",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        mode,
                        "toggle",
                        StringComparison.OrdinalIgnoreCase))
                {
                    FOTFExpansionGate
                        .ToggleDevelopmentLock();

                    WriteStatus();
                    return false;
                }

                if (string.Equals(
                        mode,
                        "auto",
                        StringComparison.OrdinalIgnoreCase))
                {
                    FOTFExpansionGate
                        .SetAutomaticMode();

                    WriteStatus();
                    return false;
                }

                if (string.Equals(
                        mode,
                        "status",
                        StringComparison.OrdinalIgnoreCase))
                {
                    WriteStatus();
                    return false;
                }

                if (string.Equals(
                        mode,
                        "lock",
                        StringComparison.OrdinalIgnoreCase))
                {
                    FOTFExpansionGate
                        .ForceLocked();

                    WriteStatus();
                    return false;
                }

                if (string.Equals(
                        mode,
                        "unlock",
                        StringComparison.OrdinalIgnoreCase))
                {
                    FOTFExpansionGate
                        .ForceUnlocked();

                    WriteStatus();
                    return false;
                }

                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        "Usage: /fotflock | /fotflock dev | /fotflock auto | /fotflock status | /fotflock lock | /fotflock unlock",
                        ChatLogLine.LogType.SystemMessages,
                        "yellow"));
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "FOTF /fotflock command failed: " +
                    exception);
            }

            return false;
        }


        private static void WriteStatus()
        {
            UpdateSocialLog.LogAdd(
                new ChatLogLine(
                    FOTFExpansionGate
                        .BuildStatusText(),
                    ChatLogLine.LogType.SystemMessages,
                    ""));
        }
    }
}
