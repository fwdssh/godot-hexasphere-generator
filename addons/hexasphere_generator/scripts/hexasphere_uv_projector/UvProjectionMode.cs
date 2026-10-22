/// <summary>How the UV projector displays its cells.</summary>
public enum UvProjectionMode
{
    Auto,
    DetailedMesh,
    Raster
}

/// <summary>Preset sizes for the raster ID and color textures.</summary>
public enum UvRasterQuality
{
    Custom,
    Low,
    Medium,
    High
}

/// <summary>State of the most recent synchronous projection build.</summary>
public enum UvProjectionBuildState
{
    NotBuilt,
    Building,
    Ready,
    Failed
}
