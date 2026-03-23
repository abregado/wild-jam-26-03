## Autoload singleton. Manages background music with crossfade transitions.
##
## API (call as autoload):
##   MusicManager.play_context("menu" | "raid" | "after_action")
##
## Track lists come from GameConfig (music_menu, music_raid, music_after_action).
## Files load from assets/music/<name>.ogg/.mp3/.wav (first extension found).
## Players route through the "Music" bus (volume controlled by SettingsManager).
##
## Crossfade: two AudioStreamPlayers ping-pong. The outgoing track fades to silence
## and the incoming track fades to full volume over music_crossfade_time seconds.

extends Node

var _player_a: AudioStreamPlayer
var _player_b: AudioStreamPlayer
var _a_is_active: bool = true

var _track_lists: Dictionary = {}  # context -> Array of AudioStream
var _current_context: String = ""


func _ready() -> void:
	_player_a = AudioStreamPlayer.new()
	_player_a.bus = "Music"
	_player_a.volume_db = -80.0
	add_child(_player_a)

	_player_b = AudioStreamPlayer.new()
	_player_b.bus = "Music"
	_player_b.volume_db = -80.0
	add_child(_player_b)

	_load_tracks()


func _load_tracks() -> void:
	_track_lists["menu"]         = _load_list(GameConfig.music_menu)
	_track_lists["raid"]         = _load_list(GameConfig.music_raid)
	_track_lists["after_action"] = _load_list(GameConfig.music_after_action)

	var total := 0
	for list in _track_lists.values():
		total += (list as Array).size()
	print("[MusicManager] Loaded %d music track(s)." % total)


func _load_list(names: Array) -> Array:
	var result := []
	for name in names:
		var stream: AudioStream = null
		for ext in [".ogg", ".mp3", ".wav"]:
			var path := "res://assets/music/%s%s" % [name, ext]
			if ResourceLoader.exists(path):
				stream = load(path) as AudioStream
				break
		if stream != null:
			result.append(stream)
	return result


func _active_player() -> AudioStreamPlayer:
	return _player_a if _a_is_active else _player_b


func _next_player() -> AudioStreamPlayer:
	return _player_b if _a_is_active else _player_a


# ── Public API ────────────────────────────────────────────────────────────────

func play_context(context: String) -> void:
	if context not in _track_lists:
		print("[MusicManager] No tracks found for context '%s'." % context)
		return
	var tracks: Array = _track_lists[context]
	if tracks.is_empty():
		print("[MusicManager] No tracks found for context '%s'." % context)
		return

	_current_context = context
	var crossfade_time: float = GameConfig.music_crossfade_time

	var stream: AudioStream = tracks[randi() % tracks.size()]
	var next := _next_player()
	var active := _active_player()

	next.stream = stream
	next.volume_db = -80.0
	next.play()

	var t := create_tween()
	t.tween_property(active, "volume_db", -80.0, crossfade_time).set_trans(Tween.TRANS_SINE)
	t.parallel().tween_property(next, "volume_db", 0.0, crossfade_time).set_trans(Tween.TRANS_SINE)
	# Capture active player reference before flip
	var active_ref := active
	t.tween_callback(func():
		active_ref.stop()
		_a_is_active = not _a_is_active
	)
