# Previous Working Shoulders Only - Native Discovery Filter Fix

The BakeMesh shoulder pass was actually succeeding:

- Left shoulder overlay: 75 triangles
- Right shoulder overlay: 80 triangles
- ShoulderOverlays=2

But the next runtime lines exposed the real regression:

    Renderer=[FOTF_Eventide_PreviousShoulder_L]
    Renderer=[FOTF_Eventide_PreviousShoulder_R]

Both were being processed again by the normal Eventide native-palette code.

Why:
the shoulder overlay GameObjects are children of `HeavyPlate5`. The normal
native renderer discovery uses `GetComponentsInChildren<Renderer>()`, so on the
next refresh it found our two shoulder overlays and treated them as additional
native Eventide body renderers.

That replaced their earlier working shoulder material with the current
NativeChestAccent body palette.

Fix:
`EV_FindNativeItemRenderers()` now ignores any renderer whose transform or
ancestor name begins with:

    FOTF_Eventide_

Result:
- HeavyPlate5 continues to receive the working native chest palette.
- FOTF shoulder overlays keep their previous-working shoulder material.
- The shoulder pass cannot be recoloured by the chest pipeline.
- Existing stale overlay snapshots are restored/removed automatically by the
  current stale-renderer cleanup logic.

No chest palette values or BakeMesh shoulder geometry were changed.
