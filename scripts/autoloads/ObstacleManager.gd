## Autoload singleton. Runs a continuous loop: Startup → Warning → Active → Warning → Active → ...
## There is no idle gap between sections — as soon as one section ends, the next warning begins.
## "Empty" sections (no cliff, no limit) are valid and produce no obstacle cubes.
##
## ObstaclePool polls is_in_warning + upcoming_* to start streaming cubes during the warning
## period, so obstacles visually approach the player before the section becomes active.

extends Node

enum CliffSide { NONE, LEFT, RIGHT }
enum MovementLimit { NONE, ROOF, PLATEAU }

signal section_activated(cliff: int, limit: int)
signal section_cleared

var active_cliff_side: int = CliffSide.NONE
var active_movement_limit: int = MovementLimit.NONE

var is_in_warning: bool = false
var upcoming_cliff_side: int = CliffSide.NONE
var upcoming_movement_limit: int = MovementLimit.NONE

var _rng := RandomNumberGenerator.new()

enum _Phase { STARTUP, WARNING, ACTIVE }
var _phase: int = _Phase.STARTUP
var _phase_timer: float = 8.0  # initial pause before first warning
var _last_movement_limit: int = MovementLimit.NONE


func _ready() -> void:
	_rng.randomize()


func _process(delta: float) -> void:
	_phase_timer -= delta

	match _phase:
		_Phase.STARTUP:
			if _phase_timer <= 0.0:
				_start_warning()
		_Phase.WARNING:
			if _phase_timer <= 0.0:
				_activate_section()
		_Phase.ACTIVE:
			if _phase_timer <= 0.0:
				_end_section()


func _start_warning() -> void:
	var rolled := _roll_next_section()
	upcoming_cliff_side = rolled[0]
	upcoming_movement_limit = rolled[1]
	is_in_warning = true
	_phase = _Phase.WARNING
	_phase_timer = GameConfig.obstacle_warning_time
	print("[ObstacleManager] Warning: %d + %d" % [upcoming_cliff_side, upcoming_movement_limit])


func _activate_section() -> void:
	active_cliff_side = upcoming_cliff_side
	active_movement_limit = upcoming_movement_limit
	_last_movement_limit = active_movement_limit
	is_in_warning = false
	_phase = _Phase.ACTIVE
	_phase_timer = _rng.randf_range(GameConfig.obstacle_section_min_duration,
		GameConfig.obstacle_section_max_duration)
	section_activated.emit(active_cliff_side, active_movement_limit)
	print("[ObstacleManager] Active: %d + %d for %.1fs" % [active_cliff_side, active_movement_limit, _phase_timer])


func _end_section() -> void:
	active_cliff_side = CliffSide.NONE
	active_movement_limit = MovementLimit.NONE
	section_cleared.emit()
	print("[ObstacleManager] Section ended → starting next warning immediately.")
	_start_warning()


func _roll_next_section() -> Array:
	# Roll cliff: 30% Left, 30% Right, 40% None
	var cliff: int
	var r := _rng.randf()
	if r < 0.3:
		cliff = CliffSide.LEFT
	elif r < 0.6:
		cliff = CliffSide.RIGHT
	else:
		cliff = CliffSide.NONE

	# Roll limit: 30% Roof, 30% Plateau, 40% None
	# Forbid roof→plateau and plateau→roof transitions
	var limit: int
	var attempts := 0
	while true:
		var r2 := _rng.randf()
		if r2 < 0.3:
			limit = MovementLimit.ROOF
		elif r2 < 0.6:
			limit = MovementLimit.PLATEAU
		else:
			limit = MovementLimit.NONE
		attempts += 1
		if attempts >= 10 or not _is_opposite_limit(_last_movement_limit, limit):
			break

	return [cliff, limit]


func _is_opposite_limit(last: int, next: int) -> bool:
	return (last == MovementLimit.ROOF and next == MovementLimit.PLATEAU) \
		or (last == MovementLimit.PLATEAU and next == MovementLimit.ROOF)
