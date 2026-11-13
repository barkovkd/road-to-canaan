using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    public enum WeaponClass { Knife, Revolver, Repeater, Rifle, Shotgun, Varmint, Bow }
    public enum GameSize { Small, Medium, Large }

    public class WeaponDef
    {
        public string id, name;
        public WeaponClass cls;
        public float damage, rate, reload, range, spread, recoil = 1f, price;
        public int pellets = 1, magazine;
        public string ammo;
        public float noise = 220f;
        public float muzzleZ, foreZ;
        public GameSize cleanFor;        // the size of game this weapon kills cleanly

        public bool Melee => cls == WeaponClass.Knife;
        public bool Long => cls != WeaponClass.Knife && cls != WeaponClass.Revolver;
        public bool Silent => cls == WeaponClass.Knife || cls == WeaponClass.Bow;
    }

    /// <summary>A weapon someone owns: its ammunition in the gun and its wear.</summary>
    public class Weapon
    {
        public WeaponDef def;
        public int loaded;
        public int shotsSinceClean;
        public GameObject model;
        public Transform muzzle;

        /// <summary>1 = clean, down to 0.6 when filthy; scales damage and accuracy.</summary>
        public float Condition => 1f - Mathf.Min(0.4f, (shotsSinceClean / 150) * 0.1f);

        public Weapon(WeaponDef def)
        {
            this.def = def;
            loaded = def.magazine;
            model = WeaponDB.BuildModel(def);
            muzzle = model.transform.Find("Muzzle");
        }
    }

    public static class WeaponDB
    {
        public static readonly List<WeaponDef> All = new List<WeaponDef>();
        static readonly Dictionary<string, WeaponDef> byId = new Dictionary<string, WeaponDef>();

        static WeaponDB()
        {
            Add(new WeaponDef { id = "knife", name = "Нож", cls = WeaponClass.Knife, damage = 45f, rate = 1.5f, range = 1.5f, price = 2f, noise = 0f, cleanFor = GameSize.Small });
            Add(new WeaponDef { id = "pilgrim", name = "Револьвер «Пилигрим»", cls = WeaponClass.Revolver, damage = 34f, rate = 2.2f, magazine = 6, reload = 2.4f, range = 45f, spread = 0.45f, price = 18f, ammo = "ammo_revolver", muzzleZ = 0.25f, cleanFor = GameSize.Medium, recoil = 1f });
            Add(new WeaponDef { id = "wasp", name = "Револьвер «Оса»", cls = WeaponClass.Revolver, damage = 26f, rate = 3.0f, magazine = 6, reload = 2.0f, range = 35f, spread = 0.55f, price = 24f, ammo = "ammo_revolver", muzzleZ = 0.2f, cleanFor = GameSize.Medium, recoil = 0.8f });
            Add(new WeaponDef { id = "drover", name = "Карабин «Перегонщик»", cls = WeaponClass.Repeater, damage = 46f, rate = 1.6f, magazine = 10, reload = 3.2f, range = 90f, spread = 0.25f, price = 34f, ammo = "ammo_rifle", muzzleZ = 0.74f, foreZ = 0.36f, cleanFor = GameSize.Medium, recoil = 1.1f });
            Add(new WeaponDef { id = "longshot", name = "Винтовка «Дальнобой»", cls = WeaponClass.Rifle, damage = 92f, rate = 0.5f, magazine = 1, reload = 1.7f, range = 220f, spread = 0.1f, price = 42f, ammo = "ammo_rifle", muzzleZ = 0.9f, foreZ = 0.40f, cleanFor = GameSize.Large, recoil = 1.6f, noise = 300f });
            Add(new WeaponDef { id = "thunder", name = "Двустволка «Гром»", cls = WeaponClass.Shotgun, damage = 14f, pellets = 9, rate = 1.5f, magazine = 2, reload = 2.2f, range = 22f, spread = 3.6f, price = 28f, ammo = "ammo_shell", muzzleZ = 0.7f, foreZ = 0.34f, cleanFor = GameSize.Large, recoil = 2f, noise = 260f });
            Add(new WeaponDef { id = "magpie", name = "Мелкашка «Сорока»", cls = WeaponClass.Varmint, damage = 18f, rate = 2.0f, magazine = 12, reload = 3.0f, range = 70f, spread = 0.2f, price = 20f, ammo = "ammo_22", muzzleZ = 0.72f, foreZ = 0.34f, cleanFor = GameSize.Small, recoil = 0.4f, noise = 120f });
            Add(new WeaponDef { id = "bow", name = "Лук", cls = WeaponClass.Bow, damage = 60f, rate = 0.8f, magazine = 1, reload = 0.9f, range = 55f, spread = 0.3f, price = 12f, ammo = "ammo_arrow", muzzleZ = 0.1f, cleanFor = GameSize.Medium, recoil = 0f, noise = 6f });
        }

        static void Add(WeaponDef d) { All.Add(d); byId[d.id] = d; }
        public static WeaponDef Get(string id) { return byId.TryGetValue(id, out WeaponDef d) ? d : null; }

        /// <summary>Model with its origin at the trigger hand and the barrel along +Z. Has a "Muzzle" child.</summary>
        public static GameObject BuildModel(WeaponDef d)
        {
            // the weapon modelled in Blender (Tools/blender/weapons.py) when this build has it
            if (Models.Has("w_" + d.id))
            {
                ModelLibrary.Entry e = Models.Get("w_" + d.id);
                GameObject model = Models.Spawn("w_" + d.id, null, Vector3.zero, Quaternion.identity);
                model.name = "W_" + d.id;
                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(model.transform, false);
                muzzle.localPosition = e.Mark("muzzle", out Vector3 at) ? at : new Vector3(0, d.cls == WeaponClass.Revolver ? 0.05f : 0.03f, d.muzzleZ);
                return model;
            }
            var b = new MeshBuilder();
            Color32 metal = Pal.GunBlue, wood = Pal.H(0x5A3A22);
            switch (d.cls)
            {
                case WeaponClass.Revolver:
                {
                    float len = d.muzzleZ;
                    b.Col(wood, 0.3f);
                    b.Box(new Vector3(0, -0.035f, -0.012f), new Vector3(0.028f, 0.095f, 0.042f), Quaternion.Euler(-18f, 0, 0));
                    b.Col(metal, 0.75f);
                    b.Box(new Vector3(0, 0.035f, 0.045f), new Vector3(0.026f, 0.05f, 0.11f));
                    b.Cyl(new Vector3(0, 0.04f, 0.035f), new Vector3(0, 0.04f, 0.085f), 0.024f, 0.024f, 8);
                    b.Cyl(new Vector3(0, 0.05f, 0.09f), new Vector3(0, 0.05f, len), 0.0105f, 0.0105f, 6);
                    b.Box(new Vector3(0, 0.062f, -0.012f), new Vector3(0.01f, 0.026f, 0.016f), Quaternion.Euler(-30f, 0, 0));
                    b.Box(new Vector3(0, 0.002f, 0.045f), new Vector3(0.006f, 0.028f, 0.05f));
                    break;
                }
                case WeaponClass.Repeater:
                case WeaponClass.Varmint:
                case WeaponClass.Rifle:
                case WeaponClass.Shotgun:
                {
                    float len = d.muzzleZ;
                    float thick = d.cls == WeaponClass.Varmint ? 0.8f : 1f;
                    b.Col(wood, 0.35f);
                    // stock, from the wrist back to the butt
                    b.Prism(new List<Vector3>
                    {
                        new Vector3(-0.016f, 0.012f, -0.04f), new Vector3(-0.016f, -0.035f, -0.05f),
                        new Vector3(-0.016f, -0.115f, -0.33f), new Vector3(-0.016f, 0.0f, -0.34f),
                    }, Vector3.right * 0.032f);
                    b.Box(new Vector3(0, 0.0f, d.foreZ), new Vector3(0.036f * thick, 0.036f, 0.24f));
                    b.Col(metal, 0.75f);
                    b.Box(new Vector3(0, 0.012f, 0.06f), new Vector3(0.034f, 0.058f, 0.20f));
                    if (d.cls == WeaponClass.Shotgun)
                    {
                        b.Cyl(new Vector3(-0.012f, 0.025f, 0.12f), new Vector3(-0.012f, 0.025f, len), 0.012f, 0.012f, 6);
                        b.Cyl(new Vector3(0.012f, 0.025f, 0.12f), new Vector3(0.012f, 0.025f, len), 0.012f, 0.012f, 6);
                    }
                    else
                    {
                        b.Cyl(new Vector3(0, 0.03f, 0.12f), new Vector3(0, 0.03f, len), 0.0105f * thick, 0.0095f * thick, 6);
                        if (d.cls != WeaponClass.Rifle)
                            b.Cyl(new Vector3(0, 0.008f, 0.16f), new Vector3(0, 0.008f, len - 0.06f), 0.009f, 0.009f, 6);
                    }
                    if (d.cls == WeaponClass.Repeater || d.cls == WeaponClass.Varmint)
                    {
                        b.Box(new Vector3(0, -0.04f, 0.02f), new Vector3(0.008f, 0.012f, 0.1f));
                        b.Box(new Vector3(0, -0.028f, -0.03f), new Vector3(0.008f, 0.03f, 0.012f));
                    }
                    else b.Box(new Vector3(0, -0.03f, 0.03f), new Vector3(0.006f, 0.02f, 0.06f));
                    break;
                }
                case WeaponClass.Knife:
                    b.Col(wood, 0.2f);
                    b.Cyl(new Vector3(0, 0, -0.05f), new Vector3(0, 0, 0.055f), 0.014f, 0.013f, 6);
                    b.Col(Pal.Steel, 1f);
                    b.Prism(new List<Vector3> { new Vector3(-0.002f, -0.016f, 0.06f), new Vector3(-0.002f, 0.016f, 0.06f), new Vector3(-0.002f, 0.004f, 0.24f) }, Vector3.right * 0.004f);
                    break;
                case WeaponClass.Bow:
                    b.Col(wood, 0.3f);
                    for (int i = 0; i < 10; i++)
                    {
                        float a0 = Mathf.Lerp(-62f, 62f, i / 10f) * Mathf.Deg2Rad, a1 = Mathf.Lerp(-62f, 62f, (i + 1) / 10f) * Mathf.Deg2Rad;
                        Vector3 p0 = new Vector3(0, Mathf.Sin(a0) * 0.72f, Mathf.Cos(a0) * 0.3f - 0.3f + 0.1f);
                        Vector3 p1 = new Vector3(0, Mathf.Sin(a1) * 0.72f, Mathf.Cos(a1) * 0.3f - 0.3f + 0.1f);
                        b.Beam(p0, p1, 0.016f, 0.022f, Vector3.right);
                    }
                    b.Col(Pal.Rope);
                    b.Beam(new Vector3(0, -0.635f, -0.06f), new Vector3(0, 0.635f, -0.06f), 0.004f, 0.004f, Vector3.right);
                    break;
            }
            GameObject go = b.ToObject("W_" + d.id, null, Mats.Lit);
            var m = new GameObject("Muzzle").transform;
            m.SetParent(go.transform, false);
            m.localPosition = new Vector3(0, d.cls == WeaponClass.Revolver ? 0.05f : 0.03f, d.muzzleZ);
            return go;
        }
    }

    /// <summary>Bullet rays: world geometry by physics, bodies by analytic per-bone tests.</summary>
    public static class Hitscan
    {
        /// <summary>Whether the last cast struck something built (boards, a post) rather than the land.</summary>
        public static bool HitBuilt;
        public static bool Cast(Vector3 origin, Vector3 dir, float range, Actor ignoreA, Actor ignoreB,
                                out Actor victim, out BodyHit body, out Vector3 end, out Vector3 normal)
        {
            victim = null;
            body = default;
            float best = range;
            normal = -dir;
            bool wall = Physics.Raycast(origin, dir, out RaycastHit hit, range, Layers.SolidMask, QueryTriggerInteraction.Ignore);
            if (wall) { best = hit.distance; normal = hit.normal; }
            HitBuilt = wall && hit.collider.gameObject.layer == Layers.Static;
            List<Actor> actors = World.actors;
            for (int i = 0; i < actors.Count; i++)
            {
                Actor a = actors[i];
                if (a == ignoreA || a == ignoreB || !a.isActiveAndEnabled) continue;
                Vector3 to = a.Chest - origin;
                float along = Vector3.Dot(to, dir);
                if (along < -3f || along > best + 3f) continue;
                if ((to - dir * along).sqrMagnitude > 9f) continue;
                if (a.RayHit(origin, dir, best, out BodyHit h) && h.dist < best)
                {
                    best = h.dist; victim = a; body = h;
                }
            }
            end = origin + dir * best;
            if (victim != null) normal = -dir;
            return wall || victim != null;
        }

        /// <summary>One projectile of a weapon. Applies damage and spawns effects.</summary>
        public static Actor Fire(Actor shooter, Actor mount, WeaponDef def, Vector3 origin, Vector3 dir, float damageMul, Vector3 visualFrom)
        {
            bool any = Cast(origin, dir, def.range * 1.6f, shooter, mount, out Actor victim, out BodyHit body, out Vector3 end, out Vector3 normal);
            Fx.Tracer(visualFrom, end);
            if (victim != null)
            {
                float falloff = body.dist <= def.range ? 1f : Mathf.Lerp(1f, 0.35f, (body.dist - def.range) / (def.range * 0.6f));
                bool hero = victim is Human vh && vh.isPlayer;     // the hero survives a graze to the head
                float mul = body.part == BodyPart.Head ? (hero ? 1.5f : 3f) : body.part == BodyPart.Heart ? (hero ? 1.2f : 1.6f)
                          : body.part == BodyPart.Torso ? 1f : 0.7f;
                victim.Damage(new DamageInfo
                {
                    amount = def.damage * damageMul * falloff * mul, attacker = shooter, point = body.point, dir = dir, part = body.part, weapon = def
                });
                Fx.Blood(body.point, dir);
            }
            else if (any)
            {
                Fx.Impact(end, normal);
                Sfx.Impact(end, HitBuilt);
            }
            return victim;
        }
    }
}
