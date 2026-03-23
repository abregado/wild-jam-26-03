## Plays the intro cutscene when Main.tscn first loads.
##
## Sequence (first raid):
##   1. Initial sweep from behind the train to a locomotive overview.
##   2. Camera pans to each waypoint (loco → caboose), showing tutorial text.
##   3. Player car appears at the front; camera sweeps to focus it.
##   4. Final text, then control handed to the player camera.
##
## Fly-in (raids 2+): sweeps from behind the caboose, arcs over the train,
## descends to the player camera position, and blends in — no text.
##
## During the cutscene: PlayerCar hidden + input disabled, HUD hidden,
## ObstacleSystem process disabled, LevelManager cutsceneActive = true.
##
## Space or LMB during any text-hold phase skips to the next waypoint.

extends Node

const LOOK_LERP_SPEED := 2.8
const SWEEP_TIME      := 1.2
const HOLD_TIME       := 3.5
const CARRIAGE_Y_MID  := 8.75   # TrackY(7) + CarriageHeight(2.5)/2

var _cam:            Camera3D    = null
var _ui:             CanvasLayer = null
var _panel:          Panel       = null
var _main_label:     Label       = null
var _continue_label: Label       = null
var _ring                        = null   # RingIndicator

var _train_builder              = null   # TrainBuilder
var _player_car: PlayerCar      = null
var _player_camera: Camera3D    = null
var _hud                        = null
var _obstacle_system: Node3D    = null
var _level_manager              = null   # LevelManager
var _turret_dome: MeshInstance3D = null

var _desired_look: Vector3 = Vector3.ZERO
var _smooth_look:  Vector3 = Vector3.ZERO
var _cutscene_running: bool = false

var _advance_requested: bool = false

const _RingScript = preload("res://scripts/ui/RingIndicator.gd")


func _ready() -> void:
	# ── Cutscene camera ────────────────────────────────────────────────────────
	_cam = Camera3D.new()
	_cam.name = "CutsceneCamera"
	_cam.fov  = 70.0
	add_child(_cam)
	_cam.make_current()

	# ── CanvasLayer for text + ring ────────────────────────────────────────────
	_ui = CanvasLayer.new()
	_ui.name  = "CutsceneUI"
	_ui.layer = 10
	add_child(_ui)

	# Ring — behind the text panel
	_ring = _RingScript.new()
	_ring.name    = "Ring"
	_ring.cam     = _cam
	_ring.visible = false
	_ui.add_child(_ring)

	# Text panel on the right half of the screen
	_panel = Panel.new()
	_panel.name    = "TextPanel"
	_panel.visible = false
	_panel.set_anchor(SIDE_LEFT,   0.52)
	_panel.set_anchor(SIDE_RIGHT,  0.98)
	_panel.set_anchor(SIDE_TOP,    0.30)
	_panel.set_anchor(SIDE_BOTTOM, 0.72)
	_ui.add_child(_panel)

	_main_label = Label.new()
	_main_label.name = "MainLabel"
	_main_label.set_anchor(SIDE_LEFT,   0.0)
	_main_label.set_anchor(SIDE_RIGHT,  1.0)
	_main_label.set_anchor(SIDE_TOP,    0.0)
	_main_label.set_anchor(SIDE_BOTTOM, 0.78)
	_main_label.set_offset(SIDE_LEFT,   16.0)
	_main_label.set_offset(SIDE_RIGHT, -16.0)
	_main_label.set_offset(SIDE_TOP,    16.0)
	_main_label.set_offset(SIDE_BOTTOM,  0.0)
	_main_label.autowrap_mode         = TextServer.AUTOWRAP_WORD_SMART
	_main_label.horizontal_alignment  = HORIZONTAL_ALIGNMENT_CENTER
	_main_label.vertical_alignment    = VERTICAL_ALIGNMENT_CENTER
	_main_label.add_theme_font_size_override("font_size", 22)
	_panel.add_child(_main_label)

	_continue_label = Label.new()
	_continue_label.name = "ContinueLabel"
	_continue_label.set_anchor(SIDE_LEFT,   0.0)
	_continue_label.set_anchor(SIDE_RIGHT,  1.0)
	_continue_label.set_anchor(SIDE_TOP,    0.80)
	_continue_label.set_anchor(SIDE_BOTTOM, 1.0)
	_continue_label.set_offset(SIDE_LEFT,    8.0)
	_continue_label.set_offset(SIDE_RIGHT,  -8.0)
	_continue_label.set_offset(SIDE_BOTTOM, -8.0)
	_continue_label.text               = "SPACE or click to continue"
	_continue_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_continue_label.vertical_alignment   = VERTICAL_ALIGNMENT_BOTTOM
	_continue_label.add_theme_font_size_override("font_size", 13)
	_panel.add_child(_continue_label)

	# ── Scene references ───────────────────────────────────────────────────────
	_train_builder   = get_parent().get_node("Train")
	_player_car      = get_parent().get_node("PlayerCar") as PlayerCar
	_hud             = get_parent().get_node("HUD")
	_obstacle_system = get_parent().get_node("ObstacleSystem") as Node3D
	_level_manager   = get_parent().get_node("LevelManager")
	_player_camera   = _player_car.get_node("Camera3D") as Camera3D
	_turret_dome     = _player_car.get_node_or_null("TurretDome") as MeshInstance3D

	# ── Disable everything until cutscene ends ─────────────────────────────────
	_player_car.visible = false
	_player_car.disable_input()
	_hud.visible = false
	_obstacle_system.process_mode = Node.PROCESS_MODE_DISABLED

	_cutscene_running = true
	if GameSession.is_first_raid:
		GameSession.mark_raid_started()
		_play_cutscene()
	else:
		_play_fly_in()


