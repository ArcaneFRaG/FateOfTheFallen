using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    [HarmonyPatch(
        typeof(AAScreen),
        "LoadAllAAButtonData")]
    internal static class Patch_EmberweaverAscensionClassTab
    {
        private static void Postfix(
            AAScreen __instance,
            bool ___classSpecific)
        {
            if (!___classSpecific ||
                __instance == null ||
                __instance.AllAASlots == null ||
                !EmberweaverAscensions.IsEmberweaverPlayer())
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

            int count =
                Mathf.Min(
                    __instance.AllAASlots.Count,
                    EmberweaverAscensions.All.Count);

            for (int i = 0;
                 i < count;
                 i++)
            {
                Ascension ascension =
                    EmberweaverAscensions.All[i];

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
                    EmberweaverAscensions
                        .GetRank(
                            skills,
                            ascension.Id)
                        .ToString();
            }
        }
    }
}
