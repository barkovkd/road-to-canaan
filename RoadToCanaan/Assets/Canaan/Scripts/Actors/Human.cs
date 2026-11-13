using System;
using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>Decision making for a person. The player's controller and every AI are brains.</summary>
    public abstract class Brain
    {
        public Human self;
        public abstract void Tick(float dt);
        public virtual void OnDamaged(DamageInfo d) { }
        public virtual void OnDeath(DamageInfo d) { }
    }

    /// <summary>
    /// A person: movement, riding, weapons in hand, melee, damage and death. Brains only set intent
    /// (moveInput, aiming, sprint...) and call actions (Fire, Reload, Mount...).
    /// </summary>
    public class Human : Actor
    {
        public const float WalkSpeed = 1.6f, JogSpeed = 3.4f, SprintSpeed = 6.2f, CrouchSpeed = 1.5f;

        public HumanRig rig;
        public HumanAnim anim;
        public HumanSpec spec;
        public Brain brain;
        public bool isPlayer;

        // ---- intent, set by the brain every frame
        public Vector3 moveInput;          // world direction, length 0..1
        public bool sprint, walk, crouch;
        public float crouchDepth = 1f;    // 1 = normal crouch, more = hunched low behind cover
        public bool aiming;
        public Vector3 aimPoint;
        public bool faceLocked;            // face 'faceYaw' instead of the direction of travel
        public float faceYaw;
        public bool handsUp, fistsUp;
        public bool looking;
        public Vector3 lookPoint;
        public float lookStrength = 1f;    // how far the head goes towards lookPoint (0..1)

        // ---- state
        public Horse mount;
        public BodyKind pose = BodyKind.Stand;
        public readonly List<Weapon> weapons = new List<Weapon>();
        public Weapon weapon;              // in the hands; null = unarmed
        public bool reloading, masked, knockedOut;
        public float speedMul = 1f;
        public bool infiniteAmmo = true;
        public Func<string, int> ammoCount;
        public Action<string, int> ammoUse;
        public float camDistance;
        public float workRate;             // > 0 while chopping or hammering

        float reloadEnd, nextShot, meleeAt = -1f, koUntil, crouchAmount, airTime, mountT = 1f, lookW, workPhase;
        float turnRate, accel, lastFwd;    // for the animation: how fast the body turns (deg/s) and speeds up (m/s2)
        Vector3 mountFromPos;
        Quaternion mountFromRot;
        bool punchLeft;
        Quaternion deathRot;
        float deathY;
        int frame;

        public static Human Create(HumanSpec spec, Vector3 pos, float yaw, Faction faction, string name)
        {
            HumanRig rig = HumanBuilder.Build(spec, name);
            var h = rig.go.AddComponent<Human>();
            h.rig = rig;
            h.spec = spec;
            h.anim = new HumanAnim(rig);
            h.faction = faction;
            h.displayName = name;
            h.SetupController(0.28f * Mathf.Max(1f, spec.build * 0.95f), spec.height);
            h.Teleport(pos, yaw);
            return h;
        }

        public override Vector3 Chest => rig.B(HB.Chest).position;
        public override Vector3 HeadPos => rig.eyes.position;
        public bool Mounted => mount != null;
        public bool Busy => dead || knockedOut;
        public bool CanFire => weapon != null && !reloading && Time.time >= nextShot;

        // ================================================================== update

        public override void Tick(float dt)
        {
            frame++;
            if (dead) return;
            if (knockedOut)
            {
                if (Time.time < koUntil) return;
                WakeUp();
            }
            brain?.Tick(dt);
            if (dead || !gameObject.activeInHierarchy) return;      // a brain may send its owner to bed
            if (mount != null) TickMounted(dt);
            else if (pose == BodyKind.Stand) Motor(dt);
            else velocity = Vector3.zero;
            TickWeapon(dt);
        }

        void Motor(float dt)
        {
            Vector3 wish = moveInput;
            wish.y = 0f;
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            float depth = World.WaterDepth(transform.position);
            float max = crouch ? CrouchSpeed : walk ? WalkSpeed : sprint ? SprintSpeed : JogSpeed;
            if (aiming) max = Mathf.Min(max, WalkSpeed * 1.15f);
            if (depth > 0.35f) max *= Mathf.Lerp(0.75f, 0.42f, Mathf.InverseLerp(0.35f, 1.2f, depth));
            max *= speedMul;

            Vector3 hv = new Vector3(velocity.x, 0f, velocity.z);
            // a body has weight: it gets going a little slower than it stops
            float rate = !grounded ? 4f : (wish * max).sqrMagnitude > hv.sqrMagnitude ? 10f : 14f;
            hv = Vector3.MoveTowards(hv, wish * max, rate * dt);
            velocity.x = hv.x; velocity.z = hv.z;
            velocity.y = grounded && velocity.y < 0f ? -2.5f : velocity.y - 19f * dt;

            // someone standing still on the ground does not need the physics query every frame
            bool resting = grounded && wish.sqrMagnitude < 1e-4f && hv.sqrMagnitude < 1e-4f && velocity.y <= 0f && (frame & 15) != 0;
            if (cc != null && cc.enabled && !resting)
            {
                cc.Move(velocity * dt);
                grounded = cc.isGrounded;
                if ((cc.collisionFlags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
            }
            airTime = grounded ? 0f : airTime + dt;

            float target = faceLocked ? faceYaw : hv.sqrMagnitude > 0.2f ? U.Yaw(hv) : yaw;
            // the faster the run, the wider the turn
            float speed = hv.magnitude;
            float turn = faceLocked ? 720f : Mathf.Lerp(540f, 330f, Mathf.InverseLerp(2f, 6f, speed));
            float before = yaw;
            yaw = Mathf.MoveTowardsAngle(yaw, target, turn * dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (dt > 1e-5f)
            {
                float fwd = Vector3.Dot(hv, transform.forward);
                turnRate = U.Damp(turnRate, Mathf.DeltaAngle(before, yaw) / dt, 10f, dt);
                accel = U.Damp(accel, Mathf.Clamp((fwd - lastFwd) / dt, -16f, 16f), 8f, dt);
                lastFwd = fwd;
            }
            crouchAmount = Mathf.MoveTowards(crouchAmount, crouch ? crouchDepth : 0f, dt * 5f);
        }

        public void Jump()
        {
            if (!grounded || mount != null || pose != BodyKind.Stand || crouch) return;
            velocity.y = 5.4f;
            grounded = false;
            airTime = 0.13f;
        }

        public override void LateTick(float dt)
        {
            Camera cam = Game.I != null ? Game.I.cam : null;
            camDistance = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : 0f;
            if (dead)
            {
                if (Time.time - deadAt < 1.2f)
                {
                    anim.body = BodyKind.Dead;
                    if (mount == null) transform.rotation = Quaternion.Slerp(transform.rotation, deathRot, 1f - Mathf.Exp(-6f * dt));
                    anim.Tick(dt);
                }
                return;
            }
            if (camDistance > 160f && (frame & 7) != 0) return;

            Quaternion inv = Quaternion.Inverse(rig.root.rotation);
            anim.localVel = inv * new Vector3(velocity.x, 0f, velocity.z);
            anim.grounded = airTime < 0.12f;
            anim.crouch = crouchAmount;
            anim.body = knockedOut ? BodyKind.Dead : mount != null ? BodyKind.Ride : pose;
            anim.arms = ChooseArms();
            if (aiming) anim.aimLocal = inv * (aimPoint - Chest - Vector3.up * (0.2f * rig.k)).normalized;
            lookW = Mathf.MoveTowards(lookW, looking && !aiming ? Mathf.Clamp01(lookStrength) : 0f, dt * 3f);
            anim.lookWeight = lookW;
            anim.turnRate = mount == null ? turnRate : 0f;
            anim.accel = mount == null ? accel : 0f;
            if (looking) anim.lookLocal = inv * (lookPoint - HeadPos);
            if (workRate > 0f) { workPhase += dt * workRate; anim.workPhase = workPhase; } else anim.workPhase = -1f;
            if (mount != null)
            {
                anim.rideLean = mount.anim.riderLean;
                anim.rideBounce = mount.anim.riderBounce;
                anim.seatPitch = mount.anim.seatPitch;
            }
            else if (pose == BodyKind.Stand && camDistance < 30f && grounded)
            {
                float y0 = transform.position.y;
                Vector3 fwd = transform.forward * (anim.localVel.z * 0.06f);
                anim.footOffsetL = U.Damp(anim.footOffsetL, FootOffset(transform.TransformPoint(-0.1f, 0f, 0f) + fwd, y0), 14f, dt);
                anim.footOffsetR = U.Damp(anim.footOffsetR, FootOffset(transform.TransformPoint(0.1f, 0f, 0f) + fwd, y0), 14f, dt);
            }
            else { anim.footOffsetL = 0f; anim.footOffsetR = 0f; }

            anim.Tick(dt);
            if (anim.stepped && grounded && camDistance < 28f && mount == null)
            {
                Sfx.Step(transform.position, sprint ? 0.42f : 0.26f, 24f);
                if (sprint) Fx.Dust(transform.position + Vector3.up * 0.05f, 0.25f, new Color32(176, 152, 118, 110));
            }
        }

        static float FootOffset(Vector3 at, float rootY)
        {
            if (Physics.Raycast(at + Vector3.up * 0.6f, Vector3.down, out RaycastHit hit, 1.2f, Layers.SolidMask, QueryTriggerInteraction.Ignore))
                return Mathf.Clamp(hit.point.y - rootY, -0.3f, 0.3f);
            return 0f;
        }

        ArmKind ChooseArms()
        {
            if (handsUp) return ArmKind.HandsUp;
            if (workRate > 0f) return ArmKind.Work;
            if (weapon == null) return fistsUp ? ArmKind.Fists : mount != null ? ArmKind.Reins : ArmKind.Free;
            if (reloading) return ArmKind.Reload;
            switch (weapon.def.cls)
            {
                case WeaponClass.Knife: return fistsUp ? ArmKind.Fists : ArmKind.Pistol;
                case WeaponClass.Revolver: return aiming ? ArmKind.PistolAim : ArmKind.Pistol;
                case WeaponClass.Bow: return aiming ? ArmKind.BowAim : ArmKind.Free;
                default: return aiming ? ArmKind.RifleAim : ArmKind.Rifle;
            }
        }

        // ================================================================== riding and sitting

        public void Mount(Horse h, bool instant = false)
        {
            if (h == null || h.dead || h.rider != null || dead) return;
            if (pose != BodyKind.Stand) StandUp();
            mount = h;
            h.rider = this;
            crouch = false;
            if (cc != null) cc.enabled = false;
            transform.SetParent(h.rig.saddle, true);
            mountFromPos = transform.localPosition;
            mountFromRot = transform.localRotation;
            mountT = instant ? 1f : 0f;
            if (instant) { transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity; anim.Snap(); }
            h.OnMounted(this);
        }

        void TickMounted(float dt)
        {
            if (mountT < 1f)
            {
                mountT = Mathf.Min(1f, mountT + dt / 0.45f);
                float e = U.Smooth01(mountT);
                transform.localPosition = Vector3.Lerp(mountFromPos, Vector3.zero, e) + Vector3.up * (Mathf.Sin(Mathf.PI * e) * 0.25f);
                transform.localRotation = Quaternion.Slerp(mountFromRot, Quaternion.identity, e);
            }
            else
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            yaw = mount.yaw;
            velocity = mount.velocity;
            grounded = true;
            airTime = 0f;
        }

        public void Dismount()
        {
            if (mount == null) return;
            Horse h = mount;
            Vector3 left = h.transform.TransformPoint(new Vector3(-1.0f, 0f, 0.1f));
            Vector3 right = h.transform.TransformPoint(new Vector3(1.0f, 0f, 0.1f));
            Vector3 at = Physics.CheckSphere(left + Vector3.up * 1f, 0.3f, Layers.SolidMask & ~Layers.GroundMask) ? right : left;
            World.Ground(at, out float gy, out _, 3f, 8f);
            at.y = gy;
            transform.SetParent(null, true);
            mount = null;
            h.rider = null;
            h.OnDismounted(this);
            transform.position = at;
            yaw = h.yaw;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            velocity = Vector3.zero;
            if (cc != null && !dead) cc.enabled = true;
            mountT = 1f;
        }

        public void SitAt(Vector3 pos, float atYaw, bool onGround, float seatHeight = 0.46f)
        {
            if (mount != null) return;
            if (cc != null) cc.enabled = false;
            transform.position = pos;
            yaw = atYaw;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            pose = onGround ? BodyKind.SitGround : BodyKind.SitChair;
            anim.seatHeight = seatHeight;
            velocity = Vector3.zero;
            crouch = false;
        }

        public void Kneel() { if (mount == null && !dead) { pose = BodyKind.Kneel; velocity = Vector3.zero; } }

        public void StandUp()
        {
            if (pose == BodyKind.Stand) return;
            pose = BodyKind.Stand;
            if (cc != null && !dead) cc.enabled = true;
        }

        // ================================================================== weapons

        public Weapon Give(string weaponId)
        {
            WeaponDef d = WeaponDB.Get(weaponId);
            if (d == null) return null;
            foreach (Weapon have in weapons) if (have.def == d) return have;
            var w = new Weapon(d);
            weapons.Add(w);
            Stow(w);
            return w;
        }

        public Weapon Find(string weaponId)
        {
            foreach (Weapon w in weapons) if (w.def.id == weaponId) return w;
            return null;
        }

        public Weapon FindClass(bool longGun)
        {
            foreach (Weapon w in weapons) if (!w.def.Melee && w.def.Long == longGun && w.def.cls != WeaponClass.Bow) return w;
            return null;
        }

        public void Remove(Weapon w)
        {
            if (w == null) return;
            if (weapon == w) weapon = null;
            weapons.Remove(w);
            if (w.model != null) Destroy(w.model);
        }

        /// <summary>Takes a weapon into the hands (null = put everything away).</summary>
        public void Equip(Weapon w)
        {
            if (weapon == w) return;
            if (weapon != null) Stow(weapon);
            weapon = w;
            reloading = false;
            if (w == null) return;
            Transform grip = w.def.cls == WeaponClass.Bow ? rig.gripL : rig.gripR;
            w.model.transform.SetParent(grip, false);
            w.model.transform.localPosition = Vector3.zero;
            w.model.transform.localRotation = Quaternion.identity;
            w.model.transform.localScale = Vector3.one * rig.k;
            w.model.SetActive(true);
            nextShot = Mathf.Max(nextShot, Time.time + 0.25f);
        }

        void Stow(Weapon w)
        {
            Transform t = w.model.transform;
            t.localScale = Vector3.one * rig.k;
            if (w.def.cls == WeaponClass.Revolver)
            {
                t.SetParent(rig.holster, false);
                t.localPosition = new Vector3(0f, 0.02f, 0f);
                t.localRotation = Quaternion.Euler(80f, 0f, 0f);
                w.model.SetActive(true);
            }
            else if (w.def.Long)
            {
                t.SetParent(rig.back, false);
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.LookRotation(new Vector3(-0.32f, 0.95f, 0f), Vector3.back);
                bool otherShown = false;
                foreach (Weapon o in weapons) if (o != w && o != weapon && o.def.Long && o.model.activeSelf) otherShown = true;
                w.model.SetActive(!otherShown);
            }
            else
            {
                t.SetParent(rig.root, false);
                w.model.SetActive(false);
            }
        }

        /// <summary>Shoots at a world point. Returns true if a shot left the barrel.</summary>
        public bool Fire(Vector3 target, float spreadMul = 1f, float damageMul = 1f)
        {
            if (weapon == null || reloading || Time.time < nextShot || dead || knockedOut) return false;
            WeaponDef d = weapon.def;
            if (d.Melee) { Strike(); return true; }
            if (weapon.loaded <= 0)
            {
                Sfx.Play("click", transform.position, 0.6f, 1f, 12f);
                nextShot = Time.time + 0.35f;
                return false;
            }
            Vector3 muzzle = weapon.muzzle.position;
            Vector3 origin = HeadPos;
            Vector3 dir = (target - origin).normalized;
            float cond = weapon.Condition;
            float spread = d.spread * spreadMul / cond;
            Actor hitActor = null;
            for (int i = 0; i < d.pellets; i++)
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle * spread;
                Vector3 pd = Quaternion.LookRotation(dir) * Quaternion.Euler(r.y, r.x, 0f) * Vector3.forward;
                Actor a = Hitscan.Fire(this, mount, d, origin, pd, damageMul * cond, muzzle);
                if (a != null) hitActor = a;
            }
            LastHit = hitActor;
            weapon.loaded--;
            weapon.shotsSinceClean++;
            nextShot = Time.time + 1f / d.rate;
            anim.Recoil(d.recoil);
            if (d.cls != WeaponClass.Bow)
            {
                Fx.Muzzle(muzzle, dir, d.cls == WeaponClass.Shotgun || d.cls == WeaponClass.Rifle);
                string snd = d.cls == WeaponClass.Revolver ? "shot_revolver" : d.cls == WeaponClass.Shotgun ? "shot_shotgun" : d.cls == WeaponClass.Varmint ? "shot_small"
                           : d.cls == WeaponClass.Repeater ? "shot_repeater" : "shot_rifle";
                Sfx.Shot(snd, muzzle, 1f, Mathf.Max(60f, d.noise));
            }
            else Sfx.Play("bow", muzzle, 0.7f, 1f, 14f);
            World.MakeNoise(transform.position, d.noise, this, !d.Silent);
            ShotsFired++;
            return true;
        }

        public Actor LastHit;
        public int ShotsFired;

        /// <summary>Clears the delay between shots (marked shots in slow motion come as fast as the hand moves).</summary>
        public void ReadyToFire() { nextShot = 0f; }

        public void Reload()
        {
            if (weapon == null || reloading || weapon.def.Melee || weapon.loaded >= weapon.def.magazine) return;
            if (!infiniteAmmo && (ammoCount == null || ammoCount(weapon.def.ammo) <= 0)) return;
            reloading = true;
            float missing = (weapon.def.magazine - weapon.loaded) / (float)weapon.def.magazine;
            reloadEnd = Time.time + weapon.def.reload * (0.45f + 0.55f * missing);
            Sfx.Play("reload", transform.position, 0.5f, 1f, 12f);
        }

        void TickWeapon(float dt)
        {
            if (reloading && Time.time >= reloadEnd)
            {
                reloading = false;
                if (weapon != null)
                {
                    int need = weapon.def.magazine - weapon.loaded;
                    int take = infiniteAmmo ? need : Mathf.Min(need, ammoCount != null ? ammoCount(weapon.def.ammo) : 0);
                    weapon.loaded += take;
                    if (!infiniteAmmo) ammoUse?.Invoke(weapon.def.ammo, take);
                    Sfx.Play("click", transform.position, 0.5f, 0.9f, 10f);
                }
            }
            if (meleeAt > 0f && Time.time >= meleeAt)
            {
                meleeAt = -1f;
                ResolveMelee();
            }
        }

        /// <summary>Punch, or stab when a knife is in the hand.</summary>
        public void Strike()
        {
            if (Time.time < nextShot || dead || knockedOut || mount != null) return;
            punchLeft = !punchLeft;
            anim.Punch(weapon != null ? false : punchLeft);
            meleeAt = Time.time + 0.13f;
            nextShot = Time.time + (weapon != null && weapon.def.Melee ? 1f / weapon.def.rate : 0.42f);
        }

        void ResolveMelee()
        {
            Actor best = null;
            float bestD = 1.75f;
            Vector3 fwd = transform.forward;
            foreach (Actor a in World.actors)
            {
                if (a == this || a.dead || a == mount) continue;
                Vector3 to = a.transform.position - transform.position;
                to.y = 0f;
                float d = to.magnitude - a.radius;
                if (d > bestD || Vector3.Dot(to.normalized, fwd) < 0.45f) continue;
                best = a; bestD = d;
            }
            if (best == null) return;
            bool knife = weapon != null && weapon.def.Melee;
            best.Damage(new DamageInfo
            {
                amount = knife ? weapon.def.damage : UnityEngine.Random.Range(9f, 14f), attacker = this, point = best.Chest,
                dir = fwd, part = BodyPart.Torso, weapon = knife ? weapon.def : null, melee = true
            });
            Sfx.Play(knife ? "flesh" : "punch", best.Chest, 0.8f, 1f, 14f);
            if (knife) Fx.Blood(best.Chest, fwd);
            LastHit = best;
        }

        // ================================================================== damage

        protected override bool NonLethal(DamageInfo d) { return d.melee && d.weapon == null; }

        protected override void OnDamaged(DamageInfo d)
        {
            anim.Flinch(Quaternion.Inverse(transform.rotation) * d.dir);
            if (d.part == BodyPart.Head && rig.hat != null) KnockHatOff(d.dir);
            if (NonLethal(d) && health <= maxHealth * 0.22f && !knockedOut && !isPlayer) KnockOut(40f);
            brain?.OnDamaged(d);
        }

        public void KnockOut(float seconds)
        {
            if (dead || knockedOut) return;
            if (mount != null) Dismount();
            knockedOut = true;
            koUntil = Time.time + seconds;
            anim.deadVariant = 0;
            if (cc != null) cc.enabled = false;
            aiming = false; handsUp = false;
            velocity = Vector3.zero;
        }

        void WakeUp()
        {
            knockedOut = false;
            health = Mathf.Max(health, maxHealth * 0.35f);
            if (cc != null) cc.enabled = true;
        }

        void KnockHatOff(Vector3 dir)
        {
            GameObject hat = rig.hat;
            rig.hat = null;
            hat.transform.SetParent(null, true);
            hat.layer = Layers.Ragdoll;
            var col = hat.AddComponent<SphereCollider>();
            col.radius = 0.1f;
            var rb = hat.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.linearVelocity = dir * 3f + Vector3.up * 3.5f + UnityEngine.Random.insideUnitSphere;
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 8f;
            Destroy(hat, 90f);
        }

        protected override void OnDeath(DamageInfo d)
        {
            if (mount != null) Dismount();
            StandUp();
            anim.deadVariant = Vector3.Dot(d.dir, transform.forward) > 0.2f ? 1 : 0;
            if (cc != null) cc.enabled = false;
            aiming = false; handsUp = false; reloading = false; knockedOut = false;
            velocity = Vector3.zero;
            World.Ground(transform.position, out float gy, out Vector3 n, 1.5f, 6f);
            deathRot = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, n, 0.8f)) * Quaternion.Euler(0f, yaw, 0f);
            Vector3 p = transform.position; p.y = gy; transform.position = p;
            if (weapon != null && weapon.model != null && !isPlayer)
            {
                // the gun falls from the hand
                GameObject g = weapon.model;
                g.transform.SetParent(null, true);
                g.layer = Layers.Ragdoll;
                var col = g.AddComponent<BoxCollider>();
                col.size = new Vector3(0.05f, 0.08f, Mathf.Max(0.2f, weapon.def.muzzleZ));
                col.center = new Vector3(0f, 0f, weapon.def.muzzleZ * 0.4f);
                var rb = g.AddComponent<Rigidbody>();
                rb.mass = 1.5f;
                rb.linearVelocity = d.dir * 1.5f + Vector3.up;
                weapons.Remove(weapon);
                weapon = null;
            }
            brain?.OnDeath(d);
        }

        /// <summary>Brings a dead or unconscious person back on their feet somewhere else (the hero after dying).</summary>
        public void Revive(Vector3 pos, float atYaw)
        {
            dead = false; knockedOut = false; aiming = false; reloading = false; handsUp = false;
            pose = BodyKind.Stand;
            health = maxHealth;
            if (cc != null) cc.enabled = true;
            Teleport(pos, atYaw);
            anim.body = BodyKind.Stand;
            anim.Snap();
            lastHurt = -99f;
        }

        static readonly HB[,] Segments =
        {
            { HB.ArmL, HB.ForeL }, { HB.ForeL, HB.HandL }, { HB.ArmR, HB.ForeR }, { HB.ForeR, HB.HandR },
            { HB.ThighL, HB.ShinL }, { HB.ShinL, HB.FootL }, { HB.ThighR, HB.ShinR }, { HB.ShinR, HB.FootR }
        };
        static readonly float[] SegRadius = { 0.055f, 0.05f, 0.055f, 0.05f, 0.085f, 0.062f, 0.085f, 0.062f };
        static readonly BodyPart[] SegPart = { BodyPart.ArmL, BodyPart.ArmL, BodyPart.ArmR, BodyPart.ArmR, BodyPart.LegL, BodyPart.LegL, BodyPart.LegR, BodyPart.LegR };

        public override bool RayHit(Vector3 origin, Vector3 dir, float maxDist, out BodyHit hit)
        {
            hit = default;
            float k = rig.k;
            Vector3 centre = rig.B(HB.Spine).position;
            if (!Geo.RaySphere(origin, dir, centre, 1.25f * k, out _)) return false;
            float best = maxDist;
            bool any = false;
            BodyPart part = BodyPart.Torso;
            Transform head = rig.B(HB.Head);
            if (Geo.RaySphere(origin, dir, head.position + head.up * (0.105f * k), 0.125f * k, out float t) && t < best) { best = t; part = BodyPart.Head; any = true; }
            if (Geo.RayCapsule(origin, dir, rig.B(HB.Hips).position, rig.B(HB.Neck).position, 0.165f * k * Mathf.Max(0.9f, spec.build), out t) && t < best)
            {
                best = t; any = true;
                Transform chest = rig.B(HB.Chest);
                Vector3 heart = chest.TransformPoint(new Vector3(-0.03f, 0.07f, 0.03f) * k);
                part = Geo.RaySphere(origin, dir, heart, 0.075f * k, out _) ? BodyPart.Heart : BodyPart.Torso;
            }
            for (int i = 0; i < 8; i++)
            {
                if (Geo.RayCapsule(origin, dir, rig.B(Segments[i, 0]).position, rig.B(Segments[i, 1]).position, SegRadius[i] * k, out t) && t < best)
                { best = t; part = SegPart[i]; any = true; }
            }
            if (!any) return false;
            hit = new BodyHit { dist = best, part = part, point = origin + dir * best };
            return true;
        }
    }
}
