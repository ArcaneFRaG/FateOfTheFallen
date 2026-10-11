using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// r72: The level-35 hybrid/group raid heals apply native shields when the
    /// actual native HealMe call resolves, not when StartSpell accepts a cast.
    /// This uses the same validated Hardened Skin / Stats.SpellShield mechanism
    /// as pure Lightbringer wards and tracks the status for expiry/depletion.
    /// All other mixed heals keep their r71 behavior until tested separately.
    /// </summary>
    internal static class LightbringerResolvedHealWard
    {
        private sealed class RecipientStamp
        {
            internal int Frame = -1;
            internal string SpellId;
        }

        private static readonly ConditionalWeakTable<Stats, RecipientStamp> Stamps =
            new ConditionalWeakTable<Stats, RecipientStamp>();
        private static readonly Dictionary<string, Spell> BridgeSpells =
            new Dictionary<string, Spell>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> LoggedUnavailable =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static int _successLogCount;

        [ThreadStatic]
        private static bool _applying;

        internal static bool Handles(LightbringerSpellDefinition definition)
        {
            return definition != null &&
                (string.Equals(definition.Id, LightbringerSpellCatalog.DawnsSanctuaryId,
                    StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(definition.Id, LightbringerSpellCatalog.DivineInterventionId,
                    StringComparison.OrdinalIgnoreCase));
        }

        internal static void Reset()
        {
            BridgeSpells.Clear();
            LoggedUnavailable.Clear();
            _successLogCount = 0;
        }

        internal static void OnNativeHeal(Stats recipient, Spell healingSpell, Character source)
        {
            recipient = LightbringerAbsorbApi.Canonical(recipient);
            if (_applying || recipient == null || recipient.CurrentHP <= 0 || healingSpell == null)
                return;

            // HealMe is used game-wide, so make non-Lightbringer spells a
            // cheap fast exit (no catalog scans or allocations per heal).
            string id = healingSpell.Id;
            if (!string.Equals(id, LightbringerSpellCatalog.DawnsSanctuaryId,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(id, LightbringerSpellCatalog.DivineInterventionId,
                    StringComparison.OrdinalIgnoreCase)) return;

            LightbringerSpellDefinition definition;
            if (!LightbringerSpellCatalog.TryGetDefinition(healingSpell, out definition) ||
                !Handles(definition)) return;

            Character caster = source;
            // Native HealMe normally reports the casting Character, but
            // permit a missing source for the actual local player's own heal.
            if (caster == null && GameData.PlayerControl != null &&
                GameData.PlayerControl.Myself != null &&
                GameData.PlayerControl.Myself.MyStats != null &&
                LightbringerCatalog.IsLightbringerClass(
                    GameData.PlayerControl.Myself.MyStats.CharacterClass))
                caster = GameData.PlayerControl.Myself;
            if (caster == null || caster.MyStats == null ||
                !LightbringerCatalog.IsLightbringerClass(caster.MyStats.CharacterClass))
                return;

            RecipientStamp stamp = Stamps.GetOrCreateValue(recipient);
            if (stamp.Frame == Time.frameCount &&
                string.Equals(stamp.SpellId, definition.Id, StringComparison.OrdinalIgnoreCase))
                return;
            stamp.Frame = Time.frameCount;
            stamp.SpellId = definition.Id;

            // Group bonuses apply to Dawn's Sanctuary only. These are the
            // same WIS/passive calculations as the existing r71 custom wards.
            bool group = definition.Kind == LightbringerSpellKind.GroupHealShield;
            int capacity = LightbringerRuntime.AdjustCapacity(definition.ShieldAmount, group, caster);
            float duration = LightbringerRuntime.AdjustDuration(definition.BarrierDuration, caster);
            if (capacity <= 0 || duration <= 0f) return;

            try
            {
                _applying = true;
                if (!TryApplyNative(recipient, caster, definition, capacity, duration))
                {
                    // The game can change its Shield ABI. In that situation
                    // retain real gameplay absorption using the existing
                    // r71 custom DamageMe path rather than report UI-only HP.
                    int applied = LightbringerAbsorbApi.ApplyAttributedShield(
                        recipient, capacity, duration, definition.Id, caster, healingSpell);
                    if (LoggedUnavailable.Add(definition.Id))
                        Plugin.LogWarning("[LBHealWard] Native status unavailable for " +
                            definition.Name + "; using r71 custom absorption as a fallback. " +
                            "Check DamageMe and ArcaneUI in game.");
                    if (_successLogCount++ < 12)
                        Plugin.LogInfo("[LBHealWard] Custom fallback " + definition.Name +
                            " recipient=" + recipient.MyName + " capacity=" + applied);
                }
                else if (_successLogCount++ < 12)
                    Plugin.LogInfo("[LBHealWard] Native " + definition.Name +
                        " recipient=" + recipient.MyName +
                        " capacity=" + LightbringerNativeShield.GetRemainingNative(recipient) +
                        " duration=" + duration.ToString("0.0"));
            }
            finally
            {
                LightbringerAbsorbApi.EndNativeWardCast();
                _applying = false;
            }
        }

        private static bool TryApplyNative(Stats recipient, Character caster,
            LightbringerSpellDefinition definition, int capacity, float duration)
        {
            if (!LightbringerNativeShield.Ready) return false;

            int before = LightbringerNativeShield.GetRemainingNative(recipient);
            // One authoritative native SpellShield pool per target. Never
            // overwrite a larger existing shield with a weaker healing ward.
            if (before >= capacity) return true;

            Spell bridge = GetBridge(definition);
            if (bridge == null || !LightbringerNativeShield.ConfigureResolvedHealWard(
                    bridge, capacity, duration)) return false;

            int slot = -1;
            try
            {
                // This exact-recipient status path bypasses self-only spell
                // cast routing. A unique effect ID keeps it distinct from
                // native Hardened Skin and pure Lightbringer ward statuses.
                LightbringerAbsorbApi.TrackNativeWard(
                    recipient, caster, bridge, capacity, 0f, duration);
                LightbringerAbsorbApi.BeginNativeWardCast(
                    recipient, caster, bridge, capacity, duration, 0f);
                slot = recipient.AddStatusEffect(bridge, !caster.isNPC, 0, caster, duration);
                if (slot < 0 || recipient.StatusEffects == null ||
                    slot >= recipient.StatusEffects.Length ||
                    recipient.StatusEffects[slot] == null ||
                    !LightbringerAbsorbApi.IsResolvedHealWardConfirmed(recipient, bridge))
                {
                    LightbringerAbsorbApi.AbandonFailedHealWard(recipient, bridge);
                    return false;
                }

                // AddStatusEffect itself may return a slot while leaving the
                // old pool unchanged. Refill only after verified application.
                bool written = LightbringerNativeShield.RefreshNativeRemaining(recipient, capacity);
                if (written && LightbringerNativeShield.GetRemainingNative(recipient) >= capacity)
                    return true;

                LightbringerAbsorbApi.AbandonFailedHealWard(recipient, bridge);
                return false;
            }
            catch (Exception ex)
            {
                Plugin.LogWarning("[LBHealWard] Native application failed for " +
                    definition.Name + " on " + recipient.MyName + ": " + ex.Message);
                LightbringerAbsorbApi.AbandonFailedHealWard(recipient, bridge);
                return false;
            }
        }

        private static Spell GetBridge(LightbringerSpellDefinition definition)
        {
            Spell bridge;
            if (BridgeSpells.TryGetValue(definition.Id, out bridge) && bridge != null)
                return bridge;

            bridge = LightbringerNativeShield.Create(definition);
            if (bridge == null) return null;
            bridge.name = "FOTF - Lightbringer - " + definition.Name + " Ward Status";
            bridge.Id = definition.Id + "_WARD_STATUS";
            bridge.SpellName = definition.Name;
            bridge.SpellDesc = definition.Description;
            bridge.SelfOnly = false;
            bridge.CanHitPlayers = true;
            bridge.hideFlags = HideFlags.HideAndDontSave;

            // Even when the Shield magnitude lives on Spell rather than its
            // effect, isolate the effect from Hardened Skin and pure wards.
            if (bridge.StatusEffectToApply != null)
            {
                Spell effect = UnityEngine.Object.Instantiate(bridge.StatusEffectToApply);
                if (effect == null)
                {
                    UnityEngine.Object.Destroy(bridge);
                    return null;
                }
                effect.Id = definition.Id + "_WARD_EFFECT";
                effect.SpellName = definition.Name;
                effect.hideFlags = HideFlags.HideAndDontSave;
                bridge.StatusEffectToApply = effect;
            }
            BridgeSpells[definition.Id] = bridge;
            return bridge;
        }
    }
}
