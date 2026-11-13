"""Four-legged animals other than the horse, on the game's quadruped skeleton with each animal's own proportions
(the presets of QuadSpec in QuadBuilder.cs).
    Tools/blender/bmcp.py run Tools/blender/quad.py [only=ox,deer] [noexport=1]
One collection and one FBX per animal: Assets/Canaan/Models/animal_<kind>.fbx with <kind>_body and, where the
males carry them, <kind>_horns.

Built the way horse.py builds the horse: sections along the body, the neck and the head (kit.profile_ring), each
family with its own tables - cattle (straight back, deep barrel, square rump, dewlap), the bison (hump, shaggy
forequarters, beard), deer and pronghorn (slender, tucked flank), wolf and coyote (deep chest, waist, paws, ruff,
brush), the rabbit; cloven hooves or paws with toes; strands of long fur; baked shade (kit.bake_ao).
The joints are exactly QuadBuilder.Build's for the preset (the game binds the mesh to them); the body is free
round them: cattle and bison carry their backs well above their spine bones, as real ones do.

Colour alpha = the part the game dyes: 1 coat, 2 mane/tail/long fur, 3 lower legs, 4 muzzle and nose,
5 hooves or paws, 6 rump, 8 pasterns (leg colour), 9 belly, 10-11 coat to legs, 12 coat to muzzle; 0 keeps its
colour (eyes, claws, the line of the mouth, horns).
"""
import importlib
import math
import random

import kit

importlib.reload(kit)
from kit import L, Skinned, bake_ao, hexcol, profile_ring, ribbon, sheet
from mathutils import Vector

COAT, MANE, LEGS, MUZZLE, HOOF, RUMP, SOCKS, BELLY = 1, 2, 3, 4, 5, 6, 8, 9
LEG1, LEG2, MUZ1 = 10, 11, 12
WHITE = (1.0, 1.0, 1.0)
QB = ["Pelvis", "Spine", "Chest", "Neck", "Head", "Tail1", "Tail2", "FLa", "FLb", "FLc", "FRa", "FRb", "FRc",
      "HLa", "HLb", "HLc", "HRa", "HRb", "HRc"]
QB_PARENT = [-1, 0, 1, 2, 3, 0, 5, 2, 7, 8, 2, 10, 11, 0, 13, 14, 0, 16, 17]
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))


class Spec:
    def __init__(self, **kw):
        self.legLen = self.legThick = self.bodyLen = self.bodyW = self.bodyD = 1.0
        self.neckLen, self.neckAngle, self.neckThick = 1.0, 53.0, 1.0
        self.headLen, self.headSize, self.headDrop = 1.0, 1.0, 42.0
        self.tailLen, self.tailThick, self.tailAngle = 1.0, 1.0, 12.0
        self.earLen, self.hump, self.ruff = 1.0, 0.0, 0.0
        self.pads, self.horns, self.beard = False, None, False
        self.__dict__.update(kw)


PRESETS = {
    "ox": Spec(legLen=0.80, legThick=1.25, bodyLen=1.05, bodyW=1.25, bodyD=1.2, neckLen=0.55, neckAngle=12, neckThick=1.45, headLen=0.8,
               headSize=1.32, headDrop=58, tailLen=1.0, tailThick=0.35, tailAngle=4, earLen=0.9, horns="cow"),
    "cow": Spec(legLen=0.80, legThick=1.1, bodyLen=1.05, bodyW=1.15, bodyD=1.2, neckLen=0.55, neckAngle=12, neckThick=1.45, headLen=0.8,
                headSize=1.32, headDrop=58, tailLen=1.0, tailThick=0.35, tailAngle=4, earLen=0.9, horns="cow"),
    "bison": Spec(legLen=0.80, legThick=1.3, bodyLen=1.05, bodyW=1.25, bodyD=1.2, neckLen=0.55, neckAngle=-8, neckThick=1.7, headLen=0.9,
                  headSize=1.45, headDrop=70, tailLen=0.6, tailThick=0.4, tailAngle=6, earLen=0.5, hump=1.0, ruff=1.0, horns="bison", beard=True),
    "pronghorn": Spec(legLen=1.12, legThick=0.6, bodyLen=0.9, bodyW=0.82, bodyD=0.86, neckLen=0.82, neckAngle=66, neckThick=0.75, headLen=0.72,
                      headSize=0.86, headDrop=28, tailLen=0.16, tailThick=0.9, tailAngle=30, earLen=1.1, horns="pronghorn"),
    "deer": Spec(legLen=1.1, legThick=0.6, bodyLen=0.92, bodyW=0.84, bodyD=0.88, neckLen=0.9, neckAngle=62, neckThick=0.78, headLen=0.74,
                 headSize=0.84, headDrop=30, tailLen=0.22, tailThick=1.0, tailAngle=25, earLen=1.5, horns="antlers"),
    "wolf": Spec(legLen=0.92, legThick=0.8, bodyLen=0.92, bodyW=0.72, bodyD=0.74, neckLen=0.5, neckAngle=22, neckThick=1.15, headLen=0.68,
                 headSize=1.1, headDrop=14, tailLen=0.72, tailThick=1.25, tailAngle=40, earLen=1.25, ruff=0.5, pads=True),
    "coyote": Spec(legLen=0.92, legThick=0.8, bodyLen=0.92, bodyW=0.72, bodyD=0.74, neckLen=0.5, neckAngle=22, neckThick=1.15, headLen=0.68,
                   headSize=1.1, headDrop=14, tailLen=0.72, tailThick=1.25, tailAngle=40, earLen=1.25, ruff=0.25, pads=True),
    "rabbit": Spec(legLen=0.50, legThick=1.1, bodyLen=0.62, bodyW=0.95, bodyD=0.92, neckLen=0.28, neckAngle=35, neckThick=1.5, headLen=0.5,
                   headSize=1.6, headDrop=12, tailLen=0.1, tailThick=1.6, tailAngle=60, earLen=5.5, pads=True),
}
FAMILY = {"ox": "cattle", "cow": "cattle", "bison": "bison", "pronghorn": "deer", "deer": "deer", "wolf": "dog", "coyote": "dog", "rabbit": "rabbit"}

