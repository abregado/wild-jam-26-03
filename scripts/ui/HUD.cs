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
    private Label _obstacleWarningLabel = null!;
    private HBoxContainer _flipUnderRow = null!;
    private HBoxContainer _flipOverRow = null!;

    private PlayerCar? _playerCar;
    private Turret? _turret;

    public override void _Ready()
    {
        _speedBar = GetNode<ProgressBar>(SpeedBarPath);
        _trainSpeedLabel = GetNode<Label>(TrainSpeedLabelPath);
        _warningLabel = GetNode<Label>(WarningLabelPath);
        _countdownLabel = GetNode<Label>(CountdownLabelPath);
        _clickPrompt = GetNode<Label>("ClickPrompt");
        _obstacleWarningLabel = GetNode<Label>("ObstacleWarning");

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

        // Obstacle warning
        var om = GetNode<ObstacleManager>("/root/ObstacleManager");
        if (om.IsInWarning)
        {
            _obstacleWarningLabel.Text = $"⚠ {BuildObstacleWarningText(om.UpcomingCliffSide, om.UpcomingMovementLimit)} AHEAD";
            _obstacleWarningLabel.Visible = true;
        }
        else
        {
            _obstacleWarningLabel.Visible = false;
        }

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

        // Movement cross with dynamic bound keys
        var moveRow = new HBoxContainer();
        moveRow.AddThemeConstantOverride("separation", 8);
        moveRow.AddChild(BuildMoveCross());
        moveRow.AddChild(MakePromptLabel("Move"));
        vbox.AddChild(moveRow);

        AddPromptRow(vbox, "fire_primary",       "Fire");
        AddPromptRow(vbox, "fire_beacon",        "Beacon");
        _flipOverRow  = AddPromptRow(vbox, "switch_side_over",  "Flip Over");
        _flipUnderRow = AddPromptRow(vbox, "switch_side_under", "Flip Under");
    }

    private static VBoxContainer BuildMoveCross()
    {
        const int G = 28;
        const int Sep = 3;

        var cross = new VBoxContainer();
        cross.AddThemeConstantOverride("separation", Sep);

        // Top row: spacer + Forward key
        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", Sep);
        topRow.AddChild(new Control { CustomMinimumSize = new Vector2(G + Sep, G) });
        topRow.AddChild(GetActionGlyph("move_forward", G));
        cross.AddChild(topRow);

        // Bottom row: Left Backward Right
        var botRow = new HBoxContainer();
        botRow.AddThemeConstantOverride("separation", Sep);
        botRow.AddChild(GetActionGlyph("move_left",    G));
        botRow.AddChild(GetActionGlyph("move_backward", G));
        botRow.AddChild(GetActionGlyph("move_right",   G));
        cross.AddChild(botRow);

        return cross;
    }

    private static HBoxContainer AddPromptRow(VBoxContainer parent, string action, string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(GetActionGlyph(action, 28));
        row.AddChild(MakePromptLabel(label));
        parent.AddChild(row);
        return row;
    }

    private static Control GetActionGlyph(string action, int size)
    {
        var events = InputMap.ActionGetEvents(action);
        if (events.Count > 0)
        {
            var ev = events[0];
            if (ev is InputEventKey key)
            {
                var k = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
                var file = KeyToGlyphFile(k);
                if (file != null)
                {
                    var tex = GD.Load<Texture2D>($"res://assets/glyphs/{file}");
                    if (tex != null) return MakeGlyph(file, size);
                }
                return MakeKeyLabel(OS.GetKeycodeString(k), size);
            }
            if (ev is InputEventMouseButton mb)
            {
                string? mbFile = mb.ButtonIndex switch
                {
                    MouseButton.Left  => "mouse_left_outline.png",
                    MouseButton.Right => "mouse_right_outline.png",
                    _ => null,
                };
                if (mbFile != null)
                {
                    var tex = GD.Load<Texture2D>($"res://assets/glyphs/{mbFile}");
                    if (tex != null) return MakeGlyph(mbFile, size);
                }
                string mbName = mb.ButtonIndex == MouseButton.Left ? "LMB" :
                                mb.ButtonIndex == MouseButton.Right ? "RMB" : "Mouse";
                return MakeKeyLabel(mbName, size);
            }
        }
        return MakeKeyLabel("?", size);
    }

    private static string? KeyToGlyphFile(Key k)
    {
        if (k >= Key.A && k <= Key.Z)
        {
            char letter = (char)((int)k - (int)Key.A + 'a');
            return $"keyboard_{letter}_outline.png";
        }
        return k switch
        {
            Key.Space => "keyboard_space_outline.png",
            Key.Ctrl  => "keyboard_ctrl_outline.png",
            _ => null,
        };
    }

    private static Label MakeKeyLabel(string text, int size)
    {
        var lbl = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(size, size),
        };
        lbl.AddThemeFontSizeOverride("font_size", 11);
        return lbl;
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

    private static string BuildObstacleWarningText(CliffSide cliff, MovementLimit limit)
    {
        string cliffPart = cliff switch
        {
            CliffSide.Left  => "LEFT CLIFF",
            CliffSide.Right => "RIGHT CLIFF",
            _               => "",
        };
        string limitPart = limit switch
        {
            MovementLimit.Roof    => "ROOF",
            MovementLimit.Plateau => "PLATEAU",
            _                     => "",
        };

        if (cliffPart.Length > 0 && limitPart.Length > 0)
            return $"{cliffPart} + {limitPart}";
        return cliffPart.Length > 0 ? cliffPart : limitPart;
    }
}
