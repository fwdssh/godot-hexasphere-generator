using Godot;
using System.Threading.Tasks;
using System.Collections.Generic;
using Godot.Hexasphere;
/// <summary>
/// Main node for generating and interacting with a hexagonal sphere (hexasphere).
/// Handles planet generation, tile selection/hover, UV projection, and input routing.
/// </summary>
public partial class HexasphereNode : Node3D
{
    /// <summary>
    /// Emitted when a tile is clicked. Provides the tile index and world-space hit position.
    /// </summary>
    [Signal] public delegate void TileClickedEventHandler(int tileIndex, Vector3 worldPosition);
    /// <summary>
    /// Emitted when the hovered tile changes. Provides the tile index (-1 if none).
    /// </summary>
    [Signal] public delegate void TileHoveredEventHandler(int tileIndex);
    /// <summary>
    /// Emitted when the currently selected tile is deselected.
    /// </summary>
    [Signal] public delegate void TileDeselectedEventHandler();
    /// <summary>
    /// Emitted when the planet generation completes. Provides the total number of tiles.
    /// </summary>
    [Signal] public delegate void PlanetGeneratedEventHandler(int tileCount);




    [ExportGroup("Geometry")]
    /// <summary>The radius of the sphere in world units.</summary>
    [Export] public float PlanetRadius = 20;
    /// <summary>Number of icosahedron subdivisions. Higher values produce more tiles.</summary>
    [Export] public int SubDivision = 20;
    /// <summary>Seed for the random color generator. Use -1 for random seed.</summary>
    [Export] public int GenerationSeed = -1;


    [ExportGroup("Interaction")]

    /// <summary>If true, tiles can be clicked to select them.</summary>
    [Export] public bool IsClickEnabled = true;
    /// <summary>If true, tiles emit hover events when the mouse moves over them.</summary>
    [Export] public bool IsHoverEnabled = true;



    [ExportGroup("Visual")]
    /// <summary>Relative size of each hexagonal tile (range 0.1–1.0).</summary>
    [Export(PropertyHint.Range, "0.1, 1.0")] public float HexSize = 1f;
    /// <summary>If true, the planet material uses emissive rendering.</summary>
    [Export] public bool IsEmissive = false;


    /// <summary>If true, the selected tile is highlighted with ClickColor.</summary>
    [Export] public bool IsClickVisualEnabled = true;
    /// <summary>Color used to highlight the selected tile.</summary>
    [Export] public Color ClickColor = Colors.Black;


    /// <summary>If true, the hovered tile is highlighted with HoverColor.</summary>
    [Export] public bool IsHoverVisualEnabled = true;
    /// <summary>Color used to highlight the hovered tile.</summary>
    [Export] public Color HoverColor = Colors.Red;



    [ExportGroup("Borders")]
    /// <summary>If true, tile borders are rendered.</summary>
    [Export] public bool IsBordering = true;
    private Color _borderColor = Colors.White;
    /// <summary>Color of the tile borders.</summary>
    [Export] public Color BorderColor
    {
        get => _borderColor;
        set { _borderColor = value; VisualController?.SetBorderColor(value); }
    }






    [ExportGroup("Shaders")]
    /// <summary>Shader used for rendering tile colors.</summary>
    [Export] public Shader ColorsShader = GD.Load<Shader>("res://addons/hexasphere_generator/scripts/hexasphere_node/shaders/hexasphere_colors.gdshader");
    /// <summary>Shader used for rendering tile borders.</summary>
    [Export] public Shader BordersShader = GD.Load<Shader>("res://addons/hexasphere_generator/scripts/hexasphere_node/shaders/hexasphere_borders.gdshader");

    [ExportGroup("UV Projector")]
    /// <summary>NodePath to the HexasphereProjectorController used for UV map projection.</summary>
    [Export] public NodePath UvProjectorPath;
    private HexasphereProjectorController UvProjector;

    protected HexasphereVisualController VisualController;
    protected ICellData[] _cellDatas;
    protected NativeHexasphere _hexasphere;
    protected Color[] _tileColors;
    private int _selectedTileIndex = -1;
    private int _hoveredTileIndex = -1;
    protected bool _planetReady = false;
    protected bool _shouldAutoGenerate = true;

