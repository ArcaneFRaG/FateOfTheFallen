using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Per-server master progression gate for Fate of the Fallen.
    ///
    /// Production unlock condition:
    ///     The four native god-raid end bosses must have actually died on the
    ///     CURRENT Erenshor server.
    ///
    /// This deliberately does NOT read Steam achievements. Steam achievement
    /// state can survive between Erenshor server resets and therefore is not a
    /// valid source of server progression.
    ///
    /// FOTF stores only four server-completion bits under ESSaveData. Erenshor
    /// deletes ESSaveData during a full server reset, so the expansion lock
    /// naturally resets with the native server.
    ///
    /// Playable custom classes remain outside this gate.
    /// </summary>
    internal static class FOTFExpansionGate
    {
        internal enum DevelopmentOverrideMode
        {
            Auto,
            ForceLocked,
            ForceUnlocked
        }


        internal sealed class NativeRaidState
        {
            internal string DisplayName;
            internal string NativeBossCompletionId;
            internal bool Complete;
        }


        // These IDs are used by Erenshor's actual final-boss NPCs through
        // NPC.SetAchievementOnDefeat. We use them only to identify WHICH
        // native end boss just died. We never query the achievement itself.
        //
        // GODESSSOLUNA is intentionally spelled exactly as Erenshor spells it.
        private static readonly NativeRaidState[] NativeRaids =
        {
            new NativeRaidState
            {
                DisplayName = "Brax",
                NativeBossCompletionId = "GODBRAX"
            },
            new NativeRaidState
            {
                DisplayName = "Soluna",
                NativeBossCompletionId = "GODESSSOLUNA"
            },
            new NativeRaidState
            {
                DisplayName = "Fernalla",
                NativeBossCompletionId = "GODDESSFERNALLA"
            },
            new NativeRaidState
            {
                DisplayName = "Vitheo",
                NativeBossCompletionId = "GODVITHEO"
            }
        };


        private const string StateFolderName =
            "FateOfTheFallen";

        private const string StateFileName =
            "ExpansionGate.state";

        private const int StateVersion =
            2;

        private const string DevelopmentOverrideStateKey =
            "DevelopmentOverride";

        private const float StateFileCheckIntervalSeconds =
            2f;


        private static bool _initialized;
        private static bool _serverUnlocked;
        private static bool _effectiveUnlocked;
        private static bool _stateFileEverObserved;
        private static Coroutine _stateWatchRoutine;


        internal static event Action<bool>
            EffectiveUnlockChanged;


        internal static DevelopmentOverrideMode
            OverrideMode
        {
            get;
            private set;
        } =
            DevelopmentOverrideMode.Auto;


        internal static bool ServerUnlocked =>
            _serverUnlocked;


        internal static bool IsUnlocked =>
            _effectiveUnlocked;


        // ============================================================
        // PATHS
        // ============================================================

        private static string ServerSaveRoot
        {
            get
            {
                return
                    Path.Combine(
                        Application.persistentDataPath,
                        "ESSaveData");
            }
        }


        private static string StateDirectory
        {
            get
            {
                return
                    Path.Combine(
                        ServerSaveRoot,
                        StateFolderName);
            }
        }


        private static string StateFilePath
        {
            get
            {
                return
                    Path.Combine(
                        StateDirectory,
                        StateFileName);
            }
        }


        // ============================================================
        // LIFECYCLE
        // ============================================================

        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized =
                true;

            OverrideMode =
                DevelopmentOverrideMode.Auto;

            SceneManager.sceneLoaded +=
                OnSceneLoaded;

            LoadServerState();

            _stateWatchRoutine =
                CoroutineHost.Start(
                    WatchServerStateFile());
        }


        internal static void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            _initialized =
                false;

            SceneManager.sceneLoaded -=
                OnSceneLoaded;

            if (_stateWatchRoutine != null)
            {
                CoroutineHost.Stop(
                    _stateWatchRoutine);

                _stateWatchRoutine =
                    null;
            }

            EffectiveUnlockChanged =
                null;

            OverrideMode =
                DevelopmentOverrideMode.Auto;

            _serverUnlocked =
                false;

            _effectiveUnlocked =
                false;

            _stateFileEverObserved =
                false;

            ClearRaidStatesInMemory();
        }


        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            // Reloading here keeps the in-memory gate synchronized if the
            // player has returned from a reset/menu flow.
            LoadServerState();
        }


        private static IEnumerator WatchServerStateFile()
        {
            while (_initialized)
            {
                // A full native server reset deletes ESSaveData. If that
                // happens while FOTF remains loaded, clear the in-memory
                // completion state immediately rather than waiting for a
                // plugin/game restart.
                if (_stateFileEverObserved &&
                    !File.Exists(
                        StateFilePath))
                {
                    ClearServerProgress(
                        false,
                        false);
                }

                yield return
                    new WaitForSecondsRealtime(
                        StateFileCheckIntervalSeconds);
            }

            _stateWatchRoutine =
                null;
        }


        // ============================================================
        // DEVELOPMENT OVERRIDE
        // ============================================================

        internal static void ToggleDevelopmentLock()
        {
            OverrideMode =
                IsUnlocked
                    ? DevelopmentOverrideMode.ForceLocked
                    : DevelopmentOverrideMode.ForceUnlocked;

            SaveServerState();
            RecalculateEffectiveState();
        }


        internal static void SetAutomaticMode()
        {
            OverrideMode =
                DevelopmentOverrideMode.Auto;

            SaveServerState();
            LoadServerState();
        }


        internal static void RefreshServerState()
        {
            LoadServerState();
        }


        internal static void ForceLocked()
        {
            OverrideMode =
                DevelopmentOverrideMode.ForceLocked;

            SaveServerState();
            RecalculateEffectiveState();
        }


        internal static void ForceUnlocked()
        {
            OverrideMode =
                DevelopmentOverrideMode.ForceUnlocked;

            SaveServerState();
            RecalculateEffectiveState();
        }


        // ============================================================
        // STATUS
        // ============================================================

        internal static string BuildStatusText()
        {
            string effective =
                IsUnlocked
                    ? "UNLOCKED"
                    : "LOCKED";

            string server =
                _serverUnlocked
                    ? "4/4 COMPLETE"
                    : GetCompletionCount() +
                      "/4 COMPLETE";

            string persistence =
                OverrideMode ==
                    DevelopmentOverrideMode.Auto
                    ? string.Empty
                    : " (persisted)";

            return
                "FOTF expansion gate: " +
                effective +
                " | Mode=" +
                OverrideMode +
                persistence +
                " | Server=" +
                server +
                " | " +
                BuildNativeRaidSummary();
        }


        private static string BuildNativeRaidSummary()
        {
            List<string> entries =
                new List<string>();

            for (int i = 0;
                 i < NativeRaids.Length;
                 i++)
            {
                NativeRaidState state =
                    NativeRaids[i];

                entries.Add(
                    state.DisplayName +
                    "=" +
                    (state.Complete
                        ? "YES"
                        : "NO"));
            }

            return
                string.Join(
                    ", ",
                    entries.ToArray());
        }


        private static int GetCompletionCount()
        {
            int complete =
                0;

            for (int i = 0;
                 i < NativeRaids.Length;
                 i++)
            {
                if (NativeRaids[i].Complete)
                {
                    complete++;
                }
            }

            return
                complete;
        }


        // ============================================================
        // NATIVE BOSS-DEATH TRACKING
        // ============================================================

        internal static bool IsNativeRaidBossCompletionId(
            string completionId)
        {
            if (string.IsNullOrEmpty(
                    completionId))
            {
                return false;
            }

            for (int i = 0;
                 i < NativeRaids.Length;
                 i++)
            {
                if (string.Equals(
                        NativeRaids[i].NativeBossCompletionId,
                        completionId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }


        internal static void RecordNativeRaidBossDeath(
            string completionId)
        {
            if (!_initialized ||
                string.IsNullOrEmpty(
                    completionId))
            {
                return;
            }

            bool changed =
                false;

            for (int i = 0;
                 i < NativeRaids.Length;
                 i++)
            {
                NativeRaidState state =
                    NativeRaids[i];

                if (!string.Equals(
                        state.NativeBossCompletionId,
                        completionId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!state.Complete)
                {
                    state.Complete =
                        true;

                    changed =
                        true;
                }

                break;
            }

            if (!changed)
            {
                return;
            }

            SaveServerState();

            RefreshServerUnlocked();
        }


        // ============================================================
        // SERVER PERSISTENCE
        // ============================================================

        private static void LoadServerState()
        {
            ClearRaidStatesInMemory();

            // The development override is persisted in the same per-server
            // state file as the four native raid-completion bits. Reset to
            // Auto before reading so an absent/legacy state file always
            // fails back to the production 4/4 progression rule.
            OverrideMode =
                DevelopmentOverrideMode.Auto;

            string path =
                StateFilePath;

            if (!File.Exists(
                    path))
            {
                _stateFileEverObserved =
                    false;

                RefreshServerUnlocked();

                return;
            }

            _stateFileEverObserved =
                true;

            try
            {
                string[] lines =
                    File.ReadAllLines(
                        path);

                for (int lineIndex = 0;
                     lineIndex < lines.Length;
                     lineIndex++)
                {
                    string line =
                        lines[lineIndex];

                    if (string.IsNullOrWhiteSpace(
                            line))
                    {
                        continue;
                    }

                    int equals =
                        line.IndexOf('=');

                    if (equals <= 0 ||
                        equals >=
                            line.Length - 1)
                    {
                        continue;
                    }

                    string key =
                        line.Substring(
                            0,
                            equals)
                            .Trim();

                    string value =
                        line.Substring(
                            equals + 1)
                            .Trim();

                    if (string.Equals(
                            key,
                            "Version",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (string.Equals(
                            key,
                            DevelopmentOverrideStateKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        OverrideMode =
                            ParseDevelopmentOverrideMode(
                                value);

                        continue;
                    }

                    bool complete =
                        value == "1" ||
                        string.Equals(
                            value,
                            "true",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            value,
                            "yes",
                            StringComparison.OrdinalIgnoreCase);

                    for (int i = 0;
                         i < NativeRaids.Length;
                         i++)
                    {
                        NativeRaidState state =
                            NativeRaids[i];

                        if (string.Equals(
                                state.NativeBossCompletionId,
                                key,
                                StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(
                                state.DisplayName,
                                key,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            state.Complete =
                                complete;

                            break;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                // Fail closed if the server state cannot be read.
                ClearRaidStatesInMemory();

                OverrideMode =
                    DevelopmentOverrideMode.Auto;

                Plugin.NativeLog.LogError(
                    "FOTF expansion gate could not read current-server state: " +
                    exception);
            }

            RefreshServerUnlocked();
        }


        private static DevelopmentOverrideMode ParseDevelopmentOverrideMode(
            string value)
        {
            if (string.Equals(
                    value,
                    DevelopmentOverrideMode.ForceUnlocked.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    DevelopmentOverrideMode.ForceUnlocked;
            }

            if (string.Equals(
                    value,
                    DevelopmentOverrideMode.ForceLocked.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    DevelopmentOverrideMode.ForceLocked;
            }

            return
                DevelopmentOverrideMode.Auto;
        }


        private static void SaveServerState()
        {
            try
            {
                Directory.CreateDirectory(
                    StateDirectory);

                string tempPath =
                    StateFilePath +
                    ".tmp";

                List<string> lines =
                    new List<string>();

                lines.Add(
                    "Version=" +
                    StateVersion);

                lines.Add(
                    DevelopmentOverrideStateKey +
                    "=" +
                    OverrideMode);

                for (int i = 0;
                     i < NativeRaids.Length;
                     i++)
                {
                    NativeRaidState state =
                        NativeRaids[i];

                    lines.Add(
                        state.NativeBossCompletionId +
                        "=" +
                        (state.Complete
                            ? "1"
                            : "0"));
                }

                File.WriteAllLines(
                    tempPath,
                    lines.ToArray());

                if (File.Exists(
                        StateFilePath))
                {
                    File.Delete(
                        StateFilePath);
                }

                File.Move(
                    tempPath,
                    StateFilePath);

                _stateFileEverObserved =
                    true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "FOTF expansion gate could not save current-server state: " +
                    exception);
            }
        }


        /// <summary>
        /// Called when Erenshor performs a server/server-first reset.
        ///
        /// deleteFile is false after DeleteAllSaveData because Erenshor has
        /// already removed ESSaveData. It is true for /server1 because that
        /// native command only resets the native server-first state.
        /// </summary>
        internal static void ClearServerProgress(
            bool deleteFile,
            bool resetDevelopmentOverride)
        {
            ClearRaidStatesInMemory();

            if (deleteFile)
            {
                try
                {
                    if (File.Exists(
                            StateFilePath))
                    {
                        File.Delete(
                            StateFilePath);
                    }

                    if (Directory.Exists(
                            StateDirectory) &&
                        Directory.GetFileSystemEntries(
                            StateDirectory).Length == 0)
                    {
                        Directory.Delete(
                            StateDirectory);
                    }
                }
                catch (Exception exception)
                {
                    Plugin.NativeLog.LogError(
                        "FOTF expansion gate could not clear current-server state file: " +
                        exception);
                }
            }

            _stateFileEverObserved =
                false;

            if (resetDevelopmentOverride)
            {
                OverrideMode =
                    DevelopmentOverrideMode.Auto;
            }

            RefreshServerUnlocked();
        }


        private static void ClearRaidStatesInMemory()
        {
            for (int i = 0;
                 i < NativeRaids.Length;
                 i++)
            {
                NativeRaids[i].Complete =
                    false;
            }
        }


        private static void RefreshServerUnlocked()
        {
            bool allComplete =
                true;

            for (int i = 0;
                 i < NativeRaids.Length;
                 i++)
            {
                if (!NativeRaids[i].Complete)
                {
                    allComplete =
                        false;

                    break;
                }
            }

            _serverUnlocked =
                allComplete;

            RecalculateEffectiveState();
        }


        private static void RecalculateEffectiveState()
        {
            bool next;

            switch (OverrideMode)
            {
                case DevelopmentOverrideMode.ForceUnlocked:
                    next =
                        true;
                    break;

                case DevelopmentOverrideMode.ForceLocked:
                    next =
                        false;
                    break;

                default:
                    next =
                        _serverUnlocked;
                    break;
            }

            if (_effectiveUnlocked ==
                next)
            {
                return;
            }

            _effectiveUnlocked =
                next;

            Action<bool> handler =
                EffectiveUnlockChanged;

            if (handler == null)
            {
                return;
            }

            try
            {
                handler(
                    _effectiveUnlocked);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "FOTF expansion gate state-change handler failed: " +
                    exception);
            }
        }
    }


    // ================================================================
    // ACTUAL NATIVE END-BOSS DEATH
    // ================================================================

    /// <summary>
    /// Character.DoDeath is the same native path that consumes
    /// NPC.SetAchievementOnDefeat. We inspect that boss identifier after the
    /// death has actually occurred, but do not read or modify the achievement.
    ///
    /// This means a Steam achievement from an older server has no effect:
    /// Brax must die again on THIS server for the Brax server flag to be set.
    /// </summary>
    [HarmonyPatch(
        typeof(Character),
        "DoDeath")]
    internal static class Patch_FOTFExpansionGate_NativeBossDeath
    {
        [HarmonyPostfix]
        private static void Postfix(
            Character __instance)
        {
            if (__instance == null ||
                __instance.MyNPC == null)
            {
                return;
            }

            string completionId =
                __instance
                    .MyNPC
                    .SetAchievementOnDefeat;

            if (!FOTFExpansionGate
                .IsNativeRaidBossCompletionId(
                    completionId))
            {
                return;
            }

            FOTFExpansionGate
                .RecordNativeRaidBossDeath(
                    completionId);
        }
    }


    // ================================================================
    // FULL NATIVE SERVER RESET
    // ================================================================

    /// <summary>
    /// Erenshor's full server reset deletes Application.persistentDataPath /
    /// ESSaveData. Our state file lives inside that same server directory.
    /// Clear memory as soon as the native reset completes as well.
    /// </summary>
    [HarmonyPatch(
        typeof(ServerReset),
        "DeleteAllSaveData")]
    internal static class Patch_FOTFExpansionGate_ServerReset
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            FOTFExpansionGate
                .ClearServerProgress(
                    false,
                    true);
        }
    }
}
