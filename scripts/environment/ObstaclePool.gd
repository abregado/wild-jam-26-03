## Pooled cube obstacles per zone. Four instances sit under ObstacleSystem in Main.tscn.
##
## Streaming model — cubes spawn one at a time from ahead of the locomotive:
##   Streaming  — is_zone_active() true: a new cube is placed at spawnZ whenever the front
##                of the stream has moved far enough back.
##   Draining   — is_zone_active() false: no new spawns; in-stream cubes keep moving.
##
## Roof zone additionally spawns vertical support pillars on each side of the cube
## (at X = ±SupportX). Supports collide with bullets only (layer 16).
##
## Cliff zones create StaticBody3D colliders on layer 8 (128) for forward-ray detection.
## All zones create layer 9 (256) bodies for flip-arc intersection checks.

extends Node3D

enum ZoneType { LEFT_CLIFF, RIGHT_CLIFF, ROOF, PLATEAU }

@export var zone: int = ZoneType.LEFT_CLIFF

const SUPPORT_X      := 11.0
const SUPPORT_WIDTH  := 1.0
const SUPPORT_ROOF_Y := 13.0
const BULLET_LAYER   := 16
const CLIFF_BODY_WIDTH      := 10.0
const CLIFF_COLLISION_LAYER := 128
const FLIP_BODY_LAYER       := 256

const CLIFF_INNER_EDGE := 1.5 + 1.5 * 2.0  # = 4.5

const PALETTE_COLORS := [
	Color(0.55, 0.38, 0.20),
	Color(0.60, 0.42, 0.22),
	Color(0.48, 0.33, 0.18),
	Color(0.65, 0.47, 0.25),
]

var _cubes:         Array = []
var _cube_meshes:   Array = []
var _cube_in_stream: Array = []
var _materials:     Array = []

var _cliff_bodies:  Array = []
var _is_cliff_zone: bool = false

var _flip_bodies:   Array = []

var _support_left:         Array = []
var _support_right:        Array = []
var _support_left_bodies:  Array = []
var _support_right_bodies: Array = []
var _is_roof_zone: bool = false

var _first_spawn_done: bool = true
var _was_active_last_frame: bool = false

var _loco_z: float = 0.0
var _despawn_z: float = 0.0
var _initialized: bool = false

var _rng := RandomNumberGenerator.new()


func _ready() -> void:
	_rng.randomize()
	_is_cliff_zone = zone == ZoneType.LEFT_CLIFF or zone == ZoneType.RIGHT_CLIFF
	_is_roof_zone  = zone == ZoneType.ROOF

	for k in PALETTE_COLORS.size():
		var mat := StandardMaterial3D.new()
		mat.albedo_color = PALETTE_COLORS[k]
		_materials.append(mat)


