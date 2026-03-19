Wire a new tunable parameter through the config system. Use this when adding a magic number to a script that should be tweakable without recompiling.

## Steps

1. **Identify the field** — if the user has not specified, ask:
   - JSON section it belongs to (e.g. `"enemies"`, `"player"`, `"turret"`)
   - JSON key name in `snake_case` (e.g. `"roof_turret_repair_time"`)
   - C# property name in `PascalCase` (e.g. `RoofTurretRepairTime`)
   - Type (`float`, `int`, or `bool`)
   - Default / initial value
   - Which script(s) consume it

2. **Add to `config/game_config.json`** — insert the key under the correct section with the chosen default value.

3. **Add property to `GameConfig.cs`** — read `scripts/autoloads/GameConfig.cs` first. Add a `public float/int/bool PropertyName { get; private set; } = defaultValue;` in the appropriate region comment block. Then add the load line inside `_Ready()` where the section is parsed:
   ```csharp
   PropertyName = section["key_name"].AsSingle(); // or AsInt32() / AsBool()
   ```

4. **Wire the consuming script(s)** — read the relevant script. Replace the hard-coded value with `_config.PropertyName` (where `_config` is the cached `GameConfig` reference). If the script does not yet cache `GameConfig`, add:
   ```csharp
   private GameConfig _config = null!;
   // in _Ready:
   _config = GetNode<GameConfig>("/root/GameConfig");
   ```

5. **Update `CLAUDE.md`** config table — add a row to the Config File section of `CLAUDE.md` mapping the JSON key → C# property name.

6. **Confirm** — report all files changed and the new property name.

## Notes
- All config properties are read-only after load (`private set`). Scripts must not mutate them; upgrades apply via `ApplyUpgrade()`.
- See `docs/systems/config.md` for the full config reference and section layout.
