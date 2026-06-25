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
        var data = hexasphere.GetBorderData();
        var positions = (Vector3[])data["positions"];
        var tileLineCounts = (int[])data["tile_line_counts"];

        int tileCount = hexasphere.GetTileCount();
        var vertPositions = new List<Vector3>();
        var uv2 = new List<Vector2>();
        var seenMidpoints = new HashSet<Vector3>();

        int idx = 0;
        for (int i = 0; i < tileCount; i++)
        {
            int count = tileLineCounts[i];
            var tileUV = new Vector2(i, 0f);
            for (int j = 0; j < count; j += 2)
            {
                Vector3 p1 = positions[idx + j];
                Vector3 p2 = positions[idx + j + 1];
                Vector3 mid = (p1 + p2) * 0.5f;
                if (seenMidpoints.Add(mid))
                {
                    vertPositions.Add(p1 * 1.0001f);
                    vertPositions.Add(p2 * 1.0001f);
                    uv2.Add(tileUV);
                    uv2.Add(tileUV);
                }
            }
            idx += count;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertPositions.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV2] = uv2.ToArray();

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
