using UnityEngine;
using UnityEngine.Rendering;

namespace Canaan
{
    /// <summary>
    /// Rock faces over the cliffs of a heightmap. The terrain stretches its textures down a cliff and has no room for
    /// crags or ledges, so the steep ground gets a skin of its own: the same heights at twice the resolution, pushed
    /// out into crags, gullies and the shelves of harder beds, drawn with the triplanar surface shader and its beds
    /// of sandstone (_STRATA). It stands a hand off the ground, so the terrain never shows through, and has no
    /// collider: the terrain under it is what people and horses walk against.
    /// </summary>
    public static class Cliffs
    {
        const float SteepFrom = 0.62f, SteepFull = 1.05f;      // slope (rise over run) where the skin begins and where it is full
        public static int Vertices { get; private set; }

        static float Sample(float[,] a, float gx, float gz)
        {
            int res = a.GetLength(0);
            int ix = Mathf.Clamp(Mathf.FloorToInt(gx), 0, res - 2), iz = Mathf.Clamp(Mathf.FloorToInt(gz), 0, res - 2);
            float fx = Mathf.Clamp01(gx - ix), fz = Mathf.Clamp01(gz - iz);
            return Mathf.Lerp(Mathf.Lerp(a[iz, ix], a[iz, ix + 1], fx), Mathf.Lerp(a[iz + 1, ix], a[iz + 1, ix + 1], fx), fz);
        }

        /// <summary>How far the face stands out at a point: crags, gullies down the face, shelves where a hard bed lies.</summary>
        static float Relief(float x, float y, float z)
        {
            float crag = Mathf.PerlinNoise(x * 0.09f + 3.1f, z * 0.09f + y * 0.06f) * 0.9f + Mathf.PerlinNoise(x * 0.31f + 7.7f, z * 0.31f + y * 0.21f) * 0.35f;
            float gully = Mathf.Abs(Mathf.PerlinNoise((x + z) * 0.045f + 11.3f, y * 0.004f) - 0.5f) * 2f;           // grooves run down the face
            float bed = y / 7.6f + Mathf.PerlinNoise(x * 0.006f, z * 0.006f) * 1.8f;
            float shelf = Mathf.Pow(1f - (bed - Mathf.Floor(bed)), 6f);                                                 // a hard bed stands out at its top
            return crag * 0.75f + gully * 0.55f + shelf * 0.9f;
        }

        /// <param name="allow">where rock faces may stand at all (not on the banks of a river)</param>
        public static void Build(Transform parent, float[,] heights, float size, float maxH, Material mat, System.Func<float, float, bool> allow)
        {
            int res = heights.GetLength(0);
            float step = size / (res - 1);
            var weight = new float[res, res];
            for (int iz = 1; iz < res - 1; iz++)
                for (int ix = 1; ix < res - 1; ix++)
                {
                    float sx = (heights[iz, ix + 1] - heights[iz, ix - 1]) * maxH / (2f * step);
                    float sz = (heights[iz + 1, ix] - heights[iz - 1, ix]) * maxH / (2f * step);
                    weight[iz, ix] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SteepFrom, SteepFull, Mathf.Sqrt(sx * sx + sz * sz)));
                    if (weight[iz, ix] > 0f && !allow(ix * step, iz * step)) weight[iz, ix] = 0f;
                }

