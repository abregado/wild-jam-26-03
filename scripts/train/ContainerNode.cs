using Godot;
using System.Collections.Generic;

/// <summary>
/// A cargo container attached to a carriage.
///
/// Hit detection: Area3D on collision layer 2 (Containers), mask 16 (Projectiles).
///
/// Signals:
///   CargoDetached(string cargoName) — cargo successfully recovered (collected).
///   ContainerDestroyed            — cargo lost (either HP=0 or failed recovery roll).
///
/// Recovery chance (on clamp-destruction detach):
///   Base: 20% + 60% * (remainingHpPercent)  → 20%–80%
///   1 beacon: +40% bonus (capped at 100%)
///   2+ beacons: always recovered
///
/// Visual:
///   Untagged  — bright orange
///   1 beacon  — cargo colour
///   2 beacons — cargo colour + emission glow (guaranteed recovery)
/// </summary>
public partial class ContainerNode : Node3D
{
    [Signal] public delegate void CargoDetachedEventHandler(string cargoName, bool wasBeaconed);
    [Signal] public delegate void ContainerDestroyedEventHandler();
    [Signal] public delegate void DamageTakenEventHandler();

    public bool IsTagged => _beaconCount > 0;
    public bool IsScrap { get; private set; }
    public string CargoName { get; private set; } = "Unknown";
    public Color CargoColor { get; private set; } = Colors.Gray;

    private float _maxHp;
    private float _hp;
    private int _beaconCount;
    private readonly List<ClampNode> _clamps = new();
    private int _livingClamps;
    private MeshInstance3D _mesh = null!;
    private StandardMaterial3D _material = null!;
    private bool _isDead;
    private bool _damageTakenFired;

    public override void _Ready()
    {
        var config = GetNode<GameConfig>("/root/GameConfig");
        _maxHp = config.ContainerHitpoints;
        _hp = _maxHp;

        _mesh = GetNode<MeshInstance3D>("MeshSlot");

        _material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.5f, 0.05f) // bright orange (untagged)
        };
        _mesh.MaterialOverride = _material;

        var area = GetNodeOrNull<Area3D>("Area3D");
        if (area != null)
            area.AreaEntered += OnAreaEntered;
    }

    public void SetCargoType(CargoType cargoType)
    {
        CargoName = cargoType.Name;
        CargoColor = cargoType.Color;
        IsScrap = cargoType.IsScrap;
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
        if (!_damageTakenFired)
        {
            _damageTakenFired = true;
            EmitSignal(SignalName.DamageTaken);
        }
        _hp -= amount;
        if (_hp <= 0f)
            Explode();
    }

    /// <summary>AoE splash check centred on origin — damages clamps within radius.</summary>
    public void TakeSplashDamage(Vector3 origin, float radius, float clampDamage)
    {
        if (_isDead) return;
        foreach (var clamp in _clamps)
        {
            if (!clamp.IsAlive) continue;
            if (clamp.GlobalPosition.DistanceTo(origin) <= radius)
                clamp.TakeDamage(clampDamage);
        }
    }

    /// <summary>
    /// Tags the container with a beacon. First tag reveals cargo colour.
    /// Second tag adds emission glow (indicates guaranteed recovery).
    /// </summary>
    public void Tag()
    {
        _beaconCount++;

        if (_beaconCount == 1)
        {
            _material.AlbedoColor = CargoColor;
        }
        else if (_beaconCount == 2)
        {
            _material.AlbedoColor = CargoColor;
            _material.EmissionEnabled = true;
            _material.Emission = CargoColor;
            _material.EmissionEnergyMultiplier = 3f;
        }
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

        // Recovery chance:
        //   Base 20%–80% based on remaining HP %.
        //   1 beacon: +40% bonus.
        //   2+ beacons: guaranteed.
        float healthPercent = _maxHp > 0f ? Mathf.Clamp(_hp / _maxHp, 0f, 1f) : 0f;
        float chance = 0.2f + 0.6f * healthPercent;

        if (_beaconCount >= 2)
            chance = 1f;
        else if (_beaconCount == 1)
            chance = Mathf.Min(chance + 0.4f, 1f);

        bool recovered = GD.Randf() < chance;

        if (recovered)
            EmitSignal(SignalName.CargoDetached, CargoName, IsTagged);
        else
            EmitSignal(SignalName.ContainerDestroyed);

        // Fall off the train
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

        _material.AlbedoColor = Colors.White;
        _material.EmissionEnabled = true;
        _material.Emission = Colors.OrangeRed;

        var tween = CreateTween();
        tween.TweenProperty(_mesh, "scale", Vector3.Zero, 0.3f);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    private void OnAreaEntered(Area3D other)
    {
        // Projectile hit resolution is handled by the projectile scripts.
    }
}
