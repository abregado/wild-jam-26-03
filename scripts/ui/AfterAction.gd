## After-action phase state machine:
##   BoxBreak → ResourceFly → Purchase (cards)
##
## BOX_BREAK: Unidentified containers shown one at a time in a 3-D SubViewport.
##   Click 4 times to break each open.
##
## RESOURCE_FLY: All gathered resources shown in centre.
##   After 1s each label flies to the top ResourceCounter and increments it.
##
## PURCHASE: Three upgrade cards slide in and flip to reveal.
##   Player buys upgrades, then clicks Next Raid.

extends Control

enum Phase { BOX_BREAK, RESOURCE_FLY, PURCHASE }

const CLICKS_REQUIRED := 4

var _phase: int = Phase.BOX_BREAK

# ── Top bar ────────────────────────────────────────────────────────────────────
var _resource_labels: HBoxContainer = null

# ── Shared ────────────────────────────────────────────────────────────────────
var _phase_label:          Label         = null
var _box_break_area:       VBoxContainer = null
var _haul_list:            VBoxContainer = null
var _card_row:             HBoxContainer = null
var _next_raid_button:     Button        = null
var _save_and_quit_button: Button        = null

# ── Box-break 3-D state ───────────────────────────────────────────────────────
var _current_box_index: int  = 0
var _clicks_on_current: int  = 0
var _is_animating:      bool = false

var _container_auto_rotate: Node3D           = null
var _container_pivot:       Node3D           = null
var _mesh_instance:         MeshInstance3D   = null
var _container_material:    StandardMaterial3D = null
var _viewport_container:    SubViewportContainer = null
var _click_prompt_label:    Label = null
var _progress_dots_label:   Label = null
var _crack_reveal_label:    Label = null
var _box_count_label:       Label = null

# ── Cards ──────────────────────────────────────────────────────────────────────
var _cards: Array = []   # Array[UpgradeCard]


# ── UpgradeCard ────────────────────────────────────────────────────────────────

