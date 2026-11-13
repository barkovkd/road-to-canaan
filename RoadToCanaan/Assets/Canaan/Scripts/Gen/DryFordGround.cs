using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>The ground of the town and of the roads near it. The small things on it cast no shadow (Site.fine): thousands
    /// of pebbles and straws in four shadow cascades cost frames and their shadows would not show.</summary>
    public static partial class RegionDryFord
    {
        static bool InBuilding(float x, float z)
        {
            foreach (Rect b in mapBuildings) if (b.Contains(new Vector2(x, z))) return true;
            return false;
        }

        /// <summary>
        /// What a town does to the ground: wheel ruts worn in pairs down the main street and out along the roads, hoof
        /// prints thickest down the middle, ground trodden dark before the rails and the doors, mud round the troughs and
        /// the well, sand blown against the porches and into the alleys (GroundMarks); and on it pebbles in the gutters,
        /// weeds along the walls, fences and posts, straw by the stable, bottles and cans behind the saloon.
        /// </summary>
        static void BuildGround(Region r, Site s, List<(BuildingSpec b, float x0, float x1, bool south)> lots, Dictionary<string, float> xs, float northZ, float southZ)
        {
            var rnd = new System.Random(77);
            float R() { return (float)rnd.NextDouble(); }
            float G() { return (R() + R() + R() - 1.5f) / 1.5f; }              // -1..1, most near 0

            // ---- wheel ruts
            void Ruts(Polyline road, float from, float to, float lateral, float strength, int seed)
            {
                foreach (float e in new[] { -1f, 1f })
                {
                    var line = new List<Vector2>();
                    for (float a = from; a <= to; a += 1.2f)
                    {
                        Vector2 p = road.At(a), t = road.Tangent(a);
                        Vector2 n = new Vector2(-t.y, t.x);
                        float wander = Mathf.Sin(a * 0.11f + seed) * 0.3f + Mathf.Sin(a * 0.037f + seed * 2.1f) * 0.45f;
                        line.Add(p + n * (lateral + wander + e * 0.78f));
                    }
                    GroundMarks.Strip(line, 0.25f + R() * 0.07f, GroundMarks.Rut, strength);
                }
            }
            mainRoad.Distance(680f, 997f, 60f, out float m0);
            mainRoad.Distance(1110f, 1005f, 60f, out float m1);
            float[] lanes = { -2.6f, 0.3f, 2.9f }, worn = { 0.6f, 1f, 0.7f };
            for (int i = 0; i < lanes.Length; i++) Ruts(mainRoad, m0, m1, lanes[i], worn[i], i * 3 + 1);
            Ruts(northRoad, 0f, Mathf.Min(160f, northRoad.length), 0f, 0.9f, 11);
            Ruts(northRoad, 0f, Mathf.Min(60f, northRoad.length), 1.8f, 0.45f, 12);
            Ruts(ranchRoad, 0f, Mathf.Min(130f, ranchRoad.length), 0f, 0.85f, 13);
            var depot = new Polyline(new Vector2(781f, 1000f), new Vector2(776f, 988f), new Vector2(772f, 974f), new Vector2(770f, 963f)).Smoothed(2);
            Ruts(depot, 0f, depot.length, 0f, 0.8f, 21);

            // ---- hoof prints: thick down the middle of the street and up the side street
            for (int i = 0; i < 900; i++)
            {
                float x = 765f + R() * 235f, z = Town.y + G() * 4.6f;
                GroundMarks.Patch(new Vector2(x, z), 0.065f, 0.075f, (R() < 0.5f ? 90f : 270f) + G() * 25f, GroundMarks.Hoof, 0.45f + R() * 0.55f);
            }
            for (int i = 0; i < 160; i++)
                GroundMarks.Patch(new Vector2(CrossX + G() * 3f, Town.y + 8f + R() * 30f), 0.065f, 0.075f, (R() < 0.5f ? 0f : 180f) + G() * 25f, GroundMarks.Hoof, 0.4f + R() * 0.5f);

            // ---- trodden ground before the rails, the doors and the stable; mud round the water
            float nRail = northZ - 3.6f, sRail = southZ + 3.6f;
            (float x, float z)[] rails = { (xs["saloon"] - 3.6f, nRail - 1.1f), (xs["saloon"] + 3.6f, nRail - 1.1f), (xs["store"] + 2.5f, nRail - 1.1f), (xs["hotel"] - 2.8f, nRail - 1.1f),
                                           (xs["gunsmith"] + 1.5f, nRail - 1.1f), (xs["bank"] + 2.6f, nRail - 1.1f), (xs["sheriff"] + 2.8f, sRail + 1.1f), (xs["stable"] - 8f, southZ + 2.6f),
                                           (xs["cantina"] - 3f, southZ + 5.0f), (xs["feed"] + 2.2f, sRail + 1.1f) };
            foreach (var (x, z) in rails) GroundMarks.Patch(new Vector2(x, z), 2.3f, 1.4f, G() * 6f, GroundMarks.Trodden, 0.95f);
            foreach (var (b, x0, x1, south) in lots)
            {
                bool porch = b.porch && b.kind != BuildKind.Stable && b.kind != BuildKind.Smithy && b.kind != BuildKind.Butcher;
                float face = south ? southZ + (porch ? Architect.PorchDepth : 0f) : northZ - (porch ? Architect.PorchDepth : 0f), out1 = south ? 1f : -1f;
                float mid = (x0 + x1) * 0.5f;
                if (b.kind == BuildKind.Stable) GroundMarks.Patch(new Vector2(mid, face + out1 * 2.6f), 3.2f, 2.4f, 0f, GroundMarks.Trodden, 1f);
                else GroundMarks.Patch(new Vector2(mid, face + out1 * 1.1f), 1.5f, 0.9f, G() * 8f, GroundMarks.Trodden, 0.7f + R() * 0.3f);
                // sand along the foot of the porch, ripples of it
                int drifts = 1 + (int)(R() * 3f);
                for (int k = 0; k < drifts; k++)
                    GroundMarks.Patch(new Vector2(Mathf.Lerp(x0 + 0.6f, x1 - 0.6f, R()), face + out1 * (0.4f + R() * 0.25f)), 0.9f + R() * 1.4f, 0.32f + R() * 0.3f, G() * 4f, GroundMarks.Sand, 0.6f + R() * 0.4f);
            }
            // sand blown into the mouths of the alleys
            for (int i = 0; i + 1 < lots.Count; i++)
            {
                var a = lots[i]; var b = lots[i + 1];
                float gap = b.x0 - a.x1;
                if (a.south != b.south || gap < 0.7f || gap > 4f) continue;
                float z = a.south ? southZ - 1.5f : northZ + 1.5f;
                GroundMarks.Patch(new Vector2((a.x1 + b.x0) * 0.5f, z), gap * 0.55f + 0.2f, 2.6f, 0f, GroundMarks.Sand, 0.95f);
            }
            (float x, float z, float r)[] wet = { (xs["saloon"] - 6.6f, northZ - 3.7f, 1.5f), (xs["stable"] + 8.2f, southZ + 1.2f, 1.5f), (xs["cantina"] + 2.4f, southZ + 4.0f, 1.4f),
                                                  (CrossX, Town.y + 0.5f, 2.4f) };
            foreach (var (x, z, rad) in wet) GroundMarks.Patch(new Vector2(x, z), rad, rad * 0.75f, G() * 20f, GroundMarks.Mud, 0.9f);
            GroundMarks.Patch(new Vector2(CrossX, Town.y + 0.5f), 4.2f, 4.2f, 0f, GroundMarks.Trodden, 0.8f);

            // ---- pebbles: in the gutters along the porches, scattered over the street, round the posts
            Color32[] stones = { Pal.Rock, Pal.Shade(Pal.Rock, 0.8f), Pal.RockRed, Pal.Shade(Pal.Stone, 0.9f), Pal.Shade(Pal.RockRed, 0.75f), Pal.Sand };
            void Pebbles(float cx, float cz, float radius, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    float a = R() * Mathf.PI * 2f, d = Mathf.Sqrt(R()) * radius;
                    float x = cx + Mathf.Cos(a) * d, z = cz + Mathf.Sin(a) * d;
                    if (InBuilding(x, z)) continue;
                    float size = 0.018f + R() * R() * 0.075f;
                    s.fine.Col(Pal.Shade(stones[rnd.Next(stones.Length)], 0.85f + R() * 0.3f)).Surface(Surf.Rock);
                    s.fine.Ellipsoid(new Vector3(x, HeightAt(x, z) + size * 0.2f, z), new Vector3(size * (0.8f + R() * 0.5f), size * (0.4f + R() * 0.3f), size * (0.8f + R() * 0.5f)), 6, 4);
                }
            }
            for (float x = 770f; x < 990f; x += 3.5f + R() * 4f)
            {
                Pebbles(x, northZ - Architect.PorchDepth - 0.5f - R() * 0.6f, 0.7f, 6 + (int)(R() * 8f));
                Pebbles(x + 1.7f, southZ + Architect.PorchDepth + 0.5f + R() * 0.6f, 0.7f, 6 + (int)(R() * 8f));
            }
            for (int i = 0; i < 70; i++) Pebbles(765f + R() * 230f, Town.y + G() * 7f, 0.4f, 2 + (int)(R() * 4f));
            for (int i = 0; i < 26; i++) Pebbles(CrossX + G() * 6f, Town.y + 9f + R() * 28f, 0.5f, 3 + (int)(R() * 5f));

            // ---- weeds: along the walls in the alleys and behind, by the fences and posts, where no one treads (their own
            // site, without shadows: a hundred bushes in the street's one mesh cost frames in every shadow cascade)
            var wild = new Site { castShadows = false };
            string[] weeds = { "bush_sage", "bush_rabbit", "bush_scrub" };
            void Weed(float x, float z, float scale)
            {
                if (InBuilding(x, z)) return;
                wild.Model(weeds[rnd.Next(weeds.Length)], new Vector3(x, HeightAt(x, z) - 0.03f, z), R() * 360f, Vector3.one * scale);
            }
            foreach (var (b, x0, x1, south) in lots)
            {
                float back = south ? southZ - b.d - 0.35f : northZ + b.d + 0.35f, side = south ? -1f : 1f;
                for (int k = 0; k < 2 + (int)(R() * 3f); k++) Weed(Mathf.Lerp(x0, x1, R()), back + side * R() * 0.6f, 0.25f + R() * 0.3f);
                foreach (float x in new[] { x0 - 0.3f, x1 + 0.3f })
                    if (R() < 0.55f) Weed(x, (south ? southZ : northZ) + side * (1.5f + R() * (b.d - 2f)), 0.22f + R() * 0.25f);
            }
            for (int i = 0; i < 16; i++) Weed(x: (R() < 0.5f ? 752f + R() * 14f : 990f + R() * 20f), z: Town.y + G() * 9f, scale: 0.3f + R() * 0.35f);
            for (float x = 786f; x < 965f; x += 29f) if (R() < 0.7f) Weed(Mathf.Abs(x - CrossX) < 8f ? x + 9.4f : x + 0.4f, southZ + 5.3f, 0.2f + R() * 0.2f);
            wild.Finish(r, "TownWeeds", Vector3.zero, 0f);

            // ---- straw blown about the stable, the corral and the feed store
            s.fine.Col(Pal.Hay).Surface(Surf.Straw);
            (float x, float z, float r, int n)[] straw = { (xs["stable"], southZ + 2.5f, 4.5f, 260), (xs["stable"], southZ - 22f, 9f, 240), (xs["feed"], southZ + 2.2f, 3f, 120) };
            foreach (var (cx, cz, rad, n) in straw)
                for (int i = 0; i < n; i++)
                {
                    float a = R() * Mathf.PI * 2f, d = Mathf.Sqrt(R()) * rad;
                    float x = cx + Mathf.Cos(a) * d, z = cz + Mathf.Sin(a) * d;
                    if (InBuilding(x, z)) continue;
                    float y = HeightAt(x, z) + 0.006f, yaw = R() * 360f, len = 0.08f + R() * 0.12f;
                    s.fine.Box(new Vector3(x, y, z), new Vector3(0.006f, 0.005f, len), Quaternion.Euler(G() * 8f, yaw, 0f));
                }

            // ---- rubbish: bottles and cans behind the saloon and in the alleys, an old wheel against a wall
            void Bottle(float x, float z, bool standing)
            {
                Vector3 p = new Vector3(x, HeightAt(x, z), z);
                s.fine.Col(R() < 0.5f ? Pal.H(0x3E4A2C) : Pal.H(0x5A3A1E), 0.8f);
                var shape = new List<Vector2> { new Vector2(0.034f, 0f), new Vector2(0.036f, 0.16f), new Vector2(0.014f, 0.22f), new Vector2(0.013f, 0.27f) };
                if (standing) s.fine.Lathe(p, shape, 8);
                else s.fine.Cyl(p + new Vector3(0f, 0.034f, 0f), p + Quaternion.Euler(0f, R() * 360f, 0f) * new Vector3(0.27f, 0.03f, 0f), 0.034f, 0.014f, 8);
            }
            void Can(float x, float z)
            {
                Vector3 p = new Vector3(x, HeightAt(x, z), z);
                s.fine.Col(Pal.Shade(Pal.Tin, 0.7f + R() * 0.2f), 0.3f).Surface(Surf.Tin);
                if (R() < 0.5f) s.fine.Cyl(p, p + Vector3.up * 0.11f, 0.038f, 0.038f, 8);
                else s.fine.Cyl(p + Vector3.up * 0.038f, p + Quaternion.Euler(0f, R() * 360f, 0f) * new Vector3(0.11f, 0.038f, 0f), 0.038f, 0.038f, 8);
            }
            float sx = xs["saloon"], bz = northZ + 13f;
            for (int i = 0; i < 9; i++) Bottle(sx - 5f + R() * 10f, bz + 0.8f + R() * 3f, R() < 0.35f);
            for (int i = 0; i < 6; i++) Can(sx - 5f + R() * 10f, bz + 0.6f + R() * 3.5f);
            for (int i = 0; i + 1 < lots.Count; i++)
            {
                var a = lots[i]; var b = lots[i + 1];
                float gap = b.x0 - a.x1;
                if (a.south != b.south || gap < 0.9f || gap > 3f || R() < 0.4f) continue;
                float z = a.south ? southZ - 2.5f - R() * 3f : northZ + 2.5f + R() * 3f;
                if (R() < 0.5f) Can((a.x1 + b.x0) * 0.5f + G() * 0.3f, z); else Bottle((a.x1 + b.x0) * 0.5f + G() * 0.3f, z, false);
                if (R() < 0.5f)
                {
                    // an old board lying in the alley
                    float x = (a.x1 + b.x0) * 0.5f;
                    s.Box(new Vector3(x, HeightAt(x, z) + 0.015f, z + 0.8f), new Vector3(0.16f, 0.025f, 1.4f + R()), Pal.Shade(Pal.WoodGrey, 0.8f), false);
                }
            }
            // a cast-off wheel leaning against the smithy's back wall
            float wx = xs["smithy"] + 1.5f, wz = southZ - 6.25f;
            s.solid.Col(Pal.Shade(Pal.WoodOld, 0.85f));
            Props.Wheel(s.solid, new Vector3(wx, HeightAt(wx, wz) + 0.55f, wz), Quaternion.Euler(-12f, 0f, 0f) * Vector3.forward, 0.55f);
        }
    }
}
