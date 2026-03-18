#!/usr/bin/env python3
"""
Generate placeholder GLB files for Wild Jam 26-03.

Each GLB contains:
  - 'Body'     : visual mesh node (box / cylinder / sphere / capsule)
  - 'Body-col' : identical geometry — Godot auto-generates ConvexPolygonShape3D
                 from any mesh node whose name ends with '-col' at import time.

Usage:
  python tools/generate_placeholders.py

No dependencies beyond the Python standard library.
"""

import json
import math
import os
import struct

# ── paths ──────────────────────────────────────────────────────────────────
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_DIR   = os.path.dirname(SCRIPT_DIR)
MODELS_DIR = os.path.join(REPO_DIR, "assets", "models")


# ── geometry helpers ────────────────────────────────────────────────────────
# Each returns (verts, indices) where:
#   verts   = list of [x, y, z]  (Python floats)
#   indices = flat list of ints  (triangle list, CCW winding)

def box_geo(w, h, d):
    hw, hh, hd = w / 2, h / 2, d / 2
    verts = [
        # +X
        [ hw, -hh, -hd], [ hw, -hh,  hd], [ hw,  hh,  hd], [ hw,  hh, -hd],
        # -X
        [-hw, -hh,  hd], [-hw, -hh, -hd], [-hw,  hh, -hd], [-hw,  hh,  hd],
        # +Y
        [-hw,  hh, -hd], [ hw,  hh, -hd], [ hw,  hh,  hd], [-hw,  hh,  hd],
        # -Y
        [-hw, -hh,  hd], [ hw, -hh,  hd], [ hw, -hh, -hd], [-hw, -hh, -hd],
        # +Z
        [ hw, -hh,  hd], [-hw, -hh,  hd], [-hw,  hh,  hd], [ hw,  hh,  hd],
        # -Z
        [-hw, -hh, -hd], [ hw, -hh, -hd], [ hw,  hh, -hd], [-hw,  hh, -hd],
    ]
    indices = []
    for i in range(6):
        b = i * 4
        indices += [b, b+2, b+1,  b, b+3, b+2]
    return verts, indices


def cylinder_geo(radius, height, segments=16):
    hh  = height / 2
    tc  = segments * 2      # top-centre vertex index
    bc  = segments * 2 + 1  # bot-centre vertex index
    verts = []
    for i in range(segments):
        a = 2 * math.pi * i / segments
        verts.append([radius * math.cos(a),  hh, radius * math.sin(a)])
    for i in range(segments):
        a = 2 * math.pi * i / segments
        verts.append([radius * math.cos(a), -hh, radius * math.sin(a)])
    verts += [[0.0,  hh, 0.0], [0.0, -hh, 0.0]]   # tc, bc

    indices = []
    for i in range(segments):
        n = (i + 1) % segments
        indices += [i, n, segments+i,  n, segments+n, segments+i]  # side
        indices += [tc, n, i]                                        # top cap
        indices += [bc, segments+i, segments+n]                      # bot cap
    return verts, indices


def sphere_geo(radius, segments=16, rings=8):
    verts = []
    for r in range(rings + 1):
        phi = math.pi * r / rings
        for s in range(segments):
            theta = 2 * math.pi * s / segments
            verts.append([
                radius * math.sin(phi) * math.cos(theta),
                radius * math.cos(phi),
                radius * math.sin(phi) * math.sin(theta),
            ])
    indices = []
    for r in range(rings):
        for s in range(segments):
            ns = (s + 1) % segments
            i0, i1 =  r      * segments + s,   r      * segments + ns
            i2, i3 = (r + 1) * segments + s,  (r + 1) * segments + ns
            if r > 0:       indices += [i0, i1, i2]
            if r < rings-1: indices += [i1, i3, i2]
    return verts, indices


