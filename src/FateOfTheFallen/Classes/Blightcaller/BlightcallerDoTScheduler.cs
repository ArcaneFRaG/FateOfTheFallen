using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class BlightcallerDotScheduler
    {
        private sealed class DotState
        {
            internal Stats Stats;
            internal int Slot;
            internal Spell Effect;
            internal float NextTickTime;
        }

        private static readonly List<DotState> ActiveDots =
            new List<DotState>();


        // ============================================================
        // UPDATE ONE STATS INSTANCE
        // ============================================================

        internal static void Update(
            Stats stats)
        {
            if (stats == null ||
                stats.StatusEffects == null)
            {
                return;
            }

            float deltaTime =
                Time.deltaTime;

            if (deltaTime <= 0f)
            {
                return;
            }

            /*
             * IMPORTANT:
             *
             * Only process DoTs belonging to THIS Stats instance.
             *
             * The previous implementation processed the entire global
             * ActiveDots list every time ANY Stats.Update() ran.
             *
             * That caused the same DoT timer to advance once per NPC,
             * player, pet, etc. every frame.
             */

            for (int i = ActiveDots.Count - 1;
                     i >= 0;
                     i--)
            {
                DotState state =
                    ActiveDots[i];

                if (state == null ||
                    state.Stats != stats)
                {
                    continue;
                }

                if (!IsValid(state))
                {
                    ActiveDots.RemoveAt(i);
                    continue;
                }

                StatusEffect statusEffect =
                    stats.StatusEffects[state.Slot];

                UseSkill ownerSkills =
                    statusEffect.Owner != null
                        ? statusEffect.Owner.MySkills
                        : null;

                float multiplier =
                    BlightcallerAscensions
                        .GetAcceleratedDecayMultiplier(
                            ownerSkills);

                /*
                 * Rank 0 means native timing only.
                 */
                if (multiplier <= 1f)
                {
                    continue;
                }

                /*
                 * Native TickEffects() occurs every ~3 seconds.
                 *
                 * We only generate the ADDITIONAL ticks.
                 *
                 * Rank 1:
                 *     33% faster
                 *     3 / 1.33 = ~2.26 seconds
                 *
                 * Rank 2:
                 *     66% faster
                 *     3 / 1.66 = ~1.81 seconds
                 *
                 * Rank 3:
                 *     99% faster
                 *     3 / 1.99 = ~1.51 seconds
                 */
                float interval =
                    3f / multiplier;

                state.NextTickTime -=
                    deltaTime;

                /*
                 * Normally this executes at most once.
                 *
                 * The while loop handles a large frame/pause safely.
                 */
                while (state.NextTickTime <= 0f)
                {
                    /*
                     * Re-check before applying the tick because
                     * the previous tick may have killed the target
                     * or otherwise removed the effect.
                     */
                    if (!IsValid(state))
                    {
                        break;
                    }

                    statusEffect =
                        stats.StatusEffects[state.Slot];

                    PerformExtraTick(
                        stats,
                        statusEffect);

                    state.NextTickTime +=
                        interval;

                    /*
                     * Do not allow a destroyed/expired effect to
                     * continue generating extra ticks.
                     */
                    if (!IsValid(state))
                    {
                        break;
                    }
                }
            }
        }


        // ============================================================
        // TRACK ACTIVE DOTS
        // ============================================================

        internal static void TrackStatusEffects(
            Stats stats)
        {
            if (stats == null ||
                stats.StatusEffects == null)
            {
                return;
            }

            for (int i = 0;
                     i < stats.StatusEffects.Length;
                     i++)
            {
                StatusEffect statusEffect =
                    stats.StatusEffects[i];

                if (statusEffect == null ||
                    statusEffect.Effect == null)
                {
                    continue;
                }

                if (!BlightcallerAscensions.IsBlightcallerDoT(
                        statusEffect.Effect))
                {
                    continue;
                }

                if (statusEffect.Duration <= 0f)
                {
                    continue;
                }

                Upsert(
                    stats,
                    i,
                    statusEffect);
            }

            RemoveInvalidEntries(stats);
        }


        // ============================================================
        // ADD / REFRESH
        // ============================================================

        private static void Upsert(
            Stats stats,
            int slot,
            StatusEffect statusEffect)
        {
            for (int i = 0;
                     i < ActiveDots.Count;
                     i++)
            {
                DotState existing =
                    ActiveDots[i];

                if (existing.Stats != stats ||
                    existing.Slot != slot)
                {
                    continue;
                }

                /*
                 * The same status slot is now occupied by a
                 * different spell.
                 */
                if (existing.Effect != statusEffect.Effect)
                {
                    existing.Effect =
                        statusEffect.Effect;

                    existing.NextTickTime =
                        GetInitialInterval(
                            statusEffect);
                }

                return;
            }

            ActiveDots.Add(
                new DotState
                {
                    Stats = stats,
                    Slot = slot,
                    Effect = statusEffect.Effect,

                    /*
                     * Start the accelerated timer from zero.
                     *
                     * Native TickEffects() remains responsible for
                     * the normal first tick.
                     */
                    NextTickTime =
                        GetInitialInterval(
                            statusEffect)
                });
        }


        // ============================================================
        // NATIVE TICK RESET
        // ============================================================

        internal static void OnNativeTick(
            Stats stats)
        {
            if (stats == null)
            {
                return;
            }

            /*
             * Native TickEffects() has just performed the normal
             * DoT tick.
             *
             * Reset our additional-tick timers so that the next
             * accelerated tick happens AFTER the native tick.
             *
             * This prevents the scheduler from fighting the native
             * 3-second tick timer.
             */

            for (int i = ActiveDots.Count - 1;
                     i >= 0;
                     i--)
            {
                DotState state =
                    ActiveDots[i];

                if (state == null ||
                    state.Stats != stats)
                {
                    continue;
                }

                if (!IsValid(state))
                {
                    ActiveDots.RemoveAt(i);
                    continue;
                }

                StatusEffect statusEffect =
                    stats.StatusEffects[state.Slot];

                state.NextTickTime =
                    GetInitialInterval(
                        statusEffect);
            }
        }


        // ============================================================
        // INITIAL INTERVAL
        // ============================================================

        private static float GetInitialInterval(
            StatusEffect statusEffect)
        {
            if (statusEffect == null)
            {
                return 3f;
            }

            UseSkill ownerSkills =
                statusEffect.Owner != null
                    ? statusEffect.Owner.MySkills
                    : null;

            float multiplier =
                BlightcallerAscensions
                    .GetAcceleratedDecayMultiplier(
                        ownerSkills);

            if (multiplier <= 1f)
            {
                return 3f;
            }

            return
                3f / multiplier;
        }


        // ============================================================
        // EXTRA DOT TICK
        // ============================================================

        private static void PerformExtraTick(
            Stats stats,
            StatusEffect statusEffect)
        {
            if (stats == null ||
                statusEffect == null ||
                statusEffect.Effect == null)
            {
                return;
            }

            Spell effect =
                statusEffect.Effect;

            if (!BlightcallerAscensions.IsBlightcallerDoT(
                    effect))
            {
                return;
            }

            if (statusEffect.Duration <= 0f)
            {
                return;
            }

            if (effect.TargetDamage <= 0)
            {
                return;
            }


            // --------------------------------------------------------
            // RESIST
            // --------------------------------------------------------

            float resist =
                stats.CheckResist(
                    effect.MyDamageType,
                    0f,
                    stats.Myself);

            float damageMultiplier =
                1f - resist;

            damageMultiplier =
                UnityEngine.Random.Range(
                    damageMultiplier * 0.5f,
                    damageMultiplier * 1.25f);

            if (damageMultiplier > 1f)
            {
                damageMultiplier = 1f;
            }

            if (damageMultiplier < 0f)
            {
                damageMultiplier = 0f;
            }


            // --------------------------------------------------------
            // NATIVE NPC RESIST OVERRIDE
            // --------------------------------------------------------

            if (UnityEngine.Random.Range(
                    0f,
                    10f) > 6.5f &&
                stats.Myself.isNPC &&
                stats.Myself.MyNPC != null &&
                !stats.Myself.MyNPC.SimPlayer)
            {
                damageMultiplier = 1f;
            }


            // --------------------------------------------------------
            // BASE DOT DAMAGE
            // --------------------------------------------------------

            int damage =
                Mathf.RoundToInt(
                    (
                        (float)effect.TargetDamage +
                        (float)statusEffect.bonusDmg
                    ) *
                    damageMultiplier);


            // --------------------------------------------------------
            // POTENT AFFLICTION
            // --------------------------------------------------------

            UseSkill ownerSkills =
                statusEffect.Owner != null
                    ? statusEffect.Owner.MySkills
                    : null;

            float potentMultiplier =
                BlightcallerAscensions
                    .GetPotentAfflictionMultiplier(
                        ownerSkills);

            if (potentMultiplier > 1f)
            {
                damage =
                    Mathf.RoundToInt(
                        damage *
                        potentMultiplier);
            }


            // --------------------------------------------------------
            // DAMAGE APPLICATION
            // --------------------------------------------------------

            if (damage > 0)
            {
                stats.Myself.DamageMe(
                    damage,
                    statusEffect.fromPlayer,
                    effect.MyDamageType,
                    statusEffect.CreditDPS,
                    false,
                    false,
                    0);
            }
        }


        // ============================================================
        // VALIDATION
        // ============================================================

        private static bool IsValid(
            DotState state)
        {
            if (state == null ||
                state.Stats == null ||
                state.Stats.StatusEffects == null)
            {
                return false;
            }

            if (state.Slot < 0 ||
                state.Slot >=
                state.Stats.StatusEffects.Length)
            {
                return false;
            }

            StatusEffect statusEffect =
                state.Stats.StatusEffects[state.Slot];

            if (statusEffect == null ||
                statusEffect.Effect == null)
            {
                return false;
            }

            if (statusEffect.Effect != state.Effect)
            {
                return false;
            }

            if (!BlightcallerAscensions.IsBlightcallerDoT(
                    statusEffect.Effect))
            {
                return false;
            }

            if (statusEffect.Duration <= 0f)
            {
                return false;
            }

            if (statusEffect.Effect.TargetDamage <= 0)
            {
                return false;
            }

            return true;
        }


        // ============================================================
        // CLEANUP FOR ONE STATS INSTANCE
        // ============================================================

        private static void RemoveInvalidEntries(
            Stats stats)
        {
            for (int i = ActiveDots.Count - 1;
                     i >= 0;
                     i--)
            {
                DotState state =
                    ActiveDots[i];

                if (state == null)
                {
                    ActiveDots.RemoveAt(i);
                    continue;
                }

                if (state.Stats != stats)
                {
                    continue;
                }

                if (!IsValid(state))
                {
                    ActiveDots.RemoveAt(i);
                }
            }
        }


        // ============================================================
        // CLEAR
        // ============================================================

        internal static void Clear()
        {
            ActiveDots.Clear();
        }
    }
}