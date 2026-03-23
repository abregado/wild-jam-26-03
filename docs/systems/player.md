# Player System

Scripts: `scripts/player/PlayerCar.gd`, `scripts/player/Turret.gd`, `scripts/player/Shield.gd`

---

## Spatial layout

- Player flies at **X = ±8.0** (right = +8, left = -8), **Y = `CarDriveHeight`** (default 8.0).
- Car body is always fixed at `RotationDegrees.Y = 90` (faces toward train, i.e. -X direction). Never rotates.
- **Camera** is a child of `PlayerCar` and handles all aim: `rotation_degrees = Vector3(pitch, look_yaw, 0)`. `_look_yaw` is relative to the car.
- **Turret** is a child of `Camera3D` — it automatically inherits camera pitch and yaw, so the barrels always follow the crosshair.

---

## Movement

- WASD moves the car projected onto camera forward/right axes.
- Velocity is clamped: Z component (train-relative) clamped by `[MinRelativeVelocity, MaxRelativeVelocity]` from config.
- Acceleration / deceleration from config: `CarAcceleration`, `CarDeceleration`.

---

## Side-switching arcs

- **Space** (`switch_side_over`) — arc over the top of the train.
- **Ctrl** (`switch_side_under`) — arc under the train; only available when `CanSwitchUnder = true`.
- Arc formula:
  - `newX = _arcStartX * cos(t * π)`
  - `newY = YHeight ± arcHeight * sin(t * π)` (+ for over, − for under)
- Arc heights: `OVER_ARC_HEIGHT = 6.0`, `UNDER_ARC_HEIGHT = 6.0` (constants in `PlayerCar.gd`).
- Duration: `GameConfig.SideChangeTime`.
- Camera aim is **unaffected** during the arc — only position moves.
- `CanSwitchUnder` is recalculated every frame by `PredictUnderArcClear()`, which sphere-queries the arc path against layer 9 (flip-path collision from obstacles).

---

## Cliff detection & auto-flip

- `PlayerCar` casts a forward ray each frame to detect obstacle cliff bodies (layer 8).
- If a cliff is detected within `CliffDetectionDistance`, the HUD shows a warning.
- If the cliff is imminent and the player is on the wrong side, `AutoFlip()` fires automatically.
- Auto-flip brakes the car by `CliffAutoFlipBrake` and triggers the appropriate arc.
- `FlipRaySamples` controls how many samples the arc-clearance check uses.

---

## Turret aiming

- Turret slerps toward `-_camera.GlobalTransform.Basis.Z` (camera forward direction) each frame.
- Slerp speed: `TurretTrackingSpeed` from config.
- Pitch clamped: cannot aim below `TurretMaxPitchDown` degrees from horizontal.
- **Alternating barrels**: `_fire_from_left` bool toggles each shot; `active_barrel_tip` property returns the active barrel tip transform.
- Barrel tips in `Turret.tscn`: `BarrelTipLeft` at `(-0.18, -0.42, -0.9)` and `BarrelTipRight` at `(0.18, -0.42, -0.9)`.
- Yellow dot crosshair: raycast from camera position; converges on aim point when tracking catches up.

---

## Burst fire

- Each trigger press fires `BurstCount` bullets.
- `BurstDelay` seconds between shots within a burst.
- `AutoFire = true`: holding the button fires continuous bursts.
- Beacons (right mouse button) use `BeaconReloadSpeed` — one beacon per reload cycle.

---

## Shield

- Transparent sphere around the player, `Shield.gd`.
- Monitors drone bullets (layer 64, mask 64).
- If a bullet's arrival angle is ≤ `ShieldBlockAngle` from camera forward → bullet destroyed, shield flashes blue.
- Otherwise → bullet passes through; applies `CarSpeedDamagePerHit` to player's max forward velocity.

---

## Mouse capture

- `get_window().focus_entered` is connected in `PlayerCar._ready()`.
- `_on_viewport_focus_entered()` cycles `Input.mouse_mode` from `MOUSE_MODE_VISIBLE → MOUSE_MODE_CAPTURED` to recover from OS capture loss.
- Mouse motion handler does **not** call `SetInputAsHandled()`.

---

## HUD node paths

Used by `HUD.gd` and `LevelManager.gd`:

| Node path | Type | Purpose |
|---|---|---|
| `BottomContainer/SpeedBar` | ProgressBar | Relative speed (0=max back, 50=stopped, 100=max fwd) |
| `TopRight/TrainSpeedLabel` | Label | "Train: N u/s" |
| `Warning/WarningLabel` | Label | Level-end warning (hidden by default) |
| `Warning/CountdownLabel` | Label | Countdown timer (hidden by default) |
