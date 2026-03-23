---
layout: page
title: "Wiring Scenes"
permalink: /wiring-scenes/
---

# One-time scene wiring guide

After running `tools/generate_placeholders.py` and opening the project in
Godot for the first time, the `.tscn` scenes still reference inline
`BoxMesh`/`CylinderMesh` primitives.  This guide shows how to replace each
one with its GLB mesh — a one-time setup per scene.

Once wired, future GLB replacements (dropping a new file and reimporting)
update the mesh automatically without repeating these steps.

> **Note**: `PillarPool` and the caboose/locomotive are wired in GDScript
> (`PillarPool.gd` and `TrainBuilder.gd`) and load from GLB at runtime — no
> scene editing needed for those.

---

## Scenes to wire

| Scene file | GLB to use | MeshInstance3D node path |
|---|---|---|
| `scenes/train/Carriage.tscn` | `assets/models/train/carriage.glb` | `MeshSlot` |
| `scenes/train/Container.tscn` | `assets/models/train/container.glb` | `MeshSlot` |
| `scenes/train/Clamp.tscn` | `assets/models/train/clamp.glb` | `MeshSlot` |
| `scenes/player/PlayerCar.tscn` | `assets/models/player/player_car.glb` | `MeshSlot` |
| `scenes/player/Turret.tscn` | `assets/models/player/turret.glb` | `MeshSlot` or barrel nodes |
| `scenes/projectiles/Bullet.tscn` | `assets/models/projectiles/bullet.glb` | `MeshSlot` |
| `scenes/projectiles/Beacon.tscn` | `assets/models/projectiles/beacon.glb` | `MeshSlot` |

---

## Step-by-step (one scene)

These steps apply to every scene in the table above.  The example uses
`Carriage.tscn`.

### 1. Open the scene

In Godot's **FileSystem** dock, double-click
`scenes/train/Carriage.tscn`.  The scene opens in the editor with the
Scene tree on the left.

### 2. Select the MeshInstance3D node

In the **Scene** dock, click the `MeshSlot` node (or whichever node holds the
current BoxMesh/CylinderMesh — see the table above).

### 3. Locate the Mesh property

In the **Inspector** dock, find the **Mesh** property under
`MeshInstance3D`.  It currently shows something like `[BoxMesh]`.

### 4. Assign the GLB mesh

Click the **Mesh** property field.  A picker dialog opens.

- Click **Load** (or drag from the FileSystem dock).
- Navigate to `assets/models/train/carriage.glb`.
- Inside the GLB resource, select the **Body** mesh (Godot exposes
  sub-resources inside GLB files).
- Click **Open**.

The Inspector now shows the GLB mesh path instead of `[BoxMesh]`.

### 5. Save the scene

Press **Ctrl+S**.  The `.tscn` file now stores a reference to the GLB mesh
resource.

### 6. Run and verify

Press **F5**.  The carriage appears with the GLB geometry instead of the
procedural box.

---

## Notes on collision

Wiring the mesh does **not** change the existing collision shapes in the `.tscn`
files — they remain `BoxShape3D` / `CylinderShape3D` from the original setup.
This is intentional: collision shapes are tuned to game dimensions and don't
need to change when the visual mesh is updated.

If you want to use the `Body-col` auto-collision from the GLB instead (e.g.
to get a tighter-fitting convex hull), see [Collision Setup](collision.md).

---

## Updating after a GLB replacement

Once a scene is wired to a GLB, **you do not repeat these steps**.  Replacing
`carriage.glb` with a new file and pressing **Reimport** in the FileSystem
dock is all that is needed — Godot updates every scene that references the
GLB automatically.
