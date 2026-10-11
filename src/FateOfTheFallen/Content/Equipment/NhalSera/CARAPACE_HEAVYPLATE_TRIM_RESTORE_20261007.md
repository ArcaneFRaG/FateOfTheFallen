# Carapace - HeavyPlate Shoulder Trim Restore

The Shoulder7 resolver is correct, but the latest visual test proved Shoulder7 is
not the visible outer shoulder trim/spike region we are trying to restore. It is
the small inner shoulder insert.

The earlier native slot-map showed that the visible outer Eventide shoulder
spikes/edge are part of the HeavyPlate5 body renderer and use
`_Color_Metal_Primary`. Unfortunately that same native slot also colours some
torso polygons, so native palette recolouring cannot make the outer shoulder
trim independently bright.

This pass uses the best native-only compromise:

- HeavyPlate5 Primary:
  - keeps the working bright Void chest chevron.
- HeavyPlate5 MetalPrimary:
  - changed from almost pure black to a restrained dark violet;
  - restores visible purple definition on the outer shoulder spikes/edge;
  - remains dark enough that the torso still reads as black armour.
- HeavyPlate5 MetalSecondary:
  - remains dark so the broad pauldron does not become neon.
- Shoulder7:
  - remains purple, but is substantially toned down because it is only the
    inner insert, not the target outer trim.

No custom/floating geometry is used.
