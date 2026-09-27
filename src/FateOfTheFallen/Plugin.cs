using System;
using HarmonyLib;
using Lunaris;

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
            "0.1.25";

        private Harmony _harmony;

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

            internal static void Info(
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

        private void Awake()
        {
            NativeLog =
                Logging;

            ModLog.Loading(
                "============================================================");

            ModLog.Loading(
                PluginName +
                " " +
                PluginVersion);

            ModLog.Loading(
                "Loading...");

            try
            {
                FateContentModule.Initialize();

                _harmony =
                    new Harmony(
                        PluginGuid);

                _harmony.PatchAll();

                BlightcallerModule.Initialize();

                ModLog.Loading(
                    "Load complete.");

                ModLog.Loading(
                    "============================================================");
            }
            catch (Exception exception)
            {
                ModLog.Error(
                    "Failed to load " +
                    PluginName,
                    exception);
            }
        }

        private void OnDestroy()
        {
            /*
             * Remove all static Unity event subscriptions before
             * releasing the Lunaris logger.
             *
             * This is important for Lunaris hot unload/reload.
             */
            BlightcallerModule.Shutdown();

            try
            {
                CoroutineHost.Shutdown();
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