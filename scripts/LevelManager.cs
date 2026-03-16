using Godot;

/// <summary>
/// Manages level end condition.
/// Attach to a Node child of Main.tscn called "LevelManager".
///
/// Each frame checks if player is out of turret range using TrainSpeedManager.IsPlayerOutOfRange.
/// "Out of range" = (LocomotiveZ - playerZ) > TurretRange config value.
///
/// When out of range:
///   1. Shows HUD warning with 3-second countdown.
///   2. Countdown Timer starts (3 seconds).
///   3. On expire: TriggerZoomAway(), disable player input.
///   4. After 2 more seconds: change scene to AfterAction.tscn.
///
/// Targetable container check: any ContainerNode still alive on the train.
/// Level does NOT end prematurely if there are still containers to shoot.
/// (This is intentional — player might speed up to re-engage.)
/// </summary>
public partial class LevelManager : Node
{
    private const float WarningDuration = 3f;
    private const float ZoomDuration = 2f;

    private PlayerCar _playerCar = null!;
    private HUD _hud = null!;
    private TrainBuilder _trainBuilder = null!;
    private TrainSpeedManager _tsm = null!;

    private float _warningTimer = -1f;
    private float _zoomTimer = -1f;
    private bool _warningActive;
    private bool _zoomTriggered;

    public override void _Ready()
    {
        _tsm = GetNode<TrainSpeedManager>("/root/TrainSpeedManager");
        _playerCar = GetParent().GetNode<PlayerCar>("PlayerCar");
        _hud = GetParent().GetNode<HUD>("HUD");
        _trainBuilder = GetParent().GetNode<TrainBuilder>("Train");

        // Position player near the middle of the train at start (in range)
        float startZ = _trainBuilder.LocomotiveZ * 0.6f;
        _playerCar.Position = new Vector3(PlayerCar.XOffset, PlayerCar.YHeight, startZ);
        _playerCar.SetTrainFrontZ(_trainBuilder.LocomotiveZ);
        GD.Print($"[LevelManager] Train LocomotiveZ={_trainBuilder.LocomotiveZ}, PlayerStart Z={startZ}");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Update player's reference to train front
        _playerCar.SetTrainFrontZ(_trainBuilder.LocomotiveZ);

        bool outOfRange = _tsm.IsPlayerOutOfRange(
            _playerCar.GlobalPosition.Z,
            _trainBuilder.LocomotiveZ
        );

        if (outOfRange && !_warningActive && !_zoomTriggered)
        {
            StartWarning();
        }
        else if (!outOfRange && _warningActive && !_zoomTriggered)
        {
            // Player moved back into range — cancel warning
            _warningActive = false;
            _warningTimer = -1f;
            _hud.HideWarning();
        }

        if (_warningActive && !_zoomTriggered)
        {
            _warningTimer -= dt;
            _hud.UpdateCountdown(_warningTimer);
            if (_warningTimer <= 0f)
                TriggerZoom();
        }

        if (_zoomTriggered)
        {
            // Physically move the train away so it visually zooms off
            _trainBuilder.Position += new Vector3(0f, 0f, _tsm.CurrentTrainSpeed * dt);

            _zoomTimer -= dt;
            if (_zoomTimer <= 0f)
                EndLevel();
        }
    }

    private void StartWarning()
    {
        _warningActive = true;
        _warningTimer = WarningDuration;
        _hud.ShowWarning(_warningTimer);
        GD.Print("[LevelManager] Player out of range. Warning started.");
    }

    private void TriggerZoom()
    {
        _zoomTriggered = true;
        _warningActive = false;
        _playerCar.DisableInput();
        _tsm.TriggerZoomAway();
        _zoomTimer = ZoomDuration;
        GD.Print("[LevelManager] Zoom away triggered.");
    }

    private void EndLevel()
    {
        GD.Print("[LevelManager] Level ended. Loading AfterAction.");
        GetTree().ChangeSceneToFile("res://scenes/ui/AfterAction.tscn");
    }
}
