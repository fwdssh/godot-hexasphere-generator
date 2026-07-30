#include "hexasphere.h"
#include "point.h"
#include "face.h"
#include "tile.h"
#include <godot_cpp/core/math.hpp>
#include <algorithm>
#include <execution>
#include <functional>
#include <numeric>
#include <stdexcept>
#include <unordered_map>

Hexasphere::~Hexasphere() = default;

Hexasphere::Hexasphere(float radius, int divisions, float hexSize)
    : _radius(radius), _divisions(divisions), _hexSize(hexSize)
{
    if (radius <= 0.0f)
        throw std::invalid_argument("Radius must be positive");

    _pointEpsilon = 1e-5f * _radius;
    _gridScale = 1e5f / _radius;

    int estimatedPoints = 10 * divisions * divisions + 2;
    _points.reserve(estimatedPoints);
    _pointGrid.reserve(estimatedPoints);
    _tiles.reserve(estimatedPoints);

    auto ico_faces = construct_icosahedron();
    subdivide_icosahedron(ico_faces);
    construct_tiles();

    // Free subdivision data
    _pointGrid = decltype(_pointGrid)();
    _faces.clear();
    _faces.shrink_to_fit();
}

std::vector<int32_t> Hexasphere::construct_icosahedron()
{
    const float tao = (1.0f + Math::sqrt(5.0f)) / 2.0f;
    const float s = 100.0f;
    float ts = tao * s;

    auto corner = [&](float x, float y, float z) -> int32_t
    {
        return cache_point(Vector3(x, y, z));
    };

    int32_t c[12] = {
        corner(s, ts, 0), corner(-s, ts, 0),
        corner(s, -ts, 0), corner(-s, -ts, 0),
        corner(0, s, ts), corner(0, -s, ts),
        corner(0, s, -ts), corner(0, -s, -ts),
        corner(ts, 0, s), corner(-ts, 0, s),
        corner(ts, 0, -s), corner(-ts, 0, -s)
    };

    _faces.reserve(20);

    auto make = [&](int32_t p1, int32_t p2, int32_t p3) -> int32_t
    {
        int32_t faceIdx = static_cast<int32_t>(_faces.size());
        _faces.emplace_back(p1, p2, p3, faceIdx,
            _points[p1].get_position(),
            _points[p2].get_position(),
            _points[p3].get_position());
        return faceIdx;
    };

    return {
        make(c[0], c[1], c[4]), make(c[1], c[9], c[4]),
        make(c[4], c[9], c[5]), make(c[5], c[9], c[3]),
        make(c[2], c[3], c[7]), make(c[3], c[2], c[5]),
        make(c[7], c[10], c[2]), make(c[0], c[8], c[10]),
        make(c[0], c[4], c[8]), make(c[8], c[2], c[10]),
        make(c[8], c[4], c[5]), make(c[8], c[5], c[2]),
        make(c[1], c[0], c[6]), make(c[3], c[9], c[11]),
        make(c[6], c[10], c[7]), make(c[3], c[11], c[7]),
        make(c[11], c[6], c[7]), make(c[6], c[0], c[10]),
        make(c[11], c[1], c[6]), make(c[9], c[1], c[11])
    };
}

int32_t Hexasphere::cache_point(const Vector3 &raw_position)
{
    Vector3 position = raw_position.normalized() * _radius;

    Vector3i gridPos(
        static_cast<int>(std::round(position.x * _gridScale)),
        static_cast<int>(std::round(position.y * _gridScale)),
        static_cast<int>(std::round(position.z * _gridScale)));

    for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                auto it = _pointGrid.find(gridPos + Vector3i(x, y, z));
                if (it != _pointGrid.end())
                    if (Point::is_overlapping(_points[it->second], position, _pointEpsilon))
                        return it->second;
            }

    int32_t idx = static_cast<int32_t>(_points.size());
    _points.emplace_back(position, idx);
    _pointGrid[gridPos] = idx;
    return idx;
}

void Hexasphere::subdivide_icosahedron(const std::vector<int32_t> &ico_faces)
{
    int estimatedFaces = 20 * _divisions * _divisions;
    _faces.reserve(_faces.size() + estimatedFaces);

    auto make_face = [&](int32_t p1, int32_t p2, int32_t p3)
    {
        int32_t faceIdx = static_cast<int32_t>(_faces.size());
        _faces.emplace_back(p1, p2, p3, faceIdx,
            _points[p1].get_position(),
            _points[p2].get_position(),
            _points[p3].get_position());
        _points[p1].assign_face_index(faceIdx);
        _points[p2].assign_face_index(faceIdx);
        _points[p3].assign_face_index(faceIdx);
    };

    for (int32_t icoFaceIdx : ico_faces)
    {
        const Face &icoFace = _faces[icoFaceIdx];
        int32_t fp[3] = {icoFace.get_point_indices()[0], icoFace.get_point_indices()[1], icoFace.get_point_indices()[2]};

        std::vector<int32_t> bottomRow = {fp[0]};

        auto cache_fn = [this](const Vector3 &pos) -> int32_t
        {
            return cache_point(pos);
        };

        std::vector<int32_t> leftSide = _points[fp[0]].subdivide(fp[1], _divisions, cache_fn, _points);
        std::vector<int32_t> rightSide = _points[fp[0]].subdivide(fp[2], _divisions, cache_fn, _points);

        for (int i = 1; i <= _divisions; i++)
        {
            std::vector<int32_t> previousRow = std::move(bottomRow);
            bottomRow = _points[leftSide[i]].subdivide(rightSide[i], i, cache_fn, _points);

            make_face(previousRow[0], bottomRow[0], bottomRow[1]);
            for (int j = 1; j < i; j++)
            {
                make_face(previousRow[j], bottomRow[j], bottomRow[j + 1]);
                make_face(previousRow[j - 1], previousRow[j], bottomRow[j]);
            }
        }
    }
}

void Hexasphere::construct_tiles()
{
    int n = static_cast<int>(_points.size());
    _tiles.resize(n);

    std::vector<int> indices(n);
    std::iota(indices.begin(), indices.end(), 0);

    std::for_each(std::execution::par, indices.begin(), indices.end(), [this](int i)
    {
        _tiles[i] = std::make_unique<Tile>(i, _radius, _hexSize, _faces, _points);
        _tiles[i]->set_index(i);
    });

    std::unordered_map<int, Tile *> tile_map;
    tile_map.reserve(n);
    for (const auto &tile : _tiles)
        tile_map[tile->get_center()] = tile.get();

    std::for_each(std::execution::par, indices.begin(), indices.end(), [this, &tile_map](int i)
    {
        _tiles[i]->resolve_neighbour_tiles_fast(tile_map);
    });
}
