using HarmonyLib;

namespace FateOfTheFallen
{
    [HarmonyPatch(
        typeof(SkillDB),
        "Start")]
    internal static class Patch_EmberweaverAscensionDatabase
    {
        private static void Postfix()
        {
            EmberweaverAscensions.Register();
        }
    }
}