class UpgradeCard extends PanelContainer:
	const COL_GOOD := Color(0.3, 1.0, 0.45)
	const COL_BAD  := Color(1.0, 0.35, 0.35)

	# [display_name, pos_is_good]
	const STAT_META := {
		"turret_tracking_speed":         ["Tracking Speed",          true],
		"turret_damage":                 ["Damage",                  true],
		"burst_count":                   ["Burst Count",             true],
		"burst_delay":                   ["Burst Delay",             false],
		"rate_of_fire":                  ["Rate of Fire",            true],
		"bullet_speed":                  ["Bullet Speed",            true],
		"beacon_reload_speed":           ["Beacon Reload",           false],
		"max_relative_velocity":         ["Max Speed",               true],
		"blast_radius":                  ["Blast Radius",            true],
		"shield_block_angle":            ["Shield Angle",            true],
		"car_speed_damage_per_hit":      ["Speed Dmg / Hit",         false],
		"number_pre_scanned_containers": ["Pre-Scanned Containers",  true],
	}

	var _def        # Dictionary or null
	var _owner_ref  # AfterAction

	var _name_label:   Label         = null
	var _mod_list:     VBoxContainer = null
	var _cost_label:   Label         = null
	var _status_label: Label         = null
	var _buy_button:   Button        = null
	var _purchased: bool = false
	var _revealed:  bool = false

	func setup(def, owner_ref) -> void:
		_def = def
		_owner_ref = owner_ref

	func _ready() -> void:
		var vbox := VBoxContainer.new()
		add_child(vbox)

		_name_label = Label.new()
		_name_label.text = "???"
		_name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_name_label.add_theme_font_size_override("font_size", 15)
		vbox.add_child(_name_label)

		vbox.add_child(HSeparator.new())

		_mod_list = VBoxContainer.new()
		_mod_list.size_flags_vertical   = Control.SIZE_EXPAND_FILL
		_mod_list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		vbox.add_child(_mod_list)

		vbox.add_child(HSeparator.new())

		_cost_label = Label.new()
		_cost_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_cost_label.modulate = Color.YELLOW
		vbox.add_child(_cost_label)

		_status_label = Label.new()
		_status_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_status_label.modulate = Color.LIME_GREEN
		vbox.add_child(_status_label)

		_buy_button = Button.new()
		_buy_button.text = "BUY"
		_buy_button.disabled = true
		_buy_button.visible = _def != null
		_buy_button.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
		_buy_button.pressed.connect(_on_buy)
		vbox.add_child(_buy_button)

	func reveal() -> void:
		_revealed = true
		if _def == null:
			_name_label.text    = "\u2014 LOCKED \u2014"
			_name_label.modulate = Color(0.41, 0.41, 0.41, 1.0)
			return

		_name_label.text = _def["name"]

		for c in _mod_list.get_children():
			c.queue_free()

		for m in _def["modifiers"]:
			var stat: String   = m["stat"]
			var flat: float    = m.get("flat", 0.0)
			var mult: float    = m.get("multiplier", 1.0)
			var found: bool    = stat in STAT_META
			var stat_label: String = STAT_META[stat][0] if found else stat
			var pos_is_good: bool  = STAT_META[stat][1] if found else true

			if flat != 0.0:
				var good := (flat > 0.0) == pos_is_good
				var val_str: String
				if flat > 0.0:
					val_str = "+%s" % _fmt(flat)
				else:
					val_str = "\u2212%s" % _fmt(absf(flat))
				_add_mod_row(stat_label, val_str, good)

			if mult != 1.0:
				var good := (mult > 1.0) == pos_is_good
				_add_mod_row(stat_label, "\u00d7%s" % _fmt(mult), good)

		var cost_parts := []
		for k in _def["cost"]:
			cost_parts.append("%d\u00d7 %s" % [_def["cost"][k], k])
		_cost_label.text = "Cost: " + "  ".join(cost_parts)

		refresh_affordability()

	func _add_mod_row(stat_name: String, value: String, good: bool) -> void:
		var row := HBoxContainer.new()

		var name_lbl := Label.new()
		name_lbl.text = stat_name
		name_lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		name_lbl.horizontal_alignment  = HORIZONTAL_ALIGNMENT_LEFT
		name_lbl.add_theme_font_size_override("font_size", 12)

		var val_lbl := Label.new()
		val_lbl.text = value
		val_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		val_lbl.modulate = COL_GOOD if good else COL_BAD
		val_lbl.add_theme_font_size_override("font_size", 12)

		row.add_child(name_lbl)
		row.add_child(val_lbl)
		_mod_list.add_child(row)

	func refresh_affordability() -> void:
		if not _revealed or _purchased or _def == null:
			return
		var can := _owner_ref.can_afford(_def)
		_buy_button.disabled = not can
		modulate = Color.WHITE if can else Color(0.5, 0.5, 0.5, 1.0)

	func mark_purchased() -> void:
		_purchased          = true
		_buy_button.disabled = true
		_status_label.text  = "ACQUIRED"
		modulate            = Color(0.4, 0.85, 0.4, 1.0)

	func _on_buy() -> void:
		if _def != null:
			_owner_ref.purchase_upgrade(_def, self)

	static func _fmt(v: float) -> String:
		# Up to 3 significant figures, no trailing zeros
		return "%.3g" % v


# ── _ready ─────────────────────────────────────────────────────────────────────

func _ready() -> void:
	_resource_labels  = get_node("ResourceCounter/VBox/ResourceLabels") as HBoxContainer
	_phase_label      = get_node("MainContent/PhaseLabel") as Label
	_box_break_area   = get_node("MainContent/BoxBreakArea") as VBoxContainer
	_haul_list        = get_node("MainContent/HaulList") as VBoxContainer
	_card_row         = get_node("MainContent/CardRow") as HBoxContainer
	_next_raid_button = get_node("MainContent/NextRaidButton") as Button

	# Reparent NextRaid into a centred button row and add Save & Quit alongside it
	var btn_row := HBoxContainer.new()
	btn_row.alignment = BoxContainer.ALIGNMENT_CENTER
	btn_row.add_theme_constant_override("separation", 20)
	var btn_parent := _next_raid_button.get_parent()
	var btn_idx    := _next_raid_button.get_index()
	btn_parent.add_child(btn_row)
	btn_parent.move_child(btn_row, btn_idx)
	_next_raid_button.reparent(btn_row)

	_save_and_quit_button = Button.new()
	_save_and_quit_button.text = "SAVE & QUIT"
	_save_and_quit_button.custom_minimum_size = Vector2(160.0, 48.0)
	_save_and_quit_button.disabled = true
	_save_and_quit_button.pressed.connect(_on_save_and_quit)
	btn_row.add_child(_save_and_quit_button)

	_next_raid_button.pressed.connect(_on_next_raid)
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	MusicManager.play_context("after_action")

	_build_resource_counter()

	if GameSession.unidentified_cargo.is_empty():
		_start_resource_fly()
	else:
		_start_box_break()


