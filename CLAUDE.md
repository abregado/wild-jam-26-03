# Wild Jam 26-03 — CLAUDE.md

Godot 4.6.1 Mono / C# game. Engine location: `D:\Programs\Godot_v4.6.1-stable_mono_win64\`

---

## Implementation Status

| Step | Status | Notes |
|------|--------|-------|
| 1 — Project Setup & Config | ✅ Done | project.godot, GameConfig.cs, game_config.json |
| 2 — Environment | ✅ Done | TrackEnvironment.tscn, shader, PillarPool |
| 3 — Train Structure | ✅ Done | TrainBuilder.cs, Carriage.tscn |
| 4 — Container & Clamp System | ✅ Done | ContainerNode.cs, ClampNode.cs |
| 5 — Train Speed Manager | ✅ Done | TrainSpeedManager.cs (autoload) |
| 6 — Player Car & Camera | ✅ Done | PlayerCar.tscn, PlayerCar.cs |
| 7 — Turret & Primary Fire | ✅ Done | Turret.cs, Bullet.tscn/cs |
| 8 — Beacon Secondary Fire | ✅ Done | Beacon.tscn/cs |
| 9 — HUD | ✅ Done | HUD.tscn/cs |
| 10 — Level End & Warning | ✅ Done | LevelManager.cs |
| 11 — Game Session & After-Action | ✅ Done | GameSession.cs, AfterAction.tscn/cs |
| 12 — Polish & Config Wiring | ✅ Done | All config fields wired |
| 13 — Enemy Drones | ✅ Done | DeployerNode.cs, DroneNode.cs, DroneBullet.cs, Shield.cs |

---

## Key Conventions

### Axis / Spatial Layout
- **Train runs along Z axis**: Locomotive at HIGHEST +Z, Caboose at lowest Z (≈ Z=0)
- TrainBuilder assembles: Caboose at Z=0 → Carriages → Locomotive at Z=totalLength
- **LocomotiveZ** is exposed by `TrainBuilder` for range checks
- **Player position**: X=8.0 (right side), Y=9.0 (flying height — raised for under-arc clearance)
- **Car body**: always fixed at `RotationDegrees.Y = 90` (faces -X toward train). Never rotates with mouse, never rotates during side-switch arc.
- **Camera**: handles both yaw and pitch via `RotationDegrees = new Vector3(pitch, lookYaw, 0)`. `_lookYaw` starts at 0 (relative to car).
- **Turret**: child of Camera3D (not PlayerCar directly) — automatically inherits camera pitch/yaw
- Player **forward** = increasing Z (toward locomotive)
- Player **backward** = decreasing Z (toward caboose / away from train)
- "Distance behind front" = `LocomotiveZ - playerZ` (positive = behind)

### Environment Scroll
- Ground and track use `ground_scroll.gdshader` at `res://scripts/shaders/ground_scroll.gdshader`
- Shader parameter: `scroll_speed` (float, world units/sec)
- Scroll direction: UV.y increases with time → ground appears to move backward
- Pillar pool: 8 pillars, spacing from `GameConfig.PillarSpacing` (default 20), start at positive Z (ahead), move in **-Z direction** (pass from ahead to behind)
- Pillar recycle threshold: Z < -10 → teleport to furthest-ahead + spacing
- Pillars centred at X=0 (no left/right offset); `PillarPool.PillarX = 0f` is a public const used by PlayerCar for arc prediction
- Each pillar has `StaticBody3D { CollisionLayer=1 }` + `CylinderShape3D` child — bullets stop on impact

### Track / Rail
- Track rail is a 0.5×0.5 box cross-section (`BoxMesh`, `size = Vector3(0.5, 0.5, 1000.0)`)
- `TrackY = 7f` (raised from 3 to give clearance for under-arc)
- Rail mesh position Y = 6.75 so top face is flush at Y=7
- `TrackRailBody` StaticBody3D on collision layer 1 — bullets stop on impact

