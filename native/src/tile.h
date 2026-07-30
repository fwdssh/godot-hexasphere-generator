#ifndef HEXASPHERE_TILE_H
#define HEXASPHERE_TILE_H

#include <array>
#include <cstdint>
#include <unordered_map>
#include <vector>
#include <godot_cpp/variant/vector3.hpp>
#include "point.h"
#include "face.h"

using namespace godot;

class Tile
{
private:
    int32_t _centerIdx;
    float _radius;
    float _size;

    std::array<Point, 6> _boundaryPoints;
    int _boundaryCount = 0;
    std::array<Face, 4> _faces{};
    std::array<std::array<int, 3>, 4> _faceIndices;
    int _faceCount = 0;
    std::array<int32_t, 6> _neighbourCenters;
    int _neighbourCenterCount = 0;
    std::array<Tile *, 6> _neighbours;
    int _neighbourCount = 0;
    int _index = -1;

public:
    Tile(int32_t centerIdx, float radius, float size, const std::vector<Face> &globalFaces, const std::vector<Point> &globalPoints);
    ~Tile() = default;
    Tile(const Tile &) = delete;
    Tile &operator=(const Tile &) = delete;

    void set_index(int idx) { _index = idx; }
    int get_index() const { return _index; }

    int32_t get_center() const { return _centerIdx; }

    const std::array<Point, 6> &get_boundary_points() const { return _boundaryPoints; }
    int get_boundary_count() const { return _boundaryCount; }

    const std::array<Face, 4> &get_faces() const { return _faces; }
    const std::array<int, 3> *get_face_indices() const { return _faceIndices.data(); }
    int get_face_count() const { return _faceCount; }

    const Tile *const *get_neighbours_data() const { return _neighbours.data(); }
    int get_neighbour_count() const { return _neighbourCount; }

    void resolve_neighbour_tiles_fast(const std::unordered_map<int, Tile *> &tile_map);

private:
    void store_neighbour_centers(const std::vector<int32_t> &icosahedron_faces, const std::vector<Face> &globalFaces);
    void build_faces(const std::vector<int32_t> &icosahedron_faces, const std::vector<Face> &globalFaces, const std::vector<Point> &globalPoints);
};

#endif
