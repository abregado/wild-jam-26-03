## Pool of transparent white box "clouds" floating at varying sky heights.
## Moves at cloud_parallax_factor × train speed — slower than the ground, giving depth.
## Clouds despawn behind the player and respawn ahead with randomised sizes and positions.
## No collision — purely decorative.

extends Node3D

var _clouds: Array = []   # Array of MeshInstance3D
var _move_speed: float = 0.0
var _despawn_z: float = 0.0
var _spawn_z: float = 0.0
var _initialized: bool = false
var _cloud_mat: StandardMaterial3D = null


func _ready() -> void:
	_cloud_mat = StandardMaterial3D.new()
	_cloud_mat.albedo_color = Color(1.0, 1.0, 1.0, 0.25)
	_cloud_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_cloud_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_cloud_mat.cull_mode = BaseMaterial3D.CULL_DISABLED


func _initialize() -> void:
	var train_node := get_tree().root.find_child("Train", true, false)
	var loco_z := 100.0
	if train_node != null and train_node.has_method("get") and "locomotive_z" in train_node:
		loco_z = train_node.locomotive_z

	_despawn_z = -GameConfig.despawn_behind_distance
	_spawn_z   = loco_z + GameConfig.spawn_ahead_distance

	var count: int = GameConfig.cloud_pool_size
	var total_range := _spawn_z - _despawn_z

	for i in count:
		var start_z := _despawn_z + randf_range(0.0, total_range)
		var box := BoxMesh.new()
		box.size = _random_cloud_size()
		box.material = _cloud_mat

		var inst := MeshInstance3D.new()
		inst.mesh = box
		inst.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		inst.position = _random_cloud_pos(start_z)
		add_child(inst)
		_clouds.append(inst)

	print("[CloudPool] Initialized %d clouds. spawnZ=%.0f, despawnZ=%.0f" % [count, _spawn_z, _despawn_z])
	_initialized = true


func _random_cloud_size() -> Vector3:
	var b := randf_range(GameConfig.cloud_size_min, GameConfig.cloud_size_max)
	return Vector3(
		b * randf_range(1.5, 3.0),
		b * randf_range(0.3, 0.7),
		b * randf_range(1.0, 2.0)
	)


func _random_cloud_pos(z: float) -> Vector3:
	return Vector3(
		randf_range(-GameConfig.cloud_spawn_spread, GameConfig.cloud_spawn_spread),
		randf_range(GameConfig.cloud_height_min, GameConfig.cloud_height_max),
		z
	)


func set_train_speed(speed: float) -> void:
	_move_speed = speed


func _process(delta: float) -> void:
	if not _initialized:
		_initialize()
		return

	var cloud_speed := _move_speed * GameConfig.cloud_parallax_factor
	if cloud_speed <= 0.0:
		return

	var move := cloud_speed * delta

	for inst in _clouds:
		var pos: Vector3 = inst.position
		pos.z -= move

		if pos.z < _despawn_z:
			# Find furthest-forward cloud
			var max_z := _spawn_z
			for c in _clouds:
				if (c as MeshInstance3D).position.z > max_z:
					max_z = (c as MeshInstance3D).position.z

			(inst.mesh as BoxMesh).size = _random_cloud_size()
			pos = _random_cloud_pos(max_z + randf_range(5.0, 20.0))

		inst.position = pos
