using Godot;

/// <summary>
/// Controls the player's flying car alongside the train.
///
/// MOUSE CAPTURE: Click the game window to capture mouse and enable aiming.
/// Press Escape to release the mouse (use this to Alt-Tab out).
/// Mouse is NOT auto-captured at startup — requires a click first.
///
/// Position: Fixed at X=8.0 (right side of train at X=0), Y=5.0.
/// Camera default: faces -X (toward train). PlayerCar starts at rotation.y = 90.
/// Mouse X = yaw (rotates whole car on Y). Mouse Y = pitch (camera only, clamped ±60°).
///
/// W = move toward locomotive (increase Z).
/// S = move toward caboose (decrease Z).
/// </summary>
public partial class PlayerCar : Node3D
{
    public const float XOffset = 8.0f;
    public const float YHeight = 5.0f;

    private float _relativeVelocity;
    private float _pitch;
    private float _lookYaw = 0f; // camera yaw relative to car (car body is always fixed)

    private Camera3D _camera = null!;
    private Turret _turret = null!;
    private GameConfig _config = null!;
    private TrainSpeedManager _tsm = null!;

    private bool _inputEnabled = true;
    private float _trainFrontZ = 60f;
    private bool _captureDesired = true; // false while player has released mouse with Escape
    private float _bobTime;
    private const float BobAmplitude = 0.12f;
    private const float BobFrequency = 0.7f; // cycles per second

    public float RelativeVelocity => _relativeVelocity;

    public override void _Ready()
    {
        _config = GetNode<GameConfig>("/root/GameConfig");
        _tsm = GetNode<TrainSpeedManager>("/root/TrainSpeedManager");
        _camera = GetNode<Camera3D>("Camera3D");
        _turret = GetNode<Turret>("Turret");

        RotationDegrees = new Vector3(0, 90f, 0); // fixed: car always faces -X toward train
        GD.Print("[PlayerCar] Ready. Mouse will auto-capture. Escape = release.");
    }

    public override void _Input(InputEvent @event)
    {
        // Escape = release mouse and stop auto-capture.
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
        {
            _captureDesired = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            GetViewport().SetInputAsHandled();
            return;
        }

        // Any click while capture was released = re-enable auto-capture.
        if (@event is InputEventMouseButton mb && mb.Pressed && !_captureDesired)
        {
            _captureDesired = true;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Input.MouseMode != Input.MouseModeEnum.Captured) return;
        if (!_inputEnabled) return;

        if (@event is InputEventMouseMotion motion)
        {
            _lookYaw -= motion.Relative.X * 0.25f;
            _pitch   -= motion.Relative.Y * 0.25f;
            _pitch    = Mathf.Clamp(_pitch, -60f, 30f);

            // Car body stays fixed. Camera handles both yaw and pitch.
            _camera.RotationDegrees = new Vector3(_pitch, _lookYaw, 0);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        // Poll every frame until capture succeeds (handles window-focus timing at startup).
        if (_captureDesired && Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            if (Input.MouseMode == Input.MouseModeEnum.Captured)
                GD.Print("[PlayerCar] Mouse captured.");
        }

        float dt = (float)delta;

        // Bob always runs (even when input disabled during zoom-out)
        _bobTime += dt;
        float bobOffset = Mathf.Sin(_bobTime * BobFrequency * Mathf.Tau) * BobAmplitude;

        if (!_inputEnabled)
        {
            Position = new Vector3(XOffset, YHeight + bobOffset, Position.Z);
            return;
        }

        float accel = _config.CarAcceleration;
        float decel = _config.CarDeceleration;

        bool goForward = Input.IsActionPressed("move_forward");
        bool goBack    = Input.IsActionPressed("move_backward");

        if (goForward)
            _relativeVelocity += accel * dt;
        else if (goBack)
            _relativeVelocity -= decel * dt;
        else
        {
            float drag = decel * dt;
            if (Mathf.Abs(_relativeVelocity) < drag)
                _relativeVelocity = 0f;
            else
                _relativeVelocity -= Mathf.Sign(_relativeVelocity) * drag;
        }

        _relativeVelocity = Mathf.Clamp(_relativeVelocity,
            _tsm.MaxRelativeBackward, _tsm.MaxRelativeForward);

        Position = new Vector3(XOffset, YHeight + bobOffset, Position.Z + _relativeVelocity * dt);
    }

    public void SetTrainFrontZ(float z) => _trainFrontZ = z;
    public void DisableInput()
    {
        _inputEnabled = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }
    public void EnableInput() => _inputEnabled = true;

    public float GetDistanceBehindFront() => _trainFrontZ - Position.Z;
}
