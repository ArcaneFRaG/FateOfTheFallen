using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Optional, stable class-identity bridge for separately distributed UI mods.
    /// Do not make ArcaneUI, Group Frames or Combat Meter reference FOTF.dll:
    /// resolve this public method by reflection only when FOTF is installed.
    /// Null means "use native class handling".
    /// </summary>
    public static class FOTFClassColorInterop
    {
        private sealed class CacheEntry
        {
            internal string ClassName;
            internal float ExpiresAt;
        }

        private static readonly Dictionary<string, CacheEntry> SimClassCache =
            new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        public static string ResolveClassName(Stats stats, string knownName)
        {
            try
            {
                if (stats != null && stats.CharacterClass != null)
                {
                    if (LightbringerCatalog.IsLightbringerClass(stats.CharacterClass))
                        return "Lightbringer";
                    if (EmberweaverCatalog.IsEmberweaverClass(stats.CharacterClass))
                        return "Emberweaver";
                    if (BlightcallerCatalog.IsBlightcallerClass(stats.CharacterClass))
                        return "Blightcaller";
                }

                // A player's selected FOTF class may be saved independently of
                // its native Arcanist template during a scene/character refresh.
                bool isPlayer =
                    (stats != null && ReferenceEquals(stats, GameData.PlayerStats)) ||
                    (!string.IsNullOrWhiteSpace(knownName) &&
                     GameData.PlayerStats != null &&
                     string.Equals(knownName, GameData.PlayerStats.MyName,
                         StringComparison.OrdinalIgnoreCase));
                if (isPlayer && GameData.CurrentCharacterSlot != null)
                {
                    string saved = GameData.CurrentCharacterSlot.CharClass;
                    if (IsKnownClass(saved))
                        return Canonical(saved);
                }

                string simName = !string.IsNullOrWhiteSpace(knownName)
                    ? knownName : (stats != null ? stats.MyName : null);
                if (string.IsNullOrWhiteSpace(simName))
                    return null;

                float now = Time.realtimeSinceStartup;
                CacheEntry cache;
                if (SimClassCache.TryGetValue(simName, out cache) &&
                    now < cache.ExpiresAt)
                    return cache.ClassName;

                string name = null;
                if (FOTFSimPlayerRoster.IsClassMember(simName, "Lightbringer"))
                    name = "Lightbringer";
                else if (FOTFSimPlayerRoster.IsClassMember(simName, "Emberweaver"))
                    name = "Emberweaver";
                else if (FOTFSimPlayerRoster.IsClassMember(simName, "Blightcaller"))
                    name = "Blightcaller";
                // Never grow the cache indefinitely as temporary encounter
                // names and fresh SimPlayers pass through UI resolvers.
                if (SimClassCache.Count >= 128)
                    SimClassCache.Clear();
                SimClassCache[simName] = new CacheEntry
                {
                    ClassName = name,
                    ExpiresAt = now + 5f
                };
                return name;
            }
            catch
            {
                // This bridge must never prevent a UI bar from rendering.
                return null;
            }
        }

        private static bool IsKnownClass(string name)
        {
            return string.Equals(name, "Blightcaller", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "Emberweaver", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "Lightbringer", StringComparison.OrdinalIgnoreCase);
        }

        private static string Canonical(string name)
        {
            if (string.Equals(name, "Lightbringer", StringComparison.OrdinalIgnoreCase))
                return "Lightbringer";
            return string.Equals(name, "Emberweaver", StringComparison.OrdinalIgnoreCase)
                ? "Emberweaver" : "Blightcaller";
        }
    }
}
