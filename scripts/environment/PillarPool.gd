## Maintains a pool of track-support pillars using object repositioning (no runtime instancing).
## Pillars move in -Z direction (backward) to simulate train moving forward (+Z).
##
## Spawn/despawn distances are config-driven (spawn_ahead_distance, despawn_behind_distance).
## Pool size is computed on first _process to cover the full train+ahead+behind range.
## Deferred init is needed because TrackEnvironment loads before Train in Main.tscn.

extends Node3D

const PILLAR_HEIGHT := 6.0
const TRACK_Y := 7.0
const PILLAR_X := 0.0

var _pillar_y: float = 0.0
var _pillars: Array = []          # Array of Node3D
var _pillar_scene: PackedScene = null
var _spacing: float = 0.0
var _x_spread: float = 0.0
var _despawn_z: float = 0.0
var _move_speed: float = 0.0
var _initialized: bool = false

const PILLAR_COLOR := Color(0.55, 0.55, 0.55)


func _ready() -> void:
	_spacing   = GameConfig.pillar_spacing
	_x_spread  = GameConfig.pillar_x_spread
	_despawn_z = -GameConfig.despawn_behind_distance
	_pillar_y  = TRACK_Y + GameConfig.pillar_y_offset
	_pillar_scene = load("res://assets/models/environment/pillar.glb") as PackedScene


func _initialize() -> void:
	var train_node := get_tree().root.find_child("Train", true, false)
	var loco_z := 100.0
	if train_node != null and "locomotive_z" in train_node:
		loco_z = train_node.locomotive_z

	var spawn_z := loco_z + GameConfig.spawn_ahead_distance
	var total_range := spawn_z - _despawn_z
	var pool_count := ceili(total_range / _spacing) + 1

	for i in pool_count:
		var pillar: Node3D
		if _pillar_scene != null:
			pillar = _create_pillar_from_glb(_pillar_scene)
		else:
			pillar = _create_pillar_procedural()

		pillar.position = Vector3(randf_range(-_x_spread, _x_spread), _pillar_y, spawn_z - i * _spacing)
		add_child(pillar)
		_pillars.append(pillar)

	print("[PillarPool] Initialized %d pillars. spawnZ=%.0f, despawnZ=%.0f" % [pool_count, spawn_z, _despawn_z])
	_initialized = true


func _create_pillar_from_glb(scene: PackedScene) -> Node3D:
	var pillar := scene.instantiate() as Node3D
	for child in pillar.get_children():
		if child is StaticBody3D:
			(child as StaticBody3D).collision_layer = 1
			(child as StaticBody3D).collision_mask = 0
		if child is MeshInstance3D:
			var mat := StandardMaterial3D.new()
			mat.albedo_color = PILLAR_COLOR
			(child as MeshInstance3D).material_override = mat
	return pillar


func _create_pillar_procedural() -> Node3D:
	var mat := StandardMaterial3D.new()
	mat.albedo_color = PILLAR_COLOR

	var mesh := MeshInstance3D.new()
	var cyl := CylinderMesh.new()
	cyl.height = PILLAR_HEIGHT
	cyl.top_radius = 0.35
	cyl.bottom_radius = 0.45
	mesh.mesh = cyl
	mesh.material_override = mat

	var body := StaticBody3D.new()
	body.collision_layer = 1
	body.collision_mask = 0
	var col := CollisionShape3D.new()
	var shape := CylinderShape3D.new()
	shape.radius = 0.45
	shape.height = PILLAR_HEIGHT
	col.shape = shape
	body.add_child(col)
	mesh.add_child(body)
	return mesh


## Returns true if any pillar's world Z is within half_range of world_z.
func has_pillar_near_z(world_z: float, half_range: float) -> bool:
	for p in _pillars:
		if p != null and absf((p as Node3D).global_position.z - world_z) < half_range:
			return true
	return false


func set_move_speed(speed: float) -> void:
	_move_speed = speed


func _process(delta: float) -> void:
	if not _initialized:
		_initialize()
		return
	if _move_speed <= 0.0:
		return

	var move := _move_speed * delta

	for p in _pillars:
		(p as Node3D).position -= Vector3(0.0, 0.0, move)

	for p in _pillars:
		var pillar := p as Node3D
		if pillar.position.z < _despawn_z:
			var max_z := -INF
			for other in _pillars:
				if (other as Node3D).position.z > max_z:
					max_z = (other as Node3D).position.z
			pillar.position = Vector3(
				randf_range(-_x_spread, _x_spread),
				pillar.position.y,
				max_z + _spacing
			)
