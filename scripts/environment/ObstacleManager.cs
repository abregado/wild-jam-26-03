using Godot;

public enum CliffSide { None, Left, Right }
public enum MovementLimit { None, Roof, Plateau }

/// <summary>
/// Autoload singleton. Runs a state machine: Clear → Warning → Active → Clear.
/// Exposes current and upcoming obstacle state for PlayerCar, HUD, Deployer, Drone.
/// </summary>
public partial class ObstacleManager : Node
{
    [Signal] public delegate void SectionActivatedEventHandler(long cliff, long limit);
    [Signal] public delegate void SectionClearedEventHandler();

    public CliffSide ActiveCliffSide { get; private set; } = CliffSide.None;
    public MovementLimit ActiveMovementLimit { get; private set; } = MovementLimit.None;

    public bool IsInWarning { get; private set; } = false;
    public CliffSide UpcomingCliffSide { get; private set; } = CliffSide.None;
    public MovementLimit UpcomingMovementLimit { get; private set; } = MovementLimit.None;

    private GameConfig _config = null!;
    private RandomNumberGenerator _rng = new();

    private enum Phase { Clear, Warning, Active }
    private Phase _phase = Phase.Clear;
    private float _phaseTimer = 8f; // initial delay before first warning

    public override void _Ready()
    {
        _config = GetNode<GameConfig>("/root/GameConfig");
        _rng.Randomize();
    }

    public override void _Process(double delta)
    {
        _phaseTimer -= (float)delta;

        switch (_phase)
        {
            case Phase.Clear:
                if (_phaseTimer <= 0f)
                    StartWarning();
                break;

            case Phase.Warning:
                if (_phaseTimer <= 0f)
                    ActivateSection();
                break;

            case Phase.Active:
                if (_phaseTimer <= 0f)
                    ClearSection();
                break;
        }
    }

    private void StartWarning()
    {
        (UpcomingCliffSide, UpcomingMovementLimit) = RollNextSection();
        IsInWarning = true;
        _phase = Phase.Warning;
        _phaseTimer = _config.ObstacleWarningTime;
        GD.Print($"[ObstacleManager] Warning: {UpcomingCliffSide} + {UpcomingMovementLimit}");
    }

    private void ActivateSection()
    {
        ActiveCliffSide = UpcomingCliffSide;
        ActiveMovementLimit = UpcomingMovementLimit;
        IsInWarning = false;
        _phase = Phase.Active;
        _phaseTimer = _rng.RandfRange(_config.ObstacleSectionMinDuration, _config.ObstacleSectionMaxDuration);
        EmitSignal(SignalName.SectionActivated, (long)ActiveCliffSide, (long)ActiveMovementLimit);
        GD.Print($"[ObstacleManager] Active: {ActiveCliffSide} + {ActiveMovementLimit} for {_phaseTimer:F1}s");
    }

    private void ClearSection()
    {
        ActiveCliffSide = CliffSide.None;
        ActiveMovementLimit = MovementLimit.None;
        IsInWarning = false;
        _phase = Phase.Clear;
        _phaseTimer = _rng.RandfRange(
            _config.ObstacleSectionMinDuration * 0.5f,
            _config.ObstacleSectionMaxDuration * 0.5f);
        EmitSignal(SignalName.SectionCleared);
        GD.Print($"[ObstacleManager] Cleared. Next warning in {_phaseTimer:F1}s");
    }

    private (CliffSide cliff, MovementLimit limit) RollNextSection()
    {
        // Roll cliff: 30% Left, 30% Right, 40% None
        CliffSide cliff;
        float r = _rng.Randf();
        if (r < 0.3f)      cliff = CliffSide.Left;
        else if (r < 0.6f) cliff = CliffSide.Right;
        else               cliff = CliffSide.None;

        // Roll limit: 30% Roof, 30% Plateau, 40% None
        MovementLimit limit;
        float r2 = _rng.Randf();
        if (r2 < 0.3f)      limit = MovementLimit.Roof;
        else if (r2 < 0.6f) limit = MovementLimit.Plateau;
        else                 limit = MovementLimit.None;

        // Guard: if cliff forces a switch, ensure at least one arc direction is open.
        // Roof blocks over-arc, Plateau blocks under-arc.
        // With only one limit active at a time this is always fine — both arcs can't be
        // simultaneously blocked by a single limit value. No additional guard needed.

        return (cliff, limit);
    }
}
