"""The furnishing of rooms: wainscot, lamps, a stove and its pipe, a piano, the back-bar mirror, pictures, a clock,
a trophy, spittoons, a desk, a doctor's cabinet, notices, a rug, a counter scale.
    Tools/blender/bmcp.py run Tools/blender/interior.py
Collection "Interior" -> Assets/Canaan/Models/interior.fbx. Architect.Interior (C#) sets them in the open houses.

Game axes: x along the wall, y up, z out of it into the room (things hung on a wall have their back on z = 0);
things standing free have their origin on the floor at their middle. Colour alpha 12 = the house's trim colour,
13 = the room's own colour (the wainscot's paint, a rug's field), both dyed in the game. Lamp glass is not
modelled: the game puts a glowing chimney round each 'flame' mark (it shines at night).
"""
import importlib
import math

import kit

importlib.reload(kit)
from kit import L, Model, Pal, gbeam, gbox, gcyl, hexcol, shade

TRIM = 12 / 255.0
ROOM = 13 / 255.0
BRASS = shade(Pal.Brass, 1.05)
NICKEL = (0.62, 0.63, 0.64)
IRON = hexcol(0x2A2B2E)
WALNUT = hexcol(0x4A3022)
PARCHMENT = hexcol(0xE9DFC6)


def mat(m, surf, tint, jitter=0.03, part=0.0):
    m.wind = part
    m.surface(surf, tint, jitter)
    return m


def lathe(m, x, y, z, profile, seg=18):
    """Revolves (radius, height) about the vertical line through (x, y, z)."""
    m.lathe(L(x, y, z), list(profile), seg)


def chain(m, top, bottom, links=8, r=0.012):
    """A chain of alternating links hanging from 'top' to 'bottom' (game points)."""
    (x0, y0, z0), (x1, y1, z1) = top, bottom
    for i in range(links):
        t = (i + 0.5) / links
        x, y, z = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, z0 + (z1 - z0) * t
        h = abs(y1 - y0) / links * 0.62
        pts = []
        for k in range(10):
            a = 2 * math.pi * k / 10
            px, py = math.cos(a) * r, math.sin(a) * h
            pts.append(L(x + (px if i % 2 else 0.0), y + py, z + (0.0 if i % 2 else px)))
        m.tube(pts, 0.0035, 4, closed=True)


# ---------------------------------------------------------------------- walls

def wainscot():
    """A metre of wainscot, 1 m high: baseboard, beaded boards, chair rail. Stretched along x only."""
    m = Model("in_wainscot")
    mat(m, "boards", (1, 1, 1), 0.04, ROOM)
    gbox(m, (0, 0.55, 0.008), (1.0, 0.78, 0.016), 0.0, "y")
    for i in range(11):                                                          # beads between the boards
        gbox(m, (-0.5 + (i + 0.5) / 11.0, 0.55, 0.0175), (0.006, 0.78, 0.004), 0.0, "y")
    mat(m, "boards", (1, 1, 1), 0.03, TRIM)
    gbox(m, (0, 0.085, 0.013), (1.0, 0.17, 0.026), 0.004, "x")                  # baseboard
    gbox(m, (0, 0.17, 0.02), (1.0, 0.018, 0.04), 0.006, "x")
    gbox(m, (0, 0.955, 0.02), (1.0, 0.05, 0.04), 0.008, "x")                    # chair rail
    gbox(m, (0, 0.99, 0.03), (1.0, 0.025, 0.06), 0.01, "x")
    return m


# ---------------------------------------------------------------------- light

def lamp_font(m, x, y, z, s=1.0):
    """The brass font of an oil lamp with its burner; y is the bottom of the font."""
    mat(m, "steel", BRASS, 0.02)
    lathe(m, x, y, z, [(0.0, 0.0), (0.035 * s, 0.0), (0.075 * s, 0.035 * s), (0.07 * s, 0.075 * s), (0.03 * s, 0.1 * s),
                       (0.03 * s, 0.12 * s), (0.04 * s, 0.13 * s), (0.0, 0.135 * s)], 16)
    for k in range(4):                                                           # the gallery that holds the chimney
        a = k * math.pi / 2
        gbeam(m, (x + math.cos(a) * 0.03 * s, y + 0.13 * s, z + math.sin(a) * 0.03 * s),
              (x + math.cos(a) * 0.04 * s, y + 0.165 * s, z + math.sin(a) * 0.04 * s), 0.006, 0.006, 0.0)


