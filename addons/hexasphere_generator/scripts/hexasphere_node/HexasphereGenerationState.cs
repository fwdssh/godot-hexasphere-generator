/// <summary>Lifecycle of the latest generation request.</summary>
public enum HexasphereGenerationState
{
    NotStarted = 0,
    Generating = 1,
    Ready = 2,
    Failed = 3,
    Cancelled = 4
}

/// <summary>Actual generation stages; progress counts completed stages, not elapsed time.</summary>
public enum HexasphereGenerationStage
{
    NotStarted = 0,
    GeneratingTopology = 1,
    CreatingCellData = 2,
    BuildingMesh = 3,
    PreparingVisuals = 4,
    Ready = 5
}

/// <summary>Settings captured on the main thread for one generation request.</summary>
public sealed class HexasphereGenerationSettings
{
    public float Radius { get; }
    public int Subdivision { get; }
    public float HexSize { get; }
    public int Seed { get; }
    public bool Borders { get; }
    public bool Emissive { get; }
    public Godot.Color BorderColor { get; }
    public Godot.Shader ColorsShader { get; }
    public Godot.Shader BordersShader { get; }

    internal HexasphereGenerationSettings(HexasphereNode node)
    {
        Radius = node.PlanetRadius;
        Subdivision = node.SubDivision;
        HexSize = node.HexSize;
        Seed = node.GenerationSeed;
        Borders = node.IsBordering;
        Emissive = node.IsEmissive;
        BorderColor = node.BorderColor;
        ColorsShader = node.ColorsShader;
        BordersShader = node.BordersShader;
    }
}
