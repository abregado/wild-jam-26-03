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
    private Label _clickPrompt = null!;
    private HBoxContainer _flipUnderRow = null!;

    private PlayerCar? _playerCar;
    private Turret? _turret;

    public override void _Ready()
    {
        _speedBar = GetNode<ProgressBar>(SpeedBarPath);
        _trainSpeedLabel = GetNode<Label>(TrainSpeedLabelPath);
        _warningLabel = GetNode<Label>(WarningLabelPath);
        _countdownLabel = GetNode<Label>(CountdownLabelPath);
        _clickPrompt = GetNode<Label>("ClickPrompt");

        HideWarning();
        BuildButtonPrompts();
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

        // Grey out Flip Under prompt when blocked
        _flipUnderRow.Modulate = _playerCar.CanSwitchUnder
            ? new Color(1f, 1f, 1f, 1f)
            : new Color(1f, 1f, 1f, 0.3f);

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

    // ── Button Prompts ────────────────────────────────────────────────────────

    private void BuildButtonPrompts()
    {
        var vbox = GetNode<VBoxContainer>("ButtonPrompts");

        // WASD cross: W centred above A-S-D, then "Move" label
        var moveRow = new HBoxContainer();
        moveRow.AddThemeConstantOverride("separation", 8);
        moveRow.AddChild(BuildWasdCross());
        moveRow.AddChild(MakePromptLabel("Move"));
        vbox.AddChild(moveRow);

        AddPromptRow(vbox, "mouse_left_outline.png",       "Fire");
        AddPromptRow(vbox, "mouse_right_outline.png",      "Beacon");
        AddPromptRow(vbox, "keyboard_space_outline.png",   "Flip Over");
        _flipUnderRow = AddPromptRow(vbox, "keyboard_ctrl_outline.png", "Flip Under");
    }

    private static VBoxContainer BuildWasdCross()
    {
        const int G = 28;
        const int Sep = 3;

        var cross = new VBoxContainer();
        cross.AddThemeConstantOverride("separation", Sep);

        // Top row: spacer + W (W sits above S)
        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", Sep);
        topRow.AddChild(new Control { CustomMinimumSize = new Vector2(G + Sep, G) });
        topRow.AddChild(MakeGlyph("keyboard_w_outline.png", G));
        cross.AddChild(topRow);

        // Bottom row: A S D
        var botRow = new HBoxContainer();
        botRow.AddThemeConstantOverride("separation", Sep);
        botRow.AddChild(MakeGlyph("keyboard_a_outline.png", G));
        botRow.AddChild(MakeGlyph("keyboard_s_outline.png", G));
        botRow.AddChild(MakeGlyph("keyboard_d_outline.png", G));
        cross.AddChild(botRow);

        return cross;
    }

    private static HBoxContainer AddPromptRow(VBoxContainer parent, string glyphFile, string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(MakeGlyph(glyphFile, 28));
        row.AddChild(MakePromptLabel(label));
        parent.AddChild(row);
        return row;
    }

    private static TextureRect MakeGlyph(string file, int size) => new TextureRect
    {
        CustomMinimumSize = new Vector2(size, size),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale,
        Texture = GD.Load<Texture2D>($"res://assets/glyphs/{file}"),
    };

    private static Label MakePromptLabel(string text)
    {
        var lbl = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        lbl.AddThemeFontSizeOverride("font_size", 13);
        return lbl;
    }
}
