using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>Lightbringer healer AI for FOTF-authored Sims only. Native Sim
    /// movement, targeting, grouping, mana and cast acceptance stay authoritative.
    /// Defers to world-boss dodge/CC overrides to avoid fighting raid AI.</summary>
    internal sealed class LightbringerSimCombatController : MonoBehaviour
    {
        private SimPlayer _sim;
        private NPC _npc;
        private CastSpell _caster;
        private float _nextDecision;
        private readonly Dictionary<string, float> _nextCast =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private const float DecisionInterval = 0.4f;
        private const float SkillRetryInterval = 12f;
        private const float WardRecastPerRecipient = 7f;
        private float _nextSkillCheck;
        private bool? _lastCombatState;
        private readonly Dictionary<Stats, float> _lastWardByRecipient =
            new Dictionary<Stats, float>();
        // Native casts may reject a distant or inaccessible raid ally.
        // Never retry the same invalid target every AI tick.
        private readonly Dictionary<Stats, float> _targetRetryAfter =
            new Dictionary<Stats, float>();
        private const float RejectedTargetDelay = 3f;

        internal static void Install(SimPlayer sim)
        {
            if (!LightbringerSimPlayers.IsAuthored(sim)) return;
            LightbringerSimCombatController ai =
                sim.GetComponent<LightbringerSimCombatController>();
            if (ai == null) ai = sim.gameObject.AddComponent<LightbringerSimCombatController>();
            ai._sim = sim;
            ai._npc = sim.GetThisNPC();
            ai._caster = ResolveCaster(sim, ai._npc);
        }

        internal static void TakeSpellOwnership(SimPlayer sim)
        {
            if (!LightbringerSimPlayers.IsAuthored(sim)) return;
            NPC npc = sim.GetThisNPC();
            if (npc == null || npc.MyAttackSpells == null) return;
            // Otherwise native DPS AI can spam a heal on an enemy while this
            // controller tries to choose an injured ally as the recipient.
            npc.MyAttackSpells.RemoveAll(delegate(Spell spell)
            {
                LightbringerSpellDefinition definition;
                return LightbringerSpellCatalog.TryGetDefinition(spell, out definition);
            });
        }

        private void Update()
        {
            if (_sim == null || !_sim.gameObject.activeInHierarchy ||
                !LightbringerSimPlayers.IsAuthored(_sim) ||
                Time.unscaledTime < _nextDecision) return;
            _nextDecision = Time.unscaledTime + DecisionInterval;
            if (Time.unscaledTime >= _nextSkillCheck)
            {
                _nextSkillCheck = Time.unscaledTime + SkillRetryInterval;
                LightbringerSimPlayers.EnsureSkills(_sim);
            }
            if (_npc == null) _npc = _sim.GetThisNPC();
            if (_caster == null) _caster = ResolveCaster(_sim, _npc);
            if (_npc == null || _caster == null || _sim.MyStats.CurrentHP <= 0 ||
                WorldBossSimAvoidanceRuntime.ShouldOverrideFullSimAi(_sim) ||
                WorldBossSimAvoidanceRuntime.ShouldOverrideNpcAi(_npc) ||
                WorldBossSimAvoidanceRuntime.ShouldSuppressCaster(_caster)) return;
            try { if (_caster.isCasting()) return; } catch { return; }

            Character owner = _npc.GetChar();
            if (owner == null || owner.MyStats == null) return;
            // Maintain two separate recipient pools. The raid roster is
            // eligible for SINGLE-target heals/wards; native Group Heal and
            // Lightbringer's group heal-shields remain strictly party-only.
            List<Character> party = LightbringerRuntime.GetCurrentGroupMembers(owner);
            List<Character> raidAllies = LightbringerRuntime.GetCurrentRaidMembers(owner);
            if (!IsInPlayerGroupOrRaid(_sim))
            {
                // An ungrouped wandering Sim may only heal itself. Never
                // grant access to the nearby player's or another raid's roster.
                party.Clear();
                party.Add(owner);
                raidAllies.Clear();
                raidAllies.Add(owner);
            }
            // Passive shielding of a healthy group outside combat repeatedly
            // drained mana and made Sims look like they were stuck casting.
            // Permit regular recovery healing while resting, but only cast
            // protective wards after a real group threat has been detected.
            bool combatActive = HasVerifiedCombatThreat(_sim, raidAllies);
            if (!_lastCombatState.HasValue || _lastCombatState.Value != combatActive)
            {
                _lastCombatState = combatActive;
                Plugin.LogInfo("[LBSimAI] " + _sim.MyStats.MyName +
                    " combat=" + combatActive + " proactiveWards=" + combatActive);
            }
            Character critical = null, lowest = null, tank = null;
            float lowestFraction = 1f;
            float criticalFraction = 0.35f;
            int woundedParty = 0, endangeredParty = 0;
            // Cross-party emergency healing and tank protection: only living,
            // in-range members of the confirmed raid roster are candidates.
            foreach (Character member in raidAllies)
            {
                if (!IsViableAlly(owner, member)) continue;
                float retryAfter;
                if (_targetRetryAfter.TryGetValue(member.MyStats, out retryAfter) &&
                    Time.unscaledTime < retryAfter) continue;
                float hp = (float)member.MyStats.CurrentHP / member.MyStats.CurrentMaxHP;
                if (hp < lowestFraction) { lowestFraction = hp; lowest = member; }
                if (hp < criticalFraction) { criticalFraction = hp; critical = member; }
                // Highest maximum HP is the existing conservative tank proxy.
                if (tank == null || member.MyStats.CurrentMaxHP > tank.MyStats.CurrentMaxHP)
                    tank = member;
            }
            // Group heal thresholds are deliberately based ONLY on the
            // caster's native subgroup, not all raid members.
            foreach (Character member in party)
            {
                if (!IsViableAlly(owner, member)) continue;
                float hp = (float)member.MyStats.CurrentHP / member.MyStats.CurrentMaxHP;
                if (hp < 0.8f) woundedParty++;
                if (hp < 0.55f) endangeredParty++;
            }
            Spell chosen = null;
            Stats recipient = null;
            if (critical != null)
            {
                if (combatActive) chosen = BestCustom(LightbringerSpellKind.HybridHealShield, true);
                if (chosen == null) chosen = BestNativeHeal(false);
                recipient = critical.MyStats;
            }
            if (chosen == null && endangeredParty >= 2)
            {
                if (combatActive) chosen = BestCustom(LightbringerSpellKind.GroupHealShield, true);
                if (chosen == null) chosen = BestNativeHeal(true);
                recipient = owner.MyStats;
            }
            if (chosen == null && woundedParty >= 2)
            {
                chosen = BestNativeHeal(true);
                if (chosen == null && combatActive)
                    chosen = BestCustom(LightbringerSpellKind.GroupHealShield, true);
                recipient = owner.MyStats;
            }
            if (chosen == null && lowest != null && lowestFraction < 0.8f)
            {
                if (combatActive) chosen = BestCustom(LightbringerSpellKind.HybridHealShield, true);
                if (chosen == null) chosen = BestNativeHeal(false);
                recipient = lowest.MyStats;
            }
            float lastWard;
            if (chosen == null && combatActive && tank != null && tank.MyStats != null &&
                LightbringerAbsorbApi.GetRemainingAbsorb(tank.MyStats) <= 0 &&
                (!_lastWardByRecipient.TryGetValue(tank.MyStats, out lastWard) ||
                    Time.unscaledTime - lastWard >= WardRecastPerRecipient))
            {
                chosen = BestCustom(LightbringerSpellKind.Shield, true);
                recipient = tank.MyStats;
            }
            if (chosen == null && combatActive && _npc.CurrentAggroTarget != null &&
                _npc.CurrentAggroTarget.Alive)
            {
                chosen = BestCustom(LightbringerSpellKind.Damage, false);
                recipient = _npc.CurrentAggroTarget.MyStats;
            }
            // Defense in depth: a combat state can change within this update.
            // The global StartSpell guard also protects native AI casts.
            if (chosen == null || recipient == null ||
                !HasVerifiedCombatThreat(_sim, raidAllies)) return;
            try
            {
                bool accepted = _caster.StartSpell(chosen, recipient);
                if (!accepted)
                {
                    _targetRetryAfter[recipient] = Time.unscaledTime + RejectedTargetDelay;
                    _nextDecision = Time.unscaledTime + 0.8f;
                }
                if (accepted)
                {
                    // Logs only genuinely accepted cross-subgroup casts.
                    // Their actual healing/absorb is still handled by native
                    // Erenshor and the existing confirmed Combat Meter bridge.
                    if (recipient.Myself != null && !party.Contains(recipient.Myself) &&
                        recipient != owner.MyStats)
                        Plugin.LogInfo("[LBSimRaidHeal] " + _sim.MyStats.MyName +
                            " accepted " + chosen.SpellName + " on raid ally " +
                            recipient.MyName + " (different subgroup)");
                    LightbringerSpellDefinition definition;
                    if (LightbringerSpellCatalog.TryGetDefinition(chosen, out definition) &&
                        definition != null && definition.Kind == LightbringerSpellKind.Shield)
                        _lastWardByRecipient[recipient] = Time.unscaledTime;
                    _nextCast[chosen.Id] = Time.unscaledTime +
                        Mathf.Max(1f, chosen.Cooldown);
                    _nextDecision = Time.unscaledTime + 0.65f;
                }
            }
            catch (Exception e)
            {
                Plugin.LogWarning("[LBSim] Cast failed " + chosen.SpellName +
                    " sim=" + _sim.MyStats.MyName + ": " + e.Message);
                _nextDecision = Time.unscaledTime + 2f;
            }
        }

        private static bool IsViableAlly(Character owner, Character member)
        {
            if (member == null || member.MyStats == null ||
                member.MyStats.CurrentHP <= 0 || member.MyStats.CurrentMaxHP <= 0)
                return false;
            return owner == null || owner.transform == null || member.transform == null ||
                Vector3.Distance(owner.transform.position, member.transform.position) <= 60f;
        }

        /// <summary>
        /// Native CurrentAggroTarget can remain assigned after an encounter.
        /// An alive target alone is therefore NOT evidence that combat is
        /// still underway. Require an enemy NPC that is actively targeting a
        /// party/raid member; this supports both player groups and raids.
        /// Return false on uncertainty to prevent idle ward spam.
        /// </summary>
        internal static bool HasVerifiedCombatThreat(SimPlayer sim,
            List<Character> members = null)
        {
            if (sim == null || sim.MyStats == null) return false;
            Character owner = sim.MyStats.Myself;
            if (owner == null)
            {
                NPC simNpc = sim.GetThisNPC();
                if (simNpc != null) owner = simNpc.GetChar();
            }
            if (owner == null) return false;
            if (members == null)
            {
                members = LightbringerRuntime.GetCurrentRaidMembers(owner);
                if (members == null) members = new List<Character>();
                if (!members.Contains(owner)) members.Add(owner);
            }
            foreach (Character member in members)
            {
                if (member == null || member.MyStats == null || member.MyStats.CurrentHP <= 0)
                    continue;
                NPC allyNpc = member.GetComponent<NPC>();
                if (allyNpc == null) allyNpc = member.GetComponentInParent<NPC>();
                if (allyNpc == null || allyNpc.CurrentAggroTarget == null ||
                    !allyNpc.CurrentAggroTarget.Alive) continue;
                Character enemy = allyNpc.CurrentAggroTarget;
                NPC enemyNpc = enemy.GetComponent<NPC>();
                if (enemyNpc == null) enemyNpc = enemy.GetComponentInParent<NPC>();
                if (enemyNpc == null || enemyNpc.CurrentAggroTarget == null ||
                    !enemyNpc.CurrentAggroTarget.Alive) continue;
                // Do not trust old friendly aggro pointers. A live enemy
                // should have an active target within our current group.
                foreach (Character ally in members)
                    if (ally != null && enemyNpc.CurrentAggroTarget == ally)
                        return true;
            }
            return false;
        }

        private Spell BestCustom(LightbringerSpellKind kind, bool reserveMana)
        {
            if (_sim == null || _sim.Spellbook == null) return null;
            Spell best = null;
            int bestLevel = -1;
            foreach (SimPlayerSpellSlot slot in _sim.Spellbook)
            {
                LightbringerSpellDefinition definition;
                if (slot == null || slot.spell == null ||
                    !LightbringerSpellCatalog.TryGetDefinition(slot.spell, out definition) ||
                    definition == null || definition.Kind != kind ||
                    definition.RequiredLevel > _sim.MyStats.Level ||
                    !CanCast(slot.spell, reserveMana) || definition.RequiredLevel <= bestLevel)
                    continue;
                best = slot.spell;
                bestLevel = definition.RequiredLevel;
            }
            return best;
        }

        private Spell BestNativeHeal(bool group)
        {
            if (_sim == null || _sim.Spellbook == null) return null;
            Spell best = null;
            int bestLevel = -1;
            foreach (SimPlayerSpellSlot slot in _sim.Spellbook)
            {
                Spell spell = slot != null ? slot.spell : null;
                if (!LightbringerNativeHealing.IsApproved(spell) ||
                    spell.RequiredLevel > _sim.MyStats.Level || !CanCast(spell, true)) continue;
                bool isGroup = spell.SpellName.StartsWith("Group ",
                    StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(spell.SpellName, "Major Group Heal", StringComparison.OrdinalIgnoreCase);
                if (isGroup != group || spell.RequiredLevel <= bestLevel) continue;
                best = spell;
                bestLevel = spell.RequiredLevel;
            }
            return best;
        }

        private bool CanCast(Spell spell, bool reserveMana)
        {
            if (spell == null || _sim == null || _sim.MyStats == null) return false;
            float ready;
            if (_nextCast.TryGetValue(spell.Id, out ready) && Time.unscaledTime < ready) return false;
            int available = _sim.MyStats.CurrentMana;
            if (available < spell.ManaCost) return false;
            if (reserveMana && available < Mathf.CeilToInt(spell.ManaCost * 1.05f)) return false;
            return true;
        }

        private static bool IsInPlayerGroupOrRaid(SimPlayer sim)
        {
            if (sim == null) return false;
            try
            {
                if (GameData.SimMngr != null &&
                    GameData.SimMngr.IsSimInPlayerGroup(sim.transform.name)) return true;
            }
            catch { }
            if (GameData.RaidManager != null && GameData.RaidManager.Raiders != null)
                foreach (RaidMemberSlot member in GameData.RaidManager.Raiders)
                    if (member != null && member.AssignedAvatar == sim) return true;
            return false;
        }

        private static CastSpell ResolveCaster(SimPlayer sim, NPC npc)
        {
            if (sim != null && sim.MyStats != null && sim.MyStats.Myself != null &&
                sim.MyStats.Myself.MySpells != null) return sim.MyStats.Myself.MySpells;
            if (npc != null)
            {
                Character character = npc.GetChar();
                if (character != null && character.MySpells != null) return character.MySpells;
                CastSpell casting = npc.GetComponent<CastSpell>();
                if (casting != null) return casting;
            }
            return sim != null ? sim.GetComponent<CastSpell>() : null;
        }
    }
}
