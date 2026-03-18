using Godot;

/// <summary>
/// Enemy drone deployed from a DeployerNode on top of train carriages.
///
/// State machine:
///   Deploying          → fly upward 3 units from deployer, then MovingToPosition
///   MovingToPosition   → fly to combat position at DroneMoveSpeed, then InPosition
///   InPosition         → wait for fire cooldown; if player too far → Repositioning;
///                        otherwise fire, then maybe Repositioning
///   Repositioning      → fly to new combat position at DroneCombatSpeed, then InPosition
///   FollowingSide      → fly over train top to follow player side-switch, then MovingToPosition
///   ReturningToDeployer → two-phase landing: fly above deployer, then descend and despawn
///   Dying              → fall to ground (gravity), QueueFree after landing
///
/// Range checks (every frame, all active states):
///   - Distance to deployer > DroneMaxDeployerDistance → ReturningToDeployer
///   - Distance to player   > DroneChaseDistance (InPosition only) → Repositioning
///
/// Collision:
///   Area3D on layer 32 (bit 6 = drones). Player bullet raycasts (mask 39) detect this.
/// </summary>
public partial class DroneNode : Node3D
{
    private enum DroneState
    {
        Deploying, MovingToPosition, InPosition, Repositioning,
        FollowingSide, ReturningToDeployer, Dying
    }

    private DroneState _state = DroneState.Deploying;
    private DeployerNode _deployer = null!;
    private PlayerCar _playerCar = null!;
    private GameConfig _config = null!;
    private RandomNumberGenerator _rng = null!;

    private float _hp;
    private float _fireCooldown;
    private Vector3 _combatTarget;
    private bool _lastKnownPlayerSide;
    private Vector3 _followWaypoint;
    private bool _crossedOverTop;
    private float _deployLiftTarget;
    private float _dyingTimer;
    private float _dyingVelocity;

    // Return-to-deployer: phase 0 = fly to hover point, phase 1 = descend
    private int _returnPhase;
    private Vector3 _returnHoverPoint;

    private Area3D _area = null!;

    private const float OverTrainHeight = 14f;
    private const float ArrivalThreshold = 0.5f;
    private const float DyingGravity = 12f;
    private const float DyingDuration = 2.0f;
    private const float ReturnHoverHeight = 4f;
    private const float LandingSpeed = 3f;

    public void Initialize(DeployerNode deployer, PlayerCar player, GameConfig config, RandomNumberGenerator rng)
    {
        _deployer = deployer;
        _playerCar = player;
        _config = config;
        _rng = rng;

        _hp = config.DroneHitpoints;
        _lastKnownPlayerSide = player.IsOnRightSide;
        _deployLiftTarget = GlobalPosition.Y + 3f;
        _fireCooldown = 1f / config.DroneFireRate;
    }

