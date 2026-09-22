# Fate of the Fallen — Architecture

Fate of the Fallen is the parent mod. Classes are child content modules.

```text
FateOfTheFallen
└── src/FateOfTheFallen
    ├── Core/                     # Shared/reusable systems
    ├── Classes/                  # Playable class modules
    │   └── Blightcaller/         # First class module
    │       └── Patches/           # Blightcaller-only Harmony patches
    ├── Patches/                  # Parent/core Harmony patches
    ├── Properties/
    └── Assets/                   # Embedded mod assets
```

### Placement rule

If a system can reasonably be reused by a future Fate of the Fallen class, it belongs in `Core/`. If it only exists to implement Blightcaller, it stays in `Classes/Blightcaller/`.
