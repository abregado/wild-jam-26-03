## Turret script. Handles primary fire (burst bullets) and secondary fire (beacons).
## Attach to the "Turret" node inside PlayerCar (sibling of Camera3D).
##
## Aiming:
##   Each frame a physics ray is cast from the camera centre.
##   A yellow dot MeshInstance3D is placed at the hit point.
##   The turret slerps its GlobalTransform toward LookingAt(aimPoint) at TurretTrackingSpeed.
##   Bullets fire along the turret's actual -Z (so tracking lag matters).
##
## Burst fire: one press fires burst_count bullets with burst_delay between each.
##   rate_of_fire controls the minimum time between burst starts.

extends Node3D

# Mask 39 = layer 1 (world/train) + layer 2 (containers) + layer 3 (clamps) + layer 6 (drones)
const AIM_RAY_MASK := 39

@export var debug_tracking: bool = false

var _camera: Camera3D = null
var _barrel_tip_left: Node3D = null
var _barrel_tip_right: Node3D = null
var _barrel_left: MeshInstance3D = null
var _barrel_right: MeshInstance3D = null
var _barrel_left_rest: Vector3 = Vector3.ZERO
var _barrel_right_rest: Vector3 = Vector3.ZERO
var _barrel_tween: Tween = null
var _fire_from_left: bool = true

var _turret_dot: MeshInstance3D = null
var _muzzle_flash: Node3D = null

var _fire_cooldown: float = 0.0
var _beacon_cooldown: float = 0.0
var _fire_enabled: bool = true

var _burst_remaining: int = 0
var _burst_delay_timer: float = 0.0

var _bullet_scene: PackedScene = null
var _beacon_scene: PackedScene = null

var _turret_target_point: Vector3 = Vector3.ZERO
var _turret_quat: Quaternion = Quaternion.IDENTITY

var _debug_label: Label = null


func set_fire_enabled(value: bool) -> void:
	_fire_enabled = value
	if not value:
		_burst_remaining = 0


func _ready() -> void:
	# Turret is sibling of Camera3D inside PlayerCar
	_camera = get_parent().get_node("Camera3D") as Camera3D
	_barrel_tip_left  = get_node("BarrelTipLeft")  as Node3D
	_barrel_tip_right = get_node("BarrelTipRight") as Node3D

	_barrel_left  = get_node("BarrelLeft")  as MeshInstance3D
	_barrel_right = get_node("BarrelRight") as MeshInstance3D
	_barrel_left_rest  = _barrel_left.position
	_barrel_right_rest = _barrel_right.position

	_bullet_scene = load("res://scenes/projectiles/Bullet.tscn") as PackedScene
	_beacon_scene = load("res://scenes/projectiles/Beacon.tscn") as PackedScene

	_setup_turret_dot()
	_setup_muzzle_flash()

	_turret_quat = global_transform.basis.get_rotation_quaternion()

	if debug_tracking:
		_setup_debug_label()


func _setup_debug_label() -> void:
	_debug_label = Label.new()
	_debug_label.position = Vector2(8, 120)
	_debug_label.add_theme_font_size_override("font_size", 13)
	get_tree().root.call_deferred("add_child", _debug_label)


func _setup_turret_dot() -> void:
	_turret_dot = MeshInstance3D.new()
	var sphere := SphereMesh.new()
	sphere.radius = 0.06
	sphere.height = 0.12
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(1.0, 0.85, 0.0)
	mat.emission_enabled = true
	mat.emission = Color(1.0, 0.75, 0.0)
	mat.emission_energy_multiplier = 4.0
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	sphere.material = mat
	_turret_dot.mesh = sphere
	get_tree().root.call_deferred("add_child", _turret_dot)


func _setup_muzzle_flash() -> void:
	_muzzle_flash = Node3D.new()

	var flash_mesh := MeshInstance3D.new()
	var flash_sphere := SphereMesh.new()
	flash_sphere.radius = 0.2
	flash_sphere.height = 0.4
	var flash_mat := StandardMaterial3D.new()
	flash_mat.albedo_color = Color(1.0, 0.85, 0.3)
	flash_mat.emission_enabled = true
	flash_mat.emission = Color(1.0, 0.7, 0.1)
	flash_mat.emission_energy_multiplier = 12.0
	flash_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	flash_sphere.material = flash_mat
	flash_mesh.mesh = flash_sphere
	_muzzle_flash.add_child(flash_mesh)

	var flash_light := OmniLight3D.new()
	flash_light.light_color = Color(1.0, 0.7, 0.2)
	flash_light.light_energy = 8.0
	flash_light.omni_range = 6.0
	_muzzle_flash.add_child(flash_light)

	_muzzle_flash.scale = Vector3.ZERO
	add_child(_muzzle_flash)


