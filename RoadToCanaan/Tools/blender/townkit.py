"""The town's adobe kit and the things of its back lots. For the adobe houses (walls and flat roofs from code,
Architect.Adobe): the ends of the roof beams (vigas), wooden drain spouts (canales), the carved caps of porch posts
(zapatas), a peeled-log post, a string of chiles, a beehive oven (horno), a pole ladder. For the yards behind the
main street: an outhouse, a woodpile, a chopping block with its axe, a wash line with laundry, a hand pump, the
loading chute of the stock pens, a tumbleweed.
    Tools/blender/bmcp.py run Tools/blender/townkit.py
Collection "TownKit" -> Assets/Canaan/Models/townkit.fbx.

Game axes as in buildkit.py: x along the wall, y up, z through it; the street side is -z, so a model placed with
the building's yaw faces the same way as its front. Free-standing things face -z too.
"""
import importlib
import math

import kit

importlib.reload(kit)
from kit import L, Model, Pal, gbeam, gbox, gcyl, hexcol, mixc, shade

GREYWOOD = shade(Pal.WoodGrey, 0.95)
ADOBE = hexcol(0xB98B63)
CHILE, CHILE_DARK = hexcol(0x9A2A1E), hexcol(0x6E1D16)
BUILDERS = []


def model(fn):
    BUILDERS.append(fn)
    return fn


def slab(m, quad, thick, out):
    """A board of the given outline (four game-space corners, in order) and thickness, pushed 'thick' along 'out'."""
    o = [L(*p) for p in quad]
    d = L(*out)
    d = d.normalized() * thick
    verts = o + [v + d for v in o]
    faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    m.raw([tuple(v) for v in verts], faces, grain="z")


# ---------------------------------------------------------------------- adobe

@model
def ab_viga():
    """The end of a roof beam where it comes out of the wall: a peeled log, a little out of round, cut square,
    weathered grey and checked. From 0.12 inside the wall face to 0.56 out."""
    m = Model("ab_viga")
    m.surface("timber", GREYWOOD, 0.08)
    seg = 11
    rings = []
    for z, r in ((0.12, 0.112), (-0.2, 0.108), (-0.5, 0.101), (-0.56, 0.099)):
        ring = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            k = 1.0 + 0.06 * math.sin(a * 3 + 0.7) + 0.03 * math.cos(a * 5)
            ring.append(L(math.cos(a) * r * k, math.sin(a) * r * k * 0.94, z))
        rings.append(ring)
    m.loft(rings, True, 0, True, grain="z")
    # checks along the top and the side
    m.surface("timber", shade(Pal.WoodDark, 0.7), 0.0)
    gbox(m, (0.02, 0.1, -0.25), (0.012, 0.012, 0.58), 0.0)
    gbox(m, (-0.098, 0.02, -0.3), (0.012, 0.01, 0.45), 0.0)
    return m


@model
def ab_canale():
    """A wooden spout through the parapet: a trough of three boards, 0.15 inside the wall face to 0.75 out."""
    m = Model("ab_canale")
    m.surface("boards", shade(Pal.WoodOld, 0.85), 0.08)
    gbox(m, (0, 0, -0.3), (0.26, 0.035, 0.9), 0.004, "z")
    for sx in (-1, 1):
        gbox(m, (sx * 0.115, 0.075, -0.3), (0.03, 0.15, 0.9), 0.004, "z")
    m.surface("dirt", shade(Pal.WoodDark, 0.6), 0.0)            # the wet stain down the trough
    gbox(m, (0, 0.02, -0.32), (0.18, 0.01, 0.84), 0.0, "z")
    return m


@model
def ab_zapata():
    """The carved cap of a porch post: a long block over a shorter one, the lower ends rounded off. The post's top
    is y = 0."""
    m = Model("ab_zapata")
    m.surface("timber", shade(Pal.WoodOld, 0.92), 0.06)
    gbox(m, (0, 0.17, 0), (1.05, 0.12, 0.18), 0.01, "x")
    gbox(m, (0, 0.06, 0), (0.62, 0.1, 0.18), 0.01, "x")
    for sx in (-1, 1):
        gcyl(m, (sx * 0.31, 0.06, -0.09), (sx * 0.31, 0.06, 0.09), 0.05, 0.05, 12, 0.006)
        gcyl(m, (sx * 0.52, 0.17, -0.09), (sx * 0.52, 0.17, 0.09), 0.06, 0.06, 12, 0.006)
    return m


