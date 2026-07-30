#include "tile.h"
#include "point.h"
#include "face.h"
#include <godot_cpp/core/math.hpp>
#include <unordered_set>

Tile::Tile(int32_t centerIdx, float radius, float size, const std::vector<Face>& globalFaces, const std::vector<Point>& globalPoints)
    : _centerIdx(centerIdx), _radius(radius), _size(Math::clamp(size, 0.01f, 1.0f))
{
    std::vector<int32_t> icosahedron_faces = globalPoints[centerIdx].get_ordered_faces(globalFaces);

    store_neighbour_centers(icosahedron_faces, globalFaces);
    build_faces(icosahedron_faces, globalFaces, globalPoints);
}

void Tile::store_neighbour_centers(const std::vector<int32_t>& icosahedron_faces, const std::vector<Face>& globalFaces)
{
    std::unordered_set<int> seen;
    for (int32_t faceIdx : icosahedron_faces)
    {
        int32_t a = -1, b = -1;
        globalFaces[faceIdx].get_other_point_indices(_centerIdx, a, b);

        if (seen.insert(a).second)
            _neighbourCenters[_neighbourCenterCount++] = a;
        if (seen.insert(b).second)
            _neighbourCenters[_neighbourCenterCount++] = b;
    }
}

void Tile::build_faces(const std::vector<int32_t>& icosahedron_faces, const std::vector<Face>& globalFaces, const std::vector<Point>& globalPoints)
{
    Vector3 centerPos = globalPoints[_centerIdx].get_position();
    int localPtId = 0;
    for (int32_t faceIdx : icosahedron_faces)
    {
        Vector3 lerped = centerPos.lerp(globalFaces[faceIdx].get_center_position(globalPoints), _size);
        float scale = _radius / lerped.length();
        _boundaryPoints[_boundaryCount++] = Point(lerped * scale, localPtId++);
    }

    int n = _boundaryCount;
    if (n < 3) return;

    int localFaceId = 0;
    for (int i = 1; i < n - 1; i++)
    {
        _faces[_faceCount] = Face(0, i, i + 1, localFaceId++,
            _boundaryPoints[0].get_position(),
            _boundaryPoints[i].get_position(),
            _boundaryPoints[i + 1].get_position());

        _faceIndices[_faceCount] = {
            _faces[_faceCount].get_point_indices()[0],
            _faces[_faceCount].get_point_indices()[1],
            _faces[_faceCount].get_point_indices()[2]
        };
        _faceCount++;
    }
}

void Tile::resolve_neighbour_tiles_fast(const std::unordered_map<int, Tile*>& tile_map)
{
    _neighbourCount = 0;
    for (int i = 0; i < _neighbourCenterCount; i++)
    {
        auto it = tile_map.find(_neighbourCenters[i]);
        if (it != tile_map.end())
            _neighbours[_neighbourCount++] = it->second;
    }
}