func _process(delta: float) -> void:
	if _phase == Phase.BOX_BREAK \
			and _container_auto_rotate != null \
			and is_instance_valid(_container_auto_rotate):
		_container_auto_rotate.rotate_y(delta * 1.1)


# ── Resource Counter ───────────────────────────────────────────────────────────

func _build_resource_counter() -> void:
	for child in _resource_labels.get_children():
		child.queue_free()

	var first := true
	for ct in GameConfig.cargo_types:
		if not first:
			_resource_labels.add_child(_make_separator_label())
		first = false

		var amount: int = GameSession.player_resources.get(ct["name"], 0)
		var lbl := Label.new()
		lbl.name     = "Res_" + ct["name"]
		lbl.text     = "%s: %d" % [ct["name"], amount]
		lbl.modulate = ct["color"]
		_resource_labels.add_child(lbl)


func _make_separator_label() -> Label:
	var lbl := Label.new()
	lbl.text = "  |  "
	return lbl


func _update_resource_label(name: String) -> void:
	var amount: int = GameSession.player_resources.get(name, 0)
	var lbl := _resource_labels.get_node_or_null("Res_" + name) as Label
	if lbl != null:
		lbl.text = "%s: %d" % [name, amount]


func _find_cargo_color(cargo_name: String) -> Color:
	for ct in GameConfig.cargo_types:
		if ct["name"] == cargo_name:
			return ct["color"]
	return Color.WHITE


# ── Phase: BOX_BREAK ──────────────────────────────────────────────────────────

func _start_box_break() -> void:
	_phase = Phase.BOX_BREAK
	_phase_label.text       = "BREAK OPEN CARGO"
	_box_break_area.visible = true
	_haul_list.visible      = false
	_card_row.visible       = false

	_build_3d_viewport()

	_box_count_label = Label.new()
	_box_count_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_box_break_area.add_child(_box_count_label)

	_click_prompt_label = Label.new()
	_click_prompt_label.text = "Click to break open!"
	_click_prompt_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_box_break_area.add_child(_click_prompt_label)

	_progress_dots_label = Label.new()
	_progress_dots_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_progress_dots_label.add_theme_font_size_override("font_size", 18)
	_box_break_area.add_child(_progress_dots_label)

	_crack_reveal_label = Label.new()
	_crack_reveal_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_crack_reveal_label.visible = false
	_crack_reveal_label.add_theme_font_size_override("font_size", 36)
	_box_break_area.add_child(_crack_reveal_label)

	_load_box(0)


