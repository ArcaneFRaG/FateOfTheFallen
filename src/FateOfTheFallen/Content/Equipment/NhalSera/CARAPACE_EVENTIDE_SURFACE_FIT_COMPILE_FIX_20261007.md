# Eventide Surface Fit - Compile Fix

The previous surface-fit pass added a fallback reference to `_spine03` in
`GetCurrentFrontPlane()` but did not declare or initialize that field.

This fix adds:

```csharp
private Transform _spine03;
```

and resolves it from the native Synty skeleton with:

```csharp
_spine03 = FindBone(actorRoot, "Spine_03");
```

No visual tuning or overlay placement logic was changed.
