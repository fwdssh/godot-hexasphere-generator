using Godot;

public partial class Uran : HexasphereNode
{
    [Export] public float OrbitRadius { get; set; } = 850.0f;
    [Export] public float OrbitSpeed { get; set; } = 0.012f;
    [Export] public float RotationSpeed { get; set; } = 0.55f;

    private float _orbitAngle = 0.0f;

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
        var data = new HexCellData[count];
        for (int i = 0; i < count; i++)
        {
            var n = centers[i].Normalized();
            float latitude = Mathf.Asin(n.Y);
            
            float bandPrimary = Mathf.Sin(latitude * 10f);
            float bandSecondary = Mathf.Cos(latitude * 18f + n.X * 2f); 
            float band = (bandPrimary * 0.7f + bandSecondary * 0.3f) * 0.5f + 0.5f; 
            
            float polarHood = Mathf.Pow(Mathf.Abs(n.Y), 2.5f); 
            
            float hue = Mathf.Lerp(0.53f, 0.56f, band);
            
            float sat = Mathf.Lerp(0.30f, 0.40f, band);
            sat = Mathf.Lerp(sat, 0.15f, polarHood); 
            
            float val = Mathf.Lerp(0.85f, 0.92f, band);
            val = Mathf.Lerp(val, 0.98f, polarHood); 
            
            data[i] = new HexCellData { color = Color.FromHsv(hue, sat, val) };
        }
        return data;
    }
}