using Godot;

/// <summary>
/// A clamp on the surface of a container.
///
/// Hit detection: Area3D on collision layer 4 (Clamps, bit 3 = value 4), mask 16 (Projectiles).
///
/// Signals:
///   Destroyed — emitted when HP reaches 0.
///
/// IsAlive: false after destroyed (prevents double-counting).
/// </summary>
public partial class ClampNode : Node3D
{
    [Signal] public delegate void DestroyedEventHandler();
    [Signal] public delegate void DamageTakenEventHandler();

    public bool IsAlive { get; private set; } = true;

    private float _hp;
    private MeshInstance3D _mesh = null!;
    private bool _damageTakenFired;

    public void SetHitpoints(float hp) => _hp = hp;

    public override void _Ready()
    {
        _mesh = GetNode<MeshInstance3D>("MeshSlot");

        var area = GetNodeOrNull<Area3D>("Area3D");
        if (area != null)
            area.AreaEntered += OnAreaEntered;
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

        // Placeholder: hide mesh
        _mesh.Visible = false;

        // Disable area so no more hits register
        var area = GetNodeOrNull<Area3D>("Area3D");
        if (area != null)
            area.SetDeferred(Area3D.PropertyName.Monitorable, false);
    }

    private void OnAreaEntered(Area3D other)
    {
        // Handled by the projectile script directly.
    }
}
