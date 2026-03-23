## Builds the train at runtime from config values.
## Attach to a Node3D called "Train" in Main.tscn.
##
## Train layout (Z axis, positive Z = forward/front of train):
##   Locomotive at highest Z.
##   Carriages extend in -Z direction.
##   Caboose at lowest Z.
##
## Carriage spacing: 12 units. Locomotive/Caboose length: 8 units. Carriage length: 10 units.
## Container attachment: 1–3 containers per carriage side (randomised per carriage).
##
## Exposes locomotive_z (world Z of locomotive front) for LevelManager.
## Exposes all_containers list for LevelManager range checks.

class_name TrainBuilder
extends Node3D

const CARRIAGE_LENGTH := 12.0
const LOCO_LENGTH     := 10.0
const CABOOSE_LENGTH  :=  8.0
const CAR_GAP         :=  0.5
const CARRIAGE_WIDTH  :=  3.0
const CARRIAGE_HEIGHT :=  2.5
const TRACK_Y         :=  7.0
const CARRIAGE_Y      := TRACK_Y + CARRIAGE_HEIGHT / 2.0

const CONTAINER_WIDTH  := 2.0
const CONTAINER_HEIGHT := 2.0
const CONTAINER_DEPTH  := 3.0
const CONTAINER_X_OFFSET := CARRIAGE_WIDTH / 2.0 + CONTAINER_WIDTH / 2.0 + 0.1

const DEPLOYER_HEIGHT := 0.4

var locomotive_z: float = 0.0
var caboose_z: float = 0.0
var all_containers: Array = []   # Array of ContainerNode
var all_deployers: Array = []    # Array of DeployerNode
var all_roof_turrets: Array = [] # Array of RoofTurretNode
var caboose_node: Node3D = null

var _carriages: Array = []       # Array of Carriage nodes
var _is_first_build: bool = true

var _carriage_scene: PackedScene = null
var _container_scene: PackedScene = null
var _clamp_scene: PackedScene = null


func _ready() -> void:
	_carriage_scene = load("res://scenes/train/Carriage.tscn") as PackedScene
	_container_scene = load("res://scenes/train/Container.tscn") as PackedScene
	_clamp_scene = load("res://scenes/train/Clamp.tscn") as PackedScene
	_build_train()


func rebuild() -> void:
	_build_train()


func _build_train() -> void:
	for child in get_children():
		child.queue_free()
	all_containers.clear()
	all_deployers.clear()
	all_roof_turrets.clear()
	_carriages.clear()

	var rng := RandomNumberGenerator.new()
	rng.randomize()

	var num_carriages := rng.randi_range(GameConfig.min_carriages, GameConfig.max_carriages)
	var current_z := 0.0
	caboose_z = current_z

	# --- Caboose ---
	var caboose := _create_box_car("Caboose",
		Vector3(CARRIAGE_WIDTH, CARRIAGE_HEIGHT, CABOOSE_LENGTH),
		Color(0.4, 0.2, 0.1),
		"res://assets/models/train/caboose.glb")
	caboose.position = Vector3(0.0, CARRIAGE_Y, current_z + CABOOSE_LENGTH / 2.0)
	add_child(caboose)
	caboose_node = caboose
	current_z += CABOOSE_LENGTH + CAR_GAP

	# --- Carriages ---
	for i in num_carriages:
		var carriage = _carriage_scene.instantiate()
		carriage.name = "Carriage%d" % i

		var slots_this_carriage := rng.randi_range(
			GameConfig.min_containers_per_carriage, GameConfig.max_containers_per_carriage)
		carriage.set_slot_count(slots_this_carriage)
		var body_len: float = carriage.body_length

		var z_center := current_z + body_len / 2.0
		carriage.position = Vector3(0.0, CARRIAGE_Y, z_center)
		add_child(carriage)
		_carriages.append(carriage)

		_attach_containers(carriage, slots_this_carriage, rng)

		var num_slots := rng.randi_range(1, GameConfig.max_deployers_per_carriage)
		_attach_roof_slots(carriage, num_slots, rng, body_len)

		current_z += body_len + CAR_GAP

	# --- Locomotive ---
	var loco := _create_box_car("Locomotive",
		Vector3(CARRIAGE_WIDTH, CARRIAGE_HEIGHT + 0.5, LOCO_LENGTH),
		Color(0.6, 0.1, 0.1),
		"res://assets/models/train/locomotive.glb")
	loco.position = Vector3(0.0, CARRIAGE_Y + 0.25, current_z + LOCO_LENGTH / 2.0)
	add_child(loco)
	locomotive_z = current_z + LOCO_LENGTH

	var player_car := get_tree().root.find_child("PlayerCar", true, false) as Node3D

	if _is_first_build:
		var has_deployer := false
		var has_turret := false
		for c in _carriages:
			if c.deployers.size() > 0: has_deployer = true
			if c.roof_turrets.size() > 0: has_turret = true

		var deployer_local_y := CARRIAGE_HEIGHT / 2.0 + DEPLOYER_HEIGHT / 2.0
		var turret_local_y   := CARRIAGE_HEIGHT / 2.0 + 0.075

		if not has_deployer and _carriages.size() > 0:
			var target = _carriages[_carriages.size() / 2]
			var d := DeployerNode.new()
			d.name = "Deployer_%s_forced" % target.name
			d.position = Vector3(0.0, deployer_local_y, 0.0)
			target.add_child(d)
			target.deployers.append(d)

		if not has_turret and _carriages.size() > 0:
			var idx := (_carriages.size() / 2 + 1) % _carriages.size() if _carriages.size() > 1 else 0
			var target = _carriages[idx]
			var t := RoofTurretNode.new()
			t.name = "RoofTurret_%s_forced" % target.name
			t.position = Vector3(0.0, turret_local_y, target.body_length * 0.25)
			target.add_child(t)
			target.roof_turrets.append(t)

		_is_first_build = false

	for carriage in _carriages:
		for deployer in carriage.deployers:
			deployer.set_player_car(player_car)
		for turret in carriage.roof_turrets:
			turret.set_player_car(player_car)

		all_deployers.append_array(carriage.deployers)
		all_roof_turrets.append_array(carriage.roof_turrets)

	_wire_deployer_activation()

	print("[TrainBuilder] Built %d carriages. locomotive_z=%.1f, containers=%d" % [
		num_carriages, locomotive_z, all_containers.size()])


