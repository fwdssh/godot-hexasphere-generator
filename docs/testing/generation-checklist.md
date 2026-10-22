# Generation lifecycle manual checks

Use the example scene or a scratch scene with a `HexasphereNode` and a linked UV projector. Observe lifecycle signals/getters from a small script. These unchecked boxes are manual scenarios, not claims of completed manual testing.

| Done | Scenario | Expected result |
|---|---|---|
| [ ] | Leave AutoGenerate enabled and run. Disable it before running again, then call GeneratePlanet after the node is ready. | Existing auto generation still works; manual mode stays NotStarted until called. Both finish with PlanetGenerated and Ready. |
| [ ] | Change subdivision and call RestartGeneration repeatedly, including before the previous request finishes. | Only the newest request becomes ready. Old work cannot change the mesh, colors, selection or failure state. Exactly one planet mesh and, when enabled, one border mesh remain. |
| [ ] | Cancel while a high-subdivision request is running, then start a smaller request. | Cancellation returns promptly, clears interaction and reports Cancelled. An existing native call may still finish in the background; the next request waits for it without blocking the main thread. No cancelled PlanetGenerated arrives. |
| [ ] | Pan/zoom/select in UV, then regenerate the owning sphere. Repeat with two spheres sharing a projector, regenerating the other sphere instead, including after PrecomputeUvGeometry for that other sphere. Check DetailedMesh and Raster. | The owner's UV closes, its camera restores and its cache is discarded. Regenerating the other sphere leaves the active UV mesh, material, selection and picking intact. Preparing the new topology works. |
| [ ] | Remove/free the node during generation. Also remove/re-add the same node, then call GeneratePlanet explicitly. | No late completion/error callback targets a removed node. Re-added nodes can generate again. |
| [ ] | Use subdivision 0 or a nonpositive radius; restore valid settings and retry. | Failed state, GenerationFailed and readable GetLastGenerationError. A valid retry clears the error and reaches Ready. |
| [ ] | Observe GenerationProgressChanged and state changes. Change radius/subdivision/seed while generation is running. | Progress counts four actual stages rather than estimating time. The active request uses its captured settings; edits apply to the next request. |
| [ ] | Use the sol_example Earth custom data/visual controller; regenerate it. Test custom generation/ready/material hooks that throw, including after calling base. | Custom data and GetColor behavior remain valid. Errors yield Failed without premature PlanetGenerated; retry works after correcting the hook. |
| [ ] | Restart/cancel from lifecycle callbacks; close UV with a ProjectionClosed handler that restarts or removes the sphere. | Nested requests are retained; the outer request does not resume against a deleted node or overwrite newer state. |
| [ ] | Inspect large subdivisions with borders both enabled and disabled; record GetLastMeshBuildMilliseconds. | Mesh timing is available, but initial main-thread mesh/index/material/border work can still pause frames. This feature controls lifecycle; it does not promise interruption of native work or stall-free mesh finalization. |

Use [the UV checklist](uv-projection-checklist.md) for projection interaction and renderer coverage.
