using System;
using UnityEngine;

namespace Canaan
{
    /// <summary>
    /// Brain of anyone who fights with a gun: outlaws, lawmen, the camp's escort. Finds cover, pops out to
    /// shoot in bursts, reloads behind it, backs off or runs when badly hurt.
    /// </summary>
    public class Gunman : Brain
    {
        public float skill = 0.5f;               // 0 clumsy .. 1 deadly
        public float sightRange = 70f;
        public Actor target;
        public Vector3 post;
        public bool hasPost;
        public bool fleeing, surrendered, holdFire;
        public Func<Actor, bool> isEnemy;          // extra enemies beyond the faction table
        public Action<Gunman> idle;                // what to do with no target (patrol, routine)
        public readonly Mover mover;
        public bool Fighting => target != null && !target.dead;

        float thinkAt, coverCheckAt, shootAt, hideUntil, peekUntil, lostAt, fleeUntil, calmAt;
        int burst = 2, strafeSide = 1;
        Vector3 coverPos, lastSeen;
        bool hasCover, sees;

        public Gunman(Human h, float skill = 0.5f)
        {
            self = h;
            this.skill = skill;
            mover = new Mover(h);
            thinkAt = Time.time + UnityEngine.Random.value * 0.3f;
        }

        public override void OnDamaged(DamageInfo d)
        {
            if (d.attacker != null && d.attacker != self && (target == null || target.dead))
            {
                target = d.attacker;
                lastSeen = target.Pos;
                lostAt = Time.time + 12f;
                shootAt = Time.time + 0.4f;
            }
            if (self.health < self.maxHealth * 0.28f && UnityEngine.Random.value < 0.45f * (1.1f - skill) && !fleeing)
            {
                fleeing = true;
                fleeUntil = Time.time + 14f;
            }
        }

        bool Enemy(Actor a)
        {
            if (a == self || a.dead) return false;
            if (a is Human h && h.knockedOut) return false;
            return Factions.Hostile(self, a) || (isEnemy != null && isEnemy(a));
        }

        void Think()
        {
            Actor best = target != null && !target.dead && Enemy(target) ? target : null;
            float bestD = best != null ? Vector3.Distance(self.Pos, best.Pos) * 0.8f : sightRange;
            foreach (Actor a in World.actors)
            {
                if (!Enemy(a)) continue;
                float d = Vector3.Distance(self.Pos, a.Pos);
                if (d >= bestD) continue;
                if (d < 7f || Factions.CanSee(self, a, sightRange)) { best = a; bestD = d; }
            }
            if (best != target)
            {
                target = best;
                if (target != null) { shootAt = Time.time + Mathf.Lerp(1.0f, 0.35f, skill); lostAt = Time.time + 8f; lastSeen = target.Pos; }
                hasCover = false;
            }
            sees = target != null && Factions.CanSee(self, target, sightRange * 1.2f, 180f);
            if (sees) { lastSeen = target.Pos; lostAt = Time.time + 8f; }
            else if (target != null && Time.time > lostAt) target = null;
            if (fleeing && Time.time > fleeUntil) fleeing = false;
        }

