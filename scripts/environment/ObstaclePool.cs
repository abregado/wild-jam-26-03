using Godot;

public enum ZoneType { LeftCliff, RightCliff, Roof, Plateau }

/// <summary>
/// Pooled cube obstacles per zone. Four instances sit under ObstacleSystem in Main.tscn.
///
/// Streaming model — cubes spawn one at a time from ahead of the locomotive:
///   Streaming  — IsZoneActive() true (active section OR matching warning): a new cube is placed
///                at spawnZ whenever the front of the stream has moved far enough back. Cubes that
///                pass despawnZ are parked and re-used when next needed at the front.
///   Draining   — IsZoneActive() false: no new spawns. In-stream cubes keep moving and park
///                themselves when they pass despawnZ. Section ends naturally — no instant hide.
///
/// IsZoneActive() returns true during BOTH the active phase AND the warning phase for this zone,
/// so obstacles visually approach the player before the section formally activates.
///
/// Pool is sized dynamically on first _Process to cover locoZ+spawnAhead → despawnBehind.
///
/// Cliff zones (LeftCliff/RightCliff) additionally create StaticBody3D colliders on layer 8 (128)
/// so PlayerCar's forward raycast can detect an approaching cliff wall.
/// </summary>
public partial class ObstaclePool : Node3D
{
    [Export] public ZoneType Zone { get; set; } = ZoneType.LeftCliff;

    private MeshInstance3D[] _cubes = System.Array.Empty<MeshInstance3D>();
    private BoxMesh[] _cubeMeshes = System.Array.Empty<BoxMesh>();
    private bool[] _cubeInStream = System.Array.Empty<bool>();
    private StandardMaterial3D[] _materials = System.Array.Empty<StandardMaterial3D>();

    // Cliff-only: physics bodies for raycast detection (layer 8 = 128)
    private StaticBody3D[] _cliffBodies = System.Array.Empty<StaticBody3D>();
    private bool _isCliffZone;

    private GameConfig _config = null!;
    private TrainSpeedManager _tsm = null!;
    private ObstacleManager _om = null!;
    private RandomNumberGenerator _rng = new();

    private float _locoZ;
    private float _despawnZ;
    private bool _initialized = false;

    // Collision body width wide enough to intercept a forward ray from the car at X=±8
    private const float CliffBodyWidth = 10f;
    private const int CliffCollisionLayer = 128; // layer 8

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

        _isCliffZone = Zone == ZoneType.LeftCliff || Zone == ZoneType.RightCliff;

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

        _cubes        = new MeshInstance3D[poolSize];
        _cubeMeshes   = new BoxMesh[poolSize];
        _cubeInStream = new bool[poolSize];

        if (_isCliffZone)
            _cliffBodies = new StaticBody3D[poolSize];

        float zoneWidth = GetZoneWidth();
        float spacing   = _config.ObstacleCubeSpacing;

        for (int i = 0; i < poolSize; i++)
        {
            float height = GetRandomHeight();
            var boxMesh = new BoxMesh
            {
                Size     = new Vector3(zoneWidth, height, spacing * 0.85f),
                Material = _materials[_rng.RandiRange(0, _materials.Length - 1)],
            };
            _cubeMeshes[i] = boxMesh;

            // All cubes start parked far ahead, invisible
            var parkPos = new Vector3(GetZoneX(), GetZoneY(height), spawnZ + (i + 1) * spacing);
            var inst = new MeshInstance3D
            {
                Mesh     = boxMesh,
                Visible  = false,
                Position = parkPos,
            };
            AddChild(inst);
            _cubes[i] = inst;

            // Create cliff collision body (parked = layer 0 = disabled)
            if (_isCliffZone)
            {
                var shape = new BoxShape3D
                {
                    Size = new Vector3(CliffBodyWidth, height, spacing * 0.85f),
                };
                var col = new CollisionShape3D { Shape = shape };
                var body = new StaticBody3D
                {
                    CollisionLayer = 0, // disabled until in-stream
                    CollisionMask  = 0,
                    Position       = parkPos,
                };
                body.AddChild(col);
                AddChild(body);
                _cliffBodies[i] = body;
            }
        }