            var root = new GameObject("Cliffs") { layer = Layers.Static };
            root.transform.SetParent(parent, false);
            Vertices = 0;
            const int Chunk = 64;
            int n = Chunk * 2 + 1;
            var index = new int[n * n];
            for (int cz = 0; cz < res - 1; cz += Chunk)
                for (int cx = 0; cx < res - 1; cx += Chunk)
                {
                    var b = new MeshBuilder();
                    b.Col(new Color32(0xB2, 0x70, 0x50, 255));
                    b.Lock(Surf.Rock);
                    b.wind = 0;
                    for (int i = 0; i < index.Length; i++) index[i] = -1;

                    int V(int sx, int sz)
                    {
                        int k = sz * n + sx;
                        if (index[k] >= 0) return index[k];
                        float gx = cx + sx * 0.5f, gz = cz + sz * 0.5f;
                        float x = gx * step, z = gz * step;
                        float h = Sample(heights, gx, gz) * maxH;
                        float hx = (Sample(heights, gx + 0.5f, gz) - Sample(heights, gx - 0.5f, gz)) * maxH / step;
                        float hz = (Sample(heights, gx, gz + 0.5f) - Sample(heights, gx, gz - 0.5f)) * maxH / step;
                        Vector3 nrm = new Vector3(-hx, 1f, -hz).normalized;
                        float w = Sample(weight, gx, gz);
                        Vector3 out2 = new Vector3(nrm.x, 0f, nrm.z);
                        out2 = out2.sqrMagnitude > 1e-6f ? out2.normalized : Vector3.zero;
                        // a hand off the ground everywhere, and the relief where the face is steep
                        Vector3 p = new Vector3(x, h, z) + nrm * 0.16f + out2 * (w * (0.35f + Relief(x, h, z)));
                        index[k] = b.Vert(p, nrm);
                        return index[k];
                    }

                    int t0 = b.tris.Count;
                    for (int iz = cz; iz < Mathf.Min(cz + Chunk, res - 1); iz++)
                        for (int ix = cx; ix < Mathf.Min(cx + Chunk, res - 1); ix++)
                        {
                            float w = Mathf.Max(Mathf.Max(weight[iz, ix], weight[iz, ix + 1]), Mathf.Max(weight[iz + 1, ix], weight[iz + 1, ix + 1]));
                            if (w < 0.02f) continue;
                            int lx = (ix - cx) * 2, lz = (iz - cz) * 2;
                            for (int qz = 0; qz < 2; qz++)
                                for (int qx = 0; qx < 2; qx++)
                                {
                                    int a = V(lx + qx, lz + qz), c = V(lx + qx + 1, lz + qz), d = V(lx + qx, lz + qz + 1), e = V(lx + qx + 1, lz + qz + 1);
                                    b.Tri(a, d, e);
                                    b.Tri(a, e, c);
                                }
                        }
                    if (b.VertexCount == 0) continue;
                    b.SmoothNormals(0, b.VertexCount, t0, b.tris.Count);
                    GameObject go = b.ToObject("Cliff_" + cx + "_" + cz, root.transform, mat, true);
                    go.layer = Layers.Static;
                    Vertices += b.VertexCount;
                }
            Debug.Log("Cliffs: " + Vertices + " vertices of rock face");
        }
    }

    /// <summary>
    /// The country beyond the map, out to the haze: plains and distant mesas on a ring of coarse ground that meets
    /// the terrain's edge exactly, so no edge of the world shows through the passes or from the tops.
    /// </summary>
    public static class Horizon
    {
        static float Outer(float x, float z)
        {
            float n1 = U.Fbm(x / 900f + 2.3f, z / 900f + 8.1f, 4);
            float h = 38f + (n1 - 0.5f) * 30f;
            float m = U.Fbm(x / 1500f + 13.1f, z / 1500f + 5.7f, 3);
            float mesa = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.54f, 0.6f, m));
            return Mathf.Lerp(h, 92f + (n1 - 0.5f) * 40f, mesa);
        }

        public static void Build(Transform parent, System.Func<float, float, float> edgeHeight, float size, Material mat)
        {
            float[] ds = { 0f, 18f, 45f, 90f, 160f, 260f, 400f, 600f, 880f, 1250f, 1750f, 2400f, 3200f, 4300f };
            const int perSide = 160;
            const int ringN = perSide * 4;
            var b = new MeshBuilder();
            b.Lock(Surf.Dirt);
            b.wind = 0;
            Color32 plain = new Color32(0xB4, 0xA4, 0x6A, 255), plainDry = new Color32(0xB8, 0x98, 0x66, 255), rock = new Color32(0xA0, 0x5E, 0x40, 255);
            var hs = new float[ds.Length, ringN];
            var ps = new Vector3[ds.Length, ringN];
            for (int k = 0; k < ds.Length; k++)
                for (int i = 0; i < ringN; i++)
                {
                    float d = ds[k], s = i / (float)perSide, t = s - Mathf.Floor(s), L = size + 2f * d;
                    int side = Mathf.FloorToInt(s);
                    Vector2 p = side == 0 ? new Vector2(-d + L * t, -d) : side == 1 ? new Vector2(size + d, -d + L * t)
                              : side == 2 ? new Vector2(size + d - L * t, size + d) : new Vector2(-d, size + d - L * t);
                    float edge = edgeHeight(p.x, p.y);
                    float h = k == 0 ? edge : Mathf.Lerp(edge, Outer(p.x, p.y), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 520f, d)));
                    hs[k, i] = h;
                    ps[k, i] = new Vector3(p.x, h, p.y);
                }
            for (int k = 0; k < ds.Length; k++)
                for (int i = 0; i < ringN; i++)
                {
                    Vector3 p = ps[k, i];
                    Vector3 a = ps[k, (i + 1) % ringN] - ps[k, (i + ringN - 1) % ringN];
                    Vector3 c = ps[Mathf.Min(k + 1, ds.Length - 1), i] - ps[Mathf.Max(k - 1, 0), i];
                    Vector3 nrm = Vector3.Cross(c, a).normalized;
                    if (nrm.y < 0f) nrm = -nrm;
                    float steep = Mathf.Clamp01((1f - nrm.y) * 3.5f);
                    Color32 col = Color32.Lerp(Color32.Lerp(plain, plainDry, U.Fbm(p.x / 400f, p.z / 400f, 2)), rock, steep);
                    b.Col(col);
                    b.Lock(Surf.Dirt);
                    b.Vert(p, nrm);
                }
            for (int k = 0; k < ds.Length - 1; k++)
                for (int i = 0; i < ringN; i++)
                {
                    int a = k * ringN + i, c = k * ringN + (i + 1) % ringN, d = (k + 1) * ringN + i, e = (k + 1) * ringN + (i + 1) % ringN;
                    b.Tri(a, c, e);
                    b.Tri(a, e, d);
                }
            GameObject go = b.ToObject("Horizon", parent, mat, false);
            go.layer = Layers.Static;
            go.GetComponent<MeshRenderer>().receiveShadows = false;
        }
    }
}
