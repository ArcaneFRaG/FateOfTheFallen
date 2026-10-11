# Carapace Eventide Variant Fix Notes - C

## Problem
The previous pass still recovered too little of the native Eventide torso
patterning, so the chest remained mostly black.

## Fix
This pass broadens textured accent recovery substantially, but ONLY for textured
materials:

- lower accent-pixel value/saturation thresholds;
- wider purple-hue / magenta-blue dominance acceptance;
- lower accent-coverage activation threshold;
- stronger recolor + stronger emission for recovered accent pixels;
- torso/body-like textured materials can enable emission whenever they contain
  any recovered accent pixels at all.

Textureless materials still keep the strict safety rule, so the whole chest
should not return to the solid hot-pink failure state.
