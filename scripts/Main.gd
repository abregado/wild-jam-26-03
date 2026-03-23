## Main scene script. Entry point for gameplay.
## Wires up HUD references to PlayerCar and Turret after all children are ready.
## Handles the pause menu (Escape key toggles it during active play).

extends Node3D

var _player_car: Node3D = null
var _pause_layer: CanvasLayer = null
var _pause_visible: bool = false


func _ready() -> void:
	_player_car = get_node("PlayerCar") as Node3D

	var hud := get_node("HUD")
	var turret := _player_car.get_node("Turret") as Node3D
	hud.set_references(_player_car, turret)

	MusicManager.play_context("raid")
	_build_pause_menu()
	print("[Main] Scene ready. Game starting.")


func _input(event: InputEvent) -> void:
	if not (event is InputEventKey):
		return
	var key := event as InputEventKey
	if not key.pressed or key.echo:
		return
	if key.keycode != KEY_ESCAPE:
		return

	if _pause_visible:
		_on_resume()
		get_viewport().set_input_as_handled()
	elif _player_car.is_input_enabled:
		_show_pause()
		get_viewport().set_input_as_handled()


# ── Pause menu ────────────────────────────────────────────────────────────────

func _build_pause_menu() -> void:
	_pause_layer = CanvasLayer.new()
	_pause_layer.layer = 10
	add_child(_pause_layer)

	var overlay := ColorRect.new()
	overlay.color = Color(0.0, 0.0, 0.0, 0.6)
	overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_pause_layer.add_child(overlay)

	var wrap := CenterContainer.new()
	wrap.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_pause_layer.add_child(wrap)

	var panel := PanelContainer.new()
	panel.custom_minimum_size = Vector2(300.0, 0.0)
	wrap.add_child(panel)

	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 16)
	panel.add_child(vbox)

	var title := Label.new()
	title.text = "PAUSED"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size", 28)
	vbox.add_child(title)

	vbox.add_child(HSeparator.new())

	var resume_btn := Button.new()
	resume_btn.text = "RESUME"
	resume_btn.custom_minimum_size = Vector2(240.0, 48.0)
	resume_btn.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	resume_btn.pressed.connect(_on_resume)
	vbox.add_child(resume_btn)

	var menu_btn := Button.new()
	menu_btn.text = "RETURN TO MENU"
	menu_btn.custom_minimum_size = Vector2(240.0, 48.0)
	menu_btn.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	menu_btn.pressed.connect(_on_return_to_menu)
	vbox.add_child(menu_btn)

	_pause_layer.visible = false


func _show_pause() -> void:
	_pause_visible = true
	_player_car.disable_input()
	_pause_layer.visible = true


func _on_resume() -> void:
	_pause_visible = false
	_pause_layer.visible = false
	_player_car.enable_input()


func _on_return_to_menu() -> void:
	GameSession.write_to_save()
	get_tree().change_scene_to_file("res://scenes/ui/MainMenu.tscn")
