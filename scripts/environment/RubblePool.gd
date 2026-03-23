## Small scattered ground-debris cubes in the canyon obstacle palette.
## They move at full train speed; their rapid scroll past the player reinforces the sense of
## velocity. Rubble sits at Y = halfHeight (bottom face flush with ground at Y=0), randomly
## tilted, and respawns ahead when it passes the despawn threshold.
## No collision — purely decorative.

extends Node3D

var _rubble: Array = []   # Array of MeshInstance3D
var _move_speed: float = 0.0
var _despawn_z: float = 0.0
var _spawn_z: float = 0.0
var _initialized: bool = false

# Desert brown palette matching ObstaclePool
const PALETTE_COLORS := [
	Color(0.55, 0.38, 0.20),
	Color(0.60, 0.42, 0.22),
	Color(0.48, 0.33, 0.18),
	Color(0.65, 0.47, 0.25),
	Color(0.40, 0.27, 0.13),
	Color(0.70, 0.52, 0.28),
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

	_despawn_z = -GameConfig.despawn_behind_distance
	_spawn_z   = loco_z + GameConfig.spawn_ahead_distance

	var count: int = GameConfig.rubble_pool_size
	var total_range := _spawn_z - _despawn_z

	for i in count:
		var sz := _make_rubble_size()
		var box := BoxMesh.new()
		box.size = sz
		box.material = _materials[randi() % _materials.size()]

		var inst := MeshInstance3D.new()
		inst.mesh = box
		inst.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		inst.position = Vector3(
			_random_rubble_x(),
			sz.y * 0.5,
			_despawn_z + randf_range(0.0, total_range)
		)
		inst.rotation_degrees = Vector3(
			randf_range(-25.0, 25.0),
			randf_range(0.0, 360.0),
			randf_range(-25.0, 25.0)
		)
		add_child(inst)
		_rubble.append(inst)

	print("[RubblePool] Initialized %d rubble pieces. spawnZ=%.0f, despawnZ=%.0f" % [count, _spawn_z, _despawn_z])
	_initialized = true


## Returns a Vector3 with random rubble dimensions.
func _make_rubble_size() -> Vector3:
	var b := randf_range(GameConfig.rubble_size_min, GameConfig.rubble_size_max)
	return Vector3(
		b * randf_range(0.6, 1.5),
		b * randf_range(0.5, 1.0),
		b * randf_range(0.6, 1.5)
	)


func _random_rubble_x() -> float:
	var spread: float = GameConfig.rubble_spread
	# Avoid ±2 unit band directly under the elevated track
	var x := randf_range(-spread, spread)
	if absf(x) < 2.0:
		x = (1.0 if x >= 0.0 else -1.0) * randf_range(2.0, spread)
	return x


func set_move_speed(speed: float) -> void:
	_move_speed = speed


func _process(delta: float) -> void:
	if not _initialized:
		_initialize()
		return
	if _move_speed <= 0.0:
		return

	var move := _move_speed * delta

	for inst in _rubble:
		var pos: Vector3 = inst.position
		pos.z -= move

		if pos.z < _despawn_z:
			var sz := _make_rubble_size()
			(inst.mesh as BoxMesh).size = sz
			(inst.mesh as BoxMesh).material = _materials[randi() % _materials.size()]
			inst.rotation_degrees = Vector3(
				randf_range(-25.0, 25.0),
				randf_range(0.0, 360.0),
				randf_range(-25.0, 25.0)
			)
			pos = Vector3(
				_random_rubble_x(),
				sz.y * 0.5,
				_spawn_z + randf_range(0.0, 10.0)
			)

		inst.position = pos
