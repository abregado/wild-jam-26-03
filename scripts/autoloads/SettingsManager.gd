## Autoload singleton. Owns audio bus setup and persistent user settings.
##
## Saves to user://settings.json:
##   - master_volume, music_volume, sfx_volume, music_muted, sfx_muted
##   - bindings: dict of action → { type: "key"|"mouse_button", physical_keycode|button_index }
##
## Must be loaded BEFORE SoundManager and MusicManager so buses exist when they start.

extends Node

const SETTINGS_PATH := "user://settings.json"

## [label, action_name] pairs for the rebind UI
const REBINDABLE_ACTIONS := [
	["Move Forward",  "move_forward"],
	["Move Backward", "move_backward"],
	["Move Left",     "move_left"],
	["Move Right",    "move_right"],
	["Fire",          "fire_primary"],
	["Beacon",        "fire_beacon"],
	["Flip Over",     "switch_side_over"],
	["Flip Under",    "switch_side_under"],
]

var master_volume: float = 1.0
var music_volume: float = 0.7
var sfx_volume: float = 1.0
var music_muted: bool = false
var sfx_muted: bool = false


func _ready() -> void:
	_setup_buses()

	# Load from config defaults first, then override from saved file
	master_volume = GameConfig.master_volume
	music_volume  = GameConfig.music_volume
	sfx_volume    = GameConfig.sfx_volume
	music_muted   = GameConfig.music_muted
	sfx_muted     = GameConfig.sfx_muted

	load_settings()
	apply_volumes()


# ── Bus setup ─────────────────────────────────────────────────────────────────

func _setup_buses() -> void:
	_ensure_bus("Music")
	_ensure_bus("SFX")


func _ensure_bus(bus_name: String) -> void:
	if AudioServer.get_bus_index(bus_name) != -1:
		return
	var idx := AudioServer.get_bus_count()
	AudioServer.add_bus(idx)
	AudioServer.set_bus_name(idx, bus_name)
	AudioServer.set_bus_send(idx, "Master")


# ── Volume API ────────────────────────────────────────────────────────────────

func set_master_volume(v: float) -> void:
	master_volume = clampf(v, 0.0, 1.0)
	apply_volumes()

func set_music_volume(v: float) -> void:
	music_volume = clampf(v, 0.0, 1.0)
	apply_volumes()

func set_sfx_volume(v: float) -> void:
	sfx_volume = clampf(v, 0.0, 1.0)
	apply_volumes()

func set_music_muted(m: bool) -> void:
	music_muted = m
	apply_volumes()

func set_sfx_muted(m: bool) -> void:
	sfx_muted = m
	apply_volumes()


func apply_volumes() -> void:
	var master_idx := AudioServer.get_bus_index("Master")
	var music_idx  := AudioServer.get_bus_index("Music")
	var sfx_idx    := AudioServer.get_bus_index("SFX")

	if master_idx >= 0:
		AudioServer.set_bus_volume_db(master_idx, linear_to_db(master_volume))
	if music_idx >= 0:
		AudioServer.set_bus_volume_db(music_idx, linear_to_db(0.0001 if music_muted else music_volume))
		AudioServer.set_bus_mute(music_idx, music_muted)
	if sfx_idx >= 0:
		AudioServer.set_bus_volume_db(sfx_idx, linear_to_db(0.0001 if sfx_muted else sfx_volume))
		AudioServer.set_bus_mute(sfx_idx, sfx_muted)


# ── Binding API ───────────────────────────────────────────────────────────────

## Returns a human-readable string for the first event of the given action.
func get_binding_label(action: String) -> String:
	var events := InputMap.action_get_events(action)
	if events.is_empty():
		return "—"
	var ev := events[0]
	if ev is InputEventKey:
		var key := ev as InputEventKey
		var k: Key = key.physical_keycode if key.physical_keycode != KEY_NONE else key.keycode
		return OS.get_keycode_string(k)
	if ev is InputEventMouseButton:
		var mb := ev as InputEventMouseButton
		match mb.button_index:
			MOUSE_BUTTON_LEFT:   return "LMB"
			MOUSE_BUTTON_RIGHT:  return "RMB"
			MOUSE_BUTTON_MIDDLE: return "MMB"
			_: return "Mouse %d" % int(mb.button_index)
	return ev.as_text()


## Applies a new event to the given action (replaces first binding).
func set_binding(action: String, new_event: InputEvent) -> void:
	InputMap.action_erase_events(action)
	InputMap.action_add_event(action, new_event)


# ── Persistence ───────────────────────────────────────────────────────────────

func save_settings() -> void:
	var bindings_dict := {}
	for pair in REBINDABLE_ACTIONS:
		var action: String = pair[1]
		var events := InputMap.action_get_events(action)
		if events.is_empty():
			continue
		var ev := events[0]
		var entry := {}
		if ev is InputEventKey:
			var key := ev as InputEventKey
			entry["type"] = "key"
			entry["physical_keycode"] = int(key.physical_keycode)
		elif ev is InputEventMouseButton:
			var mb := ev as InputEventMouseButton
			entry["type"] = "mouse_button"
			entry["button_index"] = int(mb.button_index)
		bindings_dict[action] = entry

	var data := {
		"master_volume": master_volume,
		"music_volume":  music_volume,
		"sfx_volume":    sfx_volume,
		"music_muted":   music_muted,
		"sfx_muted":     sfx_muted,
		"bindings":      bindings_dict,
	}

	var file := FileAccess.open(SETTINGS_PATH, FileAccess.WRITE)
	if file == null:
		push_error("[SettingsManager] Cannot write %s" % SETTINGS_PATH)
		return
	file.store_string(JSON.stringify(data))
	print("[SettingsManager] Settings saved.")


func load_settings() -> void:
	if not FileAccess.file_exists(SETTINGS_PATH):
		return
	var file := FileAccess.open(SETTINGS_PATH, FileAccess.READ)
	if file == null:
		return

	var json := JSON.new()
	if json.parse(file.get_as_text()) != OK:
		return

	var data: Dictionary = json.data

	if "master_volume" in data: master_volume = float(data["master_volume"])
	if "music_volume"  in data: music_volume  = float(data["music_volume"])
	if "sfx_volume"    in data: sfx_volume    = float(data["sfx_volume"])
	if "music_muted"   in data: music_muted   = bool(data["music_muted"])
	if "sfx_muted"     in data: sfx_muted     = bool(data["sfx_muted"])

	if "bindings" in data:
		var bindings: Dictionary = data["bindings"]
		for action in bindings:
			if not InputMap.has_action(action):
				continue
			var entry: Dictionary = bindings[action]
			var type: String = entry.get("type", "")
			var new_event: InputEvent = null

			if type == "key" and "physical_keycode" in entry:
				var new_key := InputEventKey.new()
				new_key.physical_keycode = int(entry["physical_keycode"]) as Key
				new_event = new_key
			elif type == "mouse_button" and "button_index" in entry:
				var new_mb := InputEventMouseButton.new()
				new_mb.button_index = int(entry["button_index"]) as MouseButton
				new_event = new_mb

			if new_event != null:
				set_binding(action, new_event)

	print("[SettingsManager] Settings loaded.")
