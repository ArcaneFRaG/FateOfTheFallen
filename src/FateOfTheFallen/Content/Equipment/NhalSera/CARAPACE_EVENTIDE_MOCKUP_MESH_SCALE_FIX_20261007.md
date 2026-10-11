# Eventide Mockup Mesh Overlay - World Scale Fix

The mesh-strip overlay reported all 19 segments as active but none were visible.

The strip dimensions were assigned with `localScale`. Erenshor's native Synty
character hierarchy contains 0.01-scale transforms, so an overlay parented under
that hierarchy can make a nominal 1 cm strip render at ~0.1 mm or smaller.

This pass sets the strip dimensions in **world scale**, compensating for the
parent's `lossyScale`.

It also logs:
- overlay host world/lossy scale;
- actual world-space bounds of the first glow core mesh.

For the calibration pass the core/halo are made slightly thicker so visibility
is obvious. Once confirmed visible, those dimensions can be reduced again while
the mockup positions are tuned.
