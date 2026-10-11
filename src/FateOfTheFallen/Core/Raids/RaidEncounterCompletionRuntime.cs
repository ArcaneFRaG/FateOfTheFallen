using System;

namespace FateOfTheFallen
{
    /// <summary>
    /// Encounter-completion state for the Raid PoC boss.
    ///
    /// A successful boss kill is different from the earlier lifecycle test:
    /// once defeated, the encounter remains complete across ordinary scene
    /// unload/re-entry and the boss is not independently respawned. A full
    /// RaidPocEncounterRuntime.ResetLifecycle() clears this PoC completion
    /// state for the next clean test session.
    /// </summary>
    internal static class RaidEncounterCompletionRuntime
    {
        private static bool _complete;
        private static float _completedAtRealtime;
        private static string _completionReason;

        internal static bool IsComplete
        {
            get
            {
                return _complete;
            }
        }

        internal static float CompletedAtRealtime
        {
            get
            {
                return _completedAtRealtime;
            }
        }

        internal static void Complete(
            string reason)
        {
            if (_complete)
            {
                return;
            }

            _complete =
                true;

            _completedAtRealtime =
                UnityEngine.Time.realtimeSinceStartup;

            _completionReason =
                string.IsNullOrEmpty(reason)
                    ? "boss defeated"
                    : reason;

            // Award persistent per-character PoC kill credit separately from
            // the corpse reward. The reward remains lootable from the native
            // boss corpse; this credit simply proves a completion ledger can
            // survive ordinary raid scene unload/re-entry.
            bool creditAwarded =
                RaidPocBossLoot.AwardCompletionCredit();

            // Stop every encounter-scoped controller. These Stop() paths
            // restore any SimPlayer AI/targets/NavMesh state they temporarily
            // owned and destroy the Phase 2 encounter add if it is still alive.
            RaidBossPhaseRuntime.Stop(
                "encounter complete");

            RaidSoakMechanicRuntime.Stop(
                "encounter complete");

            RaidAvoidableAoeMechanicRuntime.Stop(
                "encounter complete");

            RaidPhaseTwoInterruptRuntime.Stop(
                "encounter complete");

            RaidBossCastBarRuntime.Hide();

            // Defensive final release in case a mechanic was interrupted at an
            // unusual frame boundary before its normal owner-specific release.
            RaidMechanicCoordinator.ForceReleaseAll();

        }

        internal static void Reset(
            string reason)
        {
            bool wasComplete =
                _complete;

            _complete =
                false;

            _completedAtRealtime =
                0f;

            _completionReason =
                null;

            RaidBossCastBarRuntime.Hide();
            RaidMechanicCoordinator.ForceReleaseAll();

            if (wasComplete)
            {
            }
        }
    }
}
