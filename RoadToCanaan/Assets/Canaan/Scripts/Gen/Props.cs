using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>
    /// A piece of scenery under construction: solid geometry, window glass, colliders, lamps and named spots.
    /// Everything is given in the site's local frame; Finish places it in the world.
    /// </summary>
    public class Site
    {
        public readonly MeshBuilder solid = new MeshBuilder(), glass = new MeshBuilder(), thin = new MeshBuilder();
        /// <summary>Things too fine to cast a shadow the shadow map could draw truly (wires): a hard line on the ground would read as a rail.</summary>
        public readonly MeshBuilder fine = new MeshBuilder();
        /// <summary>False for a site of small things (weeds, tumbleweeds) whose shadows would cost more than they show.</summary>
        public bool castShadows = true;
        /// <summary>Blender models placed on the site, merged into one mesh with real UVs.</summary>
        public readonly MeshBuilder models = new MeshBuilder();
        struct Col { public Vector3 c, size; public Quaternion rot; }
        struct Lamp { public Vector3 p; public Color color; public float range, intensity; public bool always, fire; }
        struct Spot { public string key; public Vector3 p; public float yaw; }
        struct Sign { public string text; public Vector3 p; public Quaternion rot; public float size; public Color color; }
        struct Swing { public string model; public Vector3 hinge, offset, scale, doorway; public float yaw, open, reach; public bool spring; public Color32 dye; }
        readonly List<Swing> swings = new List<Swing>();
        readonly List<Swing> wheels = new List<Swing>();
        readonly List<(Plume kind, Vector3 p)> plumes = new List<(Plume, Vector3)>();
        readonly List<Col> cols = new List<Col>();
        readonly List<Lamp> lamps = new List<Lamp>();
        readonly List<Spot> spots = new List<Spot>();
        readonly List<Sign> signs = new List<Sign>();

        public void Box(Vector3 c, Vector3 size, Color32 col, bool collide = true, float gloss = 0f)
        {
            solid.Col(col, gloss);
            solid.Box(c, size);
            if (collide) cols.Add(new Col { c = c, size = size, rot = Quaternion.identity });
        }

        public void BoxMin(Vector3 min, Vector3 size, Color32 col, bool collide = true) { Box(min + size * 0.5f, size, col, collide); }

        public void BoxRot(Vector3 c, Vector3 size, Quaternion rot, Color32 col, bool collide = true)
        {
            solid.Col(col);
            solid.Box(c, size, rot);
            if (collide) cols.Add(new Col { c = c, size = size, rot = rot });
        }

        /// <summary>Places a Blender model. False if this build has no such model (the caller then builds it from code).</summary>
        public bool Model(string name, Vector3 p, float yaw = 0f, float scale = 1f)
        {
            if (!Models.Has(name)) return false;
            models.AppendMesh(Models.Mesh(name), Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), new Vector3(scale, scale, scale)));
            return true;
        }

        /// <summary>A Blender model stretched per axis, with its dyed part (canvas, alpha 12) in the given colour.</summary>
        public bool Model(string name, Vector3 p, float yaw, Vector3 scale, Color32? dye = null)
        {
            if (!Models.Has(name)) return false;
            models.AppendMesh(Models.Mesh(name), Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), scale), dye, 12);
            return true;
        }

        /// <summary>A Blender model at any attitude (a rail up a grade, a rifle stood in a rack), its part 12 dyed.</summary>
        public bool Model(string name, Vector3 p, Quaternion rot, Vector3 scale, Color32? dye = null)
        {
            if (!Models.Has(name)) return false;
            models.AppendMesh(Models.Mesh(name), Matrix4x4.TRS(p, rot, scale), dye, 12);
            return true;
        }

        /// <summary>Smoke, fire or steam that stands at a point of the site (see Emitters).</summary>
        public void Smoke(Plume kind, Vector3 p) { plumes.Add((kind, p)); }

        /// <summary>A wheel that turns in the wind about its own z axis (see Windmills): its own object, not part of the site's mesh.</summary>
        public void WindWheel(string model, Vector3 hub, Quaternion rot)
        {
            if (Models.Has(model)) wheels.Add(new Swing { model = model, hinge = hub, scale = Vector3.one, offset = rot.eulerAngles });
        }

        /// <summary>A model with two dyed parts: 12 in the trim colour, 13 in the wall colour (a barn door).</summary>
        public bool Model(string name, Vector3 p, float yaw, Vector3 scale, Color32 trim, Color32 wall)
        {
            if (!Models.Has(name)) return false;
            models.AppendMesh(Models.Mesh(name), Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), scale), trim, 12, wall, 13);
            return true;
        }

        /// <summary>
        /// A door leaf on a hinge that swings before whoever walks up to the doorway (see Doors). 'hinge' and 'yaw' place
        /// the hinge line and the shut leaf, 'offset' moves the model's origin off the hinge, 'open' is the swing in
        /// degrees for somebody coming from the street (-Z); a 'spring' leaf swings both ways and rings before it settles.
        /// </summary>
        public void Door(string model, Vector3 hinge, float yaw, Vector3 offset, Vector3 scale, Color32 dye, Vector3 doorway, float open, float reach, bool spring)
        {
            if (!Models.Has(model)) return;
            swings.Add(new Swing { model = model, hinge = hinge, yaw = yaw, offset = offset, scale = scale, dye = dye, doorway = doorway, open = open, reach = reach, spring = spring });
        }

        public void Collider(Vector3 c, Vector3 size) { cols.Add(new Col { c = c, size = size, rot = Quaternion.identity }); }
        public void Collider(Vector3 c, Vector3 size, Quaternion rot) { cols.Add(new Col { c = c, size = size, rot = rot }); }
        public void Light(Vector3 p, Color color, float range, float intensity, bool always = false, bool fire = false) { lamps.Add(new Lamp { p = p, color = color, range = range, intensity = intensity, always = always, fire = fire }); }
        public void Mark(string key, Vector3 p, float yaw = 0f) { spots.Add(new Spot { key = key, p = p, yaw = yaw }); }

        public void Text(string text, Vector3 p, Quaternion rot, float size, Color color)
        {
            signs.Add(new Sign { text = text, p = p, rot = rot, size = size, color = color });
        }

        /// <summary>Bakes the meshes and colliders into a GameObject at a world pose and registers its spots.</summary>
        public GameObject Finish(Region region, string name, Vector3 pos, float yaw, string prefix = "")
        {
            var go = new GameObject(name);
            go.transform.SetParent(region.root.transform, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.layer = Layers.Static;
            if (solid.VertexCount > 0) solid.ToObject("Solid", go.transform, Mats.Lit, castShadows).layer = Layers.Static;
            if (thin.VertexCount > 0) thin.ToObject("Thin", go.transform, Mats.LitDouble).layer = Layers.Static;
            if (fine.VertexCount > 0) fine.ToObject("Fine", go.transform, Mats.Lit, false).layer = Layers.Static;
            if (models.VertexCount > 0) models.ToObject("Models", go.transform, Mats.SurfaceUV, castShadows).layer = Layers.Static;
            if (glass.VertexCount > 0)
            {
                GameObject g = glass.ToObject("Glass", go.transform, RegionLights.WindowMaterial, false);
                g.layer = Layers.Static;
            }
            foreach (Col c in cols)
            {
                var cg = new GameObject("C") { layer = Layers.Static };
                cg.transform.SetParent(go.transform, false);
                cg.transform.localPosition = c.c;
                cg.transform.localRotation = c.rot;
                cg.AddComponent<BoxCollider>().size = c.size;
            }
            foreach (Lamp l in lamps)
            {
                var lg = new GameObject("Lamp");
                lg.transform.SetParent(go.transform, false);
                lg.transform.localPosition = l.p;
                var light = lg.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = l.color;
                light.range = l.range;
                light.intensity = l.intensity * RegionLights.Boost;
                light.shadows = LightShadows.None;
                light.shadowStrength = 0.85f;
                light.shadowNearPlane = 0.25f;
                RegionLights.Register(light, !l.always, l.fire);
            }
            foreach (Sign s in signs)
            {
                var sg = new GameObject("Sign");
                sg.transform.SetParent(go.transform, false);
                sg.transform.localPosition = s.p;
                sg.transform.localRotation = s.rot;
                var tm = sg.AddComponent<TextMesh>();
                tm.font = UI.font;
                tm.text = s.text;
                tm.fontSize = 72;
                tm.characterSize = s.size * 0.1f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = s.color;
                tm.fontStyle = FontStyle.Bold;
                sg.GetComponent<MeshRenderer>().sharedMaterial = RegionLights.SignMaterial;
            }
            foreach (Swing d in swings)
            {
                var pivot = new GameObject("Door");
                pivot.transform.SetParent(go.transform, false);
                pivot.transform.localPosition = d.hinge;
                pivot.transform.localRotation = Quaternion.Euler(0f, d.yaw, 0f);
                GameObject leaf = HumanModel.Dyed(d.model, d.model, 12, d.dye);
                leaf.transform.SetParent(pivot.transform, false);
                leaf.transform.localPosition = d.offset;
                leaf.transform.localScale = d.scale;
                Doors.Add(pivot.transform, go.transform.TransformPoint(d.doorway), go.transform.TransformDirection(Vector3.back), d.yaw, d.open, d.reach, d.spring);
            }
            foreach (var pl in plumes) Emitters.Add(go.transform.TransformPoint(pl.p), pl.kind);
            foreach (Swing w in wheels)
            {
                GameObject wheel = Models.Spawn(w.model, go.transform, w.hinge, Quaternion.Euler(w.offset));
                Windmills.Add(wheel.transform);
            }
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            foreach (Spot s in spots) region.Mark(prefix + s.key, pos + q * s.p, yaw + s.yaw);
            return go;
        }
    }

    /// <summary>Night lighting shared by a region: window glow and lamps that come on at dusk.</summary>
    public static class RegionLights
    {
        static Material window, sign;
        static readonly List<Light> lamps = new List<Light>();

        public static Material WindowMaterial
        {
            get
            {
                if (window == null) { window = new Material(Mats.Emissive); window.SetColor("_EmissionColor", Color.black); }
                return window;
            }
        }

        public static Material SignMaterial
        {
            get
            {
                if (sign == null)
                {
                    sign = new Material(Resources.Load<Material>("Mat/Text3D"));
                    sign.mainTexture = UI.font.material.mainTexture;
                }
                return sign;
            }
        }

        /// <summary>URP point lights fall off with the square of the distance: the old values lit a room like a candle.</summary>
        public const float Boost = 4.5f;

        struct Entry { public Light light; public float baseIntensity, seed; public bool fire; }
        static readonly List<Entry> all = new List<Entry>();
        static readonly List<Light> shadowed = new List<Light>();
        static float pickAt;

        public static void Register(Light l, bool nightOnly, bool fire)
        {
            if (nightOnly) lamps.Add(l);
            all.Add(new Entry { light = l, baseIntensity = l.intensity, seed = all.Count * 7.31f, fire = fire });
        }

        public static void Clear() { lamps.Clear(); all.Clear(); shadowed.Clear(); }

        /// <summary>Fires flicker; the two lights nearest to the camera cast shadows.</summary>
        public static void Tick(Vector3 eye, float dt)
        {
            float t = Time.time;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                Entry e = all[i];
                if (e.light == null) { all.RemoveAt(i); continue; }
                if (e.fire)
                {
                    float f = 0.82f + 0.12f * Mathf.Sin(t * 9.1f + e.seed) + 0.1f * Mathf.Sin(t * 23.7f + e.seed * 2.3f) + 0.06f * Mathf.Sin(t * 41f + e.seed * 5.1f);
                    e.light.intensity = e.baseIntensity * f;
                }
            }
            pickAt -= dt;
            if (pickAt > 0f) return;
            pickAt = 0.3f;
            Light best = null, second = null;
            float bd = 34f * 34f, sd = 34f * 34f;
            foreach (Entry e in all)
            {
                if (e.light == null || !e.light.enabled || !e.light.gameObject.activeInHierarchy) continue;
                float d = (e.light.transform.position - eye).sqrMagnitude;
                if (d < bd) { second = best; sd = bd; best = e.light; bd = d; }
                else if (d < sd) { second = e.light; sd = d; }
            }
            foreach (Light l in shadowed) if (l != null && l != best && l != second) l.shadows = LightShadows.None;
            shadowed.Clear();
            if (best != null) { best.shadows = LightShadows.Soft; shadowed.Add(best); }
            if (second != null) { second.shadows = LightShadows.Soft; shadowed.Add(second); }
        }

        public static void SetNight(bool night)
        {
            // lamplight behind the panes: warm and no brighter than the glazing bars can stand against
            WindowMaterial.SetColor("_EmissionColor", night ? new Color(1f, 0.70f, 0.34f).linear * 2.8f : Color.black);
            for (int i = lamps.Count - 1; i >= 0; i--)
            {
                if (lamps[i] == null) { lamps.RemoveAt(i); continue; }
                lamps[i].enabled = night;
            }
        }
    }

    /// <summary>Furniture, wagons, fences and other set dressing, built into a Site.</summary>
    public static class Props
    {
        static readonly Color32 Wood = Pal.WoodOld, Dark = Pal.WoodDark;

        public static void Barrel(Site s, Vector3 p, bool collide = true)
        {
            if (s.Model("barrel", p, U.Hash01(Mathf.RoundToInt(p.x * 7f), Mathf.RoundToInt(p.z * 7f)) * 360f))
            {
                if (collide) s.Collider(p + Vector3.up * 0.45f, new Vector3(0.6f, 0.9f, 0.6f));
                return;
            }
            s.solid.Col(Pal.WoodOld);
            s.solid.Lathe(p, new List<Vector2> { new Vector2(0.26f, 0f), new Vector2(0.33f, 0.3f), new Vector2(0.33f, 0.6f), new Vector2(0.26f, 0.9f) }, 10);
            s.solid.Disc(p + Vector3.up * 0.9f, Vector3.up, 0.26f, 10);
            s.solid.Col(Pal.Iron, 0.5f);
            s.solid.Cyl(p + Vector3.up * 0.2f, p + Vector3.up * 0.25f, 0.325f, 0.33f, 10, false, false);
            s.solid.Cyl(p + Vector3.up * 0.65f, p + Vector3.up * 0.7f, 0.33f, 0.325f, 10, false, false);
            if (collide) s.Collider(p + Vector3.up * 0.45f, new Vector3(0.6f, 0.9f, 0.6f));
        }

        public static void Crate(Site s, Vector3 p, float size = 0.7f, float yaw = 0f, bool collide = true)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            if (s.Model("crate", p, yaw, size / 0.72f))
            {
                if (collide) s.Collider(p + Vector3.up * size * 0.5f, new Vector3(size, size, size), q);
                return;
            }
            s.BoxRot(p + Vector3.up * size * 0.5f, new Vector3(size, size, size), q, Pal.WoodNew, collide);
            s.solid.Col(Dark);
            s.solid.Push(p + Vector3.up * size * 0.5f, q);
            float e = size * 0.5f + 0.006f;
            s.solid.Box(new Vector3(0, 0, -e), new Vector3(size, 0.07f, 0.012f));
            s.solid.Box(new Vector3(0, 0, e), new Vector3(size, 0.07f, 0.012f));
            s.solid.Box(new Vector3(-e, 0, 0), new Vector3(0.012f, 0.07f, size));
            s.solid.Box(new Vector3(e, 0, 0), new Vector3(0.012f, 0.07f, size));
            s.solid.Pop();
        }

        public static void Chair(Site s, Vector3 p, float yaw, string seatKey = null)
        {
            if (s.Model("chair", p, yaw))
            {
                if (seatKey != null) s.Mark(seatKey, p, yaw);
                return;
            }
            s.solid.Col(Wood);
            s.solid.Push(p, Quaternion.Euler(0f, yaw, 0f));
            s.solid.Box(new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.04f, 0.42f));
            foreach (float x in new[] { -0.18f, 0.18f })
                foreach (float z in new[] { -0.18f, 0.18f })
                    s.solid.Box(new Vector3(x, z < 0f ? 0.48f : 0.225f, z), new Vector3(0.04f, z < 0f ? 0.96f : 0.45f, 0.04f));
            s.solid.Box(new Vector3(0, 0.8f, -0.18f), new Vector3(0.4f, 0.22f, 0.03f));
            s.solid.Pop();
            if (seatKey != null) s.Mark(seatKey, p, yaw);
        }

        public static void Table(Site s, Vector3 p, float radius = 0.55f)
        {
            if (s.Model("table", p, 0f, radius / 0.55f))
            {
                s.Collider(p + Vector3.up * 0.4f, new Vector3(radius * 1.5f, 0.8f, radius * 1.5f));
                return;
            }
            s.solid.Col(Wood);
            s.solid.Cyl(p + Vector3.up * 0.72f, p + Vector3.up * 0.77f, radius, radius, 12);
            s.solid.Cyl(p, p + Vector3.up * 0.72f, 0.06f, 0.05f, 6);
            s.solid.Cyl(p, p + Vector3.up * 0.04f, 0.28f, 0.22f, 8);
            s.Collider(p + Vector3.up * 0.4f, new Vector3(radius * 1.5f, 0.8f, radius * 1.5f));
        }

        public static void Bench(Site s, Vector3 p, float yaw, float len = 1.6f, string seatKey = null)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            if (s.Model("bench", p, yaw, new Vector3(len / 1.6f, 1f, 1f)))
            {
                s.Collider(p + Vector3.up * 0.25f, new Vector3(len, 0.5f, 0.36f), q);
                if (seatKey != null) s.Mark(seatKey, p, yaw);
                return;
            }
            s.solid.Col(Wood);
            s.solid.Push(p, q);
            s.solid.Box(new Vector3(0, 0.45f, 0), new Vector3(len, 0.05f, 0.36f));
            s.solid.Box(new Vector3(0, 0.75f, -0.17f), new Vector3(len, 0.26f, 0.04f));
            s.solid.Box(new Vector3(-len * 0.45f, 0.22f, 0), new Vector3(0.05f, 0.45f, 0.34f));
            s.solid.Box(new Vector3(len * 0.45f, 0.22f, 0), new Vector3(0.05f, 0.45f, 0.34f));
            s.solid.Pop();
            s.Collider(p + Vector3.up * 0.25f, new Vector3(len, 0.5f, 0.36f), q);
            if (seatKey != null) s.Mark(seatKey, p, yaw);
        }

        public static void Bed(Site s, Vector3 p, float yaw)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            if (s.Model("bed", p, yaw))
            {
                s.Collider(p + Vector3.up * 0.3f, new Vector3(0.9f, 0.6f, 1.95f), q);
                return;
            }
            s.solid.Push(p, q);
            s.solid.Col(Dark);
            s.solid.Box(new Vector3(0, 0.3f, 0), new Vector3(0.9f, 0.12f, 1.95f));
            s.solid.Box(new Vector3(0, 0.55f, -0.95f), new Vector3(0.9f, 0.7f, 0.06f));
            s.solid.Col(Pal.ClothCream);
            s.solid.Box(new Vector3(0, 0.42f, 0.05f), new Vector3(0.84f, 0.14f, 1.75f));
            s.solid.Col(Pal.ClothRed);
            s.solid.Box(new Vector3(0, 0.5f, 0.3f), new Vector3(0.86f, 0.05f, 1.2f));
            s.solid.Pop();
            s.Collider(p + Vector3.up * 0.3f, new Vector3(0.9f, 0.6f, 1.95f), q);
        }

        /// <summary>A shop counter from a to b (local points on the floor), 1 m high; customers stand to the right of
        /// the run. A 'bar' is the saloon's: a wider top to lean on and a brass foot rail.</summary>
        public static void Counter(Site s, Vector3 a, Vector3 b, float depth = 0.6f, bool bar = false)
        {
            Vector3 mid = (a + b) * 0.5f, d = b - a;
            Quaternion q = Quaternion.LookRotation(d.normalized, Vector3.up);
            float len = d.magnitude;
            string model = bar ? "bar_counter" : "shop_counter";
            if (Models.Has(model))
            {
                // metre lengths of the Blender counter, so its panels keep their shape whatever the run
                int n = Mathf.Max(1, Mathf.RoundToInt(len));
                for (int i = 0; i < n; i++) s.Model(model, a + d * ((i + 0.5f) / n), q, new Vector3(depth / 0.6f, 1f, len / n));
                s.Collider(mid + Vector3.up * 0.52f, new Vector3(depth, 1.04f, len), q);
                return;
            }
            s.BoxRot(mid + Vector3.up * 0.5f, new Vector3(depth, 1.0f, len), q, Wood);
            s.solid.Col(Dark);
            s.solid.Box(mid + Vector3.up * 1.02f, new Vector3(depth + 0.1f, 0.05f, len + 0.1f), q);
        }

        static readonly string[] BarGoods = { "goods_bottle", "goods_bottle", "goods_bottle_sq", "goods_bottle", "goods_jug" };
        static readonly string[] ShopGoods = { "goods_tin", "goods_tin", "goods_jar", "goods_box", "goods_box", "goods_bolts", "goods_sack", "goods_bottle", "goods_jug", "goods_tin" };
        static readonly Color32[] GlassDyes = { Pal.H(0x3D5A38), Pal.H(0x6E4A1E), Pal.H(0x2F3D47), Pal.H(0x7A2C20), Pal.H(0xA9A680), Pal.H(0x47301C) };
        static readonly Color32[] CrockDyes = { Pal.H(0xB59B6A), Pal.H(0x8C7A5C), Pal.H(0x6B4A2E), Pal.H(0xC8BFA6) };
        static readonly Color32[] LabelDyes = { Pal.ClothRed, Pal.ClothBlue, Pal.ClothYellow, Pal.ClothGreen, Pal.WoodNew, Pal.ClothCream, Pal.Brass, Pal.H(0x8A5A3A) };

        /// <summary>Shelves against a wall, filled with goods (a saloon's with bottles).</summary>
        public static void Shelves(Site s, Vector3 p, float yaw, float width, int seed, bool bottles = false)
        {
            var r = new System.Random(seed);
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            bool kit = Models.Has("shop_shelf") && Models.Has("goods_tin");
            int bays = Mathf.Max(1, Mathf.RoundToInt(width));
            if (kit)
                for (int i = 0; i < bays; i++)
                    s.Model("shop_shelf", p + q * new Vector3(-width * 0.5f + (i + 0.5f) * width / bays, 0f, 0f), q, new Vector3(width / bays, 1f, 1f));
            s.solid.Push(p, q);
            if (!kit)
            {
                s.solid.Col(Dark);
                s.solid.Box(new Vector3(0, 1.1f, 0), new Vector3(width, 2.2f, 0.06f));
            }
            for (int i = 0; i < 4; i++)
            {
                float y = 0.35f + i * 0.5f;
                if (!kit)
                {
                    s.solid.Col(Wood);
                    s.solid.Box(new Vector3(0, y, 0.17f), new Vector3(width, 0.035f, 0.3f));
                }
                int n = Mathf.FloorToInt(width / 0.22f);
                for (int k = 0; k < n; k++)
                {
                    if (r.NextDouble() < 0.25) continue;
                    float x = -width * 0.5f + 0.12f + k * 0.22f;
                    if (kit)
                    {
                        // keep clear of the uprights between bays
                        float bay = width / bays, off = Mathf.Repeat(x + width * 0.5f, bay);
                        if (off < 0.1f) x += 0.1f - off; else if (bay - off < 0.1f) x -= 0.1f - (bay - off);
                        string g = U.Pick(r, bottles ? BarGoods : ShopGoods);
                        Color32 dye = g == "goods_jug" ? U.Pick(r, CrockDyes) : g.StartsWith("goods_bottle") || g == "goods_jar" ? U.Pick(r, GlassDyes) : U.Pick(r, LabelDyes);
                        float size = 0.9f + (float)r.NextDouble() * 0.25f;
                        s.Model(g, p + q * new Vector3(x, y + 0.018f, 0.16f + (float)r.NextDouble() * 0.05f), q * Quaternion.Euler(0f, (float)r.NextDouble() * 50f - 25f, 0f),
                                new Vector3(size, size, size), dye);
                        continue;
                    }
                    Color32[] cols = { Pal.ClothRed, Pal.ClothBlue, Pal.ClothYellow, Pal.Tin, Pal.ClothGreen, Pal.WoodNew, Pal.ClothCream, Pal.Brass };
                    s.solid.Col(U.Pick(r, cols), bottles ? 0.8f : 0.2f);
                    float hgt = 0.12f + (float)r.NextDouble() * 0.16f;
                    if (bottles || r.NextDouble() < 0.5)
                        s.solid.Cyl(new Vector3(x, y + 0.02f, 0.17f), new Vector3(x, y + 0.02f + hgt, 0.17f), 0.045f, bottles ? 0.02f : 0.045f, 6);
                    else s.solid.Box(new Vector3(x, y + 0.02f + hgt * 0.5f, 0.17f), new Vector3(0.14f, hgt, 0.16f));
                }
            }
            s.solid.Pop();
            s.Collider(p + q * new Vector3(0, 1.1f, 0.17f), new Vector3(width, 2.2f, 0.34f), q);
        }

        /// <summary>Long guns stood in a rack against a wall: butts on 'foot', muzzles up, their sides to the room ('room' is the way out from the wall).</summary>
        public static void GunRack(Site s, Vector3 foot, Vector3 step, Vector3 room, params string[] guns)
        {
            for (int i = 0; i < guns.Length; i++)
            {
                if (!Models.Has(guns[i])) continue;
                ModelLibrary.Entry e = Models.Get(guns[i]);
                // the gun's barrel (its z) goes up and leans back to the wall, its right side faces the room
                Quaternion q = Quaternion.LookRotation(Vector3.up - room * 0.06f, Vector3.Cross(Vector3.up, room));
                Vector3 butt = new Vector3(e.centre.x, e.centre.y, e.centre.z - e.size.z * 0.5f);
                s.Model(guns[i], foot + step * i - q * butt, q, Vector3.one);
            }
        }

        public static void HitchRail(Site s, Vector3 p, float yaw, float len = 3f, string key = null)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            if (s.Model("hitchrail", p, yaw, new Vector3(len / 3f, 1f, 1f)))
            {
                s.Collider(p + Vector3.up * 0.55f, new Vector3(len, 1.1f, 0.14f), q);
                if (key != null) s.Mark(key, p + q * new Vector3(0, 0, -1.4f), yaw);
                return;
            }
            s.solid.Col(Dark);
            s.solid.Push(p, q);
            s.solid.Box(new Vector3(0, 1.0f, 0), new Vector3(len, 0.1f, 0.1f));
            s.solid.Box(new Vector3(-len * 0.45f, 0.5f, 0), new Vector3(0.12f, 1.0f, 0.12f));
            s.solid.Box(new Vector3(len * 0.45f, 0.5f, 0), new Vector3(0.12f, 1.0f, 0.12f));
            s.solid.Pop();
            s.Collider(p + Vector3.up * 0.55f, new Vector3(len, 1.1f, 0.14f), q);
            if (key != null) s.Mark(key, p + q * new Vector3(0, 0, -1.4f), yaw);
        }

        public static void Trough(Site s, Vector3 p, float yaw)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            if (s.Model("trough", p, yaw))
            {
                s.Collider(p + Vector3.up * 0.28f, new Vector3(1.8f, 0.5f, 0.55f), q);
                return;
            }
            s.BoxRot(p + Vector3.up * 0.28f, new Vector3(1.8f, 0.5f, 0.55f), q, Wood);
            s.solid.Col(Pal.H(0x3F5B63), 0.9f);
            s.solid.Box(p + Vector3.up * 0.5f, new Vector3(1.7f, 0.06f, 0.45f), q);
        }

        public static void LampPost(Site s, Vector3 p)
        {
            if (!s.Model("lamppost", p))
            {
                s.solid.Col(Dark);
                s.solid.Box(p + Vector3.up * 1.5f, new Vector3(0.12f, 3f, 0.12f));
                s.solid.Box(p + new Vector3(0.25f, 2.9f, 0), new Vector3(0.6f, 0.07f, 0.07f));
                s.solid.Col(Pal.Iron, 0.5f);
                s.solid.Box(p + new Vector3(0.5f, 2.62f, 0), new Vector3(0.2f, 0.3f, 0.2f));
            }
            s.glass.Col(Pal.GlassLit);
            s.glass.Box(p + new Vector3(0.5f, 2.62f, 0), new Vector3(0.16f, 0.22f, 0.24f));
            s.glass.Box(p + new Vector3(0.5f, 2.62f, 0), new Vector3(0.24f, 0.22f, 0.16f));
            s.Light(p + new Vector3(0.5f, 2.6f, 0), new Color(1f, 0.72f, 0.4f), 13f, 2.4f);
            s.Collider(p + Vector3.up * 1.5f, new Vector3(0.16f, 3f, 0.16f));
        }

        public static void Well(Site s, Vector3 p)
        {
            if (s.Model("well", p))
            {
                s.Collider(p + Vector3.up * 0.5f, new Vector3(1.7f, 1f, 1.7f));
                return;
            }
            s.solid.Col(Pal.Stone);
            s.solid.Cyl(p, p + Vector3.up * 0.8f, 0.85f, 0.85f, 12, false, false);
            s.solid.Cyl(p + Vector3.up * 0.8f, p + Vector3.up * 0.8f, 0.85f, 0.85f, 12);
            s.solid.Col(Pal.H(0x1E2A30));
            s.solid.Disc(p + Vector3.up * 0.55f, Vector3.up, 0.7f, 12);
            s.solid.Col(Dark);
            s.solid.Box(p + new Vector3(-0.8f, 1.4f, 0), new Vector3(0.12f, 2.0f, 0.12f));
            s.solid.Box(p + new Vector3(0.8f, 1.4f, 0), new Vector3(0.12f, 2.0f, 0.12f));
            s.solid.Cyl(p + new Vector3(-0.8f, 2.1f, 0), p + new Vector3(0.8f, 2.1f, 0), 0.07f, 0.07f, 6);
            s.thin.Col(Pal.Shingle);
            s.thin.Box(p + new Vector3(0, 2.55f, -0.38f), new Vector3(2.0f, 0.05f, 1.0f), Quaternion.Euler(-32f, 0, 0));
            s.thin.Box(p + new Vector3(0, 2.55f, 0.38f), new Vector3(2.0f, 0.05f, 1.0f), Quaternion.Euler(32f, 0, 0));
            s.Collider(p + Vector3.up * 0.5f, new Vector3(1.7f, 1f, 1.7f));
        }

        public static void WaterTower(Site s, Vector3 p)
        {
            foreach (float x in new[] { -1.4f, 1.4f })
                foreach (float z in new[] { -1.4f, 1.4f })
                    s.Collider(p + new Vector3(x * 1.12f, 3f, z * 1.12f), new Vector3(0.3f, 6f, 0.3f));
            if (s.Model("water_tower", p)) return;
            s.solid.Col(Dark);
            foreach (float x in new[] { -1.4f, 1.4f })
                foreach (float z in new[] { -1.4f, 1.4f })
                    s.solid.Beam(p + new Vector3(x * 1.25f, 0, z * 1.25f), p + new Vector3(x, 6f, z), 0.2f, 0.2f);
            s.solid.Beam(p + new Vector3(-1.6f, 2.2f, -1.6f), p + new Vector3(1.6f, 4f, -1.6f), 0.1f, 0.1f);
            s.solid.Beam(p + new Vector3(-1.6f, 2.2f, 1.6f), p + new Vector3(1.6f, 4f, 1.6f), 0.1f, 0.1f);
            s.solid.Beam(p + new Vector3(-1.6f, 4f, -1.6f), p + new Vector3(-1.6f, 2.2f, 1.6f), 0.1f, 0.1f);
            s.solid.Beam(p + new Vector3(1.6f, 4f, -1.6f), p + new Vector3(1.6f, 2.2f, 1.6f), 0.1f, 0.1f);
            s.solid.Box(p + Vector3.up * 6.05f, new Vector3(3.6f, 0.15f, 3.6f));
            s.solid.Col(Pal.WoodRed);
            s.solid.Cyl(p + Vector3.up * 6.1f, p + Vector3.up * 9.3f, 1.9f, 1.75f, 14);
            s.solid.Col(Pal.Iron, 0.4f);
            for (int i = 0; i < 3; i++) s.solid.Cyl(p + Vector3.up * (6.6f + i * 1.1f), p + Vector3.up * (6.7f + i * 1.1f), 1.93f, 1.93f, 14, false, false);
            s.solid.Col(Pal.Shingle);
            s.solid.Cyl(p + Vector3.up * 9.3f, p + Vector3.up * 10.4f, 2.1f, 0.05f, 14);
            s.solid.Col(Pal.Iron);
            s.solid.Beam(p + new Vector3(0, 6.3f, -1.9f), p + new Vector3(0, 4.6f, -3.6f), 0.14f, 0.14f);
        }

        /// <summary>A wind pump: the tower stands still, the wheel turns with the weather (Windmills).</summary>
        public static void Windmill(Site s, Vector3 p)
        {
            s.Collider(p + Vector3.up * 2f, new Vector3(2.4f, 4f, 2.4f));
            if (Models.Has("windmill_wheel") && s.Model("windmill_tower", p))
            {
                if (!Models.Get("windmill_tower").Mark("hub", out Vector3 hubAt)) hubAt = new Vector3(0, 9.2f, -0.52f);
                s.WindWheel("windmill_wheel", p + hubAt, Quaternion.identity);
                return;
            }
            s.solid.Col(Pal.WoodGrey);
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f })
                    s.solid.Beam(p + new Vector3(x * 1.3f, 0, z * 1.3f), p + new Vector3(x * 0.25f, 9f, z * 0.25f), 0.14f, 0.14f);
            for (int i = 1; i < 4; i++)
            {
                float y = i * 2.2f, w = Mathf.Lerp(1.3f, 0.25f, y / 9f);
                s.solid.Beam(p + new Vector3(-w, y, -w), p + new Vector3(w, y, -w), 0.08f, 0.08f);
                s.solid.Beam(p + new Vector3(-w, y, w), p + new Vector3(w, y, w), 0.08f, 0.08f);
                s.solid.Beam(p + new Vector3(-w, y, -w), p + new Vector3(-w, y, w), 0.08f, 0.08f);
                s.solid.Beam(p + new Vector3(w, y, -w), p + new Vector3(w, y, w), 0.08f, 0.08f);
            }
            s.thin.Col(Pal.Tin, 0.5f);
            Vector3 hub = p + new Vector3(0, 9.2f, -0.5f);
            for (int i = 0; i < 12; i++)
            {
                Quaternion q = Quaternion.Euler(0, 0, i * 30f);
                s.thin.Push(hub, q);
                s.thin.Box(new Vector3(0, 0.95f, 0), new Vector3(0.36f, 1.3f, 0.02f), Quaternion.Euler(0, 22f, 0));
                s.thin.Pop();
            }
            s.thin.Box(p + new Vector3(0, 9.2f, 1.3f), new Vector3(0.03f, 0.9f, 1.4f));
        }

        /// <summary>
        /// Post-and-rail fence along world or local points; where 'gapAfter' holds the index the run is left open and
        /// a gate stands folded back outside it. Three rails and more make a corral of poles, fewer a yard fence of
        /// sawn boards. Posts and rails come from the Blender kit (yard.py), a bay at a time, a little out of true.
        /// </summary>
        public static void Fence(Site s, IList<Vector3> pts, float height = 1.1f, int rails = 2, ICollection<int> gapAfter = null, Color32? color = null)
        {
            Color32 c = color ?? Pal.WoodGrey;
            bool poles = rails >= 3;
            string postModel = poles ? "fence_post_round" : "fence_post", railModel = poles ? "fence_pole" : "fence_rail";
            bool kit = Models.Has(postModel) && Models.Has(railModel);
            var rng = new System.Random(pts.Count * 7919 + Mathf.RoundToInt(pts[0].x * 13f + pts[0].z * 7f));
            float R(float span) { return ((float)rng.NextDouble() - 0.5f) * 2f * span; }
            Vector3 middle = Vector3.zero;
            foreach (Vector3 p in pts) middle += p;
            middle /= pts.Count;
            var standing = new List<Vector3>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                float len = Vector3.Distance(a, b);
                if (gapAfter != null && gapAfter.Contains(i))
                {
                    if (kit) Gates(s, a, b, middle, height, c);
                    continue;
                }
                int n = Mathf.Max(1, Mathf.RoundToInt(len / 2.4f));
                Vector3 flat = b - a; flat.y = 0f;
                Quaternion along = Quaternion.LookRotation(flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward, Vector3.up);
                for (int k = 0; k <= n; k++)
                {
                    Vector3 p = Vector3.Lerp(a, b, k / (float)n);
                    if (kit)
                    {
                        // a corner post is shared by two runs
                        bool there = false;
                        foreach (Vector3 q in standing) if ((q - p).sqrMagnitude < 0.01f) { there = true; break; }
                        if (there) continue;
                        standing.Add(p);
                        s.Model(postModel, p, along * Quaternion.Euler(R(2.5f), R(20f), R(2.5f)), new Vector3(1f, height * (1.02f + R(0.04f)), 1f), Pal.Shade(c, 0.9f));
                    }
                    else
                    {
                        s.solid.Col(Pal.Shade(c, 0.85f));
                        s.solid.Box(p + Vector3.up * height * 0.5f, new Vector3(0.11f, height, 0.11f));
                    }
                }
                s.solid.Col(c);
                for (int r = 0; r < rails; r++)
                {
                    float y = height * (0.42f + 0.5f * r / Mathf.Max(1, rails - 1) * (rails > 1 ? 1f : 0f));
                    if (!kit) { s.solid.Beam(a + Vector3.up * y, b + Vector3.up * y, 0.05f, 0.1f); continue; }
                    for (int k = 0; k < n; k++)
                    {
                        Vector3 from = Vector3.Lerp(a, b, k / (float)n) + Vector3.up * (y + R(0.02f)), to = Vector3.Lerp(a, b, (k + 1) / (float)n) + Vector3.up * (y + R(0.02f));
                        s.Model(railModel, from, Quaternion.LookRotation(to - from, Vector3.up), new Vector3(1f, 1f, (to - from).magnitude / 2.4f), Pal.Shade(c, 0.94f + R(0.06f)));
                    }
                }
                Vector3 mid = (a + b) * 0.5f + Vector3.up * height * 0.5f;
                s.Collider(mid, new Vector3(0.12f, height, len), Quaternion.LookRotation((b - a).normalized, Vector3.up));
            }
        }

        /// <summary>The gates of a gap in a fence, swung right back against the outside of the run: one leaf, or a pair for a wide gap.</summary>
        static void Gates(Site s, Vector3 a, Vector3 b, Vector3 inside, float height, Color32 c)
        {
            Vector3 g = b - a; g.y = 0f;
            float gap = g.magnitude;
            if (gap < 1.2f || !Models.Has("fence_gate")) return;
            g /= gap;
            Vector3 outward = Vector3.Cross(Vector3.up, g);
            Vector3 fromInside = (a + b) * 0.5f - inside; fromInside.y = 0f;
            if (Vector3.Dot(outward, fromInside) < 0f) outward = -outward;
            bool pair = gap > 4.5f;
            float width = Mathf.Min(3f, pair ? gap * 0.5f : gap - 0.2f);
            void Leaf(Vector3 hinge, Vector3 back)
            {
                Vector3 dir = (back * 0.985f + outward * 0.17f).normalized;        // folded back, a hand off the fence
                float yaw = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;            // the gate runs along its own +x
                s.Model("fence_gate", hinge + outward * 0.12f, yaw, new Vector3(width / 3f, height / 1.2f, 1f), Pal.Shade(c, 0.95f));
            }
            Leaf(a, -g);
            if (pair) Leaf(b, g);
        }

        public static void Wheel(MeshBuilder b, Vector3 centre, Vector3 axis, float radius)
        {
            axis.Normalize();
            b.Col(Pal.WoodDark);
            Vector3 u = Vector3.Cross(axis, Vector3.up).normalized, v = Vector3.Cross(axis, u);
            for (int i = 0; i < 12; i++)
            {
                float a0 = i * Mathf.PI / 6f, a1 = (i + 1) * Mathf.PI / 6f;
                Vector3 p0 = centre + (u * Mathf.Cos(a0) + v * Mathf.Sin(a0)) * radius, p1 = centre + (u * Mathf.Cos(a1) + v * Mathf.Sin(a1)) * radius;
                b.Beam(p0, p1, 0.07f, 0.07f, axis);
                if (i % 2 == 0) b.Beam(centre, p0, 0.035f, 0.05f, axis);
            }
            b.Cyl(centre - axis * 0.08f, centre + axis * 0.08f, 0.09f, 0.09f, 8);
        }

        /// <summary>A covered settler wagon, tongue towards +Z. 4.2 m long.</summary>
        public static void Wagon(Site s, Vector3 p, float yaw, bool covered = true, Color32? canvas = null, string key = null)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            if (s.Model("wagon", p, yaw))
            {
                if (covered) s.Model("wagon_cover", p, yaw, Vector3.one, canvas ?? Pal.Canvas);
                s.Collider(p + q * new Vector3(0, 1.3f, 0), new Vector3(1.5f, covered ? 2.6f : 1.1f, 3.7f), q);
                if (key != null) s.Mark(key, p, yaw);
                return;
            }
            MeshBuilder b = s.solid;
            b.Push(p, q);
            b.Col(Pal.WoodOld);
            b.Box(new Vector3(0, 1.05f, 0), new Vector3(1.35f, 0.1f, 3.6f));
            b.Box(new Vector3(-0.67f, 1.4f, 0), new Vector3(0.06f, 0.65f, 3.6f));
            b.Box(new Vector3(0.67f, 1.4f, 0), new Vector3(0.06f, 0.65f, 3.6f));
            b.Box(new Vector3(0, 1.4f, -1.8f), new Vector3(1.35f, 0.65f, 0.06f));
            b.Box(new Vector3(0, 1.3f, 1.8f), new Vector3(1.35f, 0.45f, 0.06f));
            b.Col(Pal.WoodDark);
            b.Box(new Vector3(0, 1.75f, 1.55f), new Vector3(1.3f, 0.06f, 0.4f));          // driver's seat
            b.Box(new Vector3(0, 0.75f, -1.25f), new Vector3(1.6f, 0.1f, 0.1f));
            b.Box(new Vector3(0, 0.6f, 1.25f), new Vector3(1.6f, 0.1f, 0.1f));
            b.Beam(new Vector3(0, 0.6f, 1.3f), new Vector3(0, 0.75f, 4.1f), 0.08f, 0.08f);      // tongue
            Wheel(b, new Vector3(-0.85f, 0.75f, -1.25f), Vector3.right, 0.75f);
            Wheel(b, new Vector3(0.85f, 0.75f, -1.25f), Vector3.right, 0.75f);
            Wheel(b, new Vector3(-0.85f, 0.6f, 1.25f), Vector3.right, 0.6f);
            Wheel(b, new Vector3(0.85f, 0.6f, 1.25f), Vector3.right, 0.6f);
            b.Pop();
            if (covered)
            {
                MeshBuilder t = s.thin;
                t.Push(p, q);
                t.Col(canvas ?? Pal.Canvas);
                var rings = new List<Vector3[]>();
                foreach (float z in new[] { -1.95f, -0.9f, 0.3f, 1.5f })
                {
                    var ring = new Vector3[9];
                    for (int i = 0; i < 9; i++)
                    {
                        float a = Mathf.PI * i / 8f;
                        float bulge = Mathf.Abs(z) > 1.4f ? 1.06f : 1f;
                        ring[i] = new Vector3(Mathf.Cos(a) * 0.78f * bulge, 1.7f + Mathf.Sin(a) * 1.2f * bulge, z);
                    }
                    rings.Add(ring);
                }
                int start = t.verts.Count, t0 = t.tris.Count;
                foreach (Vector3[] ring in rings) foreach (Vector3 v in ring) t.Vert(v, Vector3.up);
                for (int j = 0; j < rings.Count - 1; j++)
                    for (int i = 0; i < 8; i++)
                    {
                        int k = start + j * 9 + i;
                        t.Tri(k, k + 9, k + 10);
                        t.Tri(k, k + 10, k + 1);
                    }
                t.SmoothNormals(start, t.verts.Count, t0, t.tris.Count);
                t.Pop();
            }
            s.Collider(p + q * new Vector3(0, 1.3f, 0), new Vector3(1.5f, covered ? 2.6f : 1.1f, 3.7f), q);
            if (key != null) s.Mark(key, p, yaw);
        }

        public static void Tent(Site s, Vector3 p, float yaw, float w = 2.6f, float len = 3.2f, Color32? canvas = null)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            if (s.Model("tent", p, yaw, new Vector3(w / 2.6f, 1f, len / 3.2f), canvas ?? Pal.Canvas))
            {
                s.Collider(p + q * new Vector3(0, 0.8f, -len * 0.5f), new Vector3(w * 0.8f, 1.6f, 0.2f), q);
                return;
            }
            MeshBuilder t = s.thin;
            t.Push(p, q);
            t.Col(canvas ?? Pal.Canvas);
            float h = 2.0f;
            Vector3 r0 = new Vector3(0, h, -len * 0.5f), r1 = new Vector3(0, h, len * 0.5f);
            t.Quad(new Vector3(-w * 0.5f, 0, -len * 0.5f), r0, r1, new Vector3(-w * 0.5f, 0, len * 0.5f));
            t.Quad(new Vector3(w * 0.5f, 0, len * 0.5f), r1, r0, new Vector3(w * 0.5f, 0, -len * 0.5f));
            t.Tri(new Vector3(-w * 0.5f, 0, -len * 0.5f), new Vector3(w * 0.5f, 0, -len * 0.5f), r0);
            t.Pop();
            s.solid.Col(Dark);
            s.solid.Push(p, q);
            s.solid.Box(new Vector3(0, h * 0.5f, len * 0.5f), new Vector3(0.07f, h, 0.07f));
            s.solid.Box(new Vector3(0, h * 0.5f, -len * 0.5f), new Vector3(0.07f, h, 0.07f));
            s.solid.Pop();
            s.Collider(p + q * new Vector3(0, 0.8f, -len * 0.5f), new Vector3(w * 0.8f, 1.6f, 0.2f), q);
        }

        public static void Campfire(Site s, Vector3 p, bool lit = true)
        {
            if (lit) s.Smoke(Plume.Campfire, p + Vector3.up * 0.14f);
            if (s.Model("campfire", p))
            {
                if (lit) s.Light(p + Vector3.up * 0.6f, new Color(1f, 0.55f, 0.22f), 16f, 4.2f, true, true);
                return;
            }
            s.solid.Col(Pal.StoneDark);
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI * 2f / 9f;
                s.solid.Ellipsoid(p + new Vector3(Mathf.Cos(a) * 0.55f, 0.07f, Mathf.Sin(a) * 0.55f), new Vector3(0.14f, 0.1f, 0.12f), 6, 4);
            }
            s.solid.Col(Pal.Ash);
            s.solid.Beam(p + new Vector3(-0.3f, 0.08f, -0.1f), p + new Vector3(0.3f, 0.14f, 0.1f), 0.1f, 0.1f);
            s.solid.Beam(p + new Vector3(-0.1f, 0.08f, 0.3f), p + new Vector3(0.1f, 0.16f, -0.3f), 0.1f, 0.1f);
            if (lit) s.Light(p + Vector3.up * 0.6f, new Color(1f, 0.55f, 0.22f), 16f, 4.2f, true, true);
        }

        public static void LogSeat(Site s, Vector3 p, float yaw, string seatKey = null)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            if (s.Model("logseat", p, yaw))
            {
                s.Collider(p + Vector3.up * 0.2f, new Vector3(1.6f, 0.4f, 0.4f), q);
                if (seatKey != null) s.Mark(seatKey, p + q * new Vector3(0, 0, 0.05f), yaw);
                return;
            }
            s.solid.Col(Pal.Bark);
            s.solid.Cyl(p + q * new Vector3(-0.8f, 0.2f, 0), p + q * new Vector3(0.8f, 0.2f, 0), 0.2f, 0.2f, 8);
            s.Collider(p + Vector3.up * 0.2f, new Vector3(1.6f, 0.4f, 0.4f), q);
            if (seatKey != null) s.Mark(seatKey, p + q * new Vector3(0, 0, 0.05f), yaw);
        }

        public static void HayBale(Site s, Vector3 p, float yaw)
        {
            if (s.Model("haybale", p, yaw))
            {
                s.Collider(p + Vector3.up * 0.3f, new Vector3(1.0f, 0.6f, 0.55f), Quaternion.Euler(0f, yaw, 0f));
                return;
            }
            s.BoxRot(p + Vector3.up * 0.3f, new Vector3(1.0f, 0.6f, 0.55f), Quaternion.Euler(0f, yaw, 0f), Pal.Hay);
        }

        public static void Grave(Site s, Vector3 p, float yaw, bool cross)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            if (s.Model(cross ? "grave_cross" : "grave_stone", p, yaw)) return;
            s.solid.Push(p, q);
            if (cross)
            {
                s.solid.Col(Pal.WoodGrey);
                s.solid.Box(new Vector3(0, 0.55f, 0), new Vector3(0.09f, 1.1f, 0.05f));
                s.solid.Box(new Vector3(0, 0.8f, 0), new Vector3(0.5f, 0.09f, 0.05f));
            }
            else
            {
                s.solid.Col(Pal.Stone);
                s.solid.Box(new Vector3(0, 0.35f, 0), new Vector3(0.5f, 0.7f, 0.1f));
            }
            s.solid.Col(Pal.DirtDark);
            s.solid.Ellipsoid(new Vector3(0, 0.02f, 0.95f), new Vector3(0.42f, 0.14f, 0.95f), 8, 4);
            s.solid.Pop();
        }

        public static void TelegraphPole(Site s, Vector3 p, float yaw)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            if (s.Model("telegraph_pole", p, yaw))
            {
                s.Collider(p + Vector3.up * 3f, new Vector3(0.24f, 6f, 0.24f));
                return;
            }
            s.solid.Col(Pal.Bark);
            s.solid.Cyl(p, p + Vector3.up * 6.5f, 0.12f, 0.09f, 6);
            s.solid.Box(p + Vector3.up * 5.9f, new Vector3(1.5f, 0.1f, 0.1f), q);
            s.Collider(p + Vector3.up * 3f, new Vector3(0.24f, 6f, 0.24f));
        }

        /// <summary>The wires of a telegraph line, sagging from insulator to insulator between consecutive poles (as
        /// TelegraphPole sets them: the cross-arm's insulators at x = +-0.3 and +-0.6, 6.05 m up).</summary>
        public static void Wires(Site s, IList<(Vector3 p, float yaw)> poles)
        {
            s.fine.Col(Pal.Iron, 0.3f);
            for (int i = 0; i + 1 < poles.Count; i++)
            {
                Quaternion qa = Quaternion.Euler(0f, poles[i].yaw, 0f), qb = Quaternion.Euler(0f, poles[i + 1].yaw, 0f);
                foreach (float x in new[] { -0.6f, -0.3f, 0.3f, 0.6f })
                {
                    Vector3 a = poles[i].p + qa * new Vector3(x, 6.08f, 0.1f), b = poles[i + 1].p + qb * new Vector3(x, 6.08f, 0.1f);
                    float sag = Vector3.Distance(a, b) * 0.012f;
                    Vector3 prev = a;
                    for (int k = 1; k <= 6; k++)
                    {
                        float t = k / 6f;
                        Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
                        s.fine.Cyl(prev, p, 0.007f, 0.007f, 4, false, false);
                        prev = p;
                    }
                }
            }
        }

        /// <summary>Rails and ties along a path of world points: lengths of the Blender track laid chord by chord.</summary>
        public static void Rails(Site s, IList<Vector3> pts)
        {
            bool kit = Models.Has("rail_track");
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                float len = Vector3.Distance(a, b);
                if (kit)
                {
                    if (len > 0.3f) s.Model("rail_track", a, Quaternion.LookRotation(b - a, Vector3.up), new Vector3(1f, 1f, len / 6f));
                    continue;
                }
                Vector3 d = (b - a).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, d).normalized * 0.72f;
                s.solid.Col(Pal.Iron, 0.7f);
                s.solid.Beam(a + side + Vector3.up * 0.2f, b + side + Vector3.up * 0.2f, 0.07f, 0.12f);
                s.solid.Beam(a - side + Vector3.up * 0.2f, b - side + Vector3.up * 0.2f, 0.07f, 0.12f);
                int n = Mathf.Max(1, Mathf.RoundToInt(len / 0.9f));
                s.solid.Col(Pal.WoodDark);
                for (int k = 0; k < n; k++)
                {
                    Vector3 p = Vector3.Lerp(a, b, (k + 0.5f) / n);
                    s.solid.Box(p + Vector3.up * 0.08f, new Vector3(2.3f, 0.14f, 0.24f), Quaternion.LookRotation(d, Vector3.up));
                }
            }
        }
    }
}
