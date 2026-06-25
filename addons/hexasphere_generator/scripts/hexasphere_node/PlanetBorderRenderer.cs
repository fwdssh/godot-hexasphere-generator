using Godot;

public class PlanetBorderRenderer
{
    private MeshInstance3D _bordersMeshInstance;
    private ShaderMaterial _borderMaterial;
    private Color _borderColor;

    public PlanetBorderRenderer(Node parent)
    {
        _bordersMeshInstance      = new MeshInstance3D();
        _bordersMeshInstance.Name = "BordersMesh";
        parent.AddChild(_bordersMeshInstance);
    }

    public void SetVisible(bool visible) => _bordersMeshInstance.Visible = visible;

    public void BuildStaticBorders(NativeHexasphere hexasphere, ShaderMaterial planetMaterial)
    {
        var data = hexasphere.GetBorderData();
        var positions = (Vector3[])data["positions"];
        var tileLineCounts = (int[])data["tile_line_counts"];

        int totalVerts = positions.Length;
        var vertPositions = new Vector3[totalVerts];
        var uv2 = new Vector2[totalVerts];

        int tileCount = hexasphere.GetTileCount();
        int idx = 0;
        for (int i = 0; i < tileCount; i++)
        {
            int count = tileLineCounts[i];
            var tileUV = new Vector2(i, 0f);
            for (int j = 0; j < count; j++)
            {
                vertPositions[idx] = positions[idx] * 1.0001f;
                uv2[idx] = tileUV;
                idx++;
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertPositions;
        arrays[(int)Mesh.ArrayType.TexUV2] = uv2;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);

        var shader = GD.Load<Shader>("res://addons/hexasphere_generator/scripts/hexasphere_node/shaders/hexasphere_borders.gdshader");
        _borderMaterial = new ShaderMaterial();
        _borderMaterial.Shader = shader;

        _borderMaterial.SetShaderParameter("tile_colors",
            planetMaterial.GetShaderParameter("tile_colors"));
        _borderMaterial.SetShaderParameter("tile_count",
            planetMaterial.GetShaderParameter("tile_count"));
        _borderMaterial.SetShaderParameter("tex_width",
            planetMaterial.GetShaderParameter("tex_width"));
        _borderMaterial.SetShaderParameter("selected_idx", -1);
        _borderMaterial.SetShaderParameter("border_color", _borderColor);

        _bordersMeshInstance.Mesh             = mesh;
        _bordersMeshInstance.MaterialOverride = _borderMaterial;
    }

    public void UpdateBorders(NativeHexasphere hexasphere, HexCellData[] cellDatas, int selectedIdx = -1)
    {
        _borderMaterial?.SetShaderParameter("selected_idx", selectedIdx);
    }

    public void SetBorderColor(Color color)
    {
        _borderColor = color;
        _borderMaterial?.SetShaderParameter("border_color", color);
    }
}
