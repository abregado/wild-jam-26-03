using Godot;

/// <summary>
/// Turret script. Handles primary fire (burst bullets) and secondary fire (beacons).
/// Attach to the "Turret" node inside Camera3D inside PlayerCar.
///
/// Aiming: each frame casts a physics ray from camera centre. A red dot tracks the hit
/// point in 3D space. Bullets fire from BarrelTip toward that point.
///
/// Burst fire: one trigger press fires BurstCount bullets with BurstDelay between each.
/// RateOfFire controls how often a new burst can be started.
///
/// Muzzle flash: brief emissive sphere + OmniLight at barrel tip on each shot.
/// Barrel retract: barrels snap back in +Z on each shot then spring forward.
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

    private MeshInstance3D _aimDot = null!;
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

        _camera = GetParent<Camera3D>();
        _barrelTip = GetNodeOrNull<Node3D>("BarrelTip") ?? this;

        _barrelLeft = GetNode<MeshInstance3D>("BarrelLeft");
        _barrelRight = GetNode<MeshInstance3D>("BarrelRight");
        _barrelLeftRest = _barrelLeft.Position;
        _barrelRightRest = _barrelRight.Position;

        _bulletScene = GD.Load<PackedScene>("res://scenes/projectiles/Bullet.tscn");
        _beaconScene = GD.Load<PackedScene>("res://scenes/projectiles/Beacon.tscn");

        SetupAimDot();
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
        AddChild(_aimDot);
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

        _muzzleFlash.Scale = Vector3.Zero; // hidden until fired
        AddChild(_muzzleFlash);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Update aim point and dot every frame
        _currentAimPoint = GetAimPoint();
        _aimDot.GlobalPosition = _currentAimPoint;

        // Tick cooldowns
        if (_fireCooldown > 0f) _fireCooldown -= dt;
        if (_beaconCooldown > 0f) _beaconCooldown -= dt;

        // Reload timer
        if (_isReloading)
        {
            _reloadTimer -= dt;
            if (_reloadTimer <= 0f)
            {
                _currentAmmo = _config.AmmoPerClip;
                _isReloading = false;
            }
        }

        // Handle in-progress burst
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

        // Trigger new burst
        if (Input.IsActionJustPressed("fire_primary") && !_isReloading
            && _fireCooldown <= 0f && _burstRemaining <= 0)
        {
            if (_currentAmmo > 0)
                StartBurst();
            else
                StartReload();
        }

        // Manual reload
        if (Input.IsActionJustPressed("reload") && !_isReloading && _currentAmmo < _config.AmmoPerClip)
            StartReload();

        // Beacon fire
        if (Input.IsActionJustPressed("fire_beacon") && _beaconCooldown <= 0f)
            FireBeacon();
    }

    private Vector3 GetAimPoint()
    {
        var from = _camera.GlobalPosition;
        var forward = -_camera.GlobalTransform.Basis.Z;
        var to = from + forward * 150f;

        var spaceState = _camera.GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(from, to, 6); // containers (2) + clamps (4)
        query.CollideWithAreas = true;
        query.CollideWithBodies = false;
        var result = spaceState.IntersectRay(query);

        return result.Count > 0 ? result["position"].AsVector3() : to;
    }

    private void StartBurst()
    {
        _burstRemaining = Mathf.Min(_config.BurstCount, _currentAmmo);
        _burstDelayTimer = 0f; // first shot fires immediately on next frame
        _fireCooldown = 1f / _config.RateOfFire;
    }

    private void FireSingleBullet()
    {
        var bullet = (Bullet)_bulletScene.Instantiate();
        GetTree().Root.AddChild(bullet);
        bullet.GlobalPosition = _barrelTip.GlobalPosition;

        var aimDir = (_currentAimPoint - _barrelTip.GlobalPosition).Normalized();
        if (aimDir.LengthSquared() > 0.01f && Mathf.Abs(aimDir.Dot(Vector3.Up)) < 0.99f)
            bullet.LookAt(_barrelTip.GlobalPosition + aimDir, Vector3.Up);
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
        const float snapTime = 0.04f;
        const float returnTime = 0.18f;

        // Snap barrels back to recoil position immediately, then spring forward
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
