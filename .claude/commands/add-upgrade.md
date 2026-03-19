Add a new upgrade definition to the game. Upgrades are purchased in the After-Action screen and permanently modify config stats for the current run.

## Steps

1. **Read the config** — read `config/game_config.json` and examine the existing `upgrades` array entries to understand the format. Also read `scripts/autoloads/GameConfig.cs` to see the `Upgrade` / `UpgradeModifier` classes and how `ApplyUpgrade()` works.

2. **Read AfterAction.cs** — read `scripts/ui/AfterAction.cs` to understand how upgrade cards are displayed and how purchases are applied. Check which fields it reads from the upgrade definition.

3. **Ask the user** (if not already provided) for:
   - Upgrade name (displayed on the card)
   - Description (one sentence shown to player)
   - Cost in resources
   - Which config stat(s) to modify (`target_stat` must match a `GameConfig` property name in camelCase / snake_case as used in config)
   - Modifier type: `flat` (adds a value) or `multiplier` (multiplies the current value)
   - Modifier amount

4. **Add the entry** to the `upgrades` array in `config/game_config.json`, following the exact structure of existing entries. Example:
   ```json
   {
     "name": "Reinforced Rounds",
     "description": "Bullets deal more damage per hit.",
     "cost": 3,
     "modifiers": [
       { "stat": "turret_damage", "type": "flat", "value": 10.0 }
     ]
   }
   ```

5. **Verify** — confirm the `stat` key in your new entry matches an existing key handled by `ApplyUpgrade()` in `GameConfig.cs`. If it does not, add a case for it.

6. **Report** the added entry and note that it will appear as a random card in the After-Action upgrade pool.

## Notes
- Use `"type": "multiplier"` for percentage-style boosts (e.g. `"value": 1.25` = +25%).
- Use `"type": "flat"` for absolute additions/subtractions.
- An upgrade can have multiple modifiers (array with more than one entry).
- See `docs/systems/upgrades.md` for the full upgrade system reference.
