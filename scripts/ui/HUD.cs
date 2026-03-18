using Godot;

/// <summary>
/// HUD CanvasLayer. Updates every frame from PlayerCar and Turret.
///
/// Node paths (set in scene or override via exports):
///   SpeedBar (ProgressBar) — relative speed indicator
///   TrainSpeedLabel (Label) — current train speed
///   WarningLabel (Label) — "⚠ OUT OF RANGE" warning (hidden by default)
///   CountdownLabel (Label) — countdown timer label
///</summary>
public partial class HUD : CanvasLayer
{
    [Export] public NodePath SpeedBarPath { get; set; } = "BottomContainer/SpeedBar";
    [Export] public NodePath TrainSpeedLabelPath { get; set; } = "TopRight/TrainSpeedLabel";
    [Export] public NodePath WarningLabelPath { get; set; } = "Warning/WarningLabel";
    [Export] public NodePath CountdownLabelPath { get; set; } = "Warning/CountdownLabel";

    private ProgressBar _speedBar = null!;
    private Label _trainSpeedLabel = null!;
    private Label _warningLabel = null!;
    private Label _countdownLabel = null!;
    private Label _switchUnderIndicator = null!;
    private Label _clickPrompt = null!;

    private PlayerCar? _playerCar;
    private Turret? _turret;

    public override void _Ready()
    {
        _speedBar = GetNode<ProgressBar>(SpeedBarPath);
        _trainSpeedLabel = GetNode<Label>(TrainSpeedLabelPath);
        _warningLabel = GetNode<Label>(WarningLabelPath);
        _countdownLabel = GetNode<Label>(CountdownLabelPath);
        _switchUnderIndicator = GetNode<Label>("SwitchUnderIndicator");
        _clickPrompt = GetNode<Label>("ClickPrompt");

        HideWarning();
    }

    public void SetReferences(PlayerCar car, Turret turret)
    {
        _playerCar = car;
        _turret = turret;
    }

    public override void _Process(double delta)
    {
        if (_playerCar == null || _turret == null) return;

        var tsm = GetNode<TrainSpeedManager>("/root/TrainSpeedManager");
        var config = GetNode<GameConfig>("/root/GameConfig");

        // Speed bar: map relative velocity to 0-1
        float maxBack = Mathf.Abs(config.MinRelativeVelocity);
        float maxFwd = config.MaxRelativeVelocity;
        float range = maxBack + maxFwd;
        float normalizedSpeed = ((_playerCar.RelativeVelocity + maxBack) / range);
        _speedBar.Value = Mathf.Clamp(normalizedSpeed * 100f, 0, 100);

        // Train speed
        _trainSpeedLabel.Text = $"Train: {tsm.CurrentTrainSpeed:F0} u/s";

        // Under-switch clearance indicator
        _switchUnderIndicator.Visible = _playerCar.CanSwitchUnder;

        // Hide click prompt once mouse is captured
        _clickPrompt.Visible = Input.MouseMode != Input.MouseModeEnum.Captured;
    }

    public void ShowWarning(float countdown)
    {
        _warningLabel.Visible = true;
        _countdownLabel.Visible = true;
        _countdownLabel.Text = $"OUT OF RANGE — {countdown:F0}s";
    }

    public void HideWarning()
    {
        _warningLabel.Visible = false;
        _countdownLabel.Visible = false;
    }

    public void UpdateCountdown(float countdown)
    {
        if (_countdownLabel.Visible)
            _countdownLabel.Text = $"OUT OF RANGE — {countdown:F0}s";
    }
}
