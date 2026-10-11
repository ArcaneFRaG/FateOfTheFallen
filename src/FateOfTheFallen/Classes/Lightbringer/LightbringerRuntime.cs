using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateOfTheFallen
{
    internal static class LightbringerRuntime
    {
        private static SimPlayer[] _cachedGroupSims;
        private static float _nextGroupSimCacheRefresh;

        internal static void OnSuccessfulCast(
            LightbringerSpellDefinition definition,
            Character caster,
            Stats explicitTarget,
            Spell spell)
        {
            if (definition == null || caster == null || caster.MyStats == null)
                return;

            switch (definition.Kind)
            {
                case LightbringerSpellKind.Shield:
                    // Native Shield-line casts apply their own effects. Never
                    // allocate a second custom shield or intercept that hit.
                    break;

                case LightbringerSpellKind.HybridHealShield:
                    ApplySingleTargetShield(definition, caster, explicitTarget, spell);
                    break;

                case LightbringerSpellKind.GroupHealShield:
                    ApplyGroupShield(definition, caster, spell);
                    break;
            }
        }

        private static void ApplySingleTargetShield(
            LightbringerSpellDefinition definition,
            Character caster,
            Stats explicitTarget,
            Spell spell)
        {
            Stats target = explicitTarget != null ? explicitTarget : caster.MyStats;
            if (target == null)
                return;

            LightbringerAbsorbApi.ApplyAttributedShield(
                target,
                AdjustCapacity(definition.ShieldAmount, false, caster),
                AdjustDuration(definition.BarrierDuration, caster),
                definition.Id, caster, spell);
        }

        private static void ApplyGroupShield(
            LightbringerSpellDefinition definition,
            Character caster,
            Spell spell)
        {
            List<Character> members = GetCurrentGroupMembers(caster);
            for (int i = 0; i < members.Count; i++)
            {
                Character member = members[i];
                if (!IsAlive(member) || member.MyStats == null)
                    continue;

                if (caster.transform != null && member.transform != null)
                {
                    float distance = Vector3.Distance(caster.transform.position, member.transform.position);
                    if (distance > 60f)
                        continue;
                }

                LightbringerAbsorbApi.ApplyAttributedShield(
                    member.MyStats,
                    AdjustCapacity(definition.ShieldAmount, true, caster),
                    AdjustDuration(definition.BarrierDuration, caster),
                    definition.Id, caster, spell);
            }
        }

        // Passive skills, learned from native-icon skillbooks, strengthen
        // the existing wards rather than creating additional spell entries.
        internal static int AdjustCapacity(int amount, bool group, Character caster)
        {
            float factor = LightbringerNativeShield.WisdomMultiplier(caster != null ? caster.MyStats : null);
            if (LightbringerSkillCatalog.HasCasterSkill(caster != null ? caster.MyStats : null,
                LightbringerSkillCatalog.RadiantFortitudeId))
                factor += .10f;
            if (group && LightbringerSkillCatalog.HasCasterSkill(caster != null ? caster.MyStats : null,
                LightbringerSkillCatalog.SanctifiedCommunionId))
                factor += .15f;
            return Mathf.RoundToInt(amount * factor * LightbringerAscensions.WardMultiplier(caster != null ? caster.MySkills : null));
        }

        internal static float AdjustDuration(float seconds, Character caster = null)
        {
            return seconds + (LightbringerSkillCatalog.HasCasterSkill(caster != null ? caster.MyStats : null,
                LightbringerSkillCatalog.EnduringLightId)
                ? 3f : 0f);
        }

        internal static int ConsumeShield(Character defender, int incomingDamage)
        {
            if (defender == null || defender.MyStats == null || incomingDamage <= 0)
                return incomingDamage;

            int remaining = LightbringerAbsorbApi.ConsumeShield(defender.MyStats, incomingDamage);
            if (remaining != incomingDamage)
                Plugin.LogInfo("[LBShield] Damage=" + incomingDamage +
                    " absorbed=" + (incomingDamage - remaining) +
                    " remaining=" + LightbringerAbsorbApi.GetRemainingAbsorb(defender.MyStats));
            return remaining;
        }

        internal static List<Character> GetCurrentGroupMembers(Character source)
        {
            List<Character> result = new List<Character>();
            HashSet<Character> seen = new HashSet<Character>();

            if (source == null)
                return result;

            int raidGroup = ResolveRaidGroup(source);
            if (raidGroup > 0 && GameData.RaidManager != null && GameData.RaidManager.Raiders != null)
            {
                if (raidGroup == 1 && GameData.PlayerControl != null && IsAlive(GameData.PlayerControl.Myself))
                    AddUnique(result, seen, GameData.PlayerControl.Myself);

                for (int i = 0; i < GameData.RaidManager.Raiders.Count; i++)
                {
                    RaidMemberSlot slot = GameData.RaidManager.Raiders[i];
                    if (slot == null || slot.GroupNumber != raidGroup || slot.AssignedAvatar == null)
                        continue;

                    NPC npc = slot.AssignedAvatar.GetThisNPC();
                    Character character = npc != null ? npc.GetChar() : null;
                    if (IsAlive(character))
                        AddUnique(result, seen, character);
                }

                if (IsAlive(source))
                    AddUnique(result, seen, source);

                return result;
            }

            if (GameData.PlayerControl != null && IsAlive(GameData.PlayerControl.Myself))
                AddUnique(result, seen, GameData.PlayerControl.Myself);

            if (_cachedGroupSims == null || Time.unscaledTime >= _nextGroupSimCacheRefresh)
            {
                _nextGroupSimCacheRefresh = Time.unscaledTime + 0.75f;
                _cachedGroupSims = UnityEngine.Object.FindObjectsOfType<SimPlayer>() ?? new SimPlayer[0];
            }

            if (_cachedGroupSims != null && GameData.SimMngr != null)
            {
                for (int i = 0; i < _cachedGroupSims.Length; i++)
                {
                    SimPlayer sim = _cachedGroupSims[i];
                    if (sim == null || sim.GetThisNPC() == null)
                        continue;

                    bool inPlayerGroup = false;
                    try
                    {
                        inPlayerGroup = GameData.SimMngr.IsSimInPlayerGroup(sim.transform.name);
                    }
                    catch
                    {
                    }

                    if (!inPlayerGroup)
                        continue;

                    Character character = sim.GetThisNPC().GetChar();
                    if (IsAlive(character))
                        AddUnique(result, seen, character);
                }
            }

            if (IsAlive(source))
                AddUnique(result, seen, source);

            return result;
        }

        /// <summary>
        /// r86: Resolve confirmed members of the same active native/FOTF raid
        /// for single-target Lightbringer Sim AI. Group spells must continue
        /// to use GetCurrentGroupMembers(), not this expanded list.
        ///
        /// RaidManager.Raiders is authoritative for cross-party membership;
        /// we never treat unrelated nearby Sims as healable raid allies.
        /// When there is no active assigned raid we return the caster's
        /// existing native party, preserving ordinary five-person behaviour.
        /// </summary>
        internal static List<Character> GetCurrentRaidMembers(Character source)
        {
            List<Character> members = GetCurrentGroupMembers(source);
            if (source == null || !GameData.RaidActive ||
                GameData.RaidManager == null ||
                GameData.RaidManager.Raiders == null ||
                ResolveRaidGroup(source) <= 0)
                return members;

            HashSet<Character> seen = new HashSet<Character>(members);
            if (GameData.PlayerControl != null &&
                IsAlive(GameData.PlayerControl.Myself))
                AddUnique(members, seen, GameData.PlayerControl.Myself);

            foreach (RaidMemberSlot slot in GameData.RaidManager.Raiders)
            {
                if (slot == null || slot.GroupNumber <= 0 ||
                    slot.AssignedAvatar == null)
                    continue;
                NPC npc = slot.AssignedAvatar.GetThisNPC();
                Character raider = npc != null ? npc.GetChar() : null;
                if (IsAlive(raider))
                    AddUnique(members, seen, raider);
            }
            return members;
        }

        private static int ResolveRaidGroup(Character source)
        {
            if (source == null)
                return 0;

            if (GameData.PlayerControl != null && source == GameData.PlayerControl.Myself)
                return 1;

            if (GameData.RaidManager != null && GameData.RaidManager.Raiders != null)
            {
                for (int i = 0; i < GameData.RaidManager.Raiders.Count; i++)
                {
                    RaidMemberSlot slot = GameData.RaidManager.Raiders[i];
                    if (slot == null || slot.AssignedAvatar == null || slot.AssignedAvatar.GetThisNPC() == null)
                        continue;

                    Character character = slot.AssignedAvatar.GetThisNPC().GetChar();
                    if (character == source)
                        return slot.GroupNumber;
                }
            }

            return 0;
        }

        private static bool IsAlive(Character character)
        {
            return character != null && character.MyStats != null && character.MyStats.CurrentHP > 0;
        }

        private static void AddUnique(List<Character> result, HashSet<Character> seen, Character character)
        {
            if (character == null || seen.Contains(character))
                return;

            seen.Add(character);
            result.Add(character);
        }
    }
}
