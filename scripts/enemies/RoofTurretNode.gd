## Roof-mounted enemy turret. Sits on top of train carriages.
##
## Visual structure (built procedurally; artist can provide roof_turret.glb with
## child nodes named "Base", "Dome", "Barrel" to override each part):
##   RoofTurretNode  (yaws to track player in all states)
##   ├── BaseMesh    (flat base plate — always visible)
##   ├── DomePivot   (rotates for activation flip animation)
##   │   └── DomeMesh
##   └── BarrelMount (pitches to aim at player when Active)
##       └── BarrelMesh (scales 0→1 on activation)
##
## States:
##   INACTIVE  — dormant. DomePivot.x = 180° (dome upside-down), Barrel scale = 0.
##   ACTIVE    — tracks player and fires in bursts. Dome unfolds, barrel extends.
##   REPAIRING — hit by player bullet. Dome+Barrel hidden, repair VFX plays.
##               After repair time, returns to INACTIVE.
##
## Collision:
##   Area3D on layer 32 (enemies). Monitorable only when ACTIVE.

class_name RoofTurretNode
extends Node3D

const _DroneBulletScript = preload("res://scripts/projectiles/DroneBullet.gd")

enum _State { INACTIVE, ACTIVE, REPAIRING }

const COLOR_BASE       := Color(0.25, 0.25, 0.30)
const COLOR_DOME_INACT := Color(0.30, 0.30, 0.35)
const COLOR_DOME_ACT   := Color(0.75, 0.10, 0.05)
const COLOR_DOME_REP   := Color(0.60, 0.45, 0.05)
const COLOR_BARREL     := Color(0.20, 0.20, 0.25)

const BASE_SIZE    := 0.9
const DOME_RADIUS  := 0.35
const BARREL_LEN   := 0.6
const BARREL_RAD   := 0.08
const PRE_FIRE_WINDOW := 0.35

var _state: int = _State.INACTIVE
var _player_car: Node3D = null
var _rng := RandomNumberGenerator.new()

var _activation_pending: bool = false
var _cooldown: float = 0.0

var _shots_remaining_in_burst: int = 0
var _fire_cooldown: float = 0.0
var _burst_pause_timer: float = 0.0
var _in_burst_pause: bool = false
var _pre_fired: bool = false

var _area: Area3D = null

var _dome_pivot: Node3D = null
var _dome_mesh: MeshInstance3D = null
var _barrel_mount: Node3D = null
var _barrel_mesh: MeshInstance3D = null
var _dome_mat: StandardMaterial3D = null


func _ready() -> void:
	_rng.randomize()
	_build_visuals()
	_build_collision()
	_set_inactive_visuals(false)


func _build_visuals() -> void:
	var glb_scene := load("res://assets/models/enemies/roof_turret.glb") as PackedScene
	if glb_scene != null:
		var glb_root := glb_scene.instantiate() as Node3D
		var glb_base   := glb_root.find_child("Base")   as MeshInstance3D
		var glb_dome   := glb_root.find_child("Dome")   as MeshInstance3D
		var glb_barrel := glb_root.find_child("Barrel") as MeshInstance3D

		if glb_base != null and glb_dome != null and glb_barrel != null:
			_build_from_glb(glb_base, glb_dome, glb_barrel)
			glb_root.queue_free()
			return
		glb_root.queue_free()

	_build_procedural()


func _build_from_glb(glb_base: MeshInstance3D, glb_dome: MeshInstance3D, glb_barrel: MeshInstance3D) -> void:
	var base_mesh := MeshInstance3D.new()
	base_mesh.name = "BaseMesh"
	base_mesh.mesh = glb_base.mesh
	base_mesh.material_override = StandardMaterial3D.new()
	(base_mesh.material_override as StandardMaterial3D).albedo_color = COLOR_BASE
	add_child(base_mesh)

	_dome_pivot = Node3D.new()
	_dome_pivot.name = "DomePivot"
	add_child(_dome_pivot)

	_dome_mat = StandardMaterial3D.new()
	_dome_mat.albedo_color = COLOR_DOME_INACT
	_dome_mesh = MeshInstance3D.new()
	_dome_mesh.name = "DomeMesh"
	_dome_mesh.mesh = glb_dome.mesh
	_dome_mesh.material_override = _dome_mat
	_dome_pivot.add_child(_dome_mesh)

	_barrel_mount = Node3D.new()
	_barrel_mount.name = "BarrelMount"
	_barrel_mount.position = Vector3(0.0, DOME_RADIUS, 0.0)
	_dome_pivot.add_child(_barrel_mount)

	_barrel_mesh = MeshInstance3D.new()
	_barrel_mesh.name = "BarrelMesh"
	_barrel_mesh.mesh = glb_barrel.mesh
	_barrel_mesh.material_override = StandardMaterial3D.new()
	(_barrel_mesh.material_override as StandardMaterial3D).albedo_color = COLOR_BARREL
	_barrel_mount.add_child(_barrel_mesh)


