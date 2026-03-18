using Godot;

/// <summary>
/// Pool of transparent white box "clouds" floating at varying sky heights.
/// Moves at CloudParallaxFactor × train speed — slower than the ground, giving depth.
/// Clouds despawn behind the player and respawn ahead with randomised sizes and positions.
/// No collision — purely decorative.
/// </summary>
public partial class CloudPool : Node3D
{
    private MeshInstance3D[] _clouds = System.Array.Empty<MeshInstance3D>();
    private float _moveSpeed;
    private float _despawnZ;
    private float _spawnZ;
    private bool _initialized = false;

    private GameConfig _config = null!;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _config = GetNode<GameConfig>("/root/GameConfig");
        _rng.Randomize();
    }

    private void Initialize()
    {
        var trainNode = GetTree().Root.FindChild("Train", true, false);
        float locoZ = (trainNode as TrainBuilder)?.LocomotiveZ ?? 100f;

        _despawnZ = -_config.DespawnBehindDistance;
        _spawnZ   = locoZ + _config.SpawnAheadDistance;

        int count = _config.CloudPoolSize;
        _clouds = new MeshInstance3D[count];

        var mat = new StandardMaterial3D
        {
            AlbedoColor  = new Color(1f, 1f, 1f, 0.25f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode     = BaseMaterial3D.CullModeEnum.Disabled,
        };

        float totalRange = _spawnZ - _despawnZ;

        for (int i = 0; i < count; i++)
        {
            float startZ = _despawnZ + _rng.RandfRange(0f, totalRange);
            var inst = new MeshInstance3D
            {
                Mesh       = new BoxMesh { Size = RandomCloudSize(), Material = mat },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position   = RandomCloudPos(startZ),
            };
            AddChild(inst);
            _clouds[i] = inst;
        }

        GD.Print($"[CloudPool] Initialized {count} clouds. spawnZ={_spawnZ:F0}, despawnZ={_despawnZ:F0}");
        _initialized = true;
    }

    private Vector3 RandomCloudSize()
    {
        float b = _rng.RandfRange(_config.CloudSizeMin, _config.CloudSizeMax);
        return new Vector3(
            b * _rng.RandfRange(1.5f, 3.0f),
            b * _rng.RandfRange(0.3f, 0.7f),
            b * _rng.RandfRange(1.0f, 2.0f)
        );
    }

    private Vector3 RandomCloudPos(float z) => new Vector3(
        _rng.RandfRange(-_config.CloudSpawnSpread, _config.CloudSpawnSpread),
        _rng.RandfRange(_config.CloudHeightMin, _config.CloudHeightMax),
        z
    );

    public void SetTrainSpeed(float speed) => _moveSpeed = speed;

    public override void _Process(double delta)
    {
        if (!_initialized) { Initialize(); return; }

        float cloudSpeed = _moveSpeed * _config.CloudParallaxFactor;
        if (cloudSpeed <= 0f) return;

        float move = cloudSpeed * (float)delta;

        for (int i = 0; i < _clouds.Length; i++)
        {
            var pos = _clouds[i].Position;
            pos.Z -= move;

            if (pos.Z < _despawnZ)
            {
                float maxZ = _spawnZ;
                foreach (var c in _clouds)
                    if (c.Position.Z > maxZ) maxZ = c.Position.Z;

                (_clouds[i].Mesh as BoxMesh)!.Size = RandomCloudSize();
                pos = RandomCloudPos(maxZ + _rng.RandfRange(5f, 20f));
            }

            _clouds[i].Position = pos;
        }
    }
}
