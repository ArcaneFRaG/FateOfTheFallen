using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;

namespace FateOfTheFallen
{
    /// <summary>
    /// Scoped repair for threat registration on all runtime-built FOTF
    /// world bosses.
    ///
    /// We deliberately do NOT replace Erenshor's native aggro system.
    /// If a player-side attacker (player or SimPlayer) damages the Colossus
    /// and is missing from the NPC's native AggroTable, we call NPC.AggroOn()
    /// once to register that Character. Native Erenshor hate/threat handling
    /// then remains authoritative for target switching.
    /// </summary>
    internal static class WorldBossThreatBridge
    {
        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        internal static void RegisterAttackerIfMissing(
            Character target,
            Character attacker)
        {
            if (target == null ||
                attacker == null ||
                !WorldBossHitReactionUtility.IsCustomWorldBoss(
                    target) ||
                !IsValidPlayerSideCharacter(
                    attacker))
            {
                return;
            }

            NPC bossNpc =
                target.MyNPC;

            if (bossNpc == null)
            {
                bossNpc =
                    target.GetComponent<NPC>();
            }

            if (bossNpc == null)
            {
                return;
            }

            if (NativeAggroTableContains(
                    bossNpc,
                    attacker))
            {
                return;
            }

            try
            {
                bossNpc.AggroOn(
                    attacker);

                Plugin.NativeLog.LogInfo(
                    "[WorldBoss] native aggro registered [" +
                    GetCharacterName(
                        attacker) +
                    "] on FOTF world boss [" +
                    GetCharacterName(
                        target) +
                    "].");
            }
            catch (Exception exception)
            {
                Plugin.NativeLog.LogError(
                    "[WorldBoss] failed to register native aggro for [" +
                    GetCharacterName(
                        attacker) +
                    "]: " +
                    exception);
            }
        }


        private static bool NativeAggroTableContains(
            NPC npc,
            Character attacker)
        {
            if (npc == null ||
                attacker == null)
            {
                return false;
            }

            try
            {
                FieldInfo tableField =
                    typeof(NPC).GetField(
                        "AggroTable",
                        InstanceFlags);

                object tableObject =
                    tableField != null
                        ? tableField.GetValue(
                            npc)
                        : null;

                IEnumerable enumerable =
                    tableObject as IEnumerable;

                if (enumerable == null)
                {
                    return false;
                }

                foreach (object slot in enumerable)
                {
                    if (slot == null)
                    {
                        continue;
                    }

                    Type slotType =
                        slot.GetType();

                    FieldInfo playerField =
                        slotType.GetField(
                            "Player",
                            InstanceFlags);

                    PropertyInfo playerProperty =
                        slotType.GetProperty(
                            "Player",
                            InstanceFlags);

                    object playerValue =
                        playerField != null
                            ? playerField.GetValue(
                                slot)
                            : playerProperty != null
                                ? playerProperty.GetValue(
                                    slot,
                                    null)
                                : null;

                    if (ReferenceEquals(
                            playerValue,
                            attacker))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Treat an unreadable table as "not registered" and let the
                // native AggroOn entry point repair it.
            }

            return false;
        }


        private static bool IsValidPlayerSideCharacter(
            Character character)
        {
            if (character == null ||
                !character.Alive ||
                character.MyStats == null ||
                character.MyStats.CurrentHP <= 0)
            {
                return false;
            }

            return
                character.MyFaction ==
                    Character.Faction.Player ||
                character.MyFaction ==
                    Character.Faction.PC;
        }


        private static string GetCharacterName(
            Character character)
        {
            if (character == null)
            {
                return "<null>";
            }

            if (character.MyStats != null &&
                !string.IsNullOrEmpty(
                    character.MyStats.MyName))
            {
                return
                    character.MyStats.MyName;
            }

            if (character.MyNPC != null &&
                !string.IsNullOrEmpty(
                    character.MyNPC.NPCName))
            {
                return
                    character.MyNPC.NPCName;
            }

            return
                character.name;
        }
    }


    [HarmonyPatch(
        typeof(Character),
        "DamageMe",
        new Type[]
        {
            typeof(int),
            typeof(bool),
            typeof(GameData.DamageType),
            typeof(Character),
            typeof(bool),
            typeof(bool),
            typeof(int)
        })]
    internal static class Patch_WorldBossRegisterPhysicalThreat
    {
        [HarmonyPrefix]
        private static void Prefix(
            Character __instance,
            Character _attacker)
        {
            WorldBossThreatBridge.RegisterAttackerIfMissing(
                __instance,
                _attacker);
        }
    }


    [HarmonyPatch(
        typeof(Character),
        "MagicDamageMe",
        new Type[]
        {
            typeof(int),
            typeof(bool),
            typeof(GameData.DamageType),
            typeof(Character),
            typeof(float),
            typeof(int)
        })]
    internal static class Patch_WorldBossRegisterMagicThreat
    {
        [HarmonyPrefix]
        private static void Prefix(
            Character __instance,
            Character _attacker)
        {
            WorldBossThreatBridge.RegisterAttackerIfMissing(
                __instance,
                _attacker);
        }
    }


    [HarmonyPatch(
        typeof(Character),
        "BleedDamageMe",
        new Type[]
        {
            typeof(int),
            typeof(bool),
            typeof(Character)
        })]
    internal static class Patch_WorldBossRegisterBleedThreat
    {
        [HarmonyPrefix]
        private static void Prefix(
            Character __instance,
            Character _attacker)
        {
            WorldBossThreatBridge.RegisterAttackerIfMissing(
                __instance,
                _attacker);
        }
    }
}
