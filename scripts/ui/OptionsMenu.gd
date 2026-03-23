## Options panel. Shown as an overlay from the main menu.
##
## Sections:
##   Volume — master / music / sfx sliders + mute checkboxes
##   Key Bindings — one row per rebindable action; click to rebind
##   Buttons — Save, Cancel
##
## Emits closed when the user is done (Save or Cancel).

extends PanelContainer

signal closed

var _master_slider: HSlider = null
var _music_slider:  HSlider = null
var _sfx_slider:    HSlider = null
var _music_mute:    CheckButton = null
var _sfx_mute:      CheckButton = null

# action name -> Button
var _bind_buttons: Dictionary = {}

var _rebinding_action: String = ""
var _rebinding_button: Button = null

var _snap_master: float = 0.0
var _snap_music:  float = 0.0
var _snap_sfx:    float = 0.0
var _snap_music_muted: bool = false
var _snap_sfx_muted:   bool = false


func _ready() -> void:
	_build_ui()


func open() -> void:
	_snap_master      = SettingsManager.master_volume
	_snap_music       = SettingsManager.music_volume
	_snap_sfx         = SettingsManager.sfx_volume
	_snap_music_muted = SettingsManager.music_muted
	_snap_sfx_muted   = SettingsManager.sfx_muted

	_master_slider.value       = SettingsManager.master_volume
	_music_slider.value        = SettingsManager.music_volume
	_sfx_slider.value          = SettingsManager.sfx_volume
	_music_mute.button_pressed = SettingsManager.music_muted
	_sfx_mute.button_pressed   = SettingsManager.sfx_muted

	for pair in SettingsManager.REBINDABLE_ACTIONS:
		var action: String = pair[1]
		if action in _bind_buttons:
			_bind_buttons[action].text = SettingsManager.get_binding_label(action)

	_cancel_rebind()


# ── UI Construction ────────────────────────────────────────────────────────────

func _build_ui() -> void:
	var scroll := ScrollContainer.new()
	scroll.custom_minimum_size = Vector2(0.0, 480.0)
	add_child(scroll)

	var vbox := VBoxContainer.new()
	vbox.custom_minimum_size = Vector2(640.0, 0.0)
	vbox.add_theme_constant_override("separation", 10)
	scroll.add_child(vbox)

	# ── Volume section ─────────────────────────────────────────────────────────
	_add_section_header(vbox, "VOLUME")

	_master_slider = _add_slider_row(vbox, "Master", 0.0, 1.0, SettingsManager.master_volume,
		func(v): SettingsManager.set_master_volume(v))

	var music_pair := _add_slider_row_mute(vbox, "Music", 0.0, 1.0,
		SettingsManager.music_volume,
		func(v): SettingsManager.set_music_volume(v),
		SettingsManager.music_muted,
		func(m): SettingsManager.set_music_muted(m))
	_music_slider = music_pair[0]
	_music_mute   = music_pair[1]

	var sfx_pair := _add_slider_row_mute(vbox, "SFX", 0.0, 1.0,
		SettingsManager.sfx_volume,
		func(v): SettingsManager.set_sfx_volume(v),
		SettingsManager.sfx_muted,
		func(m): SettingsManager.set_sfx_muted(m))
	_sfx_slider = sfx_pair[0]
	_sfx_mute   = sfx_pair[1]

	# ── Key Bindings section ───────────────────────────────────────────────────
	vbox.add_child(HSeparator.new())
	_add_section_header(vbox, "KEY BINDINGS")

	for pair in SettingsManager.REBINDABLE_ACTIONS:
		var label_text: String = pair[0]
		var action: String     = pair[1]
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 12)
		vbox.add_child(row)

		var name_lbl := Label.new()
		name_lbl.text = label_text
		name_lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(name_lbl)

		var bind_btn := Button.new()
		bind_btn.text = SettingsManager.get_binding_label(action)
		bind_btn.custom_minimum_size = Vector2(130.0, 32.0)
		var captured_action := action
		bind_btn.pressed.connect(func(): _start_rebind(captured_action, bind_btn))
		row.add_child(bind_btn)
		_bind_buttons[action] = bind_btn

	var reset_row := HBoxContainer.new()
	reset_row.alignment = BoxContainer.ALIGNMENT_CENTER
	vbox.add_child(reset_row)
	var reset_btn := Button.new()
	reset_btn.text = "RESET TO DEFAULTS"
	reset_btn.custom_minimum_size = Vector2(200.0, 32.0)
	reset_btn.pressed.connect(_on_reset_bindings)
	reset_row.add_child(reset_btn)

	# ── Save / Cancel ──────────────────────────────────────────────────────────
	vbox.add_child(HSeparator.new())
	var btn_row := HBoxContainer.new()
	btn_row.alignment = BoxContainer.ALIGNMENT_CENTER
	btn_row.add_theme_constant_override("separation", 20)
	vbox.add_child(btn_row)

	var save_btn := Button.new()
	save_btn.text = "SAVE"
	save_btn.custom_minimum_size = Vector2(130.0, 40.0)
	save_btn.pressed.connect(_on_save)
	btn_row.add_child(save_btn)

	var cancel_btn := Button.new()
	cancel_btn.text = "CANCEL"
	cancel_btn.custom_minimum_size = Vector2(130.0, 40.0)
	cancel_btn.pressed.connect(_on_cancel)
	btn_row.add_child(cancel_btn)


