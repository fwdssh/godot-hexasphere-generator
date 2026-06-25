using Godot;
using System.Collections.Generic;

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
        int tileCount = hexasphere.GetTileCount();

        int totalVerts = 0;
        for (int i = 0; i < tileCount; i++)
            totalVerts += hexasphere.GetTilePoints(i).Length * 2;

        var positions = new Vector3[totalVerts];
        var uv2       = new Vector2[totalVerts];

        int vertIdx = 0;
        for (int i = 0; i < tileCount; i++)
        {
            var pts = hexasphere.GetTilePoints(i);
            int ptsCount = pts.Length;
            var tileUV   = new Vector2(i, 0f);

            for (int p = 0; p < ptsCount; p++)
            {
                var p1 = pts[p];
                var p2 = pts[(p + 1) % ptsCount];

                positions[vertIdx]   = p1 * 1.0001f;
                positions[vertIdx+1] = p2 * 1.0001f;
                uv2[vertIdx]         = tileUV;
                uv2[vertIdx+1]       = tileUV;
                vertIdx += 2;
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = positions;
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
