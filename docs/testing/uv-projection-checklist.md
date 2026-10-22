# UV projection manual checks

Use `addons/hexasphere_generator/scenes/example/example.tscn`. Save your current scene settings before testing. These are manual scenarios; unchecked boxes are not claims that a scenario passed.

Select **HexasphereUvProjector** to find the standard **Projection** and **Raster** Inspector groups. Configure settings before running, or change them in the **Remote** Inspector and close/reopen the UV map to apply them. Changing a property alone does not rebuild the map. From code, call `RebuildMap()` after changing settings on a prepared projector.

Open the map with **1 while hovering the sphere**; close with **Escape**. Pan with right mouse drag and zoom with the wheel. `MapSize` is displayed size; `RasterResolution` is texture resolution and applies when `RasterQuality = Custom`.

| SubDivision | Expected tiles | Auto with default threshold 200,000 |
|---:|---:|---|
| 20 | 4,002 | DetailedMesh |
| 100 | 100,002 | DetailedMesh |
| 141 | 198,812 | DetailedMesh |
| 142 | 201,642 | Raster |

Record Godot/.NET versions, renderer, subdivision, mode, quality, and result for each failed check. Screenshots plus the console error and exact steps are useful for reproducing a failure.

| Done | ID | Steps | Expected result |
|---|---|---|---|
| [ ] | M01 — Existing detailed mode | Set subdivision 20 and DetailedMesh. Open the map, hover/click several cells, pan, zoom, close, and select a cell on the sphere again. | Cells render and respond; closing restores the 3D view and input. |
| [ ] | M02 — Forced raster | Use subdivision 20, Raster, Low quality. Repeat M01. | The same interaction works with a pixel-limited map; no mode-specific input errors. Small cells/boundaries can look coarser than detailed geometry. |
| [ ] | M03 — Auto threshold | At subdivision 100, use Auto and threshold 100,002, then 100,001; rebuild/reopen each time. | Active mode is DetailedMesh at equality and Raster above the threshold. Read `GetActiveProjectionMode()` (1/2), rather than guessing from appearance. |
| [ ] | M04 — Large map | Use subdivision 142, Auto, threshold 200,000. Open and interact with the UV map. | Raster becomes active. The first build may pause while rasterizing; subsequent openings reuse it. No missing-image/error state. |
| [ ] | M05 — Reopen persistence | In either mode, select a cell, zoom in and pan. Close/reopen ten times. | Camera position, target zoom and selected cell persist. No duplicate click callbacks or progressively slower opening. |
| [ ] | M06 — Mode changes | With a cell selected, change DetailedMesh → Raster → DetailedMesh, applying each change. | Camera and valid selection persist. `ProjectionModeChanged` reports successful changes; clicking still identifies valid cells. |
| [ ] | M07 — Quality and custom size | In Raster, apply Low/Medium/High, then Custom 1536×768. Query `GetEffectiveRasterResolution()`. | Resolutions are 1024×512, 2048×1024, 4096×2048, and 1536×768 respectively. Resolution changes rebuild IDs while preserving camera and selection. |
| [ ] | M08 — Layout versus resolution | In Raster/Custom 2048×1024, change only `MapSize`, e.g. 1920×1080 → 1600×900, then reopen. | Display size changes, effective resolution stays 2048×1024. Picking/highlights stay aligned; camera is not reset. |
| [ ] | M09 — Seam and poles | In both modes, pan to both map edges and top/bottom; hover/click adjacent cells at several zoom levels. | No unexpected blank strips, stretched triangles, or offset highlights. In raster mode a click resolves the cell represented by its pixel; subpixel cells may be unavailable. |
| [ ] | M10 — Recoloring | Prepare a full `Color[]`/`Array[Color]`, update the projector with `UpdateColors(colors)`, then repeat with another palette. Test with a selected/hovered cell. | Colors change without resetting camera or selection. Highlight colors remain visible. Do not expect a direct projector recolor to also modify the sphere's custom cell data. |
| [ ] | M11 — Script API | After `PlanetGenerated`, call `sphere.PrepareUvProjector()`, `projector.GetTileAtUv(Vector2(0.4,0.4))`, `SelectTile(id)`, and `ClearSelection()`. Repeat from C# and GDScript if both are used. | Preparation does not open the map or switch cameras; picking returns a valid ID; selection updates. Programmatic selection does not emit a mouse-click event. |
| [ ] | M12 — Failed build recovery | In Raster/Custom, apply resolution 0×1024, then restore 2048×1024 and rebuild. | Invalid settings produce Failed state and `ProjectionBuildFailed`; error is readable via `GetLastBuildError()`. Valid retry returns Ready and picking works. The error log for the intentionally invalid build is expected. |
| [ ] | M13 — Topology replacement | From a scratch script, generate a different native sphere and prepare it. For regeneration in the same native object, call `MarkDirty()` before the next build and supply fresh colors when needed. | New geometry/IDs are used; a changed sphere/tile count resets camera and highlights. Old colors with an incompatible tile count are not silently reused by `RebuildMap()`. |
| [ ] | M14 — Repeated lifetime | Repeat mode/quality changes and create/free projectors. | No crash, invalid-object errors or persistent growth of live resources. Interpret RSS/private memory together with managed and Godot counters; allocator/driver retention is not automatically a leak. |
| [ ] | M15 — Renderers | Run M01, M02, M06, M09 and M10 with Compatibility and Forward+ on the target machine. | Both renderers display colors/highlights correctly, with no shader compilation errors. |
| [ ] | M16 — Independent cameras | Record the 3D view. Open UV, scroll both directions and pan, then close. Also open UV while holding a 3D drag, release there, and close. Start a UV drag, close while holding, release in 3D, and reopen. | The 3D camera keeps its original transform while UV is open. Neither camera continues dragging after its button was released in the other view. Normal orbit, zoom, and UV pan still work. |

Keep functional failures separate from timing results: a fast map with incorrect picking has not passed the scenario.
