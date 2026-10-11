# Carapace of the Event Horizon - Mockup Glow Overlay

The native Eventide shader can recolor its existing palette regions, but it
cannot create new glowing seam geometry in positions that the native material
does not separately expose.

To match the FOTF mockup, the Carapace now adds a dedicated additive overlay
that follows the player's live Synty skeleton.

Current chest-slot overlay:
- upper chest left/right chevron
- lower rib left/right seams
- lower chest / waist trim
- collar seam
- left/right pauldron edge strips
- left/right shoulder crystal triangles
- central void diamond

This is deliberately separate from the native Eventide material. The Eventide
geometry, skinning, animation and shader remain native; FOTF owns only the
runtime glow-line GameObjects.

The same overlay component is palette-driven and can use:
- green for Blight
- purple for Void
- white for Undeath
- orange/red for Demonic

The full turnaround mockup also contains glow on the helmet, bracers/gloves,
waist/skirt, knees and boots. Those belong to other equipment slots. Carapace is
a Chest item, so this implementation adds the mockup's chest/shoulder lines
only; matching the full turnaround requires corresponding FOTF items for the
other Eventide slots.
