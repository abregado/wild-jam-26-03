## Cargo container attached to a carriage.
##
## Area3D on collision layer 2 (Containers), mask 16 (Projectiles).
##
## Signals:
##   cargo_detached(cargo_name, was_beaconed) — cargo recovered (collected).
##   container_destroyed                      — cargo lost.
##   damage_taken                             — emitted on first hit.
##
## Recovery chance (on clamp-destruction detach):
##   Base: 20% + 60% * remainingHpPercent  → 20%–80%
##   1 beacon: +40% bonus (capped at 100%)
##   2+ beacons: always recovered.
##
## Visual:
##   Untagged  — bright orange
##   1 beacon  — cargo colour
##   2 beacons — cargo colour + emission glow (guaranteed recovery)

class_name ContainerNode
extends Node3D

signal cargo_detached(cargo_name: String, was_beaconed: bool)
signal container_destroyed
signal damage_taken

var is_tagged: bool:
	get: return _beacon_count > 0
var is_scrap: bool = false
var cargo_name: String = "Unknown"
var cargo_color: Color = Color.GRAY

var _max_hp: float = 0.0
var _hp: float = 0.0
var _beacon_count: int = 0
var _clamps: Array = []
var _living_clamps: int = 0
var _mesh: MeshInstance3D = null
var _material: StandardMaterial3D = null
var _is_dead: bool = false
var _damage_taken_fired: bool = false


func _ready() -> void:
	_max_hp = GameConfig.container_hitpoints
	_hp = _max_hp

	_mesh = get_node("MeshSlot") as MeshInstance3D
	_material = StandardMaterial3D.new()
	_material.albedo_color = Color(0.95, 0.5, 0.05)
	_mesh.material_override = _material

	var area := get_node_or_null("Area3D") as Area3D
	if area != null:
		area.area_entered.connect(_on_area_entered)


func set_cargo_type(cargo_type) -> void:
	cargo_name = cargo_type.name
	cargo_color = cargo_type.color
	is_scrap = cargo_type.is_scrap


func register_clamp(clamp: Node3D) -> void:
	_clamps.append(clamp)
	_living_clamps += 1
	clamp.destroyed.connect(_on_clamp_destroyed)


## Direct damage to container HP.
func take_damage(amount: float) -> void:
	if _is_dead:
		return
	if not _damage_taken_fired:
		_damage_taken_fired = true
		damage_taken.emit()
	_hp -= amount
	if _hp <= 0.0:
		_explode()


## AoE splash check — damages clamps within radius.
func take_splash_damage(origin: Vector3, radius: float, clamp_damage: float) -> void:
	if _is_dead:
		return
	for clamp in _clamps:
		if not clamp.is_alive:
			continue
		if clamp.global_position.distance_to(origin) <= radius:
			clamp.take_damage(clamp_damage)


## Tags the container with a beacon. First reveals cargo colour; second adds glow.
func tag() -> void:
	_beacon_count += 1
	if _beacon_count == 1:
		_material.albedo_color = cargo_color
	elif _beacon_count == 2:
		_material.albedo_color = cargo_color
		_material.emission_enabled = true
		_material.emission = cargo_color
		_material.emission_energy_multiplier = 3.0


func _on_clamp_destroyed() -> void:
	_living_clamps -= 1
	if _living_clamps <= 0:
		_detach()


func _detach() -> void:
	if _is_dead:
		return
	_is_dead = true

	var health_percent := clampf(_hp / _max_hp, 0.0, 1.0) if _max_hp > 0.0 else 0.0
	var chance := 0.2 + 0.6 * health_percent

	if _beacon_count >= 2:
		chance = 1.0
	elif _beacon_count == 1:
		chance = minf(chance + 0.4, 1.0)

	var recovered := randf() < chance

	if recovered:
		cargo_detached.emit(cargo_name, is_tagged)
	else:
		container_destroyed.emit()

	SoundManager.play("container_detached")
	VfxSpawner.spawn("container_detach", global_position)

	# Fall off the train
	var tween := create_tween()
	tween.tween_property(self, "position:y", global_position.y - 15.0, 1.5)\
		.set_ease(Tween.EASE_IN)\
		.set_trans(Tween.TRANS_QUAD)
	tween.tween_callback(queue_free)


func _explode() -> void:
	if _is_dead:
		return
	_is_dead = true

	container_destroyed.emit()
	SoundManager.play("container_destroyed")
	_material.albedo_color = Color.WHITE
	_material.emission_enabled = true
	_material.emission = Color.ORANGE_RED

	var tween := create_tween()
	tween.tween_property(_mesh, "scale", Vector3.ZERO, 0.3)
	tween.tween_callback(queue_free)


func _on_area_entered(_other: Area3D) -> void:
	pass
