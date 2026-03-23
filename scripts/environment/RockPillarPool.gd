## Distant rock-pillar formations on both sides of the track, giving the desert canyon a backdrop.
## Each side (left / right) is an independent pool of tall boulder-like boxes in the obstacle
## palette colours. They move at full train speed and recycle at the despawn distance.
## The two sides are initialised with a half-spacing stagger so they don't mirror each other.
## No collision — purely decorative.

extends Node3D

var _left: Array = []   # Array of MeshInstance3D
var _right: Array = []  # Array of MeshInstance3D

var _spawn_z: float = 0.0
var _despawn_z: float = 0.0
var _move_speed: float = 0.0
var _initialized: bool = false

# Desert brown palette matching ObstaclePool
const PALETTE_COLORS := [
	Color(0.55, 0.38, 0.20),
	Color(0.60, 0.42, 0.22),
	Color(0.48, 0.33, 0.18),
	Color(0.65, 0.47, 0.25),
	Color(0.42, 0.28, 0.14),
]

var _materials: Array = []  # Array of StandardMaterial3D


func _ready() -> void:
	for col in PALETTE_COLORS:
		var mat := StandardMaterial3D.new()
		mat.albedo_color = col
		_materials.append(mat)


func _initialize() -> void:
	var train_node := get_tree().root.find_child("Train", true, false)
	var loco_z := 100.0
	if train_node != null and "locomotive_z" in train_node:
		loco_z = train_node.locomotive_z

	var spacing: float = GameConfig.rock_pillar_spacing
	_despawn_z = -GameConfig.despawn_behind_distance
	_spawn_z   = loco_z + GameConfig.spawn_ahead_distance

	var total_range := _spawn_z - _despawn_z
	var count := ceili(total_range / spacing) + 1
	var dist: float = GameConfig.rock_pillar_distance

	for i in count:
		var z_l := _spawn_z - i * spacing
		var z_r := _spawn_z - i * spacing - spacing * 0.5  # stagger right side
		_left.append(_create_rock(-(dist + randf_range(-4.0, 4.0)), z_l))
		_right.append(_create_rock( (dist + randf_range(-4.0, 4.0)), z_r))

	print("[RockPillarPool] Initialized %d×2 rocks. spawnZ=%.0f, despawnZ=%.0f" % [count, _spawn_z, _despawn_z])
	_initialized = true


func _create_rock(x: float, z: float) -> MeshInstance3D:
	var h := randf_range(GameConfig.rock_pillar_height_min, GameConfig.rock_pillar_height_max)
	var w := randf_range(3.0, 8.0)
	var d := randf_range(3.0, 10.0)

	var box := BoxMesh.new()
	box.size = Vector3(w, h, d)
	box.material = _materials[randi() % _materials.size()]

	var inst := MeshInstance3D.new()
	inst.mesh = box
	inst.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	inst.position = Vector3(x, h * 0.5, z)
	add_child(inst)
	return inst


func _recycle_rock(rock: MeshInstance3D, side: float, max_z: float) -> void:
	var dist: float = GameConfig.rock_pillar_distance
	var h := randf_range(GameConfig.rock_pillar_height_min, GameConfig.rock_pillar_height_max)
	var w := randf_range(3.0, 8.0)
	var d := randf_range(3.0, 10.0)

	(rock.mesh as BoxMesh).size = Vector3(w, h, d)
	(rock.mesh as BoxMesh).material = _materials[randi() % _materials.size()]
	rock.position = Vector3(
		side * (dist + randf_range(-4.0, 4.0)),
		h * 0.5,
		max_z + GameConfig.rock_pillar_spacing
	)


func set_move_speed(speed: float) -> void:
	_move_speed = speed


func _process(delta: float) -> void:
	if not _initialized:
		_initialize()
		return
	if _move_speed <= 0.0:
		return

	var move := _move_speed * delta
	_move_and_recycle(_left,  -1.0, move)
	_move_and_recycle(_right,  1.0, move)


func _move_and_recycle(pool: Array, side: float, move: float) -> void:
	for inst in pool:
		var pos: Vector3 = inst.position
		pos.z -= move

		if pos.z < _despawn_z:
			var max_z := -INF
			for r in pool:
				if (r as MeshInstance3D).position.z > max_z:
					max_z = (r as MeshInstance3D).position.z
			_recycle_rock(inst, side, max_z)
			continue

		inst.position = pos
