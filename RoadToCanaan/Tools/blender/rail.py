"""The railway: a length of track, the engine, its tender, a boxcar, a passenger coach, the buffer stop - and the
moving parts the game turns as the train runs (World/Trains.cs): wheelsets, the engine's drivers, coupling rods,
main rods, crossheads.
    Tools/blender/bmcp.py run Tools/blender/rail.py
Collection "Rail" -> Assets/Canaan/Models/rail.fbx.

A car's own axes: x across the track, y up, z along it (the engine's pilot at +z); the origin is the middle of
the car, RAIL below it is the top of the rail head (the game stands the cars that much over the track).
Colour alpha 12 = the car's paint (dyed in the game). Glass is not modelled: the game sets panes in the
openings (they glow at night). The bodies carry no wheels: each wheelset is its own model with its origin on the
axle, the drivers' crank pins at 205 degrees (right) and 115 degrees (left) from straight up towards +z.
"""
import importlib
import math

import kit

importlib.reload(kit)
from kit import L, Model, Pal, gbeam, gbox, gcyl, hexcol, shade

DYE = 12 / 255.0
# the engine's running gear, shared with World/Trains.cs
DRIVER_R, DRIVER_Z = 0.80, (-2.55, -0.75)
CRANK = 0.30                  # crank pin radius
PIN_R, PIN_L = 205.0, 115.0   # pin angles, degrees from up towards +z
CYL_X = 1.07                  # cylinders and crossheads, either side
MAIN_ROD = 2.25               # crank pin to crosshead
LEAD_R, LEAD_Z = 0.38, (2.1, 3.3)
RAIL = 0.11                   # top of the rail in a car's frame
GAUGE = 0.72                  # half the gauge
SEG = 6.0                     # length of the track piece
BLACK = hexcol(0x26282C)
GRIME = hexcol(0x3A3632)
DUST = hexcol(0x9A8A70)                     # what the wind lays on everything


def paint(m, k=1.0, surf="boards", jitter=0.03):
    m.wind = DYE
    m.surface(surf, (k, k, k), jitter)
    return m


def mat(m, surf, tint, jitter=0.03):
    m.wind = 0.0
    m.surface(surf, tint, jitter)
    return m


def zlathe(m, y, profile, seg=24, smooth=True):
    """Revolves (radius, z) points about the line x = 0, height y, running along z."""
    m.lathe(L(0, y, 0), [(r, -z) for r, z in profile], seg, "y", smooth)


def hoop(m, centre, radius, r, seg=20, plane="xz", tube_seg=6):
    cx, cy, cz = centre
    pts = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        c, s = math.cos(a) * radius, math.sin(a) * radius
        pts.append(L(cx + c, cy, cz + s) if plane == "xz" else L(cx, cy + c, cz + s))
    m.tube(pts, r, tube_seg, closed=True)


# ---------------------------------------------------------------------- running gear

def wheel(m, x, y, z, r, seg=20):
    """A plate wheel on the x axis; the flange is on the side towards the middle of the track."""
    out = 1.0 if x > 0 else -1.0
    prof = [(0.0, 0.065), (0.08, 0.065), (0.095, 0.035), (r - 0.085, 0.02), (r - 0.06, 0.055), (r, 0.055),
            (r - 0.005, -0.035), (r + 0.03, -0.045), (r + 0.03, -0.057), (r - 0.07, -0.05), (0.095, -0.03), (0.0, -0.03)]
    m.lathe(L(x, y, z), [(rr, -o * out) for rr, o in prof], seg, "x")


def driver(m, x, y, z, r, pin_angle, spokes=14):
    """A spoked driving wheel with its crank pin and balance weight. Returns the pin (y, z)."""
    out = 1.0 if x > 0 else -1.0
    mat(m, "steel", (0.5, 0.51, 0.53), 0.02)                                      # the tyre, kept bright
    rim = [(r - 0.10, 0.055), (r, 0.055), (r - 0.005, -0.04), (r + 0.035, -0.05), (r + 0.035, -0.064), (r - 0.10, -0.055)]
    m.lathe(L(x, y, z), [(rr, -o * out) for rr, o in rim + [rim[0]]], 28, "x")
    mat(m, "plain", shade(Pal.WoodRed, 1.05), 0.03)
    gcyl(m, (x - 0.085, y, z), (x + 0.085, y, z), 0.15, 0.15, 14, 0.01)
    for i in range(spokes):
        a = 2 * math.pi * (i + 0.5) / spokes
        gbeam(m, (x, y + math.cos(a) * 0.13, z + math.sin(a) * 0.13), (x, y + math.cos(a) * (r - 0.09), z + math.sin(a) * (r - 0.09)), 0.05, 0.05, 0.006)
    py, pz = y + math.cos(pin_angle) * 0.30, z + math.sin(pin_angle) * 0.30
    gcyl(m, (x, py, pz), (x + 0.11 * out, py, pz), 0.085, 0.085, 10, 0.006)
    for da in (-0.42, -0.14, 0.14, 0.42):                                       # the balance weight, opposite the pin
        a0, a1 = pin_angle + math.pi + da - 0.15, pin_angle + math.pi + da + 0.15
        rr = r - 0.17
        gbeam(m, (x, y + math.cos(a0) * rr, z + math.sin(a0) * rr), (x, y + math.cos(a1) * rr, z + math.sin(a1) * rr), 0.07, 0.15, 0.006)
    mat(m, "steel", (0.36, 0.37, 0.39), 0.02)
    gcyl(m, (x + 0.11 * out, py, pz), (x + 0.24 * out, py, pz), 0.04, 0.04, 8, 0.004)
    return py, pz


def axle(m, y, z, r, seg=20):
    mat(m, "iron", shade(Pal.Iron, 0.85))
    gcyl(m, (-0.8, y, z), (0.8, y, z), 0.05, 0.05, 8, 0)
    for sx in (-1, 1):
        wheel(m, sx * GAUGE, y, z, r, seg)


def truck(m, zc, r=0.42, wb=0.72, wheels=False):
    """A four-wheel truck: arch-bar side frames, bolster and springs (and its two axles if 'wheels': the game turns
    separate wheelsets)."""
    y = RAIL + r
    if wheels:
        for dz in (-wb, wb):
            axle(m, y, zc + dz, r)
    mat(m, "iron", shade(Pal.Iron, 0.8))
    for sx in (-1, 1):
        x = sx * 0.94
        gbeam(m, (x, y + 0.10, zc - wb - 0.2), (x, y + 0.10, zc + wb + 0.2), 0.03, 0.07, 0.004)
        gbeam(m, (x, y + 0.08, zc - wb), (x, y - 0.20, zc - 0.28), 0.03, 0.06, 0.004)
        gbeam(m, (x, y + 0.08, zc + wb), (x, y - 0.20, zc + 0.28), 0.03, 0.06, 0.004)
        gbeam(m, (x, y - 0.20, zc - 0.30), (x, y - 0.20, zc + 0.30), 0.03, 0.06, 0.004)
        for dz in (-wb, wb):
            gbox(m, (x, y, zc + dz), (0.13, 0.19, 0.17), 0.012, "y")           # journal boxes
        for dz in (-0.13, 0.13):
            gcyl(m, (x - 0.01, y - 0.17, zc + dz), (x - 0.01, y + 0.07, zc + dz), 0.05, 0.05, 8, 0)   # springs
    gbox(m, (0, y + 0.16, zc), (2.02, 0.17, 0.30), 0.012, "x")                 # bolster
    for sx in (-1, 1):                                                           # journal box lids
        for dz in (-wb, wb):
            gbox(m, (sx * 1.012, y + 0.01, zc + dz), (0.016, 0.12, 0.13), 0.004, "x")
    # the brake beams, a shoe against each tread on the inside of the truck
    mat(m, "iron", shade(Pal.Iron, 0.7))
    for s in (-1, 1):
        zb = zc + s * (wb - r - 0.035)
        gbox(m, (0, y - 0.04, zb), (1.56, 0.06, 0.05), 0.004, "x")
        for sx in (-1, 1):
            gbox(m, (sx * GAUGE, y - 0.02, zb), (0.1, 0.24, 0.05), 0.006, "y")


def underframe(m, length, width=2.5):
    mat(m, "timber", shade(GRIME, 1.0), 0.04)
    gbox(m, (0, 0.99, 0), (width, 0.22, length - 0.1), 0.01, "z")
    mat(m, "iron", shade(Pal.Iron, 0.85))
    for sz in (-1, 1):
        gbox(m, (0, 0.99, sz * (length * 0.5 - 0.05)), (width + 0.04, 0.26, 0.12), 0.012, "x")
        gbox(m, (0, 0.93, sz * (length * 0.5 + 0.17)), (0.26, 0.2, 0.36), 0.02, "z")      # draw bar
        gbox(m, (0, 0.93, sz * (length * 0.5 + 0.37)), (0.2, 0.06, 0.12), 0.01, "z")      # the link


