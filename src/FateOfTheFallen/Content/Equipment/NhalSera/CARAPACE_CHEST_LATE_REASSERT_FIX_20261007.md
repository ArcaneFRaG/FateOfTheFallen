# Carapace - Chest Late Reassert Fix

Observed result:
- restoring the earlier Item-level native palette brought the shoulders back;
- the working chest accent disappeared.

Cause:
Erenshor's native equipment colour system uses the Carapace `Item...Color`
values and can mutate the same HeavyPlate5 material instance after FOTF has
assigned its chest variant.

The previous FOTF reassertion only checked whether the material reference had
changed. If Erenshor changed colour properties on the same material object, FOTF
did nothing.

Fix:
- retain the old Item-level shoulder palette;
- retain the working NativeChestAccent HeavyPlate5 palette;
- add `EV_LateUpdate()`;
- every frame, after the normal equipment LateUpdate paths, reapply the exact
  working HeavyPlate5 chest colour properties to FOTF's existing variant
  material instances.

Resulting ownership:
- native shoulder attachments = Erenshor Item colours;
- HeavyPlate5 chest/body = FOTF LateUpdate chest palette.

No custom shoulder/chest geometry is used.
