# Carapace - Working Chest + Previous Native Shoulders

This replaces the unsuccessful shoulder overlay experiments.

The latest BakeMesh test proved that the copied geometry was only affecting the
small under-shoulder region, not the native shoulder treatment the earlier build
had shown.

The correct separation is much simpler:

## Shoulders
Restore the earlier working colours at the `Item` level:

- ItemPrimaryColor = dark
- ItemSecondaryColor = purple
- ItemLeatherPrimary = dark
- ItemLeatherSecondary = purple
- ItemMetalPrimary = dark metal
- ItemMetalSecondary = purple
- ItemMetalDark = near black

Erenshor's own native modular-equipment system applies those Item colours to its
shoulder attachments.

## Chest
Leave the current `HeavyPlate5` runtime material override exactly as the
NativeChestAccent build had it.

That runtime override replaces HeavyPlate5's colours after the native equipment
setup, giving:
- bright native chest chevron
- black torso
- dark broad body metal

This means the old native shoulder colouring and the current working chest
colouring coexist without sharing an overlay implementation.

No custom mesh/line/triangle/BakeMesh shoulder geometry remains active.
