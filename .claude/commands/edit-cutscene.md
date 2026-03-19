Work on the cutscene system. Before making any changes, read the relevant files to understand the current state.

## Orientation — read these files first

- `scripts/CutsceneManager.cs` — main controller; contains both `PlayCutscene()` (first raid) and `PlayFlyIn()` (subsequent raids)
- `scripts/ui/RingIndicator.cs` — screen-space bouncing ring drawn around a 3-D target during waypoints
- `scripts/autoloads/GameSession.cs` — `IsFirstRaid` flag and `MarkRaidStarted()` live here
- `scripts/autoloads/GameConfig.cs` — `CutsceneText*` properties parsed from JSON
- `config/game_config.json` (`cutscene` section) — all waypoint text strings

## Architecture overview

### First-raid: `PlayCutscene()`
Full tutorial cutscene plays once. Sequence:
1. Camera sweeps from behind the caboose to a locomotive overview.
2. Camera visits up to 6 waypoints in front-to-back Z order (container, scrap, clamp, random deployer, random turret, caboose), pausing at each to show a text panel + ring indicator.
3. Player car is revealed near the front of the train; camera moves behind the caboose then focuses on the car.
4. Final text shown; player presses Space/LMB to dismiss.
5. Camera glides to the player camera's exact world position/orientation, then `MakeCurrent()` switches cameras (seamless blend).

### Subsequent raids: `PlayFlyIn()`
Short cinematic, no text. Sequence:
1. Camera starts behind and above the caboose.
2. Sweeps up and over the middle of the train.
3. Descends to the player camera's world position while rotating to match its view direction.
4. `MakeCurrent()` switches cameras (seamless blend).

### First-raid detection
`GameSession.IsFirstRaid` starts `true`; `CutsceneManager._Ready()` calls `MarkRaidStarted()` before firing `PlayCutscene()`, so it is `false` for every subsequent `Main.tscn` load.

### Camera blend technique
Both sequences end with:
```csharp
Vector3 playerCamPos        = _playerCamera.GlobalPosition;
Vector3 playerCamLookTarget = playerCamPos - _playerCamera.GlobalBasis.Z * 20f;
_desiredLook = playerCamLookTarget;
await MoveTo(playerCamPos, <duration>);
_smoothLook = playerCamLookTarget;   // eliminate residual lerp error
// then _playerCamera.MakeCurrent()
```

### Smooth camera rotation
`_desiredLook` is set **before** each `MoveTo()` so the rotation begins while the camera is already travelling. `_Process` lerps `_smoothLook` toward `_desiredLook` at `LookLerpSpeed = 2.8f`.

### Analytical framing (`ComputeFraming`)
Computes `lookAt` so the target lands at (0.25 W, 0.5 H) — the centre of the left screen half:
```csharp
Vector3 camRight = Vector3.Up.Cross(-forward).Normalized();
Vector3 lookAt   = target + camRight * (0.5f * Mathf.Tan(halfFov) * dist);
```

### Ring indicator sizing
`RingIndicator.WorldRadius` sets the world-space radius; pixel radius is computed each frame:
```
pixelRadius = (WorldRadius / dist) * focalLength + 12
```
Per-target world radii: container/player car = 1.5, deployer/turret = 0.7, clamp = 0.5, caboose = 4.5.

### Waypoint text
All text lives in `config/game_config.json` under `"cutscene"`. Edit values there; no recompile needed.

### During the cutscene
- `PlayerCar.Visible = false`, `DisableInput()` called (`_captureDesired = false` stops mouse-capture polling).
- HUD hidden.
- `ObstacleSystem.ProcessMode = Disabled` (no obstacle spawning).
- `LevelManager._cutsceneActive = true` (suppresses out-of-range countdown).
- `LevelManager.OnCutsceneDone()` is called at the end of both sequences.

## Common tasks

### Change waypoint text
Edit `config/game_config.json` → `"cutscene"` section. Keys: `text_container`, `text_scrap`, `text_clamp`, `text_deployer`, `text_turret`, `text_caboose`, `text_final`.

### Add a new waypoint
In `BuildWaypoints()` in `CutsceneManager.cs`:
1. Find or build the target `Node3D`.
2. Call `ComputeFraming(target.GlobalPosition, <camOffset>, _cam.Fov)` to get `(cam, look)`.
3. Add to `entries` with a world radius appropriate for the target size.
4. Add the text key to `game_config.json` and wire it through `GameConfig.cs` (use `/add-config-field`).

### Adjust camera timing
- `SweepTime` — travel time between waypoints (default 1.2 s).
- `HoldTime` — max seconds per waypoint before auto-advancing (default 3.5 s).
- `LookLerpSpeed` — how fast the camera rotates to face the next target (default 2.8).
- Fly-in durations are inline in `PlayFlyIn()` (arc: 2.2 s, descent: 2.0 s).
- Final blend durations: first-raid 1.2 s, fly-in already ends at player cam position.

### Change fly-in path
Edit the arc waypoint in `PlayFlyIn()`:
```csharp
Vector3 arcPos = new(4f, 26f, midZ);  // x=right-of-centre, y=height, z=mid-train
await MoveTo(arcPos, 2.2f);
```

## Test checklist
- First scene load: full cutscene plays, text panel visible, ring highlights each target, Space/LMB advances, final blend into player camera is seamless.
- Second+ scene load: fly-in plays (no text), camera arcs over train and blends seamlessly into player camera.
- `IsFirstRaid` is `false` after first cutscene, persists across `GameSession.Reset()`.
- Obstacles do not spawn during either sequence.
- HUD is hidden during cutscene, shown after.
- `LevelManager` does not trigger the out-of-range warning during cutscene.
