using Godot;

/// <summary>
/// Roof-mounted enemy turret. Sits on top of train carriages.
///
/// States:
///   Inactive  — dormant. Activate() is called when nearby container/clamp takes damage
///               (same wiring as DeployerNode). When cooldown is zero and player is in range,
///               transitions to Active.
///   Active    — tracks player horizontally and fires in bursts. After each burst, checks range;
///               if player is too far away, returns to Inactive. Any player bullet hit sends it
///               to Repairing instead.
///   Repairing — longer cooldown version of Inactive; visual changes to yellow.
///
/// Fire rules (checked each shot):
///   - Cannot fire while player.IsFlippingUnder (flip-under arc).
///   - Can fire while player.IsFlippingOver (flip-over arc) or standing on either side.
///   - Can be Active during a Roof obstacle section (unlike Deployers which cannot spawn).
///
/// Collision:
///   Area3D on layer 32 (enemies). Player bullet raycasts (mask 39 includes 32) detect this.
///   Monitorable only when Active — no damage possible while Inactive or Repairing.
/// </summary>
public partial class RoofTurretNode : Node3D
{
    private enum TurretState { Inactive, Active, Repairing }

    private TurretState _state = TurretState.Inactive;
    private GameConfig _config = null!;
    private PlayerCar? _playerCar;
    private RandomNumberGenerator _rng = null!;

    private bool _activationPending = false;
    private float _cooldown = 0f;

    // Burst-fire tracking
    private int _shotsRemainingInBurst = 0;
    private float _fireCooldown = 0f;
    private float _burstPauseTimer = 0f;
    private bool _inBurstPause = false;

    private Area3D _area = null!;
    private StandardMaterial3D _mat = null!;

    private static readonly Color ColorInactive  = new(0.3f,  0.3f,  0.35f);
    private static readonly Color ColorActive    = new(0.8f,  0.15f, 0.05f);
    private static readonly Color ColorRepairing = new(0.65f, 0.5f,  0.05f);

    private const float TurretSize = 0.9f;

    public override void _Ready()
    {
        _config = GetNode<GameConfig>("/root/GameConfig");
        _rng = new RandomNumberGenerator();
        _rng.Randomize();

        BuildVisual();
        BuildCollision();
    }

    private void BuildVisual()
    {
        _mat = new StandardMaterial3D { AlbedoColor = ColorInactive };
        var meshInst = new MeshInstance3D { Name = "MeshSlot" };
        var glbMesh = TryLoadGlbMesh("res://assets/models/enemies/roof_turret.glb");
        if (glbMesh != null)
        {
            meshInst.Mesh = glbMesh;
        }
        else
        {
            var box = new BoxMesh { Size = new Vector3(TurretSize, TurretSize, TurretSize) };
            box.Material = _mat;
            meshInst.Mesh = box;
        }
        AddChild(meshInst);
    }