### Carriage / Container / Clamp Layout
- Carriage length: 12 units, width: 3, height: 2.5; **`CarGap = 0.5f`** between every car (caboose, carriages, locomotive)
- Container offset from carriage: X = +2.25 (right side, outward face)
- Container size: 2.0 × 2.0 × 3.0
- Clamps distributed along container Z axis (outward face at X = +1.15)
- Clamp spacing = ContainerDepth / (count + 1)

### Turret / Aiming
- Turret tracks **camera forward direction** (`-_camera.GlobalTransform.Basis.Z`), not a point
- Pitch is clamped: `TurretMaxPitchDown` degrees below horizontal (config, default 20°)
- Yellow dot ray cast from **camera position** (not barrel tip) — converges on crosshair when tracking catches up
- **Alternating barrels**: `_fireFromLeft` bool toggles each shot; `ActiveBarrelTip` returns the active tip
- Burst fire: `BurstCount` bullets per trigger press, `BurstDelay` seconds between each; no ammo limit
- Auto-fire: when `AutoFire=true`, holding mouse button fires continuous bursts

### Bullet Hit Detection
- Per-frame raycast (`PhysicsRayQueryParameters3D`, mask 7 = layers 1+2+3) replaces `Area3D.AreaEntered`
- Prevents tunneling at high speeds
- `CollideWithAreas=true`, `CollideWithBodies=true`
- **Area3D hits**: clamp parent → clamp damage; container parent → HP damage + AoE splash
- **StaticBody3D hits** (train cars, rail, pillars): bullet destroyed, no damage
- Bullet mesh scaled via `MeshSlot` child only — collision sphere stays full size

### Bullet Trail
- `CpuParticles3D` with `LocalCoords=false` (world-space, stays behind bullet)
- Orange→transparent gradient, sphere mesh radius from `GameConfig.TrailThickness`
- On hit/destroy: `DetachTrail()` — nulls field, stops emitting, reparents to scene root, uses `GetTree().CreateTimer(trail.Lifetime)` for deferred free (reliable across all hit types)

### Side-Switching (PlayerCar)
- **Up arrow** (`switch_side_over`): arc over top of train
- **Down arrow** (`switch_side_under`): arc under train — only allowed when `CanSwitchUnder` is true
- Arc: `newX = _arcStartX * cos(t*π)`, `newY = YHeight ± arcHeight * sin(t*π)`; car body rotation never changes
- Arc height: `OverArcHeight = 6f`, `UnderArcHeight = 6f`
- Duration: `GameConfig.SideChangeTime` (default 3.5s)
- Camera aim is **unaffected** during arc — only position moves
- `CanSwitchUnder` updated each frame by `PredictUnderArcClear()`: computes world-Z of car at pillar-crossing time and checks `PillarPool.HasPillarNearZ()`
- HUD shows "↓ CLEAR" label (`SwitchUnderIndicator`) when `CanSwitchUnder` is true

### Zoom-Away / Level End
- `TrainSpeedManager.TriggerZoomAway()`: sets `CurrentTrainSpeed = 0` (stops scroll/pillars) and stores `TrainZoomSpeed = CurrentTrainSpeed * 10`
- During zoom: `LevelManager` translates Train node in +Z at `TrainZoomSpeed` per second — train physically pulls away while environment is stationary

### Mouse Capture
- `GetWindow().FocusEntered` signal in `PlayerCar._Ready()` handles window focus regain
- `OnViewportFocusEntered()` cycles `Input.MouseMode` from Visible → Captured to recover from OS capture loss
- Mouse motion handler does **not** call `SetInputAsHandled()`

---

## Autoloads (registered in project.godot)

| Name | Script | Key Properties |
|------|--------|----------------|
| `GameConfig` | `scripts/autoloads/GameConfig.cs` | All config values as typed properties |
| `GameSession` | `scripts/autoloads/GameSession.cs` | `CollectedCargo`, `Reset()`, `OnCargoDetached()` |
| `TrainSpeedManager` | `scripts/autoloads/TrainSpeedManager.cs` | `CurrentTrainSpeed`, `TrainZoomSpeed`, `TriggerZoomAway()` |

