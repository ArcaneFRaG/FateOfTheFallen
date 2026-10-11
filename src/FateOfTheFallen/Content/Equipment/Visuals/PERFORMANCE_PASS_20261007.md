# Fate of the Fallen - Runtime Performance Pass

This pass is intentionally behaviour/visual-preserving.

Optimisations:

- Eventide/Carapace:
  - keeps only cached MaterialPropertyBlock Set calls in the per-frame path;
  - removes per-frame Material.SetColor, shader property enumeration,
    sharedMaterials array comparisons and GetPropertyBlock calls;
  - caches HeavyPlate5 renderer targets after first resolution;
  - periodic integrity checks run at 0.5s instead of 0.25s.
- Class restrictions:
  - removed allocation-heavy restriction rebuilding from every equipment poll;
  - one deferred refresh remains per controller lifetime for class-registration safety.
- Sim discovery:
  - SW, SB and Eventide share one FindObjectsOfType<SimPlayer>() snapshot refreshed
    at most every 0.75s;
  - SW/SB only retain actor state for Sims actually wearing relevant FOTF gear.
- Player weapons:
  - hierarchy/renderers/LOD/skinned renderer arrays cached on spawn;
  - LateUpdate reasserts cached visibility/transforms only;
  - native model discovery and bow renderer searches moved to periodic refresh.
- Sim weapons:
  - same component caching and LateUpdate reduction as player weapons;
  - native discovery runs on refresh, not every frame.
- Player body:
  - humanoid Animator cached by player root.
- Sim body:
  - Animator arrays, hierarchy arrays and humanoid bone resolutions cached;
  - token arrays are static;
  - empty body slots are skipped in LateUpdate.
- Tooltips:
  - TMP component arrays are cached and hierarchy-refreshed at 0.5s;
  - text rewriting is throttled to 5 Hz instead of every LateUpdate.
- Blighted Thralls:
  - all Thralls share one Character fallback-aggro scan instead of each Thrall
    calling FindObjectsOfType<Character>();
  - distance checks use squared distance.
- Raid/world-boss AI hot gates:
  - immediately return when suppression/override sets are empty.

- World-boss readiness/combat discovery:
  - Moldermaw/Nhal'Sera prerequisite scans now run every 15 frames during the
    same 600-frame readiness window instead of every frame;
  - custom creature fallback target discovery shares a 0.75s Character cache
    and uses squared-distance comparisons.
- Nhal'Sera equipment group effects:
  - ordinary-group Sim discovery is cached for 0.75s instead of performing a
    global SimPlayer search on every eligible proc.
