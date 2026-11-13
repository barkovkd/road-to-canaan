using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>Herds and packs of a region, herbs to pick, and what the hero does with a kill.</summary>
    public static class Wildlife
    {
        class Herd { public AnimalDef def; public Vector3 home; public float roam; public int target; public List<Animal> members = new List<Animal>(); }
        class Herb { public string item; public Vector3 pos; public GameObject go; public int pickedDay = -99; }

        static readonly List<Herd> herds = new List<Herd>();
        static readonly List<Herb> herbs = new List<Herb>();
        static float checkAt;
        static System.Random rng = new System.Random(55);

        public static void Reset() { herds.Clear(); herbs.Clear(); checkAt = 0f; }

        public static IEnumerable<Animal> Animals()
        {
            foreach (Herd h in herds) foreach (Animal a in h.members) if (a != null) yield return a;
        }

        static void AddHerd(string id, Vector3 home, int count, float roam)
        {
            var h = new Herd { def = AnimalDB.Get(id), home = home, roam = roam, target = count };
            herds.Add(h);
            for (int i = 0; i < count; i++) SpawnInto(h);
        }

        static void SpawnInto(Herd h)
        {
            Vector2 r = Random.insideUnitCircle * h.roam * 0.6f;
            Vector3 p = World.OnGround(h.home + new Vector3(r.x, 0f, r.y));
            if (World.WaterDepth(p) > 0.3f) p = World.OnGround(h.home);
            Animal a = Animal.Create(h.def, p, Random.Range(0f, 360f));
            a.home = h.home;
            a.roam = h.roam;
            h.members.Add(a);
        }

        public static void SpawnDryFord(Region r)
        {
            Reset();
            AddHerd("pronghorn", r.P("hunt_plain"), 6, 45f);
            AddHerd("pronghorn", r.P("hunt_north"), 7, 55f);
            AddHerd("deer", r.P("hunt_grove"), 4, 40f);
            AddHerd("deer", r.P("pond") + new Vector3(-40f, 0f, 30f), 3, 35f);
            AddHerd("rabbit", r.P("hunt_plain") + new Vector3(60f, 0f, -40f), 5, 40f);
            AddHerd("rabbit", r.P("camp_fire") + new Vector3(-110f, 0f, 60f), 4, 40f);
            AddHerd("rabbit", r.P("pond"), 4, 45f);
            AddHerd("coyote", r.P("pond") + new Vector3(-90f, 0f, -120f), 3, 60f);
            AddHerd("wolf", r.P("hunt_north") + new Vector3(-180f, 0f, 160f), 4, 50f);
            AddHerd("bison", r.P("bison_range"), 5, 50f);
            AddHerd("ox", r.P("camp_pen"), 6, 9f);
            AddHerd("cow", r.P("ranch_corral"), 6, 12f);
            AddHerd("cow", r.P("ranch_pasture"), 7, 40f);

            // herbs
            void Patch(string item, Vector3 centre, int count, float spread)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector2 o = Random.insideUnitCircle * spread;
                    Vector3 p = World.OnGround(centre + new Vector3(o.x, 0f, o.y));
                    if (World.WaterDepth(p) > 0.05f) continue;
                    var herb = new Herb { item = item, pos = p, go = HerbModel(item, p, r.root.transform) };
                    herbs.Add(herb);
                    ItemDef d = ItemDB.Get(item);
                    Interactable.Add("Сорвать: " + d.name.ToLowerInvariant(), () => herb.pos, _ => Pick(herb), 1.9f, () => herb.go.activeSelf);
                }
            }
            Patch("herb_yarrow", r.P("camp_fire") + new Vector3(70f, 0f, 20f), 7, 40f);
            Patch("herb_yarrow", r.P("hunt_grove"), 8, 55f);
            Patch("herb_onion", r.P("pond") + new Vector3(40f, 0f, 0f), 8, 35f);
            Patch("herb_onion", r.P("ford_west") + new Vector3(-10f, 0f, 60f), 6, 30f);
            Patch("herb_sage", r.P("hunt_plain"), 9, 70f);
            Patch("herb_sage", r.P("menu") + new Vector3(0f, 0f, -150f), 8, 60f);
            World.Noise += OnNoise;
        }

        static GameObject HerbModel(string item, Vector3 p, Transform parent)
        {
            var b = new MeshBuilder();
            if (item == "herb_yarrow")
            {
                b.Col(Pal.H(0x5E7A3A));
                for (int i = 0; i < 3; i++) b.Cyl(new Vector3(i * 0.08f - 0.08f, 0, 0), new Vector3(i * 0.1f - 0.1f, 0.42f, i * 0.04f), 0.012f, 0.008f, 4, false, false);
                b.Col(Pal.H(0xECE8D8));
                for (int i = 0; i < 3; i++) b.Ellipsoid(new Vector3(i * 0.1f - 0.1f, 0.44f, i * 0.04f), new Vector3(0.07f, 0.02f, 0.07f), 6, 3);
            }
            else if (item == "herb_onion")
            {
                b.Col(Pal.H(0x6F9A4A));
                for (int i = 0; i < 5; i++) b.Cyl(new Vector3(0, 0, 0), new Vector3(Mathf.Cos(i * 1.3f) * 0.1f, 0.34f, Mathf.Sin(i * 1.3f) * 0.1f), 0.012f, 0.004f, 4, false, false);
                b.Col(Pal.H(0xA070B0));
                b.Sphere(new Vector3(0, 0.36f, 0), 0.04f, 6, 4);
            }
            else
            {
                b.Col(Pal.H(0xA9B59A));
                b.Ellipsoid(new Vector3(0, 0.16f, 0), new Vector3(0.2f, 0.17f, 0.2f), 7, 4);
                b.Col(Pal.H(0xC9D0B8));
                b.Ellipsoid(new Vector3(0.06f, 0.26f, 0.03f), new Vector3(0.1f, 0.09f, 0.1f), 6, 3);
            }
            GameObject go = b.ToObject("Herb", parent, Mats.LitDouble, false);
            go.transform.position = p;
            return go;
        }

        static void Pick(Herb h)
        {
            Game g = Game.I;
            g.sat.Add(h.item);
            h.go.SetActive(false);
            h.pickedDay = g.clock.day;
            g.Notify("В сумке: " + ItemDB.Get(h.item).name.ToLowerInvariant() + " ×" + g.sat.Count(h.item), 2.5f);
            Sfx.Play2D("pickup", 0.5f);
        }

        static void OnNoise(Vector3 pos, float radius, Actor source, bool gunshot)
        {
            if (!gunshot) return;
            float reach = radius * 0.5f;
            foreach (Animal a in Animals())
                if (!a.dead && Vector3.Distance(a.Pos, pos) < reach) a.Spook(pos, Random.Range(10f, 16f));
        }

        public static void Tick(float dt)
        {
            if (Time.time < checkAt) return;
            checkAt = Time.time + 5f;
            Game g = Game.I;
            Vector3 pp = g.player != null ? g.player.Pos : Vector3.zero;
            foreach (Herd h in herds)
            {
                for (int i = h.members.Count - 1; i >= 0; i--)
                {
                    Animal a = h.members[i];
                    if (a == null) { h.members.RemoveAt(i); continue; }
                    if (a.dead && !a.onHorse && a.removeAt > 0f && Time.time > a.removeAt && Vector3.Distance(a.Pos, pp) > 30f)
                    {
                        Object.Destroy(a.gameObject);
                        h.members.RemoveAt(i);
                    }
                }
                int alive = 0;
                foreach (Animal a in h.members) if (!a.dead) alive++;
                if (alive < h.target && Vector3.Distance(h.home, pp) > 230f) SpawnInto(h);
            }
            foreach (Herb h in herbs) if (!h.go.activeSelf && g.clock.day > h.pickedDay) h.go.SetActive(true);
        }

        // ================================================================== the kill

        public static void Prompts(Human p, List<Prompt> prompts)
        {
            if (p.Mounted) return;
            Animal best = null;
            float bestD = 2.6f;
            foreach (Animal a in Animals())
            {
                if (!a.dead || a.skinned || a.onHorse) continue;
                float d = U.FlatDist(a.Pos, p.Pos) - a.radius;
                if (d < bestD) { bestD = d; best = a; }
            }
            if (best == null) return;
            Animal target = best;
            if (target.def.size == GameSize.Small)
            {
                prompts.Add(new Prompt { btn = Btn.Interact, text = "Подобрать: " + target.def.name.ToLowerInvariant(), action = () => Skin(target), priority = 15f });
                return;
            }
            prompts.Add(new Prompt { btn = Btn.Interact, text = "Освежевать", hold = 1.6f, action = () => Skin(target), priority = 15f });
            Horse h = Game.I.ctrl.horse;
            if (h != null && !h.dead && h.cargo == null && target.def.size == GameSize.Medium && Vector3.Distance(h.Pos, p.Pos) < 9f)
                prompts.Add(new Prompt { btn = Btn.Melee, text = "Погрузить тушу на лошадь", hold = 1.2f, action = () => Stow(target, h), priority = 15f });
        }

        static string Stars(int q) { return q >= 3 ? "отличная" : q == 2 ? "хорошая" : "плохая"; }

        static void Skin(Animal a)
        {
            Game g = Game.I;
            a.skinned = true;
            a.removeAt = Time.time + 45f;
            string got = "";
            if (!a.def.noPelt && a.def.size != GameSize.Small || a.def.id == "rabbit")
            {
                g.sat.Add(ItemDB.PeltId(a.def.id, a.peltQuality));
                got = "шкура " + a.def.genitive + " (" + Stars(a.peltQuality) + ")";
            }
            if (a.def.meat != null)
            {
                g.sat.Add(a.def.meat, a.def.meatCount);
                got += (got.Length > 0 ? ", " : "") + ItemDB.Get(a.def.meat).name.ToLowerInvariant() + " ×" + a.def.meatCount;
            }
            g.Notify("Добыто: " + got);
            g.stats.AddXp(0, 3f);
            Sfx.Play2D("pickup", 0.6f);
            // what is left shrinks into the grass
            a.transform.localScale = new Vector3(1f, 0.55f, 1f);
        }

        static void Stow(Animal a, Horse h)
        {
            a.onHorse = true;
            h.cargo = a.def.id;
            h.cargoQuality = a.peltQuality;
            h.cargoModel = a.gameObject;
            Transform t = a.transform;
            t.SetParent(h.rig.B(QB.Pelvis), false);
            t.localPosition = new Vector3(0f, 0.24f * h.rig.k, 0.05f);
            t.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Game.I.Notify("Туша на крупе. Отвезите её Баку в лагерь или мяснику");
        }

        /// <summary>Removes the carcass from the horse; returns what it was.</summary>
        public static AnimalDef TakeCargo(Horse h, out int quality)
        {
            quality = h.cargoQuality;
            AnimalDef d = h.cargo != null ? AnimalDB.Get(h.cargo) : null;
            if (h.cargoModel != null) Object.Destroy(h.cargoModel);
            h.cargo = null;
            h.cargoModel = null;
            return d;
        }
    }
}
