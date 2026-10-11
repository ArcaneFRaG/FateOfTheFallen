using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Owns Lightbringer's canonical Class ScriptableObject.
    ///
    /// IMPORTANT:
    /// Lightbringer is a distinct Class object, copying the native Druid's
    /// gameplay, proficiencies and stat progression without modifying Druid.
    /// Paladin is retained ONLY as the r52 equipment-eligibility donor.
    /// We do not broadly inject Lightbringer into arbitrary
    /// ClassDB collections. This keeps Lightbringer isolated from Blightcaller
    /// and all other custom classes.
    /// </summary>
    internal static class LightbringerCatalog
    {
        private static Class _lightbringerClass;
        private static Class _druidClass;
        private static Class _paladinClass;

        internal static Class LightbringerClass
        {
            get { return _lightbringerClass; }
        }

        // Character attributes and level scaling come from the native Druid.
        internal static Class DruidClass
        {
            get
            {
                if (_druidClass == null ||
                    IsLightbringerClass(_druidClass))
                {
                    // Prefer the exact native ClassDB entry so our stat
                    // donor cannot be confused with another loaded asset.
                    _druidClass = GameData.ClassDB != null
                        ? GameData.ClassDB.Druid
                        : null;
                    if (_druidClass == null)
                        _druidClass = FindNativeClass(
                            LightbringerConstants.NativeTemplateClass);
                }

                return _druidClass;
            }
        }

        // Keep r52 equipment eligibility unchanged until healer gear is designed.
        // This MUST NOT use NativeTemplateClass: the stat and equipment donors differ.
        internal static Class PaladinClass
        {
            get
            {
                if (_paladinClass == null ||
                    IsLightbringerClass(_paladinClass))
                {
                    _paladinClass = GameData.ClassDB != null
                        ? GameData.ClassDB.Paladin
                        : null;
                    if (_paladinClass == null)
                        _paladinClass = FindNativeClass(
                            LightbringerConstants.NativeEquipmentClass);
                }

                return _paladinClass;
            }
        }

        internal static Class EnsureClass()
        {
            return EnsureClassInternal(true);
        }

        internal static Class TryEnsureClass()
        {
            return EnsureClassInternal(false);
        }

        private static Class EnsureClassInternal(bool logFailures)
        {
            if (_lightbringerClass != null)
            {
                return _lightbringerClass;
            }

            // Resolve the real Druid template before accepting an existing
            // Lightbringer instance. This also migrates any r52 Paladin-derived
            // instance still present in memory during class recovery.
            Class druid = DruidClass;
            if (druid == null)
            {
                if (logFailures)
                {
                    Plugin.LogError(
                        "Native Druid Class template was not found; " +
                        "Lightbringer scaling cannot be initialized.");
                }

                return null;
            }

            Class existing = FindExistingLightbringerClass();
            if (existing != null)
            {
                // r52 copied Paladin; replace its entire gameplay profile
                // with Druid data when recovering, keeping our unique identity.
                CopyNativeGameplayData(druid, existing);
                ApplyLightbringerIdentity(existing);
                _lightbringerClass = existing;
                LogDruidScaling(existing);
                return _lightbringerClass;
            }

            Class created =
                ScriptableObject.CreateInstance<Class>();

            if (created == null)
            {
                if (logFailures)
                {
                    Plugin.LogError(
                        "Failed to create independent Lightbringer Class object.");
                }

                return null;
            }

            // Druid is the *entire* native stat/proficiency/level-scaling
            // donor, not just a hand-tuned set of stat-benefit coefficients.
            // Mutable collections/arrays are duplicated so Lightbringer
            // cannot modify native Druid's collections through shared lists.
            CopyNativeGameplayData(
                druid,
                created);

            created.name =
                "FOTF_Lightbringer_Class";

            created.hideFlags =
                HideFlags.HideAndDontSave;

            ApplyLightbringerIdentity(
                created);

            _lightbringerClass = created;

            Plugin.LogInfo(
                "Created independent Lightbringer Class object from Druid gameplay data.");
            LogDruidScaling(created);

            return _lightbringerClass;
        }

        /// <summary>
        /// Kept as a compatibility entry point for older calls, but we no
        /// longer reflect across ClassDB and append Lightbringer into every
        /// Class collection. That broad injection was unsafe when multiple
        /// custom classes were loaded together.
        /// </summary>
        internal static void RegisterWithNativeClassDatabase(
            ClassDB database)
        {
            TryEnsureClass();
        }

        internal static bool IsLightbringerClass(
            Class characterClass)
        {
            if (characterClass == null)
            {
                return false;
            }

            if (_lightbringerClass != null &&
                characterClass == _lightbringerClass)
            {
                return true;
            }

            string displayName =
                GetClassDisplayName(
                    characterClass);

            return string.Equals(
                displayName,
                LightbringerConstants.DisplayName,
                StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    characterClass.name,
                    "FOTF_Lightbringer_Class",
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsCurrentPlayerLightbringer()
        {
            return GameData.PlayerStats != null &&
                   IsLightbringerClass(
                       GameData.PlayerStats.CharacterClass);
        }

        internal static Class FindNativeClass(
            string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                return null;
            }

            Class[] classes =
                Resources.FindObjectsOfTypeAll<Class>();

            if (classes == null)
            {
                return null;
            }

            for (int i = 0;
                 i < classes.Length;
                 i++)
            {
                Class candidate = classes[i];

                if (candidate == null ||
                    candidate == _lightbringerClass)
                {
                    continue;
                }

                // Never use another FOTF/custom class as the native template.
                string objectName =
                    candidate.name ??
                    string.Empty;

                if (objectName.StartsWith(
                        "FOTF_",
                        StringComparison.OrdinalIgnoreCase) ||
                    objectName.StartsWith(
                        "FOTF - ",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string candidateName =
                    GetClassDisplayName(
                        candidate);

                if (string.Equals(
                        candidateName,
                        displayName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        internal static string GetClassDisplayName(
            Class characterClass)
        {
            if (characterClass == null)
            {
                return string.Empty;
            }

            string[] members =
            {
                "DisplayName",
                "ClassName",
                "ClassDisplayName"
            };

            for (int i = 0;
                 i < members.Length;
                 i++)
            {
                string value =
                    TryGetStringMember(
                        characterClass,
                        members[i]);

                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return characterClass.name ??
                   string.Empty;
        }

        private static Class FindExistingLightbringerClass()
        {
            Class[] classes =
                Resources.FindObjectsOfTypeAll<Class>();

            if (classes == null)
            {
                return null;
            }

            for (int i = 0;
                 i < classes.Length;
                 i++)
            {
                Class candidate = classes[i];

                if (candidate == null)
                {
                    continue;
                }

                if (string.Equals(
                        candidate.name,
                        "FOTF_Lightbringer_Class",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }

                string displayName =
                    GetClassDisplayName(
                        candidate);

                if (string.Equals(
                        displayName,
                        LightbringerConstants.DisplayName,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        displayName,
                        "Blightcaller",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void LogDruidScaling(Class lightbringer)
        {
            if (lightbringer == null)
                return;

            // These are the same seven native class stat-benefit fields that
            // govern other FOTF casters, now read directly from Druid instead
            // of using bespoke coefficients. Print once on class creation.
            Plugin.LogInfo(
                "[Lightbringer] Native Druid stat scaling: " +
                "STR=" + lightbringer.StrBenefit +
                " END=" + lightbringer.EndBenefit +
                " DEX=" + lightbringer.DexBenefit +
                " AGI=" + lightbringer.AgiBenefit +
                " INT=" + lightbringer.IntBenefit +
                " WIS=" + lightbringer.WisBenefit +
                " CHA=" + lightbringer.ChaBenefit +
                " Mitigation=" + lightbringer.MitigationBonus +
                " Aggro=" + lightbringer.AggroMod);
        }

        private static void CopyNativeGameplayData(
            Class source,
            Class destination)
        {
            if (source == null ||
                destination == null)
            {
                return;
            }

            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            FieldInfo[] fields =
                typeof(Class).GetFields(flags);

            for (int i = 0;
                 i < fields.Length;
                 i++)
            {
                FieldInfo field = fields[i];

                if (field == null ||
                    field.IsStatic ||
                    field.IsInitOnly ||
                    IsIdentityMember(field.Name))
                {
                    continue;
                }

                try
                {
                    object value =
                        field.GetValue(source);

                    field.SetValue(
                        destination,
                        CloneMutableValue(value));
                }
                catch
                {
                    // A non-essential field failing to copy should not stop
                    // class creation. The important public gameplay fields are
                    // normal writable serialized members.
                }
            }

            PropertyInfo[] properties =
                typeof(Class).GetProperties(flags);

            for (int i = 0;
                 i < properties.Length;
                 i++)
            {
                PropertyInfo property = properties[i];

                if (property == null ||
                    !property.CanRead ||
                    !property.CanWrite ||
                    property.GetIndexParameters().Length != 0 ||
                    IsIdentityMember(property.Name))
                {
                    continue;
                }

                try
                {
                    object value =
                        property.GetValue(
                            source,
                            null);

                    property.SetValue(
                        destination,
                        CloneMutableValue(value),
                        null);
                }
                catch
                {
                }
            }
        }

        private static object CloneMutableValue(
            object value)
        {
            if (value == null)
            {
                return null;
            }

            Array array = value as Array;
            if (array != null)
            {
                return array.Clone();
            }

            IList sourceList = value as IList;
            if (sourceList != null)
            {
                Type type = value.GetType();

                try
                {
                    IList copied =
                        Activator.CreateInstance(type)
                        as IList;

                    if (copied != null)
                    {
                        for (int i = 0;
                             i < sourceList.Count;
                             i++)
                        {
                            copied.Add(
                                sourceList[i]);
                        }

                        return copied;
                    }
                }
                catch
                {
                }
            }

            return value;
        }

        private static bool IsIdentityMember(
            string memberName)
        {
            if (string.IsNullOrEmpty(memberName))
            {
                return false;
            }

            string normalized =
                memberName.Replace(
                    "_",
                    string.Empty)
                .ToLowerInvariant();

            return normalized == "id" ||
                   normalized == "dbid" ||
                   normalized == "dbname" ||
                   normalized == "name" ||
                   normalized == "classname" ||
                   normalized == "displayname" ||
                   normalized == "classdisplayname";
        }

        private static void ApplyLightbringerIdentity(
            Class characterClass)
        {
            TrySetStringMember(
                characterClass,
                "DisplayName",
                LightbringerConstants.DisplayName);

            TrySetStringMember(
                characterClass,
                "ClassName",
                LightbringerConstants.DisplayName);

            TrySetStringMember(
                characterClass,
                "ClassDisplayName",
                LightbringerConstants.DisplayName);

            // Only set optional database identity members if they actually
            // exist on this Erenshor build. Keep them unique to Lightbringer.
            TrySetStringMember(
                characterClass,
                "Id",
                LightbringerConstants.InternalId);

            TrySetStringMember(
                characterClass,
                "ID",
                LightbringerConstants.InternalId);

            TrySetStringMember(
                characterClass,
                "DBName",
                LightbringerConstants.InternalId);
        }

        private static string TryGetStringMember(
            object instance,
            string memberName)
        {
            if (instance == null ||
                string.IsNullOrEmpty(memberName))
            {
                return null;
            }

            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            Type type = instance.GetType();

            FieldInfo field =
                type.GetField(
                    memberName,
                    flags);

            if (field != null &&
                field.FieldType == typeof(string))
            {
                try
                {
                    return field.GetValue(instance)
                        as string;
                }
                catch
                {
                }
            }

            PropertyInfo property =
                type.GetProperty(
                    memberName,
                    flags);

            if (property != null &&
                property.PropertyType == typeof(string) &&
                property.CanRead)
            {
                try
                {
                    return property.GetValue(instance, null)
                        as string;
                }
                catch
                {
                }
            }

            return null;
        }

        private static void TrySetStringMember(
            object instance,
            string memberName,
            string value)
        {
            if (instance == null ||
                string.IsNullOrEmpty(memberName))
            {
                return;
            }

            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            Type type = instance.GetType();

            FieldInfo field =
                type.GetField(
                    memberName,
                    flags);

            if (field != null &&
                field.FieldType == typeof(string))
            {
                try
                {
                    field.SetValue(
                        instance,
                        value);
                }
                catch
                {
                }
            }

            PropertyInfo property =
                type.GetProperty(
                    memberName,
                    flags);

            if (property != null &&
                property.PropertyType == typeof(string) &&
                property.CanWrite)
            {
                try
                {
                    property.SetValue(
                        instance,
                        value,
                        null);
                }
                catch
                {
                }
            }
        }
    }
}
