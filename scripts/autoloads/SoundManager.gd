## Autoload singleton. Plays sound effects via a pool of AudioStreamPlayers.
##
## API (call as autoload):
##   SoundManager.play("sound_id")
##   SoundManager.play_loop("loop_key", "sound_id")
##   SoundManager.stop_loop("loop_key")
##
## Sound names and file mappings come from GameConfig.sounds.
## All sounds load from assets/sounds/<filename>.wav.
## All players route through the "SFX" bus (volume controlled by SettingsManager).

extends Node

const POOL_SIZE := 16

var _pool: Array[AudioStreamPlayer] = []
var _loops: Dictionary = {}        # loop_key -> AudioStreamPlayer
var _active_loops: Dictionary = {} # loop_key -> true
var _streams: Dictionary = {}      # sound_id -> AudioStream


func _ready() -> void:
	_load_streams()
	for i in POOL_SIZE:
		var player := AudioStreamPlayer.new()
		player.bus = "SFX"
		add_child(player)
		_pool.append(player)


func _load_streams() -> void:
	for key in GameConfig.sounds:
		var filename: String = GameConfig.sounds[key]
		var path := "res://assets/sounds/%s.wav" % filename
		if ResourceLoader.exists(path):
			var stream := load(path) as AudioStream
			if stream != null:
				_streams[key] = stream
	print("[SoundManager] Loaded %d / %d sound(s)." % [_streams.size(), GameConfig.sounds.size()])


# ── Public API ────────────────────────────────────────────────────────────────

func play(sound_id: String) -> void:
	if sound_id not in _streams:
		return
	var stream: AudioStream = _streams[sound_id]
	# Find a free player in the pool
	for player in _pool:
		if not player.playing:
			player.stream = stream
			player.play()
			return
	# All busy — steal the first slot
	_pool[0].stream = stream
	_pool[0].play()


func play_loop(loop_key: String, sound_id: String) -> void:
	if sound_id not in _streams:
		return
	var stream: AudioStream = _streams[sound_id]
	_active_loops[loop_key] = true

	if loop_key not in _loops:
		var player := AudioStreamPlayer.new()
		player.bus = "SFX"
		add_child(player)
		_loops[loop_key] = player

		# Re-queue on finish if the loop is still marked active
		var captured_key := loop_key
		player.finished.connect(func():
			if captured_key in _active_loops and captured_key in _loops:
				_loops[captured_key].play()
		)

	var p: AudioStreamPlayer = _loops[loop_key]
	if p.stream == stream and p.playing:
		return
	p.stream = stream
	p.play()


func stop_loop(loop_key: String) -> void:
	_active_loops.erase(loop_key)
	if loop_key in _loops:
		_loops[loop_key].stop()
