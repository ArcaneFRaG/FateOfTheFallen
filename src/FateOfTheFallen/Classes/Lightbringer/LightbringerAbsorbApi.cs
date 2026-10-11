using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Stable read API for optional UI mods. Pure Lightbringer wards now use
    /// native Erenshor absorption. Hybrid/group spell portions retain a
    /// separate legacy pool until native ally targeting is verified.
    /// </summary>
    public static class LightbringerAbsorbApi
    {
        private sealed class ShieldState
        {
            internal int Remaining;
            internal int OriginalCapacity;
            internal float ExpiresAt;
            internal string WardId;
            internal Character Caster;
            internal Spell SourceSpell;
        }

        // Native wards: an optional display estimate is kept separate from
        // Erenshor's authoritative damage-absorption mechanics. It NEVER
        // intercepts or modifies a native hit. Exact readback wins when found.
        private sealed class NativeWardState
        {
            internal Character Caster;
            internal Spell Spell;
            internal int RemainingEstimate;
            internal float ActivatedAt;
            internal float ExpiresAt;
            internal int StatusSlot = -1;
            internal StatusEffect StatusInstance;
            internal bool NativeStatusConfirmed;
            internal int LastCapacity;
            // Last VERIFIED native SpellShield capacity already accounted for.
            // It is reset on status application/refresh, never on cast acceptance.
            internal int LastAccountedNativeRemaining = -1;
            internal float LastAuthoredDuration;
            internal float PendingFrom;
            internal float PendingUntil;
            internal int LastReportedFrame = -1;
            internal int LastReportedBefore;
            internal int LastReportedAfter;
            internal float LastNativeApplicationAt = -1f;
            internal bool RefreshPending;
            internal float RefreshDueAt;
            internal float RefreshAbandonAt;
            internal CastSpell PendingCaster;
            internal StatusEffect ExistingBeforeRecast;
        }

        // For instant wards, AddStatusEffect can run *inside* StartSpell,
        // before its postfix publishes the normal native-ward tracking state.
        // This thread-local context identifies exactly that synchronous cast.
        [ThreadStatic]
        private static NativeWardCastContext _currentCast;

        private sealed class NativeWardCastContext
        {
            internal Stats Target;
            internal Character Caster;
            internal Spell Spell;
            internal int Capacity;
            internal float Duration;
            internal float CastTime;
        }

        internal static void BeginNativeWardCast(Stats target, Character caster,
            Spell spell, int capacity, float duration, float castTime)
        {
            _currentCast = target != null && caster != null && spell != null
                ? new NativeWardCastContext
                {
                    Target = Canonical(target), Caster = caster, Spell = spell,
                    Capacity = capacity, Duration = duration, CastTime = castTime
                }
                : null;
        }

        internal static void EndNativeWardCast()
        {
            _currentCast = null;
        }

        private static bool _loggedNativeDisplayFallback;
        private static readonly ConditionalWeakTable<Stats, NativeWardState> NativeWards =
            new ConditionalWeakTable<Stats, NativeWardState>();

        private struct MeterAbsorbRecord
        {
            internal Character Caster;
            internal Character Target;
            internal Spell Spell;
            internal int Amount;
        }

        private const int MaxQueuedMeterAbsorbs = 512;
        private static readonly Queue<MeterAbsorbRecord> MeterAbsorbs =
            new Queue<MeterAbsorbRecord>();
        private static int _publishedAbsorbLogCount;
        private static int _lifecycleDeltaLogCount;
        private static int _uncreditedDepletionLogCount;

        private static MethodInfo _directMeterDelivery;
        private static float _nextMeterDeliveryProbe;
        private static int _directMeterDeliveryFailures;

        /// <summary>
        /// Combat Meter r75+ can acknowledge a confirmed absorb directly.
        /// An acknowledged record is NOT also put into the pull queue, so the
        /// legacy reader and the direct sink cannot count the same hit twice.
        /// If the meter is absent/older or the acknowledgement fails, continue
        /// publishing to the existing pull queue without changing gameplay.
        /// </summary>
        private static bool TryDeliverToCombatMeter(
            Character caster, Character target, Spell spell, int amount)
        {
            if (_directMeterDelivery == null && Time.unscaledTime >= _nextMeterDeliveryProbe)
            {
                _nextMeterDeliveryProbe = Time.unscaledTime + 3f;
                try
                {
                    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly == null) continue;
                        Type meter = assembly.GetType(
                            "ErenshorCombatMeter.FOTFAbsorbMeterInterop", false);
                        if (meter == null) continue;
                        MethodInfo method = meter.GetMethod(
                            "ReceiveConfirmedAbsorbFromFOTF",
                            BindingFlags.Public | BindingFlags.Static);
                        if (method == null || method.ReturnType != typeof(bool)) continue;
                        ParameterInfo[] parameters = method.GetParameters();
                        if (parameters.Length != 4 ||
                            parameters[0].ParameterType != typeof(Character) ||
                            parameters[1].ParameterType != typeof(Character) ||
                            parameters[2].ParameterType != typeof(Spell) ||
                            parameters[3].ParameterType != typeof(int)) continue;
                        _directMeterDelivery = method;
                        Plugin.LogInfo("[LBShieldMeter] Combat Meter direct-ack bridge connected.");
                        break;
                    }
                }
                catch (Exception error)
                {
                    if (_directMeterDeliveryFailures++ < 2)
                        Plugin.LogWarning("[LBShieldMeter] Direct meter discovery failed: " +
                            error.GetType().Name + ": " + error.Message);
                }
            }
            if (_directMeterDelivery == null) return false;

            try
            {
                object accepted = _directMeterDelivery.Invoke(
                    null, new object[] { caster, target, spell, amount });
                return accepted is bool && (bool)accepted;
            }
            catch (Exception error)
            {
                _directMeterDelivery = null;
                _nextMeterDeliveryProbe = Time.unscaledTime + 3f;
                if (_directMeterDeliveryFailures++ < 3)
                {
                    Exception root = error is TargetInvocationException &&
                        error.InnerException != null ? error.InnerException : error;
                    Plugin.LogWarning("[LBShieldMeter] Direct delivery failed (using queue): " +
                        root.GetType().Name + ": " + root.Message);
                }
                return false;
            }
        }

        /// <summary>
        /// Confirmed absorption records for optional Combat Meter integrations.
        /// Each record is damage prevented, not a ward's initial capacity.
        /// </summary>
        public static bool TryDequeueConsumedAbsorb(
            out Character caster, out Character target,
            out Spell spell, out int absorbed)
        {
            caster = null;
            target = null;
            spell = null;
            absorbed = 0;
            if (MeterAbsorbs.Count == 0) return false;
            MeterAbsorbRecord record = MeterAbsorbs.Dequeue();
            caster = record.Caster;
            target = record.Target;
            spell = record.Spell;
            absorbed = record.Amount;
            return caster != null && target != null && spell != null && absorbed > 0;
        }

        private static void PublishMeterAbsorb(
            Character caster, Character target, Spell spell, int amount,
            string mode)
        {
            if (caster == null || target == null || spell == null || amount <= 0)
                return;

            bool acknowledged = TryDeliverToCombatMeter(caster, target, spell, amount);
            if (!acknowledged)
            {
                if (MeterAbsorbs.Count >= MaxQueuedMeterAbsorbs)
                    MeterAbsorbs.Dequeue();
                MeterAbsorbs.Enqueue(new MeterAbsorbRecord
                {
                    Caster = caster, Target = target, Spell = spell, Amount = amount
                });
            }

            // Log both the amount and the actual transport selected. A direct
            // acknowledgement is only transport acceptance; Combat Meter still
            // applies normal encounter/scope checks when flushing the event.
            if (_publishedAbsorbLogCount++ < 16)
                Plugin.LogInfo("[LBShieldMeter] Published " + amount +
                    " absorb spell=" + spell.SpellName + " mode=" + mode +
                    " delivery=" + (acknowledged ? "direct-ack" : "pull-queue") +
                    " queued=" + MeterAbsorbs.Count +
                    " eventListeners=" + (AbsorbConsumed != null ? "yes" : "no"));

            // Preserve the legacy event for unrelated consumers. The restored
            // Combat Meter r69 path uses the pull queue and detaches the event.
            Action<Character, Character, Spell, int> handler = AbsorbConsumed;
            if (handler != null)
            {
                try { handler(caster, target, spell, amount); }
                catch (Exception error)
                {
                    Plugin.LogWarning("[LBShieldMeter] Event listener failed: " + error.Message);
                }
            }
        }

        /// <summary>Raised only for confirmed native absorption. Amounts from
        /// direct native capacity deltas are exact; full-absorb results use a
        /// bounded estimate if native remaining capacity is not exposed.</summary>
        public static event Action<Character, Character, Spell, int> AbsorbConsumed;

        internal static void TrackNativeWard(Stats target, Character caster,
            Spell spell, int capacity, float castTime, float durationSeconds)
        {
            target = Canonical(target);
            if (target == null || caster == null || spell == null || capacity <= 0) return;
            NativeWardState state = NativeWards.GetOrCreateValue(target);
            // StartSpell returns when the cast is accepted, not necessarily
            // completed. Do not show a ward until its casting time elapses.
            state.Caster = caster;
            state.Spell = spell;
            // Do not lose the saved status slot while a replacement cast is
            // pending.  It is the only unambiguous way to remove the PREVIOUS
            // ward if the native status effect shares an Arcanist template.
            state.RemainingEstimate = capacity;
            state.LastCapacity = capacity;
            state.LastAuthoredDuration = durationSeconds;
            state.PendingFrom = Time.time;
            state.PendingUntil = Time.time + Math.Max(2f, castTime + 2f);
            if (!state.NativeStatusConfirmed || state.StatusInstance == null)
                state.ActivatedAt = Time.time + Math.Max(0f, castTime);
            state.ExpiresAt = Time.time + Math.Max(0f, castTime) + Math.Max(.1f, durationSeconds);
            // Preserve the previous buff identity while a new cast is pending.
            // The native AddStatusEffect hook will replace it on application.
            if (!_loggedNativeDisplayFallback && !LightbringerNativeShield.HasNativeReadback)
            {
                _loggedNativeDisplayFallback = true;
                Plugin.LogWarning("[LBShield] Native remaining-absorb field unavailable; " +
                    "ArcaneUI/Group Frames will receive an estimated Lightbringer ward display. " +
                    "Gameplay remains native. Partial hits may not update this estimate.");
            }
        }

        // A Lightbringer native Shield can carry Hardened Skin's original
        // StatusEffectToApply reference.  The ACTUAL slot/instance is therefore
        // recorded from the successful native AddStatusEffect call.  The shared
        // reference alone is NEVER sufficient to delete a status effect.
        private static bool MatchesWard(Spell effect, NativeWardState ward, bool applying)
        {
            if (effect == null || ward == null || ward.Spell == null) return false;
            if (object.ReferenceEquals(effect, ward.Spell)) return true;
            if (string.Equals(effect.Id, ward.Spell.Id, StringComparison.OrdinalIgnoreCase)) return true;
            Spell wardEffect = ward.Spell.StatusEffectToApply;
            if (wardEffect == null) return false;
            if (object.ReferenceEquals(effect, wardEffect) ||
                string.Equals(effect.Id, wardEffect.Id, StringComparison.OrdinalIgnoreCase))
            {
                // When the effect IS the shared native template, this must
                // be a status-APPLICATION notification for a recently accepted
                // Lightbringer cast.  Never identify a buff for removal by the
                // template alone: it could be an Arcanist's Hardened Skin.
                if (object.ReferenceEquals(wardEffect, LightbringerNativeShield.NativeTemplateEffect))
                    return applying && Time.time >= ward.PendingFrom - .1f &&
                        Time.time <= ward.PendingUntil;
                return true;
            }
            return false;
        }

        private static bool IsIncomingNativeWard(Stats target, Spell effect, out NativeWardState ward)
        {
            ward = null;
            target = Canonical(target);
            if (target == null || effect == null) return false;
            if (NativeWards.TryGetValue(target, out ward) && MatchesWard(effect, ward, true))
                return true;
            NativeWardCastContext current = _currentCast;
            if (current == null || current.Target != target) return false;
            NativeWardState candidate = new NativeWardState
            {
                Spell = current.Spell,
                ActivatedAt = Time.time,
                PendingFrom = Time.time,
                PendingUntil = Time.time + 2f,
                ExpiresAt = Time.time + Math.Max(.1f, current.Duration)
            };
            if (!MatchesWard(effect, candidate, true)) return false;
            TrackNativeWard(target, current.Caster, current.Spell,
                current.Capacity, current.CastTime, current.Duration);
            return NativeWards.TryGetValue(target, out ward);
        }

        private static int FindTrackedBuffSlot(Stats target, NativeWardState ward)
        {
            if (target == null || ward == null || target.StatusEffects == null) return -1;
            if (ward.StatusSlot >= 0 && ward.StatusSlot < target.StatusEffects.Length &&
                object.ReferenceEquals(target.StatusEffects[ward.StatusSlot], ward.StatusInstance) &&
                ward.StatusInstance != null)
                return ward.StatusSlot;
            for (int i = 0; i < target.StatusEffects.Length; i++)
            {
                StatusEffect status = target.StatusEffects[i];
                if (status != null && MatchesWard(status.Effect, ward, false)) return i;
            }
            return -1;
        }

        private static void RemoveTrackedWardBuff(Stats target, NativeWardState ward, string reason)
        {
            int slot = FindTrackedBuffSlot(target, ward);
            if (slot < 0) return;
            try
            {
                target.RemoveStatusEffect(slot);
                Plugin.LogInfo("[LBWardLifecycle] Removed " + ward.Spell.SpellName +
                    " on " + target.MyName + " reason=" + reason + " slot=" + slot);
            }
            catch (Exception ex)
            {
                Plugin.LogWarning("[LBWardLifecycle] Status removal deferred: " + ex.Message);
                return;
            }
            ward.StatusSlot = -1;
            ward.StatusInstance = null;
            ward.NativeStatusConfirmed = false;
        }

        /// <summary>Public read-only attribution for Combat Meter's independent
        /// native-delta fallback. This never awards healing for unused shields
        /// and does not expose or mutate the native status-effect objects.</summary>
        public static bool TryGetNativeWardSource(Stats target,
            out Character caster, out Spell sourceSpell)
        {
            caster = null;
            sourceSpell = null;
            target = Canonical(target);
            NativeWardState ward;
            if (target == null || !NativeWards.TryGetValue(target, out ward) ||
                ward.Caster == null || ward.Spell == null ||
                Time.time < ward.ActivatedAt || Time.time >= ward.ExpiresAt)
                return false;
            caster = ward.Caster;
            sourceSpell = ward.Spell;
            return true;
        }

        /// <summary>Native shield pool only, not the custom hybrid/group pool.
        /// Intended for confirmed before/after hit deltas in UI integrations.</summary>
        public static int GetNativeWardRemaining(Stats target)
        {
            return LightbringerNativeShield.GetRemainingNative(Canonical(target));
        }

        /// <summary>If native AddStatusEffect suppresses a duplicate ward,
        /// queue a refresh of the EXACT already-tracked buff following cast
        /// completion. We do not remove the buff or grant any absorb here.</summary>
        internal static void QueueAcceptedNativeRefresh(Stats target, CastSpell cast,
            float acceptedAt, float castTime)
        {
            target = Canonical(target);
            NativeWardState ward;
            if (target == null || !NativeWards.TryGetValue(target, out ward)) return;
            // Instant applications can happen synchronously inside StartSpell.
            if (ward.LastNativeApplicationAt >= acceptedAt - .001f) return;
            int slot = FindTrackedBuffSlot(target, ward);
            if (slot < 0 || target.StatusEffects == null ||
                target.StatusEffects[slot] == null) return;
            ward.ExistingBeforeRecast = target.StatusEffects[slot];
            ward.PendingCaster = cast;
            ward.RefreshDueAt = Time.time + Math.Max(0f, castTime) + .15f;
            ward.RefreshAbandonAt = ward.RefreshDueAt + 4f;
            ward.RefreshPending = true;
            Plugin.LogInfo("[LBWardRefresh] Recast accepted; waiting for native application target=" +
                target.MyName + " spell=" + ward.Spell.SpellName +
                " due=" + ward.RefreshDueAt.ToString("0.00") +
                " slot=" + slot);
        }

        /// <summary>Fallback only for an accepted recast whose old buff remains.
        /// Allows a native ward to be refreshed without registering a second
        /// custom damage shield or touching Arcanist's buffs.</summary>
        internal static void FinishAcceptedNativeRefresh(Stats target)
        {
            target = Canonical(target);
            NativeWardState ward;
            if (target == null || !NativeWards.TryGetValue(target, out ward) ||
                !ward.RefreshPending) return;
            if (Time.time > ward.RefreshAbandonAt)
            {
                ward.RefreshPending = false;
                Plugin.LogWarning("[LBWardRefresh] Abandoned unconfirmed recast on " + target.MyName);
                return;
            }
            if (Time.time < ward.RefreshDueAt) return;
            try
            {
                if (ward.PendingCaster != null && ward.PendingCaster.isCasting()) return;
            }
            catch (Exception ex)
            {
                Plugin.LogWarning("[LBWardRefresh] Native cast-state check failed: " + ex.Message);
                ward.RefreshPending = false;
                return;
            }
            int slot = FindTrackedBuffSlot(target, ward);
            if (slot < 0 || target.StatusEffects == null ||
                !object.ReferenceEquals(target.StatusEffects[slot], ward.ExistingBeforeRecast))
            {
                // Different native status instance: this was a real application
                // (or the status vanished), NOT a suppressed duplicate.
                ward.RefreshPending = false;
                return;
            }
            int amount = LightbringerNativeShield.GetConfiguredCapacity(ward.Spell,
                ward.LastCapacity);
            if (amount <= 0 || !LightbringerNativeShield.RefreshNativeRemaining(target, amount))
            {
                ward.RefreshPending = false;
                Plugin.LogWarning("[LBWardRefresh] Could not reset native shield on " + target.MyName);
                return;
            }
            float duration = Math.Max(.1f, ward.LastAuthoredDuration);
            target.StatusEffects[slot].Duration = duration;
            ward.RemainingEstimate = amount;
            ward.LastAccountedNativeRemaining = LightbringerNativeShield.HasNativeReadback
                ? LightbringerNativeShield.GetRemainingNative(target) : -1;
            ward.ActivatedAt = Time.time;
            ward.ExpiresAt = Time.time + duration;
            ward.RefreshPending = false;
            Plugin.LogInfo("[LBWardRefresh] Fallback restored native ward=" +
                ward.Spell.SpellName + " target=" + target.MyName +
                " capacity=" + amount + " duration=" + duration.ToString("0.0") +
                " statusSlot=" + slot);
        }

        internal static void BeforeNativeWardApplied(Stats target, Spell effect)
        {
            target = Canonical(target);
            NativeWardState ward;
            if (!IsIncomingNativeWard(target, effect, out ward)) return;
            // Remove the exact previously captured status INSTANCE at the
            // point native application is occurring, not at StartSpell (a
            // cast may be interrupted).  Never search by Hardened Skin's
            // shared template ID when deciding what to remove.
            int previousSlot = FindTrackedBuffSlot(target, ward);
            Plugin.LogInfo("[LBWardLifecycle] Native apply target=" + target.MyName +
                " ward=" + ward.Spell.SpellName + " previousSlot=" + previousSlot);
            if (previousSlot >= 0)
                RemoveTrackedWardBuff(target, ward, "refresh");
        }

        internal static void AfterNativeWardApplied(Stats target, Spell effect, int nativeSlot)
        {
            target = Canonical(target);
            NativeWardState ward;
            if (!IsIncomingNativeWard(target, effect, out ward)) return;
            if (nativeSlot >= 0 && target.StatusEffects != null &&
                nativeSlot < target.StatusEffects.Length && target.StatusEffects[nativeSlot] != null)
            {
                ward.StatusSlot = nativeSlot;
                ward.StatusInstance = target.StatusEffects[nativeSlot];
                ward.NativeStatusConfirmed = true;
            }
            else
            {
                ward.StatusSlot = -1;
                ward.StatusInstance = null;
                ward.NativeStatusConfirmed = false;
                Plugin.LogWarning("[LBWardLifecycle] Native status application did not " +
                    "produce a valid status slot for " + ward.Spell.SpellName +
                    " target=" + target.MyName + " returnedSlot=" + nativeSlot);
            }
            // Refill the native remaining pool, rather than creating a second
            // custom shield. Refresh even if the previous buff was not fully
            // consumed. Do so only after native status application succeeded.
            if (ward.StatusInstance != null)
            {
                int amount = LightbringerNativeShield.GetConfiguredCapacity(ward.Spell, ward.RemainingEstimate);
                if (amount > 0)
                {
                    bool reset = LightbringerNativeShield.RefreshNativeRemaining(target, amount);
                    ward.RemainingEstimate = amount;
                    Plugin.LogInfo("[LBWardLifecycle] Refresh native capacity=" + amount +
                        " target=" + target.MyName + " reset=" + reset);
                }
            }
            // Mark the native application as live, including an instant cast.
            // The native shield has just been applied/refilled. Establish its
            // capacity baseline; the refill itself must NEVER appear as healing.
            ward.LastAccountedNativeRemaining = LightbringerNativeShield.HasNativeReadback
                ? LightbringerNativeShield.GetRemainingNative(target) : -1;
            ward.ActivatedAt = Time.time;
            ward.LastNativeApplicationAt = Time.time;
            ward.RefreshPending = false;
            ward.PendingUntil = 0f;
            Plugin.LogInfo("[LBWardLifecycle] Applied " + ward.Spell.SpellName +
                " target=" + target.MyName + " slot=" + nativeSlot +
                " nativeRemaining=" + LightbringerNativeShield.GetRemainingNative(target));
        }

        /// <summary>
        /// Preserve FOTF's public AbsorbConsumed event contract consumed by
        /// Combat Meter r63/r66.  A native shield delta is authoritative even
        /// when DamageMe's incoming argument is ZERO (seen in actual logs).
        /// Never credit speculative/unused shield capacity.
        /// </summary>
        internal static void AfterNativeDamage(Stats target, int incoming,
            int nativeResult, int nativeBefore)
        {
            target = Canonical(target);
            if (target == null) return;
            NativeWardState ward;
            if (!NativeWards.TryGetValue(target, out ward) || ward.Caster == null ||
                ward.Spell == null || Time.time < ward.ActivatedAt ||
                Time.time >= ward.ExpiresAt) return;

            int nativeAfter = LightbringerNativeShield.GetRemainingNative(target);
            int rawDelta = nativeBefore > nativeAfter ? nativeBefore - nativeAfter : 0;
            int delta = rawDelta;
            // MagicDamageMe and BleedDamageMe can nest DamageMe. If an inner
            // hook already credited part of this reduction, only report the
            // unreported portion, never the entire outer before/after delta.
            if (delta > 0 && ward.LastAccountedNativeRemaining >= 0)
                delta = Math.Min(delta, Math.Max(0,
                    ward.LastAccountedNativeRemaining - nativeAfter));
            int prevented = 0;
            if (delta > 0)
            {
                // The native before/after delta proves exact consumption.
                // Some Erenshor DamageMe calls expose __0=0 despite native
                // SpellShield being consumed; do not discard these hits.
                prevented = incoming > 0 ? Math.Min(incoming, delta) : delta;
                ward.RemainingEstimate = Math.Max(0, nativeAfter);
            }
            else if (rawDelta == 0 && incoming > 0 && nativeResult == -2)
            {
                // Confirmed full absorption without readable native delta.
                // Bound estimated credit by remaining expected ward capacity.
                prevented = Math.Min(incoming, Math.Max(0, ward.RemainingEstimate));
                ward.RemainingEstimate = Math.Max(0, ward.RemainingEstimate - prevented);
            }

            bool depleted = (delta > 0 && nativeAfter == 0) ||
                (prevented > 0 && ward.RemainingEstimate == 0);
            if (depleted && prevented > 0)
                LightbringerAscensionRuntime.OnWardDepleted(
                    ward.Caster, target, ward.Spell, ward.LastCapacity);
            if (depleted)
            {
                ward.RefreshPending = false;
                RemoveTrackedWardBuff(target, ward, "depleted");
                ward.RemainingEstimate = 0;
                ward.ExpiresAt = Time.time;
            }
            if (prevented <= 0) return;
            if (LightbringerNativeShield.HasNativeReadback)
                ward.LastAccountedNativeRemaining = Math.Max(0, nativeAfter);
            // Some engine paths nest DamageMe calls. Suppress a duplicate
            // report of the SAME observed before/after transition, but allow
            // multiple distinct hits in a single rendered frame.
            if (delta > 0 && ward.LastReportedFrame == Time.frameCount &&
                ward.LastReportedBefore == nativeBefore &&
                ward.LastReportedAfter == nativeAfter)
                return;
            ward.LastReportedFrame = Time.frameCount;
            ward.LastReportedBefore = nativeBefore;
            ward.LastReportedAfter = nativeAfter;

            PublishMeterAbsorb(ward.Caster, target.Myself, ward.Spell,
                prevented, "native");
        }

        /// <summary>
        /// Capture actual decreases in Erenshor's native SpellShield pool when
        /// the engine consumes it outside a wrapped DamageMe overload. This is
        /// a confirmed native CAPACITY DELTA, not an estimate or a ward cast.
        /// Require the SAME live tracked ward instance, a positive capacity
        /// baseline, a living recipient, and positive remaining buff lifetime;
        /// natural expiration, interrupted recasts, and untracked shields must
        /// never earn effective healing.
        /// </summary>
        private static void CaptureUnreportedNativeDelta(Stats target, NativeWardState ward)
        {
            if (target == null || ward == null || ward.Caster == null ||
                ward.Spell == null || target.Myself == null ||
                target.CurrentHP <= 0 || !LightbringerNativeShield.HasNativeReadback ||
                !ward.NativeStatusConfirmed || ward.StatusInstance == null ||
                Time.time < ward.ActivatedAt || Time.time >= ward.ExpiresAt)
                return;

            int slot = FindTrackedBuffSlot(target, ward);
            if (slot < 0 || target.StatusEffects == null ||
                !object.ReferenceEquals(target.StatusEffects[slot], ward.StatusInstance) ||
                ward.StatusInstance.Duration <= 0.05f ||
                ward.ExpiresAt - Time.time <= 0.05f)
                return;

            int now = LightbringerNativeShield.GetRemainingNative(target);
            int before = ward.LastAccountedNativeRemaining;
            if (before < 0)
            {
                ward.LastAccountedNativeRemaining = now;
                return;
            }
            // Refill or unchanged value is not an absorb. Rebaseline in case
            // native systems adjust the pool without this class doing so.
            if (now >= before)
            {
                ward.LastAccountedNativeRemaining = now;
                return;
            }
            int prevented = before - now;
            ward.LastAccountedNativeRemaining = now;
            ward.RemainingEstimate = now;
            if (_lifecycleDeltaLogCount++ < 16)
                Plugin.LogInfo("[LBShieldMeter] Confirmed native capacity delta " +
                    prevented + " via ward lifecycle target=" + target.MyName +
                    " spell=" + ward.Spell.SpellName + " before=" + before +
                    " after=" + now);
            PublishMeterAbsorb(ward.Caster, target.Myself, ward.Spell,
                prevented, "native-lifecycle-delta");
            if (now == 0)
                LightbringerAscensionRuntime.OnWardDepleted(
                    ward.Caster, target, ward.Spell, ward.LastCapacity);
        }

        // Native status removal as a failsafe when depletion happens outside
        // the observed Character.DamageMe overload.  Capture only a verified,
        // live native pool reduction BEFORE clearing the status identity.
        internal static void SweepDepletedNativeWard(Stats target)
        {
            target = Canonical(target);
            NativeWardState ward;
            if (target == null || !NativeWards.TryGetValue(target, out ward) ||
                !ward.NativeStatusConfirmed || ward.StatusInstance == null ||
                Time.time < ward.ActivatedAt) return;
            // Do not use a zero from an unavailable readback as depletion.
            if (!LightbringerNativeShield.HasNativeReadback) return;
            CaptureUnreportedNativeDelta(target, ward);
            int remaining = LightbringerNativeShield.GetRemainingNative(target);
            if (remaining > 0) return;
            if (ward.LastAccountedNativeRemaining > 0 &&
                _uncreditedDepletionLogCount++ < 10)
                Plugin.LogInfo("[LBShieldMeter] Skipped unverified depletion target=" +
                    target.MyName + " ward=" + ward.Spell.SpellName +
                    " previously=" + ward.LastAccountedNativeRemaining +
                    " expiresIn=" + (ward.ExpiresAt - Time.time).ToString("0.00") +
                    " buffDuration=" + (ward.StatusInstance != null
                        ? ward.StatusInstance.Duration.ToString("0.00") : "none"));
            ward.RefreshPending = false;
            RemoveTrackedWardBuff(target, ward, "native capacity exhausted");
            ward.RemainingEstimate = 0;
            ward.ExpiresAt = Time.time;
        }

        /// <summary>Verify that the game's AddStatusEffect hook observed an
        /// actual, live, exact Lightbringer ward status for this recipient.
        /// An arbitrary non-null slot is not enough to authorize a native
        /// SpellShield refill.</summary>
        internal static bool IsResolvedHealWardConfirmed(Stats target, Spell bridge)
        {
            target = Canonical(target);
            NativeWardState state;
            if (target == null || bridge == null ||
                !NativeWards.TryGetValue(target, out state) ||
                !object.ReferenceEquals(state.Spell, bridge) ||
                !state.NativeStatusConfirmed || state.StatusInstance == null ||
                !MatchesWard(state.StatusInstance.Effect, state, false)) return false;
            int slot = FindTrackedBuffSlot(target, state);
            return slot >= 0 && target.StatusEffects != null &&
                object.ReferenceEquals(target.StatusEffects[slot], state.StatusInstance);
        }

        /// <summary>Discard only a failed FOTF native ward application.
        /// The matching spell identity prevents touching other native shields.</summary>
        internal static void AbandonFailedHealWard(Stats target, Spell attemptedSpell)
        {
            target = Canonical(target);
            NativeWardState state;
            if (target == null || attemptedSpell == null ||
                !NativeWards.TryGetValue(target, out state) ||
                !object.ReferenceEquals(state.Spell, attemptedSpell)) return;
            // The previous tracked status may still be present if native
            // application failed before the AddStatusEffect prefix ran.
            // Remove only a status whose actual Effect matches this bridge.
            if (state.StatusInstance != null &&
                MatchesWard(state.StatusInstance.Effect, state, false))
                RemoveTrackedWardBuff(target, state, "failed resolved heal ward");
            NativeWards.Remove(target);
        }

        internal static int GetNativeBefore(Stats target)
        {
            return LightbringerNativeShield.GetRemainingNative(Canonical(target));
        }

        private static int GetNativeOrEstimate(Stats target)
        {
            int native = LightbringerNativeShield.GetRemainingNative(target);
            // Some builds expose an unrelated numeric shield property that
            // remains zero. When no positive native capacity is readable,
            // keep the conservative FOTF-only display estimate available.
            if (native > 0) return native;
            NativeWardState state;
            if (target != null && NativeWards.TryGetValue(target, out state) &&
                Time.time >= state.ActivatedAt && Time.time < state.ExpiresAt)
                return Math.Max(0, state.RemainingEstimate);
            return 0;
        }

        private static readonly ConditionalWeakTable<Stats, ShieldState> Shields =
            new ConditionalWeakTable<Stats, ShieldState>();

        // The native player HUD can hold GameData.PlayerStats separately
        // from Character.MyStats during scene setup. Use the actual character
        // Stats instance for both damage interception and optional UI reads.
        internal static Stats Canonical(Stats target)
        {
            if (target != null && GameData.PlayerStats != null &&
                target == GameData.PlayerStats && GameData.PlayerControl != null &&
                GameData.PlayerControl.Myself != null && GameData.PlayerControl.Myself.MyStats != null)
                return GameData.PlayerControl.Myself.MyStats;
            return target;
        }

        /// <summary>Active unconsumed absorb amount; 0 means no active shield.</summary>
        public static int GetRemainingAbsorb(Stats target)
        {
            target = Canonical(target);
            if (target == null || target.CurrentHP <= 0) return 0;
            ShieldState state;
            if (!Shields.TryGetValue(target, out state))
                return GetNativeOrEstimate(target);
            if (state.Remaining <= 0 || Time.time >= state.ExpiresAt)
            {
                Shields.Remove(target);
                return GetNativeOrEstimate(target);
            }
            return state.Remaining + GetNativeOrEstimate(target);
        }

        /// <summary>
        /// Reserve a single shield pool per target. A weaker ward cannot
        /// prolong a stronger one; equal/stronger wards refresh duration.
        /// </summary>
        public static int ApplyShield(Stats target, int capacity, float durationSeconds, string wardId)
        {
            target = Canonical(target);
            if (target == null || target.CurrentHP <= 0 || capacity <= 0 ||
                durationSeconds <= 0f || string.IsNullOrEmpty(wardId)) return 0;
            return ApplyAttributedShield(target, capacity, durationSeconds, wardId, null, null);
        }

        /// <summary>
        /// Lightbringer-authored custom shields, including group and hybrid
        /// wards, store their actual source for Combat Meter attribution.
        /// Never award healing for merely APPLYING unused capacity.
        /// </summary>
        internal static int ApplyAttributedShield(Stats target, int capacity,
            float durationSeconds, string wardId, Character caster, Spell sourceSpell)
        {
            target = Canonical(target);
            if (target == null || target.CurrentHP <= 0 || capacity <= 0 ||
                durationSeconds <= 0f || string.IsNullOrEmpty(wardId)) return 0;
            ShieldState state = Shields.GetOrCreateValue(target);
            int current = Time.time < state.ExpiresAt ? state.Remaining : 0;
            if (current > capacity) return current;
            state.Remaining = capacity;
            state.OriginalCapacity = capacity;
            state.ExpiresAt = Time.time + durationSeconds;
            state.WardId = wardId;
            state.Caster = caster;
            state.SourceSpell = sourceSpell;
            return capacity;
        }

        /// <summary>
        /// Returns the unabsorbed portion without editing HP or invoking
        /// native damage directly. Calling this twice for one hit would
        /// consume the shield twice.
        /// </summary>
        public static int ConsumeShield(Stats target, int incomingDamage)
        {
            target = Canonical(target);
            if (incomingDamage <= 0) return 0;
            ShieldState state;
            if (target == null || !Shields.TryGetValue(target, out state) ||
                state.Remaining <= 0 || Time.time >= state.ExpiresAt)
                return incomingDamage;
            int available = state.Remaining;
            int absorbed = Math.Min(available, incomingDamage);
            state.Remaining -= absorbed;
            // Capture provenance before removal; it is required by Combat Meter.
            Character caster = state.Caster;
            Spell source = state.SourceSpell;
            if (state.Remaining <= 0)
            {
                Shields.Remove(target);
                if (absorbed > 0)
                    LightbringerAscensionRuntime.OnWardDepleted(
                        caster, target, source, state.OriginalCapacity);
            }
            // The damage prefix calls this once per hit. A custom shield consumes
            // incoming damage here; do not count native armour/resistance as absorb.
            if (absorbed > 0 && caster != null && source != null)
            {
                PublishMeterAbsorb(caster, target.Myself, source,
                    absorbed, "custom");
            }
            return incomingDamage - absorbed;
        }

        public static void ClearShield(Stats target)
        {
            target = Canonical(target);
            if (target != null) Shields.Remove(target);
        }
    }
}
