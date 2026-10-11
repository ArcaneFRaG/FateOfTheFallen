# Eventide Mockup Glow - Visibility Fix

The previous build logged all 11 LineRenderer groups as active, but none were
visible in-game.

This pass fixes the rendering side rather than changing their design:

- glow host and every LineRenderer now use the actor's character layer;
- additive materials are forced to a late transparent render queue;
- LineRenderers use a very high sorting order;
- glow lines are pushed slightly farther off the armor to avoid z-fighting;
- front-facing direction uses the animated native `Head` bone instead of only
  the actor root transform;
- the activation log now prints layer, facing vector and chest-center world
  position.

The line layout itself remains the FOTF mockup layout.
