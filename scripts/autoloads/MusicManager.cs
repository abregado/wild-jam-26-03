using Godot;
using System.Collections.Generic;

/// <summary>
/// Autoload singleton. Manages background music with crossfade transitions.
///
/// Static API:
///   MusicManager.PlayContext("menu" | "raid" | "after_action")
///
/// Track lists come from GameConfig (music_menu, music_raid, music_after_action).
/// Files are loaded from assets/music/<name>.wav (or .ogg / .mp3 if present).
/// Players route through the "Music" bus (volume controlled by SettingsManager).
///
/// Crossfade: two AudioStreamPlayers ping-pong. The outgoing track fades to silence
/// and the incoming track fades to full bus volume over MusicCrossfadeTime seconds.
/// </summary>
public partial class MusicManager : Node
{
    private static MusicManager? _instance;

    private AudioStreamPlayer _playerA = null!;
    private AudioStreamPlayer _playerB = null!;
    private bool _aIsActive = true;

    private readonly Dictionary<string, List<AudioStream>> _trackLists = new();
    private string _currentContext = "";

    private AudioStreamPlayer ActivePlayer => _aIsActive ? _playerA : _playerB;
    private AudioStreamPlayer NextPlayer   => _aIsActive ? _playerB : _playerA;

    public override void _Ready()
    {
        _instance = this;

        _playerA = new AudioStreamPlayer { Bus = "Music", VolumeDb = -80f };
        _playerB = new AudioStreamPlayer { Bus = "Music", VolumeDb = -80f };
        AddChild(_playerA);
        AddChild(_playerB);

        LoadTracks();
    }

    private void LoadTracks()
    {
        var config = GetNode<GameConfig>("/root/GameConfig");
        _trackLists["menu"]         = LoadList(config.MusicMenu);
        _trackLists["raid"]         = LoadList(config.MusicRaid);
        _trackLists["after_action"] = LoadList(config.MusicAfterAction);

        int total = 0;
        foreach (var list in _trackLists.Values) total += list.Count;
        GD.Print($"[MusicManager] Loaded {total} music track(s).");
    }

    private static List<AudioStream> LoadList(List<string> names)
    {
        var result = new List<AudioStream>();
        foreach (var name in names)
        {
            AudioStream? stream = null;
            foreach (var ext in new[] { ".ogg", ".mp3", ".wav" })
            {
                string path = $"res://assets/music/{name}{ext}";
                if (ResourceLoader.Exists(path))
                {
                    stream = GD.Load<AudioStream>(path);
                    break;
                }
            }
            if (stream != null) result.Add(stream);
        }
        return result;
    }

    // ── Static API ────────────────────────────────────────────────────────────

    public static void PlayContext(string context) => _instance?.PlayContextInternal(context);

    // ── Internal ─────────────────────────────────────────────────────────────

    private void PlayContextInternal(string context)
    {
        if (!_trackLists.TryGetValue(context, out var tracks) || tracks.Count == 0)
        {
            GD.Print($"[MusicManager] No tracks found for context '{context}'.");
            return;
        }

        _currentContext = context;
        var config = GetNode<GameConfig>("/root/GameConfig");
        float crossfadeTime = config.MusicCrossfadeTime;

        // Pick a random track
        var stream = tracks[(int)(GD.Randi() % (uint)tracks.Count)];

        var next = NextPlayer;
        next.Stream = stream;
        next.VolumeDb = -80f;
        next.Play();

        float targetDb = 0f; // players output 0 dB; bus volume controls actual level

        var t = CreateTween();
        t.TweenProperty(ActivePlayer, "volume_db", -80f, crossfadeTime)
         .SetTrans(Tween.TransitionType.Sine);
        t.Parallel()
         .TweenProperty(next, "volume_db", targetDb, crossfadeTime)
         .SetTrans(Tween.TransitionType.Sine);
        t.TweenCallback(Callable.From(() =>
        {
            ActivePlayer.Stop();
            _aIsActive = !_aIsActive;
        }));
    }
}
