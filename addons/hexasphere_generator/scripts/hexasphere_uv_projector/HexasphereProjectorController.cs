using Godot;
using System.Collections.Generic;
using Godot.Hexasphere;

public partial class HexasphereProjectorController : Node2D
{
    [Signal] public delegate void TileClickedEventHandler(int tileIndex);
    [Signal] public delegate void TileDeselectedEventHandler();
    [Signal] public delegate void ProjectionClosedEventHandler();

    [Export] public Vector2 MapSize = new Vector2(1920, 1080);
    [Export] public NodePath MeshInstance2DPath;
    [Export] public NodePath OverlayMeshInstance2DPath;
    [Export] public NodePath Camera3DPath;
    
    public MeshInstance2D MeshInstance2D;
    public MeshInstance2D OverlayMeshInstance2D;

    private NativeHexasphere _hexasphere;
    private Color[] _colors;
    private Vector2 _lastMapSize;
    private int _selectedTile = -1;
    private const bool DebugLogging = false;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Disabled;
        
        if (MeshInstance2DPath != null)
            MeshInstance2D = GetNodeOrNull<MeshInstance2D>(MeshInstance2DPath);
        
        if (OverlayMeshInstance2DPath != null)
            OverlayMeshInstance2D = GetNodeOrNull<MeshInstance2D>(OverlayMeshInstance2DPath);
        
