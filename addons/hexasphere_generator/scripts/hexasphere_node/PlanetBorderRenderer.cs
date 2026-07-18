using Godot;
using System.Collections.Generic;

/// <summary>
/// Renders the borders between adjacent hexagonal tiles using a line mesh.
/// Shares the tile color texture with the planet material for consistent coloring.
/// </summary>
public class PlanetBorderRenderer
{
    private MeshInstance3D _bordersMeshInstance;
    private ShaderMaterial _borderMaterial;
    private Color _borderColor;

    /// <summary>
    /// Creates a new border renderer and adds the border mesh as a child of the specified parent node.
    /// </summary>
    /// <param name="parent">The parent node to attach the border mesh to.</param>
    public PlanetBorderRenderer(Node3D parent)
    {
        _bordersMeshInstance      = new MeshInstance3D();
        _bordersMeshInstance.Name = "BordersMesh";
        parent.AddChild(_bordersMeshInstance);
    }

    /// <summary>Shows or hides the border mesh.</summary>
    /// <param name="visible">True to show borders, false to hide.</param>
    public virtual void SetVisible(bool visible) => _bordersMeshInstance.Visible = visible;

    /// <summary>
    /// Builds the static border line mesh by deduplicating shared edges between adjacent tiles.
    /// </summary>
    /// <param name="hexasphere">The native hexasphere providing border data.</param>
    /// <param name="planetMaterial">The planet material to share texture parameters from.</param>
    /// <param name="borderShader">The shader to use for border rendering.</param>
    public virtual void BuildStaticBorders(NativeHexasphere hexasphere, ShaderMaterial planetMaterial, Shader borderShader)
    {
        var data = hexasphere.GetBorderData();
        var positions = (Vector3[])data["positions"];
        var tileLineCounts = (int[])data["tile_line_counts"];

        int tileCount = hexasphere.GetTileCount();
        var vertPositions = new List<Vector3>();
        List<Vector2> uv2 = new List<Vector2>();
        var edgeFirstOwner = new Dictionary<Vector3, int>();
        var edgeVertexIndex = new Dictionary<Vector3, int>();

        int idx = 0;
        for (int i = 0; i < tileCount; i++)
        {
            int count = tileLineCounts[i];
            for (int j = 0; j < count; j += 2)
            {
                Vector3 p1 = positions[idx + j];
                Vector3 p2 = positions[idx + j + 1];
                Vector3 mid = (p1 + p2) * 0.5f;
                var snappedMid = SnapToGrid(mid, 0.001f);
                if (!edgeFirstOwner.TryGetValue(snappedMid, out int firstOwner))
                {
                    edgeFirstOwner[snappedMid] = i;
                    edgeVertexIndex[snappedMid] = vertPositions.Count;
                    vertPositions.Add(p1 * 1.0001f);
                    vertPositions.Add(p2 * 1.0001f);
                    uv2.Add(new Vector2(i, -1));
                    uv2.Add(new Vector2(i, -1));
                }
                else
                {
                    int vertIdx = edgeVertexIndex[snappedMid];
                    uv2[vertIdx] = new Vector2(firstOwner, i);
                    uv2[vertIdx + 1] = new Vector2(firstOwner, i);
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

        var shader = borderShader;
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

    /// <summary>
    /// Updates the border shader with the currently selected tile index for highlighting.
    /// </summary>
    /// <param name="selectedIdx">The selected tile index, or -1 for none.</param>
    public virtual void UpdateBorders(int selectedIdx = -1)
    {
        _borderMaterial?.SetShaderParameter("selected_idx", selectedIdx);
    }

    /// <summary>Sets the color used for rendering tile borders.</summary>
    /// <param name="color">The new border color.</param>
    public virtual void SetBorderColor(Color color)
    {
        _borderColor = color;
        _borderMaterial?.SetShaderParameter("border_color", color);
    }

    private static Vector3 SnapToGrid(Vector3 v, float gridSize)
    {
        return new Vector3(
            Mathf.Round(v.X / gridSize) * gridSize,
            Mathf.Round(v.Y / gridSize) * gridSize,
            Mathf.Round(v.Z / gridSize) * gridSize
        );
    }
}