def lamp_hang():
    """A hanging lamp: ceiling rose, chain, smoke bell, a ring carrying the font, an opal shade. Origin at the
    ceiling; mark 'flame'."""
    m = Model("in_lamp_hang")
    mat(m, "steel", BRASS, 0.02)
    lathe(m, 0, 0, 0, [(0.0, 0.0), (0.09, 0.0), (0.08, -0.025), (0.02, -0.04), (0.0, -0.045)], 16)
    chain(m, (0, -0.04, 0), (0, -0.46, 0), 7)
    lathe(m, 0, -0.62, 0, [(0.0, 0.16), (0.03, 0.16), (0.1, 0.11), (0.12, 0.085), (0.11, 0.08), (0.0, 0.08)], 18)      # smoke bell
    for k in range(3):                                                           # the frame down to the font ring
        a = k * 2 * math.pi / 3
        m.tube([L(math.cos(a) * 0.1, -0.54, math.sin(a) * 0.1), L(math.cos(a) * 0.17, -0.7, math.sin(a) * 0.17),
                L(math.cos(a) * 0.16, -0.86, math.sin(a) * 0.16)], 0.006, 5)
    m.tube([L(math.cos(k * math.pi / 8) * 0.16, -0.86, math.sin(k * math.pi / 8) * 0.16) for k in range(16)], 0.008, 5, closed=True)
    lamp_font(m, 0, -0.96, 0)
    mat(m, "plain", (0.95, 0.93, 0.86), 0.0)                                     # opal shade
    lathe(m, 0, -0.8, 0, [(0.07, 0.07), (0.1, 0.05), (0.2, -0.02), (0.21, -0.03), (0.19, -0.03), (0.08, 0.05), (0.06, 0.07)], 24)
    m.mark("flame", L(0, -0.76, 0))
    return m


def sconce():
    """A wall lamp: back plate, bracket, font in a ring, a polished reflector behind the flame. Mark 'flame'."""
    m = Model("in_sconce")
    mat(m, "steel", BRASS, 0.02)
    gbox(m, (0, 0.0, 0.005), (0.09, 0.22, 0.01), 0.004, "y")
    m.tube([L(0, 0.03, 0.01), L(0, 0.02, 0.1), L(0, -0.04, 0.17), L(0, -0.06, 0.2)], 0.008, 6)
    m.tube([L(math.cos(k * math.pi / 8) * 0.06, -0.06, 0.2 + math.sin(k * math.pi / 8) * 0.06) for k in range(16)], 0.006, 5, closed=True)
    lamp_font(m, 0, -0.1, 0.2, 0.8)
    mat(m, "steel", NICKEL, 0.0)
    m.push(L(0, 0.08, 0.03))
    m.lathe((0, 0, 0), [(0.0, 0.0), (0.1, 0.0), (0.11, 0.02), (0.0, 0.035)], 18, "y")      # reflector disc
    m.pop()
    m.mark("flame", L(0, 0.04, 0.2))
    return m


def wheel_lamp():
    """A wagon wheel hung flat from the ceiling on three chains, four lamps on its rim: the saloon's light.
    Origin at the ceiling; marks 'flame0'..'flame3'."""
    m = Model("in_wheel_lamp")
    R, y = 0.62, -1.05
    mat(m, "timber", shade(Pal.WoodDark, 0.9), 0.06)
    m.tube([L(math.cos(k * math.pi / 16) * R, y, math.sin(k * math.pi / 16) * R) for k in range(32)], 0.035, 8, closed=True)
    lathe(m, 0, y - 0.07, 0, [(0.0, 0.0), (0.09, 0.0), (0.1, 0.07), (0.09, 0.14), (0.0, 0.14)], 14)       # hub
    for k in range(12):
        a = 2 * math.pi * k / 12
        gbeam(m, (math.cos(a) * 0.09, y, math.sin(a) * 0.09), (math.cos(a) * (R - 0.02), y, math.sin(a) * (R - 0.02)), 0.025, 0.035, 0.004)
    mat(m, "iron", IRON, 0.02)
    m.tube([L(math.cos(k * math.pi / 16) * (R + 0.03), y, math.sin(k * math.pi / 16) * (R + 0.03)) for k in range(32)], 0.012, 6, closed=True)  # tyre
    gcyl(m, (0, -0.02, 0), (0, 0.0, 0), 0.08, 0.08, 12, 0.004)
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.3
        chain(m, (0, -0.03, 0), (math.cos(a) * R * 0.9, y + 0.03, math.sin(a) * R * 0.9), 9, 0.01)
    for k in range(4):
        a = 2 * math.pi * k / 4
        x, z = math.cos(a) * R, math.sin(a) * R
        lamp_font(m, x, y + 0.035, z, 0.85)
        m.mark("flame%d" % k, L(x, y + 0.2, z))
    return m


