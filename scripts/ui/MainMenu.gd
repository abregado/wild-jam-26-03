## Main menu. Entry point of the game.
##
## Layout (built in code):
##   Background image (full-screen TextureRect)
##   Dark overlay for readability
##   Centred VBox: Title, 3 save-slot panels, Options, Quit
##   Options overlay (hidden by default)
##   Delete confirmation panel (hidden by default)

extends Control

const _OptionsMenuScript = preload("res://scripts/ui/OptionsMenu.gd")

var _slot_buttons:   Array = []   # Array[Button], size 3
var _delete_buttons: Array = []   # Array[Button], size 3

var _options_overlay:     ColorRect      = null
var _options_center_wrap: CenterContainer = null
var _options_menu                         = null   # OptionsMenu instance
var _confirm_panel:       Control         = null
var _pending_delete_slot: int             = -1


func _ready() -> void:
	MusicManager.play_context("menu")
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	_build_ui()
	_refresh_slots()


# ── UI Construction ────────────────────────────────────────────────────────────

func _build_ui() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)

	# Background
	var bg := TextureRect.new()
	bg.stretch_mode = TextureRect.STRETCH_SCALE
	bg.expand_mode  = TextureRect.EXPAND_IGNORE_SIZE
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var bg_tex := load("res://assets/menu/background.png") as Texture2D
	if bg_tex != null:
		bg.texture = bg_tex
	add_child(bg)

	# Dark overlay
	var overlay := ColorRect.new()
	overlay.color = Color(0.0, 0.0, 0.0, 0.45)
	overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(overlay)

	# Centre container
	var center_wrap := CenterContainer.new()
	center_wrap.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(center_wrap)

	# Content column
	var centre := VBoxContainer.new()
	centre.alignment = BoxContainer.ALIGNMENT_CENTER
	centre.custom_minimum_size = Vector2(700.0, 0.0)
	centre.add_theme_constant_override("separation", 12)
	center_wrap.add_child(centre)

	# Title
	var title := Label.new()
	title.text = "WILD JAM"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size", 52)
	centre.add_child(title)

	var spacer1 := Control.new()
	spacer1.custom_minimum_size = Vector2(0.0, 24.0)
	centre.add_child(spacer1)

	# Save slots row
	var slots_row := HBoxContainer.new()
	slots_row.alignment = BoxContainer.ALIGNMENT_CENTER
	slots_row.add_theme_constant_override("separation", 20)
	centre.add_child(slots_row)

	_slot_buttons.resize(3)
	_delete_buttons.resize(3)

	for i in 3:
		var slot_idx := i
		var slot_box := VBoxContainer.new()
		slot_box.add_theme_constant_override("separation", 6)
		slots_row.add_child(slot_box)

		var btn := Button.new()
		btn.custom_minimum_size  = Vector2(200.0, 90.0)
		btn.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
		btn.pressed.connect(func(): _on_slot_pressed(slot_idx))
		slot_box.add_child(btn)
		_slot_buttons[i] = btn

		var del := Button.new()
		del.text = "[X]"
		del.custom_minimum_size  = Vector2(200.0, 28.0)
		del.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
		del.modulate = Color(0.41, 0.41, 0.41, 1.0)
		del.pressed.connect(func(): _on_delete_pressed(slot_idx))
		slot_box.add_child(del)
		_delete_buttons[i] = del

	var spacer2 := Control.new()
	spacer2.custom_minimum_size = Vector2(0.0, 32.0)
	centre.add_child(spacer2)

	# Options button
	var opt_btn := Button.new()
	opt_btn.text = "OPTIONS"
	opt_btn.custom_minimum_size  = Vector2(220.0, 48.0)
	opt_btn.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	opt_btn.pressed.connect(_on_options_pressed)
	centre.add_child(opt_btn)

	var spacer3 := Control.new()
	spacer3.custom_minimum_size = Vector2(0.0, 10.0)
	centre.add_child(spacer3)

	# Quit button
	var quit_btn := Button.new()
	quit_btn.text = "QUIT"
	quit_btn.custom_minimum_size  = Vector2(220.0, 48.0)
	quit_btn.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	quit_btn.pressed.connect(get_tree().quit)
	centre.add_child(quit_btn)

	# Options overlay (full-screen, hidden)
	_options_overlay = ColorRect.new()
	_options_overlay.color = Color(0.0, 0.0, 0.0, 0.7)
	_options_overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_options_overlay.visible = false
	add_child(_options_overlay)

	# CenterContainer keeps the options panel centred regardless of window size
	_options_center_wrap = CenterContainer.new()
	_options_center_wrap.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_options_center_wrap.visible = false
	add_child(_options_center_wrap)

	_options_menu = _OptionsMenuScript.new()
	_options_menu.custom_minimum_size = Vector2(660.0, 520.0)
	_options_menu.closed.connect(_on_options_closed)
	_options_center_wrap.add_child(_options_menu)

	# Confirm-delete panel (hidden)
	_build_confirm_panel()


