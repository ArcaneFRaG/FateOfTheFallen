using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    /// <summary>
    /// Temporary direct controls for the Emberweaver HUD until FOTF's shared
    /// options screen exists. The HUD settings themselves are owned by
    /// EmberweaverHudRuntime so the future menu can bind to the same values.
    /// </summary>
    [HarmonyPatch(
        typeof(TypeText),
        "CheckCommands")]
    internal static class Patch_EmberweaverHudCommand
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

            bool shortCommand =
                trimmed.StartsWith(
                    "/ewhud",
                    StringComparison.OrdinalIgnoreCase);

            bool longCommand =
                trimmed.StartsWith(
                    "/emberhud",
                    StringComparison.OrdinalIgnoreCase);

            if (!shortCommand &&
                !longCommand)
            {
                return true;
            }

            string[] parts =
                trimmed.Split(
                    new char[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0 ||
                (!string.Equals(
                     parts[0],
                     "/ewhud",
                     StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(
                     parts[0],
                     "/emberhud",
                     StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            __instance.typed.text =
                string.Empty;

            try
            {
                string message;

                EmberweaverHudRuntime.TryHandleCommand(
                    parts,
                    out message);

                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        message ??
                        EmberweaverHudRuntime.BuildStatusText(),
                        ChatLogLine.LogType.SystemMessages,
                        ""));
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Emberweaver /ewhud command failed",
                    exception);

                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        "Emberweaver HUD command failed. See the log.",
                        ChatLogLine.LogType.SystemMessages,
                        "yellow"));
            }

            return false;
        }
    }
}