def capsule_geo(radius, total_height, segments=16, rings=4):
    body_h = max(0.0, total_height - 2 * radius)
    hbh    = body_h / 2
    verts  = []
    for r in range(rings + 1):          # top hemisphere
        phi = (math.pi / 2) * r / rings
        y, rr = radius * math.cos(phi) + hbh, radius * math.sin(phi)
        for s in range(segments):
            a = 2 * math.pi * s / segments
            verts.append([rr * math.cos(a), y, rr * math.sin(a)])
    for r in range(rings + 1):          # bottom hemisphere
        phi = (math.pi / 2) * r / rings
        y, rr = -(radius * math.cos(phi) + hbh), radius * math.sin(phi)
        for s in range(segments):
            a = 2 * math.pi * s / segments
            verts.append([rr * math.cos(a), y, rr * math.sin(a)])

    offset  = (rings + 1) * segments
    indices = []
    for r in range(rings):              # top hemisphere quads
        for s in range(segments):
            ns = (s + 1) % segments
            i0, i1 =  r*segments+s,   r*segments+ns
            i2, i3 = (r+1)*segments+s, (r+1)*segments+ns
            indices += [i0, i1, i2,  i1, i3, i2]
    for s in range(segments):           # equator bridge
        ns = (s + 1) % segments
        t0, t1 = rings*segments+s, rings*segments+ns
        b0, b1 = offset + rings*segments+s, offset + rings*segments+ns
        indices += [t0, t1, b0,  t1, b1, b0]
    for r in range(rings):              # bottom hemisphere quads
        for s in range(segments):
            ns = (s + 1) % segments
            i0 = offset + r*segments+s
            i1 = offset + r*segments+ns
            i2 = offset + (r+1)*segments+s
            i3 = offset + (r+1)*segments+ns
            indices += [i0, i2, i1,  i1, i2, i3]
    return verts, indices


# ── GLB writer (zero external dependencies) ─────────────────────────────────

def write_glb(path, verts, indices):
    """Write a binary GLTF (.glb) file with Body and Body-col mesh nodes."""
    # ── binary blob ──────────────────────────────────────────────────────
    vb  = struct.pack(f"{len(verts)*3}f",   *(c for v in verts   for c in v))
    ib  = struct.pack(f"{len(indices)}I",   *indices)
    pad = (4 - len(vb) % 4) % 4       # pad to 4-byte boundary between views
    bin_data = vb + b"\x00" * pad + ib

    vmin = [min(v[i] for v in verts) for i in range(3)]
    vmax = [max(v[i] for v in verts) for i in range(3)]

    # ── GLTF JSON ────────────────────────────────────────────────────────
    gltf_json = {
        "asset": {"version": "2.0", "generator": "Wild Jam 26-03 placeholder generator"},
        "scene": 0,
        "scenes":  [{"nodes": [0, 1]}],
        "nodes":   [{"name": "Body", "mesh": 0}, {"name": "Body-col", "mesh": 0}],
        "meshes":  [{"primitives": [{"attributes": {"POSITION": 0}, "indices": 1, "mode": 4}]}],
        "accessors": [
            {"bufferView": 0, "byteOffset": 0, "componentType": 5126,
             "count": len(verts),   "type": "VEC3", "min": vmin, "max": vmax},
            {"bufferView": 1, "byteOffset": 0, "componentType": 5125,
             "count": len(indices), "type": "SCALAR"},
        ],
        "bufferViews": [
            {"buffer": 0, "byteOffset": 0,              "byteLength": len(vb),  "target": 34962},
            {"buffer": 0, "byteOffset": len(vb) + pad,  "byteLength": len(ib),  "target": 34963},
        ],
        "buffers": [{"byteLength": len(bin_data)}],
    }

    # ── assemble GLB ─────────────────────────────────────────────────────
    # JSON chunk: padded to 4-byte boundary with spaces (0x20)
    json_bytes = json.dumps(gltf_json, separators=(",", ":")).encode("utf-8")
    json_pad   = (4 - len(json_bytes) % 4) % 4
    json_chunk = json_bytes + b" " * json_pad

    # BIN chunk: already 4-byte aligned above
    bin_chunk  = bin_data

    # chunk headers: [length u32][type u32]
    json_header = struct.pack("<II", len(json_chunk), 0x4E4F534A)  # "JSON"
    bin_header  = struct.pack("<II", len(bin_chunk),  0x004E4942)  # "BIN\0"

    total_length = 12 + 8 + len(json_chunk) + 8 + len(bin_chunk)
    glb_header   = struct.pack("<III", 0x46546C67, 2, total_length)  # magic, version, length

    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(glb_header)
        f.write(json_header + json_chunk)
        f.write(bin_header  + bin_chunk)
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
    "projectiles/beacon":       ("sphere",   0.15),
    "projectiles/drone_bullet": ("sphere",   0.08),

    # shape        w     h     d
    "enemies/deployer": ("box",      1.2,  0.4,  0.8),
    "enemies/drone":    ("box",      0.8,  0.25, 0.8),
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
    print(f"Done — {len(MODELS)} GLBs written.")


if __name__ == "__main__":
    main()
