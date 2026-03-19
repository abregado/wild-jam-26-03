Add a new 3D model to the GLB asset pipeline. Follow the four-step checklist used for every model in this project.

## Orientation

Read `docs/index.md` to see existing models and their dimensions. All models use a **GLB-first, procedural-fallback** pattern defined in `CLAUDE.md`.

## Steps

1. **Python generator** — read `tools/generate_placeholders.py` and add an entry to the `MODELS` dict:
   ```python
   "category/model_name": ("box"|"cylinder"|"sphere"|"capsule", *dims),
   ```
   Dimensions should match the in-game collision shape. Then run:
   ```bash
   python tools/generate_placeholders.py
   ```
   This produces `assets/models/category/model_name.glb`.

2. **C# loading with fallback** — in the script that creates/instantiates the object, add GLB loading using one of the two established patterns:

   **Scene-based** (for pooled / instanced objects, e.g. `PillarPool`):
   ```csharp
   var scene = GD.Load<PackedScene>("res://assets/models/category/model_name.glb");
   Node3D node = scene != null ? InstantiateFromGlb(scene) : CreateProcedural();
   ```
   Set `CollisionLayer` / `CollisionMask` on any auto-generated `StaticBody3D` after instantiation.

   **Mesh-only** (for inline `Node3D` construction, e.g. `TrainBuilder`):
   ```csharp
   var mesh = TryLoadGlbMesh("res://assets/models/category/model_name.glb")
              ?? new BoxMesh { Size = size };
   meshSlot.Mesh = mesh;
   ```
   Keep the manually-built `StaticBody3D` + `CollisionShape3D` for collision.

3. **Collision layer** — always set `CollisionLayer` and `CollisionMask` explicitly in code. Never rely on Godot defaults. Refer to the collision layer table in `CLAUDE.md`.

4. **Docs** — add a row to the model list table in `docs/index.md`:
   ```
   | `assets/models/category/model_name.glb` | Shape | Dimensions | Loaded by |
   ```

## GLB node convention

Every GLB must contain exactly:
- `Body` — visual mesh (rendered)
- `Body-col` — collision mesh (Godot auto-generates `ConvexPolygonShape3D`, not rendered)

## Notes
- After adding the placeholder, an artist can replace it by dropping a final `.glb` at the same path and clicking **Reimport** in Godot — no code changes needed.
- See `docs/replacing-models.md` and `docs/collision.md` for the full artist workflow.
