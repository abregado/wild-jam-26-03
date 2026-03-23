## Transparent sphere shield around the player car.
##
## Area3D on layer 8 (player/shield), monitors layer 64 (drone projectiles).
##
## When a DroneBullet enters the shield:
##   - Calculate angle between camera look direction and direction to bullet.
##   - If angle < shield_block_angle: destroy the bullet and flash the shield.
##   - Otherwise: let the bullet pass through (no effect).
##
## Parented to PlayerCar and created in PlayerCar._ready().

extends Node3D

var _camera: Camera3D = null
var _shield_mat: StandardMaterial3D = null
var _block_angle: float = 0.0
var _flash_timer: float = 0.0

const FLASH_DURATION := 0.18
const SHIELD_RADIUS := 2.5
# Offset so sphere is centred on the car body midpoint.
# Car mesh is at local (0, -0.2, 0.3); Camera3D is at (0, 1.8, 0.6).
# Midpoint ≈ (0, 0.8, 0.45) — sphere of radius 2.5 comfortably contains both.
const SHIELD_OFFSET := Vector3(0.0, 0.8, 0.45)


func _ready() -> void:
	_block_angle = GameConfig.shield_block_angle

	# Camera is a sibling inside PlayerCar
	_camera = get_parent().get_node("Camera3D") as Camera3D

	position = SHIELD_OFFSET
	_build_shield_mesh()
	_build_shield_area()


func _build_shield_mesh() -> void:
	_shield_mat = StandardMaterial3D.new()
	_shield_mat.albedo_color = Color(0.3, 0.7, 1.0, 0.03)
	_shield_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_shield_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_shield_mat.cull_mode = BaseMaterial3D.CULL_DISABLED

	var sphere := SphereMesh.new()
	sphere.radius = SHIELD_RADIUS
	sphere.height = SHIELD_RADIUS * 2.0
	sphere.material = _shield_mat

	var mesh_inst := MeshInstance3D.new()
	mesh_inst.name = "ShieldMesh"
	mesh_inst.mesh = sphere
	add_child(mesh_inst)


func _build_shield_area() -> void:
	# Layer 8 (bit 4) = player shield; mask 64 (bit 7) = drone projectiles
	var area := Area3D.new()
	area.collision_layer = 8
	area.collision_mask = 64
	area.monitoring = true
	area.monitorable = false
	area.name = "Area3D"

	var shape := SphereShape3D.new()
	shape.radius = SHIELD_RADIUS
	var col := CollisionShape3D.new()
	col.shape = shape
	area.add_child(col)
	area.area_entered.connect(_on_area_entered)
	add_child(area)


func _on_area_entered(other: Area3D) -> void:
	var bullet := other.get_parent()
	if bullet == null or not bullet.has_method("block"):
		return

	var camera_forward := -_camera.global_transform.basis.z
	var hit_dir := (bullet.global_position - global_position).normalized()
	var dot := clampf(camera_forward.dot(hit_dir), -1.0, 1.0)
	var angle_deg := rad_to_deg(acos(dot))

	if angle_deg <= _block_angle:
		bullet.block()
		_flash_shield()
		SoundManager.play("player_shield_hit")
		VfxSpawner.spawn("shield_hit", bullet.global_position)


func _flash_shield() -> void:
	_flash_timer = FLASH_DURATION
	_shield_mat.albedo_color = Color(0.4, 0.85, 1.0, 0.45)
	_shield_mat.emission_enabled = true
	_shield_mat.emission = Color(0.2, 0.6, 1.0)
	_shield_mat.emission_energy_multiplier = 3.0


## Called when a drone bullet gets through and hits the car.
func flash_hit() -> void:
	_flash_timer = FLASH_DURATION
	_shield_mat.albedo_color = Color(1.0, 0.1, 0.05, 0.5)
	_shield_mat.emission_enabled = true
	_shield_mat.emission = Color(1.0, 0.05, 0.0)
	_shield_mat.emission_energy_multiplier = 4.0


func _process(delta: float) -> void:
	if _flash_timer <= 0.0:
		return
	_flash_timer -= delta
	if _flash_timer <= 0.0:
		_shield_mat.albedo_color = Color(0.3, 0.7, 1.0, 0.03)
		_shield_mat.emission_enabled = false
