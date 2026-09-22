using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace FateOfTheFallen
{
    // ================================================================
    // PESTILENT REJUVENATION
    // ================================================================
    //
    // Native DoT ticks are detected from Stats.TickEffects().
    //
    // Accelerated Decay ticks call TryPestilentRejuvenation()
    // directly from the custom scheduler.
    //
    // This ensures ALL Blightcaller DoT ticks can proc the Ascension.
    // ================================================================

    [HarmonyPatch(typeof(Stats), "TickEffects")]
    internal static class Patch_BlightcallerPestilentRejuvenation
    {
        private static readonly MethodInfo DamageMeMethod =
            AccessTools.Method(
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
                });

        private static readonly MethodInfo NativeTickProcMethod =
            AccessTools.Method(
                typeof(Patch_BlightcallerPestilentRejuvenation),
                nameof(TryNativeTickProc));


        // ============================================================
        // NATIVE DOT TICK TRANSPILER
        // ============================================================

        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes =
                new List<CodeInstruction>(
                    instructions);

            bool injected =
                false;

            for (int i = 0;
                     i < codes.Count;
                     i++)
            {
                CodeInstruction instruction =
                    codes[i];

                if (!injected &&
                    instruction.opcode == OpCodes.Callvirt &&
                    instruction.operand is MethodInfo method &&
                    method == DamageMeMethod)
                {
                    /*
                     * Native TickEffects has just called:
                     *
                     * this.Myself.DamageMe(...)
                     *
                     * Existing native locals:
                     *
                     * V_1 = StatusEffect slot
                     * V_6 = calculated DoT damage
                     */

                    codes.InsertRange(
                        i + 1,
                        new[]
                        {
                            new CodeInstruction(
                                OpCodes.Ldarg_0),

                            new CodeInstruction(
                                OpCodes.Ldloc_1),

                            new CodeInstruction(
                                OpCodes.Ldloc_S,
                                6),

                            new CodeInstruction(
                                OpCodes.Call,
                                NativeTickProcMethod)
                        });

                    injected =
                        true;

                    break;
                }
            }

            return codes;
        }


        // ============================================================
        // NATIVE TICK ADAPTER
        // ============================================================

        private static void TryNativeTickProc(
            Stats targetStats,
            int statusEffectIndex,
            int damage)
        {
            if (targetStats == null ||
                targetStats.StatusEffects == null)
            {
                return;
            }

            if (statusEffectIndex < 0 ||
                statusEffectIndex >=
                    targetStats.StatusEffects.Length)
            {
                return;
            }

            StatusEffect statusEffect =
                targetStats.StatusEffects[
                    statusEffectIndex];

            TryPestilentRejuvenation(
                statusEffect,
                damage);
        }


        // ============================================================
        // SHARED PROC HANDLER
        // ============================================================

        internal static void TryPestilentRejuvenation(
            StatusEffect statusEffect,
            int damage)
        {
            if (damage <= 0)
            {
                return;
            }

            if (statusEffect == null ||
                statusEffect.Effect == null)
            {
                return;
            }

            Spell effect =
                statusEffect.Effect;


            // --------------------------------------------------------
            // BLIGHTCALLER DOT ONLY
            // --------------------------------------------------------

            if (!BlightcallerAscensions.IsBlightcallerDoT(
                    effect))
            {
                return;
            }


            // --------------------------------------------------------
            // OWNER
            // --------------------------------------------------------

            Character owner =
                statusEffect.Owner;

            if (owner == null ||
                owner.MyStats == null ||
                owner.MySkills == null)
            {
                return;
            }


            // --------------------------------------------------------
            // ASCENSION CHECK
            // --------------------------------------------------------

            if (!BlightcallerAscensions
                    .HasPestilentRejuvenation(
                        owner.MySkills))
            {
                return;
            }


            // --------------------------------------------------------
            // GET ASCENSION RANK
            // --------------------------------------------------------

            int rank =
                BlightcallerAscensions
                    .GetPestilentRejuvenationRank(
                        owner.MySkills);

            if (rank <= 0)
            {
                return;
            }


            // --------------------------------------------------------
            // 2% PROC CHANCE PER RANK
            // --------------------------------------------------------

            float procChance =
                rank * 0.02f;

            if (UnityEngine.Random.value >=
                procChance)
            {
                return;
            }

            // --------------------------------------------------------
            // MANA VALUES
            // --------------------------------------------------------

            Stats ownerStats =
                owner.MyStats;

            int maxMana =
                ownerStats.GetCurrentMaxMana();

            if (maxMana <= 0)
            {
                return;
            }

            int currentMana =
                ownerStats.GetCurrentMana();

            if (currentMana >= maxMana)
            {
                return;
            }


            // --------------------------------------------------------
            // RESTORE 10% MAX MANA
            // --------------------------------------------------------

            int restoration =
                UnityEngine.Mathf.RoundToInt(
                    maxMana *
                    0.10f);

            if (restoration <= 0)
            {
                return;
            }

            ownerStats.CurrentMana =
                UnityEngine.Mathf.Min(
                    currentMana +
                    restoration,
                    maxMana);
        }
    }
}