# ---------------------------------------------------------------------- the stove

def stove():
    """A pot-bellied stove on four legs, door to +z, origin on the floor; mark 'pipe' on the collar."""
    m = Model("in_stove")
    mat(m, "iron", IRON, 0.03)
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        m.tube([L(math.cos(a) * 0.17, 0.2, math.sin(a) * 0.17), L(math.cos(a) * 0.22, 0.09, math.sin(a) * 0.22),
                L(math.cos(a) * 0.25, 0.0, math.sin(a) * 0.25)], 0.022, 6)
    lathe(m, 0, 0.18, 0, [(0.0, 0.0), (0.26, 0.0), (0.27, 0.03), (0.22, 0.05), (0.2, 0.1), (0.23, 0.14), (0.0, 0.14)], 20)   # ash pit
    lathe(m, 0, 0.32, 0, [(0.0, 0.0), (0.22, 0.0), (0.29, 0.1), (0.3, 0.24), (0.27, 0.38), (0.21, 0.46), (0.2, 0.5), (0.0, 0.5)], 24)
    lathe(m, 0, 0.82, 0, [(0.0, 0.0), (0.24, 0.0), (0.25, 0.03), (0.2, 0.07), (0.12, 0.11), (0.1, 0.13), (0.09, 0.2), (0.0, 0.2)], 20)
    mat(m, "steel", NICKEL, 0.02)
    m.tube([L(math.cos(k * math.pi / 12) * 0.31, 0.56, math.sin(k * math.pi / 12) * 0.31) for k in range(24)], 0.012, 5, closed=True)  # foot rail
    lathe(m, 0, 0.83, 0, [(0.255, 0.0), (0.265, 0.01), (0.255, 0.03)], 20)
    lathe(m, 0, 1.0, 0, [(0.0, 0.0), (0.06, 0.0), (0.07, 0.04), (0.0, 0.05)], 12)        # finial on the lid
    mat(m, "iron", shade(IRON, 1.2), 0.02)
    gbox(m, (0, 0.55, 0.29), (0.2, 0.18, 0.03), 0.01, "x")                     # fire door
    for dx in (-0.06, 0.0, 0.06):
        gbox(m, (dx, 0.55, 0.307), (0.025, 0.1, 0.006), 0.002, "y")             # its draught slots
    mat(m, "steel", NICKEL, 0.02)
    gcyl(m, (0.09, 0.55, 0.3), (0.09, 0.55, 0.36), 0.012, 0.012, 6, 0)
    gbox(m, (0.09, 0.55, 0.36), (0.07, 0.02, 0.02), 0.004, "x")
    m.mark("pipe", L(0.0, 1.02, 0.0))
    return m


def pipe():
    """A metre of stovepipe going up (stretched along y), with a damper handle."""
    m = Model("in_pipe")
    mat(m, "iron", shade(IRON, 1.1), 0.03)
    gcyl(m, (0, 0, 0), (0, 1.0, 0), 0.075, 0.075, 14, 0)
    for y in (0.0, 0.5):
        gcyl(m, (0, y, 0), (0, y + 0.03, 0), 0.08, 0.08, 14, 0.003)
    mat(m, "steel", NICKEL, 0.0)
    gcyl(m, (0.075, 0.3, 0), (0.12, 0.3, 0), 0.008, 0.008, 6, 0)
    gbox(m, (0.12, 0.3, 0), (0.012, 0.012, 0.07), 0.002, "z")
    return m


# ---------------------------------------------------------------------- the saloon

