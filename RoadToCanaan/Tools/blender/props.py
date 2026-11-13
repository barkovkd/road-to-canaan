"""Set dressing: barrels, crates, furniture, wagons and the rest. Run inside Blender:
    Tools/blender/bmcp.py run Tools/blender/props.py [only=name,name]
Builds every model into the collection "Props", lays them out in rows and exports Assets/Canaan/Models/props.fbx.
"""
import importlib
import math

import kit

importlib.reload(kit)
from kit import Model, Pal, shade, mixc

BUILDERS = []


def model(fn):
    BUILDERS.append(fn)
    return fn


def lerp(a, b, t):
    return a + (b - a) * t


def at(profile, z):
    """Radius of a (radius, height) profile at a height."""
    for (r0, z0), (r1, z1) in zip(profile, profile[1:]):
        if z0 <= z <= z1:
            return lerp(r0, r1, (z - z0) / max(z1 - z0, 1e-6))
    return profile[-1][0]


def hoop(m, profile, z0, z1, thick=0.007):
    m.lathe((0, 0, 0), [(at(profile, z0) + 0.001, z0), (at(profile, z0) + thick, z0 + 0.004), (at(profile, z1) + thick, z1 - 0.004), (at(profile, z1) + 0.001, z1)], 28)


@model
def barrel():
    m = Model("barrel")
    body = [(0.250, 0.0), (0.285, 0.12), (0.318, 0.34), (0.325, 0.46), (0.318, 0.58), (0.285, 0.80), (0.250, 0.92)]
    m.surface("planks", Pal.WoodOld)
    m.lathe((0, 0, 0), [(0.0, 0.012)] + [(0.236, 0.012), (0.240, 0.0)] + body + [(0.240, 0.92), (0.236, 0.885), (0.0, 0.885)], 28)
    m.surface("iron", Pal.Iron, 0.03)
    for z0, z1 in ((0.03, 0.075), (0.21, 0.25), (0.67, 0.71), (0.845, 0.89)):
        hoop(m, body, z0, z1)
    m.surface("boards", shade(Pal.WoodDark, 1.1))
    m.cyl((0.325, 0, 0.46), (0.345, 0, 0.46), 0.022, 0.017, 10)          # the bung
    return m


@model
def crate():
    m = Model("crate")
    s = 0.72
    h = s / 2
    m.surface("planks", Pal.WoodNew, 0.09)
    # five boards a side, with gaps; lid and bottom
    n = 5
    w = (s - 0.05) / n
    for side in range(4):
        m.push((0, 0, h), (0, 0, side * 90))
        for i in range(n):
            m.box((-h + 0.025 + w * (i + 0.5), -h + 0.012, 0), (w - 0.008, 0.02, s - 0.03), 0.004, "z")
        m.pop()
    for i in range(n):
        m.box((-h + 0.025 + w * (i + 0.5), 0, s - 0.012), (w - 0.008, s - 0.03, 0.02), 0.004, "y")
        m.box((-h + 0.025 + w * (i + 0.5), 0, 0.012), (w - 0.008, s - 0.03, 0.02), 0.004, "y")
    # corner posts and battens
    m.surface("timber", shade(Pal.WoodOld, 0.92), 0.06)
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box((sx * (h - 0.02), sy * (h - 0.02), h), (0.05, 0.05, s), 0.006, "z")
    for side in range(4):
        m.push((0, 0, h), (0, 0, side * 90))
        for z in (-h + 0.06, h - 0.06):
            m.box((0, -h - 0.004, z), (s - 0.04, 0.02, 0.07), 0.005, "x")
        m.pop()
    m.surface("iron", Pal.Iron, 0.02)
    for side in range(4):
        m.push((0, 0, h), (0, 0, side * 90))
        for z in (-h + 0.06, h - 0.06):
            for x in (-h + 0.07, h - 0.07):
                m.cyl((x, -h - 0.014, z), (x, -h - 0.018, z), 0.008, 0.006, 8, 0)
        m.pop()
    return m


