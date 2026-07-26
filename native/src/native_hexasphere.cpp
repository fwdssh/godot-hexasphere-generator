#include "native_hexasphere.h"
#include "hexasphere.h"
#include "tile.h"
#include "point.h"
#include "face.h"
#include <godot_cpp/classes/array_mesh.hpp>
#include <unordered_map>

/// <summary>
/// Default constructor. Call generate() to initialize the sphere data.
/// </summary>
NativeHexasphere::NativeHexasphere() = default;

/// <summary>
/// Destructor. The internal Hexasphere instance is cleaned up automatically.
/// </summary>
NativeHexasphere::~NativeHexasphere() = default;

void NativeHexasphere::_bind_methods()
{
    ClassDB::bind_method(D_METHOD("generate", "radius", "divisions", "hexSize"), &NativeHexasphere::generate);
    ClassDB::bind_method(D_METHOD("get_tile_count"), &NativeHexasphere::get_tile_count);
    ClassDB::bind_method(D_METHOD("get_tile_center", "tile_idx"), &NativeHexasphere::get_tile_center);
    ClassDB::bind_method(D_METHOD("get_all_tile_centers"), &NativeHexasphere::get_all_tile_centers);
    ClassDB::bind_method(D_METHOD("get_tile_points", "tile_idx"), &NativeHexasphere::get_tile_points);
    ClassDB::bind_method(D_METHOD("get_tile_faces", "tile_idx"), &NativeHexasphere::get_tile_faces);
    ClassDB::bind_method(D_METHOD("get_build_data"), &NativeHexasphere::get_build_data);
    ClassDB::bind_method(D_METHOD("get_border_data"), &NativeHexasphere::get_border_data);
    ClassDB::bind_method(D_METHOD("build_mesh"), &NativeHexasphere::build_mesh);
    ClassDB::bind_method(D_METHOD("get_all_tile_neighbors"), &NativeHexasphere::get_all_tile_neighbors);
}

/// <summary>
/// Generates the hexagonal sphere. Creates the internal Hexasphere instance with the given parameters.
/// </summary>
void NativeHexasphere::generate(float radius, int divisions, float hexSize)
{
    _hexasphere = std::make_unique<Hexasphere>(radius, divisions, hexSize);
}

/// <summary>
/// Returns the number of tiles, or 0 if no sphere has been generated.
/// </summary>
int NativeHexasphere::get_tile_count() const
{
    return _hexasphere ? _hexasphere->get_tile_count() : 0;
}

/// <summary>
/// Returns the world-space center of the tile at the given index.
/// Returns Vector3() if the index is out of range or no sphere has been generated.
/// </summary>
Vector3 NativeHexasphere::get_tile_center(int tile_idx) const
{
    if (!_hexasphere || tile_idx < 0 || tile_idx >= _hexasphere->get_tile_count())
        return Vector3();
    return _hexasphere->get_tiles()[tile_idx]->get_center()->get_position();
}

/// <summary>
/// Returns an array of all tile center positions. Each entry corresponds to one tile in index order.
/// </summary>
PackedVector3Array NativeHexasphere::get_all_tile_centers() const
{
    if (!_hexasphere)
        return PackedVector3Array();

    const auto &tiles = _hexasphere->get_tiles();
    int tileCount = _hexasphere->get_tile_count();

    PackedVector3Array result;
    result.resize(tileCount);
    for (int i = 0; i < tileCount; i++)
        result[i] = tiles[i]->get_center()->get_position();

    return result;
}

/// <summary>
/// Returns the boundary vertex positions for the tile at the given index.
/// The points form a closed polygon around the tile.
/// </summary>
PackedVector3Array NativeHexasphere::get_tile_points(int tile_idx) const
{
    if (!_hexasphere || tile_idx < 0 || tile_idx >= _hexasphere->get_tile_count())
        return PackedVector3Array();

    const auto &tiles = _hexasphere->get_tiles();
    const auto &boundary = tiles[tile_idx]->get_boundary_points();
    int count = tiles[tile_idx]->get_boundary_count();

    PackedVector3Array result;
    result.resize(count);
    for (int i = 0; i < count; i++)
        result[i] = boundary[i].get_position();

    return result;
}

/// <summary>
/// Returns triangle face indices for the tile at the given index.
/// Indices are local to this tile's boundary points. Every three consecutive values form one triangle.
/// </summary>
PackedInt32Array NativeHexasphere::get_tile_faces(int tile_idx) const
{
    if (!_hexasphere || tile_idx < 0 || tile_idx >= _hexasphere->get_tile_count())
        return PackedInt32Array();

    const auto &tiles = _hexasphere->get_tiles();
    const auto &tile = tiles[tile_idx];
    const auto &faceIndices = tile->get_face_indices();
    int face_count = tile->get_face_count();

    PackedInt32Array result;
    result.resize(face_count * 3);

    for (int f = 0; f < face_count; f++)
    {
        int base = f * 3;
        result[base + 0] = faceIndices[f][0];
        result[base + 1] = faceIndices[f][1];
        result[base + 2] = faceIndices[f][2];
    }

    return result;
}

