"""Synthesises the game's tileable surface textures (run with Blender's Python: numpy + OpenImageIO).

    make_textures.py <project_root> [name ...]

For every surface it writes into Assets/Canaan/Textures/Surf:
    <name>_c.png   colour detail (sRGB). Mean brightness is normalised to 0.8 (linear): the shader multiplies by
                   1.25 and by the tint, so one texture serves every paint. Alpha = metallic.
    <name>_n.png   packed, linear: RG = tangent normal (green up), B = ambient occlusion, A = smoothness.
Terrain layers (t_*) get real colours instead of a neutral detail, plus a Unity-style normal map.
It also writes Assets/Canaan/Scripts/Gen/SurfaceTable.cs (the enum and tile sizes) and a preview sheet.
"""
import os
import sys
import numpy as np
import OpenImageIO as oiio

N = 1024
TAU = 2.0 * np.pi
V, U = np.mgrid[0:N, 0:N] / float(N)          # V runs down the image; shaders see v = 1 - V


def rng(seed):
    return np.random.default_rng(seed)


def norm01(a):
    a = a - a.min()
    return a / max(a.max(), 1e-9)


def spread(a, k=2.4):
    """Centres and scales by standard deviation, then maps to 0..1: a stable contrast for random fields."""
    return np.clip((a - a.mean()) / (a.std() * k + 1e-9) * 0.5 + 0.5, 0.0, 1.0)


def fbm(seed, beta=2.0, lo=1.0, hi=None, aniso=(1.0, 1.0)):
    """Periodic fractal noise in 0..1 from a shaped random spectrum.
    beta: higher = smoother. aniso (ax, ay): > 1 stretches features along that axis."""
    r = rng(seed)
    ky = np.fft.fftfreq(N)[:, None] * N * aniso[1]
    kx = np.fft.rfftfreq(N)[None, :] * N * aniso[0]
    k = np.sqrt(kx * kx + ky * ky)
    amp = np.where(k >= lo, 1.0 / np.maximum(k, 1e-6) ** (beta * 0.5 + 0.5), 0.0)
    if hi is not None:
        amp = amp * (k <= hi)
    spec = (r.normal(size=k.shape) + 1j * r.normal(size=k.shape)) * amp
    return spread(np.fft.irfft2(spec, s=(N, N)))


def blur(a, sigma):
    ky = np.fft.fftfreq(N)[:, None]
    kx = np.fft.rfftfreq(N)[None, :]
    g = np.exp(-2.0 * (np.pi * sigma) ** 2 * (kx * kx + ky * ky))
    return np.fft.irfft2(np.fft.rfft2(a) * g, s=(N, N))


def cells(seed, cx, cy=None, jitter=1.0, stretch=(1.0, 1.0), warp=None):
    """Periodic Worley noise. Returns (f1, f2, id): distances in cell units, a random value per cell.
    warp: two fields (in cell units) that bend the lookup, so the cell walls wander instead of running straight."""
    cy = cy or cx
    r = rng(seed)
    ox = 0.5 + (r.random((cy, cx)) - 0.5) * jitter
    oy = 0.5 + (r.random((cy, cx)) - 0.5) * jitter
    ids = r.random((cy, cx))
    x, y = U * cx, V * cy
    if warp is not None:
        x, y = x + warp[0], y + warp[1]
    ix, iy = np.floor(x).astype(int), np.floor(y).astype(int)
    f1 = np.full((N, N), 9.0)
    f2 = np.full((N, N), 9.0)
    idm = np.zeros((N, N))
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            jx, jy = (ix + dx) % cx, (iy + dy) % cy
            px = ix + dx + ox[jy, jx]
            py = iy + dy + oy[jy, jx]
            d = np.sqrt(((x - px) * stretch[0]) ** 2 + ((y - py) * stretch[1]) ** 2)
            closer = d < f1
            f2 = np.where(closer, f1, np.minimum(f2, d))
            idm = np.where(closer, ids[jy, jx], idm)
            f1 = np.where(closer, d, f1)
    return f1, f2, idm


