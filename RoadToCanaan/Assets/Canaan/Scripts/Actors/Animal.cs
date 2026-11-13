using System;
using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    public class AnimalDef
    {
        public string id, name, genitive;
        public Func<System.Random, QuadSpec> spec;
        public GameSize size;
        public float health, speed, sense;
        public bool predator, timid, livestock, noPelt;
        public string meat;
        public int meatCount;
        public float provisions;      // what a whole carcass is worth to the camp kitchen
    }

    public static class AnimalDB
    {
        public static readonly Dictionary<string, AnimalDef> All = new Dictionary<string, AnimalDef>();

        static AnimalDB()
        {
            Add(new AnimalDef { id = "pronghorn", name = "Вилорог", size = GameSize.Medium, health = 70f, speed = 15.5f, sense = 42f, meat = "meat_game", meatCount = 2, provisions = 22f, spec = r => QuadSpec.Pronghorn(r.NextDouble() < 0.4) });
            Add(new AnimalDef { id = "deer", name = "Олень", size = GameSize.Medium, health = 75f, speed = 13.5f, sense = 36f, meat = "meat_game", meatCount = 2, provisions = 24f, spec = r => QuadSpec.Deer(r.NextDouble() < 0.4) });
            Add(new AnimalDef { id = "rabbit", name = "Заяц", size = GameSize.Small, health = 12f, speed = 10f, sense = 16f, meat = "meat_small", meatCount = 1, provisions = 4f, spec = r => QuadSpec.Rabbit() });
            Add(new AnimalDef { id = "coyote", name = "Койот", size = GameSize.Medium, health = 55f, speed = 12f, sense = 34f, predator = true, timid = true, meat = "meat_small", meatCount = 1, provisions = 5f, spec = r => QuadSpec.Canine(false) });
            Add(new AnimalDef { id = "wolf", name = "Волк", size = GameSize.Medium, health = 85f, speed = 12.5f, sense = 40f, predator = true, meat = "meat_small", meatCount = 1, provisions = 6f, spec = r => QuadSpec.Canine(true) });
            Add(new AnimalDef { id = "bison", name = "Бизон", size = GameSize.Large, health = 320f, speed = 11f, sense = 26f, meat = "meat_prime", meatCount = 4, provisions = 60f, spec = r => QuadSpec.Bison() });
            Add(new AnimalDef { id = "cow", name = "Корова", size = GameSize.Large, health = 180f, speed = 6f, sense = 12f, livestock = true, meat = "meat_game", meatCount = 3, provisions = 40f, spec = r => QuadSpec.Cow(r.Next(3)) });
            Add(new AnimalDef { id = "ox", name = "Вол", size = GameSize.Large, health = 220f, speed = 5f, sense = 10f, livestock = true, meat = "meat_game", meatCount = 3, provisions = 40f, spec = r => QuadSpec.Ox(r.NextDouble() < 0.4) });
        }

        static void Add(AnimalDef d) { d.genitive = ItemDB.AnimalGenitive(d.id); All[d.id] = d; }
        public static AnimalDef Get(string id) { return All[id]; }
    }

    public enum AnimalMood { Graze, Alert, Flee, Attack }

    /// <summary>A wild or farm animal: grazes in a herd, bolts from people and gunfire; wolves attack.</summary>
    public class Animal : Quadruped
    {
        public AnimalDef def;
        public int stars = 2;               // 1 poor .. 3 pristine
        public AnimalMood mood = AnimalMood.Graze;
        public Vector3 home;
        public float roam = 35f;
        public int peltQuality;             // set at death
        public bool skinned, onHorse;
        public float removeAt = -1f;
        Actor threat;
        Vector3 goal;
        float thinkAt, moodUntil, biteAt, wanderAt, weave;
        static readonly System.Random rng = new System.Random(808);

        public static Animal Create(AnimalDef def, Vector3 pos, float yaw, int stars = 0)
        {
            QuadRig rig = QuadBuilder.Build(def.spec(rng), def.id);
            var a = rig.go.AddComponent<Animal>();
            a.def = def;
            a.InitQuad(rig, def.livestock ? Faction.Livestock : def.predator && !def.timid ? Faction.Predator : Faction.Wild, def.name);
            a.maxHealth = a.health = def.health;
            a.maxSpeed = def.speed;
            a.accel = def.speed * 0.6f;
            a.turnSlow = 220f; a.turnFast = 90f;
            a.stars = stars > 0 ? stars : (rng.NextDouble() < 0.25 ? 1 : rng.NextDouble() < 0.6 ? 2 : 3);
            a.Teleport(pos, yaw);
            a.home = pos;
            a.goal = pos;
            a.thinkAt = Time.time + (float)rng.NextDouble();
            return a;
        }

        public void Spook(Vector3 from, float seconds)
        {
            if (dead) return;
            if (def.predator && !def.timid && mood == AnimalMood.Attack) return;
            mood = AnimalMood.Flee;
            moodUntil = Time.time + seconds * (def.livestock ? 0.4f : 1f);
            Vector3 away = U.FlatDir(transform.position - from);
            goal = transform.position + Quaternion.Euler(0f, UnityEngine.Random.Range(-35f, 35f), 0f) * away * 90f;
        }

        public override void Tick(float dt)
        {
            if (dead) return;
            if (Time.time >= thinkAt) { thinkAt = Time.time + 0.4f; Think(); }
            Vector3 pos = transform.position;
            steering = false;
            switch (mood)
            {
                case AnimalMood.Flee:
                {
                    targetSpeed = def.livestock ? def.speed : maxSpeed;
                    Vector3 dir = U.FlatDir(goal - pos);
                    if (def.id == "rabbit") { weave += dt * 5f; dir = Quaternion.Euler(0f, Mathf.Sin(weave) * 40f, 0f) * dir; }
                    Steer(dir);
                    anim.graze = 0f; anim.alert = 1f;
                    if (Time.time > moodUntil || U.FlatDist(pos, goal) < 6f) { mood = AnimalMood.Graze; home = def.livestock ? home : pos; wanderAt = 0f; }
                    break;
                }
                case AnimalMood.Attack:
                {
                    if (threat == null || threat.dead || Vector3.Distance(threat.Pos, pos) > 70f) { mood = AnimalMood.Graze; break; }
                    float d = U.FlatDist(threat.Pos, pos);
                    targetSpeed = d > 2.2f ? maxSpeed : 0f;
                    Steer(U.FlatDir(threat.Pos - pos));
                    anim.graze = 0f; anim.alert = 1f;
                    if (d < 2.4f && Time.time > biteAt)
                    {
                        biteAt = Time.time + 1.3f;
                        threat.Damage(new DamageInfo { amount = 15f, attacker = this, point = threat.Chest, dir = U.FlatDir(threat.Pos - pos), part = BodyPart.LegL, melee = true, weapon = WeaponDB.Get("knife") });
                        Sfx.Play("flesh", threat.Pos, 0.7f, 1f, 14f);
                    }
                    if (health < maxHealth * 0.4f) Spook(threat.Pos, 14f);
                    break;
                }
                case AnimalMood.Alert:
                    targetSpeed = 0f;
                    anim.graze = U.Damp(anim.graze, 0f, 6f, dt);
                    anim.alert = U.Damp(anim.alert, 1f, 6f, dt);
                    if (threat != null) { steerYaw = U.Yaw(threat.Pos - pos); anim.lookYaw = Mathf.DeltaAngle(yaw, steerYaw); }
                    if (Time.time > moodUntil) mood = AnimalMood.Graze;
                    break;
                default:
                {
                    anim.alert = U.Damp(anim.alert, 0f, 2f, dt);
                    anim.lookYaw = U.Damp(anim.lookYaw, 0f, 2f, dt);
                    if (Time.time > wanderAt)
                    {
                        wanderAt = Time.time + UnityEngine.Random.Range(6f, 20f);
                        Vector2 r = UnityEngine.Random.insideUnitCircle * roam;
                        goal = home + new Vector3(r.x, 0f, r.y);
                    }
                    if (U.FlatDist(pos, goal) > 1.8f * Mathf.Max(0.4f, rig.k))
                    {
                        targetSpeed = Mathf.Min(1.4f * Mathf.Max(0.5f, rig.k * 1.4f), maxSpeed * 0.2f);
                        Steer(U.FlatDir(goal - pos));
                        anim.graze = U.Damp(anim.graze, 0f, 3f, dt);
                    }
                    else
                    {
                        targetSpeed = 0f;
                        anim.graze = U.Damp(anim.graze, def.predator ? 0f : 1f, 1.5f, dt);
                    }
                    break;
                }
            }
            MotorTick(dt);
        }

        void Steer(Vector3 dir)
        {
            // keep out of deep water and off walls
            Vector3 ahead = transform.position + dir * 6f;
            if (World.WaterDepth(ahead) > 0.9f * Mathf.Max(0.5f, rig.k) || Physics.Raycast(transform.position + Vector3.up * 0.5f, dir, 3f, 1 << Layers.Static, QueryTriggerInteraction.Ignore))
            {
                dir = Quaternion.Euler(0f, 70f * (Mathf.Sin(Time.time * 0.7f + rig.k * 9f) > 0f ? 1f : -1f), 0f) * dir;
                goal = transform.position + dir * 60f;
            }
            steerYaw = U.Yaw(dir);
            steering = true;
        }

        void Think()
        {
            if (mood == AnimalMood.Flee) return;
            Human p = Game.I != null ? Game.I.player : null;
            if (p == null || p.dead) return;
            float d = Vector3.Distance(p.Pos, transform.position);
            float sense = def.sense * (p.crouch ? 0.55f : 1f) * (p.Mounted ? 1.25f : 1f) * (p.HorizontalSpeed > 4f ? 1.2f : 1f);
            if (def.livestock) return;
            if (def.predator && !def.timid)
            {
                bool hungry = Game.I.clock.IsNight || d < 16f;
                if (d < (hungry ? 30f : 0f) && mood != AnimalMood.Attack) { mood = AnimalMood.Attack; threat = p; }
                return;
            }
            if (d < sense * 0.6f) { threat = p; Spook(p.Pos, UnityEngine.Random.Range(9f, 15f)); }
            else if (d < sense && mood == AnimalMood.Graze) { mood = AnimalMood.Alert; threat = p; moodUntil = Time.time + 3f; }
        }

        protected override void OnDamaged(DamageInfo d)
        {
            if (d.attacker == null) return;
            if (def.predator && !def.timid && health > maxHealth * 0.4f) { mood = AnimalMood.Attack; threat = d.attacker; }
            else Spook(d.attacker.Pos, 16f);
        }

        protected override void OnDeath(DamageInfo d)
        {
            base.OnDeath(d);
            // a clean kill: the right calibre for the size of the game, into the head or the heart
            Human p = Game.I != null ? Game.I.player : null;
            bool byPlayer = p != null && d.attacker == p && d.weapon != null;
            bool rightGun = byPlayer && (d.weapon.cleanFor == def.size || (d.weapon.cls == WeaponClass.Bow && def.size != GameSize.Large)) && d.weapon.cls != WeaponClass.Shotgun;
            bool vital = d.part == BodyPart.Head || d.part == BodyPart.Heart;
            peltQuality = !byPlayer || d.weapon.cls == WeaponClass.Shotgun ? 1 : rightGun && vital ? stars : rightGun || vital ? Mathf.Max(1, stars - 1) : 1;
            removeAt = Time.time + 420f;
        }
    }
}