# ---------------------------------------------------------------- shapes
# body: z / bodyLen, top, bottom (heights before the preset's leg shift dy), where the section is widest (share of the
# depth from the bottom), half width / bodyW
BODY = {
    "cattle": [(-0.89, 1.50, 1.20, 0.55, 0.06), (-0.86, 1.58, 1.07, 0.55, 0.16), (-0.80, 1.62, 0.96, 0.55, 0.25), (-0.68, 1.63, 0.90, 0.55, 0.30),
               (-0.52, 1.64, 0.86, 0.52, 0.32), (-0.36, 1.62, 0.83, 0.48, 0.31), (-0.18, 1.61, 0.80, 0.46, 0.31), (0.00, 1.61, 0.79, 0.46, 0.315),
               (0.18, 1.615, 0.80, 0.48, 0.31), (0.34, 1.625, 0.83, 0.50, 0.29), (0.48, 1.63, 0.86, 0.55, 0.26), (0.58, 1.57, 0.89, 0.55, 0.23),
               (0.65, 1.48, 0.93, 0.55, 0.18), (0.70, 1.38, 0.98, 0.55, 0.12), (0.73, 1.28, 1.04, 0.55, 0.04)],
    "bison": [(-0.89, 1.50, 1.18, 0.55, 0.05), (-0.86, 1.58, 1.06, 0.55, 0.14), (-0.80, 1.62, 0.97, 0.55, 0.22), (-0.68, 1.63, 0.91, 0.55, 0.27),
              (-0.52, 1.64, 0.87, 0.52, 0.285), (-0.36, 1.65, 0.84, 0.48, 0.29), (-0.18, 1.68, 0.81, 0.46, 0.30), (0.00, 1.75, 0.79, 0.46, 0.31),
              (0.18, 1.85, 0.79, 0.50, 0.31), (0.34, 1.94, 0.80, 0.55, 0.30), (0.48, 1.96, 0.82, 0.58, 0.28), (0.60, 1.86, 0.86, 0.60, 0.25),
              (0.70, 1.70, 0.92, 0.60, 0.20), (0.77, 1.50, 1.00, 0.60, 0.12), (0.80, 1.34, 1.08, 0.60, 0.05)],
    "deer": [(-0.885, 1.33, 1.17, 0.55, 0.05), (-0.86, 1.39, 1.08, 0.55, 0.11), (-0.80, 1.44, 1.01, 0.55, 0.18), (-0.70, 1.47, 0.99, 0.55, 0.24),
             (-0.56, 1.48, 0.98, 0.55, 0.265), (-0.40, 1.48, 0.96, 0.52, 0.27), (-0.22, 1.475, 0.90, 0.48, 0.275), (-0.04, 1.475, 0.83, 0.47, 0.285),
             (0.14, 1.48, 0.80, 0.48, 0.285), (0.30, 1.50, 0.80, 0.50, 0.275), (0.44, 1.51, 0.83, 0.55, 0.255), (0.56, 1.48, 0.88, 0.55, 0.22),
             (0.66, 1.40, 0.95, 0.55, 0.17), (0.73, 1.30, 1.02, 0.55, 0.11), (0.765, 1.21, 1.10, 0.55, 0.045)],
    "dog": [(-0.86, 1.36, 1.18, 0.50, 0.05), (-0.83, 1.41, 1.10, 0.50, 0.12), (-0.76, 1.45, 1.04, 0.50, 0.19), (-0.64, 1.465, 1.02, 0.50, 0.22),
            (-0.50, 1.47, 1.02, 0.50, 0.21), (-0.34, 1.47, 1.00, 0.48, 0.20), (-0.16, 1.475, 0.93, 0.46, 0.22), (0.02, 1.48, 0.85, 0.45, 0.25),
            (0.18, 1.49, 0.80, 0.45, 0.27), (0.34, 1.50, 0.79, 0.48, 0.27), (0.48, 1.50, 0.82, 0.52, 0.255), (0.60, 1.46, 0.88, 0.55, 0.22),
            (0.70, 1.38, 0.95, 0.55, 0.16), (0.76, 1.28, 1.02, 0.55, 0.10), (0.79, 1.20, 1.10, 0.55, 0.04)],
    "rabbit": [(-0.86, 1.34, 1.04, 0.50, 0.06), (-0.80, 1.43, 0.91, 0.50, 0.21), (-0.66, 1.48, 0.84, 0.48, 0.31), (-0.48, 1.49, 0.82, 0.46, 0.33),
               (-0.28, 1.47, 0.83, 0.46, 0.31), (-0.06, 1.44, 0.85, 0.48, 0.28), (0.16, 1.41, 0.87, 0.50, 0.26), (0.36, 1.38, 0.91, 0.50, 0.22),
               (0.52, 1.34, 0.95, 0.50, 0.17), (0.64, 1.28, 1.00, 0.50, 0.11), (0.72, 1.22, 1.06, 0.50, 0.05)],
}
# section shape: exponent over, under (p > 2 fuller, < 2 pointier), taper over, under
BODY_SHAPE = {"cattle": (2.6, 2.6, 0.12, 0.1), "bison": (2.0, 2.4, 0.2, 0.12), "deer": (2.1, 2.0, 0.18, 0.25), "dog": (2.0, 1.9, 0.18, 0.3),
              "rabbit": (2.0, 2.0, 0.1, 0.1)}
BELLY_SHARE = {"ox": 0.2, "cow": 0.2, "bison": 0.22, "pronghorn": 0.45, "deer": 0.24, "wolf": 0.3, "coyote": 0.32, "rabbit": 0.36}
# neck: t along the neck, crest, throat, half width (times neckThick and the family's crest/throat/width factors)
NECK = [(-0.30, 0.25, 0.20, 0.190), (-0.12, 0.25, 0.23, 0.185), (0.00, 0.24, 0.25, 0.170), (0.20, 0.205, 0.20, 0.145),
        (0.40, 0.18, 0.165, 0.125), (0.60, 0.165, 0.135, 0.110), (0.80, 0.145, 0.11, 0.095), (0.92, 0.125, 0.10, 0.088),
        (1.00, 0.10, 0.095, 0.082), (1.06, 0.07, 0.085, 0.070)]
NECK_K = {"cattle": (0.85, 1.2, 1.0), "bison": (1.0, 1.0, 1.0), "deer": (0.8, 0.75, 0.8), "dog": (0.8, 0.8, 0.82), "rabbit": (0.55, 0.5, 0.55)}
# head: t along the head, over, under, half width (times headSize), taper over, taper under
HEAD = {
    "cattle": [(-0.14, 0.070, 0.080, 0.075, 0.3, 0.3), (-0.02, 0.095, 0.120, 0.098, 0.3, 0.3), (0.10, 0.098, 0.150, 0.100, 0.3, 0.2),
               (0.22, 0.094, 0.150, 0.092, 0.3, 0.15), (0.36, 0.088, 0.128, 0.080, 0.3, 0.2), (0.50, 0.080, 0.108, 0.073, 0.3, 0.25),
               (0.64, 0.076, 0.096, 0.072, 0.25, 0.25), (0.78, 0.076, 0.092, 0.078, 0.2, 0.2), (0.90, 0.072, 0.088, 0.080, 0.2, 0.2),
               (0.98, 0.060, 0.080, 0.074, 0.2, 0.2), (1.03, 0.038, 0.062, 0.058, 0.2, 0.25), (1.06, 0.012, 0.035, 0.030, 0.2, 0.3)],
    "deer": [(-0.14, 0.070, 0.085, 0.070, 0.3, 0.3), (-0.02, 0.090, 0.130, 0.085, 0.35, 0.3), (0.10, 0.100, 0.160, 0.090, 0.3, 0.2),
             (0.24, 0.095, 0.150, 0.080, 0.3, 0.2), (0.40, 0.080, 0.110, 0.060, 0.3, 0.25), (0.56, 0.070, 0.085, 0.050, 0.25, 0.25),
             (0.72, 0.062, 0.070, 0.045, 0.25, 0.25), (0.86, 0.058, 0.065, 0.045, 0.2, 0.2), (0.96, 0.050, 0.060, 0.040, 0.2, 0.2),
             (1.03, 0.030, 0.045, 0.030, 0.2, 0.25), (1.07, 0.010, 0.025, 0.015, 0.2, 0.3)],
    "dog": [(-0.14, 0.070, 0.080, 0.075, 0.3, 0.3), (-0.02, 0.100, 0.110, 0.095, 0.35, 0.3), (0.12, 0.100, 0.120, 0.100, 0.35, 0.25),
            (0.26, 0.085, 0.100, 0.085, 0.3, 0.25), (0.36, 0.060, 0.085, 0.060, 0.3, 0.3), (0.50, 0.050, 0.075, 0.045, 0.25, 0.3),
            (0.66, 0.045, 0.065, 0.040, 0.25, 0.3), (0.80, 0.042, 0.058, 0.036, 0.2, 0.3), (0.92, 0.040, 0.050, 0.033, 0.2, 0.3),
            (1.00, 0.030, 0.040, 0.026, 0.2, 0.3), (1.04, 0.012, 0.020, 0.012, 0.2, 0.3)],
    "rabbit": [(-0.14, 0.060, 0.060, 0.060, 0.2, 0.2), (0.00, 0.090, 0.090, 0.085, 0.2, 0.2), (0.20, 0.100, 0.100, 0.095, 0.2, 0.2),
               (0.45, 0.080, 0.085, 0.075, 0.2, 0.2), (0.70, 0.060, 0.065, 0.055, 0.2, 0.2), (0.90, 0.045, 0.050, 0.040, 0.2, 0.2),
               (1.02, 0.020, 0.030, 0.020, 0.2, 0.2)],
}
HEAD["bison"] = [(t, o * 1.08, u, w * 1.1, a, b) for t, o, u, w, a, b in HEAD["cattle"]]
# neck section: exponent over, under, taper over, under (the horse's crest is narrow; cattle carry a broad neck)
NECK_SHAPE = {"cattle": (2.3, 2.0, 0.2, 0.3), "bison": (2.2, 2.0, 0.3, 0.3), "deer": (1.8, 1.9, 0.4, 0.4), "dog": (2.0, 2.0, 0.3, 0.3),
              "rabbit": (2.0, 2.0, 0.2, 0.2)}