def step(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def per(count, seed, axis_values):
    """A random value per integer band (plank, row, thread)."""
    return rng(seed).random(count)[np.floor(axis_values).astype(int) % count]


def shift_rows(a, amount_px):
    """Shifts every column of 'a' down by its own amount (grain that does not line up between planks)."""
    idx = (np.arange(N)[:, None] + amount_px.astype(int)) % N
    return np.take_along_axis(a, idx, axis=0)


def mix(a, b, t):
    return a * (1.0 - t) + b * t


def rgb(r, g, b):
    return np.array([r, g, b], dtype=np.float64)


def tone(value, hue=None, amount=0.0, field=None):
    """Grey detail 'value' as RGB; optionally leans towards 'hue' where 'field' is high."""
    c = np.repeat(value[:, :, None], 3, axis=2)
    if hue is not None:
        f = amount if field is None else (field * amount)[:, :, None]
        c = c * (1.0 - f) + c * hue[None, None, :] * f
    return c


class Surface:
    def __init__(self, name, tile, depth, height, colour, smooth, metal=0.0, ao_strength=1.0, real_colour=False):
        self.name, self.tile, self.depth = name, tile, depth
        self.h = norm01(height)
        self.c = colour if colour.ndim == 3 else tone(colour)
        self.s = smooth if isinstance(smooth, np.ndarray) else np.full((N, N), float(smooth))
        self.m = metal if isinstance(metal, np.ndarray) else np.full((N, N), float(metal))
        self.ao_strength = ao_strength
        self.real_colour = real_colour

    def normal(self):
        px = self.tile / N                                   # metres per pixel
        k = self.depth / px
        su = (np.roll(self.h, -1, 1) - np.roll(self.h, 1, 1)) * 0.5 * k
        sv = -(np.roll(self.h, -1, 0) - np.roll(self.h, 1, 0)) * 0.5 * k
        inv = 1.0 / np.sqrt(su * su + sv * sv + 1.0)
        return -su * inv, -sv * inv, inv

    def ao(self):
        wide = self.h - blur(self.h, N * 0.012)
        fine = self.h - blur(self.h, N * 0.003)
        k = self.depth / (self.tile * 0.01) * self.ao_strength
        return np.clip(1.0 + (wide * 0.9 + fine * 0.6) * k, 0.25, 1.0)

    def colour_srgb(self):
        c = np.clip(self.c, 0.0, None)
        if self.real_colour:
            return np.clip(c, 0.0, 1.0)                      # terrain colours are written as they should look on screen
        if not self.real_colour:
            lum = (c * np.array([0.2126, 0.7152, 0.0722])).sum(axis=2).mean()
            c = c * (0.8 / max(lum, 1e-6))
        c = np.clip(c, 0.0, 1.0)
        return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1.0 / 2.4) - 0.055)


def write(path, img, srgb):
    h, w, ch = img.shape
    spec = oiio.ImageSpec(w, h, ch, oiio.UINT8)
    spec.attribute("oiio:ColorSpace", "sRGB" if srgb else "Linear")
    spec.attribute("oiio:UnassociatedAlpha", 1)      # alpha is data (metallic, smoothness): never divide the colour by it
    buf = oiio.ImageBuf(spec)
    buf.set_pixels(oiio.ROI(0, w, 0, h), np.ascontiguousarray(np.clip(img, 0.0, 1.0), dtype=np.float32))
    buf.write(path)


# ----------------------------------------------------------------------------------------- the surfaces

def s_plain():
    f = fbm(1, 1.6, lo=24)
    return Surface("plain", 1.0, 0.0006, f, tone(0.97 + f * 0.06), 0.28)


def wood_grain(seed, bands, stretch=28.0):
    """Grain that runs along V, different in every band (plank)."""
    g = fbm(seed, 1.5, lo=3, aniso=(1.0, stretch))
    rings = 0.5 + 0.5 * np.sin((U * bands * 5.0 + fbm(seed + 1, 2.4, aniso=(1.0, 6.0)) * 7.0) * TAU)
    g = g * 0.65 + rings * 0.35
    off = (per(bands, seed + 2, U * bands) * N)
    return shift_rows(g, off)


def knots(seed, count, radius):
    f1, _, idm = cells(seed, count, count, jitter=0.9)
    k = step(radius, 0.0, f1) * (idm > 0.62)
    return k


def s_planks():
    n = 8
    x = U * n
    lx = x - np.floor(x)
    edge = np.minimum(lx, 1.0 - lx) * (N / n)
    board = step(1.5, 7.0, edge)
    g = wood_grain(11, n)
    kn = knots(12, 4, 0.11)                 # few and faint: on a wall the same knots come round every two metres
    lift = per(n, 13, x) * 0.25
    # butt joints: one or two per board, at different heights
    jr = rng(14).random(n)[np.floor(x).astype(int) % n]
    jd = np.abs(((V + jr) % 1.0) - 0.5) * N
    joint = step(1.0, 4.0, jd)
    crack = step(0.80, 0.86, fbm(15, 1.2, lo=6, aniso=(1.0, 40.0))) * 0.5
    h = board * joint * (0.72 + lift + g * 0.22 - kn * 0.08) - crack * 0.25
    val = (0.74 + per(n, 16, x) * 0.26) * (0.80 + g * 0.30) * (1.0 - kn * 0.24) * (0.55 + 0.45 * board * joint)
    val *= 0.88 + fbm(17, 2.6) * 0.24
    # nail heads near the joints and the tile seam, with a rust streak under each
    nails = np.zeros((N, N))
    for band in range(n):
        cxp = (band + 0.5) / n
        for vy in (0.03, (0.5 - rng(14).random(n)[band]) % 1.0 + 0.03, (0.5 - rng(14).random(n)[band]) % 1.0 - 0.03):
            for sx in (-0.28, 0.28):
                du = (U - (cxp + sx / n)) * N
                dv = ((V - vy + 0.5) % 1.0 - 0.5) * N
                nails = np.maximum(nails, step(4.0, 2.0, np.sqrt(du * du + dv * dv)))
                nails = np.maximum(nails, step(3.0, 0.0, np.abs(du)) * step(60.0, 0.0, dv) * (dv > 0) * 0.35)
    col = tone(val * (1.0 - nails * 0.6), rgb(0.92, 0.94, 1.08), 0.5, fbm(18, 2.2))      # sun-bleached grey patches
    return Surface("planks", 2.0, 0.012, h, col, 0.18 + g * 0.08)


