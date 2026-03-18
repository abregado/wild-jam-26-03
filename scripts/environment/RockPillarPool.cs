using Godot;

/// <summary>
/// Distant rock-pillar formations on both sides of the track, giving the desert canyon a backdrop.
/// Each side (left / right) is an independent pool of tall boulder-like boxes in the obstacle
/// palette colours.  They move at full train speed — same as the ground — and recycle at the
/// despawn distance.  The two sides are initialised with a half-spacing stagger so they don't
/// mirror each other perfectly.
/// No collision — purely decorative.
/// </summary>
public partial class RockPillarPool : Node3D
{
    private MeshInstance3D[] _left  = System.Array.Empty<MeshInstance3D>();
    private MeshInstance3D[] _right = System.Array.Empty<MeshInstance3D>();

    private float _spawnZ;
    private float _despawnZ;
    private float _moveSpeed;
    private bool  _initialized = false;

    private GameConfig _config = null!;
    private readonly RandomNumberGenerator _rng = new();

    // Desert brown palette matching ObstaclePool
    private static readonly Color[] PaletteColors =
    {
        new Color(0.55f, 0.38f, 0.20f),
        new Color(0.60f, 0.42f, 0.22f),
        new Color(0.48f, 0.33f, 0.18f),
        new Color(0.65f, 0.47f, 0.25f),
        new Color(0.42f, 0.28f, 0.14f),
    };

    private StandardMaterial3D[] _materials = System.Array.Empty<StandardMaterial3D>();

    public override void _Ready()
    {
        _config = GetNode<GameConfig>("/root/GameConfig");
        _rng.Randomize();

        _materials = new StandardMaterial3D[PaletteColors.Length];
        for (int k = 0; k < PaletteColors.Length; k++)
            _materials[k] = new StandardMaterial3D { AlbedoColor = PaletteColors[k] };
    }

    private void Initialize()
    {
        var trainNode = GetTree().Root.FindChild("Train", true, false);
        float locoZ = (trainNode as TrainBuilder)?.LocomotiveZ ?? 100f;

        float spacing = _config.RockPillarSpacing;
        _despawnZ = -_config.DespawnBehindDistance;
        _spawnZ   = locoZ + _config.SpawnAheadDistance;

        float totalRange = _spawnZ - _despawnZ;
        int count = Mathf.CeilToInt(totalRange / spacing) + 1;

        _left  = new MeshInstance3D[count];
        _right = new MeshInstance3D[count];

        float dist = _config.RockPillarDistance;

        for (int i = 0; i < count; i++)
        {
            float zL = _spawnZ - i * spacing;
            float zR = _spawnZ - i * spacing - spacing * 0.5f; // stagger right side

            _left[i]  = CreateRock(-(dist + _rng.RandfRange(-4f, 4f)), zL);
            _right[i] = CreateRock( (dist + _rng.RandfRange(-4f, 4f)), zR);
        }

        GD.Print($"[RockPillarPool] Initialized {count}×2 rocks. spawnZ={_spawnZ:F0}, despawnZ={_despawnZ:F0}");
        _initialized = true;
    }

    private MeshInstance3D CreateRock(float x, float z)
    {
        float h = _rng.RandfRange(_config.RockPillarHeightMin, _config.RockPillarHeightMax);
        float w = _rng.RandfRange(3f, 8f);
        float d = _rng.RandfRange(3f, 10f);

        var inst = new MeshInstance3D
        {
            Mesh     = new BoxMesh
            {
                Size     = new Vector3(w, h, d),
                Material = _materials[_rng.RandiRange(0, _materials.Length - 1)],
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position   = new Vector3(x, h * 0.5f, z),
        };
        AddChild(inst);
        return inst;
    }

    private void RecycleRock(MeshInstance3D rock, float side, float maxZ)
    {
        float dist = _config.RockPillarDistance;
        float h = _rng.RandfRange(_config.RockPillarHeightMin, _config.RockPillarHeightMax);
        float w = _rng.RandfRange(3f, 8f);
        float d = _rng.RandfRange(3f, 10f);

        (rock.Mesh as BoxMesh)!.Size     = new Vector3(w, h, d);
        (rock.Mesh as BoxMesh)!.Material = _materials[_rng.RandiRange(0, _materials.Length - 1)];
        rock.Position = new Vector3(
            side * (dist + _rng.RandfRange(-4f, 4f)),
            h * 0.5f,
            maxZ + _config.RockPillarSpacing
        );
    }

    public void SetMoveSpeed(float speed) => _moveSpeed = speed;

    public override void _Process(double delta)
    {
        if (!_initialized) { Initialize(); return; }
        if (_moveSpeed <= 0f) return;

        float move = _moveSpeed * (float)delta;
        MoveAndRecycle(_left,  -1f, move);
        MoveAndRecycle(_right, +1f, move);
    }

    private void MoveAndRecycle(MeshInstance3D[] pool, float side, float move)
    {
        for (int i = 0; i < pool.Length; i++)
        {
            var pos = pool[i].Position;
            pos.Z -= move;

            if (pos.Z < _despawnZ)
            {
                float maxZ = float.MinValue;
                foreach (var r in pool)
                    if (r.Position.Z > maxZ) maxZ = r.Position.Z;

                RecycleRock(pool[i], side, maxZ);
                continue;
            }

            pool[i].Position = pos;
        }
    }
}
