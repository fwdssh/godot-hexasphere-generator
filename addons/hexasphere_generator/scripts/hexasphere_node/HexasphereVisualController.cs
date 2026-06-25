using Godot;
using System.Collections.Generic;
using Godot.Hexasphere;

public partial class HexasphereVisualController : Node
{
    [Signal] public delegate void ShaderReadyEventHandler();


    public Hexasphere Hexasphere { get; private set; }

    private List<int[]>    _tileVertexIndices = new List<int[]>();
    private MeshInstance3D _planetMeshInstance;
    private ArrayMesh      _planetArrayMesh;
    private PlanetBorderRenderer _borderRenderer;

    // Шейдерные ресурсы
    private ShaderMaterial _planetMaterial;
    private ImageTexture   _tileColorTexture;
    private Image          _tileColorImage;
    private int            _tileCount;


    private bool _isBorderVisible = true;

public void SetBorderColor(Color color)=>_borderRenderer?.SetBorderColor(color);


public void ApplyGenerated(Hexasphere hexasphere, ArrayMesh mesh, List<int[]> indices,bool isBorderVisible)
{

    Hexasphere         = hexasphere;
    _planetArrayMesh   = mesh;
    _tileVertexIndices = indices;
    _tileCount         = hexasphere.Tiles.Count;
    _isBorderVisible = isBorderVisible;


    _planetMeshInstance      = new MeshInstance3D();
    _planetMeshInstance.Mesh = _planetArrayMesh;
    _planetMeshInstance.Name = "PlanetMesh";
    AddChild(_planetMeshInstance);

    if (_isBorderVisible)
    _borderRenderer = new PlanetBorderRenderer(this);

    CreateGlobalCollider();

    CallDeferred(MethodName.InitShaderMaterial);
}



private int _texWidth;
private int _texHeight;

private void InitShaderMaterial()
{

    _texWidth  = Mathf.CeilToInt(Mathf.Sqrt(_tileCount));
    _texHeight = Mathf.CeilToInt((float)_tileCount / _texWidth);

    _tileColorImage   = Image.CreateEmpty(_texWidth, _texHeight, false, Image.Format.Rgba8);
    _tileColorTexture = ImageTexture.CreateFromImage(_tileColorImage);

    var shader = GD.Load<Shader>("res://addons/hexasphere_generator/scripts/hexasphere_node/shaders/hexasphere_colors.gdshader");
    _planetMaterial = new ShaderMaterial();
    _planetMaterial.Shader = shader;
    _planetMaterial.SetShaderParameter("tile_colors", _tileColorTexture);
    _planetMaterial.SetShaderParameter("tile_count",  _tileCount);
    _planetMaterial.SetShaderParameter("tex_width",   _texWidth);
    _planetMaterial.SetShaderParameter("roughness",   0.6f);
    _planetMeshInstance.MaterialOverride = _planetMaterial;

        if (_isBorderVisible)
    _borderRenderer.BuildStaticBorders(Hexasphere.Tiles, _planetMaterial);

    EmitSignal(SignalName.ShaderReady);
}

public void Draw(HexCellData[] cellDatas, int selectedIdx = -1)
{
    if (_tileColorImage == null || cellDatas == null || cellDatas.Length == 0) return;


    int safeLength = Mathf.Min(cellDatas.Length, _tileCount);
    for (int i = 0; i < safeLength; i++)
    {
        Color c = cellDatas[i].color;
        int px = i % _texWidth;
        int py = i / _texWidth;
        _tileColorImage.SetPixel(px, py, c);
    }

    _tileColorTexture.Update(_tileColorImage);

    if (_isBorderVisible)
    _borderRenderer.UpdateBorders(
        Hexasphere.Tiles, cellDatas, selectedIdx);

}

    private void CreateGlobalCollider()
    {
        if (_planetMeshInstance?.Mesh == null) return;
        var staticBody     = new StaticBody3D();
        var collisionShape = new CollisionShape3D();
        var concaveShape   = new ConcavePolygonShape3D();
        concaveShape.Data = _planetMeshInstance.Mesh.GetFaces();
        collisionShape.Shape = concaveShape;
        staticBody.AddChild(collisionShape);
        AddChild(staticBody);
    }


}
