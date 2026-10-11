using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class FOTFCharacterMilestoneStore
    {
        private const string RootFolderName = "FateOfTheFallen";
        private const string CharacterFolderName = "CharacterMilestones";

        private static readonly HashSet<string> Credits =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static string _loadedCharacter;
        private static bool _loaded;

        internal static bool IsNonQuestMarker(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            return
                id.StartsWith("FOTF_WORLD_BOSS_", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("FOTF_RAID_POC_", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("FOTF_CREDIT_", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("FOTF_MILESTONE_", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool HasCredit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            MigrateSpecificLegacyMarker(id);
            EnsureLoaded();

            return Credits.Contains(id);
        }

        internal static bool AwardCredit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            EnsureLoaded();

            if (string.IsNullOrEmpty(_loadedCharacter))
            {
                Plugin.NativeLog.LogWarning(
                    "[FOTF Milestones] current character name is unavailable; credit [" +
                    id + "] could not be persisted.");
                return false;
            }

            if (Credits.Contains(id))
            {
                return true;
            }

            Credits.Add(id);

            if (!Save())
            {
                Credits.Remove(id);
                return false;
            }

            return true;
        }

        internal static bool RevokeCredit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            EnsureLoaded();

            if (string.IsNullOrEmpty(_loadedCharacter))
            {
                Plugin.NativeLog.LogWarning(
                    "[FOTF Milestones] current character name is unavailable; credit [" +
                    id + "] could not be revoked.");
                return false;
            }

            if (!Credits.Contains(id))
            {
                return true;
            }

            Credits.Remove(id);

            if (!Save())
            {
                Credits.Add(id);
                return false;
            }

            return true;
        }


        internal static int MigrateLegacyCompletedQuestMarkers()
        {
            object completed = GameData.CompletedQuests;
            if (completed == null)
            {
                return 0;
            }

            EnsureLoaded();
            if (string.IsNullOrEmpty(_loadedCharacter))
            {
                return 0;
            }

            List<string> markers = SnapshotNonQuestMarkers(completed);
            int migrated = 0;

            for (int i = 0; i < markers.Count; i++)
            {
                string marker = markers[i];

                if (!AwardCredit(marker))
                {
                    continue;
                }

                if (RemoveString(completed, marker))
                {
                    migrated++;
                }
            }

            if (migrated > 0)
            {
                Plugin.NativeLog.LogInfo(
                    "[FOTF Milestones] migrated " + migrated +
                    " legacy non-quest CompletedQuests marker(s) into the FOTF character milestone store.");
            }

            return migrated;
        }

        internal static bool MigrateSpecificLegacyMarker(string id)
        {
            if (string.IsNullOrEmpty(id) || !IsNonQuestMarker(id))
            {
                return false;
            }

            object completed = GameData.CompletedQuests;
            if (completed == null || !CollectionContains(completed, id))
            {
                return false;
            }

            EnsureLoaded();
            if (string.IsNullOrEmpty(_loadedCharacter))
            {
                return false;
            }

            if (!AwardCredit(id))
            {
                return false;
            }

            RemoveString(completed, id);
            return true;
        }

        internal static List<string> SnapshotNonQuestMarkers(object collection)
        {
            List<string> result = new List<string>();
            IEnumerable enumerable = collection as IEnumerable;

            if (enumerable == null)
            {
                return result;
            }

            foreach (object entry in enumerable)
            {
                string value = entry as string;
                if (IsNonQuestMarker(value))
                {
                    result.Add(value);
                }
            }

            return result;
        }

        internal static bool CollectionContains(object collection, string value)
        {
            if (collection == null || string.IsNullOrEmpty(value))
            {
                return false;
            }

            IList list = collection as IList;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i] as string, value, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            try
            {
                MethodInfo contains = AccessTools.Method(
                    collection.GetType(),
                    "Contains",
                    new Type[] { typeof(string) });

                if (contains == null)
                {
                    return false;
                }

                object result = contains.Invoke(collection, new object[] { value });
                return result is bool && (bool)result;
            }
            catch
            {
                return false;
            }
        }

        internal static bool RemoveString(object collection, string value)
        {
            if (collection == null || string.IsNullOrEmpty(value))
            {
                return false;
            }

            bool removed = false;
            IList list = collection as IList;

            if (list != null)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (!string.Equals(list[i] as string, value, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    list.RemoveAt(i);
                    removed = true;
                }

                return removed;
            }

            try
            {
                MethodInfo remove = AccessTools.Method(
                    collection.GetType(),
                    "Remove",
                    new Type[] { typeof(string) });

                if (remove == null)
                {
                    return false;
                }

                while (CollectionContains(collection, value))
                {
                    object result = remove.Invoke(collection, new object[] { value });
                    if (result is bool && !(bool)result)
                    {
                        break;
                    }

                    removed = true;
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogWarning(
                    "[FOTF Milestones] failed removing legacy marker [" + value + "]: " +
                    exception.Message);
            }

            return removed;
        }

        private static void EnsureLoaded()
        {
            string character = ResolveCharacterName();

            if (_loaded && string.Equals(_loadedCharacter, character, StringComparison.Ordinal))
            {
                return;
            }

            Credits.Clear();
            _loadedCharacter = character;
            _loaded = true;

            if (string.IsNullOrEmpty(character))
            {
                return;
            }

            string path = ResolveFilePath(character);
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string value = lines[i] != null ? lines[i].Trim() : string.Empty;
                    if (!string.IsNullOrEmpty(value))
                    {
                        Credits.Add(value);
                    }
                }
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[FOTF Milestones] failed loading character milestone file: " + exception);
            }
        }

        private static bool Save()
        {
            if (string.IsNullOrEmpty(_loadedCharacter))
            {
                return false;
            }

            try
            {
                string path = ResolveFilePath(_loadedCharacter);
                string directory = Path.GetDirectoryName(path);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                List<string> values = new List<string>(Credits);
                values.Sort(StringComparer.OrdinalIgnoreCase);

                string temp = path + ".tmp";
                File.WriteAllLines(temp, values.ToArray());

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temp, path);
                return true;
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[FOTF Milestones] failed saving character milestone file: " + exception);
                return false;
            }
        }

        private static string ResolveCharacterName()
        {
            try
            {
                if (GameData.PlayerStats == null ||
                    string.IsNullOrWhiteSpace(GameData.PlayerStats.MyName))
                {
                    return null;
                }

                return GameData.PlayerStats.MyName.Trim();
            }
            catch
            {
                return null;
            }
        }

        private static string ResolveFilePath(string characterName)
        {
            return Path.Combine(
                Application.persistentDataPath,
                "ESSaveData",
                RootFolderName,
                CharacterFolderName,
                SanitizeFileName(characterName) + ".credits");
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "UnknownCharacter";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = value.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (chars[i] == invalid[j])
                    {
                        chars[i] = '_';
                        break;
                    }
                }
            }

            return new string(chars);
        }
    }
}
