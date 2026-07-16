using Godot;

public partial class UvCamera2D : Camera2D
{
    [Export] public float TargetZoom = 1.0f;
    [Export] public float ZoomFactor = 1.1f;
    [Export] public float MinZoom = 0.1f;
    [Export] public float MaxZoom = 3.0f;

    private bool _isDragging = false;
    private HexasphereProjectorController _projector;
    private Vector2 _panLimits = new Vector2(2000, 1200);
    private Vector2 _panCenter = Vector2.Zero;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _projector = GetParent<HexasphereProjectorController>();
    }

    public override void _Process(double delta)
    {
        if (_projector != null && !_projector.Visible) return;
        
        Zoom = Zoom.Lerp(new Vector2(TargetZoom, TargetZoom), (float)delta * 10f);
    }

    public override void _Input(InputEvent @event)
    {
        if (_projector != null && !_projector.Visible) return;
        
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                _isDragging = mouseButton.Pressed;
                GetViewport().SetInputAsHandled();
            }

            if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                TargetZoom = Mathf.Clamp(TargetZoom * ZoomFactor, MinZoom, MaxZoom);
                GetViewport().SetInputAsHandled();
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                TargetZoom = Mathf.Clamp(TargetZoom / ZoomFactor, MinZoom, MaxZoom);
                GetViewport().SetInputAsHandled();
            }
        }

        if (@event is InputEventMouseMotion mouseMotion && _isDragging)
        {
            Position -= mouseMotion.Relative / Zoom;
            Position = new Vector2(
                Mathf.Clamp(Position.X, _panCenter.X - _panLimits.X, _panCenter.X + _panLimits.X),
                Mathf.Clamp(Position.Y, _panCenter.Y - _panLimits.Y, _panCenter.Y + _panLimits.Y)
            );
            GetViewport().SetInputAsHandled();
        }
    }

    public void SetPanLimits(Vector2 mapSize)
    {
        _panCenter = mapSize * 0.5f;
        _panLimits = mapSize * 0.6f;
    }
}
