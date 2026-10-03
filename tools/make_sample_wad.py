#!/usr/bin/env python3
"""Generates assets/samples/sample.wad: a small Doom-format map (MAP01) used to try the editor without an IWAD.

Two rooms joined by a corridor, a pillar in the second room, and a few things.
Run from the repository root:  python3 tools/make_sample_wad.py
"""
import struct

# vertices
V = [(0, 0), (256, 0), (256, 64), (256, 128), (256, 192), (0, 192),   # 0-5   room A
     (384, 64), (384, 128),                                            # 6-7   corridor end
     (384, 0), (640, 0), (640, 192), (384, 192),                       # 8-11  room B
     (480, 64), (544, 64), (544, 128), (480, 128)]                     # 12-15 pillar

# (v1, v2, front sector, back sector or -1); Doom's front side is on the right of v1 -> v2
L = [(0, 5, 0, -1), (5, 4, 0, -1), (4, 3, 0, -1), (3, 2, 0, 1), (2, 1, 0, -1), (1, 0, 0, -1),   # room A
     (3, 7, 1, -1), (7, 6, 1, 2), (6, 2, 1, -1),                                                # corridor
     (8, 6, 2, -1), (7, 11, 2, -1), (11, 10, 2, -1), (10, 9, 2, -1), (9, 8, 2, -1),             # room B
     (12, 15, 3, 2), (15, 14, 3, 2), (14, 13, 3, 2), (13, 12, 3, 2)]                            # pillar

# floor, ceiling, floor tex, ceiling tex, light, special, tag
S = [(0, 128, "FLOOR4_8", "CEIL3_5", 192, 0, 0),
     (0, 96, "FLOOR0_1", "CEIL3_5", 160, 0, 0),
     (16, 160, "FLOOR4_8", "CEIL3_5", 224, 0, 0),
     (64, 64, "FLAT1", "FLAT1", 255, 0, 0)]

# x, y, angle, type, flags
T = [(64, 96, 0, 1, 7), (200, 32, 90, 2001, 7), (300, 96, 180, 3001, 7),
     (500, 32, 90, 3004, 7), (590, 160, 270, 3004, 7), (420, 100, 0, 2035, 7)]


def name8(n):
    return n.encode("ascii").ljust(8, b"\0")


def build():
    sides = []
    lines = b""
    for v1, v2, front, back in L:
        two = back >= 0
        flags = 4 if two else 1   # two-sided / impassable
        fidx = len(sides)
        sides.append((front, "-" if two else "STARTAN1"))
        bidx = -1
        if two:
            bidx = len(sides)
            sides.append((back, "-"))
        lines += struct.pack("<hhhhhhh", v1, v2, flags, 0, 0, fidx, bidx)

    sidedefs = b"".join(struct.pack("<hh8s8s8sh", 0, 0, name8("-"), name8("-"), name8(mid), sector) for sector, mid in sides)
    vertexes = b"".join(struct.pack("<hh", x, y) for x, y in V)
    sectors = b"".join(struct.pack("<hh8s8shhh", f, c, name8(ft), name8(ct), light, sp, tag) for f, c, ft, ct, light, sp, tag in S)
    things = b"".join(struct.pack("<hhhhh", *t) for t in T)

    lumps = [("MAP01", b""), ("THINGS", things), ("LINEDEFS", lines), ("SIDEDEFS", sidedefs),
             ("VERTEXES", vertexes), ("SECTORS", sectors)]

    data = b""
    directory = b""
    offset = 12
    for name, content in lumps:
        directory += struct.pack("<ii8s", offset, len(content), name8(name))
        data += content
        offset += len(content)
    return b"PWAD" + struct.pack("<ii", len(lumps), offset) + data + directory


if __name__ == "__main__":
    out = "assets/samples/sample.wad"
    with open(out, "wb") as f:
        f.write(build())
    print("wrote", out)
