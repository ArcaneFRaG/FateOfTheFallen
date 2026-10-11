# Carapace Native Debug Slot Map Build

Purpose: identify exactly which Eventide chest / shoulder / trim polygons are
controlled by each native Synty colour slot.

This build intentionally disables all custom Eventide overlay geometry.
Only the native HeavyPlate5 / POLYGON_CustomCharacters palette is active.

Slot colour mapping:

- ItemPrimaryColor / `_Color_Primary` = Red
- ItemSecondaryColor / `_Color_Secondary` = Green
- ItemLeatherPrimary / `_Color_Leather_Primary` = Blue
- ItemLeatherSecondary / `_Color_Leather_Secondary` = Yellow
- ItemMetalPrimary / `_Color_Metal_Primary` = Magenta
- ItemMetalSecondary / `_Color_Metal_Secondary` = Cyan
- ItemMetalDark / `_Color_Metal_Dark` = White

Use this build to take screenshots from front / side / rear. Once we know which
native slot owns each chest region, we can decide whether the native Eventide
chest can be driven the same way as the shoulders.