func _build_3d_viewport() -> void:
	_viewport_container = SubViewportContainer.new()
	_viewport_container.stretch = true
	_viewport_container.custom_minimum_size = Vector2(260.0, 260.0)
	_viewport_container.mouse_filter = Control.MOUSE_FILTER_STOP
	_viewport_container.size_flags_horizontal = Control.SIZE_SHRINK_CENTER

	var sv := SubViewport.new()
	sv.size = Vector2i(260, 260)
	sv.transparent_bg = true
	sv.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	_viewport_container.add_child(sv)

	# Camera
	var cam := Camera3D.new()
	cam.position = Vector3(0.0, 0.4, 3.8)
	cam.look_at(Vector3.ZERO, Vector3.UP)
	sv.add_child(cam)

	# Key light
	var key_light := DirectionalLight3D.new()
	key_light.rotation_degrees = Vector3(-40.0, 30.0, 0.0)
	key_light.light_energy = 1.4
	sv.add_child(key_light)

	# Fill light
	var fill_light := OmniLight3D.new()
	fill_light.position = Vector3(-3.0, 2.0, 3.0)
	fill_light.light_energy = 0.6
	fill_light.omni_range = 15.0
	sv.add_child(fill_light)

	# Container hierarchy: auto-rotate parent → jiggle pivot → mesh
	_container_auto_rotate = Node3D.new()
	sv.add_child(_container_auto_rotate)

	_container_pivot = Node3D.new()
	_container_auto_rotate.add_child(_container_pivot)

	_mesh_instance = MeshInstance3D.new()
	_container_pivot.add_child(_mesh_instance)

	# Load GLB mesh or fall back to box primitive
	var mesh: Mesh = null
	var glb_scene := load("res://assets/models/train/container.glb") as PackedScene
	if glb_scene != null:
		var root := glb_scene.instantiate() as Node3D
		var body := root.get_node_or_null("Body") as MeshInstance3D
		if body != null:
			mesh = body.mesh
		root.queue_free()
	if mesh == null:
		var box := BoxMesh.new()
		box.size = Vector3(2.0, 2.0, 3.0)
		mesh = box
	_mesh_instance.mesh = mesh

	_container_material = StandardMaterial3D.new()
	_container_material.albedo_color = Color(0.95, 0.5, 0.05)
	_mesh_instance.material_override = _container_material

	_viewport_container.gui_input.connect(_on_viewport_input)
	_box_break_area.add_child(_viewport_container)


func _load_box(index: int) -> void:
	_current_box_index = index
	_clicks_on_current = 0
	_is_animating      = false

	_container_auto_rotate.rotation = Vector3.ZERO
	_container_pivot.rotation       = Vector3.ZERO
	_container_pivot.scale          = Vector3.ONE

	_container_material.albedo_color      = Color(0.95, 0.5, 0.05)
	_container_material.emission_enabled  = false

	_viewport_container.visible   = true
	_click_prompt_label.visible   = true
	_progress_dots_label.visible  = true
	_crack_reveal_label.visible   = false

	var total := GameSession.unidentified_cargo.size()
	_box_count_label.text = "Container %d / %d" % [index + 1, total]
	_update_dots()


func _update_dots() -> void:
	var s := ""
	for i in CLICKS_REQUIRED:
		s += "\u25cf " if i < _clicks_on_current else "\u25cb "
	_progress_dots_label.text = s.strip_edges()


func _on_viewport_input(event: InputEvent) -> void:
	if not (event is InputEventMouseButton):
		return
	var mb := event as InputEventMouseButton
	if mb.button_index != MOUSE_BUTTON_LEFT or not mb.pressed:
		return
	if _is_animating or _phase != Phase.BOX_BREAK:
		return

	_clicks_on_current += 1
	_update_dots()

	if _clicks_on_current >= CLICKS_REQUIRED:
		SoundManager.play("ui_container_open")
		_play_crack()
	else:
		SoundManager.play("ui_container_click")
		_play_jiggle(false)


func _play_jiggle(intense: bool) -> void:
	_is_animating = true
	var a := 18.0 if intense else 11.0
	var t := create_tween()
	t.tween_property(_container_pivot, "rotation_degrees:z",  a,        0.04)
	t.tween_property(_container_pivot, "rotation_degrees:z", -a,        0.07)
	t.tween_property(_container_pivot, "rotation_degrees:z",  a * 0.5,  0.05)
	t.tween_property(_container_pivot, "rotation_degrees:z", -a * 0.3,  0.05)
	t.tween_property(_container_pivot, "rotation_degrees:z",  0.0,      0.06)
	t.finished.connect(func(): _is_animating = false)


