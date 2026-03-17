---
layout: page
title: "Import Settings"
permalink: /import-settings/
---

# Godot GLB import settings reference

When you select a `.glb` file in the **FileSystem** dock and open the
**Import** dock, Godot shows a panel with several sections.  The settings
relevant to this project are listed below.

To apply any change: adjust the setting, then click **Reimport**.

---

## Root Node

| Setting | Recommended value |
|---|---|
| Root Type | `Node3D` (default) |
| Root Name | leave blank (uses the filename) |

---

## Nodes

| Setting | Notes |
|---|---|
| Import as Scene | keep checked |
| Generate LODs | optional — Godot auto-generates LOD meshes if checked |

---

## Meshes

| Setting | Recommended value | Notes |
|---|---|---|
| Ensure Tangents | on | Required for normal maps |
| Generate Shadow Meshes | optional | Adds shadow-casting duplicates |
| **Generate Collisions** | depends | See below |
| Light Baking | Disabled | No lightmap in this project |

### Collision generation

This project uses the **`-col` mesh suffix** convention rather than the
automatic collision generator.  That means:

- **Generate Collisions → Off** is correct if your GLB contains a `Body-col`
  mesh node (the `-col` suffix is processed regardless of this setting in
  Godot 4.x).
- If your GLB does **not** contain a `Body-col` mesh and you want Godot to
  generate collision from the `Body` mesh, set **Generate Collisions → On** and
  choose **Convex Polygon** for best performance.

---

## Animation

Not used in this project.  Leave all animation settings at their defaults.

---

## Scale

| Setting | Default | Notes |
|---|---|---|
| Scale Mesh | 1.0 | Multiply all vertex positions. Use if your exporter uses different units (e.g. centimetres) |

**Unit convention**: Godot uses **metres** internally, but this project's world
units are arbitrary.  The placeholder models are built exactly to match the
existing BoxMesh/CylinderMesh sizes (see the [model list](index.md#model-list)).
If your DCC exports in centimetres, set Scale Mesh to **0.01**.

---

## Saving overrides

Import settings are stored in `assets/models/**/*.glb.import`.  These files
are committed to git, so your team shares the same import configuration.
Never delete `.import` files — regenerate them by pressing **Reimport**.