    protected NativeHexasphere _pendingHexasphere;
    protected Vector3[] _pendingCenters;
    protected ICellData[] _pendingCellDatas;
    protected Vector3[] _tileDirs;

    private float _bucketScale = 5f;
    protected Dictionary<Vector3I, List<int>> _spatialBuckets;

    /// <summary>Returns true once the planet has been fully generated and is ready for interaction.</summary>
    public bool IsReady => _planetReady;
    /// <summary>Total number of tiles on the generated planet.</summary>
    public int TileCount => _cellDatas?.Length ?? 0;


    virtual protected ICellData[] CreateCellData(int count, Vector3[] centers)
    {
        var rng = new RandomNumberGenerator();
        if (GenerationSeed >= 0)
            rng.Seed = (ulong)GenerationSeed;
        else
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
        HexasphereInputRouter.Register(this);
        SetVisualController();

        if (!string.IsNullOrEmpty(UvProjectorPath))
        {
            var node = GetNodeOrNull(UvProjectorPath);
            UvProjector = node as HexasphereProjectorController;

            if (UvProjector != null)
            {
                UvProjector.ProjectionClosed += OnProjectionClosed;
            }
        }

        if (_shouldAutoGenerate)
            StartGeneration();
    }

    virtual protected void CleanupResources()
    {
        _hexasphere?.Dispose();
        _hexasphere = null;
        _cellDatas = null;
        _tileColors = null;
        _tileDirs = null;
        _spatialBuckets = null;
        _pendingHexasphere = null;
        _pendingCenters = null;
        _pendingCellDatas = null;
    }

    virtual protected void StartGeneration()
    {
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
                hexasphere.Dispose();
            }
        });
    }

    virtual protected void SetVisualController()
    {
        VisualController = GetNodeOrNull<HexasphereVisualController>("HexasphereVisual");
        if (VisualController != null && !GodotObject.IsInstanceValid(VisualController))
            VisualController = null;
        if (VisualController == null)
        {
            VisualController = new HexasphereVisualController();
            VisualController.Name = "HexasphereVisual";
            AddChild(VisualController);
        }
    }



    virtual protected void GenerateInBackground(NativeHexasphere hexasphere)
    {
        // Pure C++ generation + data extraction — safe on background thread
        hexasphere.Generate(PlanetRadius, SubDivision, HexSize);

        int tileCount = hexasphere.GetTileCount();
        var centers = hexasphere.GetAllTileCenters();
        var cellDatas = CreateCellData(tileCount, centers);

        _pendingHexasphere = hexasphere;
        _pendingCenters = centers;
        _pendingCellDatas = cellDatas;

        CallDeferred(MethodName.FinalizePlanet);
    }