        public override void Tick(float dt)
        {
            if (surrendered)
            {
                self.handsUp = true; self.aiming = false; self.moveInput = Vector3.zero; self.faceLocked = false;
                return;
            }
            self.handsUp = false;
            if (Time.time >= thinkAt) { thinkAt = Time.time + 0.3f; Think(); }

            if (target == null || target.dead)
            {
                self.aiming = false; self.sprint = false; self.crouch = false; self.faceLocked = false;
                hasCover = false;
                if (self.weapon != null && Time.time > calmAt) self.Equip(null);
                if (idle != null) idle(this);
                else if (hasPost) { self.walk = true; mover.Go(post, 0.8f); mover.Tick(dt); }
                else self.moveInput = Vector3.zero;
                return;
            }
            calmAt = Time.time + 8f;
            self.walk = false;
            Vector3 pos = self.Pos, tp = target.Chest;
            float d = Vector3.Distance(pos, target.Pos);

            if (fleeing)
            {
                self.aiming = false; self.faceLocked = false; self.crouch = false; self.sprint = true;
                mover.Go(pos + U.FlatDir(pos - target.Pos) * 20f, 1f);
                mover.Tick(dt);
                return;
            }

            if (self.weapon == null || self.weapon.def.Melee)
            {
                Weapon w = d > 20f ? self.FindClass(true) ?? self.FindClass(false) : self.FindClass(false) ?? self.FindClass(true);
                if (w != null) self.Equip(w);
                else { fleeing = true; fleeUntil = Time.time + 20f; return; }
            }
            WeaponDef def = self.weapon.def;
            float ideal = def.cls == WeaponClass.Revolver ? 14f : def.cls == WeaponClass.Shotgun ? 9f : 30f;

            self.faceLocked = true;
            self.faceYaw = U.Yaw(tp - pos);
            if (self.weapon.loaded == 0 && !self.reloading) { self.Reload(); hideUntil = Time.time + def.reload + 0.3f; }

            if (hasCover)
            {
                float toCover = U.FlatDist(pos, coverPos);
                if (toCover > 0.9f)
                {
                    self.sprint = toCover > 4f; self.crouch = false; self.aiming = false;
                    mover.Go(coverPos, 0.5f);
                    mover.Tick(dt);
                }
                else
                {
                    mover.Stop();
                    self.sprint = false;
                    bool hide = Time.time < hideUntil || self.reloading;
                    self.crouch = hide;
                    self.aiming = !hide && sees;
                    if (!hide && sees) Shoot(d, def, tp);
                    if (!hide && Time.time > peekUntil)
                    {
                        hideUntil = Time.time + UnityEngine.Random.Range(0.9f, 2.4f);
                        peekUntil = hideUntil + UnityEngine.Random.Range(1.4f, 2.8f);
                    }
                    // flanked or the fight moved on: the cover no longer covers
                    if (Time.time >= coverCheckAt)
                    {
                        coverCheckAt = Time.time + 1.5f;
                        if (World.LineOfSight(coverPos + Vector3.up * 0.9f, tp) || d > ideal * 2.2f) hasCover = false;
                    }
                }
                return;
            }

            // in the open: close to a good range, sidestep, shoot, and keep looking for cover
            self.crouch = false;
            self.aiming = sees && !self.reloading;
            if (!sees || d > ideal * 1.35f)
            {
                self.sprint = d > ideal * 2f;
                mover.Go(sees ? target.Pos : lastSeen, ideal * 0.8f);
                mover.Tick(dt);
            }
            else if (d < ideal * 0.45f)
            {
                mover.Stop();
                self.sprint = false;
                self.moveInput = U.FlatDir(pos - target.Pos);
            }
            else
            {
                mover.Stop();
                self.sprint = false;
                if (Time.time > peekUntil) { strafeSide = -strafeSide; peekUntil = Time.time + UnityEngine.Random.Range(1.2f, 3f); }
                self.moveInput = Vector3.Cross(Vector3.up, U.FlatDir(tp - pos)) * (strafeSide * 0.45f);
            }
            if (sees) Shoot(d, def, tp);
            if (Time.time >= coverCheckAt)
            {
                coverCheckAt = Time.time + 2f;
                FindCover(tp, ideal);
            }
        }

        void Shoot(float d, WeaponDef def, Vector3 tp)
        {
            if (holdFire || Time.time < shootAt || !self.CanFire || self.weapon.loaded <= 0) return;
            float pHit = Mathf.Lerp(0.16f, 0.6f, skill) * Mathf.Clamp(1.25f - d / def.range, 0.2f, 1f);
            if (target.HorizontalSpeed > 3f) pHit *= 0.6f;
            if (target is Human th)
            {
                if (th.crouch) pHit *= 0.7f;
                if (th.Mounted) pHit *= 0.75f;
            }
            Vector3 aim = tp;
            if (!World.LineOfSight(self.HeadPos, tp)) { aim = target.HeadPos; pHit *= 0.4f; }
            if (UnityEngine.Random.value > pHit)
            {
                Vector3 dir = (aim - self.HeadPos).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                aim += right * (UnityEngine.Random.Range(0.55f, 1.6f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f)) + Vector3.up * UnityEngine.Random.Range(-0.5f, 0.9f);
            }
            self.aimPoint = aim;
            if (!self.Fire(aim, 0.25f)) return;
            burst--;
            shootAt = Time.time + (1f / def.rate) * UnityEngine.Random.Range(1.15f, 1.9f);
            if (burst <= 0)
            {
                burst = UnityEngine.Random.Range(1, 4);
                shootAt += UnityEngine.Random.Range(0.5f, 1.5f) / (0.5f + skill);
            }
        }

        void FindCover(Vector3 tp, float ideal)
        {
            Vector3 pos = self.Pos;
            float best = -1e9f;
            bool found = false;
            Vector3 bestP = pos;
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f + UnityEngine.Random.Range(-12f, 12f);
                float r = UnityEngine.Random.Range(2.5f, 11f);
                Vector3 p = pos + U.Dir(a) * r;
                if (!World.Ground(p, out float gy, out _, 2f, 4f) || Mathf.Abs(gy - pos.y) > 1.5f) continue;
                p.y = gy;
                float dt = U.FlatDist(p, tp);
                if (dt < 6f) continue;
                if (World.LineOfSight(p + Vector3.up * 0.9f, tp)) continue;           // must hide a crouching man
                if (!World.LineOfSight(p + Vector3.up * 1.6f, tp)) continue;          // and let a standing one shoot over
                if (Physics.CheckSphere(p + Vector3.up * 0.9f, 0.3f, 1 << Layers.Static, QueryTriggerInteraction.Ignore)) continue;
                if (!World.LineOfSight(pos + Vector3.up * 0.9f, p + Vector3.up * 0.9f)) continue;   // walkable in a straight line
                float score = -r * 0.5f - Mathf.Abs(dt - ideal) * 0.15f;
                if (score > best) { best = score; bestP = p; found = true; }
            }
            if (!found) return;
            hasCover = true;
            coverPos = bestP;
            hideUntil = 0f;
            peekUntil = Time.time + UnityEngine.Random.Range(1.5f, 3f);
            coverCheckAt = Time.time + 1.5f;
        }
    }
}
