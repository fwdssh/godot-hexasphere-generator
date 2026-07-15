using Godot;

public partial class UvCamera2D : Camera2D
{
    [Export] public float TargetZoom = 1.0f;
    [Export] public float ZoomFactor = 1.1f;
    [Export] public float MinZoom = 0.1f;
    [Export] public float MaxZoom = 3.0f;

    private bool _isDragging = false;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        GD.Print($"[UvCamera2D] _Ready - Initial Position: {Position}, Zoom: {Zoom}, GlobalPosition: {GlobalPosition}");
    }

    public override void _Process(double delta)
    {
        var projector = GetParent<HexasphereProjectorController>();
        if (projector != null && !projector.Visible) return;
        
        Zoom = Zoom.Lerp(new Vector2(TargetZoom, TargetZoom), (float)delta * 10f);
        
        if (Engine.GetFramesDrawn() % 60 == 0)
        {
            GD.Print($"[UvCamera2D] _Process - Position: {Position}, Zoom: {Zoom}, TargetZoom: {TargetZoom}, GlobalPosition: {GlobalPosition}");
            GD.Print($"[UvCamera2D] Camera - Current: {IsCurrent()}, Offset: {Offset}");
            
            // Check if mesh is visible
            if (projector != null && projector.MeshInstance2D != null)
            {
                var mesh = projector.MeshInstance2D;
                GD.Print($"[UvCamera2D] Mesh - Visible: {mesh.Visible}, Mesh: {mesh.Mesh != null}, Material: {mesh.Material != null}");
                GD.Print($"[UvCamera2D] Mesh - GlobalPosition: {mesh.GlobalPosition}, ZIndex: {mesh.ZIndex}");
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        var projector = GetParent<HexasphereProjectorController>();
        if (projector != null && !projector.Visible) return;
        
        GD.Print($"[UvCamera2D] Input received: {@event.GetType().Name}");
        
        if (@event is InputEventMouseButton mouseButton)
        {
            GD.Print($"[UvCamera2D] MouseButton: {mouseButton.ButtonIndex}, Pressed: {mouseButton.Pressed}");
            
            if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                _isDragging = mouseButton.Pressed;
                GetViewport().SetInputAsHandled();
            }

            if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                TargetZoom = Mathf.Clamp(TargetZoom * ZoomFactor, MinZoom, MaxZoom);
                GD.Print($"[UvCamera2D] Zoom in, TargetZoom: {TargetZoom}");
                GetViewport().SetInputAsHandled();
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                TargetZoom = Mathf.Clamp(TargetZoom / ZoomFactor, MinZoom, MaxZoom);
                GD.Print($"[UvCamera2D] Zoom out, TargetZoom: {TargetZoom}");
                GetViewport().SetInputAsHandled();
            }
        }

        if (@event is InputEventMouseMotion mouseMotion && _isDragging)
        {
            Position -= mouseMotion.Relative / Zoom;
            GD.Print($"[UvCamera2D] Dragging, Position: {Position}");
            GetViewport().SetInputAsHandled();
        }
    }
}
