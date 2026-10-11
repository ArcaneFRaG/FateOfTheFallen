# Carapace - Native Synty Palette Implementation

The detached mockup overlay is removed.

The previous mesh/triangle approach could render, but it floated in front of or
behind the player and was only an approximation of the concept art.

`PlayerMat` uses Synty's `POLYGON_CustomCharacters` shader. That shader already
contains exact masks for the armour regions through seven standard Synty colour
properties:

- `_Color_Primary`
- `_Color_Secondary`
- `_Color_Leather_Primary`
- `_Color_Leather_Secondary`
- `_Color_Metal_Primary`
- `_Color_Metal_Secondary`
- `_Color_Metal_Dark`

Carapace now uses those properties directly.

Primary channels become black/dark gunmetal.
Secondary channels become HDR Void purple.

Because the masks are part of the native Eventide shader, every accent now sits
on the actual Eventide geometry and follows its exact polygons/animation. No
extra triangles, cubes or lines are positioned in front of the character.

The Carapace Item's corresponding `Item...Color` fields are set to the same
palette so Erenshor's own equipment-colour path and FOTF's runtime material pass
agree with each other.
