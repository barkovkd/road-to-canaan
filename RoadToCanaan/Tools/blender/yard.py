"""Yard and line-side structures: fence parts and a field gate, the ranch gate, the town's signpost, the water
tower, the windmill (tower and wheel apart: the game turns the wheel in the wind).
    Tools/blender/bmcp.py run Tools/blender/yard.py
Collection "Yard" -> Assets/Canaan/Models/yard.fbx.

Game axes: x right, y up, z forward. Colour alpha 12 = the fence's paint (dyed in the game).
"""
import importlib
import math

import kit

importlib.reload(kit)
from kit import L, Model, Pal, gbeam, gbox, gcyl, hexcol, shade

DYE = 12 / 255.0
BAY = 2.4                     # length of the reference rail (Props.Fence stretches it to the real bay)


def paint(m, k=1.0, surf="boards", jitter=0.05):
    m.wind = DYE
    m.surface(surf, (k, k, k), jitter)
    return m


def raw_wood(m, tint, surf="timber", jitter=0.06):
    m.wind = 0.0
    m.surface(surf, tint, jitter)
    return m


def iron(m, tint=None, surf="iron"):
    m.wind = 0.0
    m.surface(surf, tint if tint else Pal.Iron, 0.03)
    return m


def ring(m, centre, radius, r, seg=36, axis="z", tube_seg=6):
    """A hoop of round bar about a game axis through 'centre'."""
    cx, cy, cz = centre
    pts = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        c, s = math.cos(a) * radius, math.sin(a) * radius
        pts.append(L(cx + c, cy + s, cz) if axis == "z" else L(cx + c, cy, cz + s))
    m.tube(pts, r, tube_seg, closed=True)


# ---------------------------------------------------------------------- fences

def fence_post():
    """A sawn post, 1 m out of the ground (the game stretches it to the fence's height) and a foot in it."""
    m = Model("fence_post")
    paint(m, 0.86, "timber")
    gbox(m, (0, 0.36, 0), (0.115, 1.22, 0.115), 0.012, "y")
    gbox(m, (0, 0.99, 0), (0.085, 0.05, 0.085), 0.014, "y")                     # the top, cut to shed rain
    return m


def fence_post_round():
    """A corral post: a peeled log, its top sawn on the slant."""
    m = Model("fence_post_round")
    paint(m, 0.84, "timber")
    gcyl(m, (0, -0.25, 0), (0, 0.97, 0), 0.085, 0.07, 10, 0.006)
    gcyl(m, (0.0, 0.955, 0), (0.012, 1.02, 0), 0.07, 0.045, 10, 0.004)
    return m


def fence_rail():
    """A sawn rail nailed to the side of the posts: runs from z = 0 to z = BAY, a hand's breadth past both."""
    m = Model("fence_rail")
    paint(m, 1.0, "boards")
    gbeam(m, (0.073, 0, -0.07), (0.073, 0, BAY + 0.07), 0.03, 0.105, 0.005)
    iron(m, shade(Pal.Iron, 0.8))
    for z in (0.0, BAY):                                                          # nail heads
        for dy in (-0.028, 0.028):
            gcyl(m, (0.086, dy, z), (0.092, dy, z), 0.008, 0.008, 6, 0)
    return m


def fence_pole():
    """A corral rail: a thin peeled pole, lashed to the posts."""
    m = Model("fence_pole")
    paint(m, 0.97, "timber")
    m.tube([L(0.115, 0.0, -0.12), L(0.118, 0.006, BAY * 0.33), L(0.112, -0.004, BAY * 0.7), L(0.115, 0.0, BAY + 0.12)],
           0.04, 8, radii=[0.044, 0.041, 0.038, 0.035])
    return m


