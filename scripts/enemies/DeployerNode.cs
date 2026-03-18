using Godot;

/// <summary>
/// Sits on top of a carriage. Spawns drones once activated (when nearby containers/clamps
/// are first damaged). Maintains up to MaxDronesPerDeployer active drones on a cooldown.
///
/// When a drone is destroyed: cooldown resets to DeployerCooldown before respawning.
/// Parented to Carriage, created by TrainBuilder.
/// </summary>
public partial class DeployerNode : Node3D
{
    private bool _isActive;
    private float _spawnCooldown;
    private int _livingDrones;
    private GameConfig _config = null!;
    private PlayerCar? _playerCar;
    private RandomNumberGenerator _rng = null!;

    // Visual
    private static readonly Color DeployerColor = new Color(0.35f, 0.15f, 0.15f);

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
        var meshInst = new MeshInstance3D { Name = "MeshSlot" };
        var glbMesh = TryLoadGlbMesh("res://assets/models/enemies/deployer.glb");
        if (glbMesh != null)
        {
            meshInst.Mesh = glbMesh;
        }
        else
        {
            var box = new BoxMesh { Size = new Vector3(1.2f, 0.4f, 0.8f) };
            box.Material = new StandardMaterial3D { AlbedoColor = DeployerColor };
            meshInst.Mesh = box;
        }
        AddChild(meshInst);
    }

    private void BuildCollision()
    {
        // Layer 1 (world/train) — bullets stop here, no damage
        var body = new StaticBody3D { CollisionLayer = 1u, CollisionMask = 0u, Name = "Body" };
        var col = new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.2f, 0.4f, 0.8f) } };
        body.AddChild(col);
        AddChild(body);
    }

    public void SetPlayerCar(PlayerCar? player) => _playerCar = player;

    /// <summary>Connected to ContainerNode.DamageTaken and ClampNode.DamageTaken by TrainBuilder.</summary>
    public void Activate()
    {
        if (_isActive) return;
        _isActive = true;
        _spawnCooldown = 0.5f; // small delay before first drone
        GD.Print($"[DeployerNode] {Name} activated.");
    }

    public void OnDroneDestroyed()
    {
        _livingDrones = Mathf.Max(0, _livingDrones - 1);
        _spawnCooldown = _config.DeployerCooldown;
    }

    /// <summary>Called when a drone returns voluntarily. Shorter cooldown than a destroyed drone.</summary>
    public void OnDroneReturned()
    {
        _livingDrones = Mathf.Max(0, _livingDrones - 1);
        _spawnCooldown = Mathf.Min(_spawnCooldown, _config.DeployerCooldown * 0.5f);
    }

    public override void _Process(double delta)
    {
        if (!_isActive || _playerCar == null) return;

        _spawnCooldown -= (float)delta;
        if (_spawnCooldown <= 0f && _livingDrones < _config.MaxDronesPerDeployer)
        {
            float dist = GlobalPosition.DistanceTo(_playerCar.GlobalPosition);
            if (dist > _config.DroneMaxDeployerDistance)
            {
                _isActive = false;
                GD.Print($"[DeployerNode] {Name} deactivated — player out of range ({dist:F1} > {_config.DroneMaxDeployerDistance}).");
                return;
            }
            SpawnDrone();
        }
    }

    private void SpawnDrone()
    {
        var om = GetNode<ObstacleManager>("/root/ObstacleManager");
        if (om.ActiveMovementLimit == MovementLimit.Roof)
        {
            _spawnCooldown = 1f; // retry after a short delay
            return;
        }

        _spawnCooldown = _config.DeployerCooldown;
        _livingDrones++;

        var drone = new DroneNode();
        GetTree().Root.AddChild(drone);
        drone.GlobalPosition = GlobalPosition;
        drone.Initialize(this, _playerCar!, _config, _rng);
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
