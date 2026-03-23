## Controls the day/night cycle. Attach as a child of Main.
##
## Time is divided into two equal phases of day_night_phase_duration seconds each:
##   Phase 0 (0 → PhaseDuration)  : noon → sunset
##   Phase 1 (PhaseDuration → 2×) : sunset → night
## After both phases the scene stays locked at night. reset() restarts from noon.
##
## Drives:
##   - Sun (DirectionalLight3D): elevation angle, colour, energy
##   - WorldEnvironment.Environment: ambient colour + energy, sky top colour

extends Node

# ── Sun elevation angles (degrees; negative = above horizon) ─────────────────
const ELEV_NOON   := -60.0
const ELEV_SUNSET :=  -5.0
const ELEV_NIGHT  :=  25.0  # well below horizon
const SUN_AZIMUTH :=  30.0  # fixed horizontal angle

# ── Colour keyframes ──────────────────────────────────────────────────────────
const SUN_COLOR_NOON   := Color(1.00, 0.92, 0.78)
const SUN_COLOR_SUNSET := Color(1.00, 0.38, 0.08)
const SUN_COLOR_NIGHT  := Color(0.03, 0.03, 0.10)

const AMB_NOON   := Color(0.90, 0.75, 0.55)
const AMB_SUNSET := Color(0.65, 0.30, 0.08)
const AMB_NIGHT  := Color(0.04, 0.07, 0.18)

const SKY_NOON   := Color(0.15, 0.35, 0.70)
const SKY_SUNSET := Color(0.10, 0.04, 0.12)
const SKY_NIGHT  := Color(0.01, 0.01, 0.04)

# ── Energy keyframes ──────────────────────────────────────────────────────────
const SUN_ENERGY_NOON   := 1.4
const SUN_ENERGY_SUNSET := 0.8
const SUN_ENERGY_NIGHT  := 0.0

const AMB_ENERGY_NOON   := 0.30
const AMB_ENERGY_SUNSET := 0.45
const AMB_ENERGY_NIGHT  := 0.65

# ── State ─────────────────────────────────────────────────────────────────────
var _sun: DirectionalLight3D = null
var _env: Environment = null
var _sky_mat: ProceduralSkyMaterial = null
var _elapsed: float = 0.0
var _phase_duration: float = 240.0
var _enabled: bool = true


func _ready() -> void:
	_enabled        = GameConfig.day_night_enabled
	_phase_duration = GameConfig.day_night_phase_duration

	_sun = get_parent().get_node("Sun") as DirectionalLight3D
	var we := get_parent().get_node("WorldEnvironment") as WorldEnvironment
	_env = we.environment
	if _env.sky != null:
		_sky_mat = _env.sky.sky_material as ProceduralSkyMaterial

	_apply_time(0.0)


## Called at the start of each new raid to reset to noon.
func reset() -> void:
	_elapsed = 0.0
	_apply_time(0.0)


func _process(delta: float) -> void:
	if not _enabled:
		return
	var total := _phase_duration * 2.0
	if _elapsed >= total:
		return
	_elapsed = minf(_elapsed + delta, total)
	_apply_time(_elapsed)


func _apply_time(t: float) -> void:
	var total := _phase_duration * 2.0
	var t_norm := clampf(t / total, 0.0, 1.0) if total > 0.0 else 0.0

	var phase0 := clampf(t_norm * 2.0,       0.0, 1.0)  # 0→1 during first half
	var phase1 := clampf(t_norm * 2.0 - 1.0, 0.0, 1.0)  # 0→1 during second half

	# Sun elevation: noon → sunset → night
	var elev: float
	if t_norm < 0.5:
		elev = lerpf(ELEV_NOON, ELEV_SUNSET, phase0)
	else:
		elev = lerpf(ELEV_SUNSET, ELEV_NIGHT, phase1)
	_sun.rotation_degrees = Vector3(elev, SUN_AZIMUTH, 0.0)

	# Sun colour and energy
	var sun_col: Color
	if t_norm < 0.5:
		sun_col = SUN_COLOR_NOON.lerp(SUN_COLOR_SUNSET, phase0)
	else:
		sun_col = SUN_COLOR_SUNSET.lerp(SUN_COLOR_NIGHT, phase1)
	_sun.light_color = sun_col
	_sun.light_energy = lerpf(SUN_ENERGY_NOON, SUN_ENERGY_SUNSET, phase0) if t_norm < 0.5 \
		else lerpf(SUN_ENERGY_SUNSET, SUN_ENERGY_NIGHT, phase1)

	# Ambient
	var amb_col: Color
	if t_norm < 0.5:
		amb_col = AMB_NOON.lerp(AMB_SUNSET, phase0)
	else:
		amb_col = AMB_SUNSET.lerp(AMB_NIGHT, phase1)
	_env.ambient_light_color = amb_col
	_env.ambient_light_energy = lerpf(AMB_ENERGY_NOON, AMB_ENERGY_SUNSET, phase0) if t_norm < 0.5 \
		else lerpf(AMB_ENERGY_SUNSET, AMB_ENERGY_NIGHT, phase1)

	# Sky top colour
	if _sky_mat != null:
		var sky_col: Color
		if t_norm < 0.5:
			sky_col = SKY_NOON.lerp(SKY_SUNSET, phase0)
		else:
			sky_col = SKY_SUNSET.lerp(SKY_NIGHT, phase1)
		_sky_mat.sky_top_color = sky_col
