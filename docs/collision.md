---
layout: page
title: "Collision Setup"
permalink: /collision/
---

# Collision setup

Wild Jam 26-03 uses Godot's **GLB naming convention** to generate collision
shapes automatically at import time.  No manual CollisionShape3D nodes are
needed in the GLB.

---

## The `Body-col` convention

When Godot 4 imports a GLB it looks for mesh nodes whose names end with
`-col`.  For every such node it:

1. Creates a `StaticBody3D` sibling node.
2. Generates a `ConvexPolygonShape3D` from the mesh geometry.
3. Hides the original `-col` mesh (not rendered).

So a GLB structured like this:

```
SceneRoot [Node3D]
  Body      [MeshInstance3D]   ← rendered
  Body-col  [MeshInstance3D]   ← converted to collision, not rendered
```

produces a runtime scene like this:

```
SceneRoot [Node3D]
  Body          [MeshInstance3D]   ← rendered
  StaticBody3D                     ← auto-generated
    CollisionShape3D [ConvexPolygonShape3D]
```

The code in `PillarPool.cs` sets `CollisionLayer = 1` on the generated
`StaticBody3D` after instantiation so it participates in bullet raycasts.

---

## Collision layers used by this project

| Layer | Value | Usage |
|---|---|---|
| 1 | 1 | World / Train bodies (train cars, rail, pillars) |
| 2 | 2 | Containers |
| 3 | 4 | Clamps |
| 5 | 16 | Projectiles |

Bullet raycast mask = **7** (layers 1 + 2 + 3).  A `StaticBody3D` on layer 1
stops the bullet without dealing damage.

---

## Tips for the collision mesh

- **Keep `Body-col` convex.**  Godot generates a `ConvexPolygonShape3D`, which
  requires the mesh to be a convex hull.  Concave detail (holes, concavities)
  is silently ignored.
- **Lower poly is fine.**  The collision mesh only needs to match the rough
  shape; it is never rendered.  A 6-vertex box is a valid collision for a
  detailed carriage model.
- **Same scale as `Body`.**  Both nodes must share the same local transform
  (origin at 0, no scale offset) so the collision lines up with the visual.
- **Blender workflow** — Select your high-poly model, duplicate it, decimate or
  box-model a convex hull, rename the duplicate `Body-col`, then export both
  as a single GLB.

---

## Enabling collision import (Godot 4.6 import settings)

By default Godot enables the `-col` suffix processing for GLB scene imports.
To verify:

1. Select the `.glb` in the **FileSystem** dock.
2. Open the **Import** dock (top-right panel area).
3. Under **Meshes**, confirm **Generate Collisions** shows **On** (or that the
   `-col` suffix mode is active — see [Import Settings](import-settings.md)).
4. Click **Reimport** if you changed anything.
