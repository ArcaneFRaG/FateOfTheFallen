# Carapace - Property Block Split Fix

Observed result after restoring the native Item-level shoulder palette:

- the correct old shoulder appearance returned;
- the HeavyPlate5 chest chevron disappeared;
- reapplying Material.SetColor every LateUpdate did not restore it.

This strongly indicates the native equipment colour path is overriding the
shader through `MaterialPropertyBlock` state. Property-block values override
the material's own colour values, so changing the Material object alone cannot
win.

This build keeps the working separation:

## Native shoulders

The Carapace Item retains the previous native Item colour palette. Erenshor can
therefore colour its native shoulder attachments normally.

## HeavyPlate5 chest/body

FOTF captures the original renderer and per-material property blocks when the
Carapace style is installed.

It then overrides the seven Synty colour channels on HeavyPlate5 through:
- renderer-level MaterialPropertyBlock;
- per-material-index MaterialPropertyBlock.

Both are applied because Unity's indexed property blocks take precedence over
the renderer-level block.

The exact working NativeChestAccent palette is used.

The override is reasserted in LateUpdate, and the original property blocks are
restored on unequip/shutdown.

No custom geometry is used.