@model
def sack():
    m = Model("sack")
    m.surface("canvas", hexc(0xA88E66), 0.06)                          # burlap
    m.blob((0, 0, 0.24), (0.27, 0.2, 0.3), 2, squash=0.8, lumps=0.1)
    m.surface("canvas", hexc(0x6E4E36), 0.03)                          # the miller's stripe
    m.tube([(0.275 * math.cos(a), 0.205 * math.sin(a), 0.3) for a in [i * math.pi / 12 for i in range(24)]], 0.012, 5, closed=True)
    m.surface("canvas", hexc(0xA88E66), 0.06)
    m.blob((0, 0, 0.52), (0.1, 0.085, 0.09), 1, lumps=0.2)             # the tied neck
    m.surface("straw", Pal.Rope, 0.05)
    m.tube([(0.085 * math.cos(a), 0.07 * math.sin(a), 0.47) for a in [i * math.pi / 6 for i in range(12)]], 0.009, 6, closed=True)
    return m


# ---------------------------------------------------------------------- helpers in the game's axes
# The code that places props thinks in x right, y up, z forward; these wrap the kit so sizes read the same way.
GRAIN = {"x": "x", "y": "z", "z": "y"}
CANVAS = 12 / 255.0          # colour alpha 12: canvas the game may dye (wagon covers, tents)


def G(x, y, z):
    return kit.L(x, y, z)


def gbox(m, c, size, bevel=0.008, grain=None, rot=None):
    if grain is None:
        grain = "xyz"[max(range(3), key=lambda i: size[i])]
    r = None if rot is None else (-rot[0], -rot[2], rot[1])
    return m.box(G(*c), (size[0], size[2], size[1]), bevel, GRAIN[grain], r)


def gbeam(m, a, b, width, height, bevel=0.006):
    return m.beam(G(*a), G(*b), width, height, bevel, up=(0, 0, 1))


def gcyl(m, a, b, r0, r1=None, seg=16, bevel=0.004):
    return m.cyl(G(*a), G(*b), r0, r1, seg, bevel)


def wheel(m, c, radius, spokes=12):
    """A spoked wagon wheel on the x axis: felloes, iron tyre, hub and spokes."""
    x, y, z = c
    rim = []
    for i in range(24):
        a = 2 * math.pi * i / 24
        rim.append([G(x + dx, y + math.cos(a) * (radius + dr), z + math.sin(a) * (radius + dr))
                    for dx, dr in ((-0.03, -0.075), (0.03, -0.075), (0.03, -0.008), (-0.03, -0.008))])
    m.surface("timber", shade(Pal.WoodOld, 0.95), 0.05)
    m.loft(rim + [rim[0]], False, 0)
    tyre = []
    for i in range(24):
        a = 2 * math.pi * i / 24
        tyre.append([G(x + dx, y + math.cos(a) * (radius + dr), z + math.sin(a) * (radius + dr))
                     for dx, dr in ((-0.034, -0.01), (0.034, -0.01), (0.034, 0.0), (-0.034, 0.0))])
    m.surface("iron", Pal.Iron, 0.03)
    m.loft(tyre + [tyre[0]], False, 0)
    gcyl(m, (x - 0.07, y, z), (x + 0.07, y, z), 0.045, 0.045, 10)
    m.surface("timber", Pal.WoodDark, 0.05)
    gcyl(m, (x - 0.055, y, z), (x + 0.055, y, z), 0.085, 0.07, 12)
    m.surface("boards", shade(Pal.WoodOld, 1.05), 0.06)
    for i in range(spokes):
        a = 2 * math.pi * (i + 0.5) / spokes
        gcyl(m, (x, y + math.cos(a) * 0.07, z + math.sin(a) * 0.07), (x, y + math.cos(a) * (radius - 0.07), z + math.sin(a) * (radius - 0.07)), 0.02, 0.016, 6, 0)


@model
def chair():
    m = Model("chair")
    m.surface("boards", Pal.WoodOld, 0.07)
    for i in range(3):
        gbox(m, (-0.14 + i * 0.14, 0.45, 0.0), (0.135, 0.035, 0.42), 0.006, "z")
    for x in (-0.18, 0.18):
        gbox(m, (x, 0.215, 0.18), (0.04, 0.43, 0.04), 0.006)
        gbeam(m, (x, 0.0, -0.20), (x * 0.96, 0.98, -0.235), 0.04, 0.04)
        gbox(m, (x, 0.17, 0.0), (0.022, 0.03, 0.34), 0.004)
    gbox(m, (0, 0.20, 0.18), (0.34, 0.03, 0.022), 0.004)
    gbox(m, (0, 0.62, -0.213), (0.34, 0.06, 0.022), 0.004)
    gbox(m, (0, 0.82, -0.226), (0.36, 0.12, 0.024), 0.008)
    gbox(m, (0, 0.95, -0.234), (0.40, 0.05, 0.03), 0.01)
    return m


