"""Saddle, saddle bags with a bedroll, and bridle for the horse. Run inside Blender:
    Tools/blender/bmcp.py run Tools/blender/tack.py
Collection "Tack" -> Assets/Canaan/Models/tack.fbx.

The saddle and bags are modelled round the seat point (QuadRig.seat: the top of the back, game axes x right,
y up, z forward); the bridle round the poll, on the default head. The colour alpha names the part the game
dyes: 12 the saddle blanket, 13 the leather; 0 keeps its colour.
"""
import importlib
import math

import kit

importlib.reload(kit)
from kit import L, Model, Pal, shade
from mathutils import Vector

BLANKET, LEATHER = 12, 13
WHITE = (1.0, 1.0, 1.0)
HALF_W, HALF_D = 0.29, 0.35            # the horse's barrel under the saddle: an ellipse centred HALF_D below the seat


def part(m, n):
    m.wind = n / 255.0
    return m


def arc(m, z0, z1, grow, spread, thick=0.008, steps=18, closed=False):
    """A strap or sheet lying over the back between z0 and z1, 'grow' metres off the hide."""
    rx, ry = HALF_W + grow, HALF_D + grow
    a0, a1 = (-180.0, 180.0) if closed else (-spread, spread)
    rings = []
    for i in range(steps + 1):
        a = math.radians(a0 + (a1 - a0) * i / steps)
        sn, cs = math.sin(a), math.cos(a)
        ox, oy = sn * rx, -HALF_D + cs * ry
        ix, iy = sn * (rx - thick), -HALF_D + cs * (ry - thick)
        rings.append([L(ox, oy, z0), L(ox, oy, z1), L(ix, iy, z1), L(ix, iy, z0)])
    m.loft(rings, not closed, 0)


def on_back(a_deg, grow):
    a = math.radians(a_deg)
    return math.sin(a) * (HALF_W + grow), -HALF_D + math.cos(a) * (HALF_D + grow)


def saddle():
    m = Model("tack_saddle")
    # blanket with darker stripes at both ends
    part(m, BLANKET).surface("wool", WHITE, 0.0)
    arc(m, -0.36, 0.30, 0.012, 62)
    m.surface("wool", (0.72, 0.72, 0.72), 0.0)
    arc(m, -0.375, -0.33, 0.015, 62, 0.006)
    arc(m, 0.27, 0.315, 0.015, 62, 0.006)
    # skirt, seat, cantle, swell and horn
    part(m, LEATHER).surface("leather", WHITE, 0.04)
    arc(m, -0.30, 0.24, 0.024, 44, 0.01)
    seat = [(-0.27, 0.110, 0.150, 0.020), (-0.23, 0.095, 0.170, 0.030), (-0.15, 0.052, 0.172, 0.040), (-0.05, 0.040, 0.165, 0.042),
            (0.06, 0.046, 0.150, 0.042), (0.15, 0.070, 0.128, 0.050), (0.21, 0.082, 0.100, 0.050), (0.245, 0.060, 0.060, 0.030)]
    rings = []
    for z, top, half, drop in seat:
        ring = []
        for i in range(12):
            a = math.pi * i / 11.0
            x = -half * math.cos(a)
            y = top - drop * (1.0 - math.sin(a)) - 0.02 * (1.0 - math.sin(a)) ** 2
            ring.append(L(x, y, z))
        ring += [L(half * 0.8, 0.006, z), L(-half * 0.8, 0.006, z)]
        rings.append(ring)
    m.loft(rings, True, 1)
    m.cyl(L(0, 0.105, 0.205), L(0, 0.19, 0.228), 0.019, 0.015, 10, 0.003)
    m.push(L(0, 0.198, 0.231), (-12, 0, 0))
    m.blob((0, 0, 0), (0.034, 0.034, 0.012), 1)
    m.pop()
    m.surface("leather", (0.7, 0.7, 0.7), 0.03)
    arc(m, 0.09, 0.15, 0.012, 180, 0.008, 28, closed=True)                      # the cinch
    for side in (-1, 1):
        x = side * (HALF_W + 0.035)
        part(m, LEATHER).surface("leather", WHITE, 0.04)
        m.beam(L(side * HALF_W * 0.86, -0.10, 0.03), L(x + side * 0.015, -0.52, 0.06), 0.19, 0.012, 0.004, up=(1, 0, 0))       # fender
        m.beam(L(x + side * 0.015, -0.50, 0.06), L(x + side * 0.03, -0.62, 0.07), 0.03, 0.008, 0.002, up=(1, 0, 0))            # stirrup leather
        part(m, 0).surface("boards", Pal.WoodDark, 0.03)                                                                       # the stirrup: a bent wooden bow
        st = (x + side * 0.03, -0.68, 0.07)
        bow = [(-0.052, -0.07), (-0.05, -0.02), (-0.03, 0.045), (0.0, 0.062), (0.03, 0.045), (0.05, -0.02), (0.052, -0.07)]
        m.tube([L(st[0], st[1] + dy, st[2] + dz) for dz, dy in bow], 0.009, 6)
        m.box(L(st[0], st[1] - 0.07, st[2]), (0.034, 0.112, 0.014), 0.003)
        part(m, 0).surface("steel", Pal.Brass, 0.0)
        cx, cy = on_back(side * 46, 0.03)
        m.cyl(L(cx, cy, -0.22), L(cx + side * 0.006, cy, -0.22), 0.014, 0.012, 10, 0.002)                                      # conchos
        m.cyl(L(cx, cy, 0.18), L(cx + side * 0.006, cy, 0.18), 0.014, 0.012, 10, 0.002)
    return m


