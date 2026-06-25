#ifndef HEXASPHERE_TILE_H
#define HEXASPHERE_TILE_H

#include <memory>
#include <vector>
#include <unordered_map>
#include <godot_cpp/variant/vector3.hpp>

using namespace godot;

class Point;
class Face;

class Tile
{
private:
    Point *_center;
    float _radius;
    float _size;

    std::vector<std::unique_ptr<Point>> _boundaryPoints;
    std::vector<std::unique_ptr<Face>> _faces;
    std::vector<Point *> _neighbourCenters;
    std::vector<Tile *> _neighbours;

public:
    Tile(Point *center, float radius, float size);
    ~Tile() = default;
    Tile(const Tile &) = delete;
    Tile &operator=(const Tile &) = delete;

    Point *get_center() const { return _center; }
    const std::vector<std::unique_ptr<Point>> &get_boundary_points() const { return _boundaryPoints; }
    const std::vector<std::unique_ptr<Face>> &get_faces() const { return _faces; }
    std::vector<Tile *> &get_neighbours() { return _neighbours; }
    const std::vector<Tile *> &get_neighbours() const { return _neighbours; }

    void resolve_neighbour_tiles_fast(const std::unordered_map<int, Tile *> &tile_map);

private:
    void store_neighbour_centers(const std::vector<Face *> &icosahedron_faces);
    void build_faces(const std::vector<Face *> &icosahedron_faces);
};

#endif // HEXASPHERE_TILE_H
