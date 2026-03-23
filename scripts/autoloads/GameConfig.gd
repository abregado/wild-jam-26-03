## Autoload singleton. Loads config/game_config.json on _ready and exposes typed vars.
## Collision layers: 1=World, 2=Containers, 3=Clamps, 4=Player, 5=Projectiles

extends Node

# ── Inner types ───────────────────────────────────────────────────────────────

class CargoType:
	var name: String = ""
	var color: Color = Color.GRAY
	var is_scrap: bool = false

class StatModifier:
	var stat: String = ""
	var flat: float = 0.0       # additive delta (applied first)
	var multiplier: float = 1.0 # multiplicative scale (applied after all flats)

class UpgradeDefinition:
	var id: String = ""
	var name: String = ""
	var description: String = ""
	var modifiers: Array = []   # Array of StatModifier
	var cost: Dictionary = {}   # cargo_name -> int

# ── Turret ────────────────────────────────────────────────────────────────────
var turret_damage: float = 25.0
var rate_of_fire: float = 3.0
var blast_radius: float = 2.5
var bullet_speed: float = 60.0
var bullet_size: float = 0.55
var trail_thickness: float = 0.18
var turret_tracking_speed: float = 6.0
var burst_count: int = 3
var burst_delay: float = 0.08
var turret_max_pitch_down: float = 20.0
var auto_fire: bool = true

# ── Beacon ────────────────────────────────────────────────────────────────────
var beacon_reload_speed: float = 1.5
var beacon_speed: float = 25.0

# ── Train ─────────────────────────────────────────────────────────────────────
var min_carriages: int = 3
var max_carriages: int = 6
var min_containers_per_carriage: int = 1
var max_containers_per_carriage: int = 3

# ── Clamp setups ──────────────────────────────────────────────────────────────
var clamp_setup_weight_single: float = 0.2
var clamp_setup_weight_double: float = 0.3
var clamp_setup_weight_triple: float = 0.3
var clamp_setup_weight_four: float = 0.2
var single_clamp_hp: float = 80.0
var double_clamp_hp: float = 15.0
var triple_clamp_hp: float = 40.0
var four_clamp_hp: float = 40.0

# ── Containers ────────────────────────────────────────────────────────────────
var container_hitpoints: float = 150.0

# ── Player ────────────────────────────────────────────────────────────────────
var min_relative_velocity: float = -15.0
var max_relative_velocity: float = 5.0
var car_acceleration: float = 5.0
var car_deceleration: float = 8.0
var side_change_time: float = 1.5
var car_drive_height: float = 9.0
var number_pre_scanned_containers: int = 0
var cliff_detection_distance: float = 40.0
var flip_ray_samples: int = 10
var cliff_auto_flip_brake: float = 2.0

# ── Speed ─────────────────────────────────────────────────────────────────────
var base_train_speed: float = 15.0
var speed_increase_per_container: float = 2.0
var turret_range: float = 50.0

# ── Environment ───────────────────────────────────────────────────────────────
var pillar_spacing: float = 20.0
var pillar_x_spread: float = 4.0
var pillar_y_offset: float = -3.5
var spawn_ahead_distance: float = 40.0
var despawn_behind_distance: float = 20.0

# Clouds
var cloud_pool_size: int = 25
var cloud_parallax_factor: float = 0.15
var cloud_spawn_spread: float = 80.0
var cloud_height_min: float = 25.0
var cloud_height_max: float = 55.0
var cloud_size_min: float = 3.0
var cloud_size_max: float = 14.0

# Rock pillars
var rock_pillar_spacing: float = 40.0
var rock_pillar_distance: float = 50.0
var rock_pillar_height_min: float = 8.0
var rock_pillar_height_max: float = 30.0

# Ground rubble
var rubble_pool_size: int = 45
var rubble_spread: float = 35.0
var rubble_size_min: float = 0.3
var rubble_size_max: float = 1.4