def truss_rods(m, length):
    """The rods under a wooden car that keep its frame from sagging."""
    mat(m, "iron", shade(Pal.Iron, 0.9))
    e, q = length * 0.5 - 0.9, 1.3
    for sx in (-1, 1):
        x = sx * 0.9
        m.tube([L(x, 0.9, -e), L(x, 0.5, -q), L(x, 0.5, q), L(x, 0.9, e)], 0.018, 6)
        for sz in (-1, 1):
            gbox(m, (x, 0.69, sz * q), (0.07, 0.4, 0.07), 0.008, "y")
    for sz in (-1, 1):
        gbox(m, (0, 0.86, sz * q), (1.9, 0.08, 0.1), 0.008, "x")


def arched_roof(m, half, eave, crown, z0, z1, thick=0.06):
    """A roof bowed across the car, from z0 to z1."""
    xs = (-1.0, -0.72, -0.4, 0.0, 0.4, 0.72, 1.0)
    top = [(half * t, eave + (crown - eave) * (1.0 - t * t)) for t in xs]
    sect = top + [(half, eave - thick), (-half, eave - thick)]
    m.loft([[L(x, y, z) for x, y in sect] for z in (z0, z1)], True, 0, True)


# ---------------------------------------------------------------------- track

def track():
    """Six metres of track from z = 0: ballast, seven ties, two rails. The origin is on the roadbed."""
    m = Model("rail_track")
    mat(m, "dirt", hexcol(0x8A8378), 0.03)
    bed = lambda z: [L(-1.78, -0.08, z), L(-1.36, 0.07, z), L(1.36, 0.07, z), L(1.78, -0.08, z)]
    m.loft([bed(0.0), bed(SEG)], False, 0, False)
    mat(m, "timber", shade(Pal.WoodDark, 0.72), 0.14)
    for k in range(7):
        z = (k + 0.5) * SEG / 7 + m.rng.uniform(-0.03, 0.03)
        gbox(m, (m.rng.uniform(-0.05, 0.05), 0.06, z), (2.32 + m.rng.uniform(-0.06, 0.08), 0.14, 0.22), 0.0, "x",
             rot=(0, m.rng.uniform(-1.6, 1.6), 0))
    prof = [(-0.055, 0.13), (0.055, 0.13), (0.055, 0.143), (0.011, 0.158), (0.011, 0.214), (0.031, 0.224),
            (0.031, 0.25), (-0.031, 0.25), (-0.031, 0.224), (-0.011, 0.214), (-0.011, 0.158), (-0.055, 0.143)]
    for sx in (-1, 1):
        mat(m, "iron", hexcol(0x5A463A), 0.03)                                  # weathered sides
        m.loft([[L(sx * GAUGE + px, py, z) for px, py in prof] for z in (0.0, SEG)], True, 0, False)
        mat(m, "steel", (0.66, 0.67, 0.69), 0.0)                                # the running face, worn bright
        gbox(m, (sx * GAUGE, 0.2505, SEG * 0.5), (0.05, 0.003, SEG), 0.0, "z")
    return m


def buffer_stop():
    """The end of the line: a timber across the rails on two raked posts. The train side is -z."""
    m = Model("rail_buffer")
    mat(m, "timber", shade(Pal.WoodDark, 0.85), 0.08)
    for sx in (-1, 1):
        x = sx * GAUGE
        gbeam(m, (x, -0.1, 0.0), (x, 1.42, 0.0), 0.26, 0.26, 0.015)
        gbeam(m, (x, 1.2, 0.08), (x, -0.1, 1.55), 0.2, 0.2, 0.012)
        gbeam(m, (x, 0.45, 0.1), (x, 0.45, 1.0), 0.14, 0.16, 0.01)
    gbox(m, (0, 1.08, -0.27), (2.75, 0.34, 0.3), 0.02, "x")
    mat(m, "iron", shade(Pal.Iron, 0.9))
    for sx in (-1, 1):
        gbox(m, (sx * GAUGE, 1.08, -0.43), (0.42, 0.26, 0.02), 0.006, "x")
        for dy in (-0.07, 0.07):
            gcyl(m, (sx * GAUGE + 0.12, 1.08 + dy, -0.44), (sx * GAUGE + 0.12, 1.08 + dy, -0.46), 0.018, 0.018, 6, 0)
            gcyl(m, (sx * GAUGE - 0.12, 1.08 + dy, -0.44), (sx * GAUGE - 0.12, 1.08 + dy, -0.46), 0.018, 0.018, 6, 0)
    mat(m, "dirt", hexcol(0x8A8378), 0.04)
    m.push(L(0, 0.0, 0.95))
    m.blob((0, 0, 0), (1.3, 0.75, 0.26), 1, lumps=0.12)
    m.pop()
    return m


# ---------------------------------------------------------------------- the engine

def grime(m, fn):
    """Darkens and tints the paint where time leaves its mark: fn(x, y, z) in game axes -> (r, g, b) multipliers.
    The part number in the colour's alpha (the game's dye) is kept."""
    col = m.col
    for f in m.bm.faces:
        for lp in f.loops:
            co = lp.vert.co
            k = fn(-co.x, co.z, -co.y)
            c = lp[col]
            lp[col] = (c[0] * k[0], c[1] * k[1], c[2] * k[2], c[3])