func _wire_deployer_activation() -> void:
	for i in _carriages.size():
		var has_roof_enemies := _carriages[i].deployers.size() > 0 or _carriages[i].roof_turrets.size() > 0
		if not has_roof_enemies:
			continue

		for j in range(maxi(0, i - 1), mini(_carriages.size(), i + 2)):
			for carriage_child in _carriages[j].get_children():
				if not (carriage_child is ContainerNode):
					continue
				var container := carriage_child as ContainerNode

				for deployer in _carriages[i].deployers:
					container.damage_taken.connect(deployer.activate)
					for container_child in container.get_children():
						if container_child is ClampNode:
							(container_child as ClampNode).damage_taken.connect(deployer.activate)

				for turret in _carriages[i].roof_turrets:
					container.damage_taken.connect(turret.activate)
					for container_child in container.get_children():
						if container_child is ClampNode:
							(container_child as ClampNode).damage_taken.connect(turret.activate)


func _pick_clamp_setup(rng: RandomNumberGenerator) -> int:
	var total := GameConfig.clamp_setup_weight_single + GameConfig.clamp_setup_weight_double \
		+ GameConfig.clamp_setup_weight_triple + GameConfig.clamp_setup_weight_four
	var roll := rng.randf() * total
	if roll < GameConfig.clamp_setup_weight_single:
		return ClampNode.ClampSetup.SINGLE
	roll -= GameConfig.clamp_setup_weight_single
	if roll < GameConfig.clamp_setup_weight_double:
		return ClampNode.ClampSetup.DOUBLE
	roll -= GameConfig.clamp_setup_weight_double
	if roll < GameConfig.clamp_setup_weight_triple:
		return ClampNode.ClampSetup.TRIPLE
	return ClampNode.ClampSetup.FOUR


func _attach_containers(carriage: Node3D, slots_per_side: int, rng: RandomNumberGenerator) -> void:
	var spacing := CONTAINER_DEPTH + 0.3
	var start_z := -(slots_per_side - 1) * spacing / 2.0

	var setup := _pick_clamp_setup(rng)

	for side in [1, -1]:
		var is_right_side := side > 0
		var x_pos := side * CONTAINER_X_OFFSET
		for i in slots_per_side:
			var container: ContainerNode = _container_scene.instantiate() as ContainerNode
			container.name = "Container_%s_%s_%d" % [carriage.name, "R" if is_right_side else "L", i]
			container.position = Vector3(x_pos, 0.0, start_z + i * spacing)
			carriage.add_child(container)

			var cargo_index := rng.randi_range(0, GameConfig.cargo_types.size() - 1)
			container.set_cargo_type(GameConfig.cargo_types[cargo_index])

			container.cargo_detached.connect(GameSession.on_cargo_detached)
			container.cargo_detached.connect(func(_n, _b): TrainSpeedManager.on_container_detached())
			container.container_destroyed.connect(GameSession.on_container_destroyed)
			container.container_destroyed.connect(TrainSpeedManager.on_container_destroyed)

			all_containers.append(container)
			_attach_clamps_for_setup(container, is_right_side, setup, rng)


func _attach_roof_slots(carriage: Node3D, count: int, rng: RandomNumberGenerator, body_len: float) -> void:
	var spacing := body_len / (count + 1)
	var deployer_y := CARRIAGE_HEIGHT / 2.0 + DEPLOYER_HEIGHT / 2.0
	var turret_y   := CARRIAGE_HEIGHT / 2.0 + 0.075

	for i in count:
		var z_offset := -body_len / 2.0 + spacing * (i + 1)
		if rng.randf() < 0.5:
			var deployer := DeployerNode.new()
			deployer.name = "Deployer_%s_%d" % [carriage.name, i]
			deployer.position = Vector3(0.0, deployer_y, z_offset)
			carriage.add_child(deployer)
			carriage.deployers.append(deployer)
		else:
			var turret := RoofTurretNode.new()
			turret.name = "RoofTurret_%s_%d" % [carriage.name, i]
			turret.position = Vector3(0.0, turret_y, z_offset)
			carriage.add_child(turret)
			carriage.roof_turrets.append(turret)


