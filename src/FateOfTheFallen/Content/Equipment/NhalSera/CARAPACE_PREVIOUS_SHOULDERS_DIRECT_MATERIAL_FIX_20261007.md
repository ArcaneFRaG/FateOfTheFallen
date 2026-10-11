# Previous Working Shoulders Only - Direct Material Fix

The latest runtime proves the shoulder geometry is now being created correctly:

- Left = 75 triangles
- Right = 80 triangles
- ShoulderOverlays = 2
- ChestTouched = False

The remaining problem is material appearance.

The BakeMesh copy is runtime-generated, and relying on the native
`POLYGON_CustomCharacters` mask channels on that copied geometry is not giving
a visible shoulder result.

This pass leaves all geometry and chest logic unchanged, but changes the
shoulder-only overlay material to a guaranteed-visible Standard material:

- dark Void purple base
- moderate purple emission
- no dependence on Synty palette/mask channels

Because the mesh crop already contains only upper/lateral shoulder triangles,
this material cannot recolour the chest.

This is deliberately a visibility/isolation pass. Once the shoulder geometry is
confirmed visually, brightness and crop can be tuned without touching the chest.