def _smooth(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def _mottle(x, y, z):
    """A cheap, smooth pseudo-noise in 0..1 for patchy wear."""
    return 0.5 + 0.25 * math.sin(x * 3.1 + z * 1.7) * math.sin(y * 2.3 + z * 0.9) + 0.25 * math.sin(x * 7.3 - y * 5.1 + z * 4.7) * 0.6


def loco():
    """A wood-burning eight-wheeler of the 1880s: a slim boiler swelling over the firebox (a wagon top), a diamond
    spark-arresting stack on a sooty smokebox, a sand dome and a steam dome in the jacket's Russia-iron blue under
    brass caps, the bell in its yoke between them, safety valves and the whistle on the steam dome, an oil headlamp
    on its bracket, a wooden cab, four coupled drivers and a slatted pilot. Worn: soot about the stack and smokebox,
    grime low down, dull brass. 8.5 m over the frame. Marks: 'lamp' (the headlamp's lens), 'smoke' (the mouth of the
    stack), 'whistle', 'bell', 'firedoor', 'cock_r'/'cock_l' (the cylinder cocks) - as World/Trains.cs reads them."""
    m = Model("rail_loco")
    by = 2.30                                        # the boiler's axis
    front_r, back_r = 0.62, 0.70                     # its radius forward, and over the firebox
    dr, dy = DRIVER_R, RAIL + DRIVER_R
    dz = DRIVER_Z
    jacket = hexcol(0x4B5560)                        # Russia iron, blue-grey
    graphite = hexcol(0x1F2022)
    brass = shade(Pal.Brass, 0.88)
    red = shade(Pal.WoodRed, 0.92)

    # frame, firebox, deck
    mat(m, "plain", shade(BLACK, 1.0))
    for sx in (-1, 1):
        gbox(m, (sx * 0.5, 0.94, -0.05), (0.09, 0.16, 8.3), 0.01, "z")
    gbox(m, (0, 1.40, -2.35), (1.16, 0.74, 1.3), 0.03, "z")                     # firebox between the drivers
    gbox(m, (0, 1.05, 4.13), (2.4, 0.26, 0.16), 0.015, "x")                     # front beam
    gbox(m, (0, 1.2, 3.86), (2.3, 0.05, 0.56), 0.008, "x")                      # pilot deck
    mat(m, "planks", shade(Pal.WoodOld, 0.75), 0.08)
    gbox(m, (0, 1.72, -3.0), (2.5, 0.06, 2.5), 0.006, "z")                      # cab floor and footplate
    mat(m, "plain", shade(BLACK, 1.05))
    for sx in (-1, 1):
        gbox(m, (sx * 1.0, 1.735, 0.5), (0.4, 0.035, 4.5), 0.006, "z")          # running boards
        for z in (-1.2, 0.5, 2.2):
            gbeam(m, (sx * 0.62, 1.55, z), (sx * 1.16, 1.72, z), 0.04, 0.04, 0.004)

    # the boiler: a slim barrel, a conical course rising to the wagon top over the firebox, lagging bands
    mat(m, "plain", jacket, 0.03)
    zlathe(m, by, [(0.0, -2.92), (back_r, -2.92), (back_r, -0.75), (front_r, 0.15), (front_r, 2.7), (0.0, 2.7)], 28)
    mat(m, "steel", brass, 0.03)
    for z, r in ((-2.2, back_r), (-1.3, back_r), (0.6, front_r), (1.6, front_r), (2.55, front_r)):
        zlathe(m, by, [(r + 0.001, z - 0.025), (r + 0.011, z - 0.02), (r + 0.011, z + 0.02), (r + 0.001, z + 0.025)], 28)
    # the smokebox, a size bigger, matt black, its door dogged shut, the number plate on it
    mat(m, "plain", graphite, 0.04)
    zlathe(m, by, [(0.0, 2.68), (0.66, 2.68), (0.67, 2.72), (0.67, 3.58), (0.64, 3.62), (0.0, 3.62)], 28)
    zlathe(m, by, [(0.55, 3.62), (0.57, 3.65), (0.48, 3.69), (0.0, 3.71)], 24)
    gcyl(m, (0, by, 3.70), (0, by, 3.77), 0.05, 0.035, 8, 0.004)                # the door's centre handle
    for a in (0.3, 1.25, 2.2, 3.15, 4.1, 5.05):                                  # dogs round the rim
        gbox(m, (0.57 * math.cos(a), by + 0.57 * math.sin(a), 3.645), (0.05, 0.05, 0.05), 0.006, "z")
    for sx in (-1, 1):                                                           # hinge straps
        gbeam(m, (sx * 0.05, by + 0.25 * sx, 3.70), (sx * 0.45, by + 0.25 * sx, 3.66), 0.03, 0.05, 0.004)
    mat(m, "steel", brass, 0.03)
    m.lathe(L(0, by + 0.05, 3.725), [(0.0, 0.0), (0.14, 0.0), (0.15, -0.01), (0.14, -0.022), (0.0, -0.026)], 20, "y")   # number plate
    # the stack: a straight stem, a diamond of spark netting over it, a lip
    sx0, top = 3.12, by + 0.66
    mat(m, "plain", graphite, 0.05)
    m.lathe(L(0, 0, sx0), [(0.25, top - 0.06), (0.21, top + 0.04), (0.185, top + 0.1), (0.185, 3.52), (0.22, 3.57), (0.4, 3.84), (0.41, 3.88),
                           (0.27, 4.1), (0.28, 4.14), (0.255, 4.16), (0.0, 4.14)], 24)
    mat(m, "iron", shade(Pal.Rust, 0.75), 0.06)
    m.lathe(L(0, 0, sx0), [(0.19, 3.50), (0.2, 3.52), (0.2, 3.55), (0.19, 3.56)], 24)        # a rusted collar
    m.mark("smoke", L(0, 4.16, sx0))

    # the bell in its yoke, the sand dome, the steam dome with its safety valves and whistle
    def dome(z, r, h, base_y):
        mat(m, "plain", jacket, 0.03)
        m.lathe(L(0, 0, z), [(r + 0.06, base_y - 0.06), (r + 0.03, base_y + 0.02), (r, base_y + 0.08), (r, base_y + h - 0.12), (r - 0.02, base_y + h - 0.1), (0.0, base_y + h - 0.1)], 22)
        mat(m, "steel", brass, 0.03)
        m.lathe(L(0, 0, z), [(r - 0.01, base_y + h - 0.12), (r + 0.01, base_y + h - 0.1), (r - 0.04, base_y + h - 0.02), (r - 0.14, base_y + h + 0.03), (0.0, base_y + h + 0.04)], 22)
        m.lathe(L(0, 0, z), [(r + 0.062, base_y - 0.05), (r + 0.07, base_y - 0.04), (r + 0.07, base_y - 0.01), (r + 0.05, base_y + 0.0)], 22)
    dome(1.32, 0.24, 0.42, by + front_r)                                         # sand
    dome(-0.55, 0.27, 0.46, by + back_r - 0.02)                                  # steam
    sv = by + back_r + 0.46
    for dx in (-0.08, 0.08):                                                     # safety valves
        gcyl(m, (dx, sv, -0.62), (dx, sv + 0.18, -0.62), 0.035, 0.03, 10, 0.004)
        gcyl(m, (dx, sv + 0.18, -0.62), (dx, sv + 0.21, -0.62), 0.045, 0.02, 10, 0.004)
    gcyl(m, (0.13, sv - 0.04, -0.72), (0.13, sv + 0.18, -0.72), 0.022, 0.028, 10, 0.004)   # the whistle
    m.lathe(L(0.13, 0, -0.72), [(0.0, sv + 0.18), (0.045, sv + 0.18), (0.045, sv + 0.34), (0.03, sv + 0.36), (0.0, sv + 0.37)], 12)
    m.mark("whistle", L(0.13, sv + 0.36, -0.72))
    bz = 2.15
    m.lathe(L(0, 3.18, bz), [(0.0, 0.0), (0.12, 0.0), (0.115, 0.03), (0.075, 0.14), (0.06, 0.2), (0.0, 0.21)], 16)          # bell
    mat(m, "plain", shade(BLACK, 1.0))
    for sx in (-1, 1):
        gbeam(m, (sx * 0.16, by + front_r - 0.02, bz), (sx * 0.16, 3.44, bz), 0.03, 0.03, 0.004)
    gbeam(m, (-0.16, 3.43, bz), (0.16, 3.43, bz), 0.03, 0.03, 0.004)
    m.mark("bell", L(0, 3.3, bz))
    mat(m, "canvas", hexcol(0x8E7D60), 0.05)                                    # the bell cord back to the cab
    m.tube([L(0, 3.36, bz), L(0.2, 3.3, 0.9), L(0.32, 3.24, -1.7)], 0.006, 4)

    # sand from the sand dome to the rails ahead of the leading driver; feed water from the injectors to the check
    # valves; the backhead round the fire door
    mat(m, "steel", (0.28, 0.29, 0.31), 0.03)
    for sx in (-1, 1):
        m.tube([L(sx * 0.17, by + front_r + 0.1, 1.32), L(sx * 0.5, by + 0.42, 1.1), L(sx * 0.76, 1.75, 0.4), L(sx * 0.78, 1.0, 0.05), L(sx * GAUGE, 0.3, -0.02)], 0.016, 6)
    mat(m, "steel", brass, 0.03)
    for sx in (-1, 1):
        m.tube([L(sx * 0.64, by - 0.46, -1.75), L(sx * 0.68, by - 0.38, -0.6), L(sx * 0.66, by - 0.26, 0.9), L(sx * 0.6, by - 0.1, 1.15)], 0.022, 7)
        gcyl(m, (sx * 0.56, by - 0.06, 1.15), (sx * 0.67, by - 0.12, 1.15), 0.05, 0.05, 10, 0.006)          # check valve
    mat(m, "plain", shade(BLACK, 0.7), 0.02)
    gbox(m, (0, 1.95, -2.92), (0.62, 0.5, 0.04), 0.008, "x")
    mat(m, "steel", brass, 0.03)
    for gx in (-0.32, 0.32):                                                     # gauges on the backhead
        m.lathe(L(gx, 2.55, 0.0), [(r, -z) for r, z in ((0.0, -2.96), (0.07, -2.96), (0.075, -2.94), (0.0, -2.93))], 12, "y")
    m.mark("firedoor", L(0, 1.95, -2.945))
    for sx, name in ((1, "cock_r"), (-1, "cock_l")):
        m.mark(name, L(sx * CYL_X, dy + 0.04 - 0.36, 2.7))

    # the oil headlamp on its bracket over the smokebox: a box lamp with a chimney and a brass-rimmed lens
    mat(m, "plain", shade(BLACK, 1.1))
    gbox(m, (0, by + 0.66 + 0.03, 3.36), (0.36, 0.05, 0.5), 0.01, "x")
    for sx in (-1, 1):
        gbeam(m, (sx * 0.14, by + 0.62, 3.2), (sx * 0.14, by + 0.69, 3.5), 0.03, 0.03, 0.004)
    gbox(m, (0, 3.23, 3.36), (0.38, 0.44, 0.4), 0.02, "y")
    gbox(m, (0, 3.47, 3.36), (0.42, 0.04, 0.44), 0.01, "x")
    gcyl(m, (0, 3.49, 3.32), (0, 3.62, 3.32), 0.05, 0.045, 10, 0.004)
    gbox(m, (0, 3.64, 3.32), (0.16, 0.03, 0.16), 0.006, "x")
    mat(m, "steel", brass, 0.03)
    zlathe(m, 3.22, [(0.155, 3.555), (0.17, 3.565), (0.17, 3.59), (0.14, 3.59), (0.14, 3.565)], 20)
    m.mark("lamp", L(0, 3.22, 3.575))

    # the cab: boards, dyed by the game, framed windows, an arched roof
    paint(m, 1.0, "boards", 0.04)
    for sx in (-1, 1):
        x = sx * 1.225
        gbox(m, (x, 2.175, -2.975), (0.05, 0.85, 2.45), 0.004, "y")
        gbox(m, (x, 3.625, -2.975), (0.05, 0.45, 2.45), 0.004, "z")
        gbox(m, (x, 3.0, -3.9), (0.05, 0.8, 0.6), 0.004, "y")
        gbox(m, (x, 3.0, -1.975), (0.05, 0.8, 0.45), 0.004, "y")
        gbox(m, (sx * 0.9, 2.8, -1.775), (0.7, 2.1, 0.05), 0.004, "y")
    gbox(m, (0, 3.48, -1.775), (1.1, 0.74, 0.05), 0.004, "x")
    paint(m, 0.7, "boards", 0.04)
    for sx in (-1, 1):
        x = sx * 1.255
        gbox(m, (x, 2.58, -2.9), (0.03, 0.05, 1.5), 0.004, "z")
        gbox(m, (x, 3.42, -2.9), (0.03, 0.05, 1.5), 0.004, "z")
        for z in (-3.62, -2.9, -2.18):
            gbox(m, (x, 3.0, z), (0.03, 0.8, 0.045), 0.004, "y")
        gbox(m, (x, 1.76, -2.975), (0.03, 0.07, 2.47), 0.004, "z")
        for y in (2.2, 1.95):                                                    # panel mouldings under the window
            gbox(m, (x, y, -2.975), (0.02, 0.03, 2.2), 0.003, "z")
    mat(m, "canvas", hexcol(0x403C37), 0.04)
    arched_roof(m, 1.42, 3.85, 4.05, -4.5, -1.62)
    mat(m, "plain", shade(BLACK, 1.0))
    for sx in (-1, 1):
        for y in (0.62, 1.12):
            gbox(m, (sx * 1.2, y, -4.05), (0.3, 0.04, 0.4), 0.006, "z")
        gbeam(m, (sx * 1.34, 0.6, -4.05), (sx * 1.34, 1.7, -4.05), 0.02, 0.05, 0.004)
    mat(m, "steel", brass, 0.03)
    for sx in (-1, 1):                                                           # handrails along the boiler
        m.tube([L(sx * 0.76, 2.72, -1.7), L(sx * 0.72, 2.66, 2.6)], 0.013, 6)
        for z in (-1.2, 0.6, 2.4):
            r = back_r if z < -0.75 else front_r
            gcyl(m, (sx * (r - 0.02), by + 0.3, z), (sx * 0.74, 2.69, z), 0.011, 0.011, 5, 0)

    # cylinders with their steam chests, the outside steam pipes down from the smokebox, guides
    cy, cx = dy + 0.04, CYL_X
    for sx in (-1, 1):
        x = sx * cx
        mat(m, "plain", shade(BLACK, 1.1))
        gcyl(m, (x, cy, 2.3), (x, cy, 3.15), 0.27, 0.27, 18, 0.02)
        gbox(m, (x - sx * 0.02, cy + 0.34, 2.72), (0.4, 0.2, 0.62), 0.02, "z")
        gbox(m, (sx * 0.72, cy + 0.2, 2.72), (0.5, 0.5, 0.5), 0.02, "x")
        m.tube([L(sx * 0.5, by - 0.2, 3.0), L(sx * 0.8, by - 0.6, 2.95), L(sx * 0.95, cy + 0.5, 2.8)], 0.07, 10)
        mat(m, "steel", brass, 0.03)
        gcyl(m, (x, cy, 3.15), (x, cy, 3.19), 0.2, 0.17, 14, 0.006)
        gcyl(m, (x, cy, 2.3), (x, cy, 2.27), 0.2, 0.2, 14, 0.006)
        gbox(m, (x - sx * 0.02, cy + 0.45, 2.72), (0.34, 0.03, 0.5), 0.006, "z")       # the steam chest's brass cover
        mat(m, "steel", (0.36, 0.37, 0.39), 0.03)
        for ddy in (-0.1, 0.1):
            gbox(m, (x, cy + ddy, 1.62), (0.07, 0.03, 1.4), 0.004, "z")
        mat(m, "plain", shade(BLACK, 1.0))
        gbox(m, (x - sx * 0.2, cy + 0.14, 0.94), (0.5, 0.06, 0.06), 0.006, "x")
        mat(m, "steel", brass, 0.03)
        for dz2 in (2.42, 3.02):
            gcyl(m, (x, cy - 0.27, dz2), (x, cy - 0.33, dz2), 0.018, 0.012, 6, 0)
    mat(m, "plain", shade(BLACK, 1.05))
    gbox(m, (0, 1.42, 2.72), (1.3, 0.56, 0.66), 0.02, "x")                      # the saddle under the smokebox
    mat(m, "iron", shade(Pal.Iron, 0.85))
    for sx in (-1, 1):
        gbox(m, (sx * 0.72, 1.72, (dz[0] + dz[1]) * 0.5), (0.2, 0.03, 3.4), 0.004, "z")
        for z in dz:
            gbox(m, (sx * 0.5, dy, z), (0.16, 0.26, 0.24), 0.012, "y")
        # the frame plates run deep between the drivers; over each axle box a leaf spring, an equalizer between
        mat(m, "plain", shade(BLACK, 0.95))
        gbox(m, (sx * 0.5, 1.12, -1.2), (0.08, 0.36, 4.6), 0.008, "z")
        mat(m, "iron", shade(Pal.Iron, 0.8))
        for z in dz:
            for k in range(5):
                w = 0.9 - k * 0.14
                gbox(m, (sx * 0.5, dy + 0.2 + k * 0.03, z), (0.1, 0.025, w), 0.004, "z")
            gbox(m, (sx * 0.5, dy + 0.2, z), (0.12, 0.09, 0.1), 0.006, "y")
        gbeam(m, (sx * 0.5, dy + 0.3, dz[0] + 0.42), (sx * 0.5, dy + 0.28, dz[1] - 0.42), 0.05, 0.08, 0.006)
        gbox(m, (sx * 0.5, dy + 0.42, (dz[0] + dz[1]) * 0.5), (0.1, 0.18, 0.12), 0.008, "y")
    gbeam(m, (0.62, 1.42, -2.2), (0.62, 1.38, 1.0), 0.03, 0.04, 0.004)          # the reach rod to the valve gear
    # the air pump on the boiler's right side, its pipes, the reservoir under the running board
    mat(m, "plain", shade(BLACK, 1.05))
    gcyl(m, (0.86, 1.82, 1.85), (0.86, 2.62, 1.85), 0.13, 0.13, 14, 0.01)
    gcyl(m, (0.86, 2.0, 1.85), (0.86, 2.2, 1.85), 0.15, 0.15, 14, 0.01)
    mat(m, "steel", brass, 0.03)
    gcyl(m, (0.86, 2.62, 1.85), (0.86, 2.68, 1.85), 0.1, 0.07, 12, 0.004)
    m.tube([L(0.86, 2.4, 1.72), L(0.75, 2.6, 1.3), L(0.55, by + 0.3, 0.9)], 0.018, 6)
    mat(m, "plain", shade(BLACK, 1.0))
    gcyl(m, (-0.95, 1.5, -1.6), (-0.95, 1.5, 0.2), 0.2, 0.2, 14, 0.02)
    m.tube([L(0.86, 1.85, 1.85), L(0.9, 1.4, 1.2), L(0.3, 1.25, -0.4), L(-0.8, 1.45, -0.3)], 0.018, 6)
    ty = RAIL + LEAD_R
    mat(m, "plain", shade(BLACK, 0.95))
    for sx in (-1, 1):
        gbox(m, (sx * 0.86, ty + 0.02, 2.7), (0.08, 0.16, 1.7), 0.01, "z")
        for z in LEAD_Z:
            gbox(m, (sx * 0.86, ty, z), (0.14, 0.2, 0.2), 0.012, "y")
    gbox(m, (0, ty + 0.12, 2.7), (1.8, 0.12, 0.3), 0.012, "x")
    gcyl(m, (0, ty + 0.15, 2.7), (0, 0.95, 2.7), 0.12, 0.12, 10, 0)

    # the pilot: slats of faded red, iron-shod
    mat(m, "plain", red, 0.05)
    nose = 5.3
    for sx in (-1, 1):
        gbeam(m, (sx * 1.12, 0.26, 4.26), (0, 0.26, nose), 0.07, 0.07, 0.006)
    for i in range(-5, 6):
        x = i * 0.2
        z = nose - abs(x) / 1.12 * (nose - 4.26)
        gbeam(m, (x, 1.12, 4.22), (x, 0.27, z - 0.02), 0.04, 0.05, 0.005)
    mat(m, "plain", shade(BLACK, 1.0))
    gbox(m, (0, 0.5, nose - 0.02), (0.14, 0.2, 0.18), 0.015, "z")
    for sx in (-1, 1):
        gcyl(m, (sx * 1.05, 1.18, 4.13), (sx * 1.05, 1.75, 4.13), 0.014, 0.01, 6, 0)
    gbox(m, (0, 0.93, -4.42), (0.26, 0.2, 0.36), 0.02, "z")

    # years of work: soot about the stack and down the smokebox, grime low down, the paint patchy
    def wear(x, y, z):
        d2 = x * x + (z - sx0) * (z - sx0)
        soot = math.exp(-d2 / 1.1) * _smooth(2.4, 3.3, y) * 0.55
        box = _smooth(2.55, 2.75, z) * (1.0 - _smooth(3.6, 3.75, z)) * (0.25 + 0.2 * math.sin(x * 9.0 + y * 2.0))
        low = (1.0 - _smooth(0.6, 1.9, y)) * 0.35
        k = (1.0 - soot) * (1.0 - box * _smooth(1.8, 2.9, y)) * (0.9 + 0.2 * _mottle(x, y, z))
        return (k * (1.0 - low * 0.75), k * (1.0 - low * 0.85), k * (1.0 - low))
    grime(m, wear)
    return m


# ---------------------------------------------------------------------- the moving parts

def wheelset(name, r, seg=20):
    """An axle with its two wheels, origin on the axle."""
    m = Model(name)
    axle(m, 0.0, 0.0, r, seg)
    return m


def loco_driver():
    """A pair of driving wheels on their axle, quartered: the right pin at 205, the left at 115 degrees."""
    m = Model("rail_loco_driver")
    for sx in (-1, 1):
        driver(m, sx * GAUGE, 0.0, 0.0, DRIVER_R, math.radians(PIN_R if sx > 0 else PIN_L))
    mat(m, "iron", shade(Pal.Iron, 0.85))
    gcyl(m, (-0.8, 0, 0), (0.8, 0, 0), 0.07, 0.07, 10, 0)
    return m


def loco_siderod():
    """The coupling rod between the two crank pins of one side, origin halfway along it; it moves without turning."""
    m = Model("rail_loco_siderod")
    half = (DRIVER_Z[1] - DRIVER_Z[0]) * 0.5
    mat(m, "steel", (0.34, 0.35, 0.37), 0.02)
    m.loft([[L(0.0 + dx, dy, z) for dx, dy in ((-0.018, -0.045), (0.018, -0.045), (0.018, 0.045), (-0.018, 0.045))] for z in (-half, half)], True, 0, False)
    for z in (-half, half):
        gcyl(m, (-0.025, 0, z), (0.025, 0, z), 0.085, 0.085, 14, 0.006)          # the bosses round the pins
    mat(m, "steel", shade(Pal.Brass, 1.0), 0.02)
    for z in (-half, half):
        gcyl(m, (0.025, 0, z), (0.04, 0, z), 0.035, 0.035, 8, 0.003)
    return m


def loco_mainrod():
    """The main rod from the crank pin (origin) along +z to the crosshead, MAIN_ROD long, tapering."""
    m = Model("rail_loco_mainrod")
    mat(m, "steel", (0.34, 0.35, 0.37), 0.02)
    m.loft([[L(dx, dy * h, z) for dx, dy in ((-0.018, -1), (0.018, -1), (0.018, 1), (-0.018, 1))]
            for z, h in ((0.0, 0.05), (MAIN_ROD * 0.5, 0.042), (MAIN_ROD, 0.034))], True, 0, False)
    gcyl(m, (-0.03, 0, 0), (0.03, 0, 0), 0.1, 0.1, 14, 0.006)                    # big end
    gcyl(m, (-0.025, 0, MAIN_ROD), (0.025, 0, MAIN_ROD), 0.055, 0.055, 12, 0.005)  # little end
    return m


def loco_crosshead():
    """The crosshead between its slide bars and the piston rod into the cylinder, origin at the crosshead."""
    m = Model("rail_loco_crosshead")
    mat(m, "steel", (0.4, 0.41, 0.43), 0.02)
    gbox(m, (0, 0, 0), (0.09, 0.17, 0.22), 0.01, "z")
    gcyl(m, (0, 0, 0.05), (0, 0, 1.35), 0.026, 0.026, 8, 0)
    return m


def weather(m, fn):
    """Like grime(), but fn(x, y, z, n) also gets the face's normal (game axes) and returns (r, g, b, dust):
    the colour is multiplied by rgb, then drawn towards dust by the last value (the dyed paint keeps its part)."""
    m.bm.normal_update()
    col = m.col
    for f in m.bm.faces:
        n = f.normal
        gn = (-n.x, n.z, -n.y)
        for lp in f.loops:
            co = lp.vert.co
            r, g, b, t = fn(-co.x, co.z, -co.y, gn)
            c = lp[col]
            lp[col] = (c[0] * r * (1 - t) + DUST[0] * t, c[1] * g * (1 - t) + DUST[1] * t, c[2] * b * (1 - t) + DUST[2] * t, c[3])


def _streaks(u, y, top, seed):
    """Rain and rust run down a wall in thin streaks: 0..1 along u (metres along the wall), strongest under 'top'."""
    s = math.sin(u * 6.1 + seed) * math.sin(u * 2.3 + seed * 1.7) + 0.35 * math.sin(u * 17.0 + seed * 3.1)
    return max(0.0, s - 0.35) * 1.5 * _smooth(0.3, top, y) * (1.0 - _smooth(top, top + 0.3, y))


def car_wear(seed, soot=None, low_y=1.7):
    """The everyday wear of a car: dust and dirt thrown up from the track low down, streaks down the walls, a dusty
    roof, the paint patchy - and soot round a point (x, y, z, radius) if 'soot'."""
    def fn(x, y, z, n):
        up = max(0.0, n[1])
        side = 1.0 - abs(n[1])
        u = z if abs(n[0]) > abs(n[2]) else x
        low = 1.0 - _smooth(0.45, low_y, y)
        st = _streaks(u + (3.0 if x > 0 else 0.0), y, 3.6, seed) * side
        k = (0.86 + 0.24 * _mottle(x + seed, y, z)) * (1.0 - 0.22 * st)
        r, g, b = k * (1.0 - low * 0.28), k * (1.0 - low * 0.36), k * (1.0 - low * 0.46)
        if soot:
            sx, sy, sz, sr = soot
            d2 = (x - sx) ** 2 + (z - sz) ** 2 + 0.3 * (y - sy) ** 2
            s = math.exp(-d2 / (sr * sr)) * 0.6
            r, g, b = r * (1 - s), g * (1 - s), b * (1 - s * 0.95)
        dust = up * (0.12 + 0.18 * _mottle(z, x + seed, y)) + low * side * 0.12
        return (r, g, b, dust)
    return fn


def stirrup(m, x, z, top=1.0, bottom=0.55):
    """An iron step hung from the sill at a car's corner."""
    mat(m, "iron", shade(Pal.Iron, 0.8))
    m.tube([L(x, top, z - 0.16), L(x, bottom, z - 0.14), L(x, bottom, z + 0.14), L(x, top, z + 0.16)], 0.014, 5)
    gbox(m, (x, bottom + 0.01, z), (0.2, 0.02, 0.3), 0.003, "x")


def tender():
    """The engine's tender: a riveted water tank, its top flared, with a wood rack over the front two thirds heaped
    with split cordwood (thinned where the fireman has been at it); the water hatch at the back, its lid thrown
    open; tool box, brake wheel and an iron apron to the engine at the front, hoses down to it; ladder, tail lamp,
    corner steps. Worn: rust runs down the seams and from the hatch, dirt low down, dust on top. 5 m over the frame."""
    m = Model("rail_tender")
    underframe(m, 5.0)
    truck(m, -1.35, 0.42, 0.6)
    truck(m, 1.35, 0.42, 0.6)
    tank = hexcol(0x4B5560)
    mat(m, "plain", tank, 0.03)
    gbox(m, (0, 1.95, -0.05), (2.44, 1.7, 4.7), 0.03, "z")
    # lap seams up the sides and ends, the angle along the foot
    mat(m, "plain", shade(tank, 0.9), 0.02)
    for sx in (-1, 1):
        for z in (-1.62, -0.45, 0.72, 1.88):
            gbox(m, (sx * 1.226, 1.95, z), (0.012, 1.64, 0.05), 0.0, "y")
        gbox(m, (sx * 1.228, 1.16, -0.05), (0.016, 0.07, 4.7), 0.0, "z")
    for sz in (-1, 1):
        for x in (-0.6, 0.6):
            gbox(m, (x, 1.95, -0.05 + sz * 2.356), (0.05, 1.64, 0.012), 0.0, "y")
    mat(m, "plain", shade(BLACK, 0.9), 0.02)
    for sx in (-1, 1):                                                           # the flared collar round the top
        gbox(m, (sx * 1.26, 2.93, -0.05), (0.04, 0.34, 4.86), 0.006, "z", rot=(0, 0, -sx * 16.0))
    for sz in (-1, 1):
        gbox(m, (0, 2.93, -0.05 + sz * 2.39), (2.5, 0.34, 0.04), 0.006, "x", rot=(sz * 16.0, 0, 0))
    mat(m, "steel", shade(Pal.Brass, 0.95), 0.02)
    for sx in (-1, 1):                                                           # the lining round the side panel
        xo = sx * 1.234
        for y in (1.42, 2.52):
            gbox(m, (xo, y, -0.05), (0.006, 0.025, 3.9), 0.0, "z")
        for z in (-2.0, 1.9):
            gbox(m, (xo, 1.97, z), (0.006, 1.125, 0.025), 0.0, "y")
    # the wood rack: grey stakes and rails along the sides over the front two thirds, a board across behind
    mat(m, "planks", shade(Pal.WoodGrey, 0.78), 0.1)
    for sx in (-1, 1):
        for i in range(7):
            z = -1.05 + i * 0.56
            gbox(m, (sx * 1.19, 3.33, z), (0.07, 0.78, 0.08), 0.006, "y", rot=(0, 0, -sx * (3.0 + m.rng.uniform(-1.5, 1.5))))
        for y in (3.28, 3.66):
            gbox(m, (sx * 1.215, y, 0.63), (0.05, 0.09, 3.52), 0.006, "z")
    gbox(m, (0, 3.08, -1.12), (2.36, 0.56, 0.05), 0.006, "x")
    # the load: cordwood stacked crosswise in five courses, bark-round and split sticks, two to a row; the front
    # rows thinned; a few thrown on top anyhow
    rng = m.rng
    mat(m, "bark", shade(Pal.Bark, 0.7), 0.1)
    gbox(m, (0, 3.02, 0.55), (2.2, 0.42, 3.25), 0.0, "z")                       # the heart of the stack
    def stick(x0, x1, y, z, r):
        if rng.random() < 0.45:
            mat(m, "timber", shade(Pal.WoodNew, rng.uniform(0.8, 1.0)), 0.08)
            gcyl(m, (x0, y, z), (x1, y + rng.uniform(-0.02, 0.02), z + rng.uniform(-0.04, 0.04)), r * 1.15, r * 1.1, 3, 0.0)
        else:
            mat(m, "bark", shade(Pal.Bark, rng.uniform(0.85, 1.2)), 0.12)
            gcyl(m, (x0, y, z), (x1, y + rng.uniform(-0.02, 0.02), z + rng.uniform(-0.04, 0.04)), r, r * 0.95, 6, 0.0)
    for course in range(5):
        y = 2.9 + course * 0.165
        for i in range(19):
            z = -0.95 + i * 0.165 + (0.08 if course % 2 else 0.0) + rng.uniform(-0.02, 0.02)
            if z > 2.2 or (z > 1.35 and rng.random() < (0.25, 0.4, 0.6, 0.8, 0.95)[course]):
                continue
            r = rng.uniform(0.07, 0.088)
            for sx in (-1, 1):
                outer = sx * (1.13 + rng.uniform(-0.05, 0.03))
                if course < 2:                                                   # the ends showing in the rack
                    stick(outer, sx * 0.8, y, z, r)
                else:
                    stick(outer, sx * rng.uniform(0.0, 0.08), y, z, r)
    for i in range(5):
        a = rng.uniform(-0.6, 0.6)
        x, z, h = rng.uniform(-0.5, 0.5), rng.uniform(-0.6, 1.2), 3.64 + rng.uniform(0.0, 0.08)
        stick(x - 0.55 * math.cos(a), x + 0.55 * math.cos(a), h, z + 0.55 * math.sin(a), 0.08)
    # the back of the tank: the water hatch, its lid thrown open on its hinge; the tail lamp, the ladder
    mat(m, "plain", shade(BLACK, 1.1))
    gcyl(m, (0, 2.8, -1.9), (0, 2.92, -1.9), 0.32, 0.32, 16, 0.01)
    gcyl(m, (0, 3.17, -2.12), (0, 3.2, -2.21), 0.31, 0.31, 16, 0.006)          # the lid, standing up
    gbox(m, (0, 2.93, -2.22), (0.16, 0.05, 0.06), 0.006, "x")
    gbox(m, (0.86, 3.27, -2.4), (0.24, 0.3, 0.22), 0.012, "y")                 # the tail lamp on the collar
    gcyl(m, (0.86, 3.42, -2.4), (0.86, 3.53, -2.4), 0.04, 0.035, 8, 0.004)
    gbox(m, (0.86, 3.1, -2.36), (0.06, 0.12, 0.06), 0.004, "y")
    mat(m, "plain", hexcol(0x5A1E16), 0.0)
    gcyl(m, (0.86, 3.27, -2.51), (0.86, 3.27, -2.53), 0.085, 0.085, 12, 0.0)   # its red lens
    mat(m, "iron", shade(Pal.Iron, 0.85))
    for i in range(5):                                                           # rungs up the back
        m.tube([L(-0.25, 1.35 + i * 0.32, -2.42), L(-0.25, 1.35 + i * 0.32, -2.47), L(0.25, 1.35 + i * 0.32, -2.47), L(0.25, 1.35 + i * 0.32, -2.42)], 0.012, 5)
    for sx in (-1, 1):
        m.tube([L(sx * 1.24, 1.3, 2.2), L(sx * 1.29, 1.3, 2.2), L(sx * 1.29, 2.4, 2.2), L(sx * 1.24, 2.4, 2.2)], 0.013, 5)
    # the front: tool box, brake staff and wheel by the fireman, the apron over to the engine, the feed hoses
    mat(m, "plain", shade(BLACK, 1.1))
    gbox(m, (0, 1.35, 2.42), (1.3, 0.5, 0.36), 0.015, "x")
    mat(m, "iron", shade(Pal.Iron, 0.9))
    gbox(m, (0, 1.735, 2.6), (1.4, 0.025, 0.5), 0.004, "x")
    gcyl(m, (-0.95, 1.75, 2.4), (-0.95, 2.75, 2.4), 0.016, 0.016, 6, 0)
    hoop(m, (-0.95, 2.75, 2.4), 0.18, 0.013, 14)
    for a in (0.0, math.pi / 2):
        m.tube([L(-0.95 - 0.18 * math.cos(a), 2.75, 2.4 - 0.18 * math.sin(a)), L(-0.95 + 0.18 * math.cos(a), 2.75, 2.4 + 0.18 * math.sin(a))], 0.008, 4)
    mat(m, "leather", hexcol(0x2A2420), 0.04)
    for sx in (-1, 1):
        m.tube([L(sx * 0.55, 1.05, 2.45), L(sx * 0.56, 0.8, 2.66), L(sx * 0.52, 0.88, 2.92)], 0.035, 7)
    for sx in (-1, 1):
        for sz in (-1, 1):
            stirrup(m, sx * 1.12, sz * 2.25)
    # rust down the seams and from the hatch, dirt low down, dust on top
    base = car_wear(1.3, low_y=1.6)
    def wear(x, y, z, n):
        r, g, b, t = base(x, y, z, n)
        spill = math.exp(-((z + 1.9) ** 2) / 0.5) * _smooth(1.2, 2.8, y) * (0.3 + 0.2 * math.sin(x * 11.0))
        seam = sum(math.exp(-((z - s) ** 2) / 0.004) for s in (-1.62, -0.45, 0.72, 1.88)) * _smooth(1.1, 2.6, y) * 0.35
        rust = min(0.6, spill + seam) * (1.0 - abs(n[1]))
        return (r * (1 - rust * 0.3), g * (1 - rust * 0.5), b * (1 - rust * 0.65), t)
    weather(m, wear)
    return m


# ---------------------------------------------------------------------- cars

def boxcar():
    """An outside-braced wooden boxcar, 10 m over the frame: the posts and braces of its frame stand proud of the
    sheathing (a Howe truss each side of the door); sliding doors, the right-hand one run half open on a dark,
    empty car with a few crates by the door; boards replaced over the years in bare wood; an arched roof under tar
    with its running board, the brake wheel on the end. Worn: dirt low down, rust running from the iron, a dusty
    roof (the paint itself fades and flakes in the game's shader)."""
    m = Model("rail_boxcar")
    underframe(m, 10.0)
    truss_rods(m, 10.0)
    truck(m, -3.2)
    truck(m, 3.2)
    rng = m.rng
    hl, hw, y0, y1 = 4.7, 1.3, 1.1, 3.75
    dz0, dz1, dy1 = -1.1, 1.1, 3.42                                              # the door opening
    t = 0.05
    # the inside, seen through the open door: floor, dark lining, a few crates
    mat(m, "planks", shade(Pal.WoodDark, 0.6), 0.08)
    gbox(m, (0, y0 + 0.03, 0), (hw * 2 - 0.02, 0.06, hl * 2 - 0.02), 0.0, "z")
    mat(m, "planks", shade(Pal.WoodDark, 0.42), 0.06)
    gbox(m, (-hw + t + 0.012, (y0 + y1) * 0.5, 0), (0.02, y1 - y0, 3.2), 0.0, "y")
    gbox(m, (0, y1 - 0.1, 0), (hw * 2 - 0.04, 0.02, hl * 2 - 0.04), 0.0, "z")
    mat(m, "timber", shade(Pal.WoodOld, 0.8), 0.1)
    for (cx, cz, s) in ((-0.6, -0.5, 0.55), (-0.62, 0.2, 0.5), (-0.6, -0.15, 0.45)):
        gbox(m, (cx, y0 + 0.06 + s * 0.5 + (0.55 if cz == -0.15 else 0.0), cz), (s, s, s), 0.01, "y", rot=(0, rng.uniform(-8, 8), 0))
    # the sheathing: two side walls round the door, the ends; the left door's opening boarded behind its door
    paint(m, 1.0, "boards", 0.03)
    for sx in (-1, 1):
        x = sx * (hw - t * 0.5)
        for a, b in ((-hl, dz0), (dz1, hl)):
            gbox(m, (x, (y0 + y1) * 0.5, (a + b) * 0.5), (t, y1 - y0, b - a), 0.004, "y")
        gbox(m, (x, (dy1 + y1) * 0.5, 0), (t, y1 - dy1, dz1 - dz0), 0.004, "y")
        if sx < 0:
            gbox(m, (x, (y0 + dy1) * 0.5, 0), (t, dy1 - y0, dz1 - dz0), 0.004, "y")
    for sz in (-1, 1):
        gbox(m, (0, (y0 + y1) * 0.5, sz * (hl - t * 0.5)), (hw * 2, y1 - y0, t), 0.004, "y")
    # boards replaced over the years, bare and pale against the paint
    for sx in (-1, 1):
        for i in range(rng.randint(3, 5)):
            z = rng.choice((rng.uniform(-4.3, -1.4), rng.uniform(1.4, 4.3)))
            mat(m, "boards", shade(rng.choice((Pal.WoodGrey, Pal.WoodOld, Pal.WoodNew)), rng.uniform(0.8, 1.0)), 0.06)
            gbox(m, (sx * (hw + 0.004), (y0 + y1) * 0.5, z), (0.012, y1 - y0 - 0.2, rng.uniform(0.09, 0.13)), 0.0, "y")
    # the frame outside: posts, sill, plate and the braces of a Howe truss leaning in towards the door
    paint(m, 0.78, "boards", 0.03)
    posts = (1.16, 2.93, 4.66)
    for sx in (-1, 1):
        xo = sx * (hw + 0.02)
        gbox(m, (xo, y0 + 0.08, 0), (0.05, 0.16, hl * 2 + 0.06), 0.004, "z")
        gbox(m, (xo, y1 - 0.09, 0), (0.05, 0.18, hl * 2 + 0.06), 0.004, "z")
        for sz in (-1, 1):
            for p in posts:
                gbox(m, (xo, (y0 + y1) * 0.5, sz * p), (0.05, y1 - y0 - 0.3, 0.1), 0.004, "y")
            for a, b in ((posts[0], posts[1]), (posts[1], posts[2])):
                gbeam(m, (xo, y0 + 0.2, sz * (b - 0.08)), (xo, y1 - 0.2, sz * (a + 0.08)), 0.05, 0.11, 0.004)
    for sz in (-1, 1):
        zo = sz * (hl + 0.02)
        gbox(m, (0, y0 + 0.08, zo), (hw * 2 + 0.06, 0.16, 0.05), 0.004, "x")
        gbox(m, (0, y1 - 0.09, zo), (hw * 2 + 0.06, 0.18, 0.05), 0.004, "x")
        for px in (-1.27, 0.0, 1.27):
            gbox(m, (px, (y0 + y1) * 0.5, zo), (0.1, y1 - y0 - 0.3, 0.05), 0.004, "y")
        for s in (-1, 1):
            gbeam(m, (s * 1.18, y0 + 0.2, zo), (s * 0.1, y1 - 0.2, zo), 0.05, 0.11, 0.004)
    # the doors on their rails, outside the frame: the left shut, the right run back to show the dark inside
    for sx in (-1, 1):
        x = sx * (hw + 0.09)
        zc = 0.0 if sx < 0 else 1.05
        paint(m, 0.9, "boards", 0.04)
        gbox(m, (x, 2.33, zc), (0.045, 2.3, 2.26), 0.004, "y")
        paint(m, 0.72, "boards", 0.03)
        for y in (1.3, 2.33, 3.36):
            gbox(m, (x + sx * 0.03, y, zc), (0.02, 0.12, 2.26), 0.003, "z")
        gbeam(m, (x + sx * 0.03, 1.38, zc - 1.02), (x + sx * 0.03, 2.27, zc + 1.02), 0.02, 0.1, 0.003)
        gbeam(m, (x + sx * 0.03, 2.39, zc + 1.02), (x + sx * 0.03, 3.3, zc - 1.02), 0.02, 0.1, 0.003)
        mat(m, "iron", shade(Pal.Iron, 0.9))
        gbox(m, (x - sx * 0.035, 3.58, 1.15), (0.03, 0.05, 4.7), 0.004, "z")      # the rail the door runs on
        gbox(m, (x - sx * 0.035, 1.17, 1.15), (0.03, 0.04, 4.7), 0.004, "z")
        for z in (zc - 0.8, zc + 0.8):
            gcyl(m, (x, 3.58, z), (x + sx * 0.06, 3.58, z), 0.05, 0.05, 8, 0.004)
            gbox(m, (x + sx * 0.03, 3.45, z), (0.02, 0.2, 0.08), 0.003, "y")
        m.tube([L(x + sx * 0.03, 2.2, zc - 0.97), L(x + sx * 0.09, 2.2, zc - 0.97), L(x + sx * 0.09, 2.5, zc - 0.97), L(x + sx * 0.03, 2.5, zc - 0.97)], 0.012, 5)
        gbox(m, (x - sx * 0.02, 2.2, -1.22), (0.05, 0.14, 0.08), 0.004, "y")        # the hasp and its staple
        mat(m, "planks", shade(Pal.WoodNew, 0.95), 0.05)
        gbox(m, (sx * (hw + 0.035), 2.0, -1.45), (0.012, 0.32, 0.24), 0.002, "y")    # the waybill board
    # grab irons at the corners, the brake staff and wheel
    mat(m, "iron", shade(Pal.Iron, 0.9))
    for sx, sz in ((1, 1), (-1, -1)):
        for i in range(7):
            y = 1.3 + i * 0.36
            m.tube([L(sx * (hw + 0.045), y, sz * (hl - 0.55)), L(sx * (hw + 0.1), y, sz * (hl - 0.55)), L(sx * (hw + 0.1), y, sz * (hl - 0.15)), L(sx * (hw + 0.045), y, sz * (hl - 0.15))], 0.011, 5)
    gcyl(m, (0.55, 1.2, hl + 0.09), (0.55, 4.5, hl + 0.09), 0.016, 0.016, 6, 0)
    hoop(m, (0.55, 4.5, hl + 0.09), 0.2, 0.014, 14)
    for a in (0, math.pi / 3, 2 * math.pi / 3):
        m.tube([L(0.55 - 0.2 * math.cos(a), 4.5, hl + 0.09 - 0.2 * math.sin(a)), L(0.55 + 0.2 * math.cos(a), 4.5, hl + 0.09 + 0.2 * math.sin(a))], 0.008, 4)
    for sx in (-1, 1):
        for sz in (-1, 1):
            stirrup(m, sx * 1.2, sz * (hl - 0.3))
    mat(m, "canvas", hexcol(0x45413C), 0.03)                                    # tarred canvas
    arched_roof(m, 1.48, y1, y1 + 0.27, -hl - 0.14, hl + 0.14)
    paint(m, 0.7, "boards", 0.03)
    for sx in (-1, 1):                                                           # the fascia under the eaves
        gbox(m, (sx * 1.47, y1 - 0.05, 0), (0.03, 0.12, hl * 2 + 0.28), 0.003, "z")
    mat(m, "planks", shade(Pal.WoodGrey, 0.85), 0.07)
    for dx in (-0.2, 0.0, 0.2):
        gbox(m, (dx, y1 + 0.32, 0), (0.19, 0.03, hl * 2 + 0.4), 0.004, "z")
    for i in range(7):
        gbox(m, (0, y1 + 0.285, -4.2 + i * 1.4), (0.66, 0.05, 0.06), 0.004, "x")
    base = car_wear(4.1)
    def wear(x, y, z, n):
        r, g, b, t = base(x, y, z, n)
        under = (1.0 - _smooth(3.2, 3.55, y)) * _smooth(1.4, 3.3, y) * (1.0 - abs(n[1]))
        rust = under * max(0.0, math.sin(z * 9.0 + 1.3) * math.sin(z * 3.1)) * 0.5 * (1.0 if abs(z - 1.15) < 2.4 else 0.3)
        return (r * (1 - rust * 0.25), g * (1 - rust * 0.45), b * (1 - rust * 0.6), t)
    weather(m, wear)
    return m


def coach():
    """A wooden passenger coach, 10 m over the frame: six windows a side (0.9 square about y = 2.8, z = -3.5 +
    1.4 i - the game glazes them), blinds drawn in some of them, the road's name in gilt on the letter board,
    a clerestory roof whose ends sweep down over the platforms (duckbill hoods), the stove's smoke jack, open
    platforms with steps, railings and brake wheels at both ends. Worn: dirt low down, soot round the smoke jack,
    a dusty roof."""
    m = Model("rail_coach")
    underframe(m, 10.0, 2.44)
    truss_rods(m, 10.0)
    truck(m, -3.2)
    truck(m, 3.2)
    rng = m.rng
    hl, x, y0, y1 = 4.15, 1.27, 1.1, 3.6
    wz = [-3.5 + 1.4 * i for i in range(6)]
    mat(m, "plain", hexcol(0x231D18), 0.0)                                       # the dusk inside
    gbox(m, (0, 2.3, 0), (2.42, 2.3, hl * 2 - 0.1), 0.0, "z")
    paint(m, 1.0, "boards", 0.03)
    for sx in (-1, 1):
        gbox(m, (sx * x, 1.725, 0), (0.06, 1.25, hl * 2), 0.004, "y")
        gbox(m, (sx * x, 3.425, 0), (0.06, 0.35, hl * 2), 0.004, "z")
        edges = [-hl] + [e for z in wz for e in (z - 0.45, z + 0.45)] + [hl]
        for a, b in zip(edges[0::2], edges[1::2]):
            gbox(m, (sx * x, 2.8, (a + b) * 0.5), (0.06, 0.9, b - a), 0.003, "y")
    for sz in (-1, 1):
        gbox(m, (0, 2.35, sz * (hl - 0.03)), (2.6, 2.5, 0.06), 0.004, "y")
    mat(m, "timber", shade(Pal.WoodDark, 0.8), 0.05)                              # the seats, seen through the windows
    for z in wz:
        for sx in (-1, 1):
            for dz in (-0.42, 0.42):
                gbox(m, (sx * 0.72, 1.62, z + dz), (0.9, 0.08, 0.42), 0.006, "x")
                gbox(m, (sx * 0.72, 2.0, z + dz + (0.2 if dz > 0 else -0.2)), (0.9, 0.72, 0.06), 0.006, "x")
    mat(m, "cloth", hexcol(0xB9A77E), 0.06)                                      # blinds drawn down in some windows
    for sx in (-1, 1):
        for z in wz:
            if rng.random() < 0.4:
                h = rng.uniform(0.2, 0.75)
                gbox(m, (sx * (x - 0.045), 3.25 - h * 0.5, z), (0.012, h, 0.86), 0.0, "y")
    mat(m, "steel", shade(Pal.Brass, 0.9), 0.0)
    for z in (-2.1, 0.7, 3.5):                                                     # oil lamps under the clerestory
        m.lathe(L(0, 3.55, z), [(0.0, 0.0), (0.09, 0.0), (0.11, 0.08), (0.06, 0.2), (0.0, 0.22)], 10)
    paint(m, 0.7, "boards", 0.03)
    for sx in (-1, 1):
        xo = sx * (x + 0.036)
        gbox(m, (xo, 2.32, 0), (0.022, 0.07, hl * 2 + 0.02), 0.004, "z")         # belt rail
        gbox(m, (xo, 3.27, 0), (0.022, 0.05, hl * 2 + 0.02), 0.004, "z")
        gbox(m, (xo, 3.57, 0), (0.022, 0.06, hl * 2 + 0.02), 0.004, "z")
        gbox(m, (xo, 1.14, 0), (0.022, 0.08, hl * 2 + 0.02), 0.004, "z")
        for z in wz:
            for dz in (-0.47, 0.47):
                gbox(m, (xo, 2.8, z + dz), (0.022, 0.9, 0.045), 0.003, "y")
            gbox(m, (sx * (x - 0.005), 2.8, z), (0.03, 0.035, 0.9), 0.0, "z")    # the meeting rail of the sash
        for sz in (-1, 1):
            gbox(m, (xo, 2.35, sz * (hl - 0.03)), (0.022, 2.5, 0.08), 0.003, "y")
    paint(m, 0.92, "boards", 0.03)
    for sx in (-1, 1):                                                           # raised panels under the windows
        for z in wz:
            gbox(m, (sx * (x + 0.036), 1.73, z), (0.012, 0.82, 1.2), 0.002, "y")
    # the road's name in gilt along the letter board: dabs of gold, word-spaced
    mat(m, "steel", shade(Pal.WoodYellow, 0.95), 0.02)
    words = [[rng.uniform(0.11, 0.16) for k in range(n)] for n in (7, 3, 8)]
    total = sum(sum(w) for w in words) + 0.18 * (len(words) - 1)
    for sx in (-1, 1):
        z = -total * 0.5
        for word in words:
            for w in word:
                gbox(m, (sx * (x + 0.031), 3.42, z + w * 0.5), (0.006, 0.13, w * 0.8), 0.0, "y")
                z += w
            z += 0.18
    # end doors
    for sz in (-1, 1):
        z = sz * (hl + 0.012)
        paint(m, 0.82, "boards", 0.03)
        gbox(m, (0, 2.12, z), (0.78, 2.02, 0.03), 0.004, "y")
        paint(m, 0.62, "boards", 0.03)
        gbox(m, (0, 1.55, z + sz * 0.014), (0.56, 0.7, 0.012), 0.003, "y")
        mat(m, "plain", hexcol(0x33424A), 0.0)
        gbox(m, (0, 2.6, z + sz * 0.014), (0.5, 0.7, 0.012), 0.0, "y")           # the door's light
        mat(m, "steel", shade(Pal.Brass, 1.0), 0.0)
        gcyl(m, (0.3, 2.1, z), (0.3, 2.1, z + sz * 0.06), 0.02, 0.02, 6, 0.003)
    # platforms, steps, railings, the brake wheels
    for sz in (-1, 1):
        zc = sz * 4.57
        mat(m, "planks", shade(Pal.WoodGrey, 0.8), 0.06)
        gbox(m, (0, 1.115, zc), (2.44, 0.05, 0.84), 0.006, "x")
        mat(m, "iron", shade(Pal.Iron, 0.85))
        for sx in (-1, 1):
            for i, (yy, xx) in enumerate(((0.80, 1.2), (0.47, 1.32))):
                gbox(m, (sx * xx, yy, zc), (0.3, 0.035, 0.7), 0.005, "z")
            for dz in (-0.35, 0.35):
                gbeam(m, (sx * 1.15, 1.1, zc + dz), (sx * 1.45, 0.45, zc + dz), 0.02, 0.05, 0.004)
            # end railing either side of the gangway, and a handrail by the steps
            m.tube([L(sx * 1.15, 1.14, sz * 4.95), L(sx * 1.15, 1.98, sz * 4.95), L(sx * 0.42, 1.98, sz * 4.95), L(sx * 0.42, 1.14, sz * 4.95)], 0.016, 6)
            m.tube([L(sx * 1.15, 1.56, sz * 4.95), L(sx * 0.42, 1.56, sz * 4.95)], 0.012, 5)
            for k in range(1, 4):
                xx = sx * (0.42 + 0.73 * k / 4)
                m.tube([L(xx, 1.14, sz * 4.95), L(xx, 1.56, sz * 4.95)], 0.008, 4)
            m.tube([L(sx * 1.3, 1.5, sz * (hl + 0.05)), L(sx * 1.34, 1.5, sz * (hl + 0.05)), L(sx * 1.34, 2.5, sz * (hl + 0.05)), L(sx * 1.3, 2.5, sz * (hl + 0.05))], 0.012, 5)
        bx, bz = -0.8 * sz, sz * 4.84
        gcyl(m, (bx, 1.14, bz), (bx, 2.25, bz), 0.016, 0.016, 6, 0)
        hoop(m, (bx, 2.25, bz), 0.19, 0.013, 14)
        for a in (0.0, math.pi / 2):
            m.tube([L(bx - 0.19 * math.cos(a), 2.25, bz - 0.19 * math.sin(a)), L(bx + 0.19 * math.cos(a), 2.25, bz + 0.19 * math.sin(a))], 0.008, 4)
    # the roof: the lower deck and the clerestory over it, both sweeping down to hoods over the platforms
    mat(m, "canvas", hexcol(0x45413C), 0.03)                                    # tarred canvas
    sect = [(-1.46, 3.60), (-1.2, 3.72), (-0.9, 3.81), (-0.62, 3.86), (-0.62, 4.08), (-0.68, 4.09), (-0.35, 4.16), (0.0, 4.18),
            (0.35, 4.16), (0.68, 4.09), (0.62, 4.08), (0.62, 3.86), (0.9, 3.81), (1.2, 3.72), (1.46, 3.60), (1.46, 3.55), (-1.46, 3.55)]
    def ring(z, t, drop):
        out = []
        for px, py in sect:
            if py > 3.86:
                py = 3.86 + (py - 3.86) * (1.0 - t)
            out.append(L(px * (1.0 - 0.04 * t), py - drop, z))
        return out
    prof = ((-4.99, 1.0, 0.1), (-4.75, 0.82, 0.05), (-4.45, 0.42, 0.01), (-4.15, 0.0, 0.0),
            (4.15, 0.0, 0.0), (4.45, 0.42, 0.01), (4.75, 0.82, 0.05), (4.99, 1.0, 0.1))
    m.loft([ring(z, t, d) for z, t, d in prof], True, 0, True)
    mat(m, "plain", hexcol(0x33424A), 0.0)
    for sx in (-1, 1):
        for i in range(10):
            gbox(m, (sx * 0.625, 3.97, -3.6 + i * 0.8), (0.012, 0.13, 0.6), 0.0, "z")
    mat(m, "plain", shade(BLACK, 1.0))
    for z in (-2.1, 0.7):                                                         # ventilators
        m.lathe(L(0, 4.17, z), [(0.08, 0.0), (0.08, 0.1), (0.13, 0.12), (0.0, 0.16)], 8)
    gcyl(m, (0.98, 3.72, 3.55), (0.98, 4.42, 3.55), 0.07, 0.07, 10, 0.004)       # the stove's smoke jack and its cap
    m.lathe(L(0.98, 0, 3.55), [(0.0, 4.44), (0.17, 4.47), (0.12, 4.55), (0.0, 4.6)], 10)
    weather(m, car_wear(7.7, soot=(0.98, 4.2, 3.55, 0.9)))
    return m


def build():
    col = kit.collection("Rail")
    kit.clear_default_scene()
    makers = (track, buffer_stop, loco, tender, boxcar, coach, loco_driver, loco_siderod, loco_mainrod, loco_crosshead,
              lambda: wheelset("rail_wheelset", 0.42), lambda: wheelset("rail_wheelset_s", LEAD_R, 16))
    objs = [fn().finish(col, sharp_angle=35.0) for fn in makers]
    for i, ob in enumerate(objs):        # its own row in canaan_models.blend
        ob.location = (i * 7.0, -60.0, 0.0)
    return objs


if "ARGS" in globals() or __name__ == "__main__":
    args = globals().get("ARGS", {})
    objs = build()
    print(kit.stats(objs))
    if not args.get("noexport"):
        print("exported", kit.export_fbx("Rail", "rail"))