def fence_gate():
    """A five-bar field gate, 3 m wide and 1.2 high: the hinge edge is x = 0, the gate runs to +x."""
    m = Model("fence_gate")
    w = 3.0
    paint(m, 0.95, "boards")
    for y in (0.20, 0.40, 0.62, 0.86, 1.13):
        gbeam(m, (0.03, y, 0), (w, y, 0), 0.026, 0.085, 0.004)
    paint(m, 0.88, "boards")
    for x in (0.07, w * 0.5, w - 0.06):
        gbox(m, (x, 0.665, 0.027), (0.09, 1.05, 0.028), 0.004, "y")
    gbeam(m, (0.13, 0.20, -0.027), (w - 0.10, 1.10, -0.027), 0.026, 0.085, 0.004)     # the brace that keeps it square
    iron(m)
    for y in (0.24, 1.09):
        gbox(m, (0.20, y, 0.046), (0.42, 0.04, 0.008), 0.002, "x")                    # strap hinges
        gcyl(m, (0.0, y - 0.045, 0.03), (0.0, y + 0.045, 0.03), 0.014, 0.014, 8, 0.002)
    gbox(m, (w - 0.10, 0.86, 0.05), (0.22, 0.03, 0.012), 0.002, "x")                  # latch bar
    return m


# ---------------------------------------------------------------------- gate and signpost

def ranch_gate():
    """The gate over a ranch road: log posts 6.4 m apart, a log across, a board on chains for the name
    (3.2 x 0.7 about y = 3.7 - the game letters both faces)."""
    m = Model("ranch_gate")
    raw_wood(m, shade(Pal.WoodDark, 1.05))
    for sx in (-1, 1):
        gcyl(m, (sx * 3.2, -0.3, 0), (sx * 3.2, 4.62, 0), 0.15, 0.125, 12, 0.012)
        gbeam(m, (sx * 3.2, 3.45, 0), (sx * 2.25, 4.28, 0), 0.09, 0.11)             # knee brace
    gcyl(m, (-3.85, 4.34, 0), (3.85, 4.30, 0), 0.115, 0.10, 12, 0.012)
    m.surface("boards", shade(Pal.Bone, 0.93), 0.04)
    for i in range(3):
        gbox(m, (0, 3.467 + i * 0.233, 0), (3.2, 0.228, 0.06), 0.005, "x")
    raw_wood(m, shade(Pal.WoodDark, 0.9), "boards")
    for sx in (-1, 1):
        gbox(m, (sx * 1.45, 3.7, 0), (0.07, 0.74, 0.075), 0.004, "y")               # battens that hold the board together
    iron(m)
    for sx in (-1, 1):
        m.tube([L(sx * 1.45, 4.07, 0), L(sx * 1.45, 4.22, 0)], 0.012, 6)
        ring(m, (sx * 1.45, 4.07, 0), 0.03, 0.006, 10)
    return m


def signpost():
    """A town's name board between two posts (2.6 x 0.7 about y = 2.1; the game letters both faces)."""
    m = Model("signpost")
    raw_wood(m, shade(Pal.WoodDark, 1.0))
    for sx in (-1, 1):
        gbox(m, (sx * 1.38, 1.2, 0), (0.115, 3.0, 0.115), 0.012, "y")
        gbox(m, (sx * 1.38, 2.74, 0), (0.15, 0.04, 0.15), 0.008, "x")
        m.lathe(L(sx * 1.38, 2.76, 0), [(0.05, 0.0), (0.06, 0.03), (0.035, 0.075), (0.0, 0.09)], 8)
    m.surface("boards", shade(Pal.Bone, 0.95), 0.035)
    for i in range(3):
        gbox(m, (0, 1.867 + i * 0.233, 0), (2.64, 0.228, 0.05), 0.004, "x")
    raw_wood(m, shade(Pal.WoodDark, 0.92), "boards")
    for y in (1.74, 2.46):
        gbox(m, (0, y, 0), (2.70, 0.05, 0.075), 0.006, "x")
    return m


# ---------------------------------------------------------------------- water tower

