"""Trees, bushes, desert plants, rocks and driftwood. Run inside Blender:
    Tools/blender/bmcp.py run Tools/blender/flora.py
Collection "Flora" -> Assets/Canaan/Models/flora.fbx. The colour alpha is the wind weight (0 trunk .. 1 leaves).

Trees grow as branching limbs (each fork thinner) with many small leaf clumps at the twig ends, darker in the
lap of the crown: cottonwoods by the water, Gambel oak in the northern hills, juniper and pinyon on the dry
slopes under the mesas. Desert plants: yucca (a rosette of sword leaves with a flower stalk), prickly pear (flat
pads with fruit), cholla (jointed arms), rabbitbrush, sagebrush. By the water: willow thickets, cattails, river
stones, bleached driftwood.
"""
import importlib
import math

import kit

importlib.reload(kit)
from kit import Model, Pal, hexcol, mixc, shade
from mathutils import Vector


def under_shade(m, first_vert):
    """Leaves in the lap of a clump are darker than those on top: tint the new vertices by where they face."""
    m.bm.normal_update()
    m.bm.verts.index_update()
    col = m.col
    for f in m.bm.faces:
        if f.verts[0].index < first_vert:
            continue
        k = 0.62 + 0.38 * max(0.0, min(1.0, f.normal.z * 0.6 + 0.55))
        for lp in f.loops:
            c = lp[col]
            lp[col] = (c[0] * k, c[1] * k, c[2] * k, c[3])


def clump(m, centre, radius, flat, tint, subdiv=1, lumps=0.34, wind=1.0, surface="foliage"):
    """A lump of leaves, darker underneath. subdiv 0 for the small tufts of bushes (they stand in thousands)."""
    m.surface(surface, tint, 0.14)
    m.wind = wind
    first = len(m.bm.verts)
    r = m.rng
    m.blob(centre, (radius * r.uniform(0.85, 1.15), radius * r.uniform(0.85, 1.15), radius * flat), subdiv, lumps=lumps)
    under_shade(m, first)


# ---------------------------------------------------------------------- trees

def crown(m, stems, centre, radii, count, size, leaves, bark, base, flat=0.72, wind_twig=0.25, inner=0.35, lumps=0.28):
    """Fills a crown: leaf clumps scattered over an ellipsoid (most near its skin, some inside for body, none below
    'base'), each joined by a twig to the nearest point of the stems (lists of points: trunk and main limbs)."""
    r = m.rng
    pts = [p for stem in stems for p in stem]
    cx, cy, cz = centre
    rx, ry, rz = radii
    placed = 0
    tries = 0
    while placed < count and tries < count * 20:
        tries += 1
        u, v = r.uniform(-1, 1), r.uniform(0, 2 * math.pi)
        rho = math.sqrt(1 - u * u)
        shell = r.uniform(0.7, 1.0) if r.random() > inner else r.uniform(0.2, 0.6)
        p = Vector((cx + rho * math.cos(v) * rx * shell, cy + rho * math.sin(v) * ry * shell, cz + u * rz * shell))
        if p.z < base:
            continue
        near = min(pts, key=lambda q: (q - p).length)
        mid = (near + p) * 0.5 + Vector((0, 0, (p - near).length * 0.12))
        m.surface("bark", bark, 0.07)
        m.wind = wind_twig
        rr = 0.02 + 0.03 * min(1.0, (p - near).length / 3.0)
        m.tube([near, mid, p], rr, 5, radii=[rr, rr * 0.6, rr * 0.25])
        k = r.uniform(0.75, 1.2) * (0.85 if shell < 0.65 else 1.0)
        clump(m, p, size * k, flat, leaves[placed % len(leaves)], lumps=lumps)
        if not hasattr(m, "lod_clumps"):
            m.lod_clumps = []
        m.lod_clumps.append((p.copy(), size * k, flat, leaves[placed % len(leaves)]))
        placed += 1


