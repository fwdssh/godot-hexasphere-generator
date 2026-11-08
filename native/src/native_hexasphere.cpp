#include "native_hexasphere.h"
#include "hexasphere.h"
#include "tile.h"
#include "point.h"
#include "face.h"
#include <godot_cpp/classes/array_mesh.hpp>
#include <godot_cpp/classes/rendering_server.hpp>
#include <algorithm>
#include <chrono>
#include <cmath>
#include <exception>


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
    ClassDB::bind_method(D_METHOD("_prepare_border_surfaces", "vertices_per_surface"), &NativeHexasphere::prepare_border_surfaces);
    ClassDB::bind_method(D_METHOD("build_mesh"), &NativeHexasphere::build_mesh);
    ClassDB::bind_method(D_METHOD("prepare_mesh"), &NativeHexasphere::prepare_mesh);
    ClassDB::bind_method(D_METHOD("prepare_mesh_chunks", "tiles_per_chunk"), &NativeHexasphere::prepare_mesh_chunks);
    ClassDB::bind_method(D_METHOD("_prepare_indexed_mesh_chunks", "tiles_per_chunk"), &NativeHexasphere::prepare_indexed_mesh_chunks);
    ClassDB::bind_method(D_METHOD("get_prepared_mesh_chunk_count"), &NativeHexasphere::get_prepared_mesh_chunk_count);
    ClassDB::bind_method(D_METHOD("append_mesh_chunk", "mesh"), &NativeHexasphere::append_mesh_chunk);
    ClassDB::bind_method(D_METHOD("get_all_tile_neighbors"), &NativeHexasphere::get_all_tile_neighbors);
}

/// <summary>
/// Generates the hexagonal sphere. Creates the internal Hexasphere instance with the given parameters.
/// </summary>
bool NativeHexasphere::generate(double radius, int64_t divisions, double hexSize)
{
    if (!Hexasphere::are_valid_parameters(radius, divisions, hexSize))
        return false;
    // The core stores float coordinates. Reject values that cannot survive the
    // conversion instead of accepting zero/infinite coordinates from a Variant.
    const float core_radius = static_cast<float>(radius);
    if (!std::isfinite(core_radius) || core_radius <= 0.0f)
        return false;
    try
    {
        auto replacement = std::make_unique<Hexasphere>(core_radius, static_cast<int>(divisions), static_cast<float>(hexSize));
        clear_mesh_preparation();
        _hexasphere = std::move(replacement);
        return true;
    }
    catch (...)
    {
        return false;
    }
}

void NativeHexasphere::clear_mesh_preparation()
{
    _prepared_mesh_arrays = Array();
    _prepared_tile_vertex_counts = PackedInt32Array();
    _prepared_tile_vertex_indices = PackedInt32Array();
    _mesh_array_preparation_ms = 0.0;
    _prepared_mesh_chunks.clear();
    _next_mesh_chunk = 0;
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
    return _hexasphere->get_tiles()[tile_idx].get_center();
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
        result[i] = tiles[i].get_center();

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
    const auto &boundary = tiles[tile_idx].get_boundary_points();
    int count = tiles[tile_idx].get_boundary_count();

    PackedVector3Array result;
    result.resize(count);
    for (int i = 0; i < count; i++)
        result[i] = boundary[i];

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
    const auto &faceIndices = tile.get_face_indices();
    int face_count = tile.get_face_count();

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
        totalPoints += tiles[t].get_boundary_count();
        totalFaceIndices += tiles[t].get_face_count() * 3;
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
        const auto &boundary = tiles[t].get_boundary_points();
        int ptCount = tiles[t].get_boundary_count();
        pointCounts[t] = ptCount;

        for (int i = 0; i < ptCount; i++)
            points[ptOffset + i] = boundary[i];

        const auto &tileFaceIndices = tiles[t].get_face_indices();
        int faceCount = tiles[t].get_face_count();
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
        totalPositions += tiles[t].get_boundary_count() * 2;

    PackedVector3Array positions;
    positions.resize(totalPositions);
    PackedInt32Array tileLineCounts;
    tileLineCounts.resize(tileCount);

    int posOffset = 0;
    for (int t = 0; t < tileCount; t++)
    {
        const auto &boundary = tiles[t].get_boundary_points();
        int ptCount = tiles[t].get_boundary_count();
        tileLineCounts[t] = ptCount * 2;

        for (int p = 0; p < ptCount; p++)
        {
            int next = (p + 1) % ptCount;
            positions[posOffset + p * 2 + 0] = boundary[p];
            positions[posOffset + p * 2 + 1] = boundary[next];
        }

        posOffset += ptCount * 2;
    }

    result["positions"] = positions;
    result["tile_line_counts"] = tileLineCounts;

    return result;
}