func _process(delta: float) -> void:
	if not _cutscene_running:
		return
	_smooth_look = _smooth_look.lerp(_desired_look, delta * LOOK_LERP_SPEED)
	if _cam.global_position.distance_to(_smooth_look) > 0.001:
		_cam.look_at(_smooth_look, Vector3.UP)


func _input(event: InputEvent) -> void:
	if not _cutscene_running:
		return
	var space_down := false
	if event is InputEventKey:
		var k := event as InputEventKey
		space_down = k.pressed and not k.echo and k.keycode == KEY_SPACE
	var click_down := false
	if event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		click_down = mb.pressed and mb.button_index == MOUSE_BUTTON_LEFT
	if space_down or click_down:
		_advance_requested = true


# ── Full cutscene (first raid) ─────────────────────────────────────────────────

func _play_cutscene() -> void:
	# Wait one frame so all node transforms are fully propagated
	await get_tree().process_frame

	var loco_z:   float = _train_builder.locomotive_z
	var caboose_z: float = _train_builder.caboose_z

	# Phase 1: Initial sweep from behind the train to loco overview
	var start_pos := Vector3(-22.0, 22.0, caboose_z - 18.0)
	_cam.global_position = start_pos
	_desired_look = Vector3(0.0, CARRIAGE_Y_MID, loco_z - 5.0)
	_smooth_look  = _desired_look
	if _cam.global_position.distance_to(_desired_look) > 0.001:
		_cam.look_at(_desired_look, Vector3.UP)

	await _pause(0.1)

	var loco_view_pos    := Vector3(-10.0, 16.0, loco_z + 8.0)
	var loco_look_target := Vector3(0.0, CARRIAGE_Y_MID + 1.0, loco_z - 14.0)
	_desired_look = loco_look_target
	await _move_to(loco_view_pos, SWEEP_TIME * 2.0)
	await _pause(0.5)

	# Phase 2: Waypoints (camera rotates toward next target while travelling)
	var waypoints := _build_waypoints()
	for wp in waypoints:
		var cam_pos: Vector3     = wp[0]
		var look_target: Vector3 = wp[1]
		var text: String         = wp[2]
		var ring_target: Node3D  = wp[3]
		var world_radius: float  = wp[4]

		_desired_look = look_target
		await _move_to(cam_pos, SWEEP_TIME)
		_show_text(text, ring_target, world_radius)
		await _hold_or_advance(HOLD_TIME)
		_hide_text()

	# Phase 3: Reveal player car
	var player_z: float = loco_z - 4.0
	_player_car.position = Vector3(PlayerCar.X_OFFSET, _player_car.y_height, player_z)
	_player_car.visible  = true
	if _turret_dome != null:
		_turret_dome.visible = true

	var behind_pos := Vector3(0.0, 16.0, caboose_z - 18.0)
	_desired_look = Vector3(PlayerCar.X_OFFSET, _player_car.y_height, player_z)
	await _move_to(behind_pos, 2.0)
	await _pause(0.2)

	var framing := _compute_framing(_player_car.global_position, Vector3(16.0, 5.0, 3.0), _cam.fov)
	_desired_look = framing[1]
	await _move_to(framing[0], 1.5)

	_show_text(GameConfig.cutscene_text_final, _player_car, 1.5)
	await _hold_or_advance(HOLD_TIME)
	_hide_text()
	await _pause(0.2)

	# Blend into player camera
	var player_cam_pos        := _player_camera.global_position
	var player_cam_look_target := player_cam_pos - _player_camera.global_basis.z * 20.0
	_desired_look = player_cam_look_target
	await _move_to(player_cam_pos, 1.2)
	_smooth_look = player_cam_look_target

	# Phase 4: Hand control to player
	_cutscene_running = false
	_panel.visible    = false
	if _turret_dome != null:
		_turret_dome.visible = false

	_player_camera.make_current()
	_player_car.enable_input()
	_hud.visible = true
	_obstacle_system.process_mode = Node.PROCESS_MODE_INHERIT

	_level_manager.on_cutscene_done()
	print("[CutsceneManager] Cutscene complete.")


