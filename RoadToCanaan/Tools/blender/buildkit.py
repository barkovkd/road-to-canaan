"""Joinery for the buildings: casings, sashes, doors, porch posts, the cornice of a false front, the frame of a
sign, railings, a stovepipe, a wall lantern. Architect.cs still lays out walls and roofs from code and sets these
in place.
    Tools/blender/bmcp.py run Tools/blender/buildkit.py
Collection "BuildKit" -> Assets/Canaan/Models/buildkit.fbx.

Game axes: x along the wall, y up, z through it. Colour alpha 12 = the building's trim colour, 13 = its wall
colour (both dyed in the game). The casing of an opening comes in pieces - two stiles, a head, a sill - each
stretched along its length only, so boards keep their width on a window and on a barn door alike.
"""
import importlib
import math

import kit

importlib.reload(kit)
from kit import L, Model, Pal, gbeam, gbox, gcyl, hexcol, shade

TRIM = 12 / 255.0
WALL = 13 / 255.0
T = 0.18                      # wall thickness (Architect.Thick)
STILE = 0.12                  # width of a casing board
SASH_W, SASH_H = 1.25, 0.675  # the sash of the usual window (1.3 x 1.35 opening)


def trim(m, k=1.0, jitter=0.03):
    m.wind = TRIM
    m.surface("boards", (k, k, k), jitter)
    return m


def metal(m, tint=None, name="iron"):
    m.wind = 0.0
    m.surface(name, tint if tint else Pal.Iron, 0.0)
    return m


# ---------------------------------------------------------------------- the casing of an opening, in pieces

def stile():
    """One side of an opening, 2 m tall (stretched to the opening): the edge of the opening is x = 0, the boards
    lie on both faces of the wall towards -x, the jamb lining stands inside the opening. Turn it round for the
    other side."""
    m = Model("bk_stile")
    trim(m)
    for sz in (-1, 1):
        gbox(m, (-STILE / 2, 1.0, sz * (T / 2 + 0.014)), (STILE, 2.0, 0.028), 0.004, "y")
    gbox(m, (0.011, 1.0, 0), (0.022, 2.0, T), 0.0, "y")
    return m


def head():
    """The board over an opening, 1 m long (stretched to the opening and both stiles): y = 0 is the top of the opening."""
    m = Model("bk_head")
    trim(m)
    for sz in (-1, 1):
        gbox(m, (0, 0.08, sz * (T / 2 + 0.017)), (1.0, 0.16, 0.034), 0.004, "x")
        gbox(m, (0, 0.178, sz * (T / 2 + 0.032)), (1.0, 0.036, 0.075), 0.004, "x")           # drip cap
    gbox(m, (0, -0.011, 0), (1.0, 0.022, T - 0.006), 0.0, "x")                                # lining
    return m


def sill():
    """A window sill with its apron, 1 m long: y = 0 is the bottom of the opening."""
    m = Model("bk_sill")
    trim(m)
    gbox(m, (0, -0.022, 0), (1.0, 0.05, T + 0.16), 0.006, "x")                               # 3 mm proud of the wall below
    for sz in (-1, 1):
        gbox(m, (0, -0.095, sz * (T / 2 + 0.012)), (0.90, 0.10, 0.024), 0.004, "x")
    return m


def threshold():
    m = Model("bk_threshold")
    m.wind = 0.0
    m.surface("planks", shade(Pal.WoodGrey, 0.9), 0.04)
    gbox(m, (0, 0.012, 0), (1.0, 0.03, T + 0.10), 0.006, "x")
    return m


def sash():
    """One sash of a double-hung window, four lights: origin at the middle of its bottom edge, in its own plane."""
    m = Model("bk_sash")
    w, h, bar = SASH_W, SASH_H, 0.05
    trim(m, 0.92)
    gbox(m, (0, bar / 2, 0), (w, bar, 0.034), 0.0, "x")
    gbox(m, (0, h - bar / 2, 0), (w, bar, 0.034), 0.0, "x")
    for sx in (-1, 1):
        gbox(m, (sx * (w / 2 - bar / 2), h / 2, 0), (bar, h - 2 * bar + 0.002, 0.034), 0.0, "y")
    gbox(m, (0, h / 2, 0), (0.022, h - 2 * bar + 0.002, 0.026), 0.0, "y")
    gbox(m, (0, h / 2, 0), (w - 2 * bar + 0.002, 0.022, 0.026), 0.0, "x")
    return m


