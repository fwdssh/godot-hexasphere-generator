using Godot;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Renders the borders between adjacent hexagonal tiles using a line mesh.
/// Shares the tile color texture with the planet material for consistent coloring.
/// </summary>
public class PlanetBorderRenderer : System.IDisposable
{
    private MeshInstance3D _bordersMeshInstance;
    private ShaderMaterial _borderMaterial;
    private Color _borderColor;
    private ArrayMesh _mesh;
    private PreparedGeometry _preparedGeometry;
    private PreparedSurface[] _uploadSurfaces;
    private int _nextSurface;
    private bool _visible = true;

    // CPU arrays and their source identity; no rendering resources or scene
    // nodes are created until BuildStaticBorders runs on the main thread.
    internal sealed record PreparedSurface(Vector3[] Positions, Vector2[] TilePairs);
    internal sealed record PreparedGeometry(PreparedSurface[] Surfaces,
        NativeHexasphere Source, long SourceVersion);
    private readonly record struct EdgeCandidate(Vector3 Start, Vector3 End, int VertexIndex, int Next);

    internal bool IsUploading => _uploadSurfaces != null;
    internal bool HasSingleUploadSurface => _uploadSurfaces?.Length == 1;

    /// <summary>
    /// Creates a new border renderer and adds the border mesh as a child of the specified parent node.
    /// </summary>
    /// <param name="parent">The parent node to attach the border mesh to.</param>
    public PlanetBorderRenderer(Node3D parent)
    {
        _bordersMeshInstance      = new MeshInstance3D();
        _bordersMeshInstance.Name = "BordersMesh";
        parent.AddChild(_bordersMeshInstance);
    }

    internal PlanetBorderRenderer(Node3D parent, PreparedGeometry geometry) : this(parent)
    {
        _preparedGeometry = geometry;
    }

    /// <summary>Shows or hides the border mesh.</summary>
    /// <param name="visible">True to show borders, false to hide.</param>
    public virtual void SetVisible(bool visible)
    {
        _visible = visible;
        _bordersMeshInstance.Visible = visible && !IsUploading;
    }

    /// <summary>
    /// Builds the static border line mesh by deduplicating shared edges between adjacent tiles.
    /// </summary>
    /// <param name="hexasphere">The native hexasphere providing border data.</param>
    /// <param name="planetMaterial">The planet material to share texture parameters from.</param>
    /// <param name="borderShader">The shader to use for border rendering.</param>
    public virtual void BuildStaticBorders(NativeHexasphere hexasphere, ShaderMaterial planetMaterial, Shader borderShader)
    {
        BeginUpload(hexasphere, planetMaterial, borderShader);
        AdvanceUpload(0);
    }

    internal void BeginUpload(NativeHexasphere hexasphere, ShaderMaterial planetMaterial, Shader borderShader)
    {
        var geometry = _preparedGeometry;
        _preparedGeometry = null;
        // A custom finalization hook may replace or regenerate the native instance.
        if (geometry != null && (!ReferenceEquals(geometry.Source, hexasphere)
            || geometry.SourceVersion != hexasphere.GeometryVersion)) geometry = null;
        geometry ??= PrepareGeometry(hexasphere, CancellationToken.None);

        var mesh = new ArrayMesh();
        try
        {
            var shader = borderShader;
            _borderMaterial?.Dispose();
            _borderMaterial = new ShaderMaterial();
            _borderMaterial.Shader = shader;

            _borderMaterial.SetShaderParameter("tile_colors",
                planetMaterial.GetShaderParameter("tile_colors"));
            _borderMaterial.SetShaderParameter("tile_count",
                planetMaterial.GetShaderParameter("tile_count"));
            _borderMaterial.SetShaderParameter("tex_width",
                planetMaterial.GetShaderParameter("tex_width"));
            _borderMaterial.SetShaderParameter("selected_idx", -1);
            _borderMaterial.SetShaderParameter("border_color", _borderColor);

            _bordersMeshInstance.Mesh = mesh;
            _mesh?.Dispose();
            _mesh = mesh;
            _bordersMeshInstance.MaterialOverride = _borderMaterial;
            _bordersMeshInstance.Visible = false;
            _uploadSurfaces = geometry.Surfaces;
            _nextSurface = 0;
        }
        catch { mesh.Dispose(); throw; }
    }