func _play_crack() -> void:
	_is_animating = true
	var cargo_name: String = GameSession.unidentified_cargo[_current_box_index]
	var cargo_color := _find_cargo_color(cargo_name)

	_container_material.albedo_color              = cargo_color
	_container_material.emission_enabled          = true
	_container_material.emission                  = cargo_color
	_container_material.emission_energy_multiplier = 2.0

	var t := create_tween()
	t.tween_property(_container_pivot, "rotation_degrees:z",  25.0, 0.04)
	t.tween_property(_container_pivot, "rotation_degrees:z", -25.0, 0.04)
	t.tween_property(_container_pivot, "rotation_degrees:z",  20.0, 0.03)
	t.tween_property(_container_pivot, "rotation_degrees:z", -20.0, 0.03)
	t.tween_property(_container_pivot, "rotation_degrees:z",  15.0, 0.03)
	t.tween_property(_container_pivot, "rotation_degrees:z", -15.0, 0.03)
	t.tween_property(_container_pivot, "scale", Vector3.ZERO, 0.18) \
		.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_IN)
	t.tween_callback(func(): _show_reveal(cargo_name, cargo_color))


func _show_reveal(cargo_name: String, cargo_color: Color) -> void:
	_viewport_container.visible  = false
	_progress_dots_label.visible = false
	_click_prompt_label.visible  = false

	_crack_reveal_label.text    = cargo_name
	_crack_reveal_label.modulate = cargo_color
	_crack_reveal_label.visible  = true

	var flash := create_tween()
	flash.tween_property(_crack_reveal_label, "modulate", Color.WHITE * 2.5, 0.06)
	flash.tween_property(_crack_reveal_label, "modulate", cargo_color, 0.25)

	get_tree().create_timer(0.85).timeout.connect(_advance_box)


func _advance_box() -> void:
	var next := _current_box_index + 1
	if next < GameSession.unidentified_cargo.size():
		_load_box(next)
	else:
		get_tree().create_timer(0.2).timeout.connect(_start_resource_fly)


# ── Phase: RESOURCE_FLY ───────────────────────────────────────────────────────

func _start_resource_fly() -> void:
	_phase = Phase.RESOURCE_FLY
	_phase_label.text       = "RESOURCES SECURED"
	_box_break_area.visible = false
	_haul_list.visible      = true
	_card_row.visible       = false

	var combined := _build_combined_haul()

	for child in _haul_list.get_children():
		child.queue_free()

	if combined.is_empty():
		var empty := Label.new()
		empty.text = "No cargo collected."
		empty.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		empty.modulate = Color.GRAY
		_haul_list.add_child(empty)
	else:
		for cargo_name in combined:
			var color := _find_cargo_color(cargo_name)
			var lbl := Label.new()
			lbl.name = "Haul_" + cargo_name
			lbl.text = "%d\u00d7  %s" % [combined[cargo_name], cargo_name]
			lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			lbl.modulate = color
			lbl.add_theme_font_size_override("font_size", 22)
			lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			_haul_list.add_child(lbl)

	get_tree().create_timer(1.0).timeout.connect(_animate_resource_fly)


func _animate_resource_fly() -> void:
	var combined := _build_combined_haul()

	if combined.is_empty():
		get_tree().create_timer(0.3).timeout.connect(_start_cards_in)
		return

	var i := 0
	for cargo_name in combined:
		var count: int   = combined[cargo_name]
		var color        := _find_cargo_color(cargo_name)
		var delay: float = i * 0.28

		var source_label := _haul_list.get_node_or_null("Haul_" + cargo_name) as Label
		if source_label == null:
			i += 1
			continue

		var src_pos := source_label.global_position + source_label.size * 0.5

		var target_label := _resource_labels.get_node_or_null("Res_" + cargo_name) as Label
		var dst_pos: Vector2
		if target_label != null:
			dst_pos = target_label.global_position + target_label.size * 0.5
		else:
			dst_pos = Vector2(get_viewport_rect().size.x * 0.5, 30.0)

		# Fade out the haul label
		var fade_tween := create_tween()
		fade_tween.tween_interval(delay * 0.4)
		fade_tween.tween_property(source_label, "modulate:a", 0.0, 0.25)

		# Create flying clone on AfterAction so it can leave the layout
		var fly_lbl := Label.new()
		fly_lbl.text = "%d\u00d7 %s" % [count, cargo_name]
		fly_lbl.modulate = color
		fly_lbl.add_theme_font_size_override("font_size", 18)
		add_child(fly_lbl)
		fly_lbl.global_position = src_pos - Vector2(40.0, 10.0)

		var cap_name  := cargo_name
		var cap_count := count
		var cap_target := target_label

		var fly_tween := create_tween()
		fly_tween.tween_interval(delay)
		fly_tween.tween_property(fly_lbl, "global_position", dst_pos - Vector2(40.0, 10.0), 0.45) \
			.set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_IN)
		fly_tween.tween_callback(func():
			var existing: int = GameSession.player_resources.get(cap_name, 0)
			GameSession.player_resources[cap_name] = existing + cap_count
			_update_resource_label(cap_name)
			SoundManager.play("ui_resource_arrive")

			if cap_target != null and is_instance_valid(cap_target):
				var pulse := create_tween()
				pulse.tween_property(cap_target, "scale", Vector2(1.35, 1.35), 0.07)
				pulse.tween_property(cap_target, "scale", Vector2.ONE,          0.16)

			fly_lbl.queue_free()
		)

		i += 1

	var total_delay: float = combined.size() * 0.28 + 0.7
	get_tree().create_timer(total_delay).timeout.connect(_start_cards_in)


