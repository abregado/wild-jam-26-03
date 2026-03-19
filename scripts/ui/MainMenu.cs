using Godot;
using System;

/// <summary>
/// Main menu. Entry point of the game.
///
/// Layout (built in code):
///   Background image (full-screen TextureRect)
///   Centred VBox:
///     Title label
///     HBox with 3 save-slot panels, each containing:
///       Large slot button (shows "+" or raid count + date)
///       Small delete button (trash icon, grey when empty)
///     Options button
///     Quit button
///   Options overlay (hidden by default)
///   Delete confirmation panel (hidden by default)
/// </summary>
public partial class MainMenu : Control
{
    private SaveManager   _saveManager   = null!;
    private GameSession   _session       = null!;
    private GameConfig    _config        = null!;

    private Button[]  _slotButtons   = new Button[3];
    private Button[]  _deleteButtons = new Button[3];

    private Control     _optionsOverlay   = null!;
    private CenterContainer _optionsCenterWrap = null!;
    private OptionsMenu _optionsMenu     = null!;
    private Control     _confirmPanel    = null!;
    private int      _pendingDeleteSlot = -1;

    public override void _Ready()
    {
        _saveManager = GetNode<SaveManager>("/root/SaveManager");
        _session     = GetNode<GameSession>("/root/GameSession");
        _config      = GetNode<GameConfig>("/root/GameConfig");

        MusicManager.PlayContext("menu");
        Input.MouseMode = Input.MouseModeEnum.Visible;

        BuildUI();
        RefreshSlots();
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Full-screen anchor
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Background
        var bg = new TextureRect
        {
            StretchMode = TextureRect.StretchModeEnum.Scale,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var bgTex = GD.Load<Texture2D>("res://assets/menu/background.png");
        if (bgTex != null) bg.Texture = bgTex;
        AddChild(bg);

        // Dark overlay for readability
        var overlay = new ColorRect { Color = new Color(0f, 0f, 0f, 0.45f) };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        // CenterContainer fills the screen and centers its single child automatically.
        // This is the same pattern AfterAction uses (anchors_preset=8 / center anchor).
        var centerWrap = new CenterContainer();
        centerWrap.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(centerWrap);

        // Actual content column, fixed width so it never spans the whole screen.
        var centre = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            CustomMinimumSize = new Vector2(700f, 0f),
        };
        centre.AddThemeConstantOverride("separation", 12);
        centerWrap.AddChild(centre);

        // Title
        var title = new Label
        {
            Text = "WILD JAM",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 52);
        centre.AddChild(title);

        centre.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 24f) });

        // Save slots row
        var slotsRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        slotsRow.AddThemeConstantOverride("separation", 20);
        centre.AddChild(slotsRow);

        for (int i = 0; i < 3; i++)
        {
            int slotIdx = i;
            var slotBox = new VBoxContainer();
            slotBox.AddThemeConstantOverride("separation", 6);
            slotsRow.AddChild(slotBox);

            var btn = new Button
            {
                CustomMinimumSize = new Vector2(200f, 90f),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            };
            btn.Pressed += () => OnSlotPressed(slotIdx);
            slotBox.AddChild(btn);
            _slotButtons[i] = btn;

            var del = new Button
            {
                Text = "[X]",
                CustomMinimumSize = new Vector2(200f, 28f),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                Modulate = Colors.DimGray,
            };
            del.Pressed += () => OnDeletePressed(slotIdx);
            slotBox.AddChild(del);
            _deleteButtons[i] = del;
        }

        centre.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 32f) });

        // Options button
        var optBtn = new Button
        {
            Text = "OPTIONS",
            CustomMinimumSize = new Vector2(220f, 48f),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        optBtn.Pressed += OnOptionsPressed;
        centre.AddChild(optBtn);

        centre.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 10f) });

        // Quit button
        var quitBtn = new Button
        {
            Text = "QUIT",
            CustomMinimumSize = new Vector2(220f, 48f),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        quitBtn.Pressed += () => GetTree().Quit();
        centre.AddChild(quitBtn);

        // Options overlay (full-screen, hidden)
        _optionsOverlay = new ColorRect { Color = new Color(0f, 0f, 0f, 0.7f) };
        _optionsOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _optionsOverlay.Visible = false;
        AddChild(_optionsOverlay);

        // CenterContainer keeps the options panel centred regardless of window size.
        var optCenterWrap = new CenterContainer();
        optCenterWrap.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        optCenterWrap.Visible = false;
        AddChild(optCenterWrap);

        _optionsMenu = new OptionsMenu();
        _optionsMenu.CustomMinimumSize = new Vector2(660f, 520f);
        _optionsMenu.Closed += OnOptionsClosed;
        optCenterWrap.AddChild(_optionsMenu);

        _optionsCenterWrap = optCenterWrap;

        // Confirm-delete panel (hidden)
        BuildConfirmPanel();
    }

    private void BuildConfirmPanel()
    {
        // CenterContainer so the dialog is always screen-centred.
        var confirmWrap = new CenterContainer();
        confirmWrap.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        confirmWrap.Visible = false;
        AddChild(confirmWrap);
        _confirmPanel = confirmWrap;

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(360f, 0f) };
        confirmWrap.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        panel.AddChild(vbox);

        var lbl = new Label
        {
            Text = "Delete this save?",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        lbl.AddThemeFontSizeOverride("font_size", 18);
        vbox.AddChild(lbl);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 20);
        vbox.AddChild(row);

        var yes = new Button { Text = "DELETE", CustomMinimumSize = new Vector2(130f, 40f) };
        yes.Pressed += OnConfirmDelete;
        row.AddChild(yes);

        var no = new Button { Text = "CANCEL", CustomMinimumSize = new Vector2(130f, 40f) };
        no.Pressed += () => { _confirmPanel.Visible = false; };
        row.AddChild(no);
    }

    // ── Slot management ───────────────────────────────────────────────────────

    private void RefreshSlots()
    {
        for (int i = 0; i < 3; i++)
        {
            if (_saveManager.SlotExists(i))
            {
                var (raids, date) = _saveManager.GetSlotMeta(i);
                _slotButtons[i].Text = $"SLOT {i + 1}\nRaids: {raids}\n{date}";
                _deleteButtons[i].Modulate = Colors.White;
                _deleteButtons[i].Disabled = false;
            }
            else
            {
                _slotButtons[i].Text = $"SLOT {i + 1}\n+";
                _deleteButtons[i].Modulate = Colors.DimGray;
                _deleteButtons[i].Disabled = true;
            }
        }
    }

    private void OnSlotPressed(int slot)
    {
        SoundManager.Play("ui_button_click");
        if (_saveManager.SlotExists(slot))
        {
            var data = _saveManager.LoadSlot(slot)!;
            _session.LoadFromSave(data, slot);
        }
        else
        {
            _session.StartNewGame(slot);
        }
        GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
    }

    private void OnDeletePressed(int slot)
    {
        SoundManager.Play("ui_button_click");
        _pendingDeleteSlot = slot;
        _confirmPanel.Visible = true;
    }

    private void OnConfirmDelete()
    {
        if (_pendingDeleteSlot >= 0)
        {
            _saveManager.DeleteSlot(_pendingDeleteSlot);
            _pendingDeleteSlot = -1;
        }
        _confirmPanel.Visible = false;
        RefreshSlots();
    }

    private void OnOptionsPressed()
    {
        SoundManager.Play("ui_button_click");
        _optionsOverlay.Visible = true;
        _optionsCenterWrap.Visible = true;
        _optionsMenu.Open();
    }

    private void OnOptionsClosed()
    {
        _optionsOverlay.Visible = false;
        _optionsCenterWrap.Visible = false;
    }
}