func _add_section_header(parent: VBoxContainer, text: String) -> void:
	var lbl := Label.new()
	lbl.text = text
	lbl.add_theme_font_size_override("font_size", 15)
	parent.add_child(lbl)


func _add_slider_row(parent: VBoxContainer, label: String,
		min_val: float, max_val: float, initial: float,
		on_changed: Callable) -> HSlider:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 10)
	parent.add_child(row)

	var lbl := Label.new()
	lbl.text = label
	lbl.custom_minimum_size = Vector2(80.0, 0.0)
	row.add_child(lbl)

	var slider := HSlider.new()
	slider.min_value = min_val
	slider.max_value = max_val
	slider.value = initial
	slider.step = 0.01
	slider.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	slider.custom_minimum_size = Vector2(0.0, 28.0)
	slider.value_changed.connect(on_changed)
	row.add_child(slider)

	var pct_lbl := Label.new()
	pct_lbl.custom_minimum_size = Vector2(40.0, 0.0)
	pct_lbl.text = "%d%%" % int(initial * 100.0)
	slider.value_changed.connect(func(v): pct_lbl.text = "%d%%" % int(v * 100.0))
	row.add_child(pct_lbl)

	return slider


func _add_slider_row_mute(parent: VBoxContainer, label: String,
		min_val: float, max_val: float, initial: float,
		on_changed: Callable,
		mute_initial: bool, mute_cb: Callable) -> Array:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 10)
	parent.add_child(row)

	var lbl := Label.new()
	lbl.text = label
	lbl.custom_minimum_size = Vector2(80.0, 0.0)
	row.add_child(lbl)

	var slider := HSlider.new()
	slider.min_value = min_val
	slider.max_value = max_val
	slider.value = initial
	slider.step = 0.01
	slider.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	slider.custom_minimum_size = Vector2(0.0, 28.0)
	slider.value_changed.connect(on_changed)
	row.add_child(slider)

	var pct_lbl := Label.new()
	pct_lbl.custom_minimum_size = Vector2(40.0, 0.0)
	pct_lbl.text = "%d%%" % int(initial * 100.0)
	slider.value_changed.connect(func(v): pct_lbl.text = "%d%%" % int(v * 100.0))
	row.add_child(pct_lbl)

	var mute_btn := CheckButton.new()
	mute_btn.text = "Mute"
	mute_btn.button_pressed = mute_initial
	mute_btn.toggled.connect(mute_cb)
	row.add_child(mute_btn)

	return [slider, mute_btn]


# ── Rebinding ──────────────────────────────────────────────────────────────────

func _start_rebind(action: String, btn: Button) -> void:
	_cancel_rebind()
	_rebinding_action = action
	_rebinding_button = btn
	btn.text = "Press a key\u2026"


func _cancel_rebind() -> void:
	if _rebinding_button != null and _rebinding_action != "":
		_rebinding_button.text = SettingsManager.get_binding_label(_rebinding_action)
	_rebinding_action = ""
	_rebinding_button = null


func _input(event: InputEvent) -> void:
	if _rebinding_action == "" or not visible:
		return

	if event is InputEventKey:
		var key := event as InputEventKey
		if key.pressed and not key.echo:
			if key.keycode == KEY_ESCAPE:
				_cancel_rebind()
				get_viewport().set_input_as_handled()
				return
			var new_ev := InputEventKey.new()
			new_ev.physical_keycode = key.physical_keycode
			SettingsManager.set_binding(_rebinding_action, new_ev)
			if _rebinding_button != null:
				_rebinding_button.text = SettingsManager.get_binding_label(_rebinding_action)
			_cancel_rebind()
			get_viewport().set_input_as_handled()

	elif event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.pressed:
			var new_ev := InputEventMouseButton.new()
			new_ev.button_index = mb.button_index
			SettingsManager.set_binding(_rebinding_action, new_ev)
			if _rebinding_button != null:
				_rebinding_button.text = SettingsManager.get_binding_label(_rebinding_action)
			_cancel_rebind()
			get_viewport().set_input_as_handled()


# ── Reset bindings ─────────────────────────────────────────────────────────────

func _on_reset_bindings() -> void:
	InputMap.load_from_project_settings()
	for pair in SettingsManager.REBINDABLE_ACTIONS:
		var action: String = pair[1]
		if action in _bind_buttons:
			_bind_buttons[action].text = SettingsManager.get_binding_label(action)
	_cancel_rebind()


# ── Save / Cancel ──────────────────────────────────────────────────────────────

func _on_save() -> void:
	_cancel_rebind()
	SettingsManager.save()
	SoundManager.play("ui_options_save")
	closed.emit()


func _on_cancel() -> void:
	_cancel_rebind()
	SettingsManager.set_master_volume(_snap_master)
	SettingsManager.set_music_volume(_snap_music)
	SettingsManager.set_sfx_volume(_snap_sfx)
	SettingsManager.set_music_muted(_snap_music_muted)
	SettingsManager.set_sfx_muted(_snap_sfx_muted)
	SoundManager.play("ui_options_back")
	closed.emit()
