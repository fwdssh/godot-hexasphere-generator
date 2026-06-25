#include "native_hexasphere.h"
#include "hexasphere.h"
#include "tile.h"
#include "point.h"
#include "face.h"

NativeHexasphere::NativeHexasphere() = default;
NativeHexasphere::~NativeHexasphere() = default;

void NativeHexasphere::_bind_methods()
{
    ClassDB::bind_method(D_METHOD("generate", "radius", "divisions", "hexSize"), &NativeHexasphere::generate);
    ClassDB::bind_method(D_METHOD("get_tile_count"), &NativeHexasphere::get_tile_count);
    ClassDB::bind_method(D_METHOD("get_tile_center", "tile_idx"), &NativeHexasphere::get_tile_center);
    ClassDB::bind_method(D_METHOD("get_tile_points", "tile_idx"), &NativeHexasphere::get_tile_points);
    ClassDB::bind_method(D_METHOD("get_tile_faces", "tile_idx"), &NativeHexasphere::get_tile_faces);
    ClassDB::bind_method(D_METHOD("get_build_data"), &NativeHexasphere::get_build_data);
}

void NativeHexasphere::generate(float radius, int divisions, float hexSize)
{
    _hexasphere = std::make_unique<Hexasphere>(radius, divisions, hexSize);
}

int NativeHexasphere::get_tile_count() const
{
    return _hexasphere ? _hexasphere->get_tile_count() : 0;
}

Vector3 NativeHexasphere::get_tile_center(int tile_idx) const
{
    if (!_hexasphere || tile_idx < 0 || tile_idx >= _hexasphere->get_tile_count())
        return Vector3();
    return _hexasphere->get_tiles()[tile_idx]->get_center()->get_position();
}

PackedVector3Array NativeHexasphere::get_tile_points(int tile_idx) const
{
    if (!_hexasphere || tile_idx < 0 || tile_idx >= _hexasphere->get_tile_count())
        return PackedVector3Array();

    const auto &tiles = _hexasphere->get_tiles();
    const auto &boundary = tiles[tile_idx]->get_boundary_points();
    int count = (int)boundary.size();

    PackedVector3Array result;
    result.resize(count);
    for (int i = 0; i < count; i++)
        result[i] = boundary[i]->get_position();

    return result;
}

PackedInt32Array NativeHexasphere::get_tile_faces(int tile_idx) const
{
    if (!_hexasphere || tile_idx < 0 || tile_idx >= _hexasphere->get_tile_count())
        return PackedInt32Array();

    const auto &tiles = _hexasphere->get_tiles();
    const auto &tile = tiles[tile_idx];
    const auto &boundary = tile->get_boundary_points();
    const auto &faces = tile->get_faces();
    int face_count = (int)faces.size();
    int boundary_count = (int)boundary.size();

    PackedInt32Array result;
    result.resize(face_count * 3);

    for (int f = 0; f < face_count; f++)
    {
        int base = f * 3;
        for (int c = 0; c < 3; c++)
        {
            int pt_id = faces[f]->get_points()[c]->get_id();
            int local_idx = 0;
            for (int b = 0; b < boundary_count; b++)
            {
                if (boundary[b]->get_id() == pt_id)
                {
                    local_idx = b;
                    break;
                }
            }
            result[base + c] = local_idx;
        }
    }

    return result;
}

Dictionary NativeHexasphere::get_build_data() const
{
    Dictionary result;
    if (!_hexasphere || _hexasphere->get_tile_count() == 0)
    {
        result["points"] = PackedVector3Array();
        result["face_indices"] = PackedInt32Array();
        result["point_counts"] = PackedInt32Array();
        result["face_vertex_counts"] = PackedInt32Array();
        return result;
    }

    const auto &tiles = _hexasphere->get_tiles();
    int tileCount = _hexasphere->get_tile_count();

    int totalPoints = 0;
    int totalFaceIndices = 0;
    for (int t = 0; t < tileCount; t++)
    {
        totalPoints += (int)tiles[t]->get_boundary_points().size();
        totalFaceIndices += (int)tiles[t]->get_faces().size() * 3;
    }

    PackedVector3Array points;
    points.resize(totalPoints);
    PackedInt32Array faceIndices;
    faceIndices.resize(totalFaceIndices);
    PackedInt32Array pointCounts;
    pointCounts.resize(tileCount);
    PackedInt32Array faceVertexCounts;
    faceVertexCounts.resize(tileCount);

    int ptOffset = 0;
    int faceOffset = 0;

    for (int t = 0; t < tileCount; t++)
    {
        const auto &boundary = tiles[t]->get_boundary_points();
        int ptCount = (int)boundary.size();
        pointCounts[t] = ptCount;

        for (int i = 0; i < ptCount; i++)
            points[ptOffset + i] = boundary[i]->get_position();

        const auto &tileFaces = tiles[t]->get_faces();
        int faceCount = (int)tileFaces.size();
        faceVertexCounts[t] = faceCount * 3;

        for (int f = 0; f < faceCount; f++)
        {
            int base = faceOffset + f * 3;
            for (int c = 0; c < 3; c++)
            {
                int pt_id = tileFaces[f]->get_points()[c]->get_id();
                int local_idx = 0;
                for (int b = 0; b < ptCount; b++)
                {
                    if (boundary[b]->get_id() == pt_id)
                    {
                        local_idx = b;
                        break;
                    }
                }
                faceIndices[base + c] = local_idx;
            }
        }

        ptOffset += ptCount;
        faceOffset += faceCount * 3;
    }

    result["points"] = points;
    result["face_indices"] = faceIndices;
    result["point_counts"] = pointCounts;
    result["face_vertex_counts"] = faceVertexCounts;

    return result;
}
