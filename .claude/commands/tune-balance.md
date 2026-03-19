Review and adjust game balance parameters in `config/game_config.json`.

## Steps

1. **Read the current config** — read `config/game_config.json` in full. Note the current values for all sections.

2. **Identify the area** — ask the user (if not specified) what aspect of balance they want to change:
   - **Difficulty** (enemy HP, drone fire rate, speed damage, turret range)
   - **Train structure** (carriage count, container count, clamp count, HP values)
   - **Player feel** (acceleration, velocity range, side-change time)
   - **Weapon feel** (damage, fire rate, burst, bullet speed, tracking speed)
   - **Progression speed** (base train speed, speed increase per container)
   - **Economy** (upgrade costs, cargo values)
   - **Environment density** (pillar spacing, cloud/rock/rubble pool sizes)

3. **Propose changes** — for each value you plan to change:
   - State the current value
   - State the proposed value
   - Give a one-line rationale

4. **Apply changes** — edit `config/game_config.json` with the agreed values. Make only the changes the user has confirmed; do not silently adjust adjacent values.

5. **Cross-check dependencies** — some values interact:
   - `speed_increase_per_container` × max containers affects end-game train speed
   - `turret_range` should exceed the longest train (`max_carriages × 12.5` units approx)
   - `max_relative_velocity` affects how quickly the player can chase the locomotive
   - `deployer_cooldown` × `max_drones_per_deployer` sets peak drone density

6. **Report** — list all changed keys, their old and new values.

## Reference ranges (approximate "feels good" zones)

| Parameter | Easier | Harder |
|---|---|---|
| `turret_range` | 80–120 | 30–50 |
| `clamp_hitpoints` | 20–30 | 60–100 |
| `max_clamps_per_container` | 2–3 | 6–10 |
| `drone_fire_rate` | 0.5–0.8 | 1.5–2.5 |
| `car_speed_damage_per_hit` | 0.1–0.2 | 0.5–1.0 |
| `base_train_speed` | 8–12 | 18–25 |

See `docs/systems/config.md` for the full parameter reference.
