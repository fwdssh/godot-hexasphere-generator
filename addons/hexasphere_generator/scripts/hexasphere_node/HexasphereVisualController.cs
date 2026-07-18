using Godot;

/// <summary>
/// Manages the visual rendering of the hexasphere, including the planet mesh, tile colors,
/// selection/hover highlighting, and border rendering.
/// </summary>
public partial class HexasphereVisualController : Node3D
{
    /// <summary>
    /// Emitted when the shader material has been initialized and colors can be drawn.
    /// </summary>
    [Signal] public delegate void ShaderReadyEventHandler();

    /// <summary>The NativeHexasphere instance used for geometry queries.</summary>
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


    /// <summary>
    /// Extracts the display color from a cell data object.
    /// </summary>
    /// <param name="cellData">The cell data to sample.</param>
    /// <returns>The display color for the tile.</returns>
    virtual public Color GetColor(ICellData cellData)
    {
        if (cellData is HexCellData hexCellData)
        return  hexCellData.color;
        else
        return Colors.Black;
    }



    /// <summary>
    /// Sets the NativeHexasphere instance used for geometry queries.
    /// </summary>
    /// <param name="hex">The native hexasphere instance.</param>
    virtual public void SetNativeHexasphere(NativeHexasphere hex)
    {
        Hexasphere = hex;
    }

    /// <summary>Sets the color of the tile borders.</summary>
    /// <param name="color">The border color.</param>
    virtual public void SetBorderColor(Color color) => _borderRenderer?.SetBorderColor(color);

    /// <summary>Enables or disables emissive rendering on the planet material.</summary>
    /// <param name="emissive">True to enable emissive, false to disable.</param>
    virtual public void SetEmissive(bool emissive)
    {
        if (_planetMaterial != null)
            _planetMaterial.SetShaderParameter("is_emissive", emissive);
    }

    /// <summary>
    /// Applies the generated mesh and configures rendering with the specified shaders.
    /// </summary>
    /// <param name="mesh">The generated planet mesh.</param>
    /// <param name="isBorderVisible">Whether tile borders should be visible.</param>
    /// <param name="colorsShader">Shader for tile coloring.</param>
    /// <param name="bordersShader">Shader for tile borders.</param>
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

    /// <summary>
    /// Draws all tile colors onto the color texture used by the shader.
    /// </summary>
    /// <param name="cellDatas">Array of cell data for all tiles.</param>
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

    /// <summary>
    /// Updates the selection and hover highlight indices and colors on the GPU material.
    /// </summary>
    /// <param name="selectedColor">Color for the selected tile, or null to disable.</param>
    /// <param name="selectedIdx">Index of the selected tile, or -1.</param>
    /// <param name="hoverColor">Color for the hovered tile, or null to disable.</param>
    /// <param name="hoverIdx">Index of the hovered tile, or -1.</param>
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

    /// <summary>
    /// Convenience method that draws colors and sets selection/hover state in one call.
    /// </summary>
    /// <param name="cellDatas">Array of cell data for all tiles.</param>
    /// <param name="selectedColor">Color for the selected tile, or null.</param>
    /// <param name="selectedIdx">Index of the selected tile, or -1.</param>
    /// <param name="hoverColor">Color for the hovered tile, or null.</param>
    /// <param name="hoverIdx">Index of the hovered tile, or -1.</param>
    virtual public void Draw(ICellData[] cellDatas, Color? selectedColor = null, int selectedIdx = -1, Color? hoverColor = null, int hoverIdx = -1)
    {
        DrawColors(cellDatas);
        SetSelection(selectedColor, selectedIdx, hoverColor, hoverIdx);
    }



    /// <summary>
    /// Disposes the native hexasphere resources and clears the reference.
    /// </summary>
    virtual public void DisposeHexasphere()
    {
        Hexasphere?.Dispose();
        Hexasphere = null;
    }
}
