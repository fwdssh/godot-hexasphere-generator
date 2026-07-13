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

    _faces.reserve(faceCount);

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
            _neighbourCenters[_neighbourCenterCount++] = a;
        if (seen.insert(b->get_id()).second)
            _neighbourCenters[_neighbourCenterCount++] = b;
    }
}

void Tile::build_faces(const std::vector<Face *> &icosahedron_faces)
{
    Vector3 centerPos = _center->get_position();
    int localPtId = 0;
    for (Face *face : icosahedron_faces)
    {
        Vector3 lerped = centerPos.lerp(face->get_center_position(), _size);
        float scale = _radius / lerped.length();
        _boundaryPoints[_boundaryCount++] = Point(lerped * scale, localPtId++);
    }

    int n = _boundaryCount;
    if (n < 3) return;

    _faces.reserve(n - 2);
    int localFaceId = 0;
    for (int i = 1; i < n - 1; i++)
    {
        _faces.emplace_back(
            &_boundaryPoints[0],
            &_boundaryPoints[i],
            &_boundaryPoints[i + 1],
            localFaceId++);

        // Determine actual local boundary indices by pointer comparison — O(1) per vertex
        auto findLocalIdx = [&](Point *p) -> int {
            if (p == &_boundaryPoints[0]) return 0;
            if (p == &_boundaryPoints[i]) return i;
            return i + 1;
        };

        _faceIndices[_faceCount++] = {
            findLocalIdx(_faces.back().get_points()[0]),
            findLocalIdx(_faces.back().get_points()[1]),
            findLocalIdx(_faces.back().get_points()[2])};
    }
}

void Tile::resolve_neighbour_tiles_fast(const std::unordered_map<int, Tile *> &tile_map)
{
    _neighbourCount = 0;
    for (int i = 0; i < _neighbourCenterCount; i++)
    {
        auto it = tile_map.find(_neighbourCenters[i]->get_id());
        if (it != tile_map.end())
            _neighbours[_neighbourCount++] = it->second;
    }
}
