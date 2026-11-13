"""Modelling kit for the game's Blender models. Runs inside Blender (driven through the MCP server).

A Model collects parts (boxes, turned shapes, tubes, lofts) in one mesh. Every part carries:
  * a surface: the layer of the game's texture arrays (planks, iron, leather ...), written to the UV map
    "Surf" as (layer, 1) and moved to TEXCOORD3 by the Unity importer;
  * UVs in tiles of that surface, laid out so the grain runs along the part;
  * a tint in the colour attribute "Col" (sRGB), alpha = wind weight.
Models are built facing -Y with +Z up (so they face +Z in Unity) and exported as FBX.
"""
import math
import os
import random
import re

import bmesh
import bpy
from mathutils import Matrix, Vector

PROJECT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TEX = os.path.join(PROJECT, "Assets/Canaan/Textures/Surf")
MODELS = os.path.join(PROJECT, "Assets/Canaan/Models")
ART = os.path.join(PROJECT, "Art")


def _surface_table():
    src = open(os.path.join(PROJECT, "Assets/Canaan/Scripts/Gen/SurfaceTable.cs"), encoding="utf-8").read()
    names = re.findall(r'"(\w+)"', re.search(r"Names = \{([^}]*)\}", src).group(1))
    tiles = [float(t) for t in re.findall(r"([\d.]+)f", re.search(r"Tile = \{([^}]*)\}", src).group(1))]
    return {n: (i, tiles[i]) for i, n in enumerate(names)}


SURF = _surface_table()


def hexcol(h):
    """0xRRGGBB -> (r, g, b) in 0..1 (sRGB, as the game's palette writes them)."""
    return ((h >> 16 & 255) / 255.0, (h >> 8 & 255) / 255.0, (h & 255) / 255.0)


def shade(c, k):
    return tuple(min(1.0, max(0.0, v * k)) for v in c)


def mixc(a, b, t):
    return tuple(a[i] * (1 - t) + b[i] * t for i in range(3))


# the game's palette (Mats.cs), so models and code-built things agree
class Pal:
    WoodNew, WoodOld, WoodDark, WoodGrey = hexcol(0xB08A5A), hexcol(0x8A6B48), hexcol(0x5E4630), hexcol(0x8C8377)
    WoodRed, WoodWhite, WoodGreen, WoodBlue, WoodYellow = hexcol(0x8E3B2C), hexcol(0xD9D2C0), hexcol(0x55694A), hexcol(0x5B7486), hexcol(0xC9A857)
    Iron, Steel, Brass, GunBlue, Rust, Tin = hexcol(0x3A3B3F), hexcol(0x7B8088), hexcol(0xB8923E), hexcol(0x2B2F38), hexcol(0x7A4A2E), hexcol(0x9AA0A3)
    Leather, LeatherDark, LeatherTan, Rope = hexcol(0x6B4226), hexcol(0x3C2617), hexcol(0xA47445), hexcol(0xB79E6D)
    Canvas, ClothWhite, ClothCream, ClothRed, ClothBlue = hexcol(0xD8CDB2), hexcol(0xD9D4C7), hexcol(0xC9B994), hexcol(0x9A2F28), hexcol(0x3E5674)
    ClothGrey, ClothBlack, ClothBrown, ClothTan, Wool = hexcol(0x77746E), hexcol(0x25262A), hexcol(0x6D5137), hexcol(0xA98F68), hexcol(0x8B8170)
    Stone, StoneDark, Rock, RockRed, RockDark = hexcol(0x8D877C), hexcol(0x5F5B55), hexcol(0x8A7F72), hexcol(0xA1583C), hexcol(0x5C554E)
    Bark, BarkPale, Leaf, LeafDark, LeafDry, Sage = hexcol(0x5B4A3A), hexcol(0x9A8F7C), hexcol(0x6C8440), hexcol(0x47602F), hexcol(0xA39A4A), hexcol(0x8E9A7A)
    Hay, Bone, Ash, GrassDry, GrassGreen = hexcol(0xD1B665), hexcol(0xE4DCC6), hexcol(0x4B4743), hexcol(0xB5A45E), hexcol(0x7C8C45)


def _dir4(v):
    """A direction as a 4-vector (w = 0) for building a transform."""
    return Vector((v.x, v.y, v.z, 0.0))


def _basis(axis):
    axis = axis.normalized()
    ref = Vector((0, 0, 1)) if abs(axis.z) < 0.95 else Vector((1, 0, 0))
    u = ref.cross(axis).normalized()
    v = axis.cross(u)
    return u, v


