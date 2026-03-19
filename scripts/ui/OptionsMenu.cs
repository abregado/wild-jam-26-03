using Godot;
using System.Collections.Generic;

/// <summary>
/// Options panel. Shown as an overlay from the main menu.
///
/// Sections:
///   Volume — master / music / sfx sliders + mute checkboxes
///   Key Bindings — one row per rebindable action; click to rebind
///   Buttons — Save, Cancel
///
/// Emits Closed when the user is done (Save or Cancel).
/// </summary>
public partial class OptionsMenu : PanelContainer
{
    [Signal] public delegate void ClosedEventHandler();

    private SettingsManager _settings = null!;

    // Volume controls (saved so we can read them on Save)
    private HSlider _masterSlider = null!;
    private HSlider _musicSlider  = null!;
    private HSlider _sfxSlider    = null!;
    private CheckButton _musicMute = null!;
    private CheckButton _sfxMute   = null!;

    // Key binding rows: action → the button showing the current key
    private readonly Dictionary<string, Button> _bindButtons = new();

    // Rebind state
    private string _rebindingAction = "";
    private Button? _rebindingButton;

    // Snapshot for Cancel
    private float _snapMaster, _snapMusic, _snapSfx;
    private bool  _snapMusicMuted, _snapSfxMuted;

    public override void _Ready()
    {
        _settings = GetNode<SettingsManager>("/root/SettingsManager");
        BuildUI();
    }

