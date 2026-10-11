using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Disabled.
    ///
    /// Carapace now uses two native systems only:
    /// - HeavyPlate5 chest: FOTF runtime native Synty palette override.
    /// - Shoulder attachments: Erenshor's own Item colour fields.
    ///
    /// No BakeMesh, copied mesh, triangle, line, or floating overlay geometry.
    /// </summary>
    internal sealed class FOTFEventideMockupGlow : MonoBehaviour
    {
        internal static FOTFEventideMockupGlow Create(
            Transform actorRoot,
            Color glowColor,
            string context)
        {
            return null;
        }

        internal void SetGlowColor(
            Color glowColor)
        {
        }
    }
}
