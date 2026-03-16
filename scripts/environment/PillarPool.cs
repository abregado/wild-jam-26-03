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
    private const float Spacing = 20f;
    private const float PillarHeight = 9f;
    private const float TrackY = 7f;          // Track surface Y
    private const float PillarY = TrackY - PillarHeight / 2f;
    private const float RecycleBehindZ = 10f; // If pillar.Z < -this, it's behind the player

    private readonly MeshInstance3D[] _pillars = new MeshInstance3D[PoolCount];
    private float _moveSpeed;

    public override void _Ready()
    {
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

        for (int i = 0; i < PoolCount; i++)
        {
            var pillar = new MeshInstance3D
            {
                Mesh = cylinderMesh,
                MaterialOverride = mat
            };
            // Spread pillars ahead (positive Z = ahead in our convention)
            float z = i * Spacing;
            // Alternate left/right of track
            float x = (i % 2 == 0) ? -1.5f : 1.5f;
            pillar.Position = new Vector3(x, PillarY, z);
            AddChild(pillar);
            _pillars[i] = pillar;
        }
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
                    maxZ + Spacing
                );
            }
        }
    }
}