def piano():
    """An upright piano, back on z = 0, keys towards +z; origin on the floor at the middle of its back."""
    m = Model("in_piano")
    W, H, D = 1.48, 1.3, 0.6
    case = shade(WALNUT, 1.0)
    mat(m, "timber", case, 0.04)
    gbox(m, (0, H * 0.5 + 0.06, 0.16), (W, H - 0.12, 0.32), 0.012, "y")         # the case
    gbox(m, (0, H + 0.01, 0.17), (W + 0.06, 0.04, 0.36), 0.01, "x")             # lid
    for sx in (-1, 1):
        gbox(m, (sx * (W * 0.5 - 0.03), 0.42, 0.42), (0.06, 0.84, 0.2), 0.01, "y")       # cheeks
        lathe(m, sx * (W * 0.5 - 0.06), 0.0, D - 0.07, [(0.03, 0.0), (0.035, 0.06), (0.022, 0.2), (0.04, 0.42), (0.025, 0.6), (0.03, 0.68)], 10)  # turned legs
    gbox(m, (0, 0.71, 0.45), (W - 0.12, 0.06, 0.3), 0.006, "x")                 # key bed
    gbox(m, (0, 0.06, 0.34), (W - 0.12, 0.12, 0.02), 0.006, "x")                # toe board
    gbox(m, (0, 0.95, 0.335), (W - 0.12, 0.3, 0.03), 0.006, "x")                # fallboard and music desk
    mat(m, "timber", shade(case, 1.25), 0.05)
    for sx in (-1, 0, 1):                                                        # the panels of the upper front
        gbox(m, (sx * 0.44, 1.12, 0.322), (0.36, 0.28, 0.008), 0.004, "y")
    gbox(m, (0, 0.38, 0.322), (0.9, 0.36, 0.008), 0.004, "x")
    mat(m, "plain", Pal.Bone, 0.02)
    gbox(m, (0, 0.75, 0.5), (W - 0.16, 0.022, 0.15), 0.002, "x")                # the white keys
    mat(m, "plain", hexcol(0x1C1A18), 0.0)
    for i in range(52):
        gbox(m, (-0.66 + i * 1.32 / 51, 0.762, 0.51), (0.0025, 0.003, 0.14), 0.0, "z")
    octave = (1, 1, 0, 1, 1, 1, 0)
    n = 0
    for i in range(51):
        if octave[i % 7]:
            gbox(m, (-0.66 + (i + 0.5) * 1.32 / 51, 0.772, 0.47), (0.014, 0.016, 0.09), 0.002, "z")
            n += 1
    mat(m, "steel", BRASS, 0.02)
    for sx in (-1, 1):                                                           # candle arms
        m.tube([L(sx * 0.58, 1.05, 0.33), L(sx * 0.64, 1.06, 0.42), L(sx * 0.66, 1.08, 0.46)], 0.008, 5)
        lathe(m, sx * 0.66, 1.08, 0.46, [(0.0, 0.0), (0.03, 0.0), (0.02, 0.02), (0.0, 0.025)], 10)
    for dx in (-0.08, 0.0, 0.08):                                                # pedals
        gbox(m, (dx, 0.05, 0.38), (0.035, 0.012, 0.09), 0.003, "z")
    return m


def piano_stool():
    m = Model("in_stool")
    mat(m, "timber", WALNUT, 0.04)
    lathe(m, 0, 0.0, 0, [(0.0, 0.0), (0.03, 0.0), (0.035, 0.12), (0.025, 0.3), (0.04, 0.42), (0.0, 0.43)], 12)
    for k in range(3):
        a = 2 * math.pi * k / 3
        m.tube([L(math.cos(a) * 0.03, 0.12, math.sin(a) * 0.03), L(math.cos(a) * 0.17, 0.03, math.sin(a) * 0.17),
                L(math.cos(a) * 0.2, 0.0, math.sin(a) * 0.2)], 0.014, 6)
    mat(m, "leather", Pal.ClothRed, 0.04)
    lathe(m, 0, 0.43, 0, [(0.0, 0.0), (0.17, 0.0), (0.18, 0.03), (0.15, 0.06), (0.0, 0.065)], 18)
    return m


def mirror():
    """The mirror behind a bar, 2.4 wide: turned columns, a cornice and pediment, the glass. Back on z = 0, origin at
    the middle of its foot."""
    m = Model("in_mirror")
    W, H = 2.4, 1.5
    mat(m, "timber", (1, 1, 1), 0.04, TRIM)
    gbox(m, (0, 0.04, 0.04), (W, 0.08, 0.08), 0.006, "x")
    gbox(m, (0, H - 0.08, 0.05), (W + 0.1, 0.16, 0.1), 0.01, "x")                # cornice
    gbox(m, (0, H + 0.02, 0.06), (W + 0.18, 0.04, 0.12), 0.006, "x")
    m.raw([L(-0.5, H + 0.04, 0.03), L(0.5, H + 0.04, 0.03), L(0.0, H + 0.3, 0.03)], [(0, 1, 2)], double=True)   # pediment
    for sx in (-1, 1):
        lathe(m, sx * (W * 0.5 - 0.06), 0.08, 0.07, [(0.05, 0.0), (0.04, 0.08), (0.03, 0.3), (0.04, 0.6), (0.03, 0.9), (0.04, 1.2), (0.05, H - 0.24)], 12)
    for sx in (-0.4, 0.4):                                                        # mullions: three lights
        gbox(m, (sx * (W - 0.24) * 0.5, H * 0.5, 0.025), (0.04, H - 0.2, 0.05), 0.004, "y")
    mat(m, "wet", (0.30, 0.33, 0.35), 0.0)
    gbox(m, (0, H * 0.5, 0.012), (W - 0.2, H - 0.2, 0.01), 0.0, "x")
    return m