@model
def ab_post():
    """A peeled-log post, 2.6 m (stretched to the porch), standing on a flat stone."""
    m = Model("ab_post")
    m.surface("timber", GREYWOOD, 0.08)
    gcyl(m, (0, 0.04, 0), (0, 2.6, 0), 0.105, 0.09, 11, 0.012)
    m.surface("stone", shade(Pal.Stone, 0.9), 0.05)
    gbox(m, (0, 0.02, 0), (0.32, 0.05, 0.3), 0.012)
    return m


@model
def ab_ristra():
    """A string of dried chiles hung from a nail (y = 0), 0.8 m long, thick at the top and tapering."""
    m = Model("ab_ristra")
    m.surface("canvas", Pal.Rope, 0.0)
    m.tube([L(0, 0.02, 0), L(0, -0.82, 0)], 0.008, 6)
    n = 46
    for i in range(n):
        t = i / (n - 1)
        y = -0.06 - t * 0.74
        a = i * 2.4
        r = 0.055 * (1.0 - 0.55 * t)
        m.surface("plain", CHILE if m.rng.random() > 0.3 else CHILE_DARK, 0.1)
        # each pod points down and out from the cord
        m.push(L(math.cos(a) * r, y, math.sin(a) * r), (math.degrees(math.sin(a)) * 0.45, -math.degrees(math.cos(a)) * 0.45, 0))
        m.blob((0, 0, -0.035), (0.017, 0.017, 0.06), 1)
        m.pop()
    return m


@model
def ab_horno():
    """A beehive oven of adobe on its low platform, the arched mouth to the front (-z), a smoke hole high on the
    side, a board leaning by the mouth."""
    m = Model("ab_horno")
    m.surface("plaster", ADOBE, 0.04)
    m.lathe((0, 0, 0), [(0.0, 0.0), (0.82, 0.0), (0.84, 0.32), (0.8, 0.36), (0.68, 0.37), (0.68, 0.46), (0.65, 0.62), (0.58, 0.8),
                        (0.46, 0.98), (0.3, 1.12), (0.12, 1.2), (0.0, 1.22)], 24)
    m.surface("plaster", shade(ADOBE, 0.55), 0.0)                # soot over the mouth
    gbox(m, (0, 0.72, -0.6), (0.36, 0.24, 0.12), 0.02)
    m.surface("plain", hexcol(0x16120F), 0.0)                    # the mouth and the smoke hole
    gbox(m, (0, 0.54, -0.63), (0.34, 0.3, 0.1), 0.01)
    gcyl(m, (0, 0.69, -0.58), (0, 0.69, -0.68), 0.17, 0.17, 14, 0.0)
    gcyl(m, (0.36, 0.95, -0.3), (0.42, 1.0, -0.36), 0.05, 0.05, 10, 0.0)
    m.surface("boards", shade(Pal.WoodOld, 0.75), 0.06)          # the board that closes it
    gbox(m, (0.5, 0.62, -0.55), (0.45, 0.5, 0.03), 0.004, "y", (0, 0, -10))
    return m


@model
def ab_ladder():
    """A pole ladder leaning on a wall: feet 0.9 m out from the wall face (z = 0), tops 0.4 m over a 3.2 m roof."""
    m = Model("ab_ladder")
    m.surface("timber", GREYWOOD, 0.08)
    for sx in (-1, 1):
        gcyl(m, (sx * 0.25, 0, -0.95), (sx * 0.2, 3.62, 0.02), 0.045, 0.036, 8, 0.006)
    m.surface("timber", shade(Pal.WoodOld, 0.95), 0.08)
    for i in range(9):
        t = (i + 0.7) / 10.5
        y, z, w = 3.62 * t, -0.95 + 0.97 * t, 0.25 - 0.05 * t
        gcyl(m, (-w - 0.03, y, z), (w + 0.03, y + 0.01, z), 0.025, 0.025, 7, 0.004)
    return m


# ---------------------------------------------------------------------- back lots

