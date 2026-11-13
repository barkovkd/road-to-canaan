"""The horse: one skinned body on the game's quadruped skeleton (QuadBuilder.cs, default proportions).
    Tools/blender/bmcp.py run Tools/blender/horse.py
Collection "Horse" -> Assets/Canaan/Models/horse.fbx.

Game axes (x right, y up, z forward; the horse faces +z). The colour alpha of a vertex names the part the game
dyes per horse: 1 coat, 2 mane and tail, 3 lower legs, 4 muzzle, 5 hooves, 6 rump, 7 blaze (white or coat),
8 socks (white or the leg colour), 10 and 11 the coat a third and two thirds of the way to the leg colour,
12 the coat halfway to the muzzle; 0 keeps its colour. The rgb is a shade: the folds and the underside are
baked darker (kit.bake_ao).

The body is built from sections (kit.profile_ring) along the spine, the neck and the head, the legs from
horizontal sections down to the hoof; mane, forelock and tail are strands of hair. tack.py fits the bridle to
head_point() and head_ring().
"""
import importlib
import math
import random

import kit

importlib.reload(kit)
from kit import L, Skinned, bake_ao, profile_ring, ribbon, sheet
from mathutils import Vector

COAT, MANE, LEGS, MUZZLE, HOOF, RUMP, BLAZE, SOCKS = 1, 2, 3, 4, 5, 6, 7, 8
LEG1, LEG2, MUZ1 = 10, 11, 12
WHITE = (1.0, 1.0, 1.0)
QB = ["Pelvis", "Spine", "Chest", "Neck", "Head", "Tail1", "Tail2", "FLa", "FLb", "FLc", "FRa", "FRb", "FRc",
      "HLa", "HLb", "HLc", "HRa", "HRb", "HRc"]
QB_PARENT = [-1, 0, 1, 2, 3, 0, 5, 2, 7, 8, 2, 10, 11, 0, 13, 14, 0, 16, 17]


def V(*p):
    return Vector(p)


X, Y, Z = V(1, 0, 0), V(0, 1, 0), V(0, 0, 1)
NECK_BASE = V(0, 1.38, 0.56)
NECK_DIR = V(0, math.sin(math.radians(53)), math.cos(math.radians(53)))
NECK_LEN = 0.74
POLL = NECK_BASE + NECK_DIR * NECK_LEN
HEAD_DIR = V(0, -math.sin(math.radians(42)), math.cos(math.radians(42)))
HEAD_LEN = 0.54
HEAD_UP = V(0, HEAD_DIR.z, -HEAD_DIR.y)                    # perpendicular to the head, pointing up and forward
NECK_UP = V(0, NECK_DIR.z, -NECK_DIR.y)                    # perpendicular to the neck: towards the crest
TAIL_HEAD = V(0, 1.38, -0.80)
TAIL_DIR = V(0, -math.cos(math.radians(12)), -math.sin(math.radians(12)))
TAIL_LEN = 0.85

LEGS_J = {}
for _name, _sx, _front in (("FL", -1, True), ("FR", 1, True), ("HL", -1, False), ("HR", 1, False)):
    if _front:
        LEGS_J[_name] = [V(_sx * 0.19, 1.20, 0.46), V(_sx * 0.20, 0.95, 0.38), V(_sx * 0.20, 0.53, 0.41), V(_sx * 0.20, 0.0, 0.43)]
    else:
        LEGS_J[_name] = [V(_sx * 0.19, 1.24, -0.54), V(_sx * 0.205, 0.90, -0.40), V(_sx * 0.205, 0.56, -0.65), V(_sx * 0.205, 0.0, -0.60)]


def joints():
    p = {"Pelvis": (0, 1.28, -0.52), "Spine": (0, 1.30, -0.12), "Chest": (0, 1.30, 0.32), "Neck": tuple(NECK_BASE), "Head": tuple(POLL),
         "Tail1": tuple(TAIL_HEAD), "Tail2": tuple(TAIL_HEAD + TAIL_DIR * TAIL_LEN * 0.5)}
    for leg, js in LEGS_J.items():
        for seg, s in enumerate("abc"):
            p[leg + s] = tuple(js[seg])
    return p


