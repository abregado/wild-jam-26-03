## HUD CanvasLayer. Updates every frame from PlayerCar and TrainSpeedManager.
##
## Node paths (set in scene or override via exports):
##   SpeedBar (ProgressBar)       — relative speed indicator
##   TrainSpeedLabel (Label)      — current train speed text
##   WarningLabel (Label)         — "⚠ OUT OF RANGE" warning (hidden by default)
##   CountdownLabel (Label)       — countdown timer label
##   ClickPrompt (Label)          — hidden once mouse is captured
##   ObstacleWarning (Label)      — cliff/roof warning text
##   ButtonPrompts (VBoxContainer) — auto-built key glyphs

extends CanvasLayer

@export var speed_bar_path: NodePath = ^"BottomContainer/SpeedBar"
@export var train_speed_label_path: NodePath = ^"TopRight/TrainSpeedLabel"
@export var warning_label_path: NodePath = ^"Warning/WarningLabel"
@export var countdown_label_path: NodePath = ^"Warning/CountdownLabel"

var _speed_bar: ProgressBar = null
var _train_speed_label: Label = null
var _warning_label: Label = null
var _countdown_label: Label = null
var _click_prompt: Label = null
var _obstacle_warning_label: Label = null

var _player_car: Node3D = null
var _turret: Node3D = null


func _ready() -> void:
	_speed_bar          = get_node(speed_bar_path) as ProgressBar
	_train_speed_label  = get_node(train_speed_label_path) as Label
	_warning_label      = get_node(warning_label_path) as Label
	_countdown_label    = get_node(countdown_label_path) as Label
	_click_prompt       = get_node("ClickPrompt") as Label
	_obstacle_warning_label = get_node("ObstacleWarning") as Label

	hide_warning()
	_build_button_prompts()


func set_references(car: Node3D, turret: Node3D) -> void:
	_player_car = car
	_turret = turret


func _process(_delta: float) -> void:
	if _player_car == null:
		return

	# Speed bar: map relative velocity to 0–100
	var max_back := absf(GameConfig.min_relative_velocity)
	var max_fwd  := GameConfig.max_relative_velocity
	var range_   := max_back + max_fwd
	var normalized: float = (_player_car.relative_velocity + max_back) / range_
	_speed_bar.value = clampf(normalized * 100.0, 0.0, 100.0)

	# Train speed label
	_train_speed_label.text = "Train: %.0f u/s" % TrainSpeedManager.current_train_speed

	# Obstacle warning
	if ObstacleManager.is_in_warning:
		_obstacle_warning_label.text = "⚠ %s AHEAD" % _build_obstacle_warning_text(
			ObstacleManager.upcoming_cliff_side, ObstacleManager.upcoming_movement_limit)
		_obstacle_warning_label.visible = true
	else:
		_obstacle_warning_label.visible = false

	# Hide click prompt once mouse is captured
	_click_prompt.visible = Input.mouse_mode != Input.MOUSE_MODE_CAPTURED


func show_warning(countdown: float) -> void:
	_warning_label.visible = true
	_countdown_label.visible = true
	_countdown_label.text = "OUT OF RANGE — %ds" % int(countdown)


func hide_warning() -> void:
	_warning_label.visible = false
	_countdown_label.visible = false


func update_countdown(countdown: float) -> void:
	if _countdown_label.visible:
		_countdown_label.text = "OUT OF RANGE — %ds" % int(countdown)


# ── Button Prompts ────────────────────────────────────────────────────────────

func _build_button_prompts() -> void:
	var vbox := get_node("ButtonPrompts") as VBoxContainer
	if vbox == null:
		return

	var move_row := HBoxContainer.new()
	move_row.add_theme_constant_override("separation", 8)
	move_row.add_child(_build_move_cross())
	move_row.add_child(_make_prompt_label("Move"))
	vbox.add_child(move_row)

	_add_prompt_row(vbox, "fire_primary",       "Fire")
	_add_prompt_row(vbox, "fire_beacon",        "Beacon")
	_add_prompt_row(vbox, "switch_side_over",   "Flip Over")
	_add_prompt_row(vbox, "switch_side_under",  "Flip Under")