    private void BuildCollision()
    {
        _area = new Area3D
        {
            CollisionLayer = 32u,   // enemies layer — player bullets (mask 39) include this
            CollisionMask  = 0u,
            Monitorable    = false, // enabled only when Active
            Monitoring     = false,
            Name           = "Area3D",
        };
        _area.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(TurretSize, TurretSize, TurretSize) }
        });
        AddChild(_area);
    }

    public void SetPlayerCar(PlayerCar? player) => _playerCar = player;

    /// <summary>
    /// Connected to ContainerNode.DamageTaken and ClampNode.DamageTaken by TrainBuilder.
    /// Arms the turret; actual activation (→ Active) happens once the cooldown has elapsed
    /// and the player is within RoofTurretMaxRange.
    /// </summary>
    public void Activate()
    {
        if (_state != TurretState.Inactive) return;
        _activationPending = true;
    }

    /// <summary>
    /// Called by Bullet when a player bullet hits this turret's Area3D.
    /// Only has effect in the Active state.
    /// </summary>
    public void TakeDamage(float _amount)
    {
        if (_state != TurretState.Active) return;
        EnterRepairing();
    }

    public override void _Process(double delta)
    {
        if (_config == null || _playerCar == null) return;
        float dt = (float)delta;

        switch (_state)
        {
            case TurretState.Inactive:  ProcessInactive(dt);  break;
            case TurretState.Active:    ProcessActive(dt);    break;
            case TurretState.Repairing: ProcessRepairing(dt); break;
        }
    }

    // ─── State processors ────────────────────────────────────────────────────

    private void ProcessInactive(float dt)
    {
        _cooldown -= dt;
        if (!_activationPending || _cooldown > 0f) return;

        float dist = GlobalPosition.DistanceTo(_playerCar!.GlobalPosition);
        if (dist > _config.RoofTurretMaxRange)
        {
            _activationPending = false; // out of range — wait for the next damage signal
            return;
        }

        EnterActive();
    }

    private void ProcessActive(float dt)
    {
        if (!_playerCar!.IsInsideTree()) { EnterInactive(); return; }

        // Track player with horizontal-only yaw rotation
        TrackPlayer();

        if (_inBurstPause)
        {
            _burstPauseTimer -= dt;
            if (_burstPauseTimer <= 0f)
            {
                _inBurstPause = false;
                _shotsRemainingInBurst = _config.RoofTurretBurstCount;
                _fireCooldown = 1f / _config.RoofTurretFireRate;
            }
            return;
        }

        _fireCooldown -= dt;
        if (_fireCooldown > 0f) return;

        // Cannot fire during a flip-under arc
        if (_playerCar.IsFlippingUnder)
        {
            _fireCooldown = 0.2f; // short retry delay
            return;
        }

        Fire();
        _shotsRemainingInBurst--;

        // After each shot: deactivate if player has moved out of range
        if (GlobalPosition.DistanceTo(_playerCar.GlobalPosition) > _config.RoofTurretMaxRange)
        {
            EnterInactive();
            return;
        }

        if (_shotsRemainingInBurst <= 0)
        {
            _inBurstPause = true;
            _burstPauseTimer = _config.RoofTurretBurstInterval;
        }
        else
        {
            _fireCooldown = 1f / _config.RoofTurretFireRate;
        }
    }

    private void ProcessRepairing(float dt)
    {
        _cooldown -= dt;
        if (_cooldown <= 0f)
            EnterInactive();
    }

    // ─── State transitions ────────────────────────────────────────────────────

    private void EnterActive()
    {
        _state = TurretState.Active;
        _activationPending = false;
        _shotsRemainingInBurst = _config.RoofTurretBurstCount;
        _fireCooldown = 1f / _config.RoofTurretFireRate;
        _inBurstPause = false;
        _area.SetDeferred(Area3D.PropertyName.Monitorable, true);
        _mat.AlbedoColor = ColorActive;
        GD.Print($"[RoofTurret] {Name} activated.");
    }

    private void EnterInactive()
    {
        _state = TurretState.Inactive;
        _activationPending = false;
        _cooldown = _config.RoofTurretReactivationTime;
        _area.SetDeferred(Area3D.PropertyName.Monitorable, false);
        _mat.AlbedoColor = ColorInactive;
    }

    private void EnterRepairing()
    {
        _state = TurretState.Repairing;
        _cooldown = _config.RoofTurretRepairTime;
        _area.SetDeferred(Area3D.PropertyName.Monitorable, false);
        _mat.AlbedoColor = ColorRepairing;
        GD.Print($"[RoofTurret] {Name} damaged — repairing for {_cooldown:F1}s.");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void TrackPlayer()
    {
        var dir = _playerCar!.GlobalPosition - GlobalPosition;
        var flat = new Vector3(dir.X, 0f, dir.Z);
        if (flat.LengthSquared() > 0.01f)
            RotationDegrees = new Vector3(0f, Mathf.RadToDeg(Mathf.Atan2(-flat.X, -flat.Z)), 0f);
    }

    private void Fire()
    {
        bool isHit = _rng.Randf() < _config.RoofTurretHitChance;
        Vector3 targetPos;

        if (isHit)
        {
            targetPos = _playerCar!.GlobalPosition + new Vector3(
                _rng.RandfRange(-0.2f, 0.2f),
                _rng.RandfRange(-0.2f, 0.2f),
                _rng.RandfRange(-0.2f, 0.2f));
        }
        else
        {
            float missX = _rng.RandfRange(0.5f, 1.0f) * (_rng.Randf() > 0.5f ? 1f : -1f);
            float missY = _rng.RandfRange(0.5f, 1.0f) * (_rng.Randf() > 0.5f ? 1f : -1f);
            targetPos = _playerCar!.GlobalPosition + new Vector3(missX, missY, 0f);
        }

        var bullet = new DroneBullet();
        GetTree().Root.AddChild(bullet);
        bullet.GlobalPosition = GlobalPosition;
        bullet.Initialize(targetPos, isHit, _config.RoofTurretBulletSpeed);
    }

    private static Mesh? TryLoadGlbMesh(string path)
    {
        var scene = GD.Load<PackedScene>(path);
        if (scene == null) return null;
        var root = scene.Instantiate<Node3D>();
        var body = root.FindChild("Body") as MeshInstance3D;
        var mesh = body?.Mesh;
        root.QueueFree();
        return mesh;
    }
}
