# Carapace - Mesh-Derived Native Shoulder Accent

The palette-only approach is retired for the outer shoulder trim.

The seven-colour native slot map proved:
- the chest chevron can be coloured cleanly through the native Primary channel;
- the broad pauldron uses MetalSecondary;
- the outer shoulder spike/trim detail shares MetalPrimary with torso polygons.

That means palette recolouring alone cannot make the outer shoulder trim purple
without also changing torso regions.

This pass uses a different technique:

1. Keep the working native HeavyPlate5 body palette:
   - bright Void chest chevron;
   - black torso;
   - black broad pauldrons.
2. Read HeavyPlate5's actual native SkinnedMeshRenderer mesh at runtime.
3. Copy only the upper/lateral shoulder triangles into a new mesh.
4. Reuse the SAME native bones, root bone and bind poses.
5. Reuse the native POLYGON_CustomCharacters shader and masks.
6. On that shoulder-only mesh, make only MetalPrimary Void purple while keeping
   MetalSecondary black.

The overlay is therefore made from the exact HeavyPlate5 polygons and follows
the same skinning. It is not an invented line/triangle floating in front of the
character. Vertices are lifted only 0.00035 Unity units along their native
normals to prevent z-fighting.
