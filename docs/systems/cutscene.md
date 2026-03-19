# Cutscene System

Two camera sequences play at the start of `Main.tscn` before handing control to the player.

---

## Files

| File | Role |
|---|---|
| `scripts/CutsceneManager.cs` | Main controller — `PlayCutscene()` and `PlayFlyIn()` |
| `scripts/ui/RingIndicator.cs` | Screen-space bouncing ring drawn around a 3-D target |
| `scripts/autoloads/GameSession.cs` | `IsFirstRaid` flag + `MarkRaidStarted()` |
| `scripts/autoloads/GameConfig.cs` | `CutsceneText*` properties |
| `config/game_config.json` | `"cutscene"` section — all waypoint text strings |

---

## Which sequence plays

`CutsceneManager._Ready()` reads `GameSession.IsFirstRaid`:

```
IsFirstRaid == true  →  PlayCutscene()   (first-time tutorial)
IsFirstRaid == false →  PlayFlyIn()      (subsequent raids)
```

`IsFirstRaid` starts `true`. `MarkRaidStarted()` is called at the top of the first-raid path, setting it `false` before the scene can be reloaded. `GameSession` is an autoload so it persists across `ChangeSceneToFile` calls; `Reset()` does **not** touch this flag.

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

```csharp
Vector3 playerCamPos        = _playerCamera.GlobalPosition;
Vector3 playerCamLookTarget = playerCamPos - _playerCamera.GlobalBasis.Z * 20f;
_desiredLook = playerCamLookTarget;
await MoveTo(playerCamPos, <duration>);
_smoothLook = playerCamLookTarget;   // eliminate residual lerp error
_playerCamera.MakeCurrent();
```

---

## Smooth camera rotation

`_Process` lerps `_smoothLook` toward `_desiredLook` every frame:

```csharp
_smoothLook = _smoothLook.Lerp(_desiredLook, delta * LookLerpSpeed);  // LookLerpSpeed = 2.8
_cam.LookAt(_smoothLook, Vector3.Up);
```

`_desiredLook` is **always set before `MoveTo()`** so rotation begins blending while the camera is still travelling to the new position.

---

## Analytical framing — `ComputeFraming`

Places the focused object at **(0.25 W, 0.5 H)** — the centre of the left screen half.

```csharp
Vector3 camRight = Vector3.Up.Cross(-forward).Normalized();
float   halfFov  = Mathf.DegToRad(fovDeg * 0.5f);
Vector3 lookAt   = target + camRight * (0.5f * Mathf.Tan(halfFov) * dist);
```

Camera offset vectors used per target type:

| Target | Camera offset |
|---|---|
| Container, clamp, deployer, turret | `(-12, 5, 2)` |
| Caboose | `(-10, 6, -8)` |
| Player car (focus shot) | `(16, 5, 3)` |

---

## Ring indicator — `RingIndicator.cs`

Screen-space `Control` node that draws a bouncing gold arc around a 3-D target.

- `Target` — the `Node3D` to track (set by `ShowText`).
- `WorldRadius` — world-space radius of the target; drives pixel ring size.

Pixel radius computed each frame:

```
pixelRadius = (WorldRadius / dist) * (viewWidth / (2 * tan(halfFov))) + 12
```

Clamped to [20, 350]. Per-target values:

| Target | WorldRadius |
|---|---|
| Container, player car | 1.5 |
| Deployer, roof turret | 0.7 |
| Clamp | 0.5 |
| Caboose | 4.5 |

---

## What is suppressed during the cutscene

| System | How |
|---|---|
| Player input | `PlayerCar.DisableInput()` → `_inputEnabled = false`, `_captureDesired = false` |
| HUD | `_hud.Visible = false` |
| Obstacle spawning | `ObstacleSystem.ProcessMode = Disabled` (cascades to all pool children) |
| Out-of-range countdown | `LevelManager._cutsceneActive = true`; `_Process` returns early |
| Pre-scan (container tagging) | Deferred to `LevelManager.OnCutsceneDone()` so containers appear untagged during tutorial |

---

## Tuning constants

| Constant | Location | Default | Effect |
|---|---|---|---|
| `SweepTime` | `CutsceneManager.cs` | 1.2 s | Travel time between waypoints |
| `HoldTime` | `CutsceneManager.cs` | 3.5 s | Max pause per waypoint |
| `LookLerpSpeed` | `CutsceneManager.cs` | 2.8 | Camera rotation blend speed |
| Fly-in arc duration | `PlayFlyIn()` inline | 2.2 s | Time to reach arc apex |
| Fly-in descent duration | `PlayFlyIn()` inline | 2.0 s | Time from apex to player cam |
| First-raid blend duration | `PlayCutscene()` inline | 1.2 s | Final glide to player cam |
