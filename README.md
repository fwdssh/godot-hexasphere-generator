# Hexasphere Generator for Godot 4

A procedural hexagonal sphere generator for Godot 4. Generates a spherical grid of **hexagons with exactly 12 pentagons**.

The math core is written in **C++ (GDExtension)** for maximum performance, with a C# wrapper for seamless integration.

![Preview](preview.png)
![Preview — UV projector](preview2.png)

## Features

- **Hexagonal sphere** — procedural generation with configurable subdivision
- **Fullerene topology** — exactly 12 pentagons, rest are hexagons
- **High performance** — C++ GDExtension core with C# wrapper
- **Hexasphere Node** — `HexasphereNode` available in **Add Node**
- **Per-tile coloring** — custom colors via shader + `ImageTexture` (no per-tile materials)
- **Click & hover** — `TileClicked` / `TileHovered` signals with raycast hit detection
- **Custom tile data** — implement `ICellData` for biomes, heights, colors per tile
- **UV projection** — detailed or raster 2D map with pan/zoom, tile selection and automatic switching for large grids
- **Cross-platform** — Windows (pre-built binary included), Linux and macOS can be built from source

## Installation

Use **Godot .NET**: the plugin's nodes are implemented in C#, including when called from GDScript. This project currently targets Godot.NET.Sdk 4.7.2 and .NET 9; use a compatible .NET SDK to build the C# project.

