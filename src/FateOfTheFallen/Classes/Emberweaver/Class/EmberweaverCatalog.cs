using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Owns Emberweaver's canonical Class ScriptableObject.
    ///
    /// IMPORTANT:
    /// Emberweaver is a distinct Class object. We use the native Arcanist only
    /// as a source for gameplay/proficiency/stat data. We do not reuse the
    /// Arcanist object and we do not broadly inject Emberweaver into arbitrary
    /// ClassDB collections. This keeps Emberweaver isolated from Blightcaller
    /// and all other custom classes.
    /// </summary>
    internal static class EmberweaverCatalog
    {
        private static Class _emberweaverClass;
        private static Class _arcanistClass;

        internal static Class EmberweaverClass
        {
            get { return _emberweaverClass; }
        }

        internal static Class ArcanistClass
        {
            get
            {
                if (_arcanistClass == null ||
                    IsEmberweaverClass(_arcanistClass))
                {
                    _arcanistClass =
                        FindNativeClass(
                            EmberweaverConstants.NativeTemplateClass);
                }

                return _arcanistClass;
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
            if (_emberweaverClass != null)
            {
                ApplyBlightcallerStatScaling(
                    _emberweaverClass);

                return _emberweaverClass;
            }

            // Recover our own existing runtime object only. This lookup is
            // deliberately name-specific and will never return Blightcaller.
            Class existing =
                FindExistingEmberweaverClass();

            if (existing != null)
            {
                ApplyEmberweaverIdentity(
                    existing);

                ApplyBlightcallerStatScaling(
                    existing);

                _emberweaverClass = existing;
                return _emberweaverClass;
            }

            Class arcanist = ArcanistClass;
            if (arcanist == null)
            {
                if (logFailures)
                {
                    Plugin.LogError(
                        "Native Arcanist Class template was not found.");
                }

                return null;
            }

            Class created =
                ScriptableObject.CreateInstance<Class>();

            if (created == null)
            {
                if (logFailures)
                {
                    Plugin.LogError(
                        "Failed to create independent Emberweaver Class object.");
                }

                return null;
            }

            // Copy Arcanist gameplay data onto our NEW object. Mutable
            // collections/arrays are duplicated so later Emberweaver changes
            // cannot modify the native Arcanist through a shared collection.
            CopyArcanistGameplayData(
                arcanist,
                created);

            created.name =
                "FOTF_Emberweaver_Class";

            created.hideFlags =
                HideFlags.HideAndDontSave;

            ApplyEmberweaverIdentity(
                created);

            // Emberweaver intentionally uses the exact same class-stat
            // progression values as the proven Blightcaller implementation.
            // Equipment compatibility remains Arcanist-derived, but class
            // progression itself is explicit and independent.
            ApplyBlightcallerStatScaling(
                created);

            _emberweaverClass = created;

            Plugin.LogInfo(
                "Created independent Emberweaver Class object from Arcanist gameplay data.");

            return _emberweaverClass;
        }

        /// <summary>
        /// Kept as a compatibility entry point for older calls, but we no
        /// longer reflect across ClassDB and append Emberweaver into every
        /// Class collection. That broad injection was unsafe when multiple
        /// custom classes were loaded together.
        /// </summary>
        internal static void RegisterWithNativeClassDatabase(
            ClassDB database)
        {
            TryEnsureClass();
        }

        internal static bool IsEmberweaverClass(
            Class characterClass)
        {
            if (characterClass == null)
            {
                return false;
            }

            if (_emberweaverClass != null &&
                characterClass == _emberweaverClass)
            {
                return true;
            }

            string displayName =
                GetClassDisplayName(
                    characterClass);

            return string.Equals(
                displayName,
                EmberweaverConstants.DisplayName,
                StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    characterClass.name,
                    "FOTF_Emberweaver_Class",
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsCurrentPlayerEmberweaver()
        {
            return GameData.PlayerStats != null &&
                   IsEmberweaverClass(
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
                    candidate == _emberweaverClass)
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

        private static Class FindExistingEmberweaverClass()
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
                        "FOTF_Emberweaver_Class",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }

                string displayName =
                    GetClassDisplayName(
                        candidate);

                if (string.Equals(
                        displayName,
                        EmberweaverConstants.DisplayName,
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

        private static void ApplyBlightcallerStatScaling(
            Class characterClass)
        {
            if (characterClass == null)
            {
                return;
            }

            // Keep these in lockstep with BlightcallerCatalog.EnsureClass().
            characterClass.MitigationBonus =
                1f;

            characterClass.StrBenefit =
                3;

            characterClass.EndBenefit =
                10;

            characterClass.DexBenefit =
                3;

            characterClass.AgiBenefit =
                3;

            characterClass.IntBenefit =
                14;

            characterClass.WisBenefit =
                9;

            characterClass.ChaBenefit =
                10;

            characterClass.AggroMod =
                1.6f;
        }

        private static void CopyArcanistGameplayData(
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

        private static void ApplyEmberweaverIdentity(
            Class characterClass)
        {
            TrySetStringMember(
                characterClass,
                "DisplayName",
                EmberweaverConstants.DisplayName);

            TrySetStringMember(
                characterClass,
                "ClassName",
                EmberweaverConstants.DisplayName);

            TrySetStringMember(
                characterClass,
                "ClassDisplayName",
                EmberweaverConstants.DisplayName);

            // Only set optional database identity members if they actually
            // exist on this Erenshor build. Keep them unique to Emberweaver.
            TrySetStringMember(
                characterClass,
                "Id",
                EmberweaverConstants.InternalId);

            TrySetStringMember(
                characterClass,
                "ID",
                EmberweaverConstants.InternalId);

            TrySetStringMember(
                characterClass,
                "DBName",
                EmberweaverConstants.InternalId);
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
