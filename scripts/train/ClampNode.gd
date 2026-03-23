## A clamp on the surface of a container.
##
## Area3D on collision layer 4 (Clamps), mask 16 (Projectiles).
##
## Call configure(setup, surface_normal) before adding to scene tree.
## Mesh is built procedurally (or loaded from a type-specific GLB) and oriented
## so its flat face lies against the surface described by surface_normal.
##
## Signals:
##   destroyed   — emitted when HP reaches 0.
##   damage_taken — emitted on first hit.

class_name ClampNode
extends Node3D

## Setup types mirror C# ClampSetup enum — used by TrainBuilder.
enum ClampSetup { SINGLE, DOUBLE, TRIPLE, FOUR }

signal destroyed
signal damage_taken

var is_alive: bool = true

var _hp: float = 0.0
var _mesh: MeshInstance3D = null
var _damage_taken_fired: bool = false

var _setup: int = ClampSetup.SINGLE
var _surface_normal: Vector3 = Vector3.UP

const CLAMP_COLOR := Color(1.0, 0.85, 0.0)


func set_hitpoints(hp: float) -> void:
	_hp = hp


## Call before add_child. Stores setup type and surface normal used to orient the mesh.
func configure(setup: int, surface_normal: Vector3) -> void:
	_setup = setup
	_surface_normal = surface_normal.normalized()


func _ready() -> void:
	_build_mesh()
	_build_collision()


func _build_mesh() -> void:
	_mesh = MeshInstance3D.new()
	_mesh.name = "MeshSlot"

	var glb_path := "res://assets/models/train/clamp_%s.glb" % ["single", "double", "triple", "four"][_setup]
	var glb_mesh := _try_load_glb_mesh(glb_path)

	if glb_mesh != null:
		_mesh.mesh = glb_mesh
	else:
		_mesh.mesh = _build_procedural_mesh(_setup)

	var mat := StandardMaterial3D.new()
	mat.albedo_color = CLAMP_COLOR
	mat.metallic = 0.5
	mat.roughness = 0.3
	_mesh.material_override = mat

	_orient_mesh(_mesh)
	add_child(_mesh)


func _build_procedural_mesh(setup: int) -> BoxMesh:
	var box := BoxMesh.new()
	match setup:
		ClampSetup.SINGLE: box.size = Vector3(0.55, 0.08, 0.28)
		ClampSetup.DOUBLE: box.size = Vector3(0.50, 0.08, 0.50)
		ClampSetup.TRIPLE: box.size = Vector3(0.20, 0.07, 0.90)
		_:                 box.size = Vector3(0.22, 0.22, 0.22) # FOUR: corner cube
	return box


func _orient_mesh(mesh_node: Node3D) -> void:
	# Rotate so local Y axis aligns with _surface_normal.
	if _surface_normal.y > 0.5:
		mesh_node.rotation_degrees = Vector3.ZERO
	elif _surface_normal.y < -0.5:
		mesh_node.rotation_degrees = Vector3(180.0, 0.0, 0.0)
	elif _surface_normal.x > 0.5:
		mesh_node.rotation_degrees = Vector3(0.0, 0.0, -90.0)
	else:
		mesh_node.rotation_degrees = Vector3(0.0, 0.0, 90.0)


func _build_collision() -> void:
	var area := Area3D.new()
	area.collision_layer = 4
	area.collision_mask = 16
	area.monitorable = true
	area.monitoring = false
	area.name = "Area3D"

	var col := CollisionShape3D.new()
	var shape := SphereShape3D.new()
	shape.radius = 0.25
	col.shape = shape
	area.add_child(col)
	area.area_entered.connect(_on_area_entered)
	add_child(area)


func take_damage(amount: float) -> void:
	if not is_alive:
		return
	if not _damage_taken_fired:
		_damage_taken_fired = true
		damage_taken.emit()
	_hp -= amount
	if _hp <= 0.0:
		_destroy()


func _destroy() -> void:
	if not is_alive:
		return
	is_alive = false
	destroyed.emit()
	SoundManager.play("clamp_destroyed")
	VfxSpawner.spawn("clamp_destroyed", global_position)

	_mesh.visible = false

	var area := get_node_or_null("Area3D") as Area3D
	if area != null:
		area.set_deferred("monitorable", false)


func _on_area_entered(_other: Area3D) -> void:
	pass


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
