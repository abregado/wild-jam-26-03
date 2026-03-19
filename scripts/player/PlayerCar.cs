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
/// WASD accelerate/decelerate based on camera direction projected onto the train Z axis.
/// W/S use the camera's forward vector; A/D use the camera's right vector.
/// e.g. looking toward the locomotive: W = full accelerate, S = full decelerate.
/// Looking at the train side: A = accelerate, D = decelerate (or opposite when facing away).
///</summary>
public partial class PlayerCar : Node3D
{
    public const float XOffset = 8.0f;
    public float YHeight { get; private set; } = 9.0f;

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

    // Side switching
    private bool _onRightSide = true;
    private bool _wasAccelerating;
    private bool _wasDecelerating;
    private bool _isSwitchingSides = false;
    private float _switchProgress = 0f; // 0 → 1
    private float _arcStartX;
    private int _switchArcDir = 0;       // +1 = over the top, -1 = under
    private bool _flipReversed = false;  // true = arc is playing backward (reversal in progress)
    private float _flipLockedVelocity;   // relative velocity frozen at flip start
    private const float OverArcHeight = 6f;
    private const float UnderArcHeight = 6f;

    // Flip-path physics check uses layer 9 (256) — actual-geometry obstacle bodies
    private const uint FlipBodyMask = 256u;
    private SphereShape3D _flipCheckSphere = null!;

    private Shield? _shield;

    public bool  IsInputEnabled    => _inputEnabled;
    public float RelativeVelocity  => _relativeVelocity;
    // Simplified: the only gate on manual flips is "not already flipping"
    public bool CanSwitchUnder => !_isSwitchingSides;
    public bool CanSwitchOver  => !_isSwitchingSides;
    public bool IsOnRightSide  => _onRightSide;
    public bool IsFlippingUnder => _isSwitchingSides && _switchArcDir < 0;

    public override void _Ready()
    {
        _config = GetNode<GameConfig>("/root/GameConfig");
        _tsm = GetNode<TrainSpeedManager>("/root/TrainSpeedManager");
        _camera = GetNode<Camera3D>("Camera3D");
        _turret = GetNode<Turret>("Turret");
        YHeight = _config.CarDriveHeight;

        RotationDegrees = new Vector3(0, 90f, 0); // fixed: car always faces -X toward train

        _flipCheckSphere = new SphereShape3D { Radius = 0.4f };

        _shield = new Shield();
        AddChild(_shield);

        // Toggle capture on focus regain — fixes Godot/Windows bug where motion
        // events stop arriving even though the mouse is still technically captured.
        GetWindow().FocusEntered += OnViewportFocusEntered;

        GD.Print("[PlayerCar] Ready. Mouse will auto-capture. Escape = pause menu.");
    }

    public override void _Input(InputEvent @event)
    {
        if (Input.MouseMode != Input.MouseModeEnum.Captured) return;
        if (!_inputEnabled) return;

        if (@event is InputEventMouseMotion motion)
        {
            _lookYaw -= motion.Relative.X * 0.25f;
            _pitch   -= motion.Relative.Y * 0.25f;
            _pitch    = Mathf.Clamp(_pitch, -60f, 30f);

            // Car body stays fixed. Camera handles both yaw and pitch.
            _camera.RotationDegrees = new Vector3(_pitch, _lookYaw, 0);
            // Do NOT call SetInputAsHandled() on motion — it can confuse Godot's input queue.
        }
    }

