using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Canaan
{
    /// <summary>
    /// Accumulates coloured geometry and bakes it into a Mesh. Every model in the game is built with this.
    /// Vertex colour carries the tint (alpha = wind weight), uv0.xyz a stable position for the triplanar
    /// textures and shader noise, uv0.w the gloss, uv3 the surface (texture layer, repeats per metre, quarter
    /// turn), uv4 the normal at build time. Triangles are clockwise when seen from the front.
    /// </summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> verts = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Color32> colors = new List<Color32>();
        public readonly List<Vector4> uvs = new List<Vector4>();
        public readonly List<int> tris = new List<int>();
        public readonly List<BoneWeight> weights = new List<BoneWeight>();
        public readonly List<Vector4> surfs = new List<Vector4>();
        public readonly List<Vector4> tangents = new List<Vector4>();     // only for meshes with real UVs (Blender models)

        /// <summary>The tint. Setting it also picks the surface texture that colour stands for (see SurfaceMap).</summary>
        public Color32 color
        {
            get { return tint; }
            set { tint = value; if (!surfLocked) SetSurf(SurfaceMap.Of(value), false); }
        }
        Color32 tint = new Color32(255, 255, 255, 255);
        Vector4 surf = new Vector4(0f, 1f, 0f, 0f);
        bool surfLocked;
        public float gloss = 0f;
        public byte wind = 255;
        public BoneWeight weight;
        public bool skinned;

        Matrix4x4 xf = Matrix4x4.identity;
        Matrix4x4 nxf = Matrix4x4.identity;
        readonly Stack<Matrix4x4> stack = new Stack<Matrix4x4>();

        public int VertexCount => verts.Count;

        // ------------------------------------------------------------------ state

        public MeshBuilder Col(Color32 c, float g = 0f) { surfLocked = false; color = c; gloss = g; return this; }

        void SetSurf(Surf s, bool turn) { surf = new Vector4((int)s, 1f / SurfaceTable.Tile[(int)s], turn ? 1f : 0f, 0f); }

        /// <summary>Overrides the surface chosen by the colour, until the next Col().</summary>
        public MeshBuilder Surface(Surf s, bool turn = false) { SetSurf(s, turn); return this; }

        /// <summary>Keeps one surface whatever colours follow (a loft with per-ring colours), until Unlock().</summary>
        public MeshBuilder Lock(Surf s, bool turn = false) { SetSurf(s, turn); surfLocked = true; return this; }
        public MeshBuilder Unlock() { surfLocked = false; return this; }

        public void Bone(int b) { weight = new BoneWeight { boneIndex0 = b, weight0 = 1f }; }

        public void Bones(int b0, float w0, int b1)
        {
            weight = new BoneWeight { boneIndex0 = b0, weight0 = w0, boneIndex1 = b1, weight1 = 1f - w0 };
        }

        public static BoneWeight W(int b0) { return new BoneWeight { boneIndex0 = b0, weight0 = 1f }; }

        public static BoneWeight W(int b0, float w0, int b1)
        {
            return new BoneWeight { boneIndex0 = b0, weight0 = w0, boneIndex1 = b1, weight1 = 1f - w0 };
        }

        public void Push(Matrix4x4 m)
        {
            stack.Push(xf);
            xf = xf * m;
            nxf = xf.inverse.transpose;
        }

        public void Push(Vector3 pos, Quaternion rot) { Push(Matrix4x4.TRS(pos, rot, Vector3.one)); }
        public void Push(Vector3 pos, Quaternion rot, Vector3 scale) { Push(Matrix4x4.TRS(pos, rot, scale)); }
        public void Push(Vector3 pos) { Push(Matrix4x4.Translate(pos)); }

        public void Pop()
        {
            xf = stack.Pop();
            nxf = xf.inverse.transpose;
        }

        // ------------------------------------------------------------------ raw

        public int Vert(Vector3 p, Vector3 n)
        {
            Vector3 wp = xf.MultiplyPoint3x4(p);
            verts.Add(wp);
            normals.Add(nxf.MultiplyVector(n).normalized);
            colors.Add(new Color32(tint.r, tint.g, tint.b, wind));
            uvs.Add(new Vector4(wp.x, wp.y, wp.z, gloss));
            surfs.Add(surf);
            if (skinned) weights.Add(weight);
            return verts.Count - 1;
        }

        public void Tri(int a, int b, int c) { tris.Add(a); tris.Add(b); tris.Add(c); }

        public void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int i = Vert(a, n); Vert(b, n); Vert(c, n);
            Tri(i, i + 1, i + 2);
        }

        /// <summary>Quad a-b-c-d, clockwise seen from the front.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;
            int i = Vert(a, n); Vert(b, n); Vert(c, n); Vert(d, n);
            Tri(i, i + 1, i + 2);
            Tri(i, i + 2, i + 3);
        }

        public void QuadDouble(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Quad(a, b, c, d);
            Quad(d, c, b, a);
        }

        // ------------------------------------------------------------------ primitives

        public void Box(Vector3 c, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 p000 = c + new Vector3(-h.x, -h.y, -h.z), p100 = c + new Vector3(h.x, -h.y, -h.z);
            Vector3 p010 = c + new Vector3(-h.x, h.y, -h.z), p110 = c + new Vector3(h.x, h.y, -h.z);
            Vector3 p001 = c + new Vector3(-h.x, -h.y, h.z), p101 = c + new Vector3(h.x, -h.y, h.z);
            Vector3 p011 = c + new Vector3(-h.x, h.y, h.z), p111 = c + new Vector3(h.x, h.y, h.z);
            Quad(p000, p010, p110, p100); // -Z
            Quad(p101, p111, p011, p001); // +Z
            Quad(p001, p011, p010, p000); // -X
            Quad(p100, p110, p111, p101); // +X
            Quad(p010, p011, p111, p110); // +Y
            Quad(p001, p000, p100, p101); // -Y
        }

        public void Box(Vector3 c, Vector3 size, Quaternion rot)
        {
            Push(c, rot);
            Box(Vector3.zero, size);
            Pop();
        }

        /// <summary>Box given by its minimum corner and size (handy for walls and floors).</summary>
        public void BoxMin(Vector3 min, Vector3 size) { Box(min + size * 0.5f, size); }

        /// <summary>A beam of rectangular section from a to b. 'up' fixes the roll.</summary>
        public void Beam(Vector3 a, Vector3 b, float width, float height, Vector3 up)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-5f) return;
            Vector3 f = d / len;
            if (Mathf.Abs(Vector3.Dot(f, up)) > 0.999f) up = Mathf.Abs(f.y) > 0.9f ? Vector3.forward : Vector3.up;
            Push((a + b) * 0.5f, Quaternion.LookRotation(f, up));
            Box(Vector3.zero, new Vector3(width, height, len));
            Pop();
        }

        public void Beam(Vector3 a, Vector3 b, float width, float height) { Beam(a, b, width, height, Vector3.up); }

        static void Basis(Vector3 axis, out Vector3 u, out Vector3 v)
        {
            Vector3 r = Mathf.Abs(axis.y) < 0.95f ? Vector3.up : Vector3.right;
            u = Vector3.Cross(r, axis).normalized;
            v = Vector3.Cross(axis, u);
        }

        /// <summary>Tapered cylinder from p0 (radius r0) to p1 (radius r1), smooth around.</summary>
        public void Cyl(Vector3 p0, Vector3 p1, float r0, float r1, int seg = 10, bool cap0 = true, bool cap1 = true)
        {
            Vector3 axis = p1 - p0;
            float len = axis.magnitude;
            if (len < 1e-6f) return;
            axis /= len;
            Basis(axis, out Vector3 u, out Vector3 v);
            float slope = (r0 - r1) / len;
            int b = verts.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector3 dir = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                Vector3 n = (dir + axis * slope).normalized;
                Vert(p0 + dir * r0, n);
                Vert(p1 + dir * r1, n);
            }
            for (int i = 0; i < seg; i++)
            {
                int k = b + i * 2;
                Tri(k, k + 2, k + 3);
                Tri(k, k + 3, k + 1);
            }
            if (cap0 && r0 > 1e-5f) Disc(p0, -axis, r0, seg);
            if (cap1 && r1 > 1e-5f) Disc(p1, axis, r1, seg);
        }

        public void Cyl(Vector3 p0, Vector3 p1, float r, int seg = 10) { Cyl(p0, p1, r, r, seg); }

        public void Disc(Vector3 c, Vector3 normal, float r, int seg = 10)
        {
            normal.Normalize();
            Basis(normal, out Vector3 u, out Vector3 v);
            int b = Vert(c, normal);
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vert(c + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * r, normal);
            }
            for (int i = 0; i < seg; i++) Tri(b, b + 1 + i, b + 2 + i);
        }

        public void Ellipsoid(Vector3 c, Vector3 radii, int seg = 10, int rings = 6)
        {
            int b = verts.Count;
            for (int j = 0; j <= rings; j++)
            {
                float phi = Mathf.PI * j / rings;
                float y = Mathf.Cos(phi), s = Mathf.Sin(phi);
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    Vector3 d = new Vector3(Mathf.Cos(a) * s, y, Mathf.Sin(a) * s);
                    Vector3 n = new Vector3(d.x / radii.x, d.y / radii.y, d.z / radii.z).normalized;
                    Vert(c + Vector3.Scale(d, radii), n);
                }
            }
            int row = seg + 1;
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < seg; i++)
                {
                    int k = b + j * row + i;
                    Tri(k, k + 1, k + row + 1);
                    Tri(k, k + row + 1, k + row);
                }
        }

        public void Sphere(Vector3 c, float r, int seg = 10, int rings = 6) { Ellipsoid(c, new Vector3(r, r, r), seg, rings); }

        /// <summary>Revolves a profile of (radius, height) points around the Y axis at 'c'.</summary>
        public void Lathe(Vector3 c, IList<Vector2> profile, int seg = 12)
        {
            int b = verts.Count;
            int m = profile.Count;
            for (int j = 0; j < m; j++)
            {
                Vector2 prev = profile[Mathf.Max(0, j - 1)], next = profile[Mathf.Min(m - 1, j + 1)];
                Vector2 t = (next - prev).normalized;
                Vector2 n2 = new Vector2(t.y, -t.x);
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                    Vert(c + new Vector3(cs * profile[j].x, profile[j].y, sn * profile[j].x),
                         new Vector3(cs * n2.x, n2.y, sn * n2.x));
                }
            }
            int row = seg + 1;
            for (int j = 0; j < m - 1; j++)
                for (int i = 0; i < seg; i++)
                {
                    int k = b + j * row + i;
                    Tri(k, k + row, k + row + 1);
                    Tri(k, k + row + 1, k + 1);
                }
        }

        /// <summary>Extrudes a convex polygon (given in order) along 'extrude'. Both ends are capped.</summary>
        public void Prism(IList<Vector3> poly, Vector3 extrude)
        {
            int n = poly.Count;
            Vector3 centre = Vector3.zero;
            for (int i = 0; i < n; i++) centre += poly[i];
            centre /= n;
            Vector3 pn = Vector3.Cross(poly[1] - poly[0], poly[2] - poly[0]);
            bool flip = Vector3.Dot(pn, extrude) > 0f;   // want the base to face away from the extrusion
            for (int i = 0; i < n; i++)
            {
                Vector3 a = poly[i], b = poly[(i + 1) % n];
                if (flip) Quad(b, b + extrude, a + extrude, a); else Quad(a, a + extrude, b + extrude, b);
            }
            for (int i = 1; i < n - 1; i++)
            {
                if (flip)
                {
                    Tri(poly[0], poly[i + 1], poly[i]);
                    Tri(poly[0] + extrude, poly[i] + extrude, poly[i + 1] + extrude);
                }
                else
                {
                    Tri(poly[0], poly[i], poly[i + 1]);
                    Tri(poly[0] + extrude, poly[i + 1] + extrude, poly[i] + extrude);
                }
            }
        }

        /// <summary>Makes an elliptical ring of n points around 'c' in the plane spanned by u and v.</summary>
        public static Vector3[] Ring(Vector3 c, Vector3 u, Vector3 v, float ru, float rv, int n)
        {
            var r = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                r[i] = c + u * (Mathf.Cos(a) * ru) + v * (Mathf.Sin(a) * rv);
            }
            return r;
        }

        /// <summary>
        /// Skins a surface over consecutive closed rings (all with the same point count) with smooth normals.
        /// Orientation is detected, so rings may wind either way. Optional per-ring colour and bone weights.
        /// </summary>
        public void Loft(IList<Vector3[]> rings, IList<Color32> ringColors = null, IList<BoneWeight> ringWeights = null,
                         bool capStart = true, bool capEnd = true)
        {
            int m = rings.Count, n = rings[0].Length;
            int b = verts.Count;
            Color32 keepCol = color;
            BoneWeight keepW = weight;
            for (int j = 0; j < m; j++)
            {
                if (ringColors != null) color = ringColors[j];
                if (ringWeights != null) weight = ringWeights[j];
                for (int i = 0; i < n; i++) Vert(rings[j][i], Vector3.up);
            }
            int capA = -1, capB = -1;
            if (capStart)
            {
                if (ringColors != null) color = ringColors[0];
                if (ringWeights != null) weight = ringWeights[0];
                capA = Vert(Centre(rings[0]), Vector3.up);
            }
            if (capEnd)
            {
                if (ringColors != null) color = ringColors[m - 1];
                if (ringWeights != null) weight = ringWeights[m - 1];
                capB = Vert(Centre(rings[m - 1]), Vector3.up);
            }
            color = keepCol;
            weight = keepW;

            // orientation: the first quad's normal must point away from the loft axis
            float facing = 0f;
            for (int j = 0; j < m - 1; j++)
            {
                Vector3 mid = (Centre(rings[j]) + Centre(rings[j + 1])) * 0.5f;
                for (int i = 0; i < n; i++)
                {
                    Vector3 qa = rings[j][i], qb = rings[j][(i + 1) % n], qc = rings[j + 1][i];
                    facing += Vector3.Dot(Vector3.Cross(qc - qa, qb - qa), qa - mid);
                }
            }
            bool flip = facing < 0f;

            int t0 = tris.Count;
            for (int j = 0; j < m - 1; j++)
                for (int i = 0; i < n; i++)
                {
                    int a = b + j * n + i, bb = b + j * n + (i + 1) % n;
                    int c = b + (j + 1) * n + i, d = b + (j + 1) * n + (i + 1) % n;
                    if (flip) { Tri(a, bb, d); Tri(a, d, c); }
                    else { Tri(a, c, d); Tri(a, d, bb); }
                }
            if (capA >= 0)
                for (int i = 0; i < n; i++)
                {
                    int a = b + i, bb = b + (i + 1) % n;
                    if (flip) Tri(capA, bb, a); else Tri(capA, a, bb);
                }
            if (capB >= 0)
                for (int i = 0; i < n; i++)
                {
                    int a = b + (m - 1) * n + i, bb = b + (m - 1) * n + (i + 1) % n;
                    if (flip) Tri(capB, a, bb); else Tri(capB, bb, a);
                }
            SmoothNormals(b, verts.Count, t0, tris.Count);
        }

        static Vector3 Centre(Vector3[] ring)
        {
            Vector3 c = Vector3.zero;
            for (int i = 0; i < ring.Length; i++) c += ring[i];
            return c / ring.Length;
        }

        /// <summary>Recomputes area-weighted normals for the vertex range from the triangle range.</summary>
        public void SmoothNormals(int v0, int v1, int t0, int t1)
        {
            for (int i = v0; i < v1; i++) normals[i] = Vector3.zero;
            for (int t = t0; t < t1; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                normals[a] += n; normals[b] += n; normals[c] += n;
            }
            for (int i = v0; i < v1; i++)
            {
                Vector3 n = normals[i];
                normals[i] = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
            }
        }

        /// <summary>Appends another builder's geometry through the current transform.</summary>
        public void Append(MeshBuilder o)
        {
            int b = verts.Count;
            for (int i = 0; i < o.verts.Count; i++)
            {
                Vector3 wp = xf.MultiplyPoint3x4(o.verts[i]);
                verts.Add(wp);
                normals.Add(nxf.MultiplyVector(o.normals[i]).normalized);
                colors.Add(o.colors[i]);
                uvs.Add(new Vector4(wp.x, wp.y, wp.z, o.uvs[i].w));
                surfs.Add(o.surfs[i]);
            }
            for (int i = 0; i < o.tris.Count; i++) tris.Add(o.tris[i] + b);
        }

        /// <summary>
        /// Takes in a Blender model through the current transform. Its uv0 are real texture coordinates, so a
        /// builder that holds models must not also hold code-built parts (those use uv0 for a position).
        /// </summary>
        public void AppendMesh(Mesh m, Matrix4x4 local, Color32? dye = null, int dyedPart = 0, Color32? dye2 = null, int dyedPart2 = 0)
        {
            if (m == null) return;
            Matrix4x4 full = xf * local;
            Matrix4x4 nfull = full.inverse.transpose;
            int b = verts.Count;
            Vector3[] v = m.vertices, n = m.normals;
            Vector4[] t = m.tangents;
            Color32[] c = m.colors32;
            var uv = new List<Vector2>();
            var sf = new List<Vector4>();
            m.GetUVs(0, uv);
            m.GetUVs(3, sf);
            for (int i = 0; i < v.Length; i++)
            {
                verts.Add(full.MultiplyPoint3x4(v[i]));
                normals.Add(nfull.MultiplyVector(i < n.Length ? n[i] : Vector3.up).normalized);
                Vector3 tv = i < t.Length ? (Vector3)t[i] : Vector3.right;
                Vector3 tw = full.MultiplyVector(tv).normalized;
                tangents.Add(new Vector4(tw.x, tw.y, tw.z, i < t.Length ? t[i].w : 1f));
                Color32 vc = i < c.Length ? c[i] : new Color32(255, 255, 255, 0);
                if (dye.HasValue && vc.a == dyedPart)      // the part the caller colours (canvas of a tent or a wagon cover)
                    vc = new Color32((byte)(dye.Value.r * vc.r / 255), (byte)(dye.Value.g * vc.g / 255), (byte)(dye.Value.b * vc.b / 255), 0);
                else if (dye2.HasValue && vc.a == dyedPart2)
                    vc = new Color32((byte)(dye2.Value.r * vc.r / 255), (byte)(dye2.Value.g * vc.g / 255), (byte)(dye2.Value.b * vc.b / 255), 0);
                colors.Add(vc);
                uvs.Add(i < uv.Count ? new Vector4(uv[i].x, uv[i].y, 0f, 0f) : Vector4.zero);
                surfs.Add(i < sf.Count ? sf[i] : new Vector4(0f, 1f, 0f, 0f));
            }
            int[] tri = m.triangles;
            for (int i = 0; i < tri.Length; i++) tris.Add(tri[i] + b);
        }

        public void Clear()
        {
            verts.Clear(); normals.Clear(); colors.Clear(); uvs.Clear(); tris.Clear(); weights.Clear(); surfs.Clear(); tangents.Clear();
        }

        // ------------------------------------------------------------------ bake

        public Mesh ToMesh(string name, bool keepReadable = false)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(3, surfs);
            mesh.SetUVs(4, normals);
            if (tangents.Count == verts.Count) mesh.SetTangents(tangents);
            mesh.SetTriangles(tris, 0);
            if (skinned && weights.Count == verts.Count) mesh.boneWeights = weights.ToArray();
            mesh.RecalculateBounds();
            if (!keepReadable && !skinned) mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>Creates a GameObject with the baked mesh under 'parent'.</summary>
        public GameObject ToObject(string name, Transform parent, Material mat, bool shadows = true)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }
    }
}
