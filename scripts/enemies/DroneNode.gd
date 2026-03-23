## Enemy drone deployed from a DeployerNode on top of train carriages.
##
## State machine:
##   DEPLOYING          → scale 0→1 tween, fly upward 3 units from deployer, then MOVING_TO_POSITION
##   MOVING_TO_POSITION → fly to combat position at drone_move_speed, then IN_POSITION
##   IN_POSITION        → wait for fire cooldown; face player; if player too far → REPOSITIONING
##   REPOSITIONING      → fly to new combat position at drone_combat_speed, then IN_POSITION
##   FOLLOWING_SIDE     → fly over train top to follow player side-switch, then MOVING_TO_POSITION
##   RETURNING          → two-phase landing: fly above deployer, then descend and despawn
##   DYING              → fall to ground (gravity), queue_free after landing
##
## Collision:
##   Area3D on layer 32 (drones). Player bullet raycasts (mask 39) detect this.

class_name DroneNode
extends Node3D

const _DroneBulletScript = preload("res://scripts/projectiles/DroneBullet.gd")

enum _State {
	DEPLOYING, MOVING_TO_POSITION, IN_POSITION, REPOSITIONING,
	FOLLOWING_SIDE, RETURNING, DYING
}

const OVER_TRAIN_HEIGHT := 14.0
const ARRIVAL_THRESHOLD := 0.5
const DYING_GRAVITY := 12.0
const DYING_DURATION := 2.0
const RETURN_HOVER_HEIGHT := 4.0
const LANDING_SPEED := 3.0
const PRE_FIRE_WINDOW := 0.35

const DRONE_COLOR := Color(0.2, 0.2, 0.25)

var _state: int = _State.DEPLOYING
var _deployer: Node3D = null
var _player_car: Node3D = null
var _rng := RandomNumberGenerator.new()

var _hp: float = 0.0
var _fire_cooldown: float = 0.0
var _combat_target: Vector3 = Vector3.ZERO
var _last_known_player_side: bool = true
var _follow_waypoint: Vector3 = Vector3.ZERO
var _deploy_lift_target: float = 0.0
var _dying_timer: float = 0.0
var _dying_velocity: float = 0.0
var _pre_fired: bool = false
var _return_phase: int = 0
var _return_hover_point: Vector3 = Vector3.ZERO
var _area: Area3D = null
var _spawn_tween_done: bool = false


func initialize(deployer: Node3D, player: Node3D) -> void:
	_deployer = deployer
	_player_car = player
	_rng.randomize()

	_hp = GameConfig.drone_hitpoints
	_last_known_player_side = player.is_on_right_side if "is_on_right_side" in player else true
	_deploy_lift_target = global_position.y + 3.0
	_fire_cooldown = 1.0 / GameConfig.drone_fire_rate


func _ready() -> void:
	var mesh_inst := MeshInstance3D.new()
	mesh_inst.name = "MeshSlot"

	var glb_mesh := _try_load_glb_mesh("res://assets/models/enemies/drone.glb")
	if glb_mesh != null:
		mesh_inst.mesh = glb_mesh
	else:
		var box := BoxMesh.new()
		box.size = Vector3(0.8, 0.25, 0.8)
		mesh_inst.mesh = box

	mesh_inst.material_override = StandardMaterial3D.new()
	(mesh_inst.material_override as StandardMaterial3D).albedo_color = DRONE_COLOR
	add_child(mesh_inst)

	_area = Area3D.new()
	_area.collision_layer = 32
	_area.collision_mask = 0
	_area.monitorable = true
	_area.monitoring = false
	_area.name = "Area3D"
	var col := CollisionShape3D.new()
	var shape := SphereShape3D.new()
	shape.radius = 0.5
	col.shape = shape
	_area.add_child(col)
	add_child(_area)

	# Spawn punch: scale 0 → 1 with elastic ease
	scale = Vector3.ZERO
	var tween := create_tween()
	tween.tween_property(self, "scale", Vector3.ONE, 0.45)\
		.set_trans(Tween.TRANS_ELASTIC)\
		.set_ease(Tween.EASE_OUT)
	tween.tween_callback(func():
		_spawn_tween_done = true
		SoundManager.play("drone_deployed")
		VfxSpawner.spawn("drone_deployed", global_position)
	)


