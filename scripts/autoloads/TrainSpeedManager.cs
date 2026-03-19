using Godot;

/// <summary>
/// Autoload singleton. Central speed authority. All systems read CurrentTrainSpeed from here.
///
/// Speed math:
///   CurrentTrainSpeed starts at BaseTrainSpeed.
///   Each cargo detach: CurrentTrainSpeed += SpeedIncreasePerContainer.
///   MaxRelativeForward = ConfigMaxRelativeVelocity - (CurrentTrainSpeed - BaseTrainSpeed)
///   When MaxRelativeForward goes negative, player drifts backward even at "full forward".
///
/// TrackEnvironment polls CurrentTrainSpeed each _Process frame.
/// </summary>
public partial class TrainSpeedManager : Node
{
    public float CurrentTrainSpeed { get; private set; }
    public float TrainZoomSpeed { get; private set; }
    public float MaxRelativeForward { get; private set; }
    public float MaxRelativeBackward { get; private set; }

    private GameConfig _config = null!;
    private bool _isZoomingAway;
    private float _carSpeedPenalty;

    public override void _Ready()
    {
        _config = GetNode<GameConfig>("/root/GameConfig");
        ResetSpeed();
    }

    public void ResetSpeed()
    {
        _isZoomingAway = false;
        _carSpeedPenalty = 0f;
        CurrentTrainSpeed = _config.BaseTrainSpeed;
        MaxRelativeForward = _config.MaxRelativeVelocity;
        MaxRelativeBackward = _config.MinRelativeVelocity;
    }

    /// <summary>Called when a drone bullet hits the player car. Reduces max forward velocity.</summary>
    public void ApplyCarSpeedDamage(float amount)
    {
        _carSpeedPenalty += amount;
        float speedIncrease = CurrentTrainSpeed - _config.BaseTrainSpeed;
        MaxRelativeForward = _config.MaxRelativeVelocity - speedIncrease - _carSpeedPenalty;
    }

    /// <summary>Called when a container detaches (cargo collected).</summary>
    public void OnContainerDetached() => ApplySpeedIncrease();

    /// <summary>Called when a container is destroyed (cargo lost).</summary>
    public void OnContainerDestroyed() => ApplySpeedIncrease();

    private void ApplySpeedIncrease()
    {
        if (_isZoomingAway) return;
        CurrentTrainSpeed += _config.SpeedIncreasePerContainer;
        float speedIncrease = CurrentTrainSpeed - _config.BaseTrainSpeed;
        MaxRelativeForward = _config.MaxRelativeVelocity - speedIncrease - _carSpeedPenalty;
    }

    /// <summary>
    /// Returns true if the player is too far behind the train to use their turret.
    /// playerZ: world Z of player. frontZ: world Z of locomotive front.
    /// Player is behind if (frontZ - playerZ) > TurretRange.
    /// </summary>
    public bool IsPlayerOutOfRange(float playerZ, float trainFrontZ)
    {
        return (trainFrontZ - playerZ) > _config.TurretRange;
    }

    /// <summary>Called by LevelManager when the countdown expires. Zooms the train away.</summary>
    public void TriggerZoomAway()
    {
        _isZoomingAway = true;
        TrainZoomSpeed = CurrentTrainSpeed * 10f; // train physically moves away at 10× speed
        CurrentTrainSpeed = 0f;                   // environment scroll stops — player has halted
        MaxRelativeForward = float.MinValue / 2f;
        SoundManager.Play("train_zoom_off");
        GD.Print("[TrainSpeedManager] Zoom away triggered!");
    }
}
