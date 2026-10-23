using Godot;
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
    private long _interactionRevision;
    internal long InteractionRevision => _interactionRevision;
    private bool _uvOpening;
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
        using var rng = new RandomNumberGenerator();
        int seed = CurrentGenerationSettings?.Seed ?? GenerationSeed;
        if (seed >= 0)
            rng.Seed = (ulong)seed;
        else
            rng.Randomize();
        var data = new HexCellData[count];
        for (int i = 0; i < data.Length; i++)
        {
        if ((i & 1023) == 0) GenerationCancellationToken.ThrowIfCancellationRequested();
        data[i] = new HexCellData
        {
            color = Color.FromHsv(rng.Randf(), 0.6f, 0.85f)
        };
        }

        return data;
    }


    public override void _Ready()
    {
        InitializeGenerationLifecycle();
        SetVisualController();

        if (!string.IsNullOrEmpty(UvProjectorPath))
        {
            var node = GetNodeOrNull(UvProjectorPath);
            UvProjector = node as HexasphereProjectorController;

        }

        if (_shouldAutoGenerate)
            StartGeneration();
    }

    virtual protected void CleanupResources()
    {
        var requestController = _activeGeneration?.Controller;
        InvalidateGenerationRequest();
        ResetGeneratedPlanet(requestController);
    }

    private void ResetGeneratedPlanet(HexasphereVisualController requestController)
    {
        long revision = ++_interactionRevision;
        bool hadHover = _hoveredTileIndex != -1;
        var source = _hexasphere;
        var pending = _pendingHexasphere;
        _planetReady = false;
        _selectedTileIndex = -1;
        _hoveredTileIndex = -1;
        _hexasphere = null;
        _cellDatas = null;
        _tileColors = null;
        _tileDirs = null;
        _spatialBuckets = null;
        _pendingHexasphere = null;
        _pendingCenters = null;
        _pendingCellDatas = null;
        var currentController = VisualController;
        if (GodotObject.IsInstanceValid(requestController) && !ReferenceEquals(requestController, currentController))
            requestController.ClearGenerated();
        if (_interactionRevision == revision && GodotObject.IsInstanceValid(currentController))
        {
            currentController.ShaderReady -= OnGenerationShaderReady;
            currentController.ShaderInitializationFailed -= OnGenerationShaderFailed;
            currentController.ClearGenerated();
        }
        // Publish the reset before close/hover signals can start another generation.
        if (_interactionRevision == revision && HexasphereInputRouter.IsUvProjectionOwner(this))
            CloseUvProjectorFromRouter();
        if (GodotObject.IsInstanceValid(UvProjector))
            UvProjector.InvalidateSource(source);
        source?.Dispose();
        pending?.Dispose();
        if (hadHover && GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion()
            && _interactionRevision == revision)
            EmitSignal(SignalName.TileHovered, -1);
    }

    virtual protected void StartGeneration()
    {
        BeginGenerationRequest();
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
        GenerateRequestInBackground(hexasphere);
    }

