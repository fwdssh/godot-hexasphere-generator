using Godot;

public partial class HexasphereVisualController : Node
{




    [Signal] public delegate void ShaderReadyEventHandler();

    public NativeHexasphere Hexasphere { get; private set; }

    private MeshInstance3D _planetMeshInstance;
    private ArrayMesh      _planetArrayMesh;
    private PlanetBorderRenderer _borderRenderer;

    private ShaderMaterial _planetMaterial;
    private ImageTexture   _tileColorTexture;
    private Image          _tileColorImage;
    private int            _tileCount;

    private bool _isBorderVisible = true;
    private float _roughness = 0.6f;


    virtual public Color GetColor(ICellData cellData)
    {
        if (cellData is HexCellData hexCellData)
        return  hexCellData.color;
        else
        return Colors.Black;
    }



    virtual public void SetNativeHexasphere(NativeHexasphere hex)
    {
        Hexasphere = hex;
    }

    virtual public void SetBorderColor(Color color) => _borderRenderer?.SetBorderColor(color);
    virtual public void SetRoughness(float value)
    {
        _roughness = value;
        _planetMaterial?.SetShaderParameter("roughness", value);
    }

    virtual public void ApplyGenerated(ArrayMesh mesh, bool isBorderVisible)
    {
        _planetArrayMesh   = mesh;
        _tileCount         = Hexasphere.GetTileCount();
        _isBorderVisible   = isBorderVisible;

        _planetMeshInstance      = new MeshInstance3D();
        _planetMeshInstance.Mesh = _planetArrayMesh;
        _planetMeshInstance.Name = "PlanetMesh";
        AddChild(_planetMeshInstance);

        if (_isBorderVisible)
            _borderRenderer = new PlanetBorderRenderer(this);

        CallDeferred(MethodName.InitShaderMaterial);
    }

    private int _texWidth;
    private int _texHeight;

    virtual protected void InitShaderMaterial()
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
        _planetMaterial.SetShaderParameter("roughness",     _roughness);
        _planetMaterial.SetShaderParameter("selected_idx",  -1);
        _planetMaterial.SetShaderParameter("hover_idx",  -1);

        _planetMeshInstance.MaterialOverride = _planetMaterial;

        if (_isBorderVisible)
            _borderRenderer.BuildStaticBorders(Hexasphere, _planetMaterial);

        EmitSignal(SignalName.ShaderReady);
    }

    virtual public void DrawColors(ICellData[] cellDatas)
    {
        if (_tileColorImage == null || cellDatas == null || cellDatas.Length == 0) return;

        int safeLength = Mathf.Min(cellDatas.Length, _tileCount);
        var bytes = new byte[_texWidth * _texHeight * 4];
        for (int i = 0; i < safeLength; i++)
        {
            Color c = GetColor(cellDatas[i]);
            int offset = i * 4;
            bytes[offset + 0] = (byte)(c.R * 255);
            bytes[offset + 1] = (byte)(c.G * 255);
            bytes[offset + 2] = (byte)(c.B * 255);
            bytes[offset + 3] = (byte)(c.A * 255);
        }
        var img = Image.CreateFromData(_texWidth, _texHeight, false, Image.Format.Rgba8, bytes);
        _tileColorTexture.Update(img);
    }

    virtual public void SetSelection(Color? selectedColor, int selectedIdx, Color? hoverColor, int hoverIdx)
    {
        if (selectedColor != null)
        {
            _planetMaterial?.SetShaderParameter("selected_color", new Vector4(selectedColor.Value.R, selectedColor.Value.G, selectedColor.Value.B, selectedColor.Value.A));
            _planetMaterial?.SetShaderParameter("selected_idx", selectedIdx);
        }
        if (hoverColor != null)
        {
            _planetMaterial?.SetShaderParameter("hover_color", new Vector4(hoverColor.Value.R, hoverColor.Value.G, hoverColor.Value.B, hoverColor.Value.A));
            _planetMaterial?.SetShaderParameter("hover_idx", hoverIdx);
        }

        if (_isBorderVisible)
            _borderRenderer.UpdateBorders(selectedIdx);
    }

    virtual public void Draw(ICellData[] cellDatas, Color? selectedColor = null, int selectedIdx = -1, Color? hoverColor = null, int hoverIdx = -1)
    {
        DrawColors(cellDatas);
        SetSelection(selectedColor, selectedIdx, hoverColor, hoverIdx);
    }



    virtual public void DisposeHexasphere()
    {
        Hexasphere?.Dispose();
        Hexasphere = null;
    }
}