func _clamp_half_thickness(setup: int) -> float:
	match setup:
		ClampNode.ClampSetup.SINGLE: return 0.04
		ClampNode.ClampSetup.DOUBLE: return 0.04
		ClampNode.ClampSetup.TRIPLE: return 0.035
		_:                           return 0.11  # FOUR: corner cube half-size


func _attach_clamps_for_setup(container: ContainerNode, is_right_side: bool, setup: int, rng: RandomNumberGenerator) -> void:
	var half_t  := _clamp_half_thickness(setup)
	var clamp_x := CONTAINER_WIDTH / 2.0 + half_t if is_right_side else -(CONTAINER_WIDTH / 2.0 + half_t)
	var half_h  := CONTAINER_HEIGHT / 2.0
	var half_d  := CONTAINER_DEPTH  / 2.0
	var face_off := half_t

	var hp: float
	var positions: Array

	match setup:
		ClampNode.ClampSetup.SINGLE:
			hp = GameConfig.single_clamp_hp
			positions = [Vector3(clamp_x, 0.0, 0.0)]

		ClampNode.ClampSetup.DOUBLE:
			hp = GameConfig.double_clamp_hp
			var candidates := [
				Vector3(clamp_x, 0.0,               0.0),
				Vector3(0.0,  half_h + face_off,     0.0),
				Vector3(0.0, -(half_h + face_off),   0.0),
			]
			# Partial Fisher-Yates: shuffle to pick 2 at random
			for i in range(2, 0, -1):
				var j := rng.randi_range(0, i)
				var tmp := candidates[i]
				candidates[i] = candidates[j]
				candidates[j] = tmp
			positions = [candidates[0], candidates[1]]

		ClampNode.ClampSetup.TRIPLE:
			hp = GameConfig.triple_clamp_hp
			var z_step := CONTAINER_DEPTH / 3.0
			positions = [
				Vector3(0.0, half_h + face_off, -z_step),
				Vector3(0.0, half_h + face_off,  0.0),
				Vector3(0.0, half_h + face_off,  z_step),
			]

		_:  # FOUR
			hp = GameConfig.four_clamp_hp
			positions = [
				Vector3(clamp_x,  half_h * 0.7,  half_d * 0.7),
				Vector3(clamp_x,  half_h * 0.7, -half_d * 0.7),
				Vector3(clamp_x, -half_h * 0.7,  half_d * 0.7),
				Vector3(clamp_x, -half_h * 0.7, -half_d * 0.7),
			]

	for pos in positions:
		var clamp: ClampNode = _clamp_scene.instantiate() as ClampNode
		clamp.name = "Clamp_%.1f_%.1f_%.1f" % [pos.x, pos.y, pos.z]
		clamp.configure(setup, _compute_surface_normal(pos))
		clamp.position = pos
		container.add_child(clamp)
		clamp.set_hitpoints(hp)
		container.register_clamp(clamp)


func _compute_surface_normal(local_pos: Vector3) -> Vector3:
	var ax := absf(local_pos.x)
	var ay := absf(local_pos.y)
	if ax >= ay:
		return Vector3(signf(local_pos.x), 0.0, 0.0)
	return Vector3(0.0, signf(local_pos.y), 0.0)


func _create_box_car(car_name: String, size: Vector3, color: Color, glb_path: String = "") -> Node3D:
	var node := Node3D.new()
	node.name = car_name

	var mesh_slot := MeshInstance3D.new()
	mesh_slot.name = "MeshSlot"

	var loaded_mesh = _try_load_glb_mesh(glb_path) if glb_path != "" else null
	if loaded_mesh != null:
		mesh_slot.mesh = loaded_mesh
		var mat := StandardMaterial3D.new()
		mat.albedo_color = color
		mesh_slot.material_override = mat
	else:
		var box := BoxMesh.new()
		box.size = size
		var mat := StandardMaterial3D.new()
		mat.albedo_color = color
		box.material = mat
		mesh_slot.mesh = box
	node.add_child(mesh_slot)

	var body := StaticBody3D.new()
	body.collision_layer = 1
	body.collision_mask = 0
	body.name = "TrainBody"
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = size
	col.shape = shape
	body.add_child(col)
	node.add_child(body)

	return node


func _try_load_glb_mesh(glb_path: String):
	if not ResourceLoader.exists(glb_path):
		return null
	var scene := load(glb_path) as PackedScene
	if scene == null:
		return null
	var root := scene.instantiate() as Node3D
	var body := root.find_child("Body") as MeshInstance3D
	var mesh = body.mesh if body != null else null
	root.queue_free()
	return mesh
