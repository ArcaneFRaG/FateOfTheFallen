# Carapace - Native Shoulder Trim Restore

The native chest-accent build correctly restored the chest V/chevron, but the
shoulder trim disappeared because `MetalPrimary` had to remain dark on the main
HeavyPlate5 chest renderer.

This pass separates native renderers semantically:

- Body / HeavyPlate5:
  - Primary = bright Void chest accent
  - MetalPrimary = dark torso
  - MetalSecondary = dark broad pauldron

- ShoulderTrimL / ShoulderTrimR renderer(s):
  - MetalPrimary = restrained purple
  - MetalSecondary = dark

This restores the native purple shoulder edge/spike detail without recolouring
the whole pauldron purple again.

No custom LineRenderer, triangle, cube-strip, or floating overlay geometry is
used.