static bool same_border_point(const Vector3 &a, const Vector3 &b, double tolerance)
{
    return std::abs(double(a.x) - b.x) <= tolerance
        && std::abs(double(a.y) - b.y) <= tolerance
        && std::abs(double(a.z) - b.z) <= tolerance;
}

static int find_shared_border(const Tile &tile, const Tile &neighbour, double tolerance)
{
    const auto &points = tile.get_boundary_points();
    const auto &other = neighbour.get_boundary_points();
    const int count = tile.get_boundary_count();
    const int other_count = neighbour.get_boundary_count();
    int edge = -1;
    for (int p = 0; p < count; ++p)
    {
        const Vector3 &next = points[(p + 1) % count];
        for (int q = 0; q < other_count; ++q)
        {
            if (!same_border_point(points[p], other[q], tolerance)) continue;
            const int forward = (q + 1) % other_count;
            const int backward = (q + other_count - 1) % other_count;
            if (!same_border_point(next, other[forward], tolerance)
                && !same_border_point(next, other[backward], tolerance)) continue;
            if (edge >= 0) return -1;
            edge = p;
        }
    }
    return edge;
}

Variant NativeHexasphere::prepare_border_surfaces(int vertices_per_surface) const
{
    if (!_hexasphere || vertices_per_surface <= 0) return Array();
    try
    {
        const auto &tiles = _hexasphere->get_tiles();
        const bool shared = _hexasphere->has_shared_tile_edges();
        int edge_count = 0;
        double scale = 0.0;
        for (const Tile &tile : tiles)
        {
            const int count = tile.get_boundary_count();
            edge_count += count;
            if (!shared) continue;
            if (count < 3 || count != tile.get_neighbour_count()) return Array();
            for (int p = 0; p < count; ++p)
            {
                const Vector3 &pos = tile.get_boundary_points()[p];
                scale = std::max({scale, std::abs(double(pos.x)), std::abs(double(pos.y)), std::abs(double(pos.z))});
            }
        }
        if (edge_count == 0 || (shared && (edge_count % 2 != 0 || scale == 0.0))) return Array();

        const int total_vertices = shared ? edge_count : edge_count * 2;
        const int min_size = (total_vertices - 1) / RenderingServer::MAX_MESH_SURFACES + 1;
        const int64_t requested = std::max(min_size, vertices_per_surface);
        const int64_t chunk_size = requested + requested % 2;
        const int surface_count = static_cast<int>((total_vertices - 1) / chunk_size + 1);
        Array surfaces;
        if (surfaces.resize(surface_count) != OK) return Variant();

        // Keep the original tile/edge order and use the first owner's endpoints.
        // If the topology does not identify every shared edge, use the compatibility path.
        const double tolerance = scale * 0.000001;
        int surface_index = 0;
        int remaining = total_vertices;
        int offset = 0;
        PackedVector3Array positions;
        PackedVector2Array owners;
        Vector3 *position_data = nullptr;
        Vector2 *owner_data = nullptr;
        for (int t = 0; t < static_cast<int>(tiles.size()); ++t)
        {
            const Tile &tile = tiles[t];
            const auto &points = tile.get_boundary_points();
            const int count = tile.get_boundary_count();
            std::array<int, 6> edge_owners;
            edge_owners.fill(-1);
            if (shared)
            {
                const auto *neighbours = tile.get_neighbours_data();
                for (int n = 0; n < count; ++n)
                {
                    const int other = neighbours[n];
                    const int edge = find_shared_border(tile, tiles[other], tolerance);
                    if (edge < 0 || edge_owners[edge] >= 0) return Array();
                    edge_owners[edge] = other;
                }
            }

            for (int p = 0; p < count; ++p)
            {
                const int other = edge_owners[p];
                if (shared && other < t) continue;
                if (remaining == 0) return Array();
                if (offset == 0)
                {
                    const int size = static_cast<int>(std::min<int64_t>(remaining, chunk_size));
                    if (positions.resize(size) != OK || owners.resize(size) != OK) return Variant();
                    position_data = positions.ptrw();
                    owner_data = owners.ptrw();
                }
                position_data[offset] = points[p] * 1.0001f;
                position_data[offset + 1] = points[(p + 1) % count] * 1.0001f;
                owner_data[offset] = Vector2(t, other);
                owner_data[offset + 1] = owner_data[offset];
                offset += 2;
                remaining -= 2;
                if (offset == positions.size())
                {
                    Array arrays;
                    if (arrays.resize(Mesh::ARRAY_MAX) != OK) return Variant();
                    arrays[Mesh::ARRAY_VERTEX] = positions;
                    arrays[Mesh::ARRAY_TEX_UV2] = owners;
                    surfaces[surface_index++] = arrays;
                    positions = PackedVector3Array();
                    owners = PackedVector2Array();
                    offset = 0;
                }
            }
        }
        return remaining == 0 ? surfaces : Array();
    }
    catch (...)
    {
        return Variant();
    }
}

