using HarmonyLib;

namespace FateOfTheFallen
{
    /// <summary>
    /// The runtime-built FOTF world bosses intentionally use FOTF_NamePlate rather
    /// than Erenshor's serialized NamePlate(Clone). NPC.HandleNameTag assumes
    /// the complete native nameplate graph and otherwise throws every frame,
    /// aborting the rest of NPC.Update.
    /// </summary>
    [HarmonyPatch(
        typeof(NPC),
        "HandleNameTag")]
    internal static class Patch_WorldBossSkipNativeHandleNameTag
    {
        [HarmonyPrefix]
        private static bool Prefix(
            NPC __instance)
        {
            if (__instance == null)
            {
                return true;
            }

            Character character =
                __instance.GetChar();

            if (character == null)
            {
                character =
                    __instance.GetComponent<Character>();
            }

            return
                !WorldBossHitReactionUtility.IsCustomWorldBoss(
                    character);
        }
    }
}