def water_tower():
    """A railway water tank on a braced trestle: legs to 6 m, a 3.2 m tank of staves, a shingled cone, the spout
    over the track side (-z)."""
    m = Model("water_tower")

    def half(y):                                   # half-width of the trestle at a height
        return 1.75 - 0.35 * y / 6.0

    dark = shade(Pal.WoodDark, 0.95)
    raw_wood(m, dark)
    for sx in (-1, 1):
        for sz in (-1, 1):
            gbeam(m, (sx * 1.75, -0.2, sz * 1.75), (sx * 1.4, 6.0, sz * 1.4), 0.2, 0.2, 0.012)
    raw_wood(m, shade(Pal.WoodDark, 1.08), "boards")
    for y in (2.0, 4.0):                           # girts
        h = half(y)
        for s in (-1, 1):
            gbeam(m, (-h, y, s * h), (h, y, s * h), 0.07, 0.15)
            gbeam(m, (s * h, y, -h), (s * h, y, h), 0.07, 0.15)
    for y0, y1 in ((0.15, 1.9), (2.1, 3.9), (4.1, 5.85)):      # cross braces on all four sides
        h0, h1 = half(y0) + 0.09, half(y1) + 0.09
        for s in (-1, 1):
            gbeam(m, (-h0, y0, s * h0), (h1, y1, s * h1), 0.03, 0.12)
            gbeam(m, (h0, y0, s * (h0 + 0.03)), (-h1, y1, s * (h1 + 0.03)), 0.03, 0.12)
            gbeam(m, (s * h0, y0, -h0), (s * h1, y1, h1), 0.03, 0.12)
            gbeam(m, (s * (h0 + 0.03), y0, h0), (s * (h1 + 0.03), y1, -h1), 0.03, 0.12)
    raw_wood(m, dark)
    for s in (-1, 1):                              # caps over the legs, joists over the caps
        gbox(m, (0, 5.92, s * 1.4), (3.7, 0.18, 0.2), 0.01, "x")
    for i in range(7):
        gbox(m, (-1.65 + i * 0.55, 6.07, 0), (0.1, 0.14, 3.7), 0.008, "z")
    raw_wood(m, shade(Pal.WoodGrey, 0.95), "planks", 0.08)
    for i in range(10):                            # the deck
        gbox(m, (0, 6.165, -1.71 + i * 0.38), (3.8, 0.05, 0.365), 0.006, "x")

    # the tank: staves bound with iron hoops
    y0, y1, r0, r1 = 6.19, 9.3, 1.9, 1.78

    def rad(y):
        return r0 + (r1 - r0) * (y - y0) / (y1 - y0)

    raw_wood(m, shade(Pal.WoodRed, 0.95), "boards", 0.04)
    m.lathe(L(0, 0, 0), [(0.0, y0), (r0, y0), (r1, y1), (0.0, y1)], 36, smooth=False)
    iron(m, shade(Pal.Iron, 0.9))
    for y in (6.42, 6.95, 7.6, 8.35, 9.08):
        r = rad(y)
        m.lathe(L(0, 0, 0), [(r + 0.001, y - 0.04), (r + 0.014, y - 0.034), (r + 0.014, y + 0.034), (r + 0.001, y + 0.04)], 36)
        gbox(m, (r + 0.03, y, 0.0), (0.05, 0.07, 0.16), 0.006, "z")               # the lug the hoop is drawn up with
    raw_wood(m, shade(hexcol(0x4E463E), 1.0), "shingles", 0.05)
    m.lathe(L(0, 0, 0), [(0.0, 9.27), (2.12, 9.27), (2.10, 9.34), (0.10, 10.38), (0.0, 10.42)], 18, smooth=False)
    iron(m)
    gcyl(m, (0, 10.38, 0), (0, 10.85, 0), 0.02, 0.012, 6, 0)
    m.push(L(0, 10.62, 0))
    m.blob((0, 0, 0), (0.07, 0.07, 0.07), 1)
    m.pop()

    # the spout: let down over the tender by its rope, held up by a counterweight
    m.surface("tin", shade(Pal.Tin, 0.6), 0.04)
    m.tube([L(0, 6.45, -1.80), L(0, 6.38, -2.15), L(0, 5.6, -2.95), L(0, 4.75, -3.55)], 0.11, 10, radii=[0.13, 0.12, 0.10, 0.09])
    iron(m)
    m.tube([L(0, 7.9, -1.86), L(0, 7.95, -2.6)], 0.03, 6)                           # the jib
    m.tube([L(0, 7.93, -2.55), L(0, 5.72, -2.9)], 0.008, 5)                         # chain to the spout
    m.tube([L(0.25, 7.93, -2.5), L(0.25, 6.9, -2.5)], 0.008, 5)
    gbox(m, (0.25, 6.75, -2.5), (0.14, 0.3, 0.14), 0.01, "y")                       # counterweight
    m.surface("straw", Pal.Rope, 0.05)
    m.tube([L(0, 4.8, -3.5), L(0.02, 4.0, -3.52), L(0, 3.25, -3.5)], 0.012, 5)
    # ladder up the east side
    raw_wood(m, shade(Pal.WoodOld, 0.95), "boards")
    for dz in (-0.22, 0.22):
        gbeam(m, (2.35, 0.0, dz), (1.93, 6.75, dz), 0.045, 0.07)
    for i in range(19):
        t = (i + 1) / 20.0
        gbeam(m, (2.35 - 0.42 * t, 6.75 * t, -0.22), (2.35 - 0.42 * t, 6.75 * t, 0.22), 0.03, 0.03, 0.003)
    return m


