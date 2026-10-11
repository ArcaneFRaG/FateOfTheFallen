# Carapace Eventide Variant Fix Notes

## Problem
The first runtime recolor pass could promote a textureless native Eventide
material to the palette accent colour solely because its original material color
was purple-ish. On the chest body material this produced the bright magenta
"blob" seen in-game.

## Fix
The recolor logic now uses conservative accent detection:

- Texture-based materials:
  - Accent/emission is enabled only when the source texture actually contains a
    meaningful amount of native purple accent pixels.
- Textureless materials:
  - They are **not** treated as accent just because their `_Color` is purple.
  - They only become accent/emissive when the renderer/material name strongly
    implies trim, gem, crystal, glow, rune, etc.
  - All other textureless materials are recolored to dark metal.

## Result
This keeps the body/chest plates dark, while only the intended accent regions
remain bright/emissive, matching the mockup far more closely.
