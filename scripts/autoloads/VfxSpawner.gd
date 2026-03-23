## Autoload singleton. Spawns particle effects by ID.
##
## Usage: VfxSpawner.spawn("effect_id", world_position)
##
## Lookup order per ID:
##   1. res://scenes/vfx/{id}.tscn — artist-replaceable scene (must auto-free itself)
##   2. Built-in procedural CPUParticles3D fallback
##
## Effect IDs:
##   hit_damageable, hit_nondamageable, clamp_destroyed, turret_repair,
##   drone_deployed, drone_destroyed, container_detach, shield_hit, car_hit,
##   drone_prefire, drone_muzzle, turret_prefire, turret_muzzle, player_muzzle

extends Node

# Fallback configs: [color, size, amount, lifetime, speed, speed_var]
const FALLBACKS := {
	"hit_damageable":    [Color(1.0, 0.5, 0.1),  0.10, 20, 0.30, 3.0, 2.0],
	"hit_nondamageable": [Color(0.6, 0.6, 0.6),  0.08, 12, 0.25, 2.0, 1.0],
	"clamp_destroyed":   [Color(1.0, 0.85, 0.0), 0.12, 30, 0.50, 4.0, 2.0],
	"turret_repair":     [Color(0.9, 0.6, 0.0),  0.10, 20, 0.60, 2.5, 1.5],
	"drone_deployed":    [Color(0.2, 0.6, 1.0),  0.10, 24, 0.40, 3.0, 1.5],
	"drone_destroyed":   [Color(0.8, 0.3, 0.1),  0.14, 40, 0.70, 5.0, 3.0],
	"container_detach":  [Color(0.7, 0.7, 0.7),  0.18, 50, 0.80, 4.0, 2.0],
	"shield_hit":        [Color(0.3, 0.7, 1.0),  0.10, 20, 0.30, 3.0, 1.5],
	"car_hit":           [Color(1.0, 0.1, 0.1),  0.12, 25, 0.40, 3.5, 2.0],
	"drone_prefire":     [Color(1.0, 0.2, 0.0),  0.06, 10, 0.20, 1.0, 0.5],
	"drone_muzzle":      [Color(1.0, 0.5, 0.1),  0.08, 15, 0.15, 4.0, 2.0],
	"turret_prefire":    [Color(1.0, 0.2, 0.0),  0.06, 10, 0.20, 1.0, 0.5],
	"turret_muzzle":     [Color(1.0, 0.5, 0.1),  0.08, 15, 0.15, 4.0, 2.0],
	"player_muzzle":     [Color(1.0, 0.85, 0.3), 0.10, 20, 0.12, 5.0, 3.0],
}

var _scene_cache: Dictionary = {}   # id -> PackedScene
var _checked_paths: Dictionary = {} # id -> true (paths where no .tscn was found)


func spawn(id: String, world_pos: Vector3) -> void:
	var effect: Node3D

	if id in _scene_cache:
		effect = (_scene_cache[id] as PackedScene).instantiate() as Node3D
	elif id not in _checked_paths:
		_checked_paths[id] = true
		var path := "res://scenes/vfx/%s.tscn" % id
		if ResourceLoader.exists(path):
			var scene := load(path) as PackedScene
			_scene_cache[id] = scene
			effect = scene.instantiate() as Node3D
		else:
			effect = _build_fallback(id)
	else:
		effect = _build_fallback(id)

	get_tree().root.add_child(effect)
	effect.global_position = world_pos


func _build_fallback(id: String) -> Node3D:
	var cfg: Array = FALLBACKS.get(id, [Color.WHITE, 0.1, 12, 0.3, 2.0, 1.0])
	var col: Color  = cfg[0]
	var sz: float   = cfg[1]
	var amt: int    = cfg[2]
	var lt: float   = cfg[3]
	var sp: float   = cfg[4]
	var sv: float   = cfg[5]

	var root := Node3D.new()
	root.name = "Vfx_%s" % id

	var mat := StandardMaterial3D.new()
	mat.albedo_color = col
	mat.emission_enabled = true
	mat.emission = col
	mat.emission_energy_multiplier = 4.0
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED

	var sphere := SphereMesh.new()
	sphere.radius = sz * 0.1
	sphere.height = sz * 0.2
	sphere.material = mat

	var particles := CpuParticles3D.new()
	particles.amount = amt
	particles.lifetime = lt
	particles.one_shot = true
	particles.emitting = true
	particles.local_coords = false
	particles.mesh = sphere
	particles.initial_velocity_min = sp - sv
	particles.initial_velocity_max = sp + sv
	particles.spread = 180.0
	particles.direction = Vector3.UP
	particles.gravity = Vector3(0.0, -9.8, 0.0)
	root.add_child(particles)

	var timer := Timer.new()
	timer.wait_time = lt + 0.15
	timer.one_shot = true
	timer.autostart = true
	timer.timeout.connect(root.queue_free)
	root.add_child(timer)

	return root