# ── Enemies ───────────────────────────────────────────────────────────────────
var max_drones_per_deployer: int = 3
var max_deployers_per_carriage: int = 2
var deployer_cooldown: float = 8.0
var drone_move_speed: float = 10.0
var drone_combat_speed: float = 5.0
var drone_fire_rate: float = 1.2
var car_speed_damage_per_hit: float = 0.3
var drone_height_min: float = 2.0
var drone_height_max: float = 5.0
var drone_hitpoints: float = 25.0
var drone_bullet_speed: float = 35.0
var drone_bullet_size: float = 0.12
var drone_hit_chance: float = 0.6
var shield_block_angle: float = 50.0
var drone_reposition_chance: float = 0.4
var drone_chase_distance: float = 20.0
var drone_max_deployer_distance: float = 50.0

# ── Roof Turrets ──────────────────────────────────────────────────────────────
var roof_turret_hitpoints: float = 30.0
var roof_turret_fire_rate: float = 2.0
var roof_turret_burst_count: int = 3
var roof_turret_burst_interval: float = 4.0
var roof_turret_bullet_speed: float = 30.0
var roof_turret_hit_chance: float = 0.55
var roof_turret_max_range: float = 35.0
var roof_turret_repair_time: float = 12.0
var roof_turret_reactivation_time: float = 5.0

# ── Obstacles ─────────────────────────────────────────────────────────────────
var obstacle_section_min_duration: float = 12.0
var obstacle_section_max_duration: float = 25.0
var obstacle_warning_time: float = 4.0
var obstacle_cube_pool_size: int = 12
var obstacle_cube_spacing: float = 6.0
var cliff_cube_width: float = 9.0

# ── Day / Night ───────────────────────────────────────────────────────────────
var day_night_enabled: bool = true
var day_night_phase_duration: float = 240.0

# ── Cutscene texts ────────────────────────────────────────────────────────────
var cutscene_text_container: String = "Containers hold loot.\nUse a Beacon to see what is inside."
var cutscene_text_scrap: String = "Some containers are filled with useless Scrap.\nIgnore them."
var cutscene_text_clamp: String = "Shoot clamps to detach a container."
var cutscene_text_deployer: String = "Deploys enemy drones to slow you down."
var cutscene_text_turret: String = "Will defend adjacent carriages."
var cutscene_text_caboose: String = "If you fall too far behind the train,\nthe raid is over."
var cutscene_text_final: String = "After each raid you can buy upgrades"

# ── Audio ─────────────────────────────────────────────────────────────────────
var master_volume: float = 1.0
var music_volume: float = 0.7
var sfx_volume: float = 1.0
var music_muted: bool = false
var sfx_muted: bool = false
var music_crossfade_time: float = 2.0
var music_menu: Array = ["menu_theme"]
var music_raid: Array = ["raid_theme"]
var music_after_action: Array = ["after_action_theme"]
var sounds: Dictionary = {}  # id -> filename (without extension)

# ── Cargo & Upgrades ──────────────────────────────────────────────────────────
var cargo_types: Array = []       # Array of CargoType
var upgrades: Array = []          # Array of UpgradeDefinition


func _ready() -> void:
	_load_config()
	print("[GameConfig] Loaded. Carriages: %d-%d, BaseSpeed: %s, TurretRange: %s" % [
		min_carriages, max_carriages, base_train_speed, turret_range])


