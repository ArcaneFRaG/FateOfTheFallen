# Eventide Mockup Glow - Native Synty Front-Axis Fix

The overlay had:

- 11 active LineRenderers
- valid visible shaders (`Unlit/Color` + additive halo)
- the correct character layer
- valid chest/shoulder dimensions

but absolutely no lines were visible.

The remaining common factor was that every line shared the same surface offset
direction. Erenshor's imported native Synty `Head.forward` points through the
back of the visible model after FBX axis conversion, so the overlay was being
pushed behind/inside the armour and fully depth-occluded.

The overlay now uses:

    visibleFront = -Head.forward

and its fallback uses the same native-Synty front convention.

No line layout or mockup geometry was changed in this pass.
