using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FateOfTheFallen
{
    /// <summary>
    /// Custom-raid proof of concept.
    ///
    /// Stage 1 proved that FOTF_Raid_PoC can load from its external bundle,
    /// bootstrap as an Erenshor-compatible scene and place the persistent
    /// player at RaidEntry_Player.
    ///
    /// Stage 2 keeps Erenshor's native raid state authoritative. Entry now
    /// requires a real active Erenshor raid. After the custom scene has
    /// stabilized, the loader calls SimPlayerMngr.BringRaidToZone() -- the
    /// game's own raid-transfer method -- then distributes the resulting
    /// raid avatars around RaidEntry_Group1/2/3.
    /// </summary>
    internal static class CustomRaidLoader
    {
        internal const string RaidPocBundleName =
            "fotf_raid_poc";

        internal const string RaidPocSceneName =
            "FOTF_Raid_PoC";

        internal const string RaidPlayerSpawnName =
            "RaidEntry_Player";

        internal const string RaidGroup1SpawnName =
            "RaidEntry_Group1";

        internal const string RaidGroup2SpawnName =
            "RaidEntry_Group2";

        internal const string RaidGroup3SpawnName =
            "RaidEntry_Group3";

        internal const string RaidExitMarkerName =
            "RaidExit";

        internal const string WipeResetMarkerName =
            "WipeReset_Point";

        internal const string TrashPackMarkerName =
            "TrashPack_01";

        internal const string BossMarkerName =
            "Boss_01";

        internal const string BossArenaCenterMarkerName =
            "BossArena_Center";

        private static bool _initialized;
        private static bool _transitionInProgress;

        private static string _returnSceneName;
        private static Vector3 _returnPosition;
        private static Quaternion _returnRotation;
        private static bool _hasReturnPoint;

        private static bool _pendingRaidPlacement;
        private static bool _pendingReturnPlacement;
        private static bool _pendingWipeRecovery;

        // Legitimate native raid state captured immediately before entering
        // the custom scene. This is preservation state, not a parallel FOTF
        // raid implementation.
        private static bool _raidWasActiveAtEntry;
        private static RaidManager _raidManagerAtEntry;
        private static int _raidAssignedTrackingCountAtEntry;

        private sealed class RaidSlotSnapshot
        {
            internal int SlotIndex;
            internal int GroupNumber;
            internal SimPlayerTracking AssignedSimTracking;
            internal bool MainAssistMainTank;
            internal bool TrackingGrouped;
            internal bool WasInGroup1;
            internal bool WasInGroup2;
            internal bool WasInGroup3;
        }

        private static readonly List<RaidSlotSnapshot>
            _raidSlotSnapshots =
                new List<RaidSlotSnapshot>();


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

            SceneManager.sceneLoaded -=
                OnSceneLoaded;

            SceneManager.sceneLoaded +=
                OnSceneLoaded;
        }


        internal static void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            SceneManager.sceneLoaded -=
                OnSceneLoaded;

            _initialized =
                false;

            _transitionInProgress =
                false;

            _pendingRaidPlacement =
                false;

            _pendingReturnPlacement =
                false;

            _pendingWipeRecovery =
                false;

            RaidPocEncounterRuntime.ResetLifecycle();

            _raidWasActiveAtEntry =
                false;

            _raidManagerAtEntry =
                null;

            _raidAssignedTrackingCountAtEntry =
                0;

            _raidSlotSnapshots.Clear();
        }


        // ============================================================
        // ENTER RAID POC
        // ============================================================

        internal static bool EnterRaidPoc()
        {
            if (_transitionInProgress)
            {
                LogSystemMessage(
                    "A raid transition is already in progress.",
                    "yellow");

                return false;
            }

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (IsRaidPoc(
                    activeScene))
            {
                LogSystemMessage(
                    "You are already inside the FOTF Raid PoC.",
                    "yellow");

                return false;
            }

            if (!ValidateNativeRaidForEntry())
            {
                return false;
            }

            CaptureNativeRaidEntryState();

            string scenePath =
                ResolveRaidScenePath();

            if (string.IsNullOrEmpty(
                    scenePath))
            {
                LogSystemMessage(
                    "The FOTF Raid PoC could not be loaded. Check the FOTF log.",
                    "yellow");

                return false;
            }

            ZoneSceneDiagnostic.CaptureNativeBaseline(
                activeScene,
                "before /fotfraid transition");

            // Raid formation occurs in the Reliquary, which has no hostile
            // NPCs available for the bridge-PoC SpawnPoint template. The
            // template must therefore already have been cached automatically
            // while the player was previously in a normal combat zone.
            if (!BlightedThrallRuntime.HasCapturedNativeTemplate)
            {
                // One final synchronous attempt is harmless in case this is a
                // non-Reliquary entry path with hostiles available.
                BlightedThrallRuntime.CaptureNativeTemplateBeforeZoneChange();
            }

            if (!BlightedThrallRuntime.HasCapturedNativeTemplate)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] /fotfraid rejected: no hostile native SpawnPoint template has been cached this session. " +
                    "The Reliquary contains no valid enemy template source.");

                LogSystemMessage(
                    "No FOTF enemy template is cached yet. Visit any normal combat zone with hostile NPCs once, then return to the Reliquary and form the raid.",
                    "yellow");

                return false;
            }

            CaptureReturnPoint(
                activeScene);

            _transitionInProgress =
                true;

            _pendingRaidPlacement =
                true;

            _pendingReturnPlacement =
                false;

            _pendingWipeRecovery =
                false;


            try
            {
                AsyncOperation operation =
                    SceneManager.LoadSceneAsync(
                        scenePath,
                        LoadSceneMode.Single);

                if (operation == null)
                {
                    _transitionInProgress =
                        false;

                    _pendingRaidPlacement =
                        false;

                    Plugin.NativeLog.LogError(
                        "CustomRaidLoader: SceneManager.LoadSceneAsync returned null for [" +
                        scenePath +
                        "].");

                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                _transitionInProgress =
                    false;

                _pendingRaidPlacement =
                    false;

                Plugin.NativeLog.LogError(
                    "CustomRaidLoader: failed to begin raid load: " +
                    exception);

                return false;
            }
        }


        // ============================================================
        // RETURN TO ENTRY SCENE
        // ============================================================

        internal static bool ReturnToPreviousZone()
        {
            if (_transitionInProgress)
            {
                LogSystemMessage(
                    "A raid transition is already in progress.",
                    "yellow");

                return false;
            }

            if (!_hasReturnPoint ||
                string.IsNullOrEmpty(
                    _returnSceneName))
            {
                LogSystemMessage(
                    "No FOTF raid return point has been recorded.",
                    "yellow");

                return false;
            }

            if (!IsRaidPoc(
                    SceneManager.GetActiveScene()))
            {
                LogSystemMessage(
                    "The raid return command is only needed inside the FOTF Raid PoC.",
                    "yellow");

                return false;
            }

            RaidPocEncounterRuntime.Stop();

            _transitionInProgress =
                true;

            _pendingReturnPlacement =
                true;

            _pendingRaidPlacement =
                false;

            _pendingWipeRecovery =
                false;


            try
            {
                AsyncOperation operation =
                    SceneManager.LoadSceneAsync(
                        _returnSceneName,
                        LoadSceneMode.Single);

                if (operation == null)
                {
                    _transitionInProgress =
                        false;

                    _pendingReturnPlacement =
                        false;

                    Plugin.NativeLog.LogError(
                        "CustomRaidLoader: SceneManager.LoadSceneAsync returned null while returning to [" +
                        _returnSceneName +
                        "].");

                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                _transitionInProgress =
                    false;

                _pendingReturnPlacement =
                    false;

                Plugin.NativeLog.LogError(
                    "CustomRaidLoader: failed to begin raid return load: " +
                    exception);

                return false;
            }
        }


        // ============================================================
        // SCENE RESOLUTION
        // ============================================================

        private static string ResolveRaidScenePath()
        {
            AssetBundle bundle =
                AssetBundleRegistry.GetBundle(
                    RaidPocBundleName);

            if (bundle == null)
            {
                return null;
            }

            string[] scenePaths;

            try
            {
                scenePaths =
                    bundle.GetAllScenePaths();
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "CustomRaidLoader: failed to enumerate scenes in bundle [" +
                    RaidPocBundleName +
                    "]: " +
                    exception);

                return null;
            }

            if (scenePaths == null ||
                scenePaths.Length == 0)
            {
                Plugin.NativeLog.LogError(
                    "CustomRaidLoader: bundle [" +
                    RaidPocBundleName +
                    "] contains no scenes.");

                return null;
            }

            for (int i = 0;
                 i < scenePaths.Length;
                 i++)
            {
                string path =
                    scenePaths[i];

                if (string.IsNullOrEmpty(
                        path))
                {
                    continue;
                }

                string fileName =
                    Path.GetFileNameWithoutExtension(
                        path);

                if (string.Equals(
                        fileName,
                        RaidPocSceneName,
                        StringComparison.OrdinalIgnoreCase))
                {

                    return path;
                }
            }

            Plugin.NativeLog.LogError(
                "CustomRaidLoader: scene [" +
                RaidPocSceneName +
                "] was not found in bundle [" +
                RaidPocBundleName +
                "]. Available scenes: " +
                string.Join(
                    ", ",
                    scenePaths));

            return null;
        }


        // ============================================================
        // RETURN POINT
        // ============================================================

        private static void CaptureReturnPoint(
            Scene activeScene)
        {
            _returnSceneName =
                activeScene.name;

            PlayerControl player =
                GameData.PlayerControl;

            if (player != null)
            {
                _returnPosition =
                    player.transform.position;

                _returnRotation =
                    player.transform.rotation;

                _hasReturnPoint =
                    true;

                return;
            }

            _returnPosition =
                Vector3.zero;

            _returnRotation =
                Quaternion.identity;

            _hasReturnPoint =
                !string.IsNullOrEmpty(
                    _returnSceneName);

            Plugin.NativeLog.LogWarning(
                "CustomRaidLoader: PlayerControl was unavailable while capturing the return point. " +
                "The scene name was retained, but the return position could not be captured.");
        }


        // ============================================================
        // SCENE LOADED
        // ============================================================

        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            try
            {
                if (_pendingWipeRecovery &&
                    !IsRaidPoc(
                        scene))
                {
                    _transitionInProgress =
                        false;

                    _pendingWipeRecovery =
                        false;

                    CoroutineHost.Start(
                        RestoreRaidAfterNativeWipe(
                            scene));

                    return;
                }

                if (IsRaidPoc(
                        scene))
                {
                    _transitionInProgress =
                        false;

                    if (_pendingRaidPlacement)
                    {
                        _pendingRaidPlacement =
                            false;

                        CoroutineHost.Start(
                            PlacePlayerAtRaidSpawn(
                                scene));
                    }

                    return;
                }

                if (_pendingReturnPlacement &&
                    string.Equals(
                        scene.name,
                        _returnSceneName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _transitionInProgress =
                        false;

                    _pendingReturnPlacement =
                        false;

                    CoroutineHost.Start(
                        RestorePlayerToReturnPoint());
                }
            }
            catch (Exception exception)
            {
                _transitionInProgress =
                    false;

                Plugin.NativeLog.LogError(
                    "CustomRaidLoader: scene-loaded handling failed: " +
                    exception);
            }
        }


        // ============================================================
        // RAID PLACEMENT
        // ============================================================

        private static IEnumerator PlacePlayerAtRaidSpawn(
            Scene raidScene)
        {
            const int maximumFrames =
                600;

            const int stabilizationFrames =
                45;

            for (int frame = 0;
                 frame < maximumFrames;
                 frame++)
            {
                if (!raidScene.IsValid() ||
                    !raidScene.isLoaded ||
                    !IsRaidPoc(
                        SceneManager.GetActiveScene()))
                {
                    yield break;
                }

                PlayerControl player =
                    GameData.PlayerControl;

                GameObject marker =
                    FindSceneObject(
                        raidScene,
                        RaidPlayerSpawnName);

                if (player != null &&
                    marker != null)
                {
                    Vector3 spawnPosition =
                        marker.transform.position;

                    Quaternion spawnRotation =
                        marker.transform.rotation;


                    for (int settleFrame = 0;
                         settleFrame < stabilizationFrames;
                         settleFrame++)
                    {
                        PlayerControl currentPlayer =
                            GameData.PlayerControl;

                        if (currentPlayer == null ||
                            !IsRaidPoc(
                                SceneManager.GetActiveScene()))
                        {
                            yield break;
                        }

                        SetPlayerTransform(
                            currentPlayer,
                            spawnPosition,
                            spawnRotation);

                        yield return null;
                    }

                    PlayerControl finalPlayer =
                        GameData.PlayerControl;

                    if (finalPlayer != null)
                    {
                        SetPlayerTransform(
                            finalPlayer,
                            spawnPosition,
                            spawnRotation);

                    }

                    LogMarkerStatus(
                        raidScene);

                    // Stage 2: preserve/rebind the legitimate native raid and
                    // ask Erenshor itself to spawn the roster in this scene.
                    yield return TransferNativeRaidIntoScene(
                        raidScene);

                    RaidPocEncounterRuntime.Begin(
                        raidScene);

                    LogSystemMessage(
                        "Entered FOTF Raid PoC. Use /fotfraidreturn to return.",
                        "");

                    yield break;
                }

                yield return null;
            }

            Plugin.NativeLog.LogError(
                "CustomRaidLoader: timed out waiting for PlayerControl and marker [" +
                RaidPlayerSpawnName +
                "].");

            LogSystemMessage(
                "The raid scene loaded, but the player entry marker could not be resolved.",
                "yellow");
        }


        private static IEnumerator RestorePlayerToReturnPoint()
        {
            const int maximumFrames =
                600;

            for (int frame = 0;
                 frame < maximumFrames;
                 frame++)
            {
                PlayerControl player =
                    GameData.PlayerControl;

                if (player != null)
                {
                    yield return null;
                    yield return null;
                    yield return null;

                    SetPlayerTransform(
                        player,
                        _returnPosition,
                        _returnRotation);

                    yield return null;

                    SetPlayerTransform(
                        player,
                        _returnPosition,
                        _returnRotation);


                    if (GameData.RaidActive &&
                        GameData.RaidManager != null &&
                        GameData.SimMngr != null)
                    {
                        // A Single-scene transition can clear the slot tracking
                        // references again on the way back out of the PoC.
                        if (CountAssignedTracking(
                                GameData.RaidManager) <
                            _raidAssignedTrackingCountAtEntry)
                        {
                            RestoreNativeRaidRosterFromSnapshot();
                        }

                        int expectedRaiders =
                            CountAssignedTracking(
                                GameData.RaidManager);

                        try
                        {
                            GameData.SimMngr.BringRaidToZone();


                            // As on raid entry, SpawnMeInGame() has already
                            // populated each tracking object's MyAvatar by the
                            // time BringRaidToZone() returns. Restore raid AI
                            // linkage immediately so there is no multi-second
                            // inactive window.
                            RepairNativeRaidAvatarLinks(
                                "RETURN TO NATIVE SCENE IMMEDIATE");
                        }
                        catch (Exception exception)
                        {
                            Plugin.NativeLog.LogError(
                                "CustomRaidLoader: failed to bring raid back to the return scene: " +
                                exception);
                        }

                        const int maximumReturnRaidFrames =
                            600;

                        for (int raidFrame = 0;
                             raidFrame < maximumReturnRaidFrames;
                             raidFrame++)
                        {
                            if (CountAssignedAvatars(
                                    GameData.RaidManager) >=
                                expectedRaiders)
                            {
                                break;
                            }

                            yield return null;
                        }

                        RepairNativeRaidAvatarLinks(
                            "RETURN TO NATIVE SCENE");
                    }

                    LogSystemMessage(
                        "Returned from the FOTF Raid PoC.",
                        "");

                    yield break;
                }

                yield return null;
            }

            Plugin.NativeLog.LogError(
                "CustomRaidLoader: timed out waiting for PlayerControl while restoring the raid return point.");
        }


        // ============================================================
        // RAID WIPE / NATIVE RELIQUARY RECOVERY
        // ============================================================

        /// <summary>
        /// Called from the Harmony prefix on the native
        /// RespawnWindow.RespawnPlayer() method when the player actually
        /// clicks Respawn inside the custom raid. The original Erenshor method
        /// is then allowed to run unchanged; this flag tells our scene-loaded
        /// handler to rebuild the still-active raid around the player at the
        /// native Reliquary destination.
        /// </summary>
        internal static void PrepareForNativeWipeRecovery()
        {
            if (!IsRaidPoc(
                    SceneManager.GetActiveScene()))
            {
                return;
            }

            RaidPocEncounterRuntime.Stop();

            _pendingWipeRecovery =
                true;

            _pendingRaidPlacement =
                false;

            _pendingReturnPlacement =
                false;

            _transitionInProgress =
                true;

        }


        private static IEnumerator RestoreRaidAfterNativeWipe(
            Scene destinationScene)
        {
            // The native respawn flow owns player revival and placement. FOTF
            // only reconstructs the legitimate raid roster/avatars after that
            // flow reaches its destination.
            yield return null;
            yield return null;
            yield return null;

            RebindLegitimateRaidStateIfNeeded();

            if (!GameData.RaidActive ||
                GameData.RaidManager == null ||
                GameData.SimMngr == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] native Reliquary respawn completed, but raid state is unavailable. Scene=[" +
                    destinationScene.name +
                    "].");

                yield break;
            }

            if (CountAssignedTracking(
                    GameData.RaidManager) <
                _raidAssignedTrackingCountAtEntry)
            {
                RestoreNativeRaidRosterFromSnapshot();
            }

            int expectedRaiders =
                CountAssignedTracking(
                    GameData.RaidManager);

            try
            {
                GameData.SimMngr.BringRaidToZone();

                RepairNativeRaidAvatarLinks(
                    "POST-WIPE RELIQUARY IMMEDIATE");

            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[RaidPoC] failed to rebuild raid after native Reliquary respawn: " +
                    exception);

                yield break;
            }

            const int maximumFrames =
                300;

            for (int frame = 0;
                 frame < maximumFrames;
                 frame++)
            {
                if (CountAssignedAvatars(
                        GameData.RaidManager) >=
                    expectedRaiders)
                {
                    break;
                }

                yield return null;
            }

            RepairNativeRaidAvatarLinks(
                "POST-WIPE RELIQUARY");

            LogRaidState(
                "POST-WIPE RELIQUARY");

            LogSystemMessage(
                "Raid wipe confirmed. Returned to the Reliquary.",
                "yellow");
        }


        internal static bool IsRaidPocScene(
            Scene scene)
        {
            return IsRaidPoc(
                scene);
        }


        // ============================================================
        // STAGE 2 - NATIVE RAID PRESERVATION / TRANSFER
        // ============================================================

        private static bool ValidateNativeRaidForEntry()
        {
            if (!GameData.RaidActive)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] /fotfraid rejected: GameData.RaidActive=False.");

                LogSystemMessage(
                    "Form an Erenshor raid before using /fotfraid.",
                    "yellow");

                return false;
            }

            if (GameData.RaidManager == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] /fotfraid rejected: RaidActive=True but GameData.RaidManager is null.");

                LogSystemMessage(
                    "The raid is active, but Erenshor's RaidManager is not ready yet.",
                    "yellow");

                return false;
            }

            if (GameData.RaidManager.Raiders == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] /fotfraid rejected: RaidManager.Raiders is null.");

                LogSystemMessage(
                    "Erenshor's raid roster is not ready yet.",
                    "yellow");

                return false;
            }

            if (GameData.SimMngr == null)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] /fotfraid rejected: GameData.SimMngr is null.");

                LogSystemMessage(
                    "Erenshor's SimPlayer manager is not ready yet.",
                    "yellow");

                return false;
            }

            int assigned =
                CountAssignedTracking(
                    GameData.RaidManager);

            if (assigned <= 0)
            {
                Plugin.NativeLog.LogWarning(
                    "[RaidPoC] /fotfraid rejected: active raid has no assigned SimPlayer tracking entries.");

                LogSystemMessage(
                    "Add at least one SimPlayer to the raid before using /fotfraid.",
                    "yellow");

                return false;
            }

            return true;
        }


        private static void CaptureNativeRaidEntryState()
        {
            _raidWasActiveAtEntry =
                GameData.RaidActive;

            _raidManagerAtEntry =
                GameData.RaidManager;

            _raidAssignedTrackingCountAtEntry =
                CountAssignedTracking(
                    _raidManagerAtEntry);

            _raidSlotSnapshots.Clear();

            if (_raidManagerAtEntry != null &&
                _raidManagerAtEntry.Raiders != null)
            {
                foreach (RaidMemberSlot slot
                         in _raidManagerAtEntry.Raiders)
                {
                    if (slot == null ||
                        slot.AssignedSimTracking == null ||
                        string.IsNullOrEmpty(
                            slot.AssignedSimTracking.SimName))
                    {
                        continue;
                    }

                    RaidSlotSnapshot snapshot =
                        new RaidSlotSnapshot();

                    snapshot.SlotIndex =
                        slot.SlotIndex;

                    snapshot.GroupNumber =
                        slot.GroupNumber;

                    snapshot.AssignedSimTracking =
                        slot.AssignedSimTracking;

                    snapshot.MainAssistMainTank =
                        slot.MainAssistMainTank;

                    snapshot.TrackingGrouped =
                        slot.AssignedSimTracking.Grouped;

                    snapshot.WasInGroup1 =
                        _raidManagerAtEntry.Group1 != null &&
                        _raidManagerAtEntry.Group1.Contains(
                            slot);

                    snapshot.WasInGroup2 =
                        _raidManagerAtEntry.Group2 != null &&
                        _raidManagerAtEntry.Group2.Contains(
                            slot);

                    snapshot.WasInGroup3 =
                        _raidManagerAtEntry.Group3 != null &&
                        _raidManagerAtEntry.Group3.Contains(
                            slot);

                    _raidSlotSnapshots.Add(
                        snapshot);
                }
            }


            LogRaidState(
                "BEFORE LOAD");
        }



        private static void RebindLegitimateRaidStateIfNeeded()
        {
            if (!_raidWasActiveAtEntry)
            {
                return;
            }

            // If the persistent native RaidManager survived the scene load but
            // GameData lost only its static reference, restore that reference.
            // Unity's overloaded null check safely rejects a destroyed manager.
            if (GameData.RaidManager == null &&
                _raidManagerAtEntry != null)
            {
                GameData.RaidManager =
                    _raidManagerAtEntry;

            }

            // We only reassert RaidActive when the player genuinely entered
            // with an active raid AND a native RaidManager still exists.
            if (!GameData.RaidActive &&
                GameData.RaidManager != null)
            {
                GameData.RaidActive =
                    true;

            }
        }


        private static bool RestoreNativeRaidRosterFromSnapshot()
        {
            RaidManager manager =
                GameData.RaidManager;

            if (manager == null ||
                manager.Raiders == null ||
                _raidSlotSnapshots.Count == 0)
            {
                return false;
            }

            int restored =
                0;

            for (int i = 0;
                 i < _raidSlotSnapshots.Count;
                 i++)
            {
                RaidSlotSnapshot snapshot =
                    _raidSlotSnapshots[i];

                if (snapshot == null ||
                    snapshot.AssignedSimTracking == null)
                {
                    continue;
                }

                RaidMemberSlot slot =
                    FindRaidSlotByIndex(
                        manager,
                        snapshot.SlotIndex);

                if (slot == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "[RaidPoC] could not restore raid member [" +
                        snapshot.AssignedSimTracking.SimName +
                        "]; slot index [" +
                        snapshot.SlotIndex +
                        "] no longer exists.");

                    continue;
                }

                // The custom Single-scene load keeps RaidManager and its
                // fourteen UI slots alive, but Erenshor clears the actual
                // AssignedSimTracking references. Restore the legitimate
                // pre-transition roster before using BringRaidToZone().
                slot.AssignedSimTracking =
                    snapshot.AssignedSimTracking;

                // The pre-load avatar belonged to the old scene. The fresh
                // SpawnMeInGame instance will repopulate this field from
                // SimPlayer.LoadNewInstanceIntoRaid().
                slot.AssignedAvatar =
                    null;

                slot.GroupNumber =
                    snapshot.GroupNumber;

                slot.MainAssistMainTank =
                    snapshot.MainAssistMainTank;

                snapshot.AssignedSimTracking.Grouped =
                    snapshot.TrackingGrouped;

                RestoreNativeGroupMembership(
                    manager,
                    slot,
                    snapshot);

                restored++;
            }


            return restored ==
                   _raidSlotSnapshots.Count;
        }


        private static RaidMemberSlot FindRaidSlotByIndex(
            RaidManager manager,
            int slotIndex)
        {
            if (manager == null ||
                manager.Raiders == null)
            {
                return null;
            }

            foreach (RaidMemberSlot slot
                     in manager.Raiders)
            {
                if (slot != null &&
                    slot.SlotIndex ==
                        slotIndex)
                {
                    return slot;
                }
            }

            return null;
        }


        private static void RestoreNativeGroupMembership(
            RaidManager manager,
            RaidMemberSlot slot,
            RaidSlotSnapshot snapshot)
        {
            if (manager == null ||
                slot == null ||
                snapshot == null)
            {
                return;
            }

            if (manager.Group1 != null)
            {
                manager.Group1.Remove(
                    slot);
            }

            if (manager.Group2 != null)
            {
                manager.Group2.Remove(
                    slot);
            }

            if (manager.Group3 != null)
            {
                manager.Group3.Remove(
                    slot);
            }

            bool restoredFromCapturedMembership =
                false;

            if (snapshot.WasInGroup1 &&
                manager.Group1 != null)
            {
                manager.Group1.Add(
                    slot);

                restoredFromCapturedMembership =
                    true;
            }

            if (snapshot.WasInGroup2 &&
                manager.Group2 != null)
            {
                manager.Group2.Add(
                    slot);

                restoredFromCapturedMembership =
                    true;
            }

            if (snapshot.WasInGroup3 &&
                manager.Group3 != null)
            {
                manager.Group3.Add(
                    slot);

                restoredFromCapturedMembership =
                    true;
            }

            // Fallback for any native layout where the slot was not present
            // in the per-group lists at capture time.
            if (!restoredFromCapturedMembership)
            {
                if (snapshot.GroupNumber == 1 &&
                    manager.Group1 != null)
                {
                    manager.Group1.Add(
                        slot);
                }
                else if (snapshot.GroupNumber == 2 &&
                         manager.Group2 != null)
                {
                    manager.Group2.Add(
                        slot);
                }
                else if (snapshot.GroupNumber == 3 &&
                         manager.Group3 != null)
                {
                    manager.Group3.Add(
                        slot);
                }
            }
        }


        private static IEnumerator TransferNativeRaidIntoScene(
            Scene raidScene)
        {
            const int maximumReadyFrames =
                600;

            bool ready =
                false;

            for (int frame = 0;
                 frame < maximumReadyFrames;
                 frame++)
            {
                if (!raidScene.IsValid() ||
                    !raidScene.isLoaded ||
                    !IsRaidPoc(
                        SceneManager.GetActiveScene()))
                {
                    yield break;
                }

                RebindLegitimateRaidStateIfNeeded();

                if (GameData.RaidActive &&
                    GameData.RaidManager != null &&
                    GameData.RaidManager.Raiders != null &&
                    GameData.SimMngr != null &&
                    GameData.PlayerControl != null &&
                    GameData.ZoneAnnounce != null &&
                    GameData.NearestZoneIn != null)
                {
                    ready =
                        true;

                    break;
                }

                yield return null;
            }

            LogRaidState(
                "AFTER LOAD / BEFORE ROSTER RESTORE");

            if (!ready)
            {
                Plugin.NativeLog.LogError(
                    "[RaidPoC] native raid state did not become ready inside the custom raid scene.");

                LogSystemMessage(
                    "The raid scene loaded, but Erenshor's native raid state did not survive the transition. Check the FOTF log.",
                    "yellow");

                yield break;
            }

            int liveAssignedBeforeRestore =
                CountAssignedTracking(
                    GameData.RaidManager);

            if (liveAssignedBeforeRestore <
                _raidAssignedTrackingCountAtEntry)
            {

                RestoreNativeRaidRosterFromSnapshot();
            }

            LogRaidState(
                "AFTER ROSTER RESTORE / BEFORE TRANSFER");

            int expectedRaiders =
                CountAssignedTracking(
                    GameData.RaidManager);

            if (expectedRaiders <= 0)
            {
                Plugin.NativeLog.LogError(
                    "[RaidPoC] raid roster restoration failed; no assigned raiders remain for BringRaidToZone().");

                LogSystemMessage(
                    "The native raid roster could not be restored after the scene transition. Check the FOTF log.",
                    "yellow");

                yield break;
            }

            try
            {
                // This is Erenshor's own cross-zone raid-transfer routine.
                GameData.SimMngr.BringRaidToZone();


                // SpawnMeInGame() is synchronous and updates
                // AssignedSimTracking.MyAvatar before BringRaidToZone()
                // returns. Repair the NPC -> RaidMemberSlot backlink now
                // rather than waiting up to 600 frames for AssignedAvatar
                // to be populated by SimPlayer.Start().
                RepairNativeRaidAvatarLinks(
                    "CUSTOM RAID IMMEDIATE");
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[RaidPoC] native BringRaidToZone failed: " +
                    exception);

                LogSystemMessage(
                    "Erenshor's native raid transfer failed. Check the FOTF log.",
                    "yellow");

                yield break;
            }

            // The immediate repair above should populate AssignedAvatar from
            // AssignedSimTracking.MyAvatar in the same frame. Keep this loop
            // only as a fallback for any unusually delayed avatar startup.
            const int maximumAvatarFrames =
                600;

            for (int frame = 0;
                 frame < maximumAvatarFrames;
                 frame++)
            {
                int liveAvatars =
                    CountAssignedAvatars(
                        GameData.RaidManager);

                if (liveAvatars >=
                    expectedRaiders)
                {
                    break;
                }

                yield return null;
            }

            int finalAvatarCount =
                CountAssignedAvatars(
                    GameData.RaidManager);


            RepairNativeRaidAvatarLinks(
                "CUSTOM RAID");

            PlaceRaidAvatarsAtGroupMarkers(
                raidScene);

            LogRaidState(
                "AFTER NATIVE TRANSFER");

            if (finalAvatarCount <
                expectedRaiders)
            {
                LogSystemMessage(
                    "The raid loaded, but some SimPlayers did not reconnect. Check the FOTF log.",
                    "yellow");
            }
            else
            {
                LogSystemMessage(
                    "Native Erenshor raid transferred into the FOTF Raid PoC.",
                    "");
            }
        }


        private static int RepairNativeRaidAvatarLinks(
            string stage)
        {
            RaidManager manager =
                GameData.RaidManager;

            if (manager == null ||
                manager.Raiders == null)
            {
                return 0;
            }

            int occupied =
                0;

            int linked =
                0;

            foreach (RaidMemberSlot slot
                     in manager.Raiders)
            {
                if (slot == null ||
                    slot.AssignedSimTracking == null ||
                    string.IsNullOrEmpty(
                        slot.AssignedSimTracking.SimName))
                {
                    continue;
                }

                occupied++;

                SimPlayer avatar =
                    slot.AssignedAvatar;

                // SpawnMeInGame normally updates MyAvatar immediately. Use it
                // as a fallback if the RaidMemberSlot has not yet rebound.
                if (avatar == null &&
                    slot.AssignedSimTracking.MyAvatar != null)
                {
                    avatar =
                        slot.AssignedSimTracking.MyAvatar;

                    slot.AssignedAvatar =
                        avatar;
                }

                if (avatar == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "[RaidPoC] " +
                        stage +
                        " slot [" +
                        slot.SlotIndex +
                        "] [" +
                        slot.AssignedSimTracking.SimName +
                        "] has no live avatar.");

                    continue;
                }

                slot.AssignedSimTracking.MyAvatar =
                    avatar;

                avatar.InRaid =
                    true;

                NPC avatarNpc =
                    avatar.GetThisNPC();

                if (avatarNpc == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "[RaidPoC] " +
                        stage +
                        " slot [" +
                        slot.SlotIndex +
                        "] [" +
                        slot.AssignedSimTracking.SimName +
                        "] has no NPC component.");

                    continue;
                }

                avatarNpc.indexInRaid =
                    slot.SlotIndex;

                // This is the critical runtime backlink used by:
                //
                // SimPlayer.Update():
                //   else if (InRaid && MyNPC.MyRaidSlot != null)
                //       InRaidLogic();
                //
                // Without it a SimPlayer is flagged as InRaid, so ordinary
                // group/solo AI is skipped, but raid AI never runs either.
                avatarNpc.MyRaidSlot =
                    slot;

                linked++;

            }


            return linked;
        }


        private static void PlaceRaidAvatarsAtGroupMarkers(
            Scene raidScene)
        {
            RaidManager manager =
                GameData.RaidManager;

            if (manager == null ||
                manager.Raiders == null)
            {
                return;
            }

            int group1Index =
                0;

            int group2Index =
                0;

            int group3Index =
                0;

            foreach (RaidMemberSlot slot
                     in manager.Raiders)
            {
                if (slot == null ||
                    slot.AssignedAvatar == null)
                {
                    continue;
                }

                int group =
                    slot.GroupNumber;

                string markerName;

                int formationIndex;

                if (group == 1)
                {
                    markerName =
                        RaidGroup1SpawnName;

                    formationIndex =
                        group1Index++;
                }
                else if (group == 2)
                {
                    markerName =
                        RaidGroup2SpawnName;

                    formationIndex =
                        group2Index++;
                }
                else if (group == 3)
                {
                    markerName =
                        RaidGroup3SpawnName;

                    formationIndex =
                        group3Index++;
                }
                else
                {
                    Plugin.NativeLog.LogWarning(
                        "[RaidPoC] raid slot [" +
                        slot.SlotIndex +
                        "] has unsupported GroupNumber=[" +
                        group +
                        "]; leaving avatar at native spawn position.");

                    continue;
                }

                GameObject marker =
                    FindSceneObject(
                        raidScene,
                        markerName);

                if (marker == null)
                {
                    Plugin.NativeLog.LogWarning(
                        "[RaidPoC] missing group marker [" +
                        markerName +
                        "] while positioning raid slot [" +
                        slot.SlotIndex +
                        "].");

                    continue;
                }

                Vector3 desired =
                    marker.transform.position +
                    GetFormationOffset(
                        formationIndex);

                Vector3 finalPosition =
                    ResolveSafeNavMeshPosition(
                        desired);

                SimPlayer avatar =
                    slot.AssignedAvatar;

                avatar.InRaid =
                    true;

                NPC avatarNpc =
                    avatar.GetThisNPC();

                if (avatarNpc != null)
                {
                    avatarNpc.indexInRaid =
                        slot.SlotIndex;

                    // SimPlayer.Update() only enters InRaidLogic() when
                    // InRaid is true AND NPC.MyRaidSlot is non-null.
                    // LoadNewInstanceIntoRaid() restores InRaid/indexInRaid
                    // but does not restore this backlink for our custom
                    // Single-scene transition.
                    avatarNpc.MyRaidSlot =
                        slot;
                }

                NavMeshAgent agent =
                    avatar.GetComponent<NavMeshAgent>();

                bool warped =
                    false;

                if (agent != null &&
                    agent.enabled &&
                    agent.isOnNavMesh)
                {
                    try
                    {
                        warped =
                            agent.Warp(
                                finalPosition);
                    }
                    catch
                    {
                        warped =
                            false;
                    }
                }

                if (!warped)
                {
                    avatar.transform.position =
                        finalPosition;
                }

                avatar.transform.rotation =
                    marker.transform.rotation;

            }
        }


        private static Vector3 GetFormationOffset(
            int index)
        {
            // Compact 5-position formation around each authored group anchor.
            // Extra members continue in rows behind it.
            int row =
                index / 5;

            int column =
                index % 5;

            float x =
                (column - 2) *
                1.35f;

            float z =
                -row *
                1.6f;

            return new Vector3(
                x,
                0f,
                z);
        }


        private static Vector3 ResolveSafeNavMeshPosition(
            Vector3 desired)
        {
            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                    desired,
                    out hit,
                    4f,
                    NavMesh.AllAreas))
            {
                return hit.position;
            }

            return desired;
        }


        private static int CountAssignedTracking(
            RaidManager manager)
        {
            if (manager == null ||
                manager.Raiders == null)
            {
                return 0;
            }

            int count =
                0;

            foreach (RaidMemberSlot slot
                     in manager.Raiders)
            {
                if (slot != null &&
                    slot.AssignedSimTracking != null &&
                    !string.IsNullOrEmpty(
                        slot.AssignedSimTracking.SimName))
                {
                    count++;
                }
            }

            return count;
        }


        private static int CountAssignedAvatars(
            RaidManager manager)
        {
            if (manager == null ||
                manager.Raiders == null)
            {
                return 0;
            }

            int count =
                0;

            foreach (RaidMemberSlot slot
                     in manager.Raiders)
            {
                if (slot != null &&
                    slot.AssignedAvatar != null)
                {
                    count++;
                }
            }

            return count;
        }


        private static int CountLinkedRaidAvatars(
            RaidManager manager)
        {
            if (manager == null ||
                manager.Raiders == null)
            {
                return 0;
            }

            int count =
                0;

            foreach (RaidMemberSlot slot
                     in manager.Raiders)
            {
                if (slot == null ||
                    slot.AssignedAvatar == null)
                {
                    continue;
                }

                NPC npc =
                    slot.AssignedAvatar.GetThisNPC();

                if (npc != null &&
                    npc.MyRaidSlot ==
                        slot)
                {
                    count++;
                }
            }

            return count;
        }


        private static string GetRaidMemberName(
            RaidMemberSlot slot)
        {
            if (slot == null)
            {
                return "<null>";
            }

            if (slot.AssignedSimTracking != null &&
                !string.IsNullOrEmpty(
                    slot.AssignedSimTracking.SimName))
            {
                return slot.AssignedSimTracking.SimName;
            }

            if (slot.AssignedAvatar != null)
            {
                NPC avatarNpc =
                    slot.AssignedAvatar.GetThisNPC();

                if (avatarNpc != null &&
                    !string.IsNullOrEmpty(
                        avatarNpc.NPCName))
                {
                    return avatarNpc.NPCName;
                }
            }

            return "<unassigned>";
        }


        private static void LogRaidState(
            string stage)
        {
            RaidManager manager =
                GameData.RaidManager;

            int slots =
                manager != null &&
                manager.Raiders != null
                    ? manager.Raiders.Count
                    : 0;

            int assignedTracking =
                CountAssignedTracking(
                    manager);

            int avatars =
                CountAssignedAvatars(
                    manager);

            int linkedRaidSlots =
                CountLinkedRaidAvatars(
                    manager);

            string managerObjectState =
                manager != null
                    ? manager.name +
                      " ActiveSelf=" +
                      manager.gameObject.activeSelf +
                      " ActiveInHierarchy=" +
                      manager.gameObject.activeInHierarchy
                    : "<null>";

        }


        // ============================================================
        // RAID MARKER DIAGNOSTIC
        // ============================================================

        private static void LogMarkerStatus(
            Scene scene)
        {
            string[] names =
            {
                RaidPlayerSpawnName,
                RaidGroup1SpawnName,
                RaidGroup2SpawnName,
                RaidGroup3SpawnName,
                RaidExitMarkerName,
                WipeResetMarkerName,
                TrashPackMarkerName,
                BossMarkerName,
                BossArenaCenterMarkerName
            };

            for (int i = 0;
                 i < names.Length;
                 i++)
            {
                GameObject marker =
                    FindSceneObject(
                        scene,
                        names[i]);

            }
        }


        // ============================================================
        // HELPERS
        // ============================================================

        internal static bool IsRaidPoc(
            Scene scene)
        {
            if (!scene.IsValid())
            {
                return false;
            }

            if (string.Equals(
                    scene.name,
                    RaidPocSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(
                    scene.path))
            {
                string fileName =
                    Path.GetFileNameWithoutExtension(
                        scene.path);

                return string.Equals(
                    fileName,
                    RaidPocSceneName,
                    StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }


        private static GameObject FindSceneObject(
            Scene scene,
            string objectName)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                string.IsNullOrEmpty(
                    objectName))
            {
                return null;
            }

            GameObject[] roots =
                scene.GetRootGameObjects();

            if (roots == null)
            {
                return null;
            }

            for (int rootIndex = 0;
                 rootIndex < roots.Length;
                 rootIndex++)
            {
                GameObject root =
                    roots[rootIndex];

                if (root == null)
                {
                    continue;
                }

                Transform[] transforms =
                    root.GetComponentsInChildren<Transform>(
                        true);

                if (transforms == null)
                {
                    continue;
                }

                for (int transformIndex = 0;
                     transformIndex < transforms.Length;
                     transformIndex++)
                {
                    Transform candidate =
                        transforms[transformIndex];

                    if (candidate != null &&
                        string.Equals(
                            candidate.name,
                            objectName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate.gameObject;
                    }
                }
            }

            return null;
        }


        private static string GetTransformPath(
            Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            string path =
                transform.name;

            Transform current =
                transform.parent;

            while (current != null)
            {
                path =
                    current.name +
                    "/" +
                    path;

                current =
                    current.parent;
            }

            return path;
        }


        private static void SetPlayerTransform(
            PlayerControl player,
            Vector3 position,
            Quaternion rotation)
        {
            if (player == null)
            {
                return;
            }

            player.transform.position =
                position;

            player.transform.rotation =
                rotation;
        }


        private static void LogSystemMessage(
            string message,
            string color)
        {
            try
            {
                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        message,
                        ChatLogLine.LogType.SystemMessages,
                        color ?? ""));
            }
            catch
            {
                // Native console logging remains available if the chat UI is
                // not yet ready during the scene transition.
            }
        }
    }
}