def s_boards():
    n = 5
    x = U * n
    lx = x - np.floor(x)
    edge = np.minimum(lx, 1.0 - lx) * (N / n)
    seam = step(0.5, 3.0, edge)
    g = wood_grain(21, n, 22.0)
    kn = knots(22, 4, 0.09)
    h = seam * (0.85 + g * 0.15 - kn * 0.04)
    val = (0.84 + per(n, 23, x) * 0.16) * (0.84 + g * 0.24) * (1.0 - kn * 0.2) * (0.75 + 0.25 * seam)
    return Surface("boards", 1.0, 0.003, h, tone(val, rgb(1.06, 1.0, 0.9), 0.5, fbm(24, 2.4)), 0.34 + g * 0.14)


def s_timber():
    g = fbm(31, 1.3, lo=2, aniso=(1.0, 34.0))
    coarse = fbm(32, 1.8, lo=1, aniso=(1.0, 10.0))
    crack = step(0.74, 0.82, fbm(33, 1.1, lo=5, aniso=(1.0, 60.0)))
    saw = 0.5 + 0.5 * np.sin((V * 150.0 + fbm(34, 2.2) * 3.0) * TAU)             # saw marks across the grain
    h = g * 0.55 + coarse * 0.35 + saw * 0.06 - crack * 0.5
    val = (0.74 + g * 0.3) * (0.86 + coarse * 0.2) * (1.0 - crack * 0.55)
    return Surface("timber", 1.0, 0.008, h, tone(val, rgb(0.9, 0.95, 1.1), 0.45, fbm(35, 2.4)), 0.14 + g * 0.06)


def s_bark():
    f1, f2, idm = cells(41, 7, 3, jitter=1.0, stretch=(1.0, 0.42))
    plate = step(0.02, 0.22, f2 - f1)
    g = fbm(42, 1.3, lo=3, aniso=(1.0, 16.0))
    h = plate * (0.55 + idm * 0.3) + g * 0.3
    val = (0.6 + idm * 0.3 + g * 0.2) * (0.45 + 0.55 * plate)
    return Surface("bark", 1.0, 0.03, h, tone(val, rgb(1.08, 1.0, 0.86), 0.4, fbm(43, 2.2)), 0.1)


def s_shingles():
    rows = 8
    y = V * rows
    ry = y - np.floor(y)                                     # 0 at the top of a course, 1 at its butt edge
    row = np.floor(y).astype(int) % rows
    widths = 9
    shift = rng(51).random(rows)[row]
    x = (U + shift) * widths
    # uneven widths: warp the horizontal coordinate per course
    x = x + 0.32 * np.sin((x + rng(52).random(rows)[row] * 5.0) * TAU / 3.0)
    lx = x - np.floor(x)
    sid = (np.floor(x).astype(int) % 64) * 37 + row * 101
    rv = rng(53).random(64 * 37 + rows * 101 + 1)[sid]
    gap = step(0.0, 0.06, np.minimum(lx, 1.0 - lx))
    butt = step(1.0, 0.94, ry)
    g = fbm(54, 1.4, lo=3, aniso=(1.0, 30.0))
    h = (0.25 + 0.75 * ry) * gap * butt + rv * 0.12 + g * 0.1
    shade = step(0.0, 0.16, ry)                               # darker under the course above
    val = (0.62 + rv * 0.38) * (0.82 + g * 0.26) * (0.6 + 0.4 * shade) * (0.55 + 0.45 * gap)
    moss = step(0.62, 0.8, fbm(55, 2.2)) * step(0.55, 1.0, ry)
    col = tone(val, rgb(0.86, 1.02, 0.8), 0.35, moss)
    return Surface("shingles", 2.0, 0.018, h, col, 0.14)


def s_tin():
    waves = 13
    wave = 0.5 + 0.5 * np.cos(U * waves * TAU)
    seam = step(2.0, 6.0, np.abs((V % 0.5) - 0.25) * N)       # sheet overlaps twice per tile
    rust = step(0.52, 0.74, fbm(61, 2.0, lo=2) * 0.7 + fbm(62, 1.3, lo=10) * 0.3)
    scratch = fbm(63, 1.0, lo=20, aniso=(30.0, 1.0))
    h = wave * 0.9 + (1.0 - seam) * 0.25 + rust * 0.05
    val = (0.86 + scratch * 0.14) * (1.0 - rust * 0.25) * (0.8 + 0.2 * seam)
    col = tone(val, rgb(1.35, 0.78, 0.45), 0.85, rust)
    return Surface("tin", 1.0, 0.014, h, col, 0.55 - rust * 0.38, 0.9 - rust * 0.7, ao_strength=0.4)


def s_iron():
    pits = fbm(71, 1.2, lo=14)
    f1, _, idm = cells(72, 26, jitter=1.0)
    dents = step(0.18, 0.0, f1) * (idm > 0.7)
    rust = step(0.5, 0.75, fbm(73, 2.0, lo=2) * 0.65 + fbm(74, 1.2, lo=12) * 0.35)
    h = 0.7 + pits * 0.25 - dents * 0.35 + rust * 0.1
    val = 0.86 + pits * 0.14 - dents * 0.2
    col = tone(val, rgb(1.5, 0.82, 0.5), 0.8, rust)
    return Surface("iron", 0.5, 0.0015, h, col, 0.46 - rust * 0.3, 1.0 - rust * 0.75, ao_strength=0.6)


