"""People: head, hands and clothes as separate skinned pieces on the game's skeleton, plus hats.
    Tools/blender/bmcp.py run Tools/blender/human.py
Collection "Human" -> Assets/Canaan/Models/human.fbx (skinned); collection "Hats" -> hats.fbx (static).

Numbers are in the game's axes (x right, y up, z forward, metres, a 1.78 m man with his arms at his sides) and
go through kit.L() into Blender. The colour alpha of every vertex names the part the game dyes per person:
1 skin, 2 hair, 3 shirt, 4 trousers, 5 boots, 6 waistcoat, 7 coat, 8 neckerchief, 9 skirt, 10 forearm of the
sleeve (skin when the sleeves are rolled up); 0 keeps the colour it has. The rgb is a shade the dye is multiplied by.
"""
import importlib
import math
import random

import kit

importlib.reload(kit)
from kit import L, Pal, Skinned, Model, shade, bake_ao, ribbon, sheet, gbox
from mathutils import Vector

J = kit.human_joints()
SKIN, HAIR, SHIRT, PANTS, BOOTS, VEST, COAT, SCARF, SKIRT, CUFF = 1, 2, 3, 4, 5, 6, 7, 8, 9, 10
WHITE = (1.0, 1.0, 1.0)
HEAD_PIVOT, HEAD_SCALE = (0.0, 1.575, 0.008), 1.08


