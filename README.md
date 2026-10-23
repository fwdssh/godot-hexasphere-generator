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
- **Native platform mappings** — Windows x86-64 debug/release binaries included; Linux, macOS and Windows ARM64 require source builds and have not been verified here

## Installation

Use **Godot .NET**: the plugin's nodes are implemented in C#, including when called from GDScript. This project currently targets Godot.NET.Sdk 4.7.2 and .NET 9; use a compatible .NET SDK to build the C# project.

1. Copy `addons/hexasphere_generator/` into your project's `addons/` folder.
2. Enable the plugin in **Project → Project Settings → Plugins**.
3. Pre-built **Windows x86-64 debug and release** libraries are included in `addons/hexasphere_generator/bin/`; keep both libraries and `hexasphere.gdextension` together. Other mapped platforms/architectures require a source build (see [Building the Native Library](#building-the-native-library)).
4. If using keyboard UV controls, configure the input actions listed under [Input Actions](#input-actions) in your project.

The verified combination is Godot **4.7.2 .NET**, Godot.NET.Sdk **4.7.2**, a **.NET 9** target and Windows x86-64. The native descriptor requires Godot 4.7 or newer to match the generated bindings; that floor is not a claim that every other Godot/.NET/platform combination has been tested. Release-native library loading is checked separately from exported-game validation.

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

## Controlled Generation

`AutoGenerate` defaults to `true`, preserving automatic generation in `_Ready`. Disable it in the Inspector or before adding the node to start explicitly. Lifecycle methods run on the main thread after `_Ready`.

| Method | Behavior |
|---|---|
| `GeneratePlanet()` / `RestartGeneration()` | Capture current settings and start a new request; cancel an active request and clear the previous planet, selection and its UV cache. Return false when the node cannot start or settings are invalid. |
| `CancelGeneration()` | Cancel an active request without blocking; return false if none is generating. |
| `GetGenerationState()` | `NotStarted` (0), `Generating` (1), `Ready` (2), `Failed` (3), `Cancelled` (4). C# also exposes the typed `GenerationState` property. |
| `GetGenerationStage()` | `NotStarted` (0), `GeneratingTopology` (1), `CreatingCellData` (2), `BuildingMesh` (3), `PreparingVisuals` (4), `Ready` (5). |
| `GetCompletedGenerationStages()` / `GetTotalGenerationStages()` | Completed stage count out of four. This measures stages, not a prediction of elapsed time. |
| `IsGenerating()` / `GetLastGenerationError()` | Query activity or the last failure message. Starting a new request clears the error. |
| `GetLastMeshBuildMilliseconds()` | Cumulative main-thread mesh build/upload work, excluding waits between frames, topology, spatial indexing, materials and borders. This is not the largest frame pause. A synchronous fallback also includes CPU preparation when necessary. |
| `MeshUploadBudgetMilliseconds` | Soft per-node budget in ms (default 4) for sphere/border uploads and default initial color preparation. Zero uses synchronous mesh/visual setup. Individual surface calls, color batches and texture uploads can exceed the budget. Other nodes have separate budgets. Captured when generation starts. |
| `MeshUploadTilesPerSurface` | Target cells per sphere surface and initial color batch (default 8192); border surfaces target six line vertices per cell. Surface sizes grow as needed to stay within the engine's 256-surface limit per mesh. Captured per request. |
| `GetCompletedMeshUploadSurfaces()` / `GetTotalMeshUploadSurfaces()` | Completed/planned incremental sphere surfaces. Both remain zero for single-surface/native fallback generation; reset on a new request. They describe upload work, including surfaces discarded by a custom finalizer. |

```csharp
var sphere = new HexasphereNode { AutoGenerate = false, SubDivision = 100 };
sphere.PlanetGenerated += count => GD.Print($"Ready: {count} tiles");
sphere.GenerationFailed += error => GD.PrintErr(error);
AddChild(sphere);
sphere.GeneratePlanet();
// To change topology later:
// sphere.SubDivision = 142;
// sphere.RestartGeneration();
```

```gdscript
# For an existing child with AutoGenerate disabled in the Inspector:
func _ready() -> void:
    var sphere = $Hexasphere
    sphere.PlanetGenerated.connect(func(count): print("Ready: ", count))
    sphere.GenerationFailed.connect(func(error): push_error(error))
    sphere.GenerationProgressChanged.connect(
        func(stage, completed, total): print(stage, ": ", completed, "/", total))
    sphere.GeneratePlanet()
```

Geometry, seed and base rendering settings are captured per request. Editing them during generation affects the next request; interaction uses the radius of the generated geometry. Background hooks are serialized per node, including across restarts. Cancellation invalidates results immediately, but cannot interrupt a native C++ call already running. The next request waits for that node's previous worker to finish. Removing/freeing a node cancels pending work and prevents late completion/error callbacks. Removing a ready node releases its planet and resets its state to NotStarted. A removed node can be re-added and started explicitly; `_Ready` autogeneration does not repeat unless requested through Godot's usual ready lifecycle.

`PlanetGenerated` fires only after geometry, spatial indexing, materials, colors and extension hooks finish successfully. State/progress/error signals are delivered on the main thread. `IsReady` is false while generating, failed or cancelled. A failed/cancelled request leaves no active planet; connect the failure/cancellation signals when waiting for asynchronous completion.

After `GenerateInBackground` returns, the worker prepares native CPU mesh arrays (when supported by the native binary), the default spatial index and, when enabled, deduplicated border geometry. These results belong to that request and are discarded on cancellation or replacement. `BuildSpatialIndex` still runs as a virtual hook on the main thread; its base implementation adopts the prepared index after validating the supplied directions. Overrides that supply different or transformed centers retain a synchronous fallback. Border data also falls back to synchronous preparation when a finalization override replaces or regenerates the native source. Direct calls to `PlanetBorderRenderer.BuildStaticBorders` retain their synchronous behavior.

With a positive upload budget, the worker also partitions border arrays into complete line pairs. Large border meshes upload across process frames after material initialization, using the captured budget independently of node processing/paused mode. Partial borders stay hidden, and their CPU arrays are released as each surface is consumed. Cancellation, removal and failure release partial meshes and pending arrays. Small single-surface borders finish during material initialization. Chunking adds border draw calls and frame waits before Ready; it reduces individual pauses rather than guaranteeing faster generation. The budget is per node, not a global scene budget or a GPU/frame-time ceiling.

Default initial colors are sampled and packed across frames on the main thread, filling the shader palette and tile-color cache in one pass. Virtual `GetColor` still supports custom cell data. Texture upload and completion follow on a later frame. Custom `OnShaderReady` or `DrawColors` overrides, or controllers without the base palette, retain synchronous coloring so code after `base` sees the same complete result. Runtime recoloring remains synchronous. `ShaderReady` waits for completed borders; `PlanetGenerated` additionally waits for colors and the full ready hook.

`NativeHexasphere.PrepareMesh()` prepares only CPU arrays and returns false when an older native binary does not support preparation. `BuildMesh()` consumes those arrays and releases staging; without preparation it retains its synchronous behavior. Native `generate()` invalidates prepared mesh arrays, including when custom finalization regenerates the same native object. The native `build_mesh()` result retains `mesh`, `tile_vertex_counts` and `tile_vertex_indices`, and adds optional `array_preparation_milliseconds`, `surface_creation_milliseconds` and `used_prepared_arrays` diagnostics. Array preparation time may have occurred on the worker; it must not be added to main-thread mesh timing when `used_prepared_arrays` is true. The supplied Windows DLL includes this capability; rebuild other native binaries from the updated sources to enable it.

For larger spheres the worker calls `PrepareMeshChunks(tilesPerSurface)`, and the main thread consumes them with `AppendMeshChunk(mesh)` over process frames. Each call creates one surface and releases its CPU staging. Completion and interaction remain unavailable until the complete mesh, virtual finalization hooks and visuals are ready. Cancelling/removing the node discards partial resources. The existing `FinalizePlanet` hook runs once after uploads; changing/replacing its pending native source invalidates the uploaded mesh and uses a synchronous rebuild. Older native binaries without chunk support fall back automatically, including binaries that support only `PrepareMesh()`.

Chunking trades shorter loading pauses for more draw calls and potentially longer time to Ready. The default uses 13 sphere surfaces at subdivision 100 and 25 at subdivision 142. The material override and global UV2 tile IDs apply across all surfaces. Custom visual controllers receiving `ApplyGenerated` should support multiple surfaces (iterate them or use the instance material override); set the upload budget to zero if a custom renderer requires one surface. Direct `NativeHexasphere.BuildMesh()` keeps its one-surface contract, even after chunk preparation.

Sphere surface uploads, border mesh uploads, material setup and color uploads still run on the main thread. Large sphere uploads yield across frames; final border/material/color setup starts after the last sphere-upload frame and can still cause a pause. The budget is per node and is not a global frame guarantee.

`CreateCellData` and `GenerateInBackground` still run in the background: avoid scene-tree access there. Custom loops can check the protected `GenerationCancellationToken`; `CurrentGenerationSettings` exposes the captured base settings. Snapshot any additional custom settings on the main thread before starting. `GenerateInBackground` overrides can call `base` or produce their own data and call `PublishGenerationResult(native, centers, cells)` with the supplied native instance. Do not write the legacy pending fields or schedule `FinalizePlanet` from a worker. `FinalizePlanet`, `OnShaderReady`, `InitShaderMaterial` and `CleanupResources` overrides should call `base`; completion is published after the full hooks return. Fully custom visuals may emit `ShaderReady` synchronously after initializing their resources; the node waits until finalization returns before publishing completion. Custom rendering resources should be released by overriding `ClearGenerated` and calling `base`.

Losing or replacing the visual controller before completion fails the current request and releases its pending resources. A subsequent generation recreates a missing default controller through `SetVisualController`. Custom overrides must likewise supply a valid controller attached to the tree.

Direct `NativeHexasphere.Generate` calls require a finite positive radius, subdivisions 1–3454 (the mesh's signed 32-bit vertex-count bound), and a finite hex size in 0.1–1. Invalid managed input throws an argument exception before entering native code. The native `generate` method returns `true` on success or `false` when input/construction is rejected, preserving the previous geometry and prepared arrays on rejection. The wrapper also accepts older binaries whose method returns nil. Disposal waits for an active operation; queued/later operations observe `ObjectDisposedException` after disposal. Extremely small or large inputs can still be rejected if their geometry cannot be represented by the native float coordinates.

## Signals

`HexasphereNode` emits the following signals:

| Signal | Arguments | Description |
|---|---|---|
| `TileClicked` | `(int tileIndex, Vector3 worldPosition)` | A tile was clicked |
| `TileHovered` | `(int tileIndex)` | Mouse hovers over a tile (-1 if none) |
| `TileDeselected` | — | Selection was cleared (clicked empty space) |
| `PlanetGenerated` | `(int tileCount)` | Planet generation completed |
| `GenerationStarted` | — | A new request started |
| `GenerationCancelled` | — | The active request was cancelled, including when replaced by a restart |
| `GenerationFailed` | `(string error)` | Validation, background generation or main-thread finalization failed |
| `GenerationStateChanged` | `(int state)` | The generation state changed |
| `GenerationProgressChanged` | `(int stage, int completedStages, int totalStages)` | A generation stage began or all stages completed |

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

Raster detail and picking are limited by pixel resolution. Cells smaller than a pixel may not be represented; zooming does not add detail. Dimensions must be 1–8192 with at most 16,777,216 pixels total. ID buffers and textures require memory proportional to the raster resolution. Colors use a compact palette with one texel per tile (plus row padding), shared by both projection modes. Raster's shader resolves colors through the ID map, so recoloring uploads only the palette and its CPU work is independent of raster resolution.

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

Preparing a map or changing visibility alone does not enable input. `sphere.OpenUvProjector()` opens its linked map and coordinates viewport camera ownership; standalone projector callers can use `OpenProjection()` after preparation and `CloseProjection()` to close. `IsProjectionOpen` exposes this state. Only the active owner synchronizes a shared projector. Input arbitration and managed camera state are scoped to the planet's viewport; custom camera scripts should use `HexasphereInputRouter.IsUvProjectionOpenFor(GetViewport())` when more than one viewport exists. The global `IsUvProjectionOpen` remains available to callers that intentionally need to know whether any viewport has an open map.

Builds are synchronous. On failure, `BuildMap2D` emits `ProjectionBuildFailed`, records the error/state, and invalidates partial caches for a subsequent retry. `PrepareUvProjector()` returns false when the planet/projector is unavailable or its build fails.

Reopening an unchanged map reuses its mesh and raster IDs. Recoloring and highlight changes do not rasterize again. Changing only `MapSize` reuses raster IDs; changing resolution, topology, or calling `MarkDirty()` invalidates them. Pan, zoom, and selection are preserved on settings changes and reopening; a different native sphere or tile count resets the camera and highlight indices.

UV uses its own `Camera2D`; the sphere uses `Camera3D`. Custom 3D camera controllers should suspend input and movement while `HexasphereInputRouter.IsUvProjectionOpen` is true. Setting a camera's `Current` property to false does not stop its scripts. Both included 3D camera controllers respect UV mode.



## Verification

Use the [manual UV projection checklist](docs/testing/uv-projection-checklist.md) for interaction, mode changes, seams/poles and error recovery.
Use the [generation lifecycle checklist](docs/testing/generation-checklist.md) for start/restart/cancel, removal, failures, custom hooks and UV regeneration.

## Custom Cell Data

The convenience setters `SetTileColor` and `SetAllTileColors` target `HexCellData`. For custom cell models, override `CreateCellData`, `SetVisualController`, and `GetColor`, as shown by Earth in `scenes/sol_example/`; the visual controller maps your data to colors.

Bulk color updates may supply a shorter array: untouched cells retain their existing colors. The UV cache is derived from actual cell colors, including custom cells that the convenience setters do not modify. An open, owned projection refreshes its palette without rebuilding its geometry. Passing null to `SetAllTileColors` is rejected.

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

Key extension points and protected helpers in `HexasphereNode`:

| Method | Purpose |
|---|---|
| `CreateCellData(int count, Vector3[] centers)` | Create array of `ICellData[]` with custom per-tile data |
| `SetVisualController()` | Assign a custom visual controller subclass |
| `GenerateInBackground(NativeHexasphere hexasphere)` | Override to customize background generation logic |
| `PublishGenerationResult(NativeHexasphere hexasphere, Vector3[] centers, ICellData[] cells)` | Publish fully custom background results through the protected lifecycle helper (not virtual) |
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
| `ClearGenerated()` | Release owned visual resources when replacing or removing the planet; native data remains node-owned |

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

Clone the full repository and initialize its pinned `godot-cpp` submodule before building. Copying only the addon does not include native sources. Requires Python 3, SCons, a working C++17 compiler and the target platform toolchain. Exception support is configured by `SConstruct` through godot-cpp's compiler-specific options.

```bash
git submodule update --init --recursive
cd native
python -m SCons platform=windows arch=x86_64 target=template_debug -j4
python -m SCons platform=windows arch=x86_64 target=template_release -j4
```

The Windows outputs are `hexasphere.windows.template_debug.x86_64.dll` and `hexasphere.windows.template_release.x86_64.dll` in `addons/hexasphere_generator/bin/`. The descriptor selects the appropriate library for debug/editor or release use. Build variants have distinct filenames, so one target does not overwrite another. Use the explicit architecture in each command.

| Platform / architecture | Build options | Library filename pattern | Included / verification |
|---|---|---|---|
| Windows x86-64 | `platform=windows arch=x86_64` | `hexasphere.windows.<target>.x86_64.dll` | Debug/release included; tested on Godot 4.7.2 .NET |
| Windows ARM64 | `platform=windows arch=arm64` | `hexasphere.windows.<target>.arm64.dll` | Source build required; unverified |
| Linux x86-64 / ARM64 | `platform=linux arch=x86_64` or `arch=arm64` | `libhexasphere.linux.<target>.<arch>.so` | Source build required; unverified |
| macOS x86-64 / ARM64 | `platform=macos arch=x86_64` or `arch=arm64` | `libhexasphere.macos.<target>.<arch>.dylib` | Source build required; unverified |

Replace `<target>` with `template_debug` or `template_release`. Build both variants needed by the consuming project and keep their names aligned with `hexasphere.gdextension`. macOS mappings are for explicit single-architecture builds; no universal binary is supplied. Other architectures/platforms need their own build and descriptor entry. A successful native release build/load does not by itself verify a Godot export or other operating systems.


