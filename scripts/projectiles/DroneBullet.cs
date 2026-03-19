using Godot;

/// <summary>
/// Projectile fired by drones at the player car.
///
/// Flies toward a fixed world-space target position at DroneBulletSpeed.
/// Collision layer 64 (layer 7 = drone projectiles) — detected by Shield's Area3D.
///
/// If isHitBullet is true and the bullet reaches its target without being blocked
/// by the shield, it applies CarSpeedDamagePerHit to TrainSpeedManager.
///
/// Block() is called by Shield when the player deflects it.
/// </summary>
public partial class DroneBullet : Node3D
{
    private Vector3 _targetPos;
    private bool _isHitBullet;
    private float _speed;
    private bool _hasBeenBlocked;
    private bool _initialized;

    // Called by DroneNode after AddChild so Initialize values are available in _Process.
    public void Initialize(Vector3 targetPos, bool isHit, float speed)
    {
        _targetPos = targetPos;
        _isHitBullet = isHit;
        _speed = speed;
        _initialized = true;
    }

    public override void _Ready()
    {
        var config = GetNode<GameConfig>("/root/GameConfig");
        float size = config.DroneBulletSize;

        var mesh = new MeshInstance3D { Name = "MeshSlot" };
        var sphere = new SphereMesh { Radius = size, Height = size * 2f };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.2f, 0.05f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.1f, 0f),
            EmissionEnergyMultiplier = 5f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        sphere.Material = mat;
        mesh.Mesh = sphere;
        AddChild(mesh);

        // Layer 64 (value 64 = bit 7) = drone projectiles, monitorable by Shield
        var area = new Area3D
        {
            CollisionLayer = 64u,
            CollisionMask = 0u,
            Monitorable = true,
            Monitoring = false,
            Name = "Area3D",
        };
        var col = new CollisionShape3D { Shape = new SphereShape3D { Radius = size } };
        area.AddChild(col);
        AddChild(area);
    }

    /// <summary>Called by Shield when it intercepts this bullet.</summary>
    public void Block()
    {
        _hasBeenBlocked = true;
        QueueFree();
    }

    public override void _Process(double delta)
    {
        if (!_initialized) return; // wait for Initialize() call

        var dir = _targetPos - GlobalPosition;
        float dist = dir.Length();

        if (dist < 0.4f)
        {
            if (_isHitBullet && !_hasBeenBlocked)
            {
                var tsm = GetNode<TrainSpeedManager>("/root/TrainSpeedManager");
                var cfg = GetNode<GameConfig>("/root/GameConfig");
                tsm.ApplyCarSpeedDamage(cfg.CarSpeedDamagePerHit);

                var playerCar = GetTree().Root.FindChild("PlayerCar", true, false) as PlayerCar;
                playerCar?.FlashShieldHit();
                VfxSpawner.Spawn("car_hit", GlobalPosition);
            }
            QueueFree();
            return;
        }

        GlobalPosition += dir.Normalized() * _speed * (float)delta;
    }
}
