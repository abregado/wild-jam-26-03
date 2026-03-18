using Godot;

public enum CliffSide { None, Left, Right }
public enum MovementLimit { None, Roof, Plateau }

/// <summary>
/// Autoload singleton. Runs a continuous loop: Startup → Warning → Active → Warning → Active → ...
/// There is no idle gap between sections — as soon as one section ends, the next warning begins.
/// "Empty" sections (no cliff, no limit) are valid and produce no obstacle cubes.
///
/// ObstaclePool polls IsInWarning + Upcoming* to start streaming cubes during the warning period,
/// so obstacles visually approach the player before the section becomes active.
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

    private enum Phase { Startup, Warning, Active }
    private Phase _phase = Phase.Startup;
    private float _phaseTimer = 8f; // initial pause before first warning
    private MovementLimit _lastMovementLimit = MovementLimit.None;

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
            case Phase.Startup:
                if (_phaseTimer <= 0f)
                    StartWarning();
                break;

            case Phase.Warning:
                if (_phaseTimer <= 0f)
                    ActivateSection();
                break;

            case Phase.Active:
                if (_phaseTimer <= 0f)
                    EndSection();
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
        _lastMovementLimit = ActiveMovementLimit;
        IsInWarning = false;
        _phase = Phase.Active;
        _phaseTimer = _rng.RandfRange(_config.ObstacleSectionMinDuration, _config.ObstacleSectionMaxDuration);
        EmitSignal(SignalName.SectionActivated, (long)ActiveCliffSide, (long)ActiveMovementLimit);
        GD.Print($"[ObstacleManager] Active: {ActiveCliffSide} + {ActiveMovementLimit} for {_phaseTimer:F1}s");
    }

    private void EndSection()
    {
        ActiveCliffSide = CliffSide.None;
        ActiveMovementLimit = MovementLimit.None;
        EmitSignal(SignalName.SectionCleared);
        GD.Print("[ObstacleManager] Section ended → starting next warning immediately.");
        StartWarning(); // no idle gap — roll and warn for next section right away
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
        // Forbid roof→plateau and plateau→roof transitions
        MovementLimit limit;
        int attempts = 0;
        do
        {
            float r2 = _rng.Randf();
            if (r2 < 0.3f)      limit = MovementLimit.Roof;
            else if (r2 < 0.6f) limit = MovementLimit.Plateau;
            else                 limit = MovementLimit.None;
            attempts++;
        }
        while (attempts < 10 && IsOppositeLimit(_lastMovementLimit, limit));

        return (cliff, limit);
    }

    // Returns true if the two limits are roof↔plateau opposites (forbidden transition).
    private static bool IsOppositeLimit(MovementLimit last, MovementLimit next)
        => (last == MovementLimit.Roof    && next == MovementLimit.Plateau)
        || (last == MovementLimit.Plateau && next == MovementLimit.Roof);
}
