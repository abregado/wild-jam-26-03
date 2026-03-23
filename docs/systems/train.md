# Train System

Scripts: `scripts/train/TrainBuilder.gd`, `scripts/train/Carriage.gd`, `scripts/train/ContainerNode.gd`, `scripts/train/ClampNode.gd`

---

## Axis layout

- Train runs along the **Z axis**: Locomotive at highest +Z, Caboose at Z ≈ 0.
- `TrainBuilder` assembles: Caboose at Z=0 → Carriages → Locomotive at Z=totalLength.
- `TrainBuilder.locomotive_z` is the public accessor used for range checks.
- "Distance behind front" = `LocomotiveZ - playerZ` (positive = behind locomotive).

---

## Dimensions

| Part | Length | Width | Height | Gap to next |
|---|---|---|---|---|
| Caboose | 8 | 3 | 2.5 | 0.5 |
| Carriage | 12 | 3 | 2.5 | 0.5 |
| Locomotive | 10 | 3 | 3.0 | — |

- `CarGap = 0.5f` between every car.
- Container offset from carriage: X = +2.25 (right side outward face).
- Container size: 2.0 × 2.0 × 3.0.
- Clamp outward face at X = +1.15 on the container.
- Clamp spacing = ContainerDepth / (clampCount + 1).

---

## TrainBuilder

`TrainBuilder.gd` runs in `_ready()` and:

1. Creates a Caboose at Z=0.
2. Adds N random carriages (N ∈ [MinCarriages, MaxCarriages]).
3. Adds a Locomotive at the end.
4. For each carriage: attaches 1–3 containers, each with 2–20 random clamps.
5. For each carriage: attaches 0–2 deployers and 0–1 roof turrets (probabilities tuned in code).
6. Wires all container/deployer signals.

`TrainBuilder.rebuild()` can be called for a mid-session reset.

---

## Container lifecycle

1. Spawns with unknown cargo (orange placeholder colour).
2. Player fires a beacon → `ContainerNode.Tag()` called → cargo colour revealed, recovery chance increased.
3. All clamps on a container destroyed → container detaches:
   - Falls off train with physics.
   - Emits `CargoDetached(cargoType)` → `GameSession.OnCargoDetached()` + `TrainSpeedManager.OnContainerDetached()`.
   - Train speed increases by `SpeedIncreasePerContainer`.
4. Container HP reaches 0 (direct fire) → `ContainerDestroyed()` signal → no cargo collected.

---

## Signals wired by TrainBuilder

```
ContainerNode.CargoDetached(string) → GameSession.OnCargoDetached(string)
ContainerNode.CargoDetached(string) → TrainSpeedManager.OnContainerDetached()
ContainerNode.ContainerDestroyed()  → GameSession.OnContainerDestroyed()
```

---

## Clamp lifecycle

- Each clamp has `ClampHitpoints` HP.
- Bullets deal `TurretDamage` per hit; splash from nearby bullet impacts also damages clamps within `BlastRadius`.
- On destroy: emits `Destroyed` signal → `ContainerNode` tracks remaining clamp count.
- When the last clamp is destroyed, the container detaches.

---

## Cargo types

Cargo types are defined in `config/game_config.json` under `cargo_types[]`. Each has a `name` and a `color` (hex string). `GameConfig.cargo_types` exposes them as an `Array` of `Dictionary`.

To add a new cargo type, use the `/add-cargo-type` skill.

---

## Track / Rail

- Track rail: `BoxMesh` 0.5 × 0.5 × 1000 cross-section.
- `TrackY = 7f` (raised for under-arc clearance).
- Rail mesh Y = 6.75 so the top face is flush at Y=7.
- `TrackRailBody` is a `StaticBody3D` on collision layer 1 — bullets stop on impact.
