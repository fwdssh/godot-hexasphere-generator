#ifndef HEXASPHERE_TILE_H
#define HEXASPHERE_TILE_H

#include <array>
#include <cstdint>
#include <vector>
#include <godot_cpp/variant/vector3.hpp>

using namespace godot;

class Face;
class Point;

class Tile
{
private:
    friend class Hexasphere;

    Vector3 _center;
    std::array<Vector3, 6> _boundaryPoints;
    std::array<std::array<uint8_t, 3>, 4> _faceIndices{};
    std::array<int32_t, 6> _neighbours{};
    uint8_t _boundaryCount = 0;
    uint8_t _faceCount = 0;
    uint8_t _neighbourCount = 0;

public:
    Vector3 get_center() const { return _center; }
    const std::array<Vector3, 6> &get_boundary_points() const { return _boundaryPoints; }
    int get_boundary_count() const { return _boundaryCount; }
    const std::array<uint8_t, 3> *get_face_indices() const { return _faceIndices.data(); }
    int get_face_count() const { return _faceCount; }

    const int32_t *get_neighbours_data() const { return _neighbours.data(); }
    int get_neighbour_count() const { return _neighbourCount; }

private:
    void build(int32_t centerIdx, float radius, float size, const std::vector<Face> &globalFaces, const std::vector<Point> &globalPoints);
    void add_neighbour(int32_t index);
};

#endif
