using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Shared boss-room geometry for the raid PoC.
    ///
    /// The authored room is 50 x 50 Unity units. Unity's world-space convention
    /// is one unit per metre, so mechanics can use the room footprint directly.
    /// Boss_01 is treated as the room centre for the current PoC scene.
    /// </summary>
    internal static class RaidBossRoomRuntime
    {
        internal const float RoomSize =
            50f;

        internal const float HalfRoomSize =
            RoomSize * 0.5f;

        internal static bool TryGetCenter(
            out Vector3 center)
        {
            return RaidPocEncounterRuntime.TryGetBossRoomCenter(
                out center);
        }

        internal static bool ContainsPoint(
            Vector3 worldPosition,
            float inwardMargin)
        {
            Vector3 center;

            if (!TryGetCenter(
                    out center))
            {
                return false;
            }

            float usableHalfExtent =
                Mathf.Max(
                    0f,
                    HalfRoomSize - Mathf.Max(0f, inwardMargin));

            Vector3 offset =
                worldPosition - center;

            return
                Mathf.Abs(offset.x) <= usableHalfExtent &&
                Mathf.Abs(offset.z) <= usableHalfExtent;
        }

        internal static Vector3 ClampPoint(
            Vector3 worldPosition,
            float inwardMargin)
        {
            Vector3 center;

            if (!TryGetCenter(
                    out center))
            {
                return worldPosition;
            }

            float usableHalfExtent =
                Mathf.Max(
                    0f,
                    HalfRoomSize - Mathf.Max(0f, inwardMargin));

            Vector3 result =
                worldPosition;

            result.x =
                Mathf.Clamp(
                    result.x,
                    center.x - usableHalfExtent,
                    center.x + usableHalfExtent);

            result.z =
                Mathf.Clamp(
                    result.z,
                    center.z - usableHalfExtent,
                    center.z + usableHalfExtent);

            return result;
        }
    }
}
