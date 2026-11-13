#!/usr/bin/env python3
"""Draws the grass atlas: tufts of thin blades on transparent ground, for the crossed cards the game scatters
over the prairie (Scatter in TerrainKit.cs). Nothing is downloaded: every blade is drawn here.

Usage (Blender's Python: numpy + OpenImageIO):  make_grass.py <project_root>
Writes Assets/Canaan/Textures/Grass/grass_atlas.png (RGBA 2048 x 1024, 4 x 2 cells),
       Assets/Canaan/Scripts/Gen/GrassTable.cs (where each tuft sits in the atlas and how big its card is)
   and Logs/grass_preview.jpg.
"""
import math
import os
import random
import sys

import numpy as np
import OpenImageIO as oiio

project = sys.argv[1]
CELL = 512            # pixels per cell in the atlas
SS = 2                # supersampling while drawing
COLS, ROWS = 4, 2
N = CELL * SS


def srgb(r, g, b):
    return np.array([r, g, b], dtype=np.float32)


class Cell:
    """One tuft: a square canvas 'size' metres across with y up; (0.5 * size, 0) is where the tuft stands."""

    def __init__(self, size, base):
        self.size = size
        self.rgb = np.empty((N, N, 3), dtype=np.float32)
        self.rgb[:] = base                      # colour under the transparent parts: what filtering bleeds in
        self.a = np.zeros((N, N), dtype=np.float32)

    def px(self, m):
        return m / self.size * N

    def segment(self, ax, ay, bx, by, ra, rb, ca, cb):
        """A tapered stroke between two points (pixels), colours ca -> cb, drawn over what is there."""
        r = max(ra, rb) + 1.5
        x0, x1 = int(max(0, math.floor(min(ax, bx) - r))), int(min(N, math.ceil(max(ax, bx) + r)))
        y0, y1 = int(max(0, math.floor(min(ay, by) - r))), int(min(N, math.ceil(max(ay, by) + r)))
        if x1 <= x0 or y1 <= y0:
            return
        ys, xs = np.mgrid[y0:y1, x0:x1].astype(np.float32) + 0.5
        dx, dy = bx - ax, by - ay
        ll = dx * dx + dy * dy
        t = np.clip(((xs - ax) * dx + (ys - ay) * dy) / ll, 0.0, 1.0) if ll > 1e-6 else np.zeros_like(xs)
        d = np.hypot(xs - (ax + t * dx), ys - (ay + t * dy))
        cov = np.clip(ra + (rb - ra) * t - d + 0.5, 0.0, 1.0)
        if not cov.any():
            return
        # a lighter and a darker side: the blade is folded along its midrib
        side = np.sign((xs - ax) * dy - (ys - ay) * dx) if ll > 1e-6 else 0.0
        col = (ca[None, None, :] + (cb - ca)[None, None, :] * t[..., None]) * (1.0 + 0.07 * side)[..., None]
        c = cov[..., None]
        self.rgb[y0:y1, x0:x1] = self.rgb[y0:y1, x0:x1] * (1.0 - c) + col * c
        self.a[y0:y1, x0:x1] = self.a[y0:y1, x0:x1] + cov * (1.0 - self.a[y0:y1, x0:x1])

    def blade(self, x, height, lean, curve, width, root, mid, tip, y=0.0, steps=18, shade=1.0):
        """A blade from (x, y) metres: 'height' tall, its tip 'lean' metres aside, bowed by 'curve'."""
        pts = []
        for i in range(steps + 1):
            t = i / steps
            bx = x + lean * t + curve * math.sin(math.pi * t) * 0.5
            by = y + height * (t - 0.12 * t * t * min(1.0, abs(lean) / max(height, 1e-3)))
            w = max(width * (1.0 - t) ** 0.75, 0.0018)
            ao = 0.52 + 0.48 * min(1.0, t / 0.4) ** 0.8
            col = (root + (mid - root) * (t / 0.5) if t < 0.5 else mid + (tip - mid) * ((t - 0.5) / 0.5)) * ao * shade
            pts.append((self.px(bx), self.px(by), self.px(w) * 0.5, col))
        for (ax, ay, ra, ca), (bx, by, rb, cb) in zip(pts, pts[1:]):
            self.segment(ax, ay, bx, by, ra, rb, ca, cb)
        return pts[-1][0] / N * self.size, pts[-1][1] / N * self.size      # the tip, metres

    def dot(self, x, y, r, col):
        self.segment(self.px(x), self.px(y), self.px(x) + 0.01, self.px(y), self.px(r), self.px(r), col, col)

    def oval(self, x, y, length, r, angle, col):
        dx, dy = math.sin(angle) * length * 0.5, math.cos(angle) * length * 0.5
        self.segment(self.px(x - dx), self.px(y - dy), self.px(x + dx), self.px(y + dy), self.px(r), self.px(r) * 0.6, col, col * 1.08)


