"""Renders the game's cumulus without the game, to tune the cloud model quickly (seconds instead of a build).
    "$BPY" Tools/textures/cloud_preview.py <project> <out.png>
A sheet: fair weather, cloudy and overcast at noon looking up, cloudy at noon overhead, cloudy at sunset towards the
sun and away from it. The same model as CanaanClouds.hlsl (coverage, density, the march with its short steps at the
surface, the light): change both together. Colours are a plain stand-in for SkyWeather's palette and the game's
grading, so judge shapes, cover and softness here and colour in the game.
"""
import math
import os
import sys

import numpy as np
import OpenImageIO as oiio

S = 64
BASE, TOP, EARTH = 1500.0, 3700.0, 6371000.0
VOL = None


def load(path):
    cfg = oiio.ImageSpec()
    cfg.attribute("oiio:UnassociatedAlpha", 1)       # the file is not premultiplied; Unity reads it raw too
    img = oiio.ImageBuf(path, 0, 0, cfg).get_pixels(oiio.FLOAT)
    vol = np.zeros((S, S, S, 4), np.float32)
    for k in range(S):
        r, c = divmod(k, 8)
        vol[:, :, k] = img[r * S:(r + 1) * S, c * S:(c + 1) * S].transpose(1, 0, 2)
    return vol


def tex3d(p):
    """Trilinear, tiling; p in repeats of the texture."""
    q = p * S - 0.5
    i = np.floor(q).astype(np.int64)
    f = (q - i).astype(np.float32)
    out = 0.0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (f[:, 0] if dx else 1 - f[:, 0]) * (f[:, 1] if dy else 1 - f[:, 1]) * (f[:, 2] if dz else 1 - f[:, 2])
                out = out + w[:, None] * VOL[(i[:, 0] + dx) % S, (i[:, 1] + dy) % S, (i[:, 2] + dz) % S]
    return out


def sat(x):
    return np.clip(x, 0.0, 1.0)


def lerp(a, b, t):
    return a + (b - a) * t


def coverage(xz, cover):
    n = tex3d(np.stack([xz[:, 0] / 40000.0, np.full(len(xz), 0.31), xz[:, 1] / 40000.0], -1))[:, 0]
    deck = sat((cover - 0.8) * 5.0)
    return sat(cover + (n - 0.55) * 0.75 * (1.0 - deck))


def profile(hf):
    return sat(hf / 0.1) * sat((1 - hf) / 0.6)


def density(p, cov, detail):
    hf = (p[:, 1] - BASE) / (TOP - BASE)
    n = tex3d((p + np.stack([0 * hf, hf * 600, 0 * hf], -1)) / 5600.0)
    low = n[:, 1] * 0.625 + n[:, 2] * 0.25 + n[:, 3] * 0.125
    shape = sat((n[:, 0] - (low - 1)) / (2 - low))
    shape = sat((shape - 0.56) / 0.3) * profile(hf)
    if detail:
        m = tex3d(p / 1300.0)
        fine = m[:, 1] * 0.625 + m[:, 2] * 0.25 + m[:, 3] * 0.125
        shape = shape - (1 - fine) * 0.16 * lerp(1.6, 1.0, sat(hf * 4))
    c = sat((shape - (1 - cov)) * 3.0)
    return np.where((hf > 0) & (hf < 1) & (cov > 0.005), c, 0.0)


def shell(rd, cam_y, r):
    o = np.array([0.0, EARTH + cam_y, 0.0])
    b = rd @ o
    c = o @ o - r * r
    return -b + np.sqrt(np.maximum(b * b - c, 0.0))


