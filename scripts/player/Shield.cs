using Godot;

/// <summary>
/// Transparent sphere shield around the player car.
///
/// Area3D on layer 8 (player/shield), monitors layer 64 (drone projectiles).
///
/// When a DroneBullet enters the shield:
///   - Calculate angle between camera look direction and direction to bullet.
///   - If angle < ShieldBlockAngle: destroy the bullet and flash the shield.
///   - Otherwise: let the bullet pass through (no effect).
///
/// Parented to PlayerCar and created in PlayerCar._Ready().
/// </summary>
public partial class Shield : Node3D
{
    private Camera3D _camera = null!;
    private StandardMaterial3D _shieldMat = null!;
    private float _blockAngle;
    private float _flashTimer;
    private const float FlashDuration = 0.18f;
    private const float ShieldRadius = 2.5f;
    // Offset so sphere is centred on the car body midpoint.
    // Car mesh is at local (0, -0.2, 0.3); Camera3D is at (0, 1.8, 0.6).
    // Midpoint ≈ (0, 0.8, 0.45) — sphere of radius 2.5 comfortably contains both.
    private static readonly Vector3 ShieldOffset = new Vector3(0f, 0.8f, 0.45f);

    public override void _Ready()
    {
        var config = GetNode<GameConfig>("/root/GameConfig");
        _blockAngle = config.ShieldBlockAngle;

        // Camera is a sibling inside PlayerCar
        _camera = GetParent().GetNode<Camera3D>("Camera3D");

        Position = ShieldOffset;
        BuildShieldMesh();
        BuildShieldArea();
    }

    private void BuildShieldMesh()
    {
        var meshInst = new MeshInstance3D { Name = "ShieldMesh" };
        var sphere = new SphereMesh { Radius = ShieldRadius, Height = ShieldRadius * 2f };
        _shieldMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.3f, 0.7f, 1f, 0.03f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        sphere.Material = _shieldMat;
        meshInst.Mesh = sphere;
        AddChild(meshInst);
    }

    private void BuildShieldArea()
    {
        // Layer 8 (bit 4) = player shield; mask 64 (bit 7) = drone projectiles
        var area = new Area3D
        {
            CollisionLayer = 8u,
            CollisionMask = 64u,
            Monitoring = true,
            Monitorable = false,
            Name = "Area3D",
        };
        var col = new CollisionShape3D { Shape = new SphereShape3D { Radius = ShieldRadius } };
        area.AddChild(col);
        area.AreaEntered += OnAreaEntered;
        AddChild(area);
    }

    private void OnAreaEntered(Area3D other)
    {
        if (other.GetParent() is not DroneBullet bullet) return;

        var cameraForward = -_camera.GlobalTransform.Basis.Z;
        var hitDir = (bullet.GlobalPosition - GlobalPosition).Normalized();
        float dot = Mathf.Clamp(cameraForward.Dot(hitDir), -1f, 1f);
        float angleDeg = Mathf.RadToDeg(Mathf.Acos(dot));

        if (angleDeg <= _blockAngle)
        {
            bullet.Block();
            FlashShield();
            VfxSpawner.Spawn("shield_hit", bullet.GlobalPosition);
        }
    }

    private void FlashShield()
    {
        _flashTimer = FlashDuration;
        _shieldMat.AlbedoColor = new Color(0.4f, 0.85f, 1f, 0.45f);
        _shieldMat.EmissionEnabled = true;
        _shieldMat.Emission = new Color(0.2f, 0.6f, 1f);
        _shieldMat.EmissionEnergyMultiplier = 3f;
    }

    /// <summary>Called when a drone bullet gets through and hits the car.</summary>
    public void FlashHit()
    {
        _flashTimer = FlashDuration;
        _shieldMat.AlbedoColor = new Color(1f, 0.1f, 0.05f, 0.5f);
        _shieldMat.EmissionEnabled = true;
        _shieldMat.Emission = new Color(1f, 0.05f, 0f);
        _shieldMat.EmissionEnergyMultiplier = 4f;
    }

    public override void _Process(double delta)
    {
        if (_flashTimer <= 0f) return;
        _flashTimer -= (float)delta;
        if (_flashTimer <= 0f)
        {
            _shieldMat.AlbedoColor = new Color(0.3f, 0.7f, 1f, 0.03f);
            _shieldMat.EmissionEnabled = false;
        }
    }
}
