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
    private CpuParticles3D _trail = null!;

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

        SetupTrail();

        var area = GetNode<Area3D>("Area3D");
        area.AreaEntered += OnAreaEntered;
    }

    private void SetupTrail()
    {
        var config = GetNode<GameConfig>("/root/GameConfig");
        float r = config.TrailThickness;
        var sphere = new SphereMesh { Radius = r, Height = r * 2f };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.7f, 0.2f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.5f, 0.1f),
            EmissionEnergyMultiplier = 5f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
        };
        sphere.Material = mat;

        var fade = new Gradient();
        fade.SetColor(0, new Color(1f, 0.7f, 0.2f, 1f));
        fade.SetColor(1, new Color(1f, 0.4f, 0.05f, 0f));

        _trail = new CpuParticles3D
        {
            Amount = 24,
            Lifetime = 0.15,
            OneShot = false,
            Emitting = true,
            LocalCoords = false,
            Direction = Vector3.Zero,
            Spread = 0f,
            InitialVelocityMin = 0f,
            InitialVelocityMax = 0f,
            Mesh = sphere,
            ColorRamp = fade,
        };
        AddChild(_trail);
    }

    public override void _Process(double delta)
    {
        if (_hasHit) return;

        float move = _speed * (float)delta;
        // Forward is -Z in Godot local space (camera/projectile faces its -Z)
        GlobalPosition += -GlobalTransform.Basis.Z * move;
        _distanceTraveled += move;

        if (_distanceTraveled >= MaxDistance)
        {
            DetachTrail();
            QueueFree();
        }
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
        DetachTrail();
        SpawnHitEffect();
        QueueFree();
    }

    private void DetachTrail()
    {
        if (_trail == null || !_trail.IsInsideTree()) return;
        _trail.Emitting = false;
        _trail.Reparent(GetTree().Root);
        var t = _trail.CreateTween();
        t.TweenInterval(_trail.Lifetime);
        t.TweenCallback(Callable.From(_trail.QueueFree));
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