def picture(name, landscape=True):
    """A framed picture, back on z = 0, centred on the origin: a mesa at sundown, or a portrait in an oval."""
    m = Model(name)
    if landscape:
        W, H, f = 0.95, 0.68, 0.07
        mat(m, "steel", shade(Pal.Brass, 0.9), 0.04)
        for sy in (-1, 1):
            gbox(m, (0, sy * (H - f) * 0.5, 0.02), (W, f, 0.04), 0.008, "x")
        for sx in (-1, 1):
            gbox(m, (sx * (W - f) * 0.5, 0, 0.02), (f, H - 2 * f, 0.04), 0.008, "y")
        bands = [(0.33, hexcol(0x9DB4C4)), (0.2, hexcol(0xD9B98A)), (0.08, hexcol(0xE6A35F))]
        y, w, h = 0.27, W - 2 * f, H - 2 * f
        for k, (hh, c) in enumerate(bands):                                     # the sky darkening to the light
            mat(m, "canvas", c, 0.02)
            gbox(m, (0, h * 0.5 - (k * 0.1 + 0.05) * h / 0.54 * 0.54, 0.008), (w, h * 0.2 + 0.001, 0.004), 0.0, "x")
        mat(m, "canvas", hexcol(0xA45338), 0.03)                                 # the mesa
        m.raw([L(-0.3, -0.06, 0.012), L(-0.22, 0.07, 0.012), L(0.12, 0.07, 0.012), L(0.2, -0.06, 0.012)], [(0, 3, 2, 1)], double=False)
        mat(m, "canvas", hexcol(0x8A6A3E), 0.03)                                 # the plain
        gbox(m, (0, -h * 0.3, 0.011), (w, h * 0.42, 0.004), 0.0, "x")
        mat(m, "canvas", hexcol(0x5C6B3A), 0.03)
        gbox(m, (-0.1, -h * 0.42, 0.013), (0.3, 0.05, 0.004), 0.0, "x")
    else:
        mat(m, "steel", shade(Pal.Brass, 0.85), 0.04)
        m.tube([L(math.cos(k * math.pi / 20) * 0.22, math.sin(k * math.pi / 20) * 0.29, 0.02) for k in range(40)], 0.03, 8, closed=True)
        mat(m, "canvas", hexcol(0x2A2420), 0.02)
        m.push(L(0, 0, 0.01))
        m.blob((0, 0, 0), (0.21, 0.01, 0.28), 1)
        m.pop()
        mat(m, "canvas", hexcol(0xB89A80), 0.02)                                 # face
        m.push(L(0, 0.07, 0.022))
        m.blob((0, 0, 0), (0.06, 0.006, 0.08), 1)
        m.pop()
        mat(m, "canvas", hexcol(0x3A3532), 0.02)                                 # coat and hair
        m.push(L(0, -0.16, 0.022))
        m.blob((0, 0, 0), (0.15, 0.006, 0.1), 1)
        m.pop()
        m.push(L(0, 0.13, 0.024))
        m.blob((0, 0, 0), (0.065, 0.005, 0.04), 1)
        m.pop()
    return m


