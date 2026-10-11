# Carapace Eventide Variant Fix Notes - B

## Problem
The previous hot-pink fix was too conservative for textured Eventide atlas
regions. It correctly stopped the textureless body material from becoming a
solid magenta block, but it also failed to recover the intended purple accent
panels on the chest.

## Fix
This pass keeps the textureless-material safety rule, but broadens ONLY the
texture-pixel accent detector:

- darker baked purple pixels are now accepted as accent pixels;
- the textured accent-coverage threshold is reduced;
- textured accent pixels are mapped to a brighter void-purple;
- the emission mask for textured accent pixels is stronger.

## Result
Large torso/body pieces should remain dark metal overall, while the intended
Eventide purple patterning on the chest/torso returns as purple/emissive accents.
