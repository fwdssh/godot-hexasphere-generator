using Godot;
using System;

public partial class Jupiter : Node3D
{
    [Export] public float OrbitRadius { get; set; } = 550.0f;
    [Export] public float OrbitSpeed { get; set; } = 0.04f;
    [Export] public float RotationSpeed { get; set; } = 0.7f;

    private HexasphereNode _hexasphere;
    private float _orbitAngle = 0.0f;

    public override void _Ready()
    {
        _hexasphere = GetNode<HexasphereNode>("JupiterHexasphere");
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

        float longitudeAngle = Mathf.Atan2(n.X, n.Z);

        float bands = Mathf.Sin(n.Y * 16.0f) * 0.5f + Mathf.Sin(n.Y * 32.0f) * 0.2f + Mathf.Sin(n.Y * 6.0f) * 0.3f;
        float turbulence = Mathf.Sin(longitudeAngle * 6.0f + n.Y * 10.0f) * 0.12f + Mathf.Sin(longitudeAngle * 18.0f) * 0.04f;

        float mixedNoise = (bands + turbulence + 1.0f) / 2.0f;
        float baseNoise = Mathf.Clamp(mixedNoise, 0.0f, 1.0f);

        if (float.IsNaN(baseNoise))
        {
            baseNoise = 0.5f;
        }

        float hue = Mathf.Lerp(0.04f, 0.08f, baseNoise);
        float saturation = Mathf.Lerp(0.70f, 0.15f, baseNoise);
        float value = Mathf.Lerp(0.38f, 0.92f, baseNoise);

        float lat = Mathf.Asin(n.Y);
        float lon = longitudeAngle;
        float spotDist = Mathf.Sqrt(Mathf.Pow(lat + 0.35f, 2.0f) + Mathf.Pow(Mathf.Atan2(Mathf.Sin(lon - 1.0f), Mathf.Cos(lon - 1.0f)), 2.0f));
        
        if (spotDist < 0.22f)
        {
            float spotT = 1.0f - (spotDist / 0.22f);
            hue = Mathf.Lerp(hue, 0.01f, spotT);
            saturation = Mathf.Lerp(saturation, 0.85f, spotT);
            value = Mathf.Lerp(value, 0.55f, spotT);
        }

        return Color.FromHsv(hue, saturation, value);
    }
}