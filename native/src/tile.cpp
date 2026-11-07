#include "tile.h"
#include "point.h"
#include "face.h"
#include <algorithm>
#include <cmath>
#include <stdexcept>

void Tile::build(int32_t centerIdx, float radius, float size, const std::vector<Face> &globalFaces, const std::vector<Point> &globalPoints)
{
    const Point &center = globalPoints[centerIdx];
    _center = center.get_position();
    std::array<int32_t, 6> orderedFaces;
    const int faceCount = center.get_ordered_faces(globalFaces, orderedFaces);

    for (int i = 0; i < faceCount; i++)
    {
        const Face &face = globalFaces[orderedFaces[i]];
        int32_t a = -1, b = -1;
        face.get_other_point_indices(centerIdx, a, b);
        add_neighbour(a);
        add_neighbour(b);

        Vector3 lerped = _center.lerp(face.get_center_position(globalPoints), size);
        const double length = std::hypot(double(lerped.x), double(lerped.y), double(lerped.z));
        if (!std::isfinite(length) || length == 0.0)
            throw std::invalid_argument("Cannot project a degenerate tile boundary");
        _boundaryPoints[_boundaryCount++] = Vector3(lerped.x / length * radius,
            lerped.y / length * radius, lerped.z / length * radius);
    }

    for (int i = 1; i < _boundaryCount - 1; i++)
    {
        const Face triangle(0, i, i + 1, i - 1,
            _boundaryPoints[0], _boundaryPoints[i], _boundaryPoints[i + 1]);
        const int32_t *indices = triangle.get_point_indices();
        _faceIndices[_faceCount++] = {static_cast<uint8_t>(indices[0]),
            static_cast<uint8_t>(indices[1]), static_cast<uint8_t>(indices[2])};
    }
}

void Tile::add_neighbour(int32_t index)
{
    const auto end = _neighbours.begin() + _neighbourCount;
    if (std::find(_neighbours.begin(), end, index) != end)
        return;
    if (_neighbourCount == _neighbours.size())
        throw std::invalid_argument("Sphere tile exceeds supported neighbour count");
    _neighbours[_neighbourCount++] = index;
}