    public void Open()
    {
        // Snapshot current values for Cancel
        _snapMaster      = _settings.MasterVolume;
        _snapMusic       = _settings.MusicVolume;
        _snapSfx         = _settings.SfxVolume;
        _snapMusicMuted  = _settings.MusicMuted;
        _snapSfxMuted    = _settings.SfxMuted;

        // Sync sliders to current values
        _masterSlider.Value = _settings.MasterVolume;
        _musicSlider.Value  = _settings.MusicVolume;
        _sfxSlider.Value    = _settings.SfxVolume;
        _musicMute.ButtonPressed = _settings.MusicMuted;
        _sfxMute.ButtonPressed   = _settings.SfxMuted;

        // Refresh binding labels
        foreach (var (_, action) in SettingsManager.RebindableActions)
            if (_bindButtons.TryGetValue(action, out var btn))
                btn.Text = SettingsManager.GetBindingLabel(action);

        CancelRebind();
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(0f, 480f);
        AddChild(scroll);

        var vbox = new VBoxContainer();
        vbox.CustomMinimumSize = new Vector2(640f, 0f);
        vbox.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(vbox);

        // ── Volume section ────────────────────────────────────────────────────
        AddSectionHeader(vbox, "VOLUME");

        (_masterSlider, _) = AddSliderRow(vbox, "Master", 0f, 1f, _settings.MasterVolume,
            v => _settings.SetMasterVolume((float)v));

        var (musicSlider, musicMute) = AddSliderRow(vbox, "Music", 0f, 1f, _settings.MusicVolume,
            v => _settings.SetMusicVolume((float)v), hasMute: true,
            muteValue: _settings.MusicMuted, muteCallback: m => _settings.SetMusicMuted(m));
        _musicSlider = musicSlider;
        _musicMute   = musicMute!;

        var (sfxSlider, sfxMute) = AddSliderRow(vbox, "SFX", 0f, 1f, _settings.SfxVolume,
            v => _settings.SetSfxVolume((float)v), hasMute: true,
            muteValue: _settings.SfxMuted, muteCallback: m => _settings.SetSfxMuted(m));
        _sfxSlider = sfxSlider;
        _sfxMute   = sfxMute!;

        // ── Key bindings section ───────────────────────────────────────────────
        vbox.AddChild(new HSeparator());
        AddSectionHeader(vbox, "KEY BINDINGS");

        foreach (var (label, action) in SettingsManager.RebindableActions)
        {
            string capturedAction = action;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            vbox.AddChild(row);

            var nameLbl = new Label
            {
                Text = label,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            row.AddChild(nameLbl);

            var bindBtn = new Button
            {
                Text = SettingsManager.GetBindingLabel(action),
                CustomMinimumSize = new Vector2(130f, 32f),
            };
            bindBtn.Pressed += () => StartRebind(capturedAction, bindBtn);
            row.AddChild(bindBtn);
            _bindButtons[action] = bindBtn;
        }

        var resetRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        vbox.AddChild(resetRow);
        var resetBtn = new Button { Text = "RESET TO DEFAULTS", CustomMinimumSize = new Vector2(200f, 32f) };
        resetBtn.Pressed += OnResetBindings;
        resetRow.AddChild(resetBtn);

        // ── Save / Cancel ────────────────────────────────────────────────────
        vbox.AddChild(new HSeparator());
        var btnRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btnRow.AddThemeConstantOverride("separation", 20);
        vbox.AddChild(btnRow);

        var saveBtn = new Button { Text = "SAVE", CustomMinimumSize = new Vector2(130f, 40f) };
        saveBtn.Pressed += OnSave;
        btnRow.AddChild(saveBtn);

        var cancelBtn = new Button { Text = "CANCEL", CustomMinimumSize = new Vector2(130f, 40f) };
        cancelBtn.Pressed += OnCancel;
        btnRow.AddChild(cancelBtn);
    }

    private static void AddSectionHeader(VBoxContainer parent, string text)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 15);
        parent.AddChild(lbl);
    }

    private (HSlider, CheckButton?) AddSliderRow(VBoxContainer parent, string label,
        float min, float max, float initial,
        System.Action<double> onChanged,
        bool hasMute = false, bool muteValue = false,
        System.Action<bool>? muteCallback = null)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        parent.AddChild(row);

        var lbl = new Label { Text = label, CustomMinimumSize = new Vector2(80f, 0f) };
        row.AddChild(lbl);

        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Value    = initial,
            Step     = 0.01,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 28f),
        };
        slider.ValueChanged += v => onChanged(v);
        row.AddChild(slider);

        var pctLbl = new Label { CustomMinimumSize = new Vector2(40f, 0f) };
        pctLbl.Text = $"{(int)(initial * 100)}%";
        slider.ValueChanged += v => pctLbl.Text = $"{(int)(v * 100)}%";
        row.AddChild(pctLbl);

        CheckButton? muteBtn = null;
        if (hasMute)
        {
            muteBtn = new CheckButton { Text = "Mute", ButtonPressed = muteValue };
            if (muteCallback != null)
                muteBtn.Toggled += m => muteCallback!(m);
            row.AddChild(muteBtn);
        }

        return (slider, muteBtn);
    }

    // ── Rebinding ─────────────────────────────────────────────────────────────

    private void StartRebind(string action, Button btn)
    {
        CancelRebind();
        _rebindingAction = action;
        _rebindingButton = btn;
        btn.Text = "Press a key…";
    }

    private void CancelRebind()
    {
        if (_rebindingButton != null && !string.IsNullOrEmpty(_rebindingAction))
            _rebindingButton.Text = SettingsManager.GetBindingLabel(_rebindingAction);
        _rebindingAction = "";
        _rebindingButton = null;
    }

    public override void _Input(InputEvent @event)
    {
        if (string.IsNullOrEmpty(_rebindingAction)) return;
        if (!Visible) return;

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Escape)
            {
                CancelRebind();
                GetViewport().SetInputAsHandled();
                return;
            }
            SettingsManager.SetBinding(_rebindingAction,
                new InputEventKey { PhysicalKeycode = key.PhysicalKeycode });
            if (_rebindingButton != null)
                _rebindingButton.Text = SettingsManager.GetBindingLabel(_rebindingAction);
            CancelRebind();
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            SettingsManager.SetBinding(_rebindingAction,
                new InputEventMouseButton { ButtonIndex = mb.ButtonIndex });
            if (_rebindingButton != null)
                _rebindingButton.Text = SettingsManager.GetBindingLabel(_rebindingAction);
            CancelRebind();
            GetViewport().SetInputAsHandled();
        }
    }

    // ── Reset bindings ────────────────────────────────────────────────────────

    private void OnResetBindings()
    {
        InputMap.LoadFromProjectSettings();
        foreach (var (_, action) in SettingsManager.RebindableActions)
            if (_bindButtons.TryGetValue(action, out var btn))
                btn.Text = SettingsManager.GetBindingLabel(action);
        CancelRebind();
    }

    // ── Save / Cancel ─────────────────────────────────────────────────────────

    private void OnSave()
    {
        CancelRebind();
        _settings.Save();
        SoundManager.Play("ui_options_save");
        EmitSignal(SignalName.Closed);
    }

    private void OnCancel()
    {
        CancelRebind();
        // Restore snapshot
        _settings.SetMasterVolume(_snapMaster);
        _settings.SetMusicVolume(_snapMusic);
        _settings.SetSfxVolume(_snapSfx);
        _settings.SetMusicMuted(_snapMusicMuted);
        _settings.SetSfxMuted(_snapSfxMuted);
        SoundManager.Play("ui_options_back");
        EmitSignal(SignalName.Closed);
    }
}
