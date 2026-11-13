using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    public enum BuildKind { Saloon, Store, Gunsmith, Doctor, Sheriff, Office, Station, Stable, Smithy, Butcher, Facade, Church, Adobe }

    public class BuildingSpec
    {
        public BuildKind kind = BuildKind.Facade;
        public string id = "", sign = "";
        public float w = 9f, d = 10f, h = 3.5f;
        public Color32 wall = Pal.WoodOld, trim = Pal.WoodDark, roof = Pal.Shingle;
        public bool falseFront = true, porch = true;
        public bool closed;                  // boards across the door
        public int front;                    // the top of the false front: 0 straight, 1 stepped up in the middle, 2 a pediment
        public bool painted;                 // the name painted on the boards of the false front instead of a framed board
        public string ghost = "";            // a faded advertisement on a side wall, lines split by '|'
        public int ghostSide = 1;            // the wall that carries it: +1 the right one as seen from the street, -1 the left
        public bool derelict;                // given up: windows and door boarded over, the name all but gone
    }

    /// <summary>
    /// Builds frontier buildings. Local frame: the front wall runs along X at z = 0 and faces -Z (the street);
    /// the building extends towards +Z. Enterable kinds get real walls with door and window openings and furniture.
    /// </summary>
    public static class Architect
    {
        public const float Floor = 0.3f, Thick = 0.18f, PorchDepth = 2.3f;
        /// <summary>The building being built and the dice of its wear: crooked posts and signs, missing panes, patches.</summary>
        static BuildingSpec cur;
        static System.Random wear = new System.Random(1);
        static float W(float range) { return ((float)wear.NextDouble() * 2f - 1f) * range; }
        static bool Chance(float p) { return wear.NextDouble() < p; }
        /// <summary>A front this tall (BuildingSpec.h) has a second storey: its floor and the sills of its windows, above the ground floor.</summary>
        const float TallFrom = 5.3f, Deck = 2.9f, Upper = 3.7f;

        /// <summary>A hole in a wall. style: 0 a double-hung window or a cased doorway, 1 a single fixed light,
        /// 2 a church window, 3 bare (the caller dresses it).</summary>
        struct Opening { public float a, b, y0, y1; public bool glass; public int style; }

        static Opening Door(float centre, float width = 1.5f, float height = 2.35f) { return new Opening { a = centre - width * 0.5f, b = centre + width * 0.5f, y0 = 0f, y1 = height }; }
        static Opening Window(float centre, float width = 1.3f, float sill = 1.0f, float height = 1.35f) { return new Opening { a = centre - width * 0.5f, b = centre + width * 0.5f, y0 = sill, y1 = sill + height, glass = true }; }

        /// <summary>A wall from 'origin' along +X or +Z, with openings measured from the origin; openings with the same
        /// span stand one above another. 'outward' says which side the weather is on (-1 or +1 across the wall);
        /// 'floor' is the height of the floor the openings are measured from.</summary>
        static void Wall(Site s, Vector3 origin, bool alongX, float length, float height, List<Opening> ops, Color32 col, Color32 trim, float outward = -1f, float floor = Floor, float thick = Thick)
        {
            ops.Sort((p, q) => Mathf.Abs(p.a - q.a) > 0.01f ? p.a.CompareTo(q.a) : p.y0.CompareTo(q.y0));
            Vector3 along = alongX ? Vector3.right : Vector3.forward;
            float cursor = 0f;
            void Piece(float a, float b, float y0, float y1)
            {
                if (b - a < 0.02f || y1 - y0 < 0.02f) return;
                Vector3 c = origin + along * ((a + b) * 0.5f) + Vector3.up * ((y0 + y1) * 0.5f);
                Vector3 size = alongX ? new Vector3(b - a, y1 - y0, thick) : new Vector3(thick, y1 - y0, b - a);
                s.Box(c, size, col);
            }
            for (int i = 0; i < ops.Count;)
            {
                Opening first = ops[i];
                Piece(cursor, first.a, 0f, height);
                float filled = 0f;
                for (; i < ops.Count && Mathf.Abs(ops[i].a - first.a) <= 0.01f; i++)
                {
                    Opening o = ops[i];
                    Piece(o.a, o.b, filled, o.y0 > 0f ? floor + o.y0 : 0f);
                    filled = floor + o.y1;
                    Dress(s, origin + along * ((o.a + o.b) * 0.5f), alongX, outward, floor, o, trim);
                }
                Piece(first.a, first.b, filled, height);
                cursor = first.b;
            }
            Piece(cursor, length, 0f, height);
        }

        /// <summary>
        /// The joinery of one opening, from the Blender kit (Tools/blender/buildkit.py): two stiles and a head stretched
        /// along their length only, a sill and two sashes for a window, a threshold for a door; boxes if this build has
        /// no kit. 'mid' is the middle of the opening on the wall's line, at ground level.
        /// </summary>
        static void Dress(Site s, Vector3 mid, bool alongX, float outward, float floor, Opening o, Color32 trim)
        {
            if (o.style == 3) return;
            float ow = o.b - o.a, oh = o.y1 - o.y0;
            Vector3 along = alongX ? Vector3.right : Vector3.forward, across = alongX ? Vector3.forward : Vector3.right;
            Vector3 foot = mid + Vector3.up * (floor + o.y0), centre = foot + Vector3.up * (oh * 0.5f);
            float yaw = alongX ? 0f : 270f;                 // lays the kit's x axis along the wall
            Vector3 Size(float l, float h, float t) { return alongX ? new Vector3(l, h, t) : new Vector3(t, h, l); }

            bool kit;
            if (o.style == 2) kit = s.Model("bk_window_arch", foot, yaw, new Vector3(ow / 0.9f, oh / 2.3f, 1f), trim);
            else
            {
                kit = s.Model("bk_stile", foot - along * (ow * 0.5f), yaw, new Vector3(1f, oh / 2f, 1f), trim);
                if (kit)
                {
                    s.Model("bk_stile", foot + along * (ow * 0.5f), yaw + 180f, new Vector3(1f, oh / 2f, 1f), trim);
                    s.Model("bk_head", foot + Vector3.up * oh, yaw, new Vector3(ow + 0.30f, 1f, 1f), trim);
                    if (o.glass) s.Model("bk_sill", foot, yaw, new Vector3(ow + 0.26f, 1f, 1f), trim);
                    else if (floor > 0f) s.Model("bk_threshold", foot, yaw, new Vector3(ow, 1f, 1f));
                }
            }
            if (!kit)
            {
                float fw = 0.1f, ft = Thick + 0.06f;
                s.solid.Col(trim);
                s.solid.Box(foot + Vector3.up * (oh + fw * 0.5f), Size(ow + fw * 2f, fw, ft));
                foreach (float e in new[] { -1f, 1f }) s.solid.Box(centre + along * (e * (ow + fw) * 0.5f), Size(fw, oh, ft));
                if (o.glass)
                {
                    s.solid.Box(centre, Size(ow, 0.05f, 0.06f));
                    s.solid.Box(centre, Size(0.05f, oh, 0.06f));
                    s.solid.Box(foot + Vector3.down * 0.04f, Size(ow + 0.24f, 0.07f, ft + 0.1f));
                }
            }
            if (!o.glass) return;

            bool boarded = cur != null && cur.derelict;
            s.glass.Col(Pal.Glass, 0.9f);
            if (kit && o.style == 0)
            {
                // double-hung: the upper sash runs in the outer track, the lower one inside it; a pane in each - now
                // and then one is gone
                Vector3 scale = new Vector3((ow - 0.05f) / 1.25f, oh * 0.5f / 0.675f, 1f);
                for (int k = 0; k < 2; k++)
                {
                    Vector3 p = foot + Vector3.up * (k * oh * 0.5f) + across * ((k == 0 ? -outward : outward) * 0.024f);
                    s.Model("bk_sash", p, yaw, scale, trim);
                    if (!Chance(boarded ? 0.5f : 0.07f)) s.glass.Box(p + Vector3.up * (oh * 0.25f), Size(ow - 0.14f, oh * 0.5f - 0.05f, 0.008f));
                }
            }
            else if (kit && o.style == 1)
            {
                s.Model("bk_sash", foot, yaw, new Vector3((ow - 0.05f) / 1.25f, oh / 0.675f, 1f), trim);
                if (!Chance(0.07f)) s.glass.Box(centre, Size(ow - 0.12f, oh - 0.05f, 0.008f));
            }
            else s.glass.Box(centre, Size(ow - 0.05f, oh - 0.04f, 0.012f));
            if (boarded)
            {
                // rough boards nailed across the outside, none quite level
                Quaternion face = Quaternion.LookRotation(across * outward);
                for (int k = 0; k < 3; k++)
                {
                    Vector3 at = centre + Vector3.up * ((k - 1) * oh * 0.3f + W(0.04f)) + across * (outward * (Thick * 0.5f + 0.03f));
                    s.BoxRot(at, new Vector3(ow + 0.3f + W(0.06f), 0.17f + W(0.02f), 0.03f), face * Quaternion.Euler(0f, 0f, W(7f)), Pal.Shade(Pal.WoodGrey, 0.85f + W(0.1f)), false);
                }
            }
            s.Collider(centre, Size(ow, oh, 0.06f));
        }

        /// <summary>Evenly spaced centres for as many windows ('unit' wide with their casing) as sit well between two limits.</summary>
        static List<float> Bays(float from, float to, float unit)
        {
            var list = new List<float>();
            float span = to - from;
            if (span < unit + 0.1f) return list;
            int n = Mathf.Max(1, Mathf.FloorToInt(span / (unit + 0.55f)));
            float gap = (span - n * unit) / (n + 1);
            for (int i = 0; i < n; i++) list.Add(from + gap + unit * 0.5f + i * (unit + gap));
            return list;
        }

        /// <summary>A post from the kit, stretched to a height (the kit's is 2.6 m); a plain box without the kit.</summary>
        static void Post(Site s, string model, Vector3 foot, float height, float yaw, Color32 trim, float width = 0.13f, float lean = 0f)
        {
            if (s.Model(model, foot, Quaternion.Euler(W(lean), yaw, W(lean)), new Vector3(1f, height / 2.6f, 1f), trim)) return;
            s.solid.Col(trim);
            s.solid.Box(foot + Vector3.up * (height * 0.5f), new Vector3(width, height, width));
        }

        /// <summary>A panelled door standing shut in its opening ('foot' is the middle of the threshold); two leaves if 'pair'.</summary>
        static void ShutDoor(Site s, Vector3 foot, float width, float height, Color32 dye, bool pair = false)
        {
            float clear = width - 0.05f;
            int n = pair ? 2 : 1;
            bool kit = true;
            for (int i = 0; i < n; i++)
                kit &= s.Model("bk_doorleaf", foot + new Vector3((i - (n - 1) * 0.5f) * clear / n, 0f, 0.035f), i == 0 && pair ? 180f : 0f,
                               new Vector3(clear / n / 1.2f, (height - 0.03f) / 2.3f, 1f), dye);
            if (!kit) s.Box(foot + new Vector3(0, height * 0.5f, 0.03f), new Vector3(width, height, 0.06f), dye, false);
            s.Collider(foot + Vector3.up * (height * 0.5f), new Vector3(width, height, Thick));
        }

        /// <summary>A lantern on a bracket; its glass glows with the windows after dark.</summary>
        static void Lantern(Site s, Vector3 onWall)
        {
            if (!s.Model("bk_lantern", onWall)) return;
            if (!Models.Get("bk_lantern").Mark("flame", out Vector3 flame)) flame = new Vector3(0f, -0.06f, -0.24f);
            s.glass.Col(Pal.GlassLit);
            s.glass.Box(onWall + flame, new Vector3(0.085f, 0.13f, 0.085f));
        }

        static void Roof(Site s, BuildingSpec b, float rise, float wallTop, bool pipe = true)
        {
            float ov = 0.35f, half = b.w * 0.5f + ov;
            float run = Mathf.Sqrt(half * half + rise * rise), ang = Mathf.Atan2(rise, half) * Mathf.Rad2Deg;
            // behind a false front the roof stops at the wall instead of overhanging the street
            float len = b.d + ov * (b.falseFront ? 1f : 2f);
            float midZ = b.d * 0.5f + (b.falseFront ? ov * 0.5f + 0.06f : 0f);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 c = new Vector3(side * half * 0.5f, wallTop + rise * 0.5f + 0.05f, midZ);
                Quaternion tilt = Quaternion.Euler(0f, 0f, side * -ang);
                s.BoxRot(c, new Vector3(run + 0.1f, 0.12f, len), tilt, b.roof);
                // barge boards on the open gable ends
                foreach (float end in new[] { -1f, 1f })
                {
                    if (end < 0f && b.falseFront) continue;
                    s.BoxRot(new Vector3(c.x, c.y - 0.03f, midZ + end * (len * 0.5f + 0.012f)), new Vector3(run + 0.14f, 0.19f, 0.03f), tilt, b.trim, false);
                }
            }
            s.Box(new Vector3(0, wallTop + rise + 0.135f, midZ), new Vector3(0.26f, 0.05f, len + 0.02f), Pal.Shade(b.roof, 0.8f), false);    // ridge board
            // sheets of tin nailed over the worst of the shingles (they rust)
            foreach (float side in new[] { -1f, 1f })
            {
                if (!Chance(0.45f) || run < 2.4f || len < 4f) continue;
                Quaternion tilt = Quaternion.Euler(0f, 0f, side * -ang);
                float u = W(run * 0.5f - 1.0f), v = W(len * 0.5f - 1.4f);
                Vector3 c = new Vector3(side * half * 0.5f, wallTop + rise * 0.5f + 0.05f, midZ + v) + tilt * new Vector3(u, 0.07f, 0f);
                s.BoxRot(c, new Vector3(1.0f + W(0.25f) + 0.25f, 0.012f, 1.5f + W(0.5f)), tilt * Quaternion.Euler(0f, W(6f), 0f), Pal.Tin, false);
            }
            if (pipe)
            {
                // a stovepipe through one slope, a different one from house to house
                float px = b.w * 0.24f * (b.id.Length % 2 == 0 ? 1f : -1f);
                Vector3 foot = new Vector3(px, wallTop + rise * (1f - Mathf.Abs(px) / half) + 0.02f, b.d * 0.62f);
                // two stoves in three are lit
                if (s.Model("bk_stovepipe", foot) && (b.id.Length * 7 + (int)b.w) % 3 != 0) s.Smoke(Plume.Chimney, foot + Vector3.up * 1.5f);
            }
            // gable ends
            s.solid.Col(b.wall);
            foreach (float z in new[] { 0f, b.d })
            {
                if (z == 0f && b.falseFront) continue;
                s.solid.Prism(new List<Vector3>
                {
                    new Vector3(-b.w * 0.5f, wallTop, z - Thick * 0.5f), new Vector3(0f, wallTop + rise, z - Thick * 0.5f), new Vector3(b.w * 0.5f, wallTop, z - Thick * 0.5f)
                }, Vector3.forward * Thick);
            }
            if (b.falseFront)
            {
                float top = wallTop + rise + 0.75f, fw = b.w + 0.3f, face = -0.02f - Thick * 0.5f;
                s.Box(new Vector3(0, (wallTop + top) * 0.5f, -0.02f), new Vector3(fw, top - wallTop, Thick), b.wall);
                Cornice(s, fw + 0.3f, top, b.trim);
                if (b.front == 1)
                {
                    // stepped: the middle stands higher under its own cornice, the steps edged with trim
                    float sw = Mathf.Max(2.4f, Mathf.Round(fw * 0.45f)), lift = 0.7f;
                    s.Box(new Vector3(0, top + lift * 0.5f, -0.02f), new Vector3(sw, lift, Thick), b.wall);
                    Cornice(s, sw + 0.24f, top + lift, b.trim);
                    foreach (float e in new[] { -1f, 1f })
                        s.Box(new Vector3(e * sw * 0.5f, top + lift * 0.5f, face - 0.025f), new Vector3(0.1f, lift, 0.05f), b.trim, false);
                }
                else if (b.front == 2)
                {
                    // a low pediment over the middle, the cornice raking up both slopes
                    float pw = fw * 0.55f, ph = Mathf.Min(1.0f, pw * 0.2f);
                    s.solid.Col(b.wall);
                    s.solid.Prism(new List<Vector3> { new Vector3(-pw * 0.5f, top, face), new Vector3(0f, top + ph, face), new Vector3(pw * 0.5f, top, face) }, Vector3.forward * Thick);
                    float slope = Mathf.Sqrt(pw * pw * 0.25f + ph * ph), pitch = Mathf.Atan2(ph, pw * 0.5f) * Mathf.Rad2Deg;
                    foreach (float e in new[] { -1f, 1f })
                    {
                        Vector3 mid = new Vector3(e * pw * 0.25f, top + ph * 0.5f, face);
                        Quaternion rake = Quaternion.Euler(0f, 0f, -e * pitch);
                        if (!s.Model("bk_cornice", mid, rake, new Vector3(slope + 0.12f, 1f, 1f), b.trim))
                            s.BoxRot(mid + new Vector3(0, 0.08f, -0.06f), new Vector3(slope, 0.16f, 0.3f), rake, b.trim, false);
                    }
                }
                if (b.sign != "")
                {
                    Vector3 c = new Vector3(0, wallTop + (top - wallTop) * 0.5f, -0.14f);
                    if (b.painted) Painted(s, b.sign, new Vector3(0, c.y, face - 0.006f), fw - 0.9f, top - wallTop - 0.25f, b.wall);
                    else SignBoard(s, b.sign, c, Mathf.Min(b.w - 0.4f, b.sign.Length * 0.34f + 1.0f), 1.0f, 0.62f, b.trim);
                }
            }
        }

        /// <summary>The moulded cap of a false front from the kit in metre lengths, centred, 'length' long at height 'y'.</summary>
        static void Cornice(Site s, float length, float y, Color32 trim)
        {
            float z = -0.02f - Thick * 0.5f;
            if (Models.Has("bk_cornice"))
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(length));
                for (int i = 0; i < n; i++)
                    s.Model("bk_cornice", new Vector3(-length * 0.5f + (i + 0.5f) * length / n, y, z), 0f, new Vector3(length / n, 1f, 1f), trim);
            }
            else s.Box(new Vector3(0, y + 0.08f, z - 0.04f), new Vector3(length, 0.16f, 0.36f), trim, false);
        }

        /// <summary>A name painted straight on the boards: big letters inside a ruled border, light on a dark wall and
        /// dark on a light one. 'c' lies on the face of the boards.</summary>
        static void Painted(Site s, string text, Vector3 c, float width, float height, Color32 wall)
        {
            bool light = wall.r * 0.3f + wall.g * 0.59f + wall.b * 0.11f > 140f;
            Color32 ink = Pal.Lerp(light ? Pal.H(0x3B2C22) : Pal.H(0xE4D9BF), wall, cur != null && cur.derelict ? 0.55f : 0.18f);
            float letters = Mathf.Min(0.82f, height * 0.6f, width / Mathf.Max(1, text.Length) / 0.56f);
            s.Text(text, c, Quaternion.identity, letters, ink);
            float bw = Mathf.Min(width + 0.4f, text.Length * letters * 0.56f + 0.9f), bh = Mathf.Min(height, letters * 1.55f);
            Vector3 z = c + Vector3.forward * 0.002f;
            foreach (float e in new[] { -1f, 1f })
            {
                s.Box(z + new Vector3(0f, e * bh * 0.5f, 0f), new Vector3(bw, 0.045f, 0.006f), ink, false);
                s.Box(z + new Vector3(e * bw * 0.5f, 0f, 0f), new Vector3(0.045f, bh, 0.006f), ink, false);
            }
        }

        /// <summary>A faded advertisement painted on a blank side wall (BuildingSpec.ghost): a pale ground in the wall's own
        /// material - the boards or the bricks show through it and it flakes with the rest of the paint - and the
        /// letters on it.</summary>
        static void Ghost(Site s, BuildingSpec b, float top, float thick = Thick)
        {
            if (string.IsNullOrEmpty(b.ghost)) return;
            float side = b.ghostSide >= 0 ? 1f : -1f, x = side * (b.w * 0.5f + thick * 0.5f + 0.004f);
            Quaternion rot = Quaternion.Euler(0f, side > 0f ? -90f : 90f, 0f);
            string[] lines = b.ghost.Split('|');
            int longest = 1;
            foreach (string l in lines) longest = Mathf.Max(longest, l.Length);
            float y0 = 1.7f, y1 = top - 0.3f, z0 = 0.8f, z1 = b.d - 0.8f;
            if (y1 - y0 < 1f || z1 - z0 < 2f) return;
            float letters = Mathf.Min(0.95f, (y1 - y0) / (lines.Length * 1.4f + 0.3f), (z1 - z0) / (longest * 0.56f + 0.6f));
            float pw = Mathf.Min(z1 - z0, longest * letters * 0.56f + 0.8f), ph = Mathf.Min(y1 - y0, lines.Length * letters * 1.4f + 0.35f);
            Vector3 mid = new Vector3(x, Mathf.Min(y1 - ph * 0.5f, (y0 + y1) * 0.5f + 0.3f), (z0 + z1) * 0.5f);
            Color32 ground = Pal.Lerp(b.wall, Pal.H(0xE2D7BD), 0.58f);
            SurfaceMap.Hint(ground, SurfaceMap.Of(b.wall));
            s.Box(mid, new Vector3(0.006f, ph, pw), ground, false);
            Color32 ink = Pal.Lerp(Pal.H(0x5E2219), b.wall, 0.22f);
            for (int i = 0; i < lines.Length; i++)
                s.Text(lines[i], mid + new Vector3(side * 0.004f, ((lines.Length - 1) * 0.5f - i) * letters * 1.35f, 0f), rot, letters, ink);
        }

        /// <summary>A painted board in a moulded frame, lettered.</summary>
        static void SignBoard(Site s, string text, Vector3 c, float width, float height, float letters, Color32 trim)
        {
            Quaternion q = Quaternion.Euler(0f, 0f, W(2.2f));                        // never quite level
            Color32 board = Pal.Lerp(Pal.Shade(Pal.Parchment, 0.92f), Pal.H(0xB9A88A), 0.25f + W(0.2f));   // yellowed
            s.BoxRot(c, new Vector3(width, height, 0.06f), q, board, false);
            foreach (float e in new[] { -1f, 1f })
            {
                s.Model("bk_sign_rail", c + q * (Vector3.up * (e * (height * 0.5f + 0.03f))), q, new Vector3(width + 0.06f, 1f, 1f), trim);
                s.Model("bk_sign_end", c + q * (Vector3.right * (e * (width * 0.5f + 0.045f))), q, new Vector3(1f, height, 1f), trim);
            }
            s.Text(text, c + Vector3.back * 0.05f, q, letters, new Color32(0x3A, 0x2E, 0x22, 255));
        }

        static void Porch(Site s, BuildingSpec b, float wallTop)
        {
            float w = b.w + 0.3f, pz = -PorchDepth + 0.1f;
            bool deck = b.h >= TallFrom;            // a two-storey front carries a balcony instead of a lean-to roof
            s.Box(new Vector3(0, Floor * 0.5f, -PorchDepth * 0.5f), new Vector3(w, Floor, PorchDepth), Pal.WoodGrey);
            s.Box(new Vector3(0, Floor * 0.25f, -PorchDepth - 0.2f), new Vector3(Mathf.Min(w, 3f), Floor * 0.5f, 0.4f), Pal.WoodGrey);
            // the roof falls towards the street and rests on a beam along the posts
            float postH = deck ? 2.6f : 2.35f, beamH = deck ? 0.18f : 0.12f;
            if (deck) s.Box(new Vector3(0, Floor + Deck - 0.06f, -1.25f), new Vector3(w + 0.3f, 0.12f, 2.5f), Pal.WoodGrey);
            else s.BoxRot(new Vector3(0, Floor + 2.621f + W(0.03f), -1.325f), new Vector3(w + 0.3f, 0.09f, 2.67f), Quaternion.Euler(-7f + W(0.7f), 0, W(0.6f)), b.roof);
            s.Box(new Vector3(0, Floor + postH + beamH * 0.5f, pz), new Vector3(w, beamH, 0.11f), b.trim, false);
            int posts = Mathf.Max(2, Mathf.RoundToInt(w / 3.2f) + 1);
            var at = new List<float>();
            for (int i = 0; i < posts; i++)
            {
                float x = Mathf.Lerp(-w * 0.5f + 0.15f, w * 0.5f - 0.15f, i / (float)(posts - 1));
                if (Mathf.Abs(x) < 0.9f) x = Mathf.Sign(x == 0f ? 1f : x) * 1.2f;   // keep the doorway clear
                at.Add(x);
                // the end posts brace inwards only
                bool end = i == 0 || i == posts - 1;
                Post(s, end ? "bk_post_end" : "bk_post", new Vector3(x, Floor, pz), postH, i == posts - 1 ? 180f : 0f, b.trim, 0.13f, 1.8f);
            }
            if (!deck || !Models.Has("bk_railing")) return;

            // the balcony: a railing between newels over the posts, returned to the wall at both ends
            float y = Floor + Deck, rz = -2.4f, x0 = -(w * 0.5f + 0.05f);
            at[0] = x0; at[posts - 1] = -x0;
            foreach (float x in at) s.Model("bk_newel", new Vector3(x, y, rz), 0f, Vector3.one, b.trim);
            for (int i = 0; i + 1 < posts; i++) Rail(s, new Vector3(at[i], y, rz), new Vector3(at[i + 1], y, rz), b.trim);
            foreach (float x in new[] { x0, -x0 }) Rail(s, new Vector3(x, y, rz), new Vector3(x, y, -Thick * 0.5f + 0.05f), b.trim);
        }

        /// <summary>Railing from the kit in equal lengths between two newel posts.</summary>
        static void Rail(Site s, Vector3 a, Vector3 b, Color32 trim)
        {
            Vector3 d = b - a;
            float len = d.magnitude - 0.095f;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 1.1f));
            float yaw = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? 0f : 90f;
            for (int i = 0; i < n; i++)
                s.Model("bk_railing", a + d.normalized * (0.0475f + (i + 0.5f) * len / n), yaw, new Vector3(len / n, 1f, 1f), trim);
        }

        /// <summary>Boards replaced where the old ones rotted: a few mismatched planks low on the side walls.</summary>
        static void Patches(Site s, BuildingSpec b)
        {
            int n = 1 + wear.Next(3);
            for (int i = 0; i < n; i++)
            {
                float side = Chance(0.5f) ? -1f : 1f, x = side * (b.w * 0.5f + Thick * 0.5f + 0.012f);
                float z = Mathf.Lerp(0.6f, b.d - 0.6f, (float)wear.NextDouble());
                float h = 0.5f + (float)wear.NextDouble() * 0.7f, w = 0.17f + (float)wear.NextDouble() * 0.14f;
                Color32 c = Chance(0.5f) ? Pal.Shade(Pal.WoodNew, 0.9f + W(0.08f)) : Pal.Shade(Pal.WoodGrey, 0.95f + W(0.1f));
                s.Box(new Vector3(x, 0.05f + h * 0.5f, z), new Vector3(0.02f, h, w), c, false);
                if (Chance(0.5f)) s.Box(new Vector3(x, 0.05f + h * 0.35f, z + w + 0.012f), new Vector3(0.02f, h * 0.7f, w), c, false);
            }
        }

        static void CornerTrim(Site s, BuildingSpec b, float top)
        {
            s.solid.Col(b.trim);
            foreach (float x in new[] { -b.w * 0.5f, b.w * 0.5f })
                foreach (float z in new[] { 0f, b.d })
                    s.solid.Box(new Vector3(x, top * 0.5f, z), new Vector3(Thick + 0.08f, top, Thick + 0.08f));
        }

        public static Site Build(BuildingSpec b)
        {
            var s = new Site();
            cur = b;
            int seed = 17;
            foreach (char ch in b.id) seed = seed * 31 + ch;
            wear = new System.Random(seed);
            float top = b.h + Floor;
            float rise = Mathf.Min(2.2f, b.w * 0.22f);
            switch (b.kind)
            {
                case BuildKind.Stable: Stable(s, b); return s;
                case BuildKind.Smithy: Smithy(s, b); return s;
                case BuildKind.Butcher: Butcher(s, b); return s;
                case BuildKind.Facade: case BuildKind.Church: Facade(s, b, top, rise); return s;
                case BuildKind.Adobe: Adobe(s, b); return s;
            }

            // ---- shell
            s.Box(new Vector3(0, Floor * 0.5f, b.d * 0.5f), new Vector3(b.w, Floor, b.d), Pal.WoodOld);          // a boarded floor
            bool wide = b.kind == BuildKind.Saloon;
            float doorW = wide ? 2.0f : 1.5f;
            bool tall = b.h >= TallFrom;
            var front = new List<Opening> { Door(b.w * 0.5f, doorW) };
            // a tall house is one high hall inside: its upper windows light it from above the balcony
            if (tall) front.Add(new Opening { a = b.w * 0.5f - doorW * 0.5f, b = b.w * 0.5f + doorW * 0.5f, y0 = Upper, y1 = Upper + 1.35f, glass = true });
            if (b.w > 6.5f)
                foreach (float e in new[] { -1f, 1f })
                {
                    front.Add(Window(b.w * 0.5f + e * (wide ? 3.6f : 2.6f)));
                    if (tall) front.Add(Window(b.w * 0.5f + e * (wide ? 3.6f : 2.6f), 1.3f, Upper));
                }
            Wall(s, new Vector3(-b.w * 0.5f, 0, 0), true, b.w, top, front, b.wall, b.trim, -1f);
            var sideL = new List<Opening>(); var sideR = new List<Opening>();
            for (float z = 2.6f; z < b.d - 1.8f; z += 4.2f)
            {
                sideL.Add(Window(z)); sideR.Add(Window(z));
                if (tall) { sideL.Add(Window(z, 1.3f, Upper)); sideR.Add(Window(z, 1.3f, Upper)); }
            }
            if (b.kind == BuildKind.Sheriff) sideR.Clear();
            if (!string.IsNullOrEmpty(b.ghost)) (b.ghostSide >= 0 ? sideR : sideL).Clear();      // a painted ad wants a blank wall
            Wall(s, new Vector3(-b.w * 0.5f, 0, 0), false, b.d, top, sideL, b.wall, b.trim, -1f);
            Wall(s, new Vector3(b.w * 0.5f, 0, 0), false, b.d, top, sideR, b.wall, b.trim, 1f);
            var backOps = new List<Opening>();
            Wall(s, new Vector3(-b.w * 0.5f, 0, b.d), true, b.w, top, backOps, b.wall, b.trim, 1f);
            CornerTrim(s, b, top);
            Patches(s, b);
            Roof(s, b, rise, top);
            Ghost(s, b, top);
            if (b.porch) Porch(s, b, top);
            Lantern(s, new Vector3(doorW * 0.5f + 0.3f, Floor + 1.95f, -Thick * 0.5f));
            // a tall house keeps a room of the usual height on the ground floor: the storey above is not furnished,
            // dark behind its windows like a closed house
            float room = tall ? Floor + 3.5f : top;
            if (tall) s.Box(new Vector3(0, (room + top) * 0.5f + 0.02f, b.d * 0.5f), new Vector3(b.w - Thick - 0.02f, top - room - 0.04f, b.d - Thick - 0.02f), Pal.H(0x1B1814), false);
            if (!b.closed && Models.Has("in_wainscot")) Interior(s, b, room, front, sideL, sideR, backOps);
            else s.Light(new Vector3(0, top - 0.6f, b.d * 0.5f), new Color(1f, 0.8f, 0.55f), Mathf.Max(b.w, b.d) * 1.5f, 3.4f, true);
            s.Mark("door", new Vector3(0, Floor, -PorchDepth - 1.2f), 0f);
            s.Mark("inside", new Vector3(0, Floor, 2.2f), 0f);
            if (b.closed)
            {
                ShutDoor(s, new Vector3(0, Floor, 0), doorW, 2.35f, Pal.WoodDark);
                s.solid.Col(Pal.WoodNew);
                s.solid.Beam(new Vector3(-0.8f, Floor + 0.6f, -0.1f), new Vector3(0.8f, Floor + 1.8f, -0.1f), 0.14f, 0.03f, Vector3.back);
                s.solid.Beam(new Vector3(-0.8f, Floor + 1.8f, -0.1f), new Vector3(0.8f, Floor + 0.6f, -0.1f), 0.14f, 0.03f, Vector3.back);
                return s;
            }
            // the door swings in before whoever walks up to it (Doors); the saloon has batwings that swing both ways
            Vector3 doorway = new Vector3(0, Floor, 0);
            if (wide)
            {
                float hx = doorW * 0.5f - 0.022f;
                s.Door("bk_batwing", new Vector3(-hx, Floor, 0), 0f, Vector3.zero, Vector3.one, b.trim, doorway, -78f, 1.9f, true);
                s.Door("bk_batwing", new Vector3(hx, Floor, 0), 180f, Vector3.zero, Vector3.one, b.trim, doorway, 78f, 1.9f, true);
            }
            else
            {
                float leaf = doorW - 0.05f;
                s.Door("bk_doorleaf", new Vector3(-leaf * 0.5f, Floor, 0.04f), 0f, new Vector3(leaf * 0.5f, 0, 0), new Vector3(leaf / 1.2f, 2.32f / 2.3f, 1f),
                       b.trim, doorway, -100f, 2.3f, false);
            }

            float hw = b.w * 0.5f, y = Floor;
            switch (b.kind)
            {
                case BuildKind.Saloon:
                {
                    // bar along the left wall, tables on the right
                    Props.Counter(s, new Vector3(-hw + 1.7f, y, 2.4f), new Vector3(-hw + 1.7f, y, b.d - 2.2f), 0.7f, true);
                    Props.Shelves(s, new Vector3(-hw + 0.12f, y, (2.4f + b.d - 2.2f) * 0.5f), 90f, b.d - 5f, 7, true);
                    s.Mark("keeper", new Vector3(-hw + 0.95f, y, b.d * 0.5f), 90f);
                    s.Mark("counter", new Vector3(-hw + 2.6f, y, b.d * 0.5f), 270f);
                    for (int i = 0; i < 3; i++) s.Mark("bar" + i, new Vector3(-hw + 2.5f, y, 3.4f + i * 1.7f), 270f);
                    int seat = 0;
                    Vector3[] tables = { new Vector3(1.6f, y, 3.4f), new Vector3(hw - 2.2f, y, 6.2f), new Vector3(1.2f, y, 8.6f) };
                    foreach (Vector3 t in tables)
                    {
                        Props.Table(s, t);
                        for (int k = 0; k < 3; k++)
                        {
                            float a = k * 120f + 30f;
                            Vector3 cp = t + Quaternion.Euler(0, a, 0) * new Vector3(0, 0, -0.95f);
                            Props.Chair(s, cp, a, "seat" + seat++);
                        }
                    }
                    // piano against the right wall, its stool before the keys
                    if (s.Model("in_piano", new Vector3(hw - Thick * 0.5f - 0.02f, y, b.d - 1.6f), -90f))
                    {
                        s.Collider(new Vector3(hw - 0.4f, y + 0.65f, b.d - 1.6f), new Vector3(0.62f, 1.3f, 1.5f));
                        s.Model("in_stool", new Vector3(hw - 1.12f, y, b.d - 1.6f), 0f);
                    }
                    else
                    {
                        s.Box(new Vector3(hw - 0.5f, y + 0.65f, b.d - 1.6f), new Vector3(0.7f, 1.3f, 1.5f), Pal.WoodDark);
                        s.Box(new Vector3(hw - 0.95f, y + 0.72f, b.d - 1.6f), new Vector3(0.3f, 0.06f, 1.3f), Pal.Bone, false);
                    }
                    s.Mark("piano", new Vector3(hw - 1.5f, y, b.d - 1.6f), 90f);
                    Props.Barrel(s, new Vector3(-hw + 0.6f, y, b.d - 0.8f));
                    Props.Bed(s, new Vector3(hw - 0.8f, y, b.d - 4.4f), 180f);
                    s.Mark("bed", new Vector3(hw - 1.9f, y, b.d - 4.4f), 90f);
                    break;
                }
                case BuildKind.Store:
                case BuildKind.Gunsmith:
                case BuildKind.Doctor:
                case BuildKind.Office:
                case BuildKind.Station:
                {
                    float cz = b.d - 2.6f;
                    Props.Counter(s, new Vector3(-hw + 0.9f, y, cz), new Vector3(hw - 0.9f, y, cz));
                    s.Mark("keeper", new Vector3(0, y, cz + 1.0f), 180f);
                    s.Mark("counter", new Vector3(0, y, cz - 1.0f), 0f);
                    Props.Shelves(s, new Vector3(0, y, b.d - 0.14f), 180f, b.w - 1.4f, b.id.GetHashCode());
                    if (b.kind == BuildKind.Store)
                    {
                        Props.Shelves(s, new Vector3(-hw + 0.14f, y, 3.2f), 90f, 2.6f, 3);
                        Props.Barrel(s, new Vector3(hw - 0.7f, y, 1.2f));
                        Props.Barrel(s, new Vector3(hw - 0.7f, y, 2.0f));
                        Props.Crate(s, new Vector3(hw - 0.8f, y, 3.1f), 0.7f, 12f);
                        Props.Crate(s, new Vector3(-hw + 0.9f, y, 1.0f), 0.6f, -8f);
                    }
                    else if (b.kind == BuildKind.Gunsmith)
                    {
                        // rifles racked on the side wall
                        s.Box(new Vector3(-hw + 0.14f, y + 1.5f, 3.4f), new Vector3(0.05f, 1.2f, 3.0f), Pal.WoodDark, false);
                        if (Models.Has("w_drover"))
                            Props.GunRack(s, new Vector3(-hw + 0.21f, y + 0.92f, 2.25f), new Vector3(0, 0, 0.58f), Vector3.right, "w_drover", "w_longshot", "w_thunder", "w_magpie", "w_drover");
                        else
                            for (int i = 0; i < 5; i++)
                            {
                                s.solid.Col(Pal.GunBlue, 0.7f);
                                s.solid.Beam(new Vector3(-hw + 0.2f, y + 1.05f, 2.2f + i * 0.6f), new Vector3(-hw + 0.2f, y + 1.95f, 2.3f + i * 0.6f), 0.035f, 0.05f, Vector3.right);
                            }
                        s.Box(new Vector3(-hw + 0.24f, y + 0.9f, 3.4f), new Vector3(0.24f, 0.04f, 3.0f), Pal.WoodDark, false);          // the ledge the butts rest on
                        Props.Crate(s, new Vector3(hw - 0.8f, y, 1.2f), 0.65f, 5f);
                    }
                    else if (b.kind == BuildKind.Doctor)
                    {
                        Props.Bed(s, new Vector3(hw - 0.75f, y, 3.0f), 0f);
                        if (s.Model("in_cabinet", new Vector3(-hw + Thick * 0.5f + 0.02f, y, 2.6f), 90f, Vector3.one, b.trim))
                            s.Collider(new Vector3(-hw + 0.3f, y + 0.98f, 2.6f), new Vector3(0.4f, 1.95f, 0.95f));
                        else s.Box(new Vector3(-hw + 0.5f, y + 0.9f, 2.6f), new Vector3(0.6f, 1.8f, 1.2f), Pal.WoodWhite);
                    }
                    else if (b.kind == BuildKind.Office)
                    {
                        s.Box(new Vector3(hw - 0.6f, y + 0.6f, b.d - 1.2f), new Vector3(0.8f, 1.2f, 0.8f), Pal.Iron);     // safe
                        s.Box(new Vector3(-hw + 0.12f, y + 1.7f, 3.5f), new Vector3(0.04f, 1.2f, 1.9f), Pal.Parchment, false);  // wall map
                        Props.Bench(s, new Vector3(hw - 0.5f, y, 2.4f), 270f, 1.6f, "bench");
                    }
                    else
                    {
                        Props.Bench(s, new Vector3(-hw + 0.5f, y, 2.6f), 90f, 2.2f, "bench0");
                        Props.Bench(s, new Vector3(hw - 0.5f, y, 2.6f), 270f, 2.2f, "bench1");
                    }
                    break;
                }
                case BuildKind.Sheriff:
                {
                    if (s.Model("in_desk", new Vector3(-hw + 1.7f, y, 3.2f), 180f))                                   // desk
                        s.Collider(new Vector3(-hw + 1.7f, y + 0.4f, 3.2f), new Vector3(1.5f, 0.8f, 0.76f));
                    else s.Box(new Vector3(-hw + 1.7f, y + 0.4f, 3.2f), new Vector3(1.6f, 0.8f, 0.8f), Pal.WoodOld);
                    Props.Chair(s, new Vector3(-hw + 1.7f, y, 4.0f), 180f, "desk");
                    s.Mark("keeper", new Vector3(-hw + 1.7f, y, 4.0f), 180f);
                    s.Mark("counter", new Vector3(-hw + 1.7f, y, 2.2f), 0f);
                    // cell in the back right corner
                    float cx = hw - 3.2f, cz0 = b.d - 3.4f;
                    s.solid.Col(Pal.Iron, 0.5f);
                    for (float x = cx; x <= hw - 0.1f; x += 0.22f)
                        if (x < cx + 0.9f || x > cx + 1.9f) s.solid.Cyl(new Vector3(x, y, cz0), new Vector3(x, top - 0.4f, cz0), 0.025f, 0.025f, 5);
                    for (float z = cz0; z <= b.d - 0.1f; z += 0.22f) s.solid.Cyl(new Vector3(cx, y, z), new Vector3(cx, top - 0.4f, z), 0.025f, 0.025f, 5);
                    s.solid.Box(new Vector3((cx + hw) * 0.5f, top - 0.4f, cz0), new Vector3(hw - cx, 0.08f, 0.08f));
                    s.solid.Box(new Vector3(cx, top - 0.4f, (cz0 + b.d) * 0.5f), new Vector3(0.08f, 0.08f, b.d - cz0));
                    s.Collider(new Vector3(cx + 0.45f, y + 1.3f, cz0), new Vector3(0.9f, 2.6f, 0.1f));
                    s.Collider(new Vector3((cx + 1.9f + hw) * 0.5f, y + 1.3f, cz0), new Vector3(hw - cx - 1.9f, 2.6f, 0.1f));
                    s.Collider(new Vector3(cx, y + 1.3f, (cz0 + b.d) * 0.5f), new Vector3(0.1f, 2.6f, b.d - cz0));
                    Props.Bed(s, new Vector3(hw - 0.7f, y, b.d - 1.3f), 0f);
                    s.Mark("cell", new Vector3(cx + 1.6f, y, b.d - 1.6f), 180f);
                    s.Mark("celldoor", new Vector3(cx + 1.4f, y, cz0 - 0.9f), 0f);
                    // gun rack and the notice board outside
                    s.Box(new Vector3(-hw + 0.14f, y + 1.5f, 6f), new Vector3(0.05f, 1.0f, 1.6f), Pal.WoodDark, false);
                    s.Box(new Vector3(-hw + 0.24f, y + 0.98f, 6f), new Vector3(0.24f, 0.04f, 1.6f), Pal.WoodDark, false);
                    Props.GunRack(s, new Vector3(-hw + 0.21f, y + 1.0f, 5.5f), new Vector3(0, 0, 0.5f), Vector3.right, "w_thunder", "w_drover", "w_longshot");
                    s.Box(new Vector3(-hw * 0.55f, Floor + 1.5f, -0.14f), new Vector3(1.5f, 1.0f, 0.06f), Pal.WoodNew, false);
                    s.Mark("board", new Vector3(-hw * 0.55f, Floor, -1.1f), 0f);
                    break;
                }
            }
            return s;
        }

        /// <summary>
        /// The inside of an open house: wainscot to the chair rail and wallpaper (plaster, boards) above it on every wall,
        /// cut round the openings and kept off where shelves or racks stand against the wall; a boarded ceiling on
        /// joists; hanging lamps whose chimneys glow after dark and whose light fills the room; a stove under the pipe
        /// on the roof; and the things on the walls a house of its kind would have. Models from interior.py.
        /// </summary>
        static void Interior(Site s, BuildingSpec b, float top, List<Opening> front, List<Opening> left, List<Opening> right, List<Opening> back)
        {
            float hw = b.w * 0.5f, y = Floor, inner = Thick * 0.5f;
            Color32 paper, paint;
            Surf finish = Surf.Paper;
            switch (b.kind)
            {
                case BuildKind.Saloon: paper = Pal.H(0x6E2B26); paint = Pal.H(0x4A3324); break;
                case BuildKind.Store: paper = Pal.H(0xC2AE86); paint = Pal.H(0x6A4A30); break;
                case BuildKind.Gunsmith: paper = Pal.WoodOld; paint = Pal.H(0x5A4632); finish = Surf.Planks; break;
                case BuildKind.Doctor: paper = Pal.H(0x9AAA8C); paint = Pal.WoodWhite; break;
                case BuildKind.Office: paper = Pal.H(0x6F7F8C); paint = Pal.H(0x3E4A36); break;
                case BuildKind.Station: paper = Pal.H(0xB89A5E); paint = Pal.H(0x6A4A30); break;
                default: paper = Pal.Plaster; paint = Pal.H(0x5A4632); finish = Surf.Plaster; break;
            }
            // where furniture stands against a wall, the wainscot stays off it: (wall, from, to) along the wall
            var keepOff = new List<(int wall, float a, float b)>();
            float cz = b.d - 2.6f;
            switch (b.kind)
            {
                case BuildKind.Saloon: keepOff.Add((1, 2.3f, b.d - 2.1f)); break;
                case BuildKind.Store: keepOff.Add((3, 0.6f, b.w - 0.6f)); keepOff.Add((1, 1.8f, 4.6f)); break;
                case BuildKind.Gunsmith: keepOff.Add((3, 0.6f, b.w - 0.6f)); keepOff.Add((1, 1.8f, 5.0f)); break;
                case BuildKind.Doctor: case BuildKind.Office: case BuildKind.Station: keepOff.Add((3, 0.6f, b.w - 0.6f)); break;
                case BuildKind.Sheriff: keepOff.Add((1, 5.1f, 6.9f)); break;
            }
            // the four walls: 0 front, 1 left, 2 right, 3 back; origin, direction along, the way into the room
            var walls = new (Vector3 o, Vector3 along, Vector3 into, float len, List<Opening> ops)[]
            {
                (new Vector3(-hw, 0, inner), Vector3.right, Vector3.forward, b.w, front),
                (new Vector3(-hw + inner, 0, 0), Vector3.forward, Vector3.right, b.d, left),
                (new Vector3(hw - inner, 0, 0), Vector3.forward, Vector3.left, b.d, right),
                (new Vector3(-hw, 0, b.d - inner), Vector3.right, Vector3.back, b.w, back),
            };
            for (int wi = 0; wi < 4; wi++)
            {
                var wl = walls[wi];
                float yaw = Mathf.Atan2(wl.into.x, wl.into.z) * Mathf.Rad2Deg;
                // spans of the wall free of doors (with their casings) - the wainscot runs under windows
                var spans = new List<(float a, float b)> { (inner, wl.len - inner) };
                void Cut(float a, float bb)
                {
                    for (int i = spans.Count - 1; i >= 0; i--)
                    {
                        var sp = spans[i];
                        if (bb <= sp.a || a >= sp.b) continue;
                        spans.RemoveAt(i);
                        if (sp.a < a) spans.Insert(i, (sp.a, a));
                        if (bb < sp.b) spans.Insert(Mathf.Min(i + (sp.a < a ? 1 : 0), spans.Count), (bb, sp.b));
                    }
                }
                foreach (Opening o in wl.ops) if (o.y0 <= 0.01f) Cut(o.a - 0.13f, o.b + 0.13f);
                foreach (var k in keepOff) if (k.wall == wi) Cut(k.a, k.b);
                foreach (var sp in spans)
                {
                    float len = sp.b - sp.a;
                    if (len < 0.15f) continue;
                    int n = Mathf.Max(1, Mathf.RoundToInt(len));
                    for (int i = 0; i < n; i++)
                    {
                        float c = sp.a + (i + 0.5f) * len / n;
                        s.Model("in_wainscot", wl.o + wl.along * c + Vector3.up * y, yaw, new Vector3(len / n, 1f, 1f), b.trim, paint);
                    }
                }
                // above the chair rail: paper (or plaster, or boards) up to the ceiling, round the openings
                float y0 = y + 1.0f, y1 = top - 0.04f;
                var cuts = new List<(float a, float b, float lo, float hi)>();
                foreach (Opening o in wl.ops)
                {
                    float lo = o.y0 > 0f ? y + o.y0 - 0.12f : 0f, hi = (o.y0 > 0f ? y : 0f) + o.y1 + (o.y0 > 0f ? 0f : y) + 0.2f;
                    cuts.Add((o.a - 0.13f, o.b + 0.13f, lo, hi));
                }
                cuts.Sort((p, q) => p.a.CompareTo(q.a));
                void Paper(float a, float bb, float lo, float hi)
                {
                    if (bb - a < 0.02f || hi - lo < 0.02f) return;
                    Vector3 c = wl.o + wl.along * ((a + bb) * 0.5f) + wl.into * 0.006f + Vector3.up * ((lo + hi) * 0.5f);
                    Vector3 size = new Vector3(Mathf.Abs(wl.along.x) * (bb - a) + Mathf.Abs(wl.into.x) * 0.012f, hi - lo, Mathf.Abs(wl.along.z) * (bb - a) + Mathf.Abs(wl.into.z) * 0.012f);
                    s.solid.Col(paper).Surface(finish);
                    s.solid.Box(c, size);
                }
                float cur = inner;
                foreach (var c in cuts)
                {
                    Paper(cur, c.a, y0, y1);
                    Paper(c.a, c.b, Mathf.Max(y0, c.hi), y1);
                    if (c.lo > y0) Paper(c.a, c.b, y0, c.lo);
                    cur = Mathf.Max(cur, c.b);
                }
                Paper(cur, wl.len - inner, y0, y1);
                // where the wainscot is kept off (shelves, a rack), the finish comes down to the floor
                foreach (var k in keepOff) if (k.wall == wi) Paper(k.a, k.b, y, y0);
            }

            // the ceiling: boards on joists across the room
            s.solid.Col(b.kind == BuildKind.Saloon ? Pal.H(0x5A4636) : Pal.WoodOld).Surface(Surf.Planks);
            s.solid.Box(new Vector3(0f, top - 0.02f, b.d * 0.5f), new Vector3(b.w - Thick, 0.04f, b.d - Thick));
            s.solid.Col(Pal.WoodDark);
            int joists = Mathf.Max(2, Mathf.RoundToInt(b.d / 1.6f));
            for (int i = 1; i < joists; i++)
                s.solid.Box(new Vector3(0f, top - 0.04f - 0.08f, i * b.d / joists), new Vector3(b.w - Thick, 0.16f, 0.1f));

            // lamps: their light, and the chimney glass that glows after dark
            Color warm = new Color(1f, 0.78f, 0.5f);
            void Glow(Vector3 flame)
            {
                s.glass.Col(Pal.GlassLit);
                s.glass.Cyl(flame + Vector3.down * 0.05f, flame + Vector3.up * 0.1f, 0.035f, 0.03f, 10);
            }
            void Hanging(Vector3 at, float range, float intensity)
            {
                if (!s.Model("in_lamp_hang", at)) return;
                Vector3 f = at + (Models.Get("in_lamp_hang").Mark("flame", out Vector3 m) ? m : new Vector3(0f, -0.76f, 0f));
                Glow(f);
                s.Light(f, warm, range, intensity, true, true);
            }
            void Sconce(Vector3 at, float yaw, bool light)
            {
                if (!s.Model("in_sconce", at, yaw)) return;
                Vector3 m = Models.Get("in_sconce").Mark("flame", out Vector3 mk) ? mk : new Vector3(0f, 0.04f, 0.2f);
                Vector3 f = at + Quaternion.Euler(0f, yaw, 0f) * m;
                Glow(f);
                if (light) s.Light(f, warm, 4.5f, 0.8f, true, true);
            }
            float ceil = top - 0.04f;
            float wide = Mathf.Max(b.w, b.d);
            if (b.kind == BuildKind.Saloon)
            {
                Vector3 at = new Vector3(1.6f, ceil, b.d * 0.55f);
                if (s.Model("in_wheel_lamp", at))
                {
                    var e = Models.Get("in_wheel_lamp");
                    for (int k = 0; k < 4; k++) if (e.Mark("flame" + k, out Vector3 m)) Glow(at + m);
                    s.Light(at + Vector3.down * 1.45f, warm, wide * 1.2f, 3.2f, true, true);      // under the wheel: its shadow goes up
                }
                Hanging(new Vector3(-hw + 3.2f, ceil, b.d * 0.5f), 7f, 1.2f);                  // over the bar
            }
            else
            {
                int n = b.d > 8.6f || b.w > 10f ? 2 : 1;
                for (int i = 0; i < n; i++)
                    Hanging(new Vector3(b.kind == BuildKind.Station ? (i == 0 ? -2.6f : 2.6f) : 0f, ceil,
                                        b.kind == BuildKind.Station ? b.d * 0.45f : b.d * (n == 1 ? 0.45f : (i == 0 ? 0.3f : 0.62f))),
                            wide * (n == 1 ? 1.3f : 1.0f), n == 1 ? 3.0f : 2.0f);
            }

            // the stove stands under the pipe on the roof (or short of the counter), its pipe up to the ceiling
            float px = b.w * 0.24f * (b.id.Length % 2 == 0 ? 1f : -1f), pz = b.d * 0.62f;
            if (b.kind != BuildKind.Saloon && b.kind != BuildKind.Sheriff && Mathf.Abs(pz - cz) < 1.3f) pz = cz - 1.35f;
            if (s.Model("in_stove", new Vector3(px, y, pz), 180f))
            {
                s.Collider(new Vector3(px, y + 0.5f, pz), new Vector3(0.62f, 1.0f, 0.62f));
                float from = y + (Models.Get("in_stove").Mark("pipe", out Vector3 pm) ? pm.y : 1.02f);
                s.Model("in_pipe", new Vector3(px, from, pz), 0f, new Vector3(1f, ceil - from, 1f));
            }

            // on the walls
            void OnWall(string model, int wi, float along, float height, Color32? dye = null, float scale = 1f)
            {
                var wl = walls[wi];
                float yaw = Mathf.Atan2(wl.into.x, wl.into.z) * Mathf.Rad2Deg;
                s.Model(model, wl.o + wl.along * along + wl.into * 0.012f + Vector3.up * height, yaw, Vector3.one * scale, dye);
            }
            void Notice(int wi, float along, float height, string heading)
            {
                OnWall("in_poster", wi, along, height);
                var wl = walls[wi];
                float yaw = Mathf.Atan2(wl.into.x, wl.into.z) * Mathf.Rad2Deg;
                s.Text(heading, wl.o + wl.along * along + wl.into * 0.02f + Vector3.up * (height + 0.21f), Quaternion.Euler(0f, yaw + 180f, 0f), 0.06f, Pal.Ink);
            }
            switch (b.kind)
            {
                case BuildKind.Saloon:
                {
                    // the back bar: a mirror over the bottle shelves either side of a lamp
                    float mid = (2.4f + b.d - 2.2f) * 0.5f;
                    foreach (float dz in new[] { -1.55f, 1.55f })
                        OnWall("in_mirror", 1, mid + dz, y + 2.24f, b.trim, 0.8f);
                    Sconce(walls[1].o + Vector3.forward * mid + Vector3.right * 0.012f + Vector3.up * (y + 2.9f), 90f, true);
                    OnWall("in_painting", 2, 4.7f, y + 2.0f);
                    OnWall("in_portrait", 2, b.d - 3.1f, y + 2.1f);
                    OnWall("in_antlers", 0, b.w * 0.5f, top - 0.85f);
                    OnWall("in_clock", 3, b.w * 0.5f - 2.2f, y + 2.3f);
                    foreach (float z in new[] { 4.25f, 5.95f, 7.6f }) s.Model("in_spittoon", new Vector3(-hw + 2.3f, y, z));      // between the drinkers
                    break;
                }
                case BuildKind.Store:
                    OnWall("in_clock", 2, 4.7f, y + 2.3f);
                    if (s.Model("in_scale", new Vector3(-1.4f, y + 1.04f, cz), 180f)) { }
                    foreach (var (sx, sz, yaw) in new[] { (hw - 1.6f, cz - 0.9f, 20f), (hw - 2.2f, cz - 0.95f, -15f), (hw - 1.9f, cz - 1.55f, 70f) })
                        s.Model("sack", new Vector3(sx, y, sz), yaw);
                    break;
                case BuildKind.Gunsmith:
                    OnWall("in_antlers", 2, 4.7f, y + 2.0f);
                    Notice(0, b.w * 0.5f + 1.3f, y + 1.55f, "ЦЕНЫ");
                    break;
                case BuildKind.Doctor:
                    OnWall("in_painting", 2, 5.2f, y + 1.9f);
                    s.Model("in_rug", new Vector3(-0.4f, y, 3.3f), 90f, Vector3.one, b.trim, Pal.H(0x6B2F2A));
                    break;
                case BuildKind.Office:
                    OnWall("in_portrait", 2, 4.7f, y + 2.0f);
                    OnWall("in_clock", 1, 4.7f, y + 2.2f);
                    s.Model("in_rug", new Vector3(0f, y, cz - 2.1f), 0f, Vector3.one, b.trim, Pal.H(0x2F3F5A));
                    break;
                case BuildKind.Station:
                    OnWall("in_clock", 2, 4.75f, y + 2.2f);
                    Notice(1, 4.2f, y + 1.6f, "РАСПИСАНИЕ");
                    Notice(1, 4.75f, y + 1.6f, "ТЕЛЕГРАФ");
                    break;
                case BuildKind.Sheriff:
                    Notice(1, 3.9f, y + 1.65f, "РАЗЫСКИВАЕТСЯ");
                    Notice(1, 4.45f, y + 1.65f, "РАЗЫСКИВАЕТСЯ");
                    OnWall("in_clock", 3, 1.2f, y + 2.2f);
                    Sconce(walls[3].o + Vector3.right * (b.w * 0.5f - 1.6f) + Vector3.back * 0.012f + Vector3.up * (y + 1.9f), 180f, false);
                    break;
            }
        }

        /// <summary>A building you cannot enter: real walls round a dark core, so its door and windows sit in reveals
        /// like those of the open houses. A tall one has a second storey and a balcony over the porch.</summary>
        static void Facade(Site s, BuildingSpec b, float top, float rise)
        {
            float hw = b.w * 0.5f;
            bool church = b.kind == BuildKind.Church, tall = b.h >= TallFrom;
            s.Box(new Vector3(0, top * 0.5f, b.d * 0.5f), new Vector3(b.w - Thick - 0.02f, top - 0.02f, b.d - Thick - 0.02f), Pal.H(0x1B1814), false);

            float doorW = church ? 1.6f : 1.2f, doorH = church ? 2.5f : 2.35f;
            Opening Light(float c, float sill = 1.0f)
            {
                return church ? new Opening { a = c - 0.45f, b = c + 0.45f, y0 = 0.9f, y1 = 3.2f, glass = true, style = 2 } : Window(c, 1.3f, sill);
            }
            var front = new List<Opening> { Door(hw, doorW, doorH) };
            if (tall) front.Add(new Opening { a = hw - doorW * 0.5f, b = hw + doorW * 0.5f, y0 = Deck, y1 = Upper + 1.35f });
            if (b.w > 5.5f)
                foreach (float c in Bays(doorW * 0.5f + 0.15f, hw - 0.13f, church ? 1.14f : 1.54f))
                    foreach (float e in new[] { -1f, 1f })
                    {
                        front.Add(Light(hw + e * c));
                        if (tall) front.Add(Light(hw + e * c, Upper));
                    }
            var sideL = new List<Opening>(); var sideR = new List<Opening>();
            for (float z = 2.2f; z < b.d - 1.2f; z += church ? 3.2f : 3.6f)
                for (int storey = 0; storey < (tall ? 2 : 1); storey++)
                {
                    sideL.Add(Light(z, storey == 0 ? 1.0f : Upper));
                    sideR.Add(Light(z, storey == 0 ? 1.0f : Upper));
                }
            if (!string.IsNullOrEmpty(b.ghost)) (b.ghostSide >= 0 ? sideR : sideL).Clear();      // a painted ad wants a blank wall
            Wall(s, new Vector3(-hw, 0, 0), true, b.w, top, front, b.wall, b.trim, -1f);
            Wall(s, new Vector3(-hw, 0, 0), false, b.d, top, sideL, b.wall, b.trim, -1f);
            Wall(s, new Vector3(hw, 0, 0), false, b.d, top, sideR, b.wall, b.trim, 1f);
            Wall(s, new Vector3(-hw, 0, b.d), true, b.w, top, new List<Opening>(), b.wall, b.trim, 1f);
            CornerTrim(s, b, top);
            if (!church) Patches(s, b);
            Roof(s, b, rise, top, !church);
            Ghost(s, b, top);
            if (b.porch) Porch(s, b, top);
            else
            {
                // a stoop of two steps before the door
                s.Box(new Vector3(0, Floor * 0.5f, -Thick * 0.5f - 0.45f), new Vector3(doorW + 1.0f, Floor, 0.9f), Pal.WoodGrey);
                s.Box(new Vector3(0, Floor * 0.25f, -Thick * 0.5f - 1.05f), new Vector3(doorW + 1.0f, Floor * 0.5f, 0.3f), Pal.WoodGrey);
            }
            ShutDoor(s, new Vector3(0, Floor, 0), doorW, doorH, b.trim, church);
            if (b.derelict)
            {
                s.solid.Col(Pal.Shade(Pal.WoodGrey, 0.88f));
                s.solid.Beam(new Vector3(-doorW * 0.55f, Floor + 0.5f, -0.12f), new Vector3(doorW * 0.55f, Floor + 1.9f, -0.12f), 0.15f, 0.03f, Vector3.back);
                s.solid.Beam(new Vector3(-doorW * 0.55f, Floor + 1.95f, -0.12f), new Vector3(doorW * 0.55f, Floor + 0.45f, -0.12f), 0.15f, 0.03f, Vector3.back);
            }
            if (tall) ShutDoor(s, new Vector3(0, Floor + Deck, 0), doorW, Upper + 1.35f - Deck, b.trim);
            if (!church) Lantern(s, new Vector3(doorW * 0.5f + 0.3f, Floor + 1.95f, -Thick * 0.5f));
            s.Mark("door", new Vector3(0, b.porch ? Floor : 0f, b.porch ? -PorchDepth - 1.2f : -1.5f), 0f);
            if (church)
            {
                float sw = 2.4f;
                s.Box(new Vector3(0, top + rise + 1.6f, 1.6f), new Vector3(sw, 3.2f + rise, sw), b.wall);
                s.solid.Col(b.roof);
                s.solid.Cyl(new Vector3(0, top + rise * 1.5f + 3.2f, 1.6f), new Vector3(0, top + rise * 1.5f + 6.4f, 1.6f), sw * 0.75f, 0.03f, 4);
                s.solid.Col(Pal.WoodWhite);
                s.solid.Box(new Vector3(0, top + rise * 1.5f + 7.0f, 1.6f), new Vector3(0.1f, 1.2f, 0.1f));
                s.solid.Box(new Vector3(0, top + rise * 1.5f + 7.15f, 1.6f), new Vector3(0.6f, 0.1f, 0.1f));
            }
        }

        /// <summary>
        /// An adobe house (BuildKind.Adobe): mud walls 0.42 m thick and plastered, running up past the flat roof into a
        /// parapet; the ends of the roof beams (vigas) through the front and the back just under the roof; wooden
        /// spouts (canales) through the parapet at the sides; small deep-set windows and a plank door under heavy
        /// lintels; a darker splash band along the foot. A porch is a portal of log posts with carved caps under a
        /// beam and a roof of poles. Chiles hang by the door; a ladder stands against one side. Models: townkit.py.
        /// </summary>
        static void Adobe(Site s, BuildingSpec b)
        {
            const float T = 0.42f;
            float hw = b.w * 0.5f, roof = b.h, top = b.h + 0.5f, face = -T * 0.5f;
            int seed = b.id.Length * 7 + (int)(b.w * 10f);
            Color32 mud = b.wall, splash = Pal.Shade(b.wall, 0.8f), wood = Pal.Shade(Pal.WoodOld, 0.82f);
            SurfaceMap.Hint(mud, Surf.Plaster);
            SurfaceMap.Hint(splash, Surf.Plaster);
            s.Box(new Vector3(0, roof * 0.5f, b.d * 0.5f), new Vector3(b.w - T * 2f, roof - 0.05f, b.d - T * 2f), Pal.H(0x1B1814), false);

            // walls: the door in the middle of the front with a window either side; a window in each side wall
            // the front runs the full width over the ends of the side walls, so openings are measured from its outer corner
            float doorW = 1.05f, doorH = 2.05f, m0 = hw + T;
            var front = new List<Opening> { new Opening { a = m0 - doorW * 0.5f, b = m0 + doorW * 0.5f, y0 = 0f, y1 = doorH, style = 3 } };
            if (b.w > 5.6f)
                foreach (float e in new[] { -1f, 1f })
                {
                    float c = m0 + e * Mathf.Min(b.w * 0.3f, 2.4f);
                    front.Add(new Opening { a = c - 0.42f, b = c + 0.42f, y0 = 0.95f, y1 = 1.95f, glass = true, style = 3 });
                }
            var side = new List<Opening>();
            if (b.d > 5f) side.Add(new Opening { a = b.d * 0.5f - 0.4f, b = b.d * 0.5f + 0.4f, y0 = 1.0f, y1 = 1.9f, glass = true, style = 3 });
            Wall(s, new Vector3(-hw - T, 0, 0), true, b.w + T * 2f, top, front, mud, wood, -1f, 0f, T);
            Wall(s, new Vector3(-hw - T * 0.5f, 0, 0), false, b.d, top, side, mud, wood, -1f, 0f, T);
            Wall(s, new Vector3(hw + T * 0.5f, 0, 0), false, b.d, top, side, mud, wood, 1f, 0f, T);
            Wall(s, new Vector3(-hw - T, 0, b.d), true, b.w + T * 2f, top, new List<Opening>(), mud, wood, 1f, 0f, T);
            foreach (Opening o in front) AdobeOpening(s, new Vector3(-m0 + (o.a + o.b) * 0.5f, 0f, face), Vector3.right, Vector3.back, o, wood);
            foreach (float e in new[] { -1f, 1f })
                foreach (Opening o in side)
                    AdobeOpening(s, new Vector3(e * (hw + T), 0f, (o.a + o.b) * 0.5f), Vector3.forward, new Vector3(e, 0f, 0f), o, wood);

            // the splash band along the foot, round the door
            foreach (float e in new[] { -1f, 1f })
                s.Box(new Vector3(e * (m0 + doorW * 0.5f) * 0.5f, 0.21f, face - 0.012f), new Vector3(m0 - doorW * 0.5f, 0.42f, 0.03f), splash, false);
            foreach (float e in new[] { -1f, 1f })
                s.Box(new Vector3(e * (hw + T + 0.012f), 0.21f, b.d * 0.5f), new Vector3(0.03f, 0.42f, b.d + T), splash, false);
            // the flat roof inside the parapet, the parapet's top worn round
            s.Box(new Vector3(0, roof, b.d * 0.5f), new Vector3(b.w, 0.12f, b.d), Pal.Shade(mud, 0.7f), false);
            s.solid.Col(Pal.Shade(mud, 1.06f)).Surface(Surf.Plaster);
            foreach (float z in new[] { 0f, b.d })
                s.solid.Cyl(new Vector3(-hw - T, top, z), new Vector3(hw + T, top, z), T * 0.5f, T * 0.5f, 7, false, false);
            foreach (float x in new[] { -hw - T * 0.5f, hw + T * 0.5f })
                s.solid.Cyl(new Vector3(x, top, 0f), new Vector3(x, top, b.d), T * 0.5f, T * 0.5f, 7, false, false);

            // vigas through the front and the back, canales through the side parapets
            var rnd = new System.Random(seed);
            float R() { return (float)rnd.NextDouble(); }

            // years of rain: corners slumped round and fat at the foot, the parapet worn into lumps and dips
            s.solid.Col(mud).Surface(Surf.Plaster);
            foreach (float cx in new[] { -hw - T * 0.5f, hw + T * 0.5f })
                foreach (float cz in new[] { 0f, b.d })
                    s.solid.Cyl(new Vector3(cx, 0f, cz), new Vector3(cx, top - 0.05f, cz), T * 0.66f, T * 0.54f, 9, false, true);
            int lumps = 3 + (int)(R() * 4f);
            for (int i = 0; i < lumps; i++)
            {
                bool onFront = R() < 0.5f;
                Vector3 at = onFront ? new Vector3((R() - 0.5f) * b.w, top, 0f) : new Vector3((R() < 0.5f ? -1f : 1f) * (hw + T * 0.5f), top, R() * b.d);
                Vector3 radii = onFront ? new Vector3(0.35f + R() * 0.5f, 0.07f + R() * 0.07f, T * 0.48f) : new Vector3(T * 0.48f, 0.07f + R() * 0.07f, 0.35f + R() * 0.5f);
                s.solid.Col(Pal.Shade(mud, 0.95f + R() * 0.1f)).Surface(Surf.Plaster);
                s.solid.Ellipsoid(at, radii, 8, 4);
            }
            int n = Mathf.Max(2, Mathf.RoundToInt((b.w - 0.8f) / 0.95f));
            for (int i = 0; i < n; i++)
            {
                float x = -hw + 0.4f + (i + 0.5f) * (b.w - 0.8f) / n;
                Vector3 sc = new Vector3(0.9f + R() * 0.2f, 0.9f + R() * 0.2f, 0.75f + R() * 0.45f);
                s.Model("ab_viga", new Vector3(x, roof - 0.16f, face), (R() - 0.5f) * 4f, sc);
                s.Model("ab_viga", new Vector3(x, roof - 0.16f, b.d + T * 0.5f), 180f + (R() - 0.5f) * 4f, sc);
            }
            foreach (float e in new[] { -1f, 1f })
            {
                float z = b.d * (e < 0f ? 0.35f : 0.65f);
                s.Model("ab_canale", new Vector3(e * (hw + T), roof + 0.1f, z), Quaternion.Euler(0f, e < 0f ? 90f : -90f, 0f) * Quaternion.Euler(-7f, 0f, 0f), Vector3.one);
            }

            // the door, recessed in its opening, and the chiles by it
            ShutDoor(s, new Vector3(0, 0f, face + 0.14f), doorW, doorH, Pal.H(0x5C4330));
            s.Model("ab_ristra", new Vector3(doorW * 0.5f + 0.32f, doorH + 0.1f, face - 0.03f), 0f, Vector3.one);
            if (seed % 3 == 0) s.Model("ab_ristra", new Vector3(-doorW * 0.5f - 0.3f, doorH + 0.05f, face - 0.03f), 25f, new Vector3(1f, 0.85f, 1f));
            if (seed % 2 == 0 && Models.Has("ab_ladder"))
            {
                s.Model("ab_ladder", new Vector3(-hw - T, 0f, b.d * 0.62f), 90f, new Vector3(1f, top / 3.2f, 1f));
                s.Collider(new Vector3(-hw - T - 0.5f, top * 0.5f, b.d * 0.62f), new Vector3(0.9f, top, 0.6f));
            }

            if (b.porch)
            {
                // the portal: log posts with carved caps, a beam along them, a roof of poles sloping off the wall
                float pz = -2.3f, postH = 2.35f;
                int posts = Mathf.Max(2, Mathf.RoundToInt(b.w / 2.8f) + 1);
                for (int i = 0; i < posts; i++)
                {
                    float x = Mathf.Lerp(-hw + 0.25f, hw - 0.25f, i / (float)(posts - 1));
                    if (Mathf.Abs(x) < doorW * 0.5f + 0.25f) x = Mathf.Sign(x == 0f ? 1f : x) * (doorW * 0.5f + 0.35f);
                    if (!s.Model("ab_post", new Vector3(x, 0f, pz), R() * 360f, new Vector3(1f, postH / 2.6f, 1f)))
                        s.Box(new Vector3(x, postH * 0.5f, pz), new Vector3(0.2f, postH, 0.2f), wood, false);
                    s.Model("ab_zapata", new Vector3(x, postH, pz), 0f, Vector3.one);
                    s.Collider(new Vector3(x, postH * 0.5f, pz), new Vector3(0.22f, postH, 0.22f));
                }
                s.Box(new Vector3(0, postH + 0.36f, pz), new Vector3(b.w + 0.3f, 0.2f, 0.22f), wood, false);
                float run = -pz - face + 0.4f, drop = Mathf.Max(0.05f, roof - 0.3f - (postH + 0.46f));
                s.BoxRot(new Vector3(0, postH + 0.5f + drop * 0.5f, (pz + face) * 0.5f - 0.1f), new Vector3(b.w + 0.5f, 0.07f, run),
                         Quaternion.Euler(-Mathf.Atan2(drop, run) * Mathf.Rad2Deg, 0f, 0f), Pal.Shade(Pal.WoodGrey, 0.85f), false);
                for (float x = -hw; x <= hw + 0.01f; x += 0.75f)
                    s.Model("ab_viga", new Vector3(x, postH + 0.36f, pz - 0.05f), 0f, new Vector3(0.55f, 0.55f, 0.5f));
            }
            s.Mark("door", new Vector3(0, 0f, b.porch ? -3.1f : -1.6f), 0f);
        }

        /// <summary>The joinery of an opening in a thick mud wall: a heavy lintel over it, and for a window a frame set
        /// back in the wall with its panes and a board sill. 'mid' is the middle of the opening on the wall's outer face
        /// at ground level; 'along' runs with the wall, 'outward' points out of it.</summary>
        static void AdobeOpening(Site s, Vector3 mid, Vector3 along, Vector3 outward, Opening o, Color32 wood)
        {
            float ow = o.b - o.a, oh = o.y1 - o.y0;
            bool alongX = Mathf.Abs(along.x) > 0.5f;
            Vector3 Size(float l, float h, float t) { return alongX ? new Vector3(l, h, t) : new Vector3(t, h, l); }
            s.Box(mid + Vector3.up * (o.y1 + 0.09f) + outward * 0.03f - outward * 0.1f, Size(ow + 0.5f, 0.18f, 0.26f), wood, false);
            if (!o.glass) return;
            Vector3 pane = mid + Vector3.up * ((o.y0 + o.y1) * 0.5f) - outward * 0.16f;
            s.solid.Col(Pal.Shade(Pal.WoodDark, 1.05f));
            foreach (float e in new[] { -1f, 1f })
            {
                s.solid.Box(pane + along * (e * (ow * 0.5f - 0.03f)), Size(0.06f, oh, 0.08f));
                s.solid.Box(pane + Vector3.up * (e * (oh * 0.5f - 0.03f)), Size(ow, 0.06f, 0.08f));
            }
            s.solid.Box(pane, Size(0.035f, oh, 0.05f));
            s.solid.Box(pane, Size(ow, 0.035f, 0.05f));
            s.Box(mid + Vector3.up * (o.y0 - 0.03f) + outward * 0.06f - outward * 0.1f, Size(ow + 0.16f, 0.06f, 0.3f), wood, false);
            s.glass.Col(Pal.Glass, 0.9f);
            s.glass.Box(pane + outward * 0.01f, Size(ow - 0.08f, oh - 0.08f, 0.01f));
            s.Collider(pane, Size(ow, oh, 0.1f));
        }

        /// <summary>Livery barn: wide open front, stalls along both sides, hay in the back.</summary>
        static void Stable(Site s, BuildingSpec b)
        {
            float top = 4.2f, hw = b.w * 0.5f, rise = 2.6f, doorTop = 3.6f;
            var front = new List<Opening> { new Opening { a = hw - 2.2f, b = hw + 2.2f, y0 = 0f, y1 = doorTop } };
            Wall(s, new Vector3(-hw, 0, 0), true, b.w, top, front, b.wall, b.trim, -1f, 0f);
            // a small light over every stall
            var sideL = new List<Opening>(); var sideR = new List<Opening>();
            for (float z = 3f; z < b.d - 2.5f; z += 3.2f)
            {
                var light = new Opening { a = z - 0.4f, b = z + 0.4f, y0 = 1.85f, y1 = 2.55f, glass = true, style = 1 };
                sideL.Add(light); sideR.Add(light);
            }
            Wall(s, new Vector3(-hw, 0, 0), false, b.d, top, sideL, b.wall, b.trim, -1f, 0f);
            Wall(s, new Vector3(hw, 0, 0), false, b.d, top, sideR, b.wall, b.trim, 1f, 0f);
            Wall(s, new Vector3(-hw, 0, b.d), true, b.w, top, new List<Opening>(), b.wall, b.trim, 1f, 0f);
            s.Box(new Vector3(0, 0.03f, b.d * 0.5f), new Vector3(b.w, 0.06f, b.d), Pal.DirtDark, false);
            var spec = new BuildingSpec { w = b.w, d = b.d, wall = b.wall, trim = b.trim, roof = b.roof, falseFront = false };
            Roof(s, spec, rise, top, false);
            // the barn doors stand open against the wall; the hay loft door under its hoist beam
            float face = -Thick * 0.5f;
            foreach (float e in new[] { -1f, 1f })
                s.Model("bk_barndoor", new Vector3(e * 2.2f, 0f, face - 0.075f), e < 0f ? 174f : 6f, new Vector3(1f, doorTop / 3.3f, 1f), b.trim, b.wall);
            if (!s.Model("bk_barndoor", new Vector3(-0.7f, top + 0.35f, face - 0.045f), 0f, new Vector3(1.4f / 2.2f, 1.3f / 3.3f, 1f), b.trim, b.wall))
                s.Box(new Vector3(0, top + 1.0f, -0.06f), new Vector3(1.4f, 1.3f, 0.06f), b.trim, false);
            s.Box(new Vector3(0, top + 1.95f, -0.5f), new Vector3(0.12f, 0.14f, 1.0f), b.trim, false);
            s.solid.Col(Pal.Iron, 0.4f);
            s.solid.Box(new Vector3(0, top + 1.8f, -0.9f), new Vector3(0.05f, 0.18f, 0.12f));
            if (b.sign != "") SignBoard(s, b.sign, new Vector3(0, doorTop + 0.57f, -0.2f), b.sign.Length * 0.27f + 0.9f, 0.6f, 0.46f, b.trim);
            int stall = 0;
            for (float z = 3f; z < b.d - 2.5f; z += 3.2f)
                foreach (float side in new[] { -1f, 1f })
                {
                    s.Box(new Vector3(side * (hw - 1.6f), 0.7f, z + 1.6f), new Vector3(3.0f, 1.4f, 0.1f), Pal.WoodGrey);
                    s.Mark("stall" + stall++, new Vector3(side * (hw - 1.7f), 0f, z), side < 0 ? 90f : 270f);
                }
            Props.HayBale(s, new Vector3(-hw + 1.0f, 0, b.d - 0.8f), 0f);
            Props.HayBale(s, new Vector3(-hw + 2.1f, 0, b.d - 0.8f), 0f);
            Props.HayBale(s, new Vector3(-hw + 1.5f, 0.6f, b.d - 0.8f), 0f);
            Props.Barrel(s, new Vector3(hw - 0.7f, 0, 1.0f));
            s.Mark("keeper", new Vector3(hw - 1.6f, 0f, 1.8f), 225f);
            s.Mark("counter", new Vector3(hw - 2.6f, 0f, 0.6f), 45f);
            s.Mark("door", new Vector3(0, 0f, -2.5f), 0f);
            s.Mark("inside", new Vector3(0, 0f, 3f), 0f);
            s.Light(new Vector3(0, 3.4f, b.d * 0.5f), new Color(1f, 0.78f, 0.5f), 14f, 1.3f, true);
        }

        /// <summary>Open-sided forge under a lean-to roof.</summary>
        static void Smithy(Site s, BuildingSpec b)
        {
            float hw = b.w * 0.5f;
            s.Box(new Vector3(0, 0.03f, b.d * 0.5f), new Vector3(b.w, 0.06f, b.d), Pal.DirtDark, false);
            Wall(s, new Vector3(-hw, 0, b.d), true, b.w, 3.4f, new List<Opening>(), b.wall, b.trim, 1f, 0f);
            Wall(s, new Vector3(-hw, 0, b.d * 0.5f), false, b.d * 0.5f, 3.4f, new List<Opening>(), b.wall, b.trim, -1f, 0f);
            foreach (float x in new[] { -hw + 0.1f, hw - 0.1f }) Post(s, "bk_post_plain", new Vector3(x, 0f, 0.1f), 3f, 0f, b.trim, 0.18f);
            Post(s, "bk_post_plain", new Vector3(hw - 0.1f, 0f, b.d - 0.1f), 3.2f, 0f, b.trim, 0.18f);
            s.Box(new Vector3(0, 2.91f, 0.1f), new Vector3(b.w, 0.14f, 0.12f), b.trim, false);                     // the beam over the open front
            s.BoxRot(new Vector3(0, 3.3f, b.d * 0.5f), new Vector3(b.w + 0.6f, 0.1f, b.d + 0.8f), Quaternion.Euler(-5f, 0, 0), b.roof);
            // forge and anvil
            s.Box(new Vector3(-hw + 1.2f, 0.5f, b.d - 1.0f), new Vector3(1.6f, 1.0f, 1.2f), Pal.Brick);
            s.Box(new Vector3(-hw + 1.2f, 2.2f, b.d - 0.8f), new Vector3(1.0f, 2.4f, 0.8f), Pal.Brick, false);
            s.glass.Col(Pal.GlassLit);
            s.glass.Box(new Vector3(-hw + 1.2f, 1.03f, b.d - 1.0f), new Vector3(0.9f, 0.06f, 0.7f));
            s.Light(new Vector3(-hw + 1.2f, 1.5f, b.d - 1.4f), new Color(1f, 0.5f, 0.2f), 7f, 2.2f, true, true);
            s.Smoke(Plume.Forge, new Vector3(-hw + 1.2f, 1.08f, b.d - 1.0f));
            s.Smoke(Plume.Chimney, new Vector3(-hw + 1.2f, 3.75f, b.d - 0.8f));
            s.Box(new Vector3(0.6f, 0.3f, b.d * 0.5f), new Vector3(0.4f, 0.6f, 0.4f), Pal.Bark);
            s.Box(new Vector3(0.6f, 0.7f, b.d * 0.5f), new Vector3(0.24f, 0.2f, 0.6f), Pal.Iron, false, 0.6f);
            Props.Barrel(s, new Vector3(hw - 0.8f, 0, b.d - 0.8f));
            s.Mark("keeper", new Vector3(0.6f, 0f, b.d * 0.5f + 0.9f), 180f);
            s.Mark("counter", new Vector3(0.6f, 0f, -0.6f), 0f);
            s.Mark("door", new Vector3(0, 0f, -2f), 0f);
            // the sign hangs under the eave
            if (b.sign != "") SignBoard(s, b.sign, new Vector3(0, 2.67f, -0.3f), b.sign.Length * 0.27f + 0.7f, 0.46f, 0.42f, b.trim);
        }

        /// <summary>Butcher's stall: a table under an awning with meat on hooks.</summary>
        static void Butcher(Site s, BuildingSpec b)
        {
            float hw = b.w * 0.5f;
            // the awning falls to the front: short posts there, tall ones behind, a pole across the front for the sign
            foreach (float x in new[] { -hw, hw })
                foreach (float z in new[] { 0f, b.d })
                    Post(s, "bk_post_plain", new Vector3(x, 0f, z), z == 0f ? 2.6f : 3.02f, 0f, b.trim, 0.14f);
            s.Box(new Vector3(0, 2.53f, 0f), new Vector3(b.w, 0.08f, 0.08f), b.trim, false);
            s.BoxRot(new Vector3(0, 2.85f, b.d * 0.5f), new Vector3(b.w + 0.6f, 0.08f, b.d + 0.7f), Quaternion.Euler(-8f, 0, 0), Pal.ClothRed);
            s.Box(new Vector3(0, 0.45f, 0.7f), new Vector3(b.w - 0.6f, 0.9f, 0.8f), Pal.WoodOld);
            s.solid.Col(Pal.H(0x8E3A32));
            s.solid.Ellipsoid(new Vector3(-0.6f, 1.0f, 0.7f), new Vector3(0.3f, 0.1f, 0.2f), 8, 4);
            s.solid.Ellipsoid(new Vector3(0.5f, 1.0f, 0.75f), new Vector3(0.22f, 0.09f, 0.26f), 8, 4);
            s.solid.Col(b.trim);
            s.solid.Beam(new Vector3(-hw, 2.5f, b.d - 0.4f), new Vector3(hw, 2.5f, b.d - 0.4f), 0.06f, 0.06f);
            s.solid.Col(Pal.H(0x8E3A32));
            for (int i = 0; i < 3; i++)
                s.solid.Ellipsoid(new Vector3(-0.9f + i * 0.9f, 1.95f, b.d - 0.4f), new Vector3(0.16f, 0.5f, 0.14f), 8, 5);
            s.Mark("keeper", new Vector3(0, 0f, 1.7f), 180f);
            s.Mark("counter", new Vector3(0, 0f, -0.7f), 0f);
            s.Mark("door", new Vector3(0, 0f, -1.8f), 0f);
            if (b.sign != "") SignBoard(s, b.sign, new Vector3(0, 2.23f, -0.02f), b.sign.Length * 0.27f + 0.7f, 0.42f, 0.4f, b.trim);
        }
    }
}
