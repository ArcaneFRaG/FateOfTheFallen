# Eventide Mockup Glow - Renderer Fix

The previous overlay reported all 11 line groups as active on the correct
character layer, but no line was visible.

The likely failure was the additive-only material path: particle additive shaders
multiply their output by a texture and the runtime-created material did not have
an explicit white particle texture assigned.

This pass changes the overlay to two visible passes:

- Core: Unlit/Color (fallback Sprites/Default/UI/Default), 0.012 m wide.
- Halo: additive particle shader, 0.030 m wide.
- Both receive an explicit `Texture2D.whiteTexture` where the shader exposes a
  main texture property.
- LineRenderer alignment is explicitly `View`.
- Both render after the native armor and have high sorting order.
- The core is intentionally bright purple even when Erenshor bloom is absent.
- The halo uses HDR purple for the emissive/bloom effect.

The authored line geometry/positions remain the same mockup chest/shoulder layout.