# ---------------------------------------------------------------------- windmill

def windmill_tower():
    """The tower of a wind pump, 9 m to the head: four legs, girts and tie rods, a platform under the wheel,
    the gear head with its tail, the pump rod and the pump at the foot. The wheel's hub is the mark 'hub'."""
    m = Model("windmill_tower")

    def half(y):
        return 1.3 - 1.05 * y / 9.0

    raw_wood(m, shade(Pal.WoodGrey, 0.9), "timber")
    for sx in (-1, 1):
        for sz in (-1, 1):
            gbeam(m, (sx * 1.3, -0.2, sz * 1.3), (sx * 0.25, 9.0, sz * 0.25), 0.11, 0.11, 0.008)
    raw_wood(m, shade(Pal.WoodGrey, 1.0), "boards")
    tiers = (0.25, 1.8, 3.6, 5.4, 7.2, 8.7)
    for y in tiers[1:]:
        h = half(y)
        for s in (-1, 1):
            gbeam(m, (-h, y, s * h), (h, y, s * h), 0.045, 0.09)
            gbeam(m, (s * h, y, -h), (s * h, y, h), 0.045, 0.09)
    iron(m, shade(Pal.Iron, 1.1))
    for y0, y1 in zip(tiers, tiers[1:]):          # tie rods
        h0, h1 = half(y0) + 0.05, half(y1) + 0.05
        for s in (-1, 1):
            m.tube([L(-h0, y0, s * h0), L(h1, y1, s * h1)], 0.011, 5)
            m.tube([L(h0, y0, s * h0), L(-h1, y1, s * h1)], 0.011, 5)
            m.tube([L(s * h0, y0, -h0), L(s * h1, y1, h1)], 0.011, 5)
            m.tube([L(s * h0, y0, h0), L(s * h1, y1, -h1)], 0.011, 5)
    raw_wood(m, shade(Pal.WoodGrey, 0.95), "planks", 0.08)
    for i in range(5):                             # the platform
        gbox(m, (0, 7.72, -0.6 + i * 0.3), (1.5, 0.04, 0.285), 0.005, "x")
    raw_wood(m, shade(Pal.WoodOld, 0.95), "boards")
    for i in range(22):                            # rungs up one leg
        y = 0.45 + i * 0.33
        h = half(y)
        gbeam(m, (h - 0.02, y, -h - 0.02), (h - 0.34, y, -h - 0.02), 0.03, 0.035, 0.003)
    # the head: mast, gear case, tail
    iron(m, shade(Pal.Iron, 1.0))
    gcyl(m, (0, 8.6, 0), (0, 9.05, 0), 0.07, 0.07, 10)
    gbox(m, (0, 9.2, -0.08), (0.26, 0.34, 0.5), 0.03, "z")
    gcyl(m, (0, 9.2, -0.52), (0, 9.2, -0.30), 0.045, 0.045, 8)
    gbeam(m, (0, 9.2, 0.15), (0, 9.2, 2.2), 0.03, 0.06)
    gbeam(m, (0, 9.34, 0.15), (0, 9.55, 2.25), 0.02, 0.03)
    gbeam(m, (0, 9.06, 0.15), (0, 8.85, 2.25), 0.02, 0.03)
    m.surface("tin", shade(Pal.Tin, 1.0), 0.03)
    a, b, th = 1.55, 2.75, 0.006
    m.loft([[L(-th, 9.2 - 0.26, a), L(th, 9.2 - 0.26, a), L(th, 9.2 + 0.26, a), L(-th, 9.2 + 0.26, a)],
            [L(-th, 9.2 - 0.52, b), L(th, 9.2 - 0.52, b), L(th, 9.2 + 0.52, b), L(-th, 9.2 + 0.52, b)]], True, 0, False)
    m.surface("tin", shade(Pal.WoodRed, 1.1), 0.02)
    m.loft([[L(-th - 0.002, 9.2 - 0.47, 2.52), L(th + 0.002, 9.2 - 0.47, 2.52), L(th + 0.002, 9.2 + 0.47, 2.52), L(-th - 0.002, 9.2 + 0.47, 2.52)],
            [L(-th - 0.002, 9.2 - 0.523, 2.752), L(th + 0.002, 9.2 - 0.523, 2.752), L(th + 0.002, 9.2 + 0.523, 2.752), L(-th - 0.002, 9.2 + 0.523, 2.752)]], True, 0, False)
    # pump rod and pump
    iron(m, shade(Pal.Iron, 1.15))
    m.tube([L(0, 9.0, 0), L(0, 1.0, 0)], 0.012, 5)
    gcyl(m, (0, 0.0, 0), (0, 1.0, 0), 0.075, 0.065, 10)
    m.tube([L(0, 0.72, 0), L(0.22, 0.74, 0.22), L(0.34, 0.62, 0.34)], 0.035, 8)
    m.surface("stone", Pal.Stone, 0.05)
    gbox(m, (0, 0.03, 0), (1.3, 0.14, 1.3), 0.02, "x")
    m.mark("hub", L(0, 9.2, -0.52))
    return m


