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
        // UPDATE
        // ============================================================

        internal static void Update(Stats stats)
        {
            if (stats == null || stats.StatusEffects == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
            {
                return;
            }

            for (int i = ActiveDots.Count - 1; i >= 0; i--)
            {
                DotState state = ActiveDots[i];

                if (state == null || state.Stats != stats)
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
                        .GetAcceleratedDecayMultiplier(ownerSkills);

                if (multiplier <= 1f)
                {
                    continue;
                }

                float interval = 3f / multiplier;

                state.NextTickTime -= deltaTime;

                while (state.NextTickTime <= 0f)
                {
                    if (!IsValid(state))
                    {
                        break;
                    }

                    statusEffect =
                        stats.StatusEffects[state.Slot];

                    PerformExtraTick(
                        stats,
                        statusEffect);

                    state.NextTickTime += interval;

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

        internal static void TrackStatusEffects(Stats stats)
        {
            if (stats == null || stats.StatusEffects == null)
            {
                return;
            }

            for (int i = 0; i < stats.StatusEffects.Length; i++)
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
        // ADD / UPDATE TRACKED DOT
        // ============================================================

        private static void Upsert(
            Stats stats,
            int slot,
            StatusEffect statusEffect)
        {
            for (int i = 0; i < ActiveDots.Count; i++)
            {
                DotState existing = ActiveDots[i];

                if (existing.Stats != stats ||
                    existing.Slot != slot)
                {
                    continue;
                }

                if (existing.Effect != statusEffect.Effect)
                {
                    existing.Effect =
                        statusEffect.Effect;

                    existing.NextTickTime =
                        GetInitialInterval(statusEffect);
                }

                return;
            }

            ActiveDots.Add(
                new DotState
                {
                    Stats = stats,
                    Slot = slot,
                    Effect = statusEffect.Effect,
                    NextTickTime =
                        GetInitialInterval(statusEffect)
                });
        }


        // ============================================================
        // NATIVE TICK NOTIFICATION
        // ============================================================

        internal static void OnNativeTick(Stats stats)
        {
            if (stats == null)
            {
                return;
            }

            for (int i = ActiveDots.Count - 1; i >= 0; i--)
            {
                DotState state = ActiveDots[i];

                if (state == null || state.Stats != stats)
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
                    GetInitialInterval(statusEffect);
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
                    .GetAcceleratedDecayMultiplier(ownerSkills);

            if (multiplier <= 1f)
            {
                return 3f;
            }

            return 3f / multiplier;
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

            if (!BlightcallerAscensions.IsBlightcallerDoT(effect))
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
            // NPC DAMAGE OVERRIDE
            // --------------------------------------------------------

            if (UnityEngine.Random.Range(0f, 10f) > 6.5f &&
                stats.Myself.isNPC &&
                stats.Myself.MyNPC != null &&
                !stats.Myself.MyNPC.SimPlayer)
            {
                damageMultiplier = 1f;
            }


            // --------------------------------------------------------
            // DAMAGE
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
                    .GetPotentAfflictionMultiplier(ownerSkills);

            if (potentMultiplier > 1f)
            {
                damage =
                    Mathf.RoundToInt(
                        damage *
                        potentMultiplier);
            }


            // --------------------------------------------------------
            // APPLY DAMAGE
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
        // CLEANUP
        // ============================================================

        private static void RemoveInvalidEntries(
            Stats stats)
        {
            for (int i = ActiveDots.Count - 1; i >= 0; i--)
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