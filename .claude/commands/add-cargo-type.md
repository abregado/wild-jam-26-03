Add a new cargo type to the game. Cargo types are the goods stored in train containers — each has a display name and a colour used to tint the container mesh.

## Steps

1. **Read the config** — read `config/game_config.json` and note the existing entries in the `cargo_types` array so you understand the format.

2. **Ask the user** (if not already provided) for:
   - Display name (e.g. `"Fuel Cells"`)
   - Colour as a hex string (e.g. `"#ff6600"`) or a descriptive colour you can translate to hex

3. **Add the entry** — append to the `cargo_types` array in `config/game_config.json`:
   ```json
   { "name": "Fuel Cells", "color": "#ff6600" }
   ```

4. **Verify GameConfig.cs** — read `scripts/autoloads/GameConfig.cs` and confirm the `CargoTypes` list and `CargoType` class are loading `name` and `color` fields. No code change is needed unless the fields differ.

5. **Verify ContainerNode.cs** — read `scripts/train/ContainerNode.cs` and confirm it reads cargo colour from `GameConfig.CargoTypes`. No change is needed unless the property names differ.

6. **Confirm** — report the added entry and remind the user that the new type will appear in randomly generated containers on the next play.

## Notes
- Colours must be valid Godot `Color` hex strings (`#rrggbb` or `#aarrggbb`).
- The cargo name is shown in the After-Action screen; keep it short (≤ 20 chars).
- See `docs/systems/train.md` for full container/cargo architecture.
