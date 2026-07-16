using Godot;

public partial class HexasphereVisualController : Node3D
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

    private Shader _colorsShader;
    private Shader _bordersShader;
    private bool _isBorderVisible = true;
    private byte[] _colorBuffer;


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

    virtual public void ApplyGenerated(ArrayMesh mesh, bool isBorderVisible, Shader colorsShader, Shader bordersShader)
    {
        _planetArrayMesh   = mesh;
        _tileCount         = Hexasphere.GetTileCount();
        _isBorderVisible   = isBorderVisible;

        _planetMeshInstance      = new MeshInstance3D();
        _planetMeshInstance.Mesh = _planetArrayMesh;
        _planetMeshInstance.Name = "PlanetMesh";
        AddChild(_planetMeshInstance);

        _colorsShader = colorsShader;
        _bordersShader = bordersShader;

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

        var shader = _colorsShader;
        _planetMaterial = new ShaderMaterial();
        _planetMaterial.Shader = shader;
        _planetMaterial.SetShaderParameter("tile_colors", _tileColorTexture);
        _planetMaterial.SetShaderParameter("tile_count",  _tileCount);
        _planetMaterial.SetShaderParameter("tex_width",   _texWidth);
        _planetMaterial.SetShaderParameter("selected_idx",  -1);
        _planetMaterial.SetShaderParameter("hover_idx",  -1);

        _planetMeshInstance.MaterialOverride = _planetMaterial;

        if (_isBorderVisible)
            _borderRenderer.BuildStaticBorders(Hexasphere, _planetMaterial, _bordersShader);

        EmitSignal(SignalName.ShaderReady);
    }

    virtual public void DrawColors(ICellData[] cellDatas)
    {
        if (_tileColorImage == null || cellDatas == null || cellDatas.Length == 0) return;

        int safeLength = Mathf.Min(cellDatas.Length, _tileCount);
        int requiredSize = _texWidth * _texHeight * 4;
        if (_colorBuffer == null || _colorBuffer.Length != requiredSize)
            _colorBuffer = new byte[requiredSize];
        for (int i = 0; i < safeLength; i++)
        {
            Color c = GetColor(cellDatas[i]);
            int offset = i * 4;
            _colorBuffer[offset + 0] = (byte)(c.R * 255);
            _colorBuffer[offset + 1] = (byte)(c.G * 255);
            _colorBuffer[offset + 2] = (byte)(c.B * 255);
            _colorBuffer[offset + 3] = (byte)(c.A * 255);
        }
        var img = Image.CreateFromData(_texWidth, _texHeight, false, Image.Format.Rgba8, _colorBuffer);
        _tileColorTexture.Update(img);
    }

    virtual public void SetSelection(Color? selectedColor, int selectedIdx, Color? hoverColor, int hoverIdx)
    {
        // Always update indices so the shader never holds stale values.
        // When color is null (visual disabled), pass -1 to suppress the color overlay
        // while borders still receive the real index below.
        _planetMaterial?.SetShaderParameter("selected_idx", selectedColor.HasValue ? selectedIdx : -1);
        _planetMaterial?.SetShaderParameter("hover_idx", hoverColor.HasValue ? hoverIdx : -1);

        if (selectedColor != null)
            _planetMaterial?.SetShaderParameter("selected_color", new Vector4(selectedColor.Value.R, selectedColor.Value.G, selectedColor.Value.B, selectedColor.Value.A));
        if (hoverColor != null)
            _planetMaterial?.SetShaderParameter("hover_color", new Vector4(hoverColor.Value.R, hoverColor.Value.G, hoverColor.Value.B, hoverColor.Value.A));

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
