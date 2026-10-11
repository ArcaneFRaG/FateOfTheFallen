using System;
using System.Reflection;

namespace FateOfTheFallen
{
    /// <summary>
    /// Optional interoperability bridge for Arcane's Erenshor Combat Meter.
    ///
    /// The meter attributes Character.DamageMe() from a short-lived
    /// CombatContext keyed by the attacker. FOTF intentionally does not take a
    /// compile-time dependency on the meter; when it is present we discover its
    /// CombatContext.SetSource(Character,string) method via reflection and push
    /// a normal "Spell:<ability>" source immediately before mechanic damage.
    ///
    /// If the meter is not installed, mechanic damage continues normally.
    /// </summary>
    internal static class RaidMechanicDamageAttribution
    {
        private const string CombatMeterAssemblyHint =
            "ErenshorCombatMeter";

        private static MethodInfo _setSourceMethod;
        private static bool _loggedBridge;
        private static bool _loggedInvokeFailure;

        internal static void SetNamedSource(
            Character attacker,
            string mechanicName)
        {
            if (attacker == null ||
                string.IsNullOrEmpty(mechanicName))
            {
                return;
            }

            MethodInfo method =
                ResolveSetSourceMethod();

            if (method == null)
            {
                return;
            }

            try
            {
                method.Invoke(
                    null,
                    new object[]
                    {
                        attacker,
                        "Spell:" + mechanicName
                    });

                if (!_loggedBridge)
                {
                    _loggedBridge =
                        true;

                }
            }
            catch (Exception exception)
            {
                if (!_loggedInvokeFailure)
                {
                    _loggedInvokeFailure =
                        true;

                    Plugin.NativeLog.LogWarning(
                        "[RaidPoC] Combat Meter mechanic attribution bridge invocation failed; raid damage will still apply normally. " +
                        exception.GetType().Name +
                        ": " +
                        exception.Message);
                }

                _setSourceMethod =
                    null;
            }
        }

        private static MethodInfo ResolveSetSourceMethod()
        {
            if (_setSourceMethod != null)
            {
                return _setSourceMethod;
            }

            Assembly[] assemblies =
                AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0;
                 i < assemblies.Length;
                 i++)
            {
                Assembly assembly =
                    assemblies[i];

                string assemblyName =
                    assembly.GetName().Name ??
                    string.Empty;

                if (assemblyName.IndexOf(
                        CombatMeterAssemblyHint,
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                Type combatContext =
                    assembly.GetType(
                        "ErenshorCombatMeter.CombatContext",
                        false);

                if (combatContext == null)
                {
                    combatContext =
                        FindCombatContextType(
                            assembly);
                }

                if (combatContext == null)
                {
                    continue;
                }

                MethodInfo method =
                    combatContext.GetMethod(
                        "SetSource",
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic,
                        null,
                        new Type[]
                        {
                            typeof(Character),
                            typeof(string)
                        },
                        null);

                if (method != null)
                {
                    _setSourceMethod =
                        method;

                    return _setSourceMethod;
                }
            }

            return null;
        }

        private static Type FindCombatContextType(
            Assembly assembly)
        {
            try
            {
                Type[] types =
                    assembly.GetTypes();

                for (int i = 0;
                     i < types.Length;
                     i++)
                {
                    Type type =
                        types[i];

                    if (type != null &&
                        string.Equals(
                            type.Name,
                            "CombatContext",
                            StringComparison.Ordinal))
                    {
                        return type;
                    }
                }
            }
            catch (ReflectionTypeLoadException exception)
            {
                Type[] types =
                    exception.Types;

                if (types != null)
                {
                    for (int i = 0;
                         i < types.Length;
                         i++)
                    {
                        Type type =
                            types[i];

                        if (type != null &&
                            string.Equals(
                                type.Name,
                                "CombatContext",
                                StringComparison.Ordinal))
                        {
                            return type;
                        }
                    }
                }
            }
            catch
            {
                // Optional interop only. Failure must never affect raid combat.
            }

            return null;
        }
    }
}
