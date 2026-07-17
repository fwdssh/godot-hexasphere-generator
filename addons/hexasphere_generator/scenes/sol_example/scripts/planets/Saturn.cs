using Godot;
using System;

public partial class Saturn : Node3D
{
    [Export] public float OrbitRadius { get; set; } = 700.0f;
    [Export] public float OrbitSpeed { get; set; } = 0.025f;
    [Export] public float RotationSpeed { get; set; } = 0.65f;

    private HexasphereNode _hexasphere;
    private float _orbitAngle = 0.0f;

    public override void _Ready()
    {
        _hexasphere = GetNode<HexasphereNode>("SaturnHexasphere");
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

        float bands = Mathf.Sin(n.Y * 14.0f) * 0.5f + Mathf.Sin(n.Y * 26.0f) * 0.3f + Mathf.Sin(n.Y * 4.0f) * 0.2f;
        float haze = Mathf.Sin(Mathf.Atan2(n.X, n.Z) * 4.0f) * 0.03f;

        float baseNoise = Mathf.Clamp((bands + haze + 1.0f) / 2.0f, 0.0f, 1.0f);

        if (float.IsNaN(baseNoise))
        {
            baseNoise = 0.5f;
        }

        float hue = Mathf.Lerp(0.09f, 0.13f, baseNoise);
        float saturation = Mathf.Lerp(0.48f, 0.22f, baseNoise);
        float value = Mathf.Lerp(0.68f, 0.90f, baseNoise);

        return Color.FromHsv(hue, saturation, value);
    }
}