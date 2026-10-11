using UnityEngine;

namespace FateOfTheFallen
{
    internal static class ArchivistSerethDefinition
    {
        internal const string NpcName =
            "Archivist Sereth";

        internal const string SceneName =
            "Azure";

        internal const string BundleName =
            "fotf_npcs";

        // Direct names are tried first. The loader then scans the bundle
        // for any GameObject asset path containing "sereth".
        internal static readonly string[] PrefabNames =
        {
            "ArchivistSereth",
            "Archivist Sereth",
            "FOTF_Archivist_Sereth",
            "FOTF_ArchivistSereth"
        };

        internal static readonly Vector3 SpawnPosition =
            new Vector3(
                332.40f,
                36.84f,
                445.47f);

        // Northeast, using world +Z as north.
        // Sereth's FBX forward axis is offset from Unity world-forward.
        // In-game validation showed yaw 45 facing west. Adding the required
        // 135-degree model offset makes the visible character face northeast.
        internal const float SpawnYaw =
            180f;

        internal static readonly Quaternion SpawnRotation =
            Quaternion.Euler(
                0f,
                SpawnYaw,
                0f);

        // Runtime interaction shell dimensions. These can be tuned after the
        // first in-game model test without rebuilding the prefab.
        internal const float ColliderRadius =
            0.45f;

        internal const float ColliderHeight =
            1.90f;

        internal static readonly Vector3 ColliderCenter =
            new Vector3(
                0f,
                0.95f,
                0f);
}
}