func _process(delta: float) -> void:
	# 1. Yellow dot — where turret barrel is actually pointing
	_turret_target_point = _get_turret_aim_point()
	if _turret_dot != null and _turret_dot.is_inside_tree():
		_turret_dot.global_position = _turret_target_point

	# 2. Slerp turret toward camera forward
	var camera_forward := -_camera.global_transform.basis.z
	var min_y := -sin(deg_to_rad(GameConfig.turret_max_pitch_down))
	if camera_forward.y < min_y:
		camera_forward = Vector3(camera_forward.x, min_y, camera_forward.z).normalized()

	var fwd_len2  := camera_forward.length_squared()
	var dot_up    := camera_forward.dot(Vector3.UP)
	var can_track := fwd_len2 > 0.25 and absf(dot_up) < 0.99

	if debug_tracking and _debug_label != null and _debug_label.is_inside_tree():
		var reason := "gimbal" if absf(dot_up) >= 0.99 else "len²"
		_debug_label.text = "fwd: %s  len²: %.3f\ndot(up): %.3f\ntracking: %s" % [
			str(camera_forward), fwd_len2, dot_up,
			"YES" if can_track else ("NO — " + reason)]

	if can_track:
		var target_quat := Basis.looking_at(camera_forward, Vector3.UP).get_rotation_quaternion()
		var t := 1.0 - exp(-GameConfig.turret_tracking_speed * delta)
		_turret_quat = _turret_quat.slerp(target_quat, t).normalized()
		global_transform = Transform3D(Basis(_turret_quat), global_position)

	# 3. Cooldowns
	if _fire_cooldown > 0.0:  _fire_cooldown -= delta
	if _beacon_cooldown > 0.0: _beacon_cooldown -= delta

	# 4. Handle in-progress burst
	if _burst_remaining > 0:
		if _burst_delay_timer <= 0.0:
			_fire_single_bullet()
			_burst_remaining -= 1
			if _burst_remaining > 0:
				_burst_delay_timer = GameConfig.burst_delay
		else:
			_burst_delay_timer -= delta

	# 5. Trigger new burst
	var fire_input: bool
	if GameConfig.auto_fire:
		fire_input = _fire_enabled and Input.is_action_pressed("fire_primary")
	else:
		fire_input = _fire_enabled and Input.is_action_just_pressed("fire_primary")

	if fire_input and _fire_cooldown <= 0.0 and _burst_remaining <= 0:
		_start_burst()

	# 6. Beacon
	if _fire_enabled and Input.is_action_just_pressed("fire_beacon") and _beacon_cooldown <= 0.0:
		_fire_beacon()


func _get_turret_aim_point() -> Vector3:
	var from := _camera.global_position
	var to := from + (-global_transform.basis.z) * 150.0
	return _cast_aim_ray(from, to)


func _cast_aim_ray(from: Vector3, to: Vector3) -> Vector3:
	var space_state := _camera.get_world_3d().direct_space_state
	var query := PhysicsRayQueryParameters3D.create(from, to, AIM_RAY_MASK)
	query.collide_with_areas = true
	query.collide_with_bodies = true
	var result := space_state.intersect_ray(query)
	return result["position"] if result.size() > 0 else to


func _start_burst() -> void:
	_burst_remaining = GameConfig.burst_count
	_burst_delay_timer = 0.0
	_fire_cooldown = 1.0 / GameConfig.rate_of_fire


func _fire_single_bullet() -> void:
	var tip := _barrel_tip_left if _fire_from_left else _barrel_tip_right
	_fire_from_left = not _fire_from_left

	var bullet := _bullet_scene.instantiate() as Node3D
	get_tree().root.add_child(bullet)
	bullet.global_position = tip.global_position

	var fire_dir := (_turret_target_point - tip.global_position).normalized()
	if absf(fire_dir.dot(Vector3.UP)) < 0.99:
		bullet.look_at(tip.global_position + fire_dir, Vector3.UP)
	else:
		bullet.global_rotation = _camera.global_rotation

	bullet.initialize(GameConfig.turret_damage, GameConfig.blast_radius, GameConfig.bullet_speed)

	SoundManager.play("player_shoot")
	VfxSpawner.spawn("player_muzzle", tip.global_position)
	_trigger_muzzle_flash(tip)
	_trigger_barrel_retract()


func _trigger_muzzle_flash(tip: Node3D) -> void:
	_muzzle_flash.global_position = tip.global_position
	_muzzle_flash.scale = Vector3.ONE

	var tween := create_tween()
	tween.tween_property(_muzzle_flash, "scale", Vector3.ZERO, 0.07)\
		.set_trans(Tween.TRANS_QUAD)\
		.set_ease(Tween.EASE_IN)


func _trigger_barrel_retract() -> void:
	const RECOIL_Z := 0.22
	const RETURN_TIME := 0.18

	_barrel_left.position  = _barrel_left_rest  + Vector3(0.0, 0.0, RECOIL_Z)
	_barrel_right.position = _barrel_right_rest + Vector3(0.0, 0.0, RECOIL_Z)

	if _barrel_tween != null:
		_barrel_tween.kill()
	_barrel_tween = create_tween()
	_barrel_tween.tween_property(_barrel_left, "position", _barrel_left_rest, RETURN_TIME)\
		.set_trans(Tween.TRANS_ELASTIC).set_ease(Tween.EASE_OUT)
	_barrel_tween.parallel()\
		.tween_property(_barrel_right, "position", _barrel_right_rest, RETURN_TIME)\
		.set_trans(Tween.TRANS_ELASTIC).set_ease(Tween.EASE_OUT)


func _fire_beacon() -> void:
	var tip := _barrel_tip_left if _fire_from_left else _barrel_tip_right
	var beacon := _beacon_scene.instantiate() as Node3D
	get_tree().root.add_child(beacon)
	beacon.global_position = tip.global_position
	beacon.global_rotation = _camera.global_rotation
	beacon.initialize(GameConfig.beacon_speed)

	_beacon_cooldown = GameConfig.beacon_reload_speed