def stem(m, start, direction, length, radius, bark, bend=0.12, wind=0.03, segs=5):
    """A trunk or a main limb, gently curved; returns its points."""
    r = m.rng
    d = Vector(direction).normalized()
    side = Vector((r.uniform(-1, 1), r.uniform(-1, 1), 0.0)) * bend * length
    pts = []
    for i in range(segs):
        t = i / (segs - 1)
        pts.append(start + d * (length * t) + side * math.sin(math.pi * t))
    m.surface("bark", bark, 0.07)
    m.wind = wind
    m.tube(pts, radius, 9, radii=[radius * (1.0 - 0.55 * i / (segs - 1)) for i in range(segs)])
    if not hasattr(m, "lod_stems"):
        m.lod_stems = []
    m.lod_stems.append(([p.copy() for p in pts], radius, bark, wind))
    return pts


def roots(m, radius, bark, n=4):
    r = m.rng
    m.surface("bark", bark, 0.07)
    m.wind = 0.0
    for k in range(n):
        a = k * 2 * math.pi / n + r.uniform(-0.4, 0.4)
        m.tube([Vector((0, 0, 0.4)), Vector((math.cos(a) * radius * 1.6, math.sin(a) * radius * 1.6, 0.06)),
                Vector((math.cos(a) * radius * 2.5, math.sin(a) * radius * 2.5, -0.1))], radius * 0.4, 6, radii=[radius * 0.5, radius * 0.35, radius * 0.1])


def cottonwood(name, seed, height, spread, leaves):
    """A cottonwood: a stout trunk forking low into a few rising limbs under a broad, broken, rounded crown."""
    m = Model(name, seed)
    r = m.rng
    bark = Pal.BarkPale
    rad = 0.12 + 0.34 * height / 9.0
    fork = height * r.uniform(0.26, 0.32)
    tr = stem(m, Vector((0, 0, -0.1)), (r.uniform(-0.08, 0.08), r.uniform(-0.08, 0.08), 1), fork, rad, bark, 0.05, 0.02)
    roots(m, rad, bark)
    stems = [tr]
    n = r.choice((2, 3, 3))
    for k in range(n):
        a = 2 * math.pi * k / n + r.uniform(-0.5, 0.5)
        d = (Vector((math.cos(a), math.sin(a), 0)) * 0.5 * spread + Vector((0, 0, 1))).normalized()
        stems.append(stem(m, tr[-1], d, height * r.uniform(0.42, 0.52), rad * 0.6, bark, 0.15, 0.06))
    c = Vector((0, 0, height * 0.62))
    crown(m, stems, c, (height * 0.4 * spread, height * 0.38 * spread, height * 0.3), int(30 + height * 2.2), height * 0.095, leaves, bark, fork + 0.6, 0.66, 0.25, 0.4)
    return m


def oak(name, seed):
    """Gambel oak: short, crooked, many-stemmed, a dense rounded crown."""
    m = Model(name, seed)
    r = m.rng
    bark = Pal.Bark
    stems = []
    for k in range(3):
        a = k * 2.1 + r.uniform(-0.4, 0.4)
        d = (Vector((math.cos(a), math.sin(a), 0)) * 0.35 + Vector((0, 0, 1))).normalized()
        stems.append(stem(m, Vector((math.cos(a) * 0.12, math.sin(a) * 0.12, -0.05)), d, r.uniform(2.2, 3.0), 0.12, bark, 0.2))
    leaves = [hexcol(0x5A7038), hexcol(0x3F5528), mixc(hexcol(0x5A7038), hexcol(0x3F5528), 0.5)]
    crown(m, stems, Vector((0, 0, 2.7)), (1.9, 1.8, 1.35), 30, 0.55, leaves, bark, 1.2)
    return m


