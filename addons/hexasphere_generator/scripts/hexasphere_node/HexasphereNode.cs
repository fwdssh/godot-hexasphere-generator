using Godot;
using Godot.Hexasphere;
using System.Threading.Tasks;
public partial class HexasphereNode : Node3D
{
    [ExportGroup("Geometry")]
    [Export] public float PlanetRadius = 20;
    [Export] public int SubDivision = 20;

    [ExportGroup("Visual")]
    [Export(PropertyHint.Range, "0.1, 1.0")] public float HexSize = 1f;
    [Export(PropertyHint.Range, "0.0, 1.0")] public float Roughness = 0.6f;

    [ExportGroup("use it if HexSize =1f and u need borders")]
    [Export] public bool IsBordering = true;
    [Export] public Color BorderColor = Colors.White;

    private HexasphereVisualController VisualController;

    private HexCellData[] _cellDatas;
    private int _selectedTileIndex = -1;
    private bool _planetReady = false;

    private NativeHexasphere _pendingHexasphere;
    private ArrayMesh        _pendingMesh;
    private HexCellData[]       _pendingCellDatas;

    private Vector3[] _tileDirs;

    public override void _Ready()
    {
        VisualController = GetNode<HexasphereVisualController>("HexasphereVisual");
        Task.Run(GeneratePlanetAsync);
    }

    private void GeneratePlanetAsync()
{
    var hexasphere = new NativeHexasphere();
    hexasphere.Generate(PlanetRadius, SubDivision, HexSize);

    var result = hexasphere.BuildMesh();
    var mesh = (ArrayMesh)result["mesh"];

    var rng = new RandomNumberGenerator();
    rng.Randomize();

    int tileCount = hexasphere.GetTileCount();
    var cellDatas = new HexCellData[tileCount];
    for (int i = 0; i < cellDatas.Length; i++)
    {
        cellDatas[i] = new HexCellData
        {
            color = Color.FromHsv(rng.Randf(), 0.6f, 0.85f)
        };
    }

    _pendingHexasphere = hexasphere;
    _pendingMesh       = mesh;
    _pendingCellDatas  = cellDatas;

    CallDeferred(MethodName.FinalizePlanet);
}

private void FinalizePlanet()
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
private void OnShaderReady()
{
    VisualController.ShaderReady -= OnShaderReady;
    VisualController.Draw(_cellDatas, _selectedTileIndex);
    VisualController.DisposeHexasphere();
    _planetReady = true;
}

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
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
                    VisualController.Draw(_cellDatas, _selectedTileIndex);
                }
            }
        }
    }

    private void BuildSpatialIndex(NativeHexasphere hexasphere)
    {
        int count = hexasphere.GetTileCount();
        _tileDirs = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            var cp = hexasphere.GetTileCenter(i);
            _tileDirs[i] = cp.Normalized();
        }
    }

    private int FindTileIndexByDirection(Vector3 direction)
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
