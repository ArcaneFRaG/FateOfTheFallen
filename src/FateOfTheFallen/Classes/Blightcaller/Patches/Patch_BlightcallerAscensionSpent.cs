using System.Linq;
using HarmonyLib;

namespace FateOfTheFallen
{
    [HarmonyPatch(
        typeof(AAScreen),
        "CountSpent")]
    internal static class Patch_BlightcallerAscensionSpent
    {
        private static void Postfix(
            AAScreen __instance,
            ref int ___spentPoints)
        {
            if (!BlightcallerAscensions.IsBlightcallerPlayer())
            {
                return;
            }

            PlayerControl playerControl =
                GameData.PlayerControl;

            UseSkill skills = null;

            if (playerControl != null &&
                playerControl.Myself != null)
            {
                skills =
                    playerControl.Myself.MySkills;
            }

            ___spentPoints +=
                BlightcallerAscensions.All.Sum(
                    ascension =>
                        BlightcallerAscensions.GetRank(
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