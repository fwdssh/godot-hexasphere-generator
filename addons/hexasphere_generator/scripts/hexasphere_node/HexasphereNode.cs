using Godot;
using System.Threading.Tasks;
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
    [Export(PropertyHint.Range, "0.0, 1.0")] public float Roughness = 0.6f;


    [Export] public bool IsClickEnabled = true;
    [Export] public bool IsClickVisualEnabled = true;
    [Export] public Color ClickColor = Colors.Black;


    [Export] public bool IsHoverEnabled = true;
    [Export] public bool IsHoverVisualEnabled = true;
    [Export] public Color HoverColor = Colors.Red;



    [ExportGroup("use it if HexSize =1f and u need borders")]
    [Export] public bool IsBordering = true;
    [Export] public Color BorderColor = Colors.White;











    private HexasphereVisualController VisualController;
    private ICellData[] _cellDatas;
    private int _selectedTileIndex = -1;
    private int _hoveredTileIndex = -1;
    private bool _planetReady = false;

    private NativeHexasphere _pendingHexasphere;
    private ArrayMesh        _pendingMesh;
    private ICellData[]       _pendingCellDatas;
    private Vector3[] _tileDirs;

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
        Task.Run(GeneratePlanetAsync);
    }

    virtual protected void GeneratePlanetAsync()
{
    var hexasphere = new NativeHexasphere();
    hexasphere.Generate(PlanetRadius, SubDivision, HexSize);

    var result = hexasphere.BuildMesh();
    var mesh = (ArrayMesh)result["mesh"];



    int tileCount = hexasphere.GetTileCount();
    var cellDatas = CreateCellData(tileCount);


    _pendingHexasphere = hexasphere;
    _pendingMesh       = mesh;
    _pendingCellDatas  = cellDatas;

    CallDeferred(MethodName.FinalizePlanet);
}

virtual protected void FinalizePlanet()
{

    _cellDatas = _pendingCellDatas;
    VisualController.SetNativeHexasphere(_pendingHexasphere);
    VisualController.ApplyGenerated(_pendingMesh, IsBordering);
    VisualController.SetBorderColor(BorderColor);
    VisualController.SetRoughness(Roughness);
    BuildSpatialIndex(_pendingHexasphere);


    _pendingHexasphere = null;
    _pendingMesh       = null;
    _pendingCellDatas  = null;

    VisualController.ShaderReady += OnShaderReady;
}
virtual protected void OnShaderReady()
{
    VisualController.ShaderReady -= OnShaderReady;
    VisualController.Draw(_cellDatas);
    VisualController.DisposeHexasphere();
    _planetReady = true;
}

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } && IsClickEnabled)
        {
            var camera = GetViewport().GetCamera3D();
            var space = GetWorld3D().DirectSpaceState;
            var mousePos = GetViewport().GetMousePosition();
            var origin = camera.ProjectRayOrigin(mousePos);
            var end = origin + camera.ProjectRayNormal(mousePos) * 10000f;
            var query = PhysicsRayQueryParameters3D.Create(origin, end);
            var result = space.IntersectRay(query);
            if (result.Count > 0)
            {
                var dir = ((Vector3)result["position"]).Normalized();
                int idx = FindTileIndexByDirection(dir);
                if (idx >= 0)
                {
                    _selectedTileIndex = idx;

                    EmitSignal(SignalName.TileClicked, idx, (Vector3)result["position"]);
                    VisualController.Draw(_cellDatas,
                        IsClickVisualEnabled ? ClickColor : null, _selectedTileIndex,
                        IsHoverVisualEnabled ? HoverColor : null, _hoveredTileIndex);

                }
                else
                {
                    _selectedTileIndex = -1;
                    EmitSignal(SignalName.TileDeselected);
                    VisualController.Draw(_cellDatas,
                        ClickColor, -1,
                        IsHoverVisualEnabled ? HoverColor : null, _hoveredTileIndex);
                }
            }
            else
            {
                _selectedTileIndex = -1;
                EmitSignal(SignalName.TileDeselected);
                VisualController.Draw(_cellDatas,
                    ClickColor, -1,
                    IsHoverVisualEnabled ? HoverColor : null, _hoveredTileIndex);
            }
        }



        if (@event is InputEventMouseMotion && IsHoverEnabled)
    {
        var camera = GetViewport().GetCamera3D();
        var space = GetWorld3D().DirectSpaceState;
        var mousePos = GetViewport().GetMousePosition();
        var origin = camera.ProjectRayOrigin(mousePos);
        var end = origin + camera.ProjectRayNormal(mousePos) * 10000f;
        var query = PhysicsRayQueryParameters3D.Create(origin, end);
        var result = space.IntersectRay(query);
        int newHover = -1;
        if (result.Count > 0)
        {
            var dir = ((Vector3)result["position"]).Normalized();
            newHover = FindTileIndexByDirection(dir);
        }
        if (newHover != _hoveredTileIndex)
        {
            _hoveredTileIndex = newHover;
            EmitSignal(SignalName.TileHovered, newHover);
            VisualController.Draw(_cellDatas,
                IsClickVisualEnabled ? ClickColor : null, _selectedTileIndex,
                IsHoverVisualEnabled ? HoverColor : null, _hoveredTileIndex);

        }
    }

    }

    virtual protected void BuildSpatialIndex(NativeHexasphere hexasphere)
    {
        int count = hexasphere.GetTileCount();
        _tileDirs = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            var cp = hexasphere.GetTileCenter(i);
            _tileDirs[i] = cp.Normalized();
        }
    }

    virtual protected int FindTileIndexByDirection(Vector3 direction)
    {
        if (_tileDirs == null) return -1;

        Vector3 normDir = direction.Normalized();
        int bestIndex = -1;
        float maxDot = -2f;

        for (int i = 0; i < _tileDirs.Length; i++)
        {
            float d = normDir.Dot(_tileDirs[i]);
            if (d > maxDot)
            {
                maxDot = d;
                bestIndex = i;
            }
        }

        return bestIndex;
    }


}
