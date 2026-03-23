# Config System

All tunable values live in `config/game_config.json`. At startup `GameConfig.gd` (autoload singleton) loads the file and exposes every value as a typed read-only GDScript property.

Scripts access config via the `GameConfig` autoload directly (e.g. `GameConfig.turret_damage`) — no node lookup needed.

---

## Adding a new field

Use the `/add-config-field` skill, or manually:

1. Add the key to `config/game_config.json` under the appropriate section.
2. Add a `var property_name: float = 0.0` (or `int`/`bool`) in `GameConfig.gd`.
3. Add the parse line in `_ready()` inside the section block.
4. Use `GameConfig.property_name` in the consuming script.

---

## Full parameter reference

| JSON path | GDScript property | Type | Notes |
|---|---|---|---|
| `turret.damage` | `turret_damage` | float | Per-bullet damage to clamps/containers |
| `turret.rate_of_fire` | `rate_of_fire` | float | Bursts per second |
| `turret.blast_radius` | `blast_radius` | float | Splash damage radius |
| `turret.bullet_speed` | `bullet_speed` | float | |
| `turret.bullet_size` | `bullet_size` | float | Visual scale; collision stays full size |
| `turret.trail_thickness` | `trail_thickness` | float | CPUParticles3D sphere radius |
| `turret.turret_tracking_speed` | `turret_tracking_speed` | float | Slerp speed toward camera forward |
| `turret.burst_count` | `burst_count` | int | Bullets per trigger press |
| `turret.burst_delay` | `burst_delay` | float | Seconds between burst shots |
| `turret.turret_max_pitch_down` | `turret_max_pitch_down` | float | Degrees below horizontal |
| `turret.auto_fire` | `auto_fire` | bool | Hold to fire continuously |
| `beacon.beacon_reload_speed` | `beacon_reload_speed` | float | Seconds between beacon shots |
| `beacon.beacon_speed` | `beacon_speed` | float | |
| `train.min_carriages` | `min_carriages` | int | |
| `train.max_carriages` | `max_carriages` | int | |
| `train.min_containers_per_carriage` | `min_containers_per_carriage` | int | |
| `train.max_containers_per_carriage` | `max_containers_per_carriage` | int | |
| `train.min_clamps_per_container` | `min_clamps_per_container` | int | |
| `train.max_clamps_per_container` | `max_clamps_per_container` | int | |
| `clamps.clamp_hitpoints` | `clamp_hitpoints` | float | |
| `containers.container_hitpoints` | `container_hitpoints` | float | |
| `player.min_relative_velocity` | `min_relative_velocity` | float | Negative = can fall behind train |
| `player.max_relative_velocity` | `max_relative_velocity` | float | Positive = can outrun train |
| `player.car_acceleration` | `car_acceleration` | float | |
| `player.car_deceleration` | `car_deceleration` | float | |
| `player.side_change_time` | `side_change_time` | float | Duration of over/under arc |
| `player.car_drive_height` | `car_drive_height` | float | Y height when flying (default 8.0) |
| `player.number_pre_scanned_containers` | `number_pre_scanned_containers` | int | Containers revealed at raid start |
| `player.cliff_detection_distance` | `cliff_detection_distance` | float | Forward ray length for cliff warning |
| `player.flip_ray_samples` | `flip_ray_samples` | int | Ray samples along under-arc path |
| `player.cliff_auto_flip_brake` | `cliff_auto_flip_brake` | float | Velocity brake when auto-flipping |
| `speed.base_train_speed` | `base_train_speed` | float | World units/sec at raid start |
| `speed.speed_increase_per_container` | `speed_increase_per_container` | float | Speed added per detached container |
| `speed.turret_range` | `turret_range` | float | Max distance before level-end warning |
| `environment.pillar_spacing` | `pillar_spacing` | float | |
| `environment.pillar_x_spread` | `pillar_x_spread` | float | Random X offset for pillars |
| `environment.spawn_ahead_distance` | `spawn_ahead_distance` | float | |
| `environment.despawn_behind_distance` | `despawn_behind_distance` | float | |
| `environment.cloud_pool_size` | `cloud_pool_size` | int | |
| `environment.cloud_parallax_factor` | `cloud_parallax_factor` | float | Scroll speed fraction |
| `environment.cloud_spawn_spread` | `cloud_spawn_spread` | float | X/Z random spread |
| `environment.cloud_height_min` | `cloud_height_min` | float | |
| `environment.cloud_height_max` | `cloud_height_max` | float | |
| `environment.cloud_size_min` | `cloud_size_min` | float | |
| `environment.cloud_size_max` | `cloud_size_max` | float | |
| `environment.rock_pillar_spacing` | `rock_pillar_spacing` | float | |
| `environment.rock_pillar_distance` | `rock_pillar_distance` | float | X distance from track centre |
| `environment.rock_pillar_height_min` | `rock_pillar_height_min` | float | |
| `environment.rock_pillar_height_max` | `rock_pillar_height_max` | float | |
| `environment.rubble_pool_size` | `rubble_pool_size` | int | |
| `environment.rubble_spread` | `rubble_spread` | float | |
| `environment.rubble_size_min` | `rubble_size_min` | float | |
| `environment.rubble_size_max` | `rubble_size_max` | float | |
| `enemies.max_drones_per_deployer` | `max_drones_per_deployer` | int | |
| `enemies.max_deployers_per_carriage` | `max_deployers_per_carriage` | int | |
| `enemies.deployer_cooldown` | `deployer_cooldown` | float | Seconds between drone spawns |
| `enemies.drone_move_speed` | `drone_move_speed` | float | Speed during deployment phase |
| `enemies.drone_combat_speed` | `drone_combat_speed` | float | Speed while repositioning |
| `enemies.drone_fire_rate` | `drone_fire_rate` | float | Shots per second |
| `enemies.car_speed_damage_per_hit` | `car_speed_damage_per_hit` | float | Fraction of max speed lost per hit |
| `enemies.drone_height_min` | `drone_height_min` | float | |
| `enemies.drone_height_max` | `drone_height_max` | float | |
| `enemies.drone_hitpoints` | `drone_hitpoints` | float | |
| `enemies.drone_bullet_speed` | `drone_bullet_speed` | float | |
| `enemies.drone_bullet_size` | `drone_bullet_size` | float | |
| `enemies.drone_hit_chance` | `drone_hit_chance` | float | 0–1 accuracy probability |
| `enemies.shield_block_angle` | `shield_block_angle` | float | Degrees; bullets within cone are blocked |
| `enemies.drone_reposition_chance` | `drone_reposition_chance` | float | 0–1 probability after each shot |
| `enemies.drone_chase_distance` | `drone_chase_distance` | float | Max range before reposition |
| `enemies.drone_max_deployer_distance` | `drone_max_deployer_distance` | float | Max range before return |
| `cargo_types[]` | `cargo_types` | Array[Dictionary] | name + color per type |
| `upgrades[]` | `upgrades` | Array[Dictionary] | See upgrades.md |

---

## Upgrade modifiers

`apply_upgrade(upgrade_name)` in `GameConfig.gd` finds the named upgrade and applies each modifier:

- `"type": "flat"` → `property += value`
- `"type": "multiplier"` → `property *= value`

Properties are modified in-place on the singleton for the duration of the run. `reset_speed()` on `TrainSpeedManager` resets speed state but does **not** reset config properties — those reset when the scene reloads and `GameConfig._ready()` re-reads the JSON.
