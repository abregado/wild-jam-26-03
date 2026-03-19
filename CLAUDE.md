# Wild Jam 26-03 — CLAUDE.md

Godot 4.6.1 Mono / C# game. Engine: `D:\Programs\Godot_v4.6.1-stable_mono_win64\`

---

## Skills

Use these slash commands for common tasks:

| Skill | When to use |
|---|---|
| `/add-cargo-type` | Add a new cargo type (name + colour) to `game_config.json` |
| `/add-upgrade` | Add a new upgrade card to the After-Action purchase pool |
| `/add-config-field` | Wire a new tunable parameter from JSON through `GameConfig.cs` to a script |
| `/add-enemy` | Add a new enemy type (new AI script, config fields, train attachment, model) |
| `/add-model` | Add a new 3D model to the GLB pipeline (generator, C# load, collision, docs) |
| `/tune-balance` | Review and adjust difficulty or feel parameters in `game_config.json` |
| `/new-environment-pool` | Add a new scrolling decoration pool to the environment |

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

## Spatial Layout

- **Train runs along Z axis**: Locomotive at highest +Z, Caboose at Z ≈ 0.
- Player flies at **X = ±8.0**, **Y = `CarDriveHeight`** (config, default 8.0).
- **Car body**: always fixed at `RotationDegrees.Y = 90` (faces −X toward train). Never rotates.
- **Camera**: child of PlayerCar, handles yaw+pitch. **Turret**: child of Camera3D — inherits both.
- Player **forward** = +Z (toward locomotive). "Distance behind front" = `LocomotiveZ − playerZ`.

→ Full details: [docs/systems/player.md](docs/systems/player.md), [docs/systems/train.md](docs/systems/train.md)

---

## Autoloads

| Name | Script | Key API |
|------|--------|---------|
| `GameConfig` | `scripts/autoloads/GameConfig.cs` | All config values as typed properties; `ApplyUpgrade(name)` |
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
Layer 4 (value 8)  = Player Shield       (Shield Area3D)
Layer 5 (value 16) = Player projectiles  (Bullet Area3D, Beacon Area3D)
Layer 6 (value 32) = Drones              (DroneNode Area3D — hit by player bullets)
Layer 7 (value 64) = Drone projectiles   (DroneBullet Area3D — detected by Shield)
Layer 8 (value 128)= Cliff detection     (ObstaclePool wide bodies — forward ray)
Layer 9 (value 256)= Flip path           (ObstaclePool bodies — arc intersection checks)

Bullet      mask = 39  (layers 1+2+3+6)
Beacon      mask = 2   (Containers only)
Shield      mask = 64  (Drone projectiles)
DroneBullet mask = 0   (no physics — flies to world-space target)
```

---

## Input Actions

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

## Level / Session Flow

1. `LevelManager` checks each frame: `(LocomotiveZ − playerZ) > TurretRange` → warning + 3s countdown.
2. Countdown expires → `TrainSpeedManager.TriggerZoomAway()`, disable input, 2s zoom timer.
3. Zoom: train node translates +Z at `TrainZoomSpeed`/s; environment frozen (`CurrentTrainSpeed = 0`).
4. Zoom timer expires → `AfterAction.tscn` (box-break → resource-fly → upgrade purchase).
5. Play Again: `GameSession.Reset()` + `TrainSpeedManager.ResetSpeed()` → `Main.tscn`.

→ Upgrade system details: [docs/systems/upgrades.md](docs/systems/upgrades.md)

---

## GLB Asset Pipeline

All models use **GLB-first, procedural-fallback**. Placeholders generated by `tools/generate_placeholders.py`.

Every GLB must contain: `Body` (visual) + `Body-col` (collision — auto-generates `ConvexPolygonShape3D`).

→ Full pipeline: [docs/index.md](docs/index.md), [docs/replacing-models.md](docs/replacing-models.md)
→ To add a new model: use `/add-model`

---

## Known Deviations

- `TrainBuilder.cs` attaches to a **child Node3D "Train"** in Main.tscn (not Main directly).
- `LevelManager.cs` is a separate Node child of Main.tscn.
- `Turret` is child of `Camera3D` inside PlayerCar — required so barrels follow camera pitch.
- `project/assembly_name` in `project.godot` must be `WildJam2603` — changing it breaks C# loading.
- Bullet `Area3D.AreaEntered` is **unused** — hit detection is per-frame raycast only (anti-tunneling).
- `TrainSpeedManager` has two decoupled speed values: `CurrentTrainSpeed` (scroll) and `TrainZoomSpeed` (physical zoom).

---

## Documentation Index

| Doc | Contents |
|---|---|
| [docs/systems/player.md](docs/systems/player.md) | PlayerCar, side-switching, turret aiming, shield, HUD paths |
| [docs/systems/train.md](docs/systems/train.md) | Train layout, containers, clamps, cargo, track rail |
| [docs/systems/enemies.md](docs/systems/enemies.md) | Drone AI, deployer, roof turret, drone bullet |
| [docs/systems/environment.md](docs/systems/environment.md) | Scrolling, pillar/cloud/rubble/rock pools, obstacles, zoom-away |
| [docs/systems/config.md](docs/systems/config.md) | Full config parameter reference (all JSON keys → C# properties) |
| [docs/systems/upgrades.md](docs/systems/upgrades.md) | Upgrade definitions, ApplyUpgrade(), After-Action flow |
| [docs/index.md](docs/index.md) | Art pipeline overview, model registry |
| [docs/replacing-models.md](docs/replacing-models.md) | Replacing a GLB model |
| [docs/collision.md](docs/collision.md) | Body-col convention, collision import settings |
| [docs/wiring-scenes.md](docs/wiring-scenes.md) | One-time scene mesh wiring guide |