def bags():
    m = Model("tack_bags")
    for side in (-1, 1):
        part(m, LEATHER).surface("leather", (0.85, 0.85, 0.85), 0.04)
        m.push(L(side * (HALF_W + 0.03), -0.17, -0.42), (0, side * 8.0, 0))
        m.box((0, 0, 0), (0.09, 0.26, 0.24), 0.02)
        m.box((-side * -0.012, 0, 0.055), (0.10, 0.27, 0.13), 0.02)                 # the flap
        part(m, 0).surface("steel", Pal.Brass, 0.0)
        m.box((-side * 0.052 * -1, 0, 0.0), (0.012, 0.035, 0.03), 0.003)
        m.pop()
        part(m, LEATHER).surface("leather", (0.8, 0.8, 0.8), 0.03)
        x0, y0 = on_back(side * 20, 0.03)
        x1, y1 = on_back(side * 58, 0.035)
        m.beam(L(x0, y0, -0.42), L(x1, y1, -0.42), 0.05, 0.008, 0.002, up=(0, 1, 0))
    # bedroll behind the cantle, strapped down
    part(m, 0).surface("wool", Pal.ClothGrey, 0.04)
    m.cyl(L(-0.28, 0.055, -0.40), L(0.28, 0.055, -0.40), 0.078, 0.078, 16, 0.02)
    part(m, LEATHER).surface("leather", WHITE, 0.03)
    for x in (-0.15, 0.15):
        m.cyl(L(x - 0.014, 0.055, -0.40), L(x + 0.014, 0.055, -0.40), 0.082, 0.082, 16, 0.002)
    return m


def bridle():
    """Crownpiece and throatlatch, browband, cheek pieces, noseband and the bit rings, laid on the horse's head
    as horse.py shapes it (head_point), a strap's thickness off the hide. Built round the poll: the game hangs the
    bridle on the Head bone."""
    import horse
    importlib.reload(horse)
    poll = horse.POLL

    def P(t, a, lift):
        return horse.head_point(t, a, lift) - poll

    def band(t0, t1, a0, a1, steps, lift=0.006):
        rows = []
        for t in (t0, t1):
            rows.append([L(*P(t, a0 + (a1 - a0) * k / steps, lift)) for k in range(steps + 1)])
        m.grid(rows, None, closed=False)

    m = Model("tack_bridle")
    part(m, LEATHER).surface("leather", WHITE, 0.03)
    band(0.015, 0.06, -0.55, math.pi + 0.55, 16)                                  # crownpiece over the poll
    band(0.035, 0.075, math.pi + 0.55, 2 * math.pi - 0.55, 8)                      # throatlatch under the jaw
    band(0.095, 0.125, 0.30, math.pi - 0.30, 12, 0.007)                           # browband
    band(0.665, 0.715, -math.pi / 2, 1.5 * math.pi, 22, 0.006)                    # noseband, all round
    for side in (-1, 1):
        a_top = 0.25 if side > 0 else math.pi - 0.25
        a_bit = -0.55 if side > 0 else math.pi + 0.55
        rows = [[], []]
        for q in range(15):
            t = 0.04 + (0.885 - 0.04) * q / 14
            a = a_top + (a_bit - a_top) * (q / 14) ** 1.4
            c = P(t, a, 0.006)
            d = P(t + 0.01, a, 0.006) - c
            nrm = (horse.head_point(t, a, 0.0) - horse.head_c(t)).normalized()
            across = d.cross(nrm).normalized() * 0.011
            rows[0].append(L(*(c - across)))
            rows[1].append(L(*(c + across)))
        m.grid(rows, None, closed=False)                                            # cheek piece
        part(m, 0).surface("steel", Pal.Steel, 0.0)
        ring_c = P(0.90, a_bit, 0.004)
        out = (horse.head_point(0.90, a_bit, 0.0) - horse.head_c(0.90)).normalized()
        up = horse.HEAD_UP
        fwd = horse.HEAD_DIR
        ring_c = ring_c + out * 0.004
        m.tube([L(*(ring_c + fwd * (0.022 * math.cos(a)) + up * (0.022 * math.sin(a)))) for a in [i * math.pi / 7 for i in range(14)]], 0.0035, 5, closed=True)
        m.cyl(L(*(ring_c - out * 0.004)), L(*(ring_c - out * 0.03)), 0.006, 0.006, 8, 0.001)       # the bit's bar into the mouth
        part(m, LEATHER).surface("leather", WHITE, 0.03)
    return m


def build():
    col = kit.collection("Tack")
    kit.clear_default_scene()
    objs = [saddle().finish(col), bags().finish(col), bridle().finish(col)]
    return objs


if "ARGS" in globals() or __name__ == "__main__":
    args = globals().get("ARGS", {})
    objs = build()
    print(kit.stats(objs))
    if not args.get("noexport"):
        print("exported", kit.export_fbx("Tack", "tack"))
