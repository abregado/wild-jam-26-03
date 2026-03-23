## Projectile fired by drones at the player car.
##
## Flies toward a fixed world-space target position at drone_bullet_speed.
## Collision layer 64 (bit 7 = drone projectiles) — detected by Shield's Area3D.
##
## If is_hit_bullet is true and the bullet reaches its target without being blocked
## by the shield, it applies car_speed_damage_per_hit to TrainSpeedManager.
## block() is called by Shield when the player deflects it.

extends Node3D

var _target_pos: Vector3
var _is_hit_bullet: bool = false
var _speed: float = 0.0
var _has_been_blocked: bool = false
var _initialized: bool = false


## Called by DroneNode after add_child so Initialize values are available in _process.
func initialize(target_pos: Vector3, is_hit: bool, speed: float) -> void:
	_target_pos = target_pos
	_is_hit_bullet = is_hit
	_speed = speed
	_initialized = true


func _ready() -> void:
	var size: float = GameConfig.drone_bullet_size

	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(1.0, 0.2, 0.05)
	mat.emission_enabled = true
	mat.emission = Color(1.0, 0.1, 0.0)
	mat.emission_energy_multiplier = 5.0
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED

	var sphere := SphereMesh.new()
	sphere.radius = size
	sphere.height = size * 2.0
	sphere.material = mat

	var mesh := MeshInstance3D.new()
	mesh.name = "MeshSlot"
	mesh.mesh = sphere
	add_child(mesh)

	# Layer 64 (bit 7) = drone projectiles, monitorable by Shield
	var area := Area3D.new()
	area.collision_layer = 64
	area.collision_mask = 0
	area.monitorable = true
	area.monitoring = false
	area.name = "Area3D"

	var col := CollisionShape3D.new()
	var shape := SphereShape3D.new()
	shape.radius = size
	col.shape = shape
	area.add_child(col)
	add_child(area)


## Called by Shield when it intercepts this bullet.
func block() -> void:
	_has_been_blocked = true
	queue_free()


func _process(delta: float) -> void:
	if not _initialized:
		return

	var dir := _target_pos - global_position
	var dist := dir.length()

	if dist < 0.4:
		if _is_hit_bullet and not _has_been_blocked:
			TrainSpeedManager.apply_car_speed_damage(GameConfig.car_speed_damage_per_hit)
			var player_car := get_tree().root.find_child("PlayerCar", true, false) as Node
			if player_car != null and player_car.has_method("flash_shield_hit"):
				player_car.flash_shield_hit()
			VfxSpawner.spawn("car_hit", global_position)
		queue_free()
		return

	global_position += dir.normalized() * _speed * delta