func _build_procedural() -> void:
	# Base plate
	var base_inst := MeshInstance3D.new()
	base_inst.name = "BaseMesh"
	var base_box := BoxMesh.new()
	base_box.size = Vector3(BASE_SIZE, 0.15, BASE_SIZE)
	base_inst.mesh = base_box
	base_inst.material_override = StandardMaterial3D.new()
	(base_inst.material_override as StandardMaterial3D).albedo_color = COLOR_BASE
	add_child(base_inst)

	# Dome pivot (flipped 180° when inactive)
	_dome_pivot = Node3D.new()
	_dome_pivot.name = "DomePivot"
	_dome_pivot.position = Vector3(0.0, 0.07, 0.0)
	add_child(_dome_pivot)

	_dome_mat = StandardMaterial3D.new()
	_dome_mat.albedo_color = COLOR_DOME_INACT
	_dome_mesh = MeshInstance3D.new()
	_dome_mesh.name = "DomeMesh"
	var dome_sphere := SphereMesh.new()
	dome_sphere.radius = DOME_RADIUS
	dome_sphere.height = DOME_RADIUS * 2.0
	dome_sphere.radial_segments = 8
	dome_sphere.rings = 4
	_dome_mesh.mesh = dome_sphere
	_dome_mesh.material_override = _dome_mat
	_dome_pivot.add_child(_dome_mesh)

	_barrel_mount = Node3D.new()
	_barrel_mount.name = "BarrelMount"
	_barrel_mount.position = Vector3(0.0, DOME_RADIUS * 0.5, 0.0)
	_dome_pivot.add_child(_barrel_mount)

	_barrel_mesh = MeshInstance3D.new()
	_barrel_mesh.name = "BarrelMesh"
	var barrel_cyl := CylinderMesh.new()
	barrel_cyl.height = BARREL_LEN
	barrel_cyl.top_radius = BARREL_RAD
	barrel_cyl.bottom_radius = BARREL_RAD
	_barrel_mesh.mesh = barrel_cyl
	_barrel_mesh.material_override = StandardMaterial3D.new()
	(_barrel_mesh.material_override as StandardMaterial3D).albedo_color = COLOR_BARREL
	_barrel_mesh.position = Vector3(0.0, 0.0, -BARREL_LEN * 0.5)
	_barrel_mesh.rotation_degrees = Vector3(90.0, 0.0, 0.0)
	_barrel_mount.add_child(_barrel_mesh)


func _build_collision() -> void:
	_area = Area3D.new()
	_area.collision_layer = 32
	_area.collision_mask = 0
	_area.monitorable = false
	_area.monitoring = false
	_area.name = "Area3D"
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = Vector3(BASE_SIZE, BASE_SIZE, BASE_SIZE)
	col.shape = shape
	_area.add_child(col)
	add_child(_area)


func _set_inactive_visuals(animate: bool) -> void:
	_dome_mat.albedo_color = COLOR_DOME_INACT
	_dome_pivot.visible = true
	_barrel_mesh.visible = true

	if animate:
		var tw := create_tween()
		tw.tween_property(_dome_pivot, "rotation_degrees:x", 180.0, 0.4)\
			.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN_OUT)
		tw.parallel()\
			.tween_property(_barrel_mesh, "scale", Vector3.ZERO, 0.3)\
			.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	else:
		_dome_pivot.rotation_degrees = Vector3(180.0, 0.0, 0.0)
		_barrel_mesh.scale = Vector3.ZERO


func _set_active_visuals() -> void:
	_dome_mat.albedo_color = COLOR_DOME_ACT
	_dome_pivot.visible = true
	_barrel_mesh.visible = true

	var tw := create_tween()
	tw.tween_property(_dome_pivot, "rotation_degrees:x", 0.0, 0.5)\
		.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	tw.parallel()\
		.tween_property(_barrel_mesh, "scale", Vector3.ONE, 0.4)\
		.set_trans(Tween.TRANS_ELASTIC).set_ease(Tween.EASE_OUT)


func _set_repairing_visuals() -> void:
	_dome_pivot.visible = false
	VfxSpawner.spawn("turret_repair", global_position)


func set_player_car(player: Node3D) -> void:
	_player_car = player


func activate() -> void:
	if _state != _State.INACTIVE:
		return
	_activation_pending = true


func take_damage(_amount: float) -> void:
	if _state != _State.ACTIVE:
		return
	_enter_repairing()


func _process(delta: float) -> void:
	if _player_car == null:
		return

	match _state:
		_State.INACTIVE:  _process_inactive(delta)
		_State.ACTIVE:    _process_active(delta)
		_State.REPAIRING: _process_repairing(delta)


