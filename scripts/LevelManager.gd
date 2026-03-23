## Manages level end condition.
## Attach to a Node child of Main.tscn called "LevelManager".
##
## Each frame checks if player is out of turret range.
## "Out of range" = player is more than TurretRange units behind the caboose rear.
##
## When out of range:
##   1. Shows HUD warning with 3-second countdown.
##   2. Countdown starts.
##   3. On expire: TriggerZoomAway(), disable player input.
##   4. After 2 more seconds: change scene to AfterAction.tscn.

extends Node

const WARNING_DURATION := 3.0
const ZOOM_DURATION    := 2.0

var _player_car: Node3D = null
var _hud: Node = null
var _train_builder: TrainBuilder = null

var _warning_timer: float = -1.0
var _zoom_timer: float = -1.0
var _warning_active: bool = false
var _zoom_triggered: bool = false
var _cutscene_active: bool = true


func _ready() -> void:
	_player_car    = get_parent().get_node("PlayerCar") as Node3D
	_hud           = get_parent().get_node("HUD")
	_train_builder = get_parent().get_node("Train") as TrainBuilder

	# Position player near the front of the locomotive at start
	var start_z := _train_builder.locomotive_z - 4.0
	_player_car.position = Vector3(PlayerCar.X_OFFSET, _player_car.y_height, start_z)
	_player_car.set_train_front_z(_train_builder.locomotive_z)

	print("[LevelManager] locomotive_z=%.1f, caboose_z=%.1f, PlayerStart Z=%.1f" % [
		_train_builder.locomotive_z, _train_builder.caboose_z, start_z])

	# Auto-beacon all Scrap containers so they appear grey from the start.
	# Pre-scanning non-Scrap containers is deferred until on_cutscene_done()
	# so containers appear untagged (orange) during the intro cutscene.
	for c in _train_builder.all_containers:
		if c.is_scrap:
			c.tag()


## Called by CutsceneManager when the intro cutscene finishes.
func on_cutscene_done() -> void:
	_cutscene_active = false

	var pre_scan_count := GameConfig.number_pre_scanned_containers
	if pre_scan_count > 0:
		_pre_scan_containers(pre_scan_count)

	print("[LevelManager] Cutscene done. Gameplay active.")


func _process(delta: float) -> void:
	if _cutscene_active:
		return

	# Update player's reference to train front
	_player_car.set_train_front_z(_train_builder.locomotive_z)

	var caboose_world_z := _train_builder.global_position.z + _train_builder.caboose_z
	var out_of_range := TrainSpeedManager.is_player_out_of_range(
		_player_car.global_position.z, caboose_world_z)

	if out_of_range and not _warning_active and not _zoom_triggered:
		_start_warning()
	elif not out_of_range and _warning_active and not _zoom_triggered:
		# Player moved back into range — cancel warning
		_warning_active = false
		_warning_timer = -1.0
		_hud.hide_warning()

	if _warning_active and not _zoom_triggered:
		_warning_timer -= delta
		_hud.update_countdown(_warning_timer)
		if _warning_timer <= 0.0:
			_trigger_zoom()

	if _zoom_triggered:
		# Physically move the train away using zoom speed
		_train_builder.position += Vector3(0.0, 0.0, TrainSpeedManager.train_zoom_speed * delta)

		_zoom_timer -= delta
		if _zoom_timer <= 0.0:
			_end_level()


func _pre_scan_containers(count: int) -> void:
	var taggable: Array = []
	for c in _train_builder.all_containers:
		if not c.is_scrap:
			taggable.append(c)

	var rng := RandomNumberGenerator.new()
	rng.randomize()
	for i in range(taggable.size() - 1, 0, -1):
		var j := rng.randi_range(0, i)
		var tmp = taggable[i]
		taggable[i] = taggable[j]
		taggable[j] = tmp

	var to_tag := mini(count, taggable.size())
	for i in to_tag:
		taggable[i].tag()

	print("[LevelManager] Pre-scanned %d container(s)." % to_tag)


func _start_warning() -> void:
	_warning_active = true
	_warning_timer = WARNING_DURATION
	_hud.show_warning(_warning_timer)
	SoundManager.play("cliff_warning")
	print("[LevelManager] Player out of range. Warning started.")


func _trigger_zoom() -> void:
	_zoom_triggered = true
	_warning_active = false
	_player_car.disable_input()
	TrainSpeedManager.trigger_zoom_away()
	_zoom_timer = ZOOM_DURATION
	print("[LevelManager] Zoom away triggered.")


func _end_level() -> void:
	print("[LevelManager] Level ended. Loading AfterAction.")
	get_tree().change_scene_to_file("res://scenes/ui/AfterAction.tscn")