def window_arch():
    """A church window, whole: for an opening 0.9 x 2.3 whose sill is at y = 0. The opening in the wall is square;
    the boards over the springing fill its corners."""
    m = Model("bk_window_arch")
    w, h = 0.9, 2.3
    r = w / 2 - 0.03
    yc = h - r - 0.03                         # centre of the arch
    trim(m)
    for sz in (-1, 1):
        z = sz * (T / 2 + 0.014)
        for sx in (-1, 1):
            gbox(m, (sx * (w / 2 + STILE / 2), h / 2, z), (STILE, h + 0.01, 0.028), 0.004, "y")
        gbox(m, (0, h + 0.08, sz * (T / 2 + 0.017)), (w + STILE * 2 + 0.06, 0.16, 0.034), 0.004, "x")
        gbox(m, (0, h + 0.178, sz * (T / 2 + 0.032)), (w + STILE * 2 + 0.14, 0.036, 0.075), 0.004, "x")
        gbox(m, (0, -0.10, sz * (T / 2 + 0.012)), (w + 0.14, 0.10, 0.024), 0.004, "x")
    for sx in (-1, 1):
        gbox(m, (sx * (w / 2 - 0.011), h / 2, 0), (0.022, h, T), 0.0, "y")
    gbox(m, (0, h - 0.011, 0), (w, 0.022, T - 0.006), 0.0, "x")
    gbox(m, (0, -0.022, 0), (w + 0.26, 0.05, T + 0.16), 0.006, "x")
    # spandrels: flat boards between the square corner and the arch, on both faces of the sash plane
    n = 10
    arc = [(-r * math.cos(math.pi * i / (2 * n)), yc + r * math.sin(math.pi * i / (2 * n))) for i in range(n + 1)]
    trim(m, 0.97)
    for sx in (-1, 1):
        for sz in (-1, 1):
            z = sz * 0.03
            pts = [(sx * (w / 2 - 0.02), h - 0.02, z), (sx * (w / 2 - 0.02), yc, z)] + [(-sx * x, y, z) for x, y in arc] + [(0.0, h - 0.02, z)]
            verts = [L(*p) for p in pts]
            faces = [(0, i, i + 1) for i in range(1, len(pts) - 1)]
            if sx * sz < 0:                                   # as drawn the left one faces the street
                faces = [tuple(reversed(f)) for f in faces]
            m.raw(verts, faces, grain="x")
    # the arch moulding and the fixed light: stiles to the springing, a bar round the arch, glazing bars
    trim(m, 0.9)
    full = [(-r * math.cos(math.pi * i / (2 * n)), yc + r * math.sin(math.pi * i / (2 * n))) for i in range(2 * n + 1)]
    for (xa, ya), (xb, yb) in zip(full, full[1:]):
        gbeam(m, (xa, ya, 0), (xb, yb, 0), 0.075, 0.055, 0.0)
    for sx in (-1, 1):
        gbox(m, (sx * (r - 0.012), yc / 2, 0), (0.05, yc, 0.045), 0.0, "y")
    gbox(m, (0, 0.025, 0), (2 * r, 0.05, 0.045), 0.0, "x")
    trim(m, 0.88)
    gbox(m, (0, (yc + r) / 2, 0), (0.024, yc + r, 0.03), 0.0, "y")
    for y in (yc * 0.34, yc * 0.67, yc):
        gbox(m, (0, y, 0), (2 * r, 0.024, 0.03), 0.0, "x")
    for a in (math.pi * 0.25, math.pi * 0.75):                                                 # the fan in the head
        gbeam(m, (0, yc, 0), (r * math.cos(a), yc + r * math.sin(a), 0), 0.03, 0.022, 0.0)
    return m


