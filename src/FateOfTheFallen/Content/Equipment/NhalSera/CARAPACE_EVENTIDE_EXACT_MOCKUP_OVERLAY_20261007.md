# Carapace - Exact Eventide Mockup Overlay

The previous overlay finally proved the rendering path works, but it exposed two
design mistakes:

1. `-Head.forward` was the back of this native Synty character.
2. The overlay itself was a generic circuit/V-line design, not the concept art.

This replacement uses `+Head.forward` and redraws only the actual Carapace
details visible in the Void turnaround mockup:

- two large glowing triangular chest facets;
- two narrow side-chest shards;
- a thin glowing lower/front edge on each pauldron;
- one glowing shoulder crystal on each side.

Removed from the chest overlay:
- central diamond;
- collar bar;
- waist bar;
- rib U-shape;
- arbitrary back-facing line work.

The full mockup also shows glow on helmet, bracers/gloves, skirt, knees and
boots. Those are separate equipment slots and are intentionally not drawn by the
Carapace Chest item.