static Vector3 mesh_normal(const Vector3 &pos)
{
    // Scale before normalizing so real_t length-squared stays in range.
    const real_t scale = std::max({std::abs(pos.x), std::abs(pos.y), std::abs(pos.z)});
    return (pos / scale).normalized();
}

/// <summary>
/// Builds and returns a complete ArrayMesh for the entire sphere, with vertex positions,
/// normals, and UV2 data (tile index in UV2.x). Also returns per-tile vertex counts and indices
/// for tile-specific processing on the C# side.
/// Dictionary keys: "mesh" (ArrayMesh), "tile_vertex_counts", "tile_vertex_indices".
/// </summary>
void NativeHexasphere::prepare_mesh()
{
    if (!_prepared_mesh_arrays.is_empty() || !_hexasphere || _hexasphere->get_tile_count() == 0)
        return;
    const auto start = std::chrono::steady_clock::now();
    clear_mesh_preparation();
    _prepared_mesh_arrays = make_mesh_arrays(0, _hexasphere->get_tile_count(),
        &_prepared_tile_vertex_counts, &_prepared_tile_vertex_indices);
    _mesh_array_preparation_ms = std::chrono::duration<double, std::milli>(
        std::chrono::steady_clock::now() - start).count();
}

Array NativeHexasphere::make_mesh_arrays(int first_tile, int tile_count,
    PackedInt32Array *vertex_counts, PackedInt32Array *vertex_indices) const
{
    const auto &tiles = _hexasphere->get_tiles();
    int totalVertices = 0;
    const int end_tile = first_tile + tile_count;
    for (int t = first_tile; t < end_tile; t++)
        totalVertices += tiles[t].get_face_count() * 3;

    PackedVector3Array vertices;
    vertices.resize(totalVertices);
    PackedVector3Array normals;
    normals.resize(totalVertices);
    PackedVector2Array uv2s;
    uv2s.resize(totalVertices);

    if (vertex_counts) vertex_counts->resize(tile_count);
    if (vertex_indices) vertex_indices->resize(totalVertices);

    int globalVertexIndex = 0;
    int indicesOffset = 0;

    for (int t = first_tile; t < end_tile; t++)
    {
        const auto &boundary = tiles[t].get_boundary_points();
        const auto &tileFaceIndices = tiles[t].get_face_indices();
        int faceCount = tiles[t].get_face_count();

        if (vertex_counts) (*vertex_counts)[t - first_tile] = faceCount * 3;
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
                Vector3 pos = boundary[order[v]];
                int vi = globalVertexIndex++;
                vertices[vi] = pos;
                normals[vi] = mesh_normal(pos);
                uv2s[vi] = tileUV;
                if (vertex_indices) (*vertex_indices)[base + v] = vi;
            }
        }

        indicesOffset += faceCount * 3;
    }

    Array arrays;
    arrays.resize(Mesh::ARRAY_MAX);
    arrays[Mesh::ARRAY_VERTEX] = vertices;
    arrays[Mesh::ARRAY_NORMAL] = normals;
    arrays[Mesh::ARRAY_TEX_UV2] = uv2s;
    return arrays;
}