# ---------------------------------------------------------------------- doors

def doorleaf():
    m = Model("bk_doorleaf")
    w, h, th = 1.2, 2.3, 0.05
    trim(m, 0.95)
    for sx in (-1, 1):
        gbox(m, (sx * (w / 2 - 0.07), h / 2, 0), (0.14, h, th), 0.004, "y")
    for y, rail in ((0.11, 0.22), (1.05, 0.16), (h - 0.08, 0.16)):
        gbox(m, (0, y, 0), (w - 0.28, rail, th), 0.004, "x")
    gbox(m, (0, 0.58, 0), (0.12, 0.72, th), 0.004, "y")
    gbox(m, (0, 1.675, 0), (0.12, 1.09, th), 0.004, "y")
    trim(m, 0.82)
    gbox(m, (0, h / 2, 0), (w - 0.26, h - 0.3, th * 0.45), 0.0, "y")            # the panels, set back
    metal(m, Pal.Brass, "steel")
    for sz in (-1, 1):
        gcyl(m, (w / 2 - 0.13, 1.02, sz * 0.025), (w / 2 - 0.13, 1.02, sz * 0.07), 0.012, 0.012, 8, 0.002)
        m.push(L(w / 2 - 0.13, 1.02, sz * 0.08))
        m.blob((0, 0, 0), (0.026, 0.02, 0.026), 1)
        m.pop()
    return m


def batwing():
    """One leaf of a saloon's swing doors: the hinge edge is x = 0, the leaf runs to +x; y = 0 is the floor."""
    m = Model("bk_batwing")
    x0, x1, y0, th = 0.02, 0.94, 0.48, 0.034
    top_h, top_m = 1.52, 1.27                 # height at the hinge and at the meeting edge
    trim(m)
    gbox(m, (x0 + 0.035, (y0 + top_h) / 2, 0), (0.07, top_h - y0, th), 0.004, "y")
    gbox(m, (x1 - 0.035, (y0 + top_m) / 2, 0), (0.07, top_m - y0, th), 0.004, "y")
    gbox(m, ((x0 + x1) / 2, y0 + 0.05, 0), (x1 - x0 - 0.14, 0.10, th), 0.004, "x")
    gbeam(m, (x0 + 0.06, top_h - 0.06, 0), (x1 - 0.06, top_m - 0.05, 0), th, 0.10)             # the top rail falls to the middle
    trim(m, 0.86)
    slope = (top_h - top_m - 0.01) / (x1 - x0 - 0.12)
    y = y0 + 0.135
    while y < top_h - 0.16:
        # a louvre stops where the falling top rail comes down to it
        xe = min(x1 - 0.07, x0 + 0.06 + (top_h - 0.13 - y) / slope)
        if xe - (x0 + 0.07) > 0.08:
            gbox(m, ((x0 + 0.07 + xe) / 2, y, 0), (xe - x0 - 0.07, 0.012, 0.05), 0.0, "x", rot=(28, 0, 0))   # degrees
        y += 0.072
    metal(m)
    for yh in (0.68, 1.34):
        gcyl(m, (0.0, yh - 0.05, 0), (0.0, yh + 0.05, 0), 0.016, 0.016, 8, 0.002)
    return m