def clock():
    """A regulator clock, back on z = 0, centred on the dial's height; the pendulum shows through the door."""
    m = Model("in_clock")
    mat(m, "timber", WALNUT, 0.05)
    gbox(m, (0, -0.3, 0.06), (0.4, 0.96, 0.12), 0.01, "y")
    gbox(m, (0, 0.2, 0.065), (0.46, 0.06, 0.13), 0.008, "x")
    m.raw([L(-0.18, 0.23, 0.13), L(0.18, 0.23, 0.13), L(0.0, 0.36, 0.13)], [(0, 1, 2)], double=True)
    gbox(m, (0, -0.8, 0.065), (0.44, 0.05, 0.13), 0.008, "x")
    mat(m, "plain", PARCHMENT, 0.0)
    m.push(L(0, 0.0, 0.122))
    m.lathe((0, 0, 0), [(0.0, 0.0), (0.15, 0.0), (0.15, 0.004), (0.0, 0.004)], 24, "y")
    m.pop()
    mat(m, "plain", hexcol(0x1C1A18), 0.0)
    for k in range(12):
        a = 2 * math.pi * k / 12
        gbox(m, (math.sin(a) * 0.125, math.cos(a) * 0.125, 0.127), (0.008, 0.02, 0.002), 0.0, "y", rot=(0, 0, -math.degrees(a)))
    gbeam(m, (0, 0, 0.13), (0.06, 0.05, 0.13), 0.008, 0.003, 0.0)
    gbeam(m, (0, 0, 0.131), (-0.03, -0.1, 0.131), 0.006, 0.003, 0.0)
    mat(m, "wet", (0.16, 0.15, 0.14), 0.0)
    gbox(m, (0, -0.42, 0.121), (0.3, 0.6, 0.004), 0.0, "y")                      # the glazed door
    mat(m, "steel", BRASS, 0.02)
    gbox(m, (0, -0.4, 0.122), (0.008, 0.46, 0.003), 0.0, "y")                    # pendulum
    m.push(L(0, -0.62, 0.124))
    m.lathe((0, 0, 0), [(0.0, 0.0), (0.06, 0.0), (0.06, 0.006), (0.0, 0.008)], 20, "y")
    m.pop()
    return m


def antlers():
    """A stag's antlers on a shield of wood, back on z = 0."""
    m = Model("in_antlers")
    mat(m, "timber", shade(WALNUT, 1.1), 0.05)
    pts = [(-0.15, 0.12), (0.15, 0.12), (0.17, 0.0), (0.12, -0.14), (0.0, -0.2), (-0.12, -0.14), (-0.17, 0.0)]
    rows = [[L(x * k, y * k, z) for x, y in pts] for k, z in ((1.0, 0.0), (1.0, 0.03), (0.9, 0.045))]
    m.loft(rows, True, 0, False)
    mat(m, "plain", Pal.Bone, 0.04)
    m.push(L(0, 0.0, 0.07))
    m.blob((0, 0, 0), (0.07, 0.05, 0.06), 1)
    m.pop()
    mat(m, "horn", hexcol(0xC9B48C), 0.04)
    for sx in (-1, 1):
        b0 = (sx * 0.05, 0.04, 0.09)
        b1 = (sx * 0.24, 0.22, 0.16)
        b2 = (sx * 0.34, 0.5, 0.12)
        b3 = (sx * 0.3, 0.68, 0.06)
        m.tube([L(*b0), L(*b1), L(*b2), L(*b3)], 0.02, 7, radii=[0.022, 0.018, 0.013, 0.006])
        for (a, tip) in ((b1, (sx * 0.18, 0.36, 0.25)), (b2, (sx * 0.22, 0.62, 0.2)), (((b1[0] + b2[0]) * 0.5, (b1[1] + b2[1]) * 0.5, 0.14), (sx * 0.44, 0.48, 0.18))):
            m.tube([L(*a), L(*tip)], 0.01, 6, radii=[0.012, 0.004])
    return m


def spittoon():
    m = Model("in_spittoon")
    mat(m, "steel", BRASS, 0.03)
    lathe(m, 0, 0, 0, [(0.0, 0.0), (0.1, 0.0), (0.14, 0.04), (0.15, 0.09), (0.12, 0.13), (0.07, 0.15), (0.1, 0.17), (0.11, 0.18),
                       (0.065, 0.165), (0.04, 0.12), (0.0, 0.11)], 22)
    return m


# ---------------------------------------------------------------------- offices

