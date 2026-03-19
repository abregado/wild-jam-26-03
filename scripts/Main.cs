using Godot;

/// <summary>
/// Main scene script. Entry point for gameplay.
/// Wires up HUD references to PlayerCar and Turret after all children are ready.
/// Handles the pause menu (Escape key toggles it during active play).
/// </summary>
public partial class Main : Node3D
{
    private PlayerCar   _playerCar  = null!;
    private GameSession _session    = null!;
    private CanvasLayer _pauseLayer = null!;
    private bool        _pauseVisible = false;

    public override void _Ready()
    {
        _session   = GetNode<GameSession>("/root/GameSession");
        _playerCar = GetNode<PlayerCar>("PlayerCar");

        var hud    = GetNode<HUD>("HUD");
        var turret = _playerCar.GetNode<Turret>("Turret");
        hud.SetReferences(_playerCar, turret);

        MusicManager.PlayContext("raid");
        BuildPauseMenu();
        GD.Print("[Main] Scene ready. Game starting.");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (key.Keycode != Key.Escape) return;

        if (_pauseVisible)
        {
            OnResume();
            GetViewport().SetInputAsHandled();
        }
        else if (_playerCar.IsInputEnabled)
        {
            ShowPause();
            GetViewport().SetInputAsHandled();
        }
    }

    // ── Pause menu ────────────────────────────────────────────────────────────

    private void BuildPauseMenu()
    {
        _pauseLayer = new CanvasLayer { Layer = 10 };
        AddChild(_pauseLayer);

        var overlay = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f) };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _pauseLayer.AddChild(overlay);

        var wrap = new CenterContainer();
        wrap.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _pauseLayer.AddChild(wrap);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(300f, 0f) };
        wrap.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        panel.AddChild(vbox);

        var title = new Label { Text = "PAUSED", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28);
        vbox.AddChild(title);

        vbox.AddChild(new HSeparator());

        var resumeBtn = new Button
        {
            Text = "RESUME",
            CustomMinimumSize = new Vector2(240f, 48f),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        resumeBtn.Pressed += OnResume;
        vbox.AddChild(resumeBtn);

        var menuBtn = new Button
        {
            Text = "RETURN TO MENU",
            CustomMinimumSize = new Vector2(240f, 48f),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        menuBtn.Pressed += OnReturnToMenu;
        vbox.AddChild(menuBtn);

        _pauseLayer.Visible = false;
    }

    private void ShowPause()
    {
        _pauseVisible = true;
        _playerCar.DisableInput();
        _pauseLayer.Visible = true;
    }

    private void OnResume()
    {
        _pauseVisible = false;
        _pauseLayer.Visible = false;
        _playerCar.EnableInput();
    }

    private void OnReturnToMenu()
    {
        _session.WriteToSave();
        GetTree().ChangeSceneToFile("res://scenes/ui/MainMenu.tscn");
    }
}
