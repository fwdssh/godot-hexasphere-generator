using Godot;

public partial class FreeCamera3D : Camera3D
{
    [Export] public float MoveSpeed = 5.0f;
    [Export] public float LookSensitivity = 0.002f;
    
    [Export] public float ZoomSpeed = 2.0f;
    [Export] public float MinFov = 20.0f; 
    [Export] public float MaxFov = 100.0f;

    private float _yaw = 0.0f;
    private float _pitch = 0.0f;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        
        Vector3 rot = Rotation;
        _pitch = rot.X;
        _yaw = rot.Y;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        if (@event is InputEventMouseButton click && click.Pressed && click.ButtonIndex == MouseButton.Left)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw -= motion.Relative.X * LookSensitivity;
            _pitch -= motion.Relative.Y * LookSensitivity;
            
            _pitch = Mathf.Clamp(_pitch, -Mathf.Pi / 2.0f + 0.01f, Mathf.Pi / 2.0f - 0.01f);
            
            Rotation = new Vector3(_pitch, _yaw, 0);
        }

        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                Fov = Mathf.Clamp(Fov - ZoomSpeed, MinFov, MaxFov);
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                Fov = Mathf.Clamp(Fov + ZoomSpeed, MinFov, MaxFov);
            }
        }
    }

    public override void _Process(double delta)
    {
        Vector3 direction = Vector3.Zero;

        if (Input.IsPhysicalKeyPressed(Key.W))
            direction -= GlobalTransform.Basis.Z; 
        if (Input.IsPhysicalKeyPressed(Key.S))
            direction += GlobalTransform.Basis.Z; 
        if (Input.IsPhysicalKeyPressed(Key.A))
            direction -= GlobalTransform.Basis.X; 
        if (Input.IsPhysicalKeyPressed(Key.D))
            direction += GlobalTransform.Basis.X; 

        if (direction != Vector3.Zero)
        {
            direction = direction.Normalized();
        }
        
        Position += direction * MoveSpeed * (float)delta;
    }
}