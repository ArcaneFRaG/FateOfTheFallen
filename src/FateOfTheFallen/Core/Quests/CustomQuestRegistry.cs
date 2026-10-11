using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace FateOfTheFallen
{
    /// <summary>
    /// Canonical FOTF quest registry.
    ///
    /// IMPORTANT:
    /// The native Quest Log does not merely rely on GameData.HasQuest.
    /// It also enumerates Erenshor's QuestDB.QuestDatabase collection.
    ///
    /// FOTF therefore keeps canonical Quest objects AND physically injects
    /// them into the live native QuestDatabase collection.
    /// </summary>
    internal static class CustomQuestRegistry
    {
        private static readonly Dictionary<string, Quest>
            ByDbName =
                new Dictionary<string, Quest>(
                    StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Quest>
            ByDisplayTitle =
                new Dictionary<string, Quest>(
                    StringComparer.OrdinalIgnoreCase);


        internal static bool Register(
            Quest quest)
        {
            if (quest == null ||
                string.IsNullOrEmpty(
                    quest.DBName))
            {
                return false;
            }

            ByDbName[quest.DBName] =
                quest;

            if (!string.IsNullOrEmpty(
                    quest.QuestName))
            {
                ByDisplayTitle[quest.QuestName] =
                    quest;
            }

            // Native insertion may legitimately fail if QuestDB has not
            // awakened yet. ReapplyToNativeDatabase() retries later.
            TryInsertIntoNativeDatabase(
                quest,
                false);

            return true;
        }


        internal static Quest ResolveByDbName(
            string dbName)
        {
            if (string.IsNullOrEmpty(
                    dbName))
            {
                return null;
            }

            Quest quest;

            return
                ByDbName.TryGetValue(
                    dbName,
                    out quest)
                    ? quest
                    : null;
        }


        internal static Quest ResolveByDisplayTitle(
            string title)
        {
            if (string.IsNullOrEmpty(
                    title))
            {
                return null;
            }

            Quest quest;

            return
                ByDisplayTitle.TryGetValue(
                    title,
                    out quest)
                    ? quest
                    : null;
        }


        internal static bool ReapplyToNativeDatabase()
        {
            if (GameData.QuestDB == null)
            {
                return false;
            }

            bool allSucceeded =
                true;

            foreach (
                KeyValuePair<string, Quest> entry
                in ByDbName)
            {
                Quest quest =
                    entry.Value;

                if (quest == null)
                {
                    continue;
                }

                if (!TryInsertIntoNativeDatabase(
                        quest,
                        true))
                {
                    allSucceeded =
                        false;
                }
            }

            return allSucceeded;
        }


        internal static string BuildNativeDebugText()
        {
            if (GameData.QuestDB == null)
            {
                return
                    "QuestDB=<null>";
            }

            object collection;
            Type declaredType;
            string memberKind;

            if (!TryGetQuestDatabaseMember(
                    GameData.QuestDB,
                    out collection,
                    out declaredType,
                    out memberKind))
            {
                return
                    "QuestDB=" +
                    GameData.QuestDB.GetType().FullName +
                    " | QuestDatabase member=NOT FOUND";
            }

            int count =
                GetCollectionCount(
                    collection);

            bool q1 =
                CollectionContainsQuestId(
                    collection,
                    FateOfTheFallenQuests
                        .AfterLastWardenQuestId);

            bool q2 =
                CollectionContainsQuestId(
                    collection,
                    FateOfTheFallenQuests
                        .SignsOfDecayQuestId);

            Quest q1Lookup =
                null;

            Quest q2Lookup =
                null;

            try
            {
                q1Lookup =
                    GameData.QuestDB
                        .GetQuestByName(
                            FateOfTheFallenQuests
                                .AfterLastWardenQuestId);

                q2Lookup =
                    GameData.QuestDB
                        .GetQuestByName(
                            FateOfTheFallenQuests
                                .SignsOfDecayQuestId);
            }
            catch
            {
            }

            return
                "QuestDB=" +
                GameData.QuestDB.GetType().Name +
                " | QuestDatabase(" +
                memberKind +
                ")=" +
                (declaredType != null
                    ? declaredType.FullName
                    : "<unknown>") +
                " | Count=" +
                count +
                " | NativeQ1=" +
                (q1 ? "YES" : "NO") +
                " | NativeQ2=" +
                (q2 ? "YES" : "NO") +
                " | LookupQ1=" +
                (q1Lookup != null ? "YES" : "NO") +
                " | LookupQ2=" +
                (q2Lookup != null ? "YES" : "NO");
        }


        internal static bool NativeContainsQuest(
            string questId)
        {
            if (GameData.QuestDB == null ||
                string.IsNullOrEmpty(
                    questId))
            {
                return false;
            }

            object collection;
            Type declaredType;
            string memberKind;

            if (!TryGetQuestDatabaseMember(
                    GameData.QuestDB,
                    out collection,
                    out declaredType,
                    out memberKind))
            {
                return false;
            }

            return
                CollectionContainsQuestId(
                    collection,
                    questId);
        }


        private static bool TryInsertIntoNativeDatabase(
            Quest quest,
            bool logFailure)
        {
            if (quest == null ||
                GameData.QuestDB == null)
            {
                return false;
            }

            try
            {
                QuestDB questDb =
                    GameData.QuestDB;

                MemberInfo member;
                object collection;
                Type declaredType;
                string memberKind;

                if (!TryGetQuestDatabaseMember(
                        questDb,
                        out member,
                        out collection,
                        out declaredType,
                        out memberKind))
                {
                    if (logFailure)
                    {
                        Plugin.NativeLog.LogError(
                            "Fate of the Fallen: QuestDB has no QuestDatabase field/property. Runtime type=[" +
                            questDb.GetType().FullName +
                            "].");
                    }

                    return false;
                }

                if (collection == null)
                {
                    collection =
                        CreateEmptyCollection(
                            declaredType);

                    if (collection == null ||
                        !SetQuestDatabaseMemberValue(
                            questDb,
                            member,
                            collection))
                    {
                        if (logFailure)
                        {
                            Plugin.NativeLog.LogError(
                                "Fate of the Fallen: QuestDB.QuestDatabase is null and FOTF could not create/assign a compatible collection. DeclaredType=[" +
                                (declaredType != null
                                    ? declaredType.FullName
                                    : "<null>") +
                                "].");
                        }

                        return false;
                    }
                }

                // Arrays need replacement with an expanded array.
                Array array =
                    collection as Array;

                if (array != null)
                {
                    for (int i = 0;
                         i < array.Length;
                         i++)
                    {
                        Quest existing =
                            array.GetValue(
                                i) as Quest;

                        if (existing == quest)
                        {
                            return true;
                        }

                        if (existing == null ||
                            !string.Equals(
                                existing.DBName,
                                quest.DBName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        array.SetValue(
                            quest,
                            i);

                        return
                            SetQuestDatabaseMemberValue(
                                questDb,
                                member,
                                array);
                    }

                    Type elementType =
                        array.GetType()
                            .GetElementType();

                    if (elementType == null ||
                        !elementType.IsAssignableFrom(
                            typeof(Quest)))
                    {
                        if (logFailure)
                        {
                            Plugin.NativeLog.LogError(
                                "Fate of the Fallen: QuestDatabase array element type is not compatible with Quest: [" +
                                (elementType != null
                                    ? elementType.FullName
                                    : "<null>") +
                                "].");
                        }

                        return false;
                    }

                    Array expanded =
                        Array.CreateInstance(
                            elementType,
                            array.Length + 1);

                    Array.Copy(
                        array,
                        expanded,
                        array.Length);

                    expanded.SetValue(
                        quest,
                        array.Length);

                    if (!SetQuestDatabaseMemberValue(
                            questDb,
                            member,
                            expanded))
                    {
                        return false;
                    }

                    return
                        CollectionContainsQuestId(
                            expanded,
                            quest.DBName);
                }

                IList list =
                    collection as IList;

                if (list != null)
                {
                    for (int i = 0;
                         i < list.Count;
                         i++)
                    {
                        Quest existing =
                            list[i] as Quest;

                        if (existing == quest)
                        {
                            return true;
                        }

                        if (existing == null ||
                            !string.Equals(
                                existing.DBName,
                                quest.DBName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        list[i] =
                            quest;

                        return true;
                    }

                    if (list.IsFixedSize ||
                        list.IsReadOnly)
                    {
                        if (logFailure)
                        {
                            Plugin.NativeLog.LogError(
                                "Fate of the Fallen: QuestDatabase implements IList but is fixed-size/read-only. Type=[" +
                                collection.GetType().FullName +
                                "].");
                        }

                        return false;
                    }

                    list.Add(
                        quest);

                    return
                        CollectionContainsQuestId(
                            list,
                            quest.DBName);
                }

                // Final generic fallback for unusual collection types.
                foreach (object entry in
                    (collection as IEnumerable ??
                     new object[0]))
                {
                    Quest existing =
                        entry as Quest;

                    if (existing == quest ||
                        (existing != null &&
                         string.Equals(
                             existing.DBName,
                             quest.DBName,
                             StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }

                MethodInfo add =
                    AccessTools.Method(
                        collection.GetType(),
                        "Add",
                        new Type[]
                        {
                            typeof(Quest)
                        });

                if (add != null)
                {
                    add.Invoke(
                        collection,
                        new object[]
                        {
                            quest
                        });

                    return
                        CollectionContainsQuestId(
                            collection,
                            quest.DBName);
                }

                if (logFailure)
                {
                    Plugin.NativeLog.LogError(
                        "Fate of the Fallen: unsupported QuestDatabase collection type [" +
                        collection.GetType().FullName +
                        "].");
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: failed inserting custom quest [" +
                    quest.DBName +
                    "] into native QuestDB: " +
                    exception);
            }

            return false;
        }


        private static bool TryGetQuestDatabaseMember(
            QuestDB questDb,
            out object collection,
            out Type declaredType,
            out string memberKind)
        {
            MemberInfo member;

            return
                TryGetQuestDatabaseMember(
                    questDb,
                    out member,
                    out collection,
                    out declaredType,
                    out memberKind);
        }


        private static bool TryGetQuestDatabaseMember(
            QuestDB questDb,
            out MemberInfo member,
            out object collection,
            out Type declaredType,
            out string memberKind)
        {
            member =
                null;

            collection =
                null;

            declaredType =
                null;

            memberKind =
                "<none>";

            if (questDb == null)
            {
                return false;
            }

            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            Type runtimeType =
                questDb.GetType();

            FieldInfo field =
                runtimeType.GetField(
                    "QuestDatabase",
                    flags);

            if (field != null)
            {
                member =
                    field;

                collection =
                    field.GetValue(
                        questDb);

                declaredType =
                    field.FieldType;

                memberKind =
                    "field";

                return true;
            }

            PropertyInfo property =
                runtimeType.GetProperty(
                    "QuestDatabase",
                    flags);

            if (property != null &&
                property.CanRead)
            {
                member =
                    property;

                collection =
                    property.GetValue(
                        questDb,
                        null);

                declaredType =
                    property.PropertyType;

                memberKind =
                    "property";

                return true;
            }

            return false;
        }


        private static bool SetQuestDatabaseMemberValue(
            QuestDB questDb,
            MemberInfo member,
            object value)
        {
            if (questDb == null ||
                member == null)
            {
                return false;
            }

            FieldInfo field =
                member as FieldInfo;

            if (field != null)
            {
                field.SetValue(
                    questDb,
                    value);

                return true;
            }

            PropertyInfo property =
                member as PropertyInfo;

            if (property != null &&
                property.CanWrite)
            {
                property.SetValue(
                    questDb,
                    value,
                    null);

                return true;
            }

            return false;
        }


        private static object CreateEmptyCollection(
            Type declaredType)
        {
            if (declaredType == null)
            {
                return null;
            }

            if (declaredType.IsArray)
            {
                Type element =
                    declaredType.GetElementType();

                return
                    element == null
                        ? null
                        : Array.CreateInstance(
                            element,
                            0);
            }

            if (!declaredType.IsAbstract &&
                !declaredType.IsInterface)
            {
                try
                {
                    return
                        Activator.CreateInstance(
                            declaredType);
                }
                catch
                {
                }
            }

            if (declaredType.IsAssignableFrom(
                    typeof(List<Quest>)))
            {
                return
                    new List<Quest>();
            }

            return null;
        }


        private static int GetCollectionCount(
            object collection)
        {
            if (collection == null)
            {
                return 0;
            }

            ICollection collectionCount =
                collection as ICollection;

            if (collectionCount != null)
            {
                return
                    collectionCount.Count;
            }

            int count =
                0;

            IEnumerable enumerable =
                collection as IEnumerable;

            if (enumerable != null)
            {
                foreach (object ignored in enumerable)
                {
                    count++;
                }
            }

            return count;
        }


        private static bool CollectionContainsQuestId(
            object collection,
            string questId)
        {
            if (collection == null ||
                string.IsNullOrEmpty(
                    questId))
            {
                return false;
            }

            IEnumerable enumerable =
                collection as IEnumerable;

            if (enumerable == null)
            {
                return false;
            }

            foreach (object entry in enumerable)
            {
                Quest quest =
                    entry as Quest;

                if (quest != null &&
                    string.Equals(
                        quest.DBName,
                        questId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }


    // ================================================================
    // NATIVE LOOKUP FALLBACKS
    // ================================================================

    [HarmonyPatch(
        typeof(QuestDB),
        "GetQuestByName")]
    internal static class Patch_FOTFQuestDb_GetQuestByName
    {
        [HarmonyPostfix]
        private static void Postfix(
            string __0,
            ref Quest __result)
        {
            if (__result != null)
            {
                return;
            }

            Quest custom =
                CustomQuestRegistry
                    .ResolveByDbName(
                        __0);

            if (custom != null)
            {
                __result =
                    custom;
            }
        }
    }


    [HarmonyPatch(
        typeof(QuestDB),
        "GetQuestByDisplayTitle")]
    internal static class Patch_FOTFQuestDb_GetQuestByDisplayTitle
    {
        [HarmonyPostfix]
        private static void Postfix(
            string __0,
            ref Quest __result)
        {
            if (__result != null)
            {
                return;
            }

            Quest custom =
                CustomQuestRegistry
                    .ResolveByDisplayTitle(
                        __0);

            if (custom != null)
            {
                __result =
                    custom;
            }
        }
    }


    // ================================================================
    // NATIVE QUEST DATABASE LIFECYCLE
    // ================================================================

    /// <summary>
    /// Erenshor initializes/rebuilds QuestDB during its own Unity lifecycle.
    /// Patch whichever of Awake/Start exist in this game build and re-inject
    /// all canonical FOTF quests after native initialization.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_FOTFQuestDb_Lifecycle
    {
        private static IEnumerable<MethodBase>
            TargetMethods()
        {
            MethodInfo awake =
                AccessTools.Method(
                    typeof(QuestDB),
                    "Awake");

            if (awake != null)
            {
                yield return awake;
            }

            MethodInfo start =
                AccessTools.Method(
                    typeof(QuestDB),
                    "Start");

            if (start != null)
            {
                yield return start;
            }
        }


        [HarmonyPostfix]
        private static void Postfix()
        {
            FateOfTheFallenQuests
                .Register();

            CustomQuestRegistry
                .ReapplyToNativeDatabase();
        }
    }
}
