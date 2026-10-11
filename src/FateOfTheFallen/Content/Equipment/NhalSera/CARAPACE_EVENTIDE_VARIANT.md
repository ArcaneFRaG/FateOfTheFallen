# Carapace of the Event Horizon - Native Eventide Visual

The Carapace no longer uses `FOTF_Carapace_of_the_Event_Horizon` from the
equipment AssetBundle.

Runtime visual source:
- Native Erenshor item template: `Eventide Chestguard`
- Native Synty rigging / animation / trim objects are retained unchanged.
- Fate of the Fallen clones the native renderer materials at runtime.
- Neutral Eventide texture regions are remapped to black / dark gunmetal.
- Native purple Eventide texture regions become brighter void-purple.
- The same purple regions are used as an emission mask.

The palette code already contains four reusable Fallen themes:
- Blight: toxic green
- Void: violet/purple
- Undeath: spectral white
- Demonic: molten orange/red

`Carapace of the Event Horizon` currently selects the Void theme.

The old custom Carapace prefab may remain in `fotf_equipment`; it is no longer
looked up or instantiated by the runtime and can be removed from a future
AssetBundle rebuild.
