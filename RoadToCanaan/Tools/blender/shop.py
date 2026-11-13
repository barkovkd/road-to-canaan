"""Shop fittings: the counter of a store, the bar of a saloon, wall shelves, and the goods that stand on them.
    Tools/blender/bmcp.py run Tools/blender/shop.py
Collection "Shop" -> Assets/Canaan/Models/shop.fbx.

Game axes: x right, y up, z forward. A counter is a metre of its run along z, the customers' side is +x; shelving is
a metre along x with its back to -z. Goods stand on their own base. Colour alpha 12 = what the game dyes: the
label of a tin, the glass of a bottle, a bolt's cloth.
"""
import importlib
import math

import kit

importlib.reload(kit)
from kit import L, Model, Pal, gbeam, gbox, gcyl, hexcol, shade

DYE = 12 / 255.0


def mat(m, surf, tint, jitter=0.04):
    m.wind = 0.0
    m.surface(surf, tint, jitter)
    return m


def dyed(m, surf="plain", k=1.0, jitter=0.03):
    m.wind = DYE
    m.surface(surf, (k, k, k), jitter)
    return m


# ---------------------------------------------------------------------- counters

def counter(name, bar):
    """One metre of counter, 0.6 deep and 1.04 high: a panelled front to the customers (+x), open shelves to the
    keeper. The bar of a saloon has a wider top with a rounded nosing and a brass foot rail."""
    m = Model(name)
    wood = shade(Pal.WoodOld, 0.95 if bar else 1.0)
    dark = shade(Pal.WoodDark, 0.9 if bar else 1.0)
    front = 0.27
    mat(m, "boards", wood, 0.05)
    gbox(m, (front - 0.012, 0.56, 0), (0.024, 0.9, 1.0), 0.0, "y")                 # the front sheet
    for sz in (-1, 1):
        gbox(m, (0, 0.5, sz * 0.49), (0.56, 1.0, 0.02), 0.0, "y")                   # ends of the carcass
    gbox(m, (-0.02, 0.1, 0), (0.5, 0.025, 0.96), 0.0, "z")                          # bottom and the shelf inside
    gbox(m, (-0.04, 0.53, 0), (0.46, 0.025, 0.96), 0.0, "z")
    mat(m, "boards", shade(wood, 0.9), 0.05)
    for sz in (-1, 0, 1):                                                           # stiles and rails over the sheet
        gbox(m, (front + 0.008, 0.56, sz * 0.455), (0.022, 0.9, 0.09), 0.004, "y")
    for y in (0.16, 0.96):
        gbox(m, (front + 0.008, y, 0), (0.022, 0.1, 1.0), 0.004, "z")
    mat(m, "boards", shade(wood, 0.78), 0.04)
    gbox(m, (front - 0.05, 0.055, 0), (0.02, 0.11, 1.0), 0.0, "z")                  # the kick board, set back
    mat(m, "boards", dark, 0.04)
    if bar:
        gbox(m, (0.03, 1.02, 0), (0.74, 0.045, 1.0), 0.012, "z")
        gcyl(m, (0.40, 1.02, -0.5), (0.40, 1.02, 0.5), 0.034, 0.034, 10, 0)         # nosing to lean on
        gbox(m, (0.3, 0.975, 0), (0.06, 0.05, 1.0), 0.01, "z")
        mat(m, "steel", shade(Pal.Brass, 1.05), 0.02)
        gcyl(m, (0.45, 0.17, -0.5), (0.45, 0.17, 0.5), 0.02, 0.02, 8, 0)            # foot rail
        gbeam(m, (0.29, 0.10, 0.0), (0.45, 0.17, 0.0), 0.03, 0.022, 0.004)
        gcyl(m, (0.45, 0.0, 0.0), (0.45, 0.17, 0.0), 0.012, 0.012, 6, 0)
    else:
        gbox(m, (0.02, 1.02, 0), (0.68, 0.045, 1.0), 0.01, "z")
        gbox(m, (0.3, 0.985, 0), (0.035, 0.03, 1.0), 0.008, "z")
    return m