# ── Fly-in (raids 2+) ──────────────────────────────────────────────────────────

func _play_fly_in() -> void:
	await get_tree().process_frame

	var loco_z:   float = _train_builder.locomotive_z
	var caboose_z: float = _train_builder.caboose_z

	# Place player car so _player_camera.global_transform is valid
	var player_z: float = loco_z - 4.0
	_player_car.position = Vector3(PlayerCar.X_OFFSET, _player_car.y_height, player_z)
	_player_car.visible  = true
	if _turret_dome != null:
		_turret_dome.visible = true

	await get_tree().process_frame

	var player_cam_pos        := _player_camera.global_position
	var player_cam_look_target := player_cam_pos - _player_camera.global_basis.z * 20.0

	# Phase 1: Start behind and above the caboose
	var start_pos := Vector3(-22.0, 22.0, caboose_z - 18.0)
	_cam.global_position = start_pos
	_desired_look = Vector3(PlayerCar.X_OFFSET, _player_car.y_height, player_z)
	_smooth_look  = _desired_look
	if _cam.global_position.distance_to(_desired_look) > 0.001:
		_cam.look_at(_desired_look, Vector3.UP)

	await _pause(0.1)

	# Phase 2: Arc over the middle of the train
	var mid_z: float = (loco_z + caboose_z) * 0.5
	var arc_pos := Vector3(4.0, 26.0, mid_z)
	_desired_look = Vector3(PlayerCar.X_OFFSET, _player_car.y_height, player_z)
	await _move_to(arc_pos, 2.2)

	# Phase 3: Descend toward the player camera
	_desired_look = player_cam_look_target
	await _move_to(player_cam_pos, 2.0)
	_smooth_look = player_cam_look_target

	# Phase 4: Hand control to player
	_cutscene_running = false
	if _turret_dome != null:
		_turret_dome.visible = false

	_player_camera.make_current()
	_player_car.enable_input()
	_hud.visible = true
	_obstacle_system.process_mode = Node.PROCESS_MODE_INHERIT

	_level_manager.on_cutscene_done()
	print("[CutsceneManager] Fly-in complete.")


# ── Waypoint builder ───────────────────────────────────────────────────────────
# Returns Array of [cam_pos, look_target, text, ring_target, world_radius]
# sorted front-to-back (descending Z).