func _process_inactive(dt: float) -> void:
	_cooldown -= dt
	if not _activation_pending or _cooldown > 0.0:
		return

	var dist := global_position.distance_to(_player_car.global_position)
	if dist > GameConfig.roof_turret_max_range:
		_activation_pending = false
		return

	_enter_active()


func _process_active(dt: float) -> void:
	if not _player_car.is_inside_tree():
		_enter_inactive()
		return

	_track_player()

	if _in_burst_pause:
		_burst_pause_timer -= dt
		if _burst_pause_timer <= 0.0:
			_in_burst_pause = false
			_shots_remaining_in_burst = GameConfig.roof_turret_burst_count
			_fire_cooldown = 1.0 / GameConfig.roof_turret_fire_rate
			_pre_fired = false
		return

	_fire_cooldown -= dt

	if not _pre_fired and _fire_cooldown <= PRE_FIRE_WINDOW:
		_pre_fired = true
		VfxSpawner.spawn("turret_prefire", global_position)

	if _fire_cooldown > 0.0:
		return

	var is_flipping := _player_car.is_flipping_under if "is_flipping_under" in _player_car else false
	if is_flipping:
		_fire_cooldown = 0.2
		return

	_fire()
	_pre_fired = false
	_shots_remaining_in_burst -= 1

	if global_position.distance_to(_player_car.global_position) > GameConfig.roof_turret_max_range:
		_enter_inactive()
		return

	if _shots_remaining_in_burst <= 0:
		_in_burst_pause = true
		_burst_pause_timer = GameConfig.roof_turret_burst_interval
	else:
		_fire_cooldown = 1.0 / GameConfig.roof_turret_fire_rate


func _process_repairing(dt: float) -> void:
	_cooldown -= dt
	if _cooldown <= 0.0:
		_enter_inactive()


func _enter_active() -> void:
	_state = _State.ACTIVE
	_activation_pending = false
	_shots_remaining_in_burst = GameConfig.roof_turret_burst_count
	_fire_cooldown = 1.0 / GameConfig.roof_turret_fire_rate
	_in_burst_pause = false
	_pre_fired = false
	_area.set_deferred("monitorable", true)
	_set_active_visuals()
	SoundManager.play("turret_activate")
	print("[RoofTurret] %s activated." % name)


func _enter_inactive() -> void:
	_state = _State.INACTIVE
	_activation_pending = false
	_cooldown = GameConfig.roof_turret_reactivation_time
	_area.set_deferred("monitorable", false)
	_set_inactive_visuals(true)
	SoundManager.play("turret_deactivate")


func _enter_repairing() -> void:
	_state = _State.REPAIRING
	_cooldown = GameConfig.roof_turret_repair_time
	_area.set_deferred("monitorable", false)
	_dome_mat.albedo_color = COLOR_DOME_REP
	_set_repairing_visuals()
	SoundManager.play("turret_destroyed")
	print("[RoofTurret] %s damaged — repairing for %.1fs." % [name, _cooldown])


func _track_player() -> void:
	var dir := _player_car.global_position - global_position
	var flat := Vector3(dir.x, 0.0, dir.z)
	if flat.length_squared() > 0.01:
		rotation_degrees = Vector3(0.0, rad_to_deg(atan2(-flat.x, -flat.z)), 0.0)

	if _barrel_mount != null and dir.length_squared() > 0.01:
		var pitch_rad := atan2(-dir.y, flat.length())
		_barrel_mount.rotation_degrees = Vector3(rad_to_deg(pitch_rad), 0.0, 0.0)


func _fire() -> void:
	var is_hit := _rng.randf() < GameConfig.roof_turret_hit_chance
	var target_pos: Vector3

	if is_hit:
		target_pos = _player_car.global_position + Vector3(
			_rng.randf_range(-0.2, 0.2),
			_rng.randf_range(-0.2, 0.2),
			_rng.randf_range(-0.2, 0.2))
	else:
		var miss_x := _rng.randf_range(0.5, 1.0) * (1.0 if _rng.randf() > 0.5 else -1.0)
		var miss_y := _rng.randf_range(0.5, 1.0) * (1.0 if _rng.randf() > 0.5 else -1.0)
		target_pos = _player_car.global_position + Vector3(miss_x, miss_y, 0.0)

	# Muzzle position: tip of barrel in world space
	var muzzle_pos: Vector3
	if _barrel_mount != null:
		muzzle_pos = _barrel_mount.global_position + global_transform.basis.z * (-BARREL_LEN)
	else:
		muzzle_pos = global_position
	VfxSpawner.spawn("turret_muzzle", muzzle_pos)

	var bullet: Node3D = _DroneBulletScript.new()
	get_tree().root.add_child(bullet)
	bullet.global_position = muzzle_pos
	bullet.initialize(target_pos, is_hit, GameConfig.roof_turret_bullet_speed)