def vary(rng, c, spread=0.1):
    k = 1.0 + rng.uniform(-spread, spread)
    return np.clip(c * k + rng.uniform(-0.02, 0.02), 0.0, 1.0).astype(np.float32)


# palettes (sRGB): root, middle, tip
# (kept close to the colour of the ground they stand on: from the air a meadow must not show where its tufts end)
DRY = (srgb(0.33, 0.29, 0.17), srgb(0.57, 0.51, 0.33), srgb(0.73, 0.67, 0.47))
STRAW = (srgb(0.38, 0.33, 0.21), srgb(0.65, 0.59, 0.40), srgb(0.80, 0.75, 0.56))
GREEN = (srgb(0.18, 0.23, 0.11), srgb(0.33, 0.40, 0.19), srgb(0.52, 0.55, 0.29))
OLIVE = (srgb(0.25, 0.27, 0.14), srgb(0.44, 0.45, 0.24), srgb(0.62, 0.60, 0.36))
DEAD = (srgb(0.27, 0.21, 0.14), srgb(0.47, 0.38, 0.25), srgb(0.62, 0.54, 0.38))


def bunch(cell, rng, n, pal, alt, spread, h0, h1, width, fan=0.55, alt_share=0.25, dead_share=0.08):
    """A clump: blades rise from a narrow base and fan out; the tall ones stand in the middle."""
    cx = cell.size * 0.5
    blades = []
    for _ in range(n):
        off = rng.gauss(0.0, spread)
        off = max(-spread * 2.4, min(spread * 2.4, off))
        k = 1.0 - min(1.0, abs(off) / (spread * 2.6)) * 0.45
        h = rng.uniform(h0, h1) * k
        lean = off * fan * rng.uniform(0.6, 1.7) + rng.gauss(0.0, 0.035)
        blades.append((rng.random(), off, h, lean))
    blades.sort()
    for _, off, h, lean in blades:
        p = DEAD if rng.random() < dead_share else alt if rng.random() < alt_share else pal
        root, mid, tip = (vary(rng, c) for c in p)
        x = cx + off
        lim = cell.size * 0.47
        lean = max(-lim - (x - cx), min(lim - (x - cx), lean))
        cell.blade(x, h, lean, rng.gauss(0.0, 0.03), width * rng.uniform(0.7, 1.3), root, mid, tip, shade=rng.uniform(0.86, 1.08))


def turf(cell, rng, n, pal, alt, h0, h1, width):
    """Short grass standing all along the card."""
    for _ in range(n):
        x = rng.uniform(cell.size * 0.05, cell.size * 0.95)
        p = alt if rng.random() < 0.3 else pal
        root, mid, tip = (vary(rng, c) for c in p)
        lean = rng.gauss(0.0, 0.05)
        lean = max(cell.size * 0.03 - x, min(cell.size * 0.97 - x, lean))
        cell.blade(x, rng.uniform(h0, h1), lean, rng.gauss(0.0, 0.02), width * rng.uniform(0.7, 1.3), root, mid, tip, steps=10, shade=rng.uniform(0.85, 1.08))


def seed_heads(cell, rng, n, h0, h1):
    """Tall stems with a spike of seeds at the top."""
    cx = cell.size * 0.5
    for _ in range(n):
        x = cx + rng.gauss(0.0, 0.1)
        h = rng.uniform(h0, h1)
        lean = (x - cx) * rng.uniform(0.4, 1.6) + rng.gauss(0.0, 0.06)
        lean = max(-cell.size * 0.43 - (x - cx), min(cell.size * 0.43 - (x - cx), lean))
        root, mid, tip = (vary(rng, c, 0.07) for c in STRAW)
        tx, ty = cell.blade(x, h, lean, rng.gauss(0.0, 0.03), 0.0045, root, mid, tip, steps=20)
        # the spike: seeds in two ranks down from the tip
        dirx = lean / max(h, 1e-3)
        seed = vary(rng, srgb(0.76, 0.69, 0.48), 0.08)
        for k in range(rng.randint(9, 14)):
            d = k * 0.011
            sx, sy = tx - dirx * d * 0.9, ty - d
            side = -1 if k % 2 else 1
            cell.oval(sx + side * 0.006, sy, 0.016, 0.0042, dirx + side * 0.55, seed * (0.9 + 0.2 * rng.random()))


