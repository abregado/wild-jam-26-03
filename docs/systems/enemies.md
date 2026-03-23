# Enemy System

Scripts: `scripts/enemies/DroneNode.gd`, `scripts/enemies/DeployerNode.gd`, `scripts/enemies/RoofTurretNode.gd`

---

## Collision assignment

```
Layer 6 (value 32) = Drones / enemy bodies  — targeted by player bullets (mask 39)
Layer 7 (value 64) = Drone projectiles      — detected by player Shield (mask 64)
Layer 1 (value 1)  = Deployer bodies        — world/train layer, bullets stop on impact
```

---

## Deployer

`DeployerNode.gd` — sits on carriage roof.

- **Activates** when a nearby container or clamp takes damage (wired via signal in `TrainBuilder`).
- Spawns up to `MaxDronesPerDeployer` drones (config), one at a time on `DeployerCooldown`.
- When a drone dies, a shorter cooldown triggers the next spawn.
- When a drone returns voluntarily, an even shorter cooldown is used.

---

## Drone state machine

`DroneNode.gd`

```
Deploying          → fly upward from deployer, then MovingToPosition
MovingToPosition   → fly to combat position at DroneMoveSpeed, then InPosition
InPosition         → fire at player; if player too far → Repositioning
Repositioning      → fly to new combat position at DroneCombatSpeed, then InPosition
FollowingSide      → fly over train top after player side-switches, then MovingToPosition
ReturningToDeployer → two-phase landing: hover above deployer, then descend and despawn
Dying              → gravity fall, QueueFree after landing
```

**Per-frame range checks (all active states):**
- Distance to deployer > `DroneMaxDeployerDistance` → `ReturningToDeployer`
- Distance to player > `DroneChaseDistance` (InPosition only) → `Repositioning`

**Firing:**
- Fire rate: `DroneFireRate` shots/second.
- Accuracy: `DroneHitChance` probability. Misses scatter around the player.
- Projectile: `DroneBullet` flies to a world-space target (not hitscan).
- `CarSpeedDamagePerHit` reduces player's `MaxRelativeVelocity` on unblocked hits.

**Side-switch tracking:**
- Drone detects player's X-side each frame.
- If side changes while drone is in combat, enters `FollowingSide` and arcs over train top.

---

## Roof Turret

`RoofTurretNode.gd` — mounted on carriage roof.

States: `Inactive → Active → Repairing`

- **Inactive**: dark colour, does not fire.
- **Active**: fires burst at player with periodic rest intervals. Colour = red.
- **Repairing**: colour = yellow; re-enters Active after repair cooldown.

Rules:
- Activates when nearby container/clamp takes damage.
- Cannot fire while player is performing an under-arc flip.
- When hit, enters Repairing state regardless of HP.
- Colour changes signal state to the player visually.

---

## Drone bullet

`DroneBullet.gd`

- Has no physics collision (`mask = 0`).
- Flies toward a stored world-space target position.
- On arrival: if not blocked by Shield → calls `_player_car.take_speed_damage(car_speed_damage_per_hit)`.
- On arrival: always `QueueFree()`.

---

## Adding a new enemy type

Use the `/add-enemy` skill. The skill guides you through reading the existing patterns, creating the script, wiring config values, attaching to the train, and adding a placeholder model.
