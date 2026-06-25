#include "tile.h"
#include "point.h"
#include "face.h"
#include <godot_cpp/core/math.hpp>
#include <unordered_set>

Tile::Tile(Point *center, float radius, float size)
    : _center(center), _radius(radius), _size(Math::clamp(size, 0.01f, 1.0f))
{
    std::vector<Face *> icosahedron_faces = center->get_ordered_faces();
    int faceCount = (int)icosahedron_faces.size();

    _neighbourCenters.reserve(faceCount * 2);
    _neighbours.reserve(faceCount * 2);
    _boundaryPoints.reserve(faceCount);

    store_neighbour_centers(icosahedron_faces);
    build_faces(icosahedron_faces);
}

void Tile::store_neighbour_centers(const std::vector<Face *> &icosahedron_faces)
{
    std::unordered_set<int> seen;
    for (Face *face : icosahedron_faces)
    {
        Point *a = nullptr, *b = nullptr;
        face->get_other_points(_center, a, b);

        if (seen.insert(a->get_id()).second)
            _neighbourCenters.push_back(a);
        if (seen.insert(b->get_id()).second)
            _neighbourCenters.push_back(b);
    }
}

void Tile::build_faces(const std::vector<Face *> &icosahedron_faces)
{
    Vector3 centerPos = _center->get_position();
    float projRadiusHalf = _radius * 0.5f;

    for (Face *face : icosahedron_faces)
    {
        Vector3 lerped = centerPos.lerp(face->get_center_position(), _size);
        float scale = projRadiusHalf / lerped.length();
        _boundaryPoints.push_back(std::make_unique<Point>(lerped * scale));
    }

    int n = (int)_boundaryPoints.size();
    if (n < 3) return;

    _faces.reserve(n - 2);
    for (int i = 1; i < n - 1; i++)
    {
        _faces.push_back(std::make_unique<Face>(
            _boundaryPoints[0].get(),
            _boundaryPoints[i].get(),
            _boundaryPoints[i + 1].get(),
            false));
    }
}

void Tile::resolve_neighbour_tiles_fast(const std::unordered_map<int, Tile *> &tile_map)
{
    _neighbours.clear();
    for (Point *c : _neighbourCenters)
    {
        auto it = tile_map.find(c->get_id());
        if (it != tile_map.end())
            _neighbours.push_back(it->second);
    }
}
