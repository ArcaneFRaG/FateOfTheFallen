# Carapace - Shoulder Target Provenance Fix

Observed runtime log after the previous build:

    Renderer=[HeavyPlate5] Role=[Body]
    Renderers=[2]

but there was no `Role=[ShoulderTrim]` material log.

Root cause:
the previous implementation tried to decide whether a renderer was a shoulder
trim by walking upward from `renderer.transform` and looking for the native
`ShoulderTrimL` / `ShoulderTrimR` activation object. That assumption is unsafe
for Synty's skinned character setup: a renderer can be selected/activated by a
shoulder trim object while the actual SkinnedMeshRenderer lives elsewhere in
the hierarchy.

Fix:
`EV_FindNativeItemRenderers()` now records the activation target that discovered
each renderer. If a renderer was discovered through `item.ShoulderTrimL` or
`item.ShoulderTrimR`, its instance ID is explicitly classified as
`ShoulderTrim`.

The native palette remains:
- body: bright Primary chest accent, dark MetalPrimary torso, dark
  MetalSecondary broad pauldrons;
- shoulder trim: restrained purple MetalPrimary / Secondary while
  MetalSecondary stays dark.

No custom overlay geometry is used.

New diagnostics:
- one `Eventide target map` line per matched activation target;
- one `Eventide target summary` line with the native target names and count of
  shoulder-trim renderers.
