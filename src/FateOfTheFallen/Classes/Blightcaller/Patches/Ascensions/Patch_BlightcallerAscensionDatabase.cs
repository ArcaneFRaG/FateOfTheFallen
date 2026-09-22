using HarmonyLib;

namespace FateOfTheFallen
{
    [HarmonyPatch(
        typeof(SkillDB),
        "Start")]
    internal static class Patch_BlightcallerAscensionDatabase
    {
        private static void Postfix()
        {
            BlightcallerAscensions.Register();
        }
    }
}