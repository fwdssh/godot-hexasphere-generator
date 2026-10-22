using Godot;
using System.Collections.Generic;
using Godot.Hexasphere;

/// <summary>
/// Renders a 2D equirectangular UV map projection of the hexasphere, allowing tile selection
/// and interaction in UV space. Managed by HexasphereNode.
/// </summary>
public partial class HexasphereProjectorController : Node2D
{
    /// <summary>
    /// Emitted when a tile is clicked on the UV map. Provides the tile index.
    /// </summary>
    [Signal] public delegate void TileClickedEventHandler(int tileIndex);
    /// <summary>
    /// Emitted when the currently selected tile on the UV map is deselected.
    /// </summary>
    [Signal] public delegate void TileDeselectedEventHandler();
    /// <summary>
    /// Emitted when the hovered tile changes on the UV map. Provides the tile index (-1 if none).
    /// </summary>
    [Signal] public delegate void TileHoveredEventHandler(int tileIndex);
    /// <summary>
    /// Emitted when the UV projection view is closed.
    /// </summary>
    [Signal] public delegate void ProjectionClosedEventHandler();

    /// <summary>Emitted after the projection is ready, including a cached build request.</summary>
    [Signal] public delegate void ProjectionReadyEventHandler();
    /// <summary>Emitted when a projection build cannot be completed.</summary>
    [Signal] public delegate void ProjectionBuildFailedEventHandler(string error);
    /// <summary>Emitted after a successful build changes the active rendering mode.</summary>
    [Signal] public delegate void ProjectionModeChangedEventHandler(int activeMode);

    [ExportGroup("Projection")]
    /// <summary>Displayed size in local 2D units, independent of raster resolution.</summary>
    [Export] public Vector2 MapSize = new Vector2(1920, 1080);
    /// <summary>Auto selects raster when tile count exceeds MaxDetailedTileCount.</summary>
    [Export] public UvProjectionMode ProjectionMode { get; set; } = UvProjectionMode.Auto;
    [Export(PropertyHint.Range, "0,2000000,1,or_greater")]
    public int MaxDetailedTileCount { get; set; } = 200_000;

    [ExportGroup("Raster")]
    /// <summary>Low: 1024x512; Medium: 2048x1024; High: 4096x2048.</summary>
    [Export] public UvRasterQuality RasterQuality { get; set; } = UvRasterQuality.Medium;
    /// <summary>Pixel dimensions used when RasterQuality is Custom.</summary>
    [Export] public Vector2I RasterResolution { get; set; } = new Vector2I(2048, 1024);

    public UvProjectionMode ActiveProjectionMode { get; private set; } = UvProjectionMode.Auto;
    public UvProjectionBuildState BuildState { get; private set; } = UvProjectionBuildState.NotBuilt;
    public Vector2I EffectiveRasterResolution { get; private set; }
    public string LastBuildError { get; private set; } = "";

    [ExportGroup("Interaction")]
    /// <summary>If true, tiles can be clicked to select them.</summary>
    [Export] public bool IsClickEnabled = true;
    /// <summary>If true, tiles emit hover events when the mouse moves over them.</summary>
    [Export] public bool IsHoverEnabled = true;

    [ExportGroup("Visual")]
    /// <summary>If true, the selected tile is highlighted with ClickColor.</summary>
    [Export] public bool IsClickVisualEnabled = true;
    /// <summary>Color used to highlight the selected tile.</summary>
    [Export] public Color ClickColor = Colors.Yellow;
    /// <summary>If true, the hovered tile is highlighted with HoverColor.</summary>
    [Export] public bool IsHoverVisualEnabled = true;
    /// <summary>Color used to highlight the hovered tile.</summary>
    [Export] public Color HoverColor = Colors.Red;
    
    /// <summary>The MeshInstance2D that displays the main UV map.</summary>
    public MeshInstance2D MeshInstance2D;

    private NativeHexasphere _hexasphere;
    private Vector2 _lastMapSize;
    private int _selectedTile = -1;
    private int _hoveredTile = -1;
    private UvCamera2D _camera2D;
    private bool _meshDirty = true;
    private NativeHexasphere _cachedHexasphere;
    private Vector2 _cachedMapSize;
    private Vector3[] _cachedVertices;
    private int[] _cachedTriToTile;
    private List<HitTri> _cachedHitTris;
    private NativeHexasphere _cachedGeomHexasphere;
    private Vector2 _cachedGeomMapSize;
    private bool _hasCachedGeometry;
    private int _cachedTileCount;
    private Color[] _lastColors;
    private bool _hasBuiltMap;
    private ShaderMaterial _mapMaterial;

    private int _texWidth;
    private int _texHeight;
    private Image _tileColorImage;
    private ImageTexture _tileColorTexture;
    private byte[] _colorBuffer;
    private static Shader _uvTileColorsShader;


    


    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Disabled;
        
