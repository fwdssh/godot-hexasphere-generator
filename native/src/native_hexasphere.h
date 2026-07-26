#ifndef HEXASPHERE_NATIVE_HEXASPHERE_H
#define HEXASPHERE_NATIVE_HEXASPHERE_H

#include <godot_cpp/classes/ref_counted.hpp>
#include <godot_cpp/variant/dictionary.hpp>
#include <godot_cpp/variant/packed_vector3_array.hpp>
#include <godot_cpp/variant/packed_int32_array.hpp>
#include <memory>

using namespace godot;

class Hexasphere;

/// <summary>
/// GDExtension bridge class exposing hexasphere generation and data access to Godot / C#.
/// Wraps the internal Hexasphere engine and returns Godot-friendly types (Vector3, PackedArrays, Dictionary, ArrayMesh).
/// </summary>
class NativeHexasphere : public RefCounted
{
    GDCLASS(NativeHexasphere, RefCounted)

private:
    std::unique_ptr<Hexasphere> _hexasphere;

protected:
    static void _bind_methods();

public:
    NativeHexasphere();
    ~NativeHexasphere();

    /// <summary>
    /// Generates the hexagonal sphere geometry with the specified parameters.
    /// Must be called before any other methods.
    /// </summary>
    /// <param name="radius">Sphere radius in world units.</param>
    /// <param name="divisions">Icosahedron subdivision level (higher = more tiles).</param>
    /// <param name="hexSize">Relative size of hexagons (1.0 = default).</param>
    void generate(float radius, int divisions, float hexSize);

    /// <summary>
    /// Returns the total number of tiles in the generated sphere.
    /// </summary>
    /// <returns>Tile count, or 0 if generate() has not been called.</returns>
    int get_tile_count() const;

    /// <summary>
    /// Returns the world-space center position of a single tile.
    /// </summary>
    /// <param name="tile_idx">Index of the tile (0-based).</param>
    /// <returns>Center position as a Vector3, or Vector3() if the index is out of range.</returns>
    Vector3 get_tile_center(int tile_idx) const;

    /// <summary>
    /// Returns the center positions of all tiles as a PackedVector3Array.
    /// </summary>
    /// <returns>Array of center positions, one per tile.</returns>
    PackedVector3Array get_all_tile_centers() const;

    /// <summary>
    /// Returns the boundary vertex positions of a single tile.
    /// </summary>
    /// <param name="tile_idx">Index of the tile (0-based).</param>
    /// <returns>Array of vertex positions forming the tile boundary.</returns>
    PackedVector3Array get_tile_points(int tile_idx) const;

    /// <summary>
    /// Returns the triangle face indices (local to this tile) for a single tile.
    /// Each face is stored as three consecutive integers (index triples).
    /// </summary>
    /// <param name="tile_idx">Index of the tile (0-based).</param>
    /// <returns>Flat array of face vertex indices (count = face_count * 3).</returns>
    PackedInt32Array get_tile_faces(int tile_idx) const;

    /// <summary>
    /// Returns per-tile vertex and face-index data for custom mesh building on the C# side.
    /// Dictionary keys: "points", "face_indices", "point_counts", "face_vertex_counts".
    /// </summary>
    /// <returns>Dictionary containing arrays of points, indices, and per-tile counts.</returns>
    Dictionary get_build_data() const;

    /// <summary>
    /// Returns per-tile border line data for wireframe or outline rendering.
    /// Dictionary keys: "positions", "tile_line_counts".
    /// </summary>
    /// <returns>Dictionary containing line segment positions and per-tile counts.</returns>
    Dictionary get_border_data() const;

    /// <summary>
    /// Returns all tile neighbor relationships in CSR (Compressed Sparse Row) format.
    /// Dictionary keys: "neighbor_indices" (flat PackedInt32Array of neighbor tile indices),
    /// "offsets" (PackedInt32Array where offsets[t] .. offsets[t+1] is the range in neighbor_indices for tile t).
    /// </summary>
    Dictionary get_all_tile_neighbors() const;

    /// <summary>
    /// Builds a complete ArrayMesh for the entire sphere, including vertex positions,
    /// normals, and UV2 data (tile index encoded in UV2.x). Returns the mesh and per-tile
    /// vertex/index metadata.
    /// Dictionary keys: "mesh", "tile_vertex_counts", "tile_vertex_indices".
    /// </summary>
    /// <returns>Dictionary containing an ArrayMesh reference and per-tile vertex metadata.</returns>
    Dictionary build_mesh() const;
};

#endif // HEXASPHERE_NATIVE_HEXASPHERE_H
