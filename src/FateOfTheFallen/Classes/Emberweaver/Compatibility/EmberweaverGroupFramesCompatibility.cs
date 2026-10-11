using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Optional integration with Arcane's Group Frames.
    ///
    /// FOTF deliberately does not reference or bundle the Group Frames
    /// assembly. If the mod is present, this runtime Harmony patch extends its
    /// internal RaidClassColors.GetColor(string) resolver so the existing
    /// Group Frames installation recognizes Emberweaver without requiring a
    /// separate Group Frames update.
    /// </summary>
    internal static class EmberweaverGroupFramesCompatibility
    {
        private const string RaidClassColorsTypeName =
            "ArcanesGroupFrames.RaidClassColors";

        private const string GetColorMethodName =
            "GetColor";

        private const float ProbeIntervalSeconds =
            2f;

        private static bool _installed;
        private static bool _methodWarningLogged;
        private static float _nextProbeTime;
        private static MethodBase _patchedMethod;

        internal static void Initialize()
        {
            _nextProbeTime =
                0f;

            TryInstall();
        }

        internal static void Update()
        {
            if (_installed)
            {
                return;
            }

            float now =
                Time.realtimeSinceStartup;

            if (now < _nextProbeTime)
            {
                return;
            }

            _nextProbeTime =
                now + ProbeIntervalSeconds;

            TryInstall();
        }

        internal static void Shutdown()
        {
            /*
             * Plugin.OnDestroy() subsequently calls Harmony.UnpatchSelf().
             * Reset only our discovery state here so a Lunaris hot reload can
             * discover and patch Group Frames again.
             */
            _installed =
                false;

            _methodWarningLogged =
                false;

            _nextProbeTime =
                0f;

            _patchedMethod =
                null;
        }

        private static void TryInstall()
        {
            if (_installed)
            {
                return;
            }

            Type raidClassColorsType =
                AccessTools.TypeByName(
                    RaidClassColorsTypeName);

            /*
             * Group Frames is optional. Its type simply will not exist when
             * the user does not have the mod loaded.
             */
            if (raidClassColorsType == null)
            {
                return;
            }

            MethodInfo getColor =
                AccessTools.Method(
                    raidClassColorsType,
                    GetColorMethodName,
                    new Type[]
                    {
                        typeof(string)
                    });

            if (getColor == null)
            {
                if (!_methodWarningLogged)
                {
                    _methodWarningLogged =
                        true;

                    Plugin.LogWarning(
                        "Emberweaver: Arcane's Group Frames was detected, " +
                        "but RaidClassColors.GetColor(string) could not be found.");
                }

                return;
            }

            try
            {
                Harmony harmony =
                    new Harmony(
                        Plugin.PluginGuid);

                HarmonyMethod prefix =
                    new HarmonyMethod(
                        typeof(EmberweaverGroupFramesCompatibility),
                        nameof(GetColorPrefix));

                harmony.Patch(
                    getColor,
                    prefix:
                        prefix);

                _patchedMethod =
                    getColor;

                _installed =
                    true;

                Plugin.LogInfo(
                    "Emberweaver: Arcane's Group Frames class-colour integration enabled.");
            }
            catch (Exception exception)
            {
                Plugin.LogWarning(
                    "Emberweaver: failed to install Arcane's Group Frames " +
                    "class-colour integration: " +
                    exception.Message);
            }
        }

        /*
         * Prefix only intercepts Emberweaver. Every native class and every
         * other custom class continues through Group Frames' own resolver.
         */
        private static bool GetColorPrefix(
            string className,
            ref Color __result)
        {
            if (!string.Equals(
                    className == null
                        ? string.Empty
                        : className.Trim(),
                    EmberweaverConstants.DisplayName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            __result =
                EmberweaverClassIcon.ClassColor;

            return false;
        }
    }
}
