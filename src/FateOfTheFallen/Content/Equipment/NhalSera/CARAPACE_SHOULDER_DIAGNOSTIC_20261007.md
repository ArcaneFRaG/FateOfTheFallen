# Carapace Shoulder Diagnostic

The latest runtime log proves `ShoulderTrimL` and `ShoulderTrimR` are both the
string value `"7"`, not transform names. The current transform-name resolver can
therefore never identify them as shoulder trim.

This build keeps the current native chest appearance unchanged and adds a
one-time shoulder diagnostic that logs:

- both resolved HeavyPlate5 renderers:
  - type
  - active/enabled state
  - transform path
  - mesh
  - vertex count
  - root bone / bone count
  - world bounds
- all hierarchy nodes whose names contain shoulder / pauldron / trim
- child index 7 on any plausible shoulder container
- reflection over `Inventory.Modulars` members containing shoulder / trim /
  chest / armor, including transform/array child index 7 when possible

The purpose is to identify exactly what native `"7"` means before changing the
palette again.
