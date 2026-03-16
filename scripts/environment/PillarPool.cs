using Godot;

/// <summary>
/// Maintains a pool of ~8 track-support pillars using pure object repositioning (no runtime instancing).
/// Pillars move in -Z direction (backward) to simulate train moving forward (+Z).
/// When a pillar's Z position exceeds SpawnAheadZ it's beyond the player (behind them),
/// so we teleport it back to negative Z (ahead of the train).
///
/// Pillar spacing: 20 units. Pool count: 8.
/// Pillars sit on both sides of the track (X = ±1.5).
/// </summary>
public partial class PillarPool : Node3D
{
    private const int PoolCount = 8;
    private const float PillarHeight = 9f;
    private const float TrackY = 7f;          // Track surface Y
    private const float PillarY = TrackY - PillarHeight / 2f;
    private const float RecycleBehindZ = 10f; // If pillar.Z < -this, it's behind the player

    public const float PillarX = 0f; // Pillars are centred on the track

    private readonly MeshInstance3D[] _pillars = new MeshInstance3D[PoolCount];
    private float _moveSpeed;
    private float _spacing;

    public override void _Ready()
    {
        _spacing = GetNode<GameConfig>("/root/GameConfig").PillarSpacing;

        var cylinderMesh = new CylinderMesh
        {
            Height = PillarHeight,
            TopRadius = 0.35f,
            BottomRadius = 0.45f
        };

        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.55f, 0.55f, 0.55f)
        };

        var pillarShape = new CylinderShape3D { Radius = 0.45f, Height = PillarHeight };

        for (int i = 0; i < PoolCount; i++)
        {
            var pillar = new MeshInstance3D
            {
                Mesh = cylinderMesh,
                MaterialOverride = mat
            };
            float z = i * _spacing;
            pillar.Position = new Vector3(0f, PillarY, z);

            var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
            body.AddChild(new CollisionShape3D { Shape = pillarShape });
            pillar.AddChild(body);

            AddChild(pillar);
            _pillars[i] = pillar;
        }
    }

    /// <summary>Returns true if any pillar's world Z is within halfRange of worldZ.</summary>
    public bool HasPillarNearZ(float worldZ, float halfRange)
    {
        foreach (var p in _pillars)
            if (p != null && Mathf.Abs(p.GlobalPosition.Z - worldZ) < halfRange)
                return true;
        return false;
    }

    public void SetMoveSpeed(float speed)
    {
        _moveSpeed = speed;
    }

    public override void _Process(double delta)
    {
        if (_moveSpeed <= 0f) return;

        float move = _moveSpeed * (float)delta;

        for (int i = 0; i < _pillars.Length; i++)
        {
            // Move in -Z direction: pillars come from ahead (+Z) and pass behind (-Z)
            _pillars[i].Position -= new Vector3(0f, 0f, move);
        }

        // Recycle pillars that have gone behind the player
        for (int i = 0; i < _pillars.Length; i++)
        {
            if (_pillars[i].Position.Z < -RecycleBehindZ)
            {
                // Find the furthest-ahead pillar (most positive Z) and place after it
                float maxZ = float.MinValue;
                foreach (var p in _pillars)
                    if (p.Position.Z > maxZ) maxZ = p.Position.Z;

                _pillars[i].Position = new Vector3(
                    _pillars[i].Position.X,
                    _pillars[i].Position.Y,
                    maxZ + _spacing
                );
            }
        }
    }
}
