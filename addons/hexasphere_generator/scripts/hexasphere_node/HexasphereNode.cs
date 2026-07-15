using Godot;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Godot.Hexasphere;
public partial class HexasphereNode : Node3D
{
    [Signal] public delegate void TileClickedEventHandler(int tileIndex, Vector3 worldPosition);
    [Signal] public delegate void TileHoveredEventHandler(int tileIndex);
    [Signal] public delegate void TileDeselectedEventHandler();




    [ExportGroup("Geometry")]
    [Export] public float PlanetRadius = 20;
    [Export] public int SubDivision = 20;

    [ExportGroup("Visual")]
    [Export(PropertyHint.Range, "0.1, 1.0")] public float HexSize = 1f;



    [Export] public bool IsClickEnabled = true;
    [Export] public bool IsClickVisualEnabled = true;
    [Export] public Color ClickColor = Colors.Black;


    [Export] public bool IsHoverEnabled = true;
    [Export] public bool IsHoverVisualEnabled = true;
    [Export] public Color HoverColor = Colors.Red;



    [ExportGroup("use it if HexSize =1f and u need borders")]
    [Export] public bool IsBordering = true;
    private Color _borderColor = Colors.White;
    [Export] public Color BorderColor
    {
        get => _borderColor;
        set { _borderColor = value; VisualController?.SetBorderColor(value); }
    }






    [ExportGroup("Shaders")]
    [Export] public Shader ColorsShader = GD.Load<Shader>("res://addons/hexasphere_generator/scripts/hexasphere_node/shaders/hexasphere_colors.gdshader");
    [Export] public Shader BordersShader = GD.Load<Shader>("res://addons/hexasphere_generator/scripts/hexasphere_node/shaders/hexasphere_borders.gdshader");

    [ExportGroup("UV Projector")]
    [Export] public NodePath UvProjectorPath;
    private HexasphereProjectorController UvProjector;
    private Camera3D _camera3D;

    private HexasphereVisualController VisualController;
    private ICellData[] _cellDatas;
    private NativeHexasphere _hexasphere;
    private Color[] _tileColors;
    private int _selectedTileIndex = -1;
    private int _hoveredTileIndex = -1;
    private bool _planetReady = false;

    private NativeHexasphere _pendingHexasphere;
    private Vector3[] _pendingCenters;
    private ICellData[]       _pendingCellDatas;
    private Vector3[] _tileDirs;

    private float _bucketScale = 5f;
    private Dictionary<Vector3I, List<int>> _spatialBuckets;

    public bool IsReady => _planetReady;
    public int TileCount => _cellDatas?.Length ?? 0;


    virtual protected ICellData[] CreateCellData(int count)
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var data = new HexCellData[count];
        for (int i = 0; i < data.Length; i++)
        {
        data[i] = new HexCellData
        {
            color = Color.FromHsv(rng.Randf(), 0.6f, 0.85f)
        };
        }

        return data;
    }


    public override void _Ready()
    {
        VisualController = GetNodeOrNull<HexasphereVisualController>("HexasphereVisual");
        if (VisualController == null)
        {
            VisualController = new HexasphereVisualController();
            VisualController.Name = "HexasphereVisual";
            AddChild(VisualController);
        }

        if (UvProjectorPath != null)
        {
            var node = GetNodeOrNull(UvProjectorPath);
            GD.Print($"[HexasphereNode] UvProjectorPath: {UvProjectorPath}, node found: {node != null}, node type: {node?.GetType().Name}");
            UvProjector = node as HexasphereProjectorController;
            GD.Print($"[HexasphereNode] UvProjector cast result: {UvProjector != null}");
            
            // Check CanvasLayer
            var canvasLayer = UvProjector?.GetParent<CanvasLayer>();
            if (canvasLayer != null)
            {
                GD.Print($"[HexasphereNode] CanvasLayer found - Visible: {canvasLayer.Visible}, Layer: {canvasLayer.Layer}");
            }
        }

        // Find Camera3D to disable it when UV mode is active
        _camera3D = GetNodeOrNull<Camera3D>("../Camera3D");

        // Create NativeHexasphere on main thread (Godot RefCounted)
        var hexasphere = new NativeHexasphere();
        Task.Run(() =>
        {
            try
            {
                GenerateInBackground(hexasphere);
            }
            catch (System.Exception e)
            {
                GD.PrintErr($"[HexasphereNode] Generation error: {e}");
            }
        });
    }

    virtual protected void GenerateInBackground(NativeHexasphere hexasphere)
    {
        // Pure C++ generation + data extraction — safe on background thread
        hexasphere.Generate(PlanetRadius, SubDivision, HexSize);

        int tileCount = hexasphere.GetTileCount();
        var centers = hexasphere.GetAllTileCenters();
        var cellDatas = CreateCellData(tileCount);

        _pendingHexasphere = hexasphere;
        _pendingCenters = centers;
        _pendingCellDatas = cellDatas;

        CallDeferred(MethodName.FinalizePlanet);
    }