func take_damage(amount: float) -> void:
	if _state == _State.DYING or _state == _State.RETURNING:
		return
	_hp -= amount
	if _hp <= 0.0:
		_start_dying()


func _process(delta: float) -> void:
	if not _spawn_tween_done:
		return
	if _player_car == null or not _player_car.is_inside_tree():
		queue_free()
		return

	# Range checks (skip during terminal/transit states)
	if _state != _State.DYING and _state != _State.RETURNING and _state != _State.DEPLOYING:
		var dist_to_deployer := global_position.distance_to(_deployer.global_position)
		if dist_to_deployer > GameConfig.drone_max_deployer_distance:
			if ObstacleManager.active_movement_limit == ObstacleManager.MovementLimit.ROOF:
				_start_dying()
			else:
				_start_returning()
			return

	# Side-switch detection
	var player_on_right: bool = _player_car.is_on_right_side if "is_on_right_side" in _player_car else true
	if player_on_right != _last_known_player_side \
			and _state != _State.DYING \
			and _state != _State.DEPLOYING \
			and _state != _State.FOLLOWING_SIDE \
			and _state != _State.RETURNING:
		_last_known_player_side = player_on_right
		_start_following_side()

	match _state:
		_State.DEPLOYING:          _process_deploying(delta)
		_State.MOVING_TO_POSITION: _process_moving(delta, GameConfig.drone_move_speed)
		_State.IN_POSITION:        _process_in_position(delta)
		_State.REPOSITIONING:      _process_moving(delta, GameConfig.drone_combat_speed)
		_State.FOLLOWING_SIDE:     _process_following_side(delta)
		_State.RETURNING:          _process_returning(delta)
		_State.DYING:              _process_dying(delta)


func _process_deploying(dt: float) -> void:
	var new_y := global_position.y + GameConfig.drone_move_speed * dt
	global_position = Vector3(global_position.x, minf(new_y, _deploy_lift_target), global_position.z)
	if global_position.y >= _deploy_lift_target - 0.1:
		_combat_target = _compute_combat_position()
		_state = _State.MOVING_TO_POSITION


func _process_moving(dt: float, speed: float) -> void:
	var dir := _combat_target - global_position
	var dist := dir.length()
	if dist < ARRIVAL_THRESHOLD:
		global_position = _combat_target
		_fire_cooldown = 1.0 / GameConfig.drone_fire_rate
		_pre_fired = false
		_state = _State.IN_POSITION
		return
	global_position += dir.normalized() * speed * dt
	_look_toward(dir)


func _process_in_position(dt: float) -> void:
	var to_player := _player_car.global_position - global_position
	_look_toward(to_player)

	var dist_to_player := global_position.distance_to(_player_car.global_position)
	if dist_to_player > GameConfig.drone_chase_distance:
		_combat_target = _compute_combat_position()
		_state = _State.REPOSITIONING
		_pre_fired = false
		return

	_fire_cooldown -= dt

	if not _pre_fired and _fire_cooldown <= PRE_FIRE_WINDOW:
		_pre_fired = true
		VfxSpawner.spawn("drone_prefire", global_position)

	if _fire_cooldown <= 0.0:
		_fire()
		_pre_fired = false
		if _rng.randf() < GameConfig.drone_reposition_chance:
			_combat_target = _compute_combat_position()
			_state = _State.REPOSITIONING
		else:
			_fire_cooldown = 1.0 / GameConfig.drone_fire_rate


func _process_following_side(dt: float) -> void:
	var dir := _follow_waypoint - global_position
	if dir.length() < ARRIVAL_THRESHOLD:
		_combat_target = _compute_combat_position()
		_state = _State.MOVING_TO_POSITION
		return
	global_position += dir.normalized() * GameConfig.drone_move_speed * dt
	_look_toward(dir)