        if (DebugLogging) GD.Print($"[HexasphereProjectorController] _Ready - MeshInstance2D: {MeshInstance2D != null}, OverlayMeshInstance2D: {OverlayMeshInstance2D != null}");
        if (DebugLogging) GD.Print($"[HexasphereProjectorController] _Ready - Self Position: {Position}, GlobalPosition: {GlobalPosition}");
        if (MeshInstance2D != null)
        {
            if (DebugLogging) GD.Print($"[HexasphereProjectorController] MeshInstance2D - Visible: {MeshInstance2D.Visible}, GlobalPosition: {MeshInstance2D.GlobalPosition}, Position: {MeshInstance2D.Position}");
        }
    }
    
    public override void _Notification(int what)
    {
        if (what == NotificationVisibilityChanged && Visible)
        {
            var camera = GetNodeOrNull<UvCamera2D>("Camera2D");
            if (camera != null)
            {
                camera.MakeCurrent();
                if (DebugLogging) GD.Print("[HexasphereProjectorController] Camera2D made current");
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
    private const float CellSize = 64f;

    public void BuildMap2D(NativeHexasphere hexasphere, Color[] colors, Vector2 mapSize)
    {
        if (DebugLogging) GD.Print($"[HexasphereProjectorController] BuildMap2D called - hexasphere: {hexasphere != null}, MeshInstance2D: {MeshInstance2D != null}, mapSize: {mapSize}");
        
        if (hexasphere == null || MeshInstance2D == null)
        {
            GD.PrintErr($"[HexasphereProjectorController] BuildMap2D failed - hexasphere is null: {hexasphere == null}, MeshInstance2D is null: {MeshInstance2D == null}");
            return;
        }

        int tileCount = hexasphere.GetTileCount();
        if (DebugLogging) GD.Print($"[HexasphereProjectorController] Tile count: {tileCount}");

        if (colors != null && colors.Length < tileCount)
            throw new System.ArgumentException(
                $"colors.Length ({colors.Length}) < tileCount ({tileCount})");

        if (MeshInstance2D.Mesh is ArrayMesh old)
            old.Dispose();

        _hexasphere = hexasphere;
        _colors = colors;
        _lastMapSize = mapSize;
        _selectedTile = -1;

        BuildMesh(colors, mapSize);
        BuildSpatialGrid();

        if (OverlayMeshInstance2D != null)
        {
            if (OverlayMeshInstance2D.Mesh is ArrayMesh oldOverlayMesh)
                oldOverlayMesh.Dispose();
            OverlayMeshInstance2D.Mesh = null;
        }
        
        // Center camera on the map
        var camera = GetNodeOrNull<UvCamera2D>("Camera2D");
        if (camera != null)
        {
            camera.Position = new Vector2(mapSize.X / 2f, mapSize.Y / 2f);
            camera.TargetZoom = 0.5f; // Start zoomed out to see the whole map
            if (DebugLogging) GD.Print($"[HexasphereProjectorController] Camera centered at {camera.Position}, TargetZoom: {camera.TargetZoom}");
        }
        
        if (DebugLogging) GD.Print($"[HexasphereProjectorController] BuildMap2D completed - Mesh created: {MeshInstance2D.Mesh != null}");
        if (DebugLogging) GD.Print($"[HexasphereProjectorController] MeshInstance2D - Visible: {MeshInstance2D.Visible}, GlobalPosition: {MeshInstance2D.GlobalPosition}, Position: {MeshInstance2D.Position}");
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

            Vector2[] uvs = ComputeTileUvs(t);

            if (HasPolarStretch(uvs)) continue;

            Color color = colors != null ? colors[t] : Colors.White;

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                var a = UvToScreen(uvs[0], mapSize);
                var b = UvToScreen(uvs[i + 1], mapSize);
                var c = UvToScreen(uvs[next + 1], mapSize);

                st.SetColor(color);
                st.AddVertex(new Vector3(a.X, a.Y, 0));
                st.SetColor(color);
                st.AddVertex(new Vector3(b.X, b.Y, 0));
                st.SetColor(color);
                st.AddVertex(new Vector3(c.X, c.Y, 0));

                _hitTris.Add(new HitTri { TileIndex = t, A = a, B = b, C = c });
            }
        }

        MeshInstance2D.Mesh = st.Commit();
        
        // Create material if not exists
        if (MeshInstance2D.Material == null)
        {
            var material = new CanvasItemMaterial();
            material.BlendMode = CanvasItemMaterial.BlendModeEnum.Mix;
            MeshInstance2D.Material = material;
            if (DebugLogging) GD.Print("[HexasphereProjectorController] Created CanvasItemMaterial for MeshInstance2D");
        }
        
        if (DebugLogging) GD.Print($"[HexasphereProjectorController] BuildMesh completed - Mesh created: {MeshInstance2D.Mesh != null}, Vertices: {_hitTris.Count * 3}");
        if (DebugLogging) GD.Print($"[HexasphereProjectorController] MeshInstance2D - Visible: {MeshInstance2D.Visible}, GlobalPosition: {MeshInstance2D.GlobalPosition}, Position: {MeshInstance2D.Position}");
        if (DebugLogging) GD.Print($"[HexasphereProjectorController] MeshInstance2D - Material: {MeshInstance2D.Material != null}, ZIndex: {MeshInstance2D.ZIndex}");
    }

    private bool HasPolarStretch(Vector2[] uvs)
    {
        float minU = float.MaxValue, maxU = float.MinValue;
        for (int i = 0; i < uvs.Length; i++)
        {
            if (uvs[i].X < minU) minU = uvs[i].X;
            if (uvs[i].X > maxU) maxU = uvs[i].X;
        }
        return (maxU - minU) > 0.5f;
    }

    private Vector2[] ComputeTileUvs(int tileIndex)
    {
        Vector3[] pts = _hexasphere.GetTilePoints(tileIndex);
        Vector3 centerPos = _hexasphere.GetTileCenter(tileIndex);
        int n = pts.Length;

        Vector2 centerUv = HexasphereUvProjector.CalculateUv(centerPos);
        var uvs = new Vector2[n + 1];
        uvs[0] = centerUv;
        var isPole = new bool[n + 1];

        float r0 = centerPos.Length();
        isPole[0] = r0 > 0 && Mathf.Abs(centerPos.Y) / r0 > HexasphereUvProjector.PoleThreshold;

        for (int i = 0; i < n; i++)
        {
            Vector3 pos = pts[i];
            uvs[i + 1] = HexasphereUvProjector.CalculateUv(pos);
            float r = pos.Length();
            isPole[i + 1] = r > 0 && Mathf.Abs(pos.Y) / r > HexasphereUvProjector.PoleThreshold;
        }

        for (int i = 0; i < uvs.Length; i++)
        {
            if (!isPole[i]) continue;

            float sum = 0;
            int count = 0;
            float refPole = float.NaN;

            for (int j = 0; j < uvs.Length; j++)
            {
                if (j == i || isPole[j]) continue;

                if (float.IsNaN(refPole)) refPole = uvs[j].X;
                float u = uvs[j].X + HexasphereUvProjector.GetSeamOffset(uvs[j].X, refPole);
                sum += u;
                count++;
            }

            if (count > 0)
            {
                float avg = sum / count;
                uvs[i].X = avg - Mathf.Floor(avg);
            }
        }

        float refU = uvs[0].X;
        for (int i = 0; i < uvs.Length; i++)
        {
            if (!isPole[i])
                uvs[i].X += HexasphereUvProjector.GetSeamOffset(uvs[i].X, refU);
        }

        float minU = float.MaxValue, maxU = float.MinValue;
        for (int i = 0; i < uvs.Length; i++)
        {
            if (uvs[i].X < minU) minU = uvs[i].X;
            if (uvs[i].X > maxU) maxU = uvs[i].X;
        }

        if (maxU - minU > 0.5f)
        {
            float shift = refU < (minU + maxU) * 0.5f ? 1f : -1f;
            for (int i = 0; i < uvs.Length; i++)
            {
                if (!isPole[i] && ((shift > 0f && uvs[i].X < refU) || (shift < 0f && uvs[i].X > refU)))
                    uvs[i].X += shift;
            }
        }

        return uvs;
    }

    private Vector2 UvToScreen(Vector2 uv, Vector2 mapSize)
    {
        return new Vector2(uv.X * mapSize.X, (1f - uv.Y) * mapSize.Y);
    }

    private void BuildSpatialGrid()
    {
        _spatialGrid.Clear();
        for (int i = 0; i < _hitTris.Count; i++)
        {
            var tri = _hitTris[i];
            float minX = Mathf.Min(tri.A.X, Mathf.Min(tri.B.X, tri.C.X));
            float minY = Mathf.Min(tri.A.Y, Mathf.Min(tri.B.Y, tri.C.Y));
            float maxX = Mathf.Max(tri.A.X, Mathf.Max(tri.B.X, tri.C.X));
            float maxY = Mathf.Max(tri.A.Y, Mathf.Max(tri.B.Y, tri.C.Y));

            int x0 = (int)(minX / CellSize);
            int y0 = (int)(minY / CellSize);
            int x1 = (int)(maxX / CellSize);
            int y1 = (int)(maxY / CellSize);

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
        int gx = (int)(pos.X / CellSize);
        int gy = (int)(pos.Y / CellSize);

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

        Vector3[] pts = _hexasphere.GetTilePoints(selectedTile);
        int n = pts.Length;
        if (n < 3) return;

        Vector2[] uvs = ComputeTileUvs(selectedTile);

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            var a = UvToScreen(uvs[0], _lastMapSize);
            var b = UvToScreen(uvs[i + 1], _lastMapSize);
            var c = UvToScreen(uvs[next + 1], _lastMapSize);

            st.SetColor(Colors.Yellow);
            st.AddVertex(new Vector3(a.X, a.Y, 0));
            st.SetColor(Colors.Yellow);
            st.AddVertex(new Vector3(b.X, b.Y, 0));
            st.SetColor(Colors.Yellow);
            st.AddVertex(new Vector3(c.X, c.Y, 0));
        }

        if (OverlayMeshInstance2D.Mesh is ArrayMesh oldMesh)
        {
            oldMesh.Dispose();
        }
        OverlayMeshInstance2D.Mesh = st.Commit();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_close_uv_map"))
        {
            Visible = false;
            ProcessMode = ProcessModeEnum.Disabled;

            EmitSignal(SignalName.ProjectionClosed);

            // Keep the existing Camera3D restore logic as fallback
            Camera3D camera3D = null;
            if (!string.IsNullOrEmpty(Camera3DPath))
                camera3D = GetNodeOrNull<Camera3D>(Camera3DPath);
            if (camera3D != null)
            {
                camera3D.ProcessMode = ProcessModeEnum.Inherit;
                camera3D.Current = true;
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            return;

        Vector2 pos = ToLocal(GetGlobalMousePosition());

        if (pos.X < 0 || pos.X > _lastMapSize.X || pos.Y < 0 || pos.Y > _lastMapSize.Y)
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
}
