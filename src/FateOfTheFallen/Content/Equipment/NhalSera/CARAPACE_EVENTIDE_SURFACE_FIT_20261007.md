# Eventide Mockup Overlay - Native Surface Fit

The mesh-strip overlay was definitely alive:
- 19 active mesh segments;
- Standard emissive core;
- correct layer;
- world-scale bounds around 10 cm;

but the front view still showed no obvious strips. A tiny purple glint was
possibly visible only from an oblique/rear angle.

That behavior is consistent with the strips being *inside* the Eventide chest.

The previous placement offset was only about `0.118 * shoulderWidth` (~4.7 cm).
That is not enough to reach the front of a normal torso from the Synty skeleton
centerline.

This pass removes the guessed torso-depth offset.

It now:
1. finds the active native Eventide renderer (`HeavyPlate5`);
2. projects that renderer's actual world bounds onto the character front axis;
3. places all chest strips a few millimetres beyond that measured front plane;
4. places shoulder strips/triangles onto the same measured front plane;
5. logs `SurfaceSource`, `SurfaceOffset` and `BoundsDepth`.

This should make the overlay visibly sit *on top of* the native armor rather than
inside it.
