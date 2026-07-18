using Godot;

public partial class Mars : Node3D
{
    [Export] public float OrbitRadius { get; set; } = 430.0f;
    [Export] public float OrbitSpeed { get; set; } = 0.15f;
    [Export] public float RotationSpeed { get; set; } = 0.4f;

    private HexasphereNode _hexasphere;
    private float _orbitAngle = 0.0f;

    public override void _Ready()
    {
        _hexasphere = GetNode<HexasphereNode>("MarsHexasphere");
        _hexasphere.PlanetGenerated += _OnPlanetGenerated;
    }

    public override void _Process(double delta)
    {
        float fDelta = (float)delta;
        _orbitAngle += OrbitSpeed * fDelta;

        Vector3 pos = GlobalPosition;
        pos.X = Mathf.Cos(_orbitAngle) * OrbitRadius;
        pos.Z = Mathf.Sin(_orbitAngle) * OrbitRadius;
        pos.Y = 0.0f;
        GlobalPosition = pos;

        _hexasphere.RotateY(RotationSpeed * fDelta);
    }

    private void _OnPlanetGenerated(int tileCount)
    {
        Color[] colors = new Color[tileCount];
        for (int i = 0; i < tileCount; i++)
        {
            Vector3 center = _hexasphere.GetTileCenter(i);
            colors[i] = _ColorLogic(center);
        }
        _hexasphere.SetAllTileColors(colors);
    }

    private Color _ColorLogic(Vector3 tileCenter)
    {
        Vector3 n = tileCenter.Normalized();

        if (float.IsNaN(n.X) || float.IsNaN(n.Y) || float.IsNaN(n.Z))
        {
            n = Vector3.Up;
        }

        float noiseLarge = Mathf.Sin(n.X * 3.0f) * Mathf.Cos(n.Y * 2.5f) + Mathf.Sin(n.Z * 3.5f) * Mathf.Cos(n.X * 2.2f);
        float noiseMed = Mathf.Sin(n.Y * 12.0f) * Mathf.Cos(n.Z * 10.0f) + Mathf.Sin(n.X * 11.0f) * Mathf.Cos(n.Y * 13.0f);
        float noiseSmall = Mathf.Sin(n.Z * 35.0f) * Mathf.Cos(n.X * 30.0f) + Mathf.Sin(n.Y * 28.0f) * Mathf.Cos(n.Z * 32.0f);

        float mixedNoise = (noiseLarge * 0.5f) + (noiseMed * 0.3f) + (noiseSmall * 0.2f);
        float baseNoise = Mathf.Clamp((mixedNoise + 2.0f) / 4.0f, 0.0f, 1.0f);

        if (float.IsNaN(baseNoise))
        {
            baseNoise = 0.5f;
        }

        float hue = Mathf.Lerp(0.02f, 0.06f, baseNoise);
        float saturation = Mathf.Lerp(0.85f, 0.65f, baseNoise);
        float value = Mathf.Lerp(0.28f, 0.68f, baseNoise);

        float latitude = Mathf.Abs(Mathf.Asin(n.Y));
        if (latitude > 1.22f)
        {
            float capT = (latitude - 1.22f) / (Mathf.Pi / 2.0f - 1.22f);
            capT = Mathf.Clamp(capT, 0.0f, 1.0f);

            float edgeNoise = (Mathf.Sin(n.X * 25.0f) * Mathf.Cos(n.Z * 25.0f) + 1.0f) * 0.5f;
            if (capT * 1.4f + edgeNoise * 0.2f > 0.4f)
            {
                hue = 0.05f;
                saturation = Mathf.Lerp(saturation, 0.08f, capT);
                value = Mathf.Lerp(value, 0.92f, capT);
            }
        }

        return Color.FromHsv(hue, saturation, value);
    }
}