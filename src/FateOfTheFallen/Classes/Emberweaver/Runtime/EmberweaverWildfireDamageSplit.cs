using UnityEngine;

namespace FateOfTheFallen
{
    /// <summary>
    /// Single source of truth for the Wildfire damage split. It is a
    /// conversion, not a second additive copy of the direct spell damage.
    ///
    /// The immediate hit passes through Erenshor's normal damage/resist path;
    /// the share deposited afterward is reconstructed from the final, actually
    /// dealt hit so resisted/blocked casts do not mint an unresisted Wildfire
    /// pool based on the spell's tooltip damage.
    /// </summary>
    internal static class EmberweaverWildfireDamageSplit
    {
        // Current spell/ascension definitions peak at 75% (0.60 + 0.15).
        // Keep a defensive headroom cap if future effects raise this share.
        private const float MaxConvertedShare = 0.90f;

        internal static float ClampShare(float requestedShare)
        {
            return Mathf.Clamp(requestedShare, 0f, MaxConvertedShare);
        }

        internal static float ImmediateFraction(float requestedShare)
        {
            return 1f - ClampShare(requestedShare);
        }

        internal static float StoredDamageFromImmediate(
            int actualImmediateDamage,
            float requestedShare)
        {
            if (actualImmediateDamage <= 0)
            {
                return 0f;
            }

            float share = ClampShare(requestedShare);
            if (share <= 0f)
            {
                return 0f;
            }

            // For a pre-split hit H: direct = H * (1 - share),
            // wildfire = H * share. Reconstruct from the post-resist direct
            // result to avoid depositing damage from spells that fail to land.
            return actualImmediateDamage * (share / (1f - share));
        }
    }
}