@model
def table():
    m = Model("table")
    m.surface("boards", Pal.WoodOld, 0.06)
    m.lathe((0, 0, 0), [(0.0, 0.725), (0.54, 0.725), (0.55, 0.735), (0.55, 0.765), (0.54, 0.775), (0.0, 0.775)], 28)
    m.surface("boards", shade(Pal.WoodDark, 1.15), 0.05)
    m.lathe((0, 0, 0), [(0.0, 0.05), (0.07, 0.05), (0.075, 0.12), (0.05, 0.2), (0.062, 0.36), (0.045, 0.52), (0.065, 0.66), (0.20, 0.70), (0.20, 0.725), (0.0, 0.725)], 16)
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        gbeam(m, (math.cos(a) * 0.05, 0.11, math.sin(a) * 0.05), (math.cos(a) * 0.30, 0.025, math.sin(a) * 0.30), 0.06, 0.05)
    return m


@model
def bench():
    m = Model("bench")
    m.surface("planks", Pal.WoodOld, 0.07)
    for z in (-0.09, 0.09):
        gbox(m, (0, 0.45, z), (1.6, 0.045, 0.17), 0.008, "x")
    for y in (0.66, 0.82):
        gbox(m, (0, y, -0.185), (1.6, 0.13, 0.03), 0.008, "x")
    m.surface("timber", Pal.WoodDark, 0.06)
    for x in (-0.72, 0.72):
        gbox(m, (x, 0.215, 0.13), (0.06, 0.43, 0.06), 0.008)
        gbeam(m, (x, 0.0, -0.15), (x, 0.92, -0.20), 0.06, 0.06)
        gbox(m, (x, 0.40, -0.01), (0.05, 0.05, 0.30), 0.006)
        gbox(m, (x, 0.60, 0.0), (0.05, 0.04, 0.36), 0.008)
    return m


@model
def bed():
    m = Model("bed")
    m.surface("boards", Pal.WoodDark, 0.05)
    for x in (-0.43, 0.43):
        gbox(m, (x, 0.30, 0.0), (0.04, 0.14, 1.95), 0.008, "z")
        gbox(m, (x, 0.60, -0.955), (0.07, 1.20, 0.07), 0.012)
        gbox(m, (x, 0.32, 0.955), (0.07, 0.64, 0.07), 0.012)
    gbox(m, (0, 0.72, -0.955), (0.80, 0.50, 0.035), 0.008, "x")
    gbox(m, (0, 1.02, -0.955), (0.86, 0.07, 0.05), 0.012, "x")
    gbox(m, (0, 0.40, 0.955), (0.80, 0.30, 0.035), 0.008, "x")
    gbox(m, (0, 0.26, 0.0), (0.84, 0.03, 1.90), 0.004, "z")
    m.surface("cloth", Pal.ClothCream, 0.03)
    gbox(m, (0, 0.36, 0.02), (0.82, 0.16, 1.84), 0.05)
    m.push(G(0, 0.48, -0.70))
    m.blob((0, 0, 0), (0.30, 0.19, 0.06), 2, lumps=0.04)
    m.pop()
    m.surface("wool", Pal.ClothRed, 0.04)
    gbox(m, (0, 0.445, 0.30), (0.88, 0.035, 1.22), 0.015)
    gbox(m, (0, 0.455, -0.29), (0.88, 0.045, 0.10), 0.02)
    return m


@model
def hitchrail():
    m = Model("hitchrail")
    m.surface("timber", Pal.WoodDark, 0.06)
    for x in (-1.35, 1.35):
        gcyl(m, (x, 0.0, 0.0), (x, 1.08, 0.0), 0.075, 0.065, 12, 0.012)
    m.surface("timber", shade(Pal.WoodOld, 0.9), 0.06)
    gcyl(m, (-1.5, 1.0, 0.0), (1.5, 1.0, 0.0), 0.055, 0.05, 12, 0.008)
    m.surface("iron", Pal.Iron, 0.03)
    for x in (-1.35, 1.35):
        gcyl(m, (x, 0.955, 0.0), (x, 1.045, 0.0), 0.08, 0.08, 12, 0.003)
    return m


