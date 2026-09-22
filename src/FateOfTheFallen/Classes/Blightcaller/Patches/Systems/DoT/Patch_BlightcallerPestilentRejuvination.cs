using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace FateOfTheFallen
{
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

        private static readonly MethodInfo TryPestilentRejuvenationMethod =
            AccessTools.Method(
                typeof(Patch_BlightcallerPestilentRejuvenation),
                nameof(TryPestilentRejuvenation));

        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes =
                new List<CodeInstruction>(instructions);

            bool injected = false;

            for (int i = 0; i < codes.Count; i++)
            {
                CodeInstruction instruction =
                    codes[i];

                if (!injected &&
                    instruction.opcode == OpCodes.Callvirt &&
                    instruction.operand is MethodInfo method &&
                    method == DamageMeMethod)
                {
                    /*
                     * The native code has just executed:
                     *
                     * this.Myself.DamageMe(
                     *     num3,
                     *     ...);
                     *
                     * At this point:
                     *
                     * V_1 = StatusEffect index
                     * V_6 = num3
                     *
                     * Inject:
                     *
                     * TryPestilentRejuvenation(
                     *     this,
                     *     V_1,
                     *     V_6);
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
                                TryPestilentRejuvenationMethod)
                        });

                    injected = true;
                    break;
                }
            }

            return codes;
        }


        // ============================================================
        // PESTILENT REJUVENATION
        // ============================================================

        private static void TryPestilentRejuvenation(
            Stats targetStats,
            int statusEffectIndex,
            int damage)
        {
            /*
             * The native TickEffects() already guarantees that
             * this code is reached after a positive DoT damage
             * amount has been calculated and DamageMe() has been
             * called.
             *
             * Still validate everything here so this helper is
             * completely self-contained.
             */

            if (targetStats == null)
            {
                return;
            }

            if (damage <= 0)
            {
                return;
            }

            if (targetStats.StatusEffects == null)
            {
                return;
            }

            if (statusEffectIndex < 0 ||
                statusEffectIndex >= targetStats.StatusEffects.Length)
            {
                return;
            }

            StatusEffect statusEffect =
                targetStats.StatusEffects[statusEffectIndex];

            if (statusEffect == null)
            {
                return;
            }

            Spell effect =
                statusEffect.Effect;

            if (effect == null)
            {
                return;
            }


            // --------------------------------------------------------
            // BLIGHTCALLER DOT ONLY
            // --------------------------------------------------------

            if (!BlightcallerAscensions.IsBlightcallerDoT(effect))
            {
                return;
            }


            // --------------------------------------------------------
            // GET BLIGHTCALLER OWNER
            // --------------------------------------------------------

            Character owner =
                statusEffect.Owner;

            if (owner == null)
            {
                return;
            }

            if (owner.MyStats == null)
            {
                return;
            }

            if (owner.MySkills == null)
            {
                return;
            }


            // --------------------------------------------------------
            // CHECK ASCENSION
            // --------------------------------------------------------

            if (!BlightcallerAscensions.HasPestilentRejuvenation(
                owner.MySkills))
            {
                return;
            }


            // --------------------------------------------------------
            // 2% PROC CHANCE
            // --------------------------------------------------------

            if (UnityEngine.Random.value > 0.02f)
            {
                return;
            }


            // --------------------------------------------------------
            // RESTORE 5% MAX MANA
            // --------------------------------------------------------

            Stats ownerStats =
                owner.MyStats;

            int maxMana =
                ownerStats.GetCurrentMaxMana();

            if (maxMana <= 0)
            {
                return;
            }

            int restoration =
                UnityEngine.Mathf.RoundToInt(
                    maxMana * 0.05f);

            if (restoration <= 0)
            {
                return;
            }


            // --------------------------------------------------------
            // DO NOT RESTORE IF ALREADY FULL
            // --------------------------------------------------------

            int currentMana =
                ownerStats.GetCurrentMana();

            if (currentMana >= maxMana)
            {
                return;
            }


            // --------------------------------------------------------
            // APPLY RESTORATION
            // --------------------------------------------------------

            ownerStats.CurrentMana =
                UnityEngine.Mathf.Min(
                    currentMana + restoration,
                    maxMana);
        }
    }
}