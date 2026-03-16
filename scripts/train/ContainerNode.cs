using Godot;
using System.Collections.Generic;

/// <summary>
/// A cargo container attached to a carriage.
///
/// Hit detection: Area3D on collision layer 2 (Containers), mask 16 (Projectiles).
///
/// Signals:
///   CargoDetached(string cargoName) — emitted when all clamps destroyed, cargo auto-collected.
///   ContainerDestroyed            — emitted when HP reaches 0 (cargo lost, not collected).
///
/// TakeDamage(amount): reduces HP; at 0 calls Explode().
/// TakeSplashDamage(origin, radius, amount): sphere-checks all registered clamps.
/// When tagged (isTagged=true), mesh material color changes to cargo type color.
/// </summary>
public partial class ContainerNode : Node3D
{
    [Signal] public delegate void CargoDetachedEventHandler(string cargoName);
    [Signal] public delegate void ContainerDestroyedEventHandler();

    public bool IsTagged { get; private set; }
    public string CargoName { get; private set; } = "Unknown";
    public Color CargoColor { get; private set; } = Colors.Gray;

    private float _hp;
    private readonly List<ClampNode> _clamps = new();
    private int _livingClamps;
    private MeshInstance3D _mesh = null!;
    private StandardMaterial3D _material = null!;
    private bool _isDead;

    public override void _Ready()
    {
        var config = GetNode<GameConfig>("/root/GameConfig");
        _hp = config.ContainerHitpoints;

        _mesh = GetNode<MeshInstance3D>("MeshSlot");

        // Create a unique material per container so color changes are independent.
        // Bright orange untagged = clearly visible, distinct from grey carriages.
        _material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.5f, 0.05f) // Bright orange (untagged)
        };
        _mesh.MaterialOverride = _material;

        // Connect Area3D signal
        var area = GetNodeOrNull<Area3D>("Area3D");
        if (area != null)
            area.AreaEntered += OnAreaEntered;
    }

    public void SetCargoType(CargoType cargoType)
    {
        CargoName = cargoType.Name;
        CargoColor = cargoType.Color;
    }

    public void RegisterClamp(ClampNode clamp)
    {
        _clamps.Add(clamp);
        _livingClamps++;
        clamp.Destroyed += OnClampDestroyed;
    }

    /// <summary>Direct damage to container HP (e.g. from bullet hitting container body).</summary>
    public void TakeDamage(float amount)
    {
        if (_isDead) return;
        _hp -= amount;
        if (_hp <= 0f)
            Explode();
    }

    /// <summary>AoE splash check centered on origin, damages clamps within radius.</summary>
    public void TakeSplashDamage(Vector3 origin, float radius, float clampDamage)
    {
        if (_isDead) return;
        foreach (var clamp in _clamps)
        {
            if (!clamp.IsAlive) continue;
            float dist = clamp.GlobalPosition.DistanceTo(origin);
            if (dist <= radius)
                clamp.TakeDamage(clampDamage);
        }
    }

    public void Tag()
    {
        if (IsTagged) return;
        IsTagged = true;
        _material.AlbedoColor = CargoColor;
    }

    private void OnClampDestroyed()
    {
        _livingClamps--;
        if (_livingClamps <= 0)
            Detach();
    }

    private void Detach()
    {
        if (_isDead) return;
        _isDead = true;

        EmitSignal(SignalName.CargoDetached, CargoName);

        // Fall animation: tween Y down
        var tween = CreateTween();
        tween.TweenProperty(this, "position:y", GlobalPosition.Y - 15f, 1.5f)
             .SetEase(Tween.EaseType.In)
             .SetTrans(Tween.TransitionType.Quad);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    private void Explode()
    {
        if (_isDead) return;
        _isDead = true;

        EmitSignal(SignalName.ContainerDestroyed);

        // Placeholder explosion: flash white then disappear
        _material.AlbedoColor = Colors.White;
        _material.EmissionEnabled = true;
        _material.Emission = Colors.OrangeRed;

        var tween = CreateTween();
        tween.TweenProperty(_mesh, "scale", Vector3.Zero, 0.3f);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    private void OnAreaEntered(Area3D other)
    {
        // Bullet/Beacon hit detection is handled by the projectile script checking container vs clamp.
        // This is a fallback — projectiles do their own hit resolution.
    }
}
