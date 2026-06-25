using Godot;
using Godot.Hexasphere;
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

    // Вызывается один раз. Принимает те же параметры шейдера что и планета.
    public void BuildStaticBorders(List<Tile> tiles, ShaderMaterial planetMaterial)
    {
        int totalVerts = 0;
        for (int i = 0; i < tiles.Count; i++)
            totalVerts += tiles[i].Points.Count * 2;

        var positions = new Vector3[totalVerts];
        var uv2       = new Vector2[totalVerts];

        int vertIdx = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            var pts      = tiles[i].Points;
            int ptsCount = pts.Count;
            var tileUV   = new Vector2(i, 0f);

            for (int p = 0; p < ptsCount; p++)
            {
                var p1 = pts[p].Position;
                var p2 = pts[(p + 1) % ptsCount].Position;

                positions[vertIdx]   = new Vector3((float)p1.X, (float)p1.Y, (float)p1.Z) * 1.0001f;
                positions[vertIdx+1] = new Vector3((float)p2.X, (float)p2.Y, (float)p2.Z) * 1.0001f;
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

        // Шейдер границ читает ту же текстуру цветов что и планета
        var shader = GD.Load<Shader>("res://addons/hexasphere_generator/scripts/hexasphere_node/shaders/hexasphere_borders.gdshader");
        _borderMaterial = new ShaderMaterial();
        _borderMaterial.Shader = shader;

        // Копируем параметры из материала планеты — одна текстура на обоих
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

    // Теперь UpdateBorders только меняет один uniform — мгновенно
    public void UpdateBorders(List<Tile> tiles, HexCellData[] cellDatas, int selectedIdx = -1)
    {
        _borderMaterial?.SetShaderParameter("selected_idx", selectedIdx);
    }

    public void SetBorderColor(Color color)
{
    _borderColor = color;
    _borderMaterial?.SetShaderParameter("border_color", color);
}
}