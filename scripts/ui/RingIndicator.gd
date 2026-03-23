## Screen-space bouncing ring drawn around a 3D world target.
## Add to a CanvasLayer. Set target and cam each time you want to highlight something.
## Hides itself automatically when target is null or behind the camera.

extends Control

var target: Node3D = null
var cam: Camera3D = null
var world_radius: float = 1.5
var line_width: float = 3.0
var ring_color: Color = Color(1.0, 0.88, 0.2, 0.92)

var _time: float = 0.0
var _radius: float = 55.0


func _process(delta: float) -> void:
	if target == null or cam == null or not is_instance_valid(target) or not target.is_inside_tree():
		visible = false
		return

	if cam.is_position_behind(target.global_position):
		visible = false
		return

	visible = true
	position = cam.unproject_position(target.global_position)

	# Scale ring radius to match the world-space size of the target
	var dist := (target.global_position - cam.global_position).length()
	var v_size := get_viewport().get_visible_rect().size
	var focal_len := v_size.x / (2.0 * tan(deg_to_rad(cam.fov * 0.5)))
	_radius = clampf((world_radius / maxf(dist, 0.01)) * focal_len + 12.0, 20.0, 350.0)

	_time += delta
	var bounce := 1.0 + 0.14 * sin(_time * TAU * 1.3)
	scale = Vector2(bounce, bounce)

	queue_redraw()


func _draw() -> void:
	draw_arc(Vector2.ZERO, _radius, 0.0, TAU, 64, ring_color, line_width, true)
