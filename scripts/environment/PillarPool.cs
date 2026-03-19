using Godot;

/// <summary>
/// Maintains a pool of track-support pillars using object repositioning (no runtime instancing).
/// Pillars move in -Z direction (backward) to simulate train moving forward (+Z).
///
/// Spawn/despawn distances are config-driven (SpawnAheadDistance, DespawnBehindDistance).
/// Pool size is computed on first _Process to cover the full train+ahead+behind range.
/// Deferred init is needed because TrackEnvironment (parent scene) loads before Train in Main.tscn.
/// </summary>
public partial class PillarPool : Node3D
{
    private const float PillarHeight = 6.0f;
    private const float TrackY = 7f;

    public const float PillarX = 0f;

    private float _pillarY;
    private Node3D[] _pillars = System.Array.Empty<Node3D>();
    private PackedScene? _pillarScene;
    private float _spacing;
    private float _xSpread;
    private float _despawnZ;
    private float _moveSpeed;
    private bool _initialized = false;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        var config = GetNode<GameConfig>("/root/GameConfig");
        _spacing   = config.PillarSpacing;
        _xSpread   = config.PillarXSpread;
        _despawnZ  = -config.DespawnBehindDistance;
        _pillarY   = TrackY + config.PillarYOffset;
        _rng.Randomize();
        _pillarScene = GD.Load<PackedScene>("res://assets/models/environment/pillar.glb");
    }

    private void Initialize()
    {
        var config = GetNode<GameConfig>("/root/GameConfig");
        var trainNode = GetTree().Root.FindChild("Train", true, false);
        float locoZ = (trainNode as TrainBuilder)?.LocomotiveZ ?? 100f;

        float spawnZ   = locoZ + config.SpawnAheadDistance;
        float totalRange = spawnZ - _despawnZ;
        int poolCount  = Mathf.CeilToInt(totalRange / _spacing) + 1;

        _pillars = new Node3D[poolCount];
        for (int i = 0; i < poolCount; i++)
        {
            Node3D pillar = _pillarScene != null
                ? CreatePillarFromGlb(_pillarScene)
                : CreatePillarProcedural();

            pillar.Position = new Vector3(_rng.RandfRange(-_xSpread, _xSpread), _pillarY, spawnZ - i * _spacing);
            AddChild(pillar);
            _pillars[i] = pillar;
        }

        GD.Print($"[PillarPool] Initialized {poolCount} pillars. spawnZ={spawnZ:F0}, despawnZ={_despawnZ:F0}");
        _initialized = true;
    }

    private static readonly Color PillarColor = new(0.55f, 0.55f, 0.55f);

    private static Node3D CreatePillarFromGlb(PackedScene scene)
    {
        var pillar = scene.Instantiate<Node3D>();
        foreach (var child in pillar.GetChildren())
        {
            if (child is StaticBody3D body)
            {
                body.CollisionLayer = 1;
                body.CollisionMask  = 0;
            }
            if (child is MeshInstance3D mesh)
                mesh.MaterialOverride = new StandardMaterial3D { AlbedoColor = PillarColor };
        }
        return pillar;
    }

    private static Node3D CreatePillarProcedural()
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh { Height = PillarHeight, TopRadius = 0.35f, BottomRadius = 0.45f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.55f, 0.55f) },
        };
        var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.45f, Height = PillarHeight } });
        mesh.AddChild(body);
        return mesh;
    }

    /// <summary>Returns true if any pillar's world Z is within halfRange of worldZ.</summary>
    public bool HasPillarNearZ(float worldZ, float halfRange)
    {
        foreach (var p in _pillars)
            if (p != null && Mathf.Abs(p.GlobalPosition.Z - worldZ) < halfRange)
                return true;
        return false;
    }

    public void SetMoveSpeed(float speed) => _moveSpeed = speed;

    public override void _Process(double delta)
    {
        if (!_initialized) { Initialize(); return; }
        if (_moveSpeed <= 0f) return;

        float move = _moveSpeed * (float)delta;

        for (int i = 0; i < _pillars.Length; i++)
            _pillars[i].Position -= new Vector3(0f, 0f, move);

        for (int i = 0; i < _pillars.Length; i++)
        {
            if (_pillars[i].Position.Z < _despawnZ)
            {
                float maxZ = float.MinValue;
                foreach (var p in _pillars)
                    if (p.Position.Z > maxZ) maxZ = p.Position.Z;

                _pillars[i].Position = new Vector3(
                    _rng.RandfRange(-_xSpread, _xSpread),
                    _pillars[i].Position.Y,
                    maxZ + _spacing
                );
            }
        }
    }
}
