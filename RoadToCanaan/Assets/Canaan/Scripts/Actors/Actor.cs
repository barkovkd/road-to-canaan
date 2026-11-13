using System;
using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    public enum Faction { Player, Camp, Civilian, Law, Outlaw, Livestock, Wild, Predator }
    public enum BodyPart { Torso, Head, Heart, ArmL, ArmR, LegL, LegR }

    public struct DamageInfo
    {
        public float amount;
        public Actor attacker;
        public Vector3 point, dir;
        public BodyPart part;
        public WeaponDef weapon;
        public bool melee;
    }

    public struct BodyHit
    {
        public float dist;
        public BodyPart part;
        public Vector3 point;
    }

    /// <summary>Base of every living thing: health, a character controller, damage and analytic bullet hits.</summary>
    public class Actor : MonoBehaviour
    {
        public string id = "";
        public string displayName = "";
        public Faction faction = Faction.Civilian;
        public float health = 100f, maxHealth = 100f;
        public bool dead;
        public float deadAt;
        public CharacterController cc;
        public Vector3 velocity;
        public float yaw;
        public bool grounded = true;
        public float radius = 0.3f, height = 1.8f;
        public Actor lastAttacker;
        public float lastHurt = -99f;
        public DamageInfo lastDamage;
        public bool looted;

        public event Action<Actor, DamageInfo> Damaged;
        public event Action<Actor, DamageInfo> Died;

        public Vector3 Pos => transform.position;
        public Vector3 Forward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        public virtual Vector3 Chest => transform.position + Vector3.up * (height * 0.72f);
        public virtual Vector3 HeadPos => transform.position + Vector3.up * (height * 0.93f);
        public float HorizontalSpeed => new Vector2(velocity.x, velocity.z).magnitude;

        protected virtual void OnEnable() { World.Add(this); }
        protected virtual void OnDisable() { World.Remove(this); }

        public virtual void Tick(float dt) { }
        public virtual void LateTick(float dt) { }

        protected void SetupController(float r, float h)
        {
            radius = r; height = h;
            cc = gameObject.AddComponent<CharacterController>();
            cc.radius = r;
            cc.height = h;
            cc.center = new Vector3(0f, h * 0.5f, 0f);
            cc.stepOffset = Mathf.Min(0.4f, h * 0.25f);
            cc.slopeLimit = 52f;
            cc.skinWidth = 0.03f;
            cc.minMoveDistance = 0f;
            gameObject.layer = Layers.Actor;
        }

        public void Teleport(Vector3 pos, float newYaw)
        {
            bool was = cc != null && cc.enabled;
            if (was) cc.enabled = false;
            transform.position = pos;
            yaw = newYaw;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            velocity = Vector3.zero;
            if (was) cc.enabled = true;
        }

        public virtual void Damage(DamageInfo d)
        {
            if (dead) return;
            health -= d.amount;
            if (NonLethal(d) && health < 1f) health = 1f;
            lastAttacker = d.attacker;
            lastHurt = Time.time;
            lastDamage = d;
            Damaged?.Invoke(this, d);
            OnDamaged(d);
            World.NotifyHurt(this, d);
            if (health <= 0f)
            {
                health = 0f;
                dead = true;
                deadAt = Time.time;
                OnDeath(d);
                Died?.Invoke(this, d);
                World.NotifyDeath(this, d);
            }
        }

        protected virtual void OnDeath(DamageInfo d) { }
        protected virtual void OnDamaged(DamageInfo d) { }

        /// <summary>Fists and the like cannot kill: health stops at 1.</summary>
        protected virtual bool NonLethal(DamageInfo d) { return false; }

        public void Heal(float amount) { if (!dead) health = Mathf.Min(maxHealth, health + amount); }

        /// <summary>Tests a ray against the body. The default is one capsule; people and animals refine it per bone.</summary>
        public virtual bool RayHit(Vector3 origin, Vector3 dir, float maxDist, out BodyHit hit)
        {
            hit = default;
            Vector3 a = transform.position + Vector3.up * radius, b = transform.position + Vector3.up * (height - radius);
            if (!Geo.RayCapsule(origin, dir, a, b, radius, out float t) || t > maxDist) return false;
            hit = new BodyHit { dist = t, part = BodyPart.Torso, point = origin + dir * t };
            return true;
        }
    }

    public static class Geo
    {
        /// <summary>Ray against a capsule (segment a-b with radius r). Returns the entry distance.</summary>
        public static bool RayCapsule(Vector3 o, Vector3 d, Vector3 a, Vector3 b, float r, out float t)
        {
            t = 0f;
            Vector3 ab = b - a, ao = o - a;
            float abab = Vector3.Dot(ab, ab);
            float abd = Vector3.Dot(ab, d), abao = Vector3.Dot(ab, ao);
            float s, tt;
            float denom = abab - abd * abd;      // d is unit length
            if (abab < 1e-8f) { s = 0f; tt = -Vector3.Dot(ao, d); }
            else if (Mathf.Abs(denom) < 1e-6f) { s = 0f; tt = -Vector3.Dot(ao, d); }
            else
            {
                float aod = Vector3.Dot(ao, d);
                s = Mathf.Clamp01((abao - abd * aod) / denom);
                tt = Vector3.Dot(a + ab * s - o, d);
                s = Mathf.Clamp01((abao + abd * tt) / abab);
                tt = Vector3.Dot(a + ab * s - o, d);
            }
            if (tt < 0f) return false;
            Vector3 onSeg = a + ab * s, onRay = o + d * tt;
            float dist2 = (onSeg - onRay).sqrMagnitude;
            if (dist2 > r * r) return false;
            t = Mathf.Max(0f, tt - Mathf.Sqrt(r * r - dist2));
            return true;
        }

        public static bool RaySphere(Vector3 o, Vector3 d, Vector3 c, float r, out float t)
        {
            return RayCapsule(o, d, c, c, r, out t);
        }
    }

    /// <summary>Registry of actors and world queries shared by all systems.</summary>
    public static class World
    {
        public static readonly List<Actor> actors = new List<Actor>();
        public static readonly List<Human> humans = new List<Human>();
        public static readonly List<Horse> horses = new List<Horse>();
        public static float waterLevel = -1000f;
        public static Terrain terrain;
        public static event Action<Actor, DamageInfo> Death;
        public static event Action<Actor, DamageInfo> Hurt;
        public static event Action<Vector3, float, Actor, bool> Noise;   // position, radius, source, gunshot

        public static void Add(Actor a)
        {
            if (actors.Contains(a)) return;
            actors.Add(a);
            if (a is Human h) humans.Add(h);
            if (a is Horse q) horses.Add(q);
        }

        public static void Remove(Actor a)
        {
            actors.Remove(a);
            if (a is Human h) humans.Remove(h);
            if (a is Horse q) horses.Remove(q);
        }

        public static void Clear()
        {
            actors.Clear(); humans.Clear(); horses.Clear();
            Death = null; Noise = null; Hurt = null;
            terrain = null; waterLevel = -1000f;
        }

        public static void NotifyDeath(Actor a, DamageInfo d) { Death?.Invoke(a, d); }
        public static void NotifyHurt(Actor a, DamageInfo d) { Hurt?.Invoke(a, d); }
        public static void MakeNoise(Vector3 pos, float radius, Actor source, bool gunshot) { Noise?.Invoke(pos, radius, source, gunshot); }

        public static void Tick(float dt)
        {
            for (int i = actors.Count - 1; i >= 0; i--)
            {
                Actor a = actors[i];
                if (a != null && a.isActiveAndEnabled) a.Tick(dt);
            }
        }

        public static void LateTick(float dt)
        {
            for (int i = actors.Count - 1; i >= 0; i--)
            {
                Actor a = actors[i];
                if (a != null && a.isActiveAndEnabled) a.LateTick(dt);
            }
        }

        /// <summary>Height of walkable ground under a point (terrain, floors, porches).</summary>
        public static bool Ground(Vector3 pos, out float y, out Vector3 normal, float above = 2.5f, float below = 12f)
        {
            if (Physics.Raycast(pos + Vector3.up * above, Vector3.down, out RaycastHit hit, above + below, Layers.SolidMask, QueryTriggerInteraction.Ignore))
            {
                y = hit.point.y; normal = hit.normal;
                return true;
            }
            y = pos.y; normal = Vector3.up;
            return false;
        }

        public static float GroundY(Vector3 pos)
        {
            Ground(pos, out float y, out _, 30f, 200f);
            return y;
        }

        public static Vector3 OnGround(float x, float z) { return OnGround(new Vector3(x, 0f, z)); }

        public static Vector3 OnGround(Vector3 pos)
        {
            if (terrain != null) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 40f, pos.z), Vector3.down, out RaycastHit hit, 300f, Layers.SolidMask, QueryTriggerInteraction.Ignore))
                pos.y = hit.point.y;
            return pos;
        }

        /// <summary>Depth of water above the ground at a point (0 when dry).</summary>
        public static float WaterDepth(Vector3 pos)
        {
            if (waterLevel < -500f) return 0f;
            float ground = terrain != null ? terrain.SampleHeight(pos) + terrain.transform.position.y : pos.y;
            return Mathf.Max(0f, waterLevel - ground);
        }

        public static bool LineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            return len < 0.01f || !Physics.Raycast(from, d / len, len - 0.05f, Layers.SightMask, QueryTriggerInteraction.Ignore);
        }

        public static void Near(Vector3 pos, float radius, List<Actor> result)
        {
            result.Clear();
            float r2 = radius * radius;
            for (int i = 0; i < actors.Count; i++)
                if ((actors[i].transform.position - pos).sqrMagnitude <= r2) result.Add(actors[i]);
        }
    }
}