func _initialize() -> void:
	var train_node := get_tree().root.find_child("Train", true, false)
	_loco_z = 100.0
	if train_node != null and "locomotive_z" in train_node:
		_loco_z = train_node.locomotive_z
	_despawn_z = -GameConfig.despawn_behind_distance

	var spawn_z    := _loco_z + GameConfig.spawn_ahead_distance
	var total_range := spawn_z - _despawn_z
	var computed   := ceili(total_range / GameConfig.obstacle_cube_spacing) + 2
	var pool_size  := maxi(GameConfig.obstacle_cube_pool_size, computed)

	_cubes.resize(pool_size)
	_cube_meshes.resize(pool_size)
	_cube_in_stream.resize(pool_size)
	_cube_in_stream.fill(false)

	if _is_cliff_zone:
		_cliff_bodies.resize(pool_size)
	_flip_bodies.resize(pool_size)

	if _is_roof_zone:
		_support_left.resize(pool_size)
		_support_right.resize(pool_size)
		_support_left_bodies.resize(pool_size)
		_support_right_bodies.resize(pool_size)

	var zone_width := _get_zone_width()
	var spacing := GameConfig.obstacle_cube_spacing

	for i in pool_size:
		var height := _get_random_height()
		var box_mesh := BoxMesh.new()
		box_mesh.size = Vector3(zone_width, height, spacing)
		box_mesh.material = _materials[_rng.randi_range(0, _materials.size() - 1)]
		_cube_meshes[i] = box_mesh

		var park_pos := Vector3(_get_zone_x(), _get_zone_y(height), spawn_z + (i + 1) * spacing)
		var inst := MeshInstance3D.new()
		inst.mesh = box_mesh
		inst.visible = false
		inst.position = park_pos
		add_child(inst)
		_cubes[i] = inst

		# Cliff forward-detection body (wide, layer 8 = 128)
		if _is_cliff_zone:
			var shape := BoxShape3D.new()
			shape.size = Vector3(CLIFF_BODY_WIDTH, height, spacing)
			var col := CollisionShape3D.new()
			col.shape = shape
			var body := StaticBody3D.new()
			body.collision_layer = 0
			body.collision_mask = 0
			body.position = park_pos
			body.add_child(col)
			add_child(body)
			_cliff_bodies[i] = body

		# Flip-path body (layer 9 = 256)
		var flip_shape := BoxShape3D.new()
		flip_shape.size = Vector3(zone_width, height, spacing)
		var flip_col := CollisionShape3D.new()
		flip_col.shape = flip_shape
		var flip_body := StaticBody3D.new()
		flip_body.collision_layer = 0
		flip_body.collision_mask = 0
		flip_body.position = park_pos
		flip_body.add_child(flip_col)
		add_child(flip_body)
		_flip_bodies[i] = flip_body

		# Roof vertical supports (bullet-only collision, layer 16)
		if _is_roof_zone:
			_support_left[i]  = _create_support(i, park_pos, -SUPPORT_X, spacing)
			_support_right[i] = _create_support(i, park_pos,  SUPPORT_X, spacing)
			_support_left_bodies[i]  = _create_support_body(park_pos, -SUPPORT_X, spacing)
			_support_right_bodies[i] = _create_support_body(park_pos,  SUPPORT_X, spacing)

	print("[ObstaclePool] zone %d: %d cubes, spawnZ=%.0f, despawnZ=%.0f" % [
		zone, pool_size, spawn_z, _despawn_z])
	_initialized = true


func _create_support(idx: int, park_pos: Vector3, x_pos: float, depth: float) -> MeshInstance3D:
	var support_height := SUPPORT_ROOF_Y
	var mat: StandardMaterial3D = _materials[idx % _materials.size()]
	var mesh := BoxMesh.new()
	mesh.size = Vector3(SUPPORT_WIDTH, support_height, depth)
	mesh.material = mat
	var inst := MeshInstance3D.new()
	inst.mesh = mesh
	inst.visible = false
	inst.position = Vector3(x_pos, support_height * 0.5, park_pos.z)
	add_child(inst)
	return inst


func _create_support_body(park_pos: Vector3, x_pos: float, depth: float) -> StaticBody3D:
	var support_height := SUPPORT_ROOF_Y
	var shape := BoxShape3D.new()
	shape.size = Vector3(SUPPORT_WIDTH, support_height, depth)
	var col := CollisionShape3D.new()
	col.shape = shape
	var body := StaticBody3D.new()
	body.collision_layer = 0
	body.collision_mask = 0
	body.position = Vector3(x_pos, support_height * 0.5, park_pos.z)
	body.add_child(col)
	add_child(body)
	return body


