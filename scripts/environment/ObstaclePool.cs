using Godot;

public enum ZoneType { LeftCliff, RightCliff, Roof, Plateau }

/// <summary>
/// Pooled cube obstacles per zone. Four instances sit under ObstacleSystem in Main.tscn.
///
/// Streaming model:
///   Active   — cubes are visible and continuously recycled: when one passes the despawn
///              threshold it teleports to spawnZ (ahead of locomotive).
///   Draining — zone deactivated; cubes keep moving and become invisible as they pass the
///              despawn threshold, rather than being immediately hidden.
///   Idle     — all cubes invisible, ready for the next activation.
///
/// Pool size is computed dynamically on first _Process to cover the full
/// locomotive-ahead → caboose-behind range.
/// </summary>
public partial class ObstaclePool : Node3D
{
    [Export] public ZoneType Zone { get; set; } = ZoneType.LeftCliff;

    private MeshInstance3D[] _cubes = System.Array.Empty<MeshInstance3D>();
    private BoxMesh[] _cubeMeshes = System.Array.Empty<BoxMesh>();
    private bool[] _cubeInStream = System.Array.Empty<bool>();
    private StandardMaterial3D[] _materials = System.Array.Empty<StandardMaterial3D>();

    private GameConfig _config = null!;
    private TrainSpeedManager _tsm = null!;
    private ObstacleManager _om = null!;
    private RandomNumberGenerator _rng = new();

    private float _locoZ;
    private float _despawnZ;
    private bool _wasActive = false;
    private bool _initialized = false;

    // Desert brown palette
    private static readonly Color[] PaletteColors =
    {
        new Color(0.55f, 0.38f, 0.20f),
        new Color(0.60f, 0.42f, 0.22f),
        new Color(0.48f, 0.33f, 0.18f),
        new Color(0.65f, 0.47f, 0.25f),
    };

    public override void _Ready()
    {
        _config = GetNode<GameConfig>("/root/GameConfig");
        _tsm    = GetNode<TrainSpeedManager>("/root/TrainSpeedManager");
        _om     = GetNode<ObstacleManager>("/root/ObstacleManager");
        _rng.Randomize();

        _materials = new StandardMaterial3D[PaletteColors.Length];
        for (int k = 0; k < PaletteColors.Length; k++)
            _materials[k] = new StandardMaterial3D { AlbedoColor = PaletteColors[k] };
    }

    private void Initialize()
    {
        var trainNode = GetTree().Root.FindChild("Train", true, false);
        _locoZ    = (trainNode as TrainBuilder)?.LocomotiveZ ?? 100f;
        _despawnZ = -_config.DespawnBehindDistance;

        float spawnZ     = _locoZ + _config.SpawnAheadDistance;
        float totalRange = spawnZ - _despawnZ;
        int computed     = Mathf.CeilToInt(totalRange / _config.ObstacleCubeSpacing) + 2;
        int poolSize     = Mathf.Max(_config.ObstacleCubePoolSize, computed);

        float zoneWidth  = GetZoneWidth();
        _cubes       = new MeshInstance3D[poolSize];
        _cubeMeshes  = new BoxMesh[poolSize];
        _cubeInStream = new bool[poolSize];

        for (int i = 0; i < poolSize; i++)
        {
            float height = GetRandomHeight();
            var boxMesh = new BoxMesh
            {
                Size     = new Vector3(zoneWidth, height, _config.ObstacleCubeSpacing * 0.85f),
                Material = _materials[_rng.RandiRange(0, _materials.Length - 1)],
            };
            _cubeMeshes[i] = boxMesh;

            var inst = new MeshInstance3D
            {
                Mesh    = boxMesh,
                Visible = false,
                // Parked far ahead, out of sight until section activates
                Position = new Vector3(GetZoneX(), GetZoneY(height), spawnZ + i * _config.ObstacleCubeSpacing),
            };
            AddChild(inst);
            _cubes[i] = inst;
        }

        GD.Print($"[ObstaclePool] {Zone}: {poolSize} cubes, spawnZ={spawnZ:F0}, despawnZ={_despawnZ:F0}");
        _initialized = true;
    }

