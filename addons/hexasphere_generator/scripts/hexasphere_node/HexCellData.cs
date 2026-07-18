using Godot;

/// <summary>
/// Default implementation of ICellData as a Resource, storing a per-tile color value.
/// </summary>
public partial class HexCellData : Resource, ICellData
{
   /// <summary>The display color for this tile.</summary>
   public Color color {get; set;}
}
