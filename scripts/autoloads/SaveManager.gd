## Autoload singleton. Reads and writes the three save slots stored in user://saves/.
##
## Each slot file (slot_0.json, slot_1.json, slot_2.json) contains:
##   raids_played  int
##   last_save     string  (ISO date "YYYY-MM-DD")
##   resources     dict    (cargo name → count)
##   upgrades      array   (upgrade id strings)
##
## load_slot() returns a Dictionary with those keys, or null on missing/error.

extends Node

const SAVE_DIR := "user://saves"


func _ready() -> void:
	DirAccess.make_dir_recursive_absolute(SAVE_DIR)


func _slot_path(slot: int) -> String:
	return "%s/slot_%d.json" % [SAVE_DIR, slot]


func slot_exists(slot: int) -> bool:
	return FileAccess.file_exists(_slot_path(slot))


## Returns a Dictionary {raids_played, last_save, resources, upgrades}, or null on error.
func load_slot(slot: int) -> Variant:
	var path := _slot_path(slot)
	if not FileAccess.file_exists(path):
		return null

	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return null

	var json := JSON.new()
	if json.parse(file.get_as_text()) != OK:
		return null

	var d: Dictionary = json.data
	var data := {
		"raids_played": d.get("raids_played", 0),
		"last_save":    d.get("last_save",    ""),
		"resources":    {},
		"upgrades":     [],
	}

	var res_v = d.get("resources", null)
	if res_v != null:
		for key in res_v:
			data["resources"][str(key)] = int(res_v[key])

	var up_v = d.get("upgrades", null)
	if up_v != null:
		for item in up_v:
			data["upgrades"].append(str(item))

	return data


func save_slot(slot: int, session: Node) -> void:
	var resources := {}
	for key in session.player_resources:
		resources[key] = session.player_resources[key]

	var upgrades := []
	for id in session.applied_upgrades:
		upgrades.append(id)

	var data := {
		"raids_played": session.raids_played,
		"last_save":    Time.get_date_string_from_system(),
		"resources":    resources,
		"upgrades":     upgrades,
	}

	var path := _slot_path(slot)
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		push_error("[SaveManager] Cannot write %s" % path)
		return
	file.store_string(JSON.stringify(data))
	print("[SaveManager] Slot %d saved (raids=%d)." % [slot, session.raids_played])


func delete_slot(slot: int) -> void:
	var path := _slot_path(slot)
	if FileAccess.file_exists(path):
		DirAccess.remove_absolute(path)
	print("[SaveManager] Slot %d deleted." % slot)


## Returns [raids_played, last_save_date] or [0, ""] if slot is empty.
func get_slot_meta(slot: int) -> Array:
	var data = load_slot(slot)
	if data == null:
		return [0, ""]
	return [data["raids_played"], data["last_save"]]
