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
    private Node3D _barrelTip = null!;
    private MeshInstance3D _barrelLeft = null!;
    private MeshInstance3D _barrelRight = null!;
    private Vector3 _barrelLeftRest;
    private Vector3 _barrelRightRest;
    private Tween? _barrelTween;

    // Red dot  = camera-centre aim ray (where the player is looking)
    // Yellow dot = turret-barrel aim ray (where the turret is actually pointing)
    // Both live at scene root so the turret's own rotation doesn't displace them.
    private MeshInstance3D _aimDot = null!;
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

    private Vector3 _currentAimPoint;

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
        _barrelTip = GetNodeOrNull<Node3D>("BarrelTip") ?? this;

        _barrelLeft = GetNode<MeshInstance3D>("BarrelLeft");
        _barrelRight = GetNode<MeshInstance3D>("BarrelRight");
        _barrelLeftRest = _barrelLeft.Position;
        _barrelRightRest = _barrelRight.Position;

        _bulletScene = GD.Load<PackedScene>("res://scenes/projectiles/Bullet.tscn");
        _beaconScene = GD.Load<PackedScene>("res://scenes/projectiles/Beacon.tscn");

        SetupAimDot();
        SetupTurretDot();
        SetupMuzzleFlash();
    }

    private void SetupAimDot()
    {
        _aimDot = new MeshInstance3D();
        var sphere = new SphereMesh { Radius = 0.07f, Height = 0.14f };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = Colors.Red,
            EmissionEnabled = true,
            Emission = Colors.Red,
            EmissionEnergyMultiplier = 4f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        sphere.Material = mat;
        _aimDot.Mesh = sphere;
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, _aimDot);
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

        // 1. Camera-centre aim ray → red dot
        _currentAimPoint = GetAimPoint();
        if (_aimDot.IsInsideTree())
            _aimDot.GlobalPosition = _currentAimPoint;

        // Turret-barrel aim ray → yellow dot (shows where the turret is actually pointing)
        if (_turretDot.IsInsideTree())
            _turretDot.GlobalPosition = GetTurretAimPoint();

        // 2. Slerp turret rotation to face the aim point.
        //    Only interpolate the basis (rotation) — position is managed by PlayerCar.
        var toTarget = _currentAimPoint - GlobalPosition;
        if (toTarget.LengthSquared() > 0.25f)
        {
            var dir = toTarget.Normalized();
            // Basis.LookingAt(direction) → -Z axis points along dir
            var targetBasis = Basis.LookingAt(dir, Vector3.Up);
            float t = 1f - Mathf.Exp(-_config.TurretTrackingSpeed * dt);
            GlobalTransform = new Transform3D(
                GlobalTransform.Basis.Slerp(targetBasis, t),
                GlobalPosition);
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
        if (Input.IsActionJustPressed("fire_primary") && !_isReloading
            && _fireCooldown <= 0f && _burstRemaining <= 0)
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

    // Mask 7 = layer 1 (world/train bodies) + layer 2 (containers) + layer 4 (clamps)
    private const uint AimRayMask = 7u;

    private Vector3 GetAimPoint()
    {
        var from = _camera.GlobalPosition;
        var to = from + (-_camera.GlobalTransform.Basis.Z) * 150f;
        return CastAimRay(from, to);
    }

    private Vector3 GetTurretAimPoint()
    {
        var from = _barrelTip.GlobalPosition;
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
        var bullet = (Bullet)_bulletScene.Instantiate();
        GetTree().Root.AddChild(bullet);
        bullet.GlobalPosition = _barrelTip.GlobalPosition;

        // Fire along the turret's actual -Z (tracking lag is intentional)
        var fireDir = -GlobalTransform.Basis.Z;
        if (Mathf.Abs(fireDir.Dot(Vector3.Up)) < 0.99f)
            bullet.LookAt(_barrelTip.GlobalPosition + fireDir, Vector3.Up);
        else
            bullet.GlobalRotation = _camera.GlobalRotation;

        bullet.Initialize(_config.TurretDamage, _config.BlastRadius, _config.BulletSpeed);

        _currentAmmo--;
        TriggerMuzzleFlash();
        TriggerBarrelRetract();

        if (_currentAmmo <= 0)
            StartReload();
    }

    private void TriggerMuzzleFlash()
    {
        _muzzleFlash.GlobalPosition = _barrelTip.GlobalPosition;
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
        beacon.GlobalPosition = _barrelTip.GlobalPosition;
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
