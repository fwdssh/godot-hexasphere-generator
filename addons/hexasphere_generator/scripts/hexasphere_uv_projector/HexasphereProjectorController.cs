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

    /// <summary>Size of the UV map render target in pixels.</summary>
    [Export] public Vector2 MapSize = new Vector2(1920, 1080);

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
    /// Builds or rebuilds the 2D UV map mesh from the hexasphere data.
    /// </summary>
    /// <param name="hexasphere">The native hexasphere with generated geometry data.</param>
    /// <param name="colors">Array of tile colors indexed by tile index.</param>
    /// <param name="mapSize">Target size of the UV map in pixels.</param>
    public virtual void BuildMap2D(NativeHexasphere hexasphere, Color[] colors, Vector2 mapSize)
    {
        if (hexasphere == null || MeshInstance2D == null)
        {
            GD.PrintErr($"[HexasphereProjectorController] BuildMap2D failed - hexasphere is null: {hexasphere == null}, MeshInstance2D is null: {MeshInstance2D == null}");
            return;
        }

        int tileCount = hexasphere.GetTileCount();

        if (colors != null && colors.Length < tileCount)
            throw new System.ArgumentException(
                $"colors.Length ({colors.Length}) < tileCount ({tileCount})");

        bool geomChanged = _meshDirty
            || _cachedHexasphere != hexasphere
            || _cachedMapSize != mapSize;

        _cachedHexasphere = hexasphere;
        _cachedMapSize = mapSize;

        if (geomChanged)
        {
            if (MeshInstance2D.Mesh is ArrayMesh old)
                old.Dispose();

            _hexasphere = hexasphere;
            _lastMapSize = mapSize;
            _selectedTile = -1;
            _hoveredTile = -1;

            if (_hasCachedGeometry && _cachedGeomHexasphere == hexasphere && _cachedGeomMapSize == mapSize)
                BuildMeshFromCache(colors, mapSize);
            else
                BuildMesh(colors, mapSize);

            BuildSpatialGrid();
            _meshDirty = false;
        }
        else if (colors != null)
        {
            WriteColorsToImage(colors);
        }

        var camera = _camera2D;
        if (camera != null)
        {
            camera.Position = new Vector2(mapSize.X / 2f, mapSize.Y / 2f);
            camera.TargetZoom = 0.5f;
            camera.SetPanLimits(mapSize);
        }
    }

    private void BuildMesh(Color[] colors, Vector2 mapSize)
    {
        var geom = ComputeUvGeometry(_hexasphere, mapSize);
        int tileCount = _hexasphere.GetTileCount();
        int tw = Mathf.CeilToInt(Mathf.Sqrt(tileCount));
        int th = Mathf.CeilToInt((float)tileCount / tw);

        var st = new SurfaceTool();
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

        MeshInstance2D.Mesh = st.Commit();
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
        _hexasphere = hexasphere;
        int tileCount = hexasphere.GetTileCount();

        var buildData = hexasphere.GetBuildData();
        var allPoints = (Vector3[])buildData["points"];
        var pointCounts = (int[])buildData["point_counts"];
        var allCenters = hexasphere.GetAllTileCenters();

        var verts = new List<Vector3>();
        var triToTile = new List<int>();
        var hitTris = new List<HitTri>();

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
                        new Vector2(u1, v0),
                        new Vector2(u1, poleV),
                        new Vector2(u0, poleV)
                    };

                    var clipped = ClipPolygonToRect(quad);
                    if (clipped.Count < 3) continue;

                    for (int j = 1; j < clipped.Count - 1; j++)
                    {
                        var p0 = UvToScreen(clipped[0], mapSize);
                        var p1 = UvToScreen(clipped[j], mapSize);
                        var p2 = UvToScreen(clipped[j + 1], mapSize);

                        verts.Add(new Vector3(p0.X, p0.Y, 0));
                        verts.Add(new Vector3(p1.X, p1.Y, 0));
                        verts.Add(new Vector3(p2.X, p2.Y, 0));
                        triToTile.Add(t);
                        hitTris.Add(new HitTri { TileIndex = t, A = p0, B = p1, C = p2 });
                    }
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
                    var clipped = ClipTriangleToRect(uvs[0], uvs[i + 1], uvs[next + 1]);
                    if (clipped.Count >= 3)
                    {
                        for (int j = 1; j < clipped.Count - 1; j++)
                        {
                            var p0 = UvToScreen(clipped[0], mapSize);
                            var p1 = UvToScreen(clipped[j], mapSize);
                            var p2 = UvToScreen(clipped[j + 1], mapSize);

                            verts.Add(new Vector3(p0.X, p0.Y, 0));
                            verts.Add(new Vector3(p1.X, p1.Y, 0));
                            verts.Add(new Vector3(p2.X, p2.Y, 0));
                            triToTile.Add(t);
                            hitTris.Add(new HitTri { TileIndex = t, A = p0, B = p1, C = p2 });
                        }
                    }
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
        int tileCount = _cachedTriToTile.Length;
        int tw = Mathf.CeilToInt(Mathf.Sqrt(tileCount));
        int th = Mathf.CeilToInt((float)tileCount / tw);

        var st = new SurfaceTool();
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
        MeshInstance2D.Mesh = st.Commit();
        _hitTris = new List<HitTri>(_cachedHitTris);

        SetupTextureMaterial(colors);
    }

    private void SetupTextureMaterial(Color[] colors)
    {
        int tileCount = _hexasphere?.GetTileCount() ?? colors?.Length ?? 0;
        _texWidth = Mathf.CeilToInt(Mathf.Sqrt(tileCount));
        _texHeight = Mathf.CeilToInt((float)tileCount / _texWidth);

        _tileColorImage = Image.CreateEmpty(_texWidth, _texHeight, false, Image.Format.Rgba8);
        _tileColorTexture = ImageTexture.CreateFromImage(_tileColorImage);

        if (_uvTileColorsShader == null)
            _uvTileColorsShader = GD.Load<Shader>("res://addons/hexasphere_generator/scripts/hexasphere_node/shaders/uv_tile_colors.gdshader");

        var material = new ShaderMaterial();
        material.Shader = _uvTileColorsShader;
        material.SetShaderParameter("tile_colors", _tileColorTexture);
        material.SetShaderParameter("tile_count", tileCount);
        material.SetShaderParameter("tex_width", _texWidth);
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

        var img = Image.CreateFromData(_texWidth, _texHeight, false, Image.Format.Rgba8, _colorBuffer);
        _tileColorTexture.Update(img);
    }

    public void UpdateColors(Color[] colors)
    {
        if (_tileColorImage == null || MeshInstance2D?.Material as ShaderMaterial == null)
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
        return u >= 0f && v >= 0f && u + v < 1f;
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
    public virtual void MarkDirty() => _meshDirty = true;
}