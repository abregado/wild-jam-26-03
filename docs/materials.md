---
layout: page
title: "Materials"
permalink: /materials/
---

# Materials

GLB files can carry embedded materials (PBR roughness/metallic textures) or
use Godot's override system.  This page explains both approaches.

---

## Option A — Embedded materials (recommended)

Export materials with the GLB.  Godot 4 imports standard glTF PBR materials
as `StandardMaterial3D` resources automatically.  Textures are embedded in the
binary GLB — no separate texture files required.

**Blender export settings:**

- Material mode: **PBR Metal/Rough**
- Texture format: **PNG** or **JPEG** (embedded in GLB)
- Check **Include → Materials**

Once imported, the material lives inside the `.import` file.  To edit it, open
the GLB scene in Godot's scene editor, click the mesh, and edit the material
in the Inspector.

---

## Option B — Override in the .tscn scene

For simpler models you can leave the GLB material-less and assign a
`StandardMaterial3D` directly in the `.tscn` scene file via the
**Material Override** property of the `MeshInstance3D`.

This is how the placeholder generator works — it produces plain white geometry
and the runtime code (or scene editor) can assign any material.

---

## Cargo container colour coding

The container colour is currently set via `ContainerNode.cs` using the
`CargoType` config value.  When you provide a final GLB for the container, the
game code sets `MaterialOverride` on the `Body` MeshInstance3D child.  Your
GLB material will be visible until the game applies its override.

To preserve your material: change `ContainerNode.cs` to only set the colour on
a specific surface index instead of using `MaterialOverride`.

---

## Texture atlasing

If you want to share one texture atlas across all train models:

1. Bake all train parts onto a single UV map and texture sheet in your DCC.
2. Export each model as a separate GLB referencing the same texture (the
   texture will be embedded in each GLB, or you can use an external `.png`
   and reference it via a relative URI in the GLB JSON — Godot supports this).
3. Alternatively, assign one shared `StandardMaterial3D` resource via Material
   Override in each `.tscn` file.
