"""Weapons, held at the origin with the barrel along the game's +z and the sights up (+y).
    Tools/blender/bmcp.py run Tools/blender/weapons.py
Collection "Weapons" -> Assets/Canaan/Models/weapons.fbx. Lengths follow WeaponDB (muzzleZ, foreZ).
"""
import importlib
import math

import kit

importlib.reload(kit)
from kit import L, Model, Pal, gbeam, gbox, gcyl, hexcol, shade

WALNUT, BLUED, BRASS, STEEL = hexcol(0x5A3A22), Pal.GunBlue, Pal.Brass, Pal.Steel


def revolver(name, muzzle, frame_tint, grip_tint, barrel_r=0.0085):
    m = Model(name)
    m.surface("boards", grip_tint, 0.04)
    rings = []
    for t, (y, z, hw, hd) in enumerate(((0.012, -0.004, 0.0125, 0.020), (-0.020, -0.012, 0.0135, 0.021), (-0.055, -0.026, 0.0145, 0.022), (-0.088, -0.042, 0.0150, 0.024),
                                        (-0.100, -0.048, 0.0130, 0.020))):
        rings.append([L(hw * math.cos(a), y + 0.25 * hd * math.sin(a), z + hd * math.sin(a)) for a in [i * 2 * math.pi / 10 for i in range(10)]])
    m.loft(rings, True, 1)
    m.surface("steel", frame_tint, 0.02)
    gbox(m, (0, 0.036, 0.045), (0.022, 0.050, 0.110), 0.006, "z")                 # frame
    gbox(m, (0, 0.062, 0.035), (0.016, 0.010, 0.120), 0.003, "z")                 # top strap
    gcyl(m, (0, 0.040, 0.030), (0, 0.040, 0.074), 0.0205, 0.0205, 12, 0.003)      # cylinder
    for k in range(6):
        a = k * math.pi / 3
        gcyl(m, (0.0205 * math.cos(a), 0.040 + 0.0205 * math.sin(a), 0.034), (0.0205 * math.cos(a), 0.040 + 0.0205 * math.sin(a), 0.062), 0.0045, 0.0045, 6, 0)
    gcyl(m, (0, 0.050, 0.090), (0, 0.050, muzzle), barrel_r, barrel_r * 0.94, 12, 0.001)
    gcyl(m, (0, 0.030, 0.090), (0, 0.030, min(muzzle - 0.03, 0.19)), 0.0045, 0.0045, 8, 0.001)   # ejector rod housing
    gbox(m, (0, 0.066, muzzle - 0.012), (0.003, 0.010, 0.012), 0.001)             # front sight
    gbeam(m, (0, 0.058, -0.012), (0, 0.082, -0.034), 0.008, 0.012, 0.002)         # hammer
    m.tube([L(0, 0.008, 0.012), L(0, -0.018, 0.020), L(0, -0.026, 0.045), L(0, -0.016, 0.068), L(0, 0.010, 0.072)], 0.0028, 6)   # trigger guard
    gbeam(m, (0, 0.008, 0.044), (0, -0.014, 0.036), 0.004, 0.006, 0.001)          # trigger
    m.mark("muzzle", L(0, 0.050, muzzle))
    return m


def stock(m, drop=0.115, length=0.34, tint=WALNUT):
    """Wrist and butt, from the grip back; the comb drops towards the shoulder."""
    m.surface("boards", tint, 0.04)
    st = [(-0.02, 0.006, -0.026, 0.0155), (-0.07, -0.006, -0.050, 0.0165), (-0.14, -0.018, -0.082, 0.0180), (-0.24, -0.028, -0.118, 0.0190), (-length, -0.030, -(drop + 0.025), 0.0195)]
    rings = []
    for z, top, bottom, hw in st:
        mid, hh = (top + bottom) * 0.5, (top - bottom) * 0.5
        rings.append([L(hw * math.cos(a), mid + hh * math.sin(a), z) for a in [i * 2 * math.pi / 12 for i in range(12)]])
    m.loft(rings, True, 1)
    m.surface("steel", STEEL, 0.02)
    gbox(m, (0, -0.0825 + (0.0275 - drop * 0.5) * 0 - 0.0, -length - 0.004), (0.036, drop + 0.02 - 0.045, 0.008), 0.003)     # butt plate


