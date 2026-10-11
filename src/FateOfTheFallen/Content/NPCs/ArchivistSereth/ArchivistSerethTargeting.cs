using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace FateOfTheFallen
{
    internal static class ArchivistSerethTargeting
    {
        internal static bool IsSereth(
            Character character)
        {
            return
                character != null &&
                character.gameObject != null &&
                string.Equals(
                    character.gameObject.name,
                    ArchivistSerethDefinition.NpcName,
                    StringComparison.OrdinalIgnoreCase);
        }


        internal static void Target(
            Character character)
        {
            if (!IsSereth(
                    character))
            {
                return;
            }

            if (GameData.PlayerControl != null)
            {
                GameData.PlayerControl.CurrentTarget =
                    character;
            }

            if (character.TargetRing != null)
            {
                character.TargetRing.SetActive(
                    true);
            }
        }


        internal static void Untarget(
            Character character)
        {
            if (!IsSereth(
                    character))
            {
                return;
            }

            if (character.TargetRing != null)
            {
                character.TargetRing.SetActive(
                    false);
            }

            if (GameData.PlayerControl != null &&
                GameData.PlayerControl.CurrentTarget ==
                    character)
            {
                GameData.PlayerControl.CurrentTarget =
                    null;
            }
        }
    }


    [HarmonyPatch(
        typeof(Character),
        "TargetMe")]
    internal static class Patch_ArchivistSereth_TargetMe
    {
        [HarmonyPrefix]
        private static bool Prefix(
            Character __instance)
        {
            if (!ArchivistSerethTargeting.IsSereth(
                    __instance))
            {
                return true;
            }

            ArchivistSerethTargeting.Target(
                __instance);

            return false;
        }
    }


    [HarmonyPatch]
    internal static class Patch_ArchivistSereth_UntargetMe
    {
        private static IEnumerable<MethodBase>
            TargetMethods()
        {
            MethodInfo[] methods =
                typeof(Character).GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            for (int i = 0;
                 i < methods.Length;
                 i++)
            {
                MethodInfo method =
                    methods[i];

                if (method == null ||
                    !string.Equals(
                        method.Name,
                        "UntargetMe",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                yield return
                    method;
            }
        }


        [HarmonyPrefix]
        private static bool Prefix(
            Character __instance)
        {
            if (!ArchivistSerethTargeting.IsSereth(
                    __instance))
            {
                return true;
            }

            ArchivistSerethTargeting.Untarget(
                __instance);

            return false;
        }
    }
}
