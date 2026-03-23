## Autoload singleton. Tracks collected cargo and game statistics across the run.
## Connect ContainerNode.cargo_detached -> on_cargo_detached.
## Connect ContainerNode.container_destroyed -> on_container_destroyed.
##
## Save-slot fields (active_slot, raids_played, player_resources, applied_upgrades)
## are populated by load_from_save() when a slot is chosen from the main menu.
## write_to_save() persists them via SaveManager.

extends Node

signal cargo_collected(cargo_name: String)
signal stats_changed

# ── Per-run state (reset each raid) ──────────────────────────────────────────
var identified_cargo: Dictionary = {}    # cargo_name -> int
var unidentified_cargo: Array = []       # Array of String
var collected_cargo: Dictionary = {}     # cargo_name -> int
var containers_detached: int = 0
var containers_destroyed: int = 0

# ── Persistent across raids ───────────────────────────────────────────────────
var active_slot: int = -1
var raids_played: int = 0
var player_resources: Dictionary = {}  # cargo_name -> int
var applied_upgrades: Array = []       # Array of String

# ── Cutscene tracking ─────────────────────────────────────────────────────────
## True only on the very first raid; false for every subsequent one.
var is_first_raid: bool = true

func mark_raid_started() -> void:
	is_first_raid = false


func _ready() -> void:
	reset()


## Clears per-raid state. Does NOT touch save-slot or persistent fields.
func reset() -> void:
	identified_cargo.clear()
	unidentified_cargo.clear()
	collected_cargo.clear()
	containers_detached = 0
	containers_destroyed = 0
	# player_resources, applied_upgrades, raids_played, active_slot — intentionally kept


## Called by MainMenu when the player selects a used save slot.
## data is a Dictionary from SaveManager.load_slot().
func load_from_save(data: Dictionary, slot: int) -> void:
	active_slot      = slot
	raids_played     = data.get("raids_played", 0)
	player_resources = (data.get("resources", {}) as Dictionary).duplicate()
	applied_upgrades = (data.get("upgrades", []) as Array).duplicate()
	is_first_raid    = false

	# Re-apply saved upgrades to GameConfig
	for id in applied_upgrades:
		var def = null
		for u in GameConfig.upgrades:
			if u.id == id:
				def = u
				break
		if def != null:
			GameConfig.apply_upgrade(def)

	print("[GameSession] Loaded slot %d: raids=%d, upgrades=%d" % [slot, raids_played, applied_upgrades.size()])


## Called by MainMenu when the player starts a new game in an empty slot.
func start_new_game(slot: int) -> void:
	active_slot      = slot
	raids_played     = 0
	player_resources = {}
	applied_upgrades = []
	is_first_raid    = true
	reset()
	print("[GameSession] New game in slot %d." % slot)


## Increments raids_played and persists current state to the active slot.
## Called by AfterAction before scene reload.
func write_to_save() -> void:
	if active_slot < 0:
		return
	raids_played += 1
	SaveManager.save_slot(active_slot, self)


# ── Cargo callbacks ───────────────────────────────────────────────────────────

func on_cargo_detached(cargo_name: String, was_beaconed: bool) -> void:
	if was_beaconed:
		if cargo_name not in identified_cargo:
			identified_cargo[cargo_name] = 0
		identified_cargo[cargo_name] += 1
	else:
		unidentified_cargo.append(cargo_name)

	if cargo_name not in collected_cargo:
		collected_cargo[cargo_name] = 0
	collected_cargo[cargo_name] += 1
	containers_detached += 1
	cargo_collected.emit(cargo_name)
	stats_changed.emit()


func on_container_destroyed() -> void:
	containers_destroyed += 1
	stats_changed.emit()