def barndoor():
    """One leaf of a barn's double door, 2.2 x 3.3: the hinge edge is x = 0. Planks in the wall colour, ledges and
    braces in the trim colour, the same on both faces - an open leaf shows its back."""
    m = Model("bk_barndoor")
    w, h, th = 2.2, 3.3, 0.045
    n = 8
    pw = w / n
    m.wind = WALL
    for i in range(n):
        k = 0.88 + 0.12 * m.rng.random()
        m.surface("boards", (k, k, k), 0.03)
        gbox(m, ((i + 0.5) * pw, h / 2, 0), (pw - 0.008, h, th), 0.004, "y")
    trim(m)
    for sz in (-1, 1):
        z = sz * (th / 2 + 0.014)
        for y in (0.16, h / 2, h - 0.16):
            gbox(m, (w / 2, y, z), (w, 0.16, 0.028), 0.004, "x")
        gbeam(m, (0.08, 0.26, z), (w - 0.08, h / 2 - 0.10, z), 0.028, 0.15)
        gbeam(m, (0.08, h - 0.26, z), (w - 0.08, h / 2 + 0.10, z), 0.028, 0.15)
    metal(m)
    for sz in (-1, 1):
        for y in (0.16, h - 0.16):
            gbox(m, (0.30, y, sz * (th / 2 + 0.032)), (0.60, 0.05, 0.008), 0.002, "x")       # strap hinges
    for y in (0.16, h - 0.16):
        gcyl(m, (0.0, y - 0.07, 0), (0.0, y + 0.07, 0), 0.02, 0.02, 8, 0.002)
    return m


# ---------------------------------------------------------------------- porch, balcony

def post(name, braces):
    m = Model(name)
    trim(m)
    gbox(m, (0, 1.30, 0), (0.125, 2.60, 0.125), 0.016, "y")
    gbox(m, (0, 0.13, 0), (0.185, 0.26, 0.185), 0.012, "y")
    gbox(m, (0, 0.285, 0), (0.155, 0.05, 0.155), 0.008, "x")
    gbox(m, (0, 2.50, 0), (0.175, 0.07, 0.175), 0.008, "x")
    gbox(m, (0, 2.575, 0), (0.22, 0.05, 0.22), 0.008, "x")
    trim(m, 0.9)
    for sx in braces:                                                           # knee braces along the porch beam
        gbeam(m, (sx * 0.06, 2.02, 0), (sx * 0.50, 2.50, 0), 0.05, 0.07)
        gbox(m, (sx * 0.30, 2.565, 0), (0.50, 0.04, 0.07), 0.004, "x")
    return m


def railing():
    """A metre of balcony railing: origin under the middle of the bottom rail."""
    m = Model("bk_railing")
    trim(m)
    gbox(m, (0, 0.10, 0), (1.0, 0.05, 0.045), 0.004, "x")
    gbox(m, (0, 0.90, 0), (1.0, 0.05, 0.06), 0.004, "x")
    gbox(m, (0, 0.935, 0), (1.0, 0.022, 0.09), 0.004, "x")
    trim(m, 0.92)
    for i in range(7):
        gbox(m, ((i + 0.5) / 7 - 0.5, 0.50, 0), (0.032, 0.76, 0.032), 0.004, "y")
    return m


def newel():
    m = Model("bk_newel")
    trim(m)
    gbox(m, (0, 0.52, 0), (0.095, 1.04, 0.095), 0.008, "y")
    gbox(m, (0, 1.055, 0), (0.13, 0.03, 0.13), 0.006, "x")
    m.push(L(0, 1.07, 0))
    m.lathe((0, 0, 0), [(0.04, 0.0), (0.055, 0.03), (0.04, 0.07), (0.0, 0.085)], 10)
    m.pop()
    return m


# ---------------------------------------------------------------------- the top of the house

def cornice():
    m = Model("bk_cornice")
    trim(m)
    # one metre of the cap of a false front: frieze board, bed mould, dentils, crown and cap; z = 0 is the wall face
    gbox(m, (0, -0.16, -0.012), (1.0, 0.32, 0.024), 0.0, "x")
    gbox(m, (0, 0.03, -0.045), (1.0, 0.06, 0.09), 0.0, "x")
    gbox(m, (0, 0.10, -0.10), (1.0, 0.08, 0.20), 0.0, "x")
    gbox(m, (0, 0.165, -0.14), (1.0, 0.05, 0.28), 0.0, "x")
    gbox(m, (0, 0.20, 0.03), (1.0, 0.04, 0.40), 0.0, "x")
    trim(m, 0.88)
    for i in range(4):
        gbox(m, (-0.375 + i * 0.25, -0.045, -0.07), (0.11, 0.09, 0.09), 0.006, "y")
    return m