func _build_waypoints() -> Array:
	var entries := []
	var rng := RandomNumberGenerator.new()
	rng.randomize()

	# Containers on the left side (negative X), sorted front-to-back
	var left_containers := []
	for c in _train_builder.all_containers:
		if c.global_position.x < 0.0:
			left_containers.append(c)
	left_containers.sort_custom(func(a, b): return a.global_position.z > b.global_position.z)

	# Waypoint A — first left non-Scrap container (highest Z)
	var non_scrap = null
	for c in left_containers:
		if not c.is_scrap:
			non_scrap = c
			break
	if non_scrap != null:
		var fr := _compute_framing(non_scrap.global_position, Vector3(-12.0, 5.0, 2.0), _cam.fov)
		entries.append([fr[0], fr[1], GameConfig.cutscene_text_container, non_scrap, 1.5, non_scrap.global_position.z])

	# Waypoint B — first left Scrap container behind Waypoint A
	var non_scrap_z: float = non_scrap.global_position.z if non_scrap != null else INF
	var scrap = null
	for c in left_containers:
		if c.is_scrap and c.global_position.z < non_scrap_z:
			scrap = c
			break
	if scrap == null:
		for c in left_containers:
			if c.is_scrap and c != non_scrap:
				scrap = c
				break
	if scrap != null:
		var fr := _compute_framing(scrap.global_position, Vector3(-12.0, 5.0, 2.0), _cam.fov)
		entries.append([fr[0], fr[1], GameConfig.cutscene_text_scrap, scrap, 1.5, scrap.global_position.z])

	# Waypoint C — topmost alive clamp of the first left container
	var first_left = left_containers[0] if left_containers.size() > 0 else null
	if first_left != null:
		var alive_clamps := []
		for c in first_left.clamps:
			if c.is_alive:
				alive_clamps.append(c)
		alive_clamps.sort_custom(func(a, b): return a.global_position.y > b.global_position.y)
		var top_clamp = alive_clamps[0] if alive_clamps.size() > 0 else null
		if top_clamp != null:
			var fr := _compute_framing(top_clamp.global_position, Vector3(-12.0, 5.0, 2.0), _cam.fov)
			entries.append([fr[0], fr[1], GameConfig.cutscene_text_clamp, top_clamp, 0.5, top_clamp.global_position.z])

	# Waypoint D — random deployer
	var deployers: Array = _train_builder.all_deployers
	if deployers.size() > 0:
		var deployer: Node3D = deployers[rng.randi_range(0, deployers.size() - 1)]
		var fr := _compute_framing(deployer.global_position, Vector3(-12.0, 5.0, 2.0), _cam.fov)
		entries.append([fr[0], fr[1], GameConfig.cutscene_text_deployer, deployer, 0.7, deployer.global_position.z])

	# Waypoint E — random roof turret
	var roof_turrets: Array = _train_builder.all_roof_turrets
	if roof_turrets.size() > 0:
		var turret: Node3D = roof_turrets[rng.randi_range(0, roof_turrets.size() - 1)]
		var fr := _compute_framing(turret.global_position, Vector3(-12.0, 5.0, 2.0), _cam.fov)
		entries.append([fr[0], fr[1], GameConfig.cutscene_text_turret, turret, 0.7, turret.global_position.z])

	# Waypoint F — caboose
	if _train_builder.caboose_node != null:
		var caboose_pos: Vector3 = _train_builder.caboose_node.global_position
		var fr := _compute_framing(caboose_pos, Vector3(-10.0, 6.0, -8.0), _cam.fov)
		entries.append([fr[0], fr[1], GameConfig.cutscene_text_caboose, _train_builder.caboose_node, 4.5, caboose_pos.z])

	# Sort descending by Z (index 5) so camera sweeps from loco toward caboose
	entries.sort_custom(func(a, b): return a[5] > b[5])

	# Strip the sort_z column
	var result := []
	for e in entries:
		result.append([e[0], e[1], e[2], e[3], e[4]])
	return result


# ── Framing helper ─────────────────────────────────────────────────────────────
# Places target at (0.25 W, 0.5 H) — centre of the left half.
# Returns [cam_pos, look_at]

static func _compute_framing(target: Vector3, cam_offset: Vector3, fov_deg: float) -> Array:
	var cam_pos  := target + cam_offset
	var to_t     := target - cam_pos
	var dist     := to_t.length()
	var forward  := to_t / dist
	var cam_right := Vector3.UP.cross(-forward).normalized()
	var half_fov  := deg_to_rad(fov_deg * 0.5)
	var look_at   := target + cam_right * (0.5 * tan(half_fov) * dist)
	return [cam_pos, look_at]


# ── Coroutine helpers ──────────────────────────────────────────────────────────

func _move_to(target_pos: Vector3, duration: float) -> void:
	var tween := create_tween()
	tween.tween_property(_cam, "global_position", target_pos, duration) \
		.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	await tween.finished


func _hold_or_advance(max_seconds: float) -> void:
	_advance_requested = false
	var elapsed := 0.0
	while not _advance_requested and elapsed < max_seconds:
		await get_tree().process_frame
		elapsed += get_process_delta_time()
	_advance_requested = false


func _pause(seconds: float) -> void:
	await get_tree().create_timer(seconds).timeout


# ── Text / ring helpers ────────────────────────────────────────────────────────

func _show_text(text: String, ring_target: Node3D = null, world_radius: float = 1.5) -> void:
	_main_label.text   = text
	_panel.visible     = true
	_ring.world_radius = world_radius
	_ring.target       = ring_target
	_ring.visible      = ring_target != null


func _hide_text() -> void:
	_panel.visible = false
	_ring.target   = null
	_ring.visible  = false