HEAD_P = {"cattle": (2.6, 2.2), "bison": (2.6, 2.2), "deer": (2.0, 2.0), "dog": (2.2, 2.0), "rabbit": (2.0, 2.0)}
# legs: y (times legLen), forward shift (times legLen), front, back, half width (times legThick), taper front, back
HOOVED_FRONT = [
    (1.32, 0.000, 0.055, 0.065, 0.045, 0.2, 0.30), (1.20, 0.000, 0.085, 0.105, 0.072, 0.2, 0.30),
    (1.06, 0.000, 0.080, 0.110, 0.074, 0.2, 0.30), (0.98, 0.000, 0.074, 0.118, 0.072, 0.2, 0.35), (0.92, 0.000, 0.068, 0.110, 0.069, 0.2, 0.40),
    (0.85, 0.005, 0.066, 0.076, 0.066, 0.25, 0.40), (0.77, 0.008, 0.060, 0.058, 0.060, 0.25, 0.45), (0.69, 0.008, 0.052, 0.048, 0.052, 0.3, 0.50),
    (0.62, 0.006, 0.045, 0.042, 0.046, 0.3, 0.50), (0.565, 0.004, 0.042, 0.042, 0.045, 0.4, 0.40), (0.53, 0.004, 0.046, 0.046, 0.050, 0.5, 0.40),
    (0.49, 0.002, 0.040, 0.046, 0.045, 0.5, 0.60), (0.44, 0.000, 0.032, 0.046, 0.038, 0.5, 0.75), (0.34, 0.000, 0.030, 0.045, 0.036, 0.5, 0.75),
    (0.25, -0.002, 0.031, 0.048, 0.037, 0.5, 0.70), (0.20, -0.004, 0.034, 0.056, 0.042, 0.5, 0.55), (0.165, -0.002, 0.035, 0.058, 0.045, 0.5, 0.50),
    (0.13, 0.012, 0.032, 0.042, 0.040, 0.4, 0.45), (0.10, 0.026, 0.034, 0.038, 0.042, 0.4, 0.40), (0.078, 0.034, 0.040, 0.040, 0.047, 0.4, 0.30),
]
HOOVED_HIND = [
    (1.36, 0.000, 0.075, 0.105, 0.060, 0.2, 0.25), (1.24, 0.000, 0.118, 0.160, 0.092, 0.2, 0.25),
    (1.08, 0.000, 0.125, 0.165, 0.098, 0.2, 0.25), (1.00, 0.000, 0.122, 0.160, 0.094, 0.2, 0.30), (0.93, 0.000, 0.108, 0.148, 0.088, 0.25, 0.35),
    (0.86, 0.000, 0.090, 0.124, 0.078, 0.25, 0.40), (0.78, 0.000, 0.070, 0.100, 0.066, 0.3, 0.50), (0.70, 0.000, 0.055, 0.080, 0.055, 0.35, 0.60),
    (0.64, 0.000, 0.048, 0.072, 0.048, 0.4, 0.70), (0.60, 0.000, 0.050, 0.086, 0.052, 0.4, 0.60), (0.555, 0.000, 0.052, 0.070, 0.056, 0.4, 0.50),
    (0.51, 0.000, 0.044, 0.052, 0.046, 0.5, 0.60), (0.45, 0.000, 0.036, 0.050, 0.039, 0.5, 0.75), (0.34, 0.000, 0.033, 0.050, 0.037, 0.5, 0.75),
    (0.25, -0.002, 0.033, 0.052, 0.038, 0.5, 0.70), (0.20, -0.004, 0.035, 0.058, 0.042, 0.5, 0.55), (0.165, -0.002, 0.036, 0.058, 0.045, 0.5, 0.50),
    (0.13, 0.012, 0.033, 0.042, 0.040, 0.4, 0.45), (0.10, 0.026, 0.034, 0.038, 0.042, 0.4, 0.40), (0.078, 0.034, 0.040, 0.040, 0.046, 0.4, 0.30),
]
PAW_FRONT = [
    (1.32, 0.000, 0.045, 0.058, 0.042, 0.2, 0.3), (1.18, 0.000, 0.072, 0.096, 0.066, 0.2, 0.3),
    (1.06, 0.000, 0.075, 0.100, 0.070, 0.2, 0.3), (0.95, 0.000, 0.068, 0.100, 0.065, 0.2, 0.35), (0.86, 0.005, 0.060, 0.070, 0.058, 0.25, 0.4),
    (0.75, 0.008, 0.052, 0.052, 0.050, 0.3, 0.45), (0.62, 0.006, 0.042, 0.040, 0.042, 0.3, 0.5), (0.53, 0.004, 0.040, 0.042, 0.042, 0.4, 0.4),
    (0.45, 0.004, 0.034, 0.036, 0.036, 0.4, 0.5), (0.30, 0.010, 0.030, 0.032, 0.033, 0.4, 0.5), (0.17, 0.024, 0.032, 0.030, 0.035, 0.4, 0.5),
    (0.10, 0.042, 0.036, 0.030, 0.040, 0.4, 0.4), (0.06, 0.050, 0.036, 0.028, 0.040, 0.4, 0.4),
]
PAW_HIND = [
    (1.36, 0.000, 0.070, 0.098, 0.058, 0.2, 0.25), (1.22, 0.000, 0.115, 0.152, 0.090, 0.2, 0.25),
    (1.08, 0.000, 0.120, 0.160, 0.095, 0.2, 0.25), (0.98, 0.000, 0.110, 0.150, 0.090, 0.2, 0.3), (0.88, 0.000, 0.090, 0.120, 0.078, 0.25, 0.4),
    (0.78, 0.000, 0.068, 0.095, 0.064, 0.3, 0.5), (0.68, 0.000, 0.052, 0.075, 0.052, 0.35, 0.6), (0.60, 0.000, 0.048, 0.070, 0.048, 0.4, 0.6),
    (0.555, 0.000, 0.046, 0.062, 0.050, 0.4, 0.5), (0.48, 0.000, 0.036, 0.042, 0.038, 0.5, 0.6), (0.32, 0.004, 0.032, 0.036, 0.034, 0.5, 0.6),
    (0.17, 0.014, 0.032, 0.032, 0.035, 0.4, 0.5), (0.10, 0.032, 0.036, 0.030, 0.040, 0.4, 0.4), (0.06, 0.042, 0.036, 0.028, 0.040, 0.4, 0.4),
]
# how much heavier each family's upper legs are than the horse's: (front, back, width) for the fore and the hind leg;
# cattle have the deep 'round' of the thigh from the pins to the hock
BULK = {"cattle": ((0.25, 0.35, 0.15), (0.2, 1.0, 0.1)), "bison": ((0.3, 0.4, 0.2), (0.15, 0.8, 0.1)),
        "deer": ((0.1, 0.1, 0.05), (0.15, 0.6, 0.15)), "dog": ((0.1, 0.15, 0.1), (0.1, 0.35, 0.1)), "rabbit": ((0.25, 0.25, 0.3), (0.3, 0.8, 0.5))}
