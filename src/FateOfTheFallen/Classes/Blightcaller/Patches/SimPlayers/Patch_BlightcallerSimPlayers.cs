using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    // ============================================================
    // POPULATION ASSIGNMENT + SERVER ROLE CATEGORIZATION
    // ============================================================

    [HarmonyPatch(
        typeof(SimPlayerMngr),
        "CategorizeAll")]
    internal static class Patch_BlightcallerSimPlayerCategorization
    {
        private static void Prefix(
            SimPlayerMngr __instance)
        {
            try
            {
                if (__instance == null ||
                    __instance.Sims == null)
                {
                    return;
                }

                /*
                 * Assign persistent Blightcaller SimPlayers before
                 * native role categorization occurs.
                 */
                BlightcallerSimPlayers
                    .EnsurePopulation(
                        __instance.Sims);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller SimPlayers: population assignment failed: " +
                    exception);
            }
        }


        private static void Postfix(
            SimPlayerMngr __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                /*
                 * Native CategorizeAll() only knows the six native
                 * class-name strings.
                 *
                 * Append Blightcaller Sims to the manager DPS pool.
                 */
                BlightcallerSimPlayers
                    .AddManagerDpsRoles(
                        __instance);


                /*
                 * A Sim may already exist in the world when its
                 * persistent Blightcaller assignment is restored.
                 *
                 * Reapply the actual runtime class and rebuild its
                 * skills/spells where necessary.
                 */
                BlightcallerSimPlayers
                    .ReapplyActiveSimPlayers(
                        __instance);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller SimPlayers: post-categorization reconciliation failed: " +
                    exception);
            }
        }
    }


    // ============================================================
    // RUNTIME CLASS RESTORATION
    // ============================================================

    [HarmonyPatch(
        typeof(SimPlayer),
        "LoadSimData")]
    internal static class Patch_BlightcallerSimPlayerClassLoad
    {
        private static void Postfix(
            SimPlayer __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                /*
                 * Native SimPlayerSaveData contains only the native
                 * class Boolean flags.
                 *
                 * Blightcaller therefore loads through the Arcanist
                 * fallback and is restored here to the real custom
                 * Class ScriptableObject.
                 */
                BlightcallerSimPlayers
                    .ApplyRuntimeClass(
                        __instance);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller SimPlayers: runtime class restoration failed: " +
                    exception);
            }
        }
    }


    // ============================================================
    // BLIGHTCALLER SHARED SKILLS
    // ============================================================

    [HarmonyPatch(
        typeof(SimPlayer),
        "LoadSimSkills")]
    internal static class Patch_BlightcallerSimPlayerSkillLoad
    {
        private static void Postfix(
            SimPlayer __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                /*
                 * Native LoadSimSkills() hardcodes the six native
                 * classes, so append Blightcaller's shared skills
                 * after native processing.
                 */
                BlightcallerSimPlayers
                    .AddBlightcallerSkills(
                        __instance);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller SimPlayers: skill injection failed: " +
                    exception);
            }
        }
    }


    // ============================================================
    // BLIGHTCALLER SIMPLAYER ASCENSION SELECTION
    // ============================================================
    //
    // Native Stats.FindNextAscensions() knows only the six native
    // classes.
    //
    // Blightcaller therefore requires its own class-specific
    // Ascension-selection path.
    // ============================================================

    [HarmonyPatch(
        typeof(Stats),
        "SimPlayerChooseAscension")]
    internal static class Patch_BlightcallerSimPlayerAscensionSelection
    {
        private static bool Prefix(
            Stats __instance)
        {
            try
            {
                if (__instance == null ||
                    __instance.Myself == null ||
                    __instance.Myself.MySkills == null)
                {
                    return true;
                }


                // ====================================================
                // SIMPLAYER ONLY
                // ====================================================

                NPC npc =
                    __instance.Myself.MyNPC;

                if (npc == null ||
                    !npc.SimPlayer)
                {
                    return true;
                }


                // ====================================================
                // BLIGHTCALLER ONLY
                // ====================================================

                if (!BlightcallerCatalog
                        .IsBlightcallerClass(
                            __instance.CharacterClass))
                {
                    return true;
                }


                // ====================================================
                // CUSTOM ASCENSION SELECTION
                // ====================================================
                //
                // Handle Blightcaller Ascension selection ourselves.
                //
                // Returning false prevents native Erenshor from
                // falling through to General-only Ascensions because
                // "Blightcaller" is not one of its hardcoded classes.
                // ====================================================

                BlightcallerSimPlayers
                    .ChooseBlightcallerAscension(
                        __instance);

                return false;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller SimPlayers: Ascension selection patch failed: " +
                    exception);

                /*
                 * Fail open.
                 *
                 * Native behaviour is preferable to blocking the
                 * SimPlayer from spending an Ascension point entirely.
                 */
                return true;
            }
        }
    }


    // ============================================================
    // BLIGHTCALLER SIMPLAYER ASCENSION INSPECTION UI
    // ============================================================
    //
    // Native SimInspect.DoAscensionsView() displays:
    //
    // - General Ascensions
    // - Duelist Ascensions
    // - Paladin Ascensions
    // - Arcanist Ascensions
    // - Druid Ascensions
    // - Stormcaller Ascensions
    // - Reaver Ascensions
    //
    // There is no Blightcaller class branch.
    //
    // Native processing is allowed to populate the General
    // Ascensions first.
    //
    // The postfix then appends the Blightcaller-specific Ascensions
    // using the inspected SimPlayer's real UseSkill ranks.
    // ============================================================

    [HarmonyPatch(
        typeof(SimInspect),
        "DoAscensionsView")]
    internal static class Patch_BlightcallerSimPlayerAscensionInspection
    {
        private static void Postfix(
            SimInspect __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                BlightcallerSimPlayers
                    .PopulateAscensionInspection(
                        __instance);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller SimPlayers: Ascension inspection UI patch failed: " +
                    exception);
            }
        }
    }


    // ============================================================
    // NATIVE SIM SAVE FALLBACK
    // ============================================================

    [HarmonyPatch(
        typeof(SimPlayerDataManager),
        "SaveSimData")]
    internal static class Patch_BlightcallerSimPlayerSave
    {
        private static void Prefix(
            SimPlayerSaveData _data)
        {
            try
            {
                if (_data == null)
                {
                    return;
                }

                /*
                 * Native SimPlayerSaveData has no custom-class field.
                 *
                 * Save Blightcaller Sims as Arcanists so Erenshor
                 * receives a completely valid native save.
                 *
                 * The sidecar stores the actual Blightcaller
                 * assignment and LoadSimData restores it.
                 */
                BlightcallerSimPlayers
                    .ApplyNativeFallback(
                        _data);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller SimPlayers: native fallback save patch failed: " +
                    exception);
            }
        }
    }


    // ============================================================
    // DELETED SIM CLEANUP
    // ============================================================

    [HarmonyPatch(
        typeof(SimPlayerDataManager),
        "DeleteSimSaveData")]
    internal static class Patch_BlightcallerSimPlayerDelete
    {
        private static void Postfix(
            string npcName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(
                        npcName))
                {
                    return;
                }

                /*
                 * If Erenshor deletes the SimPlayer entirely,
                 * remove its custom-class sidecar assignment too.
                 */
                BlightcallerSimPlayers
                    .RemoveAssignment(
                        npcName);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller SimPlayers: assignment cleanup failed: " +
                    exception);
            }
        }
    }


    // ============================================================
    // ACTIVE GROUP DPS ROLE
    // ============================================================

    [HarmonyPatch(
        typeof(SimPlayerGrouping),
        "SetRoles")]
    internal static class Patch_BlightcallerSimPlayerGroupingRoles
    {
        private static void Postfix(
            SimPlayerGrouping __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                /*
                 * SimPlayerGrouping maintains a second role system
                 * separate from SimPlayerMngr.CategorizeAll().
                 *
                 * Native SetRoles() recognizes only native Class
                 * ScriptableObjects, so append Blightcaller members
                 * to the active DPS list.
                 */
                BlightcallerSimPlayers
                    .AddGroupingDpsRoles(
                        __instance);
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller SimPlayers: active-group DPS role patch failed: " +
                    exception);
            }
        }
    }
}