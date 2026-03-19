Add a new decorative environment pool — a set of objects that scroll past the player to fill out the world.

## Orientation — read these files first

- `scripts/environment/CloudPool.cs` — simplest pool: parallax speed, random sizes/heights
- `scripts/environment/RubblePool.cs` — ground-level objects, avoids track band
- `scripts/environment/RockPillarPool.cs` — distant background features, left/right pairs
- `scripts/environment/PillarPool.cs` — nearest objects, has collision (layer 1)
- `scripts/environment/TrackEnvironment.cs` — orchestrates all pools, passes `trainSpeed` each frame

## Steps

1. **Define the pool** — ask the user (if not specified):
   - What object is being pooled? (name, rough shape and size)
   - Should it have collision? (if yes, use layer 1 for world bodies)
   - How fast should it scroll relative to train speed? (1.0 = full speed, <1.0 = parallax)
   - Roughly how many objects in the pool?
   - Any placement constraints (height range, X spread, avoid certain zones)?

2. **Create the script** — create `scripts/environment/NewPool.cs` following the pattern from `CloudPool.cs` or `RubblePool.cs`:
   - Accept `float trainSpeed` in an `Update(float trainSpeed)` method called each frame
   - Move all objects in **-Z direction** each frame: `obj.Position += new Vector3(0, 0, -trainSpeed * factor * delta)`
   - Recycle: when `obj.Position.Z < -despawnBehind`, teleport to `frontZ + spacing` with randomised X/Y
   - Create objects procedurally in `_Ready()` using `MeshInstance3D` + chosen mesh type

3. **Add config fields** — add pool size, spread, speed factor, and any size/height ranges to `config/game_config.json` under `environment`. Wire each through `GameConfig.cs` using `/add-config-field`.

4. **Register in TrackEnvironment** — read `scripts/environment/TrackEnvironment.cs` and add:
   - A field `private NewPool _newPool = null!;`
   - Instantiation in `_Ready()`: `_newPool = new NewPool(); AddChild(_newPool);`
   - Update call in `_Process()`: `_newPool.Update(trainSpeed, (float)delta);`

5. **Add a placeholder model** (optional) — if the pool uses a GLB mesh instead of procedural primitives, follow `/add-model` steps. For simple decoration pools, procedural `BoxMesh` / `SphereMesh` is fine.

6. **Test** — run the game and verify:
   - Objects appear ahead of the player
   - Objects scroll backward and recycle cleanly with no visible pop-in
   - Pool does not allocate per-frame (all nodes created once in `_Ready()`)
   - Train-stopped state (speed = 0) does not cause drift

## Notes
- Keep decoration pools non-colliding (`CollisionLayer = 0`, no `StaticBody3D`) unless gameplay requires it.
- See `docs/systems/environment.md` for the full environment system reference.
