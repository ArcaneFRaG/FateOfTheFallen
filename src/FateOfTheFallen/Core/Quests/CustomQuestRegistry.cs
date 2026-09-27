using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace FateOfTheFallen
{
    internal static class CustomQuestRegistry
    {
        internal static bool Register(
            Quest quest)
        {
            if (quest == null ||
                string.IsNullOrEmpty(quest.DBName))
            {
                return false;
            }

            if (GameData.QuestDB == null)
            {
                Plugin.NativeLog.LogWarning(
                    "Fate of the Fallen: QuestDB was null while registering quest [" +
                    quest.DBName +
                    "].");

                return false;
            }

            try
            {
                FieldInfo questDatabaseField =
                    AccessTools.Field(
                        GameData.QuestDB.GetType(),
                        "QuestDatabase");

                if (questDatabaseField == null)
                {
                    Plugin.NativeLog.LogError(
                        "Fate of the Fallen: could not find QuestDB.QuestDatabase field.");

                    return false;
                }

                object databaseObject =
                    questDatabaseField.GetValue(
                        GameData.QuestDB);

                if (databaseObject == null)
                {
                    Plugin.NativeLog.LogError(
                        "Fate of the Fallen: native QuestDatabase field value was null.");

                    return false;
                }

                List<Quest> questList =
                    databaseObject as List<Quest>;

                if (questList != null)
                {
                    for (int i = 0;
                         i < questList.Count;
                         i++)
                    {
                        Quest existing =
                            questList[i];

                        if (existing == null ||
                            !string.Equals(
                                existing.DBName,
                                quest.DBName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        questList[i] =
                            quest;

                        return true;
                    }

                    questList.Add(quest);
                    return true;
                }

                Quest[] questArray =
                    databaseObject as Quest[];

                if (questArray != null)
                {
                    for (int i = 0;
                         i < questArray.Length;
                         i++)
                    {
                        Quest existing =
                            questArray[i];

                        if (existing == null ||
                            !string.Equals(
                                existing.DBName,
                                quest.DBName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        questArray[i] =
                            quest;

                        questDatabaseField.SetValue(
                            GameData.QuestDB,
                            questArray);

                        return true;
                    }

                    Quest[] expanded =
                        new Quest[questArray.Length + 1];

                    Array.Copy(
                        questArray,
                        expanded,
                        questArray.Length);

                    expanded[questArray.Length] =
                        quest;

                    questDatabaseField.SetValue(
                        GameData.QuestDB,
                        expanded);

                    return true;
                }

                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: unsupported QuestDatabase field type [" +
                    databaseObject.GetType().FullName +
                    "].");
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "Fate of the Fallen: failed registering quest [" +
                    quest.DBName +
                    "]: " +
                    exception);
            }

            return false;
        }
    }
}
