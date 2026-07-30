#ifndef HEXASPHERE_HEXASPHERE_H
#define HEXASPHERE_HEXASPHERE_H

#include <cstddef>
#include <memory>
#include <vector>
#include <godot_cpp/variant/vector3.hpp>
#include <godot_cpp/variant/vector3i.hpp>
#include "ankerl/unordered_dense.h"

using namespace godot;

class Point;
class Face;
class Tile;

struct Vector3iHash
{
    std::size_t operator()(const Vector3i &v) const
    {
        std::size_t h = std::hash<int>()(v.x);
        h ^= std::hash<int>()(v.y) + 0x9e3779b9 + (h << 6) + (h >> 2);
        h ^= std::hash<int>()(v.z) + 0x9e3779b9 + (h << 6) + (h >> 2);
        return h;
    }
};

class Hexasphere
{
private:
    float _radius;
    int _divisions;
    float _hexSize;
    float _pointEpsilon;
    float _gridScale;

    std::vector<Point> _points;
    std::vector<Face> _faces;
    std::vector<std::unique_ptr<Tile>> _tiles;
    ankerl::unordered_dense::map<Vector3i, int32_t, Vector3iHash> _pointGrid;

public:
    Hexasphere(float radius, int divisions, float hexSize);
    ~Hexasphere();
    Hexasphere(const Hexasphere &) = delete;
    Hexasphere &operator=(const Hexasphere &) = delete;

    const std::vector<std::unique_ptr<Tile>> &get_tiles() const { return _tiles; }
    const std::vector<Point> &get_points() const { return _points; }
    int get_tile_count() const { return static_cast<int>(_tiles.size()); }

private:
    std::vector<int32_t> construct_icosahedron();
    int32_t cache_point(const Vector3 &position);
    void subdivide_icosahedron(const std::vector<int32_t> &ico_faces);
    void construct_tiles();
};

#endif
