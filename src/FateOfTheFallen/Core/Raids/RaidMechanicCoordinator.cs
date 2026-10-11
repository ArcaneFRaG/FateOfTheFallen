using System;

namespace FateOfTheFallen
{
    /// <summary>
    /// Small encounter-mechanic mutex. Only one movement-override mechanic may
    /// own SimPlayers at a time. This prevents a soak and an avoidable AoE from
    /// fighting over the same SimPlayer's NavMeshAgent/combat target.
    /// </summary>
    internal static class RaidMechanicCoordinator
    {
        private static string _owner;

        internal static bool TryAcquire(
            string owner)
        {
            if (string.IsNullOrEmpty(owner))
            {
                return false;
            }

            if (string.IsNullOrEmpty(_owner) ||
                string.Equals(
                    _owner,
                    owner,
                    StringComparison.Ordinal))
            {
                _owner =
                    owner;

                return true;
            }

            return false;
        }

        internal static void Release(
            string owner)
        {
            if (string.IsNullOrEmpty(_owner) ||
                !string.Equals(
                    _owner,
                    owner,
                    StringComparison.Ordinal))
            {
                return;
            }

            _owner =
                null;
        }

        internal static void ForceReleaseAll()
        {
            _owner =
                null;
        }


        internal static string CurrentOwner
        {
            get
            {
                return _owner;
            }
        }
    }
}
