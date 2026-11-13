using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Canaan
{
    /// <summary>A path on the ground (river, road, railway) with fast distance queries.</summary>
    public class Polyline
    {
        public readonly Vector2[] pts;
        public readonly float[] cum;         // cumulative length at each point
        public readonly float length;
        readonly float minX, maxX, minZ, maxZ;

        public Polyline(params Vector2[] points)
        {
            pts = points;
            cum = new float[pts.Length];
            minX = minZ = float.MaxValue; maxX = maxZ = float.MinValue;
            for (int i = 0; i < pts.Length; i++)
            {
                if (i > 0) cum[i] = cum[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);
                minX = Mathf.Min(minX, pts[i].x); maxX = Mathf.Max(maxX, pts[i].x);
                minZ = Mathf.Min(minZ, pts[i].y); maxZ = Mathf.Max(maxZ, pts[i].y);
            }
            length = cum[pts.Length - 1];
        }

        /// <summary>Rounds the corners by Chaikin subdivision.</summary>
        public Polyline Smoothed(int passes = 2)
        {
            var p = new List<Vector2>(pts);
            for (int n = 0; n < passes; n++)
            {
                var q = new List<Vector2> { p[0] };
                for (int i = 0; i < p.Count - 1; i++)
                {
                    q.Add(Vector2.Lerp(p[i], p[i + 1], 0.25f));
                    q.Add(Vector2.Lerp(p[i], p[i + 1], 0.75f));
                }
                q.Add(p[p.Count - 1]);
                p = q;
            }
            return new Polyline(p.ToArray());
        }

        /// <summary>Distance from (x,z) to the path, or 'reach' if farther. 'along' is the distance along the path.</summary>
        public float Distance(float x, float z, float reach, out float along)
        {
            along = 0f;
            if (x < minX - reach || x > maxX + reach || z < minZ - reach || z > maxZ + reach) return reach;
            float best = reach * reach;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                float abx = b.x - a.x, abz = b.y - a.y;
                float len2 = abx * abx + abz * abz;
                float t = len2 > 1e-6f ? Mathf.Clamp01(((x - a.x) * abx + (z - a.y) * abz) / len2) : 0f;
                float dx = x - (a.x + abx * t), dz = z - (a.y + abz * t);
                float d2 = dx * dx + dz * dz;
                if (d2 < best) { best = d2; along = cum[i] + Mathf.Sqrt(len2) * t; }
            }
            return Mathf.Sqrt(best);
        }

        public float Distance(float x, float z, float reach) { return Distance(x, z, reach, out _); }

        public Vector2 At(float along)
        {
            along = Mathf.Clamp(along, 0f, length);
            for (int i = 0; i < pts.Length - 1; i++)
                if (along <= cum[i + 1]) return Vector2.Lerp(pts[i], pts[i + 1], Mathf.InverseLerp(cum[i], cum[i + 1], along));
            return pts[pts.Length - 1];
        }

        public Vector2 Tangent(float along)
        {
            Vector2 d = At(along + 1f) - At(along - 1f);
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.up;
        }
    }

    public static class TexKit
    {
        /// <summary>A tileable noise texture between two colours, with optional darker and lighter speckles.</summary>
        public static Texture2D Tile(int size, Color a, Color b, float scale, int seed, float speckle, Color speckleCol, float streak = 0f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var px = new Color[size * size];
            float off = seed * 37.13f;
            var rng = new System.Random(seed);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float n = Seamless(x, y, size, scale, off, streak);
                    float fine = Seamless(x, y, size, scale * 5.3f, off + 71f, streak);
                    float t = Mathf.Clamp01(n * 0.75f + fine * 0.35f - 0.05f);
                    px[y * size + x] = Color.Lerp(a, b, t);
                }
            int dots = Mathf.RoundToInt(speckle * size * size / 40f);
            for (int i = 0; i < dots; i++)
            {
                int cx = rng.Next(size), cy = rng.Next(size), r = 1 + rng.Next(2);
                float k = 0.15f + (float)rng.NextDouble() * 0.3f;
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (dx * dx + dy * dy > r * r) continue;
                        int ix = (cx + dx + size) % size, iy = (cy + dy + size) % size;
                        px[iy * size + ix] = Color.Lerp(px[iy * size + ix], speckleCol, k);
                    }
            }
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }

        static float Seamless(int x, int y, int size, float scale, float off, float streak)
        {
            float u = x / (float)size, v = y / (float)size;
            float sx = scale, sy = scale * (1f - streak * 0.8f);
            float fx = x * sx / size, fy = y * sy / size;
            float n00 = Mathf.PerlinNoise(fx + off, fy + off);
            float n10 = Mathf.PerlinNoise(fx - sx + off, fy + off);
            float n01 = Mathf.PerlinNoise(fx + off, fy - sy + off);
            float n11 = Mathf.PerlinNoise(fx - sx + off, fy - sy + off);
            return Mathf.Lerp(Mathf.Lerp(n00, n10, u), Mathf.Lerp(n01, n11, u), v);
        }
    }

    public static class TerrainKit
    {
        public static TerrainLayer Layer(string name)
        {
            var layer = Resources.Load<TerrainLayer>("Terrain/" + name);
            if (layer == null) Debug.LogError("terrain layer missing: " + name + " — run Tools/setup.sh");
            return layer;
        }

        public static TerrainLayer Layer(Texture2D tex, float tile)
        {
            return new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(tile, tile), smoothness = 0f, metallic = 0f, specular = Color.black };
        }

        /// <summary>Creates a terrain from normalised heights [z, x] and layer weights [z, x, layer].</summary>
        public static Terrain Create(Transform parent, float[,] heights, float size, float maxHeight, float[,,] alpha, TerrainLayer[] layers)
        {
            int res = heights.GetLength(0);
            var data = new TerrainData { heightmapResolution = res };
            data.size = new Vector3(size, maxHeight, size);
            data.SetHeights(0, 0, heights);
            data.terrainLayers = layers;
            data.alphamapResolution = alpha.GetLength(0);
            data.baseMapResolution = 1024;
            data.SetAlphamaps(0, 0, alpha);

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.SetParent(parent, false);
            go.layer = Layers.Ground;
            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = Mats.Terrain;
            terrain.heightmapPixelError = 4f;     // the rock skin over the cliffs (Cliffs) sits a hand off the ground; coarser patches poke through
            terrain.basemapDistance = 170f;     // beyond this the blended base map hides the repeat of the layer textures
            terrain.drawInstanced = false;     // instanced patches take their normals from a texture that lights wrongly here (see CanaanSetup)
            terrain.shadowCastingMode = ShadowCastingMode.On;
            terrain.allowAutoConnect = false;
            return terrain;
        }
    }

    /// <summary>
    /// Vegetation and rocks drawn with GPU instancing. Trees, bushes and boulders are placed once and kept in
    /// a coarse grid; grass is generated on the fly in the cells around the camera from a density function.
    /// </summary>
    public class Scatter
    {
        const float Cell = 128f, GrassCell = 16f;
        /// <summary>Tufts are drawn this far from the camera; from GrassNear on, fewer and fewer of them.</summary>
        public const float GrassRange = 78f, GrassNear = 24f;

        class Proto
        {
            public Mesh mesh, far;                  // far: a lighter copy for cells beyond farFrom, drawn without shadows
            public Material mat;
            public float view, farFrom;
            public bool shadows;
            public Dictionary<int, List<Matrix4x4>> cells = new Dictionary<int, List<Matrix4x4>>();
            public Dictionary<int, Matrix4x4[]> baked = new Dictionary<int, Matrix4x4[]>();
        }

        readonly List<Proto> protos = new List<Proto>();
        readonly List<Mesh> grassMeshes = new List<Mesh>();
        readonly List<float> grassShares = new List<float>();
        float shareSum;
        readonly Dictionary<long, Matrix4x4[][]> grassCells = new Dictionary<long, Matrix4x4[][]>();
        readonly Plane[] planes = new Plane[6];
        readonly List<long> evict = new List<long>();
        public Func<float, float, float> grassDensity;      // 0..1 at a world position
        public Func<float, float, float> groundHeight;
        public int grassPerCell = 860;                      // at full density: over three tufts to the square metre
        /// <summary>New cells of grass laid per frame, the nearest first: a camera swinging over fresh ground would
        /// otherwise stall the frame that has to lay fifty at once. The bot's screenshots take them all at once.</summary>
        public int grassCellsPerFrame = 3;
        struct Want { public int cx, cz; public float order; public bool seen; }
        readonly List<Want> wanted = new List<Want>();
        static readonly Comparison<Want> Nearest = (a, b) => a.order.CompareTo(b.order);
        public int Instances { get; private set; }

        public int AddProto(Mesh mesh, Material mat, float viewDistance, bool shadows, Mesh far = null, float farFrom = 140f)
        {
            protos.Add(new Proto { mesh = mesh, mat = mat, view = viewDistance, shadows = shadows, far = far, farFrom = farFrom });
            return protos.Count - 1;
        }

        /// <summary>A kind of tuft and its share among the tufts of a meadow.</summary>
        public void AddGrass(Mesh mesh, float share = 1f) { grassMeshes.Add(mesh); grassShares.Add(share); shareSum += share; }

        /// <summary>
        /// A tuft as three crossed cards cut from the grass atlas (make_grass.py). Normals point up, so a tuft takes
        /// the light exactly as the ground under it does; the colour alpha is the wind weight, none at the root.
        /// </summary>
        public static Mesh GrassCard(Vector4 uv, Vector2 size, string name)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<Vector2>(); var c = new List<Color32>(); var tri = new List<int>();
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI / 3f + 0.35f;
                Vector3 side = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (size.x * 0.5f);
                // the tops tip outwards a little: from above the cards do not read as a star
                Vector3 lean = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * (size.y * 0.2f * (k == 1 ? -1f : 1f));
                Vector3 foot = Vector3.down * 0.02f, top = Vector3.up * size.y + lean;
                int b = v.Count;
                v.Add(-side + foot); v.Add(side + foot); v.Add(side + top); v.Add(-side + top);
                t.Add(new Vector2(uv.x, uv.y)); t.Add(new Vector2(uv.z, uv.y)); t.Add(new Vector2(uv.z, uv.w)); t.Add(new Vector2(uv.x, uv.w));
                c.Add(new Color32(255, 255, 255, 0)); c.Add(new Color32(255, 255, 255, 0)); c.Add(new Color32(255, 255, 255, 255)); c.Add(new Color32(255, 255, 255, 255));
                for (int i = 0; i < 4; i++) n.Add(Vector3.up);
                tri.Add(b); tri.Add(b + 2); tri.Add(b + 1); tri.Add(b); tri.Add(b + 3); tri.Add(b + 2);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, t); mesh.SetColors(c); mesh.SetTriangles(tri, 0);
            mesh.bounds = new Bounds(new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x * 1.3f, size.y * 1.2f, size.x * 1.3f));
            return mesh;
        }

        public void Add(int proto, Vector3 pos, float yawDeg, Vector3 scale)
        {
            int key = Mathf.FloorToInt(pos.x / Cell) * 1000 + Mathf.FloorToInt(pos.z / Cell);
            Proto p = protos[proto];
            if (!p.cells.TryGetValue(key, out List<Matrix4x4> list)) { list = new List<Matrix4x4>(); p.cells[key] = list; }
            list.Add(Matrix4x4.TRS(pos, Quaternion.Euler(0f, yawDeg, 0f), scale));
            Instances++;
        }

        public void Bake()
        {
            foreach (Proto p in protos)
            {
                foreach (var kv in p.cells) p.baked[kv.Key] = kv.Value.ToArray();
                p.cells.Clear();
            }
        }

        /// <summary>Submits this frame's draws. Call once per frame before the cameras render.</summary>
        public void Tick(Camera cam)
        {
            Vector3 cp = cam.transform.position;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            foreach (Proto p in protos)
            {
                var rp = new RenderParams(p.mat)
                {
                    shadowCastingMode = p.shadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,
                    worldBounds = new Bounds(cp, new Vector3(p.view * 2.2f, 600f, p.view * 2.2f))
                };
                var rpFar = rp;
                rpFar.shadowCastingMode = ShadowCastingMode.Off;
                foreach (var kv in p.baked)
                {
                    float cx = (kv.Key / 1000 + 0.5f) * Cell, cz = (kv.Key % 1000 + 0.5f) * Cell;
                    float dx = Mathf.Max(0f, Mathf.Abs(cx - cp.x) - Cell * 0.5f), dz = Mathf.Max(0f, Mathf.Abs(cz - cp.z) - Cell * 0.5f);
                    if (dx * dx + dz * dz > p.view * p.view) continue;
                    var b = new Bounds(new Vector3(cx, cp.y, cz), new Vector3(Cell + 20f, 500f, Cell + 20f));
                    // shadow casters behind the camera still matter close by; cull only what is clearly out of view
                    if (dx * dx + dz * dz > 80f * 80f && !GeometryUtility.TestPlanesAABB(planes, b)) continue;
                    Matrix4x4[] arr = kv.Value;
                    bool distant = p.far != null && dx * dx + dz * dz > p.farFrom * p.farFrom;
                    for (int start = 0; start < arr.Length; start += 1000)
                        Graphics.RenderMeshInstanced(distant ? rpFar : rp, distant ? p.far : p.mesh, 0, arr, Mathf.Min(1000, arr.Length - start), start);
                }
            }
            if (grassMeshes.Count > 0 && grassDensity != null) Grass(cp);
        }

        void Grass(Vector3 cp)
        {
            int c0x = Mathf.FloorToInt((cp.x - GrassRange) / GrassCell), c1x = Mathf.FloorToInt((cp.x + GrassRange) / GrassCell);
            int c0z = Mathf.FloorToInt((cp.z - GrassRange) / GrassCell), c1z = Mathf.FloorToInt((cp.z + GrassRange) / GrassCell);
            var rp = new RenderParams(Mats.Grass)
            {
                shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true,
                worldBounds = new Bounds(cp, new Vector3(GrassRange * 2.4f, 200f, GrassRange * 2.4f))
            };
            void Draw(Matrix4x4[][] sets, float dx, float dz)
            {
                // the tufts of a cell lie in random order: drawing the first part of each list thins the meadow evenly with distance
                float far = Mathf.Max(0f, Mathf.Sqrt(dx * dx + dz * dz) - GrassCell * 0.7f);
                float keep = Mathf.Lerp(1f, 0.3f, Mathf.InverseLerp(GrassNear, GrassRange, far));
                for (int m = 0; m < sets.Length; m++)
                {
                    int count = Mathf.CeilToInt(sets[m].Length * keep);
                    if (count > 0) Graphics.RenderMeshInstanced(rp, grassMeshes[m], 0, sets[m], count, 0);
                }
            }
            wanted.Clear();
            for (int cz = c0z; cz <= c1z; cz++)
                for (int cx = c0x; cx <= c1x; cx++)
                {
                    float wx = (cx + 0.5f) * GrassCell, wz = (cz + 0.5f) * GrassCell;
                    float dx = wx - cp.x, dz = wz - cp.z;
                    if (dx * dx + dz * dz > (GrassRange + GrassCell) * (GrassRange + GrassCell)) continue;
                    var b = new Bounds(new Vector3(wx, cp.y, wz), new Vector3(GrassCell, 60f, GrassCell));
                    bool seen = GeometryUtility.TestPlanesAABB(planes, b);
                    long key = ((long)cx << 32) ^ (uint)cz;
                    if (!grassCells.TryGetValue(key, out Matrix4x4[][] sets))
                    {
                        // cells behind the camera are laid too, after the ones in view: a turn of the head finds them ready
                        wanted.Add(new Want { cx = cx, cz = cz, seen = seen, order = dx * dx + dz * dz + (seen ? 0f : 1e7f) });
                        continue;
                    }
                    if (seen) Draw(sets, dx, dz);
                }
            if (wanted.Count > 0)
            {
                wanted.Sort(Nearest);
                for (int i = 0; i < wanted.Count && i < grassCellsPerFrame; i++)
                {
                    Want w = wanted[i];
                    Matrix4x4[][] sets = MakeGrassCell(w.cx, w.cz);
                    grassCells[((long)w.cx << 32) ^ (uint)w.cz] = sets;
                    if (w.seen) Draw(sets, (w.cx + 0.5f) * GrassCell - cp.x, (w.cz + 0.5f) * GrassCell - cp.z);
                }
            }
            if (grassCells.Count > 600)
            {
                evict.Clear();
                foreach (var kv in grassCells)
                {
                    int cx = (int)(kv.Key >> 32), cz = (int)(uint)(kv.Key & 0xFFFFFFFF);
                    float dx = (cx + 0.5f) * GrassCell - cp.x, dz = (cz + 0.5f) * GrassCell - cp.z;
                    if (dx * dx + dz * dz > 160f * 160f) evict.Add(kv.Key);
                }
                foreach (long k in evict) grassCells.Remove(k);
            }
        }

        Matrix4x4[][] MakeGrassCell(int cx, int cz)
        {
            var lists = new List<Matrix4x4>[grassMeshes.Count];
            for (int i = 0; i < lists.Length; i++) lists[i] = new List<Matrix4x4>();
            for (int i = 0; i < grassPerCell; i++)
            {
                float hx = U.Hash01(cx * 73 + i, cz * 31 - i, 1), hz = U.Hash01(cx * 17 - i, cz * 59 + i, 2);
                float x = (cx + hx) * GrassCell, z = (cz + hz) * GrassCell;
                float dens = grassDensity(x, z);
                float pick = U.Hash01(cx + i * 7, cz - i * 13, 3);
                if (pick > dens) continue;
                float y = groundHeight(x, z);
                float s = 0.7f + U.Hash01(cx - i, cz + i * 3, 4) * 0.5f;
                // which tuft: by its share of the meadow
                float roll = U.Hash01(cx * 5 + i, cz * 3 + i, 5) * shareSum;
                int kind = 0;
                while (kind < grassShares.Count - 1 && roll > grassShares[kind]) { roll -= grassShares[kind]; kind++; }
                lists[kind].Add(Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, pick * 720f, 0f), new Vector3(s, s * (0.72f + dens * 0.3f), s)));
            }
            var arr = new Matrix4x4[lists.Length][];
            for (int i = 0; i < lists.Length; i++) arr[i] = lists[i].ToArray();
            return arr;
        }
    }
}