def sm(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def game(co):
    return V(-co.x, co.z, -co.y)


def B(p):
    """A game-axes vector as a Blender point."""
    return L(p.x, p.y, p.z)


def interp(table, key):
    """Rows of numbers sorted by their first column: the rest of the row at 'key', linearly between rows."""
    if key <= table[0][0]:
        return table[0][1:]
    for a, b in zip(table, table[1:]):
        if key <= b[0]:
            t = (key - a[0]) / (b[0] - a[0])
            return tuple(a[i] + (b[i] - a[i]) * t for i in range(1, len(a)))
    return table[-1][1:]


def surface_point(c, u, v, up, down, width, p_up, p_down, tt, tb, a, lift=0.0):
    """One point of kit.profile_ring, at angle a (0 on +u, pi/2 on +v), in game axes."""
    cs, sn = math.cos(a), math.sin(a)
    p = p_up if sn >= 0 else p_down
    xn = math.copysign(abs(cs) ** (2.0 / p), cs)
    yn = math.copysign(abs(sn) ** (2.0 / p), sn)
    w = width * (1.0 - (tt if yn > 0 else tb) * yn * yn)
    d = u * (xn * w) + v * (yn * (up if yn > 0 else down))
    q = c + d
    if lift and d.length > 1e-6:
        q = q + d.normalized() * lift
    return q


# ------------------------------------------------------------------ the shape: body, neck and head sections
#   z,      top,   bottom, widest at, half width, p over, p under, taper over, taper under
BODY = [
    (-0.885, 1.335, 1.115, 0.55, 0.050, 2.0, 2.0, 0.2, 0.2),
    (-0.865, 1.372, 1.045, 0.55, 0.115, 2.0, 2.0, 0.2, 0.25),
    (-0.825, 1.410, 0.975, 0.55, 0.190, 2.1, 2.0, 0.2, 0.3),
    (-0.765, 1.448, 0.925, 0.55, 0.252, 2.3, 2.0, 0.2, 0.3),
    (-0.685, 1.480, 0.890, 0.55, 0.292, 2.4, 2.0, 0.18, 0.25),
    (-0.585, 1.502, 0.862, 0.55, 0.308, 2.5, 2.0, 0.18, 0.22),
    (-0.485, 1.510, 0.842, 0.54, 0.310, 2.5, 2.0, 0.18, 0.2),
    (-0.385, 1.508, 0.822, 0.52, 0.298, 2.3, 2.05, 0.15, 0.18),
    (-0.275, 1.503, 0.802, 0.50, 0.290, 2.1, 2.1, 0.12, 0.18),
    (-0.155, 1.495, 0.787, 0.48, 0.288, 2.0, 2.15, 0.1, 0.18),
    (-0.035, 1.490, 0.780, 0.48, 0.290, 2.0, 2.15, 0.1, 0.18),
    (0.085, 1.493, 0.785, 0.48, 0.291, 2.0, 2.1, 0.1, 0.2),
    (0.195, 1.505, 0.795, 0.50, 0.289, 1.9, 2.0, 0.12, 0.25),
    (0.295, 1.525, 0.812, 0.52, 0.278, 1.7, 1.9, 0.15, 0.3),
    (0.385, 1.543, 0.832, 0.55, 0.262, 1.45, 1.8, 0.2, 0.35),
    (0.465, 1.548, 0.862, 0.55, 0.245, 1.35, 1.7, 0.25, 0.4),
    (0.545, 1.518, 0.902, 0.55, 0.225, 1.4, 1.7, 0.25, 0.45),
    (0.615, 1.458, 0.950, 0.55, 0.200, 1.6, 1.7, 0.25, 0.5),
    (0.675, 1.380, 1.000, 0.55, 0.165, 1.8, 1.8, 0.25, 0.5),
    (0.725, 1.300, 1.060, 0.55, 0.112, 2.0, 2.0, 0.25, 0.4),
    (0.758, 1.225, 1.120, 0.55, 0.045, 2.0, 2.0, 0.25, 0.3),
]


def body_ring(z, n=22):
    top, bot, wy, hw, pu, pd, tt, tb = interp(BODY, z)
    ym = bot + (top - bot) * wy
    return profile_ring(V(0, ym, z), X, Y, top - ym, ym - bot, hw, pu, pd, tt, tb, n)


#   t,     crest, throat, half width
NECK = [
    (-0.30, 0.25, 0.20, 0.190), (-0.12, 0.25, 0.23, 0.185), (0.00, 0.24, 0.25, 0.170), (0.20, 0.205, 0.20, 0.145),
    (0.40, 0.18, 0.165, 0.125), (0.60, 0.165, 0.135, 0.110), (0.80, 0.145, 0.11, 0.095), (0.92, 0.125, 0.10, 0.088),
    (1.00, 0.10, 0.095, 0.082), (1.06, 0.07, 0.085, 0.070),
]


def neck_c(t):
    return NECK_BASE + NECK_DIR * (NECK_LEN * t) - Y * (0.10 * max(0.0, 1.0 - t) ** 2)


def neck_point(t, a, lift=0.0):
    crest, throat, hw = interp(NECK, t)
    return surface_point(neck_c(t), X, NECK_UP, crest, throat, hw, 1.7, 1.8, 0.5, 0.5, a, lift)


def neck_ring(t, n=18):
    crest, throat, hw = interp(NECK, t)
    return profile_ring(neck_c(t), X, NECK_UP, crest, throat, hw, 1.7, 1.8, 0.5, 0.5, n)


#   t,     over, under, half width, taper over, taper under
HEAD = [
    (-0.14, 0.080, 0.100, 0.074, 0.30, 0.30), (-0.04, 0.105, 0.155, 0.083, 0.35, 0.30), (0.07, 0.117, 0.212, 0.095, 0.30, 0.16),
    (0.19, 0.120, 0.245, 0.104, 0.28, 0.08), (0.31, 0.116, 0.242, 0.102, 0.30, 0.10), (0.42, 0.108, 0.198, 0.088, 0.30, 0.18),
    (0.53, 0.098, 0.150, 0.072, 0.25, 0.22), (0.64, 0.090, 0.120, 0.062, 0.25, 0.22), (0.75, 0.086, 0.108, 0.062, 0.20, 0.20),
    (0.85, 0.082, 0.104, 0.068, 0.20, 0.18), (0.93, 0.074, 0.100, 0.068, 0.20, 0.15), (0.99, 0.058, 0.094, 0.060, 0.20, 0.20),
    (1.04, 0.036, 0.074, 0.045, 0.20, 0.25), (1.075, 0.012, 0.042, 0.022, 0.20, 0.25),
]


def head_c(t):
    return POLL + HEAD_DIR * (HEAD_LEN * t)


def head_point(t, a, lift=0.0):
    """A point on the head at section t (0 the poll, 1 the muzzle) and angle a (0 the right cheek, pi/2 the top)."""
    up, down, hw, tt, tb = interp(HEAD, t)
    return surface_point(head_c(t), X, HEAD_UP, up, down, hw, 2.0, 2.0, tt, tb, a, lift)


def head_ring(t, n=20, lift=0.0):
    up, down, hw, tt, tb = interp(HEAD, t)
    return profile_ring(head_c(t), X, HEAD_UP, up, down, hw, 2.0, 2.0, tt, tb, n, lift)


def head_t(p):
    return (p - POLL).dot(HEAD_DIR) / HEAD_LEN


# legs: horizontal sections. y, forward shift of the axis, front, back, half width, taper front, taper back
FRONT = [
    (1.25, 0.000, 0.056, 0.074, 0.040, 0.2, 0.30), (1.18, 0.000, 0.068, 0.094, 0.054, 0.2, 0.30), (1.115, 0.000, 0.076, 0.106, 0.064, 0.2, 0.30),
    (1.06, 0.000, 0.080, 0.110, 0.072, 0.2, 0.30), (0.98, 0.000, 0.074, 0.118, 0.072, 0.2, 0.35), (0.92, 0.000, 0.068, 0.110, 0.069, 0.2, 0.40),
    (0.85, 0.005, 0.066, 0.076, 0.066, 0.25, 0.40), (0.77, 0.008, 0.060, 0.058, 0.060, 0.25, 0.45), (0.69, 0.008, 0.052, 0.048, 0.052, 0.3, 0.50),
    (0.62, 0.006, 0.045, 0.042, 0.046, 0.3, 0.50), (0.565, 0.004, 0.042, 0.042, 0.045, 0.4, 0.40), (0.53, 0.004, 0.046, 0.046, 0.050, 0.5, 0.40),
    (0.49, 0.002, 0.040, 0.046, 0.045, 0.5, 0.60), (0.44, 0.000, 0.032, 0.046, 0.038, 0.5, 0.75), (0.34, 0.000, 0.030, 0.045, 0.036, 0.5, 0.75),
    (0.25, -0.002, 0.031, 0.048, 0.037, 0.5, 0.70), (0.20, -0.004, 0.034, 0.056, 0.042, 0.5, 0.55), (0.165, -0.002, 0.035, 0.058, 0.045, 0.5, 0.50),
    (0.13, 0.012, 0.032, 0.042, 0.040, 0.4, 0.45), (0.10, 0.026, 0.034, 0.038, 0.042, 0.4, 0.40), (0.078, 0.034, 0.040, 0.040, 0.047, 0.4, 0.30),
]
HIND = [
    (1.20, 0.000, 0.100, 0.130, 0.070, 0.2, 0.25), (1.14, 0.000, 0.116, 0.152, 0.086, 0.2, 0.25),
    (1.08, 0.000, 0.125, 0.165, 0.098, 0.2, 0.25), (1.00, 0.000, 0.122, 0.160, 0.094, 0.2, 0.30), (0.93, 0.000, 0.108, 0.148, 0.088, 0.25, 0.35),
    (0.86, 0.000, 0.090, 0.124, 0.078, 0.25, 0.40), (0.78, 0.000, 0.070, 0.100, 0.066, 0.3, 0.50), (0.70, 0.000, 0.055, 0.080, 0.055, 0.35, 0.60),
    (0.64, 0.000, 0.048, 0.072, 0.048, 0.4, 0.70), (0.60, 0.000, 0.050, 0.086, 0.052, 0.4, 0.60), (0.555, 0.000, 0.052, 0.070, 0.056, 0.4, 0.50),
    (0.51, 0.000, 0.044, 0.052, 0.046, 0.5, 0.60), (0.45, 0.000, 0.036, 0.050, 0.039, 0.5, 0.75), (0.34, 0.000, 0.033, 0.050, 0.037, 0.5, 0.75),
    (0.25, -0.002, 0.033, 0.052, 0.038, 0.5, 0.70), (0.20, -0.004, 0.035, 0.058, 0.042, 0.5, 0.55), (0.165, -0.002, 0.036, 0.058, 0.045, 0.5, 0.50),
    (0.13, 0.012, 0.033, 0.042, 0.040, 0.4, 0.45), (0.10, 0.026, 0.034, 0.038, 0.042, 0.4, 0.40), (0.078, 0.034, 0.040, 0.040, 0.046, 0.4, 0.30),
]
#   y, forward shift, front (toe), back (heel), half width
HOOF_FRONT = [(0.0, 0.050, 0.064, 0.040, 0.056), (0.006, 0.050, 0.066, 0.042, 0.058), (0.03, 0.046, 0.057, 0.041, 0.055),
              (0.06, 0.040, 0.046, 0.040, 0.050), (0.082, 0.034, 0.038, 0.038, 0.045)]
HOOF_HIND = [(0.0, 0.050, 0.066, 0.039, 0.052), (0.006, 0.050, 0.068, 0.041, 0.054), (0.03, 0.046, 0.058, 0.040, 0.052),
             (0.06, 0.040, 0.046, 0.040, 0.048), (0.082, 0.034, 0.038, 0.038, 0.044)]


def leg_axis(js, y):
    j0, j1, j2, g = js
    if y >= j1.y:
        return j1 + (j0 - j1) * ((y - j1.y) / (j0.y - j1.y))
    if y >= j2.y:
        return j2 + (j1 - j2) * ((y - j2.y) / (j1.y - j2.y))
    return g + (j2 - g) * ((y - g.y) / (j2.y - g.y))


def leg_centre(leg, y, dz):
    js = LEGS_J[leg]
    c = leg_axis(js, y)
    sx = 1.0 if js[0].x > 0 else -1.0
    inward = -sx * 0.022 * max(0.0, min(1.0, (y - js[1].y) / 0.2))      # the top of the leg tucks under the body
    if y > 0.98:                                                         # and on up inside it, well clear of the side
        inward -= sx * 0.05 * min(1.0, (y - 0.98) / 0.14)               # of the chest even with the leg swung back
    return V(c.x + inward, y, c.z + dz)


def leg_section(leg, y):
    table = sorted(FRONT if leg[0] == "F" else HIND)
    return interp(table, y)


# ------------------------------------------------------------------ weights
def _with_legs(wts, p):
    """Body weights near the top of a front leg: part of the skin over the elbow moves with the leg's upper bone, so
    the leg bends out of the chest instead of sliding through it. (Tried on the stifle too: the flank folded oddly.)"""
    for leg, (j0, j1, j2, g) in LEGS_J.items():
        front = leg[0] == "F"
        if not front:
            continue
        c = V(j1.x, j1.y + (0.06 if front else 0.12), j1.z + (0.02 if front else -0.04))
        d = (p - c).length
        reach = 0.17 if front else 0.2
        if d >= reach or p.y > (1.16 if front else 1.2):
            continue
        infl = (0.45 if front else 0.3) * sm(reach, reach * 0.35, d)
        if infl <= 0.0:
            continue
        wts = {k: v * (1.0 - infl) for k, v in wts.items()}
        wts[leg + "a"] = wts.get(leg + "a", 0.0) + infl
    return wts


def w_body(co):
    return _with_legs(_w_body(co), game(co))


def _w_body(co):
    z = game(co).z
    if z < -0.30:
        return {"Pelvis": 1.0}
    if z < -0.02:
        t = sm(-0.30, -0.02, z)
        return {"Pelvis": 1 - t, "Spine": t}
    if z < 0.14:
        return {"Spine": 1.0}
    if z < 0.42:
        t = sm(0.14, 0.42, z)
        return {"Spine": 1 - t, "Chest": t}
    return {"Chest": 1.0}


def w_neck(co):
    t = (game(co) - NECK_BASE).dot(NECK_DIR) / NECK_LEN
    if t < 0.02:
        return {"Chest": 1.0}
    if t < 0.34:
        k = sm(0.02, 0.34, t)
        return {"Chest": 1 - k, "Neck": k}
    if t < 0.88:
        return {"Neck": 1.0}
    k = sm(0.88, 1.06, t)
    return {"Neck": 1 - k, "Head": k}


def w_tail(co):
    t = (game(co) - TAIL_HEAD).dot(TAIL_DIR) / TAIL_LEN
    if t < 0.08:
        k = sm(-0.08, 0.08, t)
        return {"Pelvis": 1 - k, "Tail1": k}
    if t < 0.32:
        return {"Tail1": 1.0}
    k = sm(0.32, 0.6, t)
    return {"Tail1": 1 - k, "Tail2": k}


def w_leg(leg):
    j0, j1, j2, g = LEGS_J[leg]
    body = "Chest" if leg[0] == "F" else "Pelvis"

    def f(co):
        y = game(co).y
        if y > j1.y + 0.06:
            k = sm(j0.y + 0.10, j0.y - 0.14, y)
            return {body: 1 - k, leg + "a": k}
        if y > j1.y - 0.06:
            k = sm(j1.y + 0.06, j1.y - 0.06, y)
            return {leg + "a": 1 - k, leg + "b": k}
        if y > j2.y + 0.05:
            return {leg + "b": 1.0}
        if y > j2.y - 0.05:
            k = sm(j2.y + 0.05, j2.y - 0.05, y)
            return {leg + "b": 1 - k, leg + "c": k}
        return {leg + "c": 1.0}
    return f


def w_head(co):
    return {"Head": 1.0}


# ------------------------------------------------------------------ hair
def flat_across(path, normals):
    """For a strand along 'path' (game axes) over a surface with these normals: the way it lies flat, per point."""
    out = []
    for k, p in enumerate(path):
        d = path[min(k + 1, len(path) - 1)] - path[max(k - 1, 0)]
        a = d.cross(normals[k])
        out.append(B(a.normalized() if a.length > 1e-6 else X))
    return out


def mane(m, rng):
    """Two layers of strands from the crest, falling over the off side and back down the neck; a ridge of hair
    along the crest hides their roots."""
    m.surface("hair", WHITE, 0.08).set_part(MANE, w_neck)
    ridge = []
    for i in range(14):
        t = -0.02 + 1.06 * i / 13
        crest, throat, hw = interp(NECK, t)
        c = neck_c(t) + NECK_UP * (crest - 0.012) + X * 0.008
        ridge.append(profile_ring(c, X, NECK_UP, 0.030, 0.012, 0.026 + 0.004 * math.sin(i * 1.9), 1.6, 2.0, 0.3, 0.1, 8))
    m.loft(ridge, True, 1, wrap=tuple(B(NECK_DIR)))
    for layer, count, lift, (l0, l1) in ((0, 46, 0.004, (0.15, 0.25)), (1, 40, 0.010, (0.13, 0.30))):
        for i in range(count):
            t0 = (i + 0.5 * layer + rng.uniform(-0.25, 0.25)) / count
            if not -0.02 <= t0 <= 1.0:
                continue
            ln = rng.uniform(l0, l1) * (0.75 + 0.35 * math.sin(math.pi * min(1.0, t0 * 1.1)))
            a0 = math.pi / 2 + rng.uniform(-0.05, 0.12)                  # on the crest; the hair falls to +x, the off side
            samples, last, run = [], None, 0.0
            for k in range(61):
                s = k / 60.0
                t = t0 - 0.22 * s * s                                      # gravity draws it back down the neck
                p = neck_point(t, a0 - s * 2.2, lift * (1.0 - 0.4 * s) + 0.004)
                if last is not None:
                    run += (p - last).length
                last = p
                samples.append((run, p))
                if run >= ln:
                    break
            ln = min(ln, run)
            path = [min(samples, key=lambda rp: abs(rp[0] - ln * q / 6.0))[1] for q in range(7)]
            normals = []
            for p in path:
                t = (p - NECK_BASE).dot(NECK_DIR) / NECK_LEN
                normals.append((p - neck_c(t)).normalized())
            widths = [0.028 * (1.0 - 0.88 * (q / 6.0) ** 1.1) * rng.uniform(0.8, 1.2) for q in range(7)]
            ribbon(m, [B(p) for p in path], widths, flat_across(path, normals))


def forelock(m, rng):
    m.surface("hair", WHITE, 0.08).set_part(MANE, w_head)
    for i in range(9):
        a = math.pi / 2 + (i - 4) * 0.075 + rng.uniform(-0.03, 0.03)
        ln = rng.uniform(0.11, 0.19)
        path = []
        for q in range(6):
            s = q / 5.0
            path.append(head_point(-0.03 + (ln / HEAD_LEN) * s, a + (a - math.pi / 2) * 0.6 * s, 0.006 + 0.01 * (1 - s)))
        normals = [(p - head_c(head_t(p))).normalized() for p in path]
        widths = [0.024 * (1.0 - 0.85 * q / 5.0) for q in range(6)]
        ribbon(m, [B(p) for p in path], widths, flat_across(path, normals))


def tail(m, rng):
    """The dock, a core of hair, and strands falling from the dock to about the hocks."""
    m.surface("hair", WHITE, 0.08).set_part(MANE, w_tail)

    def dock(s):
        return TAIL_HEAD + V(0, -0.05 * s - 0.20 * s * s, -0.12 * s - 0.04 * s * s)

    def dock_dir(s):
        return V(0, -0.05 - 0.40 * s, -0.12 - 0.08 * s).normalized()

    rings = []
    for q in range(9):
        s = -0.15 + 1.15 * q / 8
        d = dock_dir(max(s, 0.0))
        up = X.cross(d).normalized()                                     # away from the body: up and back
        r = 0.058 - 0.022 * max(s, 0.0)
        rings.append(profile_ring(dock(s), X, up, r * 1.05, r * 0.9, r, 2.0, 2.0, 0.1, 0.1, 12))
    m.loft(rings, True, 1, wrap=tuple(B(dock_dir(0.5))))
    core = []
    for q in range(10):
        s = q / 9
        c = dock(1.0) + V(0, -0.52 * s, -0.05 * s - 0.03 * s * s)
        r = 0.05 * (1.0 - 0.45 * s) + 0.012
        core.append(profile_ring(c, X, Z, r, r * 1.1, r * 1.1, 2.0, 2.0, 0.1, 0.1, 10))
    m.loft(core, True, 1, wrap=(0, 0, 1))
    for i in range(56):
        s = rng.uniform(0.05, 1.0)
        phi = rng.uniform(-2.2, 2.2)                                       # 0: the top of the dock
        up = X.cross(dock_dir(s)).normalized()
        radial = (up * math.cos(phi) + X * math.sin(phi)).normalized()
        root = dock(s) + radial * (0.05 - 0.02 * s)
        ln = rng.uniform(0.55, 0.85) * (1.0 - 0.25 * s)
        out = radial * 0.05 + V(0, 0, -0.03)
        spread = X * (math.sin(phi) * rng.uniform(0.04, 0.10)) + Z * (-rng.uniform(0.0, 0.06))
        path, p, n = [root], root, 9
        for q in range(1, n + 1):
            u = q / n
            p = p + V(0, -ln / n, 0) + out * ((1.0 - u) * 0.45) + spread * (1.0 / n) + X * (0.012 * math.sin(u * 5.0 + i))
            path.append(p)
        widths = [0.03 * (1.0 - 0.85 * (q / n) ** 1.2) * rng.uniform(0.8, 1.25) for q in range(n + 1)]
        ribbon(m, [B(p) for p in path], widths, flat_across(path, [radial] * len(path)))


# ------------------------------------------------------------------ the face
def ear(m, side):
    base = head_point(-0.01, math.pi / 2 - side * 0.62, -0.012)
    e = V(side * 0.30, 0.93, 0.20).normalized()                         # the ear points up, a little out and forward
    f0 = V(side * 0.42, 0.0, 0.9)
    f = (f0 - e * f0.dot(e)).normalized()                                 # where the opening faces
    s = e.cross(f).normalized()
    outer, inner = [], []
    for q in range(8):
        u = -0.15 + 1.15 * q / 7
        x = (u + 0.15) / 1.15
        c = base + e * (0.155 * u) + f * (0.025 * x ** 3)                  # the tip turns a little forward
        w = 0.040 * math.sin(math.pi * (0.25 + 0.75 * x ** 0.8)) + 0.002
        d = w * 0.62
        ro, ri = [], []
        for k in range(9):
            phi = math.radians(-135 + 270 * k / 8)
            ro.append(c - f * (math.cos(phi) * d) + s * (math.sin(phi) * w))
            ri.append(c - f * (math.cos(phi) * d * 0.72 - 0.003) + s * (math.sin(phi) * w * 0.8))
        outer.append([B(p) for p in ro])
        inner.append([B(p) for p in ri])
    m.surface("fur", WHITE, 0.0).set_part(COAT, w_head)
    sheet(m, outer, outward=B(-f), grain="z")
    m.surface("fur", (0.55, 0.5, 0.48), 0.0).set_part(COAT, w_head)
    sheet(m, inner, outward=B(f), grain="z")


def eye(m, side):
    a = 0.30 if side > 0 else math.pi - 0.30                              # high on the side of the head
    c = head_point(0.215, a, -0.015)
    nrm = (head_point(0.215, a, 0.0) - head_c(0.215)).normalized()
    nrm = (nrm + HEAD_DIR * 0.25).normalized()                           # it looks a little forward
    up_e = (HEAD_UP - nrm * HEAD_UP.dot(nrm)).normalized()
    along = nrm.cross(up_e).normalized()
    m.surface("wet", (0.06, 0.04, 0.03), 0.0).set_part(0, w_head)
    m.push(B(c))
    m.blob((0, 0, 0), (0.024, 0.024, 0.024), 2)
    m.pop()
    m.surface("fur", (0.82, 0.8, 0.78), 0.0).set_part(COAT, w_head)
    for a0, a1, r in ((0.15, math.pi - 0.15, 0.0075), (math.pi + 0.35, 2 * math.pi - 0.35, 0.0058)):
        pts = [c + nrm * 0.007 + along * (math.cos(t) * 0.027) + up_e * (math.sin(t) * 0.019)
               for t in [a0 + (a1 - a0) * k / 8 for k in range(9)]]
        m.tube([B(p) for p in pts], r, 6)
    m.surface("plain", (0.07, 0.05, 0.04), 0.0).set_part(0, w_head)     # lashes
    for k in range(7):
        t = 0.45 + (math.pi - 0.9) * k / 6
        p0 = c + nrm * 0.012 + along * (math.cos(t) * 0.027) + up_e * (math.sin(t) * 0.021)
        p1 = p0 + nrm * 0.008 + up_e * 0.006 + along * (math.cos(t) * 0.004)
        m.tube([B(p0), B(p1)], 0.0011, 3)


def nostril(m, side):
    a = 0.12 if side > 0 else math.pi - 0.12
    c = head_point(0.905, a, -0.004)
    nrm = ((head_point(0.905, a, 0.0) - head_c(0.905)).normalized() + HEAD_DIR * 0.9).normalized()
    u = (HEAD_UP - nrm * HEAD_UP.dot(nrm)).normalized()
    w = nrm.cross(u).normalized()
    m.surface("wet", (0.05, 0.04, 0.035), 0.0).set_part(0, w_head)
    m.push(B(c))
    m.blob((0, 0, 0), (0.012, 0.012, 0.012), 1)
    m.pop()
    m.surface("fur", WHITE, 0.0).set_part(MUZZLE, w_head)
    pts = []
    for k in range(11):                                                   # the comma-shaped rim
        t = -0.5 + 2.6 * k / 10
        r = 0.019 * (1.0 - 0.25 * k / 10)
        pts.append(c + nrm * 0.004 + u * (math.sin(t) * r * 1.1) + w * (math.cos(t) * r * 0.75 * side))
    m.tube([B(p) for p in pts], 0.0055, 6)


def mouth(m):
    """The line of the lips, from the corners round the front of the muzzle, low."""
    m.surface("plain", (0.08, 0.06, 0.055), 0.0).set_part(0, w_head)
    for a0, a1 in ((-0.62, -math.pi / 2), (math.pi + 0.62, 1.5 * math.pi)):
        line = [head_point(0.86 + 0.19 * q / 8, a0 + (a1 - a0) * (q / 8) ** 1.6, 0.0015) for q in range(9)]
        m.tube([B(p) for p in line], 0.0028, 4)


def chestnut(m, leg, y):
    """The horny patch on the inside of the leg."""
    sx = 1.0 if LEGS_J[leg][0].x > 0 else -1.0
    dz, f, b, w, tt, tb = leg_section(leg, y)
    c = leg_centre(leg, y, dz) + X * (-sx * (w - 0.004))
    m.surface("horn", (0.20, 0.17, 0.14), 0.0).set_part(0, w_leg(leg))
    m.push(B(c))
    m.blob((0, 0, 0), (0.006, 0.018, 0.022), 1)
    m.pop()


# ------------------------------------------------------------------ the horse
def horse(arm, col):
    m = Skinned("horse_body", QB)
    rng = random.Random(29)

    # body, from the root of the tail to the breast
    m.surface("fur", WHITE, 0.0).set_part(COAT, w_body)
    zs, z = [], BODY[0][0]
    while z < BODY[-1][0] - 0.02:
        zs.append(z)
        z += 0.06
    zs.append(BODY[-1][0])
    m.loft([body_ring(z) for z in zs], True, 1, wrap=tuple(B(Z)))

    # neck and head
    m.set_part(COAT, w_neck)
    m.loft([neck_ring(-0.30 + 1.36 * q / 16) for q in range(17)], True, 1, wrap=tuple(B(NECK_DIR)))
    m.set_part(COAT, w_head)
    m.loft([head_ring(-0.14 + 1.215 * q / 20) for q in range(21)], True, 1, wrap=tuple(B(HEAD_DIR)))

    # legs: a loft each from inside the body to the coronet
    for leg in LEGS_J:
        table = FRONT if leg[0] == "F" else HIND
        m.set_part(COAT, w_leg(leg))
        ys = [r[0] for r in table]
        fine = []
        for a, b in zip(ys, ys[1:]):
            steps = max(1, int(round((a - b) / 0.035)))
            fine += [a + (b - a) * q / steps for q in range(steps)]
        fine.append(ys[-1])
        rings = []
        for y in fine:
            dz, f, b, w, tt, tb = leg_section(leg, y)
            rings.append(profile_ring(leg_centre(leg, y, dz), X, Z, f, b, w, 2.0, 1.8, tt, tb, 16))
        m.loft(rings, True, 0, wrap=tuple(B(Y)))          # closed: a leg's top must never show as an open rim

    # muscle and bone under the coat
    def S(c, r, off):
        return (B(V(*c)), r, B(V(*off)))
    moves = [
        S((0, 1.512, -0.62), 0.06, (0, -0.008, 0)), S((0, 1.512, -0.48), 0.06, (0, -0.007, 0)),   # the groove along the croup
        S((0, 0.80, -0.10), 0.26, (0, -0.012, 0)),                                                 # the belly hangs a little
        S((0, 1.00, 0.72), 0.045, (0, 0, -0.012)),                                                 # the groove between the breasts
    ]
    for sx in (-1, 1):
        moves += [
            S((sx * 0.20, 1.44, 0.42), 0.09, (sx * 0.010, 0, 0)), S((sx * 0.20, 1.34, 0.52), 0.09, (sx * 0.012, 0, 0)),   # shoulder blade
            S((sx * 0.18, 1.24, 0.62), 0.08, (sx * 0.010, 0, 0.006)), S((sx * 0.17, 1.16, 0.70), 0.06, (sx * 0.006, 0, 0.012)),
            S((sx * 0.25, 1.06, 0.40), 0.11, (sx * 0.012, 0, 0)),                                  # the triceps behind it
            S((sx * 0.09, 1.04, 0.70), 0.08, (sx * 0.004, 0, 0.012)),                              # breast muscles
            S((sx * 0.25, 0.92, 0.30), 0.06, (-sx * 0.010, 0.006, 0)),                             # the girth behind the elbow
            S((sx * 0.29, 1.20, -0.33), 0.13, (-sx * 0.013, 0, 0)),                                # the hollow of the flank
            S((sx * 0.23, 1.43, -0.40), 0.065, (sx * 0.012, 0.006, 0)),                            # the point of the hip
            S((sx * 0.17, 1.47, -0.64), 0.13, (sx * 0.008, 0.010, 0)),                             # the round of the croup
            S((sx * 0.12, 1.20, -0.86), 0.07, (sx * 0.004, 0, -0.012)),                            # the point of the buttock
            S((sx * 0.30, 0.96, -0.40), 0.07, (sx * 0.008, 0, 0.004)),                             # stifle
            S((sx * 0.25, 1.02, -0.60), 0.12, (sx * 0.010, 0, 0)),                                 # the big muscle of the thigh
            S((sx * 0.055, 1.66, 0.80), 0.05, (-sx * 0.006, 0, 0)), S((sx * 0.055, 1.52, 0.72), 0.05, (-sx * 0.006, 0, 0)),   # jugular groove
        ]
        a = 0.0 if sx > 0 else math.pi                                                             # the head
        moves.append((B(head_point(0.29, a - sx * 0.55)), 0.07, B(X * (sx * 0.010) - HEAD_UP * 0.004)))   # cheek
        r0, r1 = head_point(0.30, a + sx * 0.05), head_point(0.55, a + sx * 0.12)
        for q in range(4):
            moves.append((B(r0 + (r1 - r0) * (q / 3)), 0.025, B(X * (sx * 0.005))))                  # the facial crest
        moves.append((B(head_point(0.20, a + sx * 0.62)), 0.03, B(X * (sx * 0.006) + HEAD_UP * 0.003)))   # the bone over the eye
        moves.append((B(head_point(0.10, a + sx * 0.75)), 0.03, B(X * (-sx * 0.006))))                  # the hollow above it
        nos = head_point(0.905, a + sx * 0.12)
        moves.append((B(nos), 0.022, B((nos - head_c(0.905)).normalized() * -0.010)))
    moves.append((B(head_point(0.975, -math.pi / 2)), 0.03, B(-HEAD_UP * 0.008 + HEAD_DIR * 0.004)))   # chin
    m.sculpt(moves)

    # colour regions: points on the legs, socks, muzzle, blaze, the rump of a roan
    def legs_part(co):
        p = game(co)
        if abs(p.x) < 0.08 or p.y > 0.76:
            return None
        base = 0.62 if p.z < -0.15 else 0.58
        if p.y < 0.255 + 0.025 * math.sin(p.x * 60.0 + p.z * 40.0):
            return SOCKS
        if p.y < base:
            return LEGS
        if p.y < base + 0.04:
            return LEG2
        if p.y < base + 0.08:
            return LEG1
        return None
    for part in (LEG1, LEG2, LEGS, SOCKS):
        kit.repart(m, lambda co, pt=part: legs_part(co) == pt, part)

    def on_head(p):
        t = head_t(p)
        return -0.2 < t < 1.2 and (p - head_c(t)).length < 0.3

    kit.repart(m, lambda co: on_head(game(co)) and 0.72 <= head_t(game(co)) < 0.80, MUZ1)
    kit.repart(m, lambda co: on_head(game(co)) and head_t(game(co)) >= 0.80, MUZZLE)

    def blaze(co):
        p = game(co)
        if not on_head(p):
            return False
        t = head_t(p)
        if not 0.08 < t < 1.0 or (p - head_c(t)).dot(HEAD_UP) < 0.0:
            return False
        w = 0.016 + 0.020 * math.exp(-((t - 0.2) / 0.09) ** 2) + 0.016 * sm(0.72, 0.95, t) + 0.004 * math.sin(t * 40.0)
        return abs(p.x) < w
    kit.repart(m, blaze, BLAZE)

    def rump(co):
        p = game(co)
        return p.z < -0.42 and abs(p.x) < 0.33 and p.y > 1.24 + 0.05 * math.sin(p.x * 22.0 + p.z * 9.0) - 0.25 * (p.z + 0.42)
    kit.repart(m, rump, RUMP)

    # hooves and chestnuts, the face, the hair
    for leg in LEGS_J:
        table = HOOF_FRONT if leg[0] == "F" else HOOF_HIND
        m.surface("horn", WHITE, 0.0).set_part(HOOF, w_leg(leg))
        m.loft([profile_ring(leg_centre(leg, y, dz), X, Z, f, b, w, 2.2, 1.6, 0.35, 0.3, 16) for y, dz, f, b, w in table], True, 0, wrap=tuple(B(Y)))
        chestnut(m, leg, 0.665 if leg[0] == "F" else 0.47)
    for sx in (-1, 1):
        ear(m, sx)
        eye(m, sx)
        nostril(m, sx)
    mouth(m)
    forelock(m, rng)
    mane(m, rng)
    tail(m, rng)

    bake_ao(m, rays=24, dist=0.22, strength=0.85, floor=0.40)
    return m.finish_skinned(col, arm, 60.0)


def build():
    kit.clear_default_scene()
    col = kit.collection("Horse")
    arm = kit.make_armature("HorseRig", QB, QB_PARENT, joints(), col)
    return [horse(arm, col)]


if "ARGS" in globals() or __name__ == "__main__":
    args = globals().get("ARGS", {})
    objs = build()
    print(kit.stats(objs))
    if not args.get("noexport"):
        print("exported", kit.export_skinned_fbx("Horse", "horse"))
