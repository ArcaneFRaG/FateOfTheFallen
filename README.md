# Fate of the Fallen

**Fate of the Fallen** is an Erenshor mod framework and content project. The project is structured so that shared systems live at the parent-mod level while individual classes are implemented as child modules.

## Current content

### Blightcaller

Blightcaller is the first playable class under Fate of the Fallen. Its class-specific systems remain isolated under `src/FateOfTheFallen/Classes/Blightcaller/`.

## Repository layout

```text
FateOfTheFallen/
├── FateOfTheFallen.sln
├── src/
│   └── FateOfTheFallen/
│       ├── FateOfTheFallen.csproj
│       ├── Plugin.cs
│       ├── Core/
│       ├── Classes/
│       │   └── Blightcaller/
│       ├── Patches/
│       ├── Properties/
│       └── Assets/
├── .gitignore
└── README.md
```

## Building

Open `FateOfTheFallen.sln` in Visual Studio and build the solution.

The project targets **.NET Framework 4.8**, matching the existing working mod project. The `ErenshorDir` MSBuild property defaults to the current development installation path used by the project and can be overridden for another machine:

```text
ErenshorDir=C:\Path\To\Erenshor
```

Generated build output, Visual Studio caches, and local BepInEx deployment files are intentionally excluded from Git.