@model
def trough():
    m = Model("trough")
    m.surface("planks", Pal.WoodOld, 0.08)
    gbox(m, (0, 0.10, 0), (1.76, 0.04, 0.47), 0.006, "x")
    for z in (-0.255, 0.255):
        gbox(m, (0, 0.30, z), (1.80, 0.44, 0.04), 0.008, "x", rot=(8.0 if z > 0 else -8.0, 0, 0))
    for x in (-0.88, 0.88):
        gbox(m, (x, 0.30, 0), (0.04, 0.44, 0.52), 0.008, "z")
        gbox(m, (x * 0.93, 0.25, 0), (0.07, 0.50, 0.62), 0.01, "y")
    m.surface("iron", Pal.Iron, 0.03)
    for x in (-0.5, 0.5):
        gbox(m, (x, 0.30, 0.285), (0.04, 0.44, 0.006), 0.001)
        gbox(m, (x, 0.30, -0.285), (0.04, 0.44, 0.006), 0.001)
    m.surface("steel", hexc(0x3F5B63), 0.0)
    gbox(m, (0, 0.44, 0), (1.70, 0.01, 0.44), 0.0)
    return m


@model
def lamppost():
    m = Model("lamppost")
    m.surface("timber", Pal.WoodDark, 0.05)
    gbox(m, (0, 1.5, 0), (0.12, 3.0, 0.12), 0.015)
    gbox(m, (0, 0.25, 0), (0.17, 0.5, 0.17), 0.02)
    gbox(m, (0.27, 2.9, 0), (0.62, 0.06, 0.06), 0.008)
    gbeam(m, (0.03, 2.55, 0), (0.36, 2.88, 0), 0.04, 0.04)
    m.surface("iron", Pal.Iron, 0.02)
    gcyl(m, (0.5, 2.87, 0), (0.5, 2.80, 0), 0.008, 0.008, 6, 0)
    m.lathe(G(0.5, 2.74, 0), [(0.0, 0.085), (0.03, 0.07), (0.13, 0.02), (0.14, 0.0), (0.0, 0.0)], 4)
    m.lathe(G(0.5, 2.47, 0), [(0.0, 0.0), (0.11, 0.0), (0.12, 0.02), (0.0, 0.03)], 4)
    for sx in (-1, 1):
        for sz in (-1, 1):
            gbox(m, (0.5 + sx * 0.085, 2.62, sz * 0.085), (0.012, 0.25, 0.012), 0.002)
    return m


@model
def well():
    m = Model("well")
    m.surface("stone", Pal.Stone, 0.07)
    m.lathe((0, 0, 0), [(0.86, 0.0), (0.88, 0.05), (0.86, 0.72), (0.90, 0.74), (0.90, 0.82), (0.68, 0.82), (0.68, 0.30)], 24)
    m.surface("steel", hexc(0x1E2A30), 0.0)
    m.lathe((0, 0, 0), [(0.0, 0.50), (0.68, 0.50)], 24)
    m.surface("timber", Pal.WoodDark, 0.05)
    for x in (-0.8, 0.8):
        gbox(m, (x, 1.42, 0), (0.12, 2.04, 0.12), 0.015)
    gcyl(m, (-0.8, 2.1, 0), (0.8, 2.1, 0), 0.07, 0.07, 12, 0.006)
    m.surface("iron", Pal.Iron, 0.03)
    m.tube([G(0.86, 2.1, 0), G(1.0, 2.1, 0), G(1.0, 1.88, 0), G(1.12, 1.88, 0)], 0.014, 6)
    m.surface("straw", Pal.Rope, 0.05)
    for i in range(7):
        gcyl(m, (-0.21 + i * 0.06, 2.1, 0), (-0.15 + i * 0.06, 2.1, 0), 0.083, 0.083, 10, 0.006)
    m.tube([G(0.0, 2.02, 0.03), G(0.0, 1.40, 0.02), G(0.0, 1.22, 0.0)], 0.011, 6)
    m.surface("planks", Pal.WoodOld, 0.06)
    m.lathe(G(0, 0.92, 0), [(0.0, 0.0), (0.12, 0.0), (0.145, 0.28), (0.13, 0.28), (0.11, 0.02), (0.0, 0.02)], 12)
    m.surface("shingles", hexc(0x4A3F38), 0.08)
    # the two pitches of the roof fall away from the ridge (gbox turns about x the other way from the game's
    # right hand: a positive tilt lifts the -z edge)
    for sz, tilt in ((-1, 32.0), (1, -32.0)):
        gbox(m, (0, 2.55, sz * 0.40), (2.0, 0.05, 1.02), 0.006, "z", rot=(tilt, 0, 0))
    m.surface("timber", Pal.WoodDark, 0.05)
    gbox(m, (0, 2.83, 0), (2.04, 0.07, 0.07), 0.01)
    return m


