using System;
using HarmonyLib;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Prevents the Blight-Touched Colossus custom rig from being driven into
    /// native per-hit Hurt/Stunned reactions while it is being attacked.
    ///
    /// Damage, mitigation, aggro, combat credit and death handling remain
    /// native. This patch suppresses only the cosmetic reaction state that can
    /// destabilize the large custom humanoid rig under raid-scale hit volume.
    /// </summary>
    internal static class WorldBossHitReactionUtility
    {
        internal static bool IsBlightTouchedColossus(
            Character character)
        {
            if (character == null)
            {
                return false;
            }

            NPC npc =
                character.MyNPC;

            if (npc == null)
            {
                npc =
                    character.GetComponent<NPC>();
            }

            if (npc == null)
            {
                return false;
            }

            WorldBossIdentity worldBossIdentity =
                npc.GetComponent<WorldBossIdentity>();

            if (worldBossIdentity != null &&
                string.Equals(
                    worldBossIdentity.BossId,
                    WorldBossCatalog.BlightTouchedColossus.Id,
                    StringComparison.Ordinal))
            {
                return true;
            }

            return
                string.Equals(
                    npc.NPCName,
                    WorldBossCatalog.BlightTouchedColossus.DisplayName,
                    StringComparison.Ordinal) ||
                string.Equals(
                    npc.NPCName,
                    "Blight-Touched Colossus",
                    StringComparison.Ordinal);
        }

        internal static bool IsNhalSera(
            Character character)
        {
            if (character == null)
            {
                return false;
            }

            if (NhalSeraEncounterRuntime.IsCurrentBossCharacter(
                    character))
            {
                return true;
            }

            NPC npc =
                character.MyNPC;

            if (npc == null)
            {
                npc =
                    character.GetComponent<NPC>();
            }

            if (npc == null)
            {
                return false;
            }

            return
                string.Equals(
                    npc.NPCName,
                    "Nhal'Sera",
                    StringComparison.OrdinalIgnoreCase);
        }


        internal static bool IsVarkesh(
            Character character)
        {
            if (character == null)
            {
                return false;
            }

            if (VarkeshEncounterRuntime.IsCurrentBossCharacter(
                    character))
            {
                return true;
            }

            NPC npc =
                character.MyNPC;

            if (npc == null)
            {
                npc =
                    character.GetComponent<NPC>();
            }

            if (npc == null)
            {
                return false;
            }

            return
                string.Equals(
                    npc.NPCName,
                    WorldBossCatalog.Varkesh.DisplayName,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    npc.NPCName,
                    "Varkesh the Unburied",
                    StringComparison.OrdinalIgnoreCase);
        }


        internal static bool IsGorath(
            Character character)
        {
            if (character == null)
            {
                return false;
            }

            if (GorathEncounterRuntime.IsCurrentBossCharacter(
                    character))
            {
                return true;
            }

            NPC npc =
                character.MyNPC;

            if (npc == null)
            {
                npc =
                    character.GetComponent<NPC>();
            }

            return
                npc != null &&
                string.Equals(
                    npc.NPCName,
                    WorldBossCatalog.Gorath.DisplayName,
                    StringComparison.OrdinalIgnoreCase);
        }


        internal static bool SuppressPerHitReaction(
            Character character)
        {
            return
                IsCustomWorldBoss(
                    character) ||
                IsVarkesh(
                    character);
        }


        internal static bool IsCustomWorldBoss(
            Character character)
        {
            return
                IsBlightTouchedColossus(
                    character) ||
                IsNhalSera(
                    character) ||
                IsGorath(
                    character);
        }


        internal static void ClearNativeHitReaction(
            Character character)
        {
            if (character == null)
            {
                return;
            }

            Animator animator =
                character.GetMyAnim();

            if (animator == null)
            {
                animator =
                    character.GetComponentInChildren<Animator>(
                        true);
            }

            if (animator == null)
            {
                return;
            }

            if (IsVarkesh(
                    character) ||
                IsGorath(
                    character))
            {
                try
                {
                    animator.applyRootMotion =
                        false;
                }
                catch
                {
                }
            }

            try
            {
                animator.ResetTrigger(
                    "Hurt");
            }
            catch
            {
            }

            try
            {
                animator.SetBool(
                    "Stunned",
                    false);
            }
            catch
            {
            }
        }
    }


    /// <summary>
    /// Physical/direct Character.DamageMe calls expose the native animation
    /// toggle directly, so turn that off before the original method executes.
    /// </summary>
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
    internal static class Patch_WorldBossSuppressDamageHitReaction
    {
        [HarmonyPrefix]
        private static void Prefix(
            Character __instance,
            ref bool _animEffect)
        {
            if (!WorldBossHitReactionUtility.SuppressPerHitReaction(
                    __instance))
            {
                return;
            }

            _animEffect =
                false;

            WorldBossHitReactionUtility.ClearNativeHitReaction(
                __instance);
        }

        [HarmonyPostfix]
        private static void Postfix(
            Character __instance)
        {
            if (!WorldBossHitReactionUtility.SuppressPerHitReaction(
                    __instance))
            {
                return;
            }

            WorldBossHitReactionUtility.ClearNativeHitReaction(
                __instance);
        }
    }


    /// <summary>
    /// MagicDamageMe has no animation-effect argument. Clear any native Hurt
    /// trigger immediately around the original call so magic spam cannot keep
    /// knocking the custom rig into incompatible reaction states.
    /// </summary>
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
    internal static class Patch_WorldBossSuppressMagicHitReaction
    {
        [HarmonyPrefix]
        private static void Prefix(
            Character __instance)
        {
            if (!WorldBossHitReactionUtility.SuppressPerHitReaction(
                    __instance))
            {
                return;
            }

            WorldBossHitReactionUtility.ClearNativeHitReaction(
                __instance);
        }

        [HarmonyPostfix]
        private static void Postfix(
            Character __instance)
        {
            if (!WorldBossHitReactionUtility.SuppressPerHitReaction(
                    __instance))
            {
                return;
            }

            WorldBossHitReactionUtility.ClearNativeHitReaction(
                __instance);
        }
    }


    /// <summary>
    /// Keep periodic bleed damage from introducing a Hurt/Stunned reaction
    /// state on the raid-boss rig.
    /// </summary>
    [HarmonyPatch(
        typeof(Character),
        "BleedDamageMe",
        new Type[]
        {
            typeof(int),
            typeof(bool),
            typeof(Character)
        })]
    internal static class Patch_WorldBossSuppressBleedHitReaction
    {
        [HarmonyPrefix]
        private static void Prefix(
            Character __instance)
        {
            if (!WorldBossHitReactionUtility.SuppressPerHitReaction(
                    __instance))
            {
                return;
            }

            WorldBossHitReactionUtility.ClearNativeHitReaction(
                __instance);
        }

        [HarmonyPostfix]
        private static void Postfix(
            Character __instance)
        {
            if (!WorldBossHitReactionUtility.SuppressPerHitReaction(
                    __instance))
            {
                return;
            }

            WorldBossHitReactionUtility.ClearNativeHitReaction(
                __instance);
        }
    }

    /// <summary>
    /// The native humanoid animation controller emits FootL / FootR animation
    /// events. A runtime-built custom creature does not have the complete
    /// serialized native footstep/audio state those Character methods expect,
    /// so the original methods can throw NullReferenceException.
    ///
    /// Footstep events are cosmetic. Skip them only for the Blight-Touched
    /// Colossus and leave every native creature unchanged.
    /// </summary>
    [HarmonyPatch(
        typeof(Character),
        nameof(Character.FootL))]
    internal static class Patch_WorldBossSuppressFootL
    {
        [HarmonyPrefix]
        private static bool Prefix(
            Character __instance)
        {
            return
                !WorldBossHitReactionUtility.IsCustomWorldBoss(
                    __instance);
        }
    }


    [HarmonyPatch(
        typeof(Character),
        nameof(Character.FootR))]
    internal static class Patch_WorldBossSuppressFootR
    {
        [HarmonyPrefix]
        private static bool Prefix(
            Character __instance)
        {
            return
                !WorldBossHitReactionUtility.IsCustomWorldBoss(
                    __instance);
        }
    }


    [HarmonyPatch(
        typeof(Character),
        nameof(Character.FootLScuff))]
    internal static class Patch_WorldBossSuppressFootLScuff
    {
        [HarmonyPrefix]
        private static bool Prefix(
            Character __instance)
        {
            return
                !WorldBossHitReactionUtility.IsCustomWorldBoss(
                    __instance);
        }
    }


    [HarmonyPatch(
        typeof(Character),
        nameof(Character.FootRScuff))]
    internal static class Patch_WorldBossSuppressFootRScuff
    {
        [HarmonyPrefix]
        private static bool Prefix(
            Character __instance)
        {
            return
                !WorldBossHitReactionUtility.IsCustomWorldBoss(
                    __instance);
        }
    }

}
