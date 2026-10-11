using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>Runtime-only Ascension mechanics. No new absorb pool or
    /// speculative effective healing entries are created.</summary>
    internal static class LightbringerAscensionRuntime
    {
        private sealed class DepletionCooldown { internal float NextAllowed; }
        private sealed class DirectDamageCast
        {
            internal Stats ExpectedTarget;
            internal Spell Spell;
            internal float ExpiresAt;
        }
        private sealed class DirectDamageQueue
        {
            internal readonly List<DirectDamageCast> Active = new List<DirectDamageCast>();
        }
        private static readonly ConditionalWeakTable<Stats, DepletionCooldown> GraceCooldowns =
            new ConditionalWeakTable<Stats, DepletionCooldown>();
        private static readonly ConditionalWeakTable<Character, DirectDamageQueue> DamageCasts =
            new ConditionalWeakTable<Character, DirectDamageQueue>();
        private static readonly ConditionalWeakTable<Character, DepletionCooldown> MercyCooldowns =
            new ConditionalWeakTable<Character, DepletionCooldown>();
        [ThreadStatic] private static bool _procHealing;
        private static Spell _healingTemplate;
        private static Spell _graceProc;
        private static Spell _mercyProc;
        private static int _graceLogs;
        private static int _mercyLogs;
        private static int _procTemplateWarnings;
        private static int _noEffectiveHealLogs;

        internal static int AdjustHealing(Spell spell, int amount, Character caster)
        {
            if (_procHealing || caster == null || caster.MyStats == null ||
                amount <= 0 || spell == null ||
                !LightbringerCatalog.IsLightbringerClass(caster.MyStats.CharacterClass)) return amount;
            int rank = LightbringerAscensions.GetRank(caster.MySkills,
                LightbringerAscensions.RestorativeDawnId);
            bool guiding = LightbringerSkillCatalog.HasCasterSkill(caster.MyStats,
                LightbringerSkillCatalog.GuidingLightId);
            if (rank == 0 && !guiding) return amount;
            LightbringerSpellDefinition definition;
            bool isLightbringerHeal = LightbringerSpellCatalog.TryGetDefinition(spell, out definition) &&
                definition != null &&
                (definition.Kind == LightbringerSpellKind.HybridHealShield ||
                 definition.Kind == LightbringerSpellKind.GroupHealShield);
            if (!isLightbringerHeal && !LightbringerNativeHealing.IsApproved(spell)) return amount;
            // Guiding Light stacks additively with Restorative Dawn's ranks.
            return Mathf.Max(amount, Mathf.RoundToInt(amount *
                (1f + 0.05f * rank + (guiding ? 0.05f : 0f))));
        }

        internal static Patch_LightbringerSanctumDiscipline.State DiscountManaCost(Spell spell, Character caster)
        {
            if (caster == null || caster.MyStats == null || spell == null ||
                !LightbringerCatalog.IsLightbringerClass(caster.MyStats.CharacterClass)) return null;
            int rank = LightbringerAscensions.GetRank(caster.MySkills,
                LightbringerAscensions.SanctumDisciplineId);
            if (rank <= 0) return null;
            LightbringerSpellDefinition definition;
            bool isClassSupport = LightbringerSpellCatalog.TryGetDefinition(spell, out definition) &&
                definition != null && definition.Kind != LightbringerSpellKind.Damage;
            if (!isClassSupport && !LightbringerNativeHealing.IsApproved(spell)) return null;
            if (spell.ManaCost <= 0) return null;
            var state = new Patch_LightbringerSanctumDiscipline.State
            {
                Spell = spell, Original = spell.ManaCost
            };
            spell.ManaCost = Mathf.Max(1, Mathf.CeilToInt(spell.ManaCost * (1f - 0.05f * rank)));
            return state;
        }

        internal static void RestoreManaCost(Patch_LightbringerSanctumDiscipline.State state)
        {
            if (state == null || state.Spell == null) return;
            state.Spell.ManaCost = state.Original;
        }

        internal static void OnWardDepleted(Character caster, Stats recipient, Spell wardSpell,
            int originalCapacity)
        {
            recipient = LightbringerAbsorbApi.Canonical(recipient);
            if (caster == null || caster.MyStats == null || recipient == null ||
                recipient.CurrentHP <= 0 || originalCapacity <= 0 ||
                !LightbringerCatalog.IsLightbringerClass(caster.MyStats.CharacterClass)) return;
            int rank = LightbringerAscensions.GetRank(caster.MySkills,
                LightbringerAscensions.LastingGraceId);
            if (rank <= 0) return;
            DepletionCooldown cooldown = GraceCooldowns.GetOrCreateValue(recipient);
            if (Time.time < cooldown.NextAllowed) return;
            int heal = Mathf.RoundToInt(originalCapacity * rank * 0.04f);
            if (heal <= 0) return;
            if (Heal(recipient, caster, heal, true))
            {
                cooldown.NextAllowed = Time.time + 20f;
                if (_graceLogs++ < 10)
                    Plugin.LogInfo("[LBAscension] Lasting Grace recipient=" + recipient.MyName +
                        " healing=" + heal + " ward=" + (wardSpell != null ? wardSpell.SpellName : "unknown"));
            }
        }

        internal static void OnDamageCastAccepted(Character caster, Stats expectedTarget,
            Spell spell, LightbringerSpellDefinition definition)
        {
            if (caster == null || caster.MyStats == null || spell == null || definition == null ||
                definition.Kind != LightbringerSpellKind.Damage ||
                LightbringerAscensions.GetRank(caster.MySkills,
                    LightbringerAscensions.JudgementsMercyId) <= 0) return;
            DirectDamageQueue queue = DamageCasts.GetOrCreateValue(caster);
            for (int i = queue.Active.Count - 1; i >= 0; i--)
                if (queue.Active[i].ExpiresAt < Time.time) queue.Active.RemoveAt(i);
            // A later cast must not erase attribution for a projectile already
            // in flight. Bound the queue in case no damage ever lands.
            if (queue.Active.Count >= 8) queue.Active.RemoveAt(0);
            queue.Active.Add(new DirectDamageCast
            {
                ExpectedTarget = LightbringerAbsorbApi.Canonical(expectedTarget),
                Spell = spell,
                ExpiresAt = Time.time + Mathf.Max(5f, definition.CastTime + 5f)
            });
        }

        internal static void OnMagicDamage(Character caster, Character victim, int actualDamage)
        {
            if (_procHealing || caster == null || victim == null || victim.MyStats == null ||
                actualDamage <= 0 || caster.MyStats == null ||
                !LightbringerCatalog.IsLightbringerClass(caster.MyStats.CharacterClass)) return;
            DirectDamageQueue queue;
            if (!DamageCasts.TryGetValue(caster, out queue)) return;
            DirectDamageCast state = null;
            for (int i = 0; i < queue.Active.Count; i++)
            {
                DirectDamageCast pending = queue.Active[i];
                if (Time.time > pending.ExpiresAt) continue;
                if (pending.ExpectedTarget != null &&
                    LightbringerAbsorbApi.Canonical(victim.MyStats) != pending.ExpectedTarget) continue;
                state = pending;
                queue.Active.RemoveAt(i);
                break;
            }
            if (state == null || state.Spell == null) return;
            LightbringerSpellDefinition definition;
            if (!LightbringerSpellCatalog.TryGetDefinition(state.Spell, out definition) ||
                definition.Kind != LightbringerSpellKind.Damage) return;
            // Spend the single-hit attribution even if on cooldown or no ally
            // is wounded. Old pending casts cannot trigger on a later hit.
            int rank = LightbringerAscensions.GetRank(caster.MySkills,
                LightbringerAscensions.JudgementsMercyId);
            if (rank <= 0) return;
            DepletionCooldown cooldown = MercyCooldowns.GetOrCreateValue(caster);
            if (Time.time < cooldown.NextAllowed) return;
            int amount = Mathf.RoundToInt(actualDamage * rank * 0.02f);
            if (amount <= 0) return;
            Character target = FindLowestInjuredMember(caster);
            if (target == null) return;
            if (Heal(target.MyStats, caster, amount, false))
            {
                cooldown.NextAllowed = Time.time + 4f;
                if (_mercyLogs++ < 10)
                    Plugin.LogInfo("[LBAscension] Judgement's Mercy spell=" + definition.Name +
                        " damage=" + actualDamage + " target=" + target.MyStats.MyName +
                        " healing=" + amount);
            }
        }

        private static Character FindLowestInjuredMember(Character caster)
        {
            Character selected = null;
            float lowest = 1f;
            foreach (Character member in LightbringerRuntime.GetCurrentGroupMembers(caster))
            {
                if (member == null || member.MyStats == null || member.MyStats.CurrentHP <= 0 ||
                    member.MyStats.CurrentMaxHP <= 0 ||
                    member.MyStats.CurrentHP >= member.MyStats.CurrentMaxHP) continue;
                if (caster.transform != null && member.transform != null &&
                    Vector3.Distance(caster.transform.position, member.transform.position) > 60f) continue;
                float fraction = (float)member.MyStats.CurrentHP / member.MyStats.CurrentMaxHP;
                if (fraction < lowest) { lowest = fraction; selected = member; }
            }
            return selected;
        }

        private static Spell GetProcHealSpell(bool grace)
        {
            Spell existing = grace ? _graceProc : _mercyProc;
            if (existing != null) return existing;
            if (_healingTemplate == null && GameData.SpellDatabase != null &&
                GameData.SpellDatabase.SpellDatabase != null)
            {
                // The native Healing record is not guaranteed to exist at
                // the moment the proc fires. Use the first verified direct
                // heal, falling back to the Lightbringer healing catalogue.
                string[] preferred = { "Healing", "Minor Healing", "Major Healing", "Supreme Healing", "Luminous Mending" };
                foreach (string name in preferred)
                {
                    foreach (Spell candidate in GameData.SpellDatabase.SpellDatabase)
                        if (candidate != null && candidate.Type == Spell.SpellType.Heal &&
                            string.Equals(candidate.SpellName, name, StringComparison.OrdinalIgnoreCase))
                        { _healingTemplate = candidate; break; }
                    if (_healingTemplate != null) break;
                }
            }
            if (_healingTemplate == null)
            {
                Spell fallback = LightbringerSpellCatalog.GetById(LightbringerSpellCatalog.LuminousMendingId);
                if (fallback != null && fallback.Type == Spell.SpellType.Heal)
                    _healingTemplate = fallback;
            }
            if (_healingTemplate == null)
            {
                if (_procTemplateWarnings++ < 3)
                    Plugin.LogWarning("[LBAscension] Proc healing template unavailable; cannot fire Lasting Grace/Judgement's Mercy.");
                return null;
            }
            Spell proc = UnityEngine.Object.Instantiate(_healingTemplate);
            if (proc == null) return null;
            proc.Id = grace ? "CS_LB_AA_PROC_LASTING_GRACE" : "CS_LB_AA_PROC_JUDGEMENTS_MERCY";
            proc.SpellName = grace ? "Lasting Grace" : "Judgement's Mercy";
            proc.SpellDesc = "Lightbringer Ascension healing proc.";
            proc.hideFlags = HideFlags.HideAndDontSave;
            if (grace) _graceProc = proc;
            else _mercyProc = proc;
            return proc;
        }

        private static bool Heal(Stats recipient, Character source, int amount, bool grace)
        {
            if (_procHealing || recipient == null || recipient.CurrentHP <= 0 ||
                source == null || amount <= 0) return false;
            Spell proc = GetProcHealSpell(grace);
            if (proc == null) return false;
            try
            {
                _procHealing = true;
                // This is the exact native Stats.HealMe overload captured by
                // Combat Meter's Patch_HealingCapture. Its returned HP delta
                // is effective healing; do not emit an additional meter event.
                int effective = recipient.HealMe(proc, amount, false, false, source);
                if (effective > 0) return true;
                if (_noEffectiveHealLogs++ < 10)
                    Plugin.LogInfo("[LBAscension] " + proc.SpellName +
                        " attempted=" + amount + " effective=" + effective +
                        " recipient=" + recipient.MyName + " (no effective healing).");
                return false;
            }
            catch (Exception ex)
            {
                Plugin.LogWarning("[LBAscension] Proc healing failed: " + ex.Message);
                return false;
            }
            finally { _procHealing = false; }
        }
    }
}