@model
def wagon():
    m = Model("wagon")
    m.surface("planks", Pal.WoodOld, 0.08)
    for i in range(5):
        gbox(m, (-0.54 + i * 0.27, 1.05, 0), (0.262, 0.06, 3.6), 0.006, "z")
    for x in (-0.67, 0.67):
        for j in range(3):
            gbox(m, (x, 1.19 + j * 0.215, 0), (0.045, 0.205, 3.6), 0.006, "z")
    for j in range(3):
        gbox(m, (0, 1.19 + j * 0.215, -1.80), (1.30, 0.205, 0.045), 0.006, "x")
    for j in range(2):
        gbox(m, (0, 1.19 + j * 0.215, 1.80), (1.30, 0.205, 0.045), 0.006, "x")
    m.surface("timber", Pal.WoodDark, 0.06)
    for x in (-0.70, 0.70):
        for z in (-1.5, -0.5, 0.5, 1.5):
            gbox(m, (x, 1.40, z), (0.035, 0.66, 0.06), 0.006)
    gbox(m, (0, 1.75, 1.55), (1.30, 0.05, 0.40), 0.01, "x")
    gbox(m, (0, 1.92, 1.36), (1.30, 0.28, 0.04), 0.01, "x")
    for x in (-0.6, 0.6):
        gbox(m, (x, 1.66, 1.55), (0.04, 0.16, 0.36), 0.006)
    gbox(m, (0, 0.98, 0), (0.14, 0.10, 2.9), 0.012, "z")
    for z, y in ((-1.25, 0.75), (1.25, 0.60)):
        gbox(m, (0, y, z), (1.66, 0.10, 0.10), 0.012, "x")
        gbox(m, (0, (y + 0.98) / 2 + 0.02, z), (1.0, 0.98 - y - 0.02, 0.12), 0.012, "x")
    gbeam(m, (0, 0.60, 1.30), (0, 0.75, 4.10), 0.08, 0.08)
    gbox(m, (0, 0.74, 3.9), (1.1, 0.05, 0.06), 0.008, "x")
    wheel(m, (-0.85, 0.75, -1.25), 0.75, 14)
    wheel(m, (0.85, 0.75, -1.25), 0.75, 14)
    wheel(m, (-0.85, 0.60, 1.25), 0.60, 12)
    wheel(m, (0.85, 0.60, 1.25), 0.60, 12)
    m.surface("iron", Pal.Iron, 0.03)
    gbox(m, (0.74, 1.30, 1.0), (0.02, 0.30, 0.03), 0.002)
    m.surface("planks", shade(Pal.WoodOld, 0.9), 0.06)
    m.lathe(G(0.86, 1.30, 0.55), [(0.0, 0.0), (0.16, 0.0), (0.19, 0.2), (0.16, 0.42), (0.0, 0.42)], 14)       # water keg on the side
    return m


@model
def wagon_cover():
    m = Model("wagon_cover")
    m.surface("canvas", (1.0, 1.0, 1.0), 0.03)
    m.wind = CANVAS
    rows = []
    for z in (-1.98, -1.80, -1.2, -0.6, 0.0, 0.6, 1.2, 1.5):
        sag = 0.035 if int(round(z * 10)) % 12 != 0 else 0.0
        bulge = 1.05 if abs(z) > 1.4 else 1.0
        rows.append([G(math.cos(a) * 0.78 * bulge, 1.70 + math.sin(a) * (1.2 - sag) * bulge, z) for a in [math.pi * i / 14 for i in range(15)]])
    m.grid(rows, None, closed=False)
    m.grid([list(reversed(r)) for r in rows], None, closed=False)
    m.surface("boards", Pal.WoodNew, 0.05)
    m.wind = 0.0
    for z in (-1.8, -0.6, 0.6, 1.5):
        m.tube([G(math.cos(a) * 0.76, 1.70 + math.sin(a) * 1.17, z) for a in [math.pi * i / 12 for i in range(13)]], 0.016, 6)
    return m


