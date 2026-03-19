using Godot;
using System.Collections.Generic;

/// <summary>
/// Autoload singleton. Owns audio bus setup and persistent user settings.
///
/// Saves to user://settings.json:
///   - master_volume, music_volume, sfx_volume, music_muted, sfx_muted
///   - bindings: dict of action → { type: "key"|"mouse_button", physical_keycode|button_index }
///
/// Must be loaded BEFORE SoundManager and MusicManager so buses exist when they start.
/// </summary>
public partial class SettingsManager : Node
{
    private const string SettingsPath = "user://settings.json";

    public float MasterVolume { get; private set; } = 1.0f;
    public float MusicVolume  { get; private set; } = 0.7f;
    public float SfxVolume    { get; private set; } = 1.0f;
    public bool  MusicMuted   { get; private set; } = false;
    public bool  SfxMuted     { get; private set; } = false;

    // Actions that can be rebound (display name, action id)
    public static readonly (string Label, string Action)[] RebindableActions =
    {
        ("Move Forward",     "move_forward"),
        ("Move Backward",    "move_backward"),
        ("Move Left",        "move_left"),
        ("Move Right",       "move_right"),
        ("Fire",             "fire_primary"),
        ("Beacon",           "fire_beacon"),
        ("Flip Over",        "switch_side_over"),
        ("Flip Under",       "switch_side_under"),
    };

    public override void _Ready()
    {
        SetupBuses();

        // Load from config defaults first, then override from saved file
        var config = GetNode<GameConfig>("/root/GameConfig");
        MasterVolume = config.MasterVolume;
        MusicVolume  = config.MusicVolume;
        SfxVolume    = config.SfxVolume;
        MusicMuted   = config.MusicMuted;
        SfxMuted     = config.SfxMuted;

        Load();
        ApplyVolumes();
    }

    // ── Bus setup ─────────────────────────────────────────────────────────────

    private void SetupBuses()
    {
        EnsureBus("Music");
        EnsureBus("SFX");
    }

    private static void EnsureBus(string name)
    {
        if (AudioServer.GetBusIndex(name) != -1) return;
        int idx = AudioServer.BusCount;
        AudioServer.AddBus(idx);
        AudioServer.SetBusName(idx, name);
        AudioServer.SetBusSend(idx, "Master");
    }

    // ── Volume API ────────────────────────────────────────────────────────────

    public void SetMasterVolume(float v) { MasterVolume = Mathf.Clamp(v, 0f, 1f); ApplyVolumes(); }
    public void SetMusicVolume (float v) { MusicVolume  = Mathf.Clamp(v, 0f, 1f); ApplyVolumes(); }
    public void SetSfxVolume   (float v) { SfxVolume    = Mathf.Clamp(v, 0f, 1f); ApplyVolumes(); }
    public void SetMusicMuted  (bool  m) { MusicMuted   = m;                       ApplyVolumes(); }
    public void SetSfxMuted    (bool  m) { SfxMuted     = m;                       ApplyVolumes(); }

    public void ApplyVolumes()
    {
        int master = AudioServer.GetBusIndex("Master");
        int music  = AudioServer.GetBusIndex("Music");
        int sfx    = AudioServer.GetBusIndex("SFX");

        if (master >= 0)
        {
            AudioServer.SetBusVolumeDb(master, Mathf.LinearToDb(MasterVolume));
        }
        if (music >= 0)
        {
            AudioServer.SetBusVolumeDb(music, Mathf.LinearToDb(MusicMuted ? 0.0001f : MusicVolume));
            AudioServer.SetBusMute(music, MusicMuted);
        }
        if (sfx >= 0)
        {
            AudioServer.SetBusVolumeDb(sfx, Mathf.LinearToDb(SfxMuted ? 0.0001f : SfxVolume));
            AudioServer.SetBusMute(sfx, SfxMuted);
        }
    }

    // ── Binding API ───────────────────────────────────────────────────────────