        GD.Print($"[ObstaclePool] {Zone}: {poolSize} cubes, spawnZ={spawnZ:F0}, despawnZ={_despawnZ:F0}");
        _initialized = true;
    }

    public override void _Process(double delta)
    {
        if (!_initialized) { Initialize(); return; }

        bool streaming = IsZoneActive();
        float dt       = (float)delta;
        float spacing  = _config.ObstacleCubeSpacing;
        float spawnZ   = _locoZ + _config.SpawnAheadDistance;

        // Move all in-stream cubes; park them when they pass the despawn threshold
        for (int i = 0; i < _cubes.Length; i++)
        {
            if (!_cubeInStream[i]) continue;

            var pos = _cubes[i].Position;
            pos.Z -= _tsm.CurrentTrainSpeed * dt;

            if (pos.Z < _despawnZ)
            {
                // Park — whether streaming or draining, park and let the front-spawn refill if needed
                _cubeInStream[i]  = false;
                _cubes[i].Visible = false;
                var parkPos = new Vector3(GetZoneX(), _cubes[i].Position.Y, spawnZ + 100f);
                _cubes[i].Position = parkPos;
                if (_isCliffZone)
                {
                    _cliffBodies[i].CollisionLayer = 0;
                    _cliffBodies[i].Position = parkPos;
                }
                continue;
            }

            _cubes[i].Position = pos;
            if (_isCliffZone)
                _cliffBodies[i].Position = pos;
        }

        // While streaming: emit one cube from spawnZ whenever the front has a gap
        if (streaming)
        {
            float frontZ = GetFrontStreamZ();
            // frontZ == float.MinValue means no cubes in stream at all
            if (frontZ < spawnZ - spacing)
            {
                int idx = FindParkedCube();
                if (idx >= 0)
                {
                    float height = GetRandomHeight();
                    float depth  = spacing * 0.85f;
                    _cubeMeshes[idx].Size     = new Vector3(GetZoneWidth(), height, depth);
                    _cubeMeshes[idx].Material = _materials[_rng.RandiRange(0, _materials.Length - 1)];
                    var streamPos = new Vector3(GetZoneX(), GetZoneY(height), spawnZ);
                    _cubes[idx].Position  = streamPos;
                    _cubes[idx].Visible   = true;
                    _cubeInStream[idx]    = true;

                    if (_isCliffZone)
                    {
                        // Update collision shape height to match new cube
                        if (_cliffBodies[idx].GetChild(0) is CollisionShape3D cs
                            && cs.Shape is BoxShape3D bs)
                            bs.Size = new Vector3(CliffBodyWidth, height, depth);
                        _cliffBodies[idx].Position       = streamPos;
                        _cliffBodies[idx].CollisionLayer = CliffCollisionLayer;
                    }
                }
            }
        }
    }

    // Returns true during the active phase AND during the warning phase for this zone,
    // so cubes begin streaming before the section formally activates.
    private bool IsZoneActive() => Zone switch
    {
        ZoneType.LeftCliff  => _om.ActiveCliffSide == CliffSide.Left
                            || (_om.IsInWarning && _om.UpcomingCliffSide == CliffSide.Left),
        ZoneType.RightCliff => _om.ActiveCliffSide == CliffSide.Right
                            || (_om.IsInWarning && _om.UpcomingCliffSide == CliffSide.Right),
        ZoneType.Roof       => _om.ActiveMovementLimit == MovementLimit.Roof
                            || (_om.IsInWarning && _om.UpcomingMovementLimit == MovementLimit.Roof),
        ZoneType.Plateau    => _om.ActiveMovementLimit == MovementLimit.Plateau
                            || (_om.IsInWarning && _om.UpcomingMovementLimit == MovementLimit.Plateau),
        _                   => false,
    };

    private float GetFrontStreamZ()
    {
        float maxZ = float.MinValue;
        for (int i = 0; i < _cubes.Length; i++)
            if (_cubeInStream[i] && _cubes[i].Position.Z > maxZ)
                maxZ = _cubes[i].Position.Z;
        return maxZ;
    }

    private int FindParkedCube()
    {
        for (int i = 0; i < _cubes.Length; i++)
            if (!_cubeInStream[i]) return i;
        return -1;
    }

    // Inner edge of cliff wall = half carriage width (1.5) + 1.5× container width (2.0) = 4.5
    private const float CliffInnerEdge = 1.5f + 1.5f * 2.0f;

    private float GetZoneX()
    {
        if (Zone == ZoneType.RightCliff) return  CliffInnerEdge + _config.CliffCubeWidth * 0.5f;
        if (Zone == ZoneType.LeftCliff)  return -(CliffInnerEdge + _config.CliffCubeWidth * 0.5f);
        return 0f;
    }

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
        ZoneType.LeftCliff  or ZoneType.RightCliff => _config.CliffCubeWidth,
        ZoneType.Roof       or ZoneType.Plateau    => 14f,
        _                                          => 3f,
    };
}
