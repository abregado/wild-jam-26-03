using Godot;

/// <summary>
/// Small scattered ground-debris cubes in the canyon obstacle palette.
/// They move at full train speed; their rapid scroll past the player reinforces the sense of
/// velocity.  Rubble sits at Y = halfHeight (bottom face flush with ground at Y=0), randomly
/// tilted, and respawns ahead when it passes the despawn threshold.
/// No collision — purely decorative.
/// </summary>
public partial class RubblePool : Node3D
{
    private MeshInstance3D[] _rubble = System.Array.Empty<MeshInstance3D>();
    private float _moveSpeed;
    private float _despawnZ;
    private float _spawnZ;
    private bool  _initialized = false;

    private GameConfig _config = null!;
    private readonly RandomNumberGenerator _rng = new();

    // Desert brown palette matching ObstaclePool (slightly extended with lighter/darker variants)
    private static readonly Color[] PaletteColors =
    {
        new Color(0.55f, 0.38f, 0.20f),
        new Color(0.60f, 0.42f, 0.22f),
        new Color(0.48f, 0.33f, 0.18f),
        new Color(0.65f, 0.47f, 0.25f),
        new Color(0.40f, 0.27f, 0.13f),
        new Color(0.70f, 0.52f, 0.28f),
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

        _despawnZ = -_config.DespawnBehindDistance;
        _spawnZ   = locoZ + _config.SpawnAheadDistance;

        int count = _config.RubblePoolSize;
        _rubble = new MeshInstance3D[count];

        float totalRange = _spawnZ - _despawnZ;

        for (int i = 0; i < count; i++)
        {
            float sx, sy, sz;
            MakeRubbleSize(out sx, out sy, out sz);

            var inst = new MeshInstance3D
            {
                Mesh     = new BoxMesh
                {
                    Size     = new Vector3(sx, sy, sz),
                    Material = _materials[_rng.RandiRange(0, _materials.Length - 1)],
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position   = new Vector3(
                    RandomRubbleX(),
                    sy * 0.5f,
                    _despawnZ + _rng.RandfRange(0f, totalRange)
                ),
                RotationDegrees = new Vector3(
                    _rng.RandfRange(-25f, 25f),
                    _rng.RandfRange(0f, 360f),
                    _rng.RandfRange(-25f, 25f)
                ),
            };
            AddChild(inst);
            _rubble[i] = inst;
        }

        GD.Print($"[RubblePool] Initialized {count} rubble pieces. spawnZ={_spawnZ:F0}, despawnZ={_despawnZ:F0}");
        _initialized = true;
    }

    private void MakeRubbleSize(out float sx, out float sy, out float sz)
    {
        float b = _rng.RandfRange(_config.RubbleSizeMin, _config.RubbleSizeMax);
        sx = b * _rng.RandfRange(0.6f, 1.5f);
        sy = b * _rng.RandfRange(0.5f, 1.0f);
        sz = b * _rng.RandfRange(0.6f, 1.5f);
    }

    private float RandomRubbleX()
    {
        float spread = _config.RubbleSpread;
        // Avoid ±2 unit band directly under the elevated track
        float x = _rng.RandfRange(-spread, spread);
        if (Mathf.Abs(x) < 2f)
            x = (x >= 0f ? 1f : -1f) * _rng.RandfRange(2f, spread);
        return x;
    }

    public void SetMoveSpeed(float speed) => _moveSpeed = speed;

    public override void _Process(double delta)
    {
        if (!_initialized) { Initialize(); return; }
        if (_moveSpeed <= 0f) return;

        float move = _moveSpeed * (float)delta;

        for (int i = 0; i < _rubble.Length; i++)
        {
            var pos = _rubble[i].Position;
            pos.Z -= move;

            if (pos.Z < _despawnZ)
            {
                float sx, sy, sz;
                MakeRubbleSize(out sx, out sy, out sz);

                (_rubble[i].Mesh as BoxMesh)!.Size     = new Vector3(sx, sy, sz);
                (_rubble[i].Mesh as BoxMesh)!.Material = _materials[_rng.RandiRange(0, _materials.Length - 1)];
                _rubble[i].RotationDegrees = new Vector3(
                    _rng.RandfRange(-25f, 25f),
                    _rng.RandfRange(0f, 360f),
                    _rng.RandfRange(-25f, 25f)
                );

                pos = new Vector3(
                    RandomRubbleX(),
                    sy * 0.5f,
                    _spawnZ + _rng.RandfRange(0f, 10f)
                );
            }

            _rubble[i].Position = pos;
        }
    }
}
