## Script for a train carriage node.
## Holds lists of container/deployer/turret children for reference.
##
## set_slot_count(n) swaps the MeshSlot mesh to the model appropriate for n containers per side:
##   1 → carriage_1.glb  (or procedural 4-unit-long box)
##   2 → carriage_2.glb  (or procedural 8-unit-long box)
##   3 → carriage_3.glb  (or procedural 12-unit-long box)

extends Node3D

var containers: Array = []    # Array of ContainerNode
var deployers: Array = []     # Array of DeployerNode
var roof_turrets: Array = []  # Array of RoofTurretNode

## Length of this carriage body along Z (set by set_slot_count).
var body_length: float = 4.0

const CARRIAGE_COLOR := Color(0.1, 0.3, 0.85)
const WIDTH := 3.0
const HEIGHT := 2.5


func _ready() -> void:
	for child in get_children():
		# ContainerNode/DeployerNode/RoofTurretNode will be picked up by TrainBuilder
		# before _ready fires on carriages, so this is mainly for runtime reference
		if child.get_script() != null:
			var cls_name: String = child.get_class()
			if "ContainerNode" in cls_name:
				containers.append(child)


## Called by TrainBuilder after instantiation to select the correct carriage mesh.
func set_slot_count(slots: int) -> void:
	# Body length = slots × ContainerDepth + gaps between slots + end margin
	body_length = slots * 3.0 + (slots - 1) * 0.3 + 1.0

	var mesh_slot := get_node_or_null("MeshSlot") as MeshInstance3D
	if mesh_slot == null:
		return

	# Try to load the matching GLB
	var glb_path := "res://assets/models/train/carriage_%d.glb" % clampi(slots, 1, 3)
	var scene := load(glb_path) as PackedScene
	if scene != null:
		var root := scene.instantiate() as Node3D
		var body := root.find_child("Body") as MeshInstance3D
		if body != null and body.mesh != null:
			mesh_slot.mesh = body.mesh
			var mat := StandardMaterial3D.new()
			mat.albedo_color = CARRIAGE_COLOR
			mesh_slot.material_override = mat
			root.queue_free()
			_update_collision()
			return
		root.queue_free()

	# Procedural fallback: use computed body_length for Z
	var box := BoxMesh.new()
	box.size = Vector3(WIDTH, HEIGHT, body_length)
	mesh_slot.mesh = box
	var mat := StandardMaterial3D.new()
	mat.albedo_color = CARRIAGE_COLOR
	mesh_slot.material_override = mat
	_update_collision()


func _update_collision() -> void:
	var body := get_node_or_null("CarriageBody") as StaticBody3D
	if body == null:
		return
	var col := body.get_node_or_null("CollisionShape3D") as CollisionShape3D
	if col != null and col.shape is BoxShape3D:
		(col.shape as BoxShape3D).size = Vector3(WIDTH, HEIGHT, body_length)
