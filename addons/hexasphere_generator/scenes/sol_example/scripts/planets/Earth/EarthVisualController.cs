using Godot;
using System;

public partial class EarthVisualController : HexasphereVisualController
{
    public override Color GetColor(ICellData cellData)
    {
        if (cellData is EarthCellData hex)
            return _ColorLogic(hex.Height);

        return base.GetColor(cellData);
    }

    private Color _ColorLogic(float height)
    {
        float waterLevel = 0.15f;
        float mountainLevel = 0.55f;
        float snowLevel = 0.88f;

        if (height <= waterLevel)
        {
            float t = waterLevel > 0f ? height / waterLevel : 0f;
            return new Color(0.04f, 0.10f, 0.32f).Lerp(new Color(0.08f, 0.35f, 0.58f), t);
        }

        float landHeight = (height - waterLevel) / (1.0f - waterLevel);

        if (landHeight < mountainLevel)
        {
            float t = landHeight / mountainLevel;
            t = Mathf.Clamp(t, 0.0f, 1.0f);

            Color deepForest = new Color(0.10f, 0.35f, 0.12f);
            Color hillGreen = new Color(0.24f, 0.46f, 0.15f);

            return deepForest.Lerp(hillGreen, t);
        }

        if (landHeight < snowLevel)
        {
            float t = (landHeight - mountainLevel) / (snowLevel - mountainLevel);
            return new Color(0.36f, 0.32f, 0.28f).Lerp(new Color(0.48f, 0.45f, 0.42f), t);
        }

        return new Color(0.94f, 0.94f, 0.96f);
    }
}