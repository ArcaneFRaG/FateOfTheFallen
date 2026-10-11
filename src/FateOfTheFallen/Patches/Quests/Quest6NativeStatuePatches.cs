using HarmonyLib;

namespace FateOfTheFallen
{
    /// <summary>
    /// Reliquary is temporarily loaded additively only as a native static-asset
    /// source for Quest 6. These guards mirror the proven Quest 5 PlaneOfBrax
    /// preload safety fix.
    /// </summary>

    [HarmonyPatch(
        typeof(Character),
        "Start")]
    internal static class Patch_Q6Statues_SuppressAuxiliaryReliquaryCharacterStart
    {
        private static bool Prefix(
            Character __instance)
        {
            return
                !Quest6NativeStatueRuntime
                    .ShouldSuppressAuxiliaryReliquaryCharacterStart(
                        __instance);
        }
    }


    [HarmonyPatch(
        typeof(NPC),
        "UpdateNav")]
    internal static class Patch_Q6Statues_SuppressAuxiliaryReliquaryNavigation
    {
        private static bool Prefix(
            NPC __instance)
        {
            return
                !Quest6NativeStatueRuntime
                    .ShouldSuppressAuxiliaryReliquaryNpcNavigation(
                        __instance);
        }
    }
}