def s_steel():
    brush = fbm(81, 0.9, lo=18, aniso=(46.0, 1.0))
    scratch = step(0.86, 0.92, fbm(82, 0.8, lo=30, aniso=(1.0, 26.0)))
    cloud = fbm(83, 2.6)
    h = 0.7 + brush * 0.2 - scratch * 0.3
    val = 0.9 + brush * 0.08 + cloud * 0.06 + scratch * 0.1
    return Surface("steel", 0.25, 0.0003, h, tone(val), 0.6 + brush * 0.1 - scratch * 0.2, 1.0, ao_strength=0.3)


def s_stone():
    f1, f2, idm = cells(91, 6, 9, jitter=0.85)
    stone = step(0.03, 0.2, f2 - f1)
    rough = fbm(92, 1.5, lo=4)
    chip = fbm(93, 1.1, lo=20)
    h = stone * (0.6 + idm * 0.25 + rough * 0.25) + chip * 0.05
    val = (0.68 + idm * 0.3) * (0.84 + rough * 0.22) * (0.5 + 0.5 * stone)
    col = tone(val, rgb(1.1, 1.0, 0.86), 0.5, fbm(94, 2.4))
    return Surface("stone", 2.0, 0.03, h, col, 0.16 + rough * 0.08)


def s_rock():
    big = fbm(101, 2.6, lo=1)
    strata = step(0.25, 0.75, 0.5 + 0.5 * np.sin((V * 7.0 + big * 1.6 + fbm(102, 2.4, aniso=(8.0, 1.0)) * 1.2) * TAU))
    # fracture lines between big blocks: bent by noise and broken off in places, so they do not tile like flagstones
    bend = ((fbm(107, 2.2) - 0.5) * 0.9, (fbm(108, 2.2) - 0.5) * 0.9)
    f1, f2, idm = cells(103, 5, 4, jitter=1.0, warp=bend)
    width = 0.03 + 0.05 * fbm(110, 2.0)
    block = step(0.0, 1.0, (f2 - f1) / width)
    block = np.maximum(block, 1.0 - step(0.35, 0.6, fbm(109, 2.0)))
    ridged = 1.0 - np.abs(fbm(104, 2.3, lo=2) * 2.0 - 1.0)
    grit = fbm(105, 1.4, lo=24)
    h = big * 0.35 + strata * 0.2 + ridged * 0.25 + idm * 0.15 + grit * 0.03 - (1.0 - block) * 0.3
    val = (0.78 + strata * 0.14 + grit * 0.05 + idm * 0.1) * (0.85 + ridged * 0.15) * (0.74 + 0.26 * block)
    col = tone(val, rgb(1.14, 0.98, 0.82), 0.6, fbm(106, 2.4))
    return Surface("rock", 4.0, 0.16, h, col, 0.12 + grit * 0.06)


def s_brick():
    rows, cols = 20, 6
    y = V * rows
    row = np.floor(y).astype(int)
    x = U * cols + (row % 2) * 0.5
    lx, ly = x - np.floor(x), y - np.floor(y)
    ex = np.minimum(lx, 1.0 - lx) * (N / cols)
    ey = np.minimum(ly, 1.0 - ly) * (N / rows)
    brick = step(2.0, 6.0, np.minimum(ex, ey))
    bid = (np.floor(x).astype(int) % cols) + (row % rows) * cols
    rv = rng(111).random(rows * cols)[bid]
    pit = fbm(112, 1.1, lo=16)
    h = brick * (0.75 + rv * 0.15 + pit * 0.12) + (1.0 - brick) * (0.3 + pit * 0.15)
    val = mix(0.86 + pit * 0.2, (0.66 + rv * 0.34) * (0.86 + pit * 0.2), brick)
    col = tone(val, rgb(1.25, 1.2, 1.1), 0.6, 1.0 - brick)       # pale mortar
    return Surface("brick", 1.5, 0.008, h, col, 0.14)


def s_plaster():
    trowel = fbm(121, 2.4, lo=1, aniso=(2.5, 1.0))
    grain = fbm(122, 1.0, lo=24)
    f1, f2, _ = cells(123, 4, jitter=1.0)
    crack = step(0.012, 0.0, f2 - f1) * step(0.45, 0.7, fbm(124, 2.4))
    stain = fbm(125, 2.6)
    h = trowel * 0.6 + grain * 0.15 - crack * 0.5
    val = (0.9 + grain * 0.08 + trowel * 0.06) * (1.0 - crack * 0.4) * (0.9 + stain * 0.14)
    return Surface("plaster", 2.0, 0.006, h, tone(val, rgb(1.08, 1.0, 0.88), 0.4, stain), 0.14)


def s_leather():
    f1, f2, _ = cells(131, 70, jitter=1.0)
    pebble = step(0.0, 0.35, f2 - f1)
    crease = step(0.8, 0.9, 1.0 - np.abs(fbm(132, 1.5, lo=3) * 2.0 - 1.0))
    wear = fbm(133, 2.4)
    h = pebble * 0.6 - crease * 0.5 + wear * 0.2
    val = (0.84 + pebble * 0.12) * (1.0 - crease * 0.25) * (0.86 + wear * 0.22)
    return Surface("leather", 0.25, 0.0012, h, tone(val, rgb(1.12, 1.0, 0.84), 0.5, wear), 0.34 + wear * 0.16 - crease * 0.1)


def weave(threads, seed, twill=False):
    x, y = U * threads, V * threads
    ix, iy = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = x - np.floor(x), y - np.floor(y)
    warp = np.sin(fx * np.pi)                                 # round thread running along V
    weft = np.sin(fy * np.pi)                                 # round thread running along U
    if twill:
        over = ((ix + iy) % 3) != 0                           # 2/1 twill: diagonal ribs
    else:
        over = ((ix + iy) % 2) == 0
    h = np.where(over, warp * (0.6 + 0.4 * weft), weft * (0.6 + 0.4 * warp))
    tw = 0.86 + 0.14 * rng(seed).random(threads)[ix % threads]
    tf = 0.86 + 0.14 * rng(seed + 1).random(threads)[iy % threads]
    val = np.where(over, tw, tf) * (0.72 + 0.28 * h)
    return h, val, over


def s_cloth():
    h, val, _ = weave(64, 141)
    fuzz = fbm(142, 1.0, lo=30)
    fold = fbm(143, 2.6)
    return Surface("cloth", 0.2, 0.0007, h * 0.8 + fuzz * 0.2, tone(val * (0.92 + fold * 0.12 + fuzz * 0.06)), 0.12)


def s_canvas():
    h, val, _ = weave(96, 151)
    stain = fbm(152, 2.4)
    sag = fbm(153, 2.8, aniso=(1.0, 2.0))
    col = tone(val * (0.88 + stain * 0.18), rgb(1.1, 1.0, 0.82), 0.5, step(0.55, 0.9, stain))
    return Surface("canvas", 0.6, 0.0012, h * 0.55 + sag * 0.45, col, 0.1)


def s_wool():
    fuzz = fbm(161, 0.9, lo=12)
    f1 = fbm(162, 1.1, lo=8, aniso=(1.0, 7.0))
    f2 = fbm(163, 1.1, lo=8, aniso=(7.0, 1.0))
    fibre = np.maximum(f1, f2)
    nap = fbm(164, 2.5)
    h = fuzz * 0.5 + fibre * 0.5
    val = 0.82 + fibre * 0.16 + nap * 0.1 - fuzz * 0.06
    return Surface("wool", 0.25, 0.001, h, tone(val), 0.08)


def s_denim():
    h, val, over = weave(72, 171, twill=True)
    wear = fbm(172, 2.4)
    # the warp is dyed, the weft is pale: ribs of colour with pale specks between
    val = np.where(over, val * 0.86, val * 1.25) * (0.9 + wear * 0.18)
    return Surface("denim", 0.2, 0.0008, h, tone(val), 0.12)


def s_skin():
    f1, _, _ = cells(181, 110, jitter=1.0)
    pore = step(0.22, 0.05, f1)
    w1 = fbm(182, 1.2, lo=14, aniso=(1.0, 5.0))
    w2 = fbm(183, 1.2, lo=14, aniso=(5.0, 1.0))
    wrinkle = 1.0 - np.abs(w1 - w2)
    mottle = fbm(184, 2.4)
    h = 0.7 - pore * 0.35 + wrinkle * 0.25
    val = 0.95 + mottle * 0.06 - pore * 0.05
    col = tone(val, rgb(1.07, 0.96, 0.93), 0.6, step(0.4, 0.9, mottle))      # blotches of colour under the skin
    return Surface("skin", 0.25, 0.0004, h, col, 0.36 - pore * 0.08, ao_strength=0.5)


def s_hair():
    strand = fbm(191, 0.9, lo=10, aniso=(1.0, 70.0))
    clump = fbm(192, 1.6, lo=2, aniso=(1.0, 14.0))
    h = strand * 0.6 + clump * 0.4
    val = 0.66 + strand * 0.36 + clump * 0.14
    return Surface("hair", 0.25, 0.0025, h, tone(val), 0.3 + strand * 0.22)


def s_fur():
    strand = fbm(201, 1.0, lo=12, aniso=(1.0, 12.0))
    whorl = fbm(202, 2.2, lo=1)
    sheen = fbm(203, 2.6, aniso=(1.0, 3.0))
    h = strand * 0.7 + whorl * 0.3
    val = 0.82 + strand * 0.2 + sheen * 0.1
    return Surface("fur", 0.3, 0.0012, h, tone(val), 0.26 + sheen * 0.16)


def s_straw():
    a = fbm(211, 1.0, lo=10, aniso=(1.0, 50.0))
    b = shift_rows(fbm(212, 1.0, lo=10, aniso=(1.0, 50.0)), (U * 0.25) * N)       # a second layer lying askew
    fibre = np.maximum(a, b * 0.9)
    gap = step(0.3, 0.15, fibre)
    val = (0.74 + fibre * 0.34) * (1.0 - gap * 0.5)
    col = tone(val, rgb(1.0, 1.06, 0.8), 0.35, fbm(213, 2.2))
    return Surface("straw", 0.5, 0.006, fibre, col, 0.22 + fibre * 0.12)


def pebbles(seed, count, share, lo_r=0.12, hi_r=0.34):
    f1, _, idm = cells(seed, count, jitter=1.0)
    r = lo_r + (hi_r - lo_r) * ((idm * 7.13) % 1.0)
    return np.where(idm < share, np.sqrt(np.clip(1.0 - (f1 / r) ** 2, 0.0, 1.0)), 0.0), idm


def s_dirt(name="dirt", cracks=True, peb_count=22, peb_share=0.42, peb_lo=0.12, peb_hi=0.34):
    big = fbm(221, 2.3)
    mid = fbm(222, 1.6, lo=4)
    fine = fbm(223, 1.0, lo=30)
    peb, pid = pebbles(224, peb_count, peb_share, peb_lo, peb_hi)
    crack = 0.0
    if cracks:
        f1, f2, _ = cells(225, 5, jitter=1.0)
        crack = step(0.02, 0.0, f2 - f1) * step(0.5, 0.75, big)
    h = big * 0.35 + mid * 0.3 + fine * 0.08 + peb * 0.3 - crack * 0.3
    val = (0.8 + mid * 0.2 + fine * 0.08) * (1.0 - crack * 0.35)
    val = mix(val, 0.78 + ((pid * 3.7) % 1.0) * 0.3, step(0.0, 0.3, peb))
    col = tone(val, rgb(1.1, 1.0, 0.85), 0.4, big)
    return Surface(name, 2.0, 0.025, h, col, 0.12 + peb * 0.1)


def s_foliage():
    f1, f2, idm = cells(231, 26, jitter=1.0)
    leaf = step(0.0, 0.25, f2 - f1)
    vein = fbm(232, 1.2, lo=10, aniso=(1.0, 6.0))
    light = fbm(233, 2.2)
    h = leaf * (0.6 + idm * 0.3) + vein * 0.15
    val = (0.62 + idm * 0.4) * (0.6 + 0.4 * leaf) * (0.9 + vein * 0.14)
    col = tone(val, rgb(1.12, 1.08, 0.7), 0.55, step(0.5, 0.95, light))          # sunlit yellow-green patches
    return Surface("foliage", 0.5, 0.012, h, col, 0.24 + leaf * 0.1)


def s_wet():
    # eyes, a moist nose: smooth and almost without texture; the colour comes from the vertex
    f = fbm(241, 1.4, lo=20)
    return Surface("wet", 0.25, 0.0002, f, tone(0.97 + f * 0.04), 0.9)


def s_horn():
    # hooves and horn: fine growth lines running down the wall, a dull sheen
    line = fbm(251, 0.9, lo=6, aniso=(1.0, 60.0))
    band = fbm(252, 2.0, lo=2, aniso=(1.0, 8.0))
    h = line * 0.7 + band * 0.3
    val = 0.8 + line * 0.18 + band * 0.12
    return Surface("horn", 0.15, 0.0008, h, tone(val), 0.38 + line * 0.12)


def s_paper():
    # wallpaper: a printed pattern - pairs of fine stripes and a small four-petal flower set on a half-drop lattice -
    # on a slightly mottled ground; the colour comes from the vertex. Flat, matte, a hint of the paper's grain.
    y, x = np.mgrid[0:N, 0:N] / float(N)
    val = np.full((N, N), 0.97)
    for k in range(8):                                        # eight stripe pairs across the 0.5 m repeat
        c = (k + 0.5) / 8.0
        for o in (-0.006, 0.006):
            d = np.abs(((x - c - o) + 0.5) % 1.0 - 0.5)
            val -= 0.07 * np.exp(-(d / 0.0016) ** 2)
    for row in range(4):                                       # the flowers, between the stripes
        for col in range(4):
            cx = (col + (0.5 if row % 2 else 0.0)) / 4.0 + 1.0 / 16.0
            cy = (row + 0.5) / 4.0
            dx = ((x - cx) + 0.5) % 1.0 - 0.5
            dy = ((y - cy) + 0.5) % 1.0 - 0.5
            for a in range(4):
                ang = a * np.pi / 2 + np.pi / 4
                px, py = np.cos(ang) * 0.018, np.sin(ang) * 0.018
                val -= 0.16 * np.exp(-(((dx - px) / 0.011) ** 2 + ((dy - py) / 0.007 if a % 2 else (dy - py) / 0.011) ** 2))
            val -= 0.12 * np.exp(-((dx / 0.006) ** 2 + (dy / 0.006) ** 2))
            for a in range(2):                                 # a pair of leaves
                sx = 1 if a else -1
                val -= 0.08 * np.exp(-(((dx - sx * 0.036) / 0.012) ** 2 + ((dy + 0.03) / 0.005) ** 2))
    mottle = fbm(261, 2.2, lo=2)
    grain = fbm(262, 0.8, lo=40)
    val = val * (0.94 + mottle * 0.08) + grain * 0.025
    h = grain * 0.3 + (0.97 - val) * 0.5
    return Surface("paper", 0.5, 0.0004, h, tone(val, rgb(1.05, 1.0, 0.9), 0.3, mottle), 0.1)