def shelf():
    """One metre of wall shelving, 2.2 high: a boarded back, four shelves 0.3 deep (their tops at 0.37, 0.87,
    1.37 and 1.87), a closed base and a cornice."""
    m = Model("shop_shelf")
    mat(m, "boards", shade(Pal.WoodDark, 0.95), 0.05)
    gbox(m, (0, 1.1, 0.0), (1.0, 2.2, 0.03), 0.0, "y")
    for sx in (-1, 1):
        gbox(m, (sx * 0.485, 1.1, 0.165), (0.03, 2.2, 0.3), 0.004, "y")
    mat(m, "boards", shade(Pal.WoodOld, 1.0), 0.05)
    for y in (0.35, 0.85, 1.35, 1.85):
        gbox(m, (0, y, 0.165), (0.94, 0.035, 0.3), 0.004, "x")
    mat(m, "boards", shade(Pal.WoodOld, 0.88), 0.05)
    gbox(m, (0, 0.165, 0.305), (0.94, 0.33, 0.02), 0.0, "x")                        # the base, a door on it
    gbox(m, (0, 0.175, 0.322), (0.7, 0.22, 0.014), 0.004, "x")
    mat(m, "boards", shade(Pal.WoodDark, 0.9), 0.04)
    gbox(m, (0, 2.215, 0.18), (1.0, 0.045, 0.37), 0.008, "x")
    gbox(m, (0, 2.17, 0.33), (1.0, 0.05, 0.04), 0.008, "x")
    mat(m, "steel", shade(Pal.Brass, 0.95), 0.0)
    gcyl(m, (0.28, 0.2, 0.33), (0.28, 0.2, 0.35), 0.012, 0.012, 6, 0)
    return m


# ---------------------------------------------------------------------- goods (origin under the middle of the base)

def bottle():
    m = Model("goods_bottle")
    dyed(m, "plain")
    m.lathe(L(0, 0, 0), [(0.0, 0.0), (0.036, 0.0), (0.038, 0.012), (0.038, 0.15), (0.03, 0.18), (0.014, 0.205), (0.013, 0.255), (0.016, 0.26), (0.016, 0.27), (0.0, 0.27)], 8)
    mat(m, "plain", shade(Pal.Bone, 0.97), 0.0)
    m.lathe(L(0, 0, 0), [(0.0385, 0.05), (0.0395, 0.052), (0.0395, 0.12), (0.0385, 0.122)], 8)   # label
    mat(m, "plain", shade(Pal.WoodNew, 0.9), 0.0)
    gcyl(m, (0, 0.27, 0), (0, 0.285, 0), 0.011, 0.012, 6, 0)                                     # cork
    return m


def bottle_square():
    m = Model("goods_bottle_sq")
    dyed(m, "plain")
    gbox(m, (0, 0.085, 0), (0.07, 0.17, 0.07), 0.008, "y")
    m.lathe(L(0, 0, 0), [(0.03, 0.165), (0.016, 0.19), (0.014, 0.225), (0.018, 0.23), (0.018, 0.24), (0.0, 0.24)], 8)
    mat(m, "plain", shade(Pal.Bone, 0.95), 0.0)
    gbox(m, (0, 0.085, 0.0355), (0.055, 0.08, 0.003), 0.0, "y")
    mat(m, "plain", shade(Pal.WoodNew, 0.9), 0.0)
    gcyl(m, (0, 0.24, 0), (0, 0.252, 0), 0.011, 0.012, 6, 0)
    return m


def jug():
    m = Model("goods_jug")
    dyed(m, "plain", 1.0)
    m.lathe(L(0, 0, 0), [(0.0, 0.0), (0.055, 0.0), (0.068, 0.03), (0.07, 0.11), (0.05, 0.16), (0.022, 0.185), (0.022, 0.215), (0.027, 0.22), (0.0, 0.222)], 10)
    m.tube([L(0.02, 0.2, 0), L(0.065, 0.2, 0), L(0.085, 0.15, 0), L(0.068, 0.1, 0)], 0.009, 5)
    return m


