using Godot;
using System.Collections.Generic;

/// <summary>
/// Autoload singleton. Spawns particle effects by ID.
///
/// Usage: VfxSpawner.Spawn("effect_id", worldPosition)
///
/// Lookup order per ID:
///   1. res://scenes/vfx/{id}.tscn  — artist-replaceable scene (must auto-free itself)
///   2. Built-in procedural CPUParticles3D fallback
///
/// Effect IDs (see docs/systems/vfx.md):
///   hit_damageable, hit_nondamageable, clamp_destroyed, turret_repair,
///   drone_deployed, drone_destroyed, container_detach, shield_hit, car_hit,
///   drone_prefire, drone_muzzle, turret_prefire, turret_muzzle, player_muzzle
/// </summary>
public partial class VfxSpawner : Node
{
    public static VfxSpawner Instance { get; private set; } = null!;

    // null in cache = "no .tscn found, use fallback"
    private readonly Dictionary<string, PackedScene> _sceneCache = new();
    private readonly HashSet<string> _checkedPaths = new(); // tracks IDs where .tscn was not found

    private struct EffectConfig
    {
        public Color Color;
        public float Size;
        public int   Amount;
        public float Lifetime;
        public float Speed;
        public float SpeedVar;
        public EffectConfig(Color c, float sz, int amt, float lt, float sp, float sv)
        { Color = c; Size = sz; Amount = amt; Lifetime = lt; Speed = sp; SpeedVar = sv; }
    }

    private static readonly Dictionary<string, EffectConfig> Fallbacks = new()
    {
        ["hit_damageable"]    = new(new Color(1.0f, 0.5f, 0.1f), 0.10f, 20, 0.30f, 3.0f, 2.0f),
        ["hit_nondamageable"] = new(new Color(0.6f, 0.6f, 0.6f), 0.08f, 12, 0.25f, 2.0f, 1.0f),
        ["clamp_destroyed"]   = new(new Color(1.0f, 0.85f, 0.0f), 0.12f, 30, 0.50f, 4.0f, 2.0f),
        ["turret_repair"]     = new(new Color(0.9f, 0.6f, 0.0f), 0.10f, 20, 0.60f, 2.5f, 1.5f),
        ["drone_deployed"]    = new(new Color(0.2f, 0.6f, 1.0f), 0.10f, 24, 0.40f, 3.0f, 1.5f),
        ["drone_destroyed"]   = new(new Color(0.8f, 0.3f, 0.1f), 0.14f, 40, 0.70f, 5.0f, 3.0f),
        ["container_detach"]  = new(new Color(0.7f, 0.7f, 0.7f), 0.18f, 50, 0.80f, 4.0f, 2.0f),
        ["shield_hit"]        = new(new Color(0.3f, 0.7f, 1.0f), 0.10f, 20, 0.30f, 3.0f, 1.5f),
        ["car_hit"]           = new(new Color(1.0f, 0.1f, 0.1f), 0.12f, 25, 0.40f, 3.5f, 2.0f),
        ["drone_prefire"]     = new(new Color(1.0f, 0.2f, 0.0f), 0.06f, 10, 0.20f, 1.0f, 0.5f),
        ["drone_muzzle"]      = new(new Color(1.0f, 0.5f, 0.1f), 0.08f, 15, 0.15f, 4.0f, 2.0f),
        ["turret_prefire"]    = new(new Color(1.0f, 0.2f, 0.0f), 0.06f, 10, 0.20f, 1.0f, 0.5f),
        ["turret_muzzle"]     = new(new Color(1.0f, 0.5f, 0.1f), 0.08f, 15, 0.15f, 4.0f, 2.0f),
        ["player_muzzle"]     = new(new Color(1.0f, 0.85f, 0.3f), 0.10f, 20, 0.12f, 5.0f, 3.0f),
    };

    public override void _Ready()
    {
        Instance = this;
    }

    public static void Spawn(string id, Vector3 worldPos)
        => Instance?.SpawnInternal(id, worldPos);

    private void SpawnInternal(string id, Vector3 worldPos)
    {
        Node3D effect;

        if (_sceneCache.TryGetValue(id, out var scene))
        {
            effect = scene.Instantiate<Node3D>();
        }
        else if (!_checkedPaths.Contains(id))
        {
            _checkedPaths.Add(id);
            string path = $"res://scenes/vfx/{id}.tscn";
            if (ResourceLoader.Exists(path))
            {
                scene = GD.Load<PackedScene>(path);
                _sceneCache[id] = scene;
                effect = scene.Instantiate<Node3D>();
            }
            else
            {
                effect = BuildFallback(id);
            }
        }
        else
        {
            effect = BuildFallback(id);
        }

        GetTree().Root.AddChild(effect);
        effect.GlobalPosition = worldPos;
    }

    private static Node3D BuildFallback(string id)
    {
        if (!Fallbacks.TryGetValue(id, out var cfg))
            cfg = new EffectConfig(Colors.White, 0.1f, 12, 0.3f, 2f, 1f);

        var root = new Node3D { Name = $"Vfx_{id}" };

        var sphere = new SphereMesh { Radius = cfg.Size * 0.1f, Height = cfg.Size * 0.2f };
        sphere.Material = new StandardMaterial3D
        {
            AlbedoColor = cfg.Color,
            EmissionEnabled = true,
            Emission = cfg.Color,
            EmissionEnergyMultiplier = 4f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };

        var particles = new CpuParticles3D
        {
            Amount          = cfg.Amount,
            Lifetime        = cfg.Lifetime,
            OneShot         = true,
            Emitting        = true,
            LocalCoords     = false,
            Mesh            = sphere,
            InitialVelocityMin = cfg.Speed - cfg.SpeedVar,
            InitialVelocityMax = cfg.Speed + cfg.SpeedVar,
            Spread          = 180f,
            Direction       = Vector3.Up,
            Gravity         = new Vector3(0f, -9.8f, 0f),
        };
        root.AddChild(particles);

        var timer = new Timer { WaitTime = cfg.Lifetime + 0.15, OneShot = true, Autostart = true };
        timer.Timeout += root.QueueFree;
        root.AddChild(timer);

        return root;
    }
}