/// <summary>
/// Returns per-tile build data: vertex positions, face indices, point counts, and face vertex counts.
/// Useful for custom mesh construction on the C# side.
/// </summary>
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
        totalPoints += tiles[t]->get_boundary_count();
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
        int ptCount = tiles[t]->get_boundary_count();
        pointCounts[t] = ptCount;

        for (int i = 0; i < ptCount; i++)
            points[ptOffset + i] = boundary[i].get_position();

        const auto &tileFaceIndices = tiles[t]->get_face_indices();
        int faceCount = tiles[t]->get_face_count();
        faceVertexCounts[t] = faceCount * 3;

        for (int f = 0; f < faceCount; f++)
        {
            int base = faceOffset + f * 3;
            faceIndices[base + 0] = tileFaceIndices[f][0];
            faceIndices[base + 1] = tileFaceIndices[f][1];
            faceIndices[base + 2] = tileFaceIndices[f][2];
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

/// <summary>
/// Returns per-tile border line data for wireframe or outline rendering.
/// Each tile's boundary is represented as line segments (pairs of consecutive positions).
/// </summary>
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
        totalPositions += tiles[t]->get_boundary_count() * 2;

    PackedVector3Array positions;
    positions.resize(totalPositions);
    PackedInt32Array tileLineCounts;
    tileLineCounts.resize(tileCount);

    int posOffset = 0;
    for (int t = 0; t < tileCount; t++)
    {
        const auto &boundary = tiles[t]->get_boundary_points();
        int ptCount = tiles[t]->get_boundary_count();
        tileLineCounts[t] = ptCount * 2;

        for (int p = 0; p < ptCount; p++)
        {
            int next = (p + 1) % ptCount;
            positions[posOffset + p * 2 + 0] = boundary[p].get_position();
            positions[posOffset + p * 2 + 1] = boundary[next].get_position();
        }

        posOffset += ptCount * 2;
    }

    result["positions"] = positions;
    result["tile_line_counts"] = tileLineCounts;

    return result;
}

/// <summary>
/// Builds and returns a complete ArrayMesh for the entire sphere, with vertex positions,
/// normals, and UV2 data (tile index in UV2.x). Also returns per-tile vertex counts and indices
/// for tile-specific processing on the C# side.
/// Dictionary keys: "mesh" (ArrayMesh), "tile_vertex_counts", "tile_vertex_indices".
/// </summary>
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
        int ptCount = tiles[t]->get_boundary_count();
        const auto &tileFaceIndices = tiles[t]->get_face_indices();
        int faceCount = tiles[t]->get_face_count();

        tileVertexCounts[t] = faceCount * 3;
        Vector2 tileUV(t, 0.0f);

        for (int f = 0; f < faceCount; f++)
        {
            int base = indicesOffset + f * 3;
            int localIdx[3] = {
                tileFaceIndices[f][0],
                tileFaceIndices[f][1],
                tileFaceIndices[f][2]};

            int order[3] = { localIdx[0], localIdx[2], localIdx[1] };
            for (int v = 0; v < 3; v++)
            {
                Vector3 pos = boundary[order[v]].get_position();
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

Dictionary NativeHexasphere::get_all_tile_neighbors() const
{
    Dictionary result;
    if (!_hexasphere || _hexasphere->get_tile_count() == 0)
    {
        result["neighbor_indices"] = PackedInt32Array();
        result["offsets"] = PackedInt32Array();
        return result;
    }

    const auto &tiles = _hexasphere->get_tiles();
    int tileCount = _hexasphere->get_tile_count();

    std::unordered_map<const Tile *, int> tileIndex;
    tileIndex.reserve(tileCount);
    for (int i = 0; i < tileCount; i++)
        tileIndex[tiles[i].get()] = i;

    int totalNeighbors = 0;
    for (int t = 0; t < tileCount; t++)
        totalNeighbors += tiles[t]->get_neighbour_count();

    PackedInt32Array neighborIndices;
    neighborIndices.resize(totalNeighbors);
    PackedInt32Array offsets;
    offsets.resize(tileCount + 1);

    int writePos = 0;
    for (int t = 0; t < tileCount; t++)
    {
        offsets[t] = writePos;
        const Tile *const *neighbours = tiles[t]->get_neighbours_data();
        int nCount = tiles[t]->get_neighbour_count();
        for (int n = 0; n < nCount; n++)
        {
            auto it = tileIndex.find(neighbours[n]);
            neighborIndices[writePos++] = (it != tileIndex.end()) ? it->second : -1;
        }
    }
    offsets[tileCount] = writePos;

    result["neighbor_indices"] = neighborIndices;
    result["offsets"] = offsets;
    return result;
}
