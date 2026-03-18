using Godot;

/// <summary>
/// Turret script. Handles primary fire (burst bullets) and secondary fire (beacons).
/// Attach to the "Turret" node inside Camera3D inside PlayerCar.
///
/// Aiming:
///   Each frame a physics ray is cast from the camera centre (mask: containers+clamps).
///   A red dot MeshInstance3D (parented to scene root) is placed at the hit point.
///   The turret slerps its GlobalTransform toward LookingAt(aimPoint) at TurretTrackingSpeed.
///   Bullets fire along the turret's actual -Z (so tracking lag matters).
///
/// Burst fire: one press fires BurstCount bullets with BurstDelay between each.
///   RateOfFire controls the minimum time between burst starts.
///
/// Muzzle flash: brief emissive sphere + OmniLight at barrel tip each shot.
/// Barrel retract: barrels snap to recoil position then spring back with elastic easing.
/// </summary>
public partial class Turret : Node3D
{
    private GameConfig _config = null!;
    private Camera3D _camera = null!;
    private Node3D _barrelTipLeft = null!;
    private Node3D _barrelTipRight = null!;
    private MeshInstance3D _barrelLeft = null!;
    private MeshInstance3D _barrelRight = null!;
    private Vector3 _barrelLeftRest;
    private Vector3 _barrelRightRest;
    private Tween? _barrelTween;
    private bool _fireFromLeft = true;

    private Node3D ActiveBarrelTip => _fireFromLeft ? _barrelTipLeft : _barrelTipRight;

    // Yellow dot = turret-barrel aim ray (where the turret is actually pointing).
    // Lives at scene root so the turret's own rotation doesn't displace it.
    private MeshInstance3D _turretDot = null!;
    private Node3D _muzzleFlash = null!;

    private int _currentAmmo;
    private bool _isReloading;
    private float _reloadTimer;
    private float _fireCooldown;
    private float _beaconCooldown;

    private int _burstRemaining;
    private float _burstDelayTimer;

    private PackedScene _bulletScene = null!;
    private PackedScene _beaconScene = null!;

    private Vector3    _turretTargetPoint;
    private Quaternion _turretQuat = Quaternion.Identity;  // stable rotation accumulator

    // ── Debug ─────────────────────────────────────────────────────────────────
    [Export] public bool DebugTracking { get; set; } = false;
    private Label? _debugLabel;

    public int CurrentAmmo => _currentAmmo;
    public bool IsReloading => _isReloading;
    public float ReloadProgress => _isReloading && _config != null
        ? 1f - _reloadTimer / _config.ReloadTime
        : 1f;

    public override void _Ready()
    {
        _config = GetNode<GameConfig>("/root/GameConfig");
        _currentAmmo = _config.AmmoPerClip;

        // Turret is a sibling of Camera3D inside PlayerCar
        _camera = GetParent().GetNode<Camera3D>("Camera3D");
        _barrelTipLeft = GetNode<Node3D>("BarrelTipLeft");
        _barrelTipRight = GetNode<Node3D>("BarrelTipRight");

        _barrelLeft = GetNode<MeshInstance3D>("BarrelLeft");
        _barrelRight = GetNode<MeshInstance3D>("BarrelRight");
        _barrelLeftRest = _barrelLeft.Position;
        _barrelRightRest = _barrelRight.Position;

        _bulletScene = GD.Load<PackedScene>("res://scenes/projectiles/Bullet.tscn");
        _beaconScene = GD.Load<PackedScene>("res://scenes/projectiles/Beacon.tscn");

        SetupTurretDot();
        SetupMuzzleFlash();

        _turretQuat = GlobalTransform.Basis.GetRotationQuaternion();

        if (DebugTracking)
            SetupDebugLabel();
    }