Array NativeHexasphere::make_indexed_mesh_arrays(int first_tile, int tile_count) const
{
    const auto &tiles = _hexasphere->get_tiles();
    const int end_tile = first_tile + tile_count;
    int vertex_count = 0;
    int index_count = 0;
    for (int t = first_tile; t < end_tile; t++)
    {
        const Tile &tile = tiles[t];
        if (tile.get_face_count() == 0) continue;
        vertex_count += tile.get_boundary_count();
        index_count += tile.get_face_count() * 3;
    }

    PackedVector3Array vertices;
    PackedVector3Array normals;
    PackedVector2Array uv2s;
    PackedInt32Array indices;
    if (vertex_count == 0 || vertices.resize(vertex_count) != OK || normals.resize(vertex_count) != OK
        || uv2s.resize(vertex_count) != OK || indices.resize(index_count) != OK)
        return Array();

    Vector3 *vertex_data = vertices.ptrw();
    Vector3 *normal_data = normals.ptrw();
    Vector2 *uv_data = uv2s.ptrw();
    int32_t *index_data = indices.ptrw();
    int vertex_offset = 0;
    int index_offset = 0;
    for (int t = first_tile; t < end_tile; t++)
    {
        const Tile &tile = tiles[t];
        const int face_count = tile.get_face_count();
        if (face_count == 0) continue;
        const auto &boundary = tile.get_boundary_points();
        const auto *faces = tile.get_face_indices();

        // Share vertices only within a tile. Adjacent tiles carry distinct UV2 IDs.
        for (int v = 0; v < tile.get_boundary_count(); v++)
        {
            const int dst = vertex_offset + v;
            vertex_data[dst] = boundary[v];
            normal_data[dst] = mesh_normal(boundary[v]);
            uv_data[dst] = Vector2(t, 0.0f);
        }
        for (int f = 0; f < face_count; f++)
        {
            // Use the same clockwise winding as the expanded surface.
            index_data[index_offset++] = vertex_offset + faces[f][0];
            index_data[index_offset++] = vertex_offset + faces[f][2];
            index_data[index_offset++] = vertex_offset + faces[f][1];
        }
        vertex_offset += tile.get_boundary_count();
    }

    Array arrays;
    if (arrays.resize(Mesh::ARRAY_MAX) != OK)
        return Array();
    arrays[Mesh::ARRAY_VERTEX] = vertices;
    arrays[Mesh::ARRAY_NORMAL] = normals;
    arrays[Mesh::ARRAY_TEX_UV2] = uv2s;
    arrays[Mesh::ARRAY_INDEX] = indices;
    return arrays;
}

int NativeHexasphere::prepare_mesh_chunks(int tiles_per_chunk)
{
    return prepare_mesh_chunk_arrays(tiles_per_chunk, false);
}

int NativeHexasphere::prepare_indexed_mesh_chunks(int tiles_per_chunk)
{
    try
    {
        return prepare_mesh_chunk_arrays(tiles_per_chunk, true);
    }
    catch (...)
    {
        return -1;
    }
}