func _build_move_cross() -> VBoxContainer:
	const G := 28
	const SEP := 3

	var cross := VBoxContainer.new()
	cross.add_theme_constant_override("separation", SEP)

	var top_row := HBoxContainer.new()
	top_row.add_theme_constant_override("separation", SEP)
	var spacer := Control.new()
	spacer.custom_minimum_size = Vector2(G + SEP, G)
	top_row.add_child(spacer)
	top_row.add_child(_get_action_glyph("move_forward", G))
	cross.add_child(top_row)

	var bot_row := HBoxContainer.new()
	bot_row.add_theme_constant_override("separation", SEP)
	bot_row.add_child(_get_action_glyph("move_left", G))
	bot_row.add_child(_get_action_glyph("move_backward", G))
	bot_row.add_child(_get_action_glyph("move_right", G))
	cross.add_child(bot_row)

	return cross


func _add_prompt_row(parent: VBoxContainer, action: String, label: String) -> HBoxContainer:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 8)
	row.add_child(_get_action_glyph(action, 28))
	row.add_child(_make_prompt_label(label))
	parent.add_child(row)
	return row


func _get_action_glyph(action: String, size: int) -> Control:
	var events := InputMap.action_get_events(action)
	if events.size() > 0:
		var ev := events[0]
		if ev is InputEventKey:
			var key := ev as InputEventKey
			var k: Key = key.physical_keycode if key.physical_keycode != KEY_NONE else key.keycode
			var file := _key_to_glyph_file(k)
			if file != "":
				var tex := load("res://assets/glyphs/%s" % file) as Texture2D
				if tex != null:
					return _make_glyph(file, size)
			return _make_key_label(OS.get_keycode_string(k), size)
		if ev is InputEventMouseButton:
			var mb := ev as InputEventMouseButton
			var mb_file := ""
			if mb.button_index == MOUSE_BUTTON_LEFT:   mb_file = "mouse_left_outline.png"
			elif mb.button_index == MOUSE_BUTTON_RIGHT: mb_file = "mouse_right_outline.png"
			if mb_file != "":
				var tex := load("res://assets/glyphs/%s" % mb_file) as Texture2D
				if tex != null:
					return _make_glyph(mb_file, size)
			var mb_name := "LMB" if mb.button_index == MOUSE_BUTTON_LEFT \
				else ("RMB" if mb.button_index == MOUSE_BUTTON_RIGHT else "Mouse")
			return _make_key_label(mb_name, size)
	return _make_key_label("?", size)


func _key_to_glyph_file(k: Key) -> String:
	if k >= KEY_A and k <= KEY_Z:
		var letter := char(int(k) - KEY_A + "a".unicode_at(0))
		return "keyboard_%s_outline.png" % letter
	match k:
		KEY_SPACE: return "keyboard_space_outline.png"
		KEY_CTRL:  return "keyboard_ctrl_outline.png"
	return ""


func _make_key_label(text: String, size: int) -> Label:
	var lbl := Label.new()
	lbl.text = text
	lbl.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	lbl.custom_minimum_size = Vector2(size, size)
	lbl.add_theme_font_size_override("font_size", 11)
	return lbl


func _make_glyph(file: String, size: int) -> TextureRect:
	var tr := TextureRect.new()
	tr.custom_minimum_size = Vector2(size, size)
	tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tr.stretch_mode = TextureRect.STRETCH_SCALE
	tr.texture = load("res://assets/glyphs/%s" % file) as Texture2D
	return tr


func _make_prompt_label(text: String) -> Label:
	var lbl := Label.new()
	lbl.text = text
	lbl.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	lbl.add_theme_font_size_override("font_size", 13)
	return lbl


func _build_obstacle_warning_text(cliff: int, limit: int) -> String:
	var cliff_part := ""
	match cliff:
		ObstacleManager.CliffSide.LEFT:  cliff_part = "LEFT CLIFF"
		ObstacleManager.CliffSide.RIGHT: cliff_part = "RIGHT CLIFF"

	var limit_part := ""
	match limit:
		ObstacleManager.MovementLimit.ROOF:    limit_part = "ROOF"
		ObstacleManager.MovementLimit.PLATEAU: limit_part = "PLATEAU"

	if cliff_part.length() > 0 and limit_part.length() > 0:
		return "%s + %s" % [cliff_part, limit_part]
	return cliff_part if cliff_part.length() > 0 else limit_part
