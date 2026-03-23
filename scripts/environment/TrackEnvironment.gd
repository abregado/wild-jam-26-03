## Manages the scrolling ground, track, pillar pool, and all decorative pools
## (clouds, rock pillars, rubble).
## Polls TrainSpeedManager.current_train_speed each frame.
##
## Ground shader parameter: "scroll_speed" (float, units per second)
## Track shader parameter:  "scroll_speed" (float, units per second)
##
## Ground plane is resized on the first frame that the Train node is available:
##   Z : −2×DespawnBehindDistance  …  LocomotiveZ + 2×SpawnAheadDistance  (×1.4 padding)
##   X : ±2×RockPillarDistance                                              (×1.4 padding)

extends Node3D

@export var ground_mesh_path: NodePath = ^"GroundPlane"
@export var track_mesh_path: NodePath = ^"TrackMesh"

var _ground_mesh: MeshInstance3D = null
var _track_mesh: MeshInstance3D = null
var _ground_material: ShaderMaterial = null
var _track_material: ShaderMaterial = null

var _pillar_pool: Node3D = null
var _cloud_pool: Node3D = null
var _rock_pillar_pool: Node3D = null
var _rubble_pool: Node3D = null
var _ground_body: StaticBody3D = null
var _ground_resized: bool = false


func _ready() -> void:
	_ground_mesh = get_node(ground_mesh_path) as MeshInstance3D
	_track_mesh  = get_node(track_mesh_path) as MeshInstance3D
	_ground_material = _ground_mesh.get_active_material(0) as ShaderMaterial
	_track_material  = _track_mesh.get_active_material(0) as ShaderMaterial

	_pillar_pool     = get_node("PillarPool")
	_cloud_pool      = get_node("CloudPool")
	_rock_pillar_pool = get_node("RockPillarPool")
	_rubble_pool     = get_node("RubblePool")
	_ground_body     = get_node("GroundBody") as StaticBody3D

	if _ground_material != null:
		_ground_material.set_shader_parameter("base_color",   Color(0.58, 0.41, 0.21))
		_ground_material.set_shader_parameter("detail_color", Color(0.44, 0.30, 0.15))


func _resize_ground() -> void:
	var train_node := get_tree().root.find_child("Train", true, false)
	var loco_z := 100.0
	if train_node != null and "locomotive_z" in train_node:
		loco_z = train_node.locomotive_z

	var total_len  := loco_z + 2.0 * GameConfig.spawn_ahead_distance + 2.0 * GameConfig.despawn_behind_distance
	var total_wide := 4.0 * GameConfig.rock_pillar_distance

	var center_z := (loco_z + 2.0 * GameConfig.spawn_ahead_distance - 2.0 * GameConfig.despawn_behind_distance) / 2.0

	var pad_len  := total_len  * 1.4
	var pad_wide := total_wide * 1.4

	if _ground_mesh.mesh is PlaneMesh:
		(_ground_mesh.mesh as PlaneMesh).size = Vector2(pad_wide, pad_len)
	_ground_mesh.position = Vector3(0.0, 0.0, center_z)

	_ground_body.position = Vector3(0.0, -0.25, center_z)
	var ground_col := _ground_body.get_node_or_null("GroundCollision") as CollisionShape3D
	if ground_col != null and ground_col.shape is BoxShape3D:
		(ground_col.shape as BoxShape3D).size = Vector3(pad_wide, 0.5, pad_len)

	print("[TrackEnvironment] Ground resized: %.0f × %.0f, centreZ=%.0f" % [pad_wide, pad_len, center_z])
	_ground_resized = true


func set_scroll_speed(units_per_second: float) -> void:
	if _ground_material != null:
		_ground_material.set_shader_parameter("scroll_speed", units_per_second)
	if _track_material != null:
		_track_material.set_shader_parameter("scroll_speed", units_per_second)
	if _pillar_pool != null and _pillar_pool.has_method("set_move_speed"):
		_pillar_pool.set_move_speed(units_per_second)
	if _rock_pillar_pool != null and _rock_pillar_pool.has_method("set_move_speed"):
		_rock_pillar_pool.set_move_speed(units_per_second)
	if _rubble_pool != null and _rubble_pool.has_method("set_move_speed"):
		_rubble_pool.set_move_speed(units_per_second)
	if _cloud_pool != null and _cloud_pool.has_method("set_train_speed"):
		_cloud_pool.set_train_speed(units_per_second)


func _process(_delta: float) -> void:
	set_scroll_speed(TrainSpeedManager.current_train_speed)

	if not _ground_resized and get_tree().root.find_child("Train", true, false) != null:
		_resize_ground()