func _process(delta: float) -> void:
	if not _initialized:
		_initialize()
		return

	var streaming := _is_zone_active()
	var spacing := GameConfig.obstacle_cube_spacing
	var spawn_z := _loco_z + GameConfig.spawn_ahead_distance

	if streaming and not _was_active_last_frame:
		_first_spawn_done = false
	_was_active_last_frame = streaming

	# Move all in-stream cubes
	for i in _cubes.size():
		if not _cube_in_stream[i]:
			continue

		var pos: Vector3 = (_cubes[i] as MeshInstance3D).position
		pos.z -= TrainSpeedManager.current_train_speed * delta

		if pos.z < _despawn_z:
			_cube_in_stream[i] = false
			(_cubes[i] as MeshInstance3D).visible = false
			var park_pos := Vector3(_get_zone_x(), (_cubes[i] as MeshInstance3D).position.y, spawn_z + 100.0)
			(_cubes[i] as MeshInstance3D).position = park_pos

			if _is_cliff_zone:
				(_cliff_bodies[i] as StaticBody3D).collision_layer = 0
				(_cliff_bodies[i] as StaticBody3D).position = park_pos
			(_flip_bodies[i] as StaticBody3D).collision_layer = 0
			(_flip_bodies[i] as StaticBody3D).position = park_pos

			if _is_roof_zone:
				_park_supports(i, park_pos)
			continue

		(_cubes[i] as MeshInstance3D).position = pos

		if _is_cliff_zone:
			(_cliff_bodies[i] as StaticBody3D).position = pos
		(_flip_bodies[i] as StaticBody3D).position = pos

		if _is_roof_zone:
			_move_supports(i, pos)

	# While streaming: emit one cube from spawnZ whenever the front has a gap
	if streaming:
		var front_z := _get_front_stream_z()
		if front_z < spawn_z - spacing:
			var idx := _find_parked_cube()
			if idx >= 0:
				var height := _get_random_height()
				var is_first_spawn := not _first_spawn_done
				_first_spawn_done = true

				var is_phantom := is_first_spawn and \
					(zone == ZoneType.ROOF or zone == ZoneType.LEFT_CLIFF or zone == ZoneType.RIGHT_CLIFF)

				if is_first_spawn and zone == ZoneType.PLATEAU:
					height *= 0.5

				var depth := spacing
				var zone_width := _get_zone_width()
				var stream_pos := Vector3(_get_zone_x(), _get_zone_y(height), spawn_z)

				(_cubes[idx] as MeshInstance3D).position = stream_pos
				(_cubes[idx] as MeshInstance3D).visible = not is_phantom
				_cube_in_stream[idx] = true

				if not is_phantom:
					(_cube_meshes[idx] as BoxMesh).size = Vector3(zone_width, height, depth)
					(_cube_meshes[idx] as BoxMesh).material = _materials[_rng.randi_range(0, _materials.size() - 1)]

					if _is_cliff_zone:
						var cliff_body := _cliff_bodies[idx] as StaticBody3D
						var cs := cliff_body.get_child(0) as CollisionShape3D
						if cs != null and cs.shape is BoxShape3D:
							(cs.shape as BoxShape3D).size = Vector3(CLIFF_BODY_WIDTH, height, depth)
						cliff_body.position = stream_pos
						cliff_body.collision_layer = CLIFF_COLLISION_LAYER

					var flip_body := _flip_bodies[idx] as StaticBody3D
					var fcs := flip_body.get_child(0) as CollisionShape3D
					if fcs != null and fcs.shape is BoxShape3D:
						(fcs.shape as BoxShape3D).size = Vector3(zone_width, height, depth)
					flip_body.position = stream_pos
					flip_body.collision_layer = FLIP_BODY_LAYER

					if _is_roof_zone:
						_stream_supports(idx, stream_pos, depth)


func _move_supports(i: int, cube_pos: Vector3) -> void:
	var support_height := SUPPORT_ROOF_Y
	var left_pos  := Vector3(-SUPPORT_X, support_height * 0.5, cube_pos.z)
	var right_pos := Vector3( SUPPORT_X, support_height * 0.5, cube_pos.z)
	(_support_left[i]  as MeshInstance3D).position = left_pos
	(_support_right[i] as MeshInstance3D).position = right_pos
	(_support_left_bodies[i]  as StaticBody3D).position = left_pos
	(_support_right_bodies[i] as StaticBody3D).position = right_pos


func _park_supports(i: int, park_pos: Vector3) -> void:
	var support_height := SUPPORT_ROOF_Y
	var left_park  := Vector3(-SUPPORT_X, support_height * 0.5, park_pos.z)
	var right_park := Vector3( SUPPORT_X, support_height * 0.5, park_pos.z)

	(_support_left[i]  as MeshInstance3D).visible = false
	(_support_right[i] as MeshInstance3D).visible = false
	(_support_left[i]  as MeshInstance3D).position = left_park
	(_support_right[i] as MeshInstance3D).position = right_park

	(_support_left_bodies[i]  as StaticBody3D).collision_layer = 0
	(_support_right_bodies[i] as StaticBody3D).collision_layer = 0
	(_support_left_bodies[i]  as StaticBody3D).position = left_park
	(_support_right_bodies[i] as StaticBody3D).position = right_park