    private void SetupDebugLabel()
    {
        _debugLabel = new Label();
        _debugLabel.Position = new Vector2(8, 120);
        _debugLabel.AddThemeFontSizeOverride("font_size", 13);
        // Add to the first CanvasLayer or directly to the viewport
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, _debugLabel);
    }

    private void SetupTurretDot()
    {
        _turretDot = new MeshInstance3D();
        var sphere = new SphereMesh { Radius = 0.06f, Height = 0.12f };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.85f, 0f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.75f, 0f),
            EmissionEnergyMultiplier = 4f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        sphere.Material = mat;
        _turretDot.Mesh = sphere;
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, _turretDot);
    }

    private void SetupMuzzleFlash()
    {
        _muzzleFlash = new Node3D();

        var flashMesh = new MeshInstance3D();
        var flashSphere = new SphereMesh { Radius = 0.2f, Height = 0.4f };
        var flashMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.85f, 0.3f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.7f, 0.1f),
            EmissionEnergyMultiplier = 12f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        flashSphere.Material = flashMat;
        flashMesh.Mesh = flashSphere;
        _muzzleFlash.AddChild(flashMesh);

        var flashLight = new OmniLight3D
        {
            LightColor = new Color(1f, 0.7f, 0.2f),
            LightEnergy = 8f,
            OmniRange = 6f,
        };
        _muzzleFlash.AddChild(flashLight);

        _muzzleFlash.Scale = Vector3.Zero;
        AddChild(_muzzleFlash);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // 1. Yellow dot = where the turret barrel is actually pointing.
        //    Cast from camera position so it converges to crosshair when tracking catches up.
        _turretTargetPoint = GetTurretAimPoint();
        if (_turretDot.IsInsideTree())
            _turretDot.GlobalPosition = _turretTargetPoint;

        // 2. Slerp turret toward camera forward.
        //    We accumulate rotation in _turretQuat (never read back GlobalTransform.Basis)
        //    so floating-point drift in the matrix can't feed into the next Slerp.
        var cameraForward = -_camera.GlobalTransform.Basis.Z;
        float minY = -Mathf.Sin(Mathf.DegToRad(_config.TurretMaxPitchDown));
        if (cameraForward.Y < minY)
            cameraForward = new Vector3(cameraForward.X, minY, cameraForward.Z).Normalized();

        float fwdLen2  = cameraForward.LengthSquared();
        float dotUp    = cameraForward.Dot(Vector3.Up);
        bool  canTrack = fwdLen2 > 0.25f && Mathf.Abs(dotUp) < 0.99f;

        if (DebugTracking && _debugLabel != null && _debugLabel.IsInsideTree())
        {
            string reason = Mathf.Abs(dotUp) >= 0.99f ? "gimbal" : "len²";
            _debugLabel.Text =
                $"fwd: {cameraForward:F2}  len²: {fwdLen2:F3}\n" +
                $"dot(up): {dotUp:F3}\n" +
                $"tracking: {(canTrack ? "YES" : $"NO — {reason}")}";
        }

        if (canTrack)
        {
            var targetQuat = Basis.LookingAt(cameraForward, Vector3.Up).GetRotationQuaternion();
            float t = 1f - Mathf.Exp(-_config.TurretTrackingSpeed * dt);
            _turretQuat = _turretQuat.Slerp(targetQuat, t).Normalized();
            GlobalTransform = new Transform3D(new Basis(_turretQuat), GlobalPosition);
        }

        // 3. Cooldowns
        if (_fireCooldown > 0f) _fireCooldown -= dt;
        if (_beaconCooldown > 0f) _beaconCooldown -= dt;

        // 4. Reload timer
        if (_isReloading)
        {
            _reloadTimer -= dt;
            if (_reloadTimer <= 0f)
            {
                _currentAmmo = _config.AmmoPerClip;
                _isReloading = false;
            }
        }

        // 5. Handle in-progress burst
        if (_burstRemaining > 0 && !_isReloading)
        {
            if (_currentAmmo <= 0)
            {
                _burstRemaining = 0;
                StartReload();
            }
            else if (_burstDelayTimer <= 0f)
            {
                FireSingleBullet();
                _burstRemaining--;
                if (_burstRemaining > 0)
                    _burstDelayTimer = _config.BurstDelay;
            }
            else
            {
                _burstDelayTimer -= dt;
            }
        }

        // 6. Trigger new burst
        bool fireInput = _config.AutoFire
            ? Input.IsActionPressed("fire_primary")
            : Input.IsActionJustPressed("fire_primary");
        if (fireInput && !_isReloading && _fireCooldown <= 0f && _burstRemaining <= 0)
        {
            if (_currentAmmo > 0)
                StartBurst();
            else
                StartReload();
        }

        // 7. Manual reload
        if (Input.IsActionJustPressed("reload") && !_isReloading && _currentAmmo < _config.AmmoPerClip)
            StartReload();

        // 8. Beacon
        if (Input.IsActionJustPressed("fire_beacon") && _beaconCooldown <= 0f)
            FireBeacon();
    }

    // Mask 39 = layer 1 (world/train) + layer 2 (containers) + layer 3 (clamps) + layer 6 (drones)
    private const uint AimRayMask = 39u;

    private Vector3 GetTurretAimPoint()
    {
        // Cast from camera position so yellow dot lands at screen centre when tracking converges.
        var from = _camera.GlobalPosition;
        var to = from + (-GlobalTransform.Basis.Z) * 150f;
        return CastAimRay(from, to);
    }

    private Vector3 CastAimRay(Vector3 from, Vector3 to)
    {
        var spaceState = _camera.GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(from, to, AimRayMask);
        query.CollideWithAreas = true;
        query.CollideWithBodies = true;
        var result = spaceState.IntersectRay(query);
        return result.Count > 0 ? result["position"].AsVector3() : to;
    }

    private void StartBurst()
    {
        _burstRemaining = Mathf.Min(_config.BurstCount, _currentAmmo);
        _burstDelayTimer = 0f;
        _fireCooldown = 1f / _config.RateOfFire;
    }

    private void FireSingleBullet()
    {
        var tip = ActiveBarrelTip;
        _fireFromLeft = !_fireFromLeft;

        var bullet = (Bullet)_bulletScene.Instantiate();
        GetTree().Root.AddChild(bullet);
        bullet.GlobalPosition = tip.GlobalPosition;

        // Fire toward the yellow dot target point (where the turret is actually aiming).
        var fireDir = (_turretTargetPoint - tip.GlobalPosition).Normalized();
        if (Mathf.Abs(fireDir.Dot(Vector3.Up)) < 0.99f)
            bullet.LookAt(tip.GlobalPosition + fireDir, Vector3.Up);
        else
            bullet.GlobalRotation = _camera.GlobalRotation;

        bullet.Initialize(_config.TurretDamage, _config.BlastRadius, _config.BulletSpeed);

        _currentAmmo--;
        TriggerMuzzleFlash(tip);
        TriggerBarrelRetract();

        if (_currentAmmo <= 0)
            StartReload();
    }

    private void TriggerMuzzleFlash(Node3D tip)
    {
        _muzzleFlash.GlobalPosition = tip.GlobalPosition;
        _muzzleFlash.Scale = Vector3.One;

        var tween = CreateTween();
        tween.TweenProperty(_muzzleFlash, "scale", Vector3.Zero, 0.07f)
             .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
    }

    private void TriggerBarrelRetract()
    {
        const float recoilZ = 0.22f;
        const float returnTime = 0.18f;

        _barrelLeft.Position = _barrelLeftRest + new Vector3(0f, 0f, recoilZ);
        _barrelRight.Position = _barrelRightRest + new Vector3(0f, 0f, recoilZ);

        _barrelTween?.Kill();
        _barrelTween = CreateTween();
        _barrelTween.TweenProperty(_barrelLeft, "position", _barrelLeftRest, returnTime)
                    .SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
        _barrelTween.Parallel()
                    .TweenProperty(_barrelRight, "position", _barrelRightRest, returnTime)
                    .SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
    }

    private void FireBeacon()
    {
        var beacon = (Beacon)_beaconScene.Instantiate();
        GetTree().Root.AddChild(beacon);
        beacon.GlobalPosition = ActiveBarrelTip.GlobalPosition;
        beacon.GlobalRotation = _camera.GlobalRotation;
        beacon.Initialize(_config.BeaconSpeed);

        _beaconCooldown = _config.BeaconReloadSpeed;
    }

    private void StartReload()
    {
        _isReloading = true;
        _reloadTimer = _config.ReloadTime;
    }
}