@model
def yd_outhouse():
    """A privy of grey boards and battens with a shed roof falling to the back; a crescent in the door."""
    m = Model("yd_outhouse")
    w, d, hf, hb = 1.16, 1.2, 2.25, 2.0
    wood = shade(Pal.WoodGrey, 0.9)
    m.surface("boards", wood, 0.06)
    hx, hz = w / 2, d / 2
    # walls: front and back boards, the sides follow the slope of the roof
    gbox(m, (0, hf / 2, -hz), (w, hf, 0.03), 0.004, "y")
    gbox(m, (0, hb / 2, hz), (w, hb, 0.03), 0.004, "y")
    for sx in (-1, 1):
        x = sx * hx
        slab(m, [(x, 0, -hz), (x, 0, hz), (x, hb, hz), (x, hf, -hz)], 0.03, (sx, 0, 0))
    # battens over the joints
    m.surface("boards", shade(wood, 0.86), 0.06)
    for i in range(5):
        x = -hx + (i + 0.5) * w / 5
        gbox(m, (x, hb / 2, hz + 0.025), (0.06, hb, 0.02), 0.003, "y")
        if abs(x) > 0.3:
            gbox(m, (x, hf / 2, -hz - 0.025), (0.06, hf, 0.02), 0.003, "y")
    # the door, its crescent, a wooden latch
    m.surface("boards", shade(Pal.WoodOld, 0.85), 0.06)
    gbox(m, (0, 0.98, -hz - 0.03), (0.62, 1.9, 0.035), 0.004, "y")
    m.surface("plain", hexcol(0x14110E), 0.0)
    gcyl(m, (0.0, 1.66, -hz - 0.05), (0.0, 1.66, -hz - 0.054), 0.075, 0.075, 18, 0.0)
    m.surface("boards", shade(Pal.WoodOld, 0.85), 0.06)
    gcyl(m, (0.035, 1.68, -hz - 0.054), (0.035, 1.68, -hz - 0.058), 0.07, 0.07, 18, 0.0)
    gbox(m, (0.24, 1.0, -hz - 0.06), (0.04, 0.12, 0.03), 0.004)
    # the roof: boards falling to the back, overhanging all round
    m.surface("boards", shade(Pal.WoodGrey, 0.75), 0.06)
    run = math.hypot(d + 0.3, hf - hb)
    ang = math.degrees(math.atan2(hf - hb, d))
    gbox(m, (0, (hf + hb) / 2 + 0.03, 0), (w + 0.3, 0.04, run), 0.004, "z", (-ang, 0, 0))
    return m


@model
def yd_woodpile():
    """Split firewood stacked between two stakes, 1.8 m long, about a metre high. Kept light (it stands in every
    other yard): plain six-sided logs without bevels, pale split ends only on the front."""
    m = Model("yd_woodpile")
    rng = m.rng
    for row in range(5):
        y = 0.09 + row * 0.17
        n = 9 - row // 2
        for i in range(n):
            x = -0.8 + (i + 0.5) * 1.6 / n + rng.uniform(-0.03, 0.03)
            z = rng.uniform(-0.06, 0.06)
            r = rng.uniform(0.07, 0.09)
            m.surface("bark", shade(Pal.Bark, rng.uniform(0.85, 1.15)), 0.08)
            gcyl(m, (x, y, z - 0.33), (x, y + rng.uniform(-0.02, 0.02), z + 0.33), r, r * rng.uniform(0.92, 1.05), 6, 0.0)
            m.surface("timber", shade(Pal.WoodNew, rng.uniform(0.95, 1.15)), 0.05)
            gbox(m, (x, y + r * 0.3, z - 0.335), (r * 1.2, r * 0.9, 0.01), 0.0)
    m.surface("timber", GREYWOOD, 0.06)
    for sx in (-1, 1):
        gcyl(m, (sx * 0.92, 0, 0), (sx * 0.95, 1.05, 0), 0.04, 0.035, 6, 0.0)
    return m


@model
def yd_chopblock():
    """A chopping block with the axe left in it and a few split pieces round it."""
    m = Model("yd_chopblock")
    m.surface("bark", Pal.Bark, 0.06)
    gcyl(m, (0, 0, 0), (0, 0.48, 0), 0.26, 0.25, 14, 0.01)
    m.surface("timber", shade(Pal.WoodNew, 1.05), 0.05)
    gcyl(m, (0, 0.479, 0), (0, 0.482, 0), 0.245, 0.245, 14, 0.0)
    # the axe: helve leaning out, head bitten into the top
    m.surface("timber", shade(Pal.WoodNew, 0.95), 0.04)
    gbeam(m, (0.02, 0.5, -0.02), (0.42, 1.08, -0.22), 0.035, 0.024)
    m.surface("iron", Pal.Iron, 0.04)
    gbox(m, (0.0, 0.5, -0.0), (0.2, 0.09, 0.03), 0.004, "x", (0, 0, 55))
    m.surface("timber", shade(Pal.WoodNew, 1.1), 0.05)
    for i, (x, z, a) in enumerate(((0.45, 0.2, 20), (-0.38, -0.3, -40), (0.1, 0.45, 75))):
        gbox(m, (x, 0.05, z), (0.36, 0.08, 0.1), 0.01, "x", (0, a, 0))
    return m