class Model:
    """One game model: parts merged into a single mesh."""

    def __init__(self, name, seed=None):
        self.name = name
        self.bm = bmesh.new()
        self.col = self.bm.loops.layers.color.new("Col")
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.suv = self.bm.loops.layers.uv.new("Surf")
        self.stack = [Matrix.Identity(4)]
        self.rng = random.Random(seed if seed is not None else sum(ord(c) * (i + 3) for i, c in enumerate(name)))
        self.surf_name = "plain"
        self.tint = (1.0, 1.0, 1.0)
        self.wind = 0.0
        self.jitter = 0.05         # random brightness spread between parts of one colour
        self.on_vert = None        # called with every new vertex (skinned models give it its bone weights)
        self.marks = {}            # named points for the game (hand grips, seats)

    # ------------------------------------------------------------------ state
    def surface(self, name, tint=None, jitter=None):
        if name not in SURF:
            raise KeyError("unknown surface " + name)
        self.surf_name = name
        if tint is not None:
            self.tint = tint
        if jitter is not None:
            self.jitter = jitter
        return self

    def push(self, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
        m = Matrix.Translation(Vector(loc))
        for axis, angle in zip("XYZ", rot):
            if angle:
                m = m @ Matrix.Rotation(math.radians(angle), 4, axis)
        if scale != (1, 1, 1):
            m = m @ Matrix.Diagonal(Vector((scale[0], scale[1], scale[2], 1.0)))
        self.stack.append(self.stack[-1] @ m)
        return self

    def pop(self):
        self.stack.pop()
        return self

    def mark(self, key, p):
        self.marks[key] = tuple(self.stack[-1] @ Vector(p))

    # ------------------------------------------------------------------ core: move a finished part in
    def _commit(self, part, grain="z", uv_mode="box", axis=None, smooth=True, uv_scale=1.0):
        """Copies the part bmesh into the model with UVs (in the part's own space), surface and tint."""
        layer, tile = SURF[self.surf_name]
        tile /= uv_scale
        k = 1.0 + (self.rng.random() - 0.5) * 2.0 * self.jitter
        colour = (min(1.0, self.tint[0] * k), min(1.0, self.tint[1] * k), min(1.0, self.tint[2] * k), self.wind)
        ou, ov = self.rng.random(), self.rng.random()
        m = self.stack[-1]
        part.normal_update()
        vmap = {v: self.bm.verts.new(m @ v.co) for v in part.verts}
        if self.on_vert is not None:
            for nv in vmap.values():
                self.on_vert(nv)
        gi = "xyz".index(grain) if grain in "xyz" else 2
        if uv_mode == "cyl":
            au, av = _basis(axis)
            origin = Vector((0, 0, 0))
            for v in part.verts:
                origin += v.co
            origin /= max(len(part.verts), 1)
            radius = 0.0
            for v in part.verts:
                d = v.co - origin
                radius += (d - axis * d.dot(axis)).length
            radius = max(radius / max(len(part.verts), 1), 1e-3)
        for f in part.faces:
            try:
                nf = self.bm.faces.new([vmap[v] for v in f.verts])
            except ValueError:
                continue
            nf.smooth = smooth
            n = f.normal
            a = max(range(3), key=lambda i: abs(n[i]))
            cyl = uv_mode == "cyl" and abs(n.dot(axis)) < 0.7
            base = None
            for lp, nlp in zip(f.loops, nf.loops):
                p = lp.vert.co
                if cyl:
                    d = p - origin
                    r = d - axis * d.dot(axis)
                    ang = math.atan2(r.dot(av), r.dot(au))
                    if base is None:
                        base = ang
                    while ang - base > math.pi:
                        ang -= 2 * math.pi
                    while ang - base < -math.pi:
                        ang += 2 * math.pi
                    u, v = ang * radius, p.dot(axis)
                else:
                    i, j = [(1, 2), (0, 2), (0, 1)][a]
                    if i == gi:
                        i, j = j, i
                    u, v = p[i], p[j]
                nlp[self.uv].uv = (u / tile + ou, v / tile + ov)
                nlp[self.suv].uv = (layer, 1.0)
                nlp[self.col] = colour
        part.free()

    @staticmethod
    def _bevel(part, width, segments=2):
        if width <= 0:
            return
        bmesh.ops.bevel(part, geom=[e for e in part.edges if e.calc_face_angle(0.0) > math.radians(25)], offset=width,
                        segments=segments, profile=0.5, affect='EDGES', clamp_overlap=True)

    # ------------------------------------------------------------------ parts
    def box(self, c, size, bevel=0.008, grain=None, rot=None, warp=0.0):
        """A box centred at c. grain: the axis the wood grain follows (default: the longest side)."""
        part = bmesh.new()
        bmesh.ops.create_cube(part, size=1.0)
        s = Vector(size)
        for v in part.verts:
            v.co = Vector((v.co.x * s.x, v.co.y * s.y, v.co.z * s.z))
        self._bevel(part, min(bevel, min(s) * 0.3))
        if warp:
            for v in part.verts:
                v.co += Vector((self.rng.uniform(-1, 1), self.rng.uniform(-1, 1), self.rng.uniform(-1, 1))) * warp
        if grain is None:
            grain = "xyz"[max(range(3), key=lambda i: s[i])]
        self.push(c, rot or (0, 0, 0))
        self._commit(part, grain)
        self.pop()
        return self

    def beam(self, a, b, width, height, bevel=0.006, up=(0, 0, 1)):
        """A bar of rectangular section from a to b; the grain follows it."""
        a, b = Vector(a), Vector(b)
        d = b - a
        length = d.length
        if length < 1e-6:
            return self
        f = d / length
        upv = Vector(up)
        if abs(f.dot(upv)) > 0.999:
            upv = Vector((0, 1, 0))
        x = upv.cross(f).normalized()
        y = f.cross(x)
        part = bmesh.new()
        bmesh.ops.create_cube(part, size=1.0)
        for v in part.verts:
            v.co = Vector((v.co.x * width, v.co.y * height, v.co.z * length))
        self._bevel(part, min(bevel, min(width, height) * 0.3))
        # columns with w = 0 (to_4d() would put 1 there, and a push() before the part then shears it)
        m = Matrix((_dir4(x), _dir4(y), _dir4(f), Vector((0, 0, 0, 1)))).transposed()
        m.translation = (a + b) * 0.5
        self.stack.append(self.stack[-1] @ m)
        self._commit(part, "z")
        self.pop()
        return self

    def cyl(self, p0, p1, r0, r1=None, seg=20, bevel=0.004, caps=True, grain_along=True):
        """A (tapered) cylinder from p0 to p1."""
        p0, p1 = Vector(p0), Vector(p1)
        r1 = r0 if r1 is None else r1
        axis = p1 - p0
        length = axis.length
        if length < 1e-6:
            return self
        part = bmesh.new()
        bmesh.ops.create_cone(part, cap_ends=caps, cap_tris=False, segments=seg, radius1=r0, radius2=r1, depth=length)
        if bevel > 0 and caps:
            self._bevel(part, min(bevel, min(r0, r1) * 0.4, length * 0.3))
        z = axis / length
        u, v = _basis(z)
        m = Matrix((_dir4(u), _dir4(v), _dir4(z), Vector((0, 0, 0, 1)))).transposed()
        m.translation = (p0 + p1) * 0.5
        self.stack.append(self.stack[-1] @ m)
        self._commit(part, "z", "cyl" if grain_along else "box", Vector((0, 0, 1)))
        self.pop()
        return self

    def lathe(self, c, profile, seg=28, axis="z", smooth=True):
        """Revolves (radius, height) points around an axis through c."""
        part = bmesh.new()
        rings = []
        for r, h in profile:
            ring = []
            for i in range(seg):
                a = 2 * math.pi * i / seg
                ring.append(part.verts.new((math.cos(a) * r, math.sin(a) * r, h)) if r > 1e-6 else None)
            if ring[0] is None:
                apex = part.verts.new((0, 0, h))
                ring = [apex] * seg
            rings.append(ring)
        for j in range(len(rings) - 1):
            for i in range(seg):
                quad = [rings[j][i], rings[j][(i + 1) % seg], rings[j + 1][(i + 1) % seg], rings[j + 1][i]]
                uniq = []
                for q in quad:
                    if q not in uniq:
                        uniq.append(q)
                if len(uniq) >= 3:
                    try:
                        part.faces.new(uniq)
                    except ValueError:
                        pass
        bmesh.ops.recalc_face_normals(part, faces=part.faces)
        rot = {"z": (0, 0, 0), "x": (0, 90, 0), "y": (-90, 0, 0)}[axis]
        self.push(c, rot)
        self._commit(part, "z", "cyl", Vector((0, 0, 1)), smooth)
        self.pop()
        return self

    def tube(self, points, radius, seg=10, closed=False, radii=None):
        """A round bar along a polyline (rope, pipe, wire, handles)."""
        pts = [Vector(p) for p in points]
        n = len(pts)
        part = bmesh.new()
        rings = []
        prev_u = None
        for k, p in enumerate(pts):
            a = pts[(k - 1) % n] if (k > 0 or closed) else p
            b = pts[(k + 1) % n] if (k < n - 1 or closed) else p
            t = (b - a).normalized() if (b - a).length > 1e-9 else Vector((0, 0, 1))
            if prev_u is None:
                u, v = _basis(t)
            else:
                u = (prev_u - t * prev_u.dot(t)).normalized()
                v = t.cross(u)
            prev_u = u
            r = radii[k] if radii else radius
            rings.append([part.verts.new(p + (u * math.cos(2 * math.pi * i / seg) + v * math.sin(2 * math.pi * i / seg)) * r) for i in range(seg)])
        last = n if closed else n - 1
        for k in range(last):
            ra, rb = rings[k], rings[(k + 1) % n]
            for i in range(seg):
                part.faces.new([ra[i], ra[(i + 1) % seg], rb[(i + 1) % seg], rb[i]])
        if not closed:
            part.faces.new(list(reversed(rings[0])))
            part.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(part, faces=part.faces)
        self._commit(part, "z")
        return self

    def loft(self, rings, cap=True, subdiv=0, smooth=True, grain="z", wrap=None):
        """Skins closed rings (lists of points, same count). subdiv: Catmull-Clark levels for soft shapes."""
        part = bmesh.new()
        vr = [[part.verts.new(Vector(p)) for p in ring] for ring in rings]
        n = len(rings[0])
        for j in range(len(vr) - 1):
            for i in range(n):
                part.faces.new([vr[j][i], vr[j][(i + 1) % n], vr[j + 1][(i + 1) % n], vr[j + 1][i]])
        if cap:
            for ring in (list(reversed(vr[0])), vr[-1]):
                try:
                    part.faces.new(ring)
                except ValueError:
                    pass
        bmesh.ops.recalc_face_normals(part, faces=part.faces)
        if subdiv:
            bmesh.ops.subdivide_edges(part, edges=part.edges, cuts=0)
            me = bpy.data.meshes.new("_tmp")
            part.to_mesh(me)
            part.free()
            ob = bpy.data.objects.new("_tmp", me)
            mod = ob.modifiers.new("s", "SUBSURF")
            mod.levels = subdiv
            bpy.context.scene.collection.objects.link(ob)
            dg = bpy.context.evaluated_depsgraph_get()
            part = bmesh.new()
            part.from_mesh(ob.evaluated_get(dg).to_mesh())
            bpy.data.objects.remove(ob)
            bpy.data.meshes.remove(me)
        if wrap is not None:
            self._commit(part, grain, "cyl", Vector(wrap).normalized(), smooth)
        else:
            self._commit(part, grain, smooth=smooth)
        return self

    def blob(self, c, radii, subdiv=2, squash=None, lumps=0.0):
        """A soft rounded lump (sack, bedroll end, stone): a cube rounded by subdivision, optionally lumpy."""
        part = bmesh.new()
        bmesh.ops.create_cube(part, size=2.0)
        bmesh.ops.subdivide_edges(part, edges=part.edges, cuts=1, use_grid_fill=True)
        for _ in range(subdiv):
            bmesh.ops.subdivide_edges(part, edges=part.edges, cuts=1, use_grid_fill=True, smooth=1.0)
        r = Vector(radii)
        for v in part.verts:
            d = v.co.normalized()
            k = 1.0 + (self.rng.random() - 0.5) * lumps
            v.co = Vector((d.x * r.x, d.y * r.y, d.z * r.z)) * k
            if squash and v.co.z < 0:
                v.co.z *= squash
        self.push(c)
        self._commit(part, "z")
        self.pop()
        return self

    def grid(self, rows, keep=None, closed=True, smooth=True, grain="z", wrap=None):
        """A sheet through rows of points (same count each) with shared vertices, so it shades smoothly.
        keep(row_index, column_index) drops quads: a cap of hair is an ellipsoid with a hairline cut out."""
        part = bmesh.new()
        vs = [[part.verts.new(Vector(p)) for p in row] for row in rows]
        n = len(rows[0])
        for j in range(len(rows) - 1):
            for i in range(n if closed else n - 1):
                if keep is not None and not keep(j, i):
                    continue
                quad = [vs[j][i], vs[j][(i + 1) % n], vs[j + 1][(i + 1) % n], vs[j + 1][i]]
                uniq = []
                for q in quad:
                    if all((q.co - u.co).length > 1e-7 for u in uniq):
                        uniq.append(q)
                if len(uniq) >= 3:
                    try:
                        part.faces.new(uniq)
                    except ValueError:
                        pass
        bmesh.ops.delete(part, geom=[v for v in part.verts if not v.link_faces], context='VERTS')
        bmesh.ops.recalc_face_normals(part, faces=part.faces)
        if wrap is not None:
            self._commit(part, grain, "cyl", Vector(wrap).normalized(), smooth)
        else:
            self._commit(part, grain, smooth=smooth)
        return self

    def raw(self, verts, faces, grain="z", smooth=False, double=False):
        """Faces from explicit points: cloth panels, odd shapes."""
        part = bmesh.new()
        vs = [part.verts.new(Vector(p)) for p in verts]
        for f in faces:
            part.faces.new([vs[i] for i in f])
        if double:
            # the inside is a second skin a hair's breadth under the first, so the two never merge or share normals
            part.normal_update()
            back = [part.verts.new(v.co - v.normal * 0.0012) for v in vs]
            for f in faces:
                part.faces.new([back[i] for i in reversed(f)])
        self._commit(part, grain, smooth=smooth)
        return self

    # ------------------------------------------------------------------ bake
    def sculpt(self, moves):
        """Pushes vertices around like thumbs in clay. moves: (centre, radius, offset) in model space; a vertex
        within the radius moves by the offset times a smooth fall-off. Call before finish()."""
        for c, r, off in moves:
            c, off = Vector(c), Vector(off)
            for v in self.bm.verts:
                d = (v.co - c).length
                if d < r:
                    t = 1.0 - d / r
                    v.co += off * (t * t * (3.0 - 2.0 * t))

    def finish(self, collection, location=(0, 0, 0), sharp_angle=38.0):
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        if hasattr(me, "set_sharp_from_angle"):
            me.set_sharp_from_angle(angle=math.radians(sharp_angle))
        me.materials.append(_get_preview())
        ob = bpy.data.objects.new(self.name, me)
        ob.location = location
        collection.objects.link(ob)
        for key, p in self.marks.items():
            e = bpy.data.objects.new(self.name + "@" + key, None)
            e.empty_display_size = 0.05
            e.location = p
            e.parent = ob
            collection.objects.link(e)
        return ob


# ---------------------------------------------------------------------- scene helpers

def collection(name, clear=True):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    if clear:
        for ob in list(col.objects):
            me = ob.data if ob.type == "MESH" else None
            bpy.data.objects.remove(ob, do_unlink=True)
            if me is not None and me.users == 0:
                bpy.data.meshes.remove(me)
    return col


def clear_default_scene():
    for name in ("Cube", "Light", "Camera"):
        ob = bpy.data.objects.get(name)
        if ob is not None:
            bpy.data.objects.remove(ob, do_unlink=True)


PREVIEW_DYES = [(1, 1, 1), (0.83, 0.64, 0.50), (0.23, 0.16, 0.11), (0.55, 0.52, 0.47), (0.29, 0.25, 0.22), (0.24, 0.15, 0.09),
                (0.31, 0.21, 0.14), (0.64, 0.45, 0.27), (0.79, 0.25, 0.17), (0.24, 0.34, 0.45), (0.55, 0.52, 0.47)]


def preview_material():
    """Shows the tint in Blender's viewport, with sample dyes for the parts the game colours per person
    (the part number is the colour alpha). The game uses its own shader."""
    mat = bpy.data.materials.get("CanaanPreview")
    if mat is not None:
        bpy.data.materials.remove(mat)
    mat = bpy.data.materials.new("CanaanPreview")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    attr = nt.nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "Col"
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = 'CONSTANT'
    el = ramp.color_ramp.elements
    el[0].position, el[0].color = 0.0, (1, 1, 1, 1)
    el[1].position, el[1].color = 0.5 / 255.0, tuple(c ** 2.2 for c in PREVIEW_DYES[1]) + (1,)
    for i in range(2, len(PREVIEW_DYES)):
        e = el.new((i - 0.5) / 255.0)
        e.color = tuple(c ** 2.2 for c in PREVIEW_DYES[i]) + (1,)
    last = el.new((len(PREVIEW_DYES) - 0.5) / 255.0)
    last.color = (1, 1, 1, 1)
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = 'RGBA'
    mix.blend_type = 'MULTIPLY'
    mix.inputs[0].default_value = 1.0
    nt.links.new(attr.outputs["Alpha"], ramp.inputs["Fac"])
    nt.links.new(attr.outputs["Color"], mix.inputs[6])
    nt.links.new(ramp.outputs["Color"], mix.inputs[7])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.75
    return mat


_preview = None


def _get_preview():
    global _preview
    if _preview is None or _preview.name not in bpy.data.materials:
        _preview = preview_material()
    return _preview


def layout(objects, spacing=1.6, per_row=8):
    """Sets the models out in rows so they can be looked at; the game ignores object positions."""
    for i, ob in enumerate(objects):
        ob.location = ((i % per_row) * spacing, (i // per_row) * spacing * 1.4, 0.0)


def scrub_fbx(path):
    """The FBX exporter writes the .blend's absolute path into the file (ApplicationNativeFile), and with it the name
    of the user on this machine. Replace it, byte for byte, with the path relative to the project, padded with './'
    to the same length (FBX strings carry their length, so the size must not change)."""
    src = bpy.data.filepath
    if not src:
        return
    old = src.encode("utf-8")
    rel = os.path.relpath(src, PROJECT).replace(os.sep, "/").encode("utf-8")
    pad = len(old) - len(rel)
    if pad < 0:
        return
    new = b"./" * (pad // 2) + (b"/" if pad % 2 else b"") + rel
    with open(path, "rb") as f:
        data = f.read()
    if old in data:
        with open(path, "wb") as f:
            f.write(data.replace(old, new))


def export_fbx(collection_name, filename):
    """Writes a collection to Assets/Canaan/Models/<filename>.fbx. The game takes the meshes and the mark
    empties (children) by name; where the objects stand in Blender does not matter."""
    os.makedirs(MODELS, exist_ok=True)
    path = os.path.join(MODELS, filename + ".fbx")
    win = bpy.context.window_manager.windows[0]
    with bpy.context.temp_override(window=win):
        bpy.ops.export_scene.fbx(filepath=path, use_selection=False, collection=collection_name,
                                 object_types={'MESH', 'ARMATURE', 'EMPTY'}, apply_scale_options='FBX_SCALE_ALL',
                                 axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='OFF',
                                 use_mesh_modifiers=True, colors_type='SRGB', add_leaf_bones=False, bake_anim=False,
                                 use_tspace=False, use_custom_props=False)
    scrub_fbx(path)
    return path


def save_blend(name):
    os.makedirs(ART, exist_ok=True)
    path = os.path.join(ART, name + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=path, check_existing=False)
    return path


def stats(objects):
    return ", ".join("%s %dv" % (ob.name, len(ob.data.vertices)) for ob in objects if ob.type == "MESH")


# ---------------------------------------------------------------------- people: the game's skeleton

HB = ["Hips", "Spine", "Chest", "Neck", "Head", "ArmL", "ForeL", "HandL", "ArmR", "ForeR", "HandR",
      "ThighL", "ShinL", "FootL", "ToeL", "ThighR", "ShinR", "FootR", "ToeR"]
HB_PARENT = [-1, 0, 1, 2, 3, 2, 5, 6, 2, 8, 9, 0, 11, 12, 13, 0, 15, 16, 17]


def L(x, y, z):
    """A point given the way the game sees it (x right, y up, z forward) -> Blender (model faces -Y, +Z up)."""
    return Vector((-x, -z, y))


def human_joints():
    """Bone positions of HumanBuilder.cs for the reference build (1.78 m), in game axes. Left is -x."""
    p = {"Hips": (0, 0.97, 0), "Spine": (0, 1.09, 0), "Chest": (0, 1.25, 0), "Neck": (0, 1.50, 0), "Head": (0, 1.585, 0.01)}
    for side, s in (("L", -1), ("R", 1)):
        p["Arm" + side] = (s * 0.185, 1.445, 0)
        p["Fore" + side] = (s * 0.205, 1.165, 0)
        p["Hand" + side] = (s * 0.215, 0.905, 0.01)
        p["Thigh" + side] = (s * 0.092, 0.93, 0)
        p["Shin" + side] = (s * 0.098, 0.50, 0)
        p["Foot" + side] = (s * 0.10, 0.085, -0.01)
        p["Toe" + side] = (s * 0.10, 0.02, 0.13)
    return p


def make_armature(name, names, parents, joints, col):
    """An armature whose bones sit at the game's joints. Only names and weights matter to the game: it binds the
    mesh to its own bones, so bone orientation here is free."""
    arm = bpy.data.armatures.new(name)
    ob = bpy.data.objects.new(name, arm)
    col.objects.link(ob)
    win = bpy.context.window_manager.windows[0]
    with bpy.context.temp_override(window=win, active_object=ob, object=ob, selected_objects=[ob], selected_editable_objects=[ob]):
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.mode_set(mode='EDIT')
        made = []
        for i, n in enumerate(names):
            b = arm.edit_bones.new(n)
            b.head = L(*joints[n])
            b.tail = b.head + Vector((0, 0, 0.06))
            if parents[i] >= 0:
                b.parent = made[parents[i]]
            made.append(b)
        bpy.ops.object.mode_set(mode='OBJECT')
    return ob


class Skinned(Model):
    """A model bound to the game's skeleton. 'weights' maps a point (Blender space) to {bone name: weight};
    'part' goes to the colour alpha so the game can dye shirt, skin and hair per person."""

    def __init__(self, name, bones):
        super().__init__(name)
        self.bones = list(bones)
        self.deform = self.bm.verts.layers.deform.verify()
        self.weights = None
        self.part = 0
        self.on_vert = self._bind

    def _bind(self, v):
        w = self.weights(v.co) if self.weights else {self.bones[0]: 1.0}
        total = sum(w.values()) or 1.0
        for bone, x in w.items():
            if x > 1e-4:
                v[self.deform][self.bones.index(bone)] = x / total

    def set_part(self, part, weights=None):
        self.part = part
        self.wind = part / 255.0          # the colour alpha carries the part number
        if weights is not None:
            self.weights = weights
        return self

    def finish_skinned(self, col, armature, sharp_angle=45.0):
        ob = self.finish(col, sharp_angle=sharp_angle)
        for b in self.bones:
            ob.vertex_groups.new(name=b)
        mod = ob.modifiers.new("Armature", "ARMATURE")
        mod.object = armature
        ob.parent = armature
        return ob


def export_skinned_fbx(collection_name, filename):
    os.makedirs(MODELS, exist_ok=True)
    path = os.path.join(MODELS, filename + ".fbx")
    win = bpy.context.window_manager.windows[0]
    with bpy.context.temp_override(window=win):
        bpy.ops.export_scene.fbx(filepath=path, use_selection=False, collection=collection_name,
                                 object_types={'MESH', 'ARMATURE', 'EMPTY'}, apply_scale_options='FBX_SCALE_ALL',
                                 axis_forward='-Z', axis_up='Y', bake_space_transform=False, mesh_smooth_type='OFF',
                                 use_mesh_modifiers=False, colors_type='SRGB', add_leaf_bones=False, bake_anim=False,
                                 use_armature_deform_only=True, use_tspace=False, use_custom_props=False)
    scrub_fbx(path)
    return path


# ---------------------------------------------------------------------- parts given in the game's axes
# (x right, y up, z forward): the code that places and holds things thinks this way.
_GRAIN = {"x": "x", "y": "z", "z": "y"}


def gbox(m, c, size, bevel=0.008, grain=None, rot=None):
    if grain is None:
        grain = "xyz"[max(range(3), key=lambda i: size[i])]
    r = None if rot is None else (-rot[0], -rot[2], rot[1])
    return m.box(L(*c), (size[0], size[2], size[1]), bevel, _GRAIN[grain], r)


def gbeam(m, a, b, width, height, bevel=0.006):
    return m.beam(L(*a), L(*b), width, height, bevel, up=(0, 0, 1))


def gcyl(m, a, b, r0, r1=None, seg=16, bevel=0.004):
    return m.cyl(L(*a), L(*b), r0, r1, seg, bevel)


def repart(model, test, part):
    """Renames the part (colour alpha) of every face corner whose vertex passes test(co): a white belly, a pale rump."""
    alpha = part / 255.0
    col = model.col
    for f in model.bm.faces:
        for lp in f.loops:
            if test(lp.vert.co):
                c = lp[col]
                lp[col] = (c[0], c[1], c[2], alpha)


# ---------------------------------------------------------------------- creatures: sections, hair, baked shade

def profile_ring(c, u, v, up, down, width, p_up=2.0, p_down=2.0, top_taper=0.15, bottom_taper=0.15, n=16, lift=0.0):
    """A closed section through a body, in the game's axes: 'up' and 'down' metres from the centre c along v,
    'width' half-width at the centre line along u. A super-ellipse whose two halves have their own exponent
    (p < 2 pointier: the withers; p > 2 fuller: the croup) and their own taper (the half narrows towards its end).
    The first point lies on +u, then the ring turns towards +v. 'lift' moves every point outward. Returns
    Blender points."""
    out = []
    for i in range(n):
        a = 2 * math.pi * i / n
        cs, sn = math.cos(a), math.sin(a)
        p = p_up if sn >= 0 else p_down
        xn = math.copysign(abs(cs) ** (2.0 / p), cs)
        yn = math.copysign(abs(sn) ** (2.0 / p), sn)
        w = width * (1.0 - (top_taper if yn > 0 else bottom_taper) * yn * yn)
        dx = xn * w
        dy = yn * (up if yn > 0 else down)
        q = c + u * dx + v * dy
        if lift:
            d = u * dx + v * dy
            if d.length > 1e-6:
                q = q + d.normalized() * lift
        out.append(L(q.x, q.y, q.z))
    return out


def sheet(model, rows, outward=None, double=False, grain="z", smooth=True):
    """Quads through rows of points (Blender space, same count each, not closed round). 'outward' (a Blender
    vector) says which way the front faces should look; the winding is turned to match. double: a back skin."""
    verts, faces = [], []
    n = len(rows[0])
    for row in rows:
        verts += [tuple(p) for p in row]
    for j in range(len(rows) - 1):
        for i in range(n - 1):
            faces.append((j * n + i, j * n + i + 1, (j + 1) * n + i + 1, (j + 1) * n + i))
    if outward is not None and faces:
        a, b, d = Vector(verts[faces[0][0]]), Vector(verts[faces[0][1]]), Vector(verts[faces[0][3]])
        if (b - a).cross(d - a).dot(Vector(outward)) < 0:
            faces = [tuple(reversed(f)) for f in faces]
    model.raw(verts, faces, grain, smooth=smooth, double=double)
    return model


def ribbon(model, pts, widths, across, grain="z", double=True, outward=None):
    """A flat strand of hair (or a strap) along points in Blender space: widths per point, 'across' a Blender
    vector (or one per point) the strand lies flat along. Both faces drawn, or - for a lock lying on a scalp -
    only the one that looks 'outward' (a Blender vector)."""
    rows = []
    for k, p in enumerate(pts):
        a = across[k] if isinstance(across, list) else across
        a = Vector(a).normalized() * (widths[k] * 0.5)
        rows.append([Vector(p) - a, Vector(p) + a])
    verts, faces = [], []
    for j, row in enumerate(rows):
        verts += [tuple(row[0]), tuple(row[1])]
        if j:
            faces.append((2 * j - 2, 2 * j - 1, 2 * j + 1, 2 * j))
    if outward is not None and not double and faces:
        a, b, d = Vector(verts[0]), Vector(verts[1]), Vector(verts[2])
        if (b - a).cross(d - a).dot(Vector(outward)) < 0:
            faces = [tuple(reversed(f)) for f in faces]
    model.raw(verts, faces, grain, smooth=True, double=double)
    return model


def bake_ao(model, rays=24, dist=0.25, strength=0.8, floor=0.42, bias=0.003):
    """Ambient occlusion into the shade of the vertex colours (rgb; the alpha, the part, is kept): rays from every
    vertex over the hemisphere of its normal, the share that meets the model within 'dist' darkens it. Creases,
    the underside, the cup of an ear and the hide under the mane go dark; the game's own AO adds the rest."""
    from mathutils.bvhtree import BVHTree
    bm = model.bm
    bm.verts.ensure_lookup_table()
    bm.verts.index_update()
    bm.normal_update()
    tree = BVHTree.FromBMesh(bm)
    dirs = []
    for i in range(rays):                                   # cosine-weighted, a golden-angle spiral
        q = (i + 0.5) / rays
        r = math.sqrt(q)
        phi = i * 2.399963
        dirs.append((r * math.cos(phi), r * math.sin(phi), math.sqrt(max(0.0, 1.0 - q))))
    shade = [1.0] * len(bm.verts)
    for v in bm.verts:
        nrm = v.normal
        if nrm.length < 0.5:
            continue
        t = nrm.orthogonal().normalized()
        b = nrm.cross(t)
        o = v.co + nrm * bias
        occ = 0.0
        for dx, dy, dz in dirs:
            hit = tree.ray_cast(o, t * dx + b * dy + nrm * dz, dist)
            if hit[0] is not None:
                occ += 1.0 - 0.5 * hit[3] / dist
        shade[v.index] = max(floor, 1.0 - strength * occ / rays)
    col = model.col
    for f in bm.faces:
        for lp in f.loops:
            k = shade[lp.vert.index]
            if k < 0.999:
                c = lp[col]
                lp[col] = (c[0] * k, c[1] * k, c[2] * k, c[3])
    return model