@model
def tent():
    m = Model("tent")
    w, ln, h = 2.6, 3.2, 2.0
    m.surface("canvas", (1.0, 1.0, 1.0), 0.03)
    m.wind = CANVAS
    for sx in (-1, 1):
        rows = []
        for i in range(7):
            t = i / 6.0
            sag = 0.06 * math.sin(t * math.pi)
            rows.append([G(sx * (w * 0.5 * (1 - t) + sag * 0.6), h * t - sag, -ln * 0.5 + ln * j / 4.0) for j in range(5)])
        m.grid(rows, None, closed=False)
        m.grid([list(reversed(r)) for r in rows], None, closed=False)
    back = [[G(-w * 0.5 * (1 - t), h * t, -ln * 0.5 - 0.01), G(w * 0.5 * (1 - t), h * t, -ln * 0.5 - 0.01)] for t in (0.0, 0.33, 0.66, 0.999)]
    m.grid(back, None, closed=False)
    m.grid([list(reversed(r)) for r in back], None, closed=False)
    for sx in (-1, 1):                                                          # door flaps tied back
        flap = [[G(sx * w * 0.5 * (1 - t), h * t, ln * 0.5), G(sx * (w * 0.5 * (1 - t) * 0.55 + 0.04), h * t, ln * 0.5 + 0.05)] for t in (0.0, 0.33, 0.66, 0.999)]
        m.grid(flap, None, closed=False)
        m.grid([list(reversed(r)) for r in flap], None, closed=False)
    m.surface("timber", Pal.WoodDark, 0.05)
    m.wind = 0.0
    for z in (-ln * 0.5, ln * 0.5):
        gcyl(m, (0, 0.0, z), (0, h + 0.12, z), 0.035, 0.03, 8, 0.004)
    gcyl(m, (0, h, -ln * 0.5 - 0.1), (0, h, ln * 0.5 + 0.1), 0.028, 0.028, 8, 0.004)
    m.surface("straw", Pal.Rope, 0.05)
    for sx in (-1, 1):
        for z in (-ln * 0.5, 0.0, ln * 0.5):
            m.tube([G(sx * w * 0.5, 0.02, z), G(sx * (w * 0.5 + 0.55), 0.0, z)], 0.008, 5)
            m.surface("timber", Pal.WoodDark, 0.05)
            gbeam(m, (sx * (w * 0.5 + 0.52), 0.0, z), (sx * (w * 0.5 + 0.60), 0.16, z), 0.03, 0.03)
            m.surface("straw", Pal.Rope, 0.05)
    return m


@model
def campfire():
    m = Model("campfire")
    for i in range(9):
        a = i * 2 * math.pi / 9
        m.surface("rock", shade(Pal.StoneDark, 0.9 + 0.3 * m.rng.random()), 0.08)
        m.push(G(math.cos(a) * 0.55, 0.05, math.sin(a) * 0.55), (0, 0, m.rng.uniform(0, 180)))
        m.blob((0, 0, 0), (0.15 * m.rng.uniform(0.8, 1.2), 0.12, 0.11 * m.rng.uniform(0.8, 1.2)), 1, squash=0.6, lumps=0.25)
        m.pop()
    m.surface("dirt", Pal.Ash, 0.06)
    m.lathe((0, 0, 0), [(0.0, 0.035), (0.30, 0.03), (0.46, 0.008), (0.5, 0.0)], 14)
    m.surface("bark", hexc(0x2A2622), 0.08)
    for k in range(4):
        a = k * math.pi / 2 + 0.4
        gcyl(m, (math.cos(a) * 0.36, 0.07, math.sin(a) * 0.36), (math.cos(a) * -0.05, 0.20, math.sin(a) * -0.05), 0.055, 0.045, 8, 0.006)
    gcyl(m, (-0.3, 0.07, -0.12), (0.3, 0.10, 0.12), 0.05, 0.045, 8, 0.006)
    return m


