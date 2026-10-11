using System;
using HarmonyLib;
using Lunaris;
using Lunaris.Config;

namespace FateOfTheFallen
{
    [LunarisPlugin(
        PluginName,
        PluginVersion,
        "Arcane",
        "Fate of the Fallen framework and content mod for Erenshor")]
    public sealed class Plugin : LunarisPlugin
    {
        public const string PluginGuid =
            "com.fateofthefallen.erenshor";

        public const string PluginName =
            "Fate of the Fallen";

        public const string PluginVersion =
            "0.1.45";

        internal const string SourceBuildId =
            
            "20261011-r102-content-options-lightbringer-starter";

        private Harmony _harmony;
        private IConfigHandle<FOTFContentConfiguration> _contentOptions;

        internal static ILog NativeLog
        {
            get;
            private set;
        }

        internal static class ModLog
        {
            internal static void Loading(
                string message)
            {
                if (NativeLog == null)
                {
                    return;
                }

                NativeLog.LogInfo(
                    message);
            }

            internal static void Error(
                string message)
            {
                if (NativeLog == null)
                {
                    return;
                }

                NativeLog.LogError(
                    message);
            }

            internal static void Error(
                string message,
                Exception exception)
            {
                if (NativeLog == null)
                {
                    return;
                }

                if (exception == null)
                {
                    NativeLog.LogError(
                        message);

                    return;
                }

                NativeLog.LogError(
                    message +
                    ": " +
                    exception);
            }
        }

        internal static void LogInfo(
            string message)
        {
            ModLog.Loading(
                message);
        }

        internal static void LogWarning(
            string message)
        {
            if (NativeLog == null)
            {
                return;
            }

            NativeLog.LogWarning(
                message);
        }

        internal static void LogError(
            string message)
        {
            ModLog.Error(
                message);
        }

        private void Awake()
        {
            NativeLog =
                Logging;

            ModLog.Loading(
                PluginName +
                " " +
                PluginVersion +
                " loading...");

            LogLoadedAssemblyDiagnostics();

            // Lunaris renders the ConfigAttribute switches in the plugin options.
            // Capture once so menu edits never tear down active content mid-session.
            try
            {
                _contentOptions = Config.Register<FOTFContentConfiguration>();
                FOTFContentOptions.Capture(_contentOptions.Get());
                LogInfo("[FOTFOptions] Lunaris content settings loaded. Changes apply on next plugin/game load. " +
                    "Classes=[" + FOTFContentOptions.Active.NewBlightcallers + "," +
                    FOTFContentOptions.Active.NewEmberweavers + "," +
                    FOTFContentOptions.Active.NewLightbringers + "] Questline=" +
                    FOTFContentOptions.Active.Questline + " Bosses=" +
                    FOTFContentOptions.Active.WorldBosses);
            }
            catch (Exception exception)
            {
                FOTFContentOptions.Capture(null);
                LogWarning("[FOTFOptions] Lunaris settings unavailable; using existing all-enabled defaults: " + exception.Message);
            }

            try
            {
                FateContentModule.Initialize();

                _harmony =
                    new Harmony(
                        PluginGuid);

                _harmony.PatchAll();
                // Optional: the native spellbook can ignore SpellDesc.
                // Dynamically attach only if its hover method is available.
                LightbringerSpellbookTooltip.Install(_harmony);

                BlightcallerModule.Initialize();
                EmberweaverModule.Initialize();
                LightbringerCatalog.TryEnsureClass();
                if (FOTFContentOptions.Active.ClassVendors)
                    LightbringerVendor.Install();
                if (FOTFContentOptions.Active.NativeAbsorbIndicators)
                    LightbringerNativeAbsorbOverlay.Initialize();
                FOTFGroupFrameColorCompatibility.Initialize();
                if (FOTFContentOptions.Active.CustomSimPlayers)
                    FOTFSimPlayerRoster.Initialize();

                ModLog.Loading(
                    PluginName +
                    " " +
                    PluginVersion +
                    " loaded.");
            }
            catch (Exception exception)
            {
                ModLog.Error(
                    "Failed to load " +
                    PluginName,
                    exception);
            }
        }

        private static void LogLoadedAssemblyDiagnostics()
        {
            try
            {
                System.Reflection.Assembly current =
                    typeof(Plugin).Assembly;

                string currentLocation =
                    "<unknown>";

                try
                {
                    currentLocation =
                        current.Location;
                }
                catch
                {
                }

                ModLog.Loading(
                    "[FOTFBuild] SourceBuild=[" +
                    SourceBuildId +
                    "] LoadedFrom=[" +
                    currentLocation +
                    "].");

                int matchingAssemblies =
                    0;

                System.Reflection.Assembly[] loaded =
                    AppDomain.CurrentDomain.GetAssemblies();

                for (int i = 0;
                     loaded != null &&
                     i < loaded.Length;
                     i++)
                {
                    System.Reflection.Assembly assembly =
                        loaded[i];

                    if (assembly == null ||
                        !string.Equals(
                            assembly.GetName().Name,
                            "FateOfTheFallen",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    matchingAssemblies++;

                    string location =
                        "<unknown>";

                    try
                    {
                        location =
                            assembly.Location;
                    }
                    catch
                    {
                    }

                    ModLog.Loading(
                        "[FOTFBuild] MatchingAssembly=[" +
                        matchingAssemblies +
                        "] FullName=[" +
                        assembly.FullName +
                        "] Location=[" +
                        location +
                        "].");
                }

                if (matchingAssemblies > 1)
                {
                    LogWarning(
                        "[FOTFBuild] multiple FateOfTheFallen assemblies are loaded. Remove duplicate/old DLL copies before testing.");
                }
            }
            catch (Exception exception)
            {
                LogWarning(
                    "[FOTFBuild] assembly diagnostics failed: " +
                    exception.Message);
            }
        }


        private void Update()
        {
            EmberweaverModule.UpdateRuntime();
            FOTFGroupFrameColorCompatibility.Update();
            if (FOTFContentOptions.Active.NativeAbsorbIndicators)
                LightbringerNativeAbsorbOverlay.Tick();
        }

        private void OnDestroy()
        {
            /*
             * Remove all static Unity event subscriptions before
             * releasing the Lunaris logger.
             *
             * This is important for Lunaris hot unload/reload.
             */
            FOTFSimPlayerRoster.Shutdown();
            FOTFGroupFrameColorCompatibility.Shutdown();
            LightbringerNativeAbsorbOverlay.Shutdown();
            LightbringerVendor.Uninstall();
            EmberweaverModule.Shutdown();
            BlightcallerModule.Shutdown();
            FateContentModule.Shutdown();

            try
            {
                CoroutineHost.Shutdown();
                AssetBundleRegistry.Clear();
                EmbeddedAssetLoader.Clear();
                CustomItemRegistry.Clear();
            }
            catch (Exception exception)
            {
                ModLog.Error(
                    "Failed to release shared Fate of the Fallen runtime resources",
                    exception);
            }

            if (_harmony != null)
            {
                try
                {
                    _harmony.UnpatchSelf();
                }
                catch (Exception exception)
                {
                    ModLog.Error(
                        "Failed to unpatch Harmony patches",
                        exception);
                }

                _harmony =
                    null;
            }

            NativeLog =
                null;
        }
    }
}