virtual protected void FinalizePlanet()
{
    if (!IsInsideTree()) return;

    _cellDatas = _pendingCellDatas;
    _hexasphere = _pendingHexasphere;

    var result = _pendingHexasphere.BuildMesh();
    var mesh = (ArrayMesh)result["mesh"];

    VisualController.SetNativeHexasphere(_pendingHexasphere);
    VisualController.ApplyGenerated(mesh, IsBordering, ColorsShader, BordersShader);
    VisualController.SetBorderColor(BorderColor);
    BuildSpatialIndex(_pendingCenters);

    _pendingHexasphere = null;
    _pendingCenters    = null;
    _pendingCellDatas  = null;

    VisualController.ShaderReady += OnShaderReady;
}
virtual protected void OnShaderReady()
{
    VisualController.ShaderReady -= OnShaderReady;
    VisualController.DrawColors(_cellDatas);

    // Extract colors for UV projector before disposing visual controller's reference
    if (_hexasphere != null && _cellDatas != null)
    {
        _tileColors = new Color[_cellDatas.Length];
        for (int i = 0; i < _cellDatas.Length; i++)
            _tileColors[i] = VisualController.GetColor(_cellDatas[i]);
        GD.Print($"[HexasphereNode] _tileColors initialized: {_tileColors.Length} colors");
    }

    _planetReady = true;
}

    private bool TryRaycastToTile(out Vector3 hitPosition, out int tileIndex)
    {
        hitPosition = Vector3.Zero;
        tileIndex = -1;

        var camera = GetViewport().GetCamera3D();
        if (camera == null) return false;

        var mousePos = GetViewport().GetMousePosition();
        Vector3 origin = ToLocal(camera.ProjectRayOrigin(mousePos));
        Vector3 dir    = (ToLocal(camera.ProjectRayOrigin(mousePos) + camera.ProjectRayNormal(mousePos)) - origin).Normalized();

        if (!RaySphereIntersect(origin, dir, PlanetRadius, out hitPosition))
            return false;

        tileIndex = FindTileIndexByDirection(hitPosition.Normalized());
        return tileIndex >= 0;
    }

    private static bool RaySphereIntersect(Vector3 origin, Vector3 dir, float radius, out Vector3 hit)
    {
        float b = origin.Dot(dir);
        float c = origin.Dot(origin) - radius * radius;
        float disc = b * b - c;
        if (disc < 0f) { hit = Vector3.Zero; return false; }

        float sq = Mathf.Sqrt(disc);
        float t1 = -b - sq;
        float t2 = -b + sq;
        float t = t1 >= 0f ? t1 : t2;
        if (t < 0f) { hit = Vector3.Zero; return false; }

        hit = origin + dir * t;
        return true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } && IsClickEnabled)
        {
            if (TryRaycastToTile(out var hitPos, out int idx) && idx >= 0)
            {
                _selectedTileIndex = idx;
                EmitSignal(SignalName.TileClicked, idx, hitPos);
                VisualController.SetSelection(
                    IsClickVisualEnabled ? ClickColor : null, _selectedTileIndex,
                    IsHoverVisualEnabled ? HoverColor : null, _hoveredTileIndex);
            }
            else
            {
                _selectedTileIndex = -1;
                EmitSignal(SignalName.TileDeselected);
                VisualController.SetSelection(
                    IsClickVisualEnabled ? ClickColor : null, -1,
                    IsHoverVisualEnabled ? HoverColor : null, _hoveredTileIndex);
            }
        }

        if (@event is InputEventKey { Pressed: true, Keycode: Key.Key1 } && IsClickEnabled)
        {
            GD.Print($"[HexasphereNode] Key 1 pressed. UvProjector: {UvProjector != null}, _hexasphere: {_hexasphere != null}, _tileColors: {_tileColors != null}");
            if (UvProjector != null && _hexasphere != null && _tileColors != null)
            {
                GD.Print("[HexasphereNode] Opening UV projector...");
                
                // Check CanvasLayer visibility
                var canvasLayer = UvProjector.GetParent<CanvasLayer>();
                if (canvasLayer != null)
                {
                    GD.Print($"[HexasphereNode] CanvasLayer found - Visible: {canvasLayer.Visible}, Layer: {canvasLayer.Layer}, Offset: {canvasLayer.Offset}");
                }
                
                UvProjector.BuildMap2D(_hexasphere, _tileColors, UvProjector.MapSize);
                UvProjector.Visible = true;
                UvProjector.ProcessMode = ProcessModeEnum.Inherit;
                
                // Disable Camera3D to prevent it from intercepting input
                if (_camera3D != null)
                {
                    _camera3D.ProcessMode = ProcessModeEnum.Disabled;
                    _camera3D.Current = false;
                }
                
                GD.Print($"[HexasphereNode] UvProjector - Visible: {UvProjector.Visible}, ProcessMode: {UvProjector.ProcessMode}, Position: {UvProjector.Position}, GlobalPosition: {UvProjector.GlobalPosition}");
                
                // Make UV camera current
                var camera = UvProjector.GetNodeOrNull<UvCamera2D>("Camera2D");
                if (camera != null)
                {
                    camera.MakeCurrent();
                    GD.Print($"[HexasphereNode] UV Camera2D made current - Position: {camera.Position}, Zoom: {camera.Zoom}, GlobalPosition: {camera.GlobalPosition}");
                }
                
                // Check MeshInstance2D
                if (UvProjector.MeshInstance2D != null)
                {
                    GD.Print($"[HexasphereNode] MeshInstance2D - Visible: {UvProjector.MeshInstance2D.Visible}, GlobalPosition: {UvProjector.MeshInstance2D.GlobalPosition}, Mesh: {UvProjector.MeshInstance2D.Mesh != null}");
                }
                
                GD.Print("[HexasphereNode] UV projector opened");
            }
        }

        if (@event is InputEventMouseMotion && IsHoverEnabled)
        {
            int newHover = -1;
            if (TryRaycastToTile(out _, out int idx))
            {
                newHover = idx;
            }
            if (newHover != _hoveredTileIndex)
            {
                _hoveredTileIndex = newHover;
                EmitSignal(SignalName.TileHovered, newHover);
                VisualController.SetSelection(
                    IsClickVisualEnabled ? ClickColor : null, _selectedTileIndex,
                    IsHoverVisualEnabled ? HoverColor : null, _hoveredTileIndex);
            }
        }

    }

    public override void _ExitTree()
    {
        _hexasphere?.Dispose();
        _hexasphere = null;
    }

    virtual protected void BuildSpatialIndex(Vector3[] centers)
    {
        _tileDirs = new Vector3[centers.Length];
        for (int i = 0; i < centers.Length; i++)
        {
            _tileDirs[i] = centers[i].Normalized();
        }

        _bucketScale = Mathf.Sqrt(centers.Length) * 0.35f;

        // Build spatial hash buckets
        BuildSpatialBuckets();
    }

    private Vector3I Quantize(Vector3 v)
    {
        return new Vector3I(
            (int)Mathf.Round(v.X * _bucketScale),
            (int)Mathf.Round(v.Y * _bucketScale),
            (int)Mathf.Round(v.Z * _bucketScale)
        );
    }

    private void BuildSpatialBuckets()
    {
        _spatialBuckets = new Dictionary<Vector3I, List<int>>();
        for (int i = 0; i < _tileDirs.Length; i++)
        {
            var key = Quantize(_tileDirs[i]);
            if (!_spatialBuckets.TryGetValue(key, out var list))
            {
                list = new List<int>();
                _spatialBuckets[key] = list;
            }
            list.Add(i);
        }
    }

    virtual protected int FindTileIndexByDirection(Vector3 direction)
    {
        if (_tileDirs == null || _spatialBuckets == null) return -1;

        Vector3 normDir = direction.Normalized();
        var key = Quantize(normDir);

        float maxDot = -2f;
        int bestIndex = -1;

        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        for (int dz = -1; dz <= 1; dz++)
        {
            var neighbor = new Vector3I(key.X + dx, key.Y + dy, key.Z + dz);
            if (_spatialBuckets.TryGetValue(neighbor, out var list))
            {
                foreach (var idx in list)
                {
                    float d = normDir.Dot(_tileDirs[idx]);
                    if (d > maxDot)
                    {
                        maxDot = d;
                        bestIndex = idx;
                    }
                }
            }
        }

        return bestIndex;
    }


}
