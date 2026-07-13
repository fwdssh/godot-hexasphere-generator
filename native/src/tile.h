#ifndef HEXASPHERE_TILE_H
#define HEXASPHERE_TILE_H

#include <array>
#include <vector>
#include <unordered_map>
#include <godot_cpp/variant/vector3.hpp>
#include "point.h"
#include "face.h"

using namespace godot;

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
    std::array<Point *, 12> _neighbourCenters;
    int _neighbourCenterCount = 0;
    std::array<Tile *, 12> _neighbours;
    int _neighbourCount = 0;

public:
    Tile(Point *center, float radius, float size);
    ~Tile() = default;
    Tile(const Tile &) = delete;
    Tile &operator=(const Tile &) = delete;

    Point *get_center() const { return _center; }
    const std::array<Point, 6> &get_boundary_points() const { return _boundaryPoints; }
    int get_boundary_count() const { return _boundaryCount; }
    const std::vector<Face> &get_faces() const { return _faces; }
    const std::array<int, 3> *get_face_indices() const { return _faceIndices.data(); }
    int get_face_count() const { return _faceCount; }
    const Tile *const *get_neighbours_data() const { return _neighbours.data(); }
    int get_neighbour_count() const { return _neighbourCount; }

    void resolve_neighbour_tiles_fast(const std::unordered_map<int, Tile *> &tile_map);

private:
    void store_neighbour_centers(const std::vector<Face *> &icosahedron_faces);
    void build_faces(const std::vector<Face *> &icosahedron_faces);
};

#endif // HEXASPHERE_TILE_H