def juniper(name, seed):
    """Utah juniper: two or three twisted stems from the ground, shaggy pale bark, a ragged crown of blue-green
    tufts from low down, a silver dead limb."""
    m = Model(name, seed)
    r = m.rng
    bark = hexcol(0x8C7E6C)
    stems = []
    for k in range(r.choice((2, 3))):
        a = k * 2.3 + r.uniform(-0.5, 0.5)
        d = (Vector((math.cos(a), math.sin(a), 0)) * r.uniform(0.35, 0.7) + Vector((0, 0, 1))).normalized()
        stems.append(stem(m, Vector((0, 0, -0.05)), d, r.uniform(1.6, 2.6), 0.12, bark, 0.3))
    foliage = [hexcol(0x6E7F5E), hexcol(0x5C6E50), hexcol(0x7D8C66)]
    crown(m, stems, Vector((r.uniform(-0.3, 0.3), r.uniform(-0.3, 0.3), 1.9)), (1.5, 1.4, 1.4), 26, 0.36, foliage, bark, 0.45, 0.78, 0.2, 0.25, 0.42)
    m.surface("timber", hexcol(0xB4ACA0), 0.06)
    m.wind = 0.05
    a = r.uniform(0, 6.28)
    m.tube([Vector((0, 0, 0.6)), Vector((math.cos(a) * 0.8, math.sin(a) * 0.8, 1.6)), Vector((math.cos(a) * 1.3, math.sin(a) * 1.3, 2.6))], 0.05, 6,
           radii=[0.06, 0.04, 0.012])
    return m


def pinyon(name, seed):
    """Pinyon pine: a short trunk under a dense, rounded crown of dark tufts."""
    m = Model(name, seed)
    r = m.rng
    bark = hexcol(0x6A5644)
    tr = stem(m, Vector((0, 0, -0.05)), (0, 0, 1), 3.6, 0.17, bark, 0.06)
    roots(m, 0.17, bark, 3)
    foliage = [hexcol(0x4C5E36), hexcol(0x3E5030), hexcol(0x5A6C40)]
    crown(m, [tr], Vector((0, 0, 2.9)), (1.7, 1.7, 1.9), 30, 0.5, foliage, bark, 1.0, 0.8, 0.15, 0.3, 0.38)
    return m


def dead_tree(name, seed):
    m = Model(name, seed)
    r = m.rng
    m.surface("timber", Pal.WoodGrey, 0.08)
    m.wind = 0.0
    pts = [Vector((0.15 * math.sin(i * 0.9), 0.1 * math.cos(i * 1.3), i * 0.75)) for i in range(7)]
    m.tube(pts, 0.2, 9, radii=[0.30, 0.22, 0.19, 0.16, 0.12, 0.08, 0.03])
    for k in range(6):
        a = k * 1.1 + r.uniform(-0.3, 0.3)
        start = pts[2 + k % 4]
        ln = r.uniform(1.0, 2.0)
        mid = start + Vector((math.cos(a) * ln * 0.55, math.sin(a) * ln * 0.55, ln * 0.2))
        end = start + Vector((math.cos(a) * ln, math.sin(a) * ln, ln * r.uniform(0.3, 0.8)))
        m.tube([start, mid, end], 0.05, 6, radii=[0.075, 0.05, 0.012])
        tw = end + Vector((math.cos(a + 0.8) * 0.4, math.sin(a + 0.8) * 0.4, 0.35))
        m.tube([mid, (mid + tw) * 0.5 + Vector((0, 0, 0.1)), tw], 0.02, 5, radii=[0.03, 0.02, 0.006])
    return m


# ---------------------------------------------------------------------- bushes

def sagebrush(name, seed, tint, size=0.9):
    """Twisted woody stems under many small silvery tufts."""
    m = Model(name, seed)
    r = m.rng
    for k in range(6):
        a = k * 1.05 + r.uniform(-0.3, 0.3)
        m.surface("timber", shade(Pal.Bark, 1.15), 0.05)
        m.wind = 0.25
        m.tube([Vector((0, 0, -0.02)), Vector((math.cos(a) * size * 0.2, math.sin(a) * size * 0.2, size * 0.35)),
                Vector((math.cos(a) * size * 0.4, math.sin(a) * size * 0.4, size * 0.62))], 0.014, 5, radii=[0.025, 0.016, 0.006])
    for k in range(12):
        a = k * 0.53 + r.uniform(-0.3, 0.3)
        d = size * r.uniform(0.08, 0.48)
        clump(m, Vector((math.cos(a) * d, math.sin(a) * d, size * r.uniform(0.32, 0.72))), size * r.uniform(0.17, 0.27), 0.78,
              shade(tint, r.uniform(0.82, 1.12)), subdiv=0, lumps=0.3, wind=0.6)
    return m


