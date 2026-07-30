#ifndef HEXASPHERE_TILE_H
#define HEXASPHERE_TILE_H

#include <array>
#include <vector>
#include <unordered_map>
#include <godot_cpp/variant/vector3.hpp>
#include "point.h"
#include "face.h"

using namespace godot;

/// <summary>
/// A hexagonal (or pentagonal) tile on the sphere surface.
/// Tiles are generated from the subdivided icosahedron and contain boundary points,
/// triangle faces, and neighbour references. Internal — use NativeHexasphere for the GDExtension API.
/// </summary>
class Tile
{
private:
    Point *_center;
    float _radius;
    float _size;

    std::array<Point, 6> _boundaryPoints;
    int _boundaryCount = 0;
    std::vector<Face> _faces;
    std::array<std::array<int, 3>, 4> _faceIndices;
    int _faceCount = 0;
    std::array<Point *, 6> _neighbourCenters;
    int _neighbourCenterCount = 0;
    std::array<Tile *, 6> _neighbours;
    int _neighbourCount = 0;
    int _index = -1;

public:
    Tile(Point *center, float radius, float size);
    ~Tile() = default;
    Tile(const Tile &) = delete;
    Tile &operator=(const Tile &) = delete;

    void set_index(int idx) { _index = idx; }
    int get_index() const { return _index; }

    /// <summary>
    /// Returns a pointer to the center point of this tile.
    /// </summary>
    Point *get_center() const { return _center; }

    /// <summary>
    /// Returns a const reference to the array of boundary points forming the tile perimeter.
    /// Up to 6 points; use get_boundary_count() for the actual count (5 for pentagons).
    /// </summary>
    const std::array<Point, 6> &get_boundary_points() const { return _boundaryPoints; }

    /// <summary>
    /// Returns the number of boundary points (6 for hexagons, 5 for pentagons at the poles).
    /// </summary>
    int get_boundary_count() const { return _boundaryCount; }

    /// <summary>
    /// Returns a const reference to the vector of triangle faces composing this tile.
    /// </summary>
    const std::vector<Face> &get_faces() const { return _faces; }

    /// <summary>
    /// Returns a pointer to the array of face index triples.
    /// Each triple contains indices into the boundary points for one triangle.
    /// </summary>
    const std::array<int, 3> *get_face_indices() const { return _faceIndices.data(); }

    /// <summary>
    /// Returns the number of triangle faces in this tile.
    /// </summary>
    int get_face_count() const { return _faceCount; }

    /// <summary>
    /// Returns a pointer to the array of neighbouring tiles.
    /// </summary>
    const Tile *const *get_neighbours_data() const { return _neighbours.data(); }

    /// <summary>
    /// Returns the number of neighbouring tiles.
    /// </summary>
    int get_neighbour_count() const { return _neighbourCount; }

    /// <summary>
    /// Resolves neighbour tile references using a fast map lookup by tile center ID.
    /// Should be called after all tiles have been constructed.
    /// </summary>
    void resolve_neighbour_tiles_fast(const std::unordered_map<int, Tile *> &tile_map);

private:
    void store_neighbour_centers(const std::vector<Face *> &icosahedron_faces);
    void build_faces(const std::vector<Face *> &icosahedron_faces);
};

#endif // HEXASPHERE_TILE_H