1. Copy `addons/hexasphere_generator/` into your project's `addons/` folder.
2. Enable the plugin in **Project → Project Settings → Plugins**.
3. Pre-built `hexasphere.dll` for **Windows** is included in `addons/hexasphere_generator/bin/`. For Linux and macOS, build from source (see [Building the Native Library](#building-the-native-library)).

## Quick Start

### Via the Editor

1. Enable the plugin in **Project → Project Settings → Plugins**.
2. Click **Add Node (Ctrl+A)** and search for `Hexasphere`.
3. Select the node, tweak parameters (`PlanetRadius`, `SubDivision`, `HexSize`) in the Inspector, and run.


### Via Script — GDScript

Use `HexasphereNode` from GDScript by adding it as a child node and connecting to signals:

```gdscript
extends Node3D

@onready var sphere: Node3D = $MyHexasphere

func _ready():
    sphere.PlanetGenerated.connect(_on_planet_generated)

func _on_planet_generated(tile_count: int):
    var colors: Array[Color] = []
    colors.resize(tile_count)
    for i in range(tile_count):
        var center = sphere.GetTileCenter(i)
        colors[i] = _calculate_color(center)
    sphere.SetAllTileColors(colors)

func _calculate_color(tile_center: Vector3) -> Color:
    var n := tile_center.normalized()
    # Use noise based on sphere position to generate colors
    var noise = sin(n.x * 4.0) * cos(n.z * 3.0)
    var hue = lerp(0.5, 0.7, (noise + 1.0) / 2.0)
    return Color.from_hsv(hue, 0.8, 0.9)
```

### Via Script — C# (Simple)

Use `HexasphereNode` as a child node and apply colors via signals:

```csharp
using Godot;

public partial class MyPlanet : Node3D
{
    private HexasphereNode _hexasphere;

    public override void _Ready()
    {
        _hexasphere = GetNode<HexasphereNode>("MyHexasphere");
        _hexasphere.PlanetGenerated += OnPlanetGenerated;
    }

    private void OnPlanetGenerated(int tileCount)
    {
        Color[] colors = new Color[tileCount];
        for (int i = 0; i < tileCount; i++)
        {
            Vector3 center = _hexasphere.GetTileCenter(i);
            colors[i] = CalculateColor(center);
        }
        _hexasphere.SetAllTileColors(colors);
    }

    private Color CalculateColor(Vector3 tileCenter)
    {
        Vector3 n = tileCenter.Normalized();
        float noise = Mathf.Sin(n.X * 4.0f) * Mathf.Cos(n.Z * 3.0f);
        float hue = Mathf.Lerp(0.5f, 0.7f, (noise + 1.0f) / 2.0f);
        return Color.FromHsv(hue, 0.8f, 0.9f);
    }
}
```

### Via Script — C# (Advanced)

Inherit `HexasphereNode` directly for custom cell data and visual controller:

```csharp
using Godot;

class MyCellData : ICellData
{
    public float Height;
}

public partial class MyPlanet : HexasphereNode
{
    protected override ICellData[] CreateCellData(int count, Vector3[] centers)
    {
        var cells = new MyCellData[count];
        for (int i = 0; i < count; i++)
        {
            cells[i] = new MyCellData();
            Vector3 n = centers[i].Normalized();
            cells[i].Height = Mathf.Sin(n.Y * 10.0f) * 0.5f + 0.5f;
        }
        return cells;
    }

    protected override void SetVisualController()
    {
        VisualController = new MyVisualController();
        VisualController.Name = "MyVisual";
        AddChild(VisualController);
    }
}

public partial class MyVisualController : HexasphereVisualController
{
    public override Color GetColor(ICellData cellData)
    {
        if (cellData is MyCellData tile)
        {
            float hue = Mathf.Lerp(0.5f, 0.7f, tile.Height);
            return Color.FromHsv(hue, 0.8f, 0.9f);
        }
        return base.GetColor(cellData);
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

`HexasphereProjectorController` emits the following signals:

| Signal | Arguments | Description |
|---|---|---|
| `TileClicked` | `(int tileIndex)` | A tile was clicked in the 2D projection |
| `TileHovered` | `(int tileIndex)` | Hovered tile changed (-1 if none) |
| `TileDeselected` | — | Selection was cleared in the projection |
| `ProjectionClosed` | — | The UV projection view was closed |
| `ProjectionReady` | — | A projection build request completed, including cache reuse |
| `ProjectionBuildFailed` | `(string error)` | A projection build failed |
| `ProjectionModeChanged` | `(int activeMode)` | A successful build changed the active mode |

## Input Actions

| Action | Key | Description |
|---|---|---|
| `ui_toggle_uv_map` | `1` | Open UV map projection (while hovering a tile) |
| `ui_close_uv_map` | `Escape` | Close UV map projection |

## UV Projection Modes

Configure `HexasphereProjectorController` through standard Inspector properties or C#/GDScript. Settings take effect on the next `BuildMap2D`, `PrepareMap`, `PrepareUvProjector`, or `RebuildMap` call; changing a property does not start an expensive build automatically.

| Setting | Default | Meaning |
|---|---|---|
| `ProjectionMode` | `Auto` (0) | `Auto` (0), `DetailedMesh` (1), or `Raster` (2) |
| `MaxDetailedTileCount` | 200,000 | Auto uses raster above this count; equality uses detailed geometry |
| `MapSize` | 1920 × 1080 | Displayed map size in local 2D units |
| `RasterQuality` | `Medium` (2) | `Custom` (0), `Low` (1): 1024 × 512, `Medium` (2): 2048 × 1024, `High` (3): 4096 × 2048 |
| `RasterResolution` | 2048 × 1024 | Pixel dimensions when quality is `Custom`; independent of `MapSize` |

Raster mode draws two triangles and stores a pixel-to-tile ID map for hover, selection, and picking. It avoids a per-tile UV rendering mesh and its hit-test triangle storage. Native topology generation and initial rasterization still depend on tile count. The 200,000-tile threshold is configurable, not a hardware-independent performance guarantee; raster mode does not simplify the 3D sphere.

Raster detail and picking are limited by pixel resolution. Cells smaller than a pixel may not be represented; zooming does not add detail. Dimensions must be 1–8192 with at most 16,777,216 pixels total. ID/color buffers and textures each require memory proportional to the raster resolution.

### From C#

After the sphere's `PlanetGenerated` signal, configure its linked projector and prepare it without changing visibility or the active camera:

```csharp
projector.ProjectionMode = UvProjectionMode.Raster;
projector.RasterQuality = UvRasterQuality.Custom;
projector.RasterResolution = new Vector2I(2048, 1024);

// sphere.UvProjectorPath must point to this projector.
if (sphere.PrepareUvProjector())
{
    int tile = projector.GetTileAtUv(new Vector2(0.4f, 0.4f));
    projector.SelectTile(tile);
}

// Apply changed settings to an already prepared map.
projector.ProjectionMode = UvProjectionMode.Auto;
projector.RebuildMap();
```

For standalone C# use with a generated `NativeHexasphere`, call `projector.PrepareMap(hexasphere, colors)`. The existing `BuildMap2D(hexasphere, colors, mapSize)` signature is preserved. The native wrapper is a C# class; GDScript should prepare through `HexasphereNode` instead.

### From GDScript

```gdscript
# Run after the linked sphere emits PlanetGenerated.
var projector = $Sphere/Projector
projector.ProjectionMode = 2  # Raster; Auto=0, DetailedMesh=1
projector.RasterQuality = 0   # Custom; Low=1, Medium=2, High=3
projector.RasterResolution = Vector2i(2048, 1024)

if $Sphere.PrepareUvProjector():
    var tile = projector.GetTileAtUv(Vector2(0.4, 0.4))
    projector.SelectTile(tile)

projector.RasterResolution = Vector2i(4096, 2048)
projector.RebuildMap()
```

### Projection API and caching

| Method | Behavior |
|---|---|
| `sphere.PrepareUvProjector()` | Prepares the configured projector without opening it; returns success |
| `projector.RebuildMap()` | Applies current settings to the previously prepared map |
| `projector.UpdateColors(colors)` | Updates tile colors without rebuilding geometry or IDs; supply one color per tile |
| `projector.GetTileAtUv(uv)` | Returns the tile at normalized UV coordinates, or -1 outside the map/when not ready |
| `projector.SelectTile(id)` | Sets selection; returns false for an invalid ID or an unready map; -1 clears selection |
| `projector.ClearSelection()` | Clears selection and hover |
| `projector.SetSelection(selectedId, hoveredId)` | Updates both highlight indices directly |
| `projector.GetSelectedTileIndex()` / `GetHoveredTileIndex()` | Reads the current highlight indices |
| `projector.IsProjectionReady()` | Whether the last build succeeded |
| `projector.GetActiveProjectionMode()` | Actual mode: DetailedMesh=1 or Raster=2 after a successful build; Auto=0 before first build |
| `projector.GetEffectiveRasterResolution()` | Active raster dimensions, or zero in detailed mode |
| `projector.GetBuildState()` | NotBuilt=0, Building=1, Ready=2, Failed=3 |
| `projector.GetLastBuildError()` | Error from the last failed build, cleared at the next build request |
| `projector.MarkDirty()` | Invalidates geometry for the next build; use after regenerating the same native instance |

Programmatic selection changes highlights without emitting mouse-click signals. Existing `TileClicked`, `TileHovered`, and `TileDeselected` signals work in both render modes.

Builds are synchronous. On failure, `BuildMap2D` emits `ProjectionBuildFailed`, records the error/state, and invalidates partial caches for a subsequent retry. `PrepareUvProjector()` returns false when the planet/projector is unavailable or its build fails.

Reopening an unchanged map reuses its mesh and raster IDs. Recoloring and highlight changes do not rasterize again. Changing only `MapSize` reuses raster IDs; changing resolution, topology, or calling `MarkDirty()` invalidates them. Pan, zoom, and selection are preserved on settings changes and reopening; a different native sphere or tile count resets the camera and highlight indices.

UV uses its own `Camera2D`; the sphere uses `Camera3D`. Custom 3D camera controllers should suspend input and movement while `HexasphereInputRouter.IsUvProjectionOpen` is true. Setting a camera's `Current` property to false does not stop its scripts. Both included 3D camera controllers respect UV mode.



## Verification

Use the [manual UV projection checklist](docs/testing/uv-projection-checklist.md) for interaction, mode changes, seams/poles and error recovery.

## Custom Cell Data

The convenience setters `SetTileColor` and `SetAllTileColors` target `HexCellData`. For custom cell models, override `CreateCellData`, `SetVisualController`, and `GetColor`, as shown by Earth in `scenes/sol_example/`; the visual controller maps your data to colors.

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
    protected override ICellData[] CreateCellData(int count, Vector3[] centers)
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
| `CreateCellData(int count, Vector3[] centers)` | Create array of `ICellData[]` with custom per-tile data |
| `SetVisualController()` | Assign a custom visual controller subclass |
| `GenerateInBackground(NativeHexasphere hexasphere)` | Override to customize background generation logic |
| `OnShaderReady()` | Called when the visual shader is initialized |
| `FinalizePlanet()` | Called after planet generation completes |
| `FindTileIndexByDirection(Vector3 direction)` | Custom hit-test: returns tile index by ray direction |
| `BuildSpatialIndex(Vector3[] centers)` | Build custom spatial index for tile lookup |

Key virtual methods in `HexasphereVisualController`:

| Method | Purpose |
|---|---|
| `GetColor(ICellData cellData)` | Return a color for a given tile |
| `DrawColors(ICellData[] cellDatas)` | Draw all tile colors into the texture |
| `SetSelection(Color? selectedColor, int selectedIdx, Color? hoverColor, int hoverIdx)` | Handle tile selection/hover visual feedback |
| `ApplyGenerated(ArrayMesh mesh, bool isBorderVisible, Shader colorsShader, Shader bordersShader)` | Called when mesh generation is complete |
| `SetNativeHexasphere(NativeHexasphere hex)` | Assign the native hexasphere instance |
| `SetBorderColor(Color color)` | Update border color at runtime |
| `SetEmissive(bool emissive)` | Toggle emissive rendering |
| `InitShaderMaterial()` | Initialize the shader material |
| `Draw(ICellData[] cellDatas, ...)` | Full redraw of all tiles |
| `DisposeHexasphere()` | Clean up the native hexasphere instance |

Key virtual methods in `PlanetBorderRenderer`:

| Method | Purpose |
|---|---|
| `SetVisible(bool visible)` | Show or hide the border mesh |
| `BuildStaticBorders(NativeHexasphere hexasphere, ShaderMaterial planetMaterial, Shader borderShader)` | Build the static border line mesh |
| `UpdateBorders(int selectedIdx = -1)` | Update border shader with selected tile index |
| `SetBorderColor(Color color)` | Set the color used for rendering borders |


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
│  - get_all_tile_neighbors() → Dictionary (CSR)       │
└──────────────┬───────────────────────────────────────┘
               │ GDExtension
┌──────────────▼───────────────────────────────────────┐
│  C# (addons/hexasphere_generator/scripts/)           │
│  hexasphere_node/                                    │
│    NativeHexasphere.cs          — GDExtension wrapper│
│    HexasphereNode.cs            — main node          │
│    HexasphereVisualController   — visual rendering   │
│    PlanetBorderRenderer         — border lines       │
│    HexasphereInputRouter        — input arbitration  │
│  hexasphere_uv_projector/                            │
│    HexasphereProjectorController — 2D UV map view    │
│    UvCamera2D                    — pan/zoom camera   │
└──────────────────────────────────────────────────────┘
```


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

Requires a working C++17 compiler, Python 3, and SCons.


