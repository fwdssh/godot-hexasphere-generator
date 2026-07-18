using Godot;

class EarthCellData : ICellData
{
    public float Height; 
}

public partial class Earth : HexasphereNode
{
    [Export] public Texture2D EarthMap { get; set; }

    [Export] public float OrbitRadius { get; set; } = 360.0f;
    [Export] public float OrbitSpeed { get; set; } = 0.12f;
    [Export] public float RotationSpeed { get; set; } = 0.3f;

    private float _orbitAngle = 0.0f;
    private EarthCellData[] _cells;

    public override void _Process(double delta)
    {
        float fDelta = (float)delta;
        _orbitAngle += OrbitSpeed * fDelta;

        Vector3 pos = GlobalPosition;
        pos.X = Mathf.Cos(_orbitAngle) * OrbitRadius;
        pos.Z = Mathf.Sin(_orbitAngle) * OrbitRadius;
        pos.Y = 0.0f;
        GlobalPosition = pos;

        RotateY(RotationSpeed * fDelta);
    }

    protected override ICellData[] CreateCellData(int count, Vector3[] centers)
    {
        _cells = new EarthCellData[count];

        Image img = null;
        if (EarthMap != null)
        {
            img = EarthMap.GetImage();
            if (img.IsCompressed()) img.Decompress();
        }

        for (int i = 0; i < count; i++)
        {
            _cells[i] = new EarthCellData();
            Vector3 n = centers[i].Normalized();

            if (float.IsNaN(n.X) || float.IsNaN(n.Y) || float.IsNaN(n.Z))
                n = Vector3.Up;

            if (img != null)
            {
                float longitude = Mathf.Atan2(n.X, n.Z); 
                float latitude = Mathf.Asin(n.Y);       

                float u = (longitude + Mathf.Pi) / (2.0f * Mathf.Pi);
                float v = (latitude + Mathf.Pi / 2.0f) / Mathf.Pi;
                v = 1.0f - v;

                int x = Mathf.Clamp((int)(u * img.GetWidth()), 0, img.GetWidth() - 1);
                int y = Mathf.Clamp((int)(v * img.GetHeight()), 0, img.GetHeight() - 1);

                _cells[i].Height = img.GetPixel(x, y).Luminance;
            }
            else
            {
                _cells[i].Height = 0.5f; 
            }
        }

        return _cells;
    }

    protected override void SetVisualController()
    {
        VisualController = GetNodeOrNull<EarthVisualController>("HexasphereVisual");
        if (VisualController == null)
        {
            VisualController = new EarthVisualController();
            VisualController.Name = "HexasphereVisual";
            AddChild(VisualController);
        }
    }
}