---

## Signal Connections (wired in TrainBuilder.cs)

```
ContainerNode.CargoDetached(string) → GameSession.OnCargoDetached(string)
ContainerNode.CargoDetached(string) → TrainSpeedManager.OnContainerDetached()
ContainerNode.ContainerDestroyed()  → GameSession.OnContainerDestroyed()
```

---

## Collision Layers

```
Layer 1 (value 1)  = World/Train bodies  (StaticBody3D: carriages, rail, pillars, deployers)
Layer 2 (value 2)  = Containers          (ContainerNode Area3D)
Layer 3 (value 4)  = Clamps              (ClampNode Area3D)
Layer 4 (value 8)  = Player Shield       (Shield Area3D — monitors drone projectiles)
Layer 5 (value 16) = Player projectiles  (Bullet Area3D, Beacon Area3D)
Layer 6 (value 32) = Drones              (DroneNode Area3D — hit by player bullets)
Layer 7 (value 64) = Drone projectiles   (DroneBullet Area3D — detected by Shield)

Bullet      mask = 39 (hits World/Train + Containers + Clamps + Drones)
Beacon      mask = 2  (hits Containers only — passes through Clamps)
Shield      mask = 64 (monitors drone projectiles for block/flash logic)
DroneBullet mask = 0  (no physics collision — moves to world-space target position)
```

---

## Input Actions (defined in project.godot)

| Action | Default |
|--------|---------|
| `move_forward` | W |
| `move_backward` | S |
| `move_left` | A |
| `move_right` | D |
| `fire_primary` | Left mouse button |
| `fire_beacon` | Right mouse button |
| `switch_side_over` | Space |
| `switch_side_under` | Ctrl |

---

## HUD Node Paths (used by HUD.cs and LevelManager)

- `BottomContainer/SpeedBar` — ProgressBar (relative speed 0=max back, 50=stopped, 100=max fwd)
- `TopRight/TrainSpeedLabel` — Label "Train: N u/s"
- `Warning/WarningLabel` — Label (hidden by default)
- `Warning/CountdownLabel` — Label (hidden by default)

---

## Level End Flow

1. LevelManager checks each frame: `(LocomotiveZ - playerZ) > TurretRange`
2. If out of range: show HUD warning, start 3-second countdown
3. If player re-enters range before countdown: warning cancelled
4. On countdown expire: `TrainSpeedManager.TriggerZoomAway()`, disable player input, start 2-second zoom timer
5. During zoom: `CurrentTrainSpeed = 0` (environment stops); LevelManager translates Train node in +Z at `TrainZoomSpeed` per second
6. On zoom timer expire: `GetTree().ChangeSceneToFile("res://scenes/ui/AfterAction.tscn")`

---

## Play Again Flow

AfterAction.cs: `GameSession.Reset()` + `TrainSpeedManager.ResetSpeed()` → change to `res://scenes/Main.tscn`
Main._Ready() rebuilds train via TrainBuilder (which calls BuildTrain in _Ready).
TrainBuilder.Rebuild() can be called manually for a mid-session reset if needed.

---

## Config File

`config/game_config.json` — all values with their GameConfig property names:

