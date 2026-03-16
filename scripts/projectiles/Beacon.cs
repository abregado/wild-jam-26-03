using Godot;

/// <summary>
/// Beacon projectile. Tags a container to reveal its cargo type color.
/// Travels slower than bullets. Unlimited ammo, only limited by reload cooldown.
///
/// On hit ContainerNode: calls container.Tag() which changes its material to cargo color.
/// Self-destructs on hit or after MaxDistance.
/// </summary>
public partial class Beacon : Node3D
{
    private const float MaxDistance = 120f;

    private float _speed;
    private float _distanceTraveled;
    private bool _hasHit;

    public void Initialize(float speed)
    {
        _speed = speed;
    }

    public override void _Ready()
    {
        var area = GetNode<Area3D>("Area3D");
        area.AreaEntered += OnAreaEntered;
    }

    public override void _Process(double delta)
    {
        if (_hasHit) return;

        float move = _speed * (float)delta;
        GlobalPosition += -GlobalTransform.Basis.Z * move;
        _distanceTraveled += move;

        if (_distanceTraveled >= MaxDistance)
            QueueFree();
    }

    private void OnAreaEntered(Area3D other)
    {
        if (_hasHit) return;

        var parent = other.GetParent();
        if (parent is ContainerNode container)
        {
            container.Tag();
            _hasHit = true;
            QueueFree();
        }
        // Beacons pass through clamps
    }
}