def long_gun(name, muzzle, fore, kind):
    m = Model(name)
    stock(m, 0.115, 0.34, WALNUT if kind != "varmint" else hexcol(0x6B4A2E))
    thin = 0.85 if kind == "varmint" else 1.0
    # fore-end
    m.surface("boards", WALNUT if kind != "varmint" else hexcol(0x6B4A2E), 0.04)
    rings = []
    for z, hw, hh in ((fore - 0.13, 0.017, 0.020), (fore - 0.05, 0.0185, 0.021), (fore + 0.06, 0.0175, 0.019), (fore + 0.12, 0.014, 0.014)):
        rings.append([L(hw * thin * math.cos(a), 0.004 + hh * math.sin(a), z) for a in [i * 2 * math.pi / 12 for i in range(12)]])
    m.loft(rings, True, 1)
    # receiver
    m.surface("steel", BRASS if kind == "repeater" else BLUED, 0.02)
    gbox(m, (0, 0.012, 0.07), (0.034 * thin, 0.058, 0.20), 0.007, "z")
    m.surface("steel", BLUED, 0.02)
    gbeam(m, (0, 0.044, -0.022), (0, 0.068, -0.045), 0.008, 0.012, 0.002)         # hammer
    if kind == "shotgun":
        for x in (-0.0125, 0.0125):
            gcyl(m, (x, 0.026, 0.16), (x, 0.026, muzzle), 0.0125, 0.012, 12, 0.001)
        gbox(m, (0, 0.036, (0.16 + muzzle) * 0.5), (0.008, 0.004, muzzle - 0.16), 0.001, "z")      # rib
        gcyl(m, (0, 0.045, muzzle - 0.01), (0, 0.049, muzzle - 0.01), 0.003, 0.003, 6, 0)
        m.mark("muzzle", L(0, 0.026, muzzle))
    else:
        r = 0.0105 * thin
        gcyl(m, (0, 0.030, 0.16), (0, 0.030, muzzle), r, r * 0.9, 12, 0.001)
        if kind in ("repeater", "varmint"):
            gcyl(m, (0, 0.009, 0.17), (0, 0.009, muzzle - 0.06), 0.0085, 0.0085, 10, 0.001)       # magazine tube
            m.surface("steel", BRASS if kind == "repeater" else STEEL, 0.02)
            gbox(m, (0, 0.02, muzzle - 0.10), (0.03 * thin, 0.05, 0.012), 0.003)                # barrel band
            m.surface("steel", BLUED, 0.02)
        gbox(m, (0, 0.046, muzzle - 0.02), (0.003, 0.012, 0.012), 0.001)                         # front sight
        gbox(m, (0, 0.047, 0.20), (0.016, 0.008, 0.012), 0.002)                                  # rear sight
        if kind == "rifle":
            gcyl(m, (0, 0.052, 0.20), (0, 0.052, 0.62), 0.0075, 0.0075, 8, 0.001)                # long brass scope
            for z in (0.24, 0.56):
                gbox(m, (0, 0.042, z), (0.010, 0.014, 0.012), 0.002)
        m.mark("muzzle", L(0, 0.030, muzzle))
    # lever or trigger guard
    if kind in ("repeater", "varmint"):
        m.tube([L(0, -0.016, 0.06), L(0, -0.042, 0.055), L(0, -0.05, 0.0), L(0, -0.046, -0.06), L(0, -0.03, -0.085), L(0, -0.018, -0.06), L(0, -0.026, -0.01), L(0, -0.018, 0.02)], 0.0035, 6)
    else:
        m.tube([L(0, -0.016, 0.06), L(0, -0.04, 0.05), L(0, -0.044, 0.01), L(0, -0.03, -0.02), L(0, -0.016, -0.02)], 0.003, 6)
    gbeam(m, (0, -0.016, 0.026), (0, -0.034, 0.018), 0.004, 0.006, 0.001)
    return m


def knife():
    m = Model("w_knife")
    m.surface("boards", WALNUT, 0.04)
    rings = [[L(hw * math.cos(a), hh * math.sin(a), z) for a in [i * 2 * math.pi / 10 for i in range(10)]]
             for z, hw, hh in ((-0.055, 0.010, 0.014), (-0.03, 0.012, 0.016), (0.02, 0.0115, 0.0155), (0.052, 0.010, 0.014))]
    m.loft(rings, True, 1)
    m.surface("steel", BRASS, 0.0)
    gbox(m, (0, 0, 0.056), (0.012, 0.046, 0.008), 0.002)
    m.surface("steel", STEEL, 0.0)
    blade = [(0.06, 0.016, 0.0022), (0.12, 0.017, 0.0020), (0.19, 0.014, 0.0016), (0.225, 0.008, 0.001), (0.245, 0.001, 0.0005)]
    m.loft([[L(0, hh, z), L(th, hh * 0.2, z), L(0, -hh, z), L(-th, hh * 0.2, z)] for z, hh, th in blade], True, 0, smooth=False)
    return m


def bow():
    m = Model("w_bow")
    m.surface("boards", hexcol(0x7A5230), 0.04)
    pts, radii = [], []
    for i in range(17):
        a = math.radians(-64 + 128 * i / 16.0)
        pts.append(L(0, math.sin(a) * 0.72, math.cos(a) * 0.3 - 0.2))
        radii.append(0.012 - 0.006 * abs(i - 8) / 8.0)
    m.tube(pts, 0.011, 8, radii=radii)
    m.surface("leather", Pal.LeatherDark, 0.03)
    gcyl(m, (0, -0.06, 0.10), (0, 0.06, 0.10), 0.0145, 0.0145, 10, 0.002)
    m.surface("straw", Pal.Rope, 0.0)
    top = L(0, math.sin(math.radians(64)) * 0.72, math.cos(math.radians(64)) * 0.3 - 0.2)
    bot = L(0, -math.sin(math.radians(64)) * 0.72, math.cos(math.radians(64)) * 0.3 - 0.2)
    m.tube([bot, top], 0.0022, 5)
    return m


def build():
    col = kit.collection("Weapons")
    kit.clear_default_scene()
    models = [
        revolver("w_pilgrim", 0.25, BLUED, WALNUT),
        revolver("w_wasp", 0.20, STEEL, hexcol(0xD9CFBA), 0.0078),
        long_gun("w_drover", 0.74, 0.36, "repeater"),
        long_gun("w_longshot", 0.90, 0.40, "rifle"),
        long_gun("w_thunder", 0.70, 0.34, "shotgun"),
        long_gun("w_magpie", 0.72, 0.34, "varmint"),
        knife(), bow(),
    ]
    objs = [mm.finish(col, sharp_angle=40.0) for mm in models]
    kit.layout(objs, 0.5, 8)
    return objs


if "ARGS" in globals() or __name__ == "__main__":
    args = globals().get("ARGS", {})
    objs = build()
    print(kit.stats(objs))
    if not args.get("noexport"):
        print("exported", kit.export_fbx("Weapons", "weapons"))
