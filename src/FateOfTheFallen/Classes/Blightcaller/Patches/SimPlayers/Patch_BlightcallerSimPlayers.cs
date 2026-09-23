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


                // ====================================================
                // ASSIGN BLIGHTCALLER SIMPLAYERS
                // ====================================================
                //
                // This executes before native role categorization.
                //
                // BlightcallerSimPlayers.EnsurePopulation() handles:
                //
                // - deterministic SimPlayer assignment
                // - persistent sidecar assignments
                // - restoring ClassName = "Blightcaller"
                // ====================================================

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


                // ====================================================
                // DPS ROLE
                // ====================================================
                //
                // Native CategorizeAll() only knows the six native
                // class-name strings.
                //
                // Add Blightcaller Sims to the global DPS pool after
                // native processing has completed.
                // ====================================================

                BlightcallerSimPlayers
                    .AddManagerDpsRoles(
                        __instance);


                // ====================================================
                // ACTIVE SIM RECONCILIATION
                // ====================================================
                //
                // A newly assigned Sim may already have been spawned
                // and loaded using its previous native class.
                //
                // Restore the runtime Blightcaller class and rebuild
                // its skills/spells if required.
                // ====================================================

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


                // ====================================================
                // RESTORE REAL CUSTOM CLASS
                // ====================================================
                //
                // Native SimPlayerSaveData contains only the six
                // original class Boolean flags.
                //
                // Blightcaller Sims therefore load natively as the
                // Arcanist fallback.
                //
                // This postfix replaces that fallback with the actual
                // Blightcaller Class ScriptableObject before native
                // LoadSimSkills() and LoadSimSpells() execute.
                // ====================================================

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


                // ====================================================
                // CUSTOM-CLASS SKILLS
                // ====================================================
                //
                // Native LoadSimSkills() hardcodes:
                //
                // Paladin
                // Arcanist
                // Duelist
                // Druid
                // Stormcaller
                // Reaver
                //
                // Blightcaller therefore needs its shared skills
                // appended after native processing.
                // ====================================================

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
    // Native Stats.FindNextAscensions() hardcodes only:
    //
    // - Duelist
    // - Paladin
    // - Arcanist
    // - Druid
    // - Stormcaller
    // - Reaver
    //
    // A runtime class named "Blightcaller" therefore cannot receive
    // its class-specific Ascensions through native selection.
    //
    // Intercept SimPlayerChooseAscension only for actual
    // Blightcaller SimPlayers.
    //
    // Every native class continues using completely native behaviour.
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
                // ChooseBlightcallerAscension() mirrors native
                // SimPlayer Ascension behaviour but substitutes the
                // Blightcaller class-specific Ascension pool.
                //
                // Returning false prevents native FindNextAscensions()
                // from seeing the unknown "Blightcaller" class name and
                // incorrectly falling back to General Ascensions.
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
                 * If our custom implementation unexpectedly fails,
                 * allow native Erenshor processing instead of preventing
                 * the SimPlayer from spending its Ascension point.
                 */
                return true;
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


                // ====================================================
                // SAVE BLIGHTCALLER AS NATIVE ARCANIST
                // ====================================================
                //
                // Native SimPlayerSaveData has no extensible custom
                // Class field.
                //
                // The persistent sidecar remembers that this Sim is
                // actually a Blightcaller.
                //
                // Native Erenshor receives a completely valid Arcanist
                // save and our LoadSimData postfix restores the custom
                // runtime class afterwards.
                // ====================================================

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


                // ====================================================
                // REMOVE PERSISTENT CUSTOM-CLASS ASSIGNMENT
                // ====================================================
                //
                // If Erenshor deletes the SimPlayer save entirely,
                // remove our sidecar assignment too.
                // ====================================================

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


                // ====================================================
                // ACTIVE PARTY ROLE
                // ====================================================
                //
                // SimPlayerGrouping maintains a separate runtime role
                // list from SimPlayerMngr.CategorizeAll().
                //
                // Native SetRoles() only recognizes native Class
                // ScriptableObjects, so append Blightcaller members
                // to the active DPS list.
                // ====================================================

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