func _process_returning(dt: float) -> void:
	if _return_phase == 0:
		var dir := _return_hover_point - global_position
		if dir.length() < ARRIVAL_THRESHOLD:
			_return_phase = 1
			return
		global_position += dir.normalized() * GameConfig.drone_move_speed * dt
		_look_toward(dir)
	else:
		var target := _deployer.global_position
		var dir := target - global_position
		if dir.length() < 0.2:
			var tween := create_tween()
			tween.tween_property(self, "scale", Vector3.ZERO, 0.3)\
				.set_trans(Tween.TRANS_QUAD)\
				.set_ease(Tween.EASE_IN)
			tween.tween_callback(queue_free)
			_deployer.on_drone_returned()
			_state = _State.DYING
			return
		global_position += dir.normalized() * LANDING_SPEED * dt


func _process_dying(dt: float) -> void:
	_dying_velocity += DYING_GRAVITY * dt
	global_position -= Vector3(0.0, _dying_velocity * dt, 0.0)
	_dying_timer += dt
	rotation_degrees += Vector3(90.0 * dt, 120.0 * dt, 60.0 * dt)
	if _dying_timer >= DYING_DURATION:
		queue_free()


func _start_dying() -> void:
	_state = _State.DYING
	_dying_timer = 0.0
	_dying_velocity = 0.0
	_deployer.on_drone_destroyed()
	_area.set_deferred("monitorable", false)
	SoundManager.play("drone_destroyed")
	VfxSpawner.spawn("drone_destroyed", global_position)


func _start_following_side() -> void:
	_state = _State.FOLLOWING_SIDE
	_follow_waypoint = Vector3(0.0, OVER_TRAIN_HEIGHT, global_position.z)
	_last_known_player_side = _player_car.is_on_right_side if "is_on_right_side" in _player_car else true


func _start_returning() -> void:
	_state = _State.RETURNING
	_return_phase = 0
	_return_hover_point = _deployer.global_position + Vector3(0.0, RETURN_HOVER_HEIGHT, 0.0)
	_area.set_deferred("monitorable", false)


func _fire() -> void:
	var is_hit := _rng.randf() < GameConfig.drone_hit_chance
	var target_pos: Vector3

	if is_hit:
		target_pos = _player_car.global_position + Vector3(
			_rng.randf_range(-0.15, 0.15),
			_rng.randf_range(-0.15, 0.15),
			_rng.randf_range(-0.15, 0.15))
	else:
		var miss_x := _rng.randf_range(0.6, 1.2) * (1.0 if _rng.randf() > 0.5 else -1.0)
		var miss_y := _rng.randf_range(0.6, 1.2) * (1.0 if _rng.randf() > 0.5 else -1.0)
		target_pos = _player_car.global_position + Vector3(miss_x, miss_y, 0.0)

	VfxSpawner.spawn("drone_muzzle", global_position)

	var bullet: Node3D = _DroneBulletScript.new()
	get_tree().root.add_child(bullet)
	bullet.global_position = global_position
	bullet.initialize(target_pos, is_hit, GameConfig.drone_bullet_speed)


func _compute_combat_position() -> Vector3:
	var side := 1.0 if (_player_car.is_on_right_side if "is_on_right_side" in _player_car else true) else -1.0
	# Access PlayerCar.X_OFFSET — defined as const in PlayerCar.gd
	var x_offset := 8.0
	if _player_car != null and "X_OFFSET" in _player_car:
		x_offset = _player_car.X_OFFSET
	var x := side * x_offset + _rng.randf_range(-1.2, 1.2)
	var y := _player_car.global_position.y + _rng.randf_range(
		GameConfig.drone_height_min, GameConfig.drone_height_max)
	var z := _player_car.global_position.z + _rng.randf_range(-4.0, 4.0)
	return Vector3(x, y, z)


func _look_toward(dir: Vector3) -> void:
	if dir.length_squared() < 0.01:
		return
	var flat := Vector3(dir.x, 0.0, dir.z)
	if flat.length_squared() > 0.01:
		rotation_degrees = Vector3(0.0, rad_to_deg(atan2(-flat.x, -flat.z)), 0.0)


func _try_load_glb_mesh(path: String):
	if not ResourceLoader.exists(path):
		return null
	var scene := load(path) as PackedScene
	if scene == null:
		return null
	var root := scene.instantiate() as Node3D
	var body := root.find_child("Body") as MeshInstance3D
	var mesh = body.mesh if body != null else null
	root.queue_free()
	return mesh
