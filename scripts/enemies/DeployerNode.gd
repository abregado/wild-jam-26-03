## Sits on top of a carriage. Spawns drones once activated (when nearby containers/clamps
## are first damaged). Maintains up to max_drones_per_deployer active drones on a cooldown.
##
## When a drone is destroyed: cooldown resets to deployer_cooldown before respawning.
## Parented to Carriage, created by TrainBuilder.

class_name DeployerNode
extends Node3D

const _DroneNodeScript = preload("res://scripts/enemies/DroneNode.gd")

const DEPLOYER_COLOR := Color(0.35, 0.15, 0.15)

var _is_active: bool = false
var _spawn_cooldown: float = 0.0
var _living_drones: int = 0
var _player_car: Node3D = null


func _ready() -> void:
	var rng := RandomNumberGenerator.new()
	rng.randomize()
	_build_visual()
	_build_collision()


func _build_visual() -> void:
	var mesh_inst := MeshInstance3D.new()
	mesh_inst.name = "MeshSlot"

	var glb_mesh := _try_load_glb_mesh("res://assets/models/enemies/deployer.glb")
	if glb_mesh != null:
		mesh_inst.mesh = glb_mesh
	else:
		var box := BoxMesh.new()
		box.size = Vector3(1.2, 0.4, 0.8)
		mesh_inst.mesh = box

	mesh_inst.material_override = StandardMaterial3D.new()
	(mesh_inst.material_override as StandardMaterial3D).albedo_color = DEPLOYER_COLOR
	add_child(mesh_inst)


func _build_collision() -> void:
	# Layer 1 (world/train) — bullets stop here, no damage
	var body := StaticBody3D.new()
	body.collision_layer = 1
	body.collision_mask = 0
	body.name = "Body"
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = Vector3(1.2, 0.4, 0.8)
	col.shape = shape
	body.add_child(col)
	add_child(body)


func set_player_car(player: Node3D) -> void:
	_player_car = player


## Connected to ContainerNode.damage_taken and ClampNode.damage_taken by TrainBuilder.
func activate() -> void:
	if _is_active:
		return
	_is_active = true
	_spawn_cooldown = 0.5  # small delay before first drone
	print("[DeployerNode] %s activated." % name)


func on_drone_destroyed() -> void:
	_living_drones = maxi(0, _living_drones - 1)
	_spawn_cooldown = GameConfig.deployer_cooldown


## Called when a drone returns voluntarily. Shorter cooldown than a destroyed drone.
func on_drone_returned() -> void:
	_living_drones = maxi(0, _living_drones - 1)
	_spawn_cooldown = minf(_spawn_cooldown, GameConfig.deployer_cooldown * 0.5)


func _process(delta: float) -> void:
	if not _is_active or _player_car == null:
		return

	_spawn_cooldown -= delta
	if _spawn_cooldown <= 0.0 and _living_drones < GameConfig.max_drones_per_deployer:
		var dist := global_position.distance_to(_player_car.global_position)
		if dist > GameConfig.drone_max_deployer_distance:
			_is_active = false
			print("[DeployerNode] %s deactivated — player out of range (%.1f > %.1f)." % [
				name, dist, GameConfig.drone_max_deployer_distance])
			return
		_spawn_drone()


func _spawn_drone() -> void:
	if ObstacleManager.active_movement_limit == ObstacleManager.MovementLimit.ROOF:
		_spawn_cooldown = 1.0  # retry after a short delay
		return

	_spawn_cooldown = GameConfig.deployer_cooldown
	_living_drones += 1

	var drone: Node3D = _DroneNodeScript.new()
	get_tree().root.add_child(drone)
	drone.global_position = global_position
	drone.initialize(self, _player_car)


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
