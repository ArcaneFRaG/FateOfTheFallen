# Previous Working Shoulders Only - BakeMesh Fix

The previous shoulder-only package did not actually render the old shoulders.
The runtime log showed the cause:

- `Chr_Torso_Female_20` has `isReadable=false`
- direct access to its vertices/normals failed
- the shoulder overlay therefore ended with `ShoulderOverlays=[0]`

This build keeps the same intended split:
- chest = the already-working native chest-accent implementation, unchanged
- shoulders = the earlier visibly-working native Eventide palette only

The technical fix is `SkinnedMeshRenderer.BakeMesh(runtimeMesh)`.
The runtime-created baked mesh is readable, so the shoulder crop can be built
without touching the imported mesh's Read/Write setting.

The baked shoulder overlay is refreshed every LateUpdate so it follows native
skinning/animation.

No chest palette values are changed by the shoulder component.
