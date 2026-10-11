# Carapace Eventide Variant Fix Notes - D

## Root cause identified

The native Eventide torso material uses Erenshor's own shader/masking setup.

Earlier FOTF passes replaced any material that did not expose `_EmissionColor`
with Unity's `Standard` shader. That discarded the native shader channels which
produce the Eventide torso's purple pattern, leaving the chest uniformly black.

## Fix

This pass changes strategy:

- Never replace the native Eventide shader.
- Never replace the native Eventide base texture on textured materials.
- Preserve the native purple pattern exactly where Erenshor already draws it.
- Recolor common native secondary/accent color properties to the selected Fallen
  theme when those properties exist.
- Only enable true emission when the native shader already supports emission.
- The emission mask is derived from chromatic/saturated texture pixels, not an
  exact purple hue.
- Textureless non-accent materials remain dark metal.

This should restore the visible Eventide purple chest pattern first. If the
native torso shader does not support emission, the diagnostic log now prints its
exact shader name and NativeEmission=False so an additive overlay can be built
specifically for that renderer without destroying the native material.
