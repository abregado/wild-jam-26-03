# Upgrade System

After each raid the player enters the After-Action screen (`scenes/ui/AfterAction.tscn`, `scripts/ui/AfterAction.cs`). Three random affordable upgrades are presented as flip-cards.

---

## After-Action flow

1. **BOX_BREAK** — player clicks 4× to open unidentified containers in a 3D SubViewport.
2. **RESOURCE_FLY** — collected cargo resources fly to the counter with a pulse animation.
3. **PURCHASE** — three random affordable upgrade cards slide in and flip to reveal. Player picks one (or none).

---

## Upgrade definition (game_config.json)

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

- `name` — displayed on card, used as key in `ApplyUpgrade()`
- `description` — one-sentence flavour shown to player
- `cost` — resource units required to purchase
- `modifiers` — array; can contain multiple entries
  - `stat` — snake_case key matching the JSON config path (e.g. `turret_damage`)
  - `type` — `"flat"` (additive) or `"multiplier"` (multiplicative)
  - `value` — amount; for multiplier, `1.25` = +25%

---

## Applying upgrades

`GameConfig.ApplyUpgrade(string name)` finds the upgrade by name and iterates its modifiers:

```csharp
// flat
property += (float)modifier.Value;

// multiplier
property *= (float)modifier.Value;
```

The property is modified in-place on the singleton. Config values are **not** reset between raids within the same session — they accumulate across purchases. A full reset happens when the game returns to `Main.tscn` and `GameConfig._Ready()` re-reads the JSON file.

---

## Stat keys

The `stat` field in a modifier must match the JSON config key (not the C# property name). For example:

| C# property | JSON stat key |
|---|---|
| `TurretDamage` | `turret_damage` |
| `BulletSpeed` | `bullet_speed` |
| `DroneFireRate` | `drone_fire_rate` |
| `SideChangeTime` | `side_change_time` |

Full list: see `docs/systems/config.md`.

---

## Adding a new upgrade

Use the `/add-upgrade` skill.
