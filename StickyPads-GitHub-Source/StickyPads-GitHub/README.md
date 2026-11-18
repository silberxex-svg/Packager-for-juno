# StickyPads

Juno: New Origins mod source for three parts:

- **Sticky Pad**
- **Grapple**
- **Ball Joint**

The repository contains the C# source projects and a prebuilt `.sr2-mod` package in `Releases/`.

## Install the prebuilt mod

1. Download `Releases/StickyPads-Test.sr2-mod`.
2. Copy it into your Juno: New Origins `Mods` folder.
3. Start the game.

## Source layout

- `StickyPads/` — mod C# project and custom Grapple model resources.
- `StickyPadsPackager/` — source for the AssetBundle/mod packager.
- `Releases/` — prebuilt mod package.

## Building the mod source

The `StickyPads.csproj` references Juno game assemblies from a local `StickyPads/lib/` directory. Those assemblies are not included here because they are supplied with the game and should be obtained from the install on the developer's own machine.

Copy the referenced game assemblies into `StickyPads/lib/` from the local Juno installation before building. The required references are listed in `StickyPads/StickyPads.csproj`.

Build with the .NET SDK:

```powershell
dotnet build .\StickyPads\StickyPads.csproj
```

## Building the package

`StickyPadsPackager` targets .NET 10 and restores `AssetsTools.NET` from NuGet:

```powershell
dotnet build .\StickyPadsPackager\StickyPadsPackager.csproj
```

The packager source expects these local inputs in its working directory:

- `StickyPads-Clean-Windows.bundle`
- `Strut1-Stock.xml`
- `ClassData/uncompressed.tpk`
- the built `StickyPads.dll` at `bin/Debug/netstandard2.1/StickyPads.dll`
- `Jundroo.ModTools.Core.dll` from the local game installation (the current source has a Windows Steam path configured)

These game-derived inputs are intentionally not included in this source archive. They must be obtained from the developer's own Juno installation / local build setup. The included `.sr2-mod` is ready to install without running the packager.

## Notes

This is a source snapshot made from the project ZIPs supplied by the author. The source has not been rebuilt from this GitHub-ready archive in this environment. No license has been added; choose a license before inviting others to reuse or redistribute the source.
