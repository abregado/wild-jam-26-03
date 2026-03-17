using Godot;

/// <summary>
/// Visible projectile. Moves forward at bullet_speed.
///
/// Hit detection: per-frame raycast from current → next position (mask 6: Containers+Clamps).
/// This prevents tunneling at high speeds where Area3D overlap would be missed between frames.
/// The Area3D in the scene is kept for its collision layer (layer 16) but AreaEntered is unused.
///
/// Hit logic:
///   Ray hits Area3D whose parent is ClampNode     → direct damage to clamp only.
///   Ray hits Area3D whose parent is ContainerNode → direct HP damage + AoE splash to nearby clamps.
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
        // Scale only the mesh — collision shape stays full-size for reliable raycasts.
        GetNode<MeshInstance3D>("MeshSlot").Scale = Vector3.One * config.BulletSize;

        SetupTrail();
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

    // Mask 39 = layer 1 (World/Train) + layer 2 (Containers) + layer 3 (Clamps) + layer 6 (Drones)
    private const uint HitMask = 39u;

    public override void _Process(double delta)
    {
        if (_hasHit) return;

        float move = _speed * (float)delta;
        var forward = -GlobalTransform.Basis.Z;
        var nextPos = GlobalPosition + forward * move;

        // Raycast the full step so fast bullets never tunnel through thin targets.
        var spaceState = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(GlobalPosition, nextPos, HitMask);
        query.CollideWithAreas = true;
        query.CollideWithBodies = true;
        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            var hitPos = result["position"].AsVector3();

            // Only Area3D hits deal damage; StaticBody3D hits (train cars, rail, pillars) just stop the bullet.
            if (result["collider"].As<Area3D>() is { } area)
            {
                var parent = area.GetParent();
                if (parent is ClampNode clamp)
                    clamp.TakeDamage(_damage);
                else if (parent is ContainerNode container)
                {
                    container.TakeDamage(_damage);
                    container.TakeSplashDamage(hitPos, _blastRadius, _damage);
                }
                else if (parent is DroneNode drone)
                    drone.TakeDamage(_damage);
            }

            GlobalPosition = hitPos;
            HitAndDestroy();
            return;
        }

        GlobalPosition = nextPos;
        _distanceTraveled += move;

        if (_distanceTraveled >= MaxDistance)
        {
            DetachTrail();
            QueueFree();
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
        var trail = _trail;
        _trail = null!;
        trail.Emitting = false;
        trail.Reparent(GetTree().Root);
        GetTree().CreateTimer(trail.Lifetime).Timeout += trail.QueueFree;
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
