using Godot;
using Godot.Hexasphere;

public partial class HexasphereExample : Node3D
{

    [Export] private float _radius = 2f;
    [Export] private int _subDivision = 20;
    [Export] private float _hexSize = 0.95f;
    [Export] private Color _сolor = Colors.Green;         
    public override void _Ready()
    {
        Hexasphere hexasphere = new Hexasphere(_radius, _subDivision, _hexSize);
        
        ArrayMesh mesh = BuildMesh(hexasphere);

        var meshInstance = new MeshInstance3D 
        { 
            Mesh = mesh, 
            Name = "PlanetMesh" 
        };

        var material = new StandardMaterial3D();
        material.AlbedoColor = _сolor;
        meshInstance.SetSurfaceOverrideMaterial(0, material);

        AddChild(meshInstance);
    }

    private ArrayMesh BuildMesh(Hexasphere hexasphere)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        foreach (var tile in hexasphere.Tiles)
        {
            foreach (var face in tile.Faces)
            {
                AddVertex(st, face.Points[0]);
                AddVertex(st, face.Points[2]);
                AddVertex(st, face.Points[1]);
            }
        }
        st.GenerateNormals();
        return (ArrayMesh)st.Commit();
    }

    private void AddVertex(SurfaceTool st, Point p)
    {
        st.AddVertex(new Vector3((float)p.Position.X, (float)p.Position.Y, (float)p.Position.Z));
    }
}