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
        var edgeFirstOwner = new Dictionary<Vector3, int>();
        var edgeVertexIndex = new Dictionary<Vector3, int>();

        int idx = 0;
        for (int i = 0; i < tileCount; i++)
        {
            if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
            int count = tileLineCounts[i];
            for (int j = 0; j < count; j += 2)
            {
                Vector3 p1 = positions[idx + j];
                Vector3 p2 = positions[idx + j + 1];
                Vector3 mid = (p1 + p2) * 0.5f;
                var snappedMid = SnapToGrid(mid, 0.001f);
                if (!edgeFirstOwner.TryGetValue(snappedMid, out int firstOwner))
                {
                    edgeFirstOwner[snappedMid] = i;
                    edgeVertexIndex[snappedMid] = vertPositions.Count;
                    vertPositions.Add(p1 * 1.0001f);
                    vertPositions.Add(p2 * 1.0001f);
                    uv2.Add(new Vector2(i, -1));
                    uv2.Add(new Vector2(i, -1));
                }
                else
                {
                    int vertIdx = edgeVertexIndex[snappedMid];
                    uv2[vertIdx] = new Vector2(firstOwner, i);
                    uv2[vertIdx + 1] = new Vector2(firstOwner, i);
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

    private static Vector3 SnapToGrid(Vector3 v, float gridSize)
    {
        return new Vector3(
            Mathf.Round(v.X / gridSize) * gridSize,
            Mathf.Round(v.Y / gridSize) * gridSize,
            Mathf.Round(v.Z / gridSize) * gridSize
        );
    }
}
