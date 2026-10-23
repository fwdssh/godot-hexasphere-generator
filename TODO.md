# Hexasphere Generator — TODO

## Architecture audit fixes — 2026-10-23

Completed the following audit fixes; verification covered Windows x86-64 debug/release native libraries on Godot 4.7.2 .NET. A1–A9 were reproduced with the actual plugin in Godot; B1–B2 are configuration/code-boundary findings, and B3 was reproduced with a deterministically scheduled disposal/query overlap. Keep tests, reports and benchmarks out of implementation commits.

- [x] **A1 — Input eligibility and viewport isolation.** Prevent hidden, inactive or event-ineligible planets from blocking eligible planets. Scope arbitration, UV ownership and camera state to the appropriate viewport/world; include viewport/camera identity in the per-event cache. Verify overlapping planets, runtime visibility/interaction changes and independent SubViewports.
- [x] **A2 — Closed UV projector input.** Introduce an explicit open/closed state so an unopened or unbuilt projector cannot consume wheel/right-button events or activate its camera. Make programmatically created controllers and packed scenes behave consistently; verify 3D camera controls before opening and after closing UV.
- [x] **A3 — Shared projector ownership.** Allow only the current owner to synchronize selection or restore camera state. Acquire ownership and close the previous view before mutating/subscribing/showing the shared projector. Verify that selecting B's UV tile while A was selected preserves B's highlight, and that owner switching/removal does not hide or clear the new owner's view.
- [x] **A4 — Authoritative runtime colors.** Keep cell data, the 3D palette and the UV color cache consistent after `SetTileColor` and `SetAllTileColors`, including reopening UV. Define and preserve partial-array behavior without zeroing untouched colors; do not cache input colors for unsupported custom cells that were not changed.
- [x] **A5 — Scale-invariant triangle winding.** Replace the absolute-scale face orientation test with a centroid/normal sign test and handle degeneracy explicitly. Verify consistent outward native faces and correct rendered winding at radii 20, 1 and 0.1, including subdivision changes.
- [x] **A6 — Scale-invariant border identity.** Stop deduplicating unrelated edges solely by midpoints snapped to a fixed 0.001 grid. Use topology-based identity or relative tolerances with endpoint verification; preserve shared/gapped ownership and chunking. Verify subdivision 1 at radius 0.0001 retains 30 lines/60 vertices.
- [x] **A7 — Visual-controller loss during generation.** Detect removal/disposal of the visual controller while waiting for material/border readiness and transition to a defined failed/cancelled state instead of remaining `Generating` forever. Release pending resources, reject stale callbacks and support a subsequent generation with a valid controller.
- [x] **A8 — World-space click positions.** Convert both 3D hit positions and UV tile centers to world space before emitting `TileClicked`. Verify translated, rotated and nonuniformly scaled planets against the public signal contract.
- [x] **A9 — UV hover signal forwarding.** Emit the sphere's `TileHovered` when projector hover changes, consistently with 3D input. Define hover clearing on UV entry/exit and verify subscribers receive the same state as the sphere's hovered index.
- [x] **B1 — Native library platform/variant packaging.** Resolve the advertised macOS support versus missing `.gdextension` mapping. Align mappings and actual build outputs, distinguish debug/release/architecture artifacts so builds do not overwrite each other, and document the supported/tested matrix without claiming unverified platforms or release exports.
- [x] **B2 — Safe direct native input boundary.** Validate finite positive radius, supported subdivision bounds and hex-size inputs at the exposed native API as well as the managed wrapper. Ensure invalid direct C#/ClassDB calls cannot propagate an uncaught C++ exception across GDExtension or construct invalid geometry; preserve the normal node lifecycle's validation behavior.
- [x] **B3 — Native wrapper disposal contract.** Synchronize disposal with pending/in-flight operations, or explicitly enforce a supported concurrency contract. Guard disposed methods with a clear disposed-object error, keep disposal idempotent and verify a waiting query cannot resume against a cleared native reference.

## Verification follow-ups — 2026-10-24

- [x] **C1 — Disabled UV hover.** Clear the projector and sphere hover indices/highlights and emit the clearing signal on mouse motion after either hover flag is disabled.
- [x] **C2 — Closing a failed UV rebuild.** Keep the close input action available when an explicitly open map fails to rebuild; return camera ownership to 3D.
- [x] **C3 — Previous-owner removal during switching.** Retain the viewport state through synchronous close callbacks that unregister the old owner, and preserve nested ownership decisions.
- [x] **C4 — Standalone source invalidation.** After close callbacks, verify the projector still holds the invalidated source before clearing resources; preserve a newly opened owner's map.
- [x] **C5 — Replaced visual-controller cleanup.** Clear the original request controller as well as its replacement, including external controllers during node removal. Preserve generation restarted from a cleanup callback.
- [x] **C6 — Empty native neighbor data.** Return empty neighbor lists before the first successful generation instead of allocating a negative array length.
- [x] **C7 — Exact mesh tile IDs.** Apply the float-channel tile/owner ID bound consistently in node, managed and native validation: subdivisions 1–1295. Reject larger requests before allocation and preserve previous geometry/prepared arrays.

## Generation lifecycle

- [x] Make generation controllable and expose its state: not started, generating, ready, failed, and cancelled. Provide generation/restart/cancellation entry points and lifecycle signals that can be used from C# and GDScript.
- [x] Handle node removal, repeated generation, stale worker results, errors, and native resource ownership safely. Capture generation parameters per request and apply only the current request's result. Reuse the cancellation/request-ID approach from `planet-generator` while preserving custom cell-data and visual extension points.
- [x] Report progress by actual generation stages and measure main-thread mesh finalization at high subdivisions before deciding which work to move or split.
- [x] Reduce main-thread finalization pauses by preparing the default spatial index and deduplicated border geometry on the request's worker. Preserve cancellation, custom generation/finalization hooks and the standalone border API; verify the improvement with repeated graphics measurements.
- [x] Separate native CPU sphere-mesh array preparation from `ArrayMesh` resource creation. Prepare arrays on the request worker, preserve direct `BuildMesh()` and old-binary fallbacks, invalidate on regeneration and release staging after application; measure both phases separately.
- [x] Spread large sphere-mesh surface creation across frames with a configurable soft per-node budget and target cells per surface. Measure draw calls and time to Ready, preserve cancellation/custom finalization and the standalone single-surface API, and cap surfaces to the engine limit.
- [x] Reduce the remaining final-frame border/material/color pause: upload prepared borders incrementally and prepare default colors across frames. Compare added surfaces/draw calls and time to Ready; preserve synchronous custom hooks and standalone borders. The soft per-node budget still does not provide a global scene/frame-time guarantee.

## Installation and release documentation

- [x] State the Godot .NET requirement explicitly, including when using the C# nodes from GDScript. Document supported/tested Godot versions, target .NET version, available native binaries, platform/architecture support, and native build instructions. Distinguish included binaries from platforms requiring a source build.
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
