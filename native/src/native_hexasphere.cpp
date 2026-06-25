#include "native_hexasphere.h"
#include "hexasphere.h"
#include "tile.h"
#include "point.h"
#include "face.h"
#include <godot_cpp/classes/array_mesh.hpp>

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
    ClassDB::bind_method(D_METHOD("get_border_data"), &NativeHexasphere::get_border_data);
    ClassDB::bind_method(D_METHOD("build_mesh"), &NativeHexasphere::build_mesh);
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

Dictionary NativeHexasphere::get_border_data() const
{
    Dictionary result;
    if (!_hexasphere || _hexasphere->get_tile_count() == 0)
    {
        result["positions"] = PackedVector3Array();
        result["tile_line_counts"] = PackedInt32Array();
        return result;
    }

    const auto &tiles = _hexasphere->get_tiles();
    int tileCount = _hexasphere->get_tile_count();

    int totalPositions = 0;
    for (int t = 0; t < tileCount; t++)
        totalPositions += (int)tiles[t]->get_boundary_points().size() * 2;

    PackedVector3Array positions;
    positions.resize(totalPositions);
    PackedInt32Array tileLineCounts;
    tileLineCounts.resize(tileCount);

    int posOffset = 0;
    for (int t = 0; t < tileCount; t++)
    {
        const auto &boundary = tiles[t]->get_boundary_points();
        int ptCount = (int)boundary.size();
        tileLineCounts[t] = ptCount * 2;

        for (int p = 0; p < ptCount; p++)
        {
            int next = (p + 1) % ptCount;
            positions[posOffset + p * 2 + 0] = boundary[p]->get_position();
            positions[posOffset + p * 2 + 1] = boundary[next]->get_position();
        }

        posOffset += ptCount * 2;
    }

    result["positions"] = positions;
    result["tile_line_counts"] = tileLineCounts;

    return result;
}

Dictionary NativeHexasphere::build_mesh() const
{
    Dictionary result;
    if (!_hexasphere || _hexasphere->get_tile_count() == 0)
    {
        return result;
    }

    const auto &tiles = _hexasphere->get_tiles();
    int tileCount = _hexasphere->get_tile_count();

    int totalVertices = 0;
    for (int t = 0; t < tileCount; t++)
        totalVertices += (int)tiles[t]->get_faces().size() * 3;

    PackedVector3Array vertices;
    vertices.resize(totalVertices);
    PackedVector3Array normals;
    normals.resize(totalVertices);
    PackedVector2Array uv2s;
    uv2s.resize(totalVertices);

    PackedInt32Array tileVertexCounts;
    tileVertexCounts.resize(tileCount);
    PackedInt32Array allIndices;
    allIndices.resize(totalVertices);

    int globalVertexIndex = 0;
    int indicesOffset = 0;

    for (int t = 0; t < tileCount; t++)
    {
        const auto &boundary = tiles[t]->get_boundary_points();
        int ptCount = (int)boundary.size();
        const auto &tileFaces = tiles[t]->get_faces();
        int faceCount = (int)tileFaces.size();

        tileVertexCounts[t] = faceCount * 3;
        Vector2 tileUV(t, 0.0f);

        for (int f = 0; f < faceCount; f++)
        {
            int base = indicesOffset + f * 3;
            const auto &facePoints = tileFaces[f]->get_points();

            int localIdx[3];
            for (int c = 0; c < 3; c++)
            {
                int pt_id = facePoints[c]->get_id();
                localIdx[c] = 0;
                for (int b = 0; b < ptCount; b++)
                {
                    if (boundary[b]->get_id() == pt_id)
                    {
                        localIdx[c] = b;
                        break;
                    }
                }
            }

            int order[3] = { localIdx[0], localIdx[2], localIdx[1] };
            for (int v = 0; v < 3; v++)
            {
                Vector3 pos = boundary[order[v]]->get_position();
                int vi = globalVertexIndex++;
                vertices[vi] = pos;
                normals[vi] = pos.normalized();
                uv2s[vi] = tileUV;
                allIndices[base + v] = vi;
            }
        }

        indicesOffset += faceCount * 3;
    }

    Array surfaceArrays;
    surfaceArrays.resize(Mesh::ARRAY_MAX);
    surfaceArrays[Mesh::ARRAY_VERTEX] = vertices;
    surfaceArrays[Mesh::ARRAY_NORMAL] = normals;
    surfaceArrays[Mesh::ARRAY_TEX_UV2] = uv2s;

    Ref<ArrayMesh> mesh;
    mesh.instantiate();
    mesh->add_surface_from_arrays(Mesh::PRIMITIVE_TRIANGLES, surfaceArrays);

    result["mesh"] = mesh;
    result["tile_vertex_counts"] = tileVertexCounts;
    result["tile_vertex_indices"] = allIndices;

    return result;
}