    public override void _Ready()
    {
        var meshInst = new MeshInstance3D { Name = "MeshSlot" };
        var glbMesh = TryLoadGlbMesh("res://assets/models/enemies/drone.glb");
        if (glbMesh != null)
        {
            meshInst.Mesh = glbMesh;
        }
        else
        {
            var box = new BoxMesh { Size = new Vector3(0.8f, 0.25f, 0.8f) };
            box.Material = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.2f, 0.25f) };
            meshInst.Mesh = box;
        }
        AddChild(meshInst);

        // Area3D on layer 32 (drones, bit 6). Player bullets use mask 39 which includes 32.
        _area = new Area3D
        {
            CollisionLayer = 32u,
            CollisionMask = 0u,
            Monitorable = true,
            Monitoring = false,
            Name = "Area3D",
        };
        _area.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.5f } });
        AddChild(_area);
    }

    public void TakeDamage(float amount)
    {
        if (_state == DroneState.Dying || _state == DroneState.ReturningToDeployer) return;
        _hp -= amount;
        if (_hp <= 0f)
            StartDying();
    }

    public override void _Process(double delta)
    {
        if (_config == null) return;
        if (_playerCar == null || !_playerCar.IsInsideTree()) { QueueFree(); return; }

        float dt = (float)delta;

        // ── Range checks (skip during terminal/transit states) ─────────────
        if (_state != DroneState.Dying
            && _state != DroneState.ReturningToDeployer
            && _state != DroneState.Deploying)
        {
            float distToDeployer = GlobalPosition.DistanceTo(_deployer.GlobalPosition);
            if (distToDeployer > _config.DroneMaxDeployerDistance)
            {
                var om = GetNode<ObstacleManager>("/root/ObstacleManager");
                if (om.ActiveMovementLimit == MovementLimit.Roof)
                    StartDying();
                else
                    StartReturning();
                return;
            }
        }

        // ── Side-switch detection ──────────────────────────────────────────
        bool playerOnRight = _playerCar.IsOnRightSide;
        if (playerOnRight != _lastKnownPlayerSide
            && _state != DroneState.Dying
            && _state != DroneState.Deploying
            && _state != DroneState.FollowingSide
            && _state != DroneState.ReturningToDeployer)
        {
            _lastKnownPlayerSide = playerOnRight;
            StartFollowingSide();
        }

        switch (_state)
        {
            case DroneState.Deploying:            ProcessDeploying(dt);       break;
            case DroneState.MovingToPosition:     ProcessMoving(dt, _config.DroneMoveSpeed);   break;
            case DroneState.InPosition:           ProcessInPosition(dt);      break;
            case DroneState.Repositioning:        ProcessMoving(dt, _config.DroneCombatSpeed); break;
            case DroneState.FollowingSide:        ProcessFollowingSide(dt);   break;
            case DroneState.ReturningToDeployer:  ProcessReturning(dt);       break;
            case DroneState.Dying:                ProcessDying(dt);           break;
        }
    }

    // ─── State processors ────────────────────────────────────────────────────

    private void ProcessDeploying(float dt)
    {
        float newY = GlobalPosition.Y + _config.DroneMoveSpeed * dt;
        GlobalPosition = new Vector3(GlobalPosition.X, Mathf.Min(newY, _deployLiftTarget), GlobalPosition.Z);

        if (GlobalPosition.Y >= _deployLiftTarget - 0.1f)
        {
            _combatTarget = ComputeCombatPosition();
            _state = DroneState.MovingToPosition;
        }
    }

    private void ProcessMoving(float dt, float speed)
    {
        var dir = _combatTarget - GlobalPosition;
        float dist = dir.Length();
        if (dist < ArrivalThreshold)
        {
            GlobalPosition = _combatTarget;
            _fireCooldown = 1f / _config.DroneFireRate;
            _state = DroneState.InPosition;
            return;
        }
        GlobalPosition += dir.Normalized() * speed * dt;
        LookToward(dir);
    }

    private void ProcessInPosition(float dt)
    {
        // If player is out of chase range, reposition toward them instead of shooting
        float distToPlayer = GlobalPosition.DistanceTo(_playerCar.GlobalPosition);
        if (distToPlayer > _config.DroneChaseDistance)
        {
            _combatTarget = ComputeCombatPosition();
            _state = DroneState.Repositioning;
            return;
        }

        _fireCooldown -= dt;
        if (_fireCooldown <= 0f)
        {
            Fire();
            if (_rng.Randf() < _config.DroneRepositionChance)
            {
                _combatTarget = ComputeCombatPosition();
                _state = DroneState.Repositioning;
            }
            else
            {
                _fireCooldown = 1f / _config.DroneFireRate;
            }
        }
    }

    private void ProcessFollowingSide(float dt)
    {
        var dir = _followWaypoint - GlobalPosition;
        if (dir.Length() < ArrivalThreshold)
        {
            _combatTarget = ComputeCombatPosition();
            _state = DroneState.MovingToPosition;
            return;
        }
        GlobalPosition += dir.Normalized() * _config.DroneMoveSpeed * dt;
    }

    private void ProcessReturning(float dt)
    {
        if (_returnPhase == 0)
        {
            // Fly to hover point above deployer
            var dir = _returnHoverPoint - GlobalPosition;
            if (dir.Length() < ArrivalThreshold)
            {
                _returnPhase = 1;
                return;
            }
            GlobalPosition += dir.Normalized() * _config.DroneMoveSpeed * dt;
            LookToward(dir);
        }
        else
        {
            // Descend slowly onto deployer
            var target = _deployer.GlobalPosition;
            var dir = target - GlobalPosition;
            if (dir.Length() < 0.2f)
            {
                // Landed — scale down and free
                var tween = CreateTween();
                tween.TweenProperty(this, "scale", Vector3.Zero, 0.3f)
                     .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                tween.TweenCallback(Callable.From(QueueFree));
                _deployer.OnDroneReturned();
                // Prevent re-entry into this branch
                _state = DroneState.Dying;
                return;
            }
            GlobalPosition += dir.Normalized() * LandingSpeed * dt;
        }
    }

    private void ProcessDying(float dt)
    {
        _dyingVelocity += DyingGravity * dt;
        GlobalPosition -= new Vector3(0f, _dyingVelocity * dt, 0f);
        _dyingTimer += dt;
        RotationDegrees += new Vector3(90f * dt, 120f * dt, 60f * dt);
        if (_dyingTimer >= DyingDuration)
            QueueFree();
    }

    // ─── State starters ──────────────────────────────────────────────────────

    private void StartDying()
    {
        _state = DroneState.Dying;
        _dyingTimer = 0f;
        _dyingVelocity = 0f;
        _deployer.OnDroneDestroyed();
        _area.SetDeferred(Area3D.PropertyName.Monitorable, false);
    }

    private void StartFollowingSide()
    {
        _state = DroneState.FollowingSide;
        _followWaypoint = new Vector3(0f, OverTrainHeight, GlobalPosition.Z);
        _lastKnownPlayerSide = _playerCar.IsOnRightSide;
    }

    private void StartReturning()
    {
        _state = DroneState.ReturningToDeployer;
        _returnPhase = 0;
        _returnHoverPoint = _deployer.GlobalPosition + new Vector3(0f, ReturnHoverHeight, 0f);
        _area.SetDeferred(Area3D.PropertyName.Monitorable, false);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void Fire()
    {
        bool isHit = _rng.Randf() < _config.DroneHitChance;
        Vector3 targetPos;

        if (isHit)
        {
            targetPos = _playerCar.GlobalPosition + new Vector3(
                _rng.RandfRange(-0.15f, 0.15f),
                _rng.RandfRange(-0.15f, 0.15f),
                _rng.RandfRange(-0.15f, 0.15f));
        }
        else
        {
            float missX = _rng.RandfRange(0.6f, 1.2f) * (_rng.Randf() > 0.5f ? 1f : -1f);
            float missY = _rng.RandfRange(0.6f, 1.2f) * (_rng.Randf() > 0.5f ? 1f : -1f);
            targetPos = _playerCar.GlobalPosition + new Vector3(missX, missY, 0f);
        }

        var bullet = new DroneBullet();
        GetTree().Root.AddChild(bullet);
        bullet.GlobalPosition = GlobalPosition;
        bullet.Initialize(targetPos, isHit, _config.DroneBulletSpeed);
    }

    private Vector3 ComputeCombatPosition()
    {
        float side = _playerCar.IsOnRightSide ? 1f : -1f;
        float x = side * PlayerCar.XOffset + _rng.RandfRange(-1.2f, 1.2f);
        float y = _playerCar.GlobalPosition.Y
                  + _rng.RandfRange(_config.DroneHeightMin, _config.DroneHeightMax);
        float z = _playerCar.GlobalPosition.Z + _rng.RandfRange(-4f, 4f);
        return new Vector3(x, y, z);
    }

    private void LookToward(Vector3 dir)
    {
        if (dir.LengthSquared() < 0.01f) return;
        var flat = new Vector3(dir.X, 0f, dir.Z);
        if (flat.LengthSquared() > 0.01f)
            RotationDegrees = new Vector3(0f, Mathf.RadToDeg(Mathf.Atan2(-flat.X, -flat.Z)), 0f);
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
