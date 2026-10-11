# Carapace - Previous Working Shoulders Only

This build deliberately splits chest and shoulders.

## Chest

The project is based on the NativeChestAccent build where the native Eventide
chest chevron was already working.

No chest palette values were changed.

## Shoulders

The earlier native Synty palette that visibly coloured the shoulders is restored
only on a shoulder-only copy of HeavyPlate5.

The old working palette is:

- Primary = dark
- Secondary = HDR Void purple
- Leather Primary = dark
- Leather Secondary = HDR Void purple
- Metal Primary = dark metal
- Metal Secondary = HDR Void purple
- Metal Dark = near black

Only upper/lateral HeavyPlate5 triangles are copied into the shoulder pass.
Those copied polygons retain the original HeavyPlate5:
- vertices
- UVs
- vertex colours
- bone weights
- bind poses
- bones
- native POLYGON_CustomCharacters shader

The underlying working chest is never recoloured by the shoulder pass.

There is no invented chest overlay.