func _load_config() -> void:
	const PATH := "res://config/game_config.json"
	if not FileAccess.file_exists(PATH):
		push_error("[GameConfig] Config file not found, using defaults.")
		_setup_default_cargo()
		return

	var file := FileAccess.open(PATH, FileAccess.READ)
	if file == null:
		push_error("[GameConfig] Cannot open config file.")
		_setup_default_cargo()
		return

	var json := JSON.new()
	if json.parse(file.get_as_text()) != OK:
		push_error("[GameConfig] JSON parse error: " + json.get_error_message())
		_setup_default_cargo()
		return

	var data: Dictionary = json.data

	var t = data.get("turret", null)
	if t != null:
		turret_damage          = _get_float(t, "damage",                turret_damage)
		rate_of_fire           = _get_float(t, "rate_of_fire",          rate_of_fire)
		blast_radius           = _get_float(t, "blast_radius",          blast_radius)
		bullet_speed           = _get_float(t, "bullet_speed",          bullet_speed)
		bullet_size            = _get_float(t, "bullet_size",           bullet_size)
		trail_thickness        = _get_float(t, "trail_thickness",       trail_thickness)
		turret_tracking_speed  = _get_float(t, "turret_tracking_speed", turret_tracking_speed)
		burst_count            = _get_int  (t, "burst_count",           burst_count)
		burst_delay            = _get_float(t, "burst_delay",           burst_delay)
		turret_max_pitch_down  = _get_float(t, "turret_max_pitch_down", turret_max_pitch_down)
		auto_fire              = _get_bool (t, "auto_fire",             auto_fire)

	var b = data.get("beacon", null)
	if b != null:
		beacon_reload_speed = _get_float(b, "beacon_reload_speed", beacon_reload_speed)
		beacon_speed        = _get_float(b, "beacon_speed",        beacon_speed)

	var tr = data.get("train", null)
	if tr != null:
		min_carriages              = _get_int(tr, "min_carriages",               min_carriages)
		max_carriages              = _get_int(tr, "max_carriages",               max_carriages)
		min_containers_per_carriage = _get_int(tr, "min_containers_per_carriage", min_containers_per_carriage)
		max_containers_per_carriage = _get_int(tr, "max_containers_per_carriage", max_containers_per_carriage)

	var c = data.get("clamp_setups", null)
	if c != null:
		clamp_setup_weight_single = _get_float(c, "single_weight",   clamp_setup_weight_single)
		clamp_setup_weight_double = _get_float(c, "double_weight",   clamp_setup_weight_double)
		clamp_setup_weight_triple = _get_float(c, "triple_weight",   clamp_setup_weight_triple)
		clamp_setup_weight_four   = _get_float(c, "four_weight",     clamp_setup_weight_four)
		single_clamp_hp           = _get_float(c, "single_clamp_hp", single_clamp_hp)
		double_clamp_hp           = _get_float(c, "double_clamp_hp", double_clamp_hp)
		triple_clamp_hp           = _get_float(c, "triple_clamp_hp", triple_clamp_hp)
		four_clamp_hp             = _get_float(c, "four_clamp_hp",   four_clamp_hp)

	var co = data.get("containers", null)
	if co != null:
		container_hitpoints = _get_float(co, "container_hitpoints", container_hitpoints)

	var p = data.get("player", null)
	if p != null:
		min_relative_velocity          = _get_float(p, "min_relative_velocity",         min_relative_velocity)
		max_relative_velocity          = _get_float(p, "max_relative_velocity",         max_relative_velocity)
		car_acceleration               = _get_float(p, "car_acceleration",              car_acceleration)
		car_deceleration               = _get_float(p, "car_deceleration",              car_deceleration)
		side_change_time               = _get_float(p, "side_change_time",              side_change_time)
		car_drive_height               = _get_float(p, "car_drive_height",              car_drive_height)
		number_pre_scanned_containers  = _get_int  (p, "number_pre_scanned_containers", number_pre_scanned_containers)
		cliff_detection_distance       = _get_float(p, "cliff_detection_distance",      cliff_detection_distance)
		flip_ray_samples               = _get_int  (p, "flip_ray_samples",              flip_ray_samples)
		cliff_auto_flip_brake          = _get_float(p, "cliff_auto_flip_brake",         cliff_auto_flip_brake)

	var s = data.get("speed", null)
	if s != null:
		base_train_speed           = _get_float(s, "base_train_speed",            base_train_speed)
		speed_increase_per_container = _get_float(s, "speed_increase_per_container", speed_increase_per_container)
		turret_range               = _get_float(s, "turret_range",                turret_range)

	var e = data.get("environment", null)
	if e != null:
		pillar_spacing          = _get_float(e, "pillar_spacing",           pillar_spacing)
		pillar_x_spread         = _get_float(e, "pillar_x_spread",          pillar_x_spread)
		pillar_y_offset         = _get_float(e, "pillar_y_offset",          pillar_y_offset)
		spawn_ahead_distance    = _get_float(e, "spawn_ahead_distance",     spawn_ahead_distance)
		despawn_behind_distance = _get_float(e, "despawn_behind_distance",  despawn_behind_distance)
		cloud_pool_size         = _get_int  (e, "cloud_pool_size",          cloud_pool_size)
		cloud_parallax_factor   = _get_float(e, "cloud_parallax_factor",    cloud_parallax_factor)
		cloud_spawn_spread      = _get_float(e, "cloud_spawn_spread",       cloud_spawn_spread)
		cloud_height_min        = _get_float(e, "cloud_height_min",         cloud_height_min)
		cloud_height_max        = _get_float(e, "cloud_height_max",         cloud_height_max)
		cloud_size_min          = _get_float(e, "cloud_size_min",           cloud_size_min)
		cloud_size_max          = _get_float(e, "cloud_size_max",           cloud_size_max)
		rock_pillar_spacing     = _get_float(e, "rock_pillar_spacing",      rock_pillar_spacing)
		rock_pillar_distance    = _get_float(e, "rock_pillar_distance",     rock_pillar_distance)
		rock_pillar_height_min  = _get_float(e, "rock_pillar_height_min",   rock_pillar_height_min)
		rock_pillar_height_max  = _get_float(e, "rock_pillar_height_max",   rock_pillar_height_max)
		rubble_pool_size        = _get_int  (e, "rubble_pool_size",         rubble_pool_size)
		rubble_spread           = _get_float(e, "rubble_spread",            rubble_spread)
		rubble_size_min         = _get_float(e, "rubble_size_min",          rubble_size_min)
		rubble_size_max         = _get_float(e, "rubble_size_max",          rubble_size_max)

	var en = data.get("enemies", null)
	if en != null:
		max_drones_per_deployer       = _get_int  (en, "max_drones_per_deployer",        max_drones_per_deployer)
		max_deployers_per_carriage    = _get_int  (en, "max_deployers_per_carriage",     max_deployers_per_carriage)
		deployer_cooldown             = _get_float(en, "deployer_cooldown",              deployer_cooldown)
		drone_move_speed              = _get_float(en, "drone_move_speed",               drone_move_speed)
		drone_combat_speed            = _get_float(en, "drone_combat_speed",             drone_combat_speed)
		drone_fire_rate               = _get_float(en, "drone_fire_rate",                drone_fire_rate)
		car_speed_damage_per_hit      = _get_float(en, "car_speed_damage_per_hit",       car_speed_damage_per_hit)
		drone_height_min              = _get_float(en, "drone_height_min",               drone_height_min)
		drone_height_max              = _get_float(en, "drone_height_max",               drone_height_max)
		drone_hitpoints               = _get_float(en, "drone_hitpoints",                drone_hitpoints)
		drone_bullet_speed            = _get_float(en, "drone_bullet_speed",             drone_bullet_speed)
		drone_bullet_size             = _get_float(en, "drone_bullet_size",              drone_bullet_size)
		drone_hit_chance              = _get_float(en, "drone_hit_chance",               drone_hit_chance)
		shield_block_angle            = _get_float(en, "shield_block_angle",             shield_block_angle)
		drone_reposition_chance       = _get_float(en, "drone_reposition_chance",        drone_reposition_chance)
		drone_chase_distance          = _get_float(en, "drone_chase_distance",           drone_chase_distance)
		drone_max_deployer_distance   = _get_float(en, "drone_max_deployer_distance",    drone_max_deployer_distance)
		roof_turret_hitpoints         = _get_float(en, "roof_turret_hitpoints",          roof_turret_hitpoints)
		roof_turret_fire_rate         = _get_float(en, "roof_turret_fire_rate",          roof_turret_fire_rate)
		roof_turret_burst_count       = _get_int  (en, "roof_turret_burst_count",        roof_turret_burst_count)
		roof_turret_burst_interval    = _get_float(en, "roof_turret_burst_interval",     roof_turret_burst_interval)
		roof_turret_bullet_speed      = _get_float(en, "roof_turret_bullet_speed",       roof_turret_bullet_speed)
		roof_turret_hit_chance        = _get_float(en, "roof_turret_hit_chance",         roof_turret_hit_chance)
		roof_turret_max_range         = _get_float(en, "roof_turret_max_range",          roof_turret_max_range)
		roof_turret_repair_time       = _get_float(en, "roof_turret_repair_time",        roof_turret_repair_time)
		roof_turret_reactivation_time = _get_float(en, "roof_turret_reactivation_time",  roof_turret_reactivation_time)

	var o = data.get("obstacles", null)
	if o != null:
		obstacle_section_min_duration = _get_float(o, "section_min_duration", obstacle_section_min_duration)
		obstacle_section_max_duration = _get_float(o, "section_max_duration", obstacle_section_max_duration)
		obstacle_warning_time         = _get_float(o, "warning_time",         obstacle_warning_time)
		obstacle_cube_pool_size       = _get_int  (o, "cube_pool_size",       obstacle_cube_pool_size)
		obstacle_cube_spacing         = _get_float(o, "cube_spacing",         obstacle_cube_spacing)
		cliff_cube_width              = _get_float(o, "cliff_cube_width",     cliff_cube_width)

	var dn = data.get("day_night", null)
	if dn != null:
		day_night_enabled       = _get_bool (dn, "enabled",        day_night_enabled)
		day_night_phase_duration = _get_float(dn, "phase_duration", day_night_phase_duration)

	var a = data.get("audio", null)
	if a != null:
		master_volume        = _get_float(a, "master_volume",       master_volume)
		music_volume         = _get_float(a, "music_volume",        music_volume)
		sfx_volume           = _get_float(a, "sfx_volume",          sfx_volume)
		music_muted          = _get_bool (a, "music_muted",         music_muted)
		sfx_muted            = _get_bool (a, "sfx_muted",           sfx_muted)
		music_crossfade_time = _get_float(a, "music_crossfade_time", music_crossfade_time)

		var mm_v = a.get("music_menu", null)
		if mm_v != null:
			music_menu.clear()
			for track in mm_v: music_menu.append(str(track))

		var mr_v = a.get("music_raid", null)
		if mr_v != null:
			music_raid.clear()
			for track in mr_v: music_raid.append(str(track))

		var ma_v = a.get("music_after_action", null)
		if ma_v != null:
			music_after_action.clear()
			for track in ma_v: music_after_action.append(str(track))

		var s_v = a.get("sounds", null)
		if s_v != null:
			sounds.clear()
			for key in s_v:
				sounds[str(key)] = str(s_v[key])

	var cs = data.get("cutscene", null)
	if cs != null:
		cutscene_text_container = _get_string(cs, "text_container", cutscene_text_container)
		cutscene_text_scrap     = _get_string(cs, "text_scrap",     cutscene_text_scrap)
		cutscene_text_clamp     = _get_string(cs, "text_clamp",     cutscene_text_clamp)
		cutscene_text_deployer  = _get_string(cs, "text_deployer",  cutscene_text_deployer)
		cutscene_text_turret    = _get_string(cs, "text_turret",    cutscene_text_turret)
		cutscene_text_caboose   = _get_string(cs, "text_caboose",   cutscene_text_caboose)
		cutscene_text_final     = _get_string(cs, "text_final",     cutscene_text_final)

	var cargo_var = data.get("cargo_types", null)
	if cargo_var != null:
		cargo_types.clear()
		for item in cargo_var:
			var ct_name: String = item.get("name", "")
			var color_hex: String = item.get("color", "#888888")
			var is_scrap: bool = item.get("is_scrap", false)
			if Color.html_is_valid(color_hex):
				var ct := CargoType.new()
				ct.name = ct_name
				ct.color = Color.html(color_hex)
				ct.is_scrap = is_scrap
				cargo_types.append(ct)

	if cargo_types.is_empty():
		_setup_default_cargo()

	var upgrades_var = data.get("upgrades", null)
	if upgrades_var != null:
		upgrades.clear()
		for item in upgrades_var:
			var u := UpgradeDefinition.new()
			u.id          = item.get("id",          "")
			u.name        = item.get("name",        "")
			u.description = item.get("description", "")

			var mods_v = item.get("modifiers", null)
			if mods_v != null:
				for mod_item in mods_v:
					var m := StatModifier.new()
					m.stat       = mod_item.get("stat",       "")
					m.flat       = float(mod_item.get("flat",       0.0))
					m.multiplier = float(mod_item.get("multiplier", 1.0))
					u.modifiers.append(m)

			var cost_v = item.get("cost", null)
			if cost_v != null:
				for key in cost_v:
					u.cost[str(key)] = int(cost_v[key])

			upgrades.append(u)