def desk():
    """A pedestal desk: drawers on the sitter's side (-z), a panelled front to the visitor (+z); origin on the floor."""
    m = Model("in_desk")
    W, H, D = 1.5, 0.78, 0.76
    mat(m, "timber", WALNUT, 0.05)
    for sx in (-1, 1):
        gbox(m, (sx * 0.5, H * 0.5 - 0.02, 0), (0.48, H - 0.04, D - 0.04), 0.008, "y")
    gbox(m, (0, H - 0.06, 0.32), (0.52, 0.12, 0.06), 0.006, "x")                 # modesty board
    gbox(m, (0, H - 0.015, 0), (W, 0.035, D), 0.01, "x")
    mat(m, "leather", hexcol(0x2E3A2C), 0.03)                                     # green leather on the top
    gbox(m, (0, H + 0.003, 0), (W - 0.2, 0.004, D - 0.18), 0.0, "x")
    mat(m, "timber", shade(WALNUT, 1.2), 0.05)
    for sx in (-1, 1):
        for i in range(3):
            y = 0.14 + i * 0.22
            gbox(m, (sx * 0.5, y, -D * 0.5 + 0.01), (0.42, 0.19, 0.02), 0.006, "x")
        gbox(m, (sx * 0.5, H * 0.45, D * 0.5 - 0.01), (0.4, H * 0.7, 0.02), 0.006, "y")
    mat(m, "steel", BRASS, 0.02)
    for sx in (-1, 1):
        for i in range(3):
            gbox(m, (sx * 0.5, 0.15 + i * 0.22, -D * 0.5 - 0.004), (0.09, 0.015, 0.02), 0.004, "x")
    return m


def cabinet():
    """A doctor's glass-fronted cabinet with bottles, back on z = 0, origin on the floor."""
    m = Model("in_cabinet")
    W, H, D = 0.95, 1.95, 0.38
    mat(m, "timber", (1, 1, 1), 0.04, TRIM)
    gbox(m, (0, H * 0.5, 0.01), (W, H, 0.02), 0.004, "y")
    for sx in (-1, 1):
        gbox(m, (sx * (W * 0.5 - 0.02), H * 0.5, D * 0.5), (0.04, H, D), 0.006, "y")
    for y in (0.04, 0.8, 1.2, 1.6, H - 0.03):
        gbox(m, (0, y, D * 0.5), (W, 0.04 if y in (0.04, H - 0.03) else 0.025, D), 0.004, "x")
    gbox(m, (0, 0.42, D - 0.012), (W - 0.06, 0.74, 0.024), 0.006, "y")            # cupboard below
    gbox(m, (0, H + 0.04, D * 0.5 + 0.02), (W + 0.08, 0.08, D + 0.06), 0.008, "x")
    for sx in (-1, 0, 1):                                                         # door frames, glazing left open
        gbox(m, (sx * (W * 0.5 - 0.05) if sx else 0.0, 1.38, D - 0.01), (0.04, 1.08, 0.02), 0.004, "y")
    for y in (0.84, 1.92):
        gbox(m, (0, y, D - 0.01), (W - 0.06, 0.04, 0.02), 0.004, "x")
    rng = m.rng
    colours = [hexcol(0x5A3A1E), hexcol(0x2F4A32), hexcol(0x2C3A55), hexcol(0xC9C1AE), hexcol(0x6A2A22)]
    for y0 in (0.83, 1.225, 1.62):
        x = -W * 0.5 + 0.1
        while x < W * 0.5 - 0.1:
            r = rng.uniform(0.022, 0.04)
            h = rng.uniform(0.1, 0.2)
            mat(m, "wet", colours[rng.randrange(len(colours))], 0.05)
            lathe(m, x + r, y0, D * 0.5 + rng.uniform(-0.05, 0.04), [(0.0, 0.0), (r, 0.0), (r, h * 0.7), (r * 0.45, h * 0.82), (r * 0.4, h), (0.0, h)], 10)
            x += r * 2 + rng.uniform(0.01, 0.04)
    return m


def poster():
    """A printed notice tacked to a wall, 0.42 x 0.58, back on z = 0, centred: a heading (the game letters it), a
    likeness, lines of print."""
    m = Model("in_poster")
    mat(m, "canvas", PARCHMENT, 0.06)
    gbox(m, (0, 0, 0.002), (0.42, 0.58, 0.004), 0.0, "x")
    mat(m, "plain", hexcol(0x2A241E), 0.0)
    gbox(m, (0, 0.05, 0.0045), (0.2, 0.16, 0.001), 0.0, "x")                      # the likeness
    mat(m, "plain", hexcol(0x6E655A), 0.0)
    for k in range(7):
        w = 0.32 if k % 3 else 0.24
        gbox(m, (0, -0.07 - k * 0.025, 0.0045), (w, 0.008, 0.001), 0.0, "x")
    gbox(m, (0, 0.2, 0.0045), (0.34, 0.004, 0.001), 0.0, "x")
    mat(m, "steel", NICKEL, 0.0)
    for sx in (-1, 1):
        for sy in (-1, 1):
            gcyl(m, (sx * 0.19, sy * 0.27, 0.004), (sx * 0.19, sy * 0.27, 0.009), 0.007, 0.007, 6, 0)
    return m


