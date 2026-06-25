#include "hexasphere.h"
#include "point.h"
#include "face.h"
#include "tile.h"
#include <godot_cpp/core/math.hpp>
#include <cmath>
#include <functional>

Hexasphere::~Hexasphere() = default;

Hexasphere::Hexasphere(float radius, int divisions, float hexSize)
    : _radius(radius), _divisions(divisions), _hexSize(hexSize)
{
    int estimatedPoints = 10 * divisions * divisions + 2;
    _points.reserve(estimatedPoints);
    _pointGrid.reserve(estimatedPoints);
    _tiles.reserve(estimatedPoints);

    auto ico_faces = construct_icosahedron();
    subdivide_icosahedron(ico_faces);
    construct_tiles();
}

std::vector<Face *> Hexasphere::construct_icosahedron()
{
    const float tao = Math_PI / 2.0f;
    const float s = 100.0f;
    float ts = tao * s;

    auto corner = [&](float x, float y, float z) -> Point * {
        return cache_point(Vector3(x, y, z));
    };

    Point *c[12] = {
        corner( s,  ts, 0),   corner(-s,  ts, 0),
        corner( s, -ts, 0),   corner(-s, -ts, 0),
        corner(0,   s,  ts),  corner(0,  -s,  ts),
        corner(0,   s, -ts),  corner(0,  -s, -ts),
        corner( ts, 0,   s),  corner(-ts, 0,   s),
        corner( ts, 0,  -s),  corner(-ts, 0,  -s)
    };

    auto make = [&](Point *p1, Point *p2, Point *p3) -> Face * {
        auto face = std::make_unique<Face>(p1, p2, p3, false);
        Face *ptr = face.get();
        _faces.push_back(std::move(face));
        return ptr;
    };

    return {
        make(c[0],  c[1],  c[4]),   make(c[1],  c[9],  c[4]),
        make(c[4],  c[9],  c[5]),   make(c[5],  c[9],  c[3]),
        make(c[2],  c[3],  c[7]),   make(c[3],  c[2],  c[5]),
        make(c[7],  c[10], c[2]),   make(c[0],  c[8],  c[10]),
        make(c[0],  c[4],  c[8]),   make(c[8],  c[2],  c[10]),
        make(c[8],  c[4],  c[5]),   make(c[8],  c[5],  c[2]),
        make(c[1],  c[0],  c[6]),   make(c[3],  c[9],  c[11]),
        make(c[6],  c[10], c[7]),   make(c[3],  c[11], c[7]),
        make(c[11], c[6],  c[7]),   make(c[6],  c[0],  c[10]),
        make(c[11], c[1],  c[6]),   make(c[9],  c[1],  c[11])
    };
}

Point *Hexasphere::cache_point(const Vector3 &position)
{
    Vector3i gridPos(
        (int)std::round(position.x * GridScale),
        (int)std::round(position.y * GridScale),
        (int)std::round(position.z * GridScale)
    );

    for (int x = -1; x <= 1; x++)
    for (int y = -1; y <= 1; y++)
    for (int z = -1; z <= 1; z++)
    {
        auto it = _pointGrid.find(gridPos + Vector3i(x, y, z));
        if (it != _pointGrid.end())
            if (Point::is_overlapping(*it->second, position))
                return it->second;
    }

    auto pt = std::make_unique<Point>(position);
    Point *ptr = pt.get();
    _pointGrid[gridPos] = ptr;
    _points.push_back(std::move(pt));
    return ptr;
}

void Hexasphere::subdivide_icosahedron(const std::vector<Face *> &ico_faces)
{
    int estimatedFaces = 20 * _divisions * _divisions;
    _faces.reserve(_faces.size() + estimatedFaces);

    auto make_face = [&](Point *p1, Point *p2, Point *p3) -> void {
        auto face = std::make_unique<Face>(p1, p2, p3);
        _faces.push_back(std::move(face));
    };

    for (Face *icoFace : ico_faces)
    {
        Point *fp[3] = { icoFace->get_points()[0], icoFace->get_points()[1], icoFace->get_points()[2] };

        std::vector<Point *> bottomRow = { fp[0] };

        std::function<Point *(const Vector3 &)> cache_fn = [this](const Vector3 &pos) {
            return cache_point(pos);
        };

        std::vector<Point *> leftSide = fp[0]->subdivide(fp[1], _divisions, cache_fn);
        std::vector<Point *> rightSide = fp[0]->subdivide(fp[2], _divisions, cache_fn);

        for (int i = 1; i <= _divisions; i++)
        {
            std::vector<Point *> previousRow = std::move(bottomRow);
            bottomRow = leftSide[i]->subdivide(rightSide[i], i, cache_fn);

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
    for (const auto &pt : _points)
        _tiles.push_back(std::make_unique<Tile>(pt.get(), _radius, _hexSize));

    std::unordered_map<int, Tile *> tile_map;
    tile_map.reserve(_tiles.size());
    for (const auto &tile : _tiles)
        tile_map[tile->get_center()->get_id()] = tile.get();

    for (const auto &tile : _tiles)
        tile->resolve_neighbour_tiles_fast(tile_map);
}