    /// <summary>Returns a human-readable string for the first event of the given action.</summary>
    public static string GetBindingLabel(string action)
    {
        var events = InputMap.ActionGetEvents(action);
        if (events.Count == 0) return "—";
        var ev = events[0];
        if (ev is InputEventKey key)
            return OS.GetKeycodeString(key.PhysicalKeycode);
        if (ev is InputEventMouseButton mb)
            return mb.ButtonIndex switch
            {
                MouseButton.Left   => "LMB",
                MouseButton.Right  => "RMB",
                MouseButton.Middle => "MMB",
                _                  => $"Mouse {(int)mb.ButtonIndex}",
            };
        return ev.AsText();
    }

    /// <summary>Applies a new key event to the given action (replaces first binding).</summary>
    public static void SetBinding(string action, InputEvent newEvent)
    {
        InputMap.ActionEraseEvents(action);
        InputMap.ActionAddEvent(action, newEvent);
    }

    // ── Persistence ──────────────────────────────────────────────────────────

    public void Save()
    {
        var bindingsDict = new Godot.Collections.Dictionary();
        foreach (var (_, action) in RebindableActions)
        {
            var events = InputMap.ActionGetEvents(action);
            if (events.Count == 0) continue;
            var ev = events[0];
            var entry = new Godot.Collections.Dictionary();
            if (ev is InputEventKey key)
            {
                entry["type"] = "key";
                entry["physical_keycode"] = (int)key.PhysicalKeycode;
            }
            else if (ev is InputEventMouseButton mb)
            {
                entry["type"] = "mouse_button";
                entry["button_index"] = (int)mb.ButtonIndex;
            }
            bindingsDict[action] = entry;
        }

        var data = new Godot.Collections.Dictionary
        {
            ["master_volume"] = MasterVolume,
            ["music_volume"]  = MusicVolume,
            ["sfx_volume"]    = SfxVolume,
            ["music_muted"]   = MusicMuted,
            ["sfx_muted"]     = SfxMuted,
            ["bindings"]      = bindingsDict,
        };

        using var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"[SettingsManager] Cannot write {SettingsPath}");
            return;
        }
        file.StoreString(Json.Stringify(data));
        GD.Print("[SettingsManager] Settings saved.");
    }

    public void Load()
    {
        if (!FileAccess.FileExists(SettingsPath)) return;
        using var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
        if (file == null) return;

        var json = new Json();
        if (json.Parse(file.GetAsText()) != Error.Ok) return;

        var data = json.Data.AsGodotDictionary();

        if (data.TryGetValue("master_volume", out var mv)) MasterVolume = (float)mv.AsDouble();
        if (data.TryGetValue("music_volume",  out var muv)) MusicVolume  = (float)muv.AsDouble();
        if (data.TryGetValue("sfx_volume",    out var sv))  SfxVolume    = (float)sv.AsDouble();
        if (data.TryGetValue("music_muted",   out var mm))  MusicMuted   = mm.AsBool();
        if (data.TryGetValue("sfx_muted",     out var sm))  SfxMuted     = sm.AsBool();

        if (data.TryGetValue("bindings", out var bindVar))
        {
            var bindings = bindVar.AsGodotDictionary();
            foreach (var kv in bindings)
            {
                string action = kv.Key.AsString();
                if (!InputMap.HasAction(action)) continue;
                var entry = kv.Value.AsGodotDictionary();
                string type = entry.TryGetValue("type", out var t) ? t.AsString() : "";

                InputEvent? newEvent = null;
                if (type == "key" && entry.TryGetValue("physical_keycode", out var pcV))
                {
                    newEvent = new InputEventKey { PhysicalKeycode = (Key)pcV.AsInt32() };
                }
                else if (type == "mouse_button" && entry.TryGetValue("button_index", out var biV))
                {
                    newEvent = new InputEventMouseButton { ButtonIndex = (MouseButton)biV.AsInt32() };
                }

                if (newEvent != null)
                    SetBinding(action, newEvent);
            }
        }

        GD.Print("[SettingsManager] Settings loaded.");
    }
}
