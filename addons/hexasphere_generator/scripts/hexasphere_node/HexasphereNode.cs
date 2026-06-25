using Godot;
using System.Collections.Generic;
using Godot.Hexasphere;
using System.Threading.Tasks;
public partial class HexasphereNode : Node3D
{
    [ExportGroup("Geometry")]
    [Export] public float PlanetRadius = 20;
    [Export] public int SubDivision = 20;

    [ExportGroup("Visual")]
    [Export(PropertyHint.Range, "0.1, 1.0")] public float HexSize = 1f;

    //USE WITH HexSize = 1f;
    [ExportGroup("use it if HexSize =1f and u need borders")]
    [Export] public bool IsBordering = true;
    [Export] public Color BorderColor = Colors.White;



    private HexasphereVisualController VisualController;


    private HexCellData[] _cellDatas;
    private int _selectedTileIndex = -1;
    private bool _planetReady = false;



      // Временное хранилище результатов фонового потока.
    // Поля пишутся из Task.Run, читаются в FinalizePlanet() (главный поток).
    // Запись происходит до CallDeferred, чтение — после: гонки нет.
    private NativeHexasphere _pendingHexasphere;
    private ArrayMesh        _pendingMesh;
    private List<int[]>      _pendingIndices;
    private HexCellData[]       _pendingCellDatas;

    // ---------------------------------------------------------------
    // Октанты для быстрого поиска тайла (O(N/8) вместо O(N))
    // ---------------------------------------------------------------
    private List<int>[] _octants;
    private Vector3[]   _tileDirs; // кешированные нормализованные направления



    public override void _Ready()
    {
        VisualController = GetNode<HexasphereVisualController>("HexasphereVisual");
        Task.Run(GeneratePlanetAsync);
    }

    private void GeneratePlanetAsync()
{
    var hexasphere = new NativeHexasphere();
    hexasphere.Generate(PlanetRadius, SubDivision, HexSize);

    var builder = new HexasphereMeshBuilder();
    var (mesh, indices) = builder.BuildNative(hexasphere);

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
    _pendingIndices    = indices;
    _pendingCellDatas  = cellDatas;

    CallDeferred(MethodName.FinalizePlanet);
}


private void FinalizePlanet()
{

    _cellDatas = _pendingCellDatas;
    VisualController.ApplyGenerated(_pendingMesh, _pendingIndices, IsBordering);
    VisualController.SetBorderColor(BorderColor);
    BuildSpatialIndex(_pendingHexasphere);


    _pendingHexasphere = null;
    _pendingMesh       = null;
    _pendingIndices    = null;
    _pendingCellDatas  = null;

    VisualController.ShaderReady += OnShaderReady;
}
private void OnShaderReady()
{
    VisualController.ShaderReady -= OnShaderReady;
    VisualController.Draw(_cellDatas, _selectedTileIndex);
    _planetReady = true;
}

    // ---------------------------------------------------------------
    // Пространственный индекс: разбиваем тайлы по 8 октантам.
    // При поиске перебираем только 1/8 от общего числа тайлов.
    // ---------------------------------------------------------------
    private void BuildSpatialIndex(NativeHexasphere hexasphere)
    {
        int count = hexasphere.GetTileCount();

        _octants  = new List<int>[8];
        _tileDirs = new Vector3[count];

        for (int i = 0; i < 8; i++)
            _octants[i] = new List<int>(count / 6);

        for (int i = 0; i < count; i++)
        {
            var cp = hexasphere.GetTileCenter(i);
            var dir = cp.Normalized();
            _tileDirs[i] = dir;
            _octants[GetOctant(dir)].Add(i);
        }
    }

    // Кодируем октант тремя битами по знаку X, Y, Z
    private static int GetOctant(Vector3 v) =>
        (v.X >= 0 ? 4 : 0) | (v.Y >= 0 ? 2 : 0) | (v.Z >= 0 ? 1 : 0);

    // ---------------------------------------------------------------
    // Быстрый поиск тайла: ищем только в нужном октанте
    // ---------------------------------------------------------------
    private int FindTileIndexByDirection(Vector3 direction)
    {
        if (_tileDirs == null || _octants == null) return -1;

        Vector3 normDir = direction.Normalized();
        var candidates  = _octants[GetOctant(normDir)];

        int   bestIndex = -1;
        float maxDot    = -2f;

        for (int k = 0; k < candidates.Count; k++)
        {
            int i   = candidates[k];
            float d = normDir.Dot(_tileDirs[i]);
            if (d > maxDot)
            {
                maxDot    = d;
                bestIndex = i;
            }
        }

        return bestIndex;
    }


}
