# Cutscene System

Two camera sequences play at the start of `Main.tscn` before handing control to the player.

---

## Files

| File | Role |
|---|---|
| `scripts/CutsceneManager.gd` | Main controller — `play_cutscene()` and `play_fly_in()` |
| `scripts/ui/RingIndicator.gd` | Screen-space bouncing ring drawn around a 3-D target |
| `scripts/autoloads/GameSession.gd` | `is_first_raid` flag + `mark_raid_started()` |
| `scripts/autoloads/GameConfig.gd` | `cutscene_text_*` properties |
| `config/game_config.json` | `"cutscene"` section — all waypoint text strings |

---

## Which sequence plays

`CutsceneManager._ready()` reads `GameSession.is_first_raid`:

```
is_first_raid == true  →  play_cutscene()   (first-time tutorial)
is_first_raid == false →  play_fly_in()     (subsequent raids)
```

`is_first_raid` starts `true`. `mark_raid_started()` is called at the top of the first-raid path, setting it `false` before the scene can be reloaded. `GameSession` is an autoload so it persists across `get_tree().change_scene_to_file()` calls; `reset()` does **not** touch this flag.

---

## First-raid cutscene — `PlayCutscene()`

Full tutorial sequence (~30 s, skippable per waypoint with Space or LMB).

### Sequence

1. **Initial sweep** — camera moves from behind the caboose to a locomotive overview.
2. **Waypoints** — camera visits up to 6 points in front-to-back Z order, pausing at each to show a text panel (right half of screen) and a `RingIndicator` around the focused object:
   - Waypoint A: frontmost non-Scrap container on the left side
   - Waypoint B: first Scrap container behind Waypoint A on the left side
   - Waypoint C: topmost alive clamp on the first left container
   - Waypoint D: a random `DeployerNode` on the train
   - Waypoint E: a random `RoofTurretNode` on the train
   - Waypoint F: the caboose
3. **Player car reveal** — car placed at `(XOffset, YHeight, locoZ − 4)`, camera sweeps from behind the caboose then focuses on the car.
4. **Final text** — `CutsceneTextFinal`, player dismisses.
5. **Camera blend** — cutscene camera tweens to player camera world position/orientation, then `_playerCamera.MakeCurrent()`.

### Waypoint text keys (`config/game_config.json → "cutscene"`)

| Key | Waypoint |
|---|---|
| `text_container` | Non-Scrap container |
| `text_scrap` | Scrap container |
| `text_clamp` | Clamp |
| `text_deployer` | Deployer |
| `text_turret` | Roof turret |
| `text_caboose` | Caboose |
| `text_final` | Player car reveal |

---

## Subsequent-raid fly-in — `PlayFlyIn()`

Short cinematic (~4 s), no text or ring.

### Sequence

1. Camera starts behind and above the caboose (`-22, 22, cabooseZ − 18`), looking at the player car's start position.
2. Sweeps up and over the middle of the train (`4, 26, midZ`).
3. Descends to the player camera's world position while rotating to match its view direction.
4. `_playerCamera.MakeCurrent()` — seamless handover.

---

## Camera blend technique (both sequences)

At handover, the cutscene camera is tweened to the player camera's **exact** world position and orientation before switching, eliminating any visual pop:

```gdscript
var player_cam_pos := _player_camera.global_position
var player_cam_look := player_cam_pos - _player_camera.global_basis.z * 20.0
_desired_look = player_cam_look
await _move_to(player_cam_pos, duration)
_smooth_look = player_cam_look   # eliminate residual lerp error
_player_camera.make_current()
```

---

## Smooth camera rotation

`_Process` lerps `_smoothLook` toward `_desiredLook` every frame:

```gdscript
_smooth_look = _smooth_look.lerp(_desired_look, delta * LOOK_LERP_SPEED)  # LOOK_LERP_SPEED = 2.8
_cam.look_at(_smooth_look, Vector3.UP)
```

`_desiredLook` is **always set before `MoveTo()`** so rotation begins blending while the camera is still travelling to the new position.

---

## Analytical framing — `ComputeFraming`

Places the focused object at **(0.25 W, 0.5 H)** — the centre of the left screen half.

```gdscript
var cam_right := Vector3.UP.cross(-forward).normalized()
var half_fov  := deg_to_rad(fov_deg * 0.5)
var look_at   := target + cam_right * (0.5 * tan(half_fov) * dist)
```

Camera offset vectors used per target type:

| Target | Camera offset |
|---|---|
| Container, clamp, deployer, turret | `(-12, 5, 2)` |
| Caboose | `(-10, 6, -8)` |
| Player car (focus shot) | `(16, 5, 3)` |

---

## Ring indicator — `RingIndicator.gd`

Screen-space `Control` node that draws a bouncing gold arc around a 3-D target.

- `target` — the `Node3D` to track (set by `show_text`).
- `world_radius` — world-space radius of the target; drives pixel ring size.

Pixel radius computed each frame:

```
pixel_radius = (world_radius / dist) * (view_width / (2 * tan(half_fov))) + 12
```

Clamped to [20, 350]. Per-target values:

| Target | world_radius |
|---|---|
| Container, player car | 1.5 |
| Deployer, roof turret | 0.7 |
| Clamp | 0.5 |
| Caboose | 4.5 |

---

## What is suppressed during the cutscene

| System | How |
|---|---|
| Player input | `PlayerCar.disable_input()` → `_input_enabled = false`, `_capture_desired = false` |
| HUD | `_hud.visible = false` |
| Obstacle spawning | `ObstacleSystem.process_mode = PROCESS_MODE_DISABLED` (cascades to all pool children) |
| Out-of-range countdown | `LevelManager._cutscene_active = true`; `_process` returns early |
| Pre-scan (container tagging) | Deferred to `LevelManager.on_cutscene_done()` so containers appear untagged during tutorial |

---

## Tuning constants

| Constant | Location | Default | Effect |
|---|---|---|---|
| `SWEEP_TIME` | `CutsceneManager.gd` | 1.2 s | Travel time between waypoints |
| `HOLD_TIME` | `CutsceneManager.gd` | 3.5 s | Max pause per waypoint |
| `LOOK_LERP_SPEED` | `CutsceneManager.gd` | 2.8 | Camera rotation blend speed |
| Fly-in arc duration | `_play_fly_in()` inline | 2.2 s | Time to reach arc apex |
| Fly-in descent duration | `_play_fly_in()` inline | 2.0 s | Time from apex to player cam |
| First-raid blend duration | `_play_cutscene()` inline | 1.2 s | Final glide to player cam |
