using Godot;

public partial class Camera3d : Camera3D
{
    [Export] public float MouseRotateSpeed = 0.005f;
    [Export] public float ZoomSpeed = 1.0f;
    
    [Export] public float MinZoom = 1.0f; 
    [Export] public float MaxZoom = 60.0f;

    private bool _isDragging = false;

    public override void _Ready()
    {
        float startDist = Mathf.Clamp(Position.Length(), MinZoom, MaxZoom);
        Position = Position.Normalized() * startDist;
        LookAt(Vector3.Zero, Vector3.Up);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                _isDragging = mouseButton.Pressed;
            }

            if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                Position = Position.Normalized() * Mathf.Max(MinZoom, Position.Length() - ZoomSpeed);
                LookAt(Vector3.Zero, Vector3.Up);
            }

            if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                Position = Position.Normalized() * Mathf.Min(MaxZoom, Position.Length() + ZoomSpeed);
                LookAt(Vector3.Zero, Vector3.Up);
            }
        }

        if (@event is InputEventMouseMotion motion && _isDragging)
        {
            Vector3 pos = Position;

            pos = pos.Rotated(Vector3.Up, -motion.Relative.X * MouseRotateSpeed);

            Vector3 rightAxis = pos.Cross(Vector3.Up).Normalized();
            if (rightAxis.LengthSquared() > 0.001f)
            {
                Vector3 nextPos = pos.Rotated(rightAxis, -motion.Relative.Y * MouseRotateSpeed);
                
                float angleToUp = nextPos.Normalized().AngleTo(Vector3.Up);
                if (angleToUp > 0.05f && angleToUp < Mathf.Pi - 0.05f)
                {
                    pos = nextPos;
                }
            }

            Position = pos;
            LookAt(Vector3.Zero, Vector3.Up);
        }
    }
}