def flowers(cell, rng, n, kind):
    cx = cell.size * 0.5
    for _ in range(n):
        x = cx + rng.gauss(0.0, 0.13)
        h = rng.uniform(0.22, 0.46) if kind != "lupine" else rng.uniform(0.3, 0.55)
        lean = (x - cx) * rng.uniform(0.3, 1.2) + rng.gauss(0.0, 0.04)
        lean = max(-cell.size * 0.42 - (x - cx), min(cell.size * 0.42 - (x - cx), lean))
        root, mid, tip = (vary(rng, c, 0.08) for c in GREEN)
        tx, ty = cell.blade(x, h, lean, rng.gauss(0.0, 0.02), 0.005, root, mid, mid, steps=14)
        if kind == "yellow":
            petal = vary(rng, srgb(0.96, 0.78, 0.12), 0.06)
            for k in range(9):
                a = 2 * math.pi * k / 9
                cell.oval(tx + math.sin(a) * 0.011, ty + math.cos(a) * 0.011, 0.016, 0.0048, a, petal)
            cell.dot(tx, ty, 0.0065, srgb(0.50, 0.30, 0.08))
        elif kind == "white":
            petal = vary(rng, srgb(0.95, 0.94, 0.88), 0.03)
            for k in range(10):
                a = 2 * math.pi * k / 10
                cell.oval(tx + math.sin(a) * 0.010, ty + math.cos(a) * 0.010, 0.014, 0.0038, a, petal)
            cell.dot(tx, ty, 0.0055, srgb(0.93, 0.74, 0.14))
        else:
            # a lupine: florets up the top of the stem, paler towards the tip
            dirx = lean / max(h, 1e-3)
            for k in range(rng.randint(10, 15)):
                d = k * 0.0105
                t = k / 14.0
                col = vary(rng, srgb(0.42 + 0.3 * (1 - t) * 0.3, 0.36, 0.74) * (0.85 + 0.25 * (1 - t)), 0.06)
                side = -1 if k % 2 else 1
                cell.oval(tx - dirx * d + side * (0.004 + 0.007 * t), ty - d, 0.012, 0.0048 + 0.002 * t, side * 0.9, col)


def weeds(cell, rng, n):
    """Dead stalks: brittle stems with side twigs."""
    cx = cell.size * 0.5
    for _ in range(n):
        x = cx + rng.gauss(0.0, 0.12)
        h = rng.uniform(0.3, 0.66)
        lean = rng.gauss(0.0, 0.1) + (x - cx) * 0.6
        lean = max(-cell.size * 0.42 - (x - cx), min(cell.size * 0.42 - (x - cx), lean))
        root, mid, tip = (vary(rng, c, 0.1) for c in DEAD)
        cell.blade(x, h, lean, rng.gauss(0.0, 0.02), 0.006, root, mid, tip, steps=12)
        for _ in range(rng.randint(2, 5)):
            t = rng.uniform(0.35, 0.95)
            bx, by = x + lean * t, h * t
            side = rng.choice((-1, 1))
            cell.blade(bx, rng.uniform(0.05, 0.13), side * rng.uniform(0.04, 0.11), 0.0, 0.0035, mid, mid, tip, y=by, steps=6)


# name, metres across the cell, card width and height in metres, share among the tufts, how it is drawn
CELLS = [
    ("dry_bunch", 0.9, 0.9, 0.5, 0.31, lambda c, r: bunch(c, r, 170, DRY, OLIVE, 0.09, 0.22, 0.48, 0.0085)),
    ("dry_stalks", 0.9, 0.9, 0.68, 0.13, lambda c, r: (bunch(c, r, 90, STRAW, DRY, 0.08, 0.2, 0.42, 0.008), bunch(c, r, 24, STRAW, STRAW, 0.06, 0.44, 0.66, 0.0055, 0.5, 0.0, 0.0))),
    ("green_tuft", 0.9, 0.9, 0.46, 0.16, lambda c, r: bunch(c, r, 150, GREEN, OLIVE, 0.09, 0.18, 0.43, 0.0095, 0.6, 0.35, 0.04)),
    ("seed_grass", 0.95, 0.9, 0.92, 0.04, lambda c, r: (bunch(c, r, 80, DRY, STRAW, 0.07, 0.26, 0.52, 0.008), seed_heads(c, r, 13, 0.6, 0.88))),
    ("turf", 1.0, 1.0, 0.28, 0.27, lambda c, r: turf(c, r, 300, OLIVE, DRY, 0.07, 0.25, 0.0085)),
    ("yellow_flowers", 0.8, 0.8, 0.52, 0.03, lambda c, r: (bunch(c, r, 60, GREEN, OLIVE, 0.09, 0.14, 0.3, 0.009), flowers(c, r, 9, "yellow"))),
    ("lupines", 0.8, 0.8, 0.62, 0.025, lambda c, r: (bunch(c, r, 55, GREEN, OLIVE, 0.09, 0.14, 0.3, 0.009), flowers(c, r, 5, "lupine"), flowers(c, r, 4, "white"))),
    ("weeds", 0.9, 0.9, 0.72, 0.035, lambda c, r: (turf(c, r, 40, DRY, DEAD, 0.05, 0.16, 0.008), weeds(c, r, 9))),
]


