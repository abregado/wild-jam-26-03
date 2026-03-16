using Godot;

/// <summary>
/// Visible projectile. Moves forward at bullet_speed.
///
/// Collision: Area3D on layer 16 (Projectiles, bit 5), mask 6 (Containers=2 + Clamps=4).
///
/// Hit logic:
///   If hit Area3D belongs to ClampNode parent  → call clamp.TakeDamage(damage) only.
///   If hit Area3D belongs to ContainerNode parent → call container.TakeSplashDamage(pos, radius, damage).
///   (Hitting container body triggers AoE to nearby clamps — no direct container HP damage from bullet.)
///   Wait — per design: hitting container = splash to clamps within radius.
///                      hitting clamp = direct to clamp only.
///
/// Self-destructs on hit or after MaxDistance traveled.
/// </summary>
public partial class Bullet : Node3D
{
    private const float MaxDistance = 100f;

    private float _damage;
    private float _blastRadius;
    private float _speed;
    private float _distanceTraveled;
    private Vector3 _startPosition;
    private bool _hasHit;

    public void Initialize(float damage, float blastRadius, float speed)
    {
        _damage = damage;
        _blastRadius = blastRadius;
        _speed = speed;
    }

    public override void _Ready()
    {
        _startPosition = GlobalPosition;
        var area = GetNode<Area3D>("Area3D");
        area.AreaEntered += OnAreaEntered;
    }

    public override void _Process(double delta)
    {
        if (_hasHit) return;

        float move = _speed * (float)delta;
        // Forward is -Z in Godot local space (camera/projectile faces its -Z)
        GlobalPosition += -GlobalTransform.Basis.Z * move;
        _distanceTraveled += move;

        if (_distanceTraveled >= MaxDistance)
            QueueFree();
    }

    private void OnAreaEntered(Area3D other)
    {
        if (_hasHit) return;

        var parent = other.GetParent();

        if (parent is ClampNode clamp)
        {
            clamp.TakeDamage(_damage);
            HitAndDestroy();
        }
        else if (parent is ContainerNode container)
        {
            // Hitting container body: AoE splash to nearby clamps
            container.TakeSplashDamage(GlobalPosition, _blastRadius, _damage);
            HitAndDestroy();
        }
    }

    private void HitAndDestroy()
    {
        _hasHit = true;
        QueueFree();
    }
}