@model
def logseat():
    m = Model("logseat")
    m.surface("bark", Pal.Bark, 0.06)
    gcyl(m, (-0.8, 0.2, 0), (0.8, 0.2, 0), 0.2, 0.19, 16, 0.0)
    m.surface("timber", Pal.WoodNew, 0.05)
    gcyl(m, (-0.805, 0.2, 0), (-0.80, 0.2, 0), 0.185, 0.185, 16, 0)
    gcyl(m, (0.80, 0.2, 0), (0.805, 0.2, 0), 0.175, 0.175, 16, 0)
    m.surface("bark", shade(Pal.Bark, 0.9), 0.06)
    gcyl(m, (0.3, 0.33, 0.1), (0.36, 0.47, 0.2), 0.035, 0.028, 8, 0.004)
    return m


@model
def haybale():
    m = Model("haybale")
    m.surface("straw", Pal.Hay, 0.08)
    gbox(m, (0, 0.3, 0), (1.0, 0.6, 0.55), 0.035, "x")
    m.surface("straw", shade(Pal.Rope, 0.8), 0.04)
    for x in (-0.27, 0.27):
        for c, size in (((x, 0.603, 0), (0.016, 0.006, 0.56)), ((x, -0.003 + 0.0, 0), (0.016, 0.006, 0.56)), ((x, 0.3, 0.278), (0.016, 0.61, 0.006)), ((x, 0.3, -0.278), (0.016, 0.61, 0.006))):
            gbox(m, c, size, 0.001)
    return m


@model
def grave_cross():
    m = Model("grave_cross")
    m.surface("timber", Pal.WoodGrey, 0.07)
    gbox(m, (0, 0.55, 0), (0.09, 1.10, 0.05), 0.008, rot=(0, 0, 3.0))
    gbox(m, (0, 0.80, 0.0), (0.50, 0.09, 0.05), 0.008, rot=(0, 0, 3.0))
    m.surface("dirt", hexc(0x6A5038), 0.08)
    m.push(G(0, 0.0, 0.95))
    m.blob((0, 0, 0), (0.44, 0.97, 0.17), 2, squash=0.2, lumps=0.12)
    m.pop()
    return m


@model
def grave_stone():
    m = Model("grave_stone")
    m.surface("stone", Pal.Stone, 0.06)
    gbox(m, (0, 0.30, 0), (0.50, 0.60, 0.10), 0.015)
    m.lathe(G(0, 0.60, 0), [(0.25, 0.0), (0.22, 0.10), (0.13, 0.20), (0.0, 0.25)], 20, axis="y")
    m.surface("dirt", hexc(0x6A5038), 0.08)
    m.push(G(0, 0.0, 0.95))
    m.blob((0, 0, 0), (0.44, 0.97, 0.17), 2, squash=0.2, lumps=0.12)
    m.pop()
    return m


@model
def telegraph_pole():
    m = Model("telegraph_pole")
    m.surface("timber", Pal.Bark, 0.06)
    gcyl(m, (0, 0, 0), (0, 6.5, 0), 0.12, 0.09, 12, 0.01)
    m.surface("timber", Pal.WoodDark, 0.05)
    gbox(m, (0, 5.9, 0.10), (1.5, 0.10, 0.09), 0.01, "x")
    gbeam(m, (-0.45, 5.88, 0.10), (0, 5.45, 0.10), 0.04, 0.03)
    gbeam(m, (0.45, 5.88, 0.10), (0, 5.45, 0.10), 0.04, 0.03)
    m.surface("steel", hexc(0x7FA39A), 0.0)
    for x in (-0.6, -0.3, 0.3, 0.6):
        m.lathe(G(x, 5.95, 0.10), [(0.0, 0.0), (0.018, 0.0), (0.03, 0.03), (0.03, 0.06), (0.02, 0.09), (0.0, 0.10)], 10)
    return m


def hexc(h):
    return kit.hexcol(h)


def build(only=None):
    col = kit.collection("Props")
    kit.clear_default_scene()
    objects = []
    for fn in BUILDERS:
        if only and fn.__name__ not in only:
            continue
        objects.append(fn().finish(col))
    kit.layout(objects)
    return objects


if __name__ == "__main__" or "ARGS" in globals():
    args = globals().get("ARGS", {})
    only = set(args["only"].split(",")) if args.get("only") else None
    objs = build(only)
    print(kit.stats(objs))
    if not args.get("noexport") and not only:
        print("exported", kit.export_fbx("Props", "props"))
