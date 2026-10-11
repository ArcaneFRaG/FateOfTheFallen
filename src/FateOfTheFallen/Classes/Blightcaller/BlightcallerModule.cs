using System;

namespace FateOfTheFallen
{
    /// <summary>
    /// Lifecycle boundary for the Blightcaller child module.
    /// The parent plugin talks to this class instead of knowing about
    /// individual Blightcaller services.
    /// </summary>
    internal static class BlightcallerModule
    {
        internal static void Initialize()
        {
            BlightcallerClassIcon.Initialize();

            /*
             * IMPORTANT FOR LUNARIS HOT RELOAD:
             *
             * On a clean game launch SpellDB.Start / ItemDatabase.Start will
             * run after the plugin is installed and the normal Harmony
             * registration patches build Blightcaller spells, scrolls and
             * auras.
             *
             * On a Lunaris unload/reload those native Start methods have
             * already happened. The shared CustomItemRegistry is deliberately
             * cleared during Plugin.OnDestroy(), so relying only on Start
             * patches leaves the reloaded vendor with an empty custom-item
             * registry.
             *
             * Rehydrate immediately from the already-live native databases
             * before any vendor is allowed to spawn.
             */
            EnsureRuntimeContentReady(
                "BlightcallerModule.Initialize");

            if (FOTFContentOptions.Active.ClassVendors) BlightcallerVendor.Install();
            if (FOTFContentOptions.Active.ClassVendors) BlightcallerSceneLoad.Install();
        }


        /// <summary>
        /// Rebuild/rebind Blightcaller runtime content against native
        /// databases that are already alive. Safe to call repeatedly.
        ///
        /// This is intentionally public to the child module so vendor spawn
        /// coroutines can use it as a final race-condition guard.
        /// </summary>
        internal static bool EnsureRuntimeContentReady(
            string source)
        {
            try
            {
                BlightcallerCatalog.EnsureClass();

                SpellDB spellDatabase =
                    GameData.SpellDatabase;

                if (spellDatabase != null &&
                    spellDatabase.SpellDatabase != null)
                {
                    BlightcallerCatalog.RegisterSpells(
                        spellDatabase);

                    BlightcallerSimPlayers
                        .ConfigureSpellsForSimPlayers();

                    BlightcallerAuras.Register(
                        spellDatabase);

                    BlightcallerAuraPersistence
                        .TryRestoreFromSave(
                            source ??
                            "Blightcaller runtime rehydrate");
                }

                if (GameData.ItemDB != null &&
                    GameData.ItemDB.ItemDB != null)
                {
                    BlightcallerEquipment
                        .RegisterArcanistEquipment();

                    /*
                     * Register() is deliberately re-entrant. Besides creating
                     * missing scroll objects, it re-adds surviving objects to
                     * CustomItemRegistry after a hot unload/reload.
                     */
                    BlightcallerScrolls.Register();
                }

                return HasVendorContent();
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Blightcaller: runtime content rehydration failed" +
                    (string.IsNullOrEmpty(source)
                        ? string.Empty
                        : " from [" + source + "]") +
                    ": " +
                    exception);

                return false;
            }
        }


        private static bool HasVendorContent()
        {
            if (!BlightcallerScrolls.IsRegistered)
            {
                return false;
            }

            /*
             * These are the four Aura items Kaelith/other Blightcaller
             * vendors currently reference. Requiring all four prevents the
             * vendor from spawning partially populated during a database
             * lifecycle race.
             */
            return
                BlightcallerAuras.GetItem(
                    "withering") != null &&
                BlightcallerAuras.GetItem(
                    "pestilent_essence") != null &&
                BlightcallerAuras.GetItem(
                    "festering_power") != null &&
                BlightcallerAuras.GetItem(
                    "blighted_covenant") != null;
        }


        internal static void Shutdown()
        {
            try
            {
                /*
                 * Restore every FOTF-assigned Blightcaller SimPlayer back to
                 * the native class it had before assignment.
                 *
                 * Plugin.OnDestroy() calls this module shutdown before Harmony
                 * is removed, so BlightcallerSimPlayers temporarily enters a
                 * restoration mode that prevents categorization/load patches
                 * from immediately reapplying the custom class.
                 */
                BlightcallerSimPlayers
                    .RestoreNativeClassesOnShutdown();
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Failed to restore Blightcaller SimPlayers to their native classes",
                    exception);
            }

            try
            {
                BlightcallerSceneLoad.Uninstall();
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Failed to uninstall BlightcallerSceneLoad",
                    exception);
            }

            try
            {
                BlightcallerVendor.Uninstall();
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Failed to uninstall BlightcallerVendor",
                    exception);
            }
        }
    }
}