func _build_confirm_panel() -> void:
	var confirm_wrap := CenterContainer.new()
	confirm_wrap.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	confirm_wrap.visible = false
	add_child(confirm_wrap)
	_confirm_panel = confirm_wrap

	var panel := PanelContainer.new()
	panel.custom_minimum_size = Vector2(360.0, 0.0)
	confirm_wrap.add_child(panel)

	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 16)
	panel.add_child(vbox)

	var lbl := Label.new()
	lbl.text = "Delete this save?"
	lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	lbl.add_theme_font_size_override("font_size", 18)
	vbox.add_child(lbl)

	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", 20)
	vbox.add_child(row)

	var yes_btn := Button.new()
	yes_btn.text = "DELETE"
	yes_btn.custom_minimum_size = Vector2(130.0, 40.0)
	yes_btn.pressed.connect(_on_confirm_delete)
	row.add_child(yes_btn)

	var no_btn := Button.new()
	no_btn.text = "CANCEL"
	no_btn.custom_minimum_size = Vector2(130.0, 40.0)
	no_btn.pressed.connect(func(): _confirm_panel.visible = false)
	row.add_child(no_btn)


# ── Slot management ────────────────────────────────────────────────────────────

func _refresh_slots() -> void:
	for i in 3:
		if SaveManager.slot_exists(i):
			var meta: Array = SaveManager.get_slot_meta(i)
			var raids: int    = meta[0]
			var date: String  = meta[1]
			_slot_buttons[i].text    = "SLOT %d\nRaids: %d\n%s" % [i + 1, raids, date]
			_delete_buttons[i].modulate = Color.WHITE
			_delete_buttons[i].disabled = false
		else:
			_slot_buttons[i].text       = "SLOT %d\n+" % (i + 1)
			_delete_buttons[i].modulate = Color(0.41, 0.41, 0.41, 1.0)
			_delete_buttons[i].disabled = true


func _on_slot_pressed(slot: int) -> void:
	SoundManager.play("ui_button_click")
	if SaveManager.slot_exists(slot):
		var data: Dictionary = SaveManager.load_slot(slot)
		GameSession.load_from_save(data, slot)
	else:
		GameSession.start_new_game(slot)
	get_tree().change_scene_to_file("res://scenes/Main.tscn")


func _on_delete_pressed(slot: int) -> void:
	SoundManager.play("ui_button_click")
	_pending_delete_slot = slot
	_confirm_panel.visible = true


func _on_confirm_delete() -> void:
	if _pending_delete_slot >= 0:
		SaveManager.delete_slot(_pending_delete_slot)
		_pending_delete_slot = -1
	_confirm_panel.visible = false
	_refresh_slots()


func _on_options_pressed() -> void:
	SoundManager.play("ui_button_click")
	_options_overlay.visible    = true
	_options_center_wrap.visible = true
	_options_menu.open()


func _on_options_closed() -> void:
	_options_overlay.visible    = false
	_options_center_wrap.visible = false