int NativeHexasphere::prepare_mesh_chunk_arrays(int tiles_per_chunk, bool indexed)
{
    clear_mesh_preparation();
    const int count = get_tile_count();
    if (count == 0 || tiles_per_chunk <= 0) return 0;
    // Large valid inputs must not exceed ArrayMesh's engine surface limit.
    tiles_per_chunk = std::max(tiles_per_chunk, (count - 1) / RenderingServer::MAX_MESH_SURFACES + 1);
    const auto start = std::chrono::steady_clock::now();
    std::vector<Array> chunks;
    for (int first = 0; first < count;)
    {
        const int size = std::min(tiles_per_chunk, count - first);
        Array arrays = indexed ? make_indexed_mesh_arrays(first, size) : make_mesh_arrays(first, size);
        if (arrays.is_empty()) return -1;
        chunks.push_back(std::move(arrays));
        first += size;
    }
    _prepared_mesh_chunks = std::move(chunks);
    _mesh_array_preparation_ms = std::chrono::duration<double, std::milli>(
        std::chrono::steady_clock::now() - start).count();
    return get_prepared_mesh_chunk_count();
}

int NativeHexasphere::get_prepared_mesh_chunk_count() const
{
    return static_cast<int>(_prepared_mesh_chunks.size()) - _next_mesh_chunk;
}

int NativeHexasphere::append_mesh_chunk(const Ref<ArrayMesh> &mesh)
{
    if (mesh.is_null() || get_prepared_mesh_chunk_count() <= 0
        || mesh->get_surface_count() >= RenderingServer::MAX_MESH_SURFACES) return -1;
    mesh->add_surface_from_arrays(Mesh::PRIMITIVE_TRIANGLES, _prepared_mesh_chunks[_next_mesh_chunk]);
    _prepared_mesh_chunks[_next_mesh_chunk++] = Array();
    const int remaining = get_prepared_mesh_chunk_count();
    if (remaining == 0) clear_mesh_preparation();
    return remaining;
}

Dictionary NativeHexasphere::build_mesh()
{
    Dictionary result;
    const bool was_prepared = !_prepared_mesh_arrays.is_empty();
    prepare_mesh();
    if (_prepared_mesh_arrays.is_empty())
        return result;

    const auto start = std::chrono::steady_clock::now();
    Ref<ArrayMesh> mesh;
    mesh.instantiate();
    mesh->add_surface_from_arrays(Mesh::PRIMITIVE_TRIANGLES, _prepared_mesh_arrays);
    const double surface_ms = std::chrono::duration<double, std::milli>(
        std::chrono::steady_clock::now() - start).count();

    result["mesh"] = mesh;
    result["tile_vertex_counts"] = _prepared_tile_vertex_counts;
    result["tile_vertex_indices"] = _prepared_tile_vertex_indices;
    result["array_preparation_milliseconds"] = _mesh_array_preparation_ms;
    result["surface_creation_milliseconds"] = surface_ms;
    result["used_prepared_arrays"] = was_prepared;
    // CPU staging arrays should not live for the lifetime of the ready sphere.
    clear_mesh_preparation();
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

    int totalNeighbors = 0;
    for (int t = 0; t < tileCount; t++)
        totalNeighbors += tiles[t].get_neighbour_count();

    PackedInt32Array neighborIndices;
    neighborIndices.resize(totalNeighbors);
    PackedInt32Array offsets;
    offsets.resize(tileCount + 1);

    int writePos = 0;
    for (int t = 0; t < tileCount; t++)
    {
        offsets[t] = writePos;
        const int32_t *neighbours = tiles[t].get_neighbours_data();
        int nCount = tiles[t].get_neighbour_count();
        for (int n = 0; n < nCount; n++)
            neighborIndices[writePos++] = neighbours[n];
    }
    offsets[tileCount] = writePos;

    result["neighbor_indices"] = neighborIndices;
    result["offsets"] = offsets;
    return result;
}
