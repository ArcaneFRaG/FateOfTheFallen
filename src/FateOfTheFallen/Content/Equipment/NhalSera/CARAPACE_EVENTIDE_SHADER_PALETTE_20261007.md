# Carapace - Eventide Shader Palette Remap

The runtime logs identified the native material as:

- Renderer: `HeavyPlate5`
- Material: `PlayerMat`
- Shader: `Shader Graphs/POLYGON_CustomCharacters`
- Main texture: none
- Conventional `_EmissionColor`: absent

This means Eventide is a Synty custom-character palette material rather than a
normal textured armour material.

The new implementation no longer tries to recover accent pixels from a texture.
Instead it:

1. clones `PlayerMat` for only the Carapace renderer;
2. keeps `POLYGON_CustomCharacters`;
3. enumerates the Shader Graph's exposed color properties;
4. preserves skin/hair/face-related color slots;
5. remaps purple / accent-like slots to high-intensity void purple;
6. remaps neutral armour slots to black / dark gunmetal;
7. increases any exposed glow/emission float properties when present.

Purple is written as HDR colour values above 1.0. If the game's camera has bloom,
those areas can bloom even though this Shader Graph does not expose the standard
`_EmissionColor` property.

The runtime now logs how many shader color properties were classified as accent
versus dark metal. This gives a direct diagnostic if Eventide uses unusual slot
names.