def rabbitbrush(name, seed, size=0.85):
    """A rounded grey-green bush, its tops gold with flower."""
    m = Model(name, seed)
    r = m.rng
    green, gold = hexcol(0x8C9468), hexcol(0xC9A440)
    for k in range(10):
        a = k * 0.63 + r.uniform(-0.3, 0.3)
        d = size * r.uniform(0.05, 0.4)
        h = size * r.uniform(0.3, 0.62)
        clump(m, Vector((math.cos(a) * d, math.sin(a) * d, h)), size * r.uniform(0.2, 0.28), 0.8, shade(green, r.uniform(0.9, 1.1)), subdiv=0, lumps=0.3, wind=0.6)
        if r.random() < 0.8:
            clump(m, Vector((math.cos(a) * d, math.sin(a) * d, h + size * 0.2)), size * r.uniform(0.11, 0.16), 0.55,
                  shade(gold, r.uniform(0.9, 1.1)), subdiv=0, lumps=0.4, wind=0.8)
    return m


def willow(name, seed):
    """A coyote-willow thicket: a wide fan of many thin stems, feathery grey-green foliage in small tufts all
    along their upper halves, a rounded top."""
    m = Model(name, seed)
    r = m.rng
    leaves = [hexcol(0x7A8A5A), hexcol(0x6C7E50), hexcol(0x889A64)]
    tops = []
    for k in range(22):
        a = k * 2.39996 + r.uniform(-0.2, 0.2)
        lean = r.uniform(0.25, 0.6)
        h = r.uniform(1.6, 2.9)
        base = Vector((math.cos(a) * r.uniform(0.0, 0.4), math.sin(a) * r.uniform(0.0, 0.4), -0.05))
        top = base + Vector((math.cos(a) * h * lean, math.sin(a) * h * lean, h))
        m.surface("bark", hexcol(0x8A6446), 0.06)
        m.wind = 0.35
        m.tube([base, (base + top) * 0.5 + Vector((0, 0, 0.12)), top], 0.018, 4, radii=[0.026, 0.016, 0.006])
        tops.append((base, top))
    for i in range(46):
        base, top = tops[r.randrange(len(tops))]
        p = base + (top - base) * r.uniform(0.45, 1.0) + Vector((r.uniform(-0.15, 0.15), r.uniform(-0.15, 0.15), 0))
        clump(m, p, r.uniform(0.22, 0.36), 0.62, leaves[i % 3], subdiv=0, lumps=0.35, wind=1.0)
    return m


# ---------------------------------------------------------------------- desert plants

def strap(m, base, direction, length, width, droop, tint, wind):
    """A flat, pointed leaf (yucca, cattail) as a double-sided strip."""
    d = Vector(direction).normalized()
    side = d.cross(Vector((0, 0, 1)))
    if side.length < 1e-4:
        side = Vector((1, 0, 0))
    side.normalize()
    pts = []
    for i in range(5):
        t = i / 4.0
        p = base + d * (length * t) + Vector((0, 0, -droop * t * t * length))
        w = width * (1.0 - t) ** 0.7 * (0.6 + 0.4 * min(1.0, t * 4))
        pts.append((p - side * w * 0.5, p + side * w * 0.5))
    verts, faces = [], []
    for i, (a, b) in enumerate(pts):
        verts += [tuple(a), tuple(b)]
        if i:
            faces.append((2 * i - 2, 2 * i - 1, 2 * i + 1, 2 * i))
    m.surface("foliage", tint, 0.06)
    m.wind = wind
    m.raw(verts, faces, smooth=True, double=True)