@model
def yd_washline():
    """Two T-posts four metres apart, a line between them with the wash hung out."""
    m = Model("yd_washline")
    m.surface("timber", GREYWOOD, 0.08)
    for sx in (-1, 1):
        gcyl(m, (sx * 2.0, 0, 0), (sx * 2.0, 2.1, 0), 0.05, 0.045, 8, 0.005)
        gbox(m, (sx * 2.0, 2.0, 0), (0.08, 0.07, 0.7), 0.006, "z")
    m.surface("canvas", Pal.Rope, 0.0)
    sag = lambda t: 1.98 - 0.16 * math.sin(math.pi * t)
    for zl in (-0.3, 0.3):
        m.tube([L(-2.0 + 4.0 * k / 12, sag(k / 12), zl) for k in range(13)], 0.006, 5)
    items = [(-1.45, 0.62, 0.95, Pal.ClothWhite, -0.3), (-0.55, 0.7, 0.62, hexcol(0x8FA6B8), -0.3), (0.35, 0.5, 0.48, Pal.ClothCream, -0.3),
             (1.2, 0.85, 1.05, hexcol(0xE3DCCB), 0.3), (-0.9, 0.5, 0.7, Pal.ClothRed, 0.3), (0.2, 0.42, 0.38, Pal.ClothWhite, 0.3)]
    for x, w, h, col, zl in items:
        top = sag((x + 2.0) / 4.0) - 0.01
        m.surface("cloth", col, 0.05)
        rows = []
        for j in range(4):
            yy = top - h * j / 3
            bow = 0.02 * math.sin(math.pi * j / 3)
            rows.append([L(x - w / 2 + w * i / 4, yy - 0.01 * math.sin(math.pi * i / 4), zl + bow + 0.008 * math.sin(i * 2.1)) for i in range(5)])
        m.grid(rows, closed=False, smooth=True, grain="y")
        m.grid([list(reversed(r)) for r in rows], closed=False, smooth=True, grain="y")
        m.surface("timber", shade(Pal.WoodNew, 1.05), 0.0)
        for px in (x - w / 2 + 0.06, x + w / 2 - 0.06):
            gbox(m, (px, top + 0.02, zl), (0.015, 0.07, 0.022), 0.0, "y")
    return m


@model
def yd_pump():
    """A cast-iron hand pump on a plank platform, its handle raised, a bucket under the spout."""
    m = Model("yd_pump")
    m.surface("planks", shade(Pal.WoodGrey, 0.9), 0.07)
    gbox(m, (0, 0.06, 0), (1.0, 0.12, 1.0), 0.01, "x")
    m.surface("iron", Pal.Iron, 0.04)
    gcyl(m, (0, 0.12, 0), (0, 0.98, 0), 0.075, 0.065, 14, 0.006)
    gcyl(m, (0, 0.98, 0), (0, 1.06, 0), 0.085, 0.06, 14, 0.006)
    gcyl(m, (0, 0.82, -0.04), (0, 0.78, -0.32), 0.03, 0.026, 10, 0.004)          # spout
    gbeam(m, (0, 1.04, 0.04), (0, 1.32, 0.52), 0.035, 0.03)                       # handle up
    gcyl(m, (0, 1.32, 0.52), (0, 1.3, 0.6), 0.022, 0.02, 8, 0.004)
    gbox(m, (0, 1.06, 0.02), (0.04, 0.18, 0.06), 0.004)
    m.surface("planks", shade(Pal.WoodOld, 0.95), 0.05)                          # bucket
    m.lathe(L(0, 0.12, -0.36), [(0.0, 0.0), (0.12, 0.0), (0.145, 0.26), (0.135, 0.26), (0.11, 0.02), (0.0, 0.02)], 14)
    m.surface("iron", Pal.Iron, 0.03)
    m.lathe(L(0, 0.12, -0.36), [(0.123, 0.05), (0.127, 0.05), (0.141, 0.22), (0.137, 0.22)], 14)
    return m