virtual protected void FinalizePlanet()
{
    FinalizeGenerationRequest();
}
virtual protected void OnShaderReady()
{
    PrepareGenerationColors();
}

    /// <summary>
    /// Prepares the configured UV projector without opening it. Available from C# and GDScript.
    /// Returns false when the planet/projector is not ready or the projection build fails.
    /// </summary>
    public bool PrepareUvProjector()
    {
        if (!HasInteractionResources() || !GodotObject.IsInstanceValid(UvProjector)
            || !UvProjector.IsInsideTree() || UvProjector.GetViewport() != GetViewport()) return false;
        if (UvProjector.IsProjectionOpen && !UvProjector.IsOwnedBy(this)) return false;
        var source = _hexasphere;
        long revision = _interactionRevision;
        UvProjector.BuildMap2D(source, _tileColors, UvProjector.MapSize);
        return InteractionUnchanged(revision, source) && GodotObject.IsInstanceValid(UvProjector)
            && UvProjector.IsInsideTree() && UvProjector.IsProjectionReady();
    }

    /// <summary>Opens the configured UV view, acquiring its viewport before changing the shared map.</summary>
    public virtual void OpenUvProjector()
    {
        if (_uvOpening || !HasInteractionResources() || !IsVisibleInTree() || !CanProcess()
            || !GodotObject.IsInstanceValid(UvProjector) || !UvProjector.IsInsideTree()
            || UvProjector.GetViewport() != GetViewport()) return;
        if (HexasphereInputRouter.IsUvProjectionOwner(this) && UvProjector.IsOwnedBy(this))
        {
            SyncUvProjectorSelection();
            return;
        }

        var source = _hexasphere;
        long revision = _interactionRevision;
        _uvOpening = true;
        try
        {
            if (!HexasphereInputRouter.RequestUvProjection(this)) return;
            // The previous owner's close callbacks may regenerate/free this node.
            if (!InteractionUnchanged(revision, source)) return;
            var camera = GetViewport().GetCamera3D();
            if (camera == null || !PrepareUvProjector()) return;
            if (!InteractionUnchanged(revision, source) || !HexasphereInputRouter.IsUvProjectionOwner(this)) return;
            SubscribeUvSignals();
            if (!UvProjector.OpenProjectionFor(this)) return;
            // Hover starts empty when changing between the 3D and UV views.
            bool hadHover = _hoveredTileIndex != -1;
            _hoveredTileIndex = -1;
            UvProjector.SetSelection(_selectedTileIndex, -1);
            if (!InteractionUnchanged(revision, source) || !OwnsActiveUv()) return;
            ApplyVisualSelection();
            if (!InteractionUnchanged(revision, source) || !OwnsActiveUv()) return;
            HexasphereInputRouter.EnterUvMode(camera);
            if (hadHover) EmitSignal(SignalName.TileHovered, -1);
        }
        finally
        {
            _uvOpening = false;
            if (GodotObject.IsInstanceValid(this) && HexasphereInputRouter.IsUvProjectionOwner(this)
                && (!GodotObject.IsInstanceValid(UvProjector) || !UvProjector.IsOwnedBy(this)))
            {
                UnsubscribeUvSignals();
                HexasphereInputRouter.OnUvProjectionClosed(this);
            }
        }
    }

    private bool HasInteractionResources() => GodotObject.IsInstanceValid(this) && IsInsideTree()
        && !IsQueuedForDeletion() && _planetReady && _hexasphere != null;
    private bool InteractionUnchanged(long revision, NativeHexasphere source) =>
        HasInteractionResources() && revision == _interactionRevision && ReferenceEquals(source, _hexasphere);
    private bool OwnsActiveUv() => HasInteractionResources() && HexasphereInputRouter.IsUvProjectionOwner(this)
        && GodotObject.IsInstanceValid(UvProjector) && UvProjector.IsOwnedBy(this);

    private void ApplyVisualSelection()
    {
        if (GodotObject.IsInstanceValid(VisualController))
            VisualController.SetSelection(
                IsClickVisualEnabled ? ClickColor : null, _selectedTileIndex,
                IsHoverVisualEnabled ? HoverColor : null, _hoveredTileIndex);
    }

    private void SubscribeUvSignals()
    {
        UnsubscribeUvSignals();
        UvProjector.ProjectionClosed += OnProjectionClosed;
        UvProjector.TileClicked += OnUvTileClicked;
        UvProjector.TileDeselected += OnUvTileDeselected;
        UvProjector.TileHovered += OnUvTileHovered;
    }

    private void UnsubscribeUvSignals()
    {
        if (!GodotObject.IsInstanceValid(UvProjector)) return;
        UvProjector.ProjectionClosed -= OnProjectionClosed;
        UvProjector.TileClicked -= OnUvTileClicked;
        UvProjector.TileDeselected -= OnUvTileDeselected;
        UvProjector.TileHovered -= OnUvTileHovered;
    }

    private void OnUvTileClicked(int tileIndex)
    {
        if (!OwnsActiveUv() || !CanProcess() || !IsClickEnabled || tileIndex < 0 || tileIndex >= TileCount) return;
        var source = _hexasphere;
        long revision = _interactionRevision;
        _selectedTileIndex = tileIndex;
        ApplyVisualSelection();
        HexasphereInputRouter.NotifySelectionChanged(this);
        if (!InteractionUnchanged(revision, source) || !OwnsActiveUv() || _selectedTileIndex != tileIndex) return;
        EmitSignal(SignalName.TileClicked, tileIndex, ToGlobal(GetTileCenter(tileIndex)));
    }

    private void OnUvTileDeselected()
    {
        if (!OwnsActiveUv() || !CanProcess() || !IsClickEnabled) return;
        _selectedTileIndex = -1;
        ApplyVisualSelection();
        EmitSignal(SignalName.TileDeselected);
    }

    private void OnUvTileHovered(int tileIndex)
    {
        if (!OwnsActiveUv() || !CanProcess() || (!IsHoverEnabled && tileIndex >= 0) || tileIndex < -1 || tileIndex >= TileCount
            || tileIndex == _hoveredTileIndex) return;
        _hoveredTileIndex = tileIndex;
        ApplyVisualSelection();
        EmitSignal(SignalName.TileHovered, tileIndex);
    }

    private void OnProjectionClosed()
    {
        if (!HexasphereInputRouter.IsUvProjectionOwner(this)) return;
        UnsubscribeUvSignals();
        HexasphereInputRouter.OnUvProjectionClosed(this);
        ClearHover();
    }

    private void ClearHover()
    {
        if (_hoveredTileIndex == -1) return;
        _hoveredTileIndex = -1;
        ApplyVisualSelection();
        EmitSignal(SignalName.TileHovered, -1);
    }

    private bool TryRaycastToTile(out Vector3 hitPosition, out int tileIndex)
    {
        hitPosition = Vector3.Zero;
        tileIndex = -1;
        if (!_planetReady) return false;

        var camera = GetViewport().GetCamera3D();
        if (camera == null) return false;

        var mousePos = GetViewport().GetMousePosition();
        Vector3 rayOriginWorld = camera.ProjectRayOrigin(mousePos);
        Vector3 origin = ToLocal(rayOriginWorld);
        Vector3 dir    = (ToLocal(rayOriginWorld + camera.ProjectRayNormal(mousePos)) - origin).Normalized();

        if (!RaySphereIntersect(origin, dir, _generatedRadius, out hitPosition))
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
        if (!HasInteractionResources() || HexasphereInputRouter.IsUvProjectionOpenFor(GetViewport())) return;
        var viewport = GetViewport();
        if (!HexasphereInputRouter.IsEligible(this, @event, viewport))
        {
            if (@event is InputEventMouseMotion) ClearHover();
            return;
        }
        var source = _hexasphere;
        long revision = _interactionRevision;

        if (@event is InputEventKey { Pressed: true, Echo: false }
            && @event.IsActionPressed("ui_toggle_uv_map"))
        {
            if (HexasphereInputRouter.FindSphereUnderCursor(@event, viewport.GetMousePosition(), viewport, out _) == this)
                OpenUvProjector();
            return;
        }

        if (@event is InputEventMouse)
        {
            var winner = HexasphereInputRouter.FindSphereUnderCursor(@event, viewport.GetMousePosition(), viewport, out _);
            if (!InteractionUnchanged(revision, source)) return;
            if (winner != this)
            {
                if (@event is InputEventMouseMotion) ClearHover();
                return;
            }
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            if (TryRaycastToTile(out var hitPos, out int idx) && idx >= 0)
            {
                _selectedTileIndex = idx;
                ApplyVisualSelection();
                SyncUvProjectorSelection();
                viewport.SetInputAsHandled();
                HexasphereInputRouter.NotifySelectionChanged(this);
                if (!InteractionUnchanged(revision, source) || _selectedTileIndex != idx) return;
                EmitSignal(SignalName.TileClicked, idx, ToGlobal(hitPos));
            }
            else
            {
                _selectedTileIndex = -1;
                ApplyVisualSelection();
                SyncUvProjectorSelection();
                viewport.SetInputAsHandled();
                EmitSignal(SignalName.TileDeselected);
            }
            return;
        }

        if (@event is InputEventMouseMotion)
        {
            int newHover = TryRaycastToTile(out _, out int idx) ? idx : -1;
            if (newHover != _hoveredTileIndex)
            {
                _hoveredTileIndex = newHover;
                ApplyVisualSelection();
                EmitSignal(SignalName.TileHovered, newHover);
            }
        }
    }

    public override void _Notification(int what)
    {
        if (!IsInsideTree()) return;
        if ((what == NotificationVisibilityChanged && !IsVisibleInTree())
            || ((what == NotificationPaused || what == NotificationDisabled) && !CanProcess()))
        {
            if (HexasphereInputRouter.IsUvProjectionOwner(this)) CloseUvProjectorFromRouter();
            else ClearHover();
        }
    }

    public override void _ExitTree()
    {
        ShutdownGenerationLifecycle();
        try
        {
            HexasphereInputRouter.Unregister(this);
            UnsubscribeUvSignals();
            CleanupResources();
        }
        finally
        {
            // Invalidate even when a custom cleanup omits base or throws.
            InvalidateGenerationRequest();
        }
    }

    virtual protected void BuildSpatialIndex(Vector3[] centers)
    {
        var prepared = _activeGeneration?.SpatialIndex;
        // Keep the virtual hook on the main thread. Custom calls with different
        // centers still build their own index, as they did before preparation.
        if (!MatchesPreparedIndex(prepared, centers))
            prepared = PrepareSpatialIndex(centers, System.Threading.CancellationToken.None);
        _tileDirs = prepared.Directions;
        _bucketScale = prepared.Scale;
        _spatialBuckets = prepared.Buckets;
    }

    private static bool MatchesPreparedIndex(PreparedSpatialIndex prepared, Vector3[] centers)
    {
        if (prepared == null || !ReferenceEquals(prepared.Centers, centers)) return false;
        // Overrides may edit the supplied array in place before calling base.
        // Checking directions avoids rebuilding the dictionary for unchanged data
        // while retaining the old behavior for transformed centers.
        for (int i = 0; i < centers.Length; i++)
            if (centers[i].Normalized() != prepared.Directions[i]) return false;
        return true;
    }

    private Vector3I Quantize(Vector3 v) => Quantize(v, _bucketScale);

    private static Vector3I Quantize(Vector3 v, float scale)
    {
        return new Vector3I(
            (int)Mathf.Round(v.X * scale),
            (int)Mathf.Round(v.Y * scale),
            (int)Mathf.Round(v.Z * scale)
        );
    }

    private sealed record PreparedSpatialIndex(Vector3[] Centers, Vector3[] Directions,
        float Scale, Dictionary<Vector3I, List<int>> Buckets);

    private static PreparedSpatialIndex PrepareSpatialIndex(Vector3[] centers, System.Threading.CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var directions = new Vector3[centers.Length];
        // Preserve the existing bucket density and quantization for picking.
        float scale = Mathf.Sqrt(centers.Length) * 0.35f;
        var buckets = new Dictionary<Vector3I, List<int>>();
        for (int i = 0; i < centers.Length; i++)
        {
            if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
            directions[i] = centers[i].Normalized();
            var key = Quantize(directions[i], scale);
            if (!buckets.TryGetValue(key, out var list))
            {
                list = new List<int>();
                buckets[key] = list;
            }
            list.Add(i);
        }
        token.ThrowIfCancellationRequested();
        return new PreparedSpatialIndex(centers, directions, scale, buckets);
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
        if (camera == null || !_planetReady) return false;
        
        Vector3 rayOrigin = camera.ProjectRayOrigin(screenPos);
        Vector3 rayDir = camera.ProjectRayNormal(screenPos);
        
        // Convert to local space for intersection test
        Vector3 origin = ToLocal(rayOrigin);
        Vector3 dir = (ToLocal(rayOrigin + rayDir) - origin).Normalized();
        
        // Ray-sphere intersection in local space
        float b = origin.Dot(dir);
        float c = origin.Dot(origin) - _generatedRadius * _generatedRadius;
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
        if (_selectedTileIndex == -1) return;
        _selectedTileIndex = -1;
        ApplyVisualSelection();
        SyncUvProjectorSelection();
        EmitSignal(SignalName.TileDeselected);
    }

    private void SyncUvProjectorSelection()
    {
        if (OwnsActiveUv())
            UvProjector.SetSelection(_selectedTileIndex, _hoveredTileIndex);
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
        if (_cellDatas[tileIndex] is not HexCellData hex) return;
        hex.color = color;
        RefreshTileColors();
    }

    /// <summary>Updates supported cells in the supplied prefix; the remaining cells keep their colors.</summary>
    public void SetAllTileColors(Color[] colors)
    {
        if (colors == null) throw new System.ArgumentNullException(nameof(colors));
        if (_cellDatas == null) return;
        int count = Mathf.Min(colors.Length, _cellDatas.Length);
        for (int i = 0; i < count; i++)
            if (_cellDatas[i] is HexCellData hex)
                hex.color = colors[i];
        RefreshTileColors();
    }

    private void RefreshTileColors()
    {
        if (!GodotObject.IsInstanceValid(VisualController) || _cellDatas == null) return;
        var cells = _cellDatas;
        var source = _hexasphere;
        long revision = _interactionRevision;
        var colors = new Color[cells.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = VisualController.GetColor(cells[i]);
            if (!GodotObject.IsInstanceValid(this) || revision != _interactionRevision
                || !ReferenceEquals(cells, _cellDatas)) return;
        }
        _tileColors = colors;
        VisualController.DrawColors(cells);
        if (InteractionUnchanged(revision, source) && ReferenceEquals(_tileColors, colors) && OwnsActiveUv())
            UvProjector.UpdateColors(source, colors);
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
        if (!HexasphereInputRouter.IsUvProjectionOwner(this)) return;
        long revision = _interactionRevision;
        bool hadHover = _hoveredTileIndex != -1;
        _hoveredTileIndex = -1;
        ApplyVisualSelection();
        UnsubscribeUvSignals();
        HexasphereInputRouter.OnUvProjectionClosed(this);
        // Detach and restore the camera before emitting projector close callbacks.
        if (GodotObject.IsInstanceValid(UvProjector) && UvProjector.IsOwnedBy(this))
            UvProjector.CloseProjection();
        if (hadHover && GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion()
            && revision == _interactionRevision && !HexasphereInputRouter.IsUvProjectionOwner(this))
            EmitSignal(SignalName.TileHovered, -1);
    }
}
