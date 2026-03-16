using Godot;

/// <summary>
/// Turret script. Handles primary fire (bullets) and secondary fire (beacons).
/// Attach to the "Turret" node inside PlayerCar.
///
/// Primary fire: left mouse click (fire_primary).
/// Secondary fire: right mouse click (fire_beacon).
/// Manual reload: R key (reload).
///
/// Barrel tip position: GlobalPosition of BarrelTip node child.
/// Fire direction: Camera3D forward direction (-camera.GlobalTransform.Basis.Z).
/// </summary>
public partial class Turret : Node3D
{
    private GameConfig _config = null!;
    private Camera3D _camera = null!;
    private Node3D _barrelTip = null!;

    private int _currentAmmo;
    private bool _isReloading;
    private float _reloadTimer;
    private float _fireCooldown;
    private float _beaconCooldown;

    private PackedScene _bulletScene = null!;
    private PackedScene _beaconScene = null!;

    public int CurrentAmmo => _currentAmmo;
    public bool IsReloading => _isReloading;
    public float ReloadProgress => _isReloading && _config != null
        ? 1f - _reloadTimer / _config.ReloadTime
        : 1f;

    public override void _Ready()
    {
        _config = GetNode<GameConfig>("/root/GameConfig");
        _currentAmmo = _config.AmmoPerClip;

        // Turret is a child of Camera3D — parent IS the camera
        _camera = GetParent<Camera3D>();
        _barrelTip = GetNodeOrNull<Node3D>("BarrelTip") ?? this;

        _bulletScene = GD.Load<PackedScene>("res://scenes/projectiles/Bullet.tscn");
        _beaconScene = GD.Load<PackedScene>("res://scenes/projectiles/Beacon.tscn");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

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

        // Primary fire
        if (Input.IsActionJustPressed("fire_primary") && !_isReloading && _fireCooldown <= 0f)
        {
            if (_currentAmmo > 0)
                FireBullet();
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

    private void FireBullet()
    {
        var bullet = (Bullet)_bulletScene.Instantiate();
        GetTree().Root.AddChild(bullet);
        bullet.GlobalPosition = _barrelTip.GlobalPosition;
        bullet.GlobalRotation = _camera.GlobalRotation;
        bullet.Initialize(_config.TurretDamage, _config.BlastRadius, _config.BulletSpeed);

        _currentAmmo--;
        _fireCooldown = 1f / _config.RateOfFire;

        if (_currentAmmo <= 0)
            StartReload();
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