virtual protected void FinalizePlanet()
{
    if (!IsInsideTree())
    {
        _pendingHexasphere?.Dispose();
        _pendingHexasphere = null;
        return;
    }

    if (_pendingHexasphere == null)
    {
        _pendingCenters    = null;
        _pendingCellDatas  = null;
        return;
    }

    _cellDatas = _pendingCellDatas;
    _hexasphere = _pendingHexasphere;

    var result = _pendingHexasphere.BuildMesh();
    var mesh = (ArrayMesh)result["mesh"];

    if (!GodotObject.IsInstanceValid(VisualController))
    {
        _pendingHexasphere = null;
        _pendingCenters    = null;
        _pendingCellDatas  = null;
        return;
    }

    VisualController.SetNativeHexasphere(_pendingHexasphere);
    VisualController.ShaderReady += OnShaderReady;
    VisualController.ApplyGenerated(mesh, IsBordering, ColorsShader, BordersShader);
    VisualController.SetBorderColor(BorderColor);
    BuildSpatialIndex(_pendingCenters);

    _pendingHexasphere = null;
    _pendingCenters    = null;
    _pendingCellDatas  = null;
}
virtual protected void OnShaderReady()
{
    VisualController.ShaderReady -= OnShaderReady;
    VisualController.DrawColors(_cellDatas);
    VisualController.SetEmissive(IsEmissive);

    if (_hexasphere != null && _cellDatas != null)
    {
        _tileColors = new Color[_cellDatas.Length];
        for (int i = 0; i < _cellDatas.Length; i++)
            _tileColors[i] = VisualController.GetColor(_cellDatas[i]);
    }

    _planetReady = true;
    EmitSignal(SignalName.PlanetGenerated, _cellDatas?.Length ?? 0);
}

    /// <summary>
    /// Opens the UV map projection view. Disables the 3D camera and shows the 2D UV editor.
    /// </summary>
    public virtual void OpenUvProjector()
{
    if (UvProjector == null || !_planetReady) return;

    var camera3D = GetViewport().GetCamera3D();
    if (camera3D == null)
    {
        GD.PrintErr("[HexasphereNode] No active Camera3D in viewport to disable for UV mode.");
        return;
    }

    UvProjector.TileClicked += OnUvTileClicked;
    UvProjector.TileDeselected += OnUvTileDeselected;
    UvProjector.TileHovered += OnUvTileHovered;

    UvProjector.BuildMap2D(_hexasphere, _tileColors, UvProjector.MapSize);
    UvProjector.SetSelection(_selectedTileIndex, _hoveredTileIndex);
    UvProjector.Visible = true;
    UvProjector.ProcessMode = ProcessModeEnum.Always;

    HexasphereInputRouter.RequestUvProjection(this);
    HexasphereInputRouter.EnterUvMode(camera3D);

    // Make UV camera current
    var camera = UvProjector.GetNodeOrNull<UvCamera2D>("Camera2D");
    if (camera != null)
    {
        camera.MakeCurrent();
    }
}

    private void OnUvTileClicked(int tileIndex)
    {
        _selectedTileIndex = tileIndex;
        EmitSignal(SignalName.TileClicked, tileIndex, GetTileCenter(tileIndex));
        VisualController.SetSelection(
            IsClickVisualEnabled ? ClickColor : null, _selectedTileIndex,
            IsHoverVisualEnabled ? HoverColor : null, _hoveredTileIndex);

        HexasphereInputRouter.NotifySelectionChanged(this);
    }

    private void OnUvTileDeselected()
    {
        _selectedTileIndex = -1;
        EmitSignal(SignalName.TileDeselected);
        VisualController.SetSelection(
            IsClickVisualEnabled ? ClickColor : null, -1,
            IsHoverVisualEnabled ? HoverColor : null, _hoveredTileIndex);
    }

    private void OnUvTileHovered(int tileIndex)
    {
        _hoveredTileIndex = tileIndex;
        VisualController.SetSelection(
            IsClickVisualEnabled ? ClickColor : null, _selectedTileIndex,
            IsHoverVisualEnabled ? HoverColor : null, _hoveredTileIndex);
    }

    private void OnProjectionClosed()
    {
        if (UvProjector != null)
        {
            UvProjector.TileClicked -= OnUvTileClicked;
            UvProjector.TileDeselected -= OnUvTileDeselected;
            UvProjector.TileHovered -= OnUvTileHovered;
        }

        HexasphereInputRouter.ExitUvMode();
        HexasphereInputRouter.OnUvProjectionClosed(this);
    }

    private bool TryRaycastToTile(out Vector3 hitPosition, out int tileIndex)
    {
        hitPosition = Vector3.Zero;
        tileIndex = -1;

        var camera = GetViewport().GetCamera3D();
        if (camera == null) return false;

        var mousePos = GetViewport().GetMousePosition();
        Vector3 rayOriginWorld = camera.ProjectRayOrigin(mousePos);
        Vector3 origin = ToLocal(rayOriginWorld);
        Vector3 dir    = (ToLocal(rayOriginWorld + camera.ProjectRayNormal(mousePos)) - origin).Normalized();

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
        // Global key toggle — requires cursor over sphere
        if (@event is InputEventKey { Pressed: true } && Input.IsActionJustPressed("ui_toggle_uv_map") && IsClickEnabled)
        {
            if (TryRaycastToTile(out _, out int idx) && idx >= 0)
            {
                if (_planetReady && UvProjector != null) OpenUvProjector();
            }
            return;
        }

        // For mouse events, use router to determine which sphere wins
        if (@event is InputEventMouse)
        {
            var viewport = GetViewport();
            Vector2 mousePos = viewport.GetMousePosition();

            var winner = HexasphereInputRouter.FindSphereUnderCursor(@event, mousePos, GetViewport(), out _);
            if (winner != this)
            {
                // We are not the winner — clear hover if we had one
                if (_hoveredTileIndex != -1)
                {
                    _hoveredTileIndex = -1;
                    EmitSignal(SignalName.TileHovered, -1);
                    VisualController.SetSelection(
                        IsClickVisualEnabled ? ClickColor : null, _selectedTileIndex,
                        IsHoverVisualEnabled ? HoverColor : null, -1);
                }
                return;
            }
        }

        // Left click — select/deselect tile
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } && IsClickEnabled)
        {
            if (TryRaycastToTile(out var hitPos, out int idx) && idx >= 0)
            {
                _selectedTileIndex = idx;
                EmitSignal(SignalName.TileClicked, idx, hitPos);
                VisualController.SetSelection(
                    IsClickVisualEnabled ? ClickColor : null, _selectedTileIndex,
                    IsHoverVisualEnabled ? HoverColor : null, _hoveredTileIndex);
                
                // Clear selection from all other spheres
                HexasphereInputRouter.NotifySelectionChanged(this);
            }
            else
            {
                _selectedTileIndex = -1;
                EmitSignal(SignalName.TileDeselected);
                VisualController.SetSelection(
                    IsClickVisualEnabled ? ClickColor : null, -1,
                    IsHoverVisualEnabled ? HoverColor : null, _hoveredTileIndex);
            }
            SyncUvProjectorSelection();
            GetViewport().SetInputAsHandled();
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
        HexasphereInputRouter.Unregister(this);
        if (UvProjector != null)
        {
            UvProjector.ProjectionClosed -= OnProjectionClosed;
        }
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

        // 0.35f empirically tuned for SubDivision=20; scales with sqrt(tile count) for roughly uniform bucket density
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

    /// <summary>
    /// Finds the tile index for a given world-space direction vector.
    /// </summary>
    /// <param name="worldDirection">World-space direction from the sphere center.</param>
    /// <returns>The tile index, or -1 if no tile matches.</returns>
    public int FindTileByDirection(Vector3 worldDirection)
    {
        Vector3 localDir = (GlobalTransform.Basis.Inverse() * worldDirection).Normalized();
        return FindTileIndexByDirection(localDir);
    }

    /// <summary>
    /// Try to get the ray-sphere intersection distance in world space for input arbitration.
    /// Returns true if the ray from camera through screenPos intersects this sphere.
    /// Outputs worldDist = world-space distance from camera to intersection point.
    /// This is scale-invariant and correct for comparing spheres with different transforms.
    /// </summary>
    public virtual bool TryGetRayIntersectionWorldDistance(Vector2 screenPos, Camera3D camera, out float worldDist)
    {
        worldDist = float.MaxValue;
        if (camera == null) return false;
        
        Vector3 rayOrigin = camera.ProjectRayOrigin(screenPos);
        Vector3 rayDir = camera.ProjectRayNormal(screenPos);
        
        // Convert to local space for intersection test
        Vector3 origin = ToLocal(rayOrigin);
        Vector3 dir = (ToLocal(rayOrigin + rayDir) - origin).Normalized();
        
        // Ray-sphere intersection in local space
        float b = origin.Dot(dir);
        float c = origin.Dot(origin) - PlanetRadius * PlanetRadius;
        float disc = b * b - c;
        
        // Epsilon for floating-point stability at grazing angles
        const float epsilon = 1e-6f;
        if (disc < -epsilon) return false;
        
        // Clamp to zero for near-tangent rays
        if (disc < 0f) disc = 0f;
        
        float sq = Mathf.Sqrt(disc);
        float t1 = -b - sq;
        float t2 = -b + sq;
        float t = t1 >= 0f ? t1 : t2;
        
        if (t < 0f) return false;
        
        // Compute hit point in local space, convert to world, measure distance from camera
        Vector3 hitLocal = origin + dir * t;
        Vector3 hitWorld = ToGlobal(hitLocal);
        worldDist = camera.GlobalPosition.DistanceTo(hitWorld);
        
        return true;
    }

    /// <summary>
    /// Clear the current tile selection on this sphere.
    /// Called by HexasphereInputRouter when another sphere is selected.
    /// </summary>
    public virtual void ClearSelection()
    {
        if (_selectedTileIndex != -1)
        {
            _selectedTileIndex = -1;
            EmitSignal(SignalName.TileDeselected);
            VisualController.SetSelection(
                IsClickVisualEnabled ? ClickColor : null, -1,
                IsHoverVisualEnabled ? HoverColor : null, _hoveredTileIndex);
            SyncUvProjectorSelection();
        }
    }

    private void SyncUvProjectorSelection()
    {
        if (UvProjector != null && UvProjector.Visible)
            UvProjector.SetSelection(_selectedTileIndex, -1);
    }

    /// <summary>Returns the currently selected tile index, or -1 if none selected.</summary>
    public int GetSelectedTileIndex() => _selectedTileIndex;
    /// <summary>Returns the currently hovered tile index, or -1 if none hovered.</summary>
    public int GetHoveredTileIndex() => _hoveredTileIndex;

    /// <summary>
    /// Set a single tile's color at runtime (e.g. from GDScript).
    /// </summary>
    public void SetTileColor(int tileIndex, Color color)
    {
        if (_cellDatas == null || tileIndex < 0 || tileIndex >= _cellDatas.Length) return;
        if (_cellDatas[tileIndex] is HexCellData hex)
        {
            hex.color = color;
            VisualController.DrawColors(_cellDatas);
        }
    }

    /// <summary>
    /// Sets the color of every tile at once. Also rebuilds the cached color array for the UV projector.
    /// </summary>
    /// <param name="colors">Array of colors indexed by tile index.</param>
    public void SetAllTileColors(Color[] colors)
    {
        if (_cellDatas == null) return;
        int count = Mathf.Min(colors.Length, _cellDatas.Length);
        for (int i = 0; i < count; i++)
        {
            if (_cellDatas[i] is HexCellData hex)
                hex.color = colors[i];
        }
        VisualController.DrawColors(_cellDatas);
        
        // Update _tileColors for UV projector (create new array to trigger rebuild)
        _tileColors = new Color[_cellDatas.Length];
        for (int i = 0; i < count; i++)
            _tileColors[i] = colors[i];
    }

    /// <summary>
    /// Gets the current color of a specific tile.
    /// </summary>
    /// <param name="tileIndex">The tile index.</param>
    /// <returns>The tile color, or Colors.Black if unavailable.</returns>
    public Color GetTileColor(int tileIndex)
    {
        if (_cellDatas == null || tileIndex < 0 || tileIndex >= _cellDatas.Length) return Colors.Black;
        return VisualController.GetColor(_cellDatas[tileIndex]);
    }

    /// <summary>Returns the total number of tiles, or 0 if not yet generated.</summary>
    public int GetTileCount() => _cellDatas?.Length ?? 0;

    /// <summary>
    /// Gets the center position of a specific tile in local space.
    /// </summary>
    /// <param name="tileIndex">The tile index.</param>
    /// <returns>The center position, or Vector3.Zero if unavailable.</returns>
    public Vector3 GetTileCenter(int tileIndex)
    {
        return _hexasphere?.GetTileCenter(tileIndex) ?? Vector3.Zero;
    }

    /// <summary>
    /// Gets the vertex positions of a specific tile in local space.
    /// </summary>
    /// <param name="tileIndex">The tile index.</param>
    /// <returns>Array of vertex positions, or empty array if unavailable.</returns>
    public Vector3[] GetTilePoints(int tileIndex)
    {
        return _hexasphere?.GetTilePoints(tileIndex) ?? System.Array.Empty<Vector3>();
    }

    /// <summary>
    /// Called by HexasphereInputRouter to close UV projection externally.
    /// </summary>
    public virtual void CloseUvProjectorFromRouter()
    {
        if (UvProjector != null && UvProjector.Visible)
        {
            UvProjector.Visible = false;
            UvProjector.ProcessMode = ProcessModeEnum.Disabled;
            UvProjector.EmitSignal(HexasphereProjectorController.SignalName.ProjectionClosed);
        }
    }


}
