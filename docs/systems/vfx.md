# VFX System

Particle effects are spawned via `VfxSpawner.Spawn(id, worldPosition)`.

## How it works

1. `VfxSpawner` (autoload) checks for `res://scenes/vfx/{id}.tscn`.
2. If the file exists, it instantiates that scene at the given world position.
3. If no file exists, a built-in procedural `CPUParticles3D` fallback is used.
4. The spawned node auto-frees itself when done.

**Artist workflow**: drop a `.tscn` into `scenes/vfx/` with the matching ID name. The scene must auto-free itself (e.g. connect `GPUParticles3D.finished` → `QueueFree` on the root node, or use a `Timer`).

---

## Effect IDs

| ID | Trigger | Source file |
|---|---|---|
| `hit_damageable` | Player bullet hits clamp, container, drone, or turret | `Bullet.cs` |
| `hit_nondamageable` | Player bullet hits train body, pillar, or other static | `Bullet.cs` |
| `clamp_destroyed` | A clamp's HP reaches 0 | `ClampNode.cs` |
| `turret_repair` | Roof turret is hit and enters repair mode (dome hides) | `RoofTurretNode.cs` |
| `drone_deployed` | Drone finishes its spawn scale-up tween | `DroneNode.cs` |
| `drone_destroyed` | Drone HP reaches 0 and begins dying fall | `DroneNode.cs` |
| `container_detach` | Container detaches from the train (recovered or lost) | `ContainerNode.cs` |
| `shield_hit` | Enemy bullet is blocked by the player shield | `Shield.cs` |
| `car_hit` | Enemy bullet reaches the player car unblocked | `DroneBullet.cs` |
| `drone_prefire` | Drone fire cooldown drops below 0.35 s (telegraph) | `DroneNode.cs` |
| `drone_muzzle` | Drone fires a bullet | `DroneNode.cs` |
| `turret_prefire` | Roof turret fire cooldown drops below 0.35 s (telegraph) | `RoofTurretNode.cs` |
| `turret_muzzle` | Roof turret fires a bullet | `RoofTurretNode.cs` |
| `player_muzzle` | Player turret fires a bullet | `Turret.cs` |

---

## Placeholder scene spec

Each placeholder uses a `CPUParticles3D` with `OneShot = true` and a `Timer` that calls `QueueFree` after the lifetime ends. To replace one:

1. Create a new `.tscn` with root type `Node3D`.
2. Add a `GPUParticles3D` child. Set `One Shot = true`, `Emitting = true`.
3. Connect `finished` signal → `queue_free()` on the root node (or add a `Timer`).
4. Save to `res://scenes/vfx/{id}.tscn`.

The placeholder procedural fallback fires immediately — no `.tscn` file required for the game to run.
