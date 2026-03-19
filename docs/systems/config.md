# Config System

All tunable values live in `config/game_config.json`. At startup `GameConfig.cs` (autoload singleton) loads the file and exposes every value as a typed read-only C# property.

Scripts access config via `GetNode<GameConfig>("/root/GameConfig")` and cache the reference in a `_config` field.

---

## Adding a new field

Use the `/add-config-field` skill, or manually:

1. Add the key to `config/game_config.json` under the appropriate section.
2. Add a `public float/int/bool PropertyName { get; private set; } = default;` in `GameConfig.cs`.
3. Add the parse line in `_Ready()` inside the section block.
4. Use `_config.PropertyName` in the consuming script.

---

## Full parameter reference

| JSON path | C# property | Type | Notes |
|---|---|---|---|
| `turret.damage` | `TurretDamage` | float | Per-bullet damage to clamps/containers |
| `turret.rate_of_fire` | `RateOfFire` | float | Bursts per second |
| `turret.blast_radius` | `BlastRadius` | float | Splash damage radius |
| `turret.bullet_speed` | `BulletSpeed` | float | |
| `turret.bullet_size` | `BulletSize` | float | Visual scale; collision stays full size |
| `turret.trail_thickness` | `TrailThickness` | float | CpuParticles3D sphere radius |
| `turret.turret_tracking_speed` | `TurretTrackingSpeed` | float | Slerp speed toward camera forward |
| `turret.burst_count` | `BurstCount` | int | Bullets per trigger press |
| `turret.burst_delay` | `BurstDelay` | float | Seconds between burst shots |
| `turret.turret_max_pitch_down` | `TurretMaxPitchDown` | float | Degrees below horizontal |
| `turret.auto_fire` | `AutoFire` | bool | Hold to fire continuously |
| `beacon.beacon_reload_speed` | `BeaconReloadSpeed` | float | Seconds between beacon shots |
| `beacon.beacon_speed` | `BeaconSpeed` | float | |
| `train.min_carriages` | `MinCarriages` | int | |
| `train.max_carriages` | `MaxCarriages` | int | |
| `train.min_containers_per_carriage` | `MinContainersPerCarriage` | int | |
| `train.max_containers_per_carriage` | `MaxContainersPerCarriage` | int | |
| `train.min_clamps_per_container` | `MinClampsPerContainer` | int | |
| `train.max_clamps_per_container` | `MaxClampsPerContainer` | int | |
| `clamps.clamp_hitpoints` | `ClampHitpoints` | float | |
| `containers.container_hitpoints` | `ContainerHitpoints` | float | |
| `player.min_relative_velocity` | `MinRelativeVelocity` | float | Negative = can fall behind train |
| `player.max_relative_velocity` | `MaxRelativeVelocity` | float | Positive = can outrun train |
| `player.car_acceleration` | `CarAcceleration` | float | |
| `player.car_deceleration` | `CarDeceleration` | float | |
| `player.side_change_time` | `SideChangeTime` | float | Duration of over/under arc |
| `player.car_drive_height` | `CarDriveHeight` | float | Y height when flying (default 8.0) |
| `player.number_pre_scanned_containers` | `NumberPreScannedContainers` | int | Containers revealed at raid start |
| `player.cliff_detection_distance` | `CliffDetectionDistance` | float | Forward ray length for cliff warning |
| `player.flip_ray_samples` | `FlipRaySamples` | int | Ray samples along under-arc path |
| `player.cliff_auto_flip_brake` | `CliffAutoFlipBrake` | float | Velocity brake when auto-flipping |
| `speed.base_train_speed` | `BaseTrainSpeed` | float | World units/sec at raid start |
| `speed.speed_increase_per_container` | `SpeedIncreasePerContainer` | float | Speed added per detached container |
| `speed.turret_range` | `TurretRange` | float | Max distance before level-end warning |
| `environment.pillar_spacing` | `PillarSpacing` | float | |
| `environment.pillar_x_spread` | `PillarXSpread` | float | Random X offset for pillars |
| `environment.spawn_ahead_distance` | `SpawnAheadDistance` | float | |
| `environment.despawn_behind_distance` | `DespawnBehindDistance` | float | |
| `environment.cloud_pool_size` | `CloudPoolSize` | int | |
| `environment.cloud_parallax_factor` | `CloudParallaxFactor` | float | Scroll speed fraction |
| `environment.cloud_spawn_spread` | `CloudSpawnSpread` | float | X/Z random spread |
| `environment.cloud_height_min` | `CloudHeightMin` | float | |
| `environment.cloud_height_max` | `CloudHeightMax` | float | |
| `environment.cloud_size_min` | `CloudSizeMin` | float | |
| `environment.cloud_size_max` | `CloudSizeMax` | float | |
| `environment.rock_pillar_spacing` | `RockPillarSpacing` | float | |
| `environment.rock_pillar_distance` | `RockPillarDistance` | float | X distance from track centre |
| `environment.rock_pillar_height_min` | `RockPillarHeightMin` | float | |
| `environment.rock_pillar_height_max` | `RockPillarHeightMax` | float | |
| `environment.rubble_pool_size` | `RubblePoolSize` | int | |
| `environment.rubble_spread` | `RubbleSpread` | float | |
| `environment.rubble_size_min` | `RubbleSizeMin` | float | |
| `environment.rubble_size_max` | `RubbleSizeMax` | float | |
| `enemies.max_drones_per_deployer` | `MaxDronesPerDeployer` | int | |
| `enemies.max_deployers_per_carriage` | `MaxDeployersPerCarriage` | int | |
| `enemies.deployer_cooldown` | `DeployerCooldown` | float | Seconds between drone spawns |
| `enemies.drone_move_speed` | `DroneMoveSpeed` | float | Speed during deployment phase |
| `enemies.drone_combat_speed` | `DroneCombatSpeed` | float | Speed while repositioning |
| `enemies.drone_fire_rate` | `DroneFireRate` | float | Shots per second |
| `enemies.car_speed_damage_per_hit` | `CarSpeedDamagePerHit` | float | Fraction of max speed lost per hit |
| `enemies.drone_height_min` | `DroneHeightMin` | float | |
| `enemies.drone_height_max` | `DroneHeightMax` | float | |
| `enemies.drone_hitpoints` | `DroneHitpoints` | float | |
| `enemies.drone_bullet_speed` | `DroneBulletSpeed` | float | |
| `enemies.drone_bullet_size` | `DroneBulletSize` | float | |
| `enemies.drone_hit_chance` | `DroneHitChance` | float | 0–1 accuracy probability |
| `enemies.shield_block_angle` | `ShieldBlockAngle` | float | Degrees; bullets within cone are blocked |
| `enemies.drone_reposition_chance` | `DroneRepositionChance` | float | 0–1 probability after each shot |
| `enemies.drone_chase_distance` | `DroneChaseDistance` | float | Max range before reposition |
| `enemies.drone_max_deployer_distance` | `DroneMaxDeployerDistance` | float | Max range before return |
| `cargo_types[]` | `CargoTypes` | List\<CargoType\> | name + color per type |
| `upgrades[]` | `Upgrades` | List\<Upgrade\> | See upgrades.md |

---

## Upgrade modifiers

`ApplyUpgrade(string upgradeName)` in `GameConfig.cs` finds the named upgrade and applies each modifier:

- `"type": "flat"` → `property += value`
- `"type": "multiplier"` → `property *= value`

Properties are modified in-place on the singleton for the duration of the run. `ResetSpeed()` on `TrainSpeedManager` resets speed state but does **not** reset config properties — those reset when the scene reloads and `GameConfig._Ready()` re-reads the JSON.
