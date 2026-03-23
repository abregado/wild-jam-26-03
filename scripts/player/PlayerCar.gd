## Controls the player's flying car alongside the train.
##
## MOUSE CAPTURE: Click the game window to capture mouse and enable aiming.
## Press Escape to release the mouse.
## Mouse is NOT auto-captured at startup — requires a click first.
##
## Position: Fixed at X=8.0 (right side of train at X=0), Y=CarDriveHeight.
## Camera default: faces -X (toward train). PlayerCar starts at rotation.y = 90.
## Mouse X = yaw (camera). Mouse Y = pitch (camera, clamped ±60°/30°).
##
## WASD accelerate/decelerate based on camera direction projected onto train Z axis.

class_name PlayerCar
extends Node3D

const X_OFFSET := 8.0
const BOB_AMPLITUDE := 0.12
const BOB_FREQUENCY := 0.7
const OVER_ARC_HEIGHT := 6.0
const UNDER_ARC_HEIGHT := 6.0
# Flip-path physics check uses layer 9 (256)
const FLIP_BODY_MASK := 256

var y_height: float = 9.0
var is_input_enabled: bool:
	get: return _input_enabled
var relative_velocity: float:
	get: return _relative_velocity
var is_on_right_side: bool:
	get: return _on_right_side
var is_flipping_under: bool:
	get: return _is_switching_sides and _switch_arc_dir < 0
var can_switch_under: bool:
	get: return not _is_switching_sides
var can_switch_over: bool:
	get: return not _is_switching_sides

var _relative_velocity: float = 0.0
var _pitch: float = 0.0
var _look_yaw: float = 0.0

var _camera: Camera3D = null
var _turret: Node3D = null

var _input_enabled: bool = true
var _train_front_z: float = 60.0
var _capture_desired: bool = true
var _bob_time: float = 0.0

var _on_right_side: bool = true
var _was_accelerating: bool = false
var _was_decelerating: bool = false
var _is_switching_sides: bool = false
var _switch_progress: float = 0.0
var _arc_start_x: float = 0.0
var _switch_arc_dir: int = 0
var _flip_reversed: bool = false
var _flip_locked_velocity: float = 0.0

var _flip_check_sphere: SphereShape3D = null
var _shield: Node3D = null

const _ShieldScript = preload("res://scripts/player/Shield.gd")


func _ready() -> void:
	_camera = get_node("Camera3D") as Camera3D
	_turret = get_node("Turret") as Node3D
	y_height = GameConfig.car_drive_height

	rotation_degrees = Vector3(0.0, 90.0, 0.0)

	_flip_check_sphere = SphereShape3D.new()
	_flip_check_sphere.radius = 0.4

	_shield = _ShieldScript.new()
	add_child(_shield)

	get_window().focus_entered.connect(_on_viewport_focus_entered)

	print("[PlayerCar] Ready. Mouse will auto-capture. Escape = release.")


func _input(event: InputEvent) -> void:
	if Input.mouse_mode != Input.MOUSE_MODE_CAPTURED:
		return
	if not _input_enabled:
		return

	if event is InputEventMouseMotion:
		var motion := event as InputEventMouseMotion
		_look_yaw -= motion.relative.x * 0.25
		_pitch     -= motion.relative.y * 0.25
		_pitch = clampf(_pitch, -60.0, 30.0)
		_camera.rotation_degrees = Vector3(_pitch, _look_yaw, 0.0)


func _on_viewport_focus_entered() -> void:
	if not _capture_desired:
		return
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED


func _process(delta: float) -> void:
	# Poll every frame until capture succeeds
	if _capture_desired and Input.mouse_mode != Input.MOUSE_MODE_CAPTURED:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
		if Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
			print("[PlayerCar] Mouse captured.")

	_bob_time += delta
	var bob_offset := sin(_bob_time * BOB_FREQUENCY * TAU) * BOB_AMPLITUDE

	if not _input_enabled:
		_is_switching_sides = false
		var side_x := X_OFFSET if _on_right_side else -X_OFFSET
		position = Vector3(side_x, y_height + bob_offset, position.z)
		return

	var accel := GameConfig.car_acceleration
	var decel := GameConfig.car_deceleration

	if not _is_switching_sides:
		var cam_basis := _camera.global_transform.basis
		var ws_factor := -cam_basis.z.z
		var ad_factor :=  cam_basis.x.z

		var input_axis := 0.0
		if Input.is_action_pressed("move_forward"):  input_axis += ws_factor
		if Input.is_action_pressed("move_backward"): input_axis -= ws_factor
		if Input.is_action_pressed("move_right"):    input_axis += ad_factor
		if Input.is_action_pressed("move_left"):     input_axis -= ad_factor
		input_axis = clampf(input_axis, -1.0, 1.0)

		if absf(input_axis) > 0.01:
			var rate := accel if input_axis > 0.0 else decel
			_relative_velocity += input_axis * rate * delta
		else:
			var drag := decel * delta
			if absf(_relative_velocity) < drag:
				_relative_velocity = 0.0
			else:
				_relative_velocity -= signf(_relative_velocity) * drag

		_relative_velocity = clampf(_relative_velocity,
			TrainSpeedManager.max_relative_backward, TrainSpeedManager.max_relative_forward)

		# Loop sounds
		var is_accel := input_axis > 0.01
		var is_decel := input_axis < -0.01
		if is_accel != _was_accelerating:
			if is_accel: SoundManager.play_loop("car_accel", "car_accelerating")
			else:        SoundManager.stop_loop("car_accel")
			_was_accelerating = is_accel
		if is_decel != _was_decelerating:
			if is_decel: SoundManager.play_loop("car_decel", "car_decelerating")
			else:        SoundManager.stop_loop("car_decel")
			_was_decelerating = is_decel

		_check_cliff_auto_flip(delta)

		if Input.is_action_just_pressed("switch_side_over") and _is_flip_path_clear(1):
			_start_side_switch(1)
		if Input.is_action_just_pressed("switch_side_under") and _is_flip_path_clear(-1):
			_start_side_switch(-1)

	var active_velocity := _flip_locked_velocity if _is_switching_sides else _relative_velocity
	var new_z := position.z + active_velocity * delta

	if _is_switching_sides:
		if _flip_reversed:
			_switch_progress -= delta / GameConfig.side_change_time
		else:
			_switch_progress += delta / GameConfig.side_change_time

		if _switch_progress >= 1.0:
			_switch_progress = 1.0
			_is_switching_sides = false
			_flip_reversed = false
			_on_right_side = not _on_right_side
		elif _switch_progress <= 0.0:
			_switch_progress = 0.0
			_is_switching_sides = false
			_flip_reversed = false
		else:
			var t := _switch_progress
			var arc_height := OVER_ARC_HEIGHT if _switch_arc_dir > 0 else UNDER_ARC_HEIGHT
			var new_x := _arc_start_x * cos(t * PI)
			var new_y := y_height + _switch_arc_dir * arc_height * sin(t * PI)
			position = Vector3(new_x, new_y + bob_offset, new_z)
			return

	var side_x_pos := X_OFFSET if _on_right_side else -X_OFFSET
	position = Vector3(side_x_pos, y_height + bob_offset, new_z)


## Samples the flip arc at N points and sphere-queries each against obstacle flip bodies.
## Returns true when the full path is clear.
func _is_flip_path_clear(direction: int) -> bool:
	var space_state := get_world_3d().direct_space_state
	var start_x := X_OFFSET if _on_right_side else -X_OFFSET
	var arc_height := OVER_ARC_HEIGHT if direction > 0 else UNDER_ARC_HEIGHT
	var samples := GameConfig.flip_ray_samples
	var duration := GameConfig.side_change_time
	var combined_speed := _relative_velocity + TrainSpeedManager.current_train_speed

	for i in range(1, samples + 1):
		var t := float(i) / samples
		var sample_x := start_x * cos(t * PI)
		var sample_y := y_height + direction * arc_height * sin(t * PI)
		var sample_z := position.z + combined_speed * (t * duration)

		var query := PhysicsShapeQueryParameters3D.new()
		query.shape = _flip_check_sphere
		query.transform = Transform3D(Basis.IDENTITY, Vector3(sample_x, sample_y, sample_z))
		query.collision_mask = FLIP_BODY_MASK
		query.collide_with_bodies = true
		query.collide_with_areas = false

		if space_state.intersect_shape(query, 1).size() > 0:
			return false
	return true


## Casts a short forward ray. If a cliff body (layer 8 = 128) is detected, auto-flips.
func _check_cliff_auto_flip(dt: float) -> void:
	if _is_switching_sides:
		return

	var space_state := get_world_3d().direct_space_state
	var target := global_position + Vector3(0.0, 0.0, GameConfig.cliff_detection_distance)
	var query := PhysicsRayQueryParameters3D.create(global_position, target, 128)
	var result := space_state.intersect_ray(query)

	if result.size() == 0:
		return

	if _is_flip_path_clear(-1):
		_start_side_switch(-1)
	elif _is_flip_path_clear(1):
		_start_side_switch(1)
	else:
		_relative_velocity -= GameConfig.cliff_auto_flip_brake * dt


func _start_side_switch(direction: int) -> void:
	_is_switching_sides = true
	_switch_progress = 0.0
	_switch_arc_dir = direction
	_arc_start_x = X_OFFSET if _on_right_side else -X_OFFSET
	_flip_locked_velocity = _relative_velocity
	_flip_reversed = false


func flash_shield_hit() -> void:
	SoundManager.play("player_car_hit")
	if _shield != null:
		_shield.flash_hit()


func set_train_front_z(z: float) -> void:
	_train_front_z = z


func disable_input() -> void:
	_input_enabled = false
	_capture_desired = false
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	if _turret != null and _turret.has_method("set_fire_enabled"):
		_turret.set_fire_enabled(false)


func enable_input() -> void:
	_input_enabled = true
	_capture_desired = true
	if _turret != null and _turret.has_method("set_fire_enabled"):
		_turret.set_fire_enabled(true)


func get_distance_behind_front() -> float:
	return _train_front_z - position.z
