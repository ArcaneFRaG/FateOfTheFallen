using System;

namespace FateOfTheFallen
{
    /// <summary>
    /// Lifecycle boundary for the Emberweaver child module.
    ///
    /// The class owns its content and runtime, while the parent Fate of the
    /// Fallen plugin owns Harmony and shared registries. Keeping this boundary
    /// means future FOTF class modules can follow the same pattern.
    /// </summary>
    internal static class EmberweaverModule
    {
        private static bool _initialized;

        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            EmberweaverClassIcon.Initialize();

            EmberweaverHudRuntime.Initialize();

            EmberweaverTeachingItems.RegisterLazyResolvers();

            EnsureRuntimeContentReady(
                "EmberweaverModule.Initialize");

            if (FOTFContentOptions.Active.ClassVendors) EmberweaverVendor.Install();
        }

        internal static bool EnsureRuntimeContentReady(
            string source)
        {
            try
            {
                EmberweaverTeachingItems.RegisterLazyResolvers();

                Class emberweaver =
                    EmberweaverCatalog.TryEnsureClass();

                if (emberweaver == null)
                {
                    return false;
                }

                if (GameData.SkillDatabase != null)
                {
                    RegisterSkillDatabase(
                        GameData.SkillDatabase);
                }

                if (GameData.SpellDatabase != null &&
                    GameData.SpellDatabase.SpellDatabase != null)
                {
                    RegisterSpellDatabase(
                        GameData.SpellDatabase);
                }

                if (GameData.ItemDB != null &&
                    GameData.ItemDB.ItemDB != null)
                {
                    RegisterItemDatabase(
                        GameData.ItemDB);
                }

                if (GameData.SpellDatabase != null &&
                    GameData.SpellDatabase.SpellDatabase != null)
                {
                    return
                        EmberweaverAuras.GetItem(EmberweaverAuras.KindlingKey) != null &&
                        EmberweaverAuras.GetItem(EmberweaverAuras.EmbersKey) != null &&
                        EmberweaverAuras.GetItem(EmberweaverAuras.FurnaceKey) != null &&
                        EmberweaverAuras.GetItem(EmberweaverAuras.IncandescenceKey) != null &&
                        EmberweaverAuras.GetItem(EmberweaverAuras.EternalFlameKey) != null;
                }

                return true;
            }
            catch (Exception exception)
            {
                Plugin.ModLog.Error(
                    "Emberweaver: runtime content rehydration failed" +
                    (string.IsNullOrEmpty(source)
                        ? string.Empty
                        : " from [" + source + "]"),
                    exception);

                return false;
            }
        }

        internal static void RegisterSpellDatabase(
            SpellDB database)
        {
            if (database == null)
            {
                return;
            }

            if (EmberweaverCatalog.TryEnsureClass() == null)
            {
                return;
            }

            EmberweaverSpellCatalog.Register(
                database);

            EmberweaverSimPlayers.ConfigureSpellsForSimPlayers();

            EmberweaverAuras.Register(
                database);

            EmberweaverAuraPersistence.TryRestoreFromSave(
                "Emberweaver SpellDB registration");

            if (GameData.ItemDB != null &&
                GameData.ItemDB.ItemDB != null)
            {
                EmberweaverTeachingItems.Register(
                    GameData.ItemDB);
            }
        }

        internal static void RegisterSkillDatabase(
            SkillDB database)
        {
            if (database == null)
            {
                return;
            }

            if (EmberweaverCatalog.TryEnsureClass() == null)
            {
                return;
            }

            EmberweaverSkillCatalog.Register(
                database);

            EmberweaverAscensions.Register();

            if (GameData.ItemDB != null &&
                GameData.ItemDB.ItemDB != null)
            {
                EmberweaverTeachingItems.Register(
                    GameData.ItemDB);
            }
        }

        internal static void RegisterItemDatabase(
            ItemDatabase database)
        {
            if (database == null)
            {
                return;
            }

            if (EmberweaverCatalog.TryEnsureClass() == null)
            {
                return;
            }

            ArcanistDerivedCompatibility
                .RegisterEquipmentCompatibility(
                    database);

            if (GameData.SpellDatabase != null &&
                GameData.SpellDatabase.SpellDatabase != null)
            {
                EmberweaverSpellCatalog.Register(
                    GameData.SpellDatabase);

                EmberweaverSimPlayers.ConfigureSpellsForSimPlayers();

                EmberweaverAuras.Register(
                    GameData.SpellDatabase);

                EmberweaverAuraPersistence.TryRestoreFromSave(
                    "Emberweaver ItemDB registration");
            }

            if (GameData.SkillDatabase != null)
            {
                EmberweaverSkillCatalog.Register(
                    GameData.SkillDatabase);

                EmberweaverAscensions.Register();
            }

            EmberweaverTeachingItems.Register(
                database);
        }

        internal static void UpdateRuntime()
        {
            EmberweaverHudRuntime.EnsureAlive();
            EmberweaverFlashfireCooldownRuntime.UpdateRuntime();
            EmberweaverPassiveRuntime.Update();
        }

        internal static void Shutdown()
        {
            EmberweaverVendor.Uninstall();
            EmberweaverHudRuntime.Shutdown();
            EmberweaverPassiveRuntime.Shutdown();
            _initialized = false;
        }
    }
}
