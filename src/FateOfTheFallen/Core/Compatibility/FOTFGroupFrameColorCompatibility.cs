using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Optional bridge for BOTH separately distributed UI assemblies.
    ///
    /// ArcaneUI and Arcane's Group Frames intentionally contain the same
    /// namespace + class name (ArcanesGroupFrames.RaidClassColors). A global
    /// TypeByName lookup may return only one; discover the method in each
    /// specific assembly instead. No static DLL dependency on either UI mod.
    /// </summary>
    internal static class FOTFGroupFrameColorCompatibility
    {
        private const string GroupFramesAssembly = "Arcane's Group Frames";
        private const string ArcaneUIAssembly = "ArcaneUI";
        private const string ColorResolverType = "ArcanesGroupFrames.RaidClassColors";
        private const float ProbeInterval = 2f;

        private static readonly HashSet<MethodBase> PatchedMethods =
            new HashSet<MethodBase>();

        private static readonly HashSet<string> WarnedAssemblies =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static float _nextProbe;

        internal static void Initialize()
        {
            PatchedMethods.Clear();
            WarnedAssemblies.Clear();
            _nextProbe = 0f;
            TryInstall();
        }

        internal static void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _nextProbe)
            {
                return;
            }

            _nextProbe = now + ProbeInterval;
            TryInstall();
        }

        internal static void Shutdown()
        {
            // Plugin.OnDestroy removes our Harmony patches via UnpatchSelf.
            // Reset our own cache so a subsequent Lunaris reload can reapply.
            PatchedMethods.Clear();
            WarnedAssemblies.Clear();
            _nextProbe = 0f;
        }

        private static void TryInstall()
        {
            Assembly[] assemblies;
            try
            {
                assemblies = AppDomain.CurrentDomain.GetAssemblies();
            }
            catch (Exception exception)
            {
                Plugin.LogWarning("[FOTF/UIClassColors] Cannot enumerate assemblies: " + exception.Message);
                return;
            }

            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                {
                    continue;
                }

                string assemblyName;
                try
                {
                    assemblyName = assembly.GetName().Name;
                }
                catch
                {
                    continue;
                }

                bool isArcaneUI = string.Equals(
                    assemblyName, ArcaneUIAssembly, StringComparison.OrdinalIgnoreCase);

                // MSBuild may surface the XML-escaped assembly name in some
                // setups. Match both forms but NEVER every assembly that has
                // the shared ArcanesGroupFrames namespace.
                bool isGroupFrames = string.Equals(
                    assemblyName, GroupFramesAssembly, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                    assemblyName, "Arcane%27s Group Frames", StringComparison.OrdinalIgnoreCase);

                if (!isArcaneUI && !isGroupFrames)
                {
                    continue;
                }

                string label = isArcaneUI ? "ArcaneUI" : "Arcane's Group Frames";
                Type resolver;
                try
                {
                    resolver = assembly.GetType(ColorResolverType, false);
                }
                catch (Exception exception)
                {
                    WarnOnce(assemblyName, label + " failed to resolve class colors: " + exception.Message);
                    continue;
                }

                if (resolver == null)
                {
                    WarnOnce(assemblyName, label + " does not contain " + ColorResolverType);
                    continue;
                }

                MethodInfo getColor = resolver.GetMethod(
                    "GetColor",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(string) },
                    null);

                if (getColor == null || getColor.ReturnType != typeof(Color))
                {
                    WarnOnce(assemblyName, label + " class color signature differs from GetColor(string): Color");
                    continue;
                }

                if (PatchedMethods.Contains(getColor))
                {
                    continue;
                }

                try
                {
                    Harmony harmony = new Harmony(Plugin.PluginGuid);
                    harmony.Patch(
                        getColor,
                        prefix: new HarmonyMethod(
                            typeof(FOTFGroupFrameColorCompatibility),
                            nameof(GetColorPrefix)));
                    PatchedMethods.Add(getColor);
                    Plugin.LogInfo("[FOTF/UIClassColors] Patched " + label +
                        " (" + assemblyName + "): Blightcaller + Emberweaver + Lightbringer.");
                }
                catch (Exception exception)
                {
                    WarnOnce(assemblyName, label + " patch failed: " + exception.Message);
                }
            }
        }

        private static void WarnOnce(string key, string warning)
        {
            if (WarnedAssemblies.Add(key ?? "<unknown>"))
            {
                Plugin.LogWarning("[FOTF/UIClassColors] " + warning);
            }
        }

        // Only FOTF classes are intercepted; all native and other modded
        // classes continue through the target UI's own colour resolver.
        private static bool GetColorPrefix(string className, ref Color __result)
        {
            string name = className == null ? string.Empty : className.Trim();

            if (string.Equals(name, "Blightcaller", StringComparison.OrdinalIgnoreCase))
            {
                __result = BlightcallerClassIcon.GroupFrameColor;
                return false;
            }

            if (string.Equals(name, EmberweaverConstants.DisplayName,
                StringComparison.OrdinalIgnoreCase))
            {
                __result = EmberweaverClassIcon.ClassColor;
                return false;
            }

            if (string.Equals(name, LightbringerConstants.DisplayName,
                StringComparison.OrdinalIgnoreCase))
            {
                __result = LightbringerClassIcon.ClassColor;
                return false;
            }

            return true;
        }
    }
}
