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
    private bool _isSwitchingSides = false;
    private float _switchProgress = 0f; // 0 → 1
    private float _arcStartX;
    private int _switchArcDir = 0; // +1 = over the top, -1 = under
    private const float OverArcHeight = 6f;
    private const float UnderArcHeight = 6f;
    private const float PillarCollisionRadius = 2.5f; // world-Z half-range for pillar collision

    private PillarPool? _pillarPool;
    private ObstacleManager? _obstacleManager;
    private bool _canSwitchUnder = true;
    private bool _canSwitchOver = true;
    private Shield? _shield;

    public float RelativeVelocity => _relativeVelocity;
    public bool CanSwitchUnder => _canSwitchUnder;
    public bool CanSwitchOver => _canSwitchOver;
    public bool IsOnRightSide => _onRightSide;

    public override void _Ready()
    {
        _config = GetNode<GameConfig>("/root/GameConfig");
        _tsm = GetNode<TrainSpeedManager>("/root/TrainSpeedManager");
        _camera = GetNode<Camera3D>("Camera3D");
        _turret = GetNode<Turret>("Turret");
        YHeight = _config.CarDriveHeight;

        RotationDegrees = new Vector3(0, 90f, 0); // fixed: car always faces -X toward train
        _pillarPool = GetTree().Root.FindChild("PillarPool", true, false) as PillarPool;
        _obstacleManager = GetNodeOrNull<ObstacleManager>("/root/ObstacleManager");

        _shield = new Shield();
        AddChild(_shield);

        // Toggle capture on focus regain — fixes Godot/Windows bug where motion
        // events stop arriving even though the mouse is still technically captured.
        GetWindow().FocusEntered += OnViewportFocusEntered;

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

        if (@event is InputEventKey key2 && key2.Pressed && !key2.Echo
            && key2.Keycode == Key.Ctrl)
        {
            if (_canSwitchUnder && !_isSwitchingSides)
                StartSideSwitch(-1);
            GetViewport().SetInputAsHandled();
            return;
        }

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

        // Project camera axes onto the train Z axis (+Z = toward locomotive).
        // wsFactor: how much W/S aligns with train forward.
        // adFactor: how much D/A aligns with train forward.
        var camBasis = _camera.GlobalTransform.Basis;
        float wsFactor = -camBasis.Z.Z;   // camera forward = -Basis.Z
        float adFactor =  camBasis.X.Z;   // camera right  = +Basis.X

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

        // Update pole-clearance check every frame for HUD
        _canSwitchUnder = !_isSwitchingSides && PredictUnderArcClear();

        // Obstacle overrides — active state takes priority; warning state applies next
        GetEffectiveObstacleState(out CliffSide effCliff, out MovementLimit effLimit);
        bool anyCliff  = effCliff != CliffSide.None;
        bool roofUp    = effLimit == MovementLimit.Roof;
        bool plateauUp = effLimit == MovementLimit.Plateau;
        _canSwitchOver = !anyCliff && !roofUp;
        if (anyCliff || plateauUp)
            _canSwitchUnder = false;

        // Raycast-based cliff detection: if a cliff wall is within detection range, auto-switch
        if (anyCliff && !_isSwitchingSides && _inputEnabled)
            CheckCliffRaycast(effCliff, effLimit);

        // Trigger side switch (player input)
        if (!_isSwitchingSides)
        {
            if (Input.IsActionJustPressed("switch_side_over") && _canSwitchOver)
                StartSideSwitch(+1);
        }

        float newZ = Position.Z + _relativeVelocity * dt;

        if (_isSwitchingSides)
        {
            _switchProgress += dt / _config.SideChangeTime;
            if (_switchProgress >= 1f)
            {
                _switchProgress = 1f;
                _isSwitchingSides = false;
                _onRightSide = !_onRightSide;
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
    /// Predicts the two Z positions where the player crosses a pillar's X during the under-arc,
    /// then checks whether any pillar will be at those world-Z positions at those times.
    /// Returns true when no collision is predicted.
    /// </summary>
    private bool PredictUnderArcClear()
    {
        if (_pillarPool == null) return true;

        // Fraction of arc at which player X crosses PillarX (symmetric on both sides)
        float tFrac = Mathf.Acos(PillarPool.PillarX / XOffset) / Mathf.Pi;
        float t1 = tFrac * _config.SideChangeTime;
        float t2 = (1f - tFrac) * _config.SideChangeTime;

        // In world space: player moves at relativeVelocity, pillars move at -trainSpeed.
        // A pillar at current world Z = pZ will be at pZ - trainSpeed*t when the player
        // reaches world Z = Position.Z + relVelocity*t.
        // Collision when: Position.Z + relVelocity*t == pZ - trainSpeed*t
        //              →  pZ == Position.Z + (relVelocity + trainSpeed) * t
        float combinedSpeed = _relativeVelocity + _tsm.CurrentTrainSpeed;
        float targetZ1 = Position.Z + combinedSpeed * t1;
        float targetZ2 = Position.Z + combinedSpeed * t2;

        return !_pillarPool.HasPillarNearZ(targetZ1, PillarCollisionRadius)
            && !_pillarPool.HasPillarNearZ(targetZ2, PillarCollisionRadius);
    }

    /// <summary>
    /// Active state takes priority over warning. Returns None/None when no manager or no section.
    /// </summary>
    private void GetEffectiveObstacleState(out CliffSide cliff, out MovementLimit limit)
    {
        cliff = CliffSide.None;
        limit = MovementLimit.None;
        if (_obstacleManager == null) return;

        cliff = _obstacleManager.ActiveCliffSide != CliffSide.None
            ? _obstacleManager.ActiveCliffSide
            : _obstacleManager.IsInWarning ? _obstacleManager.UpcomingCliffSide : CliffSide.None;

        limit = _obstacleManager.ActiveMovementLimit != MovementLimit.None
            ? _obstacleManager.ActiveMovementLimit
            : _obstacleManager.IsInWarning ? _obstacleManager.UpcomingMovementLimit : MovementLimit.None;
    }

    /// <summary>
    /// Casts a ray forward (+Z) from the car. If it hits a cliff body (layer 8 = mask 128)
    /// and the car is on the cliff side, triggers a safe side-switch automatically.
    /// </summary>
    private void CheckCliffRaycast(CliffSide cliff, MovementLimit limit)
    {
        // Only act if we're currently on the cliff side
        bool onCliffSide = (cliff == CliffSide.Right &&  _onRightSide)
                        || (cliff == CliffSide.Left  && !_onRightSide);
        if (!onCliffSide) return;

        var spaceState = GetWorld3D().DirectSpaceState;
        var origin = GlobalPosition;
        var target = origin + new Vector3(0f, 0f, _config.CliffDetectionDistance);
        var query  = PhysicsRayQueryParameters3D.Create(origin, target, 128); // layer 8
        var result = spaceState.IntersectRay(query);

        if (result.Count == 0) return;

        // Hit a cliff wall — pick a safe arc direction; pillar blocking is ignored for forced switches
        int dir = limit == MovementLimit.Plateau ? +1   // plateau blocks under → go over
                :                                  -1;  // default under (Roof or unconstrained)
        StartSideSwitch(dir);
    }

    private void StartSideSwitch(int direction)
    {
        _isSwitchingSides = true;
        _switchProgress = 0f;
        _switchArcDir = direction;
        _arcStartX = _onRightSide ? XOffset : -XOffset;
    }

    public void FlashShieldHit() => _shield?.FlashHit();

    public void SetTrainFrontZ(float z) => _trainFrontZ = z;
    public void DisableInput()
    {
        _inputEnabled = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }
    public void EnableInput() => _inputEnabled = true;

    public float GetDistanceBehindFront() => _trainFrontZ - Position.Z;
}