def rug():
    """A woven rug, 2.4 x 1.6, lying on the floor at its middle: a field (dyed 13), a border (dyed 12), fringes."""
    m = Model("in_rug")
    W, D, b = 2.4, 1.6, 0.18
    mat(m, "wool", (1, 1, 1), 0.04, ROOM)
    gbox(m, (0, 0.004, 0), (W - 2 * b, 0.008, D - 2 * b), 0.0, "x")
    mat(m, "wool", (1, 1, 1), 0.04, TRIM)
    for sz in (-1, 1):
        gbox(m, (0, 0.004, sz * (D - b) * 0.5), (W, 0.008, b), 0.0, "x")
    for sx in (-1, 1):
        gbox(m, (sx * (W - b) * 0.5, 0.004, 0), (b, 0.008, D - 2 * b), 0.0, "z")
    mat(m, "wool", Pal.ClothCream, 0.04)
    for sz in (-1, 1):                                                            # pinstripes
        gbox(m, (0, 0.0085, sz * (D * 0.5 - b + 0.02)), (W - 2 * b + 0.04, 0.001, 0.02), 0.0, "x")
    for sx in (-1, 1):
        gbox(m, (sx * (W * 0.5 - b + 0.02), 0.0085, 0), (0.02, 0.001, D - 2 * b + 0.04), 0.0, "z")
    m.push(L(0, 0.0086, 0))                                                       # a medallion in the middle
    m.lathe((0, 0, 0), [(0.0, 0.0), (0.28, 0.0), (0.28, 0.001), (0.0, 0.001)], 6, "z")
    m.pop()
    mat(m, "cloth", Pal.ClothCream, 0.05)
    for sx in (-1, 1):
        for k in range(30):
            z = -D * 0.5 + (k + 0.5) * D / 30
            gbox(m, (sx * (W * 0.5 + 0.04), 0.003, z), (0.08, 0.004, 0.008), 0.0, "x")
    return m


def scale():
    """A counter scale: a brass scoop on a beam over its iron base; origin on the counter."""
    m = Model("in_scale")
    mat(m, "iron", hexcol(0x3B3A3E), 0.03)
    gbox(m, (0, 0.03, 0), (0.34, 0.06, 0.2), 0.01, "x")
    gcyl(m, (0.1, 0.06, 0), (0.1, 0.2, 0), 0.02, 0.016, 8, 0)
    gbeam(m, (-0.12, 0.2, 0), (0.16, 0.2, 0), 0.02, 0.025, 0.003)
    mat(m, "steel", BRASS, 0.02)
    m.push(L(-0.08, 0.23, 0))
    m.lathe((0, 0, 0), [(0.0, 0.0), (0.05, 0.0), (0.12, 0.04), (0.125, 0.05), (0.06, 0.012), (0.0, 0.01)], 18)
    m.pop()
    gcyl(m, (0.13, 0.06, 0), (0.13, 0.11, 0), 0.03, 0.03, 10, 0.003)
    mat(m, "plain", PARCHMENT, 0.0)
    m.push(L(0.1, 0.13, 0.06))
    m.lathe((0, 0, 0), [(0.0, 0.0), (0.05, 0.0), (0.05, 0.004), (0.0, 0.004)], 16, "y")
    m.pop()
    return m


def build():
    col = kit.collection("Interior")
    kit.clear_default_scene()
    makers = (wainscot, lamp_hang, sconce, wheel_lamp, stove, pipe, piano, piano_stool, mirror,
              lambda: picture("in_painting", True), lambda: picture("in_portrait", False), clock, antlers, spittoon,
              desk, cabinet, poster, rug, scale)
    objs = []
    for fn in makers:
        md = fn()
        kit.bake_ao(md, rays=14, dist=0.12, strength=0.6, floor=0.55)
        objs.append(md.finish(col, sharp_angle=40.0))
    for i, ob in enumerate(objs):        # its own row in canaan_models.blend
        ob.location = (i * 3.0, -80.0, 0.0)
    return objs


if "ARGS" in globals() or __name__ == "__main__":
    args = globals().get("ARGS", {})
    objs = build()
    print(kit.stats(objs))
    if not args.get("noexport"):
        print("exported", kit.export_fbx("Interior", "interior"))
