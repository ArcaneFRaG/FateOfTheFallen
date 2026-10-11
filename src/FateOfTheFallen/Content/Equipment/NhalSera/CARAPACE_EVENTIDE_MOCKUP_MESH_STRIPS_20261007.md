# Eventide Mockup Glow - Mesh Strip Renderer

The LineRenderer implementation is retired.

Across several tests it reported:
- all 11 paths active;
- correct character layer;
- valid world positions;
- correct front-axis;
- valid `Unlit/Color` and additive shaders;

yet Erenshor never drew a single line.

The overlay now uses ordinary Unity cube meshes (`MeshFilter` + `MeshRenderer`)
for every luminous seam segment.

Why:
- MeshRenderer is already proven by FOTF custom props and imported models.
- The core uses the Unity Standard shader with `_EMISSION`.
- The optional outer halo uses an additive particle shader.
- There is no dependence on the LineRenderer rendering path.

The existing mockup coordinates are retained:
- chest chevron;
- rib seams;
- waist/collar trim;
- pauldron strips;
- shoulder triangles;
- central void diamond.

The runtime log reports the number of actual mesh segments created.