    internal void AdvanceUpload(double budgetMilliseconds)
    {
        if (!IsUploading) return;
        var frame = System.Diagnostics.Stopwatch.StartNew();
        do
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var surface = _uploadSurfaces[_nextSurface];
            using var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = surface.Positions;
            arrays[(int)Mesh.ArrayType.TexUV2] = surface.TilePairs;
            var mesh = _mesh;
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
            _uploadSurfaces[_nextSurface++] = null;
            if (_nextSurface == _uploadSurfaces.Length)
            {
                _uploadSurfaces = null;
                _bordersMeshInstance.Visible = _visible;
                return;
            }
            if (budgetMilliseconds > 0 && frame.Elapsed.TotalMilliseconds + clock.Elapsed.TotalMilliseconds >= budgetMilliseconds) return;
        } while (true);
    }

    internal static PreparedGeometry PrepareGeometry(NativeHexasphere hexasphere, CancellationToken token, int verticesPerSurface = int.MaxValue)
    {
        token.ThrowIfCancellationRequested();
        long sourceVersion = hexasphere.GeometryVersion;
        using var data = hexasphere.GetBorderData();
        token.ThrowIfCancellationRequested();
        var positions = (Vector3[])data["positions"];
        var tileLineCounts = (int[])data["tile_line_counts"];

        int tileCount = hexasphere.GetTileCount();
        var vertPositions = new List<Vector3>();
        List<Vector2> uv2 = new List<Vector2>();
        var edgeBuckets = new Dictionary<(long, long, long), int>();
        var edges = new List<EdgeCandidate>();
        // Normalize in double precision so tiny/large radii use the same buckets
        // without underflowing squared lengths or overflowing endpoint sums.
        double scale = 0;
        for (int i = 0; i < positions.Length; i++)
        {
            if ((i & 8191) == 0) token.ThrowIfCancellationRequested();
            var p = positions[i];
            scale = System.Math.Max(scale, System.Math.Max(System.Math.Abs((double)p.X),
                System.Math.Max(System.Math.Abs((double)p.Y), System.Math.Abs((double)p.Z))));
        }
        double inverseScale = scale > 0 ? 1 / scale : 1;
        bool shareEdges = hexasphere.HasSharedTileEdges;

        int idx = 0;
        for (int i = 0; i < tileCount; i++)
        {
            if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
            int count = tileLineCounts[i];
            for (int j = 0; j < count; j += 2)
            {
                Vector3 p1 = positions[idx + j];
                Vector3 p2 = positions[idx + j + 1];
                double midpointX = ((double)p1.X + p2.X) * 0.5 * inverseScale / 0.00001;
                double midpointY = ((double)p1.Y + p2.Y) * 0.5 * inverseScale / 0.00001;
                double midpointZ = ((double)p1.Z + p2.Z) * 0.5 * inverseScale / 0.00001;
                var bucket = ((long)System.Math.Floor(midpointX),
                    (long)System.Math.Floor(midpointY), (long)System.Math.Floor(midpointZ));
                int vertexIndex = -1;
                // A midpoint only locates candidates. Verify both endpoints in
                // either direction, including neighboring buckets at grid seams.
                // Gapped tiles own independent lines even for sub-tolerance gaps.
                // Matching endpoints bound midpoint drift to 0.1 bucket widths.
                // Visit a neighbor only near that seam, avoiding 27 lookups per edge.
                const double bucketDrift = 0.100001;
                long minX = (long)System.Math.Floor(midpointX - bucketDrift);
                long maxX = (long)System.Math.Floor(midpointX + bucketDrift);
                long minY = (long)System.Math.Floor(midpointY - bucketDrift);
                long maxY = (long)System.Math.Floor(midpointY + bucketDrift);
                long minZ = (long)System.Math.Floor(midpointZ - bucketDrift);
                long maxZ = (long)System.Math.Floor(midpointZ + bucketDrift);
                for (long x = minX; shareEdges && x <= maxX && vertexIndex < 0; x++)
                for (long y = minY; y <= maxY && vertexIndex < 0; y++)
                for (long z = minZ; z <= maxZ && vertexIndex < 0; z++)
                {
                    if (!edgeBuckets.TryGetValue((x, y, z), out int candidate)) continue;
                    while (candidate >= 0)
                    {
                        var edge = edges[candidate];
                        if ((SameEndpoint(p1, edge.Start, inverseScale) && SameEndpoint(p2, edge.End, inverseScale))
                            || (SameEndpoint(p1, edge.End, inverseScale) && SameEndpoint(p2, edge.Start, inverseScale)))
                        {
                            vertexIndex = edge.VertexIndex;
                            break;
                        }
                        candidate = edge.Next;
                    }
                }
                if (vertexIndex < 0)
                {
                    if (shareEdges)
                    {
                        int next = edgeBuckets.TryGetValue(bucket, out int head) ? head : -1;
                        edgeBuckets[bucket] = edges.Count;
                        edges.Add(new EdgeCandidate(p1, p2, vertPositions.Count, next));
                    }
                    vertPositions.Add(p1 * 1.0001f);
                    vertPositions.Add(p2 * 1.0001f);
                    uv2.Add(new Vector2(i, -1));
                    uv2.Add(new Vector2(i, -1));
                }
                else
                {
                    var owners = new Vector2(uv2[vertexIndex].X, i);
                    uv2[vertexIndex] = owners;
                    uv2[vertexIndex + 1] = owners;
                }
            }
            idx += count;
        }

        token.ThrowIfCancellationRequested();
        // Keep complete line pairs and leave enough vertices per surface for the
        // engine's 256-surface limit, including independent edges on gapped tiles.
        int minimum = (int)(((long)vertPositions.Count + 255) / 256);
        int chunkSize = System.Math.Max(minimum, verticesPerSurface);
        chunkSize = (int)System.Math.Min((long)chunkSize + (chunkSize & 1), int.MaxValue - 1L);
        var surfaces = new PreparedSurface[(int)(((long)vertPositions.Count + chunkSize - 1) / chunkSize)];
        for (int i = 0, offset = 0; i < surfaces.Length; i++, offset += chunkSize)
        {
            token.ThrowIfCancellationRequested();
            int length = System.Math.Min(chunkSize, vertPositions.Count - offset);
            var surfacePositions = new Vector3[length];
            var surfaceOwners = new Vector2[length];
            vertPositions.CopyTo(offset, surfacePositions, 0, length);
            uv2.CopyTo(offset, surfaceOwners, 0, length);
            surfaces[i] = new PreparedSurface(surfacePositions, surfaceOwners);
        }
        return new PreparedGeometry(surfaces, hexasphere, sourceVersion);
    }

    /// <summary>
    /// Updates the border shader with the currently selected tile index for highlighting.
    /// </summary>
    /// <param name="selectedIdx">The selected tile index, or -1 for none.</param>
    public virtual void UpdateBorders(int selectedIdx = -1)
    {
        _borderMaterial?.SetShaderParameter("selected_idx", selectedIdx);
    }

    /// <summary>Sets the color used for rendering tile borders.</summary>
    /// <param name="color">The new border color.</param>
    public virtual void SetBorderColor(Color color)
    {
        _borderColor = color;
        _borderMaterial?.SetShaderParameter("border_color", color);
    }

    /// <summary>Releases the border node, mesh and material owned by this renderer.</summary>
    public void Dispose()
    {
        _preparedGeometry = null;
        _uploadSurfaces = null;
        if (GodotObject.IsInstanceValid(_bordersMeshInstance))
        {
            _bordersMeshInstance.Mesh = null;
            _bordersMeshInstance.MaterialOverride = null;
            _bordersMeshInstance.Free();
        }
        _bordersMeshInstance = null;
        _borderMaterial?.Dispose(); _borderMaterial = null;
        _mesh?.Dispose(); _mesh = null;
    }

    private static bool SameEndpoint(Vector3 a, Vector3 b, double inverseScale) =>
        System.Math.Abs((double)a.X - b.X) * inverseScale <= 0.000001
        && System.Math.Abs((double)a.Y - b.Y) * inverseScale <= 0.000001
        && System.Math.Abs((double)a.Z - b.Z) * inverseScale <= 0.000001;
}
