using Godot;

/// <summary>
/// A clamp on the surface of a container.
///
/// Hit detection: Area3D on collision layer 4 (Clamps, bit 3 = value 4), mask 16 (Projectiles).
///
/// Visual:
///   Call Configure(setup, surfaceNormal) before adding to the scene tree.
///   The mesh is built procedurally (or loaded from a type-specific GLB) and oriented
///   so its flat face lies against the surface described by surfaceNormal.
///   GLB lookup order: assets/models/train/clamp_{type}.glb, fallback = procedural box.
///
/// Signals:
///   Destroyed — emitted when HP reaches 0.
///   DamageTaken — emitted on first hit.
///
/// IsAlive: false after destroyed.
/// </summary>
public partial class ClampNode : Node3D
{
    [Signal] public delegate void DestroyedEventHandler();
    [Signal] public delegate void DamageTakenEventHandler();

    public bool IsAlive { get; private set; } = true;

    private float _hp;
    private MeshInstance3D _mesh = null!;
    private bool _damageTakenFired;

    // Set before adding to scene tree
    private ClampSetup _setup = ClampSetup.Single;
    private Vector3 _surfaceNormal = Vector3.Up;

    private static readonly Color ClampColor = new(1.0f, 0.85f, 0.0f);

    public void SetHitpoints(float hp) => _hp = hp;

    /// <summary>
    /// Call before AddChild. Stores the setup type and surface normal used to orient the mesh.
    /// </summary>
    public void Configure(ClampSetup setup, Vector3 surfaceNormal)
    {
        _setup = setup;
        _surfaceNormal = surfaceNormal.Normalized();
    }

    public override void _Ready()
    {
        BuildMesh();
        BuildCollision();
    }

    private void BuildMesh()
    {
        _mesh = new MeshInstance3D { Name = "MeshSlot" };

        // Try type-specific GLB
        string glbPath = $"res://assets/models/train/clamp_{_setup.ToString().ToLower()}.glb";
        Mesh? glbMesh = TryLoadGlbMesh(glbPath);

        if (glbMesh != null)
        {
            _mesh.Mesh = glbMesh;
        }
        else
        {
            _mesh.Mesh = BuildProceduralMesh(_setup);
        }

        _mesh.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = ClampColor,
            Metallic = 0.5f,
            Roughness = 0.3f,
        };

        OrientMesh(_mesh);
        AddChild(_mesh);
    }

    private static Mesh BuildProceduralMesh(ClampSetup setup)
    {
        // Each type gets a distinct flat-box shape. The box's thin axis is Y (height),
        // and OrientMesh rotates it so that Y aligns with the surface normal.
        return setup switch
        {
            ClampSetup.Single => new BoxMesh { Size = new Vector3(0.55f, 0.08f, 0.28f) },
            ClampSetup.Double => new BoxMesh { Size = new Vector3(0.50f, 0.08f, 0.50f) },
            ClampSetup.Triple => new BoxMesh { Size = new Vector3(0.20f, 0.07f, 0.90f) },
            _                 => new BoxMesh { Size = new Vector3(0.22f, 0.22f, 0.22f) }, // Four: corner cube
        };
    }

    private void OrientMesh(Node3D meshNode)
    {
        // Rotate the mesh node so its local Y axis aligns with _surfaceNormal.
        // The mesh box's thin dimension is along Y by default.
        if (_surfaceNormal.Y > 0.5f)
        {
            // Top face — no rotation
            meshNode.RotationDegrees = Vector3.Zero;
        }
        else if (_surfaceNormal.Y < -0.5f)
        {
            // Bottom face — flip 180° around X
            meshNode.RotationDegrees = new Vector3(180f, 0f, 0f);
        }
        else if (_surfaceNormal.X > 0.5f)
        {
            // Right outer face (+X) — rotate −90° around Z so Y→+X
            meshNode.RotationDegrees = new Vector3(0f, 0f, -90f);
        }
        else
        {
            // Left outer face (−X) — rotate +90° around Z so Y→−X
            meshNode.RotationDegrees = new Vector3(0f, 0f, 90f);
        }
    }

    private void BuildCollision()
    {
        var area = new Area3D
        {
            CollisionLayer = 4u,
            CollisionMask  = 16u,
            Monitorable    = true,
            Monitoring     = false,
            Name           = "Area3D",
        };
        area.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.25f } });
        area.AreaEntered += OnAreaEntered;
        AddChild(area);
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive) return;
        if (!_damageTakenFired)
        {
            _damageTakenFired = true;
            EmitSignal(SignalName.DamageTaken);
        }
        _hp -= amount;
        if (_hp <= 0f)
            Destroy();
    }

    private void Destroy()
    {
        if (!IsAlive) return;
        IsAlive = false;
        EmitSignal(SignalName.Destroyed);
        SoundManager.Play("clamp_destroyed");
        VfxSpawner.Spawn("clamp_destroyed", GlobalPosition);

        _mesh.Visible = false;

        var area = GetNodeOrNull<Area3D>("Area3D");
        if (area != null)
            area.SetDeferred(Area3D.PropertyName.Monitorable, false);
    }

    private void OnAreaEntered(Area3D other)
    {
        // Hit detection handled by projectile scripts.
    }

    private static Mesh? TryLoadGlbMesh(string path)
    {
        var scene = GD.Load<PackedScene>(path);
        if (scene == null) return null;
        var root = scene.Instantiate<Node3D>();
        var body = root.FindChild("Body") as MeshInstance3D;
        var mesh = body?.Mesh;
        root.QueueFree();
        return mesh;
    }
}