# (new layers go at the end: the models and the code name layers by their index)
SURFACES = [s_plain, s_planks, s_boards, s_timber, s_bark, s_shingles, s_tin, s_iron, s_steel, s_stone, s_rock,
            s_brick, s_plaster, s_leather, s_cloth, s_canvas, s_wool, s_denim, s_skin, s_hair, s_fur, s_straw,
            s_dirt, s_foliage, s_wet, s_horn, s_paper]

# tints used only for the preview sheet
PREVIEW_TINT = {
    "plain": (0.8, 0.8, 0.8), "planks": (0.54, 0.42, 0.28), "boards": (0.50, 0.33, 0.19), "timber": (0.45, 0.35, 0.25),
    "bark": (0.36, 0.29, 0.23), "shingles": (0.34, 0.28, 0.24), "tin": (0.60, 0.63, 0.64), "iron": (0.23, 0.23, 0.25),
    "steel": (0.55, 0.57, 0.6), "stone": (0.55, 0.53, 0.49), "rock": (0.63, 0.40, 0.28), "brick": (0.56, 0.29, 0.22),
    "plaster": (0.80, 0.75, 0.64), "leather": (0.42, 0.26, 0.15), "cloth": (0.75, 0.72, 0.64), "canvas": (0.85, 0.80, 0.70),
    "wool": (0.22, 0.17, 0.13), "denim": (0.29, 0.37, 0.47), "skin": (0.83, 0.64, 0.50), "hair": (0.23, 0.16, 0.11),
    "fur": (0.42, 0.25, 0.14), "straw": (0.82, 0.71, 0.40), "dirt": (0.55, 0.42, 0.29), "foliage": (0.42, 0.52, 0.25),
    "wet": (0.12, 0.08, 0.06), "horn": (0.30, 0.26, 0.22), "paper": (0.48, 0.24, 0.21),
}


# ----------------------------------------------------------------------------------------- terrain layers

def t_road():
    s = s_dirt("t_road", cracks=False, peb_count=46, peb_share=0.22, peb_lo=0.08, peb_hi=0.2)
    dust = fbm(302, 2.4)
    base = rgb(0.60, 0.47, 0.33)
    c = s.c / s.c.mean() * base[None, None, :]
    c = c * (0.9 + 0.16 * dust[:, :, None])
    c = mix(c, c * rgb(1.1, 1.04, 0.92)[None, None, :], step(0.5, 0.9, dust)[:, :, None])
    return Surface("t_road", 5.0, 0.022, s.h, c, s.s, real_colour=True)


def grass_mat(seed):
    blades = np.zeros((N, N))
    for i, (ax, ay) in enumerate(((1.0, 18.0), (18.0, 1.0), (9.0, 14.0))):
        blades = np.maximum(blades, fbm(seed + i, 1.0, lo=14, aniso=(ax, ay)) * (1.0 - i * 0.1))
    return blades


def t_grass():
    blades = grass_mat(311)
    soil = fbm(312, 1.8, lo=6)
    dry = fbm(313, 2.0, lo=4)
    h = blades * 0.75 + soil * 0.25
    straw = mix(rgb(0.60, 0.54, 0.28), rgb(0.74, 0.66, 0.37), dry[:, :, None])
    green = rgb(0.47, 0.50, 0.25)
    c = mix(straw, green[None, None, :], (step(0.4, 0.8, fbm(314, 1.9, lo=5)) * 0.5)[:, :, None])
    c = c * (0.68 + 0.42 * blades[:, :, None])
    c = mix(c, rgb(0.50, 0.40, 0.28)[None, None, :], (step(0.7, 0.9, soil) * 0.5)[:, :, None])
    return Surface("t_grass", 6.0, 0.03, h, c, 0.1 + blades * 0.06, real_colour=True)


def t_scrub():
    sand = fbm(321, 1.2, lo=10)
    ripple = 0.5 + 0.5 * np.sin((V * 26.0 + fbm(322, 2.4) * 2.5) * TAU)
    peb, pid = pebbles(323, 44, 0.2, 0.06, 0.18)
    tuft = step(0.72, 0.88, fbm(324, 1.8, lo=6)) * grass_mat(325)
    big = fbm(326, 2.0, lo=3)
    h = sand * 0.25 + ripple * 0.12 + peb * 0.3 + tuft * 0.35 + big * 0.2
    c = mix(rgb(0.68, 0.58, 0.41), rgb(0.78, 0.69, 0.50), big[:, :, None]) * (0.9 + 0.16 * sand[:, :, None])
    c = mix(c, (rgb(0.58, 0.54, 0.47)[None, None, :] * (0.8 + 0.3 * ((pid * 3.7) % 1.0))[:, :, None]), step(0.0, 0.3, peb)[:, :, None])
    c = mix(c, rgb(0.58, 0.54, 0.30)[None, None, :] * (0.75 + 0.35 * tuft[:, :, None]), step(0.1, 0.5, tuft)[:, :, None])
    return Surface("t_scrub", 6.0, 0.025, h, c, 0.1 + peb * 0.08, real_colour=True)


