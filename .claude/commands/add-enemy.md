Add a new enemy type to the game. Before writing any code, read the existing enemy scripts to understand the patterns in use.

## Orientation — read these files first

- `scripts/enemies/DroneNode.cs` — state-machine enemy with combat AI
- `scripts/enemies/DeployerNode.cs` — spawner that manages drone lifecycle
- `scripts/enemies/RoofTurretNode.cs` — stationary enemy that activates on nearby damage
- `scripts/train/TrainBuilder.cs` — where enemies are instantiated and attached to carriages
- `config/game_config.json` (`enemies` section) — all enemy tuning values

## Steps

1. **Define the enemy** — ask the user (if not already specified):
   - What is the enemy's name and role?
   - Is it mobile (like a drone) or stationary (like a roof turret)?
   - What triggers it to activate?
   - How does it damage the player?
   - Should it spawn from a deployer, or be placed directly on a carriage?

2. **Create the script** — create `scripts/enemies/NewEnemyNode.cs` following the patterns in the files above:
   - Use an `enum` for states
   - Cache `_config`, `_playerCar` in `_Ready()`
   - Use `Area3D` on **collision layer 32** (bit 6, value 32) so player bullets hit it
   - Call `QueueFree()` or enter a dying state when HP reaches 0

3. **Add config fields** — add all tunable values to `config/game_config.json` under `enemies` and wire them through `GameConfig.cs` (use `/add-config-field` for each).

4. **Attach to train** — read `TrainBuilder.cs` and add instantiation logic in the appropriate location (per-carriage, per-container, or global). Mirror the pattern used for `DeployerNode` or `RoofTurretNode`.

5. **Create a scene (if needed)** — if the enemy needs a `.tscn` (e.g. for a reusable prefab), create it under `scenes/enemies/`. If it's fully procedural (like deployers), no scene file is needed.

6. **Add a placeholder model** — add an entry to `tools/generate_placeholders.py` and run the script. Wire the GLB in the enemy's `_Ready()` using the `TryLoadGlbMesh` / `InstantiateFromGlb` pattern. Add a row to `docs/index.md`.

7. **Wire signals** — if the enemy responds to container/clamp damage signals, wire them in `TrainBuilder.cs` following the existing `ContainerNode.CargoDetached` / `ContainerNode.ContainerDestroyed` pattern.

8. **Test checklist**:
   - Enemy appears on carriages
   - Enemy activates at the right time
   - Player bullets register hits (layer 32 + mask 39)
   - Enemy dies correctly and does not leave orphan nodes
   - Config values are tunable without recompile

## Collision reminder
```
Layer 6 (value 32) = Drones / enemies  — player bullet mask 39 hits this
Layer 7 (value 64) = Drone projectiles — Shield mask 64 detects this
```
See `docs/systems/enemies.md` for full enemy architecture.
