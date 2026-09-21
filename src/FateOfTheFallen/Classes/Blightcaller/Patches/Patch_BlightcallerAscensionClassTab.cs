using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    [HarmonyPatch(
        typeof(AAScreen),
        "LoadAllAAButtonData")]
    internal static class Patch_BlightcallerAscensionClassTab
    {
        private static void Postfix(
            AAScreen __instance,
            bool ___classSpecific)
        {
            if (!___classSpecific)
            {
                return;
            }

            if (__instance == null ||
                __instance.AllAASlots == null)
            {
                return;
            }

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

            int count =
                Mathf.Min(
                    __instance.AllAASlots.Count,
                    BlightcallerAscensions.All.Count);

            for (int i = 0; i < count; i++)
            {
                Ascension ascension =
                    BlightcallerAscensions.All[i];

                AAButton button =
                    __instance.AllAASlots[i];

                if (ascension == null ||
                    button == null)
                {
                    continue;
                }

                button.Name.text =
                    ascension.SkillName;

                button.SkillDescription =
                    ascension.SkillDesc;

                button.id =
                    ascension.Id;

                button.MaxLevel.text =
                    " / " +
                    ascension.MaxRank.ToString();

                button.Level.text =
                    BlightcallerAscensions
                        .GetRank(
                            skills,
                            ascension.Id)
                        .ToString();
            }
        }
    }
}