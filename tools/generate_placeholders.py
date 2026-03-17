#!/usr/bin/env python3
"""
Generate placeholder GLB files for Wild Jam 26-03.

Each GLB contains:
  - 'Body'     : visual mesh node (box / cylinder / sphere / capsule)
  - 'Body-col' : identical geometry used by Godot's importer to auto-generate
                 ConvexPolygonShape3D collision (the -col suffix convention)

Usage:
  python tools/generate_placeholders.py

Requires:
  pip install pygltflib numpy
"""

import os
import numpy as np
from pygltflib import (
    GLTF2, Asset, Scene, Node, Mesh, Primitive, Attributes,
    Accessor, BufferView, Buffer,
    FLOAT, UNSIGNED_INT, ARRAY_BUFFER, ELEMENT_ARRAY_BUFFER,
)

# ── paths ──────────────────────────────────────────────────────────────────
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_DIR   = os.path.dirname(SCRIPT_DIR)
MODELS_DIR = os.path.join(REPO_DIR, "assets", "models")


# ── geometry helpers ────────────────────────────────────────────────────────

def box_geo(w: float, h: float, d: float):
    """Axis-aligned box centred at origin, 24 unique vertices (4 per face)."""
    hw, hh, hd = w / 2, h / 2, d / 2
    v = np.array([
        # +X face
        [ hw, -hh, -hd], [ hw, -hh,  hd], [ hw,  hh,  hd], [ hw,  hh, -hd],
        # -X face
        [-hw, -hh,  hd], [-hw, -hh, -hd], [-hw,  hh, -hd], [-hw,  hh,  hd],
        # +Y face
        [-hw,  hh, -hd], [ hw,  hh, -hd], [ hw,  hh,  hd], [-hw,  hh,  hd],
        # -Y face
        [-hw, -hh,  hd], [ hw, -hh,  hd], [ hw, -hh, -hd], [-hw, -hh, -hd],
        # +Z face
        [ hw, -hh,  hd], [-hw, -hh,  hd], [-hw,  hh,  hd], [ hw,  hh,  hd],
        # -Z face
        [-hw, -hh, -hd], [ hw, -hh, -hd], [ hw,  hh, -hd], [-hw,  hh, -hd],
    ], dtype=np.float32)
    face = np.array([[0, 1, 2], [0, 2, 3]], dtype=np.uint32)
    idx  = np.concatenate([face + 4 * i for i in range(6)]).ravel()
    return v, idx.astype(np.uint32)


def cylinder_geo(radius: float, height: float, segments: int = 16):
    """Capped cylinder centred at origin."""
    hh  = height / 2
    ang = np.linspace(0, 2 * np.pi, segments, endpoint=False)
    xs, zs = radius * np.cos(ang), radius * np.sin(ang)

    top_ring = np.column_stack([xs,  np.full(segments,  hh), zs]).astype(np.float32)
    bot_ring = np.column_stack([xs,  np.full(segments, -hh), zs]).astype(np.float32)
    top_cap  = np.array([[0.0,  hh, 0.0]], dtype=np.float32)
    bot_cap  = np.array([[0.0, -hh, 0.0]], dtype=np.float32)
    verts    = np.vstack([top_ring, bot_ring, top_cap, bot_cap])   # len = 2*seg+2

    tc = segments * 2      # top-centre index
    bc = segments * 2 + 1  # bot-centre index

    tris = []
    for i in range(segments):
        n = (i + 1) % segments
        # side quad (two tris)
        tris += [i,    n,           segments + i]
        tris += [n,    segments+n,  segments + i]
        # top cap fan
        tris += [tc, i, n]
        # bottom cap fan (reverse winding to face outward)
        tris += [bc, segments + n, segments + i]

    return verts, np.array(tris, dtype=np.uint32)


def sphere_geo(radius: float, segments: int = 16, rings: int = 8):
    """UV sphere centred at origin."""
    verts = []
    for r in range(rings + 1):
        phi = np.pi * r / rings
        for s in range(segments):
            theta = 2 * np.pi * s / segments
            verts.append([
                radius * np.sin(phi) * np.cos(theta),
                radius * np.cos(phi),
                radius * np.sin(phi) * np.sin(theta),
            ])
    verts = np.array(verts, dtype=np.float32)

    tris = []
    for r in range(rings):
        for s in range(segments):
            ns = (s + 1) % segments
            i0, i1 =  r      * segments + s,   r      * segments + ns
            i2, i3 = (r + 1) * segments + s,  (r + 1) * segments + ns
            if r > 0:
                tris += [i0, i2, i1]
            if r < rings - 1:
                tris += [i1, i2, i3]

    return verts, np.array(tris, dtype=np.uint32)


def capsule_geo(radius: float, total_height: float, segments: int = 16, rings: int = 4):
    """
    Capsule centred at origin.  total_height includes both hemispheres.
    Constructed as: top hemisphere  +  equator bridge  +  bottom hemisphere.
    """
    body_h = max(0.0, total_height - 2 * radius)
    hbh    = body_h / 2
    verts  = []

    # top hemisphere  (phi: 0 → π/2,  pole → equator, y positive)
    for r in range(rings + 1):
        phi  = (np.pi / 2) * r / rings
        y    =  radius * np.cos(phi) + hbh
        rr   =  radius * np.sin(phi)
        for s in range(segments):
            theta = 2 * np.pi * s / segments
            verts.append([rr * np.cos(theta), y, rr * np.sin(theta)])

    # bottom hemisphere (phi: 0 → π/2,  equator → pole, y negative)
    for r in range(rings + 1):
        phi  = (np.pi / 2) * r / rings
        y    = -(radius * np.cos(phi) + hbh)
        rr   =  radius * np.sin(phi)
        for s in range(segments):
            theta = 2 * np.pi * s / segments
            verts.append([rr * np.cos(theta), y, rr * np.sin(theta)])

    verts  = np.array(verts, dtype=np.float32)
    offset = (rings + 1) * segments   # start index of bottom hemisphere

    tris = []

    # top hemisphere quads
    for r in range(rings):
        for s in range(segments):
            ns = (s + 1) % segments
            i0, i1 =  r      * segments + s,   r      * segments + ns
            i2, i3 = (r + 1) * segments + s,  (r + 1) * segments + ns
            tris += [i0, i2, i1,  i1, i2, i3]

    # equator bridge: top ring[rings] ↔ bottom ring[0]
    top_eq = rings * segments
    bot_eq = offset
    for s in range(segments):
        ns = (s + 1) % segments
        t0, t1 = top_eq + s, top_eq + ns
        b0, b1 = bot_eq + s, bot_eq + ns
        tris += [t0, b0, t1,  t1, b0, b1]

    # bottom hemisphere quads (reversed winding so normals face out)
    for r in range(rings):
        for s in range(segments):
            ns = (s + 1) % segments
            i0 = offset +  r      * segments + s
            i1 = offset +  r      * segments + ns
            i2 = offset + (r + 1) * segments + s
            i3 = offset + (r + 1) * segments + ns
            tris += [i0, i1, i2,  i1, i3, i2]

    return verts, np.array(tris, dtype=np.uint32)


# ── GLB writer ──────────────────────────────────────────────────────────────

def write_glb(path: str, verts: np.ndarray, indices: np.ndarray) -> None:
    """Write a minimal GLB with two mesh nodes: 'Body' and 'Body-col'."""
    vb  = verts.astype(np.float32).tobytes()
    ib  = indices.astype(np.uint32).tobytes()
    pad = (4 - len(vb) % 4) % 4        # align index buffer to 4-byte boundary
    blob = vb + b"\x00" * pad + ib

    gltf = GLTF2()
    gltf.asset = Asset(version="2.0", generator="Wild Jam 26-03 placeholder generator")

    gltf.buffers = [Buffer(byteLength=len(blob))]

    gltf.bufferViews = [
        BufferView(buffer=0, byteOffset=0,
                   byteLength=len(vb), target=ARRAY_BUFFER),
        BufferView(buffer=0, byteOffset=len(vb) + pad,
                   byteLength=len(ib), target=ELEMENT_ARRAY_BUFFER),
    ]

    vmin = verts.min(axis=0).tolist()
    vmax = verts.max(axis=0).tolist()

    gltf.accessors = [
        Accessor(bufferView=0, byteOffset=0, componentType=FLOAT,
                 count=len(verts),   type="VEC3", min=vmin, max=vmax),
        Accessor(bufferView=1, byteOffset=0, componentType=UNSIGNED_INT,
                 count=len(indices), type="SCALAR"),
    ]

    prim = Primitive(attributes=Attributes(POSITION=0), indices=1, mode=4)
    gltf.meshes = [Mesh(primitives=[prim])]

    # Two scene nodes sharing the same mesh
    gltf.nodes  = [Node(name="Body", mesh=0), Node(name="Body-col", mesh=0)]
    gltf.scenes = [Scene(nodes=[0, 1])]
    gltf.scene  = 0

    gltf.set_binary_blob(blob)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    gltf.save_binary(path)
    print(f"  {os.path.relpath(path, REPO_DIR)}")


# ── model catalogue ─────────────────────────────────────────────────────────
#   key  = relative path under assets/models/ (without .glb)
#   value = (shape, *dimensions)
#
#   Dimensions match current primitive sizes exactly (from TrainBuilder.cs,
#   Carriage.tscn, Container.tscn, Clamp.tscn, PillarPool.cs, etc.)

MODELS = {
    # shape        w     h     d
    "train/carriage":     ("box",      3.0,  2.5, 12.0),
    "train/container":    ("box",      2.0,  2.0,  3.0),
    "train/locomotive":   ("box",      3.0,  3.0, 10.0),
    "train/caboose":      ("box",      3.0,  2.5,  8.0),
    "player/player_car":  ("box",      3.0,  0.6,  1.5),

    # shape        radius  height
    "train/clamp":        ("cylinder", 0.2,   0.35),
    "player/turret":      ("cylinder", 0.09,  0.8),
    "environment/pillar": ("cylinder", 0.3,  10.0),

    # shape        radius  total_height
    "projectiles/bullet": ("capsule",  0.18,  0.7),

    # shape        radius
    "projectiles/beacon": ("sphere",   0.15),
}


def main():
    print(f"Generating placeholder GLBs → {MODELS_DIR}")
    for rel, spec in MODELS.items():
        shape, *dims = spec
        if   shape == "box":      v, idx = box_geo(*dims)
        elif shape == "cylinder": v, idx = cylinder_geo(*dims)
        elif shape == "sphere":   v, idx = sphere_geo(*dims)
        elif shape == "capsule":  v, idx = capsule_geo(*dims)
        else:
            raise ValueError(f"Unknown shape: {shape!r}")
        write_glb(os.path.join(MODELS_DIR, rel + ".glb"), v, idx)
    print("Done — 10 GLBs written.")


if __name__ == "__main__":
    main()
