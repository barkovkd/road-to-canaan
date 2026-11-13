using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>The settled places of region 1: the town of Сухой Брод, the wagon camp, the ranch, the homesteads.</summary>
    public static partial class RegionDryFord
    {
        static void Place(Region r, BuildingSpec spec, float x, float z, float yaw)
        {
            Site s = Architect.Build(spec);
            s.Finish(r, "B_" + spec.id, OnLand(x, z), yaw, spec.id + "_");
            float d = spec.d + (spec.porch ? 1f : 0f);
            bool flip = Mathf.Abs(Mathf.DeltaAngle(yaw, 180f)) < 1f;
            mapBuildings.Add(new Rect(x - spec.w * 0.5f, flip ? z - d : z, spec.w, d));
            noGrass.Add(new Rect(x - spec.w * 0.5f - 1.5f, (flip ? z - spec.d : z) - 4f, spec.w + 3f, spec.d + 8f));
        }

        static Vector3 L(float x, float z, float up = 0f) { Vector3 p = OnLand(x, z); p.y += up; return p; }

        static void BuildPlaces(Region r)
        {
            mapBuildings.Clear();
            noGrass.Clear();
            GroundMarks.Clear();
            BuildTown(r);
            BuildRailway(r);
            BuildCamp(r);
            BuildRanch(r);
            BuildOutskirts(r);
            GroundMarks.Finish(r);

            r.Mark("menu", OnLand(Town.x - 10f, Town.y));
            r.respawn = r.P("camp_bed");
            Vector3 camp = r.P("camp_fire");
            Blip.Add(() => camp, "tent", UI.Paper, "Лагерь", true, null, 24f);
            (string key, string icon, string label)[] shops =
            {
                ("saloon_door", "house", "Салун"), ("store_door", "house", "Лавка"), ("gunsmith_door", "house", "Оружейник"), ("doctor_door", "cross", "Доктор"),
                ("stable_door", "house", "Конюшня"), ("sheriff_door", "star", "Шериф"), ("station_door", "house", "Станция и почта"), ("butcher_door", "house", "Мясник"),
            };
            foreach (var sh in shops)
            {
                Vector3 p = r.P(sh.key);
                Blip.Add(() => p, sh.icon, UI.Muted, sh.label, false, null, 18f);
            }
        }

        // ================================================================== town

        /// <summary>The main street is laid out in two parts on each side, west and east of the side street (CrossX0 to
        /// CrossX1) that runs north to the church; each building follows the one before it after its gap.</summary>
        const float CrossX0 = 864f, CrossX1 = 879f;
        static float CrossX => (CrossX0 + CrossX1) * 0.5f;

        /// <summary>Planks painted in a colour that is not one of the palette's: still a wooden wall.</summary>
        static Color32 Paint(int hex) { Color32 c = Pal.H(hex); SurfaceMap.Hint(c, Surf.Planks); return c; }

        static BuildingSpec B(BuildKind kind, string id, string sign, float w, float d, Color32 wall, Color32 trim) { return new BuildingSpec { kind = kind, id = id, sign = sign, w = w, d = d, wall = wall, trim = trim }; }

        /// <summary>Lays a row out from 'x' (west to east): (spec, gap after it). Returns each building's middle.</summary>
        static void Row(Region r, (BuildingSpec b, float gap)[] row, float x, float z, bool south, Dictionary<string, float> xs, List<(BuildingSpec b, float x0, float x1, bool south)> lots)
        {
            foreach (var (b, gap) in row)
            {
                float mid = x + b.w * 0.5f;
                Place(r, b, mid, z, south ? 180f : 0f);
                xs[b.id] = mid;
                lots.Add((b, x, x + b.w, south));
                x += b.w + gap;
            }
        }

        static void BuildTown(Region r)
        {
            float northZ = Town.y + 9.5f, southZ = Town.y - 9.5f;
            var xs = new Dictionary<string, float>();
            var lots = new List<(BuildingSpec b, float x0, float x1, bool south)>();

            // A railhead town of the desert, after the towns of the old Southwest: false fronts shoulder to shoulder
            // along the main street, a two-storey saloon and hotel by the depot, a brick bank on the corner of the
            // side street that climbs to the church, an adobe quarter where the street runs out east. Yards behind.
            Color32 undertaker = Paint(0x4F4B45), rooms = Paint(0xC9B98F), laundry = Paint(0xC8C2B0);
            Row(r, new (BuildingSpec, float)[]
            {
                (new BuildingSpec { kind = BuildKind.Facade, id = "hotel", sign = "ГОСТИНИЦА", w = 12f, d = 10f, h = 5.6f, wall = Pal.WoodWhite, trim = Pal.WoodBlue, front = 2 }, 1.4f),
                (new BuildingSpec { kind = BuildKind.Saloon, id = "saloon", sign = "САЛУН «ПОДКОВА»", w = 14f, d = 13f, h = 5.6f, wall = Pal.WoodRed, trim = Pal.WoodDark, front = 1, painted = true }, 1.4f),
                (new BuildingSpec { kind = BuildKind.Facade, id = "barber", sign = "ЦИРЮЛЬНЯ", w = 6f, d = 8f, wall = Pal.WoodBlue, trim = Pal.WoodWhite, painted = true }, 1.0f),
                (new BuildingSpec { kind = BuildKind.Store, id = "store", sign = "ЛАВКА ПИБОДИ", w = 10.5f, d = 10f, wall = Pal.WoodYellow, trim = Pal.WoodDark, front = 2 }, 2.2f),
                (new BuildingSpec { kind = BuildKind.Gunsmith, id = "gunsmith", sign = "ОРУЖЕЙНИК", w = 8.5f, d = 9f, wall = Pal.WoodGrey, trim = Pal.WoodDark, front = 1 }, 1.0f),
                (new BuildingSpec { kind = BuildKind.Facade, id = "paper", sign = "ГАЗЕТА", w = 7f, d = 8f, wall = Pal.WoodGreen, trim = Pal.WoodWhite, painted = true, derelict = true }, 2.4f),
                (B(BuildKind.Doctor, "doctor", "ДОКТОР", 7.5f, 8.5f, Pal.WoodWhite, Pal.WoodGreen), 1.0f),
                (new BuildingSpec { kind = BuildKind.Office, id = "office", sign = "ЗЕМЕЛЬНАЯ КОМПАНИЯ", w = 9f, d = 9f, wall = Pal.WoodGreen, trim = Pal.WoodWhite, front = 2,
                                    ghost = "ЗЕМЛЯ У ДОРОГИ|ДОЛЛАР ЗА АКР", ghostSide = 1 }, 0f),
            }, 779f, northZ, false, xs, lots);
            Row(r, new (BuildingSpec, float)[]
            {
                (new BuildingSpec { kind = BuildKind.Facade, id = "bank", sign = "БАНК", w = 10f, d = 10f, h = 4.6f, wall = Pal.Brick, trim = Pal.Stone, front = 1,
                                    ghost = "ТАБАК «КАПИТАН»|ЛУЧШИЙ НА ЗАПАДЕ", ghostSide = -1 }, 1.4f),
                (new BuildingSpec { kind = BuildKind.Facade, id = "undertaker", sign = "ГРОБОВЩИК", w = 6.5f, d = 8f, wall = undertaker, trim = Pal.WoodGrey, painted = true }, 1.2f),
                (new BuildingSpec { kind = BuildKind.Facade, id = "rooms", sign = "КОМНАТЫ ВНАЁМ", w = 11f, d = 10f, h = 5.6f, wall = rooms, trim = Pal.WoodRed, painted = true }, 2.5f),
                (new BuildingSpec { kind = BuildKind.Facade, id = "house_n1", w = 7f, d = 8f, falseFront = false, wall = Pal.WoodOld, trim = Pal.WoodWhite }, 3f),
                (new BuildingSpec { kind = BuildKind.Facade, id = "house_n2", w = 7.5f, d = 7f, falseFront = false, wall = Pal.WoodBlue, trim = Pal.WoodWhite }, 3.5f),
                (new BuildingSpec { kind = BuildKind.Adobe, id = "adobe_n1", w = 9f, d = 7f, h = 3.1f, wall = Pal.AdobeLight }, 4f),
                (new BuildingSpec { kind = BuildKind.Adobe, id = "adobe_n2", w = 7f, d = 6f, h = 3.0f, wall = Pal.Adobe, porch = false }, 0f),
            }, CrossX1, northZ, false, xs, lots);
            Row(r, new (BuildingSpec, float)[]
            {
                (B(BuildKind.Sheriff, "sheriff", "ШЕРИФ", 9.5f, 9f, Pal.Stone, Pal.WoodDark), 1.5f),
                (new BuildingSpec { kind = BuildKind.Facade, id = "laundry", sign = "ПРАЧЕЧНАЯ", w = 6f, d = 7f, wall = laundry, trim = Pal.WoodRed, painted = true }, 1.2f),
                (new BuildingSpec { kind = BuildKind.Facade, id = "feed", sign = "ЗЕРНО И КОРМ", w = 9f, d = 9f, wall = Pal.WoodOld, trim = Pal.WoodRed, front = 1 }, 2f),
                (new BuildingSpec { kind = BuildKind.Smithy, id = "smithy", sign = "КУЗНИЦА", w = 7f, d = 6f, wall = Pal.WoodOld, trim = Pal.WoodDark, porch = false }, 2f),
                (new BuildingSpec { kind = BuildKind.Butcher, id = "butcher", sign = "МЯСО", w = 5f, d = 3f, trim = Pal.WoodDark, porch = false }, 3f),
                (new BuildingSpec { kind = BuildKind.Stable, id = "stable", sign = "КОНЮШНЯ ХАРПЕРА", w = 12f, d = 14f, wall = Pal.WoodRed, trim = Pal.WoodWhite, porch = false }, 1.6f),
                (new BuildingSpec { kind = BuildKind.Adobe, id = "cantina", sign = "КАНТИНА", w = 11f, d = 9f, h = 3.4f, wall = Pal.AdobePink }, 0f),
            }, 789f, southZ, true, xs, lots);
            Row(r, new (BuildingSpec, float)[]
            {
                (new BuildingSpec { kind = BuildKind.Facade, id = "house_s1", w = 7f, d = 7.5f, falseFront = false, wall = Pal.WoodYellow, trim = Pal.WoodDark }, 3f),
                (new BuildingSpec { kind = BuildKind.Facade, id = "house_s2", w = 8f, d = 8f, falseFront = false, wall = Pal.WoodGrey, trim = Pal.WoodRed }, 3.5f),
                (new BuildingSpec { kind = BuildKind.Adobe, id = "adobe_s1", w = 8.5f, d = 7f, h = 3.1f, wall = Pal.Adobe }, 3.5f),
                (new BuildingSpec { kind = BuildKind.Adobe, id = "adobe_s2", w = 7f, d = 6f, h = 2.9f, wall = Pal.Whitewash, porch = false }, 3f),
                (new BuildingSpec { kind = BuildKind.Adobe, id = "adobe_s3", w = 9f, d = 7.5f, h = 3.1f, wall = Pal.AdobeLight }, 4f),
                (new BuildingSpec { kind = BuildKind.Adobe, id = "adobe_s4", w = 7f, d = 6f, h = 3.0f, wall = Pal.AdobePink, porch = false }, 0f),
            }, CrossX1, southZ, true, xs, lots);
            // the church at the head of the side street, looking down it to the main street
            Place(r, new BuildingSpec { kind = BuildKind.Church, id = "church", w = 8f, d = 14f, h = 4.4f, falseFront = false, porch = false, wall = Pal.WoodWhite, trim = Pal.WoodGrey },
                  CrossX, Town.y + 40f, 0f);

            var s = new Site();
            float py = Architect.Floor;
            StreetFurniture(s, xs, northZ, southZ, py);
            Boardwalks(s, lots, northZ, southZ);
            BackLots(s, lots, northZ, southZ);
            BuildGround(r, s, lots, xs, northZ, southZ);

            // corral behind the stable, a wagon waiting by it, hay stacked on the other side
            float cx = xs["stable"], cz = southZ - 15f;
            Props.Wagon(s, L(cx - 15.5f, cz - 6f), 18f, true);
            for (int i = 0; i < 4; i++)
                if (s.Model("haybale", L(cx + 13.8f + (i % 2) * 1.1f, cz - 3f - (i / 2) * 1.6f, i == 3 ? 0.5f : 0f), 90f + i * 7f) && i < 3)
                    s.Collider(L(cx + 13.8f + (i % 2) * 1.1f, cz - 3f - (i / 2) * 1.6f, 0.25f), new Vector3(1.0f, 0.5f, 1.4f));
            var corral = new List<Vector3>
            {
                L(cx - 12f, cz), L(cx - 12f, cz - 18f), L(cx + 12f, cz - 18f), L(cx + 12f, cz), L(cx + 6f, cz), L(cx - 6f, cz), L(cx - 12f, cz)
            };
            Props.Fence(s, corral, 1.3f, 3, new HashSet<int> { 4 });
            s.Mark("stable_corral", L(cx, cz - 9f));

            // signpost at the western entrance
            Vector3 sp = L(771f, Town.y + 7.5f);
            if (!s.Model("signpost", sp))
            {
                s.solid.Col(Pal.WoodDark);
                s.solid.Box(sp + Vector3.up * 1.2f, new Vector3(0.14f, 2.4f, 0.14f));
                s.Box(sp + Vector3.up * 2.1f, new Vector3(2.6f, 0.7f, 0.08f), Pal.Shade(Pal.Parchment, 0.9f), false);
            }
            s.Mark("town_sign", sp);
            s.Text("СУХОЙ БРОД", sp + new Vector3(0, 2.18f, -0.06f), Quaternion.Euler(0, 0, 0), 0.42f, new Color32(0x2A, 0x20, 0x18, 255));
            s.Text("жителей — 86", sp + new Vector3(0, 1.9f, -0.06f), Quaternion.identity, 0.22f, new Color32(0x4A, 0x3A, 0x2A, 255));
            s.Text("СУХОЙ БРОД", sp + new Vector3(0, 2.1f, 0.06f), Quaternion.Euler(0, 180f, 0), 0.42f, new Color32(0x2A, 0x20, 0x18, 255));

            // places to stand around: along the boardwalks on both sides
            for (int i = 0; i < 10; i++)
            {
                float x = 808f + i * 15f;
                s.Mark("town_spot" + i, L(x + (i % 3) * 2f, Town.y + (i % 2 == 0 ? 7.6f : -7.6f)), i % 2 == 0 ? 180f : 0f);
            }
            s.Mark("town_west", L(782f, Town.y));
            s.Mark("town_east", L(980f, Town.y));

            // cemetery on its rise
            var fence = new List<Vector3>();
            for (int i = 0; i <= 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                fence.Add(L(Cemetery.x + Mathf.Cos(a) * 14f, Cemetery.y + Mathf.Sin(a) * 11f));
            }
            Props.Fence(s, fence, 1.0f, 2, new HashSet<int> { 9 }, Pal.WoodWhite);
            var rng = new System.Random(5);
            for (int i = 0; i < 14; i++)
                Props.Grave(s, L(Cemetery.x - 9f + (i % 7) * 3f, Cemetery.y - 3f + (i / 7) * 6f), 180f + (float)rng.NextDouble() * 12f - 6f, rng.NextDouble() < 0.6);
            s.Mark("cemetery", L(Cemetery.x, Cemetery.y - 8f));
            s.Finish(r, "TownProps", Vector3.zero, 0f);
        }

        /// <summary>Hitching rails, troughs, lamps, the well where the side street crosses, the telegraph line along the
        /// south side, benches and chairs on the porches, goods stacked by the stores, wanted bills, the barber's pole,
        /// coffins by the undertaker's door, wagons waiting, droppings in the street.</summary>
        static void StreetFurniture(Site s, Dictionary<string, float> xs, float northZ, float southZ, float py)
        {
            float nRail = northZ - 3.6f, sRail = southZ + 3.6f;
            Props.HitchRail(s, L(xs["saloon"] - 3.6f, nRail), 0f, 3.2f, "saloon_hitch");
            Props.HitchRail(s, L(xs["saloon"] + 3.6f, nRail), 0f, 3.2f);
            Props.HitchRail(s, L(xs["store"] + 2.5f, nRail), 0f, 3f, "store_hitch");
            Props.HitchRail(s, L(xs["hotel"] - 2.8f, nRail), 0f, 3f);
            Props.HitchRail(s, L(xs["gunsmith"] + 1.5f, nRail), 0f, 2.6f);
            Props.HitchRail(s, L(xs["bank"] + 2.6f, nRail), 0f, 3f);
            Props.HitchRail(s, L(xs["sheriff"] + 2.8f, sRail), 180f, 3f, "sheriff_hitch");
            Props.HitchRail(s, L(xs["stable"] - 8f, southZ + 1.5f), 180f, 3.2f, "stable_hitch");
            Props.HitchRail(s, L(xs["cantina"] - 3f, southZ + 3.9f), 180f, 3f);
            Props.HitchRail(s, L(xs["feed"] + 2.2f, sRail), 180f, 3f);
            Props.Trough(s, L(xs["saloon"] - 6.6f, northZ - 3.7f), 0f);
            Props.Trough(s, L(xs["stable"] + 8.2f, southZ + 1.2f), 0f);
            Props.Trough(s, L(xs["cantina"] + 2.4f, southZ + 4.0f), 0f);
            Props.Well(s, L(CrossX, Town.y + 0.5f));
            s.Mark("town_well", L(CrossX, Town.y + 0.5f));
            for (int i = 0; i < 6; i++)
            {
                float x = 806f + i * 28f;
                if (Mathf.Abs(x - CrossX) < 9f) x += 10f;
                Props.LampPost(s, L(x, Town.y + (i % 2 == 0 ? 6.4f : -6.4f)));
            }

            // the telegraph line from the depot along the south side of the street, wired pole to pole
            var poles = new List<(Vector3 p, float yaw)>();
            for (float x = 786f; x < 965f; x += 29f)
            {
                float px = Mathf.Abs(x - CrossX) < 8f ? x + 9f : x;
                Vector3 p = L(px, southZ + 5.0f);
                Props.TelegraphPole(s, p, 90f);
                poles.Add((p, 90f));
            }
            Props.Wires(s, poles);

            // on the porches: benches, chairs, goods by the stores
            Props.Bench(s, L(xs["saloon"] - 4.6f, northZ - 0.6f, py), 180f, 1.6f, "town_seat0");
            Props.Bench(s, L(xs["store"] - 3.2f, northZ - 0.6f, py), 180f, 1.6f, "town_seat1");
            Props.Bench(s, L(xs["sheriff"] - 2.6f, southZ + 0.6f, py), 0f, 1.6f, "town_seat2");
            Props.Bench(s, L(xs["doctor"] + 2.2f, northZ - 0.6f, py), 180f, 1.4f, "town_seat3");
            Props.Bench(s, L(xs["hotel"] + 3.4f, northZ - 0.6f, py), 180f, 1.6f);
            Props.Chair(s, L(xs["saloon"] + 2.7f, northZ - 0.9f, py), 165f);
            Props.Chair(s, L(xs["saloon"] + 3.6f, northZ - 1.0f, py), 195f);
            Props.Chair(s, L(xs["hotel"] - 3.3f, northZ - 1.0f, py), 175f);
            Props.Chair(s, L(xs["barber"] + 1.6f, northZ - 1.0f, py), 200f);
            Props.Chair(s, L(xs["rooms"] - 3.1f, northZ - 0.9f, py), 170f);
            Props.Chair(s, L(xs["laundry"] + 1.6f, southZ + 0.9f, py), 15f);
            Props.Barrel(s, L(xs["store"] + 3.9f, northZ - 0.8f, py));
            Props.Barrel(s, L(xs["store"] + 4.6f, northZ - 1.5f, py));
            Props.Crate(s, L(xs["store"] + 3.8f, northZ - 1.7f, py), 0.7f, 20f);
            s.Model("sack", L(xs["store"] + 2.2f, northZ - 0.9f, py), 15f);
            s.Model("sack", L(xs["store"] + 2.75f, northZ - 1.05f, py), -20f);
            Props.Crate(s, L(xs["gunsmith"] - 3.2f, northZ - 0.9f, py), 0.6f, -10f);
            Props.Crate(s, L(xs["gunsmith"] - 2.6f, northZ - 1.1f, py + 0.6f), 0.5f, 25f, false);
            Props.Barrel(s, L(xs["saloon"] + 5.8f, northZ - 0.9f, py));
            Props.Barrel(s, L(xs["saloon"] - 6.1f, northZ - 0.8f, py));
            for (int i = 0; i < 6; i++)
                s.Model("sack", L(xs["feed"] - 3.4f + (i % 3) * 0.55f, southZ + 0.8f + (i / 3) * 0.1f, py + (i / 3) * 0.36f), 170f + i * 23f);
            Props.Barrel(s, L(xs["feed"] + 3.6f, southZ + 0.9f, py));
            Props.Crate(s, L(xs["smithy"] - 4.6f, southZ - 1f), 0.8f, 30f);
            Props.Barrel(s, L(xs["cantina"] - 4.9f, southZ + 2.6f));

            // the barber's pole, the undertaker's stock stood up by his door, wanted bills on the posts
            BarberPole(s, L(xs["barber"] + 2.2f, northZ - 1.95f, py));
            Coffin(s, L(xs["undertaker"] - 2.0f, northZ - 0.35f, py), 180f, true, Pal.WoodNew);
            Coffin(s, L(xs["undertaker"] - 2.8f, northZ - 0.4f, py), 186f, true, Pal.Shade(Pal.WoodDark, 1.1f));
            Coffin(s, L(xs["undertaker"] + 2.2f, northZ - 1.2f, py + 0.55f), 92f, false, Pal.WoodNew);
            s.Box(L(xs["undertaker"] + 1.7f, northZ - 1.2f, py + 0.27f), new Vector3(0.08f, 0.54f, 0.6f), Pal.WoodOld);
            s.Box(L(xs["undertaker"] + 2.8f, northZ - 1.2f, py + 0.27f), new Vector3(0.08f, 0.54f, 0.6f), Pal.WoodOld);
            Bill(s, L(xs["saloon"] - 5.5f, northZ - 0.13f, py + 1.55f), 0f, "РАЗЫСКИВАЕТСЯ", "$200");
            Bill(s, L(xs["store"] - 4.3f, northZ - 0.13f, py + 1.5f), 0f, "РАЗЫСКИВАЕТСЯ", "$50");
            Bill(s, L(xs["bank"] - 4.3f, northZ - 0.13f, py + 1.6f), 0f, "РОДЕО", "в субботу");
            Bill(s, L(xs["feed"] + 4.0f, southZ + 0.13f, py + 1.5f), 180f, "ПРОДАЁТСЯ", "мул и упряжь");

            // wagons waiting in the street: a covered one at the store, a buckboard by the feed store
            Props.Wagon(s, L(xs["store"] - 1.5f, Town.y + 3.2f), 94f, true);
            Props.Wagon(s, L(xs["feed"] - 1.0f, Town.y - 3.6f), 268f, false);
            Props.Wagon(s, L(CrossX + 3.2f, Town.y + 20f), 6f, false);

            // droppings, mostly by the hitching rails and in the middle of the street (ruts and mud were tried as
            // overlays: an opaque strip never matches the terrain's own shading, they read as boards and stickers)
            var dirt = new System.Random(9);
            float D() { return (float)dirt.NextDouble(); }
            Vector3 well = L(CrossX, Town.y + 0.5f);
            s.fine.Col(Pal.H(0x75614A)).Surface(Surf.Dirt);
            (float x, float z)[] rails = { (xs["saloon"] - 3.6f, nRail - 0.8f), (xs["store"] + 2.5f, nRail - 0.8f), (xs["sheriff"] + 2.8f, sRail + 0.8f), (xs["stable"] - 8f, southZ + 2.6f), (xs["cantina"] - 3f, southZ + 4.7f) };
            for (int i = 0; i < 24; i++)
            {
                float x = 780f + D() * 200f, z = Town.y + (D() - 0.5f) * 8f;
                if (i < 16)
                {
                    var (hx, hz) = rails[i % rails.Length];
                    x = hx + (D() - 0.5f) * 3f; z = hz + (D() - 0.5f) * 1.6f;
                }
                if (new Vector2(x - well.x, z - well.z).sqrMagnitude < 9f) continue;
                for (int k = 0; k < 3; k++)
                    s.fine.Ellipsoid(L(x + (D() - 0.5f) * 0.25f, z + (D() - 0.5f) * 0.25f, 0.012f), new Vector3(0.1f, 0.035f, 0.085f) * (0.7f + D() * 0.5f), 6, 4);
            }
            // tumbleweeds blown up against the porches and the posts
            (float x, float z, float k)[] weeds = { (xs["hotel"] - 6.6f, northZ - 2.9f, 0.9f), (xs["paper"] + 3.9f, northZ - 2.7f, 0.7f), (xs["undertaker"] + 3.3f, northZ - 2.8f, 1.0f),
                                                    (xs["laundry"] - 3.4f, southZ + 2.8f, 0.8f), (xs["cantina"] + 6.2f, southZ + 3.6f, 1.1f), (xs["adobe_s2"] + 1.5f, southZ + 1.6f, 0.75f),
                                                    (962f, Town.y + 5.4f, 1.0f), (CrossX - 5.8f, Town.y + 18f, 0.85f) };
            foreach (var (x, z, k) in weeds) s.Model("yd_tumbleweed", L(x, z, -0.05f), x * 37f % 360f, Vector3.one * k);
        }

        /// <summary>Planks across the narrow gaps between porches, so the boardwalk runs on along a row.</summary>
        static void Boardwalks(Site s, List<(BuildingSpec b, float x0, float x1, bool south)> lots, float northZ, float southZ)
        {
            bool Porched(BuildingSpec b) { return b.porch && (b.kind != BuildKind.Adobe && b.kind != BuildKind.Stable && b.kind != BuildKind.Smithy && b.kind != BuildKind.Butcher && b.kind != BuildKind.Church); }
            for (int i = 0; i + 1 < lots.Count; i++)
            {
                var a = lots[i]; var b = lots[i + 1];
                if (a.south != b.south || !Porched(a.b) || !Porched(b.b)) continue;
                float gap = b.x0 - a.x1;
                if (gap <= 0.05f || gap > 2.6f) continue;
                float zc = a.south ? southZ + Architect.PorchDepth * 0.5f : northZ - Architect.PorchDepth * 0.5f;
                s.Box(L((a.x1 + b.x0) * 0.5f, zc, Architect.Floor * 0.5f), new Vector3(gap + 0.2f, Architect.Floor, Architect.PorchDepth - 0.1f), Pal.WoodGrey);
            }
        }

        /// <summary>The yards behind the main street: privies, woodpiles, wash lines, ovens behind the adobe houses, a
        /// pump, junk, plank fences along some of the lot lines.</summary>
        static void BackLots(Site s, List<(BuildingSpec b, float x0, float x1, bool south)> lots, float northZ, float southZ)
        {
            var rng = new System.Random(31);
            float R() { return (float)rng.NextDouble(); }
            foreach (var (b, x0, x1, south) in lots)
            {
                float side = south ? -1f : 1f, z0 = south ? southZ - b.d : northZ + b.d;   // the back wall
                float mid = (x0 + x1) * 0.5f, yaw = south ? 0f : 180f;                  // things in the yard face the house
                Vector3 Y(float dx, float back) { return L(mid + dx, z0 + side * back); }
                if (b.kind == BuildKind.Stable || b.kind == BuildKind.Smithy || b.kind == BuildKind.Butcher || b.kind == BuildKind.Church) continue;
                if (b.kind == BuildKind.Adobe)
                {
                    if (s.Model("ab_horno", Y(b.w * 0.25f, 2.6f), yaw + 20f)) s.Collider(Y(b.w * 0.25f, 2.6f) + Vector3.up * 0.5f, new Vector3(1.5f, 1f, 1.5f));
                    if (s.Model("yd_woodpile", Y(-b.w * 0.2f, 1.2f), yaw)) s.Collider(Y(-b.w * 0.2f, 1.2f) + Vector3.up * 0.5f, new Vector3(1.9f, 1f, 0.8f), Quaternion.Euler(0f, yaw, 0f));
                    continue;
                }
                switch (b.id)
                {
                    case "hotel": case "rooms":
                        s.Model("yd_washline", Y(0f, 4.5f), yaw + 4f);
                        break;
                    case "laundry":
                        s.Model("yd_washline", Y(0.2f, 3.2f), yaw - 3f);
                        s.Model("yd_washline", Y(-0.4f, 6.6f), yaw + 5f);
                        Props.Barrel(s, Y(2.6f, 1.0f));
                        break;
                    case "saloon":
                        for (int i = 0; i < 5; i++) Props.Barrel(s, Y(-4.5f + (i % 3) * 0.7f, 1.0f + (i / 3) * 0.7f));
                        Props.Crate(s, Y(-2.2f, 1.1f), 0.7f, 12f);
                        Props.Crate(s, Y(-2.1f, 1.1f) + Vector3.up * 0.7f, 0.6f, -18f, false);
                        break;
                    case "store": case "feed":
                        for (int i = 0; i < 2; i++) Props.Crate(s, Y(-2.5f + i * 0.85f, 1.1f + (i % 2) * 0.3f), 0.72f, i * 13f);
                        Props.Barrel(s, Y(-0.6f, 1.0f));
                        break;
                    case "sheriff":
                        if (s.Model("yd_pump", Y(2.4f, 2.5f), yaw)) s.Collider(Y(2.4f, 2.5f) + Vector3.up * 0.5f, new Vector3(1f, 1f, 1f));
                        break;
                }
                // a privy at the back of the lot, a woodpile and a chopping block by the back door of every other house
                int k = (int)(mid * 7f) % 3;
                if (b.id != "laundry")
                {
                    Vector3 privy = Y((k - 1) * b.w * 0.32f, b.d < 8.5f ? 6.5f : 5.5f);
                    if (s.Model("yd_outhouse", privy, yaw + (R() - 0.5f) * 8f)) s.Collider(privy + Vector3.up * 1.1f, new Vector3(1.25f, 2.2f, 1.3f), Quaternion.Euler(0f, yaw, 0f));
                }
                if (k != 1)
                {
                    Vector3 pile = Y(-(k - 1) * b.w * 0.28f, 1.1f);
                    if (s.Model("yd_woodpile", pile, yaw)) s.Collider(pile + Vector3.up * 0.5f, new Vector3(1.9f, 1f, 0.8f), Quaternion.Euler(0f, yaw, 0f));
                    s.Model("yd_chopblock", Y(-(k - 1) * b.w * 0.28f + 1.8f, 2.3f), R() * 360f);
                }
            }
            // plank fences closing some of the yards off behind the north row, gates left open
            void Back(float xa, float xb, float z)
            {
                var pts = new List<Vector3>();
                for (float x = xa; x <= xb + 0.01f; x += (xb - xa) / Mathf.Max(1, Mathf.RoundToInt((xb - xa) / 2.4f))) pts.Add(L(x, z));
                Props.Fence(s, pts, 1.4f, 2, new HashSet<int> { pts.Count / 2 }, Pal.WoodGrey);
            }
            Back(814f, 836f, northZ + 18.5f);
            Back(889f, 909f, northZ + 18f);
            Back(800f, 812f, southZ - 15.5f);
            // tumbleweeds caught by the fences
            foreach (var (x, z) in new[] { (820.5f, northZ + 18.1f), (903f, northZ + 17.6f), (805f, southZ - 15.1f), (948f, northZ + 12f) })
                s.Model("yd_tumbleweed", L(x, z, -0.05f), x * 13f % 360f, Vector3.one * 0.9f);
        }

        /// <summary>A barber's pole: red, white and blue bands under a white ball, on a bracket off the porch post.</summary>
        static void BarberPole(Site s, Vector3 foot)
        {
            Color32[] bands = { Pal.H(0xB23A2C), Pal.H(0xE6E0D2), Pal.H(0x3D5A86) };
            for (int i = 0; i < 9; i++)
            {
                s.solid.Col(bands[i % 3], 0.4f);
                s.solid.Cyl(foot + Vector3.up * (1.0f + i * 0.13f), foot + Vector3.up * (1.13f + i * 0.13f), 0.075f, 0.075f, 12, i == 0, i == 8);
            }
            s.solid.Col(Pal.H(0xE6E0D2), 0.5f);
            s.solid.Ellipsoid(foot + Vector3.up * 2.26f, new Vector3(0.09f, 0.09f, 0.09f), 10, 6);
            s.solid.Col(Pal.Iron, 0.4f);
            s.solid.Cyl(foot + Vector3.up * 0.85f, foot + Vector3.up * 1.0f, 0.06f, 0.08f, 10);
            s.Collider(foot + Vector3.up * 1.5f, new Vector3(0.18f, 1.6f, 0.18f));
        }

        /// <summary>A plain pine coffin, stood up against a wall or laid on trestles.</summary>
        static void Coffin(Site s, Vector3 foot, float yaw, bool standing, Color32 wood)
        {
            // the outline: narrow at the head and the foot, widest at the shoulders
            var outline = new List<Vector3> { new Vector3(-0.16f, 0f, 0f), new Vector3(0.16f, 0f, 0f), new Vector3(0.27f, 1.4f, 0f), new Vector3(0.18f, 1.9f, 0f),
                                              new Vector3(-0.18f, 1.9f, 0f), new Vector3(-0.27f, 1.4f, 0f) };
            Quaternion q = Quaternion.Euler(0f, yaw, 0f) * (standing ? Quaternion.Euler(-4f, 0f, 0f) : Quaternion.Euler(90f, 0f, 0f));
            Vector3 origin = foot + (standing ? Vector3.zero : Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -0.95f));
            var poly = new List<Vector3>();
            foreach (Vector3 v in outline) poly.Add(origin + q * v);
            s.solid.Col(wood);
            s.solid.Prism(poly, q * new Vector3(0f, 0f, standing ? 0.3f : -0.3f));
            s.Collider(origin + q * new Vector3(0f, 0.95f, 0.15f), new Vector3(0.55f, 1.9f, 0.3f), q);
        }

        /// <summary>A bill tacked to a post or a wall: a sheet of paper with a heading and a line under it.</summary>
        static void Bill(Site s, Vector3 c, float yaw, string head, string line)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            s.BoxRot(c, new Vector3(0.36f, 0.48f, 0.01f), q, Pal.Shade(Pal.Parchment, 0.94f), false);
            Vector3 front = q * Vector3.back * 0.008f;
            s.Text(head, c + front + Vector3.up * 0.15f, q, 0.05f, new Color32(0x2A, 0x20, 0x18, 255));
            s.Text(line, c + front - Vector3.up * 0.02f, q, 0.07f, new Color32(0x3A, 0x22, 0x18, 255));
        }

        // ================================================================== railway

        static void BuildRailway(Region r)
        {
            var s = new Site();
            var pts = new List<Vector3>();
            // equal chords of about six metres, the length of the Blender track piece: no short piece with crowded ties at the end
            int chords = Mathf.Max(1, Mathf.RoundToInt(rail.length / 6f));
            for (int i = 0; i <= chords; i++)
            {
                Vector2 p = rail.At(rail.length * i / chords);
                pts.Add(new Vector3(p.x, HeightAt(p.x, p.y) + 0.02f, p.y));
            }
            Vector2 end = rail.At(rail.length), dir = rail.Tangent(rail.length - 2f);
            Props.Rails(s, pts);
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            Vector3 stop = L(end.x, end.y);
            if (s.Model("rail_buffer", stop, yaw)) s.Collider(stop + Vector3.up * 0.8f, new Vector3(2.7f, 1.6f, 0.6f), Quaternion.Euler(0, yaw, 0));
            else s.BoxRot(stop + Vector3.up * 0.8f, new Vector3(2.6f, 1.2f, 0.5f), Quaternion.Euler(0, yaw, 0), Pal.WoodDark);
            s.Mark("rail_end", stop, yaw);

            // the train: the engine at the buffers, tender, a boxcar, two coaches; it keeps a timetable (World/Trains.cs)
            float[] back = { 9f, 16.4f, 24.7f, 35.5f, 46.3f };
            int[] kinds = { 0, 1, 2, 3, 3 };
            for (int i = 0; i < back.Length; i++)
            {
                Vector2 p = rail.At(rail.length - back[i]);
                s.Mark("train" + i, L(p.x, p.y), yaw);
            }
            Trains.Build(r.root.transform, pts, back, kinds);

            // station, platform and water tower
            Vector2 car = rail.At(rail.length - 37f);
            Vector2 side = new Vector2(-dir.y, dir.x);       // to the left of the track: north
            float stationX = car.x + 2f, stationZ = car.y + side.y * 12.5f;
            Place(r, new BuildingSpec { kind = BuildKind.Station, id = "station", sign = "СТАНЦИЯ · ПОЧТА", w = 13f, d = 7.5f, wall = Pal.WoodYellow, trim = Pal.WoodRed, falseFront = true }, stationX, stationZ, 0f);
            float deckY = HeightAt(car.x, car.y) + 0.55f;
            Vector3 deck = new Vector3(car.x + 1f, deckY - 0.275f, car.y + side.y * 4.4f);
            s.BoxRot(deck, new Vector3(4.6f, 0.55f, 34f), Quaternion.Euler(0, yaw, 0), Pal.WoodGrey);
            s.BoxRot(deck + new Vector3(0, -0.14f, side.y * 2.9f), new Vector3(1.4f, 0.28f, 34f), Quaternion.Euler(0, yaw, 0), Pal.WoodGrey);
            Props.Crate(s, new Vector3(car.x - 9f, deckY, car.y + side.y * 4.6f), 0.8f, 15f);
            Props.Crate(s, new Vector3(car.x - 9.9f, deckY, car.y + side.y * 5.3f), 0.6f, -20f);
            Props.Barrel(s, new Vector3(car.x + 12f, deckY, car.y + side.y * 5.2f));
            Props.Bench(s, new Vector3(car.x + 6f, deckY, car.y + side.y * 5.6f), 180f, 2f, "platform_seat");
            Props.WaterTower(s, L(end.x + 9f, end.y + 9f));
            s.Mark("water_tower", L(end.x + 9f, end.y + 9f));

            // stock pens west of the depot: three pens of round poles, the loading chute climbing from the middle one
            // to the level of a car's door by the track, a windmill pumping into the trough
            float s0 = rail.length - 78f;
            Vector3 P(float along, float off)
            {
                Vector2 q = rail.At(s0 + along), tq = rail.Tangent(s0 + along);
                return L(q.x - tq.y * off, q.y + tq.x * off);
            }
            Props.Fence(s, new List<Vector3> { P(0f, 9.5f), P(0f, 23.5f), P(30f, 23.5f), P(30f, 9.5f), P(16.2f, 9.5f), P(13.8f, 9.5f), P(0f, 9.5f) }, 1.6f, 3, new HashSet<int> { 4 });
            Props.Fence(s, new List<Vector3> { P(10f, 9.5f), P(10f, 18f), P(10f, 23.5f) }, 1.6f, 3, new HashSet<int> { 0 });
            Props.Fence(s, new List<Vector3> { P(20f, 9.5f), P(20f, 14.5f), P(20f, 23.5f) }, 1.6f, 3, new HashSet<int> { 1 });
            Vector3 low = P(15f, 9.5f), high = P(15f, 2.3f);
            Vector3 up = high - low;
            float chuteYaw = Mathf.Atan2(up.x, up.z) * Mathf.Rad2Deg;
            if (s.Model("yd_chute", low, chuteYaw))
            {
                Quaternion cq = Quaternion.Euler(0f, chuteYaw, 0f);
                foreach (float e in new[] { -1f, 1f }) s.Collider(low + cq * new Vector3(e * 0.68f, 1.3f, 3.6f), new Vector3(0.1f, 2.6f, 7.2f), cq);
            }
            Props.Trough(s, P(5f, 21.8f), chuteYaw + 90f);
            Props.Trough(s, P(25f, 21.8f), chuteYaw + 90f);
            Props.Windmill(s, P(-5f, 22f));
            for (int i = 0; i < 3; i++)
                if (s.Model("haybale", P(33.5f + (i % 2) * 1.1f, 20f + (i / 2) * 1.5f) + Vector3.up * (i == 2 ? 0.5f : 0f), 80f + i * 9f) && i < 2)
                    s.Collider(P(33.5f + (i % 2) * 1.1f, 20f) + Vector3.up * 0.25f, new Vector3(1.0f, 0.5f, 1.4f));
            s.Model("yd_tumbleweed", P(2f, 23.9f) + Vector3.down * 0.05f, 40f, Vector3.one * 0.9f);
            s.Model("yd_tumbleweed", P(27f, 9.1f) + Vector3.down * 0.05f, 200f, Vector3.one * 0.7f);
            s.Mark("stock_pens", P(15f, 16f));
            {
                // the pens trodden bare and dark, mud at the troughs, wheel ruts to the chute
                Vector3 c = P(15f, 16.5f), t1 = P(5f, 21.8f), t2 = P(25f, 21.8f);
                GroundMarks.Patch(new Vector2(c.x, c.z), 15.5f, 7.5f, chuteYaw + 90f, GroundMarks.Trodden, 1f);
                GroundMarks.Patch(new Vector2(t1.x, t1.z), 1.6f, 1.1f, chuteYaw, GroundMarks.Mud, 0.9f);
                GroundMarks.Patch(new Vector2(t2.x, t2.z), 1.6f, 1.1f, chuteYaw, GroundMarks.Mud, 0.9f);
            }
            Vector3 pa = P(-2f, 8f), pb = P(32f, 25f);
            noGrass.Add(Rect.MinMaxRect(Mathf.Min(pa.x, pb.x), Mathf.Min(pa.z, pb.z), Mathf.Max(pa.x, pb.x), Mathf.Max(pa.z, pb.z)));
            Props.HitchRail(s, L(stationX + 9.5f, stationZ + 4f), 90f, 3f, "station_hitch");
            var line = new List<(Vector3 p, float yaw)>();
            for (float a = 30f; a < rail.length - 60f; a += 48f)
            {
                Vector2 p = rail.At(a), t = rail.Tangent(a);
                Vector3 at = L(p.x - t.y * 6.5f, p.y + t.x * 6.5f);
                float py = Mathf.Atan2(t.x, t.y) * Mathf.Rad2Deg;
                Props.TelegraphPole(s, at, py);
                line.Add((at, py));
            }
            Props.Wires(s, line);
            s.Finish(r, "Railway", Vector3.zero, 0f);

            noGrass.Add(new Rect(car.x - 20f, car.y - 4f, 42f, 16f));
            r.playerStart = new Vector3(car.x + 1f, deckY, car.y + side.y * 4.4f);
            r.playerYaw = 0f;
            r.horseStart = L(stationX + 9.5f, stationZ + 2.4f);
            r.horseYaw = 270f;
            r.Mark("platform", r.playerStart);
        }

        // ================================================================== camp

        static void BuildCamp(Region r)
        {
            var s = new Site();
            Vector3 c = OnLand(CampC);
            Vector3 At(float angle, float radius, float up = 0f)
            {
                Vector3 d = U.Dir(angle) * radius;
                return L(CampC.x + d.x, CampC.y + d.z, up);
            }
            Color32[] canvases = { Pal.Canvas, Pal.H(0xCFC3A4), Pal.H(0xE0D7BE), Pal.H(0xC6BB9E) };
            int w = 0;
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                if (i == 7 || i == 8) continue;                   // the gap that faces the road
                Props.Wagon(s, At(a, 21f), a + 90f, true, canvases[i % canvases.Length], "camp_wagon" + w++);
            }
            Props.Campfire(s, c);
            for (int i = 0; i < 4; i++) Props.LogSeat(s, At(i * 90f + 45f, 2.7f), i * 90f + 45f + 180f, "camp_seat" + i);
            s.Mark("camp_fire", c);

            // kitchen: Buck's chuck wagon
            Vector3 cook = At(60f, 11.5f);
            Props.Wagon(s, At(60f, 14.5f), 150f, false);
            s.Box(At(60f, 11.2f, 0.42f), new Vector3(1.8f, 0.84f, 0.8f), Pal.WoodOld);
            s.solid.Col(Pal.Iron, 0.5f);
            s.solid.Lathe(At(60f, 9.4f, 0.05f), new List<Vector2> { new Vector2(0.18f, 0f), new Vector2(0.3f, 0.2f), new Vector2(0.28f, 0.42f) }, 10);
            Props.Barrel(s, At(72f, 12.5f));
            s.Mark("camp_cook", At(60f, 12.2f), 240f);
            s.Mark("camp_cookfront", At(60f, 10.1f), 60f);
            s.Mark("camp_pot", At(60f, 9.4f));
            s.Smoke(Plume.Steam, At(60f, 9.4f, 0.5f));

            // Marta's table with the ledger and the strongbox
            s.Box(At(300f, 11f, 0.4f), new Vector3(1.5f, 0.8f, 0.8f), Pal.WoodNew);
            s.Box(At(300f, 11f, 0.84f) + new Vector3(0.2f, 0, 0), new Vector3(0.4f, 0.06f, 0.3f), Pal.ClothRed, false);
            s.Box(At(292f, 11.4f, 0.25f), new Vector3(0.7f, 0.5f, 0.45f), Pal.Iron, true, 0.5f);
            Props.Chair(s, At(300f, 12f), 120f);
            s.Mark("camp_ledger", At(300f, 12f), 120f);
            s.Mark("camp_ledgerfront", At(300f, 9.9f), 300f);
            s.Mark("camp_box", At(292f, 10.4f), 292f);

            // Yefim's forge
            Props.Wagon(s, At(120f, 15.5f), 30f, false);
            s.Box(At(120f, 12.4f, 0.3f), new Vector3(0.4f, 0.6f, 0.4f), Pal.Bark);
            s.Box(At(120f, 12.4f, 0.7f), new Vector3(0.24f, 0.2f, 0.6f), Pal.Iron, false, 0.6f);
            s.Box(At(128f, 13.2f, 0.4f), new Vector3(0.8f, 0.8f, 0.8f), Pal.Brick);
            s.glass.Col(Pal.GlassLit);
            s.glass.Box(At(128f, 13.2f, 0.82f), new Vector3(0.5f, 0.05f, 0.5f));
            s.Smoke(Plume.Forge, At(128f, 13.2f, 0.86f));
            s.Mark("camp_smith", At(120f, 13.3f), 300f);
            s.Mark("camp_smithfront", At(120f, 11.2f), 120f);

            // tents: the doctor's, the captain's, families'
            Props.Tent(s, At(0f, 13.5f), 180f, 3f, 3.4f, Pal.H(0xE4DCC8));
            Props.Crate(s, At(8f, 11.6f), 0.6f, 12f);
            s.Mark("camp_doc", At(0f, 11.6f), 180f);
            s.Mark("camp_docfront", At(0f, 10f), 0f);
            Props.Tent(s, At(180f, 13f), 0f, 2.8f, 3.2f, Pal.H(0xB9AE90));
            s.Box(At(180f, 13.2f, 0.06f), new Vector3(0.8f, 0.12f, 1.9f), Pal.ClothBrown, false);
            s.Mark("camp_bed", At(180f, 11f), 0f);
            Props.Tent(s, At(30f, 14f), 210f);
            Props.Tent(s, At(150f, 14.5f), 330f);
            Props.Tent(s, At(330f, 14f), 150f);
            Props.Tent(s, At(270f, 14.5f), 90f);

            // chores
            s.solid.Col(Pal.Bark);
            for (int i = 0; i < 9; i++)
            {
                Vector3 p = At(150f, 18f, 0.14f + (i / 3) * 0.26f) + new Vector3((i % 3) * 0.27f - 0.27f, 0, 0);
                s.solid.Cyl(p + new Vector3(0, 0, -0.5f), p + new Vector3(0, 0, 0.5f), 0.13f, 0.13f, 6);
            }
            s.Box(At(158f, 17f, 0.25f), new Vector3(0.5f, 0.5f, 0.5f), Pal.Bark);
            s.Mark("camp_wood", At(158f, 16f), 158f);
            Props.Barrel(s, At(85f, 7.5f));
            s.Mark("camp_water", At(85f, 6.4f), 85f);
            Props.HayBale(s, At(18f, 31f), 10f);
            Props.HayBale(s, At(21f, 31.5f), 40f);
            Props.HayBale(s, At(19.5f, 31.2f, 0.6f), 20f);
            s.Mark("camp_hay", At(20f, 29.6f), 20f);

            // hitching line and the ox pen
            Props.HitchRail(s, At(238f, 29f), 328f, 6f, "camp_hitch");
            var pen = new List<Vector3>();
            Vector3 pc = At(20f, 46f);
            for (int i = 0; i <= 14; i++)
            {
                float a = i * Mathf.PI * 2f / 14f;
                pen.Add(L(pc.x + Mathf.Cos(a) * 13f, pc.z + Mathf.Sin(a) * 13f));
            }
            Props.Fence(s, pen, 1.2f, 2, new HashSet<int> { 10 });
            s.Mark("camp_pen", pc);
            for (int i = 0; i < 8; i++) s.Mark("camp_spot" + i, At(i * 45f + 20f, 6f + (i % 3) * 3f), i * 45f + 200f);
            s.Mark("camp_gate", At(225f, 24f), 45f);
            s.Finish(r, "Camp", Vector3.zero, 0f);
        }

        // ================================================================== ranch and outskirts

        static void BuildRanch(Region r)
        {
            Place(r, new BuildingSpec { kind = BuildKind.Facade, id = "ranch_house", w = 12f, d = 9f, falseFront = false, wall = Pal.WoodWhite, trim = Pal.WoodRed }, Ranch.x - 22f, Ranch.y + 12f, 0f);
            Place(r, new BuildingSpec { kind = BuildKind.Stable, id = "ranch_barn", sign = "ДАБЛ-БАР", w = 13f, d = 15f, wall = Pal.WoodRed, trim = Pal.WoodWhite, porch = false }, Ranch.x + 24f, Ranch.y + 10f, 0f);
            var s = new Site();
            float cx = Ranch.x, cz = Ranch.y - 30f;
            var pen = new List<Vector3> { L(cx - 24f, cz + 16f), L(cx - 24f, cz - 16f), L(cx + 24f, cz - 16f), L(cx + 24f, cz + 16f), L(cx + 5f, cz + 16f), L(cx - 5f, cz + 16f), L(cx - 24f, cz + 16f) };
            Props.Fence(s, pen, 1.3f, 3, new HashSet<int> { 4 });
            Props.Windmill(s, L(Ranch.x - 40f, Ranch.y - 8f));
            s.Mark("ranch_mill", L(Ranch.x - 40f, Ranch.y - 8f));
            Props.Trough(s, L(Ranch.x - 36f, Ranch.y - 11f), 0f);
            Props.Trough(s, L(cx + 12f, cz + 14.5f), 0f);
            Props.HayBale(s, L(Ranch.x + 12f, Ranch.y + 6f), 0f);
            Props.HayBale(s, L(Ranch.x + 12.4f, Ranch.y + 4.8f), 35f);
            Props.HitchRail(s, L(Ranch.x - 22f, Ranch.y + 6f), 0f, 3f, "ranch_hitch");
            // gate over the road in
            Vector3 g = L(Ranch.x - 9f, Ranch.y + 50f);
            if (!s.Model("ranch_gate", g))
            {
                s.solid.Col(Pal.WoodDark);
                s.solid.Box(g + new Vector3(-3.2f, 2.2f, 0), new Vector3(0.24f, 4.4f, 0.24f));
                s.solid.Box(g + new Vector3(3.2f, 2.2f, 0), new Vector3(0.24f, 4.4f, 0.24f));
                s.solid.Box(g + new Vector3(0, 4.3f, 0), new Vector3(7f, 0.24f, 0.24f));
                s.Box(g + new Vector3(0, 3.7f, 0), new Vector3(3.2f, 0.7f, 0.08f), Pal.Shade(Pal.Parchment, 0.9f), false);
            }
            s.Mark("ranch_gate", g);
            s.Text("ДАБЛ-БАР", g + new Vector3(0, 3.7f, 0.07f), Quaternion.Euler(0, 180f, 0), 0.45f, new Color32(0x2A, 0x20, 0x18, 255));
            s.Text("ДАБЛ-БАР", g + new Vector3(0, 3.7f, -0.07f), Quaternion.identity, 0.45f, new Color32(0x2A, 0x20, 0x18, 255));
            s.Mark("ranch_yard", L(Ranch.x, Ranch.y + 2f));
            s.Mark("ranch_corral", L(cx, cz));
            s.Mark("ranch_pasture", L(Ranch.x + 90f, Ranch.y - 60f));
            for (int i = 0; i < 3; i++) s.Mark("ranch_hand" + i, L(Ranch.x - 8f + i * 9f, Ranch.y - 6f - i * 3f), 180f);
            s.Finish(r, "RanchProps", Vector3.zero, 0f);
        }

        static void BuildOutskirts(Region r)
        {
            Place(r, new BuildingSpec { kind = BuildKind.Facade, id = "olson", w = 6.5f, d = 5.5f, h = 2.9f, falseFront = false, wall = Pal.WoodOld, trim = Pal.WoodDark }, Olson.x - 6f, Olson.y + 4f, 0f);
            Place(r, new BuildingSpec { kind = BuildKind.Facade, id = "millfarm", w = 7f, d = 6f, h = 3f, falseFront = false, wall = Pal.WoodGrey, trim = Pal.WoodWhite }, MillFarm.x - 4f, MillFarm.y + 5f, 0f);
            var s = new Site();
            Props.Well(s, L(Olson.x + 6f, Olson.y - 4f));
            var field = new List<Vector3> { L(Olson.x + 14f, Olson.y + 14f), L(Olson.x + 14f, Olson.y - 14f), L(Olson.x + 46f, Olson.y - 14f), L(Olson.x + 46f, Olson.y + 14f), L(Olson.x + 14f, Olson.y + 14f) };
            Props.Fence(s, field, 1.0f, 2);
            Props.Windmill(s, L(MillFarm.x + 12f, MillFarm.y - 6f));
            var yard = new List<Vector3> { L(MillFarm.x - 16f, MillFarm.y - 2f), L(MillFarm.x - 16f, MillFarm.y - 20f), L(MillFarm.x + 6f, MillFarm.y - 20f), L(MillFarm.x + 6f, MillFarm.y - 2f) };
            Props.Fence(s, yard, 1.0f, 2);
            s.Mark("olson_yard", L(Olson.x, Olson.y - 3f));
            s.Mark("millfarm_yard", L(MillFarm.x, MillFarm.y - 3f));

            // the burnt-out relay station in its gully
            Vector3 o = OnLand(OldStation);
            s.Box(o + new Vector3(-4f, 1.3f, 0), new Vector3(0.3f, 2.6f, 7f), Pal.Adobe);
            s.Box(o + new Vector3(0, 0.9f, 3.5f), new Vector3(8f, 1.8f, 0.3f), Pal.Adobe);
            s.Box(o + new Vector3(4f, 0.6f, 1f), new Vector3(0.3f, 1.2f, 4f), Pal.Adobe);
            s.Box(o + new Vector3(2.6f, 1.9f, 3.2f), new Vector3(1.2f, 3.8f, 1.2f), Pal.StoneDark);
            s.solid.Col(Pal.Ash);
            s.solid.Beam(o + new Vector3(-3.8f, 2.4f, -2f), o + new Vector3(1.5f, 0.3f, -1f), 0.2f, 0.2f);
            s.solid.Beam(o + new Vector3(-3.8f, 2.5f, 1.5f), o + new Vector3(3.8f, 1.1f, 2.5f), 0.2f, 0.2f);
            s.Mark("old_station", o + new Vector3(0, 0, -6f));

            // the ford
            Vector2 f1 = Ford + new Vector2(-48f, -3f), f2 = Ford + new Vector2(48f, 5f);
            foreach (Vector2 f in new[] { f1, f2 })
            {
                Vector3 p = L(f.x, f.y + 6f);
                s.solid.Col(Pal.WoodDark);
                s.solid.Box(p + Vector3.up * 1.1f, new Vector3(0.14f, 2.2f, 0.14f));
                s.Box(p + Vector3.up * 1.9f, new Vector3(1.3f, 0.5f, 0.07f), Pal.Shade(Pal.Parchment, 0.9f), false);
                s.Text("БРОД", p + new Vector3(0, 1.9f, -0.06f), Quaternion.identity, 0.36f, new Color32(0x2A, 0x20, 0x18, 255));
                s.Text("БРОД", p + new Vector3(0, 1.9f, 0.06f), Quaternion.Euler(0, 180f, 0), 0.36f, new Color32(0x2A, 0x20, 0x18, 255));
            }
            s.Mark("ford_west", L(f1.x, f1.y));
            s.Mark("ford_east", L(f2.x, f2.y));
            s.Mark("hunt_north", L(900f, 1560f));
            s.Mark("hunt_plain", L(1080f, 1330f));
            s.Mark("hunt_grove", L(1420f, 1420f));
            s.Mark("bison_range", L(520f, 1720f));
            s.Mark("pond", L(Pond.x - 60f, Pond.y));
            s.Finish(r, "Outskirts", Vector3.zero, 0f);
        }
    }
}
