using HarmonyLib;
using System;

namespace FateOfTheFallen
{
    [HarmonyPatch(
        typeof(SpellDB),
        "Start")]
    internal static class Patch_BlightcallerSpellDatabase
    {
        private static void Postfix(
            SpellDB __instance)
        {
            if (__instance == null)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: SpellDB instance was null.");

                return;
            }


            if (__instance.SpellDatabase == null)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: native SpellDatabase was null.");

                return;
            }


            try
            {
                BlightcallerCatalog.EnsureClass();


                // ====================================================
                // REGISTER BLIGHTCALLER SPELLS
                // ====================================================

                BlightcallerCatalog.RegisterSpells(
                    __instance);


                // ====================================================
                // CONFIGURE SPELLS FOR SIMPLAYERS
                // ====================================================
                //
                // Native SimPlayer.LoadSimSpells() requires:
                //
                // - Spell.UsedBy contains the runtime class
                // - Spell.SimUsable == true
                // - SimsNeedHelpToLearn is satisfied
                //
                // Hidden carrier effects stay non-castable.
                // ====================================================

                BlightcallerSimPlayers
                    .ConfigureSpellsForSimPlayers();


                // ====================================================
                // SCROLLS
                // ====================================================

                BlightcallerScrolls.Register();


                // ====================================================
                // AURAS
                // ====================================================

                BlightcallerAuras.Register(
                    __instance);


                // ====================================================
                // AURA SAVE RESTORATION
                // ====================================================

                BlightcallerAuraPersistence
                    .TryRestoreFromSave(
                        "SpellDB.Start");
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: failed during SpellDB initialization: " +
                    exception);
            }
        }
    }
}