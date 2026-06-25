#ifndef HEXASPHERE_HEXASPHERE_H
#define HEXASPHERE_HEXASPHERE_H

#include <cstddef>
#include <memory>
#include <vector>
#include <unordered_map>
#include <godot_cpp/variant/vector3.hpp>
#include <godot_cpp/variant/vector3i.hpp>

using namespace godot;

class Point;
class Face;
class Tile;

struct Vector3iHash
{
    std::size_t operator()(const Vector3i &v) const
    {
        return std::hash<int>()(v.x) ^ (std::hash<int>()(v.y) << 1) ^ (std::hash<int>()(v.z) << 2);
    }
};

class Hexasphere
{
private:
    float _radius;
    int _divisions;
    float _hexSize;

    std::vector<std::unique_ptr<Point>> _points;
    std::vector<std::unique_ptr<Face>> _faces;
    std::vector<std::unique_ptr<Tile>> _tiles;
    std::unordered_map<Vector3i, Point *, Vector3iHash> _pointGrid;

    static constexpr float GridScale = 5000.0f;

public:
    Hexasphere(float radius, int divisions, float hexSize);
    ~Hexasphere();
    Hexasphere(const Hexasphere &) = delete;
    Hexasphere &operator=(const Hexasphere &) = delete;

    const std::vector<std::unique_ptr<Tile>> &get_tiles() const { return _tiles; }
    const std::vector<std::unique_ptr<Point>> &get_points() const { return _points; }

    int get_tile_count() const { return (int)_tiles.size(); }

private:
    std::vector<Face *> construct_icosahedron();
    Point *cache_point(const Vector3 &position);
    void subdivide_icosahedron(const std::vector<Face *> &ico_faces);
    void construct_tiles();
};

#endif // HEXASPHERE_HEXASPHERE_H
