# Hexasphere Generator for Godot 4

A procedural hexagonal sphere generator for Godot 4. Generates a spherical grid of **hexagons with exactly 12 pentagons**.

The math core is written in **C++ (GDExtension)** for maximum performance, with a C# wrapper for seamless integration.

Inspired by [Em3rgencyLT's Unity Hexasphere](https://github.com/Em3rgencyLT/Hexasphere).

![Preview](preview_manytiles.png)
![Preview — wireframe](preview2.png)

## Features

- **Hexagonal sphere** — procedural generation with configurable subdivision
- **Fullerene topology** — exactly 12 pentagons, rest are hexagons
- **High performance** — C++ GDExtension core with thread-safe C# wrapper
- **Hexasphere Node** — `HexasphereNode` available in **Add Node**
- **Per-tile coloring** — custom colors via shader + `ImageTexture` (no per-tile materials)
- **Click & hover** — `TileClicked` / `TileHovered` signals with raycast hit detection
- **Custom tile data** — implement `ICellData` for biomes, heights, colors per tile
- **UV projection** — 2D map view with pan/zoom and tile selection
- **Cross-platform** — Windows, Linux (pre-built binaries included)

## Installation

1. Copy `addons/hexasphere_generator/` into your project's `addons/` folder.
2. Enable the plugin in **Project → Project Settings → Plugins**.
3. Pre-built `hexasphere.dll` for **Windows** and `libhexasphere.so` for **Linux** are included in `addons/hexasphere_generator/bin/`.

## Quick Start

### Via the Editor

1. Enable the plugin in **Project → Project Settings → Plugins**.
2. Click **Add Node (Ctrl+A)** and search for `Hexasphere`.
3. Select the node, tweak parameters (`PlanetRadius`, `SubDivision`, `HexSize`) in the Inspector, and run.

### Via Script — HexasphereNode

Inherit `HexasphereNode`, override virtual methods, subscribe to signals:

```csharp
public partial class MyPlanet : HexasphereNode
{
    public override void _Ready()
    {
        base._Ready();
        TileClicked += (idx, pos) => GD.Print($"Clicked tile {idx}");
    }

    protected override ICellData[] CreateCellData(int count)
    {
        var data = new MyTileData[count];
        for (int i = 0; i < count; i++)
            data[i] = new MyTileData { Biome = GD.Randi() % 3 };
        return data;
    }
}
```

### Via Script — NativeHexasphere

Only generation and mesh, no editor node:

```csharp
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
        
        hex.Dispose();
    }
}
```

## Signals

`HexasphereNode` emits the following signals:

| Signal | Arguments | Description |
|---|---|---|
| `TileClicked` | `(int tileIndex, Vector3 worldPosition)` | A tile was clicked |
| `TileHovered` | `(int tileIndex)` | Mouse hovers over a tile (-1 if none) |
| `TileDeselected` | — | Selection was cleared (clicked empty space) |
| `PlanetGenerated` | `(int tileCount)` | Planet generation completed |

```csharp
public partial class MyPlanet : HexasphereNode
{
    public override void _Ready()
    {
        base._Ready();
        TileClicked += OnTileClicked;
        TileHovered += OnTileHovered;
        PlanetGenerated += OnPlanetGenerated;
    }

    private void OnTileClicked(int index, Vector3 position)
    {
        GD.Print($"Clicked tile {index} at {position}");
    }

    private void OnTileHovered(int index)
    {
        GD.Print($"Hovering over tile {index}");
    }

    private void OnPlanetGenerated(int tileCount)
    {
        GD.Print($"Planet generated with {tileCount} tiles");
    }
}
```

## Custom Cell Data

Implement `ICellData` for custom per-tile data and override `GetColor` in a custom visual controller:

```csharp
using Godot;

public class MyTileData : ICellData
{
    public Color color;
    public float Height;
    public int Biome;
}

public partial class MyVisual : HexasphereVisualController
{
    public override Color GetColor(ICellData cellData)
    {
        if (cellData is MyTileData tile)
            return tile.Height > 0.5f ? Colors.Green : Colors.Brown;
        return base.GetColor(cellData);
    }
}
```

To provide custom data, override `CreateCellData` in a subclass of `HexasphereNode`:

```csharp
using Godot;

public partial class MyPlanet : HexasphereNode
{
    protected override ICellData[] CreateCellData(int count)
    {
        var data = new MyTileData[count];
        for (int i = 0; i < count; i++)
            data[i] = new MyTileData { color = Colors.Gray, Height = 1f };
        return data;
    }
}
```

## Overridable Methods

Key virtual methods in `HexasphereNode`:

| Method | Purpose |
|---|---|
| `CreateCellData(int count)` | Create array of `ICellData[]` with custom per-tile data |
| `OnShaderReady()` | Called when the visual shader is initialized |
| `FinalizePlanet()` | Called after planet generation completes |
| `FindTileIndexByDirection(Vector3 direction)` | Custom hit-test: returns tile index by ray direction |
| `BuildSpatialIndex(NativeHexasphere hex)` | Build custom spatial index for tile lookup |

Key virtual methods in `HexasphereVisualController`:

| Method | Purpose |
|---|---|
| `GetColor(ICellData cellData)` | Return a color for a given tile |
| `SetRoughness(float value)` | Set material roughness |
| `Draw(ICellData[] cellDatas, ...)` | Full redraw of all tiles |
| `InitShaderMaterial()` | Initialize the shader material |

## Export Properties

`HexasphereNode` exposes the following properties in the Inspector:

### Geometry
| Property | Type | Default | Description |
|---|---|---|---|
| `PlanetRadius` | `float` | `20` | Radius of the sphere in world units |
| `SubDivision` | `int` | `20` | Number of icosahedron subdivisions (higher = more tiles) |
| `GenerationSeed` | `int` | `-1` | Seed for random color generator (-1 = random) |

### Interaction
| Property | Type | Default | Description |
|---|---|---|---|
| `IsClickEnabled` | `bool` | `true` | Enable tile clicking |
| `IsHoverEnabled` | `bool` | `true` | Enable tile hover detection |

### Visual
| Property | Type | Default | Description |
|---|---|---|---|
| `HexSize` | `float` | `1.0` | Relative size of each hexagonal tile (0.1–1.0) |
| `IsEmissive` | `bool` | `false` | Use emissive rendering |
| `IsClickVisualEnabled` | `bool` | `true` | Highlight selected tile |
| `ClickColor` | `Color` | `Black` | Color for selected tile |
| `IsHoverVisualEnabled` | `bool` | `true` | Highlight hovered tile |
| `HoverColor` | `Color` | `Red` | Color for hovered tile |

### Borders
| Property | Type | Default | Description |
|---|---|---|---|
| `IsBordering` | `bool` | `true` | Render tile borders |
| `BorderColor` | `Color` | `White` | Color of tile borders |

## Architecture

```
┌──────────────────────────────────────────────────────┐
│  C++ (native/src/)                                   │
│  Point → Face → Tile → Hexasphere                    │
│         ↕                                            │
│  NativeHexasphere (RefCounted bridge)                │
│  - generate()                                        │
│  - build_mesh()         → Dictionary (ArrayMesh)     │
│  - get_border_data()    → Dictionary                 │
│  - get_tile_center()    → Vector3                    │
│  - get_tile_points()    → Vector3[]                  │
│  - get_all_tile_centers() → Vector3[]                │
└──────────────┬───────────────────────────────────────┘
               │ GDExtension
┌──────────────▼───────────────────────────────────────┐
│  C# (addons/hexasphere_generator/scripts/)           │
│  hexasphere_node/                                    │
│    NativeHexasphere.cs          — thread-safe wrapper│
│    HexasphereNode.cs            — main node          │
│    HexasphereVisualController   — visual rendering   │
│    PlanetBorderRenderer         — border lines       │
│    HexasphereInputRouter        — input arbitration  │
│  hexasphere_uv_projector/                            │
│    HexasphereProjectorController — 2D UV map view    │
│    UvCamera2D                    — pan/zoom camera   │
└──────────────────────────────────────────────────────┘
```

- **C++ layer** — pure math: icosahedron subdivision, tile boundary computation, mesh array generation. No Godot dependencies in the core classes.
- **NativeHexasphere** — a `RefCounted` registered with GDExtension. Exposes `generate()`, `build_mesh()`, `get_border_data()`, etc.
- **C# layer** — orchestration, Godot node management, shader material setup, border rendering, input routing, UV projection.

## Building the Native Library

### From Source

```bash
cd native
scons target=template_debug
```

The binary is output to `addons/hexasphere_generator/bin/`.

| Platform | `platform=` |
|---|---|
| Windows | (default) |
| Linux | `platform=linux` |
| macOS | `platform=macos` |

Requires a working C++17 compiler, Python 3, and SCons.

## License

See [LICENSE](LICENSE) for details.