@model
def yd_chute():
    """The loading chute of the stock pens: a cleated ramp rising 1.2 m over six metres to the level of a car's
    door, plank sides on posts. The low end is at z = 0, it climbs towards +z."""
    m = Model("yd_chute")
    length, rise, half = 6.0, 1.2, 0.55
    ang = math.degrees(math.atan2(rise, length))
    run = math.hypot(length, rise)
    m.surface("planks", shade(Pal.WoodOld, 0.9), 0.07)
    gbox(m, (0, rise / 2 + 0.03, length / 2), (half * 2, 0.06, run), 0.006, "z", (-ang, 0, 0))
    m.surface("timber", shade(Pal.WoodDark, 1.1), 0.06)
    for i in range(14):
        t = (i + 0.5) / 14
        gbox(m, (0, rise * t + 0.08, length * t), (half * 2 - 0.1, 0.04, 0.05), 0.004, "x")
    m.surface("timber", GREYWOOD, 0.07)
    for k in range(5):
        z = length * k / 4
        top = rise * (z / length) + 1.75
        for sx in (-1, 1):
            gbox(m, (sx * (half + 0.08), top / 2, z), (0.14, top, 0.14), 0.01, "y")
    m.surface("boards", shade(Pal.WoodGrey, 0.92), 0.07)
    for sx in (-1, 1):
        for j in range(5):
            y0 = 0.35 + j * 0.3
            slab(m, [(sx * (half + 0.02), y0, 0), (sx * (half + 0.02), y0 + rise, length), (sx * (half + 0.02), y0 + rise + 0.18, length),
                     (sx * (half + 0.02), y0 + 0.18, 0)], 0.035, (sx, 0, 0))
    # the deck at the top, level with a car door
    m.surface("planks", shade(Pal.WoodOld, 0.9), 0.07)
    gbox(m, (0, rise + 0.03, length + 0.6), (half * 2 + 0.3, 0.06, 1.2), 0.006, "x")
    for sx in (-1, 1):
        for z in (length + 0.1, length + 1.15):
            gbox(m, (sx * (half + 0.12), (rise) / 2, z), (0.14, rise, 0.14), 0.01, "y")
    return m


@model
def yd_tumbleweed():
    """A dead tumbleweed: a ball of thin, crooked twigs about 0.8 m across."""
    m = Model("yd_tumbleweed")
    rng = m.rng
    m.surface("straw", shade(Pal.GrassDry, 0.85), 0.12)
    for b in range(30):
        a = rng.uniform(0, 2 * math.pi)
        e = rng.uniform(-1.0, 1.0)
        d0 = (math.cos(a) * math.sqrt(1 - e * e), e, math.sin(a) * math.sqrt(1 - e * e))
        a2 = a + rng.uniform(1.4, 2.6)
        e2 = max(-1.0, min(1.0, e + rng.uniform(-0.9, 0.9)))
        d1 = (math.cos(a2) * math.sqrt(1 - e2 * e2), e2, math.sin(a2) * math.sqrt(1 - e2 * e2))
        pts = []
        for k in range(4):
            t = k / 3
            v = [d0[i] * (1 - t) + d1[i] * t for i in range(3)]
            n = math.sqrt(sum(c * c for c in v)) or 1.0
            r = 0.38 * (0.75 + 0.25 * math.sin(math.pi * t)) + rng.uniform(-0.04, 0.04)
            pts.append(L(v[0] / n * r, 0.36 + v[1] / n * r * 0.85, v[2] / n * r))
        m.tube(pts, 0.007, 3)
    return m


def build():
    col = kit.collection("TownKit")
    kit.clear_default_scene()
    objs = [fn().finish(col, sharp_angle=40.0) for fn in BUILDERS]
    kit.layout(objs, 8.0, 7)
    for ob in objs:                      # its own row in canaan_models.blend
        ob.location.y -= 70.0
    return objs


if "ARGS" in globals() or __name__ == "__main__":
    args = globals().get("ARGS", {})
    objs = build()
    print(kit.stats(objs))
    if not args.get("noexport"):
        print("exported", kit.export_fbx("TownKit", "townkit"))
