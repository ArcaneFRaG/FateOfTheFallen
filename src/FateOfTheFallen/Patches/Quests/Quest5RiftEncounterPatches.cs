using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Hooks the Quest 5 encounter to the already-proven FlameRiftRuntime
    /// lifecycle, and prevents the shard being harvested before the encounter
    /// is defeated.
    ///
    /// This is additive: it does not replace FlameRiftRuntime.cs or its world
    /// coordinates.
    /// </summary>

    [HarmonyPatch(
        typeof(FlameRiftRuntime),
        "Initialize")]
    internal static class Patch_Q5Encounter_RiftInitialize
    {
        private static void Postfix()
        {
            RiftFiendEncounterRuntime.Initialize();
        }
    }


    [HarmonyPatch(
        typeof(FlameRiftRuntime),
        "Shutdown")]
    internal static class Patch_Q5Encounter_RiftShutdown
    {
        private static void Prefix()
        {
            RiftFiendEncounterRuntime.Shutdown();
        }
    }


    [HarmonyPatch(
        typeof(FlameRiftShardInteractable),
        "TryHarvest")]
    internal static class Patch_Q5Encounter_BlockShardUntilClear
    {
        private static bool Prefix()
        {
            if (!FOTFExpansionGate.IsUnlocked ||
                !AshBeneathEarthQuest.IsActive() ||
                AshBeneathEarthQuest.IsComplete() ||
                AshBeneathEarthContent.PlayerHasShard())
            {
                return true;
            }

            if (!string.Equals(
                    UnityEngine.SceneManagement.SceneManager
                        .GetActiveScene()
                        .name,
                    FlameRiftRuntime.SceneName,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (RiftFiendEncounterRuntime.IsEncounterCleared)
            {
                return true;
            }

            try
            {
                UpdateSocialLog.LogAdd(
                    new ChatLogLine(
                        "The shard is still bound to the fiends surrounding the rift.",
                        ChatLogLine.LogType.SystemMessages,
                        "yellow"));
            }
            catch
            {
            }

            return false;
        }
    }


    /// <summary>
    /// PlaneOfBrax is loaded only as an auxiliary additive source scene while
    /// Quest 5 is acquiring the Lava Fiend template. Native NPCs in that
    /// non-active scene can start their navigation coroutine before the raid
    /// scene has a complete active-zone navigation context.
    ///
    /// Suppress UpdateNav only for those temporary source-scene NPCs.
    /// Quest 5 mobs are moved into SaltedStrand and continue using Erenshor's
    /// native navigation normally.
    /// </summary>
    [HarmonyPatch(
        typeof(NPC),
        "UpdateNav")]
    internal static class Patch_Q5Encounter_SuppressAuxiliaryBraxNavigation
    {
        private static bool Prefix(
            NPC __instance)
        {
            return
                !RiftFiendEncounterRuntime
                    .ShouldSuppressAuxiliaryPlaneNpcNavigation(
                        __instance);
        }
    }



    /// <summary>
    /// The additive PlaneOfBrax preload is a source-data operation, not a real
    /// zone transition. Character.Start() on those temporary source-scene
    /// characters can access gameplay state that only exists for the active
    /// SaltedStrand scene.
    ///
    /// Skip Start only for PlaneOfBrax Character components while the automatic
    /// Quest 5 preload is active. Quest 5 encounter clones are in SaltedStrand,
    /// so they receive normal Character.Start().
    /// </summary>
    [HarmonyPatch(
        typeof(Character),
        "Start")]
    internal static class Patch_Q5Encounter_SuppressAuxiliaryBraxCharacterStart
    {
        private static bool Prefix(
            Character __instance)
        {
            return
                !RiftFiendEncounterRuntime
                    .ShouldSuppressAuxiliaryPlaneCharacterStart(
                        __instance);
        }
    }

}