def sm(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def game(co):
    return -co.x, co.z, -co.y


# ------------------------------------------------------------------ which bones move a point
def w_torso(co):
    x, y, z = game(co)
    if y < 1.00:
        return {"Hips": 1.0}
    if y < 1.12:
        t = sm(1.00, 1.12, y)
        return {"Hips": 1 - t, "Spine": t}
    if y < 1.16:
        return {"Spine": 1.0}
    if y < 1.28:
        t = sm(1.16, 1.28, y)
        return {"Spine": 1 - t, "Chest": t}
    if y < 1.47:
        return {"Chest": 1.0}
    if y < 1.53:
        t = sm(1.47, 1.53, y)
        return {"Chest": 1 - t, "Neck": t}
    if y < 1.56:
        return {"Neck": 1.0}
    if y < 1.60:
        t = sm(1.56, 1.60, y)
        return {"Neck": 1 - t, "Head": t}
    return {"Head": 1.0}


def w_head(co):
    return {"Head": 1.0}


def w_arm(side):
    s, e, w = J["Arm" + side], J["Fore" + side], J["Hand" + side]

    def f(co):
        y = co.z
        if y > e[1] + 0.05:
            t = sm(s[1] + 0.035, s[1] - 0.075, y)
            return {"Chest": 1 - t, "Arm" + side: t}
        if y > e[1] - 0.05:
            t = sm(e[1] + 0.05, e[1] - 0.05, y)
            return {"Arm" + side: 1 - t, "Fore" + side: t}
        t = sm(w[1] + 0.04, w[1] - 0.015, y)
        return {"Fore" + side: 1 - t, "Hand" + side: t}
    return f


def w_leg(side):
    h, k, a = J["Thigh" + side], J["Shin" + side], J["Foot" + side]

    def f(co):
        x, y, z = game(co)
        if y > k[1] + 0.05:
            t = sm(h[1] + 0.07, h[1] - 0.10, y)
            return {"Hips": 1 - t, "Thigh" + side: t}
        if y > k[1] - 0.05:
            t = sm(k[1] + 0.05, k[1] - 0.05, y)
            return {"Thigh" + side: 1 - t, "Shin" + side: t}
        if y > a[1] + 0.07:
            return {"Shin" + side: 1.0}
        if z > 0.055:
            t = sm(0.055, 0.115, z)
            return {"Foot" + side: 1 - t, "Toe" + side: t}
        t = sm(a[1] + 0.07, a[1] + 0.005, y)
        return {"Shin" + side: 1 - t, "Foot" + side: t}
    return f


def w_pelvis(co):
    x, y, z = game(co)
    if y > 0.96:
        return w_torso(co)
    side = "L" if x < 0 else "R"
    t = sm(0.97, 0.83, y) * min(1.0, abs(x) / 0.05)
    return {"Hips": 1 - t, "Thigh" + side: t}


# ------------------------------------------------------------------ shapes
def ring(cx, y, cz, rx, rz, n=16, sq=2.3, wob=0.0, seed=0.0):
    """A horizontal super-ellipse in game axes; wob adds shallow cloth folds."""
    out = []
    e = 2.0 / sq
    for i in range(n):
        a = i * 2 * math.pi / n
        cs, sn = math.cos(a), math.sin(a)
        k = 1.0 + wob * math.sin(a * 5.0 + seed) * math.sin(a * 2.0 + seed * 1.7)
        out.append(L(cx + rx * k * math.copysign(abs(cs) ** e, cs), y, cz + rz * k * math.copysign(abs(sn) ** e, sn)))
    return out


def lerp3(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def resample(stations, step):
    """(centre, radius) stations with more in between, at most 'step' metres apart."""
    dense = [stations[0]]
    for (c0, r0), (c1, r1) in zip(stations, stations[1:]):
        k = max(1, int(math.ceil(math.dist(c0, c1) / step)))
        dense += [(lerp3(c0, c1, q / k), r0 + (r1 - r0) * q / k) for q in range(1, k + 1)]
    return dense


def limb(m, stations, n=10, sq=2.0, subdiv=1, cap=False, step=None):
    """stations: (centre, radius) down a limb -> one lofted tube. Open-ended: a capped tube shrinks at its ends
    when it is smoothed and leaves a gap at the joint; open tubes that share a ring stay joined. 'step': rings at
    most this far apart and no smoothing - room for folds."""
    if step:
        stations, subdiv = resample(stations, step), 0
    m.loft([ring(c[0], c[1], c[2], r, r, n, sq) for c, r in stations], cap, subdiv, wrap=(0, 0, 1))


def folds(m, start, a, b, waves, amp, bias=None, seed=0.0):
    """Cloth folds on the vertices made since 'start': crests round the line a-b (game axes), bent and broken by
    the angle round it, fading out at both ends, stronger on the side 'bias' (a game direction) looks to."""
    A, Bp = Vector(a), Vector(b)
    ax = Bp - A
    ln = ax.length
    ax.normalize()
    u = ax.orthogonal().normalized()
    w = ax.cross(u)
    bv = Vector(bias).normalized() if bias else None
    for v in list(m.bm.verts)[start:]:
        p = Vector(game(v.co))
        s = (p - A).dot(ax) / ln
        if not 0.0 < s < 1.0:
            continue
        r = p - (A + ax * (s * ln))
        if r.length < 1e-5:
            continue
        rn = r.normalized()
        th = math.atan2(rn.dot(w), rn.dot(u))
        k = 1.0 if bv is None else 0.3 + 0.7 * max(0.0, rn.dot(bv))
        wave = math.sin(2 * math.pi * waves * s + 1.2 * math.sin(th * 2.0 + s * 7.0 + seed) + seed)
        wave = math.copysign(abs(wave) ** 0.6, wave)
        p = p + rn * (amp * math.sin(math.pi * s) * k * wave)
        v.co = L(*p)


def arc(cx, y, cz, rx, rz, a0, a1, n, sq=2.15, wob=0.0, seed=0.0):
    """Part of a ring (an open garment): n + 1 points from angle a0 to a1 (radians; 0 is +x, pi/2 the front)."""
    out = []
    e = 2.0 / sq
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / n
        cs, sn = math.cos(a), math.sin(a)
        k = 1.0 + wob * math.sin(a * 5.0 + seed) * math.sin(a * 2.0 + seed * 1.7)
        out.append(L(cx + rx * k * math.copysign(abs(cs) ** e, cs), y, cz + rz * k * math.copysign(abs(sn) ** e, sn)))
    return out


def outward(p, by):
    """A Blender point pushed 'by' metres away from the body's upright axis."""
    g = Vector(game(p))
    d = Vector((g.x, 0.0, g.z))
    return L(*(g + d.normalized() * by)) if d.length > 1e-6 else p


TORSO = [  # y, rx, rz, z of the body under the clothes: the chest a little fuller, the shoulders sloping to the neck
    (1.02, 0.156, 0.101, 0.000), (1.06, 0.150, 0.098, 0.000), (1.12, 0.142, 0.094, 0.003), (1.20, 0.150, 0.104, 0.008),
    (1.31, 0.172, 0.121, 0.012), (1.38, 0.186, 0.116, 0.008), (1.43, 0.188, 0.104, 0.002), (1.465, 0.165, 0.090, 0.000),
    (1.49, 0.122, 0.078, 0.000), (1.515, 0.074, 0.066, 0.002),
]
SQ = 2.15        # the section of a garment over the torso: a rounded box, rounder than it was


def torso_at(y):
    for (y0, rx0, rz0, z0), (y1, rx1, rz1, z1) in zip(TORSO, TORSO[1:]):
        if y0 <= y <= y1:
            t = (y - y0) / (y1 - y0)
            return rx0 + (rx1 - rx0) * t, rz0 + (rz1 - rz0) * t, z0 + (z1 - z0) * t
    return TORSO[-1][1:] if y > TORSO[-1][0] else TORSO[0][1:]


def torso_shell(m, y0, y1, grow, n=22, steps=14, wob=0.012, cap=False, open_front=0.0):
    """A garment over the torso between two heights, 'grow' metres off the body."""
    rings = []
    for i in range(steps + 1):
        y = y0 + (y1 - y0) * i / steps
        rx, rz, z = torso_at(y)
        rings.append(ring(0, y, z, rx + grow, rz + grow, n, SQ, wob, y * 9.0))
    m.loft(rings, cap, 1, wrap=(0, 0, 1))


def torso_front(x, y, grow=0.0):
    """z of the front of the torso garment (plus 'grow') at x across and height y."""
    rx, rz, z0 = torso_at(y)
    t = min(0.97, abs(x) / (rx + grow))
    return z0 + (rz + grow) * (1.0 - t ** SQ) ** (1.0 / SQ)


def front_z(y, grow=0.0):
    rx, rz, z = torso_at(y)
    return z + rz + grow


# ------------------------------------------------------------------ pieces
def head(arm, col):
    m = Skinned("hu_head", kit.HB)
    m.surface("skin", WHITE, 0.0).set_part(SKIN, w_torso)
    rows = [  # y, rx, rz, z
        (1.500, 0.058, 0.060, 0.000), (1.540, 0.052, 0.056, 0.004), (1.575, 0.050, 0.055, 0.008), (1.590, 0.056, 0.068, 0.014),
        (1.606, 0.063, 0.080, 0.016), (1.628, 0.069, 0.088, 0.015), (1.652, 0.074, 0.093, 0.012), (1.676, 0.077, 0.097, 0.009),
        (1.700, 0.078, 0.099, 0.006), (1.724, 0.078, 0.098, 0.003), (1.748, 0.074, 0.093, 0.000), (1.768, 0.064, 0.081, -0.003),
        (1.784, 0.046, 0.060, -0.006), (1.793, 0.022, 0.030, -0.008),
    ]
    m.loft([ring(0, y, z, rx, rz, 20, 2.15) for y, rx, rz, z in rows], True, 2, wrap=(0, 0, 1))
    S = lambda c, r, off: (L(*c), r, L(*off))
    moves = []
    for sx in (-1, 1):
        moves += [
            S((sx * 0.031, 1.697, 0.094), 0.024, (0, 0, -0.012)),          # eye sockets
            S((sx * 0.034, 1.719, 0.088), 0.030, (0, 0.001, 0.007)),       # brow ridge
            S((sx * 0.056, 1.668, 0.062), 0.036, (sx * 0.006, 0, 0.006)),  # cheekbones
            S((sx * 0.046, 1.640, 0.074), 0.028, (0, 0, -0.005)),          # the hollow under them
            S((sx * 0.062, 1.606, 0.030), 0.050, (-sx * 0.009, 0, 0)),     # the jaw narrows to the chin
            S((sx * 0.066, 1.624, -0.012), 0.034, (sx * 0.004, -0.002, 0)),   # its angle under the ear
            S((sx * 0.024, 1.628, 0.092), 0.016, (0, 0, -0.004)),          # corners of the mouth
            S((sx * 0.074, 1.720, 0.020), 0.040, (-sx * 0.004, 0, 0)),     # temples
        ]
    moves += [
        S((0, 1.676, 0.098), 0.030, (0, 0, 0.007)),                        # the root of the nose (the nose itself is laid on below)
        S((0, 1.628, 0.096), 0.026, (0, 0, 0.005)),                        # the mouth stands forward a little
        S((0, 1.590, 0.078), 0.040, (0, -0.005, 0.011)),                   # chin
        S((0, 1.745, 0.070), 0.060, (0, 0, 0.004)),                        # forehead
        S((0, 1.70, -0.085), 0.070, (0, 0, -0.008)),                       # back of the skull
    ]
    m.sculpt(moves)
    m.set_part(SKIN, w_head)
    for sx in (-1, 1):                                                     # ears
        m.push(L(sx * 0.080, 1.676, -0.004), (0, 0, -sx * 18.0))
        m.blob((0, 0, 0), (0.010, 0.017, 0.026), 1)
        m.pop()
    # the nose: arches from the bridge down to the tip, wings at the nostrils, tucked back under to the lip
    nose = [  # y, half-width, ridge z, base z
        (1.714, 0.0075, 0.1030, 0.0960), (1.700, 0.0080, 0.1075, 0.0950), (1.686, 0.0092, 0.1130, 0.0960), (1.672, 0.0115, 0.1190, 0.0975),
        (1.661, 0.0150, 0.1245, 0.0990), (1.653, 0.0168, 0.1265, 0.1000), (1.648, 0.0150, 0.1215, 0.1000), (1.644, 0.0100, 0.1080, 0.0990),
    ]
    m.loft([[L(-w, y, base), L(-w * 0.82, y, base + (ridge - base) * 0.5), L(-w * 0.42, y, ridge - (ridge - base) * 0.1), L(0, y + 0.0005, ridge),
             L(w * 0.42, y, ridge - (ridge - base) * 0.1), L(w * 0.82, y, base + (ridge - base) * 0.5), L(w, y, base)] for y, w, ridge, base in nose], True, 1)
    m.surface("plain", (0.20, 0.12, 0.10), 0.0).set_part(0, w_head)       # nostrils
    for sx in (-1, 1):
        m.push(L(sx * 0.0068, 1.6478, 0.1135))
        m.blob((0, 0, 0), (0.0036, 0.0030, 0.0022), 1)
        m.pop()
    # eyes: the white, a brown iris and the pupil, under an upper lid with its lash line, over a lower lid
    for sx in (-1, 1):
        ex = sx * 0.031
        m.surface("wet", (0.90, 0.88, 0.84), 0.0).set_part(0, w_head)
        # the ball is turned a little outwards, as the face is there: set square, its outer corner stood proud and the eyes squinted
        m.push(L(ex, 1.697, 0.0822), (0, 0, -sx * 11.0))
        m.blob((0, 0, 0), (0.0146, 0.0098, 0.0086), 2)
        m.pop()
        m.surface("wet", (0.22, 0.13, 0.07), 0.0)
        m.push(L(ex + sx * 0.0012, 1.6968, 0.0903))
        m.blob((0, 0, 0), (0.0064, 0.0024, 0.0064), 1)
        m.pop()
        m.surface("wet", (0.02, 0.02, 0.02), 0.0)
        m.push(L(ex + sx * 0.0012, 1.6968, 0.0919))
        m.blob((0, 0, 0), (0.0031, 0.0014, 0.0031), 1)
        m.pop()
        arc = [math.radians(d) for d in (0, 30, 60, 90, 120, 150, 180)]
        m.surface("skin", (0.84, 0.78, 0.76), 0.0).set_part(SKIN, w_head)  # upper lid
        m.loft([[L(ex + 0.0182 * math.cos(a), 1.7018 + 0.0062 * math.sin(a) + dy, 0.0890 + 0.0042 * math.sin(a) + dz)
                 for dy, dz in ((0.0045, 0), (0, 0.0042), (-0.0028, 0.0012), (0, -0.004))] for a in arc], True, 0)
        m.surface("plain", (0.10, 0.07, 0.06), 0.0).set_part(0, w_head)    # lashes: a dark line under the lid
        m.loft([[L(ex + 0.0178 * math.cos(a), 1.6990 + 0.0058 * math.sin(a) + dy, 0.0906 + 0.0040 * math.sin(a) + dz)
                 for dy, dz in ((0.0012, 0), (0, 0.0018), (-0.0012, 0), (0, -0.002))] for a in arc], True, 0)
        m.surface("skin", (0.88, 0.80, 0.78), 0.0).set_part(SKIN, w_head)  # lower lid
        m.loft([[L(ex + 0.0170 * math.cos(a), 1.6922 - 0.0042 * math.sin(a) + dy, 0.0890 + 0.0030 * math.sin(a) + dz)
                 for dy, dz in ((0.0022, 0.0006), (0, 0.003), (-0.003, 0), (0, -0.004))] for a in arc], True, 0)
    # lips, a shade redder than the skin, and the line between them
    m.surface("skin", (0.86, 0.66, 0.64), 0.0).set_part(SKIN, w_head)
    m.loft([ring(0, 1.6350, 0.1010, 0.0150, 0.0050, 10, 2.0), ring(0, 1.6318, 0.1015, 0.0205, 0.0082, 10, 2.0), ring(0, 1.6285, 0.1010, 0.0215, 0.0080, 10, 2.0)], True, 1)
    m.surface("skin", (0.90, 0.70, 0.67), 0.0).set_part(SKIN, w_head)
    m.loft([ring(0, 1.6272, 0.1000, 0.0208, 0.0080, 10, 2.0), ring(0, 1.6230, 0.1005, 0.0185, 0.0092, 10, 2.0), ring(0, 1.6185, 0.0990, 0.0125, 0.0055, 10, 2.0)], True, 1)
    m.surface("plain", (0.30, 0.17, 0.15), 0.0).set_part(0, w_head)
    m.loft([ring(0, 1.6286, 0.1010, 0.0200, 0.0086, 10, 2.0), ring(0, 1.6270, 0.1010, 0.0200, 0.0086, 10, 2.0)], True, 0)
    m.surface("hair", (0.85, 0.85, 0.85), 0.04).set_part(HAIR, w_head)    # brows
    for sx in (-1, 1):
        m.loft([[L(sx * (0.010 + 0.044 * t), 1.7215 + 0.005 * math.sin(t * math.pi) - 0.003 * t + dy * (1.0 - 0.5 * t), 0.1025 - 0.016 * t * t + dz)
                 for dy, dz in ((0.0065, 0), (0, 0.005), (-0.0065, 0), (0, -0.004))] for t in (0.0, 0.25, 0.5, 0.75, 1.0)], True, 1)
    bake_ao(m, rays=20, dist=0.03, strength=0.6, floor=0.55)
    ob = m.finish_skinned(col, arm)
    pivot = L(*HEAD_PIVOT)                                                 # the game draws heads a touch larger
    for v in ob.data.vertices:
        if v.co.z > pivot.z:
            v.co = pivot + (v.co - pivot) * HEAD_SCALE
    return ob


def hp(p):
    """A point on the head, in the larger head's space."""
    return L(*[HEAD_PIVOT[i] + (p[i] - HEAD_PIVOT[i]) * HEAD_SCALE for i in range(3)])


def shell(m, centre, radii, seg, rings, keep, wob=0.0):
    """Part of an ellipsoid (a cap of hair, a beard): quads whose centre passes keep(x, y, z) in game axes."""
    pts = []
    for j in range(rings + 1):
        phi = math.pi * j / rings
        row = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            k = 1.0 + wob * math.sin(a * 9.0 + j * 1.3)
            row.append((centre[0] + radii[0] * k * math.cos(a) * math.sin(phi), centre[1] + radii[1] * math.cos(phi),
                        centre[2] + radii[2] * k * math.sin(a) * math.sin(phi)))
        pts.append(row)

    def ok(j, i):
        q = [pts[j][i], pts[j][(i + 1) % seg], pts[j + 1][(i + 1) % seg], pts[j + 1][i]]
        return keep(*[sum(p[k] for p in q) / 4 for k in range(3)])
    m.grid([[hp(p) for p in row] for row in pts], ok, wrap=(0, 0, 1))


SCALP_C, SCALP_R = Vector((0, 1.712, -0.012)), Vector((0.085, 0.094, 0.106))


def on_scalp(p, centre, radii, lift):
    """p (game axes) moved onto the ellipsoid, then 'lift' metres out along its normal."""
    d = Vector(((p[0] - centre[0]) / radii[0], (p[1] - centre[1]) / radii[1], (p[2] - centre[2]) / radii[2]))
    d.normalize()
    q = Vector((centre[0] + d.x * radii[0], centre[1] + d.y * radii[1], centre[2] + d.z * radii[2]))
    n = Vector((d.x / radii[0], d.y / radii[1], d.z / radii[2])).normalized()
    return q + n * lift, n


def locks(m, rng, count, keep, flow, length, width, lift=0.004, centre=SCALP_C, radii=SCALP_R, roots=None):
    """Locks of hair lying on an ellipsoid (the scalp, a beard): from roots that pass keep(x, y, z), each walks
    'flow(p)' (a game direction) over the surface for 'length' metres, stopping where the cap ends."""
    made = 0
    tries = 0
    while made < count and tries < count * 40:
        tries += 1
        if roots:
            g = Vector(rng.choice(roots)) + Vector((rng.uniform(-0.01, 0.01), rng.uniform(-0.01, 0.01), rng.uniform(-0.01, 0.01)))
        else:
            th, ph = rng.uniform(0, 2 * math.pi), math.acos(rng.uniform(-0.6, 1.0))
            g = centre + Vector((radii[0] * math.sin(ph) * math.cos(th), radii[1] * math.cos(ph), radii[2] * math.sin(ph) * math.sin(th)))
        if not keep(*g):
            continue
        ln = rng.uniform(*length)
        pts, nrm = [], []
        p, n = on_scalp(g, centre, radii, lift)
        for q in range(6):
            pts.append(p)
            nrm.append(n)
            d = Vector(flow(p))
            d = (d - n * d.dot(n)).normalized()
            nxt, n2 = on_scalp(p + d * (ln / 5.0), centre, radii, lift * (1.0 + 0.6 * q / 5.0))
            gx = nxt - n2 * (lift * (1.0 + 0.6 * q / 5.0))
            if not keep(*gx):
                break
            p, n = nxt, n2
        if len(pts) < 3:
            continue
        across = []
        for k, a in enumerate(pts):
            d = pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]
            across.append(hp(tuple(a + d.cross(nrm[k]).normalized()))
                          - hp(tuple(a)))
        wd = rng.uniform(*width)
        widths = [wd * (1.0 - 0.85 * (k / (len(pts) - 1)) ** 1.2) for k in range(len(pts))]
        ribbon(m, [hp(tuple(a)) for a in pts], widths, across, double=False, outward=L(*nrm[0]))
        made += 1


def hair(arm, col, kind):
    m = Skinned("hu_hair_" + kind, kit.HB)
    m.surface("hair", WHITE, 0.05).set_part(HAIR, w_head)
    rng = random.Random(len(kind) * 31 + 7)
    c, r = (0, 1.712, -0.012), (0.085, 0.094, 0.106)
    crown = Vector((0.025, 1.80, -0.025))                                  # the parting is a little to the right
    if kind == "balding":
        keep = lambda x, y, z: z < 0.01 and 1.625 < y < 1.745
    elif kind in ("long", "bun"):
        keep = lambda x, y, z: y > 1.726 or (z < 0.052 and y > 1.625)
    else:
        keep = lambda x, y, z: y > 1.737 or (z < 0.034 and y > 1.625)
    shell(m, c, r, 28, 16, keep, 0.010)
    if kind == "bun":
        bun = Vector((0, 1.715, -0.115))
        flow = lambda p: (bun - p) + Vector((0, 0.02, 0))
    else:
        def flow(p):
            if p[2] > 0.0:                                                # in front of the ears: combed back and aside
                return Vector((p[0] * 7.0, -0.35, -1.0))
            return (p - crown).normalized() + Vector((0, -0.9, 0))
    locks(m, rng, 95 if kind != "balding" else 40, lambda x, y, z: keep(x, y, z), flow, (0.03, 0.06), (0.010, 0.017), 0.003)
    if kind == "long":
        shell(m, (0, 1.60, -0.045), (0.088, 0.13, 0.07), 24, 12, lambda x, y, z: z < -0.02, 0.015)
        lc, lr = Vector((0, 1.60, -0.045)), Vector((0.088, 0.13, 0.07))
        locks(m, rng, 56, lambda x, y, z: z < -0.02 and y > 1.50, lambda p: Vector((0, -1, -0.15)), (0.06, 0.12), (0.012, 0.020), 0.004, lc, lr)
    if kind == "bun":
        m.push(hp((0, 1.715, -0.115)))
        m.blob((0, 0, 0), (0.047, 0.047, 0.047), 2, lumps=0.06)
        m.pop()
    bake_ao(m, rays=16, dist=0.035, strength=0.25, floor=0.8)          # light: the cap under the locks must not show dark between them
    return m.finish_skinned(col, arm)


def beard(arm, col, kind):
    m = Skinned("hu_beard_" + kind, kit.HB)
    m.surface("hair", WHITE, 0.05).set_part(HAIR, w_head)
    rng = random.Random(len(kind) * 17 + 3)
    if kind == "full":
        bc, br = Vector((0, 1.618, 0.02)), Vector((0.076, 0.058, 0.088))
        keep = lambda x, y, z: y < 1.652 and z > -0.035
        shell(m, tuple(bc), tuple(br), 28, 12, keep, 0.014)
        locks(m, rng, 56, lambda x, y, z: keep(x, y, z) and y < 1.646, lambda p: Vector((0, -1, 0.25)), (0.02, 0.04), (0.009, 0.015), 0.003, bc, br)
    if kind == "goatee":
        m.push(hp((0, 1.598, 0.078)))
        m.blob((0, 0, 0), (0.028, 0.022, 0.030), 1, lumps=0.08)
        m.pop()
    # every kind has a moustache
    for sx in (-1, 1):
        m.loft([[hp((sx * (0.002 + 0.030 * t), 1.643 - 0.010 * t * t + dy, 0.103 - 0.018 * t * t + dz))
                 for dy, dz in ((0.007, 0), (0, 0.006), (-0.006, 0), (0, -0.004))] for t in (0.0, 0.35, 0.7, 1.0)], True, 0)
    bake_ao(m, rays=16, dist=0.03, strength=0.45, floor=0.65)
    return m.finish_skinned(col, arm)


def hands(arm, col):
    m = Skinned("hu_hands", kit.HB)
    m.surface("skin", WHITE, 0.0)
    for side, sx in (("L", -1), ("R", 1)):
        w = J["Hand" + side]
        m.set_part(SKIN, lambda co, s=side: {"Hand" + s: 1.0})
        inward = Vector((-sx, 0, 0))                                       # the palm faces the thigh
        down = Vector((0, -1, 0))
        # wrist stub up into the cuff, then the palm: a rounded slab, thin across, broad front to back
        limb(m, [((w[0], w[1] + 0.045, w[2]), 0.026), ((w[0], w[1] + 0.005, w[2]), 0.025), ((w[0], w[1] - 0.02, w[2] + 0.004), 0.027)], 10)
        palm = [(w[1] - 0.010, 0.029, 0.019, 0.004), (w[1] - 0.040, 0.041, 0.018, 0.008), (w[1] - 0.068, 0.044, 0.016, 0.006),
                (w[1] - 0.090, 0.042, 0.0145, 0.004)]
        m.loft([ring(w[0] - sx * 0.002, y, w[2] + dz, hx, hz, 12, 2.4) for y, hz, hx, dz in palm], True, 1)
        for i, (ln, r, curl) in enumerate(((0.071, 0.0092, 0.85), (0.079, 0.0095, 0.95), (0.074, 0.0091, 1.05), (0.060, 0.0082, 1.15))):
            base = Vector((w[0] - sx * 0.002, w[1] - 0.088, w[2] + 0.031 - i * 0.0192))
            pts, radii, p, th = [base], [r], base, 0.0
            for seg, (frac, bend) in enumerate(((0.46, 0.22), (0.30, 0.42), (0.24, 0.36))):
                th += bend * curl
                d = down * math.cos(th) + inward * math.sin(th)
                p = p + d * (ln * frac)
                pts.append(p)
                radii.append(r * (0.93 - 0.08 * seg))
            pts.append(p + d * (radii[-1] * 0.55))                           # the tip rounds off
            radii.append(radii[-1] * 0.5)
            m.tube([L(*q) for q in pts], r, 7, radii=radii)
        t0 = Vector((w[0] - sx * 0.008, w[1] - 0.030, w[2] + 0.030))          # the thumb lies along the front of the hand
        thumb = [t0, t0 + Vector((-sx * 0.010, -0.025, 0.012)), t0 + Vector((-sx * 0.018, -0.048, 0.014)), t0 + Vector((-sx * 0.024, -0.066, 0.010))]
        tip = thumb[-1] + (thumb[-1] - thumb[-2]).normalized() * 0.0045
        m.tube([L(*q) for q in thumb + [tip]], 0.0105, 8, radii=[0.0108, 0.0098, 0.0090, 0.0080, 0.0042])
    bake_ao(m, rays=16, dist=0.025, strength=0.6, floor=0.55)
    return m.finish_skinned(col, arm)


def sleeves(m, grow=0.0, part_low=CUFF, part_up=SHIRT, cuff=True):
    for side, sx in (("L", -1), ("R", 1)):
        s, e, w = J["Arm" + side], J["Fore" + side], J["Hand" + side]
        g = grow
        start = len(m.bm.verts)
        m.set_part(part_up, w_arm(side))
        # the shoulder is rounded over: rings closing in towards the neck, not the open end of a pipe
        limb(m, [((s[0] - sx * 0.050, s[1] + 0.046, 0), 0.006), ((s[0] - sx * 0.036, s[1] + 0.043, 0), 0.026 + g * 0.5),
                 ((s[0] - sx * 0.020, s[1] + 0.032, 0), 0.042 + g), ((s[0] - sx * 0.006, s[1] + 0.012, 0), 0.052 + g), ((s[0], s[1] - 0.02, 0), 0.056 + g),
                 (lerp3(s, e, 0.2), 0.054 + g), (lerp3(s, e, 0.55), 0.050 + g), (lerp3(s, e, 0.88), 0.044 + g), (e, 0.042 + g)], 12, step=0.012)
        m.set_part(part_low, w_arm(side))
        st = [(e, 0.042 + g), (lerp3(e, w, 0.15), 0.041 + g), (lerp3(e, w, 0.5), 0.037 + g), (lerp3(e, w, 0.82), 0.032 + g)]
        if cuff:
            st += [(lerp3(e, w, 0.84), 0.035 + g), (lerp3(e, w, 0.97), 0.034 + g)]
        limb(m, st, 12, step=0.012)
        folds(m, start, lerp3(s, e, 0.60), lerp3(e, w, 0.32), 3.5, 0.0035 + g * 0.1, bias=(0, 0, 1), seed=sx)     # the crook of the elbow
        folds(m, start, lerp3(s, e, 0.10), lerp3(s, e, 0.45), 1.5, 0.0022, bias=(0, 0, -1), seed=sx * 4.0)      # pulled across the back
        folds(m, start, lerp3(e, w, 0.50), lerp3(e, w, 0.83), 2.5, 0.0028, seed=sx * 2.0)                         # gathered above the cuff


def shirt(arm, col):
    m = Skinned("hu_shirt", kit.HB)
    m.surface("cloth", WHITE, 0.0).set_part(SHIRT, w_torso)
    torso_shell(m, 1.03, 1.515, 0.004, cap=True)
    folds(m, 0, (0, 1.025, 0), (0, 1.17, 0), 2.5, 0.0032, seed=0.7)                     # bunched above the belt
    sleeves(m)
    m.set_part(SHIRT, w_torso)
    # collar: a band and two points
    m.loft([ring(0, 1.508, 0.004, 0.066, 0.064, 14), ring(0, 1.548, 0.006, 0.060, 0.060, 14), ring(0, 1.552, 0.006, 0.056, 0.056, 14)], False, 1)
    for sx in (-1, 1):
        m.raw([L(sx * 0.004, 1.545, 0.066), L(sx * 0.052, 1.552, 0.040), L(sx * 0.050, 1.492, 0.072), L(sx * 0.008, 1.478, 0.086)],
              [(0, 1, 2, 3)], "z", double=True)
    # the placket, one strip down the front; the yoke seam; two pockets with pointed flaps
    m.surface("cloth", (0.93, 0.93, 0.93), 0.0)
    ys = [1.05 + (1.475 - 1.05) * q / 12 for q in range(13)]
    ribbon(m, [L(0, y, front_z(y, 0.0065)) for y in ys], [0.03] * 13, L(1, 0, 0))
    m.surface("cloth", (0.78, 0.78, 0.78), 0.0)
    yoke = []
    for q in range(25):
        x = -0.17 + 0.34 * q / 24
        y = 1.428 - 0.016 * max(0.0, 1.0 - abs(abs(x) - 0.085) / 0.05)
        yoke.append(L(x, y, torso_front(x, y, 0.0062)))
    m.tube(yoke, 0.0017, 4)
    for sx in (-1, 1):
        m.surface("cloth", (0.90, 0.90, 0.90), 0.0)
        rows = []
        for y, drop in ((1.372, 0.0), (1.336, 1.0)):
            row = []
            for q in range(5):
                x = sx * (0.045 + 0.08 * q / 4)
                yy = y - drop * 0.012 * (1.0 - abs(q - 2) / 2.0)                 # the flap comes to a point
                row.append(L(x, yy, torso_front(x, yy, 0.009)))
            rows.append(row)
        sheet(m, rows, outward=L(0, 0, 1), double=True)
        m.surface("plain", (0.86, 0.82, 0.72), 0.0).set_part(0, w_torso)
        bx, by = sx * 0.085, 1.326
        m.cyl(L(bx, by, torso_front(bx, by, 0.010)), L(bx, by, torso_front(bx, by, 0.0145)), 0.0052, 0.0046, 8, 0.001)
        m.set_part(SHIRT, w_torso)
    m.surface("plain", (0.86, 0.82, 0.72), 0.0).set_part(0, w_torso)
    for y in (1.09, 1.165, 1.24, 1.315, 1.39, 1.455):
        z = front_z(y, 0.008)
        m.cyl(L(0, y, z), L(0, y, z + 0.004), 0.0065, 0.0055, 8, 0.001)
    bake_ao(m, rays=20, dist=0.06, strength=0.75, floor=0.5)
    return m.finish_skinned(col, arm)


def trousers(arm, col):
    m = Skinned("hu_trousers", kit.HB)
    m.surface("denim", WHITE, 0.0).set_part(PANTS, w_pelvis)
    rows = [(0.835, 0.070, 0.070), (0.87, 0.128, 0.094), (0.92, 0.160, 0.105), (0.97, 0.165, 0.107), (1.02, 0.160, 0.104), (1.065, 0.153, 0.100)]
    m.loft([ring(0, y, 0, rx, rz, 18, 2.3, 0.008, y * 7) for y, rx, rz in rows], True, 1, wrap=(0, 0, 1))
    for side, sx in (("L", -1), ("R", 1)):
        h, k, a = J["Thigh" + side], J["Shin" + side], J["Foot" + side]
        m.set_part(PANTS, w_leg(side))
        start = len(m.bm.verts)
        st = [((h[0], h[1] + 0.01, 0), 0.088), (lerp3(h, k, 0.2), 0.086), (lerp3(h, k, 0.55), 0.075), (lerp3(h, k, 0.88), 0.062),
              (k, 0.059), (lerp3(k, a, 0.14), 0.056), (lerp3(k, a, 0.34), 0.055)]
        limb(m, st, 14, step=0.013)
        m.surface("denim", (0.70, 0.70, 0.70), 0.0)                       # the outside seam, before the folds so it follows them
        m.tube([L(c[0] + sx * (r + 0.0012), c[1], c[2]) for c, r in resample(st, 0.03)[1:]], 0.0016, 4)
        m.surface("denim", WHITE, 0.0)
        folds(m, start, lerp3(h, k, 0.72), lerp3(k, a, 0.10), 3.0, 0.0042, bias=(0, 0, -1), seed=sx)         # behind the knee
        folds(m, start, lerp3(h, k, 0.80), lerp3(k, a, 0.06), 2.0, 0.0024, bias=(0, 0, 1), seed=sx * 3.0)    # and a little in front
        folds(m, start, lerp3(k, a, 0.08), lerp3(k, a, 0.32), 2.5, 0.0045, seed=sx * 5.0)                     # gathered over the boot
        folds(m, start, lerp3(h, k, 0.05), lerp3(h, k, 0.40), 1.5, 0.0025, bias=(-sx, 0, 0.6), seed=sx * 7.0)  # the inside of the thigh
    m.set_part(PANTS, w_pelvis)
    m.surface("denim", (0.70, 0.70, 0.70), 0.0)
    fly = [L(0.014, y, 0.106 - 0.002 * (1.0 - y)) for y in (1.02, 0.99, 0.96, 0.93, 0.905)] + [L(0.008, 0.888, 0.104), L(0.0, 0.880, 0.102)]
    m.tube(fly, 0.0016, 4)
    m.surface("denim", (0.92, 0.92, 0.92), 0.0)
    for sx in (-1, 1):                                                     # back pockets
        rows = []
        for y, drop in ((1.005, 0.0), (0.925, 1.0)):
            row = []
            for q in range(5):
                x = sx * (0.035 + 0.075 * q / 4)
                yy = y - drop * 0.012 * (1.0 - abs(q - 2) / 2.0)
                rx, rz = 0.165, 0.107
                t = min(0.97, abs(x) / (rx + 0.004))
                row.append(L(x, yy, -(rz + 0.004) * (1.0 - t ** 2.3) ** (1 / 2.3)))
            rows.append(row)
        sheet(m, rows, outward=L(0, 0, -1), double=True)
    # belt, its loops and the buckle
    m.surface("leather", Pal.LeatherDark, 0.03).set_part(0, w_torso)
    m.loft([ring(0, 1.030, 0, 0.164, 0.110, 18), ring(0, 1.034, 0, 0.167, 0.113, 18), ring(0, 1.066, 0, 0.160, 0.109, 18), ring(0, 1.070, 0, 0.157, 0.106, 18)], False, 0)
    m.surface("denim", (0.85, 0.85, 0.85), 0.0).set_part(PANTS, w_pelvis)
    for a in (0.62, 1.05, 2.09, 2.52, 3.77, 4.71, 5.65):
        x, z = 0.171 * math.cos(a), 0.117 * math.sin(a)
        gbox(m, (x, 1.050, z), (0.012 if abs(math.sin(a)) > 0.5 else 0.005, 0.052, 0.012 if abs(math.sin(a)) <= 0.5 else 0.005), 0.001)
    m.surface("steel", Pal.Brass, 0.0).set_part(0, w_torso)
    m.box(L(0, 1.050, 0.114), (0.046, 0.008, 0.040), 0.003)
    bake_ao(m, rays=20, dist=0.06, strength=0.75, floor=0.5)
    return m.finish_skinned(col, arm)


def boots(arm, col):
    m = Skinned("hu_boots", kit.HB)
    m.surface("leather", WHITE, 0.03)
    for side, sx in (("L", -1), ("R", 1)):
        k, a = J["Shin" + side], J["Foot" + side]
        m.set_part(BOOTS, w_leg(side))
        start = len(m.bm.verts)
        limb(m, [(lerp3(k, a, 0.27), 0.063), (lerp3(k, a, 0.30), 0.064), (lerp3(k, a, 0.5), 0.057), (lerp3(k, a, 0.74), 0.050), ((a[0], a[1] + 0.03, a[2]), 0.046)], 14, step=0.012)
        folds(m, start, lerp3(k, a, 0.60), (a[0], a[1] + 0.04, a[2]), 2.5, 0.0028, bias=(0, 0, 1), seed=sx * 4.0)    # creased at the ankle
        foot = [(-0.078, 0.046, 0.034, 0.045), (-0.045, 0.058, 0.042, 0.056), (0.0, 0.060, 0.046, 0.058), (0.07, 0.044, 0.049, 0.042),
                (0.135, 0.034, 0.047, 0.032), (0.185, 0.027, 0.036, 0.025), (0.205, 0.022, 0.020, 0.018)]
        rings = []
        for z, y, rx, ry in foot:
            rings.append([L(a[0] + rx * math.cos(t), y + ry * math.sin(t), a[2] + z) for t in [i * 2 * math.pi / 10 for i in range(10)]])
        m.loft(rings, True, 1)
        top = lerp3(k, a, 0.27)
        m.surface("leather", (0.78, 0.78, 0.78), 0.03)                       # a band round the top and two pull straps
        m.loft([ring(top[0], top[1] - 0.004, top[2], 0.0655, 0.0655, 14), ring(top[0], top[1] + 0.012, top[2], 0.0655, 0.0655, 14)], False, 0)
        for sz in (-1, 1):
            gbox(m, (top[0], top[1] + 0.022, top[2] + sz * 0.064), (0.016, 0.05, 0.004), 0.001)
        m.set_part(0, w_leg(side))
        m.surface("leather", (0.16, 0.12, 0.09), 0.0)
        m.box(L(a[0], 0.014, a[2] - 0.045), (0.066, 0.062, 0.028), 0.004)        # heel
        m.box(L(a[0], 0.006, a[2] + 0.075), (0.084, 0.21, 0.012), 0.004)         # sole
        m.surface("leather", WHITE, 0.03)
    bake_ao(m, rays=16, dist=0.05, strength=0.7, floor=0.5)
    return m.finish_skinned(col, arm)


def vest_open(y):
    """Half the angle the waistcoat stands open at the front at height y: buttoned low, a V at the chest."""
    return math.radians(5.0 + 24.0 * sm(1.18, 1.47, y))


def vest(arm, col):
    m = Skinned("hu_vest", kit.HB)
    m.surface("wool", WHITE, 0.0).set_part(VEST, w_torso)
    rows = []
    for i in range(14):
        y = 1.045 + (1.47 - 1.045) * i / 13
        rx, rz, z = torso_at(y)
        o = vest_open(y)
        rows.append(arc(0, y, z, rx + 0.013, rz + 0.013, math.pi / 2 + o, math.pi / 2 + 2 * math.pi - o, 26, SQ, 0.006, y * 5))
    sheet(m, rows, outward=L(0, 0, 1), double=True)
    m.surface("wool", (0.62, 0.62, 0.62), 0.0)                              # binding along the edges and the hem
    for edge in ([r[0] for r in rows], [r[-1] for r in rows], rows[0]):
        m.tube([outward(p, 0.0015) for p in edge], 0.0042, 5)
    m.surface("wool", (0.45, 0.45, 0.45), 0.0)                              # welt pockets
    for x, y in ((-0.09, 1.13), (0.09, 1.13), (-0.085, 1.335)):
        gbox(m, (x, y, torso_front(x, y, 0.0155)), (0.052, 0.007, 0.004), 0.001)
    m.surface("plain", (0.75, 0.7, 0.6), 0.0).set_part(0, w_torso)          # buttons on the right edge
    for y in (1.07, 1.115, 1.16):
        x = 0.155 * math.sin(vest_open(y)) * 0.6
        m.cyl(L(x, y, front_z(y, 0.016)), L(x, y, front_z(y, 0.021)), 0.0068, 0.006, 8, 0.001)
    m.surface("steel", Pal.Brass, 0.0)                                      # a watch chain from a button to the left pocket
    chain = []
    for q in range(11):
        t = q / 10
        x = 0.012 - 0.10 * t
        y = 1.16 - 0.03 * t - 0.035 * math.sin(math.pi * t)
        chain.append(L(x, y, torso_front(x, y, 0.019)))
    m.tube(chain, 0.0018, 4)
    bake_ao(m, rays=20, dist=0.06, strength=0.75, floor=0.5)
    return m.finish_skinned(col, arm)


def gunbelt(arm, col):
    m = Skinned("hu_gunbelt", kit.HB)
    m.surface("leather", Pal.Leather, 0.03).set_part(0, lambda co: {"Hips": 1.0})
    # worn low and slanted: lower on the right hip, where the holster hangs
    def r(y, x_off, grow):
        return [L(p[0] + x_off, y - 0.035 * p[0] / 0.17, p[2]) for p in
                [(-(v.x), 0, -(v.y)) for v in ring(0, 0, 0, 0.176 + grow, 0.122 + grow, 20)]]
    m.loft([r(0.955, 0, 0.0), r(0.957, 0, 0.004), r(1.003, 0, 0.004), r(1.005, 0, 0.0)], False, 0)
    m.surface("steel", Pal.Brass, 0.0)
    for i in range(9):                                                      # cartridges across the back
        a = math.radians(205 + i * 16)
        x, z = 0.178 * math.cos(a), 0.124 * math.sin(a)
        y = 0.98 - 0.035 * x / 0.17
        m.cyl(L(x * 1.02, y - 0.02, z * 1.02), L(x * 1.02, y + 0.022, z * 1.02), 0.0055, 0.0055, 6, 0.001)
    m.box(L(0, 0.975, 0.127), (0.05, 0.008, 0.045), 0.003)
    m.surface("leather", Pal.LeatherDark, 0.03).set_part(0, lambda co: {"ThighR": 0.6, "Hips": 0.4})
    hx = 0.178
    m.loft([[L(hx + dx, y, 0.018 + dz) for dx, dz in ((-0.022, -0.04), (0.02, -0.036), (0.022, 0.04), (-0.022, 0.044))]
            for y in (0.93, 0.86, 0.79)] + [[L(hx + dx * 0.6, 0.745, 0.018 + dz * 0.5) for dx, dz in ((-0.022, -0.04), (0.02, -0.036), (0.022, 0.04), (-0.022, 0.044))]], True, 1)
    bake_ao(m, rays=16, dist=0.04, strength=0.7, floor=0.5)
    return m.finish_skinned(col, arm)


def neckerchief(arm, col):
    m = Skinned("hu_bandana", kit.HB)
    m.surface("cloth", WHITE, 0.0).set_part(SCARF, w_torso)
    m.loft([ring(0, 1.492, 0.002, 0.086, 0.080, 14, 2.2, 0.03, 1.0), ring(0, 1.515, 0.004, 0.076, 0.072, 14, 2.2, 0.03, 2.0),
            ring(0, 1.542, 0.006, 0.064, 0.064, 14, 2.2, 0.02, 3.0)], False, 1)
    m.raw([L(-0.075, 1.505, 0.066), L(0.075, 1.505, 0.066), L(0.03, 1.45, 0.108), L(0.0, 1.385, 0.128), L(-0.03, 1.45, 0.108)],
          [(0, 1, 2, 3, 4)], "z", smooth=True, double=True)
    m.blob(L(0, 1.497, 0.082), (0.014, 0.010, 0.011), 1)                   # the knot
    bake_ao(m, rays=16, dist=0.04, strength=0.7, floor=0.5)
    return m.finish_skinned(col, arm)


def coat_open(y):
    return math.radians(9.0 + 21.0 * sm(1.24, 1.49, y))


def coat(arm, col):
    m = Skinned("hu_coat", kit.HB)
    m.surface("wool", WHITE, 0.0)

    def w(co):
        x, y, z = game(co)
        if y > 0.94:
            return w_torso(co)
        t = sm(0.94, 0.56, y) * 0.5
        return {"Hips": 1 - t, ("ThighL" if x < 0 else "ThighR"): t}
    m.set_part(COAT, w)
    stations = [(0.56, 0.222, 0.156, -0.010), (0.61, 0.217, 0.152, -0.009), (0.66, 0.212, 0.148, -0.008), (0.71, 0.207, 0.144, -0.006),
                (0.76, 0.202, 0.140, -0.005), (0.81, 0.196, 0.135, -0.004), (0.86, 0.190, 0.130, -0.002), (0.91, 0.183, 0.124, -0.001),
                (0.96, 0.176, 0.118, 0.0)]
    stations += [(y, rx + 0.02, rz + 0.02, z) for y, rx, rz, z in TORSO[1:8]] + [(1.49, 0.132, 0.092, 0.0)]
    rows = []
    for y, rx, rz, z in stations:
        o = coat_open(y)
        wob = 0.008 if y > 0.96 else 0.008 + 0.022 * sm(0.96, 0.6, y)          # the skirt hangs in folds
        rows.append(arc(0, y, z, rx, rz, math.pi / 2 + o, math.pi / 2 + 2 * math.pi - o, 30, SQ, wob, y * 4))
    sheet(m, rows, outward=L(0, 0, 1), double=True)
    sleeves(m, 0.012, COAT, COAT, cuff=False)
    m.set_part(COAT, w_torso)
    m.surface("wool", (0.84, 0.84, 0.84), 0.0)
    m.loft([ring(0, 1.488, 0.0, 0.094, 0.086, 14), ring(0, 1.548, -0.004, 0.078, 0.076, 14), ring(0, 1.553, -0.006, 0.090, 0.086, 14)], False, 1)
    m.surface("wool", (0.88, 0.88, 0.88), 0.0)
    for sx in (-1, 1):
        rows = []                                                           # the lapel, folded back off the front edge
        for q in range(7):
            y = 1.27 + (1.485 - 1.27) * q / 6
            rx, rz, z = torso_at(y)
            o = coat_open(y)
            width = math.radians(26.0) * sm(1.27, 1.43, y) * (0.75 if y > 1.455 else 1.0)
            a0 = math.pi / 2 - sx * o
            row = arc(0, y, z, rx + 0.026, rz + 0.026, a0, a0 - sx * max(width, 0.02), 4, SQ)
            rows.append(row)
        sheet(m, rows, outward=L(0, 0, 1), double=True)
        rows = []                                                           # a pocket flap at the hip
        for y in (0.995, 0.955):
            rx, rz, z = torso_at(0.96)
            a0 = math.pi / 2 - sx * math.radians(32)
            rows.append(arc(0, y, -0.0, 0.176 + 0.027, 0.118 + 0.027, a0, a0 - sx * math.radians(30), 5, SQ))
        sheet(m, rows, outward=L(0, 0, 1), double=True)
    m.surface("plain", (0.22, 0.18, 0.15), 0.0).set_part(0, w_torso)        # horn buttons down the right edge
    for y in (1.02, 1.12, 1.22):
        rx, rz, z = torso_at(y)
        o = coat_open(y) + math.radians(4.0)
        a = math.pi / 2 - o
        p = arc(0, y, z, rx + 0.024, rz + 0.024, a, a, 1, SQ)[0]
        g = Vector(game(p))
        d = Vector((g.x, 0, g.z)).normalized()
        m.cyl(L(*(g)), L(*(g + d * 0.005)), 0.0085, 0.0075, 8, 0.001)
    bake_ao(m, rays=20, dist=0.07, strength=0.75, floor=0.5)
    return m.finish_skinned(col, arm)


def skirt(arm, col):
    m = Skinned("hu_skirt", kit.HB)
    m.surface("cloth", WHITE, 0.0)

    def w(co):
        x, y, z = game(co)
        if y > 0.94:
            return w_torso(co)
        t = sm(0.94, 0.45, y) * 0.35
        return {"Hips": 1 - t, ("ThighL" if x < 0 else "ThighR"): t}
    m.set_part(SKIRT, w)
    rows = [(1.075, 0.152, 0.102), (1.02, 0.166, 0.118), (0.95, 0.196, 0.152), (0.82, 0.228, 0.190), (0.66, 0.262, 0.228), (0.48, 0.294, 0.262),
            (0.30, 0.320, 0.292), (0.13, 0.338, 0.316)]
    m.loft([ring(0, y, 0, rx, rz, 24, 2.0, 0.02 + 0.05 * (1.075 - y), y * 6) for y, rx, rz in rows], False, 1, wrap=(0, 0, 1))
    m.surface("cloth", (0.82, 0.82, 0.82), 0.0)
    m.loft([ring(0, 0.13, 0, 0.338, 0.316, 24, 2.0, 0.067, 0.78), ring(0, 0.10, 0, 0.342, 0.320, 24, 2.0, 0.07, 0.6)], False, 0)
    bake_ao(m, rays=16, dist=0.08, strength=0.7, floor=0.5)
    return m.finish_skinned(col, arm)


# ------------------------------------------------------------------ hats (rigid, worn on the head bone)
def hat(kind):
    m = Model("hat_" + kind)
    if kind == "straw":
        m.surface("straw", WHITE, 0.03)
    else:
        m.surface("wool", WHITE, 0.0)
    m.wind = 11 / 255.0                                                    # part 11: the hat's own colour
    band = lambda: setattr(m, "wind", 0.0)
    G = lambda r, y: (r, y)
    if kind == "cowboy":
        m.lathe((0, 0, 0), [G(0.218, 0.004), G(0.214, 0.0), G(0.18, 0.0), G(0.14, 0.0), G(0.096, 0.0), G(0.094, 0.012), G(0.090, 0.075), G(0.080, 0.118), G(0.052, 0.128), G(0.0, 0.124)], 44)
        m.lathe((0, 0, 0), [G(0.094, 0.012), G(0.14, 0.010), G(0.18, 0.009), G(0.214, 0.008), G(0.218, 0.004)][::-1], 44)
        moves = [(Vector((0, 0, 0.128)), 0.07, Vector((0, 0, -0.026)))]
        for sx in (-1, 1):
            moves += [(Vector((sx * 0.21, 0, 0.0)), 0.16, Vector((0, 0, 0.06))), (Vector((sx * 0.062, -0.05, 0.11)), 0.05, Vector((-sx * 0.012, 0, -0.01)))]
        moves += [(Vector((0, -0.2, 0)), 0.1, Vector((0, 0, -0.016))), (Vector((0, 0.2, 0)), 0.1, Vector((0, 0, -0.01)))]
        m.sculpt(moves)
        band()
        m.surface("leather", Pal.LeatherDark, 0.0)
        m.lathe((0, 0, 0), [G(0.0965, 0.012), G(0.0975, 0.016), G(0.0955, 0.034), G(0.0935, 0.036)], 28)
    elif kind == "derby":
        m.lathe((0, 0, 0), [G(0.132, 0.012), G(0.128, 0.004), G(0.092, 0.0), G(0.090, 0.012), G(0.089, 0.07), G(0.078, 0.105), G(0.05, 0.122), G(0.0, 0.128)], 28)
        band()
        m.surface("cloth", Pal.ClothBlack, 0.0)
        m.lathe((0, 0, 0), [G(0.0915, 0.012), G(0.0925, 0.016), G(0.0915, 0.036), G(0.0905, 0.038)], 28)
    elif kind == "flatbrim":
        m.lathe((0, 0, 0), [G(0.20, 0.006), G(0.198, 0.0), G(0.094, 0.0), G(0.092, 0.012), G(0.090, 0.10), G(0.086, 0.108), G(0.0, 0.110)], 28)
        band()
        m.surface("leather", Pal.LeatherDark, 0.0)
        m.lathe((0, 0, 0), [G(0.0945, 0.012), G(0.0955, 0.016), G(0.0945, 0.03), G(0.0935, 0.032)], 28)
    elif kind == "kepi":
        m.lathe((0, 0, 0), [G(0.092, 0.0), G(0.090, 0.012), G(0.086, 0.075), G(0.080, 0.085), G(0.0, 0.08)], 24)
        band()
        m.surface("leather", Pal.LeatherDark, 0.0)
        m.raw([Vector((-0.07, -0.085, 0.004)), Vector((0.07, -0.085, 0.004)), Vector((0.05, -0.15, -0.012)), Vector((-0.05, -0.15, -0.012))], [(0, 1, 2, 3)], "y", double=True)
    elif kind == "straw":
        m.lathe((0, 0, 0), [G(0.235, -0.012), G(0.23, -0.016), G(0.094, 0.0), G(0.092, 0.012), G(0.088, 0.085), G(0.07, 0.108), G(0.0, 0.112)], 28)
        band()
        m.surface("cloth", Pal.ClothRed, 0.0)
        m.lathe((0, 0, 0), [G(0.0945, 0.012), G(0.0955, 0.016), G(0.0945, 0.034), G(0.0935, 0.036)], 28)
    elif kind == "bonnet":
        m.surface("cloth", WHITE, 0.0)
        m.push((0, 0.02, 0.0))
        m.blob((0, 0, 0.02), (0.098, 0.115, 0.105), 2, squash=0.5)
        m.pop()
        m.loft([[Vector((0.105 * math.cos(a), -0.06 - 0.075 * abs(math.sin(a)) * k, 0.02 + 0.105 * math.sin(a))) for a in
                 [math.radians(d) for d in range(-30, 211, 20)]] for k in (0.0, 1.0)], False, 0)
    return m


def build():
    kit.clear_default_scene()
    col = kit.collection("Human")
    arm = kit.make_armature("HumanRig", kit.HB, kit.HB_PARENT, J, col)
    parts = [head(arm, col), hands(arm, col), shirt(arm, col), trousers(arm, col), boots(arm, col), vest(arm, col), coat(arm, col),
             skirt(arm, col), gunbelt(arm, col), neckerchief(arm, col)]
    parts += [hair(arm, col, k) for k in ("short", "long", "balding", "bun")]
    parts += [beard(arm, col, k) for k in ("full", "moustache", "goatee")]
    hats = kit.collection("Hats")
    hat_objs = [bake_ao(hat(k), rays=16, dist=0.05, strength=0.7, floor=0.5).finish(hats) for k in ("cowboy", "derby", "flatbrim", "kepi", "straw", "bonnet")]
    kit.layout(hat_objs, 0.6, 6)
    for ob in hat_objs:
        ob.location.y += 1.2
    return parts, hat_objs


if "ARGS" in globals() or __name__ == "__main__":
    args = globals().get("ARGS", {})
    parts, hats = build()
    print(kit.stats(parts))
    print(kit.stats(hats))
    if not args.get("noexport"):
        print("exported", kit.export_skinned_fbx("Human", "human"))
        print("exported", kit.export_fbx("Hats", "hats"))
