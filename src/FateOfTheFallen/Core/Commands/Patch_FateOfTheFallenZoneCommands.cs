using HarmonyLib;
using System;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Temporary proof-of-concept commands for custom Fate of the Fallen
    /// zones and zone diagnostics.
    ///
    /// /fotfzone         -> enter Fallen Reach
    /// /fallenreach      -> enter Fallen Reach
    /// /fotfreturn       -> return to the native scene/position used to enter
    /// /fotfraid         -> enter the minimal FOTF raid proof-of-concept scene
    /// /fotfraidreturn   -> return to the scene/position used to enter the raid
    /// /fotfzonebaseline -> capture the current native zone as diagnostic baseline
    /// /fotfzonedump     -> dump the focused current zone contract/UI state
    /// /fotfzonebootstrap -> reapply Fallen Reach native zone bootstrap
    /// /fotfmonster       -> rebuild the Blighted Thrall runtime SpawnPoint
    /// /fotfmonsterstatus -> write monster PoC state to the log
    /// </summary>
    [HarmonyPatch(
        typeof(TypeText),
        "CheckCommands")]
    internal static class Patch_FateOfTheFallenZoneCommands
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

            string command =
                __instance.typed.text;

            if (string.IsNullOrWhiteSpace(
                    command))
            {
                return true;
            }

            string trimmed =
                command.Trim();

            bool enterCommand =
                string.Equals(
                    trimmed,
                    "/fotfzone",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    trimmed,
                    "/fallenreach",
                    StringComparison.OrdinalIgnoreCase);

            bool returnCommand =
                string.Equals(
                    trimmed,
                    "/fotfreturn",
                    StringComparison.OrdinalIgnoreCase);

            bool raidCommand =
                string.Equals(
                    trimmed,
                    "/fotfraid",
                    StringComparison.OrdinalIgnoreCase);

            bool raidReturnCommand =
                string.Equals(
                    trimmed,
                    "/fotfraidreturn",
                    StringComparison.OrdinalIgnoreCase);

            bool baselineCommand =
                string.Equals(
                    trimmed,
                    "/fotfzonebaseline",
                    StringComparison.OrdinalIgnoreCase);

            bool dumpCommand =
                string.Equals(
                    trimmed,
                    "/fotfzonedump",
                    StringComparison.OrdinalIgnoreCase);

            bool bootstrapCommand =
                string.Equals(
                    trimmed,
                    "/fotfzonebootstrap",
                    StringComparison.OrdinalIgnoreCase);

            bool monsterCommand =
                string.Equals(
                    trimmed,
                    "/fotfmonster",
                    StringComparison.OrdinalIgnoreCase);

            bool monsterStatusCommand =
                string.Equals(
                    trimmed,
                    "/fotfmonsterstatus",
                    StringComparison.OrdinalIgnoreCase);

            if (!enterCommand &&
                !returnCommand &&
                !raidCommand &&
                !raidReturnCommand &&
                !baselineCommand &&
                !dumpCommand &&
                !bootstrapCommand &&
                !monsterCommand &&
                !monsterStatusCommand)
            {
                return true;
            }

            // We own this command. Clear it ourselves because returning
            // false prevents native TypeText.CheckCommands from doing its
            // normal command cleanup.
            __instance.typed.text =
                string.Empty;

            if (!FOTFExpansionGate.IsUnlocked)
            {
                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        "Fate of the Fallen expansion content is locked until all four native god raids are complete. Use /fotflock for development testing.",
                        ChatLogLine.LogType.SystemMessages,
                        "yellow"));

                return false;
            }

            try
            {
                if (enterCommand)
                {
                    CustomZoneLoader.EnterFallenReach();
                }
                else if (returnCommand)
                {
                    CustomZoneLoader.ReturnToPreviousZone();
                }
                else if (raidCommand)
                {
                    CustomRaidLoader.EnterRaidPoc();
                }
                else if (raidReturnCommand)
                {
                    CustomRaidLoader.ReturnToPreviousZone();
                }
                else if (baselineCommand)
                {
                    Scene scene =
                        SceneManager.GetActiveScene();

                    ZoneSceneDiagnostic.CaptureNativeBaseline(
                        scene,
                        "manual /fotfzonebaseline command");

                    UpdateSocialLog.LogAdd(
                        new ChatLogLine(
                            "FOTF native zone diagnostic baseline captured. See the log.",
                            ChatLogLine.LogType.SystemMessages,
                            ""));
                }
                else if (dumpCommand)
                {
                    ZoneSceneDiagnostic.DumpCurrentScene();

                    UpdateSocialLog.LogAdd(
                        new ChatLogLine(
                            "FOTF focused zone diagnostic written to the log.",
                            ChatLogLine.LogType.SystemMessages,
                            ""));
                }
                else if (bootstrapCommand)
                {
                    CustomZoneBootstrap.ReapplyForCurrentScene();

                    UpdateSocialLog.LogAdd(
                        new ChatLogLine(
                            "FOTF custom-scene bootstrap reapplied.",
                            ChatLogLine.LogType.SystemMessages,
                            ""));
                }
                else if (monsterCommand)
                {
                    BlightedThrallRuntime.RebuildCurrentSpawn();
                }
                else
                {
                    BlightedThrallRuntime.WriteStatusToLog();
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen zone command failed: " +
                    exception);
            }

            return false;
        }
    }
}