def render(cover, sun_el, sun_az, az, el, lit, dark, glow, w=240, h=150, fov=70.0, cam=(0.0, 300.0, 0.0)):
    ys, xs = np.mgrid[0:h, 0:w]
    tanv = math.tan(math.radians(fov) / 2)
    u = ((xs + 0.5) / w * 2 - 1) * tanv * w / h
    v = (1 - (ys + 0.5) / h * 2) * tanv
    fw = np.array([math.sin(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(el)), math.cos(math.radians(az)) * math.cos(math.radians(el))])
    rt = np.cross([0, 1, 0], fw)
    rt /= np.linalg.norm(rt)
    up = np.cross(fw, rt)
    d = fw[None, :] + u.reshape(-1, 1) * rt[None, :] + v.reshape(-1, 1) * up[None, :]
    d /= np.linalg.norm(d, axis=1, keepdims=True)
    s = np.array([math.sin(math.radians(sun_az)) * math.cos(math.radians(sun_el)), math.sin(math.radians(sun_el)), math.cos(math.radians(sun_az)) * math.cos(math.radians(sun_el))])
    hh = d[:, 1]
    k = sat(hh)[:, None] ** 0.45
    sky = np.array([0.10, 0.25, 0.65]) * k + np.array([0.55, 0.65, 0.80]) * (1 - k)
    sd = d @ s
    sky = sky + np.array([1.0, 0.85, 0.6])[None, :] * (sat(sd) ** 600 * 3 + sat(sd) ** 60 * 0.3)[:, None]
    sky = np.where(hh[:, None] > 0, sky, np.array([0.3, 0.25, 0.2]))

    t0 = shell(d, cam[1], EARTH + BASE)
    t1 = np.minimum(np.minimum(shell(d, cam[1], EARTH + TOP), t0 + 24000.0), 130000.0)
    steps = np.round(26 + (14 - 26) * sat(hh * 2.5))
    coarse = (t1 - t0) / steps
    step = coarse.copy()
    fine = np.zeros(len(d), bool)
    empty = np.zeros(len(d), int)
    t = t0 + coarse * np.random.default_rng(1).random(len(d))
    T = np.ones(len(d))
    acc = np.zeros((len(d), 3))
    forward = sat(sd) ** 6
    sun_up = sat(s[1] * 8 + 0.4)
    earth = np.array([0.0, EARTH + cam[1], 0.0])
    for _ in range(44):
        act = (t < t1) & (T > 0.02) & (hh > -0.02)
        if not act.any():
            break
        idx = np.nonzero(act)[0]
        dd, tt = d[idx], t[idx]
        alt = np.linalg.norm(earth[None, :] + dd * tt[:, None], axis=1) - EARTH
        p = np.stack([cam[0] + dd[:, 0] * tt, alt, cam[2] + dd[:, 2] * tt], -1)
        cov = coverage(p[:, [0, 2]], cover)
        den = density(p, cov, True)
        hit = den > 0.002
        back = hit & ~fine[idx]                         # the first touch on a long step: back off, go on finely
        jb = idx[back]
        t[jb] -= coarse[jb] * 0.67
        step[jb] = coarse[jb] * 0.33
        fine[jb] = True
        empty[jb] = 0
        hit &= ~back
        miss = idx[~(den > 0.002) & fine[idx]]
        empty[miss] += 1
        out = miss[empty[miss] >= 3]
        step[out] = coarse[out]
        fine[out] = False
        if hit.any():
            j, pm, dm, cm = idx[hit], p[hit], den[hit], cov[hit]
            ld = 0.0
            for kk in range(1, 5):
                ld = ld + density(pm + s[None, :] * (kk * kk * 70.0), cm, False) * ((kk * 2 - 1) * 70.0)
            x = ld * 0.01
            sun_t = (np.exp(-x) * 0.6 + np.exp(-x * 0.3) * 0.28 + np.exp(-x * 0.08) * 0.12) * sun_up
            powder = 1 - np.exp(-dm * 4)
            hf = sat((pm[:, 1] - BASE) / (TOP - BASE))
            light = sun_t * lerp(1.0, powder * 1.5, 0.4 * (1 - forward[j]))
            c = lerp(dark[None, :] * (0.55 + 0.6 * hf)[:, None], lit[None, :], sat(light)[:, None]) + glow[None, :] * (sun_t * forward[j] * 1.4 * (1 - dm * 0.5))[:, None]
            a = 1 - np.exp(-dm * 0.02 * step[j])
            acc[j] += (T[j] * a)[:, None] * c
            T[j] *= 1 - a
        t[idx] += step[idx]
    col = (sky * T[:, None] + acc).reshape(h, w, 3)
    col = sat((col * (2.51 * col + 0.03)) / (col * (2.43 * col + 0.59) + 0.14))      # a filmic curve
    return col ** (1 / 2.2)


def main():
    global VOL
    project, out = sys.argv[1], sys.argv[2]
    VOL = load(os.path.join(project, "Assets/Canaan/Resources/Clouds/cloud_noise.png"))
    lin = lambda c: np.array(c) ** 2.2
    noon = (lin((0.98, 0.97, 0.94)), lin((0.62, 0.66, 0.72)), np.array([1.0, 0.93, 0.80]))
    gold = (lin((1.0, 0.80, 0.62)), lin((0.50, 0.42, 0.44)), np.array([1.0, 0.62, 0.30]))
    shots = [(0.22, 68, 180, 20, 32, noon), (0.55, 68, 180, 20, 32, noon), (0.95, 68, 180, 20, 24, noon),
             (0.55, 68, 180, 140, 62, noon), (0.55, 7, 270, 270, 14, gold), (0.55, 7, 270, 90, 14, gold)]
    tiles = [render(cv, se, sa, az, el, *pal) for cv, se, sa, az, el, pal in shots]
    img = np.concatenate([np.concatenate(tiles[:3], 1), np.concatenate(tiles[3:], 1)], 0).astype(np.float32)
    buf = oiio.ImageBuf(oiio.ImageSpec(img.shape[1], img.shape[0], 3, oiio.UINT8))
    buf.set_pixels(oiio.ROI(0, img.shape[1], 0, img.shape[0]), img)
    buf.write(out)
    print("wrote", out)


if __name__ == "__main__":
    main()