def tin():
    m = Model("goods_tin")
    mat(m, "tin", shade(Pal.Tin, 1.0), 0.03)
    m.lathe(L(0, 0, 0), [(0.0, 0.0), (0.046, 0.0), (0.046, 0.006), (0.044, 0.008), (0.044, 0.122), (0.046, 0.124), (0.046, 0.13), (0.0, 0.13)], 10)
    dyed(m, "plain")
    m.lathe(L(0, 0, 0), [(0.0445, 0.02), (0.0455, 0.022), (0.0455, 0.108), (0.0445, 0.11)], 10)  # paper label
    return m


def jar():
    m = Model("goods_jar")
    dyed(m, "plain")
    m.lathe(L(0, 0, 0), [(0.0, 0.0), (0.05, 0.0), (0.052, 0.01), (0.052, 0.13), (0.042, 0.15), (0.042, 0.16), (0.0, 0.16)], 10)
    mat(m, "tin", shade(Pal.Tin, 0.9), 0.02)
    gcyl(m, (0, 0.158, 0), (0, 0.178, 0), 0.046, 0.046, 10, 0.003)
    return m


def box():
    m = Model("goods_box")
    dyed(m, "plain")
    gbox(m, (0, 0.085, 0), (0.15, 0.17, 0.13), 0.004, "y")
    mat(m, "plain", shade(Pal.Bone, 0.95), 0.0)
    gbox(m, (0, 0.09, 0.066), (0.1, 0.07, 0.003), 0.0, "x")
    return m


def bolts():
    """Three bolts of cloth lying one on another along the shelf, their flat sides to the room."""
    m = Model("goods_bolts")
    for i, k in enumerate((1.0, 0.82, 1.12)):
        dyed(m, "cloth", k, 0.02)
        y = 0.036 + i * 0.07
        ring = lambda x: [L(x, y + math.sin(a) * 0.034, math.cos(a) * 0.075 + (i - 1) * 0.008) for a in [2 * math.pi * j / 10 for j in range(10)]]
        m.loft([ring(-0.095 + i * 0.006), ring(0.095 - i * 0.01)], True, 0, True)
        mat(m, "boards", shade(Pal.WoodNew, 1.0), 0.0)                                           # the board it is wound on
        gbox(m, (0, y, (i - 1) * 0.008), (0.2 - i * 0.012, 0.012, 0.11), 0.0, "x")
    return m


def sack():
    m = Model("goods_sack")
    mat(m, "canvas", shade(Pal.Canvas, 0.92), 0.05)
    m.push(L(0, 0.1, 0))
    m.blob((0, 0, 0), (0.085, 0.07, 0.1), 1, squash=0.8, lumps=0.08)
    m.pop()
    m.lathe(L(0, 0.185, 0), [(0.03, 0.0), (0.018, 0.02), (0.035, 0.05), (0.0, 0.045)], 7)
    dyed(m, "plain", 0.9)
    gbox(m, (0, 0.1, 0.071), (0.08, 0.06, 0.004), 0.0, "x")                                      # the stencil mark
    return m


def build():
    col = kit.collection("Shop")
    kit.clear_default_scene()
    makers = (lambda: counter("shop_counter", False), lambda: counter("bar_counter", True), shelf,
              bottle, bottle_square, jug, tin, jar, box, bolts, sack)
    objs = [fn().finish(col, sharp_angle=35.0) for fn in makers]
    for i, ob in enumerate(objs):        # its own row in canaan_models.blend
        ob.location = (i * 1.5, -70.0, 0.0)
    return objs


if "ARGS" in globals() or __name__ == "__main__":
    args = globals().get("ARGS", {})
    objs = build()
    print(kit.stats(objs))
    if not args.get("noexport"):
        print("exported", kit.export_fbx("Shop", "shop"))
