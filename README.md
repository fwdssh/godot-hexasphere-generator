# Hexasphere Generator for Godot 4 (C#)

A procedural hexagonal sphere generator for Godot 4, written in C#. It generates a spherical grid composed of hexagons and exactly 12 pentagons. 
This project is an optimized port of the [Unity implementation by Em3rgencyLT](https://github.com/Em3rgencyLT/Hexasphere), tailored specifically for Godot.

![Preview](preview.png)
![Preview2](preview2.png)

## How to Use

### 1. Via the Godot Inspector
1. Add the `HexasphereDemo` node to your 3D scene.
2. In the Inspector, configure the three main parameters:
   * **Radius:** Controls the overall size of the generated sphere in 3D space.
   * **Divisions:** Controls the density of the hex grid (higher values mean more hexagons).
   * **Hex Size:** A clamped value between `0.01` and `1.0`. Setting it to `1.0` makes tiles perfectly touch each other. Lowering it (e.g., `0.95`) creates clean, visible gaps between individual tiles.

### 2. Via C# Scripting 
*(You can also find a better implementation in the `HexasphereExample.cs` script)*

```csharp
using Godot;
using Godot.Hexasphere;

public partial class MyPlanet : Node3D
{
    private Hexasphere _hexasphere;

    public override void _Ready()
    {
        // Initialize a sphere: radius = 10.0, divisions = 4, tile size = 0.95
        _hexasphere = new Hexasphere(10f, 4, 0.95f);

        GD.Print($"Planet generated with {_hexasphere.Tiles.Count} tiles!");

        // Access a specific tile (e.g., the first one)
        Tile randomTile = _hexasphere.Tiles[0];
        
        // Get its exact 3D position to spawn a building or unit
        Vector3 spawnPosition = randomTile.Center.Position; 

        // Iterate through neighbors for pathfinding, AI, or cellular automata
        foreach (Tile neighbor in randomTile.Neighbours)
        {
            // Handle neighbor logic here
        }
    }
}
```
## Core Architecture

The framework completely separates pure mathematical data from Godot's rendering engine:

* **`Hexasphere.cs`** — The main grid manager. It handles the initial icosahedron creation, manages subdivision logic, caches unique vertices using a spatial hash grid, and exposes the final `Tiles` list and raw `MeshDetails`.
* **`Tile.cs`** — Represents an individual cell on the sphere (either a hexagon or one of the 12 pentagons). It holds references to its boundary points, its center, and a list of its direct `Neighbours`. Includes built-in `ToJson()` serialization.
* **`Face.cs`** — An internal data structure used to track triangular faces during the icosahedron subdivision phase. It calculates centers and handles fast adjacency checks via unique integer IDs.
* **`Point.cs`** — Represents a 3D vertex on the sphere. It generates unique incremental integer IDs for $O(1)$ comparison performance and contains logic to sort faces in a clockwise/counterclockwise ring to guarantee correct mesh normals.
* **`MeshDetails.cs`** — A lightweight data container holding raw vertex and triangle arrays, ready to be fed directly into Godot's `ArrayMesh`.