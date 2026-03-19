using Godot;

/// <summary>
/// Roof-mounted enemy turret. Sits on top of train carriages.
///
/// Visual structure (built procedurally; artist can provide roof_turret.glb with
/// child nodes named "Base", "Dome", "Barrel" to override each part):
///   RoofTurretNode  (yaws to track player in all states)
///   ├── BaseMesh    (flat base plate — always visible)
///   ├── DomePivot   (rotates for activation flip animation)
///   │   └── DomeMesh
///   └── BarrelMount (pitches to aim at player when Active)
///       └── BarrelMesh (scales 0→1 on activation)
///
/// States:
///   Inactive  — dormant. DomePivot.X = 180° (dome upside-down), Barrel scale = 0.
///   Active    — tracks player and fires in bursts. Dome unfolds, barrel extends.
///   Repairing — hit by player bullet. Dome+Barrel hidden, repair VFX plays.
///               After repair time, returns to Inactive (dome re-appears flipped,
///               barrel re-appears at scale 0, ready for next activation).
///
/// Collision:
///   Area3D on layer 32 (enemies). Monitorable only when Active.
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

    // Pre-fire effect
    private const float PreFireWindow = 0.35f;
    private bool _preFired;

    private Area3D _area = null!;

    // Visual nodes
    private Node3D _domePivot = null!;
    private MeshInstance3D _domeMesh = null!;
    private Node3D _barrelMount = null!;
    private MeshInstance3D _barrelMesh = null!;

    private static readonly Color ColorBase      = new(0.25f, 0.25f, 0.30f);
    private static readonly Color ColorDomeInact = new(0.30f, 0.30f, 0.35f);
    private static readonly Color ColorDomeAct   = new(0.75f, 0.10f, 0.05f);
    private static readonly Color ColorDomeRep   = new(0.60f, 0.45f, 0.05f);
    private static readonly Color ColorBarrel    = new(0.20f, 0.20f, 0.25f);

    private StandardMaterial3D _domeMat = null!;

    private const float BaseSize   = 0.9f;
    private const float DomeRadius = 0.35f;
    private const float BarrelLen  = 0.6f;
    private const float BarrelRad  = 0.08f;

    public override void _Ready()
    {
        _config = GetNode<GameConfig>("/root/GameConfig");
        _rng = new RandomNumberGenerator();
        _rng.Randomize();

        BuildVisuals();
        BuildCollision();

        // Start in Inactive visual state
        SetInactiveVisuals(animate: false);
    }

    // ─── Visual construction ─────────────────────────────────────────────────

    private void BuildVisuals()
    {
        // Try to load multi-part GLB first
        var glbScene = GD.Load<PackedScene>("res://assets/models/enemies/roof_turret.glb");
        if (glbScene != null)
        {
            var glbRoot = glbScene.Instantiate<Node3D>();
            var glbBase   = glbRoot.FindChild("Base")   as MeshInstance3D;
            var glbDome   = glbRoot.FindChild("Dome")   as MeshInstance3D;
            var glbBarrel = glbRoot.FindChild("Barrel") as MeshInstance3D;

            if (glbBase != null && glbDome != null && glbBarrel != null)
            {
                // Re-parent each part into our structure
                BuildFromGlb(glbBase, glbDome, glbBarrel);
                glbRoot.QueueFree();
                return;
            }
            glbRoot.QueueFree();
        }

        BuildProcedural();
    }

    private void BuildFromGlb(MeshInstance3D glbBase, MeshInstance3D glbDome, MeshInstance3D glbBarrel)
    {
        var baseMesh = new MeshInstance3D { Name = "BaseMesh", Mesh = glbBase.Mesh };
        baseMesh.MaterialOverride = new StandardMaterial3D { AlbedoColor = ColorBase };
        AddChild(baseMesh);

        _domePivot = new Node3D { Name = "DomePivot" };
        AddChild(_domePivot);

        _domeMat = new StandardMaterial3D { AlbedoColor = ColorDomeInact };
        _domeMesh = new MeshInstance3D { Name = "DomeMesh", Mesh = glbDome.Mesh };
        _domeMesh.MaterialOverride = _domeMat;
        _domePivot.AddChild(_domeMesh);

        _barrelMount = new Node3D { Name = "BarrelMount" };
        _barrelMount.Position = new Vector3(0f, DomeRadius, 0f);
        _domePivot.AddChild(_barrelMount);

        _barrelMesh = new MeshInstance3D { Name = "BarrelMesh", Mesh = glbBarrel.Mesh };
        _barrelMesh.MaterialOverride = new StandardMaterial3D { AlbedoColor = ColorBarrel };
        _barrelMount.AddChild(_barrelMesh);
    }

    private void BuildProcedural()
    {
        // Base plate
        var baseInst = new MeshInstance3D { Name = "BaseMesh" };
        var baseBox = new BoxMesh { Size = new Vector3(BaseSize, 0.15f, BaseSize) };
        baseInst.Mesh = baseBox;
        baseInst.MaterialOverride = new StandardMaterial3D { AlbedoColor = ColorBase };
        AddChild(baseInst);

        // Dome pivot (flipped 180° when inactive)
        _domePivot = new Node3D { Name = "DomePivot" };
        _domePivot.Position = new Vector3(0f, 0.07f, 0f);
        AddChild(_domePivot);

        _domeMat = new StandardMaterial3D { AlbedoColor = ColorDomeInact };
        _domeMesh = new MeshInstance3D { Name = "DomeMesh" };
        _domeMesh.Mesh = new SphereMesh { Radius = DomeRadius, Height = DomeRadius * 2f, RadialSegments = 8, Rings = 4 };
        _domeMesh.MaterialOverride = _domeMat;
        _domePivot.AddChild(_domeMesh);

        // Barrel mount (offset upward from dome centre so barrel extends from the dome)
        _barrelMount = new Node3D { Name = "BarrelMount" };
        _barrelMount.Position = new Vector3(0f, DomeRadius * 0.5f, 0f);
        _domePivot.AddChild(_barrelMount);

        _barrelMesh = new MeshInstance3D { Name = "BarrelMesh" };
        var barrelCyl = new CylinderMesh
        {
            Height = BarrelLen,
            TopRadius = BarrelRad,
            BottomRadius = BarrelRad,
        };
        _barrelMesh.Mesh = barrelCyl;
        _barrelMesh.MaterialOverride = new StandardMaterial3D { AlbedoColor = ColorBarrel };
        // Position so barrel extends forward (-Z) from the dome
        _barrelMesh.Position = new Vector3(0f, 0f, -BarrelLen * 0.5f);
        _barrelMesh.RotationDegrees = new Vector3(90f, 0f, 0f); // cylinder along Z
        _barrelMount.AddChild(_barrelMesh);
    }

    private void BuildCollision()
    {
        _area = new Area3D
        {
            CollisionLayer = 32u,
            CollisionMask  = 0u,
            Monitorable    = false,
            Monitoring     = false,
            Name           = "Area3D",
        };
        _area.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(BaseSize, BaseSize, BaseSize) }
        });
        AddChild(_area);
    }

    // ─── Visual state helpers ─────────────────────────────────────────────────

    private void SetInactiveVisuals(bool animate)
    {
        _domeMat.AlbedoColor = ColorDomeInact;
        _domePivot.Visible = true;
        _barrelMesh.Visible = true;

        if (animate)
        {
            var tw = CreateTween();
            tw.TweenProperty(_domePivot, "rotation_degrees:x", 180f, 0.4f)
              .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
            tw.Parallel()
              .TweenProperty(_barrelMesh, "scale", Vector3.Zero, 0.3f)
              .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        }
        else
        {
            _domePivot.RotationDegrees = new Vector3(180f, 0f, 0f);
            _barrelMesh.Scale = Vector3.Zero;
        }
    }

    private void SetActiveVisuals()
    {
        _domeMat.AlbedoColor = ColorDomeAct;
        _domePivot.Visible = true;
        _barrelMesh.Visible = true;

        var tw = CreateTween();
        tw.TweenProperty(_domePivot, "rotation_degrees:x", 0f, 0.5f)
          .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.Parallel()
          .TweenProperty(_barrelMesh, "scale", Vector3.One, 0.4f)
          .SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
    }

    private void SetRepairingVisuals()
    {
        // Hide moving parts, play explosion VFX
        _domePivot.Visible = false;
        VfxSpawner.Spawn("turret_repair", GlobalPosition);
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    public void SetPlayerCar(PlayerCar? player) => _playerCar = player;

    public void Activate()
    {
        if (_state != TurretState.Inactive) return;
        _activationPending = true;
    }

    public void TakeDamage(float _amount)
    {
        if (_state != TurretState.Active) return;
        EnterRepairing();
    }

    // ─── Process ─────────────────────────────────────────────────────────────

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

    private void ProcessInactive(float dt)
    {
        _cooldown -= dt;
        if (!_activationPending || _cooldown > 0f) return;

        float dist = GlobalPosition.DistanceTo(_playerCar!.GlobalPosition);
        if (dist > _config.RoofTurretMaxRange)
        {
            _activationPending = false;
            return;
        }

        EnterActive();
    }

    private void ProcessActive(float dt)
    {
        if (!_playerCar!.IsInsideTree()) { EnterInactive(); return; }

        TrackPlayer();

        if (_inBurstPause)
        {
            _burstPauseTimer -= dt;
            if (_burstPauseTimer <= 0f)
            {
                _inBurstPause = false;
                _shotsRemainingInBurst = _config.RoofTurretBurstCount;
                _fireCooldown = 1f / _config.RoofTurretFireRate;
                _preFired = false;
            }
            return;
        }

        _fireCooldown -= dt;

        // Pre-fire VFX
        if (!_preFired && _fireCooldown <= PreFireWindow)
        {
            _preFired = true;
            VfxSpawner.Spawn("turret_prefire", GlobalPosition);
        }

        if (_fireCooldown > 0f) return;

        if (_playerCar.IsFlippingUnder)
        {
            _fireCooldown = 0.2f;
            return;
        }

        Fire();
        _preFired = false;
        _shotsRemainingInBurst--;

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
        _preFired = false;
        _area.SetDeferred(Area3D.PropertyName.Monitorable, true);
        SetActiveVisuals();
        GD.Print($"[RoofTurret] {Name} activated.");
    }

    private void EnterInactive()
    {
        _state = TurretState.Inactive;
        _activationPending = false;
        _cooldown = _config.RoofTurretReactivationTime;
        _area.SetDeferred(Area3D.PropertyName.Monitorable, false);
        SetInactiveVisuals(animate: true);
    }

    private void EnterRepairing()
    {
        _state = TurretState.Repairing;
        _cooldown = _config.RoofTurretRepairTime;
        _area.SetDeferred(Area3D.PropertyName.Monitorable, false);
        _domeMat.AlbedoColor = ColorDomeRep;
        SetRepairingVisuals();
        GD.Print($"[RoofTurret] {Name} damaged — repairing for {_cooldown:F1}s.");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void TrackPlayer()
    {
        // Yaw whole node to face player
        var dir = _playerCar!.GlobalPosition - GlobalPosition;
        var flat = new Vector3(dir.X, 0f, dir.Z);
        if (flat.LengthSquared() > 0.01f)
            RotationDegrees = new Vector3(0f, Mathf.RadToDeg(Mathf.Atan2(-flat.X, -flat.Z)), 0f);

        // Pitch barrel mount to aim vertically
        if (_barrelMount != null && dir.LengthSquared() > 0.01f)
        {
            float pitchRad = Mathf.Atan2(-dir.Y, flat.Length());
            _barrelMount.RotationDegrees = new Vector3(Mathf.RadToDeg(pitchRad), 0f, 0f);
        }
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

        // Muzzle position: tip of barrel in world space
        var muzzlePos = _barrelMount != null
            ? _barrelMount.GlobalPosition + GlobalTransform.Basis.Z * (-BarrelLen)
            : GlobalPosition;
        VfxSpawner.Spawn("turret_muzzle", muzzlePos);

        var bullet = new DroneBullet();
        GetTree().Root.AddChild(bullet);
        bullet.GlobalPosition = muzzlePos;
        bullet.Initialize(targetPos, isHit, _config.RoofTurretBulletSpeed);
    }
}
