## Visible projectile. Moves forward at bullet_speed.
##
## Hit detection: per-frame raycast from current → next position (mask 39).
## This prevents tunneling at high speeds where Area3D overlap would miss frames.
## The Area3D in the scene keeps collision layer 16 but area_entered is unused.
##
## Hit logic:
##   Ray hits Area3D whose parent is ClampNode     → direct damage to clamp.
##   Ray hits Area3D whose parent is ContainerNode → HP damage + AoE splash to nearby clamps.
##   Ray hits Area3D whose parent is DroneNode or RoofTurretNode → take_damage.
##
## Self-destructs on hit or after MAX_DISTANCE traveled.

extends Node3D

const MAX_DISTANCE := 100.0
# Mask 39 = layer 1 (World/Train) + layer 2 (Containers) + layer 3 (Clamps) + layer 6 (Drones)
const HIT_MASK := 39

var _damage: float = 0.0
var _blast_radius: float = 0.0
var _speed: float = 0.0
var _distance_traveled: float = 0.0
var _has_hit: bool = false
var _trail: CPUParticles3D = null


func initialize(damage: float, blast_radius: float, speed: float) -> void:
	_damage = damage
	_blast_radius = blast_radius
	_speed = speed


func _ready() -> void:
	var mesh := get_node("MeshSlot") as MeshInstance3D
	if mesh != null:
		mesh.scale = Vector3.ONE * GameConfig.bullet_size

	_setup_trail()


func _setup_trail() -> void:
	var r: float = GameConfig.trail_thickness
	var sphere := SphereMesh.new()
	sphere.radius = r
	sphere.height = r * 2.0

	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(1.0, 0.7, 0.2)
	mat.emission_enabled = true
	mat.emission = Color(1.0, 0.5, 0.1)
	mat.emission_energy_multiplier = 5.0
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.vertex_color_use_as_albedo = true
	sphere.material = mat

	var fade := Gradient.new()
	fade.set_color(0, Color(1.0, 0.7, 0.2, 1.0))
	fade.set_color(1, Color(1.0, 0.4, 0.05, 0.0))

	_trail = CPUParticles3D.new()
	_trail.amount = 24
	_trail.lifetime = 0.15
	_trail.one_shot = false
	_trail.emitting = true
	_trail.local_coords = false
	_trail.direction = Vector3.ZERO
	_trail.spread = 0.0
	_trail.initial_velocity_min = 0.0
	_trail.initial_velocity_max = 0.0
	_trail.mesh = sphere
	_trail.color_ramp = fade
	add_child(_trail)


func _process(delta: float) -> void:
	if _has_hit:
		return

	var move := _speed * delta
	var forward := -global_transform.basis.z
	var next_pos := global_position + forward * move

	var space_state := get_world_3d().direct_space_state
	var query := PhysicsRayQueryParameters3D.create(global_position, next_pos, HIT_MASK)
	query.collide_with_areas = true
	query.collide_with_bodies = true
	var result := space_state.intersect_ray(query)

	if result.size() > 0:
		var hit_pos: Vector3 = result["position"]
		var hit_damageable := false

		if result["collider"] is Area3D:
			var area := result["collider"] as Area3D
			var parent := area.get_parent()
			if parent is ClampNode:
				(parent as ClampNode).take_damage(_damage)
				hit_damageable = true
			elif parent is ContainerNode:
				(parent as ContainerNode).take_damage(_damage)
				(parent as ContainerNode).take_splash_damage(hit_pos, _blast_radius, _damage)
				hit_damageable = true
			elif parent != null and parent.has_method("take_damage"):
				parent.take_damage(_damage)
				hit_damageable = true

		SoundManager.play("bullet_hit_damagable" if hit_damageable else "bullet_hit_non_damagable")
		VfxSpawner.spawn("hit_damageable" if hit_damageable else "hit_nondamageable", hit_pos)
		global_position = hit_pos
		_hit_and_destroy()
		return

	global_position = next_pos
	_distance_traveled += move

	if _distance_traveled >= MAX_DISTANCE:
		queue_free()


func _hit_and_destroy() -> void:
	_has_hit = true
	_spawn_hit_effect()
	queue_free()


func _spawn_hit_effect() -> void:
	var effect := Node3D.new()
	get_tree().root.add_child(effect)
	effect.global_position = global_position

	# Flash sphere
	var sphere := SphereMesh.new()
	sphere.radius = 0.35
	sphere.height = 0.7
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(1.0, 0.55, 0.05)
	mat.emission_enabled = true
	mat.emission = Color(1.0, 0.35, 0.0)
	mat.emission_energy_multiplier = 8.0
	sphere.material = mat
	var mesh := MeshInstance3D.new()
	mesh.mesh = sphere
	effect.add_child(mesh)

	# Brief point light
	var light := OmniLight3D.new()
	light.light_color = Color(1.0, 0.5, 0.1)
	light.light_energy = 5.0
	light.omni_range = 5.0
	effect.add_child(light)

	# Quick scale-out then free
	var tween := effect.create_tween()
	tween.tween_property(effect, "scale", Vector3.ZERO, 0.18)\
		.set_trans(Tween.TRANS_QUAD)\
		.set_ease(Tween.EASE_IN)
	tween.tween_callback(effect.queue_free)