# one claw of a cloven hoof: y, forward shift (times legLen), front, back, half width (times legThick)
CLAW = [(0.08, 0.030, 0.034, 0.036, 0.020), (0.05, 0.036, 0.044, 0.038, 0.022), (0.02, 0.042, 0.054, 0.038, 0.023),
        (0.004, 0.046, 0.060, 0.038, 0.022), (0.0, 0.046, 0.058, 0.036, 0.020)]


def sm(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def game(co):
    return Vector((-co.x, co.z, -co.y))


def B(p):
    return L(p.x, p.y, p.z)


def interp(table, key):
    rows = sorted(table)
    if key <= rows[0][0]:
        return rows[0][1:]
    for a, b in zip(rows, rows[1:]):
        if key <= b[0]:
            t = (key - a[0]) / (b[0] - a[0])
            return tuple(a[i] + (b[i] - a[i]) * t for i in range(1, len(a)))
    return rows[-1][1:]


def surface_point(c, u, v, up, down, width, p_up, p_down, tt, tb, a, lift=0.0):
    """The point of a profile_ring section at angle a (0 = +u, pi/2 = +v), in the game's axes."""
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


def flat_across(path, normals):
    """For a strand along 'path' (game axes) over a surface with these normals: the way it lies flat (Blender)."""
    out = []
    for k in range(len(path)):
        d = path[min(k + 1, len(path) - 1)] - path[max(k - 1, 0)]
        a = d.cross(normals[k])
        out.append(B(a.normalized() if a.length > 1e-6 else X))
    return out


class Frame:
    """Joints and directions of one animal, exactly as QuadBuilder.Build lays them out."""

    def __init__(self, s):
        self.s = s
        self.dy = 1.2 * (s.legLen - 1.0)
        self.neck_base = self.body(0, 1.38 + s.hump * 0.06, 0.56)
        na = math.radians(s.neckAngle)
        self.neck_dir = Vector((0, math.sin(na), math.cos(na)))
        self.neck_len = 0.74 * s.neckLen
        self.neck_up = Vector((0, self.neck_dir.z, -self.neck_dir.y))
        self.poll = self.neck_base + self.neck_dir * self.neck_len
        ha = math.radians(-s.headDrop)
        self.head_dir = Vector((0, math.sin(ha), math.cos(ha)))
        self.head_len = 0.54 * s.headLen
        self.head_up = Vector((0, self.head_dir.z, -self.head_dir.y))
        if self.head_up.y < 0:
            self.head_up = -self.head_up
        self.tail_head = self.body(0, 1.38, -0.80)
        ta = math.radians(s.tailAngle)
        self.tail_dir = Vector((0, -math.cos(ta), -math.sin(ta)))
        self.tail_len = 0.85 * s.tailLen
        self.legs = {}
        for name, sx, front in (("FL", -1, True), ("FR", 1, True), ("HL", -1, False), ("HR", 1, False)):
            if front:
                self.legs[name] = [self.leg(sx * 0.19, 1.20, 0.46), self.leg(sx * 0.20, 0.95, 0.38), self.leg(sx * 0.20, 0.53, 0.41), self.leg(sx * 0.20, 0.0, 0.43)]
            else:
                self.legs[name] = [self.leg(sx * 0.19, 1.24, -0.54), self.leg(sx * 0.205, 0.90, -0.40), self.leg(sx * 0.205, 0.56, -0.65), self.leg(sx * 0.205, 0.0, -0.60)]

    def body(self, x, y, z):
        return Vector((x * self.s.bodyW, y + self.dy, z * self.s.bodyLen))

    def leg(self, x, y, z):
        return Vector((x * self.s.bodyW, y * self.s.legLen, z * self.s.bodyLen))

    def joints(self):
        p = {"Pelvis": tuple(self.body(0, 1.28, -0.52)), "Spine": tuple(self.body(0, 1.30, -0.12)), "Chest": tuple(self.body(0, 1.30, 0.32)),
             "Neck": tuple(self.neck_base), "Head": tuple(self.poll), "Tail1": tuple(self.tail_head),
             "Tail2": tuple(self.tail_head + self.tail_dir * self.tail_len * 0.5)}
        for leg, js in self.legs.items():
            for seg, c in enumerate("abc"):
                p[leg + c] = tuple(js[seg])
        return p

    def head_pt(self, t, side, up):
        hs = self.s.headSize
        return self.poll + self.head_dir * (self.head_len * t) + X * (side * hs) + self.head_up * (up * hs)


def animal(kind, s, col, arm):
    f = Frame(s)
    fam = FAMILY[kind]
    hs, lt, bl, ll = s.headSize, s.legThick, s.bodyLen, s.legLen
    m = Skinned(kind + "_body", QB)
    rng = random.Random(sum(ord(c) for c in kind) * 13)
    R = {}                                     # vertex ranges of the pieces, so colour regions never leak

    def mark():
        return len(m.bm.verts)

    def repart_in(keys, test, part):
        alpha = part / 255.0
        m.bm.verts.index_update()
        spans = [R[k] for k in keys]
        for fc in m.bm.faces:
            for lp in fc.loops:
                i = lp.vert.index
                if any(lo <= i < hi for lo, hi in spans) and test(game(lp.vert.co)):
                    c = lp[m.col]
                    lp[m.col] = (c[0], c[1], c[2], alpha)

    # ---------------------------------------------------------------- weights
    def w_body(co):
        z = game(co).z / bl
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
        t = (game(co) - f.neck_base).dot(f.neck_dir) / f.neck_len
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
        t = (game(co) - f.tail_head).dot(f.tail_dir) / max(f.tail_len, 1e-3)
        if t < 0.08:
            k = sm(-0.08, 0.08, t)
            return {"Pelvis": 1 - k, "Tail1": k}
        if t < 0.32:
            return {"Tail1": 1.0}
        k = sm(0.32, 0.6, t)
        return {"Tail1": 1 - k, "Tail2": k}

    def w_tail_hang(co):
        # cattle and bison: the root sits on top of the rump, above the tail joint, and stays with the pelvis;
        # what hangs below the joint swings from it
        y = game(co).y
        j1 = f.tail_head.y
        j2 = j1 - f.tail_len * 0.5
        if y > j1 + 0.04:
            return {"Pelvis": 1.0}
        if y > j1 - 0.06:
            k = sm(j1 + 0.04, j1 - 0.06, y)
            return {"Pelvis": 1 - k, "Tail1": k}
        if y > j2 + 0.06:
            return {"Tail1": 1.0}
        k = sm(j2 + 0.06, j2 - 0.12, y)
        return {"Tail1": 1 - k, "Tail2": k}

    def w_leg(leg):
        j0, j1, j2, g = f.legs[leg]
        body = "Chest" if leg[0] == "F" else "Pelvis"
        e = 0.06 * ll

        def fn(co):
            y = game(co).y
            if y > j1.y + e:
                k = sm(j0.y + 0.10 * ll, j0.y - 0.14 * ll, y)
                return {body: 1 - k, leg + "a": k}
            if y > j1.y - e:
                k = sm(j1.y + e, j1.y - e, y)
                return {leg + "a": 1 - k, leg + "b": k}
            if y > j2.y + e * 0.8:
                return {leg + "b": 1.0}
            if y > j2.y - e * 0.8:
                k = sm(j2.y + e * 0.8, j2.y - e * 0.8, y)
                return {leg + "b": 1 - k, leg + "c": k}
            return {leg + "c": 1.0}
        return fn

    def w_head(co):
        return {"Head": 1.0}

    # ---------------------------------------------------------------- body
    rows = BODY[fam]
    pu, pd, tt0, tb0 = BODY_SHAPE[fam]

    def section(zn):
        top, bot, wy, hw = interp(rows, zn)
        top += f.dy
        bot += f.dy
        return top, bot, bot + (top - bot) * wy, hw * s.bodyW

    def body_ring(zn):
        top, bot, ym, hw = section(zn)
        return profile_ring(Vector((0, ym, zn * bl)), X, Y, top - ym, ym - bot, hw, pu, pd, tt0, tb0, 22)

    lo = mark()
    m.surface("fur", WHITE, 0.0).set_part(COAT, w_body)
    zs, z = [], rows[0][0]
    while z < rows[-1][0] - 0.02:
        zs.append(z)
        z += 0.06
    zs.append(rows[-1][0])
    m.loft([body_ring(z) for z in zs], True, 1, wrap=tuple(B(Z)))
    R["body"] = (lo, mark())

    # ---------------------------------------------------------------- neck
    nt = s.neckThick
    kc, kth, kw_ = NECK_K[fam]

    def neck_c(t):
        return f.neck_base + f.neck_dir * (f.neck_len * t) - Y * (0.10 * max(0.0, 1.0 - t) ** 2)

    def neck_sec(t):
        crest, throat, hw = interp(NECK, t)
        return crest * nt * kc, throat * nt * kth, hw * nt * kw_

    def neck_t(p):
        return (p - f.neck_base).dot(f.neck_dir) / f.neck_len

    def neck_ring(t):
        cr, th, hw = neck_sec(t)
        npu, npd, ntu, ntd = NECK_SHAPE[fam]
        return profile_ring(neck_c(t), X, f.neck_up, cr, th, hw, npu, npd, ntu, ntd, 18)

    lo = mark()
    m.set_part(COAT, w_neck)
    m.loft([neck_ring(-0.30 + 1.36 * q / 14) for q in range(15)], True, 1, wrap=tuple(B(f.neck_dir)))
    R["neck"] = (lo, mark())

    # ---------------------------------------------------------------- head
    htab = HEAD[fam]
    hpu, hpd = HEAD_P[fam]
    t_first, t_last = htab[0][0], htab[-1][0]

    def head_c(t):
        return f.poll + f.head_dir * (f.head_len * t)

    def head_sec(t):
        up, down, hw, tt, tb = interp(htab, t)
        return up * hs, down * hs, hw * hs, tt, tb

    def head_point(t, a, lift=0.0):
        up, down, hw, tt, tb = head_sec(t)
        return surface_point(head_c(t), X, f.head_up, up, down, hw, hpu, hpd, tt, tb, a, lift)

    def head_ring(t):
        up, down, hw, tt, tb = head_sec(t)
        return profile_ring(head_c(t), X, f.head_up, up, down, hw, hpu, hpd, tt, tb, 18)

    def head_t(p):
        return (p - f.poll).dot(f.head_dir) / f.head_len

    lo = mark()
    m.set_part(COAT, w_head)
    m.loft([head_ring(t_first + (t_last - t_first) * q / 18) for q in range(19)], True, 1, wrap=tuple(B(f.head_dir)))
    R["head"] = (lo, mark())

    # ---------------------------------------------------------------- legs
    paws = s.pads
    lo = mark()
    for leg, js in f.legs.items():
        front = leg[0] == "F"
        table = (PAW_FRONT if front else PAW_HIND) if paws else (HOOVED_FRONT if front else HOOVED_HIND)
        sx = 1.0 if js[0].x > 0 else -1.0
        m.set_part(COAT, w_leg(leg))
        ys = [r[0] for r in table]
        fine = []
        for a, b in zip(ys, ys[1:]):
            steps = max(1, int(round((a - b) / 0.04)))
            fine += [a + (b - a) * q / steps for q in range(steps)]
        fine.append(ys[-1])
        rings = []
        j0, j1, j2, g = js
        kf, kb, kw = BULK[fam][0 if front else 1]
        for yr in fine:
            dz, fr, bk, w, tt, tb = interp(table, yr)
            # the extra muscle sits between the knee (hock) and the joint above the elbow (stifle); higher up the leg
            # is buried in the body, so nothing of it shows past the body's own outline
            up = (sm(0.5, 0.9, yr) if paws else sm(0.55, 0.9, yr)) * (1.0 - sm(1.0, 1.2, yr))
            fr, bk, w = fr * (1 + kf * up), bk * (1 + kb * up), w * (1 + kw * up)
            y = yr * ll
            if y >= j1.y:
                c = j1 + (j0 - j1) * ((y - j1.y) / max(j0.y - j1.y, 1e-4))
            elif y >= j2.y:
                c = j2 + (j1 - j2) * ((y - j2.y) / max(j1.y - j2.y, 1e-4))
            else:
                c = g + (j2 - g) * ((y - g.y) / max(j2.y - g.y, 1e-4))
            inward = -sx * s.bodyW * (0.022 * max(0.0, min(1.0, (y - j1.y) / (0.2 * ll))) + 0.07 * sm(1.12, 1.32, yr))
            c = Vector((c.x + inward, y, c.z + dz * ll))
            rings.append(profile_ring(c, X, Z, fr * lt, bk * lt, w * lt, 2.0, 1.8, tt, tb, 14))
        m.loft(rings, True, 0, wrap=tuple(B(Y)))
    R["legs"] = (lo, mark())

    # ---------------------------------------------------------------- muscle and bone under the hide
    def S(c, r, off):
        return (B(c), r, B(off))
    moves = []
    for sx in (-1, 1):
        if fam in ("cattle", "bison"):
            moves += [S(f.body(sx * 0.22, 1.60, -0.50), 0.09, Vector((sx * 0.02, 0.025, 0))),               # the hooks of the hips
                      S(f.body(sx * 0.10, 1.56, -0.84), 0.07, Vector((sx * 0.006, 0.015, -0.012))),         # the pins
                      S(f.body(sx * 0.27, 1.25, -0.30), 0.15, Vector((-sx * 0.016, 0, 0)))]                # the hollow before the hip
        if fam in ("deer", "dog"):
            moves += [S(f.body(sx * 0.20, 1.40, 0.45), 0.10, Vector((sx * 0.010, 0, 0))),                  # shoulder
                      S(f.body(sx * 0.22, 1.30, -0.62), 0.12, Vector((sx * 0.010, 0.006, 0)))]              # haunch
    if moves:
        m.sculpt(moves)

    # ---------------------------------------------------------------- colour regions of the hide
    def legs_part(p):
        base = (0.62 if p.z < 0 else 0.58) * ll
        if p.y < 0.24 * ll + 0.02 * math.sin(p.x * 60.0 + p.z * 40.0):
            return SOCKS
        if p.y < base:
            return LEGS
        if p.y < base + 0.04 * ll:
            return LEG2
        if p.y < base + 0.08 * ll:
            return LEG1
        return None
    for part in (LEG1, LEG2, LEGS, SOCKS):
        repart_in(["legs"], lambda p, pt=part: legs_part(p) == pt, part)

    hip_z = f.legs["HL"][0].z
    if fam == "bison":
        # dark woolly forequarters (the long hair's colour, so the strands lie on hide of their own colour), paler behind
        def woolly(p):          # a ragged edge where the long hair ends behind the shoulders
            return p.z > (-0.05 + 0.07 * math.sin(p.y * 11.0 + p.x * 5.0) + 0.04 * math.sin(p.y * 23.0 - p.x * 9.0)) * bl
        repart_in(["body"], lambda p: not woolly(p), RUMP)
        repart_in(["body"], woolly, MANE)
        repart_in(["neck"], lambda p: True, MANE)
        repart_in(["head"], lambda p: head_t(p) < 0.62, MANE)
        repart_in(["legs"], lambda p: p.z > 0 and p.y > 0.58 * ll, MANE)
    elif fam == "cattle":
        # the rump colour of a two-coloured beast is a belt round the middle, as on belted cattle
        repart_in(["body"], lambda p: -0.40 * bl < p.z + 0.03 * math.sin(p.y * 9.0) < 0.06 * bl, RUMP)
    else:
        repart_in(["body"], lambda p: p.z < -0.72 * bl, RUMP)
        repart_in(["legs"], lambda p: p.z < hip_z - 0.07 and p.y > f.legs["HL"][1].y, RUMP)

    bs = BELLY_SHARE[kind]

    def under(p):
        zn = p.z / bl
        if zn < rows[0][0] + 0.05 or zn > rows[-1][0] - 0.04:
            return False
        top, bot, ym, hw = section(zn)
        return p.y < bot + (top - bot) * bs
    repart_in(["body"], under, BELLY)
    if fam in ("deer", "dog", "rabbit"):
        repart_in(["legs"], lambda p: legs_part(p) is None and abs(p.x) < 0.185 * s.bodyW and p.y < f.legs["FL"][0].y, BELLY)

        def throat(p):
            t = neck_t(p)
            tc = max(-0.3, min(1.06, t))
            cr, th, hw = neck_sec(tc)
            return t > (0.55 if fam == "deer" else 0.05) and (p - neck_c(tc)).dot(f.neck_up) < -0.45 * th
        repart_in(["neck"], throat, BELLY)

        def chin(p):
            t = head_t(p)
            if t < 0.12:
                return False
            tc = max(t_first, min(t_last, t))
            up, down, hw, tt, tb = head_sec(tc)
            return (p - head_c(tc)).dot(f.head_up) < -0.55 * down
        repart_in(["head"], chin, BELLY)
    mz = {"cattle": (0.70, 0.80), "bison": (0.70, 0.80), "deer": (0.86, 0.93)}.get(fam)
    if mz:
        repart_in(["head"], lambda p: mz[0] <= head_t(p) < mz[1], MUZ1)
        repart_in(["head"], lambda p: head_t(p) >= mz[1], MUZZLE)

    # ---------------------------------------------------------------- the dewlap: a fold of skin from the throat to the brisket
    if fam == "cattle":
        m.surface("fur", WHITE, 0.0).set_part(COAT, w_neck)
        line = [head_point(0.40, -math.pi / 2, -0.02)]
        for t in (0.95, 0.75, 0.55, 0.35, 0.15):
            cr, th, hw = neck_sec(t)
            line.append(neck_c(t) - f.neck_up * (th - 0.02))
        top, bot, ym, hw = section(0.70)
        line.append(Vector((0, bot + 0.03, 0.70 * bl)))
        hang = [0.0, 0.015, 0.03, 0.045, 0.055, 0.05, 0.02]
        rows2 = []
        for p, h in zip(line, hang):
            h *= s.bodyD
            wv = 0.02 + 0.3 * h
            rows2.append([B(p + Vector((-wv, 0.004, 0))), B(p + Vector((-wv * 0.45, -h * 0.6, 0))), B(p + Vector((0, -h - 0.01, 0))),
                          B(p + Vector((wv * 0.45, -h * 0.6, 0))), B(p + Vector((wv, 0.004, 0)))])
        sheet(m, rows2, outward=B(X), double=True)

    # ---------------------------------------------------------------- hooves or paws
    for leg, js in f.legs.items():
        g = js[3]
        hind = leg[0] == "H"
        wl = lambda co, n=leg: {n + "c": 1.0}
        if paws:
            pw, pl, ph = 0.062 * lt, 0.080 * lt, 0.042 * lt
            if kind == "rabbit" and hind:
                pl *= 1.7
            pc = Vector((g.x, ph * 0.92, g.z + 0.045 * ll + 0.2 * pl))
            m.surface("fur", WHITE, 0.0).set_part(HOOF, wl)
            m.push(B(pc))
            m.blob((0, 0, 0), (pw, pl, ph), 1)
            m.pop()
            toes = []
            for k in range(4):
                outer = k in (0, 3)
                tc = pc + Vector(((k - 1.5) * 0.52 * pw, ph * (0.12 if outer else 0.2), pl * (0.48 if outer else 0.62)))
                toes.append(tc)
                m.push(B(tc))
                m.blob((0, 0, 0), (pw * 0.30, pl * 0.36, ph * 0.72), 0)
                m.pop()
            m.surface("horn", (0.22, 0.2, 0.19), 0.0).set_part(0, wl)
            for tc in toes:
                m.push(B(tc + Vector((0, -ph * 0.35, pl * 0.33))))
                m.blob((0, 0, 0), (pw * 0.09, pl * 0.13, ph * 0.25), 0)
                m.pop()
        else:
            m.surface("horn", WHITE, 0.0).set_part(HOOF, wl)
            for side in (-1, 1):                                                # the two claws of a cloven hoof
                rings = []
                for y, dz, fr, bk, w in CLAW:
                    c = Vector((g.x + side * 0.025 * lt, y * ll, g.z + dz * ll))
                    rings.append(profile_ring(c, X, Z, fr * lt, bk * lt, w * lt, 2.2, 1.6, 0.3, 0.3, 10))
                m.loft(rings, True, 0, wrap=tuple(B(Y)))
            m.surface("horn", (0.5, 0.45, 0.42), 0.0)
            for side in (-1, 1):                                                # dew claws behind the fetlock
                m.push(B(Vector((g.x + side * 0.026 * lt, 0.12 * ll, g.z - 0.045 * lt))))
                m.blob((0, 0, 0), (0.008 * lt, 0.011 * lt, 0.008 * lt), 0)
                m.pop()

    # ---------------------------------------------------------------- the face
    eye_t = {"cattle": 0.16, "bison": 0.16, "deer": 0.22, "dog": 0.27, "rabbit": 0.25}[fam]
    eye_a = {"cattle": 0.42, "bison": 0.42, "deer": 0.30, "dog": 0.50, "rabbit": 0.25}[fam]
    eye_r = {"cattle": 0.018, "bison": 0.016, "deer": 0.022, "dog": 0.013, "rabbit": 0.024}[fam] * hs
    for sx in (-1, 1):
        a = eye_a if sx > 0 else math.pi - eye_a
        c = head_point(eye_t, a, -eye_r * 0.6)
        nrm = ((head_point(eye_t, a) - head_c(eye_t)).normalized() + f.head_dir * 0.3).normalized()
        m.surface("wet", (0.06, 0.04, 0.03), 0.0).set_part(0, w_head)
        m.push(B(c))
        m.blob((0, 0, 0), (eye_r, eye_r, eye_r), 1)
        m.pop()
        up_e = (f.head_up - nrm * f.head_up.dot(nrm)).normalized()
        along = nrm.cross(up_e).normalized()
        m.surface("fur", (0.8, 0.78, 0.76), 0.0).set_part(COAT, w_head)
        for a0, a1, r in ((0.15, math.pi - 0.15, eye_r * 0.32), (math.pi + 0.35, 2 * math.pi - 0.35, eye_r * 0.25)):
            pts = [c + nrm * (eye_r * 0.3) + along * (math.cos(t) * eye_r * 1.15) + up_e * (math.sin(t) * eye_r * 0.85)
                   for t in [a0 + (a1 - a0) * k / 6 for k in range(7)]]
            m.tube([B(p) for p in pts], r, 5)
    # the nose: a moist pad at the tip of the muzzle, turned with the head
    m.surface("wet", (0.75, 0.7, 0.68), 0.0).set_part(MUZZLE, w_head)
    lift_nose = {"cattle": -0.012, "bison": -0.012, "deer": 0.0, "dog": 0.010, "rabbit": 0.0}[fam] * hs
    pad = {"cattle": (0.055, 0.04, 0.022), "bison": (0.06, 0.042, 0.022), "deer": (0.034, 0.026, 0.016), "dog": (0.026, 0.02, 0.018),
           "rabbit": (0.018, 0.014, 0.01)}[fam]
    pw_, ph_, pd_ = pad[0] * hs, pad[1] * hs, pad[2] * hs
    tip = head_c(t_last) - f.head_dir * (pd_ * 0.85) + f.head_up * lift_nose
    m.push(B(tip), rot=(s.headDrop, 0, 0))
    m.blob((0, 0, 0), (pw_, pd_, ph_), 1)
    m.pop()
    m.surface("plain", (0.05, 0.04, 0.035), 0.0).set_part(0, w_head)                 # nostrils
    for sx in (-1, 1):
        n_c = tip + f.head_dir * (pd_ * 0.72) + X * (sx * pw_ * 0.5) + f.head_up * (ph_ * 0.05)
        m.push(B(n_c), rot=(s.headDrop, 0, 0))
        m.blob((0, 0, 0), (pw_ * 0.2, pd_ * 0.35, ph_ * 0.3), 0)
        m.pop()
    m.surface("plain", (0.08, 0.06, 0.055), 0.0).set_part(0, w_head)       # the line of the mouth
    for sx in (-1, 1):
        a0 = -0.55 if sx > 0 else math.pi + 0.55
        a1 = -math.pi / 2 if sx > 0 else 1.5 * math.pi
        line = [head_point(0.62 + 0.40 * q / 6, a0 + (a1 - a0) * (q / 6) ** 1.8, 0.0012) for q in range(7)]
        m.tube([B(p) for p in line], 0.0028 * hs, 4)

    # ears: cupped leaves; cattle hold them out to the sides, deer up and out, dogs upright, the rabbit's are long
    lo = mark()
    for sx in (-1, 1):
        if fam in ("cattle", "bison"):
            base = head_point(0.02, (0.25 if sx > 0 else math.pi - 0.25), -0.01)
            e = (X * sx * 0.9 - f.head_up * 0.15 - f.head_dir * 0.2).normalized()
            ln, wd = 0.13 * s.earLen * hs, 0.05 * hs
        elif fam == "deer":
            base = head_point(0.0, (0.75 if sx > 0 else math.pi - 0.75), -0.01)
            e = (f.head_up * 0.55 + X * sx * 0.75 - f.head_dir * 0.25).normalized()
            ln, wd = 0.16 * s.earLen * hs, 0.055 * hs
        elif fam == "dog":
            base = head_point(0.04, (1.0 if sx > 0 else math.pi - 1.0), -0.01)
            e = (f.head_up * 0.95 + X * sx * 0.22 - f.head_dir * 0.1).normalized()
            ln, wd = 0.11 * s.earLen * hs, 0.06 * hs
        else:
            base = head_point(0.05, (1.2 if sx > 0 else math.pi - 1.2), -0.01)
            e = (f.head_up * 0.9 + X * sx * 0.12 - f.head_dir * 0.35).normalized()
            ln, wd = 0.13 * s.earLen * hs * 0.32, 0.045 * hs
        f0 = (f.head_dir + X * sx * 0.5).normalized()
        fo = (f0 - e * f0.dot(e)).normalized()
        side = e.cross(fo).normalized()
        outer, inner = [], []
        triangular = fam == "dog"
        for q in range(7):
            u = -0.12 + 1.12 * q / 6
            x = (u + 0.12) / 1.12
            c = base + e * (ln * u)
            w = (wd * (1.0 - x) + 0.002) if triangular else wd * math.sin(math.pi * (0.25 + 0.75 * x ** 0.8)) + 0.002
            d = w * 0.6
            ro, ri = [], []
            for k in range(9):
                phi = math.radians(-135 + 270 * k / 8)
                ro.append(c - fo * (math.cos(phi) * d) + side * (math.sin(phi) * w))
                ri.append(c - fo * (math.cos(phi) * d * 0.72 - 0.003 * hs) + side * (math.sin(phi) * w * 0.8))
            outer.append([B(p) for p in ro])
            inner.append([B(p) for p in ri])
        m.surface("fur", WHITE, 0.0).set_part(COAT, w_head)
        sheet(m, outer, outward=B(-fo), grain="z")
        pale = fam in ("deer", "dog", "rabbit")
        m.surface("fur", (0.85, 0.8, 0.76) if pale else (0.6, 0.55, 0.52), 0.0).set_part(BELLY if pale else COAT, w_head)
        sheet(m, inner, outward=B(fo), grain="z")
    R["ears"] = (lo, mark())

    # ---------------------------------------------------------------- tails
    td = f.tail_dir
    side_t = td.cross(X).normalized()                      # across the tail, towards the rump
    lo = mark()
    if fam in ("cattle", "bison"):
        zr = rows[0][0] + 0.03
        root = Vector((0, section(zr)[0] - 0.04, zr * bl))
        p1 = root + Vector((0, -0.035, -0.04))
        p2 = p1 + Vector((0, -0.10, -0.012))
        end_y = max(0.25, root.y - 1.15 * f.tail_len)
        pts = [root, p1, p2]
        for q in range(1, 7):
            k = q / 6.0
            pts.append(Vector((0, p2.y + (end_y - p2.y) * k, p2.z - 0.02 * math.sin(math.pi * k))))
        radii = [0.05 * s.tailThick * (1.0 - 0.55 * k / (len(pts) - 1)) + 0.008 for k in range(len(pts))]
        radii[0] *= 1.4
        m.surface("fur", WHITE, 0.0).set_part(COAT, w_tail_hang)
        m.tube([B(p) for p in pts], 0.03, 8, radii=radii)
        m.surface("hair", WHITE, 0.06).set_part(MANE, w_tail_hang)           # the switch at its end
        end = pts[-1]
        long_ = 0.20 if fam == "cattle" else 0.13
        for k in range(16):
            a = 2 * math.pi * k / 16
            off = Vector((math.cos(a), 0, math.sin(a))) * 0.012
            path = [end + off * 0.6 + Vector((0, 0.07, 0)), end + off * 1.6 + Vector((0, -0.05, 0)),
                    end + off * 2.3 + Vector((0, -long_ - 0.04 * math.sin(a * 3), 0))]
            ribbon(m, [B(p) for p in path], [0.022, 0.017, 0.004], B(Vector((-math.sin(a), 0, math.cos(a)))))
    elif fam == "deer":
        m.surface("fur", WHITE, 0.0).set_part(COAT, w_tail)
        rings = []
        for t, r in ((-0.1, 0.035), (0.2, 0.05), (0.6, 0.052), (0.95, 0.03), (1.08, 0.008)):
            c = f.tail_head + td * (f.tail_len * t)
            rings.append(profile_ring(c, X, side_t, r * 0.55 * s.tailThick, r * 0.55 * s.tailThick, r * s.tailThick, 2.0, 2.0, 0.1, 0.1, 10))
        m.loft(rings, True, 1, wrap=tuple(B(td)))
    elif fam == "dog":
        # the brush: a lumpy core under strands of long hair, hanging in a curve
        m.surface("fur", WHITE, 0.05).set_part(COAT, w_tail)
        core = []

        def brush_r(t):
            return (0.045 + 0.04 * math.sin(math.pi * min(1.0, t * 1.1))) * s.tailThick * (1.0 - 0.7 * max(0.0, t - 0.8) / 0.2)

        def brush_c(t):
            return f.tail_head + td * (f.tail_len * t * 0.92) + Y * (-0.16 * f.tail_len * t * t) + Z * (0.06 * f.tail_len * t * t)
        for q in range(9):
            t = q / 8.0
            r = brush_r(t)
            core.append(profile_ring(brush_c(t), X, side_t, r, r, r, 2.0, 2.0, 0.1, 0.1, 10))
        m.loft(core, True, 1, wrap=tuple(B(td)))
        m.surface("fur", WHITE, 0.08).set_part(MANE, w_tail)
        for k in range(30):
            t = rng.uniform(0.12, 0.92)
            a = rng.uniform(0, 2 * math.pi)
            radial = (X * math.cos(a) + side_t * math.sin(a)).normalized()
            p0 = brush_c(t) + radial * (brush_r(t) * 0.9)
            along = (brush_c(min(1.0, t + 0.1)) - brush_c(t)).normalized()
            path = [p0, p0 + along * 0.06 + radial * 0.012, p0 + along * 0.13 + radial * 0.01]
            ribbon(m, [B(p) for p in path], [0.032, 0.024, 0.005], flat_across(path, [radial] * 3))
    else:
        m.surface("fur", WHITE, 0.0).set_part(BELLY, w_tail)                 # a cotton tail
        m.push(B(f.tail_head + td * (f.tail_len * 0.5)))
        r = 0.075 * s.tailThick
        m.blob((0, 0, 0), (r, r, r), 1, lumps=0.2)
        m.pop()
    R["tail"] = (lo, mark())
    if fam == "deer":                                                         # the pale underside of the tail
        repart_in(["tail"], lambda p: (p - f.tail_head).dot(side_t) > 0.0, BELLY)

    # ---------------------------------------------------------------- long fur: the bison's forequarters, a dog's ruff
    if fam == "bison" or s.ruff > 0:
        m.bm.verts.index_update()
        m.bm.normal_update()
        pool = []
        if fam == "bison":
            def grows(i, p):
                if R["body"][0] <= i < R["body"][1]:
                    return p.z > (-0.12 + 0.07 * math.sin(p.y * 11.0 + p.x * 5.0)) * bl
                if R["neck"][0] <= i < R["neck"][1]:
                    return True
                if R["head"][0] <= i < R["head"][1]:
                    return head_t(p) < 0.6
                if R["legs"][0] <= i < R["legs"][1]:
                    return p.z > 0 and p.y > 0.6 * ll
                return False
            count, length, width, flow = 340, (0.10, 0.22), (0.04, 0.07), Vector((0, -1, -0.35))
        else:
            def grows(i, p):
                if R["neck"][0] <= i < R["neck"][1]:
                    return neck_t(p) < 0.9
                if R["body"][0] <= i < R["body"][1]:
                    return p.z > 0.45 * bl
                if R["head"][0] <= i < R["head"][1]:
                    return head_t(p) < 0.2
                return False
            count, length, width, flow = int(60 + 120 * s.ruff), (0.04, 0.085), (0.022, 0.035), Vector((0, -0.6, -1.0))
        for v in m.bm.verts:
            if v.normal.length > 0.5:
                p = game(v.co)
                if grows(v.index, p):
                    pool.append((p, (game(v.co + v.normal) - p).normalized()))
        m.surface("fur", WHITE, 0.08).set_part(MANE, (lambda co: w_body(co) if game(co).z < 0.55 * bl else w_neck(co)) if fam == "bison" else w_neck)
        for k in range(min(count, len(pool))):
            p0, n = rng.choice(pool)
            ln = rng.uniform(*length)
            d = flow - n * flow.dot(n)
            d = d.normalized() if d.length > 1e-4 else Vector((0, -1, 0))
            p1 = p0 + n * (0.08 * ln) + d * (ln * 0.5)
            p2 = p0 + n * (0.04 * ln) + d * ln + Vector((0, -0.02, 0))
            path = [p0 - n * 0.004, p1, p2]
            wd = rng.uniform(*width)
            ribbon(m, [B(p) for p in path], [wd, wd * 0.75, wd * 0.15], flat_across(path, [n, n, n]), double=False, outward=B(n))
        if s.beard:
            m.set_part(MANE, w_head)
            for k in range(30):
                a = rng.uniform(-0.6, 0.6)
                t = rng.uniform(0.3, 0.8)
                p0 = head_point(t, -math.pi / 2 + a, 0.0)
                path = [p0, p0 + Vector((0, -0.07, 0.01)), p0 + Vector((0, -0.16 - rng.uniform(0, 0.07), 0.0))]
                ribbon(m, [B(p) for p in path], [0.04, 0.03, 0.006], B(X))

    bake_ao(m, rays=20, dist=0.18 * max(0.6, s.bodyD), strength=0.8, floor=0.42)
    body_ob = m.finish_skinned(col, arm, 60.0)

    # ---------------------------------------------------------------- horns: a separate piece, only the males carry them
    out = [body_ob]
    if s.horns:
        h = Skinned(kind + "_horns", QB)
        tint = {"antlers": hexcol(0xC9B48C), "pronghorn": hexcol(0x2A2420), "bison": hexcol(0x2A2420), "cow": hexcol(0xE4DCC6)}[s.horns]
        h.surface("horn", tint, 0.04).set_part(0, w_head)
        up, fwd = f.head_up, f.head_dir
        for sx in (-1, 1):
            right = X * sx
            o = f.head_pt(-0.02, sx * 0.05, 0.09)
            T = lambda pts, radii: h.tube([B(p) for p in pts], 0.01, 8, radii=[r * hs for r in radii])
            if s.horns == "cow":
                a = o + right * (0.03 * hs)
                mid = a + (right * 0.9 + up * 0.25).normalized() * (0.16 * hs)
                e = mid + (right * 0.4 + up * 0.9 - fwd * 0.2).normalized() * (0.16 * hs)
                T([a, a + (mid - a) * 0.5, mid, mid + (e - mid) * 0.5, e], [0.03, 0.027, 0.022, 0.014, 0.004])
            elif s.horns == "bison":
                a = o + right * (0.06 * hs)
                mid = a + (right * 0.95 + up * 0.2).normalized() * (0.09 * hs)
                e = mid + (right * 0.1 + up).normalized() * (0.10 * hs)
                T([a, mid, mid + (e - mid) * 0.5, e], [0.032, 0.024, 0.016, 0.004])
            elif s.horns == "pronghorn":
                mid = o + (up + right * 0.12).normalized() * (0.22 * hs)
                e = mid + (up * 0.6 - fwd * 0.6 - right * 0.3).normalized() * (0.10 * hs)
                T([o, mid, e], [0.022, 0.016, 0.004])
                p0 = o + (mid - o) * 0.6
                T([p0, p0 + (fwd * 0.9 + up * 0.4).normalized() * (0.07 * hs)], [0.014, 0.004])
            else:
                b0 = o
                b1 = b0 + (up * 0.75 + right * 0.55 - fwd * 0.3).normalized() * (0.26 * hs)
                b2 = b1 + (up * 0.55 + right * 0.25 + fwd * 0.75).normalized() * (0.30 * hs)
                T([b0, b0 + (b1 - b0) * 0.5, b1, b1 + (b2 - b1) * 0.5, b2], [0.02, 0.018, 0.016, 0.011, 0.006])
                p0 = b0 + (b1 - b0) * 0.3
                T([p0, p0 + (up * 0.8 + fwd * 0.6).normalized() * (0.14 * hs)], [0.012, 0.004])
                T([b1, b1 + (up - fwd * 0.1).normalized() * (0.20 * hs)], [0.012, 0.004])
                p1 = b1 + (b2 - b1) * 0.5
                T([p1, p1 + up * (0.16 * hs)], [0.010, 0.004])
        bake_ao(h, rays=12, dist=0.05, strength=0.5, floor=0.6)
        out.append(h.finish_skinned(col, arm, 60.0))
    return out


def build(only=None):
    kit.clear_default_scene()
    made = []
    for kind, spec in PRESETS.items():
        if only and kind not in only:
            continue
        col = kit.collection("Animal_" + kind)
        arm = kit.make_armature(kind.capitalize() + "Rig", QB, QB_PARENT, Frame(spec).joints(), col)
        made.append((kind, animal(kind, spec, col, arm)))
    return made


if "ARGS" in globals() or __name__ == "__main__":
    args = globals().get("ARGS", {})
    only = set(args["only"].split(",")) if args.get("only") else None
    made = build(only)
    for kind, objs in made:
        print(kit.stats(objs))
        if not args.get("noexport"):
            kit.export_skinned_fbx("Animal_" + kind, "animal_" + kind)
    for i, (kind, objs) in enumerate(made):
        objs[0].parent.location = (i * 2.2, 0, 0)
