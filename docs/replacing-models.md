---
layout: page
title: "Replacing Models"
permalink: /replacing-models/
---

# Replacing a placeholder model

This is the main day-to-day workflow.  You only need to do the one-time
[scene-wiring](wiring-scenes.md) step once per scene.

---

## 1. Create your GLB

Export your model from Blender (or any DCC tool) as **Binary GLTF (.glb)**.

Required mesh node names inside the GLB:

| Node name | Purpose |
|---|---|
| `Body` | Visual mesh — what the player sees |
| `Body-col` | Collision geometry — same topology or a lower-poly convex hull |

Both nodes must be present.  The `Body-col` mesh is invisible in-game; Godot
converts it to a `ConvexPolygonShape3D` at import time (see
[Collision Setup](collision.md)).

---

## 2. Drop the file

Copy your `.glb` over the existing placeholder file at the path shown in the
[model list](index.md#model-list).  Keep the same filename.

---

## 3. Reimport in Godot

In Godot's **FileSystem** dock:

1. Right-click the `.glb` file.
2. Choose **Reimport**.
3. Godot regenerates the `.import` file.  The new mesh is live immediately.

If you want to change import settings (e.g. scale, collision type) do so in
the **Import** dock *before* clicking Reimport — see
[Import Settings](import-settings.md).

---

## 4. Run the game

Press **F5** (or the Play button).  Your model appears in place of the
placeholder.  No code changes are needed unless you change the node names
inside the GLB.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| Mesh doesn't update | Right-click → Reimport again; check the `.import` file was overwritten |
| No collision | Check `Body-col` node is present and named exactly (case-sensitive) |
| Model appears at wrong scale | Set **Scale Mesh** in Import dock, then Reimport |
| Collision shape looks wrong | Use a convex hull for `Body-col`; concave meshes need a different import mode — see [Collision Setup](collision.md) |
