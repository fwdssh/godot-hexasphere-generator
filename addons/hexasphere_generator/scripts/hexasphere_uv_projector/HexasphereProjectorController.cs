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
    /// Emitted when the UV projection view is closed.
    /// </summary>
    [Signal] public delegate void ProjectionClosedEventHandler();

    /// <summary>Size of the UV map render target in pixels.</summary>
    [Export] public Vector2 MapSize = new Vector2(1920, 1080);
    /// <summary>Color used to highlight the selected tile on the UV map.</summary>
    [Export] public Color SelectionColor = Colors.Yellow;
    
    /// <summary>The MeshInstance2D that displays the main UV map.</summary>
    public MeshInstance2D MeshInstance2D;
    /// <summary>The MeshInstance2D that displays the selection overlay on the UV map.</summary>
    public MeshInstance2D OverlayMeshInstance2D;

    private NativeHexasphere _hexasphere;
    private Color[] _colors;
    private Vector2 _lastMapSize;
    private int _selectedTile = -1;
    private UvCamera2D _camera2D;
    private bool _meshDirty = true;
    private NativeHexasphere _cachedHexasphere;
    private Color[] _cachedColors;
    private Vector2 _cachedMapSize;


    


    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Disabled;
        
        EnsureChildNodes();

    MeshInstance2D = GetNodeOrNull<MeshInstance2D>("MeshInstance2D");
    OverlayMeshInstance2D = GetNodeOrNull<MeshInstance2D>("OverlayMeshInstance2D");
    
    _camera2D = GetNodeOrNull<UvCamera2D>("Camera2D");
        
    }



    private void EnsureChildNodes()
{
    if (GetNodeOrNull<MeshInstance2D>("MeshInstance2D") == null)
    {
        var mesh = new MeshInstance2D { Name = "MeshInstance2D" };
        AddChild(mesh);
    }
    if (GetNodeOrNull<MeshInstance2D>("OverlayMeshInstance2D") == null)
    {
        var overlay = new MeshInstance2D { Name = "OverlayMeshInstance2D" };
        AddChild(overlay);
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

        bool needsRebuild = _meshDirty
            || _cachedHexasphere != hexasphere
            || _cachedColors != colors
            || _cachedMapSize != mapSize;

        _cachedHexasphere = hexasphere;
        _cachedColors = colors;
        _cachedMapSize = mapSize;

        if (needsRebuild)
        {
            if (MeshInstance2D.Mesh is ArrayMesh old)
                old.Dispose();

            _hexasphere = hexasphere;
            _colors = colors;
            _lastMapSize = mapSize;
            _selectedTile = -1;

            BuildMesh(colors, mapSize);
            BuildSpatialGrid();
            _meshDirty = false;

            if (OverlayMeshInstance2D != null)
            {
                if (OverlayMeshInstance2D.Mesh is ArrayMesh oldOverlayMesh)
                    oldOverlayMesh.Dispose();
                OverlayMeshInstance2D.Mesh = null;
            }
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
        int tileCount = _hexasphere.GetTileCount();
        _hitTris.Clear();

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        for (int t = 0; t < tileCount; t++)
        {
            Vector3[] pts = _hexasphere.GetTilePoints(t);
            int n = pts.Length;
            if (n < 3) continue;

            Color color = colors != null ? colors[t] : Colors.White;

            if (IsPoleCapTile(t))
            {
                EmitPoleCapTile(st, t, color, mapSize);
                continue;
            }

            Vector2[] uvs = ComputeTileUvs(t);

            // Clip each fan triangle against [0,1]x[0,1] to handle seam wrapping and poles
            Vector2 center = uvs[0];
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                var clipped = ClipTriangleToRect(center, uvs[i + 1], uvs[next + 1]);
                if (clipped.Count >= 3)
                {
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

                        _hitTris.Add(new HitTri { TileIndex = t, A = p0, B = p1, C = p2 });
                    }
                }
            }
        }

        MeshInstance2D.Mesh = st.Commit();
        
        if (MeshInstance2D.Material == null)
        {
            var material = new CanvasItemMaterial();
            material.BlendMode = CanvasItemMaterial.BlendModeEnum.Mix;
            MeshInstance2D.Material = material;
        }
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
        // Round to integer pixels so mathematically identical vertices affected by float error merge into one point for the GPU.
        float px = Mathf.Round(uv.X * mapSize.X);
        float py = Mathf.Round(uv.Y * mapSize.Y);
        
        return new Vector2(px, py);
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

    private void RebuildSelectionOverlay(int selectedTile)
    {
        if (OverlayMeshInstance2D == null) return;
        
        if (selectedTile < 0 || _hexasphere == null)
        {
            OverlayMeshInstance2D.Mesh = null;
            return;
        }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        if (IsPoleCapTile(selectedTile))
        {
            EmitOverlayPoleCapTile(st, selectedTile, _lastMapSize);
        }
        else
        {
            Vector3[] pts = _hexasphere.GetTilePoints(selectedTile);
            int n = pts.Length;
            if (n < 3) return;

            Vector2[] uvs = ComputeTileUvs(selectedTile);

            // Clip fan triangles against [0,1]x[0,1]
            Vector2 center = uvs[0];
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                var clipped = ClipTriangleToRect(center, uvs[i + 1], uvs[next + 1]);
                if (clipped.Count >= 3)
                {
                    for (int j = 1; j < clipped.Count - 1; j++)
                    {
                        var p0 = UvToScreen(clipped[0], _lastMapSize);
                        var p1 = UvToScreen(clipped[j], _lastMapSize);
                        var p2 = UvToScreen(clipped[j + 1], _lastMapSize);

                        st.SetColor(SelectionColor);
                        st.AddVertex(new Vector3(p0.X, p0.Y, 0));
                        st.SetColor(SelectionColor);
                        st.AddVertex(new Vector3(p1.X, p1.Y, 0));
                        st.SetColor(SelectionColor);
                        st.AddVertex(new Vector3(p2.X, p2.Y, 0));
                    }
                }
            }
        }

        if (OverlayMeshInstance2D.Mesh is ArrayMesh oldMesh)
            oldMesh.Dispose();
        OverlayMeshInstance2D.Mesh = st.Commit();
    }

    private void EmitOverlayPoleCapTile(SurfaceTool st, int tileIndex, Vector2 mapSize)
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
            EmitClippedOverlayPoleQuad(st, u0, v0, u1, v1, poleV, mapSize);
        }
    }

    private void EmitClippedOverlayPoleQuad(SurfaceTool st, float u0, float v0, float u1, float v1, float poleV, Vector2 mapSize)
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

            st.SetColor(SelectionColor);
            st.AddVertex(new Vector3(p0.X, p0.Y, 0));
            st.SetColor(SelectionColor);
            st.AddVertex(new Vector3(p1.X, p1.Y, 0));
            st.SetColor(SelectionColor);
            st.AddVertex(new Vector3(p2.X, p2.Y, 0));
        }
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

        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            return;

        Vector2 pos = ToLocal(GetGlobalMousePosition());

        if (pos.Y < 0 || pos.Y > _lastMapSize.Y)
            return;

        int hit = HitTest(pos);

        if (hit == _selectedTile) return;

        _selectedTile = hit;
        RebuildSelectionOverlay(hit);

        if (hit >= 0)
            EmitSignal(SignalName.TileClicked, hit);
        else
            EmitSignal(SignalName.TileDeselected);

        GetViewport().SetInputAsHandled();
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