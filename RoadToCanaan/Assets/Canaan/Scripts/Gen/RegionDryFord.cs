using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Canaan
{
    /// <summary>
    /// Region 1, «Сухой Брод»: rolling prairie with a river and its ford, a railhead town, the settlers' camp,
    /// a ranch and homesteads, ringed by mesas. 2048 x 2048 m, generated from code at load.
    /// This file makes the land; DryFordPlaces.cs puts the town and the camp on it.
    /// </summary>
    public static partial class RegionDryFord
    {
        public const float Size = 2048f, MaxH = 170f, Water = 26f;
        const int Res = 1025, ARes = 1024, MaskRes = 512;

        public static readonly Vector2 Town = new Vector2(900f, 1000f), CampC = new Vector2(1320f, 1250f), Ranch = new Vector2(1150f, 560f);
        public static readonly Vector2 Ford = new Vector2(1490f, 1040f), Pond = new Vector2(600f, 700f), Butte = new Vector2(380f, 420f);
        public static readonly Vector2 Olson = new Vector2(520f, 1380f), MillFarm = new Vector2(1250f, 820f), Cemetery = new Vector2(760f, 1135f);
        public static readonly Vector2 OldStation = new Vector2(330f, 1650f);
        /// <summary>The stock pens by the depot (DryFordPlaces.BuildRailway): trodden bare, nothing growing.</summary>
        public static readonly Vector2 StockPens = new Vector2(727f, 965f);

        static Polyline river, mainRoad, campRoad, ranchRoad, northRoad, rail;
        static Polyline[] roads;
        static float fordAlong;
        static float[] railHeights;
        static float[,] heights;
        static byte[] grassMask;
        static Terrain terrain;
        static Scatter scatter;

        struct Flat { public float cx, cz, hx, hz, fall, h; }
        static readonly List<Flat> flats = new List<Flat>();

        public static Region Build()
        {
            float t0 = Time.realtimeSinceStartup;
            var r = new Region { id = "dryford", name = "Сухой Брод", subtitle = "Территория Вермильон, весна 1887 года", root = new GameObject("Region_DryFord") };
            RegionLights.Clear();
            Doors.Clear();
            Windmills.Clear();
            Trains.Clear();
            Emitters.Clear();
            DefinePaths();
            DefineFlats();
            BuildTerrain(r);
            World.terrain = terrain;
            World.waterLevel = Water;
            BuildWater(r);
            // rock faces on the mesas and the butte; the banks of the river and the pond stay earth
            Cliffs.Build(r.root.transform, heights, Size, MaxH, Mats.Cliff,
                         (x, z) => HeightAt(x, z) > Water + 10f && river.Distance(x, z, 90f) >= 90f && Vector2.Distance(new Vector2(x, z), Pond) > 130f);
            Horizon.Build(r.root.transform, (x, z) => HeightAt(Mathf.Clamp(x, 0f, Size), Mathf.Clamp(z, 0f, Size)), Size, Mats.Lit);
            BuildVegetation(r);
            BuildPlaces(r);
            Physics.SyncTransforms();
            BuildNav(r);
            r.map = BuildMap();
            Population.SpawnDryFord(r);
            Wildlife.SpawnDryFord(r);
            Game.I.Ticked += TickScatter;
            Game.I.Ticked -= Doors.Tick;
            Game.I.Ticked += Doors.Tick;
            Game.I.Ticked -= Windmills.Tick;
            Game.I.Ticked += Windmills.Tick;
            Game.I.Ticked -= Trains.Tick;
            Game.I.Ticked += Trains.Tick;
            Game.I.Ticked -= Emitters.Tick;
            Game.I.Ticked += Emitters.Tick;
            Sfx.Grass = GrassAt;
            Sfx.Water = RiverNear;
            Game.I.Ticked -= Sfx.Tick;
            Game.I.Ticked += Sfx.Tick;
            Game.I.clock.NightChanged += RegionLights.SetNight;
            RegionLights.SetNight(Game.I.clock.IsNight);
            Debug.Log("RegionDryFord built in " + (Time.realtimeSinceStartup - t0).ToString("0.00") + " s, " + scatter.Instances + " scattered instances");
            return r;
        }

        static void TickScatter(float dt)
        {
            if (scatter != null && terrain != null && Game.I != null) scatter.Tick(Game.I.cam);
        }

        /// <summary>Draws vegetation for cameras that are positioned by hand (menu, tests).</summary>
        public static void SubmitScatter() { if (scatter != null && terrain != null) scatter.Tick(Game.I.cam); }

        // ================================================================== layout

        static void DefinePaths()
        {
            river = new Polyline(new Vector2(1570, 2048), new Vector2(1520, 1760), new Vector2(1560, 1480), new Vector2(1500, 1240),
                                 new Vector2(1490, 1040), new Vector2(1540, 860), new Vector2(1480, 600), new Vector2(1530, 300), new Vector2(1500, 0)).Smoothed(3);
            river.Distance(Ford.x, Ford.y, 500f, out fordAlong);
            mainRoad = new Polyline(new Vector2(0, 1000), new Vector2(300, 1010), new Vector2(620, 995), new Vector2(780, 1000), new Vector2(1040, 1000),
                                    new Vector2(1250, 1015), new Vector2(1400, 1035), new Vector2(1490, 1040), new Vector2(1600, 1050), new Vector2(1820, 1090), new Vector2(2048, 1100)).Smoothed(2);
            campRoad = new Polyline(new Vector2(1180, 1010), new Vector2(1230, 1100), new Vector2(1290, 1200), new Vector2(1305, 1228)).Smoothed(2);
            ranchRoad = new Polyline(new Vector2(1010, 990), new Vector2(1050, 880), new Vector2(1110, 720), new Vector2(1140, 610)).Smoothed(2);
            // up the side street to the church, then away north-west past the cemetery
            northRoad = new Polyline(new Vector2(871.5f, 1008), new Vector2(871.5f, 1027), new Vector2(856, 1036), new Vector2(826, 1058), new Vector2(800, 1090), new Vector2(690, 1230), new Vector2(540, 1360), new Vector2(420, 1520), new Vector2(340, 1635)).Smoothed(2);
            rail = new Polyline(new Vector2(0, 600), new Vector2(330, 760), new Vector2(560, 885), new Vector2(690, 946), new Vector2(790, 954)).Smoothed(2);
            roads = new[] { mainRoad, campRoad, ranchRoad, northRoad };
        }

        static void DefineFlats()
        {
            flats.Clear();
            void Add(Vector2 c, float hx, float hz, float fall, float minH = Water + 4f)
            {
                flats.Add(new Flat { cx = c.x, cz = c.y, hx = hx, hz = hz, fall = fall, h = Mathf.Max(minH, Raw(c.x, c.y)) });
            }
            Add(new Vector2(Town.x - 10f, Town.y - 6f), 185f, 105f, 70f, Water + 9f);
            Add(CampC, 50f, 50f, 55f, Water + 5f);
            Add(Ranch, 85f, 70f, 55f);
            Add(Olson, 34f, 30f, 30f);
            Add(MillFarm, 30f, 28f, 30f);
            Add(OldStation, 26f, 22f, 28f);
        }

        /// <summary>Natural relief before any flattening or carving.</summary>
        static float Raw(float x, float z)
        {
            float h = 39f + (U.Fbm(x / 430f + 3.1f, z / 430f + 7.7f, 4) - 0.5f) * 24f + (U.Fbm(x / 70f + 9f, z / 70f + 2f, 3) - 0.5f) * 2.2f;
            float nh = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1430f, 1850f, z));
            h += nh * (U.Fbm(x / 240f + 11f, z / 240f + 5f, 4) * 64f - 9f);

            float rimW = Mathf.InverseLerp(210f, 40f, x), rimS = Mathf.InverseLerp(200f, 40f, z);
            float rimE = Mathf.InverseLerp(1850f, 2010f, x), rimN = Mathf.InverseLerp(1900f, 2030f, z);
            // passes where the road and the railway leave the map
            rimW *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(130f, 45f, Mathf.Abs(z - 1000f)));
            rimW *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(110f, 40f, Mathf.Abs(z - 615f)));
            rimE *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(130f, 45f, Mathf.Abs(z - 1098f)));
            float rim = Mathf.Max(Mathf.Max(rimW, rimS), Mathf.Max(rimE, rimN));
            float rn = U.Fbm(x / 120f + 40f, z / 120f + 13f, 3);
            float mesa = Terrace(Mathf.InverseLerp(0.36f, 0.6f, rim + (rn - 0.5f) * 0.55f));
            h = Mathf.Lerp(h, 84f + rn * 16f, mesa);

            float bd = Vector2.Distance(new Vector2(x, z), Butte) + (U.Fbm(x / 45f, z / 45f, 2) - 0.5f) * 40f;
            float butte = Terrace(Mathf.InverseLerp(108f, 64f, bd));
            h = Mathf.Lerp(h, 98f + rn * 5f, butte);
            return h;
        }

        /// <summary>
        /// The side of a mesa from the plain (0) to the top (1), the input running evenly across the slope: an apron of
        /// talus, a cliff, a bench, a second cliff and the rounded lip of the caprock.
        /// </summary>
        static float Terrace(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            if (t < 0.32f) { float u = t / 0.32f; return 0.17f * u * u; }
            if (t < 0.42f) return Mathf.Lerp(0.17f, 0.50f, (t - 0.32f) / 0.10f);
            if (t < 0.56f) return Mathf.Lerp(0.50f, 0.56f, (t - 0.42f) / 0.14f);
            if (t < 0.70f) return Mathf.Lerp(0.56f, 0.95f, (t - 0.56f) / 0.14f);
            float v = (t - 0.70f) / 0.30f;
            return Mathf.Lerp(0.95f, 1f, 1f - (1f - v) * (1f - v));
        }

        static float RiverHalfWidth(float along, out float fordK)
        {
            fordK = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(75f, 35f, Mathf.Abs(along - fordAlong)));
            return Mathf.Lerp(24f, 37f, fordK) + (Mathf.PerlinNoise(along / 90f, 3.3f) - 0.5f) * 8f * (1f - fordK);
        }

        /// <summary>Final ground height in metres (without the railway embankment).</summary>
        static float Ground(float x, float z)
        {
            float h = Raw(x, z);
            foreach (Flat f in flats)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x - f.cx) - f.hx), dz = Mathf.Max(0f, Mathf.Abs(z - f.cz) - f.hz);
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < f.fall) h = Mathf.Lerp(f.h, h, Mathf.SmoothStep(0f, 1f, d / f.fall));
            }
            h = Mathf.Max(h, Water + 1.6f);

            float rd = river.Distance(x, z, 175f, out float along);
            if (rd < 175f)
            {
                float valley = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(170f, 55f, rd));
                h = Mathf.Lerp(h, Water + 1.9f + (h - Water - 1.9f) * 0.12f, valley * 0.92f);
                float halfW = RiverHalfWidth(along, out float fordK);
                float depth = Mathf.Lerp(3.2f, 0.42f, fordK);
                float bed = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfW + 9f, halfW - 6f, rd));
                h = Mathf.Lerp(h, Water - depth, bed);
            }
            float pd = Vector2.Distance(new Vector2(x, z), Pond) + (Mathf.PerlinNoise(x / 20f, z / 20f) - 0.5f) * 10f;
            if (pd < 110f)
            {
                h = Mathf.Lerp(h, Water + 1.4f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(110f, 50f, pd)) * 0.9f);
                h = Mathf.Lerp(h, Water - 2.2f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(40f, 24f, pd)));
            }
            return h;
        }

        static float RailHeight(float along)
        {
            float f = Mathf.Clamp(along / rail.length, 0f, 1f) * (railHeights.Length - 1);
            int i = Mathf.Min(railHeights.Length - 2, Mathf.FloorToInt(f));
            return Mathf.Lerp(railHeights[i], railHeights[i + 1], f - i);
        }

        // ================================================================== terrain

        static void BuildTerrain(Region r)
        {
            // the railway keeps an even grade: sample the ground under it and smooth
            const int rn = 48;
            railHeights = new float[rn];
            for (int i = 0; i < rn; i++) { Vector2 p = rail.At(rail.length * i / (rn - 1)); railHeights[i] = Ground(p.x, p.y) + 0.35f; }
            for (int pass = 0; pass < 6; pass++)
            {
                var s = (float[])railHeights.Clone();
                for (int i = 1; i < rn - 1; i++) railHeights[i] = (s[i - 1] + s[i] * 2f + s[i + 1]) * 0.25f;
            }

            heights = new float[Res, Res];
            float step = Size / (Res - 1);
            for (int iz = 0; iz < Res; iz++)
                for (int ix = 0; ix < Res; ix++)
                {
                    float x = ix * step, z = iz * step;
                    float h = Ground(x, z);
                    float d = rail.Distance(x, z, 14f, out float along);
                    if (d < 14f) h = Mathf.Lerp(RailHeight(along), h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4.5f, 14f, d)));
                    heights[iz, ix] = h / MaxH;
                }

            // layer weights: 0 grass, 1 dirt, 2 rock, 3 sand and gravel
            var alpha = new float[ARes, ARes, 4];
            grassMask = new byte[MaskRes * MaskRes];
            float astep = Size / ARes;
            for (int iz = 0; iz < ARes; iz++)
                for (int ix = 0; ix < ARes; ix++)
                {
                    float x = (ix + 0.5f) * astep, z = (iz + 0.5f) * astep;
                    int hx = Mathf.Clamp(Mathf.RoundToInt(x / step), 1, Res - 2), hz = Mathf.Clamp(Mathf.RoundToInt(z / step), 1, Res - 2);
                    float sx = (heights[hz, hx + 1] - heights[hz, hx - 1]) * MaxH / (2f * step);
                    float sz = (heights[hz + 1, hx] - heights[hz - 1, hx]) * MaxH / (2f * step);
                    float slope = Mathf.Sqrt(sx * sx + sz * sz);
                    float h = heights[hz, hx] * MaxH;

                    float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.85f, slope));
                    float sand = 0f, dirt = 0f;
                    float rd = river.Distance(x, z, 60f, out float along);
                    if (rd < 60f)
                    {
                        float halfW = RiverHalfWidth(along, out _);
                        sand = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfW + 16f, halfW + 3f, rd));
                    }
                    float pd = Vector2.Distance(new Vector2(x, z), Pond);
                    sand = Mathf.Max(sand, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(52f, 38f, pd)));
                    float patch = U.Fbm(x / 55f + 21f, z / 55f + 4f, 3);
                    dirt = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.75f, patch)) * 0.55f;
                    if (h > 70f) dirt = Mathf.Max(dirt, 0.55f);                       // mesa tops are bare
                    foreach (Polyline road in roads)
                    {
                        float dd = road.Distance(x, z, 9f);
                        if (dd < 9f) dirt = Mathf.Max(dirt, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(6.5f, 2.8f, dd + (patch - 0.5f) * 3f)));
                    }
                    float rl = rail.Distance(x, z, 8f);
                    if (rl < 8f) sand = Mathf.Max(sand, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5f, 2.5f, rl)));
                    dirt = Mathf.Max(dirt, YardDirt(x, z, patch));

                    float grass = Mathf.Clamp01(1f - dirt);
                    grass *= 1f - sand; dirt *= 1f - sand;
                    grass *= 1f - rock; dirt *= 1f - rock; sand *= 1f - rock * 0.7f;
                    float sum = grass + dirt + rock + sand + 1e-5f;
                    alpha[iz, ix, 0] = grass / sum; alpha[iz, ix, 1] = dirt / sum; alpha[iz, ix, 2] = rock / sum; alpha[iz, ix, 3] = sand / sum;
                    if ((ix & 1) == 0 && (iz & 1) == 0) grassMask[(iz / 2) * MaskRes + ix / 2] = (byte)(Mathf.Clamp01(grass / sum - 0.25f) / 0.75f * 255f);
                }

            // grass, trodden dirt, rock, sand: textures with normal maps made by Tools/textures/make_textures.py
            var layers = new[] { TerrainKit.Layer("t_grass"), TerrainKit.Layer("t_road"), TerrainKit.Layer("t_rock"), TerrainKit.Layer("t_scrub") };
            terrain = TerrainKit.Create(r.root.transform, heights, Size, MaxH, alpha, layers);
        }

        /// <summary>Bare trodden ground in the town, the camp and the yards.</summary>
        static float YardDirt(float x, float z, float patch)
        {
            float d = 0f;
            float tx = Mathf.Max(0f, Mathf.Abs(x - (Town.x + 5f)) - 135f), tz = Mathf.Max(0f, Mathf.Abs(z - Town.y) - 34f);
            d = Mathf.Max(d, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(26f, 4f, Mathf.Sqrt(tx * tx + tz * tz) + (patch - 0.5f) * 14f)) * 0.95f);
            d = Mathf.Max(d, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 12f, Vector2.Distance(new Vector2(x, z), CampC) + (patch - 0.5f) * 16f)) * 0.75f);
            d = Mathf.Max(d, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(24f, 10f, Vector2.Distance(new Vector2(x, z), StockPens) + (patch - 0.5f) * 12f)) * 0.85f);
            d = Mathf.Max(d, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(52f, 26f, Vector2.Distance(new Vector2(x, z), Ranch) + (patch - 0.5f) * 20f)) * 0.8f);
            return d;
        }

        public static float GrassAt(float x, float z)
        {
            // nothing grows through floors and decks
            for (int i = 0; i < noGrass.Count; i++) if (noGrass[i].Contains(new Vector2(x, z))) return 0f;
            int ix = Mathf.Clamp((int)(x / Size * MaskRes), 0, MaskRes - 1), iz = Mathf.Clamp((int)(z / Size * MaskRes), 0, MaskRes - 1);
            return grassMask[iz * MaskRes + ix] / 255f;
        }

        public static float HeightAt(float x, float z) { return terrain.SampleHeight(new Vector3(x, 0f, z)); }

        /// <summary>The nearest reach of the river (its surface) within earshot of 'p', for the sound of it.</summary>
        static bool RiverNear(Vector3 p, out Vector3 at)
        {
            at = default;
            if (river == null) return false;
            float d = river.Distance(p.x, p.z, 140f, out float along);
            if (d >= 140f) return false;
            Vector2 q = river.At(along);
            at = new Vector3(q.x, Water + 0.2f, q.y);
            return true;
        }
        public static Vector3 OnLand(float x, float z) { return new Vector3(x, HeightAt(x, z), z); }
        public static Vector3 OnLand(Vector2 p) { return OnLand(p.x, p.y); }

        // ================================================================== water

        static void BuildWater(Region r)
        {
            var b = new MeshBuilder();
            b.Col(Color.white);
            const float stepLen = 10f;
            int n = Mathf.CeilToInt(river.length / stepLen);
            int start = b.verts.Count;
            for (int i = 0; i <= n; i++)
            {
                float along = Mathf.Min(river.length, i * stepLen);
                Vector2 c = river.At(along), t = river.Tangent(along);
                Vector2 side = new Vector2(t.y, -t.x);
                float w = RiverHalfWidth(along, out float fordK) + 10f;
                byte deep = (byte)Mathf.Lerp(255f, 70f, fordK);
                b.wind = 0; b.Vert(new Vector3(c.x - side.x * w, Water, c.y - side.y * w), Vector3.up);
                b.wind = deep; b.Vert(new Vector3(c.x, Water, c.y), Vector3.up);
                b.wind = 0; b.Vert(new Vector3(c.x + side.x * w, Water, c.y + side.y * w), Vector3.up);
            }
            for (int i = 0; i < n; i++)
            {
                int k = start + i * 3;
                Vector3 a = b.verts[k], c2 = b.verts[k + 3], d = b.verts[k + 1];
                bool flip = Vector3.Cross(c2 - a, d - a).y < 0f;
                for (int j = 0; j < 2; j++)
                {
                    int q = k + j;
                    if (flip) { b.Tri(q, q + 1, q + 4); b.Tri(q, q + 4, q + 3); }
                    else { b.Tri(q, q + 4, q + 1); b.Tri(q, q + 3, q + 4); }
                }
            }
            // pond
            int centre = b.verts.Count;
            b.wind = 255; b.Vert(new Vector3(Pond.x, Water, Pond.y), Vector3.up);
            for (int i = 0; i <= 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24f;
                b.wind = 0; b.Vert(new Vector3(Pond.x + Mathf.Cos(a) * 52f, Water, Pond.y + Mathf.Sin(a) * 52f), Vector3.up);
            }
            for (int i = 0; i < 24; i++) b.Tri(centre, centre + 2 + i, centre + 1 + i);
            GameObject go = b.ToObject("Water", r.root.transform, Mats.Water, false);
            go.layer = Layers.Water;
        }

        // ================================================================== vegetation

        static Mesh TreeMesh(int seed, float trunkH, float crown, Color32 bark, Color32 leaf, Color32 leafDark)
        {
            var rng = new System.Random(seed);
            var b = new MeshBuilder();
            b.wind = 0;
            b.Col(bark);
            b.Cyl(new Vector3(0, -0.3f, 0), new Vector3(0.15f, trunkH, 0.1f), 0.3f, 0.16f, 7, false, false);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f + (float)rng.NextDouble();
                Vector3 from = new Vector3(0.1f, trunkH * (0.6f + 0.12f * i), 0.05f);
                Vector3 to = from + new Vector3(Mathf.Cos(a) * crown * 0.6f, crown * 0.45f, Mathf.Sin(a) * crown * 0.6f);
                b.Cyl(from, to, 0.11f, 0.05f, 5, false, false);
            }
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.3f + (float)rng.NextDouble() * 0.8f, rr = i == 0 ? 0f : crown * 0.5f;
                Vector3 c = new Vector3(Mathf.Cos(a) * rr + 0.1f, trunkH + crown * (0.3f + 0.25f * (float)rng.NextDouble()), Mathf.Sin(a) * rr);
                float s = crown * (i == 0 ? 0.85f : 0.55f + 0.2f * (float)rng.NextDouble());
                b.wind = 150;
                b.Col(Pal.Lerp(leafDark, leaf, (float)rng.NextDouble()));
                b.Ellipsoid(c, new Vector3(s, s * 0.72f, s), 7, 5);
            }
            return b.ToMesh("Tree" + seed);
        }

        static Mesh DeadTreeMesh()
        {
            var b = new MeshBuilder { wind = 0 };
            b.Col(Pal.WoodGrey);
            b.Cyl(new Vector3(0, -0.3f, 0), new Vector3(0.2f, 4.6f, 0), 0.26f, 0.09f, 6, false, false);
            b.Cyl(new Vector3(0.1f, 2.4f, 0), new Vector3(1.6f, 3.9f, 0.4f), 0.09f, 0.03f, 5, false, false);
            b.Cyl(new Vector3(0.1f, 3.0f, 0), new Vector3(-1.2f, 4.4f, -0.6f), 0.08f, 0.03f, 5, false, false);
            b.Cyl(new Vector3(0.15f, 3.6f, 0), new Vector3(0.5f, 5.2f, 0.9f), 0.06f, 0.02f, 5, false, false);
            return b.ToMesh("DeadTree");
        }

        static Mesh BushMesh(Color32 col)
        {
            var b = new MeshBuilder { wind = 70 };
            b.Col(col);
            b.Ellipsoid(new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.42f, 0.55f), 7, 4);
            b.Col(Pal.Shade(col, 0.85f));
            b.Ellipsoid(new Vector3(0.35f, 0.24f, 0.2f), new Vector3(0.4f, 0.3f, 0.4f), 6, 4);
            b.Ellipsoid(new Vector3(-0.3f, 0.2f, -0.25f), new Vector3(0.38f, 0.26f, 0.36f), 6, 4);
            return b.ToMesh("Bush");
        }

        static Mesh RockMesh(int seed, Color32 col)
        {
            var b = new MeshBuilder { wind = 0 };
            b.Col(col);
            b.Ellipsoid(Vector3.zero, new Vector3(1f, 0.7f, 0.85f), 8, 5);
            for (int i = 0; i < b.verts.Count; i++)
            {
                Vector3 v = b.verts[i];
                float n = Mathf.PerlinNoise(v.x * 1.7f + seed * 5.1f, v.z * 1.7f + v.y * 1.3f);
                b.verts[i] = v * (0.75f + n * 0.5f);
            }
            b.SmoothNormals(0, b.verts.Count, 0, b.tris.Count);
            return b.ToMesh("Rock" + seed);
        }

        static void BuildVegetation(Region r)
        {
            scatter = new Scatter { grassDensity = GrassAt, groundHeight = HeightAt };
            if (Args.IsTest && !Args.RealRender) scatter.grassCellsPerFrame = int.MaxValue;       // a screenshot must not catch a meadow half laid
            var rng = new System.Random(77);
            // trees, bushes and boulders modelled in Blender (Tools/blender/flora.py); the code-built ones stand in if a build lacks them
            int Proto(string model, System.Func<Mesh> code, Material codeMat, bool sways, float view, bool shadows)
            {
                return Models.Has(model) ? scatter.AddProto(Models.Mesh(model), sways ? Mats.SurfaceUVWind : Mats.SurfaceUV, view, shadows,
                                                            Models.Has(model + "_far") ? Models.Mesh(model + "_far") : null)
                                         : scatter.AddProto(code(), codeMat, view, shadows);
            }
            int cotton1 = Proto("tree_cotton_a", () => TreeMesh(1, 5.2f, 3.0f, Pal.BarkPale, Pal.Leaf, Pal.LeafDark), Mats.Wind, true, 900f, true);
            int cotton2 = Proto("tree_cotton_b", () => TreeMesh(2, 4.0f, 2.4f, Pal.BarkPale, Pal.H(0x7C9048), Pal.LeafDark), Mats.Wind, true, 900f, true);
            int oak = Proto("tree_oak", () => TreeMesh(3, 2.8f, 3.2f, Pal.Bark, Pal.H(0x5A7038), Pal.H(0x3F5528)), Mats.Wind, true, 900f, true);
            int dead = Proto("tree_dead", DeadTreeMesh, Mats.Lit, false, 600f, true);
            int sage = Proto("bush_sage", () => BushMesh(Pal.Sage), Mats.Wind, true, 260f, false);
            int scrub = Proto("bush_scrub", () => BushMesh(Pal.H(0x7A8450)), Mats.Wind, true, 260f, false);
            int rock1 = Proto("rock_a", () => RockMesh(1, Pal.Rock), Mats.Lit, false, 500f, true);
            int rock2 = Proto("rock_b", () => RockMesh(2, Pal.RockRed), Mats.Lit, false, 500f, true);
            // tufts drawn by make_grass.py: bunch grass, turf, seed heads, a few flowers and weeds
            for (int i = 0; i < GrassTable.Names.Length; i++)
                scatter.AddGrass(Scatter.GrassCard(GrassTable.Rects[i], GrassTable.Sizes[i], "Grass_" + GrassTable.Names[i]), GrassTable.Shares[i]);

            var trunks = new GameObject("Trunks") { layer = Layers.Static };
            trunks.transform.SetParent(r.root.transform, false);
            void Tree(int proto, float x, float z, float scale, float trunkR) { PlantTree(trunks, rng, proto, x, z, scale, trunkR); }

            // cottonwoods along the river and round the pond
            for (float along = 0f; along < river.length; along += 7f)
            {
                if (Mathf.Abs(along - fordAlong) < 70f) continue;
                Vector2 c = river.At(along), t = river.Tangent(along);
                Vector2 side = new Vector2(t.y, -t.x);
                float halfW = RiverHalfWidth(along, out _);
                for (int s = -1; s <= 1; s += 2)
                {
                    if (rng.NextDouble() > 0.55 * Mathf.PerlinNoise(along / 60f, s * 3.7f + 5f) * 2f) continue;
                    float off = halfW + 12f + (float)rng.NextDouble() * (float)rng.NextDouble() * 70f;
                    Tree(rng.NextDouble() < 0.6 ? cotton1 : cotton2, c.x + side.x * off * s, c.y + side.y * off * s, 0.85f + (float)rng.NextDouble() * 0.6f, 0.3f);
                }
            }
            for (int i = 0; i < 16; i++)
            {
                float a = i * 0.41f + (float)rng.NextDouble(), rr = 56f + (float)rng.NextDouble() * 26f;
                Tree(i % 3 == 0 ? cotton2 : cotton1, Pond.x + Mathf.Cos(a * 2.6f) * rr, Pond.y + Mathf.Sin(a * 2.6f) * rr, 0.9f + (float)rng.NextDouble() * 0.5f, 0.3f);
            }
            // lone trees on the prairie, oaks in the northern hills
            for (int i = 0; i < 520; i++)
            {
                float x = (float)rng.NextDouble() * Size, z = (float)rng.NextDouble() * Size;
                float clump = U.Fbm(x / 180f + 50f, z / 180f + 9f, 3);
                bool north = z > 1420f;
                if (clump < (north ? 0.5f : 0.63f)) continue;
                if (HeightAt(x, z) > 70f) continue;
                int proto = north ? oak : rng.NextDouble() < 0.15 ? dead : rng.NextDouble() < 0.5 ? oak : cotton2;
                Tree(proto, x, z, 0.8f + (float)rng.NextDouble() * 0.7f, proto == dead ? 0.22f : 0.3f);
            }
            // sage, scrub and (where flora.py has it) rabbitbrush
            var dice = new System.Random(78);
            int rabbit = Models.Has("bush_rabbit") ? scatter.AddProto(Models.Mesh("bush_rabbit"), Mats.SurfaceUVWind, 240f, false) : -1;
            for (int i = 0; i < 9000; i++)
            {
                float x = 20f + (float)rng.NextDouble() * (Size - 40f), z = 20f + (float)rng.NextDouble() * (Size - 40f);
                if (GrassAt(x, z) < 0.35f || Blocked(x, z, 3f)) continue;
                float clump = U.Fbm(x / 90f + 31f, z / 90f + 77f, 3);
                if (clump < 0.42f) continue;
                float s = 0.6f + (float)rng.NextDouble() * 0.9f;
                int bush = rng.NextDouble() < 0.7 ? sage : scrub;
                if (rabbit >= 0 && dice.NextDouble() < 0.28) bush = rabbit;
                scatter.Add(bush, new Vector3(x, HeightAt(x, z) - 0.03f, z), (float)rng.NextDouble() * 360f, new Vector3(s, s * (0.8f + (float)rng.NextDouble() * 0.5f), s));
            }
            // rocks: under the mesas, on the banks, a few on the prairie
            var rocks = new GameObject("Rocks") { layer = Layers.Static };
            rocks.transform.SetParent(r.root.transform, false);
            for (int i = 0; i < 2600; i++)
            {
                float x = 12f + (float)rng.NextDouble() * (Size - 24f), z = 12f + (float)rng.NextDouble() * (Size - 24f);
                if (Blocked(x, z, 4f)) continue;
                float y = HeightAt(x, z);
                Vector3 nrm = terrain.terrainData.GetInterpolatedNormal(x / Size, z / Size);
                float steep = 1f - nrm.y;
                bool bank = river.Distance(x, z, 60f) < 55f && y < Water + 2.5f && y > Water - 0.2f;
                if (!(steep > 0.06f || bank && rng.NextDouble() < 0.5 || rng.NextDouble() < 0.02)) continue;
                float s = 0.3f + (float)rng.NextDouble() * (float)rng.NextDouble() * (steep > 0.06f ? 2.6f : 1.1f);
                scatter.Add(y > 60f || steep > 0.12f ? rock2 : rock1, new Vector3(x, y - 0.15f * s, z), (float)rng.NextDouble() * 360f, new Vector3(s, s * (0.6f + (float)rng.NextDouble() * 0.5f), s * (0.8f + (float)rng.NextDouble() * 0.4f)));
                if (s > 0.9f)
                {
                    var c = new GameObject("R") { layer = Layers.Static };
                    c.transform.SetParent(rocks.transform, false);
                    c.transform.position = new Vector3(x, y + 0.1f * s, z);
                    c.AddComponent<SphereCollider>().radius = 0.7f * s;
                }
            }
            // talus: blocks fallen from the cliffs lie in the apron below them
            for (int i = 0; i < 14000; i++)
            {
                float x = 12f + (float)rng.NextDouble() * (Size - 24f), z = 12f + (float)rng.NextDouble() * (Size - 24f);
                if (Blocked(x, z, 4f)) continue;
                Vector3 nrm = terrain.terrainData.GetInterpolatedNormal(x / Size, z / Size);
                float steep = 1f - nrm.y;
                if (steep > 0.30f) continue;
                Vector2 up = -new Vector2(nrm.x, nrm.z);
                if (up.sqrMagnitude < 1e-6f) continue;
                up.Normalize();
                bool cliff = false;
                for (float reach = 5f; reach <= 17f && !cliff; reach += 4f)
                    cliff = 1f - terrain.terrainData.GetInterpolatedNormal((x + up.x * reach) / Size, (z + up.y * reach) / Size).y > 0.42f;
                if (!cliff) continue;
                float y = HeightAt(x, z);
                if (y < Water + 0.5f) continue;
                float s = 0.35f + (float)rng.NextDouble() * (float)rng.NextDouble() * (float)rng.NextDouble() * 3.2f;
                scatter.Add(rng.NextDouble() < 0.7 ? rock2 : rock1, new Vector3(x, y - 0.2f * s, z), (float)rng.NextDouble() * 360f,
                            new Vector3(s, s * (0.55f + (float)rng.NextDouble() * 0.5f), s * (0.8f + (float)rng.NextDouble() * 0.4f)));
                if (s > 1.2f)
                {
                    var c = new GameObject("R") { layer = Layers.Static };
                    c.transform.SetParent(rocks.transform, false);
                    c.transform.position = new Vector3(x, y + 0.1f * s, z);
                    c.AddComponent<SphereCollider>().radius = 0.65f * s;
                }
            }
            if (Models.Has("plant_yucca")) DesertAndRiverside(r, trunks, Tree, dice);
            scatter.Bake();
        }

        /// <summary>A tree from the scatter, with a trunk the hero and the bullets run into.</summary>
        static void PlantTree(GameObject trunks, System.Random rng, int proto, float x, float z, float scale, float trunkR)
        {
            if (x < 20f || z < 20f || x > Size - 20f || z > Size - 20f || Blocked(x, z, 7f)) return;
            float y = HeightAt(x, z);
            if (y < Water + 0.3f) return;
            scatter.Add(proto, new Vector3(x, y, z), (float)rng.NextDouble() * 360f, Vector3.one * scale);
            var c = new GameObject("T") { layer = Layers.Static };
            c.transform.SetParent(trunks.transform, false);
            c.transform.position = new Vector3(x, y + 1.5f * scale, z);
            var cap = c.AddComponent<CapsuleCollider>();
            cap.radius = trunkR * scale; cap.height = 4.5f * scale;
        }

        static bool NearRoad(float x, float z, float within)
        {
            foreach (Polyline road in roads) if (road.Distance(x, z, within + 4f) < within) return true;
            return false;
        }

        /// <summary>
        /// Juniper and pinyon on the slopes and benches under the mesas and on the high ground; yucca, prickly pear and
        /// cholla on the dry plain; willow thickets, cattails and driftwood along the river and round the pond; water-worn
        /// stones in the shallows of the ford. Plants from Tools/blender/flora.py.
        /// </summary>
        static void DesertAndRiverside(Region r, GameObject trunks, System.Action<int, float, float, float, float> tree, System.Random dice)
        {
            int P(string model, bool sways, float view, bool shadows)
            {
                return scatter.AddProto(Models.Mesh(model), sways ? Mats.SurfaceUVWind : Mats.SurfaceUV, view, shadows,
                                        Models.Has(model + "_far") ? Models.Mesh(model + "_far") : null);
            }
            int juniper = P("tree_juniper", true, 600f, true), juniper2 = P("tree_juniper_b", true, 600f, true), pinyon = P("tree_pinyon", true, 600f, true);
            int willow = P("bush_willow", true, 420f, true), yucca = P("plant_yucca", true, 260f, false), yucca2 = P("plant_yucca_b", true, 260f, false);
            int pear = P("plant_pear", false, 200f, false), cholla = P("plant_cholla", false, 220f, false), reeds = P("plant_reeds", true, 240f, false);
            int stones = P("rock_river", false, 200f, false), drift = P("log_drift", false, 300f, true);
            float Rnd() { return (float)dice.NextDouble(); }
            var count = new int[12];
            float Steep(float x, float z) { return 1f - terrain.terrainData.GetInterpolatedNormal(x / Size, z / Size).y; }

            // the woodland of the dry slopes: juniper and pinyon, thick where the noise says so
            for (int i = 0; i < 7000; i++)
            {
                float x = 20f + Rnd() * (Size - 40f), z = 20f + Rnd() * (Size - 40f);
                float y = HeightAt(x, z);
                if (y < Water + 4f) continue;
                float steep = Steep(x, z), wood = U.Fbm(x / 140f + 13f, z / 140f + 71f, 3);
                bool slope = steep > 0.04f && steep < 0.38f, high = y > 48f;
                if (!(slope && wood > 0.52f || high && wood > 0.45f) || Rnd() < 0.45f) continue;
                int proto = Rnd() < 0.4f ? pinyon : Rnd() < 0.5f ? juniper : juniper2;
                tree(proto, x, z, 0.7f + Rnd() * 0.6f, 0.2f);
                if (count[3]++ == 60 && !Blocked(x, z, 7f)) r.Mark("flora_juniper", new Vector3(x, y, z));
            }
            // the plain: yucca where the grass is thin, prickly pear by stones and on banks, cholla in the dry south
            for (int i = 0; i < 9000; i++)
            {
                float x = 20f + Rnd() * (Size - 40f), z = 20f + Rnd() * (Size - 40f);
                if (Blocked(x, z, 2.5f)) continue;
                float y = HeightAt(x, z);
                if (y < Water + 1.2f) continue;
                // they grow where the grass is thin and the ground shows, not hidden in the meadows
                float n = U.Fbm(x / 70f + 5f, z / 70f + 91f, 3), steep = Steep(x, z), roll = Rnd(), g = GrassAt(x, z);
                Vector3 at = new Vector3(x, y - 0.03f, z);
                float yaw = Rnd() * 360f;
                if (g < 0.5f && steep < 0.3f && Rnd() < 0.035f)
                {
                    float s = 0.9f + Rnd() * 0.5f;
                    scatter.Add(cholla, at, yaw, new Vector3(s, s, s));
                    if (count[2]++ == 5) r.Mark("flora_cholla", at);
                }
                else if (steep < 0.3f && g < 0.5f && n > 0.42f && roll < 0.55f)
                {
                    float s = 1.05f + Rnd() * 0.5f;
                    scatter.Add(Rnd() < 0.6f ? yucca : yucca2, at, yaw, new Vector3(s, s * (0.85f + Rnd() * 0.3f), s));
                    if (count[0]++ == 40) r.Mark("flora_yucca", at);
                }
                else if (g < 0.45f && (steep > 0.04f || n < 0.42f) && roll < 0.4f)
                {
                    float s = 0.8f + Rnd() * 0.6f;
                    scatter.Add(pear, at, yaw, new Vector3(s, s * (0.8f + Rnd() * 0.4f), s));
                    if (count[1]++ == 20) r.Mark("flora_pear", at);
                }
            }
            // the river: cattails at the water's edge, willows on the bank, now and then a stranded log
            for (float along = 0f; along < river.length; along += 2.5f)
            {
                Vector2 c = river.At(along), t = river.Tangent(along), side = new Vector2(t.y, -t.x);
                float halfW = RiverHalfWidth(along, out float fordK);
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    if (fordK < 0.5f && Rnd() < 1.2f * Mathf.PerlinNoise(along / 40f, sg * 2.1f + 7f))
                    {
                        Vector2 p = c + side * ((halfW + Rnd() * 2.5f - 1.5f) * sg);
                        float y = HeightAt(p.x, p.y);
                        if (y > Water - 0.5f && y < Water + 0.3f)
                        {
                            for (int k = 0; k < 3; k++)
                            {
                                Vector2 q = p + new Vector2(Rnd() - 0.5f, Rnd() - 0.5f) * 2.2f;
                                float yq = HeightAt(q.x, q.y);
                                if (yq > Water + 0.35f) continue;
                                float s = 0.85f + Rnd() * 0.45f;
                                scatter.Add(reeds, new Vector3(q.x, yq - 0.05f, q.y), Rnd() * 360f, new Vector3(s, s * (0.8f + Rnd() * 0.4f), s));
                            }
                            if (count[5]++ == 60) r.Mark("flora_reeds", new Vector3(p.x, y, p.y));
                        }
                    }
                    if (Rnd() < 0.28f * Mathf.PerlinNoise(along / 60f, sg * 5.3f + 1f))
                    {
                        Vector2 p = c + side * ((halfW + 2f + Rnd() * 9f) * sg);
                        float y = HeightAt(p.x, p.y);
                        if (y > Water + 0.1f && y < Water + 3f && !NearRoad(p.x, p.y, 5f))
                        {
                            int n = 2 + (int)(Rnd() * 3f);
                            if (count[4]++ == 30) r.Mark("flora_willow", new Vector3(p.x, y, p.y));
                            for (int k = 0; k < n; k++)
                            {
                                Vector2 q = p + new Vector2(Rnd() - 0.5f, Rnd() - 0.5f) * 4.5f;
                                float yq = HeightAt(q.x, q.y);
                                if (yq < Water + 0.05f) continue;
                                float s = 0.75f + Rnd() * 0.55f;
                                scatter.Add(willow, new Vector3(q.x, yq - 0.05f, q.y), Rnd() * 360f, new Vector3(s, s * (0.85f + Rnd() * 0.3f), s));
                            }
                        }
                    }
                    if (Rnd() < 0.012f)
                    {
                        Vector2 p = c + side * ((halfW + 1f + Rnd() * 4f) * sg);
                        float y = HeightAt(p.x, p.y);
                        if (y > Water - 0.1f && y < Water + 1.5f)
                        {
                            float s = 0.8f + Rnd() * 0.5f;
                            scatter.Add(drift, new Vector3(p.x, y + 0.02f, p.y), Mathf.Atan2(t.x, t.y) * Mathf.Rad2Deg + 90f + (Rnd() - 0.5f) * 60f, new Vector3(s, s, s));
                        }
                    }
                }
            }
            // stones in the shallows of the ford and on its bars - off the road that crosses it
            Vector2 fc = river.At(fordAlong), ft = river.Tangent(fordAlong), fs = new Vector2(ft.y, -ft.x);
            for (int i = 0; i < 320; i++)
            {
                Vector2 p = fc + ft * ((Rnd() * 2f - 1f) * 48f) + fs * ((Rnd() * 2f - 1f) * 44f);
                float y = HeightAt(p.x, p.y);
                if (y > Water + 0.7f) continue;
                if (NearRoad(p.x, p.y, 4.5f)) continue;
                float s = 0.6f + Rnd() * Rnd() * 1.2f;
                scatter.Add(stones, new Vector3(p.x, y - 0.06f, p.y), Rnd() * 360f, new Vector3(s, s, s));
                if (count[6]++ == 10) r.Mark("flora_stones", new Vector3(p.x, y, p.y));
            }
            Debug.Log("Desert and riverside: " + count[0] + " yucca, " + count[1] + " prickly pear, " + count[2] + " cholla, " + count[3] + " juniper and pinyon");
            // round the pond
            for (int i = 0; i < 120; i++)
            {
                float a = i / 120f * Mathf.PI * 2f + Rnd() * 0.05f, rr = 49f + Rnd() * 6f;
                Vector2 p = Pond + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                float y = HeightAt(p.x, p.y);
                if (y > Water - 0.5f && y < Water + 0.35f && Rnd() < 0.75f)
                {
                    float s = 0.8f + Rnd() * 0.4f;
                    scatter.Add(reeds, new Vector3(p.x, y - 0.05f, p.y), Rnd() * 360f, new Vector3(s, s * (0.8f + Rnd() * 0.4f), s));
                }
            }
            for (int i = 0; i < 16; i++)
            {
                float a = i * 0.39f + Rnd() * 0.3f, rr = 55f + Rnd() * 9f;
                Vector2 p = Pond + new Vector2(Mathf.Cos(a * 2.6f), Mathf.Sin(a * 2.6f)) * rr;
                float y = HeightAt(p.x, p.y);
                if (y < Water + 0.1f || NearRoad(p.x, p.y, 5f)) continue;
                float s = 0.8f + Rnd() * 0.5f;
                scatter.Add(willow, new Vector3(p.x, y - 0.05f, p.y), Rnd() * 360f, new Vector3(s, s, s));
            }
        }

        /// <summary>True where nothing should grow: roads, rails, water, yards and building plots.</summary>
        static bool Blocked(float x, float z, float margin)
        {
            foreach (Polyline road in roads) if (road.Distance(x, z, 6f + margin) < 6f + margin) return true;
            if (rail.Distance(x, z, 5f + margin) < 5f + margin) return true;
            if (Mathf.Abs(x - (Town.x + 5f)) < 150f + margin && Mathf.Abs(z - (Town.y - 4f)) < 62f + margin) return true;
            if (Vector2.Distance(new Vector2(x, z), CampC) < 40f + margin) return true;
            if (Vector2.Distance(new Vector2(x, z), Ranch) < 62f + margin) return true;
            if (Vector2.Distance(new Vector2(x, z), Olson) < 22f + margin || Vector2.Distance(new Vector2(x, z), MillFarm) < 22f + margin) return true;
            if (Vector2.Distance(new Vector2(x, z), Cemetery) < 16f + margin || Vector2.Distance(new Vector2(x, z), OldStation) < 16f + margin) return true;
            if (Vector2.Distance(new Vector2(x, z), StockPens) < 21f + margin) return true;
            return false;
        }

        // ================================================================== navigation and map

        static void BuildNav(Region r)
        {
            NavMesh.RemoveAllNavMeshData();
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.36f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = 0.42f;
            settings.agentSlope = 42f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.2f;
            settings.overrideTileSize = true;
            settings.tileSize = 128;
            (Vector3 c, Vector3 size)[] zones =
            {
                (OnLand(Town.x + 5f, Town.y - 6f), new Vector3(330f, 30f, 170f)),
                (OnLand(CampC), new Vector3(120f, 24f, 120f)),
                (OnLand(Ranch), new Vector3(170f, 24f, 150f)),
            };
            foreach (var zone in zones)
            {
                var bounds = new Bounds(zone.c, zone.size);
                var sources = new List<NavMeshBuildSource>();
                NavMeshBuilder.CollectSources(bounds, Layers.SolidMask, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
                NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(Vector3.zero, zone.size), zone.c, Quaternion.identity);
                if (data != null) NavMesh.AddNavMeshData(data);
            }
        }

        static MapData BuildMap()
        {
            const int n = 1024;
            var tex = new Texture2D(n, n, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            Color paper = new Color(0.80f, 0.73f, 0.58f), ink = new Color(0.25f, 0.19f, 0.14f), wet = new Color(0.45f, 0.58f, 0.60f);
            Color roadCol = new Color(0.52f, 0.40f, 0.28f), high = new Color(0.66f, 0.50f, 0.38f), green = new Color(0.66f, 0.68f, 0.46f);
            float s = Size / n;
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                {
                    float x = (ix + 0.5f) * s, z = (iz + 0.5f) * s;
                    int hx = Mathf.Clamp(Mathf.RoundToInt(x / Size * (Res - 1)), 1, Res - 2), hz = Mathf.Clamp(Mathf.RoundToInt(z / Size * (Res - 1)), 1, Res - 2);
                    float h = heights[hz, hx] * MaxH;
                    float shade = ((heights[hz, hx - 1] - heights[hz, hx + 1]) + (heights[hz + 1, hx] - heights[hz - 1, hx])) * MaxH * 0.16f;
                    Color c = Color.Lerp(paper, green, GrassAt(x, z) * 0.55f);
                    c = Color.Lerp(c, high, Mathf.InverseLerp(52f, 95f, h));
                    c *= 1f + Mathf.Clamp(shade, -0.3f, 0.3f);
                    float contour = Mathf.Abs(Mathf.Repeat(h, 10f) - 5f);
                    if (contour > 4.6f && h > 45f) c = Color.Lerp(c, ink, 0.18f);
                    if (h < Water - 0.05f) c = Color.Lerp(wet, wet * 0.8f, Mathf.InverseLerp(Water, Water - 3f, h));
                    foreach (Polyline road in roads) if (road.Distance(x, z, 4f) < 3.2f) c = Color.Lerp(c, roadCol, 0.85f);
                    float rl = rail.Distance(x, z, 4f, out float ra);
                    if (rl < 2.4f) c = Mathf.Repeat(ra, 14f) < 7f ? ink : paper;
                    px[iz * n + ix] = c;
                }
            foreach (Rect b in mapBuildings)
                for (int iz = Mathf.Max(0, (int)(b.yMin / s)); iz <= Mathf.Min(n - 1, (int)(b.yMax / s)); iz++)
                    for (int ix = Mathf.Max(0, (int)(b.xMin / s)); ix <= Mathf.Min(n - 1, (int)(b.xMax / s)); ix++)
                        px[iz * n + ix] = ink;
            tex.SetPixels(px);
            tex.Apply(true, true);
            return new MapData { tex = tex, origin = Vector2.zero, size = Size };
        }

        static readonly List<Rect> mapBuildings = new List<Rect>();
        static readonly List<Rect> noGrass = new List<Rect>();
    }
}
