using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    public class HorseBreed
    {
        public string id, name;
        public int speedStat, accelStat, staminaStat, healthStat;
        public float calm;        // 0 skittish .. 1 unshakeable
        public float price;
        public int coat;

        public float Gallop => 9.5f + speedStat * 0.55f;
        public float Accel => 2.6f + accelStat * 0.42f;
        public float Stamina => 60f + staminaStat * 10f;
        public float Health => 120f + healthStat * 20f;
    }

    public static class HorseDB
    {
        public static readonly List<HorseBreed> All = new List<HorseBreed>
        {
            new HorseBreed { id = "nag", name = "Рабочая помесь", speedStat = 4, accelStat = 4, staminaStat = 5, healthStat = 5, calm = 0.9f, price = 45f, coat = 5 },
            new HorseBreed { id = "mustang", name = "Мустанг", speedStat = 5, accelStat = 5, staminaStat = 6, healthStat = 6, calm = 0.5f, price = 80f, coat = 6 },
            new HorseBreed { id = "morgan", name = "Морган", speedStat = 5, accelStat = 6, staminaStat = 5, healthStat = 4, calm = 0.9f, price = 95f, coat = 0 },
            new HorseBreed { id = "quarter", name = "Квотерхорс", speedStat = 6, accelStat = 7, staminaStat = 5, healthStat = 5, calm = 0.7f, price = 140f, coat = 1 },
            new HorseBreed { id = "appaloosa", name = "Аппалуза", speedStat = 6, accelStat = 5, staminaStat = 7, healthStat = 6, calm = 0.7f, price = 180f, coat = 3 },
            new HorseBreed { id = "thoroughbred", name = "Чистокровная", speedStat = 8, accelStat = 7, staminaStat = 4, healthStat = 3, calm = 0.35f, price = 260f, coat = 2 },
        };

        public static HorseBreed Get(string id)
        {
            foreach (HorseBreed b in All) if (b.id == id) return b;
            return All[0];
        }
    }

    public enum HorseMode { Idle, Follow, Flee, Hitched, Wander }

    /// <summary>
    /// A horse: ridden through targetSpeed and steerYaw, or acting on its own (waiting, following a whistle,
    /// bolting from gunfire). Tracks stamina and the bond with its owner.
    /// </summary>
    public class Horse : Quadruped
    {
        public const float WalkSpeed = 1.8f, TrotSpeed = 4f, CanterSpeed = 7.5f;
        static readonly float[] BondAt = { 0f, 100f, 250f, 450f };

        public HorseBreed breed;
        public Human rider, owner;
        public string horseName = "";
        public bool saddled;
        public float stamina = 100f, maxStamina = 100f;
        public float bondXp;
        public bool exhausted;
        public HorseMode mode = HorseMode.Idle;
        public Transform followTarget;
        public Vector3 home;
        public float fear;
        public string cargo;              // id of a carcass or a bound person carried behind the saddle
        public bool othersProperty;       // riding off on it is horse theft
        public float wanderRadius = 14f;
        public GameObject cargoModel;
        public int cargoQuality;

        float baseHealth, baseStamina, modeUntil, stuckT, grazeT, lastPat = -99f, lastWhinny = -99f, rodeMetres;
        Vector3 fleeFrom, wanderTo;

        public static Horse Create(HorseBreed breed, Vector3 pos, float yaw, bool saddled, string name, int coatOverride = -1)
        {
            QuadSpec spec = QuadSpec.Horse(coatOverride >= 0 ? coatOverride : breed.coat);
            QuadRig rig = QuadBuilder.Build(spec, "Horse_" + name);
            if (saddled) QuadBuilder.AddTack(rig, breed.coat % 2 == 0 ? Pal.ClothRed : Pal.ClothBlue, Pal.Leather);
            var h = rig.go.AddComponent<Horse>();
            h.InitQuad(rig, Faction.Livestock, name);
            h.breed = breed;
            h.horseName = name;
            h.saddled = saddled;
            h.maxSpeed = breed.Gallop;
            h.accel = breed.Accel;
            h.baseHealth = breed.Health;
            h.baseStamina = breed.Stamina;
            h.ApplyBond();
            h.health = h.maxHealth;
            h.stamina = h.maxStamina;
            h.Teleport(pos, yaw);
            h.home = pos;
            World.Noise += h.OnNoise;
            return h;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            World.Noise -= OnNoise;
        }

        public int BondLevel
        {
            get { int l = 1; for (int i = 1; i < BondAt.Length; i++) if (bondXp >= BondAt[i]) l = i + 1; return l; }
        }

        public float BondProgress
        {
            get
            {
                int l = BondLevel;
                if (l >= BondAt.Length) return 1f;
                return Mathf.InverseLerp(BondAt[l - 1], BondAt[l], bondXp);
            }
        }

        public float WhistleRange => BondLevel == 1 ? 40f : BondLevel == 2 ? 80f : BondLevel == 3 ? 140f : 220f;

        void ApplyBond()
        {
            float mul = 1f + 0.1f * (BondLevel - 1);
            float hk = maxHealth > 0f ? health / maxHealth : 1f, sk = maxStamina > 0f ? stamina / maxStamina : 1f;
            maxHealth = baseHealth * mul;
            maxStamina = baseStamina * mul;
            health = maxHealth * hk;
            stamina = maxStamina * sk;
        }

        public void AddBond(float xp)
        {
            int before = BondLevel;
            bondXp = Mathf.Min(bondXp + xp, BondAt[BondAt.Length - 1] + 1f);
            if (BondLevel != before)
            {
                ApplyBond();
                if (owner != null && owner.isPlayer && Game.I != null) Game.I.Notify("Привязанность лошади выросла: уровень " + BondLevel);
            }
        }

        /// <summary>Calm and reward the horse. Small bond gain, on a cooldown.</summary>
        public void Pat()
        {
            fear = Mathf.Max(0f, fear - 0.5f);
            if (Time.time - lastPat > 20f) { AddBond(4f); lastPat = Time.time; }
            Sfx.Play("snort", transform.position, 0.4f, 1f, 12f);
        }

        public void OnMounted(Human h)
        {
            mode = HorseMode.Idle;
            if (owner == null && h.isPlayer) owner = h;
        }

        public void OnDismounted(Human h)
        {
            targetSpeed = 0f;
            steering = false;
            mode = HorseMode.Idle;
            home = transform.position;
        }

        /// <summary>Whistle: come to the caller if within earshot. Returns false if too far to hear.</summary>
        public bool Call(Human caller)
        {
            if (dead || rider != null) return false;
            float d = Vector3.Distance(transform.position, caller.transform.position);
            if (d > WhistleRange) return false;
            followTarget = caller.transform;
            if (Time.time - lastWhinny > 4f)                                         // it answers the whistle
            {
                lastWhinny = Time.time;
                Sfx.Play("whinny", transform.position + Vector3.up * 1.6f, 0.6f, Sfx.Vary(0.97f, 1.05f), 90f, 0.7f);
            }
            mode = HorseMode.Follow;
            modeUntil = Time.time + 60f;
            stuckT = 0f;
            return true;
        }

        void OnNoise(Vector3 pos, float radius, Actor source, bool gunshot)
        {
            if (dead || !gunshot) return;
            float d = Vector3.Distance(pos, transform.position);
            if (d > radius * 0.25f) return;
            float bondCalm = 0.15f * (BondLevel - 1);
            float amount = Mathf.Clamp01(1f - breed.calm - bondCalm + 0.2f) * Mathf.Clamp01(1f - d / (radius * 0.25f));
            if (rider != null) amount *= 0.25f;
            float was = fear;
            fear = Mathf.Clamp01(fear + amount * 0.6f);
            if (was < 0.45f && fear >= 0.45f && Time.time - lastWhinny > 6f)       // a shot too close: it screams
            {
                lastWhinny = Time.time;
                Sfx.Play("whinny", transform.position + Vector3.up * 1.6f, 0.75f, Sfx.Vary(0.95f, 1.08f), 70f, 0.15f);
            }
            if (rider == null && mode != HorseMode.Hitched && fear > 0.75f)
            {
                mode = HorseMode.Flee;
                fleeFrom = pos;
                modeUntil = Time.time + 6f;
            }
        }

        public override void Tick(float dt)
        {
            if (dead) return;
            fear = Mathf.Max(0f, fear - dt * 0.12f);
            if (rider == null) Self(dt);
            else
            {
                anim.graze = 0f;
                if (rider.isPlayer)
                {
                    rodeMetres += Mathf.Abs(speed) * dt;
                    if (rodeMetres > 40f) { rodeMetres = 0f; AddBond(1f); }
                }
            }

            // stamina: only the gallop tires a horse
            float canter = CanterSpeed + 0.8f;
            if (speed > canter && rider != null)
            {
                stamina -= dt * 100f / (18f + breed.staminaStat * 5f);
                if (stamina <= 0f) { stamina = 0f; exhausted = true; }
            }
            else stamina = Mathf.Min(maxStamina, stamina + dt * (speed < 2.5f ? 9f : 4f));
            if (exhausted && stamina > maxStamina * 0.3f) exhausted = false;
            if (exhausted) targetSpeed = Mathf.Min(targetSpeed, CanterSpeed);

            // jump low obstacles when moving fast enough
            if (speed > 5f && grounded && jumpT < 0f && (frame & 3) == 0)
            {
                Vector3 o = transform.position, f = U.Dir(yaw);
                float reach = speed * 0.38f + 0.7f;    // take off so the top of the arc is over the obstacle
                if (Physics.Raycast(o + Vector3.up * 0.55f, f, out RaycastHit low, reach, 1 << Layers.Static, QueryTriggerInteraction.Ignore)
                    && !Physics.Raycast(o + Vector3.up * 1.45f, f, reach + 2.5f, 1 << Layers.Static, QueryTriggerInteraction.Ignore)
                    && low.distance < reach) Jump();
            }
            MotorTick(dt);
        }

        void Self(float dt)
        {
            steering = false;
            switch (mode)
            {
                case HorseMode.Follow:
                {
                    if (followTarget == null || Time.time > modeUntil) { mode = HorseMode.Idle; break; }
                    Vector3 to = followTarget.position - transform.position;
                    to.y = 0f;
                    float d = to.magnitude;
                    if (d < 3.2f) { targetSpeed = 0f; mode = HorseMode.Idle; home = transform.position; break; }
                    targetSpeed = d > 28f ? maxSpeed : d > 9f ? CanterSpeed : d > 5f ? TrotSpeed : WalkSpeed;
                    SteerAround(to / d);
                    stuckT = speed < 0.5f ? stuckT + dt : 0f;
                    if (stuckT > 2.5f)
                    {
                        // boxed in: step out behind the caller instead of grinding against a wall
                        Vector3 behind = followTarget.position - followTarget.forward * 6f + followTarget.right * 2f;
                        Teleport(World.OnGround(behind), U.Yaw(followTarget.position - behind));
                        stuckT = 0f;
                    }
                    anim.graze = 0f;
                    break;
                }
                case HorseMode.Flee:
                {
                    if (Time.time > modeUntil) { mode = HorseMode.Idle; home = transform.position; break; }
                    Vector3 away = transform.position - fleeFrom;
                    away.y = 0f;
                    targetSpeed = maxSpeed * 0.85f;
                    SteerAround(away.sqrMagnitude > 0.01f ? away.normalized : transform.forward);
                    anim.graze = 0f;
                    break;
                }
                case HorseMode.Wander:
                {
                    if (Time.time > modeUntil)
                    {
                        Vector2 r = Random.insideUnitCircle * wanderRadius;
                        wanderTo = home + new Vector3(r.x, 0f, r.y);
                        modeUntil = Time.time + Random.Range(8f, 20f);
                    }
                    Vector3 to = wanderTo - transform.position;
                    to.y = 0f;
                    if (to.magnitude > 1.5f) { targetSpeed = WalkSpeed; SteerAround(to.normalized); anim.graze = 0f; }
                    else { targetSpeed = 0f; Graze(dt); }
                    break;
                }
                default:
                    targetSpeed = 0f;
                    if (mode == HorseMode.Idle) Graze(dt); else anim.graze = U.Damp(anim.graze, 0f, 3f, dt);
                    break;
            }
        }

        void Graze(float dt)
        {
            grazeT += dt;
            bool down = Mathf.Sin(grazeT * 0.21f + rig.k * 3f) > 0.25f && Mathf.Abs(speed) < 0.2f;
            anim.graze = U.Damp(anim.graze, down ? 1f : 0f, 1.6f, dt);
        }

        /// <summary>Steers towards a direction, veering around buildings and fences in the way.</summary>
        public void SteerAround(Vector3 dir)
        {
            Vector3 o = transform.position + Vector3.up * 0.9f;
            float look = 3f + Mathf.Abs(speed) * 0.6f;
            int mask = 1 << Layers.Static;
            if (Physics.SphereCast(o, 0.4f, dir, out RaycastHit hit, look, mask, QueryTriggerInteraction.Ignore))
            {
                Vector3 l = Quaternion.Euler(0f, -50f, 0f) * dir, r = Quaternion.Euler(0f, 50f, 0f) * dir;
                bool lb = Physics.SphereCast(o, 0.4f, l, out RaycastHit lh, look, mask, QueryTriggerInteraction.Ignore);
                bool rb = Physics.SphereCast(o, 0.4f, r, out RaycastHit rh, look, mask, QueryTriggerInteraction.Ignore);
                dir = !lb ? l : !rb ? r : (lh.distance > rh.distance ? l : r);
                if (hit.distance < 1.8f) targetSpeed = Mathf.Min(targetSpeed, WalkSpeed);
            }
            steerYaw = U.Yaw(dir);
            steering = true;
        }

        protected override void OnDeath(DamageInfo d)
        {
            Human r = rider;
            if (r != null)
            {
                r.Dismount();
                if (!r.isPlayer && !r.dead) r.KnockOut(3f);
            }
            base.OnDeath(d);
        }
    }
}
