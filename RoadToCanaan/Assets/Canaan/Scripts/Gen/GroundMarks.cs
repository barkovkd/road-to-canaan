using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Canaan
{
    /// <summary>
    /// What feet, hooves and wheels leave on the ground: ruts along the streets and roads, trodden patches at the rails,
    /// troughs and doors, hoof prints, loose sand drifted against porches, mud round the water. Thin meshes draped over
    /// the land and drawn with Canaan/GroundMark, which shades the ground's own colour (2x multiply) instead of laying a
    /// colour of its own over it - an opaque strip never matched the terrain's shading and read as a board.
    /// </summary>
    public static class GroundMarks
    {
        public const byte Rut = 0, Trodden = 1, Hoof = 2, Sand = 3, Mud = 4;
        static readonly List<Vector3> verts = new List<Vector3>();
        static readonly List<Vector2> uvs = new List<Vector2>();
        static readonly List<Color32> cols = new List<Color32>();
        static readonly List<int> tris = new List<int>();
        const float Lift = 0.02f;

        public static void Clear() { verts.Clear(); uvs.Clear(); cols.Clear(); tris.Clear(); }

        static void Vert(float x, float z, Vector2 uv, byte kind, float strength)
        {
            verts.Add(new Vector3(x, RegionDryFord.HeightAt(x, z) + Lift, z));
            uvs.Add(uv);
            cols.Add(new Color32(kind, (byte)Mathf.Clamp(Mathf.RoundToInt(strength * 255f), 0, 255), 0, 255));
        }

        /// <summary>A strip 'width' wide along a line of ground points; it fades in and out over its first and last 3 m.</summary>
        public static void Strip(IList<Vector2> line, float width, byte kind, float strength)
        {
            if (line.Count < 2) return;
            float total = 0f;
            for (int i = 1; i < line.Count; i++) total += Vector2.Distance(line[i], line[i - 1]);
            int start = verts.Count;
            float u = 0f;
            for (int i = 0; i < line.Count; i++)
            {
                if (i > 0) u += Vector2.Distance(line[i], line[i - 1]);
                Vector2 d = i == 0 ? line[1] - line[0] : i == line.Count - 1 ? line[i] - line[i - 1] : line[i + 1] - line[i - 1];
                d.Normalize();
                Vector2 side = new Vector2(-d.y, d.x) * (width * 0.5f);
                float ends = Mathf.Clamp01(Mathf.Min(u, total - u) / 3f);
                Vert(line[i].x - side.x, line[i].y - side.y, new Vector2(u, 0f), kind, strength * ends);
                Vert(line[i].x + side.x, line[i].y + side.y, new Vector2(u, 1f), kind, strength * ends);
                if (i == 0) continue;
                int a = start + (i - 1) * 2;
                tris.Add(a); tris.Add(a + 2); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3);
            }
        }

        /// <summary>An oval patch (half-sizes rx across, rz along its yaw) draped on a small grid, uv 0..1 over it.</summary>
        public static void Patch(Vector2 c, float rx, float rz, float yaw, byte kind, float strength)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(rx, rz) / 1.2f), 1, 6);
            float s = Mathf.Sin(yaw * Mathf.Deg2Rad), co = Mathf.Cos(yaw * Mathf.Deg2Rad);
            int start = verts.Count;
            for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    float u = i / (float)n, v = j / (float)n;
                    float lx = (u * 2f - 1f) * rx, lz = (v * 2f - 1f) * rz;
                    Vert(c.x + lx * co + lz * s, c.y - lx * s + lz * co, new Vector2(u, v), kind, strength);
                }
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int a = start + j * (n + 1) + i;
                    tris.Add(a); tris.Add(a + n + 1); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(a + n + 1); tris.Add(a + n + 2);
                }
        }

        /// <summary>Bakes everything marked so far into one mesh under the region.</summary>
        public static void Finish(Region r)
        {
            if (verts.Count == 0 || Mats.GroundMark == null) return;
            var mesh = new Mesh { name = "GroundMarks", indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("GroundMarks");
            go.transform.SetParent(r.root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.GroundMark;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Debug.Log("CANAAN ground marks: " + verts.Count + " vertices");
            Clear();
        }
    }
}
