"""The noise the clouds are shaped from: a 64-cube that tiles on all three axes, written as an 8 x 8 flipbook of
slices (Unity's importer stacks it into a Texture3D, CanaanSetup.Clouds).
    "$BPY" Tools/textures/make_clouds.py <project>
R: Perlin-Worley - billowy lumps with soft gaps (the shape of a cumulus); G, B, A: Worley fbm at three
frequencies (the cauliflower edges and the wisps at the base). Linear data, not colour.
"""
import os
import sys

import numpy as np
import OpenImageIO as oiio

S = 64


def grid(freq):
    g = (np.arange(S) + 0.5) / S * freq
    return np.meshgrid(g, g, g, indexing="ij")


def worley(freq, seed):
    """Distance to the nearest of one random point per cell, periodic; 0 at a point, about 1 between them."""
    rng = np.random.default_rng(seed)
    pts = rng.random((freq, freq, freq, 3))
    X, Y, Z = grid(freq)
    ix, iy, iz = np.floor(X).astype(int), np.floor(Y).astype(int), np.floor(Z).astype(int)
    best = np.full(X.shape, 9.0)
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            for dz in (-1, 0, 1):
                cx, cy, cz = ix + dx, iy + dy, iz + dz
                p = pts[cx % freq, cy % freq, cz % freq]
                d = np.sqrt((cx + p[..., 0] - X) ** 2 + (cy + p[..., 1] - Y) ** 2 + (cz + p[..., 2] - Z) ** 2)
                best = np.minimum(best, d)
    return np.clip(best, 0.0, 1.0)


def value(freq, seed):
    """Smooth periodic value noise."""
    rng = np.random.default_rng(seed)
    lat = rng.random((freq, freq, freq))
    X, Y, Z = grid(freq)
    i, j, k = np.floor(X).astype(int), np.floor(Y).astype(int), np.floor(Z).astype(int)
    fx, fy, fz = X - i, Y - j, Z - k
    fx, fy, fz = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy), fz * fz * (3 - 2 * fz)
    out = 0.0
    for a in (0, 1):
        for b in (0, 1):
            for c in (0, 1):
                w = (fx if a else 1 - fx) * (fy if b else 1 - fy) * (fz if c else 1 - fz)
                out = out + w * lat[(i + a) % freq, (j + b) % freq, (k + c) % freq]
    return out


def worley_fbm(freq, seed):
    return 1.0 - (worley(freq, seed) * 0.625 + worley(freq * 2, seed + 1) * 0.25 + worley(freq * 4, seed + 2) * 0.125)


def main():
    project = sys.argv[1]
    perlin = value(4, 11) * 0.5 + value(8, 12) * 0.25 + value(16, 13) * 0.15 + value(32, 14) * 0.1
    perlin = (perlin - perlin.min()) / (perlin.max() - perlin.min())
    wf = worley_fbm(4, 21)
    # Perlin-Worley: the Worley cells carve the smooth noise into lumps
    pw = np.clip((perlin - (wf - 1.0)) / (1.0 - (wf - 1.0)), 0.0, 1.0)
    pw = (pw - pw.min()) / (pw.max() - pw.min())
    g, b, a = worley_fbm(4, 31), worley_fbm(8, 41), worley_fbm(12, 51)
    vol = np.stack([pw, g, b, a], axis=-1)                       # (x, y, z, 4)
    img = np.zeros((S * 8, S * 8, 4), np.float32)
    for k in range(S):                                           # slice z = k goes to row k // 8, column k % 8, top-left first
        r, c = divmod(k, 8)
        img[r * S:(r + 1) * S, c * S:(c + 1) * S] = vol[:, :, k].transpose(1, 0, 2)
    out = os.path.join(project, "Assets/Canaan/Resources/Clouds/cloud_noise.png")
    spec = oiio.ImageSpec(S * 8, S * 8, 4, oiio.UINT8)
    spec.attribute("oiio:ColorSpace", "Linear")
    spec.attribute("oiio:UnassociatedAlpha", 1)
    buf = oiio.ImageBuf(spec)
    buf.set_pixels(oiio.ROI(0, S * 8, 0, S * 8), img)
    buf.write(out)
    print("wrote", out, "ranges", [round(float(vol[..., i].min()), 2) for i in range(4)], [round(float(vol[..., i].max()), 2) for i in range(4)])


if __name__ == "__main__":
    main()