func _stream_supports(idx: int, stream_pos: Vector3, depth: float) -> void:
	var support_height := SUPPORT_ROOF_Y
	var left_pos  := Vector3(-SUPPORT_X, support_height * 0.5, stream_pos.z)
	var right_pos := Vector3( SUPPORT_X, support_height * 0.5, stream_pos.z)

	var lm := (_support_left[idx]  as MeshInstance3D).mesh as BoxMesh
	var rm := (_support_right[idx] as MeshInstance3D).mesh as BoxMesh
	if lm != null: lm.size = Vector3(SUPPORT_WIDTH, support_height, depth)
	if rm != null: rm.size = Vector3(SUPPORT_WIDTH, support_height, depth)

	(_support_left[idx]  as MeshInstance3D).visible = true
	(_support_right[idx] as MeshInstance3D).visible = true
	(_support_left[idx]  as MeshInstance3D).position = left_pos
	(_support_right[idx] as MeshInstance3D).position = right_pos

	var lcs := (_support_left_bodies[idx]  as StaticBody3D).get_child(0) as CollisionShape3D
	var rcs := (_support_right_bodies[idx] as StaticBody3D).get_child(0) as CollisionShape3D
	if lcs != null and lcs.shape is BoxShape3D:
		(lcs.shape as BoxShape3D).size = Vector3(SUPPORT_WIDTH, support_height, depth)
	if rcs != null and rcs.shape is BoxShape3D:
		(rcs.shape as BoxShape3D).size = Vector3(SUPPORT_WIDTH, support_height, depth)

	(_support_left_bodies[idx]  as StaticBody3D).position = left_pos
	(_support_right_bodies[idx] as StaticBody3D).position = right_pos
	(_support_left_bodies[idx]  as StaticBody3D).collision_layer = BULLET_LAYER
	(_support_right_bodies[idx] as StaticBody3D).collision_layer = BULLET_LAYER


func _is_zone_active() -> bool:
	match zone:
		ZoneType.LEFT_CLIFF:
			return ObstacleManager.active_cliff_side == ObstacleManager.CliffSide.LEFT \
				or (ObstacleManager.is_in_warning and ObstacleManager.upcoming_cliff_side == ObstacleManager.CliffSide.LEFT)
		ZoneType.RIGHT_CLIFF:
			return ObstacleManager.active_cliff_side == ObstacleManager.CliffSide.RIGHT \
				or (ObstacleManager.is_in_warning and ObstacleManager.upcoming_cliff_side == ObstacleManager.CliffSide.RIGHT)
		ZoneType.ROOF:
			return ObstacleManager.active_movement_limit == ObstacleManager.MovementLimit.ROOF \
				or (ObstacleManager.is_in_warning and ObstacleManager.upcoming_movement_limit == ObstacleManager.MovementLimit.ROOF)
		ZoneType.PLATEAU:
			return ObstacleManager.active_movement_limit == ObstacleManager.MovementLimit.PLATEAU \
				or (ObstacleManager.is_in_warning and ObstacleManager.upcoming_movement_limit == ObstacleManager.MovementLimit.PLATEAU)
		_:
			return false


func _get_front_stream_z() -> float:
	var max_z := -INF
	for i in _cubes.size():
		if _cube_in_stream[i] and (_cubes[i] as MeshInstance3D).position.z > max_z:
			max_z = (_cubes[i] as MeshInstance3D).position.z
	return max_z


func _find_parked_cube() -> int:
	for i in _cubes.size():
		if not _cube_in_stream[i]:
			return i
	return -1


func _get_zone_x() -> float:
	if zone == ZoneType.RIGHT_CLIFF: return  CLIFF_INNER_EDGE + GameConfig.cliff_cube_width * 0.5
	if zone == ZoneType.LEFT_CLIFF:  return -(CLIFF_INNER_EDGE + GameConfig.cliff_cube_width * 0.5)
	return 0.0


func _get_zone_y(height: float) -> float:
	match zone:
		ZoneType.LEFT_CLIFF, ZoneType.RIGHT_CLIFF: return height * 0.5
		ZoneType.ROOF:                             return 13.0
		ZoneType.PLATEAU:                          return height * 0.5
		_:                                         return 0.0


func _get_random_height() -> float:
	match zone:
		ZoneType.LEFT_CLIFF, ZoneType.RIGHT_CLIFF: return _rng.randf_range(8.0, 20.0)
		ZoneType.ROOF:                             return _rng.randf_range(3.0, 5.0)
		ZoneType.PLATEAU:                          return _rng.randf_range(3.0, 7.0)
		_:                                         return 5.0


func _get_zone_width() -> float:
	match zone:
		ZoneType.LEFT_CLIFF, ZoneType.RIGHT_CLIFF:
			return GameConfig.cliff_cube_width
		ZoneType.ROOF, ZoneType.PLATEAU:
			return (CLIFF_INNER_EDGE + GameConfig.cliff_cube_width) * 2.0
		_:
			return 3.0