# ── Phase: PURCHASE ───────────────────────────────────────────────────────────

func _start_cards_in() -> void:
	_phase = Phase.PURCHASE
	_phase_label.text = "CHOOSE UPGRADE"
	_haul_list.visible = false
	_card_row.visible  = true
	_next_raid_button.disabled     = false
	_save_and_quit_button.disabled = false

	var affordable := []
	for u in GameConfig.upgrades:
		if can_afford(u):
			affordable.append(u)
	affordable.shuffle()

	_cards.clear()
	for i in 3:
		var def = affordable[i] if i < affordable.size() else null
		var card := UpgradeCard.new()
		card.setup(def, self)
		card.custom_minimum_size = Vector2(210.0, 230.0)
		_card_row.add_child(card)
		_cards.append(card)

		# Slide in from below (staggered)
		card.position += Vector2(0.0, 320.0)
		var slide_delay: float = i * 0.13
		var slide_tween := create_tween()
		slide_tween.tween_interval(slide_delay)
		slide_tween.tween_property(card, "position:y", card.position.y - 320.0, 0.42) \
			.set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)

		# Flip to reveal after slide settles
		var flip_delay: float = slide_delay + 0.42 + i * 0.08
		var ci := i
		var flip_tween := create_tween()
		flip_tween.tween_interval(flip_delay)
		flip_tween.tween_property(card, "scale:y", 0.0, 0.14)
		flip_tween.tween_callback(func(): _cards[ci].reveal())
		flip_tween.tween_property(card, "scale:y", 1.0, 0.14)


# ── Upgrade Purchase ───────────────────────────────────────────────────────────

func can_afford(u: Dictionary) -> bool:
	for k in u["cost"]:
		var have: int = GameSession.player_resources.get(k, 0)
		if have < u["cost"][k]:
			return false
	return true


func purchase_upgrade(u: Dictionary, card) -> void:
	if not can_afford(u):
		return
	for k in u["cost"]:
		GameSession.player_resources[k] -= u["cost"][k]
		_update_resource_label(k)
	GameConfig.apply_upgrade(u)
	GameSession.applied_upgrades.append(u["id"])
	SoundManager.play("ui_upgrade_buy")
	card.mark_purchased()
	for c in _cards:
		c.refresh_affordability()


# ── Next Raid / Save & Quit ────────────────────────────────────────────────────

func _on_next_raid() -> void:
	GameSession.write_to_save()
	GameSession.reset()
	TrainSpeedManager.reset_speed()
	get_tree().change_scene_to_file("res://scenes/Main.tscn")


func _on_save_and_quit() -> void:
	GameSession.write_to_save()
	get_tree().quit()


# ── Helpers ────────────────────────────────────────────────────────────────────

func _build_combined_haul() -> Dictionary:
	var combined := {}
	for k in GameSession.identified_cargo:
		combined[k] = GameSession.identified_cargo[k]
	for name in GameSession.unidentified_cargo:
		if name in combined:
			combined[name] += 1
		else:
			combined[name] = 1
	return combined