## Applies an upgrade's modifiers to the live config values.
## All flat deltas are added first, then all multipliers are applied.
func apply_upgrade(u: UpgradeDefinition) -> void:
	for m in u.modifiers:
		if m.flat != 0.0:
			_apply_flat(m.stat, m.flat)
	for m in u.modifiers:
		if m.multiplier != 1.0:
			_apply_mult(m.stat, m.multiplier)


func _apply_flat(stat: String, v: float) -> void:
	match stat:
		"turret_tracking_speed":       turret_tracking_speed   += v
		"turret_damage":               turret_damage           += v
		"burst_count":                 burst_count              = maxi(1, burst_count + int(v))
		"burst_delay":                 burst_delay             += v
		"rate_of_fire":                rate_of_fire            += v
		"bullet_speed":                bullet_speed            += v
		"beacon_reload_speed":         beacon_reload_speed     += v
		"max_relative_velocity":       max_relative_velocity   += v
		"number_pre_scanned_containers": number_pre_scanned_containers = maxi(0, number_pre_scanned_containers + int(v))
		"blast_radius":                blast_radius            += v
		"shield_block_angle":          shield_block_angle      += v
		"car_speed_damage_per_hit":    car_speed_damage_per_hit += v


func _apply_mult(stat: String, m: float) -> void:
	match stat:
		"turret_tracking_speed":    turret_tracking_speed   *= m
		"turret_damage":            turret_damage           *= m
		"burst_count":              burst_count              = maxi(1, int(burst_count * m))
		"burst_delay":              burst_delay             *= m
		"rate_of_fire":             rate_of_fire            *= m
		"bullet_speed":             bullet_speed            *= m
		"beacon_reload_speed":      beacon_reload_speed     *= m
		"max_relative_velocity":    max_relative_velocity   *= m
		"car_drive_height":         car_drive_height        *= m
		"blast_radius":             blast_radius            *= m
		"shield_block_angle":       shield_block_angle      *= m
		"car_speed_damage_per_hit": car_speed_damage_per_hit *= m


func _setup_default_cargo() -> void:
	cargo_types.clear()
	var defaults := [
		["Electronics", "#4488FF", false],
		["Chemicals",   "#44FF88", false],
		["Weapons",     "#FF4444", false],
		["Credits",     "#FFCC00", false],
		["Scrap",       "#888888", true ],
	]
	for d in defaults:
		var ct := CargoType.new()
		ct.name    = d[0]
		ct.color   = Color.html(d[1])
		ct.is_scrap = d[2]
		cargo_types.append(ct)


# ── JSON helpers ──────────────────────────────────────────────────────────────

func _get_string(d: Dictionary, key: String, fallback: String) -> String:
	return str(d[key]) if key in d else fallback

func _get_float(d: Dictionary, key: String, fallback: float) -> float:
	return float(d[key]) if key in d else fallback

func _get_int(d: Dictionary, key: String, fallback: int) -> int:
	return int(d[key]) if key in d else fallback

func _get_bool(d: Dictionary, key: String, fallback: bool) -> bool:
	return bool(d[key]) if key in d else fallback