        EnsureChildNodes();

    MeshInstance2D = GetNodeOrNull<MeshInstance2D>("MeshInstance2D");
    
    _camera2D = GetNodeOrNull<UvCamera2D>("Camera2D");
        
    }



    private void EnsureChildNodes()
{
    if (GetNodeOrNull<MeshInstance2D>("MeshInstance2D") == null)
    {
        var mesh = new MeshInstance2D { Name = "MeshInstance2D" };
        AddChild(mesh);
    }
    if (GetNodeOrNull<UvCamera2D>("Camera2D") == null)
    {
        var cam = new UvCamera2D { Name = "Camera2D" };
        AddChild(cam);
    }
}
    
    public override void _Notification(int what)
    {
        if (what == NotificationVisibilityChanged && Visible)
        {
            var camera = _camera2D;
            if (camera != null)
            {
                camera.MakeCurrent();

            }
        }
    }

    private struct HitTri
    {
        public int TileIndex;
        public Vector2 A, B, C;
    }
    private List<HitTri> _hitTris = new List<HitTri>();
    private Dictionary<(int, int), List<int>> _spatialGrid = new();
    private float _cellSize = 64f;

    /// <summary>
    /// Builds the projection or reuses its cache. Call MarkDirty after regenerating
    /// topology in the same NativeHexasphere instance. Failures emit ProjectionBuildFailed.
    /// </summary>
    /// <param name="hexasphere">The native hexasphere with generated geometry data.</param>
    /// <param name="colors">Array of tile colors indexed by tile index.</param>
    /// <param name="mapSize">Displayed size of the UV map in local 2D units.</param>
    public virtual void BuildMap2D(NativeHexasphere hexasphere, Color[] colors, Vector2 mapSize)
    {
        BuildState = UvProjectionBuildState.Building;
        LastBuildError = "";
        try
        {
            BuildProjection(hexasphere, colors, mapSize);
        }
        catch (System.Exception error)
        {
            // A failed build may already have replaced/cleared part of a cache.
            // Never advertise those partial resources as a reusable successful map.
            MarkDirty();
            BuildState = UvProjectionBuildState.Failed;
            LastBuildError = error.Message;
            GD.PrintErr($"[HexasphereProjectorController] {LastBuildError}");
            EmitSignal(SignalName.ProjectionBuildFailed, LastBuildError);
        }
    }

    private void BuildProjection(NativeHexasphere hexasphere, Color[] colors, Vector2 mapSize)
    {
        if (hexasphere == null || !GodotObject.IsInstanceValid(MeshInstance2D))
            throw new System.InvalidOperationException("Add the projector to the scene tree before building a generated hexasphere.");
        if (!mapSize.IsFinite() || mapSize.X <= 0 || mapSize.Y <= 0)
            throw new System.ArgumentOutOfRangeException(nameof(mapSize), "MapSize must be finite and positive.");
        int tileCount = hexasphere.GetTileCount();
        if (tileCount <= 0)
            throw new System.InvalidOperationException("Generate the hexasphere before building a projection.");
        ValidateColors(colors, tileCount);
        if (!System.Enum.IsDefined(ProjectionMode) || MaxDetailedTileCount < 0)
            throw new System.ArgumentOutOfRangeException(nameof(ProjectionMode), "Use a valid projection mode and a nonnegative tile threshold.");

        var mode = ProjectionMode == UvProjectionMode.Auto
            ? (tileCount > MaxDetailedTileCount ? UvProjectionMode.Raster : UvProjectionMode.DetailedMesh)
            : ProjectionMode;
        var resolution = mode == UvProjectionMode.Raster ? ResolveRasterResolution() : Vector2I.Zero;
        bool topologyChanged = _cachedHexasphere != hexasphere || _cachedTileCount != tileCount;
        bool geometryChanged = _meshDirty || topologyChanged || !_hasBuiltMap
            || ActiveProjectionMode != mode || _cachedMapSize != mapSize
            || EffectiveRasterResolution != resolution;
        bool firstMap = !_hasBuiltMap;
        var previousMode = ActiveProjectionMode;
        var currentColors = colors ?? (topologyChanged ? null : _lastColors);
        if (_meshDirty || topologyChanged)
        {
            if (_hasCachedGeometry && _cachedGeomHexasphere == hexasphere && _cachedGeomMapSize == mapSize)
                ClearRasterCache();
            else
                ClearGeometryCaches();
        }

        _hexasphere = hexasphere;
        _lastMapSize = mapSize;
        if (geometryChanged)
        {
            if (mode == UvProjectionMode.Raster)
                BuildRasterMap(hexasphere, currentColors, mapSize, resolution);
            else
            {
                _rasterMode = false;
                ClearRasterCache();
                if (_hasCachedGeometry && _cachedGeomHexasphere == hexasphere && _cachedGeomMapSize == mapSize)
                    BuildMeshFromCache(currentColors, mapSize);
                else
                    BuildMesh(currentColors, mapSize);
                BuildSpatialGrid();
            }
        }
        else if (colors != null)
            WriteColorsToImage(colors);

        _lastColors = currentColors;
        _cachedHexasphere = hexasphere;
        _cachedMapSize = mapSize;
        _cachedTileCount = tileCount;
        _meshDirty = false;
        _hasBuiltMap = true;
        MapSize = mapSize;
        ActiveProjectionMode = mode;
        EffectiveRasterResolution = resolution;
        if (topologyChanged) { _selectedTile = -1; _hoveredTile = -1; }
        UpdateShaderSelection();

        var camera = _camera2D;
        if (camera != null)
        {
            camera.SetPanLimits(mapSize);
            if (firstMap || topologyChanged)
            {
                camera.Position = mapSize / 2f;
                camera.TargetZoom = 0.5f;
            }
        }
        BuildState = UvProjectionBuildState.Ready;
        if (previousMode != mode)
            EmitSignal(SignalName.ProjectionModeChanged, (int)mode);
        EmitSignal(SignalName.ProjectionReady);
    }

    /// <summary>Builds with the current Inspector settings, reusing unchanged geometry.</summary>
    public void PrepareMap(NativeHexasphere hexasphere, Color[] colors) => BuildMap2D(hexasphere, colors, MapSize);

    /// <summary>Applies current settings to the previously prepared map. Unchanged IDs are reused.</summary>
    public void RebuildMap() => BuildMap2D(_hexasphere, null, MapSize);

    public bool IsProjectionReady() => BuildState == UvProjectionBuildState.Ready;
    public int GetActiveProjectionMode() => (int)ActiveProjectionMode;
    public int GetBuildState() => (int)BuildState;
    public Vector2I GetEffectiveRasterResolution() => EffectiveRasterResolution;
    public string GetLastBuildError() => LastBuildError;

    /// <summary>Returns a tile at normalized map coordinates, or -1 outside the map/not ready.</summary>
    public int GetTileAtUv(Vector2 uv)
    {
        if (!IsProjectionReady() || !uv.IsFinite() || uv.X < 0 || uv.X > 1 || uv.Y < 0 || uv.Y > 1)
            return -1;
        return HitTest(uv * _lastMapSize);
    }

    /// <summary>Programmatic selection; does not emit mouse-click signals.</summary>
    public bool SelectTile(int tileIndex)
    {
        if (!IsProjectionReady() || tileIndex < -1 || tileIndex >= _cachedTileCount) return false;
        SetSelection(tileIndex, _hoveredTile);
        return true;
    }

    public void ClearSelection() => SetSelection(-1, -1);
    public int GetSelectedTileIndex() => _selectedTile;
    public int GetHoveredTileIndex() => _hoveredTile;

    private static void ValidateColors(Color[] colors, int tileCount)
    {
        if (colors != null && colors.Length < tileCount)
            throw new System.ArgumentException($"colors.Length ({colors.Length}) < tileCount ({tileCount})");
    }

    private void ReplaceMapMesh(ArrayMesh mesh)
    {
        var previous = MeshInstance2D.Mesh;
        MeshInstance2D.Mesh = mesh;
        previous?.Dispose();
    }

    private void ClearGeometryCaches()
    {
        _hasCachedGeometry = false;
        _cachedVertices = null;
        _cachedTriToTile = null;
        _cachedHitTris = null;
        _cachedGeomHexasphere = null;
        ClearRasterCache();
    }

    private void BuildMesh(Color[] colors, Vector2 mapSize)
    {
        var geom = ComputeUvGeometry(_hexasphere, mapSize);
        int tileCount = _hexasphere.GetTileCount();
        int tw = Mathf.CeilToInt(Mathf.Sqrt(tileCount));
        int th = Mathf.CeilToInt((float)tileCount / tw);

        using var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        int vi = 0;
        for (int ti = 0; ti < geom.TriToTile.Length; ti++)
        {
            int idx = geom.TriToTile[ti];
            var uv = new Vector2((idx % tw + 0.5f) / tw, (idx / tw + 0.5f) / th);
            st.SetUV(uv);
            st.SetColor(Colors.White);
            st.AddVertex(geom.Vertices[vi++]);
            st.SetUV(uv);
            st.SetColor(Colors.White);
            st.AddVertex(geom.Vertices[vi++]);
            st.SetUV(uv);
            st.SetColor(Colors.White);
            st.AddVertex(geom.Vertices[vi++]);
        }

        ReplaceMapMesh(st.Commit());
        _hitTris = geom.HitTris;

        SetupTextureMaterial(colors);
    }

    private struct UvGeometryData
    {
        public Vector3[] Vertices;
        public int[] TriToTile;
        public List<HitTri> HitTris;
    }

    private UvGeometryData ComputeUvGeometry(NativeHexasphere hexasphere, Vector2 mapSize)
    {
        int tileCount = hexasphere.GetTileCount();

        using var buildData = hexasphere.GetBuildData();
        var allPoints = (Vector3[])buildData["points"];
        var pointCounts = (int[])buildData["point_counts"];
        var allCenters = hexasphere.GetAllTileCenters();

        var verts = new List<Vector3>();
        var triToTile = new List<int>();
        var hitTris = new List<HitTri>();

        void AppendWrappedPolygon(List<Vector2> polygon, int tile)
        {
            float minU = polygon[0].X, maxU = minU;
            foreach (var point in polygon)
            {
                minU = Mathf.Min(minU, point.X);
                maxU = Mathf.Max(maxU, point.X);
            }
            // Keep both sides of a cell crossing the longitude seam.
            int firstShift = maxU > 1f ? -1 : 0;
            int lastShift = minU < 0f ? 1 : 0;
            for (int shift = firstShift; shift <= lastShift; shift++)
            {
                var shifted = polygon;
                if (shift != 0)
                {
                    shifted = new List<Vector2>(polygon.Count);
                    foreach (var point in polygon)
                        shifted.Add(point + new Vector2(shift, 0));
                }
                var clipped = ClipPolygonToRect(shifted);
                for (int j = 1; j < clipped.Count - 1; j++)
                {
                    var p0 = UvToScreen(clipped[0], mapSize);
                    var p1 = UvToScreen(clipped[j], mapSize);
                    var p2 = UvToScreen(clipped[j + 1], mapSize);
                    verts.Add(new Vector3(p0.X, p0.Y, 0));
                    verts.Add(new Vector3(p1.X, p1.Y, 0));
                    verts.Add(new Vector3(p2.X, p2.Y, 0));
                    triToTile.Add(tile);
                    hitTris.Add(new HitTri { TileIndex = tile, A = p0, B = p1, C = p2 });
                }
            }
        }

        int ptOffset = 0;

        for (int t = 0; t < tileCount; t++)
        {
            int n = pointCounts[t];

            if (n < 3) { ptOffset += n; continue; }

            Vector3 centerPos = allCenters[t];

            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 uvA = HexasphereUvProjector.CalculateUv(allPoints[ptOffset + i]);
                Vector2 uvB = HexasphereUvProjector.CalculateUv(allPoints[ptOffset + (i + 1) % n]);
                float d = uvB.X - uvA.X;
                d -= Mathf.Round(d);
                total += d;
            }
            bool isPoleCap = Mathf.Abs(Mathf.Abs(total) - 1f) < 0.1f;

            if (isPoleCap)
            {
                float poleV = centerPos.Y > 0 ? 0f : 1f;

                var ringUv = new Vector2[n];
                for (int i = 0; i < n; i++)
                    ringUv[i] = HexasphereUvProjector.CalculateUv(allPoints[ptOffset + i]);

                int[] order = new int[n];
                for (int i = 0; i < n; i++) order[i] = i;
                System.Array.Sort(order, (a, b) => ringUv[a].X.CompareTo(ringUv[b].X));

                for (int k = 0; k < n; k++)
                {
                    int i0 = order[k];
                    int i1 = order[(k + 1) % n];

                    float u0 = ringUv[i0].X, v0 = ringUv[i0].Y;
                    float u1 = ringUv[i1].X, v1 = ringUv[i1].Y;
                    if (k == n - 1) u1 += 1f;

                    var quad = new List<Vector2>
                    {
                        new Vector2(u0, v0),
                        new Vector2(u1, v1),
                        new Vector2(u1, poleV),
                        new Vector2(u0, poleV)
                    };

                    AppendWrappedPolygon(quad, t);
                }
            }
            else
            {
                Vector2 centerUv = HexasphereUvProjector.CalculateUv(centerPos);
                var uvs = new Vector2[n + 1];
                uvs[0] = centerUv;

                for (int i = 0; i < n; i++)
                    uvs[i + 1] = HexasphereUvProjector.CalculateUv(allPoints[ptOffset + i]);

                float refU = uvs[0].X;
                for (int i = 0; i < uvs.Length; i++)
                    uvs[i].X += HexasphereUvProjector.GetSeamOffset(uvs[i].X, refU);

                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;
                    AppendWrappedPolygon(new List<Vector2> { uvs[0], uvs[i + 1], uvs[next + 1] }, t);
                }
            }

            ptOffset += n;
        }

        return new UvGeometryData
        {
            Vertices = verts.ToArray(),
            TriToTile = triToTile.ToArray(),
            HitTris = hitTris
        };
    }

    private void BuildMeshFromCache(Color[] colors, Vector2 mapSize)
    {
        int tileCount = _hexasphere.GetTileCount();
        int tw = Mathf.CeilToInt(Mathf.Sqrt(tileCount));
        int th = Mathf.CeilToInt((float)tileCount / tw);

        using var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        int vi = 0;
        for (int ti = 0; ti < _cachedTriToTile.Length; ti++)
        {
            int idx = _cachedTriToTile[ti];
            var uv = new Vector2((idx % tw + 0.5f) / tw, (idx / tw + 0.5f) / th);
            st.SetUV(uv);
            st.SetColor(Colors.White);
            st.AddVertex(_cachedVertices[vi++]);
            st.SetUV(uv);
            st.SetColor(Colors.White);
            st.AddVertex(_cachedVertices[vi++]);
            st.SetUV(uv);
            st.SetColor(Colors.White);
            st.AddVertex(_cachedVertices[vi++]);
        }
        ReplaceMapMesh(st.Commit());
        _hitTris = new List<HitTri>(_cachedHitTris);

        SetupTextureMaterial(colors);
    }

    private void SetupTextureMaterial(Color[] colors)
    {
        ReleaseColorMaterial();
        int tileCount = _hexasphere?.GetTileCount() ?? colors?.Length ?? 0;
        _texWidth = Mathf.CeilToInt(Mathf.Sqrt(tileCount));
        _texHeight = Mathf.CeilToInt((float)tileCount / _texWidth);
        if (_rasterMode)
        {
            _texWidth = _rasterResolution.X;
            _texHeight = _rasterResolution.Y;
        }
        if (_colorBuffer != null && _colorBuffer.Length != _texWidth * _texHeight * 4)
            _colorBuffer = null;

        _tileColorImage = Image.CreateEmpty(_texWidth, _texHeight, false, Image.Format.Rgba8);
        _tileColorTexture = ImageTexture.CreateFromImage(_tileColorImage);

        if (_uvTileColorsShader == null)
            _uvTileColorsShader = GD.Load<Shader>("res://addons/hexasphere_generator/scripts/hexasphere_node/shaders/uv_tile_colors.gdshader");

        var material = new ShaderMaterial();
        _mapMaterial = material;
        material.Shader = _uvTileColorsShader;
        material.SetShaderParameter("tile_colors", _tileColorTexture);
        material.SetShaderParameter("tile_count", tileCount);
        material.SetShaderParameter("tex_width", _texWidth);
        material.SetShaderParameter("raster_mode", _rasterMode);
        if (_rasterMode)
            material.SetShaderParameter("raster_ids", _rasterIdTexture);
        material.SetShaderParameter("hover_idx", -1);
        material.SetShaderParameter("selected_idx", -1);
        material.SetShaderParameter("selected_color", new Vector4(ClickColor.R, ClickColor.G, ClickColor.B, ClickColor.A));
        material.SetShaderParameter("hover_color", new Vector4(HoverColor.R, HoverColor.G, HoverColor.B, HoverColor.A));
        MeshInstance2D.Material = material;

        if (colors != null)
            WriteColorsToImage(colors);
    }

    private void WriteColorsToImage(Color[] colors)
    {
        if (_tileColorImage == null || _texWidth <= 0 || _texHeight <= 0) return;
        if (_rasterMode)
        {
            WriteRasterColorsToImage(colors);
            return;
        }

        int count = Mathf.Min(colors?.Length ?? 0, _texWidth * _texHeight);
        int requiredSize = _texWidth * _texHeight * 4;
        if (_colorBuffer == null || _colorBuffer.Length != requiredSize)
            _colorBuffer = new byte[requiredSize];

        for (int i = 0; i < count; i++)
        {
            int offset = i * 4;
            _colorBuffer[offset + 0] = (byte)(colors[i].R * 255);
            _colorBuffer[offset + 1] = (byte)(colors[i].G * 255);
            _colorBuffer[offset + 2] = (byte)(colors[i].B * 255);
            _colorBuffer[offset + 3] = 255;
        }

        _tileColorImage.SetData(_texWidth, _texHeight, false, Image.Format.Rgba8, _colorBuffer);
        _tileColorTexture.Update(_tileColorImage);
    }

    public void UpdateColors(Color[] colors)
    {
        if (colors == null) return;
        ValidateColors(colors, _hexasphere?.GetTileCount() ?? 0);
        _lastColors = colors;
        if (!IsProjectionReady() || _tileColorImage == null || MeshInstance2D?.Material as ShaderMaterial == null)
        {
            if (_hexasphere != null)
                BuildMap2D(_hexasphere, colors, _lastMapSize);
            return;
        }
        WriteColorsToImage(colors);
    }

    public void PrecomputeUvGeometry(NativeHexasphere hexasphere, Vector2 mapSize)
    {
        if (hexasphere == null) return;
        if (ProjectionMode == UvProjectionMode.Raster
            || (ProjectionMode == UvProjectionMode.Auto && hexasphere.GetTileCount() > MaxDetailedTileCount))
            return;

        var geom = ComputeUvGeometry(hexasphere, mapSize);
        _cachedVertices = geom.Vertices;
        _cachedTriToTile = geom.TriToTile;
        _cachedHitTris = geom.HitTris;
        _cachedGeomHexasphere = hexasphere;
        _cachedGeomMapSize = mapSize;
        _hasCachedGeometry = true;
    }

    private Vector2[] ComputeTileUvs(int tileIndex)
    {
        Vector3[] pts = _hexasphere.GetTilePoints(tileIndex);
        Vector3 centerPos = _hexasphere.GetTileCenter(tileIndex);
        int n = pts.Length;

        Vector2 centerUv = HexasphereUvProjector.CalculateUv(centerPos);
        var uvs = new Vector2[n + 1];
        uvs[0] = centerUv;

        float r0 = centerPos.Length();

        for (int i = 0; i < n; i++)
        {
            Vector3 pos = pts[i];
            uvs[i + 1] = HexasphereUvProjector.CalculateUv(pos);
            float r = pos.Length();
        }



        // Unwrap relative to center for ALL vertices, including fixed pole vertices —
        // otherwise they remain in a different longitude domain than the rest of the tile.
        float refU = uvs[0].X;
        for (int i = 0; i < uvs.Length; i++)
            uvs[i].X += HexasphereUvProjector.GetSeamOffset(uvs[i].X, refU);

        return uvs;
    }

    private Vector2 UvToScreen(Vector2 uv, Vector2 mapSize)
    {
        return new Vector2(uv.X * mapSize.X, uv.Y * mapSize.Y);
    }

    private void BuildSpatialGrid()
    {
        _spatialGrid.Clear();
        
        // Dynamic cell size: target ~64 cells across map width, minimum 16
        _cellSize = Mathf.Max(16f, _lastMapSize.X / 64f);
        
        for (int i = 0; i < _hitTris.Count; i++)
        {
            var tri = _hitTris[i];
            float minX = Mathf.Min(tri.A.X, Mathf.Min(tri.B.X, tri.C.X));
            float minY = Mathf.Min(tri.A.Y, Mathf.Min(tri.B.Y, tri.C.Y));
            float maxX = Mathf.Max(tri.A.X, Mathf.Max(tri.B.X, tri.C.X));
            float maxY = Mathf.Max(tri.A.Y, Mathf.Max(tri.B.Y, tri.C.Y));

            int x0 = Mathf.FloorToInt(minX / _cellSize);
            int y0 = Mathf.FloorToInt(minY / _cellSize);
            int x1 = Mathf.FloorToInt(maxX / _cellSize);
            int y1 = Mathf.FloorToInt(maxY / _cellSize);

            for (int gx = x0; gx <= x1; gx++)
            for (int gy = y0; gy <= y1; gy++)
            {
                var key = (gx, gy);
                if (!_spatialGrid.TryGetValue(key, out var list))
                    _spatialGrid[key] = list = new List<int>();
                list.Add(i);
            }
        }
    }

    private int HitTest(Vector2 pos)
    {
        if (_rasterMode)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(pos.X / _lastMapSize.X * _rasterResolution.X), 0, _rasterResolution.X - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(pos.Y / _lastMapSize.Y * _rasterResolution.Y), 0, _rasterResolution.Y - 1);
            return _rasterTileIds[y * _rasterResolution.X + x];
        }
        int gx = Mathf.FloorToInt(pos.X / _cellSize);
        int gy = Mathf.FloorToInt(pos.Y / _cellSize);

        if (!_spatialGrid.TryGetValue((gx, gy), out var candidates))
            return -1;

        for (int i = 0; i < candidates.Count; i++)
        {
            var tri = _hitTris[candidates[i]];
            if (IsPointInTriangle(pos, tri.A, tri.B, tri.C))
                return tri.TileIndex;
        }
        return -1;
    }

    private void UpdateShaderSelection()
    {
        var material = MeshInstance2D?.Material as ShaderMaterial;
        if (material == null) return;

        material.SetShaderParameter("selected_idx", IsClickVisualEnabled ? _selectedTile : -1);
        material.SetShaderParameter("hover_idx", IsHoverVisualEnabled ? _hoveredTile : -1);
        material.SetShaderParameter("selected_color", new Vector4(ClickColor.R, ClickColor.G, ClickColor.B, ClickColor.A));
        material.SetShaderParameter("hover_color", new Vector4(HoverColor.R, HoverColor.G, HoverColor.B, HoverColor.A));
    }

    /// <summary>
    /// Updates the selection and hover indices on the shader.
    /// </summary>
    public virtual void SetSelection(int selectedIdx, int hoveredIdx)
    {
        _selectedTile = selectedIdx;
        _hoveredTile = hoveredIdx;
        UpdateShaderSelection();
    }

    private bool IsPoleCapTile(int tileIndex)
    {
        // Detect pole-cap tiles by how far their vertex ring winds around the
        // pole in longitude, not by center Y-position. A tile whose vertices
        // wrap almost a full turn (~1.0) around the pole needs fan rendering;
        // this doesn't require tuning a threshold against sphere subdivision.
        Vector3[] pts = _hexasphere.GetTilePoints(tileIndex);
        int n = pts.Length;
        if (n < 3) return false;

        float total = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 uvA = HexasphereUvProjector.CalculateUv(pts[i]);
            Vector2 uvB = HexasphereUvProjector.CalculateUv(pts[(i + 1) % n]);
            float d = uvB.X - uvA.X;
            d -= Mathf.Round(d);
            total += d;
        }

        return Mathf.Abs(Mathf.Abs(total) - 1f) < 0.1f;
    }

    private void EmitPoleCapTile(SurfaceTool st, int tileIndex, Color color, Vector2 mapSize)
    {
        Vector3[] pts = _hexasphere.GetTilePoints(tileIndex);
        Vector3 centerPos = _hexasphere.GetTileCenter(tileIndex);
        int n = pts.Length;

        float poleV = centerPos.Y > 0 ? 0f : 1f;

        var ringUv = new Vector2[n];
        for (int i = 0; i < n; i++)
            ringUv[i] = HexasphereUvProjector.CalculateUv(pts[i]);

        int[] order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        System.Array.Sort(order, (a, b) => ringUv[a].X.CompareTo(ringUv[b].X));

        for (int k = 0; k < n; k++)
        {
            int i0 = order[k];
            int i1 = order[(k + 1) % n];

            float u0 = ringUv[i0].X, v0 = ringUv[i0].Y;
            float u1 = ringUv[i1].X, v1 = ringUv[i1].Y;
            if (k == n - 1) u1 += 1f;

            // Clip pole quad against [0,1]x[0,1] bounds
            EmitClippedPoleQuad(st, u0, v0, u1, v1, poleV, mapSize, color, tileIndex);
        }
    }

    private void EmitClippedPoleQuad(SurfaceTool st, float u0, float v0, float u1, float v1, float poleV, Vector2 mapSize, Color color, int tileIndex)
    {
        // Quad vertices: (u0,v0), (u1,v0), (u1,poleV), (u0,poleV)
        var quad = new List<Vector2>
        {
            new Vector2(u0, v0),
            new Vector2(u1, v0),
            new Vector2(u1, poleV),
            new Vector2(u0, poleV)
        };

        var clipped = ClipPolygonToRect(quad);
        if (clipped.Count < 3) return;

        // Fan-triangulate from first vertex
        for (int j = 1; j < clipped.Count - 1; j++)
        {
            var p0 = UvToScreen(clipped[0], mapSize);
            var p1 = UvToScreen(clipped[j], mapSize);
            var p2 = UvToScreen(clipped[j + 1], mapSize);

            st.SetColor(color);
            st.AddVertex(new Vector3(p0.X, p0.Y, 0));
            st.SetColor(color);
            st.AddVertex(new Vector3(p1.X, p1.Y, 0));
            st.SetColor(color);
            st.AddVertex(new Vector3(p2.X, p2.Y, 0));

            _hitTris.Add(new HitTri { TileIndex = tileIndex, A = p0, B = p1, C = p2 });
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;

        if (@event.IsActionPressed("ui_close_uv_map"))
        {
            Visible = false;
            ProcessMode = ProcessModeEnum.Disabled;

            EmitSignal(SignalName.ProjectionClosed);

            GetViewport().SetInputAsHandled();
            return;
        }

        if (!IsProjectionReady()) return;

        Vector2 pos = ToLocal(GetGlobalMousePosition());
        bool inBounds = pos.Y >= 0 && pos.Y <= _lastMapSize.Y && pos.X >= 0 && pos.X <= _lastMapSize.X;

        if (@event is InputEventMouseMotion && IsHoverEnabled)
        {
            int newHover = -1;
            if (inBounds)
                newHover = HitTest(pos);

            if (newHover != _hoveredTile)
            {
                _hoveredTile = newHover;
                UpdateShaderSelection();
                EmitSignal(SignalName.TileHovered, _hoveredTile);
            }
        }

        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && IsClickEnabled)
        {
            if (!inBounds) return;

            int hit = HitTest(pos);

            if (hit == _selectedTile) return;

            _selectedTile = hit;
            UpdateShaderSelection();

            if (hit >= 0)
                EmitSignal(SignalName.TileClicked, hit);
            else
                EmitSignal(SignalName.TileDeselected);

            GetViewport().SetInputAsHandled();
        }
    }

    private static bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 v0 = c - a;
        Vector2 v1 = b - a;
        Vector2 v2 = p - a;
        float dot00 = v0.Dot(v0);
        float dot01 = v0.Dot(v1);
        float dot02 = v0.Dot(v2);
        float dot11 = v1.Dot(v1);
        float dot12 = v1.Dot(v2);
        float denom = dot00 * dot11 - dot01 * dot01;
        if (denom <= 0f) return false;
        float inv = 1f / denom;
        float u = (dot11 * dot02 - dot01 * dot12) * inv;
        float v = (dot00 * dot12 - dot01 * dot02) * inv;
        // Tile centers and shared edges lie on triangle boundaries. Include them
        // with a small barycentric tolerance so floating-point roundoff cannot
        // make a visible cell unpickable.
        const float tolerance = 0.00001f;
        return u >= -tolerance && v >= -tolerance && u + v <= 1f + tolerance;
    }

    #region Polygon clipping (Sutherland-Hodgman)

    /// <summary>
    /// Clips a polygon against the rectangle [0,1]×[0,1].
    /// Returns clipped polygon vertices (may differ from input count),
    /// or empty list if entirely outside.
    /// </summary>
    private static List<Vector2> ClipPolygonToRect(List<Vector2> polygon)
    {
        if (polygon.Count < 3)
            return new List<Vector2>(polygon);

        var result = new List<Vector2>(polygon);

        result = ClipToHalfPlane(result,  1f,  0f, 0f);
        if (result.Count < 3) return result;

        result = ClipToHalfPlane(result, -1f,  0f, 1f);
        if (result.Count < 3) return result;

        result = ClipToHalfPlane(result,  0f,  1f, 0f);
        if (result.Count < 3) return result;

        result = ClipToHalfPlane(result,  0f, -1f, 1f);
        if (result.Count < 3) return result;

        return result;
    }

    /// <summary>
    /// Clips polygon against the half-plane a*u + b*v + c >= 0.
    /// Implements one pass of Sutherland-Hodgman.
    /// </summary>
    private static List<Vector2> ClipToHalfPlane(List<Vector2> polygon, float a, float b, float c)
    {
        var output = new List<Vector2>();
        int n = polygon.Count;

        for (int i = 0; i < n; i++)
        {
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % n];

            float dCurrent = a * current.X + b * current.Y + c;
            float dNext = a * next.X + b * next.Y + c;

            bool currentInside = dCurrent >= 0f;
            bool nextInside = dNext >= 0f;

            if (currentInside)
            {
                if (nextInside)
                {
                    // Both inside: output next
                    output.Add(next);
                }
                else
                {
                    // Exiting: output intersection with boundary
                    float t = dCurrent / (dCurrent - dNext);
                    output.Add(new Vector2(
                        current.X + t * (next.X - current.X),
                        current.Y + t * (next.Y - current.Y)
                    ));
                }
            }
            else if (nextInside)
            {
                // Entering: output intersection, then next
                float t = dCurrent / (dCurrent - dNext);
                output.Add(new Vector2(
                    current.X + t * (next.X - current.X),
                    current.Y + t * (next.Y - current.Y)
                ));
                output.Add(next);
            }
        }

        return output;
    }

    /// <summary>
    /// Clips a single triangle against [0,1]×[0,1].
    /// Returns clipped polygon (may have 0, 3, 4, 5, 6, or 7 vertices).
    /// </summary>
    private static List<Vector2> ClipTriangleToRect(Vector2 a, Vector2 b, Vector2 c)
    {
        var tri = new List<Vector2> { a, b, c };
        return ClipPolygonToRect(tri);
    }

    #endregion

    /// <summary>
    /// Marks the UV map mesh as dirty, forcing a full rebuild on the next BuildMap2D call.
    /// </summary>
    public virtual void MarkDirty()
    {
        _meshDirty = true;
        _hasCachedGeometry = false;
        _cachedVertices = null;
        _cachedTriToTile = null;
        _cachedHitTris = null;
    }

    private void ReleaseColorMaterial()
    {
        if (GodotObject.IsInstanceValid(MeshInstance2D)) MeshInstance2D.Material = null;
        _mapMaterial?.Dispose();
        _mapMaterial = null;
        _tileColorTexture?.Dispose();
        _tileColorTexture = null;
        _tileColorImage?.Dispose();
        _tileColorImage = null;
    }

    public override void _ExitTree()
    {
        ReleaseColorMaterial();
        if (GodotObject.IsInstanceValid(MeshInstance2D))
        {
            var mesh = MeshInstance2D.Mesh;
            MeshInstance2D.Mesh = null;
            mesh?.Dispose();
        }
        ClearGeometryCaches();
        _hitTris.Clear();
        _spatialGrid.Clear();
        _colorBuffer = null;
        _lastColors = null;
        _hasBuiltMap = false;
        _meshDirty = true;
        BuildState = UvProjectionBuildState.NotBuilt;
    }
}
