# Hexasphere Generator — TODO

## Generation lifecycle

- [ ] Make generation controllable and expose its state: not started, generating, ready, failed, and cancelled. Provide generation/restart/cancellation entry points and lifecycle signals that can be used from C# and GDScript.
- [ ] Handle node removal, repeated generation, stale worker results, errors, and native resource ownership safely. Capture generation parameters per request and apply only the current request's result. Review the existing cancellation/request-ID implementation in `D:/godotgames/planet-generator/godot-planet-generator/addons/hexasphere_generator/scripts/hexasphere_node/HexasphereNode.cs` for reusable behavior while preserving the plugin's custom generation extension points.
- [ ] Report progress by actual generation stages and measure main-thread mesh finalization at high subdivisions before deciding which work to move or split.

## Installation and release documentation

- [ ] State the Godot .NET requirement explicitly, including when using the C# nodes from GDScript. Document supported/tested Godot versions, target .NET version, available native binaries, platform/architecture support, and native build instructions. Distinguish included binaries from platforms requiring a source build.
- [x] Document the existing custom-cell extension pattern (`CreateCellData`, `SetVisualController`, and `GetColor`) using the `sol_example` Earth implementation; clarify that the convenience color setters target `HexCellData`.

## Raster UV map mode

- [x] Bring the generic raster UV projection from `D:/godotgames/planet-generator/godot-planet-generator/addons/hexasphere_generator/scripts/hexasphere_uv_projector/HexasphereProjectorController.cs` and its UV shader into this plugin, without planet simulation dependencies.
- [x] Offer automatic, detailed-mesh, and raster projection modes, with configurable raster resolution and an automatic tile-count threshold. The reference implementation currently uses 200,000 tiles; treat this as a starting setting, not a measured universal limit.
- [x] Preserve hover, click, selection, and color updates through the pixel-to-tile ID map. Cache the rasterization across reopenings and color changes, and invalidate it when topology or raster settings change.
- [x] Document that raster mode reduces UV rendering geometry and hit-test storage; initial topology generation and rasterization still depend on tile count. Describe the loss of detail/picking precision for cells smaller than a raster pixel.
- [x] Sample a per-tile color palette through the ID texture in the shader, so recoloring uploads only the compact palette instead of rebuilding a full-resolution color image on the CPU.

### Inspector settings and scripting API

- [x] Provide standard exported Godot properties, Inspector groups, enum dropdowns, and numeric/vector fields for projection mode, automatic mode threshold, raster resolution, and quality presets with a custom-resolution option. Expose the same settings through a documented C#/GDScript API. Separate raster pixel resolution from the map's displayed size (`MapSize`), so changing layout does not force rerasterization. Keep editor integration to standard Inspector controls; no runtime UI panel or custom editor panel is planned.
- [x] Provide simple operations to prepare/rebuild a projection, update colors, select/clear a tile, and resolve a tile from normalized map UV coordinates. Keep these operations consistent across detailed and raster modes; callers should not manage ID textures or renderer internals.
- [x] Expose the requested mode, actual active mode, effective raster resolution, and build state. Publish ready/error/mode-change notifications; reuse the generation lifecycle conventions where applicable.
- [x] Preserve pan, zoom, and valid tile selection across mode/resolution changes. Recoloring and highlighting must not trigger topology or ID-map rebuilds; opening an unchanged map should reuse its cached projection.

## Verification

- [x] Add a manual UV interaction checklist with expected results, threshold cases, seam/pole checks, settings changes, error recovery and repeated lifetime checks. See `docs/testing/uv-projection-checklist.md`.