```
turret.damage                     → TurretDamage
turret.rate_of_fire               → RateOfFire
turret.blast_radius               → BlastRadius
turret.bullet_speed               → BulletSpeed
turret.bullet_size                → BulletSize
turret.trail_thickness            → TrailThickness
turret.turret_tracking_speed      → TurretTrackingSpeed
turret.burst_count                → BurstCount
turret.burst_delay                → BurstDelay
turret.turret_max_pitch_down      → TurretMaxPitchDown
turret.auto_fire                  → AutoFire
beacon.beacon_reload_speed        → BeaconReloadSpeed
beacon.beacon_speed               → BeaconSpeed
train.min_carriages               → MinCarriages
train.max_carriages               → MaxCarriages
train.min_containers_per_carriage → MinContainersPerCarriage
train.max_containers_per_carriage → MaxContainersPerCarriage
train.min_clamps_per_container    → MinClampsPerContainer
train.max_clamps_per_container    → MaxClampsPerContainer
clamps.clamp_hitpoints            → ClampHitpoints
containers.container_hitpoints    → ContainerHitpoints
player.min_relative_velocity      → MinRelativeVelocity
player.max_relative_velocity      → MaxRelativeVelocity
player.car_acceleration           → CarAcceleration
player.car_deceleration           → CarDeceleration
player.side_change_time           → SideChangeTime
speed.base_train_speed            → BaseTrainSpeed
speed.speed_increase_per_container → SpeedIncreasePerContainer
speed.turret_range                → TurretRange
environment.pillar_spacing        → PillarSpacing
cargo_types[]                     → CargoTypes (List<CargoType>)
enemies.max_drones_per_deployer   → MaxDronesPerDeployer
enemies.max_deployers_per_carriage → MaxDeployersPerCarriage
enemies.deployer_cooldown         → DeployerCooldown
enemies.drone_move_speed          → DroneMoveSpeed
enemies.drone_combat_speed        → DroneCombatSpeed
enemies.drone_fire_rate           → DroneFireRate
enemies.car_speed_damage_per_hit  → CarSpeedDamagePerHit
enemies.drone_height_min          → DroneHeightMin
enemies.drone_height_max          → DroneHeightMax
enemies.drone_hitpoints           → DroneHitpoints
enemies.drone_bullet_speed        → DroneBulletSpeed
enemies.drone_bullet_size         → DroneBulletSize
enemies.drone_hit_chance          → DroneHitChance
enemies.shield_block_angle        → ShieldBlockAngle
enemies.drone_reposition_chance   → DroneRepositionChance
enemies.drone_chase_distance      → DroneChaseDistance
enemies.drone_max_deployer_distance → DroneMaxDeployerDistance
```

---

## GLB Asset Pipeline

All 3D models use a **GLB-first, procedural-fallback** pattern.  Placeholder
GLBs matching the current primitive dimensions live in `assets/models/` and are
generated by `tools/generate_placeholders.py`.

### Directory layout

```
assets/models/
  train/        carriage, container, clamp, locomotive, caboose
  player/       player_car, turret
  projectiles/  bullet, beacon
  environment/  pillar
```

### GLB node convention

Every GLB must contain exactly these two mesh nodes:

| Node name | Purpose |
|---|---|
| `Body` | Visual mesh (rendered) |
| `Body-col` | Collision mesh — Godot auto-generates `ConvexPolygonShape3D` from this at import time; not rendered |

### Adding a new model — checklist

When adding a new object that needs a 3D mesh, do **all four** of the following:

1. **Python generator** (`tools/generate_placeholders.py`) — add an entry to the
   `MODELS` dict with the correct shape and dimensions:
   ```python
   "category/model_name": ("box"|"cylinder"|"sphere"|"capsule", *dims),
   ```
   Re-run the script to produce the `.glb` file.

2. **C# loading with fallback** — in the script that creates/instantiates the
   object, try to load from GLB first and fall back to a procedural primitive
   if the file is missing.  Use one of the two established patterns:

   **Scene-based (for pooled/instanced objects, e.g. PillarPool):**
   ```csharp
   var scene = GD.Load<PackedScene>("res://assets/models/category/model.glb");
   Node3D node = scene != null ? InstantiateFromGlb(scene) : CreateProcedural();
   ```
   Set `CollisionLayer`/`CollisionMask` on any auto-generated `StaticBody3D`
   children after instantiation.

   **Mesh-only (for inline Node3D construction, e.g. TrainBuilder caboose/loco):**
   ```csharp
   var mesh = TryLoadGlbMesh("res://assets/models/category/model.glb")
              ?? new BoxMesh { Size = size };
   meshSlot.Mesh = mesh;
   ```
   Keep the manually-built `StaticBody3D` + `CollisionShape3D` for collision;
   only the visual mesh is replaced by the GLB.