def t_rock():
    s = s_rock()
    warm = fbm(331, 2.4)
    base = mix(rgb(0.60, 0.38, 0.26), rgb(0.72, 0.50, 0.35), warm[:, :, None])
    c = s.c / s.c.mean() * base
    return Surface("t_rock", 10.0, 0.22, s.h, c, s.s, real_colour=True)


def level(s):
    """Takes the large blotches out of a terrain tile: a patch the size of the tile repeats visibly to the
    horizon, fine detail does not. The broad variation of the land comes from the terrain's own layer map."""
    for k in range(3):
        ch = s.c[:, :, k]
        s.c[:, :, k] = ch - blur(ch, N * 0.10) + ch.mean()
    s.h = norm01(s.h - blur(s.h, N * 0.12))
    return s


TERRAIN = [lambda: level(t_road()), lambda: level(t_grass()), lambda: level(t_scrub()), t_rock]


# ----------------------------------------------------------------------------------------- output

def preview(s, tint):
    nx, ny, nz = s.normal()
    light = np.array([-0.55, 0.5, 0.67])
    light = light / np.linalg.norm(light)
    ndl = np.clip(nx * light[0] + ny * light[1] + nz * light[2], 0.0, 1.0)
    albedo = np.clip(s.c, 0, None)
    if not s.real_colour:
        lum = (albedo * np.array([0.2126, 0.7152, 0.0722])).sum(axis=2).mean()
        albedo = albedo * (1.0 / max(lum, 1e-6)) * np.array(tint)[None, None, :] ** 2.2
    hx, hy, hz = light[0], light[1], light[2] + 1.0
    hl = np.sqrt(hx * hx + hy * hy + hz * hz)
    spec = np.clip((nx * hx + ny * hy + nz * hz) / hl, 0, 1) ** (8.0 + s.s * 120.0) * (0.04 + s.m * 0.6) * (0.3 + s.s * 2.0)
    lit = albedo * ((0.22 + 1.25 * ndl) * s.ao())[:, :, None] * (1.0 - s.m * 0.6)[:, :, None] + spec[:, :, None]
    lit = np.clip(lit, 0, 1) ** (1 / 2.2)
    return lit[::4, ::4]


def main():
    project = sys.argv[1]
    only = set(sys.argv[2:])
    out = os.path.join(project, "Assets/Canaan/Textures/Surf")
    os.makedirs(out, exist_ok=True)
    previews, table = [], []
    for make in SURFACES + TERRAIN:
        s = make()
        terrain = s.name.startswith("t_")
        if not terrain:
            table.append(s)
        if only and s.name not in only:
            continue
        nx, ny, nz = s.normal()
        colour = np.dstack([s.colour_srgb(), s.m[:, :, None]])
        if terrain:
            colour = np.dstack([s.colour_srgb(), s.s[:, :, None]])                 # terrain: alpha = smoothness
            # Unity's terrain shader reads x from alpha and y from green on this platform (the "DXT5nm" layout)
            one = np.ones((N, N))
            normal = np.dstack([one, ny * 0.5 + 0.5, one, nx * 0.5 + 0.5])
            write(os.path.join(out, s.name + "_n.png"), normal, False)
        else:
            packed = np.dstack([nx * 0.5 + 0.5, ny * 0.5 + 0.5, s.ao(), s.s])
            write(os.path.join(out, s.name + "_n.png"), packed, False)
        write(os.path.join(out, s.name + "_c.png"), colour, True)
        previews.append(preview(s, PREVIEW_TINT.get(s.name, (0.7, 0.7, 0.7))))
        print("wrote", s.name, "tile %.2f m" % s.tile)

    if previews:
        cols = 7
        rows = (len(previews) + cols - 1) // cols
        cell = previews[0].shape[0]
        sheet = np.zeros((rows * cell, cols * cell, 3))
        for i, p in enumerate(previews):
            sheet[(i // cols) * cell:(i // cols + 1) * cell, (i % cols) * cell:(i % cols + 1) * cell] = p
        os.makedirs(os.path.join(project, "Logs"), exist_ok=True)
        write(os.path.join(project, "Logs/textures_preview.jpg"), sheet, True)

    names = [s.name for s in table]
    lines = [
        "// Generated by Tools/textures/make_textures.py — do not edit.",
        "namespace Canaan",
        "{",
        "    /// <summary>Layers of the surface texture arrays, in order.</summary>",
        "    public enum Surf { " + ", ".join(n.capitalize() for n in names) + " }",
        "",
        "    public static class SurfaceTable",
        "    {",
        "        public static readonly string[] Names = { " + ", ".join('"%s"' % n for n in names) + " };",
        "        /// <summary>Metres covered by one repeat of each texture.</summary>",
        "        public static readonly float[] Tile = { " + ", ".join("%gf" % s.tile for s in table) + " };",
        "        public const int Count = %d;" % len(names),
        "    }",
        "}",
        "",
    ]
    path = os.path.join(project, "Assets/Canaan/Scripts/Gen/SurfaceTable.cs")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print("wrote", path)


if __name__ == "__main__":
    main()