def yucca(name, seed, stalk=True):
    """Soapweed yucca: a rosette of stiff sword leaves, dry ones hanging below, a stalk of creamy bells."""
    m = Model(name, seed)
    r = m.rng
    green, tip, dry = hexcol(0x667A66), hexcol(0x9AA680), hexcol(0x8A7656)      # the blue-grey green of yucca
    for i in range(64):
        a = i * 2.39996 + r.uniform(-0.1, 0.1)
        up = r.uniform(0.2, 1.0)
        d = Vector((math.cos(a) * (1.0 - up * 0.7), math.sin(a) * (1.0 - up * 0.7), up))
        strap(m, Vector((math.cos(a) * 0.05, math.sin(a) * 0.05, 0.1)), d, r.uniform(0.55, 0.85), 0.036, 0.04, mixc(green, tip, r.uniform(0, 0.35)), 0.12)
    for i in range(14):                                                 # the dry skirt
        a = i * 0.45 + r.uniform(-0.2, 0.2)
        d = Vector((math.cos(a), math.sin(a), -0.35))
        strap(m, Vector((0, 0, 0.12)), d, r.uniform(0.3, 0.45), 0.026, 0.1, dry, 0.05)
    if stalk:
        m.surface("timber", hexcol(0x8A7A50), 0.05)
        m.wind = 0.4
        h = r.uniform(1.3, 1.8)
        lean = Vector((r.uniform(-0.1, 0.1), r.uniform(-0.1, 0.1), 1)).normalized()
        m.tube([Vector((0, 0, 0.1)), lean * h * 0.5, lean * h], 0.015, 6, radii=[0.022, 0.016, 0.006])
        for k in range(14):
            t = 0.55 + k / 14 * 0.42
            p = lean * h * t
            a = k * 2.0
            m.surface("plain", hexcol(0xE6DEC4), 0.05)
            m.wind = 0.6
            m.blob(p + Vector((math.cos(a) * 0.06, math.sin(a) * 0.06, -0.02)), (0.028, 0.028, 0.035), 0, lumps=0.2)
    return m


def prickly_pear(name, seed):
    """Prickly pear: flat oval pads, each grown from the rim of another, a few red fruits on the top rims."""
    m = Model(name, seed)
    r = m.rng
    pad_c = [hexcol(0x6F8A48), hexcol(0x7C9452), hexcol(0x627A40)]
    pads = []

    def pad(c, yaw, tilt, size, colour):
        m.surface("foliage", colour, 0.08)
        m.wind = 0.0
        m.push(tuple(c), (tilt, 0, math.degrees(yaw)))
        m.blob((0, 0, 0), (size * 0.8, size * 0.16, size), 1, lumps=0.08)
        m.pop()
        pads.append((c, yaw, tilt, size))

    for k in range(3):                                                  # the ground pads
        a = k * 2.1 + r.uniform(-0.4, 0.4)
        pad(Vector((math.cos(a) * 0.22, math.sin(a) * 0.22, 0.17)), a + 1.57, r.uniform(-25, 25), 0.2, pad_c[k % 3])
    for k in range(9):
        c0, yaw0, tilt0, s0 = pads[r.randrange(len(pads))]
        a = yaw0 + r.uniform(-0.8, 0.8)
        up = Vector((math.cos(yaw0 - 1.57) * 0.25 * r.uniform(-1, 1), math.sin(yaw0 - 1.57) * 0.25 * r.uniform(-1, 1), 1)).normalized()
        c = c0 + up * s0 * 1.6
        if c.z > 1.1:
            continue
        pad(c, a, r.uniform(-30, 30), s0 * r.uniform(0.75, 0.95), pad_c[k % 3])
    m.surface("plain", hexcol(0x8E2A3E), 0.1)                          # tunas
    m.wind = 0.0
    for c, yaw, tilt, s in pads[-5:]:
        if r.random() < 0.7:
            m.blob(c + Vector((0, 0, s * 0.95)), (0.025, 0.025, 0.035), 0)
    return m


def cholla(name, seed):
    """Cholla: a short trunk and arms of jointed, spiny segments, yellow fruit at the tips."""
    m = Model(name, seed)
    r = m.rng
    body = hexcol(0x8E9A70)

    def joint(a, b, rad):
        m.surface("fur", body, 0.06)
        m.wind = 0.0
        mid = (a + b) * 0.5
        m.tube([a, mid, b], rad, 7, radii=[rad * 0.85, rad * 1.1, rad * 0.85])

    trunk_top = Vector((0, 0, 0.5))
    joint(Vector((0, 0, 0)), trunk_top, 0.07)
    ends = []

    def arm(p, d, depth):
        for i in range(r.randint(2, 3)):
            q = p + d * 0.22
            joint(p, q, 0.045)
            p = q
            d = (d + Vector((r.uniform(-0.3, 0.3), r.uniform(-0.3, 0.3), 0.35))).normalized()
        ends.append(p)
        if depth > 0:
            for k in range(2):
                a = r.uniform(0, 6.28)
                arm(p, (d + Vector((math.cos(a), math.sin(a), 0.2)) * 0.8).normalized(), depth - 1)

    for k in range(3):
        a = k * 2.1 + r.uniform(-0.3, 0.3)
        arm(trunk_top, Vector((math.cos(a), math.sin(a), 0.6)).normalized(), 1)
    m.surface("plain", hexcol(0xC9B24A), 0.1)
    for p in ends:
        if r.random() < 0.6:
            m.blob(p + Vector((0, 0, 0.03)), (0.03, 0.03, 0.025), 0)
    return m


def reeds(name, seed):
    """Cattails at the water's edge: a sheaf of long leaves and stalks with brown heads."""
    m = Model(name, seed)
    r = m.rng
    green = [hexcol(0x7C8C48), hexcol(0x6A7C40), hexcol(0x8E9A54)]
    for i in range(48):
        a = r.uniform(0, 6.28)
        rad = r.uniform(0.0, 0.4)
        base = Vector((math.cos(a) * rad, math.sin(a) * rad, -0.05))
        d = Vector((math.cos(a) * r.uniform(0.05, 0.3), math.sin(a) * r.uniform(0.05, 0.3), 1))
        strap(m, base, d, r.uniform(1.1, 1.9), 0.04, r.uniform(0.05, 0.2), green[i % 3], 1.0)
    for i in range(8):
        a = r.uniform(0, 6.28)
        rad = r.uniform(0.0, 0.3)
        base = Vector((math.cos(a) * rad, math.sin(a) * rad, -0.05))
        h = r.uniform(1.4, 1.9)
        top = base + Vector((r.uniform(-0.1, 0.1), r.uniform(-0.1, 0.1), h))
        m.surface("foliage", hexcol(0x7C8448), 0.05)
        m.wind = 0.9
        m.tube([base, top], 0.008, 4)
        m.surface("fur", hexcol(0x5A3E28), 0.08)
        m.tube([top - Vector((0, 0, 0.32)), top - Vector((0, 0, 0.08))], 0.022, 6)
    return m


def river_stones(name, seed):
    """Water-worn stones in a little heap: smooth, rounded, flattened."""
    m = Model(name, seed)
    r = m.rng
    for k in range(5):
        a = k * 1.3 + r.uniform(-0.3, 0.3)
        d = 0.0 if k == 0 else r.uniform(0.25, 0.5)
        s = 0.32 if k == 0 else r.uniform(0.12, 0.24)
        m.surface("stone", shade(hexcol(0x8A8478), r.uniform(0.8, 1.15)), 0.05)
        m.wind = 0.0
        m.blob((math.cos(a) * d, math.sin(a) * d, s * 0.35), (s, s * r.uniform(0.7, 0.9), s * 0.55), 1, squash=0.6, lumps=0.06)
    return m


def driftwood(name, seed):
    """A bleached log left by a flood, its roots at one end."""
    m = Model(name, seed)
    r = m.rng
    m.surface("timber", hexcol(0xB8AE9C), 0.07)
    m.wind = 0.0
    pts = [Vector((x, 0.06 * math.sin(x * 1.3), 0.14 + 0.03 * math.cos(x * 2.1))) for x in (-1.6, -0.8, 0.0, 0.8, 1.5)]
    m.tube(pts, 0.17, 9, radii=[0.2, 0.18, 0.16, 0.13, 0.09])
    for k in range(7):                                                  # the root mass
        a = k * 0.9 + r.uniform(-0.3, 0.3)
        e = Vector((-1.6 - r.uniform(0.2, 0.6), math.cos(a) * r.uniform(0.3, 0.7), 0.14 + math.sin(a) * r.uniform(0.2, 0.6)))
        m.tube([pts[0], (pts[0] + e) * 0.5, e], 0.05, 5, radii=[0.07, 0.04, 0.012])
    m.tube([pts[3], pts[3] + Vector((0.2, 0.5, 0.35))], 0.04, 5, radii=[0.05, 0.015])
    return m


def rock(name, seed, tint, size=1.0):
    m = Model(name, seed)
    r = m.rng
    m.surface("rock", tint, 0.08)
    m.wind = 0.0
    m.blob((0, 0, size * 0.22), (size * 0.62, size * 0.48, size * 0.46), 2, squash=0.45, lumps=0.42)
    for k in range(3):
        a = k * 2.2 + r.uniform(-0.5, 0.5)
        d = size * r.uniform(0.35, 0.6)
        s = size * r.uniform(0.2, 0.36)
        m.blob((math.cos(a) * d, math.sin(a) * d, s * 0.25), (s, s * 0.8, s * 0.7), 1, squash=0.4, lumps=0.4)
    return m


def far(m):
    """The same tree for the distance: its stems with few sides, its leaf clumps where they were but coarse, no
    twigs. Drawn beyond 140 m (Scatter), where the shadows have ended too."""
    f = Model(m.name + "_far", 7)
    for pts, radius, bark, wind in getattr(m, "lod_stems", []):
        f.surface("bark", bark, 0.07)
        f.wind = wind
        f.tube([pts[0], pts[len(pts) // 2], pts[-1]], radius, 5, radii=[radius, radius * 0.72, radius * 0.45])
    for p, size, flat, tint in getattr(m, "lod_clumps", []):
        f.surface("foliage", tint, 0.14)
        f.wind = 1.0
        first = len(f.bm.verts)
        f.blob(p, (size, size, size * flat), 0, lumps=0.2)
        under_shade(f, first)
    return f


def build():
    col = kit.collection("Flora")
    kit.clear_default_scene()
    leaf, leaf_dark, cotton2 = Pal.Leaf, Pal.LeafDark, hexcol(0x7C9048)
    models = [
        cottonwood("tree_cotton_a", 11, 9.0, 1.0, [leaf, mixc(leaf, leaf_dark, 0.5), cotton2, shade(leaf, 1.1)]),
        cottonwood("tree_cotton_b", 23, 7.0, 0.85, [cotton2, leaf, mixc(cotton2, leaf_dark, 0.5)]),
        oak("tree_oak", 37),
        dead_tree("tree_dead", 5),
        juniper("tree_juniper", 41),
        juniper("tree_juniper_b", 43),
        pinyon("tree_pinyon", 47),
        sagebrush("bush_sage", 3, Pal.Sage, 0.9),
        sagebrush("bush_scrub", 8, hexcol(0x7A8450), 0.85),
        rabbitbrush("bush_rabbit", 13),
        willow("bush_willow", 17),
        yucca("plant_yucca", 19, True),
        yucca("plant_yucca_b", 29, False),
        prickly_pear("plant_pear", 31),
        cholla("plant_cholla", 53),
        reeds("plant_reeds", 59),
        river_stones("rock_river", 61),
        driftwood("log_drift", 67),
        rock("rock_a", 1, Pal.Rock, 1.0),
        rock("rock_b", 2, Pal.RockRed, 1.0),
    ]
    models += [far(m) for m in models if hasattr(m, "lod_clumps")]
    objs = [m.finish(col, sharp_angle=50.0) for m in models]
    kit.layout(objs, 7.0, 5)
    return objs


if "ARGS" in globals() or __name__ == "__main__":
    args = globals().get("ARGS", {})
    objs = build()
    print(kit.stats(objs))
    if not args.get("noexport"):
        print("exported", kit.export_fbx("Flora", "flora"))
