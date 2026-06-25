# Hexasphere Generator for Godot 4

A procedural hexagonal sphere generator for Godot 4. Generates a spherical grid of hexagons with exactly 12 pentagons (fullerene topology).

The math core is written in **C++ (GDExtension)** for maximum performance, with a thin C# wrapper for seamless integration.

Inspired by [Em3rgencyLT's Unity Hexasphere](https://github.com/Em3rgencyLT/Hexasphere).

![Preview](preview.png)
![Preview2](preview2.png)

## Requirements

- Godot 4.7+
- .NET 9 SDK
- A C++ compiler (only if rebuilding the native DLL)

## Installation

1. Copy `addons/hexasphere_generator/` into your project's `addons/` folder.
2. Enable the plugin in **Project → Project Settings → Plugins**.
3. Pre-built `hexasphere.dll` for Windows is included in `addons/hexasphere_generator/bin/`.

For other platforms you need to [build from source](#building-the-c-dll-from-source).

## Quick Start

### Via the Scene

1. Instance `hexasphere.tscn` (or `addons/hexasphere_generator/example.tscn`) into your scene.
2. Select the `Hexasphere` node and tweak parameters in the Inspector.
3. Run — the sphere generates on a background thread.

### Via Script

```csharp
using Godot;

public partial class MyPlanet : Node3D
{
    public override void _Ready()
    {
        var hex = new NativeHexasphere();
        hex.Generate(10f, 20, 1f);

        var result = hex.BuildMesh();
        var mesh = (ArrayMesh)result["mesh"];

        var mi = new MeshInstance3D();
        mi.Mesh = mesh;
        AddChild(mi);
    }
}
```

## Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `PlanetRadius` | float | 20 | Sphere radius |
| `SubDivision` | int | 20 | Grid density (tile count ∝ divisions²) |
| `HexSize` | float (0–1) | 1.0 | 1.0 = gapless, lower = gaps between tiles |
| `IsBordering` | bool | true | Show tile borders |
| `BorderColor` | Color | White | Border line color |

## Architecture

```
┌─────────────────────────────────────────────┐
│  C++ (native/src/)                          │
│  Point → Face → Tile → Hexasphere          │
│         ↕                                   │
│  NativeHexasphere (RefCounted bridge)       │
│  - generate()                               │
│  - build_mesh()    → ArrayMesh              │
│  - get_border_data() → Dictionary           │
│  - get_build_data()  → Dictionary           │
└──────────────┬──────────────────────────────┘
               │ GDExtension
┌──────────────▼──────────────────────────────┐
│  C# (addons/scripts/hexasphere_node/)        │
│  NativeHexasphere.cs  — thin wrapper        │
│  HexasphereNode.cs    — main node, async     │
│  HexasphereVisualController.cs              │
│  PlanetBorderRenderer.cs — border lines      │
└─────────────────────────────────────────────┘
```

- **C++ layer** — pure math: icosahedron subdivision, tile boundary computation, mesh array generation. No Godot dependencies in the core classes.
- **NativeHexasphere** — a `RefCounted` registered with GDExtension. Exposes `generate()`, `build_mesh()`, `get_border_data()`, etc.
- **C# layer** — orchestration, Godot node management, shader material setup, border rendering.

`build_mesh()` builds the `ArrayMesh` entirely in C++ using direct vertex/normal/UV2 arrays + `add_surface_from_arrays()`, bypassing `SurfaceTool` entirely.

## Building the C++ DLL from Source

```bash
cd native
scons target=template_debug
```

The DLL is output to `addons/hexasphere_generator/bin/hexasphere.dll`.

For other platforms:

| Platform | `platform=` |
|---|---|
| Windows | (default) |
| Linux | `platform=linux` |
| macOS | `platform=macos` |

Requires a working C++17 compiler and Python 3 + SCons.

## Benchmark

`Divisions=100 → 100,002 tiles` on a 12-core machine:

| Stage | C# (original) | C++ bulk data | C++ direct arrays |
|---|---|---|---|
| Generate | 249 ms | 248 ms | 253 ms |
| BuildMesh | 1197 ms | 472 ms | **142 ms** |
| **Total** | **1446 ms** | **720 ms** | **395 ms** |

`build_mesh()` on C++ is ~8× faster than the original C# SurfaceTool approach.

## License

MIT — see `LICENSE`.

Copyright (c) 2021 Em3rgencyLT, 2026 fwdssh