def sign_rail():
    m = Model("bk_sign_rail")
    trim(m)
    gbox(m, (0, 0, -0.015), (1.0, 0.07, 0.05), 0.0, "x")
    gbox(m, (0, 0, -0.05), (1.0, 0.035, 0.03), 0.0, "x")
    return m


def sign_end():
    m = Model("bk_sign_end")
    trim(m)
    gbox(m, (0, 0, -0.02), (0.09, 1.16, 0.06), 0.006, "y")
    for sy in (-1, 1):
        gcyl(m, (0, sy * 0.60, -0.055), (0, sy * 0.60, 0.01), 0.06, 0.06, 12, 0.006)
    return m


def stovepipe():
    m = Model("bk_stovepipe")
    m.wind = 0.0
    m.surface("tin", shade(Pal.Tin, 0.55), 0.05)
    m.lathe(L(0, 0, 0), [(0.20, 0.0), (0.12, 0.10), (0.092, 0.12), (0.092, 1.25), (0.11, 1.26), (0.11, 1.30), (0.0, 1.30)], 14)
    m.lathe(L(0, 1.36, 0), [(0.0, 0.14), (0.17, 0.02), (0.18, 0.0), (0.0, 0.0)], 14)
    for a in (0.0, 2.1, 4.2):
        gcyl(m, (0.09 * math.cos(a), 1.28, 0.09 * math.sin(a)), (0.11 * math.cos(a), 1.38, 0.11 * math.sin(a)), 0.006, 0.006, 5, 0)
    return m


def lantern():
    """A lantern on a bracket by the door: the origin is on the wall, the bracket reaches out to -z. The game puts
    the glowing glass (0.085 x 0.13 x 0.085) round the mark 'flame'."""
    m = Model("bk_lantern")
    metal(m, shade(Pal.Iron, 0.9))
    gbox(m, (0, 0, -0.006), (0.07, 0.20, 0.012), 0.003, "y")                                  # wall plate
    gbeam(m, (0, 0.06, -0.01), (0, 0.10, -0.26), 0.016, 0.016, 0.002)                          # arm
    gbeam(m, (0, -0.08, -0.01), (0, 0.07, -0.17), 0.012, 0.012, 0.002)                         # stay
    cz, cy = -0.24, -0.06                                                                      # centre of the glass
    gcyl(m, (0, 0.10, cz), (0, cy + 0.14, cz), 0.006, 0.006, 6, 0)                             # hook
    m.lathe(L(0, cy + 0.075, cz), [(0.0, 0.0), (0.075, 0.0), (0.06, 0.012), (0.03, 0.05), (0.016, 0.06), (0.016, 0.075), (0.0, 0.075)], 8)
    gbox(m, (0, cy - 0.075, cz), (0.11, 0.016, 0.11), 0.003, "x")
    gbox(m, (0, cy - 0.095, cz), (0.07, 0.03, 0.07), 0.004, "x")                               # the oil font
    for sx in (-1, 1):
        for sz in (-1, 1):
            gbox(m, (sx * 0.048, cy, cz + sz * 0.048), (0.008, 0.15, 0.008), 0.0, "y")
    m.mark("flame", L(0, cy, cz))
    return m


def build():
    col = kit.collection("BuildKit")
    kit.clear_default_scene()
    makers = (stile, head, sill, threshold, sash, window_arch, doorleaf, batwing, barndoor,
              lambda: post("bk_post", (-1, 1)), lambda: post("bk_post_end", (1,)), lambda: post("bk_post_plain", ()),
              railing, newel, cornice, sign_rail, sign_end, stovepipe, lantern)
    objs = [fn().finish(col, sharp_angle=35.0) for fn in makers]
    kit.layout(objs, 2.6, 7)
    for ob in objs:                      # its own rows in canaan_models.blend, clear of the other collections
        ob.location.y -= 30.0
    return objs


if "ARGS" in globals() or __name__ == "__main__":
    args = globals().get("ARGS", {})
    objs = build()
    print(kit.stats(objs))
    if not args.get("noexport"):
        print("exported", kit.export_fbx("BuildKit", "buildkit"))