def windmill_wheel():
    """The wheel of the wind pump, 3.2 m across: eighteen pitched sails on two hoops. Its axis is z, the wind
    side -z; the origin is the hub."""
    m = Model("windmill_wheel")
    iron(m, shade(Pal.Iron, 1.0))
    gcyl(m, (0, 0, -0.10), (0, 0, 0.06), 0.085, 0.085, 10)
    for i in range(6):
        a = 2 * math.pi * i / 6
        m.tube([L(math.cos(a) * 0.07, math.sin(a) * 0.07, 0.0), L(math.cos(a) * 1.5, math.sin(a) * 1.5, 0.0)], 0.014, 5)
        m.tube([L(math.cos(a) * 0.06, math.sin(a) * 0.06, -0.09), L(math.cos(a) * 1.05, math.sin(a) * 1.05, -0.005)], 0.008, 4)
    ring(m, (0, 0, 0.0), 0.62, 0.012, 30)
    ring(m, (0, 0, 0.0), 1.42, 0.012, 42)
    m.surface("tin", shade(Pal.Tin, 1.05), 0.06)
    n, pitch, th = 18, math.radians(24), 0.004
    for i in range(n):
        a = 2 * math.pi * i / n
        er = (math.cos(a), math.sin(a), 0.0)
        et = (-math.sin(a), math.cos(a), 0.0)
        p = (et[0] * math.cos(pitch), et[1] * math.cos(pitch), math.sin(pitch))        # across the sail
        nn = (-et[0] * math.sin(pitch), -et[1] * math.sin(pitch), math.cos(pitch))     # its normal
        rings = []
        for r, w in ((0.50, 0.11), (1.05, 0.25), (1.60, 0.40)):
            rings.append([L(*(er[k] * r + p[k] * sw * w * 0.5 + nn[k] * sn * th for k in range(3)))
                          for sw, sn in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
        m.loft(rings, True, 0, False)
    return m


def build():
    col = kit.collection("Yard")
    kit.clear_default_scene()
    makers = (fence_post, fence_post_round, fence_rail, fence_pole, fence_gate, ranch_gate, signpost,
              water_tower, windmill_tower, windmill_wheel)
    objs = [fn().finish(col, sharp_angle=35.0) for fn in makers]
    kit.layout(objs, 5.0, 10)
    for ob in objs:                      # its own row in canaan_models.blend
        ob.location.y -= 44.0
    return objs


if "ARGS" in globals() or __name__ == "__main__":
    args = globals().get("ARGS", {})
    objs = build()
    print(kit.stats(objs))
    if not args.get("noexport"):
        print("exported", kit.export_fbx("Yard", "yard"))