    public override void _Process(double delta)
    {
        if (!_initialized) { Initialize(); return; }

        bool isActive = IsZoneActive();
        float dt      = (float)delta;
        float spacing = _config.ObstacleCubeSpacing;
        float spawnZ  = _locoZ + _config.SpawnAheadDistance;

        // Detect activation edge: place all cubes into the stream
        if (isActive && !_wasActive)
            PlaceAllCubesIntoStream(spawnZ, spacing);
        _wasActive = isActive;

        for (int i = 0; i < _cubes.Length; i++)
        {
            if (!_cubeInStream[i]) continue;

            var pos = _cubes[i].Position;
            pos.Z -= _tsm.CurrentTrainSpeed * dt;

            if (pos.Z < _despawnZ)
            {
                if (isActive)
                {
                    // Recycle: place ahead of the furthest cube
                    float maxZ = GetMaxStreamZ();
                    float newHeight = GetRandomHeight();
                    _cubeMeshes[i].Size = new Vector3(GetZoneWidth(), newHeight, spacing * 0.85f);
                    _cubeMeshes[i].Material = _materials[_rng.RandiRange(0, _materials.Length - 1)];
                    pos.Z = maxZ + spacing;
                    pos.Y = GetZoneY(newHeight);
                }
                else
                {
                    // Drain: hide and park
                    _cubeInStream[i] = false;
                    _cubes[i].Visible = false;
                    pos = new Vector3(GetZoneX(), _cubes[i].Position.Y,
                                      spawnZ + i * spacing); // park out of sight
                }
            }

            _cubes[i].Position = pos;
        }
    }

    private void PlaceAllCubesIntoStream(float spawnZ, float spacing)
    {
        for (int i = 0; i < _cubes.Length; i++)
        {
            float height = GetRandomHeight();
            _cubeMeshes[i].Size = new Vector3(GetZoneWidth(), height, spacing * 0.85f);
            _cubeMeshes[i].Material = _materials[_rng.RandiRange(0, _materials.Length - 1)];

            float z = spawnZ - i * spacing;
            _cubes[i].Position = new Vector3(GetZoneX(), GetZoneY(height), z);
            _cubes[i].Visible  = z >= _despawnZ; // only show if above despawn line
            _cubeInStream[i]   = true;
        }
    }

    private float GetMaxStreamZ()
    {
        float maxZ = float.MinValue;
        for (int i = 0; i < _cubes.Length; i++)
            if (_cubeInStream[i] && _cubes[i].Position.Z > maxZ)
                maxZ = _cubes[i].Position.Z;
        return maxZ == float.MinValue ? _locoZ + _config.SpawnAheadDistance : maxZ;
    }

    private bool IsZoneActive() => Zone switch
    {
        ZoneType.LeftCliff  => _om.ActiveCliffSide == CliffSide.Left,
        ZoneType.RightCliff => _om.ActiveCliffSide == CliffSide.Right,
        ZoneType.Roof       => _om.ActiveMovementLimit == MovementLimit.Roof,
        ZoneType.Plateau    => _om.ActiveMovementLimit == MovementLimit.Plateau,
        _                   => false,
    };

    private float GetZoneX() => Zone switch
    {
        ZoneType.LeftCliff  => -5.5f,
        ZoneType.RightCliff =>  5.5f,
        _                   =>  0f,
    };

    private float GetZoneY(float height) => Zone switch
    {
        ZoneType.LeftCliff  or ZoneType.RightCliff => height * 0.5f,
        ZoneType.Roof                               => 13f,
        ZoneType.Plateau                            => height * 0.5f,
        _                                           => 0f,
    };

    private float GetRandomHeight() => Zone switch
    {
        ZoneType.LeftCliff  or ZoneType.RightCliff => _rng.RandfRange(8f, 20f),
        ZoneType.Roof                               => _rng.RandfRange(3f, 5f),
        ZoneType.Plateau                            => _rng.RandfRange(3f, 7f),
        _                                           => 5f,
    };

    private float GetZoneWidth() => Zone switch
    {
        ZoneType.LeftCliff  or ZoneType.RightCliff => 3f,
        ZoneType.Roof       or ZoneType.Plateau    => 14f,
        _                                          => 3f,
    };
}
