using Godot;
using System.Collections.Generic;

/// <summary>
/// Autoload singleton. Plays sound effects via a pool of AudioStreamPlayers.
///
/// Static API (mirrors VfxSpawner pattern — call from anywhere):
///   SoundManager.Play("sound_id")
///   SoundManager.PlayLoop("loop_key", "sound_id")
///   SoundManager.StopLoop("loop_key")
///
/// Sound names and file mappings come from GameConfig.Sounds.
/// All sounds load from assets/sounds/<filename>.wav.
/// All players route through the "SFX" bus (volume controlled by SettingsManager).
/// </summary>
public partial class SoundManager : Node
{
    private const int PoolSize = 16;

    private static SoundManager? _instance;

    private AudioStreamPlayer[] _pool = new AudioStreamPlayer[PoolSize];
    private readonly Dictionary<string, AudioStreamPlayer> _loops = new();
    private readonly HashSet<string> _activeLoops = new();
    private readonly Dictionary<string, AudioStream> _streams = new();

    public override void _Ready()
    {
        _instance = this;
        LoadStreams();

        for (int i = 0; i < PoolSize; i++)
        {
            _pool[i] = new AudioStreamPlayer { Bus = "SFX" };
            AddChild(_pool[i]);
        }
    }

    private void LoadStreams()
    {
        var config = GetNode<GameConfig>("/root/GameConfig");
        foreach (var kv in config.Sounds)
        {
            string path = $"res://assets/sounds/{kv.Value}.wav";
            if (ResourceLoader.Exists(path))
            {
                var stream = GD.Load<AudioStream>(path);
                if (stream != null)
                {
                    _streams[kv.Key] = stream;
                }
            }
        }
        GD.Print($"[SoundManager] Loaded {_streams.Count} / {config.Sounds.Count} sound(s).");
    }

    // ── Static API ────────────────────────────────────────────────────────────

    public static void Play(string soundId) => _instance?.PlayInternal(soundId);

    public static void PlayLoop(string loopKey, string soundId) =>
        _instance?.PlayLoopInternal(loopKey, soundId);

    public static void StopLoop(string loopKey) => _instance?.StopLoopInternal(loopKey);

    // ── Internal ─────────────────────────────────────────────────────────────

    private void PlayInternal(string soundId)
    {
        if (!_streams.TryGetValue(soundId, out var stream)) return;

        // Find a free player in the pool
        foreach (var player in _pool)
        {
            if (!player.Playing)
            {
                player.Stream = stream;
                player.Play();
                return;
            }
        }

        // All busy — steal the first slot
        _pool[0].Stream = stream;
        _pool[0].Play();
    }

    private void PlayLoopInternal(string loopKey, string soundId)
    {
        if (!_streams.TryGetValue(soundId, out var stream)) return;

        _activeLoops.Add(loopKey);

        if (!_loops.TryGetValue(loopKey, out var player))
        {
            player = new AudioStreamPlayer { Bus = "SFX" };
            AddChild(player);
            _loops[loopKey] = player;

            // Re-queue on finish if the loop is still marked active
            string capturedKey = loopKey;
            player.Finished += () =>
            {
                if (_activeLoops.Contains(capturedKey) &&
                    _loops.TryGetValue(capturedKey, out var p))
                    p.Play();
            };
        }

        if (player.Stream == stream && player.Playing) return;
        player.Stream = stream;
        player.Play();
    }

    private void StopLoopInternal(string loopKey)
    {
        _activeLoops.Remove(loopKey);
        if (_loops.TryGetValue(loopKey, out var player))
            player.Stop();
    }
}