    private void OnViewportFocusEntered()
    {
        if (!_captureDesired) return;
        // Cycle through Visible to force the OS to re-register capture.
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Input.MouseMode = Input.MouseModeEnum.Captured;
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

        // Bob always runs
        _bobTime += dt;
        float bobOffset = Mathf.Sin(_bobTime * BobFrequency * Mathf.Tau) * BobAmplitude;

        if (!_inputEnabled)
        {
            _isSwitchingSides = false;
            float sideX = _onRightSide ? XOffset : -XOffset;
            Position = new Vector3(sideX, YHeight + bobOffset, Position.Z);
            return;
        }

        float accel = _config.CarAcceleration;
        float decel = _config.CarDeceleration;

        // Velocity input is frozen during a flip — speed is locked to what it was at flip start
        if (!_isSwitchingSides)
        {
            var camBasis = _camera.GlobalTransform.Basis;
            float wsFactor = -camBasis.Z.Z;
            float adFactor =  camBasis.X.Z;

            float inputAxis = 0f;
            if (Input.IsActionPressed("move_forward"))  inputAxis += wsFactor;
            if (Input.IsActionPressed("move_backward")) inputAxis -= wsFactor;
            if (Input.IsActionPressed("move_right"))    inputAxis += adFactor;
            if (Input.IsActionPressed("move_left"))     inputAxis -= adFactor;
            inputAxis = Mathf.Clamp(inputAxis, -1f, 1f);

            if (Mathf.Abs(inputAxis) > 0.01f)
            {
                float rate = inputAxis > 0f ? accel : decel;
                _relativeVelocity += inputAxis * rate * dt;
            }
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

            // Loop sounds
            bool isAccel = inputAxis > 0.01f;
            bool isDecel = inputAxis < -0.01f;
            if (isAccel != _wasAccelerating)
            {
                if (isAccel) SoundManager.PlayLoop("car_accel", "car_accelerating");
                else         SoundManager.StopLoop("car_accel");
                _wasAccelerating = isAccel;
            }
            if (isDecel != _wasDecelerating)
            {
                if (isDecel) SoundManager.PlayLoop("car_decel", "car_decelerating");
                else         SoundManager.StopLoop("car_decel");
                _wasDecelerating = isDecel;
            }

            // Auto-flip from forward cliff detection
            CheckCliffAutoFlip(dt);

            // Manual flips
            if (Input.IsActionJustPressed("switch_side_over") && IsFlipPathClear(+1))
                StartSideSwitch(+1);
            if (Input.IsActionJustPressed("switch_side_under") && IsFlipPathClear(-1))
                StartSideSwitch(-1);
        }

        float activeVelocity = _isSwitchingSides ? _flipLockedVelocity : _relativeVelocity;
        float newZ = Position.Z + activeVelocity * dt;

        if (_isSwitchingSides)
        {
            // Safety: if an obstacle appears mid-flip, reverse back.
            // DISABLED for testing — cycle of flip/reverse when near cliff.
            // if (!_flipReversed && _switchProgress > 0.05f)
            //     CheckMidFlipReversal();

            // Advance or retreat progress
            if (_flipReversed)
                _switchProgress -= dt / _config.SideChangeTime;
            else
                _switchProgress += dt / _config.SideChangeTime;

            if (_switchProgress >= 1f)
            {
                _switchProgress = 1f;
                _isSwitchingSides = false;
                _flipReversed = false;
                _onRightSide = !_onRightSide;
            }
            else if (_switchProgress <= 0f)
            {
                // Reversed all the way back — returned to original side
                _switchProgress = 0f;
                _isSwitchingSides = false;
                _flipReversed = false;
            }
            else
            {
                float t = _switchProgress;
                float arcHeight = _switchArcDir > 0 ? OverArcHeight : UnderArcHeight;
                float newX = _arcStartX * Mathf.Cos(t * Mathf.Pi);
                float newY = YHeight + _switchArcDir * arcHeight * Mathf.Sin(t * Mathf.Pi);
                Position = new Vector3(newX, newY + bobOffset, newZ);
                return;
            }
        }

        float sideXPos = _onRightSide ? XOffset : -XOffset;
        Position = new Vector3(sideXPos, YHeight + bobOffset, newZ);
    }

