# Environment System

Scripts: `scripts/environment/TrackEnvironment.gd`, `scripts/environment/PillarPool.gd`, `scripts/environment/CloudPool.gd`, `scripts/environment/RockPillarPool.gd`, `scripts/environment/RubblePool.gd`, `scripts/environment/ObstaclePool.gd`, `scripts/environment/ObstacleManager.gd`

---

## Scrolling

The train does not move — the **world scrolls backward** to simulate motion.

- Ground and track use `res://scripts/shaders/ground_scroll.gdshader`.
- Shader parameter `scroll_speed` (float, world units/sec) is set each frame from `TrainSpeedManager.CurrentTrainSpeed`.
- Scroll direction: UV.y increases with time → ground appears to move backward (−Z).
- `TrackEnvironment.gd` sets the shader parameter and calls `update()` on all pools each frame.

---

## Pillar pool

`PillarPool.gd`

- 8 pillars (track support columns), spacing from `GameConfig.PillarSpacing`.
- Move in **−Z direction** each frame at train speed.
- Recycle threshold: Z < −`DespawnBehindDistance` → teleport to furthest-ahead + spacing.
- `PillarPool.PillarX = 0f` — centred under track; used by `PlayerCar` for arc prediction.
- Each pillar: `StaticBody3D` on collision layer 1 + `CylinderShape3D` — bullets stop on impact.
- Loads GLB from `assets/models/environment/pillar.glb`; falls back to procedural `CylinderMesh`.

---

## Cloud pool

`CloudPool.gd`

- 25 clouds (configurable via `CloudPoolSize`).
- Scroll at `CloudParallaxFactor × trainSpeed` (parallax — slower than ground).
- Randomised X/Z spread, height (25–55), and size (3–14 units).
- No collision.

---

## Rock pillar pool

`RockPillarPool.gd`

- Distant left/right rock formations, spacing `RockPillarSpacing`.
- Two pools (left side, right side) with half-spacing stagger.
- Randomised heights and widths; desert colour palette.
- No collision.

---

## Rubble pool

`RubblePool.gd`

- Ground-level debris cubes (`RubblePoolSize` = 245 by default).
- Desert colour palette, randomly tilted.
- Avoids ±2 unit band under track (X near 0).
- Moves at full train speed. No collision.

---

## Obstacle system

`ObstacleManager.gd` + `ObstaclePool.gd`

`ObstacleManager` is a finite state machine: `Startup → Warning → Active → Warning → Active → ...`

- **Warning** phase: announces the upcoming obstacle section to the player (HUD warning label).
- **Active** phase: obstacle cubes stream in from the front.

Four `ObstaclePool` instances handle separate zones:
- `LeftCliff` / `RightCliff` — canyon walls (collision layers 8 + 9)
- `Roof` — overhead barrier (layer 9)
- `Plateau` — raised ground platform (layer 9, half height on first spawn)

**Collision detail for cliff zones:**
- Each cliff cube produces two collision bodies:
  - Wide body on **layer 8** — detected by `PlayerCar`'s forward detection ray.
  - Flip-path body on **layer 9** — detected by `PlayerCar.PredictUnderArcClear()`.

**First-spawn phantom gap:** the first obstacle block of a Roof or Cliff section is invisible with no collision, giving the player a moment to react.

---

## Adding a new decoration pool

Use the `/new-environment-pool` skill. The skill walks through creating the pool script, adding config fields, and registering it in `TrackEnvironment.gd`.

---

## Zoom-away (level end)

When `TrainSpeedManager.TriggerZoomAway()` is called:

1. `CurrentTrainSpeed = 0` — environment stops scrolling; shader and all pools freeze.
2. `TrainZoomSpeed = previousSpeed × 10` — stored for physical movement.
3. `LevelManager` translates the Train node in +Z at `TrainZoomSpeed` per second.
4. The train physically pulls away while the environment is stationary.
5. After 2 seconds, scene changes to `AfterAction.tscn`.
