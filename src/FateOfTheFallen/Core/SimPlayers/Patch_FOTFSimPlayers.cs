using HarmonyLib;
using System;
using System.Collections.Generic;

namespace FateOfTheFallen
{
    /// <summary>
    /// Erenshor calls LoadGuildData inside CreateGenericSimPlayers(), before
    /// CategorizeAll(). Inject the authored Sims before the guild roster
    /// snapshot is built, not only during the later DPS categorization.
    /// </summary>
    [HarmonyPatch(
        typeof(GuildManager),
        "LoadGuildData")]
    internal static class Patch_FOTFSimPlayerBeforeGuildLoad
    {
        private static void Prefix()
        {
            try
            {
                if (GameData.SimMngr != null)
                {
                    FOTFSimPlayerRoster.EnsurePopulation(
                        GameData.SimMngr);
                }
            }
            catch (Exception exception)
            {
                Plugin.LogError(
                    "FOTF SimPlayers: pre-guild roster reconciliation failed: " +
                    exception);
            }
        }
    }

    [HarmonyPatch(
        typeof(SimPlayerMngr),
        "CategorizeAll")]
    internal static class Patch_FOTFSimPlayerPopulation
    {
        private static void Prefix(
            SimPlayerMngr __instance)
        {
            try
            {
                FOTFSimPlayerRoster.EnsurePopulation(
                    __instance);
            }
            catch (Exception exception)
            {
                Plugin.LogError(
                    "FOTF SimPlayers: authored population injection failed: " +
                    exception);
            }
        }

        private static void Postfix(
            SimPlayerMngr __instance)
        {
            try
            {
                FOTFSimPlayerRoster.AddManagerDpsRoles(
                    __instance);
                FOTFSimPlayerRoster.AddManagerHealerRoles(__instance);

                FOTFSimPlayerRoster.ReapplyActiveSimPlayers(
                    __instance);
            }
            catch (Exception exception)
            {
                Plugin.LogError(
                    "FOTF SimPlayers: post-categorization reconciliation failed: " +
                    exception);
            }
        }
    }

    [HarmonyPatch(
        typeof(SimPlayer),
        "LoadSimData")]
    internal static class Patch_FOTFSimPlayerRuntimeClass
    {
        private static void Postfix(
            SimPlayer __instance)
        {
            try
            {
                FOTFSimPlayerRoster.ApplyRuntimeClass(
                    __instance);
            }
            catch (Exception exception)
            {
                Plugin.LogError(
                    "FOTF SimPlayers: runtime class restore failed: " +
                    exception);
            }
        }
    }

    [HarmonyPatch(
        typeof(SimPlayer),
        "LoadSimSkills")]
    internal static class Patch_FOTFSimPlayerSkills
    {
        private static void Postfix(
            SimPlayer __instance)
        {
            try
            {
                FOTFSimPlayerDefinition definition;

                if (!FOTFSimPlayerRoster.TryGetDefinitionForSim(
                        __instance,
                        out definition) ||
                    definition == null)
                {
                    return;
                }

                if (string.Equals(
                        definition.ClassName,
                        FOTFSimPlayerRoster.EmberweaverClassName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    EmberweaverSimPlayers.AddSkills(
                        __instance);
                }
                else if (string.Equals(definition.ClassName,
                    FOTFSimPlayerRoster.LightbringerClassName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    LightbringerSimPlayers.EnsureSkills(__instance);
                }
            }
            catch (Exception exception)
            {
                Plugin.LogError(
                    "FOTF SimPlayers: custom skill load failed: " +
                    exception);
            }
        }
    }

    [HarmonyPatch(
        typeof(SimPlayer),
        "LoadSimSpells")]
    internal static class Patch_FOTFSimPlayerCombatInstall
    {
        private static void Postfix(
            SimPlayer __instance)
        {
            try
            {
                // The native loader may have skipped Emberweaver spells
                // during database re-registration. Reconcile first, then
                // hand the spell selection to the class combat controller.
                EmberweaverSimPlayers.EnsureSpellbook(
                    __instance);
                LightbringerSimPlayers.EnsureSpellbook(__instance);

                FOTFSimCombatBridge.InstallFor(
                    __instance);
            }
            catch (Exception exception)
            {
                Plugin.LogError(
                    "FOTF SimPlayers: custom combat controller installation failed: " +
                    exception);
            }
        }
    }


    [HarmonyPatch(
        typeof(SimPlayerDataManager),
        "GetMyData")]
    internal static class Patch_FOTFSimPlayerOwnedLoad
    {
        private static bool Prefix(
            string _name,
            ref SimPlayerSaveData __result)
        {
            if (!FOTFSimPlayerRoster.IsOwnedName(
                    _name))
            {
                return true;
            }

            __result =
                FOTFSimPlayerRoster.GetOwnedSave(
                    _name);

            return false;
        }
    }

    [HarmonyPatch(
        typeof(SimPlayerDataManager),
        "CheckLoadData")]
    internal static class Patch_FOTFSimPlayerOwnedCheckLoad
    {
        private static bool Prefix(
            string _name,
            List<Item> _inv,
            int _level,
            float _skill,
            ref SimPlayerSaveData __result)
        {
            if (!FOTFSimPlayerRoster.IsOwnedName(
                    _name))
            {
                return true;
            }

            __result =
                FOTFSimPlayerRoster.GetOwnedSave(
                    _name);

            return false;
        }
    }

    [HarmonyPatch(
        typeof(SimPlayerDataManager),
        "SaveSimData")]
    internal static class Patch_FOTFSimPlayerOwnedSave
    {
        private static bool Prefix(
            SimPlayerSaveData _data)
        {
            try
            {
                if (_data == null ||
                    !FOTFSimPlayerRoster.IsOwnedName(
                        _data.NPCName))
                {
                    return true;
                }

                FOTFSimPlayerRoster.SaveOwnedData(
                    _data);

                // Prevent authored FOTF Sims from being written into
                // ESSaveData/Sims*. Otherwise Erenshor would count them as
                // native server population on the next launch.
                return false;
            }
            catch (Exception exception)
            {
                Plugin.LogError(
                    "FOTF SimPlayers: owned save interception failed: " +
                    exception);

                return false;
            }
        }
    }

    [HarmonyPatch(
        typeof(SimPlayerGrouping),
        "SetRoles")]
    internal static class Patch_FOTFSimPlayerGroupingRoles
    {
        private static void Postfix(
            SimPlayerGrouping __instance)
        {
            try
            {
                FOTFSimPlayerRoster.AddGroupingDpsRoles(
                    __instance);
                FOTFSimPlayerRoster.AddGroupingHealerRoles(__instance);
            }
            catch (Exception exception)
            {
                Plugin.LogError(
                    "FOTF SimPlayers: active-group role integration failed: " +
                    exception);
            }
        }
    }
}
