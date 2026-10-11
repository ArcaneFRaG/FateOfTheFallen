using System.Linq;
using HarmonyLib;

namespace FateOfTheFallen
{
    [HarmonyPatch(
        typeof(AAScreen),
        "CountSpent")]
    internal static class Patch_EmberweaverAscensionSpent
    {
        private static void Postfix(
            AAScreen __instance,
            ref int ___spentPoints)
        {
            if (!EmberweaverAscensions.IsEmberweaverPlayer())
            {
                return;
            }

            PlayerControl playerControl =
                GameData.PlayerControl;

            UseSkill skills =
                playerControl != null &&
                playerControl.Myself != null
                    ? playerControl.Myself.MySkills
                    : null;

            ___spentPoints +=
                EmberweaverAscensions.All.Sum(
                    ascension =>
                        EmberweaverAscensions.GetRank(
                            skills,
                            ascension.Id));

            if (__instance != null &&
                __instance.spent != null)
            {
                __instance.spent.text =
                    "Points spent: " +
                    ___spentPoints.ToString();
            }
        }
    }
}