def main():
    atlas = np.zeros((ROWS * CELL, COLS * CELL, 4), dtype=np.float32)
    table = []
    for i, (name, size, cw, ch, share, draw) in enumerate(CELLS):
        rng = random.Random(1000 + i * 77)
        base = {"green_tuft": GREEN, "yellow_flowers": GREEN, "lupines": GREEN, "weeds": DEAD}.get(name, DRY)[1]
        cell = Cell(size, base)
        draw(cell, rng)
        rgb = cell.rgb.reshape(CELL, SS, CELL, SS, 3).mean(axis=(1, 3))
        a = cell.a.reshape(CELL, SS, CELL, SS).mean(axis=(1, 3))
        # a clear margin, so neighbours never bleed into each other in the smaller mips
        m = 6
        a[:, :m] = 0; a[:, -m:] = 0; a[-m:, :] = 0
        col, row = i % COLS, i // COLS
        y0, x0 = row * CELL, col * CELL
        atlas[y0:y0 + CELL, x0:x0 + CELL, :3] = rgb
        atlas[y0:y0 + CELL, x0:x0 + CELL, 3] = a
        u0 = (col + 0.5 - cw / size * 0.5) / COLS
        u1 = (col + 0.5 + cw / size * 0.5) / COLS
        v0 = row / ROWS
        v1 = (row + ch / size) / ROWS
        table.append((name, u0, v0, u1, v1, cw, ch, share))
        print("grass: %-15s %3d%% covered" % (name, round(float((a > 0.4).mean()) * 100)))

    out = os.path.join(project, "Assets/Canaan/Textures/Grass")
    os.makedirs(out, exist_ok=True)
    img = atlas[::-1]                                  # the canvas has y up; the file's first row is its top
    spec = oiio.ImageSpec(img.shape[1], img.shape[0], 4, oiio.UINT8)
    spec.attribute("oiio:ColorSpace", "sRGB")
    spec.attribute("oiio:UnassociatedAlpha", 1)
    buf = oiio.ImageBuf(spec)
    buf.set_pixels(oiio.ROI(0, img.shape[1], 0, img.shape[0]), np.ascontiguousarray(np.clip(img, 0.0, 1.0), dtype=np.float32))
    buf.write(os.path.join(out, "grass_atlas.png"))

    with open(os.path.join(project, "Assets/Canaan/Scripts/Gen/GrassTable.cs"), "w") as f:
        f.write("// Generated by Tools/textures/make_grass.py - do not edit.\nusing UnityEngine;\n\nnamespace Canaan\n{\n")
        f.write("    /// <summary>The tufts of the grass atlas: where each sits in the texture, the size of its card in metres, its share of a meadow.</summary>\n")
        f.write("    public static class GrassTable\n    {\n")
        f.write("        public static readonly string[] Names = { %s };\n" % ", ".join('"%s"' % t[0] for t in table))
        f.write("        public static readonly Vector4[] Rects = { %s };\n" % ", ".join("new Vector4(%.5ff, %.5ff, %.5ff, %.5ff)" % t[1:5] for t in table))
        f.write("        public static readonly Vector2[] Sizes = { %s };\n" % ", ".join("new Vector2(%.3ff, %.3ff)" % t[5:7] for t in table))
        f.write("        public static readonly float[] Shares = { %s };\n" % ", ".join("%.3ff" % t[7] for t in table))
        f.write("    }\n}\n")

    # preview on the colour of the plain
    ground = np.array([0.52, 0.47, 0.27], dtype=np.float32)
    prev = img[..., :3] * img[..., 3:4] + ground * (1.0 - img[..., 3:4])
    pspec = oiio.ImageSpec(prev.shape[1], prev.shape[0], 3, oiio.UINT8)
    pbuf = oiio.ImageBuf(pspec)
    pbuf.set_pixels(oiio.ROI(0, prev.shape[1], 0, prev.shape[0]), np.ascontiguousarray(np.clip(prev, 0.0, 1.0)))
    os.makedirs(os.path.join(project, "Logs"), exist_ok=True)
    pbuf.write(os.path.join(project, "Logs/grass_preview.jpg"))
    print("grass: wrote", os.path.join(out, "grass_atlas.png"))


main()
