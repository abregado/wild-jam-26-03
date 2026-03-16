using Godot;

/// <summary>
/// Visible projectile. Moves forward at bullet_speed.
///
/// Collision: Area3D on layer 16 (Projectiles, bit 5), mask 6 (Containers=2 + Clamps=4).
///
/// Hit logic:
///   If hit Area3D belongs to ClampNode parent  → direct damage to clamp only.
///   If hit Area3D belongs to ContainerNode parent → direct HP damage + AoE splash to nearby clamps.
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
    private bool _hasHit;

    public void Initialize(float damage, float blastRadius, float speed)
    {
        _damage = damage;
        _blastRadius = blastRadius;
        _speed = speed;
    }

    public override void _Ready()
    {
        var config = GetNode<GameConfig>("/root/GameConfig");
        Scale = Vector3.One * config.BulletSize;

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
            // Direct HP damage to the container body, plus AoE splash to nearby clamps
            container.TakeDamage(_damage);
            container.TakeSplashDamage(GlobalPosition, _blastRadius, _damage);
            HitAndDestroy();
        }
    }

    private void HitAndDestroy()
    {
        _hasHit = true;
        SpawnHitEffect();
        QueueFree();
    }

    private void SpawnHitEffect()
    {
        var effect = new Node3D();
        GetTree().Root.AddChild(effect);
        effect.GlobalPosition = GlobalPosition;

        // Flash sphere
        var mesh = new MeshInstance3D();
        var sphere = new SphereMesh { Radius = 0.35f, Height = 0.7f };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.55f, 0.05f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.35f, 0f),
            EmissionEnergyMultiplier = 8f,
        };
        sphere.Material = mat;
        mesh.Mesh = sphere;
        effect.AddChild(mesh);

        // Brief point light
        var light = new OmniLight3D
        {
            LightColor = new Color(1f, 0.5f, 0.1f),
            LightEnergy = 5f,
            OmniRange = 5f,
        };
        effect.AddChild(light);

        // Quick scale-out then free
        var tween = effect.CreateTween();
        tween.TweenProperty(effect, "scale", Vector3.Zero, 0.18f)
             .SetTrans(Tween.TransitionType.Quad)
             .SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(effect.QueueFree));
    }
}