    /// <summary>
    /// Samples the flip arc at N points and sphere-queries each against obstacle flip bodies
    /// (layer 9 = 256). Returns true when the full path is clear.
    /// </summary>
    private bool IsFlipPathClear(int direction)
    {
        var spaceState = GetWorld3D().DirectSpaceState;
        float startX    = _onRightSide ? XOffset : -XOffset;
        float arcHeight = direction > 0 ? OverArcHeight : UnderArcHeight;
        int   samples   = _config.FlipRaySamples;
        float duration  = _config.SideChangeTime;
        // Combined speed: car's world-space Z movement + obstacle approach speed.
        // Obstacles move at -trainSpeed, so we must look ahead by relativeVelocity + trainSpeed.
        // If car matches train speed (relativeVelocity=0), obstacles still approach at trainSpeed.
        float combinedSpeed = _relativeVelocity + _tsm.CurrentTrainSpeed;

        for (int i = 1; i <= samples; i++)
        {
            float t       = (float)i / samples;
            float sampleX = startX * Mathf.Cos(t * Mathf.Pi);
            float sampleY = YHeight + direction * arcHeight * Mathf.Sin(t * Mathf.Pi);
            float sampleZ = Position.Z + combinedSpeed * (t * duration);

            var query = new PhysicsShapeQueryParameters3D
            {
                Shape         = _flipCheckSphere,
                Transform     = new Transform3D(Basis.Identity, new Vector3(sampleX, sampleY, sampleZ)),
                CollisionMask = FlipBodyMask,
                CollideWithBodies = true,
                CollideWithAreas  = false,
            };

            if (spaceState.IntersectShape(query, 1).Count > 0)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Casts a short forward ray. If a cliff body (layer 8) is detected, picks the clear flip
    /// direction using IsFlipPathClear. If neither direction is clear, slows the car slightly.
    /// </summary>
    private void CheckCliffAutoFlip(float dt)
    {
        if (_isSwitchingSides) return;

        var spaceState = GetWorld3D().DirectSpaceState;
        var origin = GlobalPosition;
        var target = origin + new Vector3(0f, 0f, _config.CliffDetectionDistance);
        var query  = PhysicsRayQueryParameters3D.Create(origin, target, 128u); // cliff bodies
        var result = spaceState.IntersectRay(query);

        if (result.Count == 0) return;

        // Prefer under, fall back to over, otherwise brake
        if (IsFlipPathClear(-1))
            StartSideSwitch(-1);
        else if (IsFlipPathClear(+1))
            StartSideSwitch(+1);
        else
            _relativeVelocity -= _config.CliffAutoFlipBrake * dt;
    }

    /// <summary>
    /// During a flip, casts a short forward ray against flip bodies (layer 9).
    /// If hit, reverses the arc so the car returns to its original side.
    /// </summary>
    private void CheckMidFlipReversal()
    {
        var spaceState = GetWorld3D().DirectSpaceState;
        var origin = GlobalPosition;
        var target = origin + new Vector3(0f, 0f, _config.CliffDetectionDistance * 0.5f);
        var query  = PhysicsRayQueryParameters3D.Create(origin, target, FlipBodyMask);
        var result = spaceState.IntersectRay(query);

        if (result.Count == 0) return;

        _flipReversed = true;
    }

    private void StartSideSwitch(int direction)
    {
        _isSwitchingSides    = true;
        _switchProgress      = 0f;
        _switchArcDir        = direction;
        _arcStartX           = _onRightSide ? XOffset : -XOffset;
        _flipLockedVelocity  = _relativeVelocity;
        _flipReversed        = false;
    }

    public void FlashShieldHit()
    {
        SoundManager.Play("player_car_hit");
        _shield?.FlashHit();
    }

    public void SetTrainFrontZ(float z) => _trainFrontZ = z;
    public void DisableInput()
    {
        _inputEnabled = false;
        _captureDesired = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _turret.SetFireEnabled(false);
    }

    public void EnableInput()
    {
        _inputEnabled = true;
        _captureDesired = true;
        _turret.SetFireEnabled(true);
    }

    public float GetDistanceBehindFront() => _trainFrontZ - Position.Z;
}
