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
            BlightcallerVendor.Install();
            BlightcallerSceneLoad.Install();
        }

        internal static void Shutdown()
        {
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
