# Carapace - Native Shoulder Index 7 Fix

The diagnostic resolved the native Eventide shoulder system exactly.

For Carapace / Eventide:
- `ShoulderTrimL = "7"`
- `ShoulderTrimR = "7"`

Those strings are child indices, not transform names.

Native containers:
- `Inventory.Modulars.ShoulderL`
  -> `All_06_Shoulder_Attachment_Left`
- `Inventory.Modulars.ShoulderR`
  -> `All_05_Shoulder_Attachment_Right`

Both containers have 21 children, and:
- `child[7] = Shoulder7`
- `Shoulder7` is active
- `Shoulder7` has its own renderer

This production fix now resolves `Shoulder7` directly through the native
ModularParts shoulder containers and applies the shoulder-trim palette to that
renderer.

Palette behavior:
- HeavyPlate5 body keeps the working native chest accent.
- Shoulder7 MetalPrimary / Secondary become restrained Void purple.
- Shoulder7 MetalSecondary remains dark, preventing the full pauldron from
  becoming a solid neon block.

No custom floating geometry is used.