3. **Collision layer** — always set `CollisionLayer` and `CollisionMask`
   explicitly in code; never rely on Godot's default (layer 1, mask 1).
   See the Collision Layers table below.

4. **Docs** (`docs/index.md` model list table) — add a row with the GLB path,
   shape, and dimensions so the artist knows the target bounds.

### Current model registry

| GLB path | Shape | Dimensions | Loaded by |
|---|---|---|---|
| `train/carriage.glb` | Box | 3.0 × 2.5 × 12.0 | Carriage.tscn (manual wire) |
| `train/container.glb` | Box | 2.0 × 2.0 × 3.0 | Container.tscn (manual wire) |
| `train/clamp.glb` | Cylinder | r 0.2, h 0.35 | Clamp.tscn (manual wire) |
| `train/locomotive.glb` | Box | 3.0 × 3.0 × 10.0 | TrainBuilder.TryLoadGlbMesh |
| `train/caboose.glb` | Box | 3.0 × 2.5 × 8.0 | TrainBuilder.TryLoadGlbMesh |
| `player/player_car.glb` | Box | 3.0 × 0.6 × 1.5 | PlayerCar.tscn (manual wire) |
| `player/turret.glb` | Cylinder | r 0.09, h 0.8 | Turret.tscn (manual wire) |
| `projectiles/bullet.glb` | Capsule | r 0.18, total h 0.7 | Bullet.tscn (manual wire) |
| `projectiles/beacon.glb` | Sphere | r 0.15 | Beacon.tscn (manual wire) |
| `environment/pillar.glb` | Cylinder | r 0.3, h 10.0 | PillarPool.CreatePillarFromGlb |
| `enemies/deployer.glb` | Box | 1.2 × 0.4 × 0.8 | DeployerNode.BuildVisual |
| `enemies/drone.glb` | Box | 0.8 × 0.25 × 0.8 | DroneNode._Ready |
| `projectiles/drone_bullet.glb` | Sphere | r 0.08 | (visual only, mesh proc in code) |

"Manual wire" = mesh is assigned once in the Godot editor (see `docs/wiring-scenes.md`).
After that, reimporting the GLB updates it automatically.

---

## Known Deviations from Plan

- `TrainBuilder.cs` is attached to a **child Node3D called "Train"** in Main.tscn (not directly on Main), to avoid one-script-per-node conflict with Main.cs
- `LevelManager.cs` is a separate **Node child** of Main.tscn
- `Turret` is a child of `Camera3D` (inside PlayerCar), not a direct child of PlayerCar — required so barrels follow camera pitch
- Turret barrel transforms use rotation via transform matrix (barrels point in -Z direction, lying horizontal)
- `Turret.tscn` has two barrel tips: `BarrelTipLeft` at `(-0.18, -0.42, -0.9)` and `BarrelTipRight` at `(0.18, -0.42, -0.9)` — alternating per shot
- `PillarPool.cs` instantiates GLB scene at runtime; falls back to procedural `CylinderMesh` if GLB not found
- `TrainBuilder.cs` caboose and locomotive load visual mesh from GLB via `TryLoadGlbMesh`; collision remains a manually-built `BoxShape3D`
- `project/assembly_name` in `project.godot` must be `WildJam2603` (matches the `.csproj` output DLL name) — changing it breaks C# script loading entirely
- Bullet `Area3D.AreaEntered` is **unused** — hit detection is per-frame raycast only (prevents tunneling)
- `TrainSpeedManager` has two speed values: `CurrentTrainSpeed` (drives environment scroll) and `TrainZoomSpeed` (drives physical train movement during zoom-away) — these are intentionally decoupled
