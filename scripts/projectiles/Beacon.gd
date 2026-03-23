## Beacon projectile. Tags a container to reveal its cargo type color.
## Travels slower than bullets. Unlimited ammo, only limited by reload cooldown.
##
## On hit ContainerNode: calls container.tag() which changes its material to cargo color.
## Self-destructs on hit or after MAX_DISTANCE.

extends Node3D

const MAX_DISTANCE := 120.0

var _speed: float = 0.0
var _distance_traveled: float = 0.0
var _has_hit: bool = false


func initialize(speed: float) -> void:
	_speed = speed


func _ready() -> void:
	var area := get_node("Area3D") as Area3D
	area.area_entered.connect(_on_area_entered)


func _process(delta: float) -> void:
	if _has_hit:
		return

	var move := _speed * delta
	global_position += -global_transform.basis.z * move
	_distance_traveled += move

	if _distance_traveled >= MAX_DISTANCE:
		queue_free()


func _on_area_entered(other: Area3D) -> void:
	if _has_hit:
		return

	var parent := other.get_parent()
	if parent != null and parent.has_method("tag"):
		parent.tag()
		_has_hit = true
		queue_free()
	# Beacons pass